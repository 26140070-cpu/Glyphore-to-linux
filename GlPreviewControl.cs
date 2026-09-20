using System.ComponentModel;
using System.Diagnostics;
using Majorsilence.Forms;

namespace Glyphore;

internal enum PreviewViewMode
{
    Fit = 0,
    Fill = 1,
    Stretch = 2
}

internal enum PreviewBackgroundMode
{
    Solid = 0,
    Checkerboard = 1
}

/// <summary>
/// Cross-platform preview surface. This control renders the shared CharacterRenderer
/// scene model directly through the Forms/Avalonia backend, so no platform-specific
/// window API is required here.
/// </summary>
internal sealed class GlPreviewControl : UserControl
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Timer _frameTimer;
    private int _frames;
    private long _fpsWindowStart = Stopwatch.GetTimestamp();
    private double _actualFps;
    private string _lastAsciiFrame = string.Empty;
    private string _rendererInfo = "Avalonia + Skia preview";
    private EffectSettings _settings = new();
    private GlyphoreScene _scene = GlyphoreScene.CreateDefault();
    private readonly Dictionary<Guid, LayerClockState> _layerClocks = new();
    private double _pausedAccum;
    private double _pauseAt;
    private bool _paused;
    private int _targetFps = 30;
    private double _animationTime;
    private double _temporalTime;
    private double _lastRenderedTime = double.NaN;
    private Guid? _activeMaskId;
    private bool _maskSnapping = true;
    private bool _maskRotationSnapping = true;
    private bool _showInactiveMasks;
    private Color _previewBackgroundColor = Color.Black;

    private double _drawX0;
    private double _drawY0;
    private double _drawWidthPx = 1;
    private double _drawHeightPx = 1;
    private MaskDragMode _maskDragMode = MaskDragMode.None;
    private SceneEffectLayer? _maskDragLayer;
    private double _maskDragOffsetX;
    private double _maskDragOffsetY;
    private double _maskDragFixedX;
    private double _maskDragFixedY;
    private const double MaskSnapStep = 0.02;
    private const double MaskHandleTolerancePx = 7.0;

    private enum MaskDragMode
    {
        None,
        Move,
        ResizeTopLeft,
        ResizeTopRight,
        ResizeBottomLeft,
        ResizeBottomRight
    }

    private sealed class LayerClockState
    {
        public double Animation;
        public double Temporal;
    }

    public event Action<double, double, string>? FrameStats;
    public event Action<string, double, double, double>? CameraChanged;
    public event Action<double, double, double>? SdfCameraChanged;
    public event Action<SceneEffectLayer, SceneLayerMask, bool>? MaskEdited;
    public event Action<SceneEffectLayer, SceneLayerMask>? MaskDeleteRequested;

    public Guid? ActiveMaskId
    {
        get => _activeMaskId;
        set { if (_activeMaskId == value) return; _activeMaskId = value; InvalidatePreview(); }
    }

    public bool MaskSnapping
    {
        get => _maskSnapping;
        set { _maskSnapping = value; InvalidatePreview(); }
    }

    public bool MaskRotationSnapping
    {
        get => _maskRotationSnapping;
        set { _maskRotationSnapping = value; InvalidatePreview(); }
    }

    public bool ShowInactiveMasks
    {
        get => _showInactiveMasks;
        set { _showInactiveMasks = value; InvalidatePreview(); }
    }

    public PreviewBackgroundMode PreviewBackgroundMode { get; set; } = PreviewBackgroundMode.Solid;
    public PreviewViewMode PreviewViewMode { get; set; } = PreviewViewMode.Stretch;
    public double PreviewZoom { get; set; } = 1.0;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color PreviewBackgroundColor
    {
        get => _previewBackgroundColor;
        set
        {
            if (_previewBackgroundColor == value) return;
            _previewBackgroundColor = value;
            BackColor = value;
            InvalidatePreview();
        }
    }

    internal GlyphoreScene Scene
    {
        get => _scene;
        set
        {
            _scene = value ?? GlyphoreScene.CreateDefault();
            _layerClocks.Clear();
            _lastRenderedTime = double.NaN;
            SyncSettingsFromScene();
            InvalidatePreview();
        }
    }

    internal EffectSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value ?? new EffectSettings();
            InvalidatePreview();
        }
    }

    public int TargetFps
    {
        get => _targetFps;
        set
        {
            _targetFps = Math.Max(1, value);
            _frameTimer.Interval = Math.Max(1, (int)Math.Round(1000.0 / _targetFps));
        }
    }

    public string GpuInfo => _rendererInfo;
    public double CurrentTimeSeconds => _paused ? _pauseAt : _clock.Elapsed.TotalSeconds - _pausedAccum;
    public bool Paused => _paused;

    public GlPreviewControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = true;
        BackColor = _previewBackgroundColor;
        _frameTimer = new Timer { Interval = Math.Max(1, 1000 / _targetFps) };
        _frameTimer.Tick += (_, _) =>
        {
            if (!_paused) InvalidatePreview();
            RenderFrame(CurrentTimeSeconds, notifyStats: true);
        };
        Load += (_, _) =>
        {
            _frameTimer.Start();
            RenderFrame(CurrentTimeSeconds, notifyStats: false);
        };
        Disposed += (_, _) => _frameTimer.Stop();
    }

    public void InvalidatePreview()
    {
        RenderFrame(CurrentTimeSeconds, notifyStats: false);
        Invalidate();
    }

    public void TogglePause()
    {
        if (!_paused)
        {
            _pauseAt = CurrentTimeSeconds;
            _paused = true;
        }
        else
        {
            _pausedAccum = _clock.Elapsed.TotalSeconds - _pauseAt;
            _paused = false;
        }
        InvalidatePreview();
    }

    public void RestartAnimation()
    {
        _clock.Restart();
        _pausedAccum = 0;
        _pauseAt = 0;
        _paused = false;
        _animationTime = 0;
        _temporalTime = 0;
        _layerClocks.Clear();
        _lastRenderedTime = double.NaN;
        InvalidatePreview();
    }

    public void DuplicateLayerClock(Guid sourceLayerId, Guid targetLayerId)
    {
        if (!_layerClocks.TryGetValue(sourceLayerId, out var source)) return;
        _layerClocks[targetLayerId] = new LayerClockState { Animation = source.Animation, Temporal = source.Temporal };
    }

    public void ForgetLayerRuntime(Guid layerId) => _layerClocks.Remove(layerId);

    public string RenderText()
    {
        if (string.IsNullOrEmpty(_lastAsciiFrame))
            RenderFrame(CurrentTimeSeconds, notifyStats: false);
        return _lastAsciiFrame;
    }

    public string CaptureAsciiFrame(double timeSeconds)
    {
        return CharacterRenderer.Render(_scene, timeSeconds);
    }

    public Task<string> CaptureAsciiFrameAsync(double timeSeconds)
        => Task.FromResult(CaptureAsciiFrame(timeSeconds));

    public string CaptureCurrentAsciiFrame() => CaptureAsciiFrame(CurrentTimeSeconds);
    public Task<string> CaptureCurrentAsciiFrameAsync() => Task.FromResult(CaptureCurrentAsciiFrame());

    public ExportFrame CaptureExportFrame(double timeSeconds, bool includeRaster = false,
        RasterBackgroundMode backgroundMode = RasterBackgroundMode.SceneBackground, Color? solidColor = null)
        => BuildExportFrame(_scene, timeSeconds, includeRaster, backgroundMode, solidColor);

    public ExportFrame CaptureExportFrame(GlyphoreScene sceneSnapshot, double timeSeconds, bool includeRaster = false,
        RasterBackgroundMode backgroundMode = RasterBackgroundMode.SceneBackground, Color? solidColor = null)
        => BuildExportFrame(sceneSnapshot, timeSeconds, includeRaster, backgroundMode, solidColor);

    public Task<ExportFrame> CaptureExportFrameAsync(GlyphoreScene? sceneSnapshot, double timeSeconds,
        bool includeRaster = false, RasterBackgroundMode backgroundMode = RasterBackgroundMode.SceneBackground,
        Color? solidColor = null)
        => Task.FromResult(CaptureExportFrame(sceneSnapshot ?? _scene, timeSeconds, includeRaster, backgroundMode, solidColor));

    public RasterFrame CaptureRasterExportFrame(GlyphoreScene sceneSnapshot, double timeSeconds,
        RasterBackgroundMode backgroundMode, Color? solidColor = null, byte[]? reusableRgba = null)
        => BuildRasterFrame(sceneSnapshot, timeSeconds, backgroundMode, solidColor, reusableRgba);

    public Task<RasterFrame> CaptureRasterExportFrameAsync(GlyphoreScene sceneSnapshot, double timeSeconds,
        RasterBackgroundMode backgroundMode, Color? solidColor = null, byte[]? reusableRgba = null)
        => Task.FromResult(CaptureRasterExportFrame(sceneSnapshot, timeSeconds, backgroundMode, solidColor, reusableRgba));

    public double BenchmarkGpu(int frames = 240)
    {
        frames = Math.Clamp(frames, 1, 5000);
        var sw = Stopwatch.StartNew();
        double t = CurrentTimeSeconds;
        for (int i = 0; i < frames; i++)
            _ = CharacterRenderer.Render(_scene, t + i / (double)Math.Max(1, _targetFps));
        sw.Stop();
        _rendererInfo = "Avalonia + Skia software scene preview";
        return sw.Elapsed.TotalMilliseconds / frames;
    }

    public Task<double> BenchmarkGpuAsync(int frames = 240) => Task.FromResult(BenchmarkGpu(frames));

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        DrawBackground(e.Graphics, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        RenderFrame(CurrentTimeSeconds, notifyStats: false);
        DrawAscii(e.Graphics, ClientRectangle, _lastAsciiFrame, _scene);
        DrawMaskOverlay(e.Graphics);
    }

    private void RenderFrame(double timeSeconds, bool notifyStats)
    {
        if (!double.IsFinite(timeSeconds)) timeSeconds = 0;
        _lastAsciiFrame = CharacterRenderer.Render(_scene, timeSeconds);
        _lastRenderedTime = timeSeconds;
        _frames++;
        if (notifyStats)
        {
            double elapsed = (Stopwatch.GetTimestamp() - _fpsWindowStart) / (double)Stopwatch.Frequency;
            if (elapsed >= 1.0)
            {
                _actualFps = _frames / elapsed;
                _frames = 0;
                _fpsWindowStart = Stopwatch.GetTimestamp();
            }
            double ms = elapsed > 0 ? 1000.0 / Math.Max(.01, _actualFps) : 0;
            FrameStats?.Invoke(_actualFps, ms, GpuInfo);
        }
    }

    private void SyncSettingsFromScene()
    {
        var layer = _scene.EnsureActiveLayer(_settings);
        _settings = _scene.CreateSettings(layer);
        TargetFps = Math.Max(1, _scene.Fps);
    }

    private static void DrawBackground(Graphics graphics, Rectangle bounds)
    {
        using var brush = new SolidBrush(Color.Black);
        graphics.FillRectangle(brush, bounds);
    }

    private void DrawAscii(Graphics graphics, Rectangle bounds, string text, GlyphoreScene scene)
    {
        if (bounds.Width <= 2 || bounds.Height <= 2 || string.IsNullOrEmpty(text)) return;

        if (PreviewBackgroundMode == PreviewBackgroundMode.Checkerboard)
        {
            const int tile = 12;
            using var a = new SolidBrush(Color.FromArgb(24, 24, 24));
            using var b = new SolidBrush(Color.FromArgb(36, 36, 36));
            for (int y = bounds.Top; y < bounds.Bottom; y += tile)
                for (int x = bounds.Left; x < bounds.Right; x += tile)
                    graphics.FillRectangle(((x / tile + y / tile) & 1) == 0 ? a : b, x, y, tile, tile);
        }
        else
        {
            using var brush = new SolidBrush(PreviewBackgroundColor);
            graphics.FillRectangle(brush, bounds);
        }

        string[] rows = text.Replace("\r", string.Empty).Split('\n');
        int columns = rows.Max(r => r.Length);
        int rowCount = Math.Max(1, rows.Length);
        if (columns == 0) return;

        double zoom = Math.Clamp(PreviewZoom, .10, 8.0);
        double sx = bounds.Width / Math.Max(1.0, columns * 0.62);
        double sy = bounds.Height / Math.Max(1.0, rowCount * 1.05);
        double size = PreviewViewMode switch
        {
            PreviewViewMode.Fit => Math.Min(sx, sy),
            PreviewViewMode.Fill => Math.Max(sx, sy),
            _ => Math.Min(sx, sy)
        } * zoom;
        size = Math.Clamp(size, 5, 72);

        using var font = new Font(FontFamily.GenericMonospace, (float)size, FontStyle.Regular, GraphicsUnit.Pixel);
        var measure = graphics.MeasureString("M", font);
        double cellW = Math.Max(1, measure.Width * .95);
        double cellH = Math.Max(1, measure.Height * .92);
        double sceneW = columns * cellW;
        double sceneH = rowCount * cellH;

        double x0 = bounds.Left + (bounds.Width - sceneW) / 2.0;
        double y0 = bounds.Top + (bounds.Height - sceneH) / 2.0;
        if (PreviewViewMode == PreviewViewMode.Stretch)
        {
            cellW = bounds.Width / (double)Math.Max(1, columns);
            cellH = bounds.Height / (double)Math.Max(1, rowCount);
            x0 = bounds.Left;
            y0 = bounds.Top;
        }

        string charset = string.IsNullOrEmpty(scene.Charset) ? " @" : scene.Charset;
        for (int y = 0; y < rowCount; y++)
        {
            string row = rows[y];
            for (int x = 0; x < row.Length; x++)
            {
                char ch = row[x];
                if (char.IsWhiteSpace(ch)) continue;
                double intensity = charset.Length <= 1 ? 1 : Math.Clamp(charset.IndexOf(ch), 0, charset.Length - 1) / (double)(charset.Length - 1);
                Color color = scene.ColorEnabled ? PaletteColor(scene.PaletteStops, intensity) : Color.White;
                using var brush = new SolidBrush(color);
                graphics.DrawString(ch.ToString(), font, brush, (float)(x0 + x * cellW), (float)(y0 + y * cellH));
            }
        }

        _drawX0 = x0;
        _drawY0 = y0;
        _drawWidthPx = Math.Max(1, columns * cellW);
        _drawHeightPx = Math.Max(1, rowCount * cellH);
    }

    private bool ScreenToNormalized(int screenX, int screenY, out double nx, out double ny)
    {
        nx = (screenX - _drawX0) / _drawWidthPx;
        ny = (screenY - _drawY0) / _drawHeightPx;
        return nx >= -0.5 && nx <= 1.5 && ny >= -0.5 && ny <= 1.5;
    }

    private (SceneLayerMask Mask, MaskDragMode Mode)? HitTestMask(IReadOnlyList<SceneLayerMask> masks, double nx, double ny)
    {
        double tolX = MaskHandleTolerancePx / _drawWidthPx;
        double tolY = MaskHandleTolerancePx / _drawHeightPx;
        for (int i = masks.Count - 1; i >= 0; i--)
        {
            var mask = masks[i];
            if (!mask.Enabled) continue;
            double left = mask.X - mask.Width / 2.0;
            double right = mask.X + mask.Width / 2.0;
            double top = mask.Y - mask.Height / 2.0;
            double bottom = mask.Y + mask.Height / 2.0;

            if (Math.Abs(nx - left) <= tolX && Math.Abs(ny - top) <= tolY) return (mask, MaskDragMode.ResizeTopLeft);
            if (Math.Abs(nx - right) <= tolX && Math.Abs(ny - top) <= tolY) return (mask, MaskDragMode.ResizeTopRight);
            if (Math.Abs(nx - left) <= tolX && Math.Abs(ny - bottom) <= tolY) return (mask, MaskDragMode.ResizeBottomLeft);
            if (Math.Abs(nx - right) <= tolX && Math.Abs(ny - bottom) <= tolY) return (mask, MaskDragMode.ResizeBottomRight);

            double dx = (nx - mask.X) / Math.Max(.001, mask.Width * .5);
            double dy = (ny - mask.Y) / Math.Max(.001, mask.Height * .5);
            bool inside = mask.Type switch
            {
                SceneMaskType.Ellipse => dx * dx + dy * dy <= 1,
                SceneMaskType.Diamond => Math.Abs(dx) + Math.Abs(dy) <= 1,
                SceneMaskType.Ring => dx * dx + dy * dy is >= .45 and <= 1,
                SceneMaskType.Triangle => dy >= -1 && dy <= 1 && Math.Abs(dx) <= 1 - dy * .5,
                _ => Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1
            };
            if (inside) return (mask, MaskDragMode.Move);
        }
        return null;
    }

    private static double SnapValue(double value, bool snap)
        => snap ? Math.Round(value / MaskSnapStep) * MaskSnapStep : value;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (!ScreenToNormalized(e.X, e.Y, out double nx, out double ny)) return;

        var layer = _scene.EnsureActiveLayer(_settings);
        var hit = HitTestMask(layer.Masks, nx, ny);
        if (hit is null)
        {
            return;
        }

        Focus();
        var (mask, mode) = hit.Value;
        ActiveMaskId = mask.Id;
        _maskDragLayer = layer;
        _maskDragMode = mode;

        if (mode == MaskDragMode.Move)
        {
            _maskDragOffsetX = nx - mask.X;
            _maskDragOffsetY = ny - mask.Y;
        }
        else
        {
            double left = mask.X - mask.Width / 2.0;
            double right = mask.X + mask.Width / 2.0;
            double top = mask.Y - mask.Height / 2.0;
            double bottom = mask.Y + mask.Height / 2.0;
            _maskDragFixedX = mode is MaskDragMode.ResizeTopLeft or MaskDragMode.ResizeBottomLeft ? right : left;
            _maskDragFixedY = mode is MaskDragMode.ResizeTopLeft or MaskDragMode.ResizeTopRight ? bottom : top;
        }

        InvalidatePreview();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_maskDragMode == MaskDragMode.None || _maskDragLayer is null) return;
        if (!ScreenToNormalized(e.X, e.Y, out double nx, out double ny)) return;

        var mask = _maskDragLayer.Masks.FirstOrDefault(m => m.Id == ActiveMaskId);
        if (mask is null)
        {
            _maskDragMode = MaskDragMode.None;
            _maskDragLayer = null;
            return;
        }

        if (_maskDragMode == MaskDragMode.Move)
        {
            mask.X = SnapValue(nx - _maskDragOffsetX, _maskSnapping);
            mask.Y = SnapValue(ny - _maskDragOffsetY, _maskSnapping);
        }
        else
        {
            double newX = SnapValue(nx, _maskSnapping);
            double newY = SnapValue(ny, _maskSnapping);
            mask.Width = Math.Abs(newX - _maskDragFixedX);
            mask.Height = Math.Abs(newY - _maskDragFixedY);
            mask.X = (newX + _maskDragFixedX) / 2.0;
            mask.Y = (newY + _maskDragFixedY) / 2.0;
        }

        mask.Clamp();
        MaskEdited?.Invoke(_maskDragLayer, mask, false);
        InvalidatePreview();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_maskDragMode == MaskDragMode.None || _maskDragLayer is null) return;

        var mask = _maskDragLayer.Masks.FirstOrDefault(m => m.Id == ActiveMaskId);
        var layer = _maskDragLayer;
        _maskDragMode = MaskDragMode.None;
        _maskDragLayer = null;
        if (mask is not null) MaskEdited?.Invoke(layer!, mask, true);
        InvalidatePreview();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Delete && e.KeyCode != Keys.Back) return;
        if (ActiveMaskId is not { } activeId) return;

        var layer = _scene.EnsureActiveLayer(_settings);
        var mask = layer.Masks.FirstOrDefault(m => m.Id == activeId);
        if (mask is null) return;

        MaskDeleteRequested?.Invoke(layer, mask);
        e.Handled = true;
    }

    private void DrawMaskOverlay(Graphics graphics)
    {
        var layer = _scene.EnsureActiveLayer(_settings);
        if (layer.Masks.Count == 0) return;

        foreach (var mask in layer.Masks)
        {
            if (!mask.Enabled) continue;
            bool active = mask.Id == ActiveMaskId;
            if (!active && !_showInactiveMasks) continue;

            double left = _drawX0 + (mask.X - mask.Width / 2.0) * _drawWidthPx;
            double top = _drawY0 + (mask.Y - mask.Height / 2.0) * _drawHeightPx;
            double w = mask.Width * _drawWidthPx;
            double h = mask.Height * _drawHeightPx;

            using var pen = new Pen(active ? Color.FromArgb(220, 90, 220, 120) : Color.FromArgb(120, 200, 200, 200), active ? 2f : 1f);
            pen.DashStyle = active ? Majorsilence.Forms.Drawing.Drawing2D.DashStyle.Solid : Majorsilence.Forms.Drawing.Drawing2D.DashStyle.Dash;
            graphics.DrawRectangle(pen, (float)left, (float)top, (float)w, (float)h);

            if (!active) continue;
            const int handle = 6;
            using var handleBrush = new SolidBrush(Color.FromArgb(230, 90, 220, 120));
            graphics.FillRectangle(handleBrush, (float)(left - handle / 2.0), (float)(top - handle / 2.0), handle, handle);
            graphics.FillRectangle(handleBrush, (float)(left + w - handle / 2.0), (float)(top - handle / 2.0), handle, handle);
            graphics.FillRectangle(handleBrush, (float)(left - handle / 2.0), (float)(top + h - handle / 2.0), handle, handle);
            graphics.FillRectangle(handleBrush, (float)(left + w - handle / 2.0), (float)(top + h - handle / 2.0), handle, handle);
        }
    }

    private static Color PaletteColor(IReadOnlyList<string> stops, double t)
    {
        if (stops.Count == 0) return Color.White;
        if (stops.Count == 1) return ColorUtil.ParseHtmlOrWhite(stops[0]);
        t = Math.Clamp(t, 0, 1);
        double scaled = t * (stops.Count - 1);
        int index = Math.Clamp((int)Math.Floor(scaled), 0, stops.Count - 2);
        double local = scaled - index;
        Color a = ColorUtil.ParseHtmlOrWhite(stops[index]);
        Color b = ColorUtil.ParseHtmlOrWhite(stops[index + 1]);
        return Color.FromArgb(
            (byte)Math.Round(a.R + (b.R - a.R) * local),
            (byte)Math.Round(a.G + (b.G - a.G) * local),
            (byte)Math.Round(a.B + (b.B - a.B) * local));
    }

    private static ExportFrame BuildExportFrame(GlyphoreScene scene, double timeSeconds, bool includeRaster,
        RasterBackgroundMode backgroundMode, Color? solidColor)
    {
        string text = CharacterRenderer.Render(scene, timeSeconds);
        BuildCellBuffers(scene, text, out byte[] rgb24, out byte[] alpha8);
        RasterFrame? raster = includeRaster ? BuildRasterFrame(scene, timeSeconds, backgroundMode, solidColor, null) : null;
        return new ExportFrame(text, rgb24, alpha8, raster);
    }

    private static void BuildCellBuffers(GlyphoreScene scene, string text, out byte[] rgb24, out byte[] alpha8)
    {
        int cols = Math.Max(1, scene.Width);
        int rows = Math.Max(1, scene.Height);
        rgb24 = new byte[checked(cols * rows * 3)];
        alpha8 = new byte[checked(cols * rows)];
        string[] lines = text.Replace("\r", string.Empty).Split('\n');
        string charset = string.IsNullOrEmpty(scene.Charset) ? " @" : scene.Charset;

        for (int y = 0; y < rows; y++)
        {
            string line = y < lines.Length ? lines[y] : string.Empty;
            int x = 0;
            foreach (var rune in line.EnumerateRunes())
            {
                if (x >= cols) break;
                char ch = rune.Value <= char.MaxValue ? (char)rune.Value : ' ';
                if (!char.IsWhiteSpace(ch))
                {
                    double intensity = charset.Length <= 1 ? 1 : Math.Clamp(charset.IndexOf(ch), 0, charset.Length - 1) / (double)(charset.Length - 1);
                    Color color = scene.ColorEnabled ? PaletteColor(scene.PaletteStops, intensity) : Color.White;
                    int rgb = (y * cols + x) * 3;
                    rgb24[rgb] = color.R; rgb24[rgb + 1] = color.G; rgb24[rgb + 2] = color.B;
                    alpha8[y * cols + x] = 255;
                }
                x++;
            }
        }
    }

    private static RasterFrame BuildRasterFrame(GlyphoreScene scene, double timeSeconds,
        RasterBackgroundMode backgroundMode, Color? solidColor, byte[]? reusableRgba)
    {
        int cols = Math.Max(2, scene.Width);
        int rows = Math.Max(2, scene.Height);
        const int cellW = 10;
        const int cellH = 18;
        int width = checked(cols * cellW);
        int height = checked(rows * cellH);
        int length = checked(width * height * 4);
        byte[] rgba = reusableRgba is { Length: var n } && n == length ? reusableRgba : new byte[length];

        Color bg = backgroundMode switch
        {
            RasterBackgroundMode.SolidColor => solidColor ?? Color.Black,
            RasterBackgroundMode.SceneBackground => ColorUtil.ParseHtmlOrWhite(scene.BackgroundColor),
            _ => Color.Black
        };
        bool transparent = backgroundMode == RasterBackgroundMode.Transparent;
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = bg.R; rgba[i + 1] = bg.G; rgba[i + 2] = bg.B; rgba[i + 3] = transparent ? (byte)0 : (byte)255;
        }

        string text = CharacterRenderer.Render(scene, timeSeconds);
        string[] rowsText = text.Replace("\r", string.Empty).Split('\n');
        string charset = string.IsNullOrEmpty(scene.Charset) ? " @" : scene.Charset;
        for (int y = 0; y < Math.Min(rows, rowsText.Length); y++)
        {
            string row = rowsText[y];
            for (int x = 0; x < Math.Min(cols, row.Length); x++)
            {
                char ch = row[x];
                if (char.IsWhiteSpace(ch)) continue;
                double intensity = charset.Length <= 1 ? 1 : Math.Clamp(charset.IndexOf(ch), 0, charset.Length - 1) / (double)(charset.Length - 1);
                Color color = scene.ColorEnabled ? PaletteColor(scene.PaletteStops, intensity) : Color.White;
                for (int yy = 0; yy < cellH; yy++)
                {
                    int py = y * cellH + yy;
                    for (int xx = 0; xx < cellW; xx++)
                    {
                        int px = x * cellW + xx;
                        int i = (py * width + px) * 4;
                        rgba[i] = color.R; rgba[i + 1] = color.G; rgba[i + 2] = color.B; rgba[i + 3] = 255;
                    }
                }
            }
        }
        return new RasterFrame(width, height, rgba);
    }
}
