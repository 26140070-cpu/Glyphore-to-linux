using System.ComponentModel;

namespace Glyphore;






internal class GlyphoreWindow : Form
{
    private bool _resizable = true;





    public Panel ContentPanel { get; } = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Resizable
    {
        get => _resizable;
        set
        {
            if (_resizable == value) return;
            _resizable = value;
            ApplyBorderStyle();
        }
    }

    public GlyphoreWindow()
    {
        LinuxControlInitialization.Defer(this, () => BackColor = Theme.Bg, nameof(BackColor));
        LinuxControlInitialization.Defer(this, () => ForeColor = Theme.Text, nameof(ForeColor));
        LinuxControlInitialization.Defer(this, () => Font = new Font("Segoe UI", 9f), nameof(Font));
        LinuxControlInitialization.Defer(this, () => AutoScaleMode = AutoScaleMode.Dpi, nameof(AutoScaleMode));
        LinuxControlInitialization.Defer(this, () => KeyPreview = true, nameof(KeyPreview));
        LinuxControlInitialization.Defer(this, () => DoubleBuffered = true, nameof(DoubleBuffered));

        ApplyBorderStyle();
        Shown += (_, _) => LinuxWindowIcon.Apply(this);

        LinuxControlInitialization.Defer(ContentPanel, () =>
        {
            ContentPanel.Dock = DockStyle.Fill;
            ContentPanel.BackColor = Theme.Bg;
            ContentPanel.Margin = Padding.Empty;
            ContentPanel.Padding = Padding.Empty;
        }, "configure");
        base.Controls.Add(ContentPanel);
    }

    private void ApplyBorderStyle()
    {
        FormBorderStyle desired = _resizable
            ? FormBorderStyle.Sizable
            : FormBorderStyle.FixedSingle;

        if (FormBorderStyle != desired)
            FormBorderStyle = desired;
    }
}
