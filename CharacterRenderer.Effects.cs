namespace Glyphore;

internal static partial class CharacterRenderer
{
    private readonly struct Vec2
    {
        public readonly double X, Y;
        public Vec2(double x, double y) { X = x; Y = y; }
        public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, Vec2 b) => new(a.X * b.X, a.Y * b.Y);
        public static Vec2 operator /(Vec2 a, Vec2 b) => new(a.X / b.X, a.Y / b.Y);
        public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
        public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);
        public static Vec2 operator +(Vec2 a, double s) => new(a.X + s, a.Y + s);
        public static Vec2 operator +(double s, Vec2 a) => new(a.X + s, a.Y + s);
        public static Vec2 operator -(Vec2 a, double s) => new(a.X - s, a.Y - s);
        public double Length => Math.Sqrt(X * X + Y * Y);
        public double Dot(Vec2 b) => X * b.X + Y * b.Y;
    }

    private readonly struct Ctx
    {
        public readonly Vec2 P;
        public readonly Vec2 Cell;
        public readonly int GridW, GridH, Seed;
        public readonly double T, PhaseDeg, PhaseRad, Pulse, PulseParam, Scale, Amp, Fx, Fy, Fd, Fr, Tf, Turb, Warp, Dx, Dy, Density, Iterations;

        public Ctx(Vec2 p, Vec2 cell, int gridW, int gridH, int seed, double t, double phaseDeg, double pulse, double pulseParam,
            double scale, double amp, double fx, double fy, double fd, double fr, double tf, double turb, double warp,
            double dx, double dy, double density, double iterations)
        {
            P = p; Cell = cell; GridW = gridW; GridH = gridH; Seed = seed; T = t;
            PhaseDeg = phaseDeg; PhaseRad = phaseDeg * Math.PI / 180.0; Pulse = pulse; PulseParam = pulseParam;
            Scale = scale; Amp = amp; Fx = fx; Fy = fy; Fd = fd; Fr = fr; Tf = tf; Turb = turb; Warp = warp;
            Dx = dx; Dy = dy; Density = density; Iterations = iterations;
        }
    }

    private static double P(SceneEffectLayer layer, string key)
        => layer.Values.TryGetValue(key, out var v) ? v : ParameterCatalog.Defaults.GetValueOrDefault(key, 0);

    private static double Sat(double x) => Math.Clamp(x, 0, 1);
    private static double Frac(double x) => x - Math.Floor(x);
    private static double Mod(double a, double b) => a - b * Math.Floor(a / b);
    private static double Mix(double a, double b, double t) => a + (b - a) * t;
    private static double SmoothStep(double e0, double e1, double x) { var t = Sat((x - e0) / Math.Max(1e-9, e1 - e0)); return t * t * (3 - 2 * t); }
    private static double StepFn(double edge, double x) => x < edge ? 0.0 : 1.0;
    private static double Tri(double x) => Math.Abs(Frac(x) * 2 - 1);
    private static double Sign(double x) => x > 0 ? 1.0 : x < 0 ? -1.0 : 0.0;
    private static double Smooth1(double t) => t * t * (3 - 2 * t);

    private static Vec2 RotateGlsl(Vec2 v, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return new Vec2(c * v.X + s * v.Y, -s * v.X + c * v.Y);
    }

    private static double Hash21(Vec2 p, int seed)
    {
        double px = Frac(p.X * 123.34), py = Frac(p.Y * 456.21);
        double k = 45.32 + (seed % 997) * 0.001;
        double d = px * (px + k) + py * (py + k);
        px += d; py += d;
        return Frac(px * py);
    }

    private static double Hash11(double x, int seed) => Hash21(new Vec2(x, x * 1.317 + 17.0), seed);

    private static double VNoise(Vec2 p, int seed)
    {
        double ix = Math.Floor(p.X), iy = Math.Floor(p.Y);
        double fx = Smooth1(p.X - ix), fy = Smooth1(p.Y - iy);
        double a = Hash21(new Vec2(ix, iy), seed);
        double b = Hash21(new Vec2(ix + 1, iy), seed);
        double cc = Hash21(new Vec2(ix, iy + 1), seed);
        double d = Hash21(new Vec2(ix + 1, iy + 1), seed);
        return Mix(Mix(a, b, fx), Mix(cc, d, fx), fy);
    }

    private static double Fbm(Vec2 p, double t, int seed)
    {
        double s = 0, a = .5, n = 0;
        for (int i = 0; i < 7; i++)
        {
            var off = new Vec2(t * (.22 + i * .047), -t * (.17 + i * .031));
            s += VNoise(p + off, seed) * a;
            n += a;
            p = p * 2.02 + new Vec2(13.7, 7.1);
            a *= .5;
        }
        return s / Math.Max(n, .001);
    }

    private static double StaticFbm(Vec2 p, int seed)
    {
        double sum = 0, weight = .5, norm = 0;
        for (int i = 0; i < 5; i++)
        {
            sum += VNoise(p, seed) * weight;
            norm += weight;
            p = p * 2.03 + new Vec2(17.13, 9.71);
            weight *= .5;
        }
        return sum / Math.Max(norm, .001);
    }

    private static double PeriodicLine(double coord, double width)
    {
        double f = Frac(coord);
        double d = Math.Min(f, 1 - f);
        return 1 - SmoothStep(width, width + 0.006, d);
    }

    private static double SampleNamed(SceneEffectLayer layer, Ctx c) => layer.Effect switch
    {
        "Plasma" => EffectPlasma(c),
        "Mandelbrot Zoom" => Mandelbrot(c, false),
        "Julia" => EffectJulia(c),
        "Burning Ship" => Mandelbrot(c, true),
        "Value Noise" => EffectValueNoise(c),
        "FBM Noise" => EffectFbmNoise(c),
        "Tunnel" => EffectTunnel(layer, c),
        "Starfield" => EffectStarfield(layer, c),
        "Interference" => EffectInterference(c),
        "Metaballs" => EffectMetaballs(c),
        "Kaleidoscope" => EffectKaleidoscope(c),
        "Ripples" => EffectRipples(c),
        "Fire" => EffectFire(layer, c),
        "Matrix Rain" => EffectMatrixRain(layer, c),
        "XOR Pattern" => EffectXorPattern(c),
        "Moire" => EffectMoire(c),
        "Fireworks" => EffectFireworks(layer, c),
        "Radio Waves" => EffectRadioWaves(layer, c),
        "Rain Drops" => EffectRainDrops(layer, c),
        "Rotating Galaxy" => EffectGalaxy(layer, c),
        "Spinning Donut" => EffectSpinningDonutSdf(layer, c),
        "3D Shapes" => Effect3DShapesSdf(layer, c),
        "Spinning Shapes" => EffectSpinningShapes(layer, c),
        "Horizon" => EffectHorizon(layer, c),
        "Bouncing Balls" => EffectBouncingBalls(layer, c),
        "Cellular Automaton" => EffectCellularAutomaton(layer, c),
        "Wave Field" => EffectWaveField(layer, c),
        "Ocean Waves" => EffectOceanWaves(layer, c),
        "Ripple Tank" => EffectRippleTank(layer, c),
        "Oscilloscope" => EffectOscilloscope(layer, c),
        "Water Caustics" => EffectWaterCaustics(layer, c),
        "Aurora" => EffectAurora(layer, c),
        "3D Terrain" => EffectTerrain(layer, c),
        "SDF Lab" => EffectSdfLab(layer, c),
        "Flow Field" => EffectFlowField(layer, c),
        "Lightning" => EffectLightning(layer, c),
        "Black Hole" => EffectBlackHole(layer, c),
        "Strange Attractor" => EffectStrangeAttractor(layer, c),
        "Voronoi Cells" => EffectVoronoi(layer, c),
        "Snowstorm" => EffectSnowstorm(layer, c),
        "DNA Helix" => EffectDnaHelix(layer, c),
        "Warp Grid 3D" => EffectWarpGrid(layer, c),
        "Conway Life" => EffectConwayLife(layer, c),
        "Reaction-Diffusion" => EffectReactionDiffusion(layer, c),
        "Boids" => EffectBoids(layer, c),
        "N-Body Gravity" => EffectNBody(layer, c),
        "Falling Sand" => EffectFallingSand(layer, c),
        "Cloth Simulation" => EffectCloth(layer, c),
        "Volumetric Clouds" => EffectClouds(layer, c),
        "Procedural City" => EffectCity(layer, c),
        "Raymarch Lab" => EffectRaymarchLab(layer, c),
        _ => LegacyFallback(c)
    };

    private static double LegacyFallback(Ctx c)
        => Sat((Math.Sin(c.P.X * 6 + c.T + c.Seed * 0.00031) + Math.Sin(c.P.Y * 5 - c.T * .7) + Math.Sin((c.P.X + c.P.Y) * 4.5 + c.T) + 3) / 6);

    private static double EffectPlasma(Ctx c)
    {
        var q = c.P * (2.2 * c.Scale * c.Pulse);
        double z = Math.Sin(q.X * c.Fx + c.T * (1.5 + c.Dx * .30) + c.PhaseRad)
                 + Math.Sin(q.Y * c.Fy - c.T * (1.1 - c.Dy * .30) + c.PhaseRad * .7)
                 + Math.Sin((q.X + q.Y) * c.Fd + c.T * .8 - c.PhaseRad)
                 + Math.Cos(new Vec2(q.X + Math.Sin(c.T), q.Y + Math.Cos(c.T)).Length * c.Fr - c.T);
        return .5 + z * .125 * Math.Max(.05, Math.Abs(c.Amp));
    }

    private static double Mandelbrot(Ctx c, bool burning)
    {
        double breathe = 1.0 + (.55 + .35 * c.PulseParam) * (1.0 + Math.Sin(c.T + c.PhaseRad)) * Math.Max(.1, Math.Abs(c.Amp));
        double zoom = Math.Max(.25, breathe * Math.Max(.03, c.Scale) * Math.Max(.35, Math.Abs(c.Density)));
        double driftX = Math.Sin(c.T * .19) * c.Dx * .025;
        double driftY = Math.Cos(c.T * .17) * c.Dy * .025;
        double cx = (burning ? -.47 : -.74364388703) + driftX + c.P.X / ((burning ? 1.55 : 2.4) * zoom);
        double cy = (burning ? -.56 : .13182590421) + driftY + c.P.Y / ((burning ? 1.55 : 2.4) * zoom);
        double zr = 0, zi = 0;
        int maxIt = (int)Math.Clamp(c.Iterations, 4, 250);
        for (int i = 0; i < maxIt; i++)
        {
            if (burning) { zr = Math.Abs(zr); zi = Math.Abs(zi); }
            double nzr = zr * zr - zi * zi + cx;
            double nzi = 2 * zr * zi + cy;
            zr = nzr; zi = nzi;
            if (zr * zr + zi * zi > 16.0) return Frac(i / 12.0 + c.T * .045 * c.Tf);
        }
        return 0.0;
    }

    private static double EffectJulia(Ctx c)
    {
        double s = Math.Max(.03, c.Scale);
        var z = c.P / Math.Max(.2, 1.15 * s);
        double orbit = .06 + .075 * Math.Abs(c.Amp);
        double cx = -.745 + orbit * Math.Cos(c.T * .23 + c.PhaseRad) + Math.Sin(c.T * .17) * c.Dx * .02;
        double cy = .113 + orbit * Math.Sin(c.T * .19 + c.PhaseRad) + Math.Cos(c.T * .15) * c.Dy * .02;
        int maxIt = (int)Math.Clamp(c.Iterations, 4, 250);
        double zr = z.X, zi = z.Y;
        for (int i = 0; i < maxIt; i++)
        {
            double nzr = zr * zr - zi * zi + cx;
            double nzi = 2 * zr * zi + cy;
            zr = nzr; zi = nzi;
            if (zr * zr + zi * zi > 9.0) return Math.Pow(i / (double)maxIt, .45);
        }
        return 0.0;
    }

    private static double EffectValueNoise(Ctx c)
    {
        var flow = new Vec2(c.Dx, c.Dy) * (c.T * .55);
        var q = (c.P + flow) * new Vec2(c.Fx, c.Fy) * (8.0 * c.Scale) + new Vec2(c.T * .7 * c.Tf, -c.T * .4 * c.Tf);
        double v = VNoise(q, c.Seed);
        return .5 + (v - .5) * Math.Max(.05, Math.Abs(c.Density)) * c.Pulse;
    }

    private static double EffectFbmNoise(Ctx c)
    {
        var flow = new Vec2(c.Dx, c.Dy) * (c.T * .42);
        var q = (c.P + flow) * new Vec2(c.Fx, c.Fy) * (4.2 * c.Scale);
        double v = Fbm(q, c.T, c.Seed);
        return .5 + (v - .5) * (1.3 + Math.Abs(c.Turb) * .2) * Math.Max(.05, Math.Abs(c.Density)) * c.Pulse;
    }

    private static double EffectTunnel(SceneEffectLayer layer, Ctx c)
    {
        double rings = P(layer, "tunnel_rings"), twist = P(layer, "tunnel_twist"), depthP = P(layer, "tunnel_depth");
        double r = Math.Max(.035, c.P.Length / c.Scale);
        double a = Math.Atan2(c.P.Y, c.P.X) + Math.Sin(c.T * .21) * c.Dx * .45;
        double depth = Math.Max(.05, Math.Abs(depthP)) / r;
        return .5 + .25 * Math.Sin(depth * 2.7 * Math.Max(.05, Math.Abs(rings)) - c.T * (3.4 + c.Dy * .18))
                  + .25 * Math.Sin(a * 9.0 * twist + depth * .8 + c.T * 1.1);
    }

    private static double EffectStarfield(SceneEffectLayer layer, Ctx c)
    {
        double amount = P(layer, "star_amount"), size = P(layer, "star_size"), depthP = P(layer, "star_depth");
        double best = 0;
        int layers = (int)Math.Clamp(3.0 + Math.Abs(depthP) * 2.0, 2, 10);
        for (int i = 0; i < layers; i++)
        {
            double z = .1 + Frac(Hash11(i + c.Seed, c.Seed) + c.T * (.06 + i * .012)) * .9;
            var g = (c.P + new Vec2(c.Dx, c.Dy) * c.T * .08) * (z * 18.0 / Math.Max(.05, c.Scale * Math.Abs(depthP)));
            var ig = new Vec2(Math.Round(g.X), Math.Round(g.Y));
            double rnd = Hash21(ig + i * 37.7, c.Seed);
            double d = (g - ig).Length;
            double hit = StepFn(Math.Max(.80, 1.0 - .035 * Math.Abs(amount)), rnd);
            best += hit * Math.Max(0, 1.0 - d * (2.9 / Math.Max(.05, Math.Abs(size)))) * (1.0 - z * .55);
        }
        return best;
    }

    private static double EffectInterference(Ctx c)
    {
        var drift = new Vec2(Math.Sin(c.T * .23) * c.Dx, Math.Cos(c.T * .19) * c.Dy) * .22;
        var a = new Vec2(.6 * Math.Sin(c.T * .43), .55 * Math.Cos(c.T * .37)) + drift;
        var b = new Vec2(.65 * Math.Cos(c.T * .31 + 2.1), .45 * Math.Sin(c.T * .51)) + drift * .75;
        var cc = new Vec2(.35 * Math.Sin(c.T * .61 + 4.2), .70 * Math.Cos(c.T * .29)) - drift * .55;
        return .5 + (Math.Sin((c.P - a).Length * c.Fr - c.T * 1.4) + Math.Sin((c.P - b).Length * (c.Fr + 2.0) - c.T * 1.7) + Math.Sin((c.P - cc).Length * (c.Fr + 4.0) - c.T * 2.0)) / 6.0;
    }

    private static double EffectMetaballs(Ctx c)
    {
        double z = 0;
        int count = (int)Math.Clamp(3.0 + Math.Abs(c.Density) * 3.0, 1, 20);
        for (int i = 0; i < count; i++)
        {
            double fi = i, aa = c.T * (.23 + fi * .035) + fi * 1.7;
            var cc = new Vec2(Math.Sin(aa * (1.2 + fi * .04)) * (.35 + fi * .025), Math.Cos(aa * (1.55 - fi * .03)) * (.30 + fi * .022));
            var d = (c.P - cc) * c.Scale;
            z += .055 / (d.X * d.X + d.Y * d.Y + .025);
        }
        return (z - .28) * .55;
    }

    private static double EffectKaleidoscope(Ctx c)
    {
        double r = c.P.Length * c.Scale, a = Math.Atan2(c.P.Y, c.P.X), seg = Math.Max(2.0, Math.Round(Math.Abs(c.Fd)));
        double folded = Math.Abs(Frac(a / 6.2831853 * seg + .5) - .5) * 2.0;
        return .5 + .25 * (Math.Sin(r * c.Fr * 2.0 - c.T * 1.8) + Math.Cos(folded * 12.566 + r * 5.0 + c.T));
    }

    private static double EffectRipples(Ctx c)
    {
        var drift = new Vec2(Math.Sin(c.T * .18) * c.Dx, Math.Cos(c.T * .16) * c.Dy) * .24;
        double a = Math.Sin((c.P - (new Vec2(.45 * Math.Sin(c.T * .4), 0) + drift)).Length * c.Fr * c.Scale - c.T * 2.1);
        double b = Math.Sin((c.P + (new Vec2(.45 * Math.Cos(c.T * .33), -.2 * Math.Sin(c.T)) - drift * .7)).Length * (c.Fr * .82) * c.Scale + c.T * 1.6);
        return .5 + .25 * (a + b);
    }

    private static double EffectFire(SceneEffectLayer layer, Ctx c)
    {
        double heightP = P(layer, "fire_height"), widthP = P(layer, "fire_width"), windP = P(layer, "fire_wind");
        double particlesP = P(layer, "fire_particles"), particleSizeP = P(layer, "fire_particle_size"), liftP = P(layer, "fire_particle_lift");

        double hn = .5 + c.P.Y * .5;
        double wind = windP;
        double qx = c.P.X + wind * (1.0 - hn) * .55 + Math.Sin(c.T * .72) * wind * .035;
        double qy = c.P.Y;
        var flow = new Vec2(c.Dx, c.Dy) * (c.T * .42);
        double n = Fbm(new Vec2((qx + flow.X) * c.Fx * 2.0, (qy + flow.Y) * c.Fy * 3.0 - c.T * .75) * c.Scale, c.T, c.Seed);
        double width = Math.Max(.03, Math.Abs(widthP)) * (.20 + hn * .80);
        double mask = Sat(1.0 - Math.Abs(qx) / width);
        double basev = hn * (1.12 + .20 * Math.Abs(c.Density)) + (heightP - 1.0) * .38 - .30;
        double tongues = Math.Sin(qx * c.Fx + n * (4.0 + Math.Abs(c.Turb) * 2.0) + c.T * 1.7) * (.04 + .04 * Math.Abs(c.Amp));
        var sparkFlow = new Vec2(c.T * (.10 + c.Dx * .08), -c.T * (liftP * .22 - c.Dy * .06));
        var sp = (c.P + sparkFlow) * new Vec2(c.GridW, c.GridH) / Math.Max(1.0, Math.Abs(particleSizeP) * 2.0);
        double sparks = StepFn(1.0 - Sat(Math.Abs(particlesP) * .018), Hash21(new Vec2(Math.Floor(sp.X), Math.Floor(sp.Y)), c.Seed));
        double sparkMask = Sat(1.35 - Math.Abs(qx) / Math.Max(.10, width * 2.6));
        double v = (basev + (n - .5) * (.35 + .20 * Math.Abs(c.Turb)) + tongues) * mask * c.Pulse;
        v = Math.Max(v, sparks * sparkMask * Sat((1.0 - hn) * 1.25) * .85);
        return v;
    }

    private static double EffectMatrixRain(SceneEffectLayer layer, Ctx c)
    {
        double spacingP = P(layer, "matrix_spacing"), trailP = P(layer, "matrix_trail"), headP = P(layer, "matrix_head");
        double spacing = Math.Max(1.0, Math.Round(Math.Abs(spacingP)));
        double colX = Math.Floor(c.Cell.X);
        if (Mod(colX, spacing) > .1) return 0.0;
        double col = colX;
        double speed = 3.5 + Hash11(col, c.Seed) * 8.0;
        double trail = (5.0 + Hash11(col + 11.0, c.Seed) * c.GridH * .45) * Math.Max(.05, Math.Abs(trailP));
        double head = Mod(c.T * speed + Hash11(col + 23.0, c.Seed) * c.GridH, c.GridH + trail) - trail;
        double d = head - c.Cell.Y;
        if (d >= -trail && d <= 0.0) return Sat(1.0 + d / Math.Max(.001, trail)) * Math.Max(.1, Math.Abs(headP));
        return 0.0;
    }

    private static double EffectXorPattern(Ctx c)
    {
        int x = (int)c.Cell.X, y = (int)c.Cell.Y;
        int k = (int)(c.T * 6.0 * c.Tf + c.PhaseDeg);
        int a = (x * Math.Max(1, (int)Math.Abs(c.Fx))) ^ (y * Math.Max(1, (int)Math.Abs(c.Fy))) ^ k;
        int b = (x + k) & (y * Math.Max(1, (int)(Math.Abs(c.Fd) + 1.0)) + k * 2);
        int mask = (int)Math.Clamp(15.0 + Math.Abs(c.Density) * 24.0, 3, 255);
        return ((a + b) & mask) / (double)mask;
    }

    private static double EffectMoire(Ctx c)
    {
        var q = c.P * new Vec2(c.Fx, c.Fy) * c.Scale;
        double a = Math.Sin(q.X + Math.Sin(c.T * .5) * q.Y);
        double b = Math.Sin(q.X * Math.Cos(c.T * .17) + q.Y * Math.Sin(c.T * .17) + c.T * 1.2);
        double cc = Math.Cos(q.Length * .85 - c.T);
        return .5 + (a + b + cc) / 6.0;
    }

    private static double EffectFireworks(SceneEffectLayer layer, Ctx c)
    {
        double countP = P(layer, "firework_count"), sizeP = P(layer, "firework_size"), sparksP = P(layer, "firework_sparks");
        double gravP = P(layer, "firework_gravity"), decayP = P(layer, "firework_decay");
        double total = 0;
        int bursts = (int)Math.Clamp(Math.Abs(countP), 1, 20);
        int sparks = (int)Math.Clamp(Math.Abs(sparksP), 4, 80);
        for (int i = 0; i < bursts; i++)
        {
            double fi = i;
            double cycle = 1.9 + Hash11(fi + 4.0, c.Seed) * 2.4;
            double age = Frac(c.T / cycle + Hash11(fi + 5.0, c.Seed));
            var center = new Vec2(Hash11(fi + 6.0, c.Seed) * 1.65 - .825 + c.Dx * .08, Hash11(fi + 7.0, c.Seed) * .75 - .62 + c.Dy * .05);
            const double launch = .24;
            if (age < launch)
            {
                double q = age / launch;
                double ease = 1.0 - Math.Pow(1.0 - q, 1.7);
                double sy = Mix(.96, center.Y, ease);
                double rr = .025 + .012 * Math.Abs(sizeP);
                total = Math.Max(total, Math.Max(0.0, 1.0 - (c.P - new Vec2(center.X, sy)).Length * c.Scale / rr));
                for (int tr = 1; tr <= 3; tr++)
                {
                    double qt = Math.Max(0.0, q - tr * .055);
                    double sty = Mix(.96, center.Y, 1.0 - Math.Pow(1.0 - qt, 1.7));
                    double glow = Math.Max(0.0, 1.0 - (c.P - new Vec2(center.X, sty)).Length * c.Scale / (rr * .8));
                    total = Math.Max(total, glow * (.55 - tr * .11));
                }
            }
            else
            {
                double a = (age - launch) / (1.0 - launch);
                double fade = Math.Pow(Math.Max(0.0, 1.0 - a), Math.Max(.05, Math.Abs(decayP)));
                double core = Math.Max(0.0, 1.0 - (c.P - center).Length * c.Scale / (.045 + .025 * Math.Abs(sizeP))) * (1.0 - SmoothStep(0.0, .13, a));
                total += core * 1.4;
                for (int j = 0; j < sparks; j++)
                {
                    double fj = j;
                    double jitter = (Hash11(fi * 97.0 + fj * 3.17, c.Seed) - .5) * .28;
                    double ang = 6.2831853 * (fj / Math.Max(1, sparks)) + jitter + Hash11(fi + 51.0, c.Seed) * 6.2831853;
                    double velocity = .72 + Hash11(fi * 131.0 + fj + 71.0, c.Seed) * .62;
                    double radius = a * (.28 + .44 * Math.Abs(sizeP)) * velocity;
                    var pos = center + new Vec2(Math.Cos(ang), Math.Sin(ang)) * radius;
                    pos += new Vec2(0, Math.Max(0.0, gravP) * a * a * .28);
                    double ps = (.016 + .012 * Math.Abs(sizeP)) * (1.0 - .30 * a);
                    double spark = Math.Max(0.0, 1.0 - (c.P - pos).Length * c.Scale / Math.Max(.006, ps));
                    double twinkle = .72 + .28 * Math.Sin(c.T * 13.0 + fj * 2.31 + fi * 1.7);
                    total += spark * fade * twinkle;
                }
            }
        }
        return total * (.72 + .28 * Math.Abs(c.Amp));
    }

    private static double EffectRadioWaves(SceneEffectLayer layer, Ctx c)
    {
        double thicknessP = P(layer, "radio_thickness"), decayP = P(layer, "radio_decay"), expandP = P(layer, "radio_expand");
        var cc = new Vec2(Math.Sin(c.T * .37) * .28 * c.Dx, Math.Cos(c.T * .31) * .22 * c.Dy);
        double r = (c.P - cc).Length * c.Scale;
        double wavelength = Math.Max(.03, 4.0 / Math.Max(.1, Math.Abs(c.Fr)));
        double wave = .5 + .5 * Math.Cos((r - c.T * .55 * expandP) / wavelength * 6.2831853);
        double band = Math.Max(0.0, 1.0 - Math.Abs(wave - 1.0) / Math.Max(.005, Math.Abs(thicknessP)));
        return band / (1.0 + r * Math.Max(0.0, decayP)) * Math.Max(.2, Math.Abs(c.Amp)) * c.Pulse;
    }

    private static double EffectRainDrops(SceneEffectLayer layer, Ctx c)
    {
        double countP = P(layer, "rain_count"), ringSizeP = P(layer, "rain_ring_size"), ringWidthP = P(layer, "rain_ring_width"), decayP = P(layer, "rain_decay");
        double total = 0;
        int count = (int)Math.Clamp(Math.Abs(countP), 1, 30);
        double ringw = Math.Max(.002, Math.Abs(ringWidthP));
        for (int i = 0; i < count; i++)
        {
            double fi = i;
            var cc = new Vec2(Hash11(fi + 17.0, c.Seed) * 2.4 - 1.2, Hash11(fi + 29.0, c.Seed) * 1.8 - .9);
            double age = Frac(c.T * .18 + Hash11(fi + 41.0, c.Seed) * 3.0);
            double radius = age * (1.15 + .55 * Math.Abs(c.Scale)) * Math.Max(.05, Math.Abs(ringSizeP));
            double d = (c.P - cc).Length * c.Scale;
            double edge = Math.Max(0.0, 1.0 - Math.Abs(d - radius) / ringw);
            double fade = Math.Pow(1.0 - age, Math.Max(.05, Math.Abs(decayP)));
            total += edge * fade;
        }
        return total * (.55 + .25 * Math.Abs(c.Amp));
    }

    private static double EffectGalaxy(SceneEffectLayer layer, Ctx c)
    {
        double armsP = P(layer, "galaxy_arms"), coreP = P(layer, "galaxy_core"), coreBrightP = P(layer, "galaxy_core_brightness");
        double twistP = P(layer, "galaxy_twist"), armWidthP = P(layer, "galaxy_arm_width"), radiusP = P(layer, "galaxy_radius");
        double ellipP = P(layer, "galaxy_ellipticity"), baseRotP = P(layer, "galaxy_base_rotation"), rotP = P(layer, "galaxy_rotation");
        double haloP = P(layer, "galaxy_halo"), starDensP = P(layer, "galaxy_star_density"), dustP = P(layer, "galaxy_dust");

        double flatten = Math.Max(.18, Math.Abs(ellipP));
        var gp = new Vec2(c.P.X, c.P.Y / flatten) * c.Scale;
        double r = gp.Length, a = Math.Atan2(gp.Y, gp.X) - baseRotP * Math.PI / 180.0 - c.T * rotP;
        double arms = Math.Max(1.0, Math.Round(Math.Abs(armsP)));
        double spiralRaw = .5 + .5 * Math.Cos(a * arms - r * (3.0 + twistP) + c.PhaseRad);
        double armPower = 1.0 / Math.Max(.12, Math.Abs(armWidthP));
        double spiral = Math.Pow(Math.Clamp(spiralRaw, 0.0, 1.0), armPower);
        double dustNoise = Mix(1.0, .42 + .9 * Fbm(gp * (3.2 + dustP * 2.0) + new Vec2(c.T * .03, -c.T * .02), c.T, c.Seed), Math.Clamp(dustP * .5, 0.0, 1.0));
        spiral *= dustNoise;
        double core = Math.Exp(-r / Math.Max(.018, Math.Abs(coreP))) * Math.Max(0.0, coreBrightP);
        double radius = Math.Max(.12, Math.Abs(radiusP));
        double disk = 1.0 - SmoothStep(radius * .72, radius, r);
        double halo = Math.Exp(-r / Math.Max(.08, radius * .82)) * Math.Max(0.0, haloP) * .32;
        double starDensity = Math.Max(0.0, starDensP);
        var sv = (c.P + 2.0) * new Vec2(c.GridW, c.GridH) * (.23 + .08 * starDensity);
        double starNoise = Hash21(new Vec2(Math.Floor(sv.X), Math.Floor(sv.Y)), c.Seed);
        double stars = SmoothStep(.995 - .045 * Math.Clamp(starDensity, 0.0, 3.0), .9995, starNoise) * (.35 + .4 * starDensity) * Math.Clamp(starDensity, 0.0, 3.0);
        return (spiral * .72 + core) * disk + halo + stars;
    }

    private static double EffectSpinningShapes(SceneEffectLayer layer, Ctx c)
    {
        int shapeMode = EffectRegistry.ShapeId(layer.ShapeMode);
        double a = c.T * .75 + c.PhaseRad;
        var q = RotateGlsl(c.P, a) / Math.Max(.15, c.Scale);
        double d;
        if (shapeMode == 1) d = Math.Abs(Math.Abs(q.X) + Math.Abs(q.Y) - .72);
        else if (shapeMode == 2)
        {
            double an = Math.Atan2(q.Y, q.X), rr = q.Length;
            double rad = .62 + .18 * Math.Cos(5.0 * an);
            d = Math.Abs(rr - rad);
        }
        else if (shapeMode == 3) d = Math.Abs(Math.Max(Math.Abs(q.X) * .866 + Math.Abs(q.Y) * .5, Math.Abs(q.Y)) - .62);
        else if (shapeMode == 4) d = Math.Abs(Math.Min(Math.Max(Math.Abs(q.X) - .18, Math.Abs(q.Y) - .72), Math.Max(Math.Abs(q.X) - .72, Math.Abs(q.Y) - .18)));
        else d = Math.Abs(Math.Max(Math.Abs(q.X), Math.Abs(q.Y)) - .62);
        double thick = .045 + .035 * Math.Max(.1, Math.Abs(c.Density));
        return Sat(1.0 - d / thick) * c.Pulse;
    }

    private static double EffectHorizon(SceneEffectLayer layer, Ctx c)
    {
        double heightP = P(layer, "horizon_height"), fovP = P(layer, "horizon_fov"), waveP = P(layer, "horizon_wave");
        double h = Math.Clamp(heightP, -.65, .55);
        double groundY = c.P.Y - h;
        double horizonLine = 1.0 - SmoothStep(.012, .055, Math.Abs(groundY));
        if (groundY < 0.0)
        {
            double star = StepFn(.9965, Hash21(new Vec2(Math.Floor(c.Cell.X + c.Seed % 31), Math.Floor(c.Cell.Y)), c.Seed));
            double skyFade = Sat(-groundY * .55);
            return Math.Max(star * .65 * skyFade, horizonLine * .88);
        }
        double q = Math.Max(.018, groundY);
        double fov = Math.Max(.12, Math.Abs(fovP));
        double depth = Math.Min(60.0, fov / q);
        double bend = Math.Sin(depth * .20 + c.T * .32 + c.P.X * 2.7) * waveP * .014;
        double worldX = (c.P.X + bend) * depth;
        double worldZ = depth + c.T * (.75 + .025 * Math.Abs(c.Fy));
        double gx = PeriodicLine(worldX * Math.Max(.12, Math.Abs(c.Fx)) * .105, .050);
        double gz = PeriodicLine(worldZ * Math.Max(.12, Math.Abs(c.Fy)) * .070, .042);
        double nearFade = SmoothStep(.045, .16, q);
        double distanceFade = .58 + .42 * Sat(q * 1.2);
        double grid = Math.Max(gx, gz) * nearFade * distanceFade;
        double guide = (1.0 - SmoothStep(.018, .055, Math.Abs(c.P.X))) * SmoothStep(.10, .32, q) * .28 * Sat(c.Density);
        return Math.Max(Math.Max(grid, guide), horizonLine * .92);
    }

    private static double EffectBouncingBalls(SceneEffectLayer layer, Ctx c)
    {
        double countP = P(layer, "ball_count"), radiusP = P(layer, "ball_radius"), gravP = P(layer, "ball_gravity");
        double bounceP = P(layer, "ball_bounce"), speedP = P(layer, "ball_speed"), trailsP = P(layer, "ball_trails");
        double best = 0;
        int count = (int)Math.Clamp(Math.Abs(countP), 1, 64);
        for (int i = 0; i < count; i++)
        {
            double fi = i;
            double hx = Hash11(fi + 7.1, c.Seed), hy = Hash11(fi + 19.7, c.Seed);
            double sp = speedP * (.32 + .55 * Hash11(fi + 31.3, c.Seed));
            double xp = Tri(c.T * sp + hx) * 2.0 - 1.0;
            double q = Frac(c.T * sp * .73 + hy);
            double arch = 4.0 * q * (1.0 - q);
            double yp = .82 - arch * (.65 + .35 * bounceP) * Math.Pow(Math.Max(.05, Math.Abs(gravP)), .35);
            double rr = Math.Max(.005, Math.Abs(radiusP) * (.65 + .7 * Hash11(fi + 53.2, c.Seed)));
            best = Math.Max(best, Sat(1.0 - (c.P - new Vec2(xp, yp)).Length / rr));
            if (trailsP > .001)
            {
                double xp2 = Tri((c.T - .08 * trailsP) * sp + hx) * 2.0 - 1.0;
                double q2 = Frac((c.T - .08 * trailsP) * sp * .73 + hy);
                double yp2 = .82 - 4.0 * q2 * (1.0 - q2) * (.65 + .35 * bounceP);
                best = Math.Max(best, Sat(1.0 - (c.P - new Vec2(xp2, yp2)).Length / (rr * .8)) * trailsP * .55);
            }
        }
        return best;
    }

    private static int CaSeedState(int x, int block, double seedDensity, int gridW, int seed)
    {
        double density = Math.Clamp(seedDensity, 0, 1);
        if (density <= .001) return x == gridW / 2 ? 1 : 0;
        double r = Hash21(new Vec2(x + block * 17.31, block * 41.73 + 13.7), seed);
        return r < density ? 1 : 0;
    }

    private static double EffectCellularAutomaton(SceneEffectLayer layer, Ctx c)
    {
        double rule0 = P(layer, "ca_rule"), stepRate = P(layer, "ca_step_rate"), historyP = P(layer, "ca_history");
        double seedDensity = P(layer, "ca_seed_density"), aliveP = P(layer, "ca_alive"), scrollP = P(layer, "ca_scroll");

        int cx = (int)Math.Floor(c.Cell.X), cy = (int)Math.Floor(c.Cell.Y);
        int history = (int)Math.Clamp(Math.Round(Math.Abs(historyP)), 4, 32);
        int scrollSteps = (int)Math.Floor(c.T * Math.Max(0, stepRate) * Math.Max(0, scrollP));
        int globalRow = cy + scrollSteps;
        int block = globalRow / history;
        int gen = globalRow - block * history;
        if (gen < 0) { gen += history; block -= 1; }
        int width = gen * 2 + 1;
        int baseX = cx - gen;
        var state = new int[65];
        for (int i = 0; i < 65; i++) state[i] = i < width ? CaSeedState(baseX + i, block, seedDensity, c.GridW, c.Seed) : 0;
        int rule = (int)Math.Clamp(Math.Round(Math.Abs(rule0)), 0, 255);
        for (int step = 0; step < 32; step++)
        {
            if (step >= gen) break;
            int nextWidth = width - 2;
            for (int j = 0; j < 63; j++)
            {
                if (j >= nextWidth) break;
                int n = state[j] * 4 + state[j + 1] * 2 + state[j + 2];
                state[j] = (rule >> n) & 1;
            }
            width = nextWidth;
        }
        double alive = state[0];
        double shade = .72 + .28 * (1.0 - gen / Math.Max(1.0, (double)history));
        return Sat(alive * Math.Max(.05, aliveP) * shade);
    }

    private static double EffectWaveField(SceneEffectLayer layer, Ctx c)
    {
        double countP = P(layer, "wave_count"), lengthP = P(layer, "wave_length"), dirP = P(layer, "wave_direction");
        double spreadP = P(layer, "wave_spread"), sharpP = P(layer, "wave_sharpness"), heightP = P(layer, "wave_height");
        int count = (int)Math.Clamp(Math.Round(Math.Abs(countP)), 1, 8);
        double sum = 0, baseK = 6.2831853 / Math.Max(.06, Math.Abs(lengthP));
        double center = (count - 1.0) * .5;
        for (int i = 0; i < count; i++)
        {
            double fi = i;
            double ang = (dirP + (fi - center) * spreadP) * Math.PI / 180.0;
            var dir = new Vec2(Math.Cos(ang), Math.Sin(ang));
            double phase = c.P.Dot(dir) * baseK * (1.0 + fi * .075) - c.T * (.85 + fi * .09);
            double wv = Math.Sin(phase);
            double shaped = Sign(wv) * Math.Pow(Math.Abs(wv), 1.0 / Math.Max(.15, Math.Abs(sharpP)));
            sum += shaped;
        }
        double z = sum / Math.Max(1.0, (double)count);
        return .5 + .48 * z * Math.Clamp(Math.Abs(heightP), 0.0, 2.5);
    }

    private static double EffectOceanWaves(SceneEffectLayer layer, Ctx c)
    {
        double heightP = P(layer, "ocean_height"), lengthP = P(layer, "ocean_length"), layersP = P(layer, "ocean_layers");
        double dirP = P(layer, "ocean_direction"), choppyP = P(layer, "ocean_choppiness"), foamP = P(layer, "ocean_foam");
        int layers = (int)Math.Clamp(Math.Round(Math.Abs(layersP)), 1, 8);
        double sum = 0, norm = 0;
        double baseK = 6.2831853 / Math.Max(.08, Math.Abs(lengthP));
        for (int i = 0; i < layers; i++)
        {
            double fi = i, amp = Math.Pow(.56, fi);
            double angle = dirP * Math.PI / 180.0 + (Hash11(fi + 31.0, c.Seed) - .5) * (.45 + .35 * choppyP) + fi * .19;
            var dir = new Vec2(Math.Cos(angle), Math.Sin(angle));
            double k = baseK * Math.Pow(1.72, fi);
            double distort = Math.Sin(c.P.Dot(new Vec2(-dir.Y, dir.X)) * k * .22 + c.T * (.28 + fi * .05)) * choppyP * .32;
            double phase = c.P.Dot(dir) * k + distort - c.T * (.62 + Math.Sqrt(k) * .085 + fi * .035);
            sum += Math.Sin(phase) * amp; norm += amp;
        }
        double sea = sum / Math.Max(.001, norm);
        double crest = SmoothStep(.42, .86, sea) * Math.Max(0.0, foamP);
        var np = c.P * 18.0 + new Vec2(c.T * .24, -c.T * .18);
        double micro = (VNoise(np, c.Seed) - .5) * .12 * choppyP;
        return .5 + sea * .40 * Math.Abs(heightP) + micro + crest * .40;
    }

    private static double EffectRippleTank(SceneEffectLayer layer, Ctx c)
    {
        double sourcesP = P(layer, "tank_sources"), freqP = P(layer, "tank_frequency"), speedP = P(layer, "tank_speed");
        double dampP = P(layer, "tank_damping"), motionP = P(layer, "tank_motion"), interfP = P(layer, "tank_interference");
        int count = (int)Math.Clamp(Math.Round(Math.Abs(sourcesP)), 1, 12);
        double sum = 0;
        for (int i = 0; i < count; i++)
        {
            double fi = i;
            var basePos = new Vec2(Hash11(fi + 11.0, c.Seed) * 1.7 - .85, Hash11(fi + 37.0, c.Seed) * 1.45 - .72);
            var motion = new Vec2(Math.Sin(c.T * (.19 + fi * .013) + fi * 1.7), Math.Cos(c.T * (.17 + fi * .011) + fi * 2.3)) * (motionP * .16);
            double r = (c.P - (basePos + motion)).Length;
            double wave = Math.Sin(r * Math.Max(.1, freqP) - c.T * Math.Max(.01, speedP) * 2.4 + fi * .73);
            double fade = Math.Exp(-r * Math.Max(0.0, dampP));
            sum += wave * fade;
        }
        double z = sum / Math.Max(1.0, Math.Sqrt(count));
        return .5 + .44 * z * Math.Max(0.0, interfP);
    }

    private static double WaveShape(double x, int mode, double mix1, double mix2, double mix2freq, double mix2phase, double mix3, double mix3freq, double mix3phase)
    {
        double s = Math.Sin(x);
        if (mode == 1) return s >= 0.0 ? 1.0 : -1.0;
        if (mode == 2) return (2.0 / 3.14159265) * Math.Asin(s);
        if (mode == 3) return Frac(x / 6.2831853) * 2.0 - 1.0;
        if (mode == 4)
        {
            double a = mix1 * Math.Sin(x);
            double b = mix2 * Math.Sin(x * Math.Max(.01, Math.Abs(mix2freq)) + mix2phase * Math.PI / 180.0);
            double cc = mix3 * Math.Sin(x * Math.Max(.01, Math.Abs(mix3freq)) + mix3phase * Math.PI / 180.0);
            double norm = Math.Max(.15, Math.Abs(mix1) + Math.Abs(mix2) + Math.Abs(mix3));
            return Math.Clamp((a + b + cc) / norm, -1.0, 1.0);
        }
        return s;
    }

    private static double EffectOscilloscope(SceneEffectLayer layer, Ctx c)
    {
        double waveformP = P(layer, "scope_waveform"), freqP = P(layer, "scope_frequency"), ampP = P(layer, "scope_amplitude");
        double thickP = P(layer, "scope_thickness"), dualP = P(layer, "scope_dual"), phaseP = P(layer, "scope_phase");
        double mix1 = P(layer, "scope_mix1"), mix2 = P(layer, "scope_mix2"), mix2freq = P(layer, "scope_mix2_freq"), mix2phase = P(layer, "scope_mix2_phase");
        double mix3 = P(layer, "scope_mix3"), mix3freq = P(layer, "scope_mix3_freq"), mix3phase = P(layer, "scope_mix3_phase");

        double qx = (c.Cell.X / c.GridW - .5) * 2.0;
        double qy = (c.Cell.Y / c.GridH - .5) * 2.0;
        double x = qx, freq = Math.Max(.05, Math.Abs(freqP));
        int mode = (int)Math.Clamp(Math.Round(Math.Abs(waveformP)), 0, 4);
        double amp = Math.Clamp(Math.Abs(ampP), .01, .98);
        double thick = Math.Max(.002, Math.Abs(thickP));
        const double aaY = .004, aaX = .003;

        double phase1 = x * 3.14159265 * freq + c.T * 2.0;
        double y1 = WaveShape(phase1, mode, mix1, mix2, mix2freq, mix2phase, mix3, mix3freq, mix3phase) * amp;
        double line1 = 1.0 - SmoothStep(thick, thick + aaY, Math.Abs(qy - y1));
        if (mode == 1 || mode == 3)
        {
            double cyc = Frac(phase1 / 6.2831853);
            double phaseDist = mode == 1 ? Math.Min(Math.Min(cyc, 1.0 - cyc), Math.Abs(cyc - .5)) * 6.2831853 : Math.Min(cyc, 1.0 - cyc) * 6.2831853;
            double xDist = phaseDist / Math.Max(.0001, 3.14159265 * freq);
            double vertical = (1.0 - SmoothStep(thick * .7, thick * .7 + aaX, xDist)) * (1.0 - SmoothStep(amp + thick, amp + thick + aaY, Math.Abs(qy)));
            line1 = Math.Max(line1, vertical);
        }
        double line2 = 0;
        if (dualP > .001)
        {
            double freq2 = freq * .73;
            double amp2 = Math.Clamp(amp * .82, .01, .98);
            double phase2 = x * 3.14159265 * freq2 - c.T * 1.45 + phaseP * Math.PI / 180.0;
            double y2 = WaveShape(phase2, mode, mix1, mix2, mix2freq, mix2phase, mix3, mix3freq, mix3phase) * amp2;
            line2 = 1.0 - SmoothStep(thick, thick + aaY, Math.Abs(qy - y2));
            if (mode == 1 || mode == 3)
            {
                double cyc2 = Frac(phase2 / 6.2831853);
                double phaseDist2 = mode == 1 ? Math.Min(Math.Min(cyc2, 1.0 - cyc2), Math.Abs(cyc2 - .5)) * 6.2831853 : Math.Min(cyc2, 1.0 - cyc2) * 6.2831853;
                double xDist2 = phaseDist2 / Math.Max(.0001, 3.14159265 * freq2);
                double vertical2 = (1.0 - SmoothStep(thick * .7, thick * .7 + aaX, xDist2)) * (1.0 - SmoothStep(amp2 + thick, amp2 + thick + aaY, Math.Abs(qy)));
                line2 = Math.Max(line2, vertical2);
            }
            line2 *= Math.Clamp(dualP, 0.0, 1.0);
        }
        double grid = .10 * (PeriodicLine((x + 1.0) * 5.0, .025) + PeriodicLine((qy + 1.0) * 4.0, .025));
        return Math.Max(Math.Max(line1, line2), grid);
    }

    private static double EffectWaterCaustics(SceneEffectLayer layer, Ctx c)
    {
        double scaleP = P(layer, "caustic_scale"), distortP = P(layer, "caustic_distortion"), speedP = P(layer, "caustic_speed");
        double sharpP = P(layer, "caustic_sharpness"), layersP = P(layer, "caustic_layers");
        int layers = (int)Math.Clamp(Math.Round(Math.Abs(layersP)), 1, 6);
        var q = c.P * Math.Max(.05, Math.Abs(scaleP));
        double total = 0;
        for (int i = 0; i < layers; i++)
        {
            double fi = i;
            var dir = new Vec2(Math.Cos(fi * 1.71 + .4), Math.Sin(fi * 1.71 + .4));
            double warp = Math.Sin(q.Dot(new Vec2(-dir.Y, dir.X)) * (1.2 + fi * .31) + c.T * speedP * (.65 + fi * .07)) * distortP * .35;
            double a = Math.Sin(q.Dot(dir) * (2.2 + fi * .58) + warp + c.T * speedP * (.8 + fi * .09));
            double line = Math.Pow(Math.Max(0.0, 1.0 - Math.Abs(a)), Math.Max(.15, Math.Abs(sharpP)));
            total += line;
        }
        return Math.Pow(total / Math.Max(1.0, (double)layers), .72);
    }

    private static double EffectAurora(SceneEffectLayer layer, Ctx c)
    {
        double bandsP = P(layer, "aurora_bands"), widthP = P(layer, "aurora_width"), flowP = P(layer, "aurora_flow");
        double curlP = P(layer, "aurora_curl"), shimmerP = P(layer, "aurora_shimmer"), heightP = P(layer, "aurora_height");
        double bands = Math.Max(1.0, Math.Round(Math.Abs(bandsP)));
        double height = Math.Max(.05, Math.Abs(heightP));
        double yfade = 1.0 - SmoothStep(.15, 1.05, Math.Abs(c.P.Y + .18) / height);
        double n = Fbm(new Vec2(c.P.X * 1.5 + c.T * flowP * .12, c.P.Y * 2.1 - c.T * flowP * .08), c.T, c.Seed);
        double bend = Math.Sin(c.P.Y * (2.2 + curlP) + c.T * flowP + n * 2.4) * curlP * .12;
        double stripe = .5 + .5 * Math.Cos((c.P.X + bend + n * .10) * bands * 3.14159265);
        double curtain = Math.Pow(stripe, Math.Max(.35, 2.2 / Math.Max(.03, Math.Abs(widthP) * 5.0)));
        var sp = c.P * 22.0 + new Vec2(c.T * 1.2, -c.T * .5);
        double shimmer = 1.0 + (VNoise(sp, c.Seed) - .5) * shimmerP;
        return curtain * yfade * shimmer * (.62 + .38 * n);
    }

    private static double TerrainHeightF(Vec2 xz, double detailP, double heightP, double speedP, double t, int seed)
    {
        double d = Math.Max(.15, detailP);
        double n = Fbm(xz * (1.2 * d) + new Vec2(0, t * speedP * .22), t, seed);
        return (n - .48) * heightP;
    }

    private static double EffectTerrain(SceneEffectLayer layer, Ctx c)
    {
        double heightP = P(layer, "terrain_height"), detailP = P(layer, "terrain_detail"), speedP = P(layer, "terrain_speed");
        double cameraP = P(layer, "terrain_camera"), yawP = P(layer, "terrain_yaw"), pitchP = P(layer, "terrain_pitch");
        double zoomP = P(layer, "terrain_zoom"), panX = P(layer, "terrain_pan_x"), panY = P(layer, "terrain_pan_y");
        double waterP = P(layer, "terrain_water"), gridP = P(layer, "terrain_grid");

        var p = c.P / Math.Max(.2, c.Scale) - new Vec2(panX, panY);
        double pitch = pitchP * Math.PI / 180.0, yaw = yawP * Math.PI / 180.0;
        double hy = -.1 + cameraP * .12 + Math.Sin(pitch) * .34;
        double zoom = Math.Max(.55, zoomP);
        double best = 0, prev = -2.0;
        for (int i = 1; i < 72; i++)
        {
            double z = (i / 14.0 + .12) * zoom;
            double x = p.X * z * 1.35;
            var world = RotateGlsl(new Vec2(x, z + c.T * speedP * .35), yaw);
            double h = TerrainHeightF(world, detailP, heightP, speedP, c.T, c.Seed);
            double sy = hy + h / z * 1.65 + .42 / z - .25;
            double line = 1.0 - SmoothStep(.012, .035, Math.Abs(p.Y - sy));
            if (sy > prev) { best = Math.Max(best, line); prev = Math.Max(prev, sy); }
            if (h < waterP)
            {
                double waterLine = 1.0 - SmoothStep(.012, .03, Math.Abs(p.Y - (hy + waterP / z * 1.65 + .42 / z - .25)));
                best = Math.Max(best, waterLine * .35);
            }
        }
        double grid = 0;
        if (gridP > .001) grid = PeriodicLine(p.X * 12.0 / Math.Max(.15, p.Y + 1.25), .035) * gridP * .18;
        return Sat(Math.Max(best, grid));
    }

    private static double EffectFlowField(SceneEffectLayer layer, Ctx c)
    {
        double particlesP = P(layer, "flow_particles"), scaleP = P(layer, "flow_scale"), strengthP = P(layer, "flow_strength");
        double trailsP = P(layer, "flow_trails"), curlP = P(layer, "flow_curl"), speedP = P(layer, "flow_speed");
        double total = 0;
        int count = (int)Math.Clamp(Math.Round(particlesP), 4, 80);
        for (int i = 0; i < count; i++)
        {
            double fi = i;
            double sx = Hash11(fi * 7.1 + 3.0, c.Seed) * 2.0 - 1.0;
            double sy = Hash11(fi * 11.7 + 9.0, c.Seed) * 2.0 - 1.0;
            sx += c.T * speedP * .08;
            sx = Mod(sx + 1.0, 2.0) - 1.0;
            sy = Mod(sy + 1.0, 2.0) - 1.0;
            var q = new Vec2(sx, sy);
            double best = 10.0;
            int maxJ = (int)(6.0 * trailsP);
            for (int j = 0; j < 18 && j < maxJ; j++)
            {
                double ang = Fbm(q * scaleP + c.T * .03, c.T, c.Seed) * 6.2831 * curlP + Math.Sin(q.Y * 2.3) * strengthP;
                var vel = new Vec2(Math.Cos(ang), Math.Sin(ang)) * .055;
                best = Math.Min(best, (c.P - q).Length);
                q += vel;
            }
            total += Math.Exp(-best * 95.0);
        }
        return Sat(total * .75);
    }

    private static double LightningPath(double y, double seed, double jitter, int rngSeed)
    {
        double segments = 12.0 + Math.Clamp(jitter, 0.0, 3.0) * 5.0;
        double sy = Math.Clamp(y, 0.0, 1.0) * segments;
        double cell = Math.Floor(sy), f = sy - cell;
        double a = Hash11(seed + cell * 13.73, rngSeed) - .5;
        double b = Hash11(seed + (cell + 1.0) * 13.73, rngSeed) - .5;
        double coarse = Math.Sin(y * 3.14159265 + seed * .17) * (.05 + .05 * jitter) * (Hash11(seed + 91.3, rngSeed) - .5);
        return Mix(a, b, f) * (.10 + .10 * jitter) + coarse;
    }

    private static double LightningBolt(Vec2 p, double seed, double center, double width, double jitter, double forkAmount, int branchCount, double flash, int rngSeed)
    {
        double y = (p.Y + 1.0) * .5;
        if (y < 0.0 || y > 1.0) return 0.0;
        double path = center + LightningPath(y, seed, jitter, rngSeed);
        double d = Math.Abs(p.X - path);
        double main = 1.0 - SmoothStep(width, width * 2.7, d);
        double forks = 0.0;
        for (int i = 0; i < branchCount; i++)
        {
            double fi = i;
            double start = .08 + Hash11(seed + fi * 5.17, rngSeed) * .72;
            double len = .10 + Hash11(seed + fi * 8.31, rngSeed) * .24;
            double local = (y - start) / len;
            if (local >= 0.0 && local <= 1.0)
            {
                double source = center + LightningPath(start, seed, jitter, rngSeed);
                double side = Hash11(seed + fi * 9.71, rngSeed) > .5 ? 1.0 : -1.0;
                double reach = (.14 + .30 * Hash11(seed + fi * 3.43, rngSeed)) * Math.Max(.05, forkAmount);
                double branchJitter = LightningPath(local, seed + fi * 27.1 + 3.7, jitter * .62, rngSeed) * (.48 * (1.0 - local) + .10);
                double branchX = source + side * local * reach + branchJitter;
                double branchWidth = Math.Max(.002, width * Mix(.72, .30, local));
                double bd = Math.Abs(p.X - branchX);
                double branch = (1.0 - SmoothStep(branchWidth, branchWidth * 2.5, bd)) * Math.Pow(1.0 - local, .28);
                forks = Math.Max(forks, branch);
            }
        }
        double halo = Math.Exp(-d * (6.0 + 8.0 / Math.Max(.2, flash))) * .20 * flash;
        return Sat(Math.Max(main, forks) + halo);
    }

    private static double EffectLightning(SceneEffectLayer layer, Ctx c)
    {
        double branchesP = P(layer, "lightning_branches"), widthP = P(layer, "lightning_width"), jitterP = P(layer, "lightning_jitter");
        double forksP = P(layer, "lightning_forks"), flashP = P(layer, "lightning_flash"), speedP = P(layer, "lightning_speed");
        double chaosP = P(layer, "lightning_flicker_chaos"), durationP = P(layer, "lightning_flash_duration");
        double baseGlowP = P(layer, "lightning_base_glow"), brightnessP = P(layer, "lightning_brightness"), aftershockP = P(layer, "lightning_aftershock");

        double rate = Math.Max(.05, speedP);
        double cycle = c.T * rate;
        double epoch = Math.Floor(cycle);
        double age = cycle - epoch;
        double sceneSeed = epoch * 19.31 + c.Seed * .731;
        double chaos = Math.Clamp(chaosP, 0.0, 1.0);
        double durationNorm = Math.Clamp(Math.Max(.01, durationP) * rate, .01, .92);
        int branches = (int)Math.Clamp(Math.Round(branchesP), 1, 12);
        int bolts = (int)Math.Clamp(Math.Floor(.5 + Math.Max(.1, c.Density) * 1.6), 1, 4);
        double total = 0;
        for (int b = 0; b < bolts; b++)
        {
            double fb = b;
            double seed = sceneSeed + fb * 43.17;
            double center = bolts == 1
                ? (Hash11(seed + 1.7, c.Seed) - .5) * .18
                : Mix(-.78, .78, (fb + .5) / bolts) + (Hash11(seed + 2.1, c.Seed) - .5) * .16;
            double strike = LightningBolt(c.P, seed, center, Math.Max(.003, widthP), Math.Max(0.0, jitterP), Math.Max(0.0, forksP), branches, Math.Max(0.0, flashP), c.Seed);
            double maxStart = Math.Max(0.0, .94 - durationNorm);
            double start = .03 + maxStart * chaos * Hash11(seed + 17.4, c.Seed);
            double since = age - start;
            double strikeActive = StepFn(0.0, since) * StepFn(since, durationNorm);
            double normalizedAge = Math.Clamp(since / Math.Max(.001, durationNorm), 0.0, 1.0);
            double envelope = strikeActive * Math.Pow(1.0 - normalizedAge, .42);
            double strengthVariation = Mix(1.0, .55 + 1.05 * Hash11(seed + 31.9, c.Seed), chaos);
            double mainPulse = envelope * strengthVariation * Math.Max(.0, brightnessP);
            double afterAmount = Math.Max(0.0, aftershockP);
            double afterDelay = durationNorm * (1.15 + .95 * Hash11(seed + 44.1, c.Seed));
            double afterWidth = Math.Max(.012, durationNorm * (.22 + .18 * Hash11(seed + 52.7, c.Seed)));
            double afterAge = Math.Abs(since - afterDelay);
            double afterPulse = (1.0 - SmoothStep(0.0, afterWidth, afterAge)) * .48 * afterAmount;
            double secondDelay = afterDelay + afterWidth * (1.7 + 1.3 * Hash11(seed + 62.8, c.Seed));
            double secondPulse = (1.0 - SmoothStep(0.0, afterWidth * .72, Math.Abs(since - secondDelay))) * .22 * afterAmount * chaos;
            double baseGlow = Math.Max(0.0, baseGlowP);
            double temporal = Math.Max(baseGlow, Math.Max(mainPulse, Math.Max(afterPulse, secondPulse)));
            total = Math.Max(total, strike * temporal);
        }
        return Sat(total);
    }

    private static double EffectBlackHole(SceneEffectLayer layer, Ctx c)
    {
        double sizeP = P(layer, "blackhole_size"), lensP = P(layer, "blackhole_lens"), starsP = P(layer, "blackhole_stars");
        double diskAngleP = P(layer, "blackhole_disk_angle"), inclinationP = P(layer, "blackhole_disk_inclination");
        double ringsP = P(layer, "blackhole_disk_rings"), diskP = P(layer, "blackhole_disk"), diskWidthP = P(layer, "blackhole_disk_width");
        double spinP = P(layer, "blackhole_spin"), haloEnabledP = P(layer, "blackhole_halo_enabled"), haloRadiusP = P(layer, "blackhole_halo_radius");
        double haloWidthP = P(layer, "blackhole_halo_width"), haloBrightP = P(layer, "blackhole_halo_brightness");
        double jetAngleP = P(layer, "blackhole_jet_angle"), jetLengthP = P(layer, "blackhole_jet_length"), jetWidthP = P(layer, "blackhole_jet_width");
        double jetsP = P(layer, "blackhole_jets"), jetBrightP = P(layer, "blackhole_jet_brightness");

        double r = c.P.Length, bh = Math.Max(.05, sizeP);
        double lens = Math.Max(.0, lensP) * bh * bh / Math.Max(.002, r * r);
        double warpedR = r + lens * .18;
        double shadow = 1.0 - SmoothStep(bh * .82, bh * 1.08, r);

        var starsUv = c.P / Math.Max(.25, 1.0 - lens * .08) + 2.0;
        starsUv *= new Vec2(c.GridW, c.GridH) * .23;
        double starHash = Hash21(new Vec2(Math.Floor(starsUv.X), Math.Floor(starsUv.Y)), c.Seed);
        double stars = StepFn(.994 / Math.Max(.2, starsP), starHash) * starsP * .75;

        double da = diskAngleP * Math.PI / 180.0;
        var dp = RotateGlsl(c.P, da);
        dp = new Vec2(dp.X, dp.Y / Math.Max(.08, Math.Abs(inclinationP)));
        double dr = dp.Length + lens * .18;
        double dang = Math.Atan2(dp.Y, dp.X);
        int ringCount = (int)Math.Clamp(Math.Round(Math.Abs(ringsP)), 1, 12);
        double diskSpan = .12 + .22 * Math.Max(.05, Math.Abs(diskP));
        double ringWidth = Math.Max(.003, Math.Abs(diskWidthP));
        double disk = 0;
        for (int i = 0; i < ringCount; i++)
        {
            double fi = i;
            double tRing = ringCount <= 1 ? .5 : fi / (ringCount - 1.0);
            double rr = bh + .065 + tRing * diskSpan;
            double band = 1.0 - SmoothStep(ringWidth, ringWidth + .003, Math.Abs(dr - rr));
            double swirl = .58 + .42 * Math.Sin(dang * (5.0 + fi * .42) - c.T * spinP * (2.2 + fi * .07) + dr * (19.0 + fi));
            double fade = Mix(1.0, .52, tRing);
            disk = Math.Max(disk, band * swirl * fade);
        }
        disk *= SmoothStep(bh * .88, bh * 1.08, r);

        double brightHalo = 0;
        if (haloEnabledP > .5)
        {
            double haloRadius = bh * Math.Max(1.01, haloRadiusP);
            double haloWidth = Math.Max(.002, Math.Abs(haloWidthP));
            brightHalo = (1.0 - SmoothStep(haloWidth, haloWidth + .003, Math.Abs(warpedR - haloRadius))) * Math.Max(0.0, haloBrightP);
        }

        double ja = jetAngleP * Math.PI / 180.0;
        var jp = RotateGlsl(c.P, ja);
        double jetLength = Math.Max(.08, Math.Abs(jetLengthP));
        double jy = Math.Abs(jp.Y);
        double taper = Math.Max(.22, 1.0 - jy / Math.Max(.001, jetLength) * .68);
        double jetWidth = Math.Max(.003, Math.Abs(jetWidthP)) * taper;
        double jetCore = 1.0 - SmoothStep(jetWidth, jetWidth * 2.2 + .003, Math.Abs(jp.X));
        double jetStart = SmoothStep(bh * .72, bh * 1.1, jy);
        double jetEnd = 1.0 - SmoothStep(jetLength * .82, jetLength, jy);
        double jetTexture = .72 + .28 * Math.Sin(jy * 34.0 - c.T * (5.0 + Math.Abs(spinP)));
        double jets = jetCore * jetStart * jetEnd * jetTexture * Math.Max(0.0, jetsP) * Math.Max(0.0, jetBrightP);

        double v = Math.Max(Math.Max(stars * (1.0 - shadow), disk), Math.Max(brightHalo, jets));
        v *= 1.0 - shadow * .95;
        return v;
    }

    private static double EffectStrangeAttractor(SceneEffectLayer layer, Ctx c)
    {
        double pointsP = P(layer, "attractor_points"), typeP = P(layer, "attractor_type"), zoomP = P(layer, "attractor_zoom");
        double baseRotP = P(layer, "attractor_base_rotation"), rotP = P(layer, "attractor_rotation");
        double trailP = P(layer, "attractor_trail"), glowP = P(layer, "attractor_glow");

        int count = (int)Math.Clamp(Math.Round(pointsP), 12, 96);
        double best = 10.0;
        double typ = Math.Round(typeP);
        var q = new Vec2(.1, .1);
        double aa = 1.4 + .25 * Math.Sin(c.T * .17), bb = -2.3 + .2 * Math.Cos(c.T * .13), cc = 2.4, dd = -2.1;
        double rot = baseRotP * Math.PI / 180.0 + c.T * rotP;

        for (int i = 0; i < count; i++)
        {
            if (typ < .5) q = new Vec2(Math.Sin(aa * q.Y) - Math.Cos(bb * q.X), Math.Sin(cc * q.X) - Math.Cos(dd * q.Y));
            else if (typ < 1.5) q = new Vec2(Math.Sin(aa * q.Y) + cc * Math.Cos(aa * q.X), Math.Sin(bb * q.X) + dd * Math.Cos(bb * q.Y));
            else if (typ < 2.5)
            {
                double xx = q.X, yy = q.Y;
                q = new Vec2(yy - Sign(xx) * Math.Sqrt(Math.Abs(bb * xx - cc)), aa - xx);
            }
            else q = new Vec2(Math.Sin(q.Y * 2.2 + i * .13), Math.Sin(q.X * 2.7 + i * .09));

            var qp = RotateGlsl(q * (.36 / Math.Max(.1, zoomP)), rot);
            best = Math.Min(best, (c.P - qp).Length);
        }
        double line = Math.Exp(-best * (45.0 / Math.Max(.2, trailP)));
        return Sat(line * (.55 + .45 * glowP));
    }

    private static double VoronoiField(Vec2 p, double cellsP, double speedP, double edgesP, double fillP, double pulseP, double t, int seed)
    {
        var q = p * (2.2 + cellsP * .18);
        var g = new Vec2(Math.Floor(q.X), Math.Floor(q.Y));
        var f = q - g;
        double d1 = 9.0, d2 = 9.0;
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            var o = new Vec2(x, y);
            var r = new Vec2(Hash21(g + o, seed), Hash21(g + o + 17.3, seed));
            r = new Vec2(.5 + .38 * Math.Sin(t * speedP + 6.2831 * r.X), .5 + .38 * Math.Sin(t * speedP + 6.2831 * r.Y));
            double d = (o + r - f).Length;
            if (d < d1) { d2 = d1; d1 = d; } else d2 = Math.Min(d2, d);
        }
        double edge = Sat((d2 - d1) * 7.0 * edgesP);
        double fill = (.5 + .5 * Math.Cos(d1 * 8.0 + t * pulseP)) * fillP;
        return Sat(Math.Max(1.0 - edge, fill * .55));
    }

    private static double EffectVoronoi(SceneEffectLayer layer, Ctx c)
    {
        double cellsP = P(layer, "voronoi_cells"), speedP = P(layer, "voronoi_speed"), edgesP = P(layer, "voronoi_edges");
        double fillP = P(layer, "voronoi_fill"), warpP = P(layer, "voronoi_warp"), pulseP = P(layer, "voronoi_pulse");
        var q = c.P + new Vec2(Math.Sin(c.P.Y * 3.0 + c.T) * warpP * .05, Math.Cos(c.P.X * 3.0 - c.T) * warpP * .05);
        return VoronoiField(q, cellsP, speedP, edgesP, fillP, pulseP, c.T, c.Seed);
    }

    private static double EffectSnowstorm(SceneEffectLayer layer, Ctx c)
    {
        double amountP = P(layer, "snow_amount"), sizeP = P(layer, "snow_size"), windP = P(layer, "snow_wind");
        double depthP = P(layer, "snow_depth"), gustP = P(layer, "snow_gust"), twinkleP = P(layer, "snow_twinkle");
        double total = 0;
        int layers = (int)Math.Clamp(2.0 + depthP * 2.0, 2, 8);
        for (int l = 0; l < layers; l++)
        {
            double fl = l;
            double sc = 8.0 + fl * 6.0;
            var q = c.P * sc;
            double qy = q.Y - c.T * (1.2 + .27 * fl);
            double qx = q.X - c.T * windP * (.18 + .04 * fl) + Math.Sin(qy * .13 + c.T) * gustP * .35;
            var id = new Vec2(Math.Floor(qx), Math.Floor(qy));
            var fr = new Vec2(qx - id.X - .5, qy - id.Y - .5);
            double rnd = Hash21(id + fl * 31.7, c.Seed);
            double hit = StepFn(1.0 - .13 * amountP, rnd);
            double sz = (.08 + .03 * sizeP) / (1.0 + fl * .2);
            double flake = hit * Math.Exp(-(fr.X * fr.X + fr.Y * fr.Y) / (sz * sz));
            flake *= 1.0 + Math.Sin(c.T * 5.0 + rnd * 20.0) * .25 * twinkleP;
            total += flake / (1.0 + fl * .3);
        }
        return Sat(total);
    }

    private static double EffectDnaHelix(SceneEffectLayer layer, Ctx c)
    {
        double turnsP = P(layer, "dna_turns"), radiusP = P(layer, "dna_radius"), baseRotP = P(layer, "dna_base_rotation");
        double speedP = P(layer, "dna_speed"), rungsP = P(layer, "dna_rungs"), tiltP = P(layer, "dna_tilt"), depthP = P(layer, "dna_depth");

        double y = c.P.Y + Math.Sin(c.P.X * .8) * tiltP * .1;
        double phase = y * turnsP * 3.14159 + baseRotP * Math.PI / 180.0 - c.T * speedP;
        double z1 = Math.Cos(phase), z2 = -z1;
        double x1 = Math.Sin(phase) * radiusP, x2 = -x1;
        double depth1 = .55 + .45 * (z1 * depthP * .5 + .5), depth2 = .55 + .45 * (z2 * depthP * .5 + .5);
        double d1 = Math.Abs(c.P.X - x1), d2 = Math.Abs(c.P.X - x2);
        double strand = Math.Max(Math.Exp(-d1 * 75.0) * depth1, Math.Exp(-d2 * 75.0) * depth2);
        double rungPhase = Frac((y + 1.0) * rungsP * .5);
        double rungGate = 1.0 - SmoothStep(.08, .18, Math.Min(rungPhase, 1.0 - rungPhase));
        double lo = Math.Min(x1, x2), hi = Math.Max(x1, x2);
        double rung = StepFn(lo, c.P.X) * StepFn(c.P.X, hi) * rungGate * .55;
        return Sat(Math.Max(strand, rung));
    }

    private static double WarpGridTerrain(Vec2 world, double scaleP, double smoothP, double ridgesP, double modeP, double detailP, double terracesP, double valleysP, double islandP, int seed)
    {
        double scale = Math.Max(.05, scaleP);
        double seedF = seed % 9973;
        var seedOffset = new Vec2(Math.Sin(seedF * .017), Math.Cos(seedF * .013)) * 7.0;
        var q = world * (.12 * scale) + seedOffset;
        double broad = .50 * Math.Sin(q.X * 1.35) + .32 * Math.Cos(q.Y * 1.05) + .18 * Math.Sin((q.X + q.Y) * .72);
        double rugged = StaticFbm(q * 1.35, seed) * 2.0 - 1.0;
        double smoothness = Math.Clamp(smoothP, 0.0, 1.0);
        double terrain = Mix(rugged, broad, smoothness);
        double ridgeNoise = 1.0 - Math.Abs(rugged);
        ridgeNoise = ridgeNoise * ridgeNoise * 1.35 - .35;
        terrain += ridgeNoise * Math.Max(0.0, ridgesP) * .42;
        int mode = (int)Math.Clamp(Math.Round(modeP), 0, 3);
        if (mode == 1) terrain = Mix(terrain, ridgeNoise, .72);
        double detail = (StaticFbm(q * 4.2 + new Vec2(4.7, 11.3), seed) * 2.0 - 1.0) * .34 * Math.Max(0.0, detailP);
        terrain += detail * (1.0 - .45 * smoothness);
        double terraces = Math.Max(0.0, terracesP);
        if (mode == 2) terraces = Math.Max(terraces, 6.0);
        if (terraces > 0.5) terrain = Math.Floor(terrain * terraces + 0.5) / terraces;
        terrain -= Math.Max(0.0, -terrain) * Math.Max(0.0, valleysP) * .65;
        double island = Math.Max(0.0, islandP);
        if (mode == 3) island = Math.Max(island, 1.15);
        if (island > 0.001)
        {
            double edge = SmoothStep(.55, 2.25, (q * .13).Length);
            terrain -= edge * island * 1.25;
        }
        return terrain;
    }

    private static double EffectWarpGrid(SceneEffectLayer layer, Ctx c)
    {
        double panX = P(layer, "warpgrid_pan_x"), panY = P(layer, "warpgrid_pan_y");
        double pitchP = P(layer, "warpgrid_pitch"), yawP = P(layer, "warpgrid_yaw");
        double horizonP = P(layer, "warpgrid_horizon"), depthP = P(layer, "warpgrid_depth"), cameraP = P(layer, "warpgrid_camera");
        double twistP = P(layer, "warpgrid_twist"), speedP = P(layer, "warpgrid_speed"), waveP = P(layer, "warpgrid_wave");
        double densityP = P(layer, "warpgrid_density");
        double terrH = P(layer, "warpgrid_terrain_height"), terrMode = P(layer, "warpgrid_terrain_mode"), terrScale = P(layer, "warpgrid_terrain_scale");
        double terrSmooth = P(layer, "warpgrid_terrain_smooth"), terrRidges = P(layer, "warpgrid_terrain_ridges"), terrValleys = P(layer, "warpgrid_terrain_valleys");
        double terrTerraces = P(layer, "warpgrid_terrain_terraces"), terrIsland = P(layer, "warpgrid_terrain_island"), terrDetail = P(layer, "warpgrid_terrain_detail");
        double terrWater = P(layer, "warpgrid_terrain_water");

        var viewP = c.P - new Vec2(panX, panY);
        double pitch = pitchP * Math.PI / 180.0, yaw = yawP * Math.PI / 180.0;
        double horizon = Math.Clamp(horizonP + Math.Sin(pitch) * .42, -.78, .78);
        double screenY = viewP.Y - horizon;
        if (screenY <= .015) return 0.0;

        double depthScale = Math.Max(.1, depthP) * Math.Max(.5, cameraP);
        double depth = depthScale / screenY;
        double wx = 0, wz = 0, terrain = 0;

        for (int iteration = 0; iteration < 3; iteration++)
        {
            double twist = Math.Sin(depth * .18 + c.T * .3) * twistP * .08;
            var world = RotateGlsl(new Vec2((viewP.X + twist) * depth, depth + c.T * speedP), yaw);
            wx = world.X; wz = world.Y;
            terrain = WarpGridTerrain(world, terrScale, terrSmooth, terrRidges, terrMode, terrDetail, terrTerraces, terrValleys, terrIsland, c.Seed) * Math.Max(0.0, terrH);
            if (iteration == 2) break;
            double perspectiveFalloff = 1.0 / (1.0 + depth * .06);
            double displacedY = Math.Max(.015, screenY + terrain * .11 * perspectiveFalloff);
            depth = depthScale / displacedY;
        }

        double wave = Math.Sin(wx * .45 + wz * .18) * waveP * .15;
        double density = Math.Max(2.0, densityP);
        double gx = PeriodicLine(wx * density * .06, .045);
        double gz = PeriodicLine((wz + wave) * density * .045, .040);
        double heightShade = Math.Clamp(.07 + terrain * .035, 0.0, .16) * StepFn(.001, terrH);
        double water = StepFn(terrain, terrWater) * .075 * StepFn(.001, terrH);
        double shore = (1.0 - SmoothStep(.015, .075, Math.Abs(terrain - terrWater))) * .28 * StepFn(.001, terrH);
        return Math.Max(Math.Max(Math.Max(gx, gz), heightShade), Math.Max(water, shore)) * SmoothStep(.02, .14, screenY);
    }

    private static int LifeInit(int cx, int cy, double cycle, double densityP, int seed)
    {
        double r = Hash21(new Vec2(cx + cycle * 31.7, cy + cycle * 17.3), seed);
        return r < Math.Clamp(densityP, .01, .99) ? 1 : 0;
    }

    private static int LifeStep1(int cx, int cy, double cycle, double densityP, int seed)
    {
        int n = 0;
        for (int yy = -1; yy <= 1; yy++)
        for (int xx = -1; xx <= 1; xx++)
            if (xx != 0 || yy != 0) n += LifeInit(cx + xx, cy + yy, cycle, densityP, seed);
        int alive = LifeInit(cx, cy, cycle, densityP, seed);
        return n == 3 || (alive == 1 && n == 2) ? 1 : 0;
    }

    private static int LifeStep2(int cx, int cy, double cycle, double densityP, int seed)
    {
        int n = 0;
        for (int yy = -1; yy <= 1; yy++)
        for (int xx = -1; xx <= 1; xx++)
            if (xx != 0 || yy != 0) n += LifeStep1(cx + xx, cy + yy, cycle, densityP, seed);
        int alive = LifeStep1(cx, cy, cycle, densityP, seed);
        return n == 3 || (alive == 1 && n == 2) ? 1 : 0;
    }

    private static int LifeAt(int cx, int cy, int phase, double cycle, double densityP, int seed) => phase switch
    {
        0 => LifeInit(cx, cy, cycle, densityP, seed),
        1 => LifeStep1(cx, cy, cycle, densityP, seed),
        _ => LifeStep2(cx, cy, cycle, densityP, seed)
    };

    private static double EffectConwayLife(SceneEffectLayer layer, Ctx c)
    {
        double cellSizeP = P(layer, "life_cell_size"), densityP = P(layer, "life_density"), speedP = P(layer, "life_speed"), glowP = P(layer, "life_glow");
        double cs = Math.Max(1.0, Math.Round(cellSizeP));
        int cx = (int)Math.Floor(c.Cell.X / cs), cy = (int)Math.Floor(c.Cell.Y / cs);
        double gen = Math.Floor(c.T * Math.Max(.05, speedP));
        double cycle = Math.Floor(gen / 3.0);
        int phase = (int)Mod(gen, 3.0);
        int alive = LifeAt(cx, cy, phase, cycle, densityP, c.Seed);
        double neighbors = 0;
        if (glowP > .001)
        {
            for (int yy = -1; yy <= 1; yy++)
            for (int xx = -1; xx <= 1; xx++)
                if (xx != 0 || yy != 0) neighbors += LifeAt(cx + xx, cy + yy, phase, cycle, densityP, c.Seed);
        }
        return Math.Max(alive, neighbors / 8.0 * glowP * .55);
    }

    private static double EffectReactionDiffusion(SceneEffectLayer layer, Ctx c)
    {
        double scaleP = P(layer, "rd_scale"), feedP = P(layer, "rd_feed"), killP = P(layer, "rd_kill"), contrastP = P(layer, "rd_contrast");
        var q = c.P * Math.Max(.2, scaleP);
        double t = c.T * .18;
        double n1 = Fbm(q + new Vec2(t, -t * .71), c.T, c.Seed);
        double n2 = Fbm(q * 1.73 + new Vec2(-t * .43, t * .57) + n1 * 1.8, c.T, c.Seed);
        double chemistry = Math.Sin((n1 - n2 + feedP * 7.0 - killP * 5.5) * 18.0 + q.X * .7 - q.Y * .4);
        double spots = .5 + .5 * chemistry;
        double ridge = 1.0 - Math.Abs(spots * 2.0 - 1.0);
        return Math.Pow(Sat(ridge), Math.Max(.2, contrastP));
    }

    private static double EffectBoids(SceneEffectLayer layer, Ctx c)
    {
        double countP = P(layer, "boids_count"), sizeP = P(layer, "boids_size"), cohesionP = P(layer, "boids_cohesion"), speedP = P(layer, "boids_speed");
        int count = (int)Math.Clamp(Math.Round(countP), 4, 96);
        double best = 10.0;
        for (int i = 0; i < count; i++)
        {
            double fi = i;
            double phase = Hash11(fi * 17.1, c.Seed) * 6.28318;
            double radius = .25 + .62 * Hash11(fi * 3.7 + 8.0, c.Seed);
            double flockPhase = c.T * speedP * (.3 + .35 * Hash11(fi + 2.0, c.Seed)) + phase;
            var center = new Vec2(Math.Sin(c.T * .21), Math.Cos(c.T * .17)) * (.22 * cohesionP);
            var pos = center + new Vec2(Math.Cos(flockPhase), Math.Sin(flockPhase * 1.07)) * (radius / Math.Max(.35, cohesionP * .45 + .55));
            pos += new Vec2(Math.Sin(fi * 2.1 + c.T), Math.Cos(fi * 1.7 - c.T * .8)) * .08;
            best = Math.Min(best, (c.P - pos).Length);
        }
        return Math.Exp(-best * Math.Max(12.0, 95.0 / Math.Max(.005, sizeP)) * .045);
    }

    private static double EffectNBody(SceneEffectLayer layer, Ctx c)
    {
        double countP = P(layer, "nbody_count"), gravP = P(layer, "nbody_gravity"), sizeP = P(layer, "nbody_size"), trailsP = P(layer, "nbody_trails");
        int count = (int)Math.Clamp(Math.Round(countP), 2, 24);
        double body = 0, trail = 0;
        for (int i = 0; i < count; i++)
        {
            double fi = i, rnd = Hash11(fi * 13.7 + 4.0, c.Seed);
            double radius = .15 + .78 * rnd;
            double omega = (.18 + .42 / (radius + .15)) * Math.Max(.05, gravP);
            double a = c.T * omega + fi / count * 6.28318;
            var pos = new Vec2(Math.Cos(a), Math.Sin(a)) * radius;
            double d = (c.P - pos).Length;
            body = Math.Max(body, Math.Exp(-d / Math.Max(.002, sizeP) * 3.1));
            double polar = Math.Atan2(c.P.Y, c.P.X), rr = c.P.Length;
            double orbit = Math.Exp(-Math.Abs(rr - radius) * 85.0 / Math.Max(.1, trailsP));
            double arc = .5 + .5 * Math.Cos((polar - a) * 3.0);
            trail = Math.Max(trail, orbit * arc * .45 * trailsP);
        }
        return Sat(Math.Max(body, trail));
    }

    private static double EffectFallingSand(SceneEffectLayer layer, Ctx c)
    {
        double amountP = P(layer, "sand_amount"), sizeP = P(layer, "sand_size"), speedP = P(layer, "sand_speed"), pileP = P(layer, "sand_pile");
        var q = new Vec2(c.Cell.X / c.GridW, c.Cell.Y / c.GridH);
        double pile = .08 + pileP * .18 * (1.0 - Math.Abs(q.X - .5) * 1.35);
        double ground = 1.0 - SmoothStep(.0, .025, Math.Abs(1.0 - q.Y - pile));
        double total = ground * .75;
        double grains = Math.Max(6.0, amountP * 45.0);
        for (int i = 0; i < 48 && i < grains; i++)
        {
            double fi = i;
            double x = Hash11(fi * 9.7 + c.Seed % 101, c.Seed) * .94 + .03;
            double y = Frac(Hash11(fi * 17.3 + 7.0, c.Seed) + c.T * speedP * (.08 + .08 * Hash11(fi + 3.0, c.Seed)));
            y = Math.Min(y, 1.0 - pile - Math.Abs(x - .5) * .14);
            var d = new Vec2((q.X - x) * c.GridW / Math.Max(1, c.GridH), q.Y - y);
            total = Math.Max(total, Math.Exp(-(d.X * d.X + d.Y * d.Y) * 900.0 / Math.Max(.1, sizeP)));
        }
        return Sat(total);
    }

    private static double EffectCloth(SceneEffectLayer layer, Ctx c)
    {
        double densityP = P(layer, "cloth_density"), waveP = P(layer, "cloth_wave"), tensionP = P(layer, "cloth_tension"), windP = P(layer, "cloth_wind");
        double qx = c.P.X, qy = c.P.Y;
        double tension = Math.Max(.1, tensionP);
        qy += Math.Sin(qx * 3.5 + c.T * 1.2 + windP) * .12 * waveP / tension;
        qx += Math.Sin(qy * 4.1 - c.T * .83) * .07 * waveP / tension;
        double den = Math.Max(3.0, densityP);
        double gx = PeriodicLine((qx + 1.2) * den * .5, .035);
        double gy = PeriodicLine((qy + 1.2) * den * .5, .035);
        double shade = .18 + .18 * Math.Sin((qx + qy) * 4.0 + c.T + windP);
        return Sat(Math.Max(gx, gy) + shade * waveP * .25);
    }

    private static double EffectClouds(SceneEffectLayer layer, Ctx c)
    {
        double coverageP = P(layer, "cloud_coverage"), softnessP = P(layer, "cloud_softness"), detailP = P(layer, "cloud_detail"), windP = P(layer, "cloud_wind");
        var q = c.P * Math.Max(.2, detailP);
        q += new Vec2(c.T * windP * .12, 0);
        double n = Fbm(q * 1.15, c.T, c.Seed) + Fbm(q * 2.3 + 7.1, c.T, c.Seed) * .35 + Fbm(q * 4.7 - 3.4, c.T, c.Seed) * .16;
        n /= 1.51;
        double edge = .48 + (coverageP - .5) * .52;
        double soft = Math.Max(.02, softnessP) * .35;
        double v = SmoothStep(edge - soft, edge + soft, n);
        v *= .72 + .28 * Fbm(q * .55 + new Vec2(2.0, -4.0), c.T, c.Seed);
        return v;
    }

    private static double EffectCity(SceneEffectLayer layer, Ctx c)
    {
        double densityP = P(layer, "city_density"), heightP = P(layer, "city_height"), windowsP = P(layer, "city_windows"), parallaxP = P(layer, "city_parallax");
        var uv = new Vec2(c.Cell.X / c.GridW, c.Cell.Y / c.GridH);
        double density = Math.Max(4.0, densityP);
        double layerAcc = 0;
        for (int l = 0; l < 3; l++)
        {
            double fl = l;
            double d = density * (1.0 - fl * .19);
            double sx = uv.X * d + c.T * .025 * parallaxP * fl;
            double id = Math.Floor(sx), fx = sx - id;
            double h = (.18 + .62 * Hash11(id + fl * 91.0, c.Seed)) * heightP / (1.0 + fl * .24);
            double basev = 1.0 - uv.Y;
            double building = StepFn(basev, Math.Clamp(h, .05, .92)) * StepFn(.06, fx) * StepFn(fx, .94);
            double wx = StepFn(.2, Frac(fx * 5.0)) * StepFn(Frac(fx * 5.0), .72);
            double wy = StepFn(.2, Frac(basev * d * 1.25)) * StepFn(Frac(basev * d * 1.25), .66);
            double lit = StepFn(.46, Hash21(new Vec2(Math.Floor(fx * 5.0) + id * 7.0, Math.Floor(basev * d * 1.25) + fl * 17.0) + Math.Floor(c.T * .5), c.Seed));
            double windows = building * wx * wy * lit * windowsP;
            layerAcc = Math.Max(layerAcc, building * (.18 + .18 * fl) + windows * .82);
        }
        return Sat(layerAcc);
    }
}
