using System;
using System.Threading;
using System.Windows.Forms;
using LumiShift.Infrastructure;
using LumiShift.Services;

namespace LumiShift
{
    internal static class Program
    {
        private static readonly Mutex _mutex =
            new Mutex(true, "LumiShift_SingleInstance_Mutex");

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

            Application.Run(new Form1());
        }
    }
}