using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using Gdk;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Gtk;
using SkiaSharp;

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

internal sealed partial class GlPreviewControl : UserControl
{
    private uint _previewProgram;
    private uint _captureProgram;
    private uint _vao;
    private uint _atlas;
    private uint _intensityTex;
    private uint _intensityFbo;
    private uint _colorTex;
    private uint _colorFbo;
    private uint _glyphSelectTex;
    private uint _glyphSelectFbo;
    private uint _glyphMapTex;
    private uint _rasterExportTex;
    private uint _rasterExportFbo;
    private int _rasterExportW;
    private int _rasterExportH;
    private int _intensityW;
    private int _intensityH;
    private int _atlasCols = 1, _atlasRows = 1;
    private int _atlasGlyphCount = 1;
    private string _atlasRamp = "";
    private string _sceneGlyphSignature = "";
    private readonly Dictionary<Guid, (int Slot, int Count)> _layerGlyphRows = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Stopwatch _fpsClock = Stopwatch.StartNew();
    private int _frames;
    private double _actualFps;
    private static readonly Dictionary<(uint Program, string Name), int> UniformLocationCache = new();
    private volatile bool _loaded;
    private string _gpuInfo = "OpenGL no inicializado";
    private EffectSettings _settings = new();
    private GlyphoreScene _scene = GlyphoreScene.CreateDefault();
    private readonly Dictionary<Guid, LayerClockState> _layerClocks = new();
    private readonly Dictionary<Guid, LayerRenderSettings> _layerRenderSettings = new();
    private double _lastSceneTimelineTime;
    private bool _sceneClockInitialized;
    private int _targetFps = 30;
    private volatile bool _paused;
    private double _pauseAt;
    private double _pausedAccum;
    private double _animationTime;
    private double _temporalTime;
    private double _lastTimelineTime;
    private bool _previewClockInitialized;
    private bool _cameraOrbiting;
    private DrawingPoint _cameraOrbitLast;
    private Camera3DSpec _cameraOrbitSpec;
    private Guid? _activeMaskId;
    private bool _maskSnapping = true;
    private bool _maskRotationSnapping = true;
    private bool _showInactiveMasks;
    private float _maskSnapGuideX = -1f;
    private float _maskSnapGuideY = -1f;
    private readonly System.Windows.Forms.Timer _frameTimer;
    private GLArea? _glArea;
    private bool _glResourcesDisposed;
    private string? _lastAsciiFrame;

    private sealed class LayerClockState
    {
        public double Animation;
        public double Temporal;
    }

    private sealed class LayerRenderSettings
    {
        public int CommonRevision = int.MinValue;
        public int LayerRevision = int.MinValue;
        public EffectSettings Settings = new();
    }

    private sealed class TitleTextureResource
    {
        public uint Texture;
        public string Signature = string.Empty;
    }

    private readonly struct CaptureRequest(double animation, double temporal, TaskCompletionSource<string> tcs)
    {
        public readonly double Animation = animation;
        public readonly double Temporal = temporal;
        public readonly TaskCompletionSource<string> Tcs = tcs;
    }

    private readonly object _captureQueueGate = new();
    private readonly Queue<CaptureRequest> _captureQueue = new();
    private readonly Queue<ExportCaptureRequest> _exportCaptureQueue = new();
    private readonly Queue<RasterCaptureRequest> _rasterCaptureQueue = new();
    private TaskCompletionSource<double>? _benchmarkRequest;
    private int _benchmarkFrames;
    private readonly Dictionary<Guid, TitleTextureResource> _titleTextures = new();

    public event Action<double, double, string>? FrameStats;
    public event Action<string, double, double, double>? CameraChanged;
    public event Action<double, double, double>? SdfCameraChanged;
    public event Action<SceneEffectLayer, SceneLayerMask, bool>? MaskEdited;
    public event Action<SceneEffectLayer, SceneLayerMask>? MaskDeleteRequested;

    public Guid? ActiveMaskId
    {
        get => _activeMaskId;
        set { if (_activeMaskId == value) return; _activeMaskId = value; RequestNextFrameRendering(); }
    }

    public bool MaskSnapping
    {
        get => _maskSnapping;
        set { _maskSnapping = value; if (!value) { _maskSnapGuideX = -1f; _maskSnapGuideY = -1f; } RequestNextFrameRendering(); }
    }

    public bool MaskRotationSnapping
    {
        get => _maskRotationSnapping;
        set { _maskRotationSnapping = value; RequestNextFrameRendering(); }
    }

    public bool ShowInactiveMasks
    {
        get => _showInactiveMasks;
        set { _showInactiveMasks = value; RequestNextFrameRendering(); }
    }

    public PreviewBackgroundMode PreviewBackgroundMode { get; set; } = PreviewBackgroundMode.Solid;
    public PreviewViewMode PreviewViewMode { get; set; } = PreviewViewMode.Stretch;
    public double PreviewZoom { get; set; } = 1.0;
    private DrawingColor _previewBackgroundColor = DrawingColor.Black;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DrawingColor PreviewBackgroundColor
    {
        get => _previewBackgroundColor;
        set
        {
            if (_previewBackgroundColor == value) return;
            _previewBackgroundColor = value;
            BackColor = value;
            RequestNextFrameRendering();
        }
    }

    internal GlyphoreScene Scene
    {
        get => _scene;
        set
        {
            _scene = value ?? GlyphoreScene.CreateDefault();
            _layerClocks.Clear();
            _layerRenderSettings.Clear();
            _lastSceneTimelineTime = 0;
            _sceneClockInitialized = false;
            SyncSettingsFromScene();
            RequestNextFrameRendering();
        }
    }

    internal EffectSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            SyncAtlasIfNeeded();
            if (_paused) RequestNextFrameRendering();
        }
    }

    public int TargetFps
    {
        get => _targetFps;
        set
        {
            int clamped = Math.Max(1, value);
            if (_targetFps == clamped) return;
            _targetFps = clamped;
            _frameTimer.Interval = Math.Max(1, (int)Math.Round(1000.0 / _targetFps));
            if (IsHandleCreated)
            {
                _frameTimer.Stop();
                _frameTimer.Start();
            }
        }
    }

    public void InvalidatePreview() => RequestNextFrameRendering();

    public string GpuInfo => _gpuInfo;
    public double CurrentTimeSeconds => _paused ? _pauseAt : _clock.Elapsed.TotalSeconds - _pausedAccum;
    public bool Paused => _paused;

    public void TogglePause()
    {
        if (!_paused)
        {
            _pauseAt = _clock.Elapsed.TotalSeconds - _pausedAccum;
            _paused = true;
        }
        else
        {
            _pausedAccum = _clock.Elapsed.TotalSeconds - _pauseAt;
            _paused = false;
        }
        RequestNextFrameRendering();
    }

    public void RestartAnimation()
    {
        _clock.Restart();
        _paused = false;
        _pauseAt = 0;
        _pausedAccum = 0;
        _animationTime = 0;
        _temporalTime = 0;
        _lastTimelineTime = 0;
        _previewClockInitialized = false;
        _layerClocks.Clear();
        _lastSceneTimelineTime = 0;
        _sceneClockInitialized = false;
        RequestNextFrameRendering();
    }

    public void DuplicateLayerClock(Guid sourceLayerId, Guid targetLayerId)
    {
        if (!_layerClocks.TryGetValue(sourceLayerId, out var sourceClock)) return;
        _layerClocks[targetLayerId] = new LayerClockState { Animation = sourceClock.Animation, Temporal = sourceClock.Temporal };
    }

    public void ForgetLayerRuntime(Guid layerId)
    {
        _layerClocks.Remove(layerId);
        _layerRenderSettings.Remove(layerId);
        if (_titleTextures.TryGetValue(layerId, out var title))
        {
            if (_loaded && title.Texture != 0)
            {
                uint texture = title.Texture;
                NativeGl.DeleteTextures(1, ref texture);
            }
            _titleTextures.Remove(layerId);
        }
    }

    public string RenderText()
    {
        if (!_loaded || string.IsNullOrEmpty(_lastAsciiFrame))
            return CharacterRenderer.Render(_scene, CurrentTimeSeconds);
        return _lastAsciiFrame;
    }

    public GlPreviewControl()
    {
        SetStyle(ControlStyles.Opaque | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = DrawingColor.Black;
        _frameTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1, (int)Math.Round(1000.0 / _targetFps)) };
        _frameTimer.Tick += (_, _) =>
        {
            if (!_paused) RequestNextFrameRendering();
        };
        Load += HandleLoaded;
        Disposed += HandleDisposed;
    }

    private void HandleLoaded(object? sender, EventArgs e)
    {
        EnsureGlArea();
        _frameTimer.Start();
    }

    private void HandleDisposed(object? sender, EventArgs e)
    {
        _frameTimer.Stop();
        CleanupGlResources();
        _glArea = null;
    }

    private void SyncSettingsFromScene()
    {
        var layer = _scene.EnsureActiveLayer(_settings);
        _settings = _scene.CreateSettings(layer);
        TargetFps = Math.Max(1, _scene.Fps);
        SyncAtlasIfNeeded();
    }

    private void EnsureGlArea()
    {
        if (_glArea is not null || !IsHandleCreated) return;

        _glArea = new GLArea();
        GtkNative.gtk_gl_area_set_required_version(_glArea.Handle, 3, 3);
        GtkNative.gtk_gl_area_set_auto_render(_glArea.Handle, 0);
        GtkNative.gtk_gl_area_set_has_depth_buffer(_glArea.Handle, 0);
        GtkNative.gtk_gl_area_set_has_stencil_buffer(_glArea.Handle, 0);
        GtkNative.gtk_widget_set_can_focus(_glArea.Handle, 1);
        GtkNative.gtk_widget_add_events(_glArea.Handle, (int)(EventMask.ButtonPressMask | EventMask.ButtonReleaseMask |
                                 EventMask.PointerMotionMask | EventMask.ScrollMask | EventMask.KeyPressMask | EventMask.KeyReleaseMask));
        _glArea.Realized += (_, _) => InitializeGlResources();
        _glArea.Unrealized += (_, _) => CleanupGlResources();
        _glArea.Render += (_, _) => RenderFromGlArea();
        _glArea.ButtonPressEvent += OnGlButtonPress;
        _glArea.ButtonReleaseEvent += OnGlButtonRelease;
        _glArea.MotionNotifyEvent += OnGlMotion;
        _glArea.ScrollEvent += OnGlScroll;
        _glArea.KeyPressEvent += OnGlKeyPress;

        GtkNative.gtk_widget_set_hexpand(_glArea.Handle, 1);
        GtkNative.gtk_widget_set_vexpand(_glArea.Handle, 1);
        GtkNative.gtk_widget_set_can_focus(_glArea.Handle, 1);
        GtkNative.gtk_container_add(Handle, _glArea.Handle);
        GtkNative.gtk_widget_show(_glArea.Handle);
    }

    private void InitializeGlResources()
    {
        if (_glArea is null) return;
        try
        {
            GtkNative.MakeCurrent(_glArea.Handle);
            GtkNative.CheckGlAreaError(_glArea.Handle);
            NativeGl.Load(GtkNative.GetProcAddress);
            UniformLocationCache.Clear();
            string vert = ReadResource("fullscreen.vert.glsl");
            string effects = ReadResource("effects.glsl");
            string preview = ReadResource("preview.frag.glsl");
            string intensity = ReadResource("intensity.frag.glsl").Replace("/*__EFFECTS__*/", effects);
            _previewProgram = NativeGl.CompileProgram(vert, preview);
            _captureProgram = NativeGl.CompileProgram(vert, intensity);
            unsafe
            {
                uint vao;
                NativeGl.GenVertexArrays(1, &vao);
                _vao = vao;
            }
            NativeGl.BindVertexArray(_vao);
            SyncSettingsFromScene();
            SyncSceneGlyphResources(_scene);
            _gpuInfo = GetGlString(NativeGl.GL_RENDERER) + " · OpenGL " + GetGlString(NativeGl.GL_VERSION);
            _loaded = true;
            _glResourcesDisposed = false;
            RequestNextFrameRendering();
        }
        catch (Exception ex)
        {
            _loaded = false;
            _gpuInfo = "OpenGL error: " + ex.Message;
            RequestNextFrameRendering();
        }
    }

    private void CleanupGlResources()
    {
        if (_glResourcesDisposed) return;
        _glResourcesDisposed = true;
        _loaded = false;
        try
        {
            if (_previewProgram != 0) { NativeGl.DeleteProgram(_previewProgram); _previewProgram = 0; }
            if (_captureProgram != 0) { NativeGl.DeleteProgram(_captureProgram); _captureProgram = 0; }
            if (_atlas != 0) { NativeGl.DeleteTextures(1, ref _atlas); _atlas = 0; }
            if (_intensityFbo != 0) { NativeGl.DeleteFramebuffers(1, ref _intensityFbo); _intensityFbo = 0; }
            if (_intensityTex != 0) { NativeGl.DeleteTextures(1, ref _intensityTex); _intensityTex = 0; }
            if (_colorFbo != 0) { NativeGl.DeleteFramebuffers(1, ref _colorFbo); _colorFbo = 0; }
            if (_colorTex != 0) { NativeGl.DeleteTextures(1, ref _colorTex); _colorTex = 0; }
            if (_glyphSelectFbo != 0) { NativeGl.DeleteFramebuffers(1, ref _glyphSelectFbo); _glyphSelectFbo = 0; }
            if (_glyphSelectTex != 0) { NativeGl.DeleteTextures(1, ref _glyphSelectTex); _glyphSelectTex = 0; }
            if (_glyphMapTex != 0) { NativeGl.DeleteTextures(1, ref _glyphMapTex); _glyphMapTex = 0; }
            if (_rasterExportFbo != 0) { NativeGl.DeleteFramebuffers(1, ref _rasterExportFbo); _rasterExportFbo = 0; }
            if (_rasterExportTex != 0) { NativeGl.DeleteTextures(1, ref _rasterExportTex); _rasterExportTex = 0; }
            foreach (var resource in _titleTextures.Values)
            {
                if (resource.Texture != 0)
                {
                    uint texture = resource.Texture;
                    NativeGl.DeleteTextures(1, ref texture);
                    resource.Texture = 0;
                }
            }
            _titleTextures.Clear();
            if (_vao != 0) { NativeGl.DeleteVertexArrays(1, ref _vao); _vao = 0; }
        }
        catch { }
        _intensityW = _intensityH = _rasterExportW = _rasterExportH = 0;
        _atlasRamp = string.Empty;
        _atlasGlyphCount = 1;
        _sceneGlyphSignature = string.Empty;
        _layerGlyphRows.Clear();
        UniformLocationCache.Clear();
        lock (_captureQueueGate)
        {
            while (_captureQueue.Count > 0)
                _captureQueue.Dequeue().Tcs.TrySetException(new InvalidOperationException("OpenGL context cerrado"));
            while (_exportCaptureQueue.Count > 0)
                _exportCaptureQueue.Dequeue().Completion.TrySetException(new InvalidOperationException("OpenGL context cerrado"));
            while (_rasterCaptureQueue.Count > 0)
                _rasterCaptureQueue.Dequeue().Completion.TrySetException(new InvalidOperationException("OpenGL context cerrado"));
        }
        _cameraOrbiting = false;
    }

    private void RenderFromGlArea()
    {
        if (!_loaded) return;
        try
        {
            NativeGl.GetIntegerv(NativeGl.GL_FRAMEBUFFER_BINDING, out int fb);
            RenderCurrentFrame((uint)Math.Max(0, fb));
        }
        catch (Exception ex)
        {
            _gpuInfo = "OpenGL render error: " + ex.Message;
        }
    }

    private unsafe void RenderCurrentFrame(uint fb)
    {
        if (!_loaded) return;
        var renderTimer = Stopwatch.StartNew();
        try
        {
            SyncSettingsFromScene();
            SyncAtlasIfNeeded();

            if (_benchmarkRequest is { } benchTcs)
            {
                _benchmarkRequest = null;
                try
                {
                    double ms = BenchmarkGpu(_benchmarkFrames);
                    benchTcs.TrySetResult(ms);
                }
                catch (Exception ex) { benchTcs.TrySetException(ex); }
            }

            AdvanceSceneClocks(_scene);
            _ = AdvancePreviewClocks();
            RenderSceneIntensity(_scene);
            RenderSceneColor(_scene);
            RenderSceneGlyphSelection(_scene);

            NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, (uint)fb);
            var bg = PreviewBackgroundColor;
            NativeGl.ClearColor(bg.R / 255f, bg.G / 255f, bg.B / 255f, 1f);
            NativeGl.Clear(NativeGl.GL_COLOR_BUFFER_BIT);
            RenderPreviewPass();

            try { _lastAsciiFrame = ReadGlyphSelectionAsAscii(_scene.Width, _scene.Height, _scene.Transform); }
            catch (Exception ex) { _gpuInfo = "OpenGL capture error: " + ex.Message; }

            while (true)
            {
                CaptureRequest req;
                lock (_captureQueueGate)
                {
                    if (_captureQueue.Count == 0) break;
                    req = _captureQueue.Dequeue();
                }
                try
                {
                    RenderSceneIntensity(_scene, req.Animation / Math.Max(.0001, _settings.Get("speed")));
                    RenderSceneGlyphSelection(_scene, req.Animation / Math.Max(.0001, _settings.Get("speed")));
                    string frame = ReadGlyphSelectionAsAscii(_scene.Width, _scene.Height, _scene.Transform);
                    _lastAsciiFrame = frame;
                    req.Tcs.TrySetResult(frame);
                }
                catch (Exception ex) { req.Tcs.TrySetException(ex); }
            }

            while (true)
            {
                ExportCaptureRequest req;
                lock (_captureQueueGate)
                {
                    if (_exportCaptureQueue.Count == 0) break;
                    req = _exportCaptureQueue.Dequeue();
                }
                try
                {
                    req.Completion.TrySetResult(CaptureExportFrameCore(req.Scene, req.TimeSeconds, req.IncludeRaster, req.BackgroundMode, req.SolidColor));
                }
                catch (Exception ex) { req.Completion.TrySetException(ex); }
            }

            while (true)
            {
                RasterCaptureRequest req;
                lock (_captureQueueGate)
                {
                    if (_rasterCaptureQueue.Count == 0) break;
                    req = _rasterCaptureQueue.Dequeue();
                }
                try
                {
                    req.Completion.TrySetResult(CaptureRasterExportFrameCore(req.Scene, req.TimeSeconds, req.BackgroundMode, req.SolidColor, req.ReusableRgba));
                }
                catch (Exception ex) { req.Completion.TrySetException(ex); }
            }

            NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, (uint)fb);
            _frames++;
            if (_fpsClock.ElapsedMilliseconds >= 500)
            {
                _actualFps = _frames / _fpsClock.Elapsed.TotalSeconds;
                _frames = 0;
                _fpsClock.Restart();
                FrameStats?.Invoke(_actualFps, renderTimer.Elapsed.TotalMilliseconds, _gpuInfo);
            }
        }
        catch (Exception ex)
        {
            _gpuInfo = "OpenGL render error: " + ex.Message;
            NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, (uint)fb);
        }
        finally
        {
            renderTimer.Stop();
        }
    }

    private void RenderPreviewPass()
    {
        NativeGl.Viewport(0, 0, Math.Max(1, Width), Math.Max(1, Height));
        NativeGl.Disable(NativeGl.GL_BLEND);
        NativeGl.UseProgram(_previewProgram);
        var outputSettings = SceneOutputSettings(_scene);
        bool useComposite = _scene.Layers.Count > 0;
        NativeGl.Uniform2i(UniformLocation(_previewProgram, "u_grid"), Math.Max(2, outputSettings.Width), Math.Max(2, outputSettings.Height));
        U2(_previewProgram, "u_view", (float)Width, (float)Height);
        Ui(_previewProgram, "u_preview_mode", (int)PreviewViewMode);
        U1(_previewProgram, "u_preview_zoom", (float)Math.Clamp(PreviewZoom * _scene.Transform.Scale, .1, 4.0));
        U1(_previewProgram, "u_cell_aspect", .55f);
        Ui(_previewProgram, "u_output_transparent", 0);
        SetPreviewGlyphUniforms(outputSettings, useComposite, useComposite);
        SetSceneTransformUniforms(_previewProgram, _scene.Transform);
        SetPostProcessUniforms(_previewProgram, _scene.PostProcess);
        U1(_previewProgram, "u_post_time", (float)CurrentTimeSeconds);
        var bg = PreviewBackgroundColor;
        U3(_previewProgram, "u_background_color", bg.R / 255f, bg.G / 255f, bg.B / 255f);
        BindEditorMaskUniforms(_previewProgram);

        NativeGl.ActiveTexture(NativeGl.GL_TEXTURE0);
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _atlas);
        Ui(_previewProgram, "u_atlas", 0);
        NativeGl.ActiveTexture(NativeGl.GL_TEXTURE0 + 1);
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _intensityTex);
        Ui(_previewProgram, "u_intensity", 1);
        NativeGl.ActiveTexture(NativeGl.GL_TEXTURE0 + 2);
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _colorTex);
        Ui(_previewProgram, "u_color", 2);
        NativeGl.ActiveTexture(NativeGl.GL_TEXTURE0 + 3);
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _glyphSelectTex);
        Ui(_previewProgram, "u_glyph_select", 3);
        NativeGl.BindVertexArray(_vao);
        NativeGl.DrawArrays(NativeGl.GL_TRIANGLES, 0, 3);
    }

    public Task<string> CaptureAsciiFrameAsync(double timeSeconds)
        => EnqueueCapture(timeSeconds * _settings.Get("speed"), timeSeconds * _settings.Get("speed") * _settings.Get("time_freq"));

    public Task<string> CaptureCurrentAsciiFrameAsync()
    {
        var times = AdvancePreviewClocks();
        return EnqueueCapture(times.Animation, times.Temporal);
    }

    private Task<string> EnqueueCapture(double animation, double temporal)
    {
        if (!_loaded) return Task.FromResult(CharacterRenderer.Render(_scene, CurrentTimeSeconds));
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_captureQueueGate)
            _captureQueue.Enqueue(new CaptureRequest(animation, temporal, tcs));
        RequestNextFrameRendering();
        return tcs.Task;
    }

    private static string ReadResource(string suffix)
    {
        var asm = Assembly.GetExecutingAssembly();
        string? name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (name is null) throw new FileNotFoundException($"No se encontró el recurso embebido {suffix}.");
        using var sr = new StreamReader(asm.GetManifestResourceStream(name)!);
        return sr.ReadToEnd();
    }

    private static string GetGlString(uint name)
    {
        var p = NativeGl.GetString(name);
        return p == IntPtr.Zero ? "?" : Marshal.PtrToStringAnsi(p) ?? "?";
    }

    private void RequestNextFrameRendering() { if (_glArea is not null) GtkNative.QueueRender(_glArea.Handle); }

    private void InvalidateCompat() => RequestNextFrameRendering();
}
