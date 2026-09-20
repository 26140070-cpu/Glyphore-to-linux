using System.ComponentModel;
using Majorsilence.Forms.Drawing.Drawing2D;
using Forms = Majorsilence.Forms;

namespace Glyphore;

internal sealed class FlatButtonAppearance
{
    public int BorderSize { get; set; }
    public Color BorderColor { get; set; } = Color.Empty;
    public Color MouseOverBackColor { get; set; } = Color.Empty;
    public Color MouseDownBackColor { get; set; } = Color.Empty;
    public Color CheckedBackColor { get; set; } = Color.Empty;
}

internal static class TextRenderer
{
    public static void DrawText(Graphics graphics, string? text, Font font, Rectangle bounds,
        Color foreColor, TextFormatFlags flags)
    {
        text ??= string.Empty;
        using var brush = new SolidBrush(foreColor);
        var format = new StringFormat(StringFormat.GenericDefault)
        {
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = (flags & TextFormatFlags.EndEllipsis) != 0 ? StringTrimming.EllipsisCharacter : StringTrimming.None,
            LineAlignment = (flags & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center : StringAlignment.Near,
            Alignment = (flags & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center : StringAlignment.Near
        };
        if ((flags & TextFormatFlags.WordBreak) != 0) format.FormatFlags &= ~StringFormatFlags.NoWrap;
        graphics.DrawString(text, font, brush, bounds, format);
    }
}

internal static class ControlPaint
{
    public static void DrawFocusRectangle(Graphics graphics, Rectangle bounds, Color foreColor, Color backColor)
    {
        var old = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.None;
        try
        {
            using var pen = new Pen(foreColor) { DashStyle = DashStyle.Dot };
            graphics.DrawRectangle(pen, bounds);
        }
        finally { graphics.SmoothingMode = old; }
    }
}

internal static class SystemInformation
{
    public const int MouseWheelScrollLines = 3;
    public const int HorizontalScrollBarHeight = 13;
    public const int VerticalScrollBarWidth = 13;
}

internal enum RichTextBoxScrollBars
{
    None = 0,
    Horizontal = 1,
    Vertical = 2,
    Both = 3
}

internal class CompatButton : Forms.Button { public new FlatButtonAppearance FlatAppearance { get; } = new(); public new bool UseVisualStyleBackColor { get; set; } }
internal class CompatCheckBox : Forms.CheckBox { public new FlatButtonAppearance FlatAppearance { get; } = new(); public new bool UseVisualStyleBackColor { get; set; } }
internal class CompatComboBox : Forms.ComboBox { public new bool DroppedDown { get; set; } }
internal class CompatTextBox : Forms.TextBox { public new int TextLength => Text?.Length ?? 0; }
internal class CompatRichTextBox : Forms.RichTextBox
{
    public new int TextLength => Text?.Length ?? 0;
    public new bool DetectUrls { get; set; }
    public new bool AcceptsTab { get; set; }
    public new bool ShortcutsEnabled { get; set; } = true;
    public new RichTextBoxScrollBars ScrollBars { get; set; } = RichTextBoxScrollBars.Both;
    public new void ScrollToCaret() { try { Focus(); } catch { } }
    public new Point GetPositionFromCharIndex(int index)
    {
        index = Math.Clamp(index, 0, TextLength);
        string text = Text ?? string.Empty;
        int line = 0, column = 0;
        for (int i = 0; i < index && i < text.Length; i++) { if (text[i] == '\n') { line++; column = 0; } else if (text[i] != '\r') column++; }
        int charWidth = Math.Max(1, Font.Height / 2);
        return new Point(column * charWidth, line * Math.Max(1, Font.Height));
    }
    protected IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam) => IntPtr.Zero;
}
internal class CompatLabel : Forms.Label { public new bool AutoEllipsis { get; set; } }
internal class CompatToolTip : Forms.ToolTip { }
internal class CompatFlowLayoutPanel : Forms.FlowLayoutPanel { }
internal class CompatTableLayoutPanel : Forms.TableLayoutPanel { }
internal class CompatSplitContainer : Forms.SplitContainer { }
internal class CompatListBox : Forms.ListBox { }

internal class CompatForm : Forms.Form
{
    private CompatButton? _acceptButton;
    private CompatButton? _cancelButton;
    public new CompatButton? AcceptButton { get => _acceptButton; set => _acceptButton = value; }
    public new CompatButton? CancelButton { get => _cancelButton; set => _cancelButton = value; }
    public CompatForm()
    {
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && _acceptButton is not null) { _acceptButton.PerformClick(); e.SuppressKeyPress = true; e.Handled = true; }
            else if (e.KeyCode == Keys.Escape && _cancelButton is not null) { _cancelButton.PerformClick(); e.SuppressKeyPress = true; e.Handled = true; }
        };
    }
}

internal class CompatContextMenuStrip : Forms.ContextMenuStrip
{
    public new void Show(Control anchor, Point position)
    {
        base.Show(anchor, position);
    }
}
