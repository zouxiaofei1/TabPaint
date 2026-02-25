using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TabPaint.Services;

namespace TabPaint
{
    internal sealed class PythonRuntimeManager
    {
        internal enum PyRuntimeStage
        {
            DownloadPython,
            ExtractPython,
            InstallPip
        }

        internal sealed class PyRuntimeProgressStatus
        {
            public PyRuntimeStage Stage { get; set; }
            public double Percentage { get; set; }
            public string? LeftText { get; set; }
            public string? RightText { get; set; }
        }

        private readonly SemaphoreSlim _initLock = new(1, 1);
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };

        public string PythonExePath => Path.Combine(AppConsts.PyOcrRuntimeDir, "python.exe");
        public string RequirementsPath => Path.Combine(AppContext.BaseDirectory, "pyocr", "requirements.txt");

        public static bool IsRuntimeInstalled()
        {
            string markerPath = Path.Combine(AppConsts.PyOcrRootDir, "runtime.ready");
            string pythonExePath = Path.Combine(AppConsts.PyOcrRuntimeDir, "python.exe");
            return File.Exists(markerPath) && File.Exists(pythonExePath);
        }

        public static long GetInstalledSizeBytes()
        {
            if (!Directory.Exists(AppConsts.PyOcrRootDir)) return 0;

            long total = 0;
            foreach (var file in Directory.GetFiles(AppConsts.PyOcrRootDir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // ignore single file access failures to keep UI responsive
                }
            }

            return total;
        }

        public static void UninstallRuntime()
        {
            if (!Directory.Exists(AppConsts.PyOcrRootDir)) return;
            Directory.Delete(AppConsts.PyOcrRootDir, recursive: true);
        }

        public async Task EnsureReadyAsync(IProgress<PyRuntimeProgressStatus>? progress = null, CancellationToken cancellationToken = default)
        {
            await _initLock.WaitAsync(cancellationToken);
            try
            {
                bool didRuntimeWork = false;

                Directory.CreateDirectory(AppConsts.PyOcrRootDir);
                Directory.CreateDirectory(AppConsts.PyOcrRuntimeDir);
                Directory.CreateDirectory(AppConsts.PyOcrDownloadsDir);
                Directory.CreateDirectory(AppConsts.PyOcrModelsDir);
                Directory.CreateDirectory(AppConsts.PyOcrLogsDir);

                if (!File.Exists(PythonExePath))
                {
                    didRuntimeWork = true;
                    await DownloadAndExtractPythonAsync(progress, cancellationToken);
                }

                EnsureImportSiteEnabled();

                string markerPath = Path.Combine(AppConsts.PyOcrRootDir, "runtime.ready");
                if (!File.Exists(markerPath))
                {
                    didRuntimeWork = true;
                    await InstallPipAndPackagesAsync(progress, cancellationToken);
                    File.WriteAllText(markerPath, DateTime.Now.ToString("O"), Encoding.UTF8);
                }

                if (didRuntimeWork)
                {
                    progress?.Report(new PyRuntimeProgressStatus
                    {
                        Stage = PyRuntimeStage.InstallPip,
                        Percentage = 100,
                        LeftText = "",
                        RightText = ""
                    });
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        private static double MapToGlobalPercent(double stageStart, double stageEnd, double localPercent)
        {
            double p = Math.Max(0, Math.Min(100, localPercent));
            return stageStart + (stageEnd - stageStart) * (p / 100.0);
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = Math.Max(0, bytes);
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "";
            return $"{FormatFileSize((long)bytesPerSecond)}/s";
        }

        private async Task DownloadAndExtractPythonAsync(IProgress<PyRuntimeProgressStatus>? progress, CancellationToken cancellationToken)
        {
            string zipPath = Path.Combine(AppConsts.PyOcrDownloadsDir, AppConsts.PyOcrPythonEmbedZipName);
            if (!File.Exists(zipPath))
            {
                progress?.Report(new PyRuntimeProgressStatus
                {
                    Stage = PyRuntimeStage.DownloadPython,
                    Percentage = 0,
                    LeftText = "0 / ?",
                    RightText = ""
                });

                if (!await DownloadFileAsync(
                        AppConsts.PyOcrPythonDownloadUrlTuna,
                        zipPath,
                        cancellationToken,
                        (received, total, speed) =>
                        {
                            string totalText = total > 0 ? FormatFileSize(total) : "?";
                            progress?.Report(new PyRuntimeProgressStatus
                            {
                                Stage = PyRuntimeStage.DownloadPython,
                                Percentage = MapToGlobalPercent(0, 60, total > 0 ? (received * 100.0 / total) : 0),
                                LeftText = $"{FormatFileSize(received)} / {totalText}",
                                RightText = FormatSpeed(speed)
                            });
                        }))
                {
                    bool ok = await DownloadFileAsync(
                        AppConsts.PyOcrPythonDownloadUrlOfficial,
                        zipPath,
                        cancellationToken,
                        (received, total, speed) =>
                        {
                            string totalText = total > 0 ? FormatFileSize(total) : "?";
                            progress?.Report(new PyRuntimeProgressStatus
                            {
                                Stage = PyRuntimeStage.DownloadPython,
                                Percentage = MapToGlobalPercent(0, 60, total > 0 ? (received * 100.0 / total) : 0),
                                LeftText = $"{FormatFileSize(received)} / {totalText}",
                                RightText = FormatSpeed(speed)
                            });
                        });
                    if (!ok) throw new Exception("Python runtime download failed.");
                }

                progress?.Report(new PyRuntimeProgressStatus
                {
                    Stage = PyRuntimeStage.DownloadPython,
                    Percentage = 60,
                    LeftText = "",
                    RightText = ""
                });
            }
            else
            {
                progress?.Report(new PyRuntimeProgressStatus
                {
                    Stage = PyRuntimeStage.DownloadPython,
                    Percentage = 60,
                    LeftText = "",
                    RightText = ""
                });
            }

            string extractingFlag = Path.Combine(AppConsts.PyOcrRuntimeDir, ".extracting");
            File.WriteAllText(extractingFlag, "1");
            try
            {
                foreach (var file in Directory.GetFiles(AppConsts.PyOcrRuntimeDir, "*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
                foreach (var dir in Directory.GetDirectories(AppConsts.PyOcrRuntimeDir))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }

                using var archive = ZipFile.OpenRead(zipPath);
                int totalEntries = Math.Max(1, archive.Entries.Count);
                int finishedEntries = 0;

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string targetPath = Path.GetFullPath(Path.Combine(AppConsts.PyOcrRuntimeDir, entry.FullName));
                    if (!targetPath.StartsWith(Path.GetFullPath(AppConsts.PyOcrRuntimeDir), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("Zip entry path is invalid.");
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(targetPath);
                    }
                    else
                    {
                        string? dirPath = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(dirPath))
                        {
                            Directory.CreateDirectory(dirPath);
                        }
                        entry.ExtractToFile(targetPath, overwrite: true);
                    }

                    finishedEntries++;
                    double local = finishedEntries * 100.0 / totalEntries;
                    progress?.Report(new PyRuntimeProgressStatus
                    {
                        Stage = PyRuntimeStage.ExtractPython,
                        Percentage = MapToGlobalPercent(60, 80, local),
                        LeftText = $"{finishedEntries} / {totalEntries}",
                        RightText = ""
                    });
                }
            }
            finally
            {
                try { File.Delete(extractingFlag); } catch { }
            }
        }

        private void EnsureImportSiteEnabled()
        {
            string pthPath = Path.Combine(AppConsts.PyOcrRuntimeDir, "python311._pth");
            if (!File.Exists(pthPath)) return;

            var lines = File.ReadAllLines(pthPath);
            bool hasImportSite = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed == "import site") hasImportSite = true;
                if (trimmed == "#import site")
                {
                    lines[i] = "import site";
                    hasImportSite = true;
                }
            }

            if (!hasImportSite)
            {
                var list = new System.Collections.Generic.List<string>(lines) { "import site" };
                lines = list.ToArray();
            }

            File.WriteAllLines(pthPath, lines, Encoding.UTF8);
        }

        private async Task InstallPipAndPackagesAsync(IProgress<PyRuntimeProgressStatus>? progress, CancellationToken cancellationToken)
        {
            progress?.Report(new PyRuntimeProgressStatus
            {
                Stage = PyRuntimeStage.InstallPip,
                Percentage = 80,
                LeftText = "",
                RightText = ""
            });

            string getPipPath = Path.Combine(AppConsts.PyOcrDownloadsDir, "get-pip.py");
            if (!File.Exists(getPipPath))
            {
                bool ok = await DownloadFileAsync(AppConsts.PyOcrGetPipUrl, getPipPath, cancellationToken);
                if (!ok) throw new Exception("get-pip.py download failed.");
            }

            progress?.Report(new PyRuntimeProgressStatus
            {
                Stage = PyRuntimeStage.InstallPip,
                Percentage = 84,
                LeftText = "get-pip.py",
                RightText = ""
            });

            await RunPythonAsync($"\"{getPipPath}\"", cancellationToken);

            progress?.Report(new PyRuntimeProgressStatus
            {
                Stage = PyRuntimeStage.InstallPip,
                Percentage = 90,
                LeftText = "pip",
                RightText = ""
            });

            if (!File.Exists(RequirementsPath))
            {
                throw new FileNotFoundException("Missing pyocr requirements.txt", RequirementsPath);
            }

            await RunPythonAsync($"-m pip install -i {AppConsts.PyOcrPipMirror} --trusted-host pypi.tuna.tsinghua.edu.cn --upgrade pip", cancellationToken);
            progress?.Report(new PyRuntimeProgressStatus
            {
                Stage = PyRuntimeStage.InstallPip,
                Percentage = 95,
                LeftText = "pip upgrade",
                RightText = ""
            });

            await RunPythonAsync($"-m pip install -i {AppConsts.PyOcrPipMirror} --trusted-host pypi.tuna.tsinghua.edu.cn -r \"{RequirementsPath}\"", cancellationToken);

            progress?.Report(new PyRuntimeProgressStatus
            {
                Stage = PyRuntimeStage.InstallPip,
                Percentage = 100,
                LeftText = "requirements.txt",
                RightText = ""
            });
        }

        private async Task RunPythonAsync(string arguments, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = PythonExePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = AppConsts.PyOcrRootDir
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            Task<string> outTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errTask = process.StandardError.ReadToEndAsync();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(AppConsts.PyOcrProcessTimeoutSeconds));

            await process.WaitForExitAsync(timeoutCts.Token);
            string stdout = await outTask;
            string stderr = await errTask;

            if (process.ExitCode != 0)
            {
                Logger.Error($"[PyOCR] Python command failed: {arguments}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
                throw new Exception(string.IsNullOrWhiteSpace(stderr) ? "Python command failed." : stderr.Trim());
            }
        }

        private static async Task<bool> DownloadFileAsync(
            string url,
            string destinationPath,
            CancellationToken cancellationToken,
            Action<long, long, double>? progress = null)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode) return false;

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                long receivedBytes = 0;
                var sw = Stopwatch.StartNew();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                while (true)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read <= 0) break;

                    await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    receivedBytes += read;
                    double speed = sw.Elapsed.TotalSeconds > 0 ? receivedBytes / sw.Elapsed.TotalSeconds : 0;
                    progress?.Invoke(receivedBytes, totalBytes, speed);
                }

                progress?.Invoke(receivedBytes, totalBytes, sw.Elapsed.TotalSeconds > 0 ? receivedBytes / sw.Elapsed.TotalSeconds : 0);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}