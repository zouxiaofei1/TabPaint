using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabPaint.Services;

namespace TabPaint
{
    internal sealed class PaddleOcrClient : IDisposable
    {
        public static class a
        {
            public static void s(params object[] args)
            {
                string message = string.Join(" ", args);
                Debug.WriteLine(message);
            }
        }
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly PythonRuntimeManager _runtimeManager = new PythonRuntimeManager();
        private readonly SemaphoreSlim _requestLock = new(1, 1);
        private Process? _process;
        private StreamWriter? _stdin;
        private StreamReader? _stdout;

        private static string GetStageTitle(PythonRuntimeManager.PyRuntimeStage stage)
        {
            return stage switch
            {
                PythonRuntimeManager.PyRuntimeStage.DownloadPython => LocalizationManager.GetString("L_PyOCR_Runtime_DownloadPython"),
                PythonRuntimeManager.PyRuntimeStage.ExtractPython => LocalizationManager.GetString("L_PyOCR_Runtime_ExtractPython"),
                PythonRuntimeManager.PyRuntimeStage.InstallPip => LocalizationManager.GetString("L_PyOCR_Runtime_InstallPip"),
                _ => LocalizationManager.GetString("L_PyOCR_Runtime_Preparing")
            };
        }

        private static void UpdateRuntimeProgressUi(PythonRuntimeManager.PyRuntimeProgressStatus status)
        {
            var app = Application.Current;
            if (app == null) return;

            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                var mw = MainWindow.GetCurrentInstance();
                if (mw == null) return;
                mw.TaskProgressPopup.SetIcon(AppConsts.PathTaskProgress);
                mw.TaskProgressPopup.UpdateProgress(
                    status.Percentage,
                    GetStageTitle(status.Stage),
                    status.LeftText ?? string.Empty,
                    status.RightText ?? string.Empty);
            }));
        }

        private static void FinishRuntimeProgressUi()
        {
            var app = Application.Current;
            if (app == null) return;

            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                var mw = MainWindow.GetCurrentInstance();
                mw?.TaskProgressPopup.Finish();
            }));
        }

        public async Task<OcrRecognizeResult?> RecognizeDetailedAsync(BitmapSource source, CancellationToken cancellationToken = default)
        {
            await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await EnsureProcessAsync(cancellationToken).ConfigureAwait(false);
                Logger.Info("EnsureProcessAsync finished!");
                string requestId = Guid.NewGuid().ToString("N");
                string imageBase64 = EncodeBitmapToBase64(source);
                Logger.Info("EncodeBitmapToBase64 finished!");
                string reqJson = JsonSerializer.Serialize(new OcrRequest
                {
                    Id = requestId,
                    ImageBase64 = imageBase64
                });
                //  a.s(source);
                Logger.Info(imageBase64.Length.ToString());
                if (_stdin == null || _stdout == null)
                {
                    throw new Exception("PaddleOCR process is not available.");
                }

                await _stdin.WriteLineAsync(reqJson).ConfigureAwait(false);
                await _stdin.FlushAsync().ConfigureAwait(false);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(AppConsts.PyOcrRecognizeTimeoutSeconds));

                while (true)
                {
                    string? line = await _stdout.ReadLineAsync().WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                    if (line == null)
                    {
                        throw new Exception("PaddleOCR process exited unexpectedly.");
                    }
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    OcrResponse? response;
                    try
                    {
                        response = JsonSerializer.Deserialize<OcrResponse>(line, JsonOptions);
                    }
                    catch (JsonException)
                    {
                        Logger.Info($"[PyOCR][stdout] {line}");
                        continue;
                    }
                    if (response == null) continue;

                    if (string.Equals(response.Event, "log", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!string.Equals(response.Id, requestId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!response.Ok)
                    {
                        throw new Exception(string.IsNullOrWhiteSpace(response.Error) ? "PaddleOCR failed." : response.Error);
                    }

                    string fullText = string.IsNullOrWhiteSpace(response.Text) ? string.Empty : response.Text.Trim();
                    var lines = new List<OcrLineResult>();
                    if (response.Lines != null)
                    {
                        foreach (var lineItem in response.Lines)
                        {
                            if (lineItem == null || string.IsNullOrWhiteSpace(lineItem.Text)) continue;

                            Rect? rect = null;
                            if (lineItem.Box != null && lineItem.Box.Length >= 4)
                            {
                                double x1 = lineItem.Box[0];
                                double y1 = lineItem.Box[1];
                                double x2 = lineItem.Box[2];
                                double y2 = lineItem.Box[3];
                                double left = Math.Min(x1, x2);
                                double top = Math.Min(y1, y2);
                                double right = Math.Max(x1, x2);
                                double bottom = Math.Max(y1, y2);
                                if (right > left && bottom > top)
                                {
                                    rect = new Rect(left, top, right - left, bottom - top);
                                }
                            }

                            lines.Add(new OcrLineResult(lineItem.Text.Trim(), rect));
                        }
                    }

                    if (string.IsNullOrWhiteSpace(fullText) && lines.Count > 0)
                    {
                        fullText = string.Join("\n", lines.Select(l => l.Text));
                    }

                    if (string.IsNullOrWhiteSpace(fullText) && lines.Count == 0) return null;
                    return new OcrRecognizeResult(fullText, lines);
                }
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public async Task<string?> RecognizeAsync(BitmapSource source, CancellationToken cancellationToken = default)
        {
            var result = await RecognizeDetailedAsync(source, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(result?.FullText) ? null : result.FullText;
        }

        private async Task EnsureProcessAsync(CancellationToken cancellationToken)
        {
            if (_process != null && !_process.HasExited && _stdin != null && _stdout != null)
            {
                return;
            }

            DisposeProcess();

            bool runtimeProgressShown = false;
            var runtimeProgress = new Progress<PythonRuntimeManager.PyRuntimeProgressStatus>(status =>
            {
                runtimeProgressShown = true;
                UpdateRuntimeProgressUi(status);
            });

            try
            {
                await _runtimeManager.EnsureReadyAsync(runtimeProgress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (runtimeProgressShown)
                {
                    FinishRuntimeProgressUi();
                }
            }

            string serverScriptPath = System.IO.Path.Combine(AppContext.BaseDirectory, "pyocr", "ocr_server.py");
            if (!File.Exists(serverScriptPath))
            {
                throw new FileNotFoundException("Missing OCR server script.", serverScriptPath);
            }

            var psi = new ProcessStartInfo
            {
                FileName = _runtimeManager.PythonExePath,
                Arguments = $"\"{serverScriptPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(serverScriptPath) ?? AppContext.BaseDirectory
            };
            psi.Environment["PADDLEOCR_HOME"] = AppConsts.PyOcrModelsDir;
            psi.Environment["PYTHONUNBUFFERED"] = "1";
            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Logger.Info($"[PyOCR][stderr] {e.Data}");
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            a.s("processstart");
            _stdin = process.StandardInput;
            _stdout = process.StandardOutput;
            _process = process;
           
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(AppConsts.PyOcrProcessTimeoutSeconds));
 a.s("processready");
            while (true)
            {
                string? line = await _stdout.ReadLineAsync().WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                a.s("line:",line);
                if (line == null)
                {
                    throw new Exception("PaddleOCR process exited before ready event.");
                }
                if (string.IsNullOrWhiteSpace(line)) continue;
             
                OcrResponse? response;
                try
                {
                    response = JsonSerializer.Deserialize<OcrResponse>(line, JsonOptions);
                }
                catch (JsonException)
                {
                    Logger.Info($"[PyOCR][stdout] {line}");
                    continue;
                }
                if (response == null) continue;
                if (string.Equals(response.Event, "ready", StringComparison.OrdinalIgnoreCase))
                {
                    if (!response.Ok)
                    {
                        throw new Exception(string.IsNullOrWhiteSpace(response.Error) ? "PaddleOCR init failed." : response.Error);
                    }
                    return;
                }
            }
        }

        private static string EncodeBitmapToBase64(BitmapSource source)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return Convert.ToBase64String(ms.ToArray());
        }

        private void DisposeProcess()
        {
            try { _stdin?.Dispose(); } catch { }
            try { _stdout?.Dispose(); } catch { }
            _stdin = null;
            _stdout = null;

            if (_process == null) return;
            try
            {
                if (!_process.HasExited) _process.Kill(true);
            }
            catch { }
            try { _process.Dispose(); } catch { }
            _process = null;
        }

        public void Dispose()
        {
            DisposeProcess();
            _requestLock.Dispose();
        }

        private sealed class OcrRequest
        {
            public string Id { get; set; } = string.Empty;
            public string ImageBase64 { get; set; } = string.Empty;
        }

        private sealed class OcrResponse
        {
            public string? Event { get; set; }
            public string? Id { get; set; }
            public bool Ok { get; set; }
            public string? Text { get; set; }
            public List<OcrLineResponse>? Lines { get; set; }
            public string? Error { get; set; }
        }

        private sealed class OcrLineResponse
        {
            public string? Text { get; set; }
            public double[]? Box { get; set; }
        }
    }
}