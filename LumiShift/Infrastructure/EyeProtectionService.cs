using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LumiShift.Infrastructure
{
    public static class EyeProtectionService
    {
        private const int COLOR_WINDOW = 5;
        private const int COLOR_BACKGROUND = 1;
        private const int COLOR_APPWORKSPACE = 12;

        [DllImport("user32.dll")]
        private static extern bool SetSysColors(int cElements, int[] lpaElements, int[] lpaRgbValues);

        public static bool ApplyColor(int red, int green, int blue)
        {
            try
            {
                int rgb = RGB(red, green, blue);

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors", true))
                {
                    if (key != null)
                    {
                        string colorValue = $"{red} {green} {blue}";
                        key.SetValue("Window", colorValue, RegistryValueKind.String);
                        key.SetValue("Background", colorValue, RegistryValueKind.String);
                        key.SetValue("AppWorkspace", colorValue, RegistryValueKind.String);
                    }
                }

                int[] elements = { COLOR_WINDOW, COLOR_BACKGROUND, COLOR_APPWORKSPACE };
                int[] colors = { rgb, rgb, rgb };
                return SetSysColors(3, elements, colors);
            }
            catch
            {
                return false;
            }
        }

        public static bool RestoreDefault()
        {
            try
            {
                int rgb = RGB(255, 255, 255);

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors", true))
                {
                    if (key != null)
                    {
                        key.SetValue("Window", "255 255 255", RegistryValueKind.String);
                        key.SetValue("Background", "0 0 0", RegistryValueKind.String);
                        key.SetValue("AppWorkspace", "255 255 255", RegistryValueKind.String);
                    }
                }

                int[] elements = { COLOR_WINDOW, COLOR_BACKGROUND, COLOR_APPWORKSPACE };
                int[] colors = { rgb, RGB(0, 0, 0), rgb };
                return SetSysColors(3, elements, colors);
            }
            catch
            {
                return false;
            }
        }

        private static int RGB(int r, int g, int b)
        {
            return (r | (g << 8) | (b << 16));
        }
    }
}