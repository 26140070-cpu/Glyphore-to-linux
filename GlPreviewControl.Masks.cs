using System.Drawing;
using System.Windows.Forms;

using System.Diagnostics;

namespace Glyphore;

internal sealed partial class GlPreviewControl
{
    private enum MaskDragMode
    {
        None, Move, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight, Rotate
    }

    private MaskDragMode _maskDragMode;
    private SceneEffectLayer? _maskDragLayer;
    private SceneLayerMask? _maskDragMask;
    private SceneLayerMask? _maskDragStart;
    private PointF _maskDragStartUv;
    private double _maskRotationPointerStart;
    private long _maskDragLastPaintTicks;

    private bool TryBeginMaskEdit(Point point, Keys modifiers)
    {
        var active = FindActiveMask();
        if (active is not { } pair) return false;
        if (!TryClientToLayerUv(point, out PointF uv)) return false;

        MaskDragMode hit = HitTestMask(pair.Mask, uv);
        if (hit == MaskDragMode.None) return false;

        Focus();
        _maskDragMode = hit;
        _maskDragLayer = pair.Layer;
        _maskDragMask = pair.Mask;
        _maskDragStart = pair.Mask.Clone();
        _maskDragStartUv = uv;
        _maskRotationPointerStart = Math.Atan2(uv.Y - pair.Mask.Y, uv.X - pair.Mask.X);
        _maskDragLastPaintTicks = 0;
        if (_glArea is not null) GtkNative.GtkGrab(_glArea.Handle);
        Cursor = CursorForMaskDrag(hit);
        return true;
    }

    private bool UpdateMaskEdit(Point point, Keys modifiers)
    {
        if (_maskDragMode == MaskDragMode.None || _maskDragMask is null || _maskDragStart is null || _maskDragLayer is null)
            return false;
        if (!TryClientToLayerUv(point, out PointF uv)) return true;

        SceneLayerMask mask = _maskDragMask;
        SceneLayerMask start = _maskDragStart;
        bool ignoreSnap = (modifiers & (Keys.Control | Keys.Alt)) != 0;
        _maskSnapGuideX = -1f;
        _maskSnapGuideY = -1f;

        if (_maskDragMode == MaskDragMode.Move)
        {
            double x = start.X + (uv.X - _maskDragStartUv.X);
            double y = start.Y + (uv.Y - _maskDragStartUv.Y);
            if (_maskSnapping && !ignoreSnap) ApplyMoveSnapping(start, ref x, ref y);
            mask.X = x;
            mask.Y = y;
        }
        else if (_maskDragMode == MaskDragMode.Rotate)
        {
            double now = Math.Atan2(uv.Y - start.Y, uv.X - start.X);
            double delta = (now - _maskRotationPointerStart) * 180.0 / Math.PI;
            double rotation = NormalizeDegrees(start.Rotation - delta);
            if (_maskRotationSnapping && !ignoreSnap) rotation = SnapMaskRotation(rotation);
            mask.Rotation = rotation;
        }
        else
        {
            ResizeMaskFromHandle(mask, start, uv, _maskDragMode, (modifiers & Keys.Shift) != 0);
        }

        mask.Clamp();
        _maskDragLayer.Touch();
        MaskEdited?.Invoke(_maskDragLayer, mask, false);
        RequestLiveMaskRepaint();
        return true;
    }

    private void EndMaskEdit(bool commit)
    {
        if (_maskDragMode == MaskDragMode.None) return;
        var layer = _maskDragLayer;
        var mask = _maskDragMask;
        _maskDragMode = MaskDragMode.None;
        _maskDragLayer = null;
        _maskDragMask = null;
        _maskDragStart = null;
        _maskSnapGuideX = -1f;
        _maskSnapGuideY = -1f;
        _maskDragLastPaintTicks = 0;
        if (_glArea is not null) GtkNative.GtkUngrab(_glArea.Handle);
        Cursor = Cursors.Default;
        if (commit && layer is not null && mask is not null) MaskEdited?.Invoke(layer, mask, true);
        RequestNextFrameRendering();
    }

    private void CancelMaskEdit()
    {
        if (_maskDragMode == MaskDragMode.None || _maskDragMask is null || _maskDragStart is null || _maskDragLayer is null)
        {
            _maskSnapGuideX = -1f;
            _maskSnapGuideY = -1f;
            RequestNextFrameRendering();
            return;
        }
        CopyMaskValues(_maskDragStart, _maskDragMask);
        _maskDragLayer.Touch();
        MaskEdited?.Invoke(_maskDragLayer, _maskDragMask, false);
        EndMaskEdit(false);
    }

    private void HandlePreviewKey(Keys key, Keys modifiers)
    {
        var active = FindActiveMask();
        if (key == Keys.Escape && _maskDragMode != MaskDragMode.None)
        {
            CancelMaskEdit();
            return;
        }
        if (active is not { } pair) return;
        if (key == Keys.Delete)
        {
            MaskDeleteRequested?.Invoke(pair.Layer, pair.Mask);
            return;
        }
        double amount = (modifiers & Keys.Shift) != 0 ? .025 : .005;
        double dx = key switch { Keys.Left => -amount, Keys.Right => amount, _ => 0.0 };
        double dy = key switch { Keys.Up => -amount, Keys.Down => amount, _ => 0.0 };
        if (dx != 0.0 || dy != 0.0)
        {
            pair.Mask.X += dx;
            pair.Mask.Y += dy;
            pair.Mask.Clamp();
            pair.Layer.Touch();
            MaskEdited?.Invoke(pair.Layer, pair.Mask, true);
            RequestNextFrameRendering();
        }
    }

    private void RequestLiveMaskRepaint()
    {
        RequestNextFrameRendering();
        long now = Stopwatch.GetTimestamp();
        int liveFps = Math.Clamp(TargetFps, 30, 60);
        long period = Math.Max(1L, Stopwatch.Frequency / liveFps);
        if (_maskDragLastPaintTicks != 0 && now - _maskDragLastPaintTicks < period) return;
        _maskDragLastPaintTicks = now;
        RequestNextFrameRendering();
    }

    private MaskDragMode HitTestMask(SceneLayerMask mask, PointF uv)
    {
        PointF local = RotateToMaskLocal(uv, mask);
        double hw = Math.Max(.0005, mask.Width * .5), hh = Math.Max(.0005, mask.Height * .5);
        double threshold = MaskHitThresholdUv();
        double rotHandleY = -hh - threshold * 4.0;
        if (Distance(local.X, local.Y, 0, rotHandleY) <= threshold * 1.5) return MaskDragMode.Rotate;

        (double X, double Y, MaskDragMode Mode)[] handles =
        [
            (-hw, -hh, MaskDragMode.TopLeft), (0, -hh, MaskDragMode.Top), (hw, -hh, MaskDragMode.TopRight),
            (hw, 0, MaskDragMode.Right), (hw, hh, MaskDragMode.BottomRight), (0, hh, MaskDragMode.Bottom),
            (-hw, hh, MaskDragMode.BottomLeft), (-hw, 0, MaskDragMode.Left)
        ];
        foreach (var handle in handles)
            if (Distance(local.X, local.Y, handle.X, handle.Y) <= threshold * 1.5) return handle.Mode;
        return Math.Abs(local.X) <= hw && Math.Abs(local.Y) <= hh ? MaskDragMode.Move : MaskDragMode.None;
    }

    private void ResizeMaskFromHandle(SceneLayerMask mask, SceneLayerMask start, PointF uv, MaskDragMode mode, bool keepAspect)
    {
        PointF local = RotatePoint(new PointF((float)(uv.X - start.X), (float)(uv.Y - start.Y)), start.Rotation);
        double hw = Math.Max(.0005, start.Width * .5), hh = Math.Max(.0005, start.Height * .5);
        double left = -hw, right = hw, top = -hh, bottom = hh;
        bool editLeft = mode is MaskDragMode.Left or MaskDragMode.TopLeft or MaskDragMode.BottomLeft;
        bool editRight = mode is MaskDragMode.Right or MaskDragMode.TopRight or MaskDragMode.BottomRight;
        bool editTop = mode is MaskDragMode.Top or MaskDragMode.TopLeft or MaskDragMode.TopRight;
        bool editBottom = mode is MaskDragMode.Bottom or MaskDragMode.BottomLeft or MaskDragMode.BottomRight;
        if (editLeft) left = Math.Min(local.X, right - .01);
        if (editRight) right = Math.Max(local.X, left + .01);
        if (editTop) top = Math.Min(local.Y, bottom - .01);
        if (editBottom) bottom = Math.Max(local.Y, top + .01);
        if (keepAspect && (editLeft || editRight) && (editTop || editBottom))
        {
            double ratio = Math.Max(.001, start.Width / Math.Max(.001, start.Height));
            double width = right - left, height = bottom - top;
            if (width / Math.Max(.001, height) > ratio)
            {
                double wanted = width / ratio;
                if (editTop) top = bottom - wanted; else bottom = top + wanted;
            }
            else
            {
                double wanted = height * ratio;
                if (editLeft) left = right - wanted; else right = left + wanted;
            }
        }
        if (start.Type == SceneMaskType.Triangle)
        {
            const double maxAspect = 32.0;
            double width = right - left, height = bottom - top;
            if (width < height / maxAspect)
            {
                double wanted = height / maxAspect;
                if (editLeft) left = right - wanted; else right = left + wanted;
            }
            else if (height < width / maxAspect)
            {
                double wanted = width / maxAspect;
                if (editTop) top = bottom - wanted; else bottom = top + wanted;
            }
        }
        double centerLocalX = (left + right) * .5, centerLocalY = (top + bottom) * .5;
        PointF centerWorldDelta = RotatePoint(new PointF((float)centerLocalX, (float)centerLocalY), -start.Rotation);
        mask.X = start.X + centerWorldDelta.X;
        mask.Y = start.Y + centerWorldDelta.Y;
        mask.Width = Math.Max(.01, right - left);
        mask.Height = Math.Max(.01, bottom - top);
    }

    private void ApplyMoveSnapping(SceneLayerMask start, ref double x, ref double y)
    {
        double threshold = MaskHitThresholdUv(), hw = start.Width * .5, hh = start.Height * .5;
        double bestX = threshold, bestY = threshold;
        foreach (double guide in new[] { 0.0, .5, 1.0 })
        {
            foreach (double point in new[] { x, x - hw, x + hw })
            {
                double d = Math.Abs(point - guide);
                if (d < bestX) { x += guide - point; bestX = d; _maskSnapGuideX = (float)guide; }
            }
            foreach (double point in new[] { y, y - hh, y + hh })
            {
                double d = Math.Abs(point - guide);
                if (d < bestY) { y += guide - point; bestY = d; _maskSnapGuideY = (float)guide; }
            }
        }
    }

    private bool TryClientToLayerUv(PointF point, out PointF layerUv)
    {
        layerUv = default;
        int gridW = Math.Max(2, _scene.Width), gridH = Math.Max(2, _scene.Height);
        double viewW = Math.Max(1, Width), viewH = Math.Max(1, Height);
        double sceneAspect = Math.Max(.05, (gridW / (double)gridH) * .55), viewAspect = viewW / viewH;
        double renderedW = viewW, renderedH = viewH, originX = 0, originY = 0;
        if (PreviewViewMode == PreviewViewMode.Fit)
        {
            if (viewAspect > sceneAspect) { renderedW = viewH * sceneAspect; originX = (viewW - renderedW) * .5; }
            else { renderedH = viewW / sceneAspect; originY = (viewH - renderedH) * .5; }
        }
        else if (PreviewViewMode == PreviewViewMode.Fill)
        {
            if (viewAspect > sceneAspect) { renderedH = viewW / sceneAspect; originY = (viewH - renderedH) * .5; }
            else { renderedW = viewH * sceneAspect; originX = (viewW - renderedW) * .5; }
        }
        double zoom = Math.Clamp(PreviewZoom * _scene.Transform.Scale, .10, 4.0);
        renderedW *= zoom; renderedH *= zoom;
        originX = (viewW - renderedW) * .5; originY = (viewH - renderedH) * .5;
        double topU = (point.X - originX) / Math.Max(1.0, renderedW), topV = (point.Y - originY) / Math.Max(1.0, renderedH);
        if (topU < 0 || topU > 1 || topV < 0 || topV > 1) return false;
        double sourceX = topU, sourceYBottom = 1.0 - topV;
        SceneTransform transform = _scene.Transform;
        if (Math.Abs(transform.Rotation) > .001 || Math.Abs(transform.PerspectiveX) > .001 || Math.Abs(transform.PerspectiveY) > .001)
        {
            double px = sourceX * 2.0 - 1.0, py = sourceYBottom * 2.0 - 1.0;
            double aspect = Math.Max(.10, (gridW / (double)gridH) * .55);
            px *= aspect;
            double angle = -transform.Rotation * Math.PI / 180.0, c = Math.Cos(angle), sn = Math.Sin(angle);
            double rx = c * px - sn * py, ry = sn * px + c * py;
            double perspX = Math.Max(.20, 1.0 + transform.PerspectiveX * ry), perspY = Math.Max(.20, 1.0 + transform.PerspectiveY * rx);
            rx /= perspX; ry /= perspY; rx /= aspect;
            sourceX = rx * .5 + .5; sourceYBottom = ry * .5 + .5;
            if (sourceX < 0 || sourceX > 1 || sourceYBottom < 0 || sourceYBottom > 1) return false;
        }
        layerUv = new PointF((float)sourceX, (float)(1.0 - sourceYBottom));
        return true;
    }

    private double MaskHitThresholdUv()
        => 8.0 / Math.Max(64.0, Math.Min(Math.Max(1, Width), Math.Max(1, Height)));

    private static PointF RotateToMaskLocal(PointF uv, SceneLayerMask mask)
        => RotatePoint(new PointF((float)(uv.X - mask.X), (float)(uv.Y - mask.Y)), mask.Rotation);

    private static PointF RotatePoint(PointF point, double degrees)
    {
        double a = degrees * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
        return new PointF((float)(c * point.X - s * point.Y), (float)(s * point.X + c * point.Y));
    }

    private static double Distance(double ax, double ay, double bx, double by)
        => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    private static double SnapMaskRotation(double value)
    {
        const double step = 15.0, threshold = 3.0;
        double snapped = Math.Round(value / step) * step;
        return Math.Abs(NormalizeDegrees(value - snapped)) <= threshold ? NormalizeDegrees(snapped) : value;
    }

    private static double NormalizeDegrees(double value)
    {
        value %= 360.0;
        if (value > 180.0) value -= 360.0;
        if (value < -180.0) value += 360.0;
        return value;
    }

    private static Cursor CursorForMaskDrag(MaskDragMode mode) => mode switch
    {
        MaskDragMode.Left or MaskDragMode.Right or MaskDragMode.Top or MaskDragMode.Bottom or MaskDragMode.TopLeft or MaskDragMode.TopRight or MaskDragMode.BottomLeft or MaskDragMode.BottomRight => Cursors.SizeAll,
        MaskDragMode.Rotate => Cursors.Hand,
        _ => Cursors.SizeAll
    };

    private static void CopyMaskValues(SceneLayerMask source, SceneLayerMask target)
    {
        target.Name = source.Name; target.Enabled = source.Enabled; target.Type = source.Type; target.Invert = source.Invert;
        target.X = source.X; target.Y = source.Y; target.Width = source.Width; target.Height = source.Height; target.Rotation = source.Rotation;
        target.Feather = source.Feather; target.Strength = source.Strength; target.GradientAngle = source.GradientAngle;
        target.NoiseScale = source.NoiseScale; target.NoiseSeed = source.NoiseSeed; target.GradientSoftness = source.GradientSoftness;
        target.ShapeAmount = source.ShapeAmount; target.TriangleType = source.TriangleType; target.PolygonSides = source.PolygonSides; target.StarPoints = source.StarPoints;
    }
}
