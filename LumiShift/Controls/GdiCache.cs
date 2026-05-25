using System;
using System.Collections.Generic;
using System.Drawing;

namespace LumiShift.Controls
{
    public static class GdiCache
    {
        private static readonly Dictionary<int, SolidBrush> _brushes = new Dictionary<int, SolidBrush>();
        private static readonly Dictionary<long, Pen> _pens = new Dictionary<long, Pen>();
        private static readonly object _cacheLock = new object();

        public static SolidBrush GetBrush(Color color)
        {
            int argb = color.ToArgb();
            lock (_cacheLock)
            {
                if (!_brushes.TryGetValue(argb, out var brush))
                {
                    brush = new SolidBrush(color);
                    _brushes[argb] = brush;
                }
                return brush;
            }
        }

        public static Pen GetPen(Color color, float width = 1f)
        {
            int argb = color.ToArgb();
            int widthBits = BitConverter.ToInt32(BitConverter.GetBytes((float)Math.Round(width, 2)), 0);
            long key = ((long)argb << 32) | (uint)widthBits;
            lock (_cacheLock)
            {
                if (!_pens.TryGetValue(key, out var pen))
                {
                    pen = new Pen(color, width);
                    _pens[key] = pen;
                }
                return pen;
            }
        }

        public static void Clear()
        {
            lock (_cacheLock)
            {
                foreach (var b in _brushes.Values)
                    b.Dispose();
                _brushes.Clear();
                foreach (var p in _pens.Values)
                    p.Dispose();
                _pens.Clear();
            }
        }
    }
}