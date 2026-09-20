using System.ComponentModel;

namespace Glyphore;

internal sealed class ThemedRichTextBoxHost : UserControl
{
    internal RichTextBox Editor { get; } = new();

    private bool _showHorizontalScrollBar = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowHorizontalScrollBar
    {
        get => _showHorizontalScrollBar;
        set
        {
            _showHorizontalScrollBar = value;
            if (!Editor.IsDisposed)
                Editor.ScrollBars = value ? RichTextBoxScrollBars.Both : RichTextBoxScrollBars.Vertical;
        }
    }

    public ThemedRichTextBoxHost()
    {
        BackColor = Theme.Input;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        Editor.Dock = DockStyle.Fill;
        Editor.Margin = Padding.Empty;
        Editor.BorderStyle = BorderStyle.None;
        Editor.ScrollBars = RichTextBoxScrollBars.Both;
        Editor.WordWrap = false;
        Editor.BackColor = Theme.Input;
        Editor.ForeColor = Theme.Text;
        Editor.DetectUrls = false;
        Controls.Add(Editor);
    }

    internal void RefreshScrollBars() => Editor.Invalidate();
}
