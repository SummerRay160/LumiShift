using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace LumiShift.Infrastructure
{
    /// <summary>
    /// 诊断文件日志：%LocalAppData%\LumiShift\log.txt。
    /// 默认关闭，由设置 DiagnosticsLoggingEnabled 控制开关（SetEnabled）。
    /// 开启时写入会话头（版本/系统/运行时），行内含级别与线程号，错误附堆栈，
    /// 便于定位"哪个线程、哪个环节、什么异常"。
    /// 超过 1MB 时轮转为 log.old.txt（保留上一份），不直接清空。
    /// </summary>
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LumiShift", "log.txt");

        private static bool _enabled;

        /// <summary>写日志事件本身绕过开关：开关切换动作必须在文件里留痕。</summary>
        public static void SetEnabled(bool value)
        {
            bool changed = _enabled != value;
            _enabled = value;
            if (!changed) return;

            if (value)
                WriteDirect("INFO", "Log", "诊断日志已开启");
            else
                WriteDirect("INFO", "Log", "诊断日志已关闭");
        }

        /// <summary>写入会话头：应用版本、操作系统、运行时。仅开关打开时落盘。</summary>
        public static void WriteSessionHeader(string reason)
        {
            try
            {
                string version = AssemblyVersion();
                string os = Environment.OSVersion.VersionString;
                string clr = Environment.Version.ToString();
                Write("INFO", "Session",
                    $"===== LumiShift v{version} · {reason} =====");
                Write("INFO", "Session", $"OS: {os} · CLR: {clr} · 64位进程: {Environment.Is64BitProcess}");
            }
            catch
            {
                // 日志写入失败不影响主流程
            }
        }

        public static void Info(string context, string message)
        {
            Write("INFO", context, message);
        }

        public static void Warn(string context, string message)
        {
            Write("WARN", context, message);
        }

        public static void Error(string context, Exception ex)
        {
            Write("ERROR", context, Format(ex));
        }

        /// <summary>
        /// 关键故障（例如配置损坏、被迫回退默认值）绕过开关强制落盘：
        /// 这类问题会静默丢弃用户数据，必须留痕，否则无从排查。
        /// </summary>
        public static void Critical(string context, Exception ex)
        {
            WriteDirect("ERROR", context, Format(ex));
        }

        // 异常类型 + 消息 + 完整堆栈：堆栈在 catch 边界处截取，长度有限
        private static string Format(Exception ex)
        {
            return $"{ex.GetType().Name}: {ex.Message}\r\n    {ex.StackTrace?.Trim()}";
        }

        private static void Write(string level, string context, string message)
        {
            if (!_enabled) return;
            WriteDirect(level, context, message);
        }

        private static void WriteDirect(string level, string context, string message)
        {
            try
            {
                lock (Gate)
                {
                    // 配置目录可能被"删除配置"功能移除，重新创建
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    RotateIfNeeded();

                    var line = new StringBuilder(128);
                    line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                        .Append(" [").Append(level).Append(']')
                        .Append(" [T").Append(Thread.CurrentThread.ManagedThreadId).Append(']')
                        .Append(" [").Append(context).Append("] ")
                        .Append(message)
                        .Append("\r\n");

                    File.AppendAllText(FilePath, line.ToString());
                }
            }
            catch
            {
                // 日志写入失败不影响主流程
            }
        }

        /// <summary>超过 1MB：当前日志改名保留为 log.old.txt（覆盖上一份），新日志从空文件开始。</summary>
        private static void RotateIfNeeded()
        {
            const long MaxBytes = 1024 * 1024;
            if (!File.Exists(FilePath) || new FileInfo(FilePath).Length <= MaxBytes) return;

            string oldPath = Path.Combine(Path.GetDirectoryName(FilePath), "log.old.txt");
            if (File.Exists(oldPath))
                File.Delete(oldPath);
            File.Move(FilePath, oldPath);
        }

        private static string AssemblyVersion()
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return fileVersion.ProductVersion ?? fileVersion.FileVersion ?? "?";
        }
    }
}
