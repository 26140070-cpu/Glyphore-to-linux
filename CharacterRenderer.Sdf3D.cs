namespace Glyphore;

internal static partial class CharacterRenderer
{
    private readonly struct Vec3
    {
        public readonly double X, Y, Z;
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(double s, Vec3 a) => a * s;
        public static Vec3 operator /(Vec3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
        public Vec3 Normalize()
        {
            double len = Length;
            return len > 1e-12 ? this / len : new Vec3(0, 0, 1);
        }
    }

    private readonly struct RayTrace
    {
        public readonly bool Hit;
        public readonly double Distance;
        public readonly int Steps;
        public readonly double MinDistance;
        public readonly Vec3 Position;

        public RayTrace(bool hit, double distance, int steps, double minDistance, Vec3 position)
        {
            Hit = hit;
            Distance = distance;
            Steps = steps;
            MinDistance = minDistance;
            Position = position;
        }
    }

    private static Vec3 Cross(Vec3 a, Vec3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    private static double LengthXZ(Vec3 p) => Math.Sqrt(p.X * p.X + p.Z * p.Z);

    private static Vec3 RotateX(Vec3 p, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return new Vec3(p.X, c * p.Y - s * p.Z, s * p.Y + c * p.Z);
    }

    private static Vec3 RotateY(Vec3 p, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return new Vec3(c * p.X + s * p.Z, p.Y, -s * p.X + c * p.Z);
    }

    private static Vec3 RotateZ(Vec3 p, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return new Vec3(c * p.X - s * p.Y, s * p.X + c * p.Y, p.Z);
    }

    private static Vec3 RotateEuler(Vec3 p, double x, double y, double z)
        => RotateZ(RotateY(RotateX(p, x), y), z);

    private static double SdSphere(Vec3 p, double r) => p.Length - r;

    private static double SdBox(Vec3 p, Vec3 b)
    {
        var q = new Vec3(Math.Abs(p.X), Math.Abs(p.Y), Math.Abs(p.Z)) - b;
        var outside = new Vec3(Math.Max(0, q.X), Math.Max(0, q.Y), Math.Max(0, q.Z));
        double inside = Math.Min(Math.Max(q.X, Math.Max(q.Y, q.Z)), 0);
        return outside.Length + inside;
    }

    private static double SdOctahedron(Vec3 p, double s)
        => (Math.Abs(p.X) + Math.Abs(p.Y) + Math.Abs(p.Z) - s) * 0.57735026919;

    private static double SdTorus(Vec3 p, double majorRadius, double minorRadius)
    {
        double qx = LengthXZ(p) - majorRadius;
        return Math.Sqrt(qx * qx + p.Y * p.Y) - minorRadius;
    }

    private static double SdCylinder(Vec3 p, double radius, double halfHeight)
    {
        double radial = LengthXZ(p) - radius;
        double vertical = Math.Abs(p.Y) - halfHeight;
        double ox = Math.Max(radial, 0), oy = Math.Max(vertical, 0);
        return Math.Min(Math.Max(radial, vertical), 0) + Math.Sqrt(ox * ox + oy * oy);
    }

    private static double SdCapsule(Vec3 p, double halfSegment, double radius)
    {
        double q = Math.Sqrt(p.X * p.X + p.Z * p.Z);
        double y = Math.Abs(p.Y) - halfSegment;
        double ox = Math.Max(q - radius, 0);
        double oy = Math.Max(y, 0);
        return Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(q - radius, y), 0);
    }

    private static double SdPyramid(Vec3 p, double halfBase, double height)
    {
        // Square pyramid field: a broad square base narrowing to a single apex.
        double h = Math.Max(.05, height);
        double t = Math.Clamp((p.Y + h) / (2.0 * h), 0, 1);
        double halfWidth = halfBase * (1.0 - t);
        double side = Math.Max(Math.Abs(p.X), Math.Abs(p.Z)) - halfWidth;
        double vertical = Math.Abs(p.Y) - h;
        return Math.Max(side, vertical);
    }

    private static double SdSmoothUnion(double a, double b, double k)
    {
        k = Math.Max(1e-5, k);
        double h = Math.Clamp(.5 + .5 * (b - a) / k, 0, 1);
        return Mix(b, a, h) - k * h * (1 - h);
    }

    private static double SdRepeatRadial(Func<Vec3, double> sdf, Vec3 p, int repeats, double spacing)
    {
        if (repeats <= 0) return sdf(p);
        double best = sdf(p);
        for (int i = 0; i < repeats; i++)
        {
            double angle = (i / (double)repeats) * Math.PI * 2.0;
            var offset = new Vec3(Math.Cos(angle) * spacing, 0, Math.Sin(angle) * spacing);
            best = Math.Min(best, sdf(p - offset));
        }
        return best;
    }

    private static double ShapeSdf(Vec3 p, int shape, double scale = 1.0)
    {
        p /= Math.Max(.05, scale);
        return shape switch
        {
            0 => SdSphere(p, .88),
            1 => SdBox(p, new Vec3(.72, .72, .72)),
            2 => SdOctahedron(p, 1.17),
            3 => SdTorus(p, .58, .27),
            4 => SdCylinder(p, .68, .76),
            5 => SdCapsule(p, .45, .33),
            6 => SdPyramid(p, .82, .92),
            _ => SdSphere(p, .88)
        } * Math.Max(.05, scale);
    }

    private static Vec3 CameraPosition(double yawDeg, double pitchDeg, double distance, Vec3 target)
    {
        double yaw = yawDeg * Math.PI / 180.0;
        double pitch = pitchDeg * Math.PI / 180.0;
        double cp = Math.Cos(pitch);
        return target + new Vec3(
            Math.Sin(yaw) * cp * distance,
            Math.Sin(pitch) * distance,
            Math.Cos(yaw) * cp * distance);
    }

    private static Vec3 CameraRay(Vec3 origin, Vec3 target, double screenX, double screenY, double lens)
    {
        Vec3 forward = (target - origin).Normalize();
        Vec3 worldUp = Math.Abs(forward.Y) > .985 ? new Vec3(0, 0, 1) : new Vec3(0, 1, 0);
        Vec3 right = Cross(forward, worldUp).Normalize();
        Vec3 up = Cross(right, forward).Normalize();
        return (forward + right * (screenX * lens) + up * (screenY * lens)).Normalize();
    }

    private static RayTrace Raymarch(
        Func<Vec3, double> sdf,
        Vec3 origin,
        Vec3 direction,
        double maxDistance,
        int maxSteps,
        double epsilon,
        double safety)
    {
        double t = 0;
        double minDistance = double.MaxValue;
        Vec3 position = origin;
        for (int i = 0; i < maxSteps; i++)
        {
            position = origin + direction * t;
            double d = sdf(position);
            if (!double.IsFinite(d)) d = 1.0;
            minDistance = Math.Min(minDistance, Math.Abs(d));
            if (d < epsilon)
                return new RayTrace(true, t, i + 1, minDistance, position);

            double step = Math.Max(epsilon * .55, d * Math.Clamp(safety, .35, 1.0));
            t += step;
            if (t >= maxDistance)
                break;
        }
        return new RayTrace(false, t, maxSteps, minDistance, position);
    }

    private static Vec3 EstimateNormal(Func<Vec3, double> sdf, Vec3 p, double epsilon)
    {
        double e = Math.Clamp(epsilon, .00045, .0045);
        return new Vec3(
            sdf(p + new Vec3(e, 0, 0)) - sdf(p - new Vec3(e, 0, 0)),
            sdf(p + new Vec3(0, e, 0)) - sdf(p - new Vec3(0, e, 0)),
            sdf(p + new Vec3(0, 0, e)) - sdf(p - new Vec3(0, 0, e))).Normalize();
    }

    private static double Shadow(Func<Vec3, double> sdf, Vec3 origin, Vec3 lightDirection, double maxDistance)
    {
        double t = .025;
        double result = 1.0;
        for (int i = 0; i < 20 && t < maxDistance; i++)
        {
            double h = sdf(origin + lightDirection * t);
            if (h < .001) return 0.08;
            result = Math.Min(result, 10.0 * h / Math.Max(.001, t));
            t += Math.Clamp(h, .008, .25);
        }
        return Math.Clamp(result, 0.08, 1.0);
    }

    private static double AmbientOcclusion(Func<Vec3, double> sdf, Vec3 p, Vec3 normal)
    {
        double occ = 0;
        double scale = .025;
        for (int i = 1; i <= 4; i++)
        {
            double h = scale * i;
            double d = sdf(p + normal * h);
            occ += Math.Max(0, h - d) / h;
        }
        return Math.Clamp(1.0 - occ * .18, .58, 1.0);
    }

    private static double ShadeSdf(
        Func<Vec3, double> sdf,
        Vec3 position,
        Vec3 rayDirection,
        Vec3 lightDirection,
        double lightStrength,
        double normalEpsilon,
        double depth,
        double rimStrength,
        double glow)
    {
        Vec3 normal = EstimateNormal(sdf, position, normalEpsilon);
        Vec3 view = (-rayDirection).Normalize();
        double diffuse = Math.Max(0, normal.Dot(lightDirection));
        double shadow = Shadow(sdf, position + normal * .012, lightDirection, 7.0);
        double ao = AmbientOcclusion(sdf, position, normal);
        Vec3 halfVector = (lightDirection + view).Normalize();
        double specular = Math.Pow(Math.Max(0, normal.Dot(halfVector)), 32.0) * .22;
        double rim = Math.Pow(1.0 - Math.Max(0, normal.Dot(view)), 2.2) * Math.Max(0, rimStrength);
        double depthFade = 1.0 - Math.Clamp(depth / 18.0, 0, .35);
        double value = .055 + diffuse * .63 * Math.Max(.15, lightStrength) * shadow * ao + specular + rim * .28;
        value *= .72 + .28 * depthFade;
        value += Math.Clamp(glow, 0, 3) * .025;
        return Sat(value);
    }

    private static double EffectSpinningDonutSdf(SceneEffectLayer layer, Ctx c)
    {
        double major = Math.Max(.2, P(layer, "donut_major"));
        double minor = Math.Max(.035, P(layer, "donut_minor"));
        double rx = (P(layer, "donut_rotation_x") + c.T * P(layer, "donut_spin_x")) * Math.PI / 180.0;
        double ry = (P(layer, "donut_rotation_y") + c.T * P(layer, "donut_spin_y")) * Math.PI / 180.0;
        double rz = P(layer, "donut_rotation_z") * Math.PI / 180.0;
        double yaw = P(layer, "donut_yaw");
        double pitch = P(layer, "donut_pitch");
        double distance = Math.Max(2.2, P(layer, "donut_camera"));
        var target = new Vec3(P(layer, "donut_pan_x") * .55, P(layer, "donut_pan_y") * .55, 0);
        Vec3 camera = CameraPosition(yaw, pitch, distance, target);
        Vec3 ray = CameraRay(camera, target, c.P.X, c.P.Y, .78);
        Vec3 light = CameraPosition(P(layer, "donut_light_yaw"), P(layer, "donut_light_pitch"), 4.0, new Vec3(0, 0, 0)).Normalize();
        double detail = Math.Max(.2, P(layer, "donut_detail"));
        double Surface(Vec3 p)
        {
            Vec3 q = RotateEuler(p, -rx, -ry, -rz);
            double d = SdTorus(q, major, minor);
            double micro = Math.Sin(q.X * 9.0 * detail + q.Y * 7.0) * Math.Sin(q.Z * 8.0 * detail - q.X * 5.0);
            return d + micro * .0018 * detail;
        }
        var trace = Raymarch(Surface, camera, ray, 14.0, 56, .0016, .9);
        if (!trace.Hit) return .018 + .012 * Math.Exp(-Math.Abs(c.P.Y + .15) * 2.5);
        return ShadeSdf(Surface, trace.Position, ray, light, 1.0, .0018, trace.Distance, .9, 0);
    }

    private static double Effect3DShapesSdf(SceneEffectLayer layer, Ctx c)
    {
        int shape = (int)Math.Clamp(Math.Round(P(layer, "shape3d_type")), 0, 6);
        double rx = (P(layer, "shape3d_rotation_x") + c.T * P(layer, "shape3d_spin_x") * 28.0) * Math.PI / 180.0;
        double ry = (P(layer, "shape3d_rotation_y") + c.T * P(layer, "shape3d_spin_y") * 28.0) * Math.PI / 180.0;
        double rz = (P(layer, "shape3d_rotation_z") + c.T * P(layer, "shape3d_spin_z") * 28.0) * Math.PI / 180.0;
        double yaw = P(layer, "shape3d_yaw"), pitch = P(layer, "shape3d_pitch");
        double distance = Math.Max(2.2, P(layer, "shape3d_camera"));
        double fov = Math.Clamp(P(layer, "shape3d_fov"), .7, 3.5);
        var target = new Vec3(P(layer, "shape3d_pan_x") * .55, P(layer, "shape3d_pan_y") * .55, 0);
        Vec3 camera = CameraPosition(yaw, pitch, distance, target);
        Vec3 ray = CameraRay(camera, target, c.P.X, c.P.Y, Math.Clamp(fov * .55, .42, 1.7));
        Vec3 light = CameraPosition(P(layer, "shape3d_light_yaw"), P(layer, "shape3d_light_pitch"), 4.0, new Vec3(0, 0, 0)).Normalize();
        double lighting = Math.Clamp(P(layer, "shape3d_light"), 0, 2);
        int mode = (int)Math.Clamp(Math.Round(P(layer, "shape3d_mode")), 0, 2);
        double Surface(Vec3 p)
        {
            Vec3 q = RotateEuler(p, -rx, -ry, -rz);
            return ShapeSdf(q, shape, .92);
        }
        var trace = Raymarch(Surface, camera, ray, 12.0, 56, .0015, .92);
        if (!trace.Hit) return .012;

        double baseValue = ShadeSdf(Surface, trace.Position, ray, light, .35 + lighting * .75, .0015, trace.Distance, 1.0, 0);
        if (mode == 1)
        {
            double band = .5 + .5 * Math.Sin(trace.Distance * 7.0);
            baseValue = .25 + baseValue * (.65 + .35 * band);
        }
        else if (mode == 2)
        {
            Vec3 n = EstimateNormal(Surface, trace.Position, .0015);
            Vec3 v = (-ray).Normalize();
            double rim = Math.Pow(1.0 - Math.Max(0, n.Dot(v)), 3.5);
            baseValue = Math.Max(baseValue * .42, rim * .88 + baseValue * .22);
        }
        return Sat(baseValue);
    }

    private static double SdfLabPrimitive(Vec3 p, int shape)
    {
        return shape switch
        {
            0 => SdSmoothUnion(SdSphere(p - new Vec3(-.24, 0, 0), .52), SdSphere(p - new Vec3(.24, 0, 0), .52), .24),
            1 => SdBox(p, new Vec3(.62, .62, .62)),
            2 => SdTorus(p, .58, .24),
            3 => SdCapsule(p, .48, .30),
            4 => SdSmoothUnion(SdTorus(p, .58, .22), SdBox(p - new Vec3(0, .12, 0), new Vec3(.42, .42, .42)), .2),
            _ => SdSphere(p, .58)
        };
    }

    private static double EffectSdfLab(SceneEffectLayer layer, Ctx c)
    {
        int shape = (int)Math.Clamp(Math.Round(P(layer, "sdf_shape")), 0, 4);
        int repeats = (int)Math.Clamp(Math.Round(P(layer, "sdf_repeat")), 0, 4);
        double spacing = Math.Max(1.15, P(layer, "sdf_spacing"));
        double twist = P(layer, "sdf_twist");
        double smooth = Math.Clamp(P(layer, "sdf_smooth"), 0, 1);
        double rotation = (P(layer, "sdf_base_rotation") + c.T * P(layer, "sdf_spin") * 22.0) * Math.PI / 180.0;
        double yaw = P(layer, "sdf_yaw"), pitch = P(layer, "sdf_pitch");
        double distance = Math.Max(1.8, P(layer, "sdf_depth"));
        var target = new Vec3(P(layer, "sdf_pan_x") * .24, P(layer, "sdf_pan_y") * .24, 0);
        Vec3 camera = CameraPosition(yaw, pitch, distance, target);
        Vec3 ray = CameraRay(camera, target, c.P.X, c.P.Y, .82);
        Vec3 light = CameraPosition(P(layer, "sdf_light_yaw"), P(layer, "sdf_light_pitch"), 4.0, new Vec3(0, 0, 0)).Normalize();

        double Surface(Vec3 world)
        {
            Vec3 q = RotateY(world - target, -rotation);
            double best = double.PositiveInfinity;

            double SampleOne(Vec3 local, int index)
            {
                double twistAngle = local.Y * twist * .32;
                local = RotateY(RotateZ(local, twistAngle), twistAngle * .35);
                if (shape == 4)
                {
                    int variant = Math.Abs(index) % 4;
                    return SdfLabPrimitive(local, variant == 0 ? 0 : variant);
                }
                return SdfLabPrimitive(local, shape);
            }

            best = Math.Min(best, SampleOne(q, 0));
            if (repeats > 0)
            {
                for (int i = 0; i < repeats; i++)
                {
                    double a = i * Math.PI * 2.0 / repeats;
                    var offset = new Vec3(Math.Cos(a) * spacing, 0, Math.Sin(a) * spacing);
                    double d = SampleOne(q - offset, i + 1);
                    best = smooth > .001 ? SdSmoothUnion(best, d, .04 + smooth * .25) : Math.Min(best, d);
                }
            }
            if (smooth > .001 && repeats == 0)
                best = SdSmoothUnion(best, SampleOne(q + new Vec3(.0, .24, 0), 7), .06 + smooth * .16);
            return best;
        }

        var trace = Raymarch(Surface, camera, ray, 28.0, 64, .0016, .87);
        if (!trace.Hit) return .012 + .01 * Math.Exp(-Math.Abs(c.P.Y) * 2.0);
        return ShadeSdf(Surface, trace.Position, ray, light, .85, .0016, trace.Distance, .95, .1 * smooth);
    }

    private static double MandelboxDistance(Vec3 p, double scale, int iterations, double detail)
    {
        Vec3 z = p;
        double dr = 1.0;
        double s = Math.Clamp(scale, 1.2, 3.0);
        int count = Math.Clamp(iterations, 2, 12);
        for (int i = 0; i < count; i++)
        {
            z = new Vec3(
                Math.Clamp(z.X, -1, 1) * 2.0 - z.X,
                Math.Clamp(z.Y, -1, 1) * 2.0 - z.Y,
                Math.Clamp(z.Z, -1, 1) * 2.0 - z.Z);
            double r2 = z.Dot(z);
            if (r2 < .25)
            {
                double factor = 4.0;
                z *= factor;
                dr *= factor;
            }
            else if (r2 < 1.0)
            {
                z /= r2;
                dr /= r2;
            }
            z = z * s + p;
            dr = dr * Math.Abs(s) + 1.0;
            if (z.Dot(z) > 64.0) break;
        }
        double r = z.Length;
        return Math.Abs(r - 1.0) / Math.Max(1.0, Math.Abs(dr)) * Math.Max(.25, detail) * .72;
    }

    private static double KaleidoscopeDistance(Vec3 p, double scale, int iterations, double detail)
    {
        Vec3 q = p;
        for (int i = 0; i < Math.Clamp(iterations, 2, 12); i++)
        {
            q = new Vec3(Math.Abs(q.X), Math.Abs(q.Y), Math.Abs(q.Z));
            if (q.X < q.Y) q = new Vec3(q.Y, q.X, q.Z);
            if (q.X < q.Z) q = new Vec3(q.Z, q.Y, q.X);
            if (q.Y < q.Z) q = new Vec3(q.X, q.Z, q.Y);
            q = new Vec3(q.X - .42, q.Y - .22, q.Z - .32);
            double k = Math.Clamp(scale * (.82 + i * .035), 1.2, 3.2);
            q *= k;
        }
        double d = SdTorus(q, .62, .18);
        return Math.Max(.002, d * Math.Max(.3, detail) / Math.Max(.8, scale));
    }

    private static double GyroidDistance(Vec3 p, double scale, double detail)
    {
        double s = Math.Max(.25, scale * detail);
        var q = p * s;
        double g = Math.Sin(q.X) * Math.Cos(q.Y) + Math.Sin(q.Y) * Math.Cos(q.Z) + Math.Sin(q.Z) * Math.Cos(q.X);
        double band = Math.Abs(g) - .22;
        return band * .48 / s;
    }

    private static double RaymarchLabDistance(Vec3 p, int mode, double scale, int iterations, double detail, double rotation, int seed)
    {
        Vec3 q = RotateEuler(p, rotation * .37, rotation, rotation * .23);
        q += new Vec3(Math.Sin(seed * .013) * .17, Math.Cos(seed * .017) * .11, Math.Sin(seed * .019) * .13);
        return mode switch
        {
            0 => MandelboxDistance(q, scale, iterations, detail),
            1 => KaleidoscopeDistance(q, scale, iterations, detail),
            _ => GyroidDistance(q, scale, detail)
        };
    }

    private static double EffectRaymarchLab(SceneEffectLayer layer, Ctx c)
    {
        int mode = (int)Math.Clamp(Math.Round(P(layer, "raymarch_mode")), 0, 2);
        int iterations = (int)Math.Clamp(Math.Round(P(layer, "raymarch_iterations")), 2, 12);
        double scale = P(layer, "raymarch_scale"), detail = P(layer, "raymarch_detail");
        double rotation = (P(layer, "raymarch_rotation") + c.T * P(layer, "raymarch_spin") * 24.0) * Math.PI / 180.0;
        double yaw = P(layer, "raymarch_yaw"), pitch = P(layer, "raymarch_pitch");
        double distance = Math.Max(1.8, P(layer, "raymarch_camera"));
        var target = new Vec3(P(layer, "raymarch_pan_x") * .42, P(layer, "raymarch_pan_y") * .42, 0);
        Vec3 camera = CameraPosition(yaw, pitch, distance, target);
        Vec3 ray = CameraRay(camera, target, c.P.X, c.P.Y, .88);
        Vec3 light = CameraPosition(P(layer, "raymarch_light_yaw"), P(layer, "raymarch_light_pitch"), 4.0, new Vec3(0, 0, 0)).Normalize();
        double glow = Math.Max(0, P(layer, "raymarch_glow"));
        double safety = mode == 0 ? .72 : .82;

        double Surface(Vec3 p) => RaymarchLabDistance(p - target, mode, scale, iterations, detail, rotation, c.Seed);

        var trace = Raymarch(Surface, camera, ray, 24.0, mode == 0 ? 86 : 72, .0015, safety);
        if (!trace.Hit)
        {
            double g = glow * Math.Exp(-trace.MinDistance * 42.0) * .2;
            return Sat(.008 + g);
        }
        double shade = ShadeSdf(Surface, trace.Position, ray, light, .72, .0015, trace.Distance, 1.18, glow);
        double glowTerm = glow * Math.Exp(-trace.MinDistance * 58.0) * .20;
        if (mode == 2)
            shade = Math.Max(shade, .18 + .34 * glowTerm);
        return Sat(shade + glowTerm);
    }
}
