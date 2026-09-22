namespace Glyphore;

internal static class Theme
{
    
    
    public static readonly Color Bg = Color.FromArgb(11, 14, 20);
    public static readonly Color Panel = Color.FromArgb(15, 19, 26);
    public static readonly Color PanelRaised = Color.FromArgb(20, 25, 34);
    public static readonly Color PanelHover = Color.FromArgb(29, 35, 44);
    public static readonly Color PanelPressed = Color.FromArgb(35, 39, 47);
    public static readonly Color Input = Color.FromArgb(24, 30, 39);
    public static readonly Color Border = Color.FromArgb(55, 58, 63);
    public static readonly Color BorderHot = Color.FromArgb(113, 79, 46);
    public static readonly Color Track = Color.FromArgb(56, 50, 43);
    public static readonly Color Text = Color.FromArgb(242, 244, 248);
    public static readonly Color Muted = Color.FromArgb(168, 158, 145);
    public static readonly Color Accent = Color.FromArgb(218, 139, 59);
    public static readonly Color AccentHot = Color.FromArgb(234, 173, 88);
    public static readonly Color AccentPressed = Color.FromArgb(190, 105, 43);
    public static readonly Color AccentSoft = Color.FromArgb(69, 44, 25);
    public static readonly Color AccentText = Color.FromArgb(237, 183, 100);
    public static readonly Color BrandSurface = Color.FromArgb(11, 14, 20);

    public static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        
        
        
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddRectangle(rect);
        return path;
    }

    public static void TextBox(TextBox x)
    {
        x.BackColor = Input;
        x.ForeColor = Text;
        x.BorderStyle = BorderStyle.FixedSingle;
    }

    public static void Combo(ComboBox x)
    {
        x.BackColor = Input;
        x.ForeColor = Text;
        x.FlatStyle = FlatStyle.Flat;
    }

    public static void Button(Button b, bool accent = false)
    {
        if (b is GlyphButton glyph)
        {
            glyph.AccentStyle = accent;
            glyph.BackColor = accent ? Accent : PanelRaised;
            glyph.ForeColor = Text;
            glyph.FlatAppearance.BorderSize = 0;
            glyph.Invalidate();
            return;
        }

        b.BackColor = accent ? Accent : PanelRaised;
        b.ForeColor = Text;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = accent ? AccentHot : Border;
        b.FlatAppearance.MouseOverBackColor = accent ? AccentHot : PanelHover;
        b.FlatAppearance.MouseDownBackColor = accent ? AccentPressed : PanelPressed;
        b.UseVisualStyleBackColor = false;
    }

    public static void Numeric(NumericUpDown n)
    {
        n.BackColor = Input;
        n.ForeColor = Text;
        n.BorderStyle = BorderStyle.FixedSingle;
    }

    public static void CheckBox(CheckBox box)
    {
        box.ForeColor = Text;
        box.BackColor = Panel;
        box.FlatStyle = FlatStyle.Flat;
        box.UseVisualStyleBackColor = false;
        box.FlatAppearance.BorderColor = Color.FromArgb(205, 211, 220);
        box.FlatAppearance.CheckedBackColor = Accent;
        box.FlatAppearance.MouseOverBackColor = PanelHover;
        box.Invalidate();
    }
}
