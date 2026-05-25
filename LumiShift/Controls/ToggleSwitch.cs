using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LumiShift.Resources;

namespace LumiShift.Controls
{
    public class ToggleSwitch : Control
    {
        private bool _checked;
        private float _animProgress;
        private Timer _animTimer;
        private bool _hovered;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    StartAnimation(value ? 1f : 0f);
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.DoubleBuffer |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Width = 42;
            Height = 22;
            BackColor = Color.Transparent;
            _animProgress = _checked ? 1f : 0f;
            _animTimer = new Timer { Interval = 16 };
            _animTimer.Tick += OnAnimationTick;
        }

        private void StartAnimation(float target)
        {
            _animTimer.Stop();
            _animTimer.Tag = target;
            _animTimer.Start();
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            float target = (float)_animTimer.Tag;
            _animProgress += (target - _animProgress) * 0.28f;
            if (Math.Abs(_animProgress - target) < 0.01f)
            {
                _animProgress = target;
                _animTimer.Stop();
            }
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
        }

        private static readonly Color ThumbEdgeColor = Color.FromArgb(30, 0, 0, 0);
        private static readonly Color GlowColorStatic = Color.FromArgb(40, 0, 0, 0);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float h = Height;
            float w = Width;
            float trackH = 12f;
            float trackY = (h - trackH) / 2f;
            float trackX = 2f;
            float trackW = w - 4f;

            float thumbSize = 18f;
            float thumbY = (h - thumbSize) / 2f;
            float thumbMin = 2f;
            float thumbMax = w - thumbSize - 2f;
            float thumbX = thumbMin + (thumbMax - thumbMin) * _animProgress;

            Color trackColor = _checked ? Colors.Green : Colors.BorderLight;

            using (var trackPath = CreateRoundRect(trackX, trackY, trackW, trackH, trackH / 2f))
            {
                g.FillPath(GdiCache.GetBrush(trackColor), trackPath);
            }

            if (_checked)
            {
                using (var glowPath = CreateRoundRect(trackX, trackY, trackW, trackH, trackH / 2f))
                {
                    g.FillPath(GdiCache.GetBrush(Color.FromArgb(40, Colors.Green)), glowPath);
                    var big = new RectangleF(trackX - 2, trackY - 2, trackW + 4, trackH + 4);
                    using (var gp = CreateRoundRect(big.X, big.Y, big.Width, big.Height, big.Height / 2f))
                    {
                        g.FillPath(GdiCache.GetBrush(Color.FromArgb(40, Colors.Green)), gp);
                    }
                }
            }

            g.FillEllipse(GdiCache.GetBrush(_hovered ? Colors.BrandHover : Color.White), thumbX, thumbY, thumbSize, thumbSize);

            g.DrawEllipse(GdiCache.GetPen(ThumbEdgeColor), thumbX, thumbY, thumbSize, thumbSize);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                Checked = !Checked;
            base.OnMouseClick(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animTimer?.Dispose();
                _animTimer = null;
            }
            base.Dispose(disposing);
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