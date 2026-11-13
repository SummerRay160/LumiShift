using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace LumiShift.Infrastructure
{
    /// <summary>日志级别。数值越大越严重，ERROR/FATAL 不受诊断日志开关限制。</summary>
    internal enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Fatal = 4
    }

    /// <summary>
    /// 诊断文件日志：%LocalAppData%\LumiShift\log.txt。
    /// 每行包含时间戳、级别、线程、请求 ID、上下文；关键操作通过 OperationScope 记录
    /// 操作类型、入参、状态与耗时，同一次调用链共享请求 ID。
    /// 存储策略：
    ///   DEBUG       仅详细模式（Verbose，Debug 构建默认开启）写入 log.txt；
    ///   INFO/WARN   受“诊断日志”开关控制，写入 log.txt；
    ///   ERROR/FATAL 始终写入 log.txt，并镜像到 error.log，不受开关影响。
    /// 主日志超过 1MB、错误日志超过 512KB 时轮转为 *.old.txt。
    /// </summary>
    internal static class Log
    {
        private const long MaxLogBytes = 1024 * 1024;
        private const long MaxErrorBytes = 512 * 1024;

        private static readonly object Gate = new object();
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LumiShift", "log.txt");
        private static readonly string ErrorFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LumiShift", "error.log");

        private static bool _enabled;

#if DEBUG
        private static bool _verbose = true;
#else
        private static bool _verbose;
#endif

        [ThreadStatic]
        private static string _requestId;

        /// <summary>日志文件所在目录（%LocalAppData%\LumiShift）。</summary>
        public static string FolderPath => Path.GetDirectoryName(FilePath);

        /// <summary>主日志文件完整路径，供界面校验日志是否真的写入成功。</summary>
        public static string LogFilePath => FilePath;

        /// <summary>
        /// 主路径写入失败时的兜底日志路径（程序所在目录）。
        /// 部分环境的 AppData 目录不允许创建新文件，但允许修改已有文件；
        /// 此时若没有兜底，日志会完全丢失，问题无法排查。
        /// </summary>
        public static string FallbackLogPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory ?? ".", "log.fallback.txt");

        /// <summary>最近一次日志写入失败的原因；写入成功时为空。界面据此给出可读提示。</summary>
        public static string LastWriteError { get; private set; }

        /// <summary>诊断日志开关；关闭后仅 ERROR/FATAL 仍然记录。</summary>
        public static bool Enabled => _enabled;

        /// <summary>是否输出 DEBUG 级日志。Debug 构建默认开启，Release 默认关闭。</summary>
        public static bool Verbose
        {
            get => _verbose;
            set => _verbose = value;
        }

        /// <summary>当前线程正在执行的请求 ID；没有进行中的操作时为 null。</summary>
        public static string CurrentRequestId => _requestId;

        /// <summary>生成 8 位请求 ID，用于关联同一次操作链路的全部日志。</summary>
        public static string NewRequestId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>写日志事件本身绕过开关：开关切换动作必须在文件里留痕。</summary>
        public static void SetEnabled(bool value)
        {
            bool changed = _enabled != value;
            _enabled = value;
            if (!changed) return;

            WriteDirect(LogLevel.Info, "Log", value ? "诊断日志已开启" : "诊断日志已关闭");
        }

        /// <summary>
        /// 开启一次可度量的操作。同线程嵌套调用会共享请求 ID，便于串联调用链。
        /// 默认以 DEBUG 级别输出起止记录；关键业务可传 completionLevel: LogLevel.Info。
        /// </summary>
        public static OperationScope BeginOperation(string operation, string parameters = null,
            LogLevel completionLevel = LogLevel.Debug)
        {
            return new OperationScope(operation, parameters, completionLevel);
        }

        public static void Debug(string context, string message)
        {
            Write(LogLevel.Debug, context, message);
        }

        public static void Info(string context, string message)
        {
            Write(LogLevel.Info, context, message);
        }

        public static void Warn(string context, string message)
        {
            Write(LogLevel.Warn, context, message);
        }

        public static void Error(string context, Exception ex)
        {
            WriteException(LogLevel.Error, context, ex, null, null);
        }

        /// <summary>记录异常及定位所需上下文：入参摘要、状态快照、运行环境。</summary>
        public static void Error(string context, Exception ex, string inputs, Func<string> stateSnapshot = null)
        {
            WriteException(LogLevel.Error, context, ex, inputs, stateSnapshot);
        }

        /// <summary>致命错误：始终落盘并镜像到 error.log，用于未捕获异常等进程级故障。</summary>
        public static void Fatal(string context, Exception ex, string inputs = null, Func<string> stateSnapshot = null)
        {
            WriteException(LogLevel.Fatal, context, ex, inputs, stateSnapshot);
        }

        /// <summary>写入会话头：应用版本、运行环境与日志级别策略。仅开关打开时落盘。</summary>
        public static void WriteSessionHeader(string reason)
        {
            try
            {
                Write(LogLevel.Info, "Session", $"===== LumiShift v{AssemblyVersion()} · {reason} =====");
                Write(LogLevel.Info, "Session",
                    $"OS: {Environment.OSVersion.VersionString} · CLR: {Environment.Version} · 64位进程: {Environment.Is64BitProcess} · 语言: {CultureInfo.CurrentUICulture.Name}");
                Write(LogLevel.Info, "Session",
                    $"日志策略: DEBUG={(Verbose ? "开启" : "仅详细模式")} · INFO/WARN={(Enabled ? "开启" : "关闭")} · ERROR/FATAL=始终记录 · 目录: {FolderPath}");
            }
            catch
            {
                // 日志写入失败不影响主流程
            }
        }

        private static bool ShouldWrite(LogLevel level)
        {
            if (level == LogLevel.Debug && !_verbose) return false;
            if (level >= LogLevel.Error) return true;   // 错误与致命错误必须可靠落盘
            return _enabled;
        }

        private static void Write(LogLevel level, string context, string message)
        {
            if (!ShouldWrite(level)) return;
            WriteDirect(level, context, message);
        }

        private static void WriteException(LogLevel level, string context, Exception ex, string inputs,
            Func<string> stateSnapshot)
        {
            if (ex == null) return;

            var sb = new StringBuilder(512);
            sb.Append("异常: ").Append(ex.GetType().FullName).Append(": ").Append(ex.Message);
            sb.Append("\r\n    Stack: ").Append(ex.StackTrace?.Trim() ?? "(无堆栈)");

            int depth = 0;
            for (var inner = ex.InnerException; inner != null && depth < 4; inner = inner.InnerException, depth++)
            {
                sb.Append("\r\n    Inner[").Append(depth + 1).Append("]: ")
                    .Append(inner.GetType().FullName).Append(": ").Append(inner.Message);
                sb.Append("\r\n      Stack: ").Append(inner.StackTrace?.Trim() ?? "(无堆栈)");
            }

            sb.Append("\r\n    Inputs: ").Append(string.IsNullOrEmpty(inputs) ? "(未提供)" : inputs);

            string state = null;
            try { state = stateSnapshot?.Invoke(); }
            catch (Exception stateEx) { state = "(状态快照采集失败: " + stateEx.Message + ")"; }
            if (!string.IsNullOrEmpty(state))
                sb.Append("\r\n    State: ").Append(state);

            sb.Append("\r\n    Env: ").Append(BuildEnvironmentSnapshot());

            WriteDirect(level, context, sb.ToString());
        }

        private static string BuildEnvironmentSnapshot()
        {
            try
            {
                var sb = new StringBuilder(256);
                sb.Append("app=").Append(AssemblyVersion())
                    .Append("; os=").Append(Environment.OSVersion.VersionString)
                    .Append("; clr=").Append(Environment.Version)
                    .Append("; x64=").Append(Environment.Is64BitProcess)
                    .Append("; culture=").Append(CultureInfo.CurrentCulture.Name)
                    .Append("; ui=").Append(CultureInfo.CurrentUICulture.Name)
                    .Append("; ws=").Append(Environment.WorkingSet / (1024 * 1024)).Append("MB")
                    .Append("; localAppData=").Append(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
                    .Append("; temp=").Append(Path.GetTempPath());
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "(环境快照采集失败: " + ex.Message + ")";
            }
        }

        private static void WriteDirect(LogLevel level, string context, string message)
        {
            try
            {
                string line = BuildLine(level, context, message);

                lock (Gate)
                {
                    Append(FilePath, line, MaxLogBytes);

                    // 错误与致命错误额外镜像到独立文件，便于优先排查
                    if (level >= LogLevel.Error)
                        Append(ErrorFilePath, line, MaxErrorBytes);
                }
                LastWriteError = null;
            }
            catch (Exception ex)
            {
                // 日志写入失败不影响主流程，但必须留下原因供界面提示
                LastWriteError = $"{ex.GetType().Name}: {ex.Message}";
                try
                {
                    lock (Gate)
                        Append(FallbackLogPath, BuildLine(level, context, message), MaxLogBytes);
                }
                catch
                {
                    // 兜底路径也失败时只能放弃，LastWriteError 已记录主路径原因
                }
            }
        }

        private static string BuildLine(LogLevel level, string context, string message)
        {
            var line = new StringBuilder(128 + (message?.Length ?? 0));
            line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(LevelText(level)).Append(']')
                .Append(" [T").Append(Thread.CurrentThread.ManagedThreadId).Append(']')
                .Append(" [RID:").Append(string.IsNullOrEmpty(_requestId) ? "-" : _requestId).Append(']')
                .Append(" [").Append(context).Append("] ")
                .Append(message);
            line.Append("\r\n");
            return line.ToString();
        }

        private static string LevelText(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug: return "DEBUG";
                case LogLevel.Warn: return "WARN";
                case LogLevel.Error: return "ERROR";
                case LogLevel.Fatal: return "FATAL";
                default: return "INFO";
            }
        }

        private static void Append(string path, string content, long maxBytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            RotateIfNeeded(path, maxBytes);
            File.AppendAllText(path, content);
        }

        /// <summary>超过上限：当前日志改名保留为 *.old.*（覆盖上一份），新日志从空文件开始。</summary>
        private static void RotateIfNeeded(string path, long maxBytes)
        {
            if (!File.Exists(path) || new FileInfo(path).Length <= maxBytes) return;

            string dir = Path.GetDirectoryName(path);
            string oldPath = Path.Combine(dir,
                Path.GetFileNameWithoutExtension(path) + ".old" + Path.GetExtension(path));
            if (File.Exists(oldPath))
                File.Delete(oldPath);
            File.Move(path, oldPath);
        }

        private static string AssemblyVersion()
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return fileVersion.ProductVersion ?? fileVersion.FileVersion ?? "?";
        }

        /// <summary>一次操作的度量范围：开始时生成/继承请求 ID，结束时记录状态与耗时。</summary>
        internal sealed class OperationScope : IDisposable
        {
            private readonly string _id;
            private readonly string _operation;
            private readonly string _parameters;
            private readonly LogLevel _completionLevel;
            private readonly string _previousRequestId;
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            private bool _completed;

            internal OperationScope(string operation, string parameters, LogLevel completionLevel)
            {
                _operation = operation ?? "Operation";
                _parameters = parameters;
                _completionLevel = completionLevel;
                _previousRequestId = _requestId;
                _id = string.IsNullOrEmpty(_previousRequestId) ? NewRequestId() : _previousRequestId;
                _requestId = _id;

                Write(LogLevel.Debug, _operation, $"开始 params={FormatParameters(_parameters)}");
            }

            /// <summary>标记操作完成，记录实际状态与耗时。</summary>
            public void Complete(string status = "Success", string detail = null)
            {
                if (_completed) return;
                _completed = true;
                _stopwatch.Stop();

                string message = $"status={status} elapsed={_stopwatch.ElapsedMilliseconds}ms params={FormatParameters(_parameters)}";
                if (!string.IsNullOrEmpty(detail))
                    message += " | " + detail;
                Write(_completionLevel, _operation, message);

                RestoreRequestId();
            }

            /// <summary>记录异常并结束操作；异常明细包含堆栈、入参、状态快照与环境信息。</summary>
            public void Fail(Exception ex, string inputs = null, Func<string> stateSnapshot = null)
            {
                if (_completed) return;
                _completed = true;
                _stopwatch.Stop();

                WriteException(LogLevel.Error, _operation, ex, inputs ?? _parameters, stateSnapshot);
                Write(_completionLevel, _operation,
                    $"status=Failed elapsed={_stopwatch.ElapsedMilliseconds}ms params={FormatParameters(_parameters)}");

                RestoreRequestId();
            }

            public void Dispose()
            {
                // 未显式标记结果时如实记录为中断，避免伪造成功状态
                if (!_completed)
                    Complete("Aborted", "未显式标记结果");
            }

            private void RestoreRequestId()
            {
                _requestId = _previousRequestId;
            }

            private static string FormatParameters(string parameters)
            {
                return string.IsNullOrEmpty(parameters) ? "-" : parameters;
            }
        }
    }
}
