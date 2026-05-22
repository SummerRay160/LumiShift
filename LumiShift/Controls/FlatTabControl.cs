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

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= TabCount) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var tabBounds = e.Bounds;
            bool selected = SelectedIndex == e.Index;

            using (var bgBrush = new SolidBrush(selected ? Colors.Background : Colors.TabInactive))
            {
                g.FillRectangle(bgBrush, tabBounds);
            }

            if (selected)
            {
                using (var lineBrush = new SolidBrush(Colors.Brand))
                {
                    g.FillRectangle(lineBrush, tabBounds.X + 8, tabBounds.Bottom - 2,
                        tabBounds.Width - 16, 2);
                }
            }

            string text = TabPages[e.Index].Text;
            using (var textBrush = new SolidBrush(selected ? Colors.TextPrimary : Colors.TextSecondary))
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
            {
                g.DrawString(text, Font, textBrush, tabBounds, sf);
            }

            if (e.Index == TabCount - 1)
            {
                using (var borderPen = new Pen(Colors.Border))
                {
                    g.DrawLine(borderPen, 0, tabBounds.Bottom, ClientSize.Width, tabBounds.Bottom);
                }
            }
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            TabSelected?.Invoke(this, SelectedIndex);
            Invalidate();
        }
    }
}
