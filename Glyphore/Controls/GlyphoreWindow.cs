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
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        DoubleBuffered = true;

        
        
        ApplyBorderStyle();

        ContentPanel.Dock = DockStyle.Fill;
        ContentPanel.BackColor = Theme.Bg;
        ContentPanel.Margin = Padding.Empty;
        ContentPanel.Padding = Padding.Empty;
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
