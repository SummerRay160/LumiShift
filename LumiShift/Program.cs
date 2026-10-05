using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LumiShift.Infrastructure;
using LumiShift.Resources;
using LumiShift.Services;

namespace LumiShift
{
    internal static class Program
    {
        private static readonly Mutex _mutex =
            new Mutex(true, "LumiShift_SingleInstance_Mutex");

        internal static readonly Icon AppIcon = LoadAppIcon();

        private static Icon LoadAppIcon()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "LumiShift.app.ico";
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                    return new Icon(stream);
            }
            return System.Drawing.SystemIcons.Application;
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (!_mutex.WaitOne(TimeSpan.Zero, true))
            {
                NativeMethods.PostMessage(
                    (IntPtr)NativeMethods.HWND_BROADCAST,
                    NativeMethods.WM_SHOW_LUMISHIFT,
                    IntPtr.Zero,
                    IntPtr.Zero);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 多语言初始化必须在任何 UI 创建之前
            Lang.Init(SettingsStore.LoadSettings().Language);

            // 命令行 --minimized 只影响本次启动，不回写配置
            // （持久化的"最小化启动"由设置界面控制，注册表启动项自身已带 --minimized）
            bool startMinimized = false;
            foreach (var arg in args)
            {
                if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    startMinimized = true;
                    break;
                }
            }

            var context = new ApplicationContext();
            var bgService = new BackgroundService();

            if (startMinimized || bgService.Settings.StartMinimized)
                bgService.ScheduleLightweightModeEntry();
            else
                bgService.ShowMainWindow();

            try
            {
                Application.Run(context);
            }
            finally
            {
                try { bgService.Dispose(); } catch { }
                try { Controls.GdiCache.Clear(); } catch { }
                try { Typography.Cleanup(); } catch { }
                try { UpdateService.Shutdown(); } catch { }
                try { _mutex.ReleaseMutex(); } catch { }
                try { _mutex.Dispose(); } catch { }
                try { AppIcon?.Dispose(); } catch { }
            }
        }
    }
}