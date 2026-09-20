using DrawingPoint = System.Drawing.Point;
using System.Windows.Forms;
using Gdk;
using Gtk;
using GtkScrollEventArgs = Gtk.ScrollEventArgs;
using GtkKeyPressEventArgs = Gtk.KeyPressEventArgs;

namespace Glyphore;

internal sealed partial class GlPreviewControl
{
    private static Keys ModifiersToKeys(ModifierType state)
    {
        Keys result = Keys.None;
        if ((state & ModifierType.ControlMask) != 0) result |= Keys.Control;
        if ((state & ModifierType.ShiftMask) != 0) result |= Keys.Shift;
        if ((state & ModifierType.Mod1Mask) != 0) result |= Keys.Alt;
        return result;
    }

    private void OnGlButtonPress(object? sender, ButtonPressEventArgs e)
    {
        if (_glArea is null || e.Event is null) return;
        var point = new DrawingPoint((int)Math.Round(e.Event.X), (int)Math.Round(e.Event.Y));
        var modifiers = ModifiersToKeys(e.Event.State);
        if (e.Event.Button != 1) return;

        if (TryBeginMaskEdit(point, modifiers))
        {
            _glArea.GrabFocus();
            return;
        }

        if (Camera3D.TryGetSpec(_settings.Effect, out var spec))
        {
            _cameraOrbiting = true;
            _cameraOrbitSpec = spec;
            _cameraOrbitLast = point;
            GtkNative.GtkGrab(_glArea.Handle);
            Cursor = Cursors.SizeAll;
        }
    }

    private void OnGlMotion(object? sender, MotionNotifyEventArgs e)
    {
        if (_glArea is null || e.Event is null) return;
        var point = new DrawingPoint((int)Math.Round(e.Event.X), (int)Math.Round(e.Event.Y));
        var modifiers = ModifiersToKeys(e.Event.State);
        if (UpdateMaskEdit(point, modifiers)) return;
        if (!_cameraOrbiting) return;

        if (!_settings.Effect.Equals(_cameraOrbitSpec.Effect, StringComparison.OrdinalIgnoreCase))
        {
            EndCameraOrbit();
            return;
        }

        double dx = point.X - _cameraOrbitLast.X;
        double dy = point.Y - _cameraOrbitLast.Y;
        _cameraOrbitLast = point;
        double yaw = Camera3D.WrapYaw(_settings.Get(_cameraOrbitSpec.YawKey) + dx * _cameraOrbitSpec.DragYawScale);
        double pitch = Math.Clamp(_settings.Get(_cameraOrbitSpec.PitchKey) - dy * _cameraOrbitSpec.DragPitchScale, _cameraOrbitSpec.MinPitch, _cameraOrbitSpec.MaxPitch);
        double distance = Math.Clamp(_settings.Get(_cameraOrbitSpec.DistanceKey), Camera3D.MinimumDistance(_cameraOrbitSpec, _settings), _cameraOrbitSpec.MaxDistance);
        _settings.Set(_cameraOrbitSpec.YawKey, yaw);
        _settings.Set(_cameraOrbitSpec.PitchKey, pitch);
        _settings.Set(_cameraOrbitSpec.DistanceKey, distance);
        CameraChanged?.Invoke(_cameraOrbitSpec.Effect, yaw, pitch, distance);
        if (_cameraOrbitSpec.Effect.Equals("SDF Lab", StringComparison.OrdinalIgnoreCase))
            SdfCameraChanged?.Invoke(yaw, pitch, distance);
        RequestNextFrameRendering();
    }

    private void OnGlButtonRelease(object? sender, ButtonReleaseEventArgs e)
    {
        if (_glArea is null || e.Event is null) return;
        if (e.Event.Button != 1) return;
        if (_maskDragMode != MaskDragMode.None)
        {
            EndMaskEdit(true);
            return;
        }
        if (_cameraOrbiting) EndCameraOrbit();
    }

    private void OnGlScroll(object? sender, GtkScrollEventArgs e)
    {
        if (e.Event is null || !Camera3D.TryGetSpec(_settings.Effect, out var spec)) return;
        int delta = e.Event.Direction switch
        {
            ScrollDirection.Up => 120,
            ScrollDirection.Down => -120,
            ScrollDirection.Smooth => (int)Math.Round(-e.Event.DeltaY * 120.0),
            _ => 0
        };
        if (delta == 0) return;

        double minDistance = Camera3D.MinimumDistance(spec, _settings);
        double distance = Math.Clamp(_settings.Get(spec.DistanceKey) - Math.Sign(delta) * spec.WheelStep, minDistance, spec.MaxDistance);
        _settings.Set(spec.DistanceKey, distance);
        CameraChanged?.Invoke(spec.Effect, _settings.Get(spec.YawKey), _settings.Get(spec.PitchKey), distance);
        if (spec.Effect.Equals("SDF Lab", StringComparison.OrdinalIgnoreCase))
            SdfCameraChanged?.Invoke(_settings.Get(spec.YawKey), _settings.Get(spec.PitchKey), distance);
        RequestNextFrameRendering();
    }

    private void OnGlKeyPress(object? sender, GtkKeyPressEventArgs e)
    {
        if (e.Event is null) return;
        Keys key = e.Event.Key switch
        {
            Gdk.Key.Escape => Keys.Escape,
            Gdk.Key.Delete => Keys.Delete,
            Gdk.Key.Left => Keys.Left,
            Gdk.Key.Right => Keys.Right,
            Gdk.Key.Up => Keys.Up,
            Gdk.Key.Down => Keys.Down,
            _ => Keys.None
        };
        if (key == Keys.None) return;
        HandlePreviewKey(key, ModifiersToKeys(e.Event.State));
    }

    private void EndCameraOrbit()
    {
        _cameraOrbiting = false;
        if (_glArea is not null) GtkNative.GtkUngrab(_glArea.Handle);
        Cursor = Cursors.Default;
    }
}
