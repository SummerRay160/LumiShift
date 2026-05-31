using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Text;

namespace LumiShift.Infrastructure
{
    public static class GcHelper
    {
        private static readonly long _initialMemory = GC.GetTotalMemory(true);
        private static long _peakMemory = _initialMemory;
        private static DateTime _peakTime = DateTime.Now;
        private static long _peakPrivateBytes;
        private static DateTime _peakPrivateTime = DateTime.Now;

        private static readonly int MaxHistorySize = 20;
        private static readonly List<MemorySample> _history = new List<MemorySample>(MaxHistorySize);
        private static DateTime _lastSampleTime = DateTime.MinValue;
        private static readonly TimeSpan MinSampleInterval = TimeSpan.FromMinutes(5);

        private const long WarningThresholdBytes = 80 * 1024 * 1024;
        private const long CriticalThresholdBytes = 150 * 1024 * 1024;
        private const long PrivateWarningThresholdBytes = 120 * 1024 * 1024;
        private const long PrivateCriticalThresholdBytes = 250 * 1024 * 1024;
        private const int GdiWarningThreshold = 500;
        private const int GdiCriticalThreshold = 1000;
        private const double TrendSlopeWarningBytesPerMinute = 512 * 1024;

        private static readonly string _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LumiShift");

        private static readonly string _logPath = Path.Combine(_logDir, "memory_diagnostics.log");

        private static readonly object _logLock = new object();
        private static readonly int MaxLogSizeBytes = 256 * 1024;

        private static readonly List<WeakReference<EventHandler<MemoryLeakWarningEventArgs>>> _leakWarningHandlers =
            new List<WeakReference<EventHandler<MemoryLeakWarningEventArgs>>>();

        public static event EventHandler<MemoryLeakWarningEventArgs> LeakWarning
        {
            add
            {
                if (value == null) return;
                lock (_leakWarningHandlers)
                {
                    _leakWarningHandlers.RemoveAll(wr => !wr.TryGetTarget(out _));
                    _leakWarningHandlers.Add(new WeakReference<EventHandler<MemoryLeakWarningEventArgs>>(value));
                }
            }
            remove
            {
                if (value == null) return;
                lock (_leakWarningHandlers)
                {
                    _leakWarningHandlers.RemoveAll(wr =>
                        !wr.TryGetTarget(out var target) || ReferenceEquals(target, value));
                }
            }
        }

        private static void OnLeakWarning(MemoryLeakWarningEventArgs e)
        {
            LogMemoryWarning(e);

            List<EventHandler<MemoryLeakWarningEventArgs>> handlers;
            lock (_leakWarningHandlers)
            {
                handlers = new List<EventHandler<MemoryLeakWarningEventArgs>>(_leakWarningHandlers.Count);
                var dead = new List<int>();
                for (int i = 0; i < _leakWarningHandlers.Count; i++)
                {
                    if (_leakWarningHandlers[i].TryGetTarget(out var handler))
                        handlers.Add(handler);
                    else
                        dead.Add(i);
                }
                for (int i = dead.Count - 1; i >= 0; i--)
                    _leakWarningHandlers.RemoveAt(dead[i]);
            }
            foreach (var h in handlers)
            {
                try { h(null, e); }
                catch { }
            }
        }

        public static void CollectFull()
        {
            GC.Collect(1, GCCollectionMode.Forced, false);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, true, true);
            }
            catch
            {
            }
        }

        public static void TrimWorkingSet()
        {
            try
            {
                NativeMethods.SetProcessWorkingSetSize(
                    NativeMethods.GetCurrentProcess(),
                    new IntPtr(-1),
                    new IntPtr(-1));
            }
            catch
            {
            }
        }

        public static MemorySnapshot GetSnapshot()
        {
            long managed = GC.GetTotalMemory(false);
            if (managed > _peakMemory)
            {
                _peakMemory = managed;
                _peakTime = DateTime.Now;
            }

            long privateBytes = GetPrivateBytes();
            if (privateBytes > _peakPrivateBytes)
            {
                _peakPrivateBytes = privateBytes;
                _peakPrivateTime = DateTime.Now;
            }

            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);

            return new MemorySnapshot
            {
                ManagedMemoryBytes = managed,
                PeakMemoryBytes = _peakMemory,
                PeakTime = _peakTime,
                InitialMemoryBytes = _initialMemory,
                PrivateBytes = privateBytes,
                PeakPrivateBytes = _peakPrivateBytes,
                PeakPrivateTime = _peakPrivateTime,
                Gen0Collections = gen0,
                Gen1Collections = gen1,
                Gen2Collections = gen2,
                GdiObjectsCount = GetGdiCount(),
                UserObjectsCount = GetUserCount()
            };
        }

        public static bool DetectLeakSuspect(long thresholdBytes = 100 * 1024 * 1024)
        {
            long current = GC.GetTotalMemory(false);
            return current > thresholdBytes;
        }

        public static void RecordSampleAndCheck()
        {
            var now = DateTime.Now;
            if (now - _lastSampleTime < MinSampleInterval)
                return;
            _lastSampleTime = now;

            var snapshot = GetSnapshot();

            _history.Add(new MemorySample { Time = now, ManagedBytes = snapshot.ManagedMemoryBytes, PrivateBytes = snapshot.PrivateBytes, GdiCount = snapshot.GdiObjectsCount });
            if (_history.Count > MaxHistorySize)
                _history.RemoveAt(0);

            CheckMemoryTrend();
            CheckPrivateBytesTrend();
            CheckGdiLeak(snapshot.GdiObjectsCount);
        }

        private static void CheckMemoryTrend()
        {
            if (_history.Count < 5) return;

            int half = _history.Count / 2;
            long firstHalfSum = 0, secondHalfSum = 0;
            for (int i = 0; i < half; i++)
                firstHalfSum += _history[i].ManagedBytes;
            for (int i = half; i < _history.Count; i++)
                secondHalfSum += _history[i].ManagedBytes;

            long firstHalfAvg = firstHalfSum / half;
            long secondHalfAvg = secondHalfSum / (_history.Count - half);

            long growth = secondHalfAvg - firstHalfAvg;
            double growthPercent = firstHalfAvg > 0 ? (double)growth / firstHalfAvg * 100.0 : 0;

            double slopeBytesPerMin = ComputeLinearRegressionSlope();

            if (secondHalfAvg > CriticalThresholdBytes)
            {
                OnLeakWarning(new MemoryLeakWarningEventArgs(
                    MemoryLeakSeverity.Critical,
                    $"内存使用达到临界水平: {secondHalfAvg / 1024 / 1024}MB (增长 {growthPercent:F1}%, 斜率 {slopeBytesPerMin / 1024:F0}KB/min)",
                    secondHalfAvg));
            }
            else if (secondHalfAvg > WarningThresholdBytes && (growthPercent > 20 || slopeBytesPerMin > TrendSlopeWarningBytesPerMinute))
            {
                OnLeakWarning(new MemoryLeakWarningEventArgs(
                    MemoryLeakSeverity.Warning,
                    $"内存持续增长: {secondHalfAvg / 1024 / 1024}MB (增长 {growthPercent:F1}%, 斜率 {slopeBytesPerMin / 1024:F0}KB/min)",
                    secondHalfAvg));
            }
        }

        private static void CheckPrivateBytesTrend()
        {
            if (_history.Count < 5) return;

            int half = _history.Count / 2;
            long firstHalfSum = 0, secondHalfSum = 0;
            for (int i = 0; i < half; i++)
                firstHalfSum += _history[i].PrivateBytes;
            for (int i = half; i < _history.Count; i++)
                secondHalfSum += _history[i].PrivateBytes;

            long secondHalfAvg = secondHalfSum / (_history.Count - half);

            if (secondHalfAvg > PrivateCriticalThresholdBytes)
            {
                OnLeakWarning(new MemoryLeakWarningEventArgs(
                    MemoryLeakSeverity.Critical,
                    $"进程私有内存达到临界水平: {secondHalfAvg / 1024 / 1024}MB",
                    secondHalfAvg));
            }
            else if (secondHalfAvg > PrivateWarningThresholdBytes)
            {
                long firstHalfAvg = firstHalfSum / half;
                double growthPercent = firstHalfAvg > 0 ? (double)(secondHalfAvg - firstHalfAvg) / firstHalfAvg * 100.0 : 0;
                if (growthPercent > 20)
                {
                    OnLeakWarning(new MemoryLeakWarningEventArgs(
                        MemoryLeakSeverity.Warning,
                        $"进程私有内存持续增长: {secondHalfAvg / 1024 / 1024}MB (增长 {growthPercent:F1}%)",
                        secondHalfAvg));
                }
            }
        }

        private static double ComputeLinearRegressionSlope()
        {
            if (_history.Count < 3) return 0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            double t0 = _history[0].Time.Ticks;
            int n = _history.Count;

            for (int i = 0; i < n; i++)
            {
                double x = (_history[i].Time.Ticks - t0) / (double)TimeSpan.TicksPerMinute;
                double y = _history[i].ManagedBytes;
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            double denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 1e-10) return 0;

            return (n * sumXY - sumX * sumY) / denominator;
        }

        private static void CheckGdiLeak(int gdiCount)
        {
            if (gdiCount < 0) return;

            if (gdiCount > GdiCriticalThreshold)
            {
                OnLeakWarning(new MemoryLeakWarningEventArgs(
                    MemoryLeakSeverity.Critical,
                    $"GDI对象数达到临界水平: {gdiCount}",
                    gdiCount));
            }
            else if (gdiCount > GdiWarningThreshold)
            {
                OnLeakWarning(new MemoryLeakWarningEventArgs(
                    MemoryLeakSeverity.Warning,
                    $"GDI对象数偏高: {gdiCount}",
                    gdiCount));
            }
        }

        public static string GetDiagnosticReport()
        {
            var snapshot = GetSnapshot();
            var sb = new StringBuilder();

            sb.AppendLine("=== LumiShift 内存诊断报告 ===");
            sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"托管内存: {snapshot.ManagedMemoryBytes / 1024 / 1024}MB");
            sb.AppendLine($"峰值托管内存: {snapshot.PeakMemoryBytes / 1024 / 1024}MB ({snapshot.PeakTime:HH:mm:ss})");
            sb.AppendLine($"初始内存: {snapshot.InitialMemoryBytes / 1024 / 1024}MB");
            sb.AppendLine($"进程私有内存: {snapshot.PrivateBytes / 1024 / 1024}MB");
            sb.AppendLine($"峰值私有内存: {snapshot.PeakPrivateBytes / 1024 / 1024}MB ({snapshot.PeakPrivateTime:HH:mm:ss})");
            sb.AppendLine($"GDI对象数: {snapshot.GdiObjectsCount}");
            sb.AppendLine($"USER对象数: {snapshot.UserObjectsCount}");
            sb.AppendLine($"GC: Gen0={snapshot.Gen0Collections} Gen1={snapshot.Gen1Collections} Gen2={snapshot.Gen2Collections}");
            sb.AppendLine();

            if (_history.Count >= 2)
            {
                double slope = ComputeLinearRegressionSlope();
                sb.AppendLine($"趋势斜率: {slope / 1024:F0} KB/min");
                sb.AppendLine($"采样点数: {_history.Count}/{MaxHistorySize}");
                sb.AppendLine();

                sb.AppendLine("--- 历史采样 ---");
                for (int i = 0; i < _history.Count; i++)
                {
                    var s = _history[i];
                    sb.AppendLine($"  [{s.Time:HH:mm:ss}] 托管:{s.ManagedBytes / 1024 / 1024}MB 私有:{s.PrivateBytes / 1024 / 1024}MB GDI:{s.GdiCount}");
                }
            }
            else
            {
                sb.AppendLine("采样数据不足，需要至少2个采样点才能计算趋势");
            }

            return sb.ToString();
        }

        public static string GetQuickHealthStatus()
        {
            var snapshot = GetSnapshot();
            long managedMb = snapshot.ManagedMemoryBytes / 1024 / 1024;
            long privateMb = snapshot.PrivateBytes / 1024 / 1024;
            int gdi = snapshot.GdiObjectsCount;

            string status = managedMb < WarningThresholdBytes / 1024 / 1024 ? "正常" : "偏高";
            return $"托管:{managedMb}MB 私有:{privateMb}MB GDI:{gdi} [{status}]";
        }

        public static void LogDiagnosticReport()
        {
            try
            {
                string report = GetDiagnosticReport();
                lock (_logLock)
                {
                    if (!Directory.Exists(_logDir))
                        Directory.CreateDirectory(_logDir);

                    File.AppendAllText(_logPath, report + Environment.NewLine);

                    try
                    {
                        var fileInfo = new FileInfo(_logPath);
                        if (fileInfo.Exists && fileInfo.Length > MaxLogSizeBytes)
                        {
                            string content = File.ReadAllText(_logPath);
                            int keepPos = Math.Max(0, content.Length - MaxLogSizeBytes / 2);
                            int newlinePos = content.IndexOf('\n', keepPos);
                            if (newlinePos > 0)
                                content = content.Substring(newlinePos + 1);
                            File.WriteAllText(_logPath, content);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static void LogMemoryWarning(MemoryLeakWarningEventArgs e)
        {
            try
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{e.Severity}] {e.Message}";
                lock (_logLock)
                {
                    if (!Directory.Exists(_logDir))
                        Directory.CreateDirectory(_logDir);
                    File.AppendAllText(_logPath, entry + Environment.NewLine);
                }
            }
            catch { }
        }

        private static Process _cachedProcess;
        private static bool _cachedProcessDisposed;

        private static long GetPrivateBytes()
        {
            try
            {
                if (_cachedProcess == null || _cachedProcessDisposed)
                {
                    _cachedProcess?.Dispose();
                    _cachedProcess = Process.GetCurrentProcess();
                    _cachedProcessDisposed = false;
                }
                _cachedProcess.Refresh();
                return _cachedProcess.PrivateMemorySize64;
            }
            catch
            {
                return 0;
            }
        }

        public static void DisposeCachedProcess()
        {
            if (_cachedProcess != null && !_cachedProcessDisposed)
            {
                _cachedProcess.Dispose();
                _cachedProcessDisposed = true;
            }
        }

        private static int GetGdiCount()
        {
            try
            {
                return NativeMethods.GetGuiResources(
                    NativeMethods.GetCurrentProcess(),
                    NativeMethods.GR_GDIOBJECTS);
            }
            catch
            {
                return -1;
            }
        }

        private static int GetUserCount()
        {
            try
            {
                return NativeMethods.GetGuiResources(
                    NativeMethods.GetCurrentProcess(),
                    NativeMethods.GR_USEROBJECTS);
            }
            catch
            {
                return -1;
            }
        }

        private struct MemorySample
        {
            public DateTime Time;
            public long ManagedBytes;
            public long PrivateBytes;
            public int GdiCount;
        }
    }

    public struct MemorySnapshot
    {
        public long ManagedMemoryBytes;
        public long PeakMemoryBytes;
        public DateTime PeakTime;
        public long InitialMemoryBytes;
        public long PrivateBytes;
        public long PeakPrivateBytes;
        public DateTime PeakPrivateTime;
        public int Gen0Collections;
        public int Gen1Collections;
        public int Gen2Collections;
        public int GdiObjectsCount;
        public int UserObjectsCount;

        public override string ToString()
        {
            return $"Managed: {ManagedMemoryBytes / 1024}KB, Private: {PrivateBytes / 1024}KB, " +
                   $"Peak: {PeakMemoryBytes / 1024}KB, GDI: {GdiObjectsCount}, User: {UserObjectsCount}, " +
                   $"Gen0: {Gen0Collections}, Gen1: {Gen1Collections}, Gen2: {Gen2Collections}";
        }
    }

    public enum MemoryLeakSeverity
    {
        Warning,
        Critical
    }

    public class MemoryLeakWarningEventArgs : EventArgs
    {
        public MemoryLeakSeverity Severity { get; }
        public string Message { get; }
        public long Value { get; }

        public MemoryLeakWarningEventArgs(MemoryLeakSeverity severity, string message, long value)
        {
            Severity = severity;
            Message = message;
            Value = value;
        }
    }
}
