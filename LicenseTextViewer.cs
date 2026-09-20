namespace Glyphore;

internal sealed class LicenseTextViewer : RichTextBox
{
    private const int EmGetFirstVisibleLine = 0x00CE;

    internal int FirstVisibleLine => IsHandleCreated
        ? SendMessage(Handle, EmGetFirstVisibleLine, IntPtr.Zero, IntPtr.Zero).ToInt32()
        : 0;

    internal bool IsDocumentEndVisible
    {
        get
        {
            if (!IsHandleCreated || TextLength == 0) return true;
            Point end = GetPositionFromCharIndex(Math.Max(0, TextLength - 1));
            return end.Y >= -Font.Height && end.Y <= ClientSize.Height;
        }
    }

    public LicenseTextViewer()
    {
        Multiline = true;
        ReadOnly = true;
        WordWrap = false;
        DetectUrls = false;
        HideSelection = false;
        ShortcutsEnabled = true;
        BorderStyle = BorderStyle.None;
        ScrollBars = RichTextBoxScrollBars.Both;
        TabStop = true;
        KeyDown += HandleKeyDown;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Home && !e.Control && !e.Alt)
        {
            SelectionLength = 0;
            SelectionStart = 0;
            ScrollToCaret();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.End && !e.Control && !e.Alt)
        {
            SelectionLength = 0;
            SelectionStart = TextLength;
            ScrollToCaret();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }
    }

}
