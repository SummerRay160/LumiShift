using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace LumiShift.Controls
{
    public static class GdiCache
    {
        private const int MaxBrushCache = 64;
        private const int MaxPenCache = 64;

        private static readonly Dictionary<int, CachedBrush> _brushes = new Dictionary<int, CachedBrush>();
        private static readonly Dictionary<long, CachedPen> _pens = new Dictionary<long, CachedPen>();
        private static readonly object _cacheLock = new object();
        private static long _accessCounter;

        private struct CachedBrush
        {
            public SolidBrush Brush;
            public long LastAccess;
        }

        private struct CachedPen
        {
            public Pen Pen;
            public long LastAccess;
        }

        public static SolidBrush GetBrush(Color color)
        {
            int argb = color.ToArgb();
            lock (_cacheLock)
            {
                _accessCounter++;
                if (_brushes.TryGetValue(argb, out var cached))
                {
                    _brushes[argb] = new CachedBrush { Brush = cached.Brush, LastAccess = _accessCounter };
                    return cached.Brush;
                }

                var brush = new SolidBrush(color);
                if (_brushes.Count >= MaxBrushCache)
                    EvictOldestBrush();
                _brushes[argb] = new CachedBrush { Brush = brush, LastAccess = _accessCounter };
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
                _accessCounter++;
                if (_pens.TryGetValue(key, out var cached))
                {
                    _pens[key] = new CachedPen { Pen = cached.Pen, LastAccess = _accessCounter };
                    return cached.Pen;
                }

                var pen = new Pen(color, width);
                if (_pens.Count >= MaxPenCache)
                    EvictOldestPen();
                _pens[key] = new CachedPen { Pen = pen, LastAccess = _accessCounter };
                return pen;
            }
        }

        private static void EvictOldestBrush()
        {
            int oldestKey = 0;
            long oldestAccess = long.MaxValue;
            foreach (var kvp in _brushes)
            {
                if (kvp.Value.LastAccess < oldestAccess)
                {
                    oldestAccess = kvp.Value.LastAccess;
                    oldestKey = kvp.Key;
                }
            }
            if (_brushes.TryGetValue(oldestKey, out var victim))
            {
                victim.Brush.Dispose();
                _brushes.Remove(oldestKey);
            }
        }

        private static void EvictOldestPen()
        {
            long oldestKey = 0;
            long oldestAccess = long.MaxValue;
            foreach (var kvp in _pens)
            {
                if (kvp.Value.LastAccess < oldestAccess)
                {
                    oldestAccess = kvp.Value.LastAccess;
                    oldestKey = kvp.Key;
                }
            }
            if (_pens.TryGetValue(oldestKey, out var victim))
            {
                victim.Pen.Dispose();
                _pens.Remove(oldestKey);
            }
        }

        public static void Clear()
        {
            lock (_cacheLock)
            {
                foreach (var kvp in _brushes)
                    kvp.Value.Brush.Dispose();
                _brushes.Clear();
                foreach (var kvp in _pens)
                    kvp.Value.Pen.Dispose();
                _pens.Clear();
            }
        }
    }
}