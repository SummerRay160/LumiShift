using System.Windows.Forms;

namespace LumiShift.Controls
{
    /// <summary>
    /// DateTimePicker 扩展：按 Enter 键让父容器获得焦点，使 picker 真正失焦。
    /// 原控件在 ShowUpDown 模式下会吞掉 Enter 键，导致用户无法用回车确认输入。
    /// </summary>
    internal class DateTimePickerEx : DateTimePicker
    {
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                // 让父容器（FocusablePanel）获得焦点，picker 真正失焦
                if (Parent != null && Parent.CanFocus)
                    Parent.Focus();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
