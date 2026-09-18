namespace Glyphore;

internal sealed class GlyphNumericUpDown : NumericUpDown
{
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (e.Delta != 0)
        {
            MouseWheelRouting.Route(this, e.Delta);
            return;
        }
        base.OnMouseWheel(e);
    }
}
