using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using SkiaSharp;

namespace Glyphore;

// Avalonia/Linux port of GlPreviewControl (from AsciiForge.Linux + Glyphore shaders/scene).
//
// Context: OpenGlControlBase owns the real GL context. Captures/benchmarks that need a
// current context are queued and resolved inside OnOpenGlRender.
// Glyph atlas: SkiaSharp (cross-platform), not System.Drawing/GDI+.
public sealed class GlPreviewControl : OpenGlControlBase
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
    private int _intensityW;
    private int _intensityH;
    private int _atlasCols = 1, _atlasRows = 1;
    private int _atlasGlyphCount = 1;
    private string _atlasRamp = "";
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Stopwatch _fpsClock = Stopwatch.StartNew();
    private int _frames;
    private static readonly Dictionary<(uint Program, string Name), int> UniformLocationCache = new();
    private volatile bool _loaded;
    private string _gpuInfo = "OpenGL no inicializado";
    private EffectSettings _settings = new();
    private GlyphoreScene _scene = GlyphoreScene.FromSettings(new EffectSettings());
    private string? _lastAsciiFrame;
    private int _targetFps = 30;
    private volatile bool _paused;
    private double _pauseAt;
    private double _pausedAccum;
    private double _animationTime;
    private double _temporalTime;
    private double _lastTimelineTime;
    private bool _previewClockInitialized;
    private bool _sdfOrbiting;
    private Avalonia.Point _sdfOrbitLast;
    private readonly DispatcherTimer _frameTimer;

    private readonly struct CaptureRequest(double animation, double temporal, TaskCompletionSource<string> tcs)
    {
        public readonly double Animation = animation;
        public readonly double Temporal = temporal;
        public readonly TaskCompletionSource<string> Tcs = tcs;
    }
    private readonly Queue<CaptureRequest> _captureQueue = new();
    private TaskCompletionSource<double>? _benchmarkRequest;
    private int _benchmarkFrames;

    public event Action<double, double, string>? FrameStats;
    public event Action<double, double, double>? SdfCameraChanged;

    internal GlyphoreScene Scene
    {
        get => _scene;
        set
        {
            _scene = value ?? GlyphoreScene.FromSettings(new EffectSettings());
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
        set { _targetFps = Math.Max(1, value); _frameTimer.Interval = TimeSpan.FromSeconds(1.0 / _targetFps); }
    }

    public string GpuInfo => _gpuInfo;
    public double CurrentTimeSeconds => _paused ? _pauseAt : _clock.Elapsed.TotalSeconds - _pausedAccum;
    public bool Paused => _paused;

    public void TogglePause()
    {
        if (!_paused) { _pauseAt = _clock.Elapsed.TotalSeconds - _pausedAccum; _paused = true; }
        else { _pausedAccum = _clock.Elapsed.TotalSeconds - _pauseAt; _paused = false; }
        RequestNextFrameRendering();
    }

    public void RestartAnimation()
    {
        _clock.Restart(); _paused = false; _pauseAt = 0; _pausedAccum = 0;
        _animationTime = 0; _temporalTime = 0; _lastTimelineTime = 0; _previewClockInitialized = false;
        RequestNextFrameRendering();
    }

    /// <summary>
    /// ASCII snapshot for export/clipboard. Uses last GPU capture when available;
    /// otherwise CharacterRenderer (also used when GL is not ready).
    /// </summary>
    public string RenderText()
    {
        SyncSettingsFromScene();
        if (!_loaded || string.IsNullOrEmpty(_lastAsciiFrame))
            return CharacterRenderer.Render(_scene, CurrentTimeSeconds);
        return _lastAsciiFrame;
    }

    public GlPreviewControl()
    {
        SyncSettingsFromScene();
        _frameTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1.0 / _targetFps) };
        _frameTimer.Tick += (_, _) => { if (_loaded && !_paused) RequestNextFrameRendering(); };
        _frameTimer.Start();

        PointerPressed += OnPointerPressedHandler;
        PointerMoved += OnPointerMovedHandler;
        PointerReleased += OnPointerReleasedHandler;
        PointerWheelChanged += OnPointerWheelChangedHandler;
    }

    private void SyncSettingsFromScene()
    {
        var layer = _scene.EnsureActiveLayer(_settings);
        _settings = _scene.CreateSettings(layer);
        TargetFps = Math.Max(1, _scene.Fps);
        SyncAtlasIfNeeded();
    }

    private void OnPointerPressedHandler(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            _settings.Effect.Equals("SDF Lab", StringComparison.OrdinalIgnoreCase))
        {
            _sdfOrbiting = true;
            _sdfOrbitLast = e.GetPosition(this);
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.SizeAll);
        }
    }

    private void OnPointerMovedHandler(object? sender, PointerEventArgs e)
    {
        if (!_sdfOrbiting) return;
        var pos = e.GetPosition(this);
        double dx = pos.X - _sdfOrbitLast.X, dy = pos.Y - _sdfOrbitLast.Y;
        _sdfOrbitLast = pos;
        double yaw = _settings.Get("sdf_yaw") + dx * .45;
        while (yaw > 180) yaw -= 360;
        while (yaw < -180) yaw += 360;
        double pitch = Math.Clamp(_settings.Get("sdf_pitch") - dy * .35, -75, 75);
        _settings.Set("sdf_yaw", yaw);
        _settings.Set("sdf_pitch", pitch);
        SdfCameraChanged?.Invoke(yaw, pitch, _settings.Get("sdf_depth"));
        RequestNextFrameRendering();
    }

    private void OnPointerReleasedHandler(object? sender, PointerReleasedEventArgs e)
    {
        if (_sdfOrbiting)
        {
            _sdfOrbiting = false;
            e.Pointer.Capture(null);
            Cursor = Cursor.Default;
        }
    }

    private void OnPointerWheelChangedHandler(object? sender, PointerWheelEventArgs e)
    {
        if (_settings.Effect.Equals("SDF Lab", StringComparison.OrdinalIgnoreCase))
        {
            double copies = Math.Clamp(Math.Round(_settings.Get("sdf_repeat")), 0, 4);
            double safeMin = Math.Max(1.8, copies * Math.Max(1.15, _settings.Get("sdf_spacing")) + 1.15);
            double depth = Math.Clamp(_settings.Get("sdf_depth") - Math.Sign(e.Delta.Y) * .35, safeMin, 24.0);
            _settings.Set("sdf_depth", depth);
            SdfCameraChanged?.Invoke(_settings.Get("sdf_yaw"), _settings.Get("sdf_pitch"), depth);
            RequestNextFrameRendering();
            e.Handled = true;
        }
    }

    protected override unsafe void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        try
        {
            NativeGl.Load(gl.GetProcAddress);
            UniformLocationCache.Clear();

            string vert = ReadResource("fullscreen.vert.glsl");
            string effects = ReadResource("effects.glsl");
            string preview = ReadResource("preview.frag.glsl");
            string intensity = ReadResource("intensity.frag.glsl").Replace("/*__EFFECTS__*/", effects);
            _previewProgram = NativeGl.CompileProgram(vert, preview);
            _captureProgram = NativeGl.CompileProgram(vert, intensity);
            uint vao;
            NativeGl.GenVertexArrays(1, &vao);
            _vao = vao;
            NativeGl.BindVertexArray(_vao);
            SyncSettingsFromScene();
            BuildGlyphAtlas(_settings.Charset);
            _gpuInfo = GetGlString(NativeGl.GL_RENDERER) + " · OpenGL " + GetGlString(NativeGl.GL_VERSION);
            _loaded = true;
        }
        catch (Exception ex)
        {
            _gpuInfo = "OpenGL error: " + ex.Message;
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (_previewProgram != 0) { NativeGl.DeleteProgram(_previewProgram); _previewProgram = 0; }
        if (_captureProgram != 0) { NativeGl.DeleteProgram(_captureProgram); _captureProgram = 0; }
        if (_atlas != 0) NativeGl.DeleteTextures(1, ref _atlas);
        if (_intensityFbo != 0) NativeGl.DeleteFramebuffers(1, ref _intensityFbo);
        if (_intensityTex != 0) NativeGl.DeleteTextures(1, ref _intensityTex);
        if (_colorFbo != 0) NativeGl.DeleteFramebuffers(1, ref _colorFbo);
        if (_colorTex != 0) NativeGl.DeleteTextures(1, ref _colorTex);
        if (_glyphSelectFbo != 0) NativeGl.DeleteFramebuffers(1, ref _glyphSelectFbo);
        if (_glyphSelectTex != 0) NativeGl.DeleteTextures(1, ref _glyphSelectTex);
        if (_vao != 0) NativeGl.DeleteVertexArrays(1, ref _vao);
        UniformLocationCache.Clear();
        _loaded = false;
        base.OnOpenGlDeinit(gl);
    }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (!_loaded) return;

        SyncSettingsFromScene();

        if (_benchmarkRequest is { } benchTcs)
        {
            _benchmarkRequest = null;
            try
            {
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < _benchmarkFrames; i++)
                {
                    double sourceTime = i / 120.0;
                    double animationTime = sourceTime * _settings.Get("speed");
                    RenderIntensity(_settings, (float)animationTime, (float)(animationTime * _settings.Get("time_freq")));
                    NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, (uint)fb);
                    RenderPreviewPass();
                }
                NativeGl.Finish();
                sw.Stop();
                benchTcs.TrySetResult(sw.Elapsed.TotalMilliseconds / Math.Max(1, _benchmarkFrames));
            }
            catch (Exception ex) { benchTcs.TrySetException(ex); }
        }

        SyncAtlasIfNeeded();
        var previewTimes = AdvancePreviewClocks();
        RenderIntensity(_settings, (float)previewTimes.Animation, (float)previewTimes.Temporal);
        NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, (uint)fb);
        var (br, bg, bb) = ParseColor(_scene.BackgroundColor);
        NativeGl.ClearColor(br / 255f, bg / 255f, bb / 255f, 1);
        NativeGl.Clear(NativeGl.GL_COLOR_BUFFER_BIT);
        RenderPreviewPass();

        try
        {
            _lastAsciiFrame = CaptureAsciiFrameInternal();
        }
        catch
        {
            // Keep previous / CPU fallback for RenderText().
        }

        while (_captureQueue.Count > 0)
        {
            var req = _captureQueue.Dequeue();
            try
            {
                RenderIntensity(_settings, (float)req.Animation, (float)req.Temporal);
                string frame = CaptureAsciiFrameInternal();
                _lastAsciiFrame = frame;
                req.Tcs.TrySetResult(frame);
            }
            catch (Exception ex) { req.Tcs.TrySetException(ex); }
        }
        NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, (uint)fb);

        _frames++;
        if (_fpsClock.ElapsedMilliseconds >= 500)
        {
            double actualFps = _frames / _fpsClock.Elapsed.TotalSeconds;
            _frames = 0; _fpsClock.Restart();
            FrameStats?.Invoke(actualFps, 0, _gpuInfo);
        }
    }

    private void RenderPreviewPass()
    {
        NativeGl.Viewport(0, 0, Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
        NativeGl.Disable(NativeGl.GL_BLEND);
        NativeGl.UseProgram(_previewProgram);
        NativeGl.Uniform2i(UniformLocation(_previewProgram, "u_grid"), Math.Max(2, _settings.Width), Math.Max(2, _settings.Height));
        U2(_previewProgram, "u_view", (float)Bounds.Width, (float)Bounds.Height);
        Ui(_previewProgram, "u_preview_mode", 0); // Fit
        U1(_previewProgram, "u_preview_zoom", 1f);
        U1(_previewProgram, "u_cell_aspect", 0.55f);
        Ui(_previewProgram, "u_output_transparent", 0);
        U1(_previewProgram, "u_gamma", _settings.F("gamma"));
        Ui(_previewProgram, "u_invert", _settings.Invert ? 1 : 0);
        Ui(_previewProgram, "u_color_enabled", _settings.ColorEnabled ? 1 : 0);
        Ui(_previewProgram, "u_use_composited_color", 0);
        Ui(_previewProgram, "u_use_composited_glyph", 0);
        U1(_previewProgram, "u_glyph_scale", (float)_settings.GlyphDisplayScale);
        Ui(_previewProgram, "u_glyph_count", Math.Max(1, _settings.Charset.EnumerateRunes().Count()));
        Ui(_previewProgram, "u_atlas_glyph_count", Math.Max(1, _atlasGlyphCount));
        NativeGl.Uniform2i(UniformLocation(_previewProgram, "u_atlas_grid"), _atlasCols, _atlasRows);
        SetPaletteUniforms(_previewProgram, _settings.PaletteStops);
        SetSceneTransformUniforms(_previewProgram, _scene.Transform);
        SetPostProcessUniforms(_previewProgram, _scene.PostProcess);
        U1(_previewProgram, "u_post_time", (float)CurrentTimeSeconds);
        var (br, bg, bb) = ParseColor(_scene.BackgroundColor);
        U3(_previewProgram, "u_background_color", br / 255f, bg / 255f, bb / 255f);
        Ui(_previewProgram, "u_preview_background_mode", 0);
        Ui(_previewProgram, "u_editor_mask_enabled", 0);
        NativeGl.ActiveTexture(NativeGl.GL_TEXTURE0); NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _atlas); Ui(_previewProgram, "u_atlas", 0);
        NativeGl.ActiveTexture(NativeGl.GL_TEXTURE0 + 1); NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _intensityTex); Ui(_previewProgram, "u_intensity", 1);
        NativeGl.ActiveTexture(NativeGl.GL_TEXTURE0 + 2); NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _colorTex); Ui(_previewProgram, "u_color", 2);
        NativeGl.ActiveTexture(NativeGl.GL_TEXTURE0 + 3); NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _glyphSelectTex); Ui(_previewProgram, "u_glyph_select", 3);
        NativeGl.BindVertexArray(_vao); NativeGl.DrawArrays(NativeGl.GL_TRIANGLES, 0, 3);
    }

    public Task<double> BenchmarkGpuAsync(int frames = 240)
    {
        if (!_loaded) return Task.FromResult(double.NaN);
        var tcs = new TaskCompletionSource<double>();
        _benchmarkFrames = frames;
        _benchmarkRequest = tcs;
        RequestNextFrameRendering();
        return tcs.Task;
    }

    public Task<string> CaptureAsciiFrameAsync(double timeSeconds)
    {
        double animation = timeSeconds * _settings.Get("speed");
        double temporal = animation * _settings.Get("time_freq");
        return EnqueueCapture(animation, temporal);
    }

    public Task<string> CaptureCurrentAsciiFrameAsync()
    {
        var times = AdvancePreviewClocks();
        return EnqueueCapture(times.Animation, times.Temporal);
    }

    private Task<string> EnqueueCapture(double animation, double temporal)
    {
        if (!_loaded) return Task.FromResult(CharacterRenderer.Render(_scene, CurrentTimeSeconds));
        var tcs = new TaskCompletionSource<string>();
        _captureQueue.Enqueue(new CaptureRequest(animation, temporal, tcs));
        RequestNextFrameRendering();
        return tcs.Task;
    }

    private unsafe void EnsureIntensityTarget(int w, int h)
    {
        if (_intensityTex != 0 && _colorTex != 0 && _glyphSelectTex != 0 && _intensityW == w && _intensityH == h) return;
        if (_intensityFbo != 0) NativeGl.DeleteFramebuffers(1, ref _intensityFbo);
        if (_intensityTex != 0) NativeGl.DeleteTextures(1, ref _intensityTex);
        if (_colorFbo != 0) NativeGl.DeleteFramebuffers(1, ref _colorFbo);
        if (_colorTex != 0) NativeGl.DeleteTextures(1, ref _colorTex);
        if (_glyphSelectFbo != 0) NativeGl.DeleteFramebuffers(1, ref _glyphSelectFbo);
        if (_glyphSelectTex != 0) NativeGl.DeleteTextures(1, ref _glyphSelectTex);
        _intensityTex = 0; _intensityFbo = 0;
        _colorTex = 0; _colorFbo = 0;
        _glyphSelectTex = 0; _glyphSelectFbo = 0;

        uint intensityTex;
        NativeGl.GenTextures(1, &intensityTex);
        _intensityTex = intensityTex;
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _intensityTex);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MIN_FILTER, (int)NativeGl.GL_NEAREST);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MAG_FILTER, (int)NativeGl.GL_NEAREST);
        NativeGl.TexImage2D(NativeGl.GL_TEXTURE_2D, 0, (int)NativeGl.GL_RGBA8, w, h, 0, NativeGl.GL_RGBA, NativeGl.GL_UNSIGNED_BYTE, IntPtr.Zero);
        uint intensityFbo;
        NativeGl.GenFramebuffers(1, &intensityFbo);
        _intensityFbo = intensityFbo;
        NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, _intensityFbo);
        NativeGl.FramebufferTexture2D(NativeGl.GL_FRAMEBUFFER, NativeGl.GL_COLOR_ATTACHMENT0, NativeGl.GL_TEXTURE_2D, _intensityTex, 0);
        if (NativeGl.CheckFramebufferStatus(NativeGl.GL_FRAMEBUFFER) != NativeGl.GL_FRAMEBUFFER_COMPLETE)
            throw new InvalidOperationException("FBO de intensidad incompleto");

        uint colorTex;
        NativeGl.GenTextures(1, &colorTex);
        _colorTex = colorTex;
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _colorTex);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MIN_FILTER, (int)NativeGl.GL_NEAREST);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MAG_FILTER, (int)NativeGl.GL_NEAREST);
        NativeGl.TexImage2D(NativeGl.GL_TEXTURE_2D, 0, (int)NativeGl.GL_RGBA8, w, h, 0, NativeGl.GL_RGBA, NativeGl.GL_UNSIGNED_BYTE, IntPtr.Zero);
        uint colorFbo;
        NativeGl.GenFramebuffers(1, &colorFbo);
        _colorFbo = colorFbo;
        NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, _colorFbo);
        NativeGl.FramebufferTexture2D(NativeGl.GL_FRAMEBUFFER, NativeGl.GL_COLOR_ATTACHMENT0, NativeGl.GL_TEXTURE_2D, _colorTex, 0);

        uint glyphSelectTex;
        NativeGl.GenTextures(1, &glyphSelectTex);
        _glyphSelectTex = glyphSelectTex;
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _glyphSelectTex);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MIN_FILTER, (int)NativeGl.GL_NEAREST);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MAG_FILTER, (int)NativeGl.GL_NEAREST);
        NativeGl.TexImage2D(NativeGl.GL_TEXTURE_2D, 0, (int)NativeGl.GL_RGBA8, w, h, 0, NativeGl.GL_RGBA, NativeGl.GL_UNSIGNED_BYTE, IntPtr.Zero);
        uint glyphSelectFbo;
        NativeGl.GenFramebuffers(1, &glyphSelectFbo);
        _glyphSelectFbo = glyphSelectFbo;
        NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, _glyphSelectFbo);
        NativeGl.FramebufferTexture2D(NativeGl.GL_FRAMEBUFFER, NativeGl.GL_COLOR_ATTACHMENT0, NativeGl.GL_TEXTURE_2D, _glyphSelectTex, 0);

        _intensityW = w; _intensityH = h;
    }

    private (double Animation, double Temporal) AdvancePreviewClocks()
    {
        double timeline = CurrentTimeSeconds;
        if (!_previewClockInitialized)
        {
            _previewClockInitialized = true;
            _lastTimelineTime = timeline;
            return (_animationTime, _temporalTime);
        }

        double dt = Math.Clamp(timeline - _lastTimelineTime, 0.0, 0.25);
        _lastTimelineTime = timeline;
        double speed = _settings.Get("speed");
        _animationTime += dt * speed;
        _temporalTime += dt * speed * _settings.Get("time_freq");
        return (_animationTime, _temporalTime);
    }

    private void RenderIntensity(EffectSettings settings, float animationTime, float temporalTime)
    {
        int w = Math.Max(2, settings.Width), h = Math.Max(2, settings.Height);
        EnsureIntensityTarget(w, h);
        NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, _intensityFbo);
        NativeGl.Viewport(0, 0, w, h);
        NativeGl.Disable(NativeGl.GL_BLEND);
        NativeGl.UseProgram(_captureProgram);
        SetCommonUniforms(_captureProgram, settings, animationTime, temporalTime);
        Ui(_captureProgram, "u_mask_count", 0);
        Ui(_captureProgram, "u_title_enabled", 0);
        Ui(_captureProgram, "u_output_color", 0);
        Ui(_captureProgram, "u_output_glyph", 0);
        U1(_captureProgram, "u_color_gamma", settings.F("gamma"));
        Ui(_captureProgram, "u_color_invert", settings.Invert ? 1 : 0);
        U1(_captureProgram, "u_layer_opacity", 1f);
        Ui(_captureProgram, "u_layer_mode", 0);
        NativeGl.BindVertexArray(_vao);
        NativeGl.DrawArrays(NativeGl.GL_TRIANGLES, 0, 3);
    }

    private void SyncAtlasIfNeeded()
    {
        if (!_loaded) return;
        var ramp = string.IsNullOrEmpty(_settings.Charset) ? " " : _settings.Charset;
        if (ramp == _atlasRamp) return;
        BuildGlyphAtlas(ramp);
    }

    private unsafe void BuildGlyphAtlas(string ramp)
    {
        var runes = ramp.EnumerateRunes().ToArray();
        if (runes.Length == 0) runes = " ".EnumerateRunes().ToArray();
        const int cw = 32, ch = 48, cols = 16;
        int rows = (int)Math.Ceiling(runes.Length / (double)cols);
        int atlasW = cols * cw, atlasH = rows * ch;

        using var bitmap = new SKBitmap(atlasW, atlasH, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);
            using var typeface = ResolveMonospaceTypeface();
            using var font = new SKFont(typeface, 26) { Edging = SKFontEdging.SubpixelAntialias };
            using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            for (int i = 0; i < runes.Length; i++)
            {
                string text = runes[i].ToString();
                float x = (i % cols) * cw, y = (i / cols) * ch;
                font.MeasureText(MemoryMarshal.Cast<char, ushort>(text.AsSpan()), out var bounds, paint);
                float tx = x + (cw - bounds.Width) / 2f - bounds.Left;
                float ty = y + (ch - bounds.Height) / 2f - bounds.Top;
                canvas.DrawText(text, tx, ty, font, paint);
            }
        }

        IntPtr pixels = bitmap.GetPixels();
        if (_atlas == 0)
        {
            uint atlas;
            NativeGl.GenTextures(1, &atlas);
            _atlas = atlas;
        }
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _atlas);
        NativeGl.PixelStorei(NativeGl.GL_UNPACK_ALIGNMENT, 1);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MIN_FILTER, (int)NativeGl.GL_LINEAR);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MAG_FILTER, (int)NativeGl.GL_LINEAR);
        NativeGl.TexImage2D(NativeGl.GL_TEXTURE_2D, 0, (int)NativeGl.GL_RGBA8, atlasW, atlasH, 0, NativeGl.GL_BGRA, NativeGl.GL_UNSIGNED_BYTE, pixels);
        _atlasCols = cols; _atlasRows = rows; _atlasRamp = ramp; _atlasGlyphCount = runes.Length;
    }

    private static SKTypeface ResolveMonospaceTypeface()
    {
        string[] candidates = ["Cascadia Mono", "Cascadia Code", "JetBrains Mono", "Fira Code", "DejaVu Sans Mono", "Noto Sans Mono", "monospace"];
        foreach (var name in candidates)
        {
            var tf = SKTypeface.FromFamilyName(name, SKFontStyle.Normal);
            if (tf is not null && !string.IsNullOrEmpty(tf.FamilyName) &&
                tf.FamilyName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return tf;
            tf?.Dispose();
        }
        return SKTypeface.Default;
    }

    private static string ReadResource(string suffix)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().First(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        using var sr = new StreamReader(asm.GetManifestResourceStream(name)!);
        return sr.ReadToEnd();
    }

    private static string GetGlString(uint name)
    {
        var p = NativeGl.GetString(name);
        return p == IntPtr.Zero ? "?" : Marshal.PtrToStringAnsi(p) ?? "?";
    }

    private unsafe string CaptureAsciiFrameInternal()
    {
        int w = Math.Max(2, _settings.Width), h = Math.Max(2, _settings.Height);
        NativeGl.BindFramebuffer(NativeGl.GL_FRAMEBUFFER, _intensityFbo);
        NativeGl.Finish();
        byte[] pixels = new byte[w * h * 4];
        fixed (byte* p = pixels) NativeGl.ReadPixels(0, 0, w, h, NativeGl.GL_RGBA, NativeGl.GL_UNSIGNED_BYTE, (IntPtr)p);

        var runes = _settings.Charset.EnumerateRunes().ToArray();
        if (runes.Length == 0) runes = " ".EnumerateRunes().ToArray();
        var sb = new StringBuilder((w + 1) * h);
        double gamma = Math.Max(.05, _settings.Get("gamma"));
        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++)
            {
                double v = pixels[(y * w + x) * 4] / 255.0;
                v = Math.Pow(Math.Clamp(v, 0, 1), 1.0 / gamma);
                if (_settings.Invert) v = 1 - v;
                int idx = Math.Clamp((int)Math.Round(v * (runes.Length - 1)), 0, runes.Length - 1);
                sb.Append(runes[idx].ToString());
            }
            if (y > 0) sb.AppendLine();
        }
        return sb.ToString();
    }

    private static void SetCommonUniforms(uint p, EffectSettings s, float animationTime, float temporalTime)
    {
        Ui(p, "u_effect", EffectRegistry.EffectId(s.Effect)); Ui(p, "u_seed", s.Seed);
        NativeGl.Uniform2i(UniformLocation(p, "u_grid"), Math.Max(2, s.Width), Math.Max(2, s.Height));
        U1(p, "u_time", animationTime); U1(p, "u_temporal_time", temporalTime); U1(p, "u_scale", s.F("scale"));
        U1(p, "u_amp", s.F("osc_amp")); U1(p, "u_fx", s.F("freq_x")); U1(p, "u_fy", s.F("freq_y")); U1(p, "u_fd", s.F("freq_diag"));
        U1(p, "u_fr", s.F("freq_radial")); U1(p, "u_tf", s.F("time_freq")); U1(p, "u_phase", s.F("phase_deg")); U1(p, "u_turb", s.F("turbulence"));
        U1(p, "u_warp", s.F("warp")); U1(p, "u_dx", s.F("drift_x")); U1(p, "u_dy", s.F("drift_y")); U1(p, "u_pulse", s.F("pulse"));
        U1(p, "u_density", s.F("density")); U1(p, "u_aspect", s.F("aspect")); U1(p, "u_iterations", s.F("iterations"));
        foreach (var binding in ShaderUniformBindings.EffectSpecific)
            U1(p, binding.UniformName, s.F(binding.SettingKey));
        Ui(p, "u_shape_mode", EffectRegistry.ShapeId(s.ShapeMode));
    }

    private static void SetSceneTransformUniforms(uint p, SceneTransform? transform)
    {
        U1(p, "u_scene_rotation", transform is null ? 0f : (float)transform.Rotation);
        U1(p, "u_scene_perspective_x", transform is null ? 0f : (float)transform.PerspectiveX);
        U1(p, "u_scene_perspective_y", transform is null ? 0f : (float)transform.PerspectiveY);
    }

    private static void SetPostProcessUniforms(uint p, ScenePostProcess? post)
    {
        bool enabled = post?.Enabled == true;
        Ui(p, "u_post_enabled", enabled ? 1 : 0);
        U1(p, "u_post_exposure", enabled ? (float)post!.Exposure : 0f);
        U1(p, "u_post_contrast", enabled ? (float)post!.Contrast : 1f);
        U1(p, "u_post_saturation", enabled ? (float)post!.Saturation : 1f);
        U1(p, "u_post_bloom", enabled ? (float)post!.Bloom : 0f);
        U1(p, "u_post_bloom_radius", enabled ? (float)post!.BloomRadius : 1f);
        U1(p, "u_post_vignette", enabled ? (float)post!.Vignette : 0f);
        U1(p, "u_post_scanlines", enabled ? (float)post!.Scanlines : 0f);
        U1(p, "u_post_grain", enabled ? (float)post!.Grain : 0f);
        U1(p, "u_post_chromatic", enabled ? (float)post!.ChromaticAberration : 0f);
        U1(p, "u_post_posterize", enabled ? (float)post!.Posterize : 0f);
        U1(p, "u_post_threshold", enabled ? (float)post!.Threshold : 0f);
        U1(p, "u_post_blur", enabled ? (float)post!.Blur : 0f);
        U1(p, "u_post_sharpen", enabled ? (float)post!.Sharpen : 0f);
        U1(p, "u_post_pixelate", enabled ? (float)post!.Pixelate : 1f);
        U1(p, "u_post_dither", enabled ? (float)post!.Dither : 0f);
    }

    private static void SetPaletteUniforms(uint p, List<string> stops)
    {
        var list = stops.Count >= 2 ? stops.Take(8).ToList() : new List<string> { "#cccccc", "#ffffff" };
        Ui(p, "u_palette_count", list.Count);
        for (int i = 0; i < 8; i++)
        {
            var (r, g, b) = ParseColor(list[Math.Min(i, list.Count - 1)]);
            int loc = UniformLocation(p, $"u_palette{i}");
            if (loc >= 0) NativeGl.Uniform3f(loc, r / 255f, g / 255f, b / 255f);
        }
    }

    private static (byte R, byte G, byte B) ParseColor(string s)
    {
        try
        {
            var c = SKColor.Parse(s);
            return (c.Red, c.Green, c.Blue);
        }
        catch { return (255, 255, 255); }
    }

    private static int UniformLocation(uint program, string name)
    {
        var key = (program, name);
        if (UniformLocationCache.TryGetValue(key, out int location)) return location;
        location = NativeGl.GetUniformLocation(program, name);
        UniformLocationCache[key] = location;
        return location;
    }

    private static void U1(uint program, string name, float value)
    {
        int location = UniformLocation(program, name);
        if (location >= 0) NativeGl.Uniform1f(location, value);
    }

    private static void Ui(uint program, string name, int value)
    {
        int location = UniformLocation(program, name);
        if (location >= 0) NativeGl.Uniform1i(location, value);
    }

    private static void U2(uint program, string name, float a, float b)
    {
        int location = UniformLocation(program, name);
        if (location >= 0) NativeGl.Uniform2f(location, a, b);
    }

    private static void U3(uint program, string name, float a, float b, float c)
    {
        int location = UniformLocation(program, name);
        if (location >= 0) NativeGl.Uniform3f(location, a, b, c);
    }
}
