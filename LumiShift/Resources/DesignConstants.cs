using System;
using System.Drawing;

namespace LumiShift.Resources
{
    public enum ThemeMode
    {
        Dark,
        Light,
        Auto
    }

    public class ThemeColors
    {
        public Color Background { get; set; }
        public Color BackgroundSecondary { get; set; }
        public Color Surface { get; set; }
        public Color SurfaceLight { get; set; }
        public Color Border { get; set; }
        public Color BorderLight { get; set; }
        public Color Brand { get; set; }
        public Color BrandHover { get; set; }
        public Color BrandGlow { get; set; }
        public Color Green { get; set; }
        public Color Red { get; set; }
        public Color Yellow { get; set; }
        public Color TextPrimary { get; set; }
        public Color TextSecondary { get; set; }
        public Color TextDisabled { get; set; }
        public Color TabInactive { get; set; }
    }

    public static class ThemeManager
    {
        private static ThemeMode _currentMode = ThemeMode.Auto;
        private static ThemeColors _active;
        private static bool _watching;
        private static bool _lastSystemDark;

        public static event EventHandler ThemeChanged;

        public static ThemeMode CurrentMode
        {
            get => _currentMode;
            set
            {
                if (_currentMode != value)
                {
                    _currentMode = value;
                    UpdateActiveTheme();
                    ThemeChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        public static ThemeColors Active => _active ?? (_active = GetDarkTheme());

        public static bool IsSystemDarkMode()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val is int i)
                            return i == 0;
                    }
                }
            }
            catch { }
            return true;
        }

        public static void StartWatchingSystemTheme()
        {
            if (_watching) return;
            _watching = true;
            _lastSystemDark = IsSystemDarkMode();
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        public static void StopWatchingSystemTheme()
        {
            if (!_watching) return;
            _watching = false;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }

        private static void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (_currentMode != ThemeMode.Auto) return;
            if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;

            bool isDark = IsSystemDarkMode();
            if (isDark == _lastSystemDark) return;
            _lastSystemDark = isDark;

            UpdateActiveTheme();
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void UpdateActiveTheme()
        {
            bool useDark;
            if (_currentMode == ThemeMode.Dark)
                useDark = true;
            else if (_currentMode == ThemeMode.Light)
                useDark = false;
            else
                useDark = IsSystemDarkMode();
            _active = useDark ? GetDarkTheme() : GetLightTheme();
            Controls.GdiCache.Clear();
        }

        public static ThemeColors GetDarkTheme()
        {
            return new ThemeColors
            {
                Background = Color.FromArgb(0x12, 0x13, 0x1E),
                BackgroundSecondary = Color.FromArgb(0x1A, 0x1B, 0x2E),
                Surface = Color.FromArgb(0x23, 0x25, 0x40),
                SurfaceLight = Color.FromArgb(0x2A, 0x2B, 0x3E),
                Border = Color.FromArgb(0x2D, 0x2F, 0x4A),
                BorderLight = Color.FromArgb(0x3A, 0x3B, 0x55),
                Brand = Color.FromArgb(0x6C, 0x63, 0xFF),
                BrandHover = Color.FromArgb(0x7B, 0x73, 0xFF),
                BrandGlow = Color.FromArgb(0x8B, 0x83, 0xFF),
                Green = Color.FromArgb(0x00, 0xD9, 0xA6),
                Red = Color.FromArgb(0xFF, 0x6B, 0x6B),
                Yellow = Color.FromArgb(0xFF, 0xD9, 0x3D),
                TextPrimary = Color.FromArgb(0xF0, 0xF0, 0xF5),
                TextSecondary = Color.FromArgb(0xD0, 0xD0, 0xE0),
                TextDisabled = Color.FromArgb(0x5A, 0x5A, 0x72),
                TabInactive = Color.FromArgb(0x1E, 0x1F, 0x2E)
            };
        }

        public static ThemeColors GetLightTheme()
        {
            return new ThemeColors
            {
                Background = Color.FromArgb(0xF5, 0xF5, 0xFA),
                BackgroundSecondary = Color.FromArgb(0xEE, 0xEE, 0xF5),
                Surface = Color.FromArgb(0xE5, 0xE5, 0xEE),
                SurfaceLight = Color.FromArgb(0xDC, 0xDC, 0xE5),
                Border = Color.FromArgb(0xCC, 0xCC, 0xD8),
                BorderLight = Color.FromArgb(0xBB, 0xBB, 0xCC),
                Brand = Color.FromArgb(0x6C, 0x63, 0xFF),
                BrandHover = Color.FromArgb(0x5A, 0x52, 0xE0),
                BrandGlow = Color.FromArgb(0x7B, 0x73, 0xFF),
                Green = Color.FromArgb(0x00, 0xB8, 0x8A),
                Red = Color.FromArgb(0xE0, 0x4A, 0x4A),
                Yellow = Color.FromArgb(0xD4, 0xA0, 0x00),
                TextPrimary = Color.FromArgb(0x1A, 0x1A, 0x2E),
                TextSecondary = Color.FromArgb(0x6A, 0x6A, 0x82),
                TextDisabled = Color.FromArgb(0xAA, 0xAA, 0xBA),
                TabInactive = Color.FromArgb(0xE8, 0xE8, 0xF0)
            };
        }
    }

    public static class Colors
    {
        public static Color Background => ThemeManager.Active.Background;
        public static Color BackgroundSecondary => ThemeManager.Active.BackgroundSecondary;
        public static Color Surface => ThemeManager.Active.Surface;
        public static Color SurfaceLight => ThemeManager.Active.SurfaceLight;
        public static Color Border => ThemeManager.Active.Border;
        public static Color BorderLight => ThemeManager.Active.BorderLight;
        public static Color Brand => ThemeManager.Active.Brand;
        public static Color BrandHover => ThemeManager.Active.BrandHover;
        public static Color BrandGlow => ThemeManager.Active.BrandGlow;
        public static Color Green => ThemeManager.Active.Green;
        public static Color Red => ThemeManager.Active.Red;
        public static Color Yellow => ThemeManager.Active.Yellow;
        public static Color TextPrimary => ThemeManager.Active.TextPrimary;
        public static Color TextSecondary => ThemeManager.Active.TextSecondary;
        public static Color TextDisabled => ThemeManager.Active.TextDisabled;
        public static Color TabInactive => ThemeManager.Active.TabInactive;
    }

    public static class Spacing
    {
        public const double Phi = 1.618;

        public static int Get(int level)
        {
            return (int)(4 * Math.Pow(Phi, level));
        }

        public static readonly int XS = Get(0);
        public static readonly int SM = Get(1);
        public static readonly int MD = Get(2);
        public static readonly int LG = Get(3);
        public static readonly int XL = Get(4);
        public static readonly int XXL = Get(5);

        public static readonly int ContentWidth = 400 - 2 * LG;
    }

    public static class Typography
    {
        private static readonly FontFamily FontFamily = new FontFamily("Segoe UI");
        private static readonly FontFamily MonoFontFamily = new FontFamily("Consolas");

        public static Font GetFont(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(FontFamily, size, style);
        }

        public static Font GetMonoFont(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(MonoFontFamily, size, style);
        }

        public static readonly Font H1 = GetFont(12f, FontStyle.Bold);
        public static readonly Font Body = GetFont(9f);
        public static readonly Font BodyBold = GetFont(9f, FontStyle.Bold);
        public static readonly Font Caption = GetFont(8f);
        public static readonly Font Mono = GetMonoFont(8.5f);
    }
}