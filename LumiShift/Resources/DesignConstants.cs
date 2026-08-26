using System;
using System.Collections.Generic;
using System.Drawing;

namespace LumiShift.Resources
{
    public static class Colors
    {
        public static readonly Color Background = Color.FromArgb(0xF5, 0xF5, 0xFA);
        public static readonly Color Surface = Color.FromArgb(0xE5, 0xE5, 0xEE);
        public static readonly Color SurfaceLight = Color.FromArgb(0xDC, 0xDC, 0xE5);
        public static readonly Color Border = Color.FromArgb(0xCC, 0xCC, 0xD8);
        public static readonly Color BorderLight = Color.FromArgb(0xBB, 0xBB, 0xCC);
        public static readonly Color Brand = Color.FromArgb(0x08, 0x91, 0xB2);
        public static readonly Color BrandHover = Color.FromArgb(0x0E, 0x74, 0x90);
        public static readonly Color BrandGlow = Color.FromArgb(0x22, 0xD3, 0xEE);
        public static readonly Color Green = Color.FromArgb(0x00, 0xB8, 0x8A);
        public static readonly Color Red = Color.FromArgb(0xE0, 0x4A, 0x4A);
        public static readonly Color TextPrimary = Color.FromArgb(0x1A, 0x1A, 0x2E);
        public static readonly Color TextSecondary = Color.FromArgb(0x6A, 0x6A, 0x82);
        public static readonly Color TextDisabled = Color.FromArgb(0xAA, 0xAA, 0xBA);
        public static readonly Color TabInactive = Color.FromArgb(0xE8, 0xE8, 0xF0);
    }

    public static class Spacing
    {
        public const double Phi = 1.618;

        public static int Get(int level)
        {
            return (int)(4 * Math.Pow(Phi, level));
        }

        public static readonly int SM = Get(1);
        public static readonly int LG = Get(3);
    }

    public static class Typography
    {
        private static FontFamily _fontFamily = new FontFamily("Segoe UI");
        private static FontFamily _monoFontFamily = new FontFamily("Consolas");
        private static Font _h1;
        private static Font _body;
        private static Font _bodyBold;
        private static Font _caption;
        private static Font _mono;
        private static readonly object _lock = new object();

        private static FontFamily FontFamily
        {
            get
            {
                if (_fontFamily == null)
                    _fontFamily = new FontFamily("Segoe UI");
                return _fontFamily;
            }
        }

        private static FontFamily MonoFontFamily
        {
            get
            {
                if (_monoFontFamily == null)
                    _monoFontFamily = new FontFamily("Consolas");
                return _monoFontFamily;
            }
        }

        public static Font GetFont(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(FontFamily, size, style);
        }

        public static Font GetMonoFont(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(MonoFontFamily, size, style);
        }

        public static Font H1
        {
            get
            {
                if (_h1 == null) _h1 = GetFont(12f, FontStyle.Bold);
                return _h1;
            }
        }

        public static Font Body
        {
            get
            {
                if (_body == null) _body = GetFont(9f);
                return _body;
            }
        }

        public static Font BodyBold
        {
            get
            {
                if (_bodyBold == null) _bodyBold = GetFont(9f, FontStyle.Bold);
                return _bodyBold;
            }
        }

        public static Font Caption
        {
            get
            {
                if (_caption == null) _caption = GetFont(8f);
                return _caption;
            }
        }

        public static Font Mono
        {
            get
            {
                if (_mono == null) _mono = GetMonoFont(8.5f);
                return _mono;
            }
        }

        public static void Cleanup()
        {
            lock (_lock)
            {
                _h1?.Dispose();
                _h1 = null;
                _body?.Dispose();
                _body = null;
                _bodyBold?.Dispose();
                _bodyBold = null;
                _caption?.Dispose();
                _caption = null;
                _mono?.Dispose();
                _mono = null;
                _fontFamily?.Dispose();
                _fontFamily = null;
                _monoFontFamily?.Dispose();
                _monoFontFamily = null;
            }
        }
    }
}