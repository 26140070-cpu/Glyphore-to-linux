namespace Glyphore;

internal static class CharacterRenderer
{
    public static string Render(GlyphoreScene scene, double time)
    {
        var active = scene.ActiveLayer ?? scene.Layers.FirstOrDefault();
        if (active is not null && active.Effect.Equals("ASCII Title", StringComparison.OrdinalIgnoreCase))
            return AsciiTitleGenerator.Generate(active.TitleText, active.TitlePrefab).TrimEnd();
        var charset = string.IsNullOrEmpty(scene.Charset) ? " " : scene.Charset;
        var rows = new string[scene.Height];
        for (var y = 0; y < scene.Height; y++)
        {
            var chars = new char[scene.Width];
            for (var x = 0; x < scene.Width; x++)
            {
                var nx = x / (double)Math.Max(1, scene.Width - 1);
                var ny = y / (double)Math.Max(1, scene.Height - 1);
                var value = 0d;
                foreach (var layer in scene.Layers.Where(layer => layer.Visible))
                {
                    var sample = Sample(layer, x, y, scene.Width, scene.Height, time);
                    sample *= MaskCoverage(layer.Masks, nx, ny);
                    value = layer.BlendMode == LayerBlendMode.Additive ? value + sample * layer.Opacity : Math.Max(value, sample * layer.Opacity);
                }
                value = Math.Clamp(value / Math.Max(1, scene.Layers.Count), 0d, 1d);
                value = PostProcess(value, scene.PostProcess, nx, ny);
                if (scene.Invert) value = 1d - value;
                chars[x] = charset[Math.Clamp((int)Math.Round(value * (charset.Length - 1)), 0, charset.Length - 1)];
            }
            rows[y] = new string(chars);
        }
        return string.Join(Environment.NewLine, rows);
    }

    private static double PostProcess(double value, ScenePostProcess post, double x, double y)
    {
        if (!post.Enabled) return value;
        value = Math.Clamp((value + post.Exposure) * post.Contrast, 0d, 1d);
        if (post.Threshold > 0) value = value >= post.Threshold ? 1 : 0;
        if (post.Posterize > 1) value = Math.Round(value * post.Posterize) / post.Posterize;
        if (post.Vignette > 0) value *= 1 - Math.Clamp(Math.Sqrt(Math.Pow(x - .5, 2) + Math.Pow(y - .5, 2)) * post.Vignette, 0, .9);
        if (post.Scanlines > 0) value *= 1 - (Math.Sin(y * 400) + 1) * .5 * post.Scanlines;
        return Math.Clamp(value, 0d, 1d);
    }

    private static double MaskCoverage(IEnumerable<SceneLayerMask> masks, double x, double y)
    {
        var coverage = 1d;
        foreach (var mask in masks.Where(mask => mask.Enabled))
        {
            var dx = (x - mask.X) / Math.Max(.001, mask.Width * .5);
            var dy = (y - mask.Y) / Math.Max(.001, mask.Height * .5);
            var inside = mask.Type switch
            {
                SceneMaskType.Ellipse => dx * dx + dy * dy <= 1,
                SceneMaskType.Diamond => Math.Abs(dx) + Math.Abs(dy) <= 1,
                SceneMaskType.Ring => dx * dx + dy * dy is >= .45 and <= 1,
                SceneMaskType.Triangle => dy >= -1 && dy <= 1 && Math.Abs(dx) <= 1 - dy * .5,
                _ => Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1
            };
            var amount = inside ? mask.Strength : 0;
            coverage *= mask.Invert ? 1 - amount : amount;
        }
        return coverage;
    }

    public static (float R, float G, float B) PreviewColor(GlyphoreScene scene, double time)
    {
        var hue = (float)((Math.Sin(time * 0.3) + 1) * 0.5);
        return (0.04f + hue * 0.15f, 0.025f + hue * 0.04f, 0.10f + hue * 0.30f);
    }

    private static double Sample(SceneEffectLayer layer, int x, int y, int width, int height, double time)
    {
        var scale = layer.Values.GetValueOrDefault("scale", 1);
        var speed = layer.Values.GetValueOrDefault("speed", 1);
        var density = layer.Values.GetValueOrDefault("density", 1);
        var nx = x / (double)Math.Max(1, width - 1);
        var ny = y / (double)Math.Max(1, height - 1);
        var n = layer.Seed * 0.00031;
        time *= speed;
        nx *= scale;
        ny *= scale;
        return Math.Clamp(layer.Effect switch
        {
            "Matrix Rain" => Math.Sin(x * .31 + y * .07 - time * 4 + n) > .75 ? 1 : 0.08,
            "Waves" => (Math.Sin(nx * 18 + time * 2) + Math.Cos(ny * 16 - time * 1.4) + 2) * .25,
            "Noise" => Hash(x, y, time, layer.Seed),
            _ => (Math.Sin(nx * 12 + time + n) + Math.Sin(ny * 10 - time * .7) + Math.Sin((nx + ny) * 9 + time) + 3) / 6
        } * density, 0, 1);
    }

    private static double Hash(int x, int y, double time, int seed)
    {
        var value = Math.Sin(x * 12.9898 + y * 78.233 + time * 9.17 + seed) * 43758.5453;
        return value - Math.Floor(value);
    }
}
