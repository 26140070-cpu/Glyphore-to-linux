namespace Glyphore;

internal sealed class SceneTransform
{
    public double Rotation { get; set; }
    public double PerspectiveX { get; set; }
    public double PerspectiveY { get; set; }
    /// <summary>Linux UI convenience; maps to glyph display scale elsewhere when needed.</summary>
    public double Scale { get; set; } = 1.0;

    public SceneTransform Clone() => new()
    {
        Rotation = Rotation,
        PerspectiveX = PerspectiveX,
        PerspectiveY = PerspectiveY,
        Scale = Scale
    };

    public void Clamp()
    {
        Rotation = Math.Clamp(Rotation, -180.0, 180.0);
        PerspectiveX = Math.Clamp(PerspectiveX, -1.5, 1.5);
        PerspectiveY = Math.Clamp(PerspectiveY, -1.5, 1.5);
        Scale = Math.Clamp(Scale, 0.1, 8.0);
    }
}
