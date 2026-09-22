using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Glyphore;






#pragma warning disable CS0114, CS0109, CS0067

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
            Trimming = StringTrimming.EllipsisCharacter,
            LineAlignment = (flags & TextFormatFlags.VerticalCenter) != 0
                ? StringAlignment.Center
                : StringAlignment.Near,
            Alignment = (flags & TextFormatFlags.HorizontalCenter) != 0
                ? StringAlignment.Center
                : StringAlignment.Near
        };
        if ((flags & TextFormatFlags.EndEllipsis) == 0)
            format.Trimming = StringTrimming.None;
        if ((flags & TextFormatFlags.WordBreak) != 0)
            format.FormatFlags &= ~StringFormatFlags.NoWrap;
        graphics.DrawString(text, font, brush, bounds, format);
        format.Dispose();
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
        finally
        {
            graphics.SmoothingMode = old;
        }
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
    Both = 3,
}

internal class CompatButton : System.Windows.Forms.Button
{
    private FlatStyle _flatStyle;
    private bool _useVisualStyleBackColor;

    public FlatStyle FlatStyle
    {
        get => _flatStyle;
        set => _flatStyle = value;
    }

    public FlatButtonAppearance FlatAppearance { get; } = new();

    public bool UseVisualStyleBackColor
    {
        get => _useVisualStyleBackColor;
        set => _useVisualStyleBackColor = value;
    }
}

internal class CompatCheckBox : System.Windows.Forms.CheckBox
{
    private FlatStyle _flatStyle;

    public FlatStyle FlatStyle
    {
        get => _flatStyle;
        set => _flatStyle = value;
    }

    public FlatButtonAppearance FlatAppearance { get; } = new();

    public bool UseVisualStyleBackColor { get; set; }
}

internal class CompatComboBox : System.Windows.Forms.ComboBox
{
    public FlatStyle FlatStyle { get; set; }
    public DrawMode DrawMode { get; set; }
    public int ItemHeight { get; set; }
    public bool DroppedDown { get; set; }
    public event DrawItemEventHandler? DrawItem;


}

internal class CompatTextBox : System.Windows.Forms.TextBox
{
    public int TextLength => Text?.Length ?? 0;
}

internal class CompatRichTextBox : System.Windows.Forms.RichTextBox
{
    public int TextLength => Text?.Length ?? 0;
    public bool WordWrap { get; set; }
    public bool DetectUrls { get; set; }
    public bool AcceptsTab { get; set; }
    public bool Multiline { get; set; }
    public bool HideSelection { get; set; }
    public bool ShortcutsEnabled { get; set; } = true;
    public RichTextBoxScrollBars ScrollBars { get; set; } = RichTextBoxScrollBars.Both;

    public new void ScrollToCaret()
    {
        
        
        try { Focus(); } catch { }
    }

    public new Point GetPositionFromCharIndex(int index)
    {
        index = Math.Clamp(index, 0, TextLength);
        string text = Text ?? string.Empty;
        int line = 0;
        int column = 0;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n') { line++; column = 0; }
            else if (text[i] != '\r') column++;
        }
        int charWidth = Math.Max(1, Font.Height / 2);
        return new Point(column * charWidth, line * Math.Max(1, Font.Height));
    }

    protected IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        return IntPtr.Zero;
    }
}

internal class CompatLabel : System.Windows.Forms.Label
{
    public bool AutoEllipsis { get; set; }
}

internal class CompatFlowLayoutPanel : System.Windows.Forms.FlowLayoutPanel
{
    public AutoSizeMode AutoSizeMode { get; set; }
}

internal class CompatTableLayoutPanel : System.Windows.Forms.TableLayoutPanel
{
    public AutoSizeMode AutoSizeMode { get; set; }
}

internal class CompatSplitContainer : System.Windows.Forms.SplitContainer
{
    private int _panel1MinSize;
    private int _panel2MinSize;
    private bool _panel1Collapsed;
    private int _splitterDistance;

    public int Panel1MinSize
    {
        get => _panel1MinSize;
        set => _panel1MinSize = Math.Max(0, value);
    }

    public int Panel2MinSize
    {
        get => _panel2MinSize;
        set => _panel2MinSize = Math.Max(0, value);
    }

    public bool Panel1Collapsed
    {
        get => _panel1Collapsed;
        set
        {
            if (_panel1Collapsed == value) return;
            _panel1Collapsed = value;
            try
            {
                Panel1.Visible = !value;
                if (!value && _splitterDistance > 0)
                    SplitterDistance = Math.Max(1, _splitterDistance);
            }
            catch { }
            SplitterMoved?.Invoke(this, EventArgs.Empty);
        }
    }

    public new int SplitterDistance
    {
        get => _splitterDistance > 0 ? _splitterDistance : base.SplitterDistance;
        set
        {
            _splitterDistance = Math.Max(0, value);
            try { base.SplitterDistance = value; } catch { }
        }
    }

    public event EventHandler? SplitterMoved;

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        SplitterMoved?.Invoke(this, e);
    }
}

internal class CompatListBox : System.Windows.Forms.ListBox
{
    public DrawMode DrawMode { get; set; }
    public event DrawItemEventHandler? DrawItem;


}

internal class CompatForm : System.Windows.Forms.Form
{
    private CompatButton? _acceptButton;
    private CompatButton? _cancelButton;

    public CompatButton? AcceptButton
    {
        get => _acceptButton;
        set => _acceptButton = value;
    }

    public CompatButton? CancelButton
    {
        get => _cancelButton;
        set => _cancelButton = value;
    }

    public new event EventHandler? Activated;

    public CompatForm()
    {
        Shown += (_, _) => Activated?.Invoke(this, EventArgs.Empty);
        GotFocus += (_, _) => Activated?.Invoke(this, EventArgs.Empty);
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && _acceptButton is not null)
            {
                _acceptButton.PerformClick();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape && _cancelButton is not null)
            {
                _cancelButton.PerformClick();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        };
    }
}

internal class CompatContextMenuStrip : System.Windows.Forms.ContextMenuStrip
{
    private bool _isDisposed;

    public new event EventHandler? Closed;

    public new bool IsDisposed => _isDisposed;

    protected override void Dispose(bool disposing)
    {
        _isDisposed = true;
        base.Dispose(disposing);
    }

    public void Show(Control anchor, Point position)
    {
        var popup = new CompatForm
        {
            Text = string.Empty,
            FormBorderStyle = FormBorderStyle.FixedSingle,
            StartPosition = FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(260, Math.Max(1, Items.Count) * 34 + 12),
            BackColor = Theme.PanelRaised,
            ForeColor = Theme.Text,
        };

        var list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(6),
            Margin = Padding.Empty,
            BackColor = Theme.PanelRaised,
        };

        foreach (ToolStripItem raw in Items)
        {
            if (raw is not ToolStripMenuItem item) continue;
            var button = new CompatButton
            {
                Text = item.Text,
                Width = 240,
                Height = 28,
                Margin = new Padding(0, 0, 0, 4),
                BackColor = Theme.PanelRaised,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
            };
            button.Click += (_, _) =>
            {
                if (item.Enabled) item.PerformClick();
                popup.Close();
            };
            list.Controls.Add(button);
        }

        popup.Controls.Add(list);
        popup.FormClosed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
        popup.Shown += (_, _) =>
        {
            try { popup.Activate(); } catch { }
        };
        popup.ShowDialog();
        if (!popup.IsDisposed) popup.Dispose();
    }
}

#pragma warning restore CS0114, CS0109, CS0067

