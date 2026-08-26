using System;
using System.Windows.Forms;

namespace LumiShift.Controls
{
    /// <summary>
    /// ComboBox 扩展：下拉关闭后把焦点移到最近的可聚焦祖先（TabPage / FocusablePanel），
    /// 使下拉框真正失焦，避免滚轮/方向键误改选项。思路同 DateTimePickerEx。
    /// </summary>
    internal class BlurComboBox : ComboBox
    {
        protected override void OnDropDownClosed(EventArgs e)
        {
            base.OnDropDownClosed(e);
            for (Control c = Parent; c != null; c = c.Parent)
                if (c.CanSelect) { c.Focus(); break; }
        }
    }
}
