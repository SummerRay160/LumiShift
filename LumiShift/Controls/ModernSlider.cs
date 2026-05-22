using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LumiShift.Resources;

namespace LumiShift.Controls
{
    public class ModernSlider : Control
    {
        private int _value = 50;
        private int _minimum;
        private int _maximum = 100;
        private bool _dragging;
        private bool _hovered;
        private bool _hoveredThumb;

        public event EventHandler ValueChanged;

        [DefaultValue(0)]
        public int Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_value < value) _value = value;
                Invalidate();
            }
        }

        [DefaultValue(100)]
        public int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                if (_value > value) _value = value;
                Invalidate();
            }
        }

        [DefaultValue(50)]
        public int Value
        {
            get => _value;
            set
            {
                int newVal = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value != newVal)
                {
                    _value = newVal;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public ModernSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.DoubleBuffer |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Height = 22;
            Width = 200;
            BackColor = Color.Transparent;
        }

        private float GetThumbCenter()
        {
            if (_maximum <= _minimum) return 8f;
            float ratio = (_value - _minimum) / (float)(_maximum - _minimum);
            return 8f + ratio * (Width - 16f);
        }

        private int GetValueFromX(float x)
        {
            if (_maximum <= _minimum) return _minimum;
            float ratio = (x - 8f) / (Width - 16f);
            ratio = Math.Max(0, Math.Min(1, ratio));
            return (int)(_minimum + ratio * (_maximum - _minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var bgBrush = new SolidBrush(Colors.Background))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            Form1.DrawBackgroundOnGraphics(g, ClientRectangle);

            float trackY = Height / 2f - 2f;
            float trackH = 4f;
            float thumbCenter = GetThumbCenter();
            float thumbSize = _hoveredThumb ? 16f : 14f;

            bool isActive = Enabled;

            Color inactiveTrack = _hovered ? Colors.Border : Colors.BorderLight;
            Color activeTrack = isActive ? Colors.Brand : Colors.TextDisabled;
            Color thumbColor = isActive
                ? (_hoveredThumb ? Colors.BrandGlow : Colors.Brand)
                : Colors.TextDisabled;

            using (var trackPath = CreateRoundRect(8f, trackY, Width - 16f, trackH, trackH / 2f))
            using (var trackBrush = new SolidBrush(inactiveTrack))
            {
                g.FillPath(trackBrush, trackPath);
            }

            float filledW = thumbCenter - 8f;
            if (filledW > 0 && isActive)
            {
                using (var fillPath = CreateRoundRect(8f, trackY, filledW, trackH, trackH / 2f))
                using (var fillBrush = new SolidBrush(activeTrack))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }

            float thumbX = thumbCenter - thumbSize / 2f;
            float thumbY = (Height - thumbSize) / 2f;

            using (var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, thumbX + 1, thumbY + 1, thumbSize, thumbSize);
            }

            g.FillEllipse(new SolidBrush(thumbColor), thumbX, thumbY, thumbSize, thumbSize);

            if (_hoveredThumb && isActive)
            {
                using (var hlBrush = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
                {
                    g.FillEllipse(hlBrush, thumbX + 3, thumbY + 3, thumbSize / 2f, thumbSize / 2f);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _dragging = true;
                Value = GetValueFromX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            float thumbCenter = GetThumbCenter();
            float thumbSize = 14f;
            bool overThumb = Math.Abs(e.X - thumbCenter) <= thumbSize / 2f + 3;

            if (_hoveredThumb != overThumb)
            {
                _hoveredThumb = overThumb;
                Invalidate();
            }

            if (_dragging && Enabled)
                Value = GetValueFromX(e.X);

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Cursor = Cursors.Hand;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _hoveredThumb = false;
            _dragging = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        private static GraphicsPath CreateRoundRect(float x, float y, float w, float h, float r)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}