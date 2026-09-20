using System.Drawing;
using System.Windows.Forms;

namespace Glyphore;


internal sealed class SimpleColorPickerForm : Form
{
    private readonly TrackBar _r = new() { Minimum = 0, Maximum = 255, TickFrequency = 16, TickStyle = TickStyle.None };
    private readonly TrackBar _g = new() { Minimum = 0, Maximum = 255, TickFrequency = 16, TickStyle = TickStyle.None };
    private readonly TrackBar _b = new() { Minimum = 0, Maximum = 255, TickFrequency = 16, TickStyle = TickStyle.None };
    private readonly TextBox _hex = new();
    private readonly Panel _preview = new() { BorderStyle = BorderStyle.FixedSingle };

    public Color SelectedColor { get; private set; }

    public SimpleColorPickerForm(Color initial)
    {
        Text = "Color personalizado";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(340, 300);
        BackColor = Color.FromArgb(0x10, 0x10, 0x18);
        ForeColor = Color.White;
        SelectedColor = initial;

        _r.Value = initial.R; _g.Value = initial.G; _b.Value = initial.B;

        var lblR = MakeLabel("R"); var lblG = MakeLabel("G"); var lblB = MakeLabel("B");
        var lblHex = MakeLabel("Hex");
        _hex.Text = Html(initial);
        _preview.BackColor = initial;
        _preview.Size = new Size(304, 48);

        var ok = MakeButton("Aceptar", DialogResult.OK);
        var cancel = MakeButton("Cancelar", DialogResult.Cancel);
        AcceptButton = ok; CancelButton = cancel;

        void SyncFromSliders(object? s, EventArgs e)
        {
            SelectedColor = Color.FromArgb(_r.Value, _g.Value, _b.Value);
            _preview.BackColor = SelectedColor;
            _hex.Text = Html(SelectedColor);
        }
        void SyncFromHex(object? s, EventArgs e)
        {
            Color? c = Parse(_hex.Text);
            if (c is null) return;
            SelectedColor = c.Value;
            _r.Value = c.Value.R; _g.Value = c.Value.G; _b.Value = c.Value.B;
            _preview.BackColor = SelectedColor;
        }
        _r.ValueChanged += SyncFromSliders; _g.ValueChanged += SyncFromSliders; _b.ValueChanged += SyncFromSliders;
        _hex.Leave += SyncFromHex; _hex.KeyDown += (_, ke) => { if (ke.KeyCode == Keys.Enter) SyncFromHex(null, EventArgs.Empty); };

        int y = 12;
        foreach (var (lbl, tb) in new[] { (lblR, _r), (lblG, _g), (lblB, _b) })
        {
            lbl.Location = new Point(12, y + 4); tb.Location = new Point(36, y); tb.Size = new Size(280, 30);
            Controls.Add(lbl); Controls.Add(tb); y += 40;
        }
        lblHex.Location = new Point(12, y + 6); _hex.Location = new Point(48, y); _hex.Size = new Size(268, 26);
        Controls.Add(lblHex); Controls.Add(_hex); y += 40;
        _preview.Location = new Point(12, y); Controls.Add(_preview); y += 60;
        ok.Location = new Point(150, y); cancel.Location = new Point(238, y);
        Controls.Add(ok); Controls.Add(cancel);
    }

    private static Label MakeLabel(string text) => new() { Text = text, ForeColor = Color.White, AutoSize = true };
    private static Button MakeButton(string text, DialogResult dr) => new() { Text = text, DialogResult = dr, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x22, 0x22, 0x2C), ForeColor = Color.White };
    private static string Html(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    private static Color? Parse(string s)
    {
        s = s.Trim().TrimStart('#');
        if (s.Length != 6) return null;
        try { return Color.FromArgb(Convert.ToInt32(s[..2], 16), Convert.ToInt32(s[2..4], 16), Convert.ToInt32(s[4..6], 16)); }
        catch { return null; }
    }
}
