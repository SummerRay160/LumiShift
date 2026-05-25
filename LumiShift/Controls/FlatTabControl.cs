using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LumiShift.Resources;

namespace LumiShift.Controls
{
    public class FlatTabControl : TabControl
    {
        public event EventHandler<int> TabSelected;

        public FlatTabControl()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.DoubleBuffer |
                     ControlStyles.OptimizedDoubleBuffer, true);
            ItemSize = new Size(0, 30);
            SizeMode = TabSizeMode.FillToRight;
            Alignment = TabAlignment.Top;
            DrawMode = TabDrawMode.OwnerDrawFixed;
            Appearance = TabAppearance.Normal;
        }

        private static readonly StringFormat CenteredStringFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= TabCount) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var tabBounds = e.Bounds;
            bool selected = SelectedIndex == e.Index;

            g.FillRectangle(GdiCache.GetBrush(selected ? Colors.Background : Colors.TabInactive), tabBounds);

            if (selected)
            {
                g.FillRectangle(GdiCache.GetBrush(Colors.Brand), tabBounds.X + 8, tabBounds.Bottom - 2,
                    tabBounds.Width - 16, 2);
            }

            string text = TabPages[e.Index].Text;
            g.DrawString(text, Font, GdiCache.GetBrush(selected ? Colors.TextPrimary : Colors.TextSecondary), tabBounds, CenteredStringFormat);

            if (e.Index == TabCount - 1)
            {
                g.DrawLine(GdiCache.GetPen(Colors.Border), 0, tabBounds.Bottom, ClientSize.Width, tabBounds.Bottom);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CenteredStringFormat?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            TabSelected?.Invoke(this, SelectedIndex);
            Invalidate();
        }
    }
}
