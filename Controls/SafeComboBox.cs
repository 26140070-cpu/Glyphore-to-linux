namespace Glyphore;

internal sealed class SafeComboBox : ComboBox
{
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (e.Delta != 0 && !DroppedDown)
        {
            MouseWheelRouting.Route(this, e.Delta);
            return;
        }
        base.OnMouseWheel(e);
    }
}
