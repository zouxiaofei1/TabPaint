using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace TabPaint.Services
{
    /// <summary>
    /// 启动性能跟踪器：记录方法级耗时（聚合统计 + 时间线）。
    /// </summary>
    public static class StartupPerformanceTracer
    {
        private sealed class ScopeStat
        {
            public long Calls;
            public long TotalTicks;
            public long MaxTicks;
        }

        private sealed class TimelineEvent
        {
            public string Name { get; set; } = string.Empty;
            public long StartTicks { get; set; }
            public long DurationTicks { get; set; }
            public int ThreadId { get; set; }
            public bool IsPoint { get; set; }
        }

        private sealed class ScopeToken : IDisposable
        {
            private readonly string _name;
            private readonly long _startTicks;
            private readonly int _threadId;
            private bool _disposed;

            public ScopeToken(string name)
            {
                _name = name;
                _startTicks = _sessionStopwatch.ElapsedTicks;
                _threadId = Environment.CurrentManagedThreadId;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                long endTicks = _sessionStopwatch.ElapsedTicks;
                long duration = Math.Max(0, endTicks - _startTicks);

                var stat = _stats.GetOrAdd(_name, _ => new ScopeStat());
                lock (stat)
                {
                    stat.Calls++;
                    stat.TotalTicks += duration;
                    if (duration > stat.MaxTicks) stat.MaxTicks = duration;
                }

                lock (_timelineLock)
                {
                    _timeline.Add(new TimelineEvent
                    {
                        Name = _name,
                        StartTicks = _startTicks,
                        DurationTicks = duration,
                        ThreadId = _threadId,
                        IsPoint = false
                    });
                }
            }
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();
            public void Dispose() { }
        }

        private static readonly object _lifecycleLock = new object();
        private static readonly object _timelineLock = new object();
        private static readonly Stopwatch _sessionStopwatch = new Stopwatch();
        private static readonly ConcurrentDictionary<string, ScopeStat> _stats = new ConcurrentDictionary<string, ScopeStat>();
        private static readonly List<TimelineEvent> _timeline = new List<TimelineEvent>();

        private static bool _started;
        private static string _sessionName = "Startup";
        private static DateTime _sessionStartUtc;

        private static readonly string TraceDirectory = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TabPaint",
            "StartupTrace");

        public static void StartSession(string sessionName = "Startup")
        {
            lock (_lifecycleLock)
            {
                _sessionName = string.IsNullOrWhiteSpace(sessionName) ? "Startup" : sessionName.Trim();
                _sessionStartUtc = DateTime.UtcNow;

                _stats.Clear();
                lock (_timelineLock)
                {
                    _timeline.Clear();
                }

                _sessionStopwatch.Restart();
                _started = true;
                Point("Session.Start");
            }
        }

        public static IDisposable Measure(string name)
        {
            if (!_started || string.IsNullOrWhiteSpace(name)) return NoopDisposable.Instance;
            return new ScopeToken(name.Trim());
        }

        public static void Point(string name)
        {
            return;
            if (!_started || string.IsNullOrWhiteSpace(name)) return;

            long tick = _sessionStopwatch.ElapsedTicks;
            lock (_timelineLock)
            {
                _timeline.Add(new TimelineEvent
                {
                    Name = name.Trim(),
                    StartTicks = tick,
                    DurationTicks = 0,
                    ThreadId = Environment.CurrentManagedThreadId,
                    IsPoint = true
                });
            }
        }

        public static string Flush(string reason = "Manual")
        {
            lock (_lifecycleLock)
            {
                return string.Empty;
                if (!_started) return string.Empty;

                Point($"Session.Flush[{reason}]");
                _sessionStopwatch.Stop();

                System.IO.Directory.CreateDirectory(TraceDirectory);
                string file = System.IO.Path.Combine(
                    TraceDirectory,
                    $"StartupTrace_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.log");

                var sb = new StringBuilder();
                sb.AppendLine("=== TabPaint Startup Performance Trace ===");
                sb.AppendLine($"SessionName: {_sessionName}");
                sb.AppendLine($"Started(UTC): {_sessionStartUtc:O}");
                sb.AppendLine($"Reason: {reason}");
                sb.AppendLine($"TotalElapsed: {ToMs(_sessionStopwatch.ElapsedTicks):F3} ms");
                sb.AppendLine();

                sb.AppendLine("--- Aggregated Scopes (sorted by Total desc) ---");
                var statsSnapshot = _stats
                    .Select(kv => new
                    {
                        Name = kv.Key,
                        Calls = kv.Value.Calls,
                        TotalTicks = kv.Value.TotalTicks,
                        MaxTicks = kv.Value.MaxTicks
                    })
                    .OrderByDescending(x => x.TotalTicks)
                    .ToList();

                foreach (var item in statsSnapshot)
                {
                    double totalMs = ToMs(item.TotalTicks);
                    double avgMs = item.Calls > 0 ? totalMs / item.Calls : 0;
                    double maxMs = ToMs(item.MaxTicks);
                    sb.AppendLine($"{item.Name} | calls={item.Calls} | total={totalMs:F3} ms | avg={avgMs:F3} ms | max={maxMs:F3} ms");
                }

                sb.AppendLine();
                sb.AppendLine("--- Timeline (ordered by start) ---");
                List<TimelineEvent> timelineSnapshot;
                lock (_timelineLock)
                {
                    timelineSnapshot = _timeline.OrderBy(t => t.StartTicks).ToList();
                }

                foreach (var e in timelineSnapshot)
                {
                    double offset = ToMs(e.StartTicks);
                    double duration = ToMs(e.DurationTicks);
                    if (e.IsPoint)
                    {
                        sb.AppendLine($"+{offset,9:F3} ms | point | tid={e.ThreadId,2} | {e.Name}");
                    }
                    else
                    {
                        sb.AppendLine($"+{offset,9:F3} ms | dur={duration,8:F3} ms | tid={e.ThreadId,2} | {e.Name}");
                    }
                }

                System.IO.File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
                _started = false;
                return file;
            }
        }

        private static double ToMs(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }
    }
}
