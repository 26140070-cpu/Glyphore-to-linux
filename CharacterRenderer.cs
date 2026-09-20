namespace Glyphore;

internal static partial class CharacterRenderer
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
        double scale = Math.Max(.001, Math.Abs(P(layer, "scale")));
        double speed = P(layer, "speed");
        double density = P(layer, "density");
        double aspect = P(layer, "aspect");
        double amp = P(layer, "osc_amp");
        double fx = P(layer, "freq_x");
        double fy = P(layer, "freq_y");
        double fd = P(layer, "freq_diag");
        double fr = P(layer, "freq_radial");
        double tf = P(layer, "time_freq");
        double phaseDeg = P(layer, "phase_deg");
        double turb = P(layer, "turbulence");
        double warp = P(layer, "warp");
        double dx = P(layer, "drift_x");
        double dy = P(layer, "drift_y");
        double pulseParam = P(layer, "pulse");
        double iterations = P(layer, "iterations");

        double t = time * Math.Max(.001, speed);
        double phaseRad = phaseDeg * Math.PI / 180.0;
        double pulse = 1.0 + Math.Sin(t + phaseRad) * .18 * pulseParam;

        double gx = (x / (double)Math.Max(1, width) - .5) * 2.0;
        double gy = (y / (double)Math.Max(1, height) - .5) * 2.0;
        gx *= (width / (double)Math.Max(1, height)) * aspect;
        if (Math.Abs(warp) > .0001)
        {
            double qx = gx, qy = gy;
            gx += Math.Sin(qy * Math.Max(.05, Math.Abs(fy)) + t + phaseRad) * .10 * warp;
            gy += Math.Cos(qx * Math.Max(.05, Math.Abs(fx)) - t * .87 + phaseRad) * .10 * warp;
        }

        var p = new Vec2(gx, gy);
        var cell = new Vec2(x, y);
        var ctx = new Ctx(p, cell, width, height, layer.Seed, t, phaseDeg, pulse, pulseParam, scale, amp, fx, fy, fd, fr, tf, turb, warp, dx, dy, density, iterations);
        return Math.Clamp(SampleNamed(layer, ctx), 0, 1);
    }
}
