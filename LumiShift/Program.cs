using System;
using System.Drawing;
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

            bool startMinimized = false;
            foreach (var arg in args)
            {
                if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    startMinimized = true;
                    break;
                }
            }

            if (startMinimized)
            {
                var settings = Services.SettingsStore.LoadSettings();
                settings.StartMinimized = true;
                Services.SettingsStore.SaveSettings(settings);
            }

            var context = new ApplicationContext();
            var bgService = new BackgroundService();

            if (!bgService.Settings.StartMinimized)
                bgService.ShowMainWindow();
            else
                bgService.ScheduleLightweightModeEntry();

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