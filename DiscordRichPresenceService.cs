using System.Diagnostics;
using System.Net.Sockets;

namespace Glyphore;


internal sealed class DiscordRichPresenceService : IAsyncDisposable
{
    internal const string ApplicationId = "1549417846138863637";
    internal const string LogoAssetKey = "glyphore_logo";
    internal const string IdleAssetKey = "glyphore_idle";
    internal static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LoopDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MinimumPublishInterval = TimeSpan.FromMilliseconds(650);
    private static readonly TimeSpan PreviewPresenceDuration = TimeSpan.FromSeconds(8);

    private readonly object _gate = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly CancellationTokenSource _abort = new();
    private readonly TimeSpan _idleTimeout;
    private readonly long _sessionStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private Task? _runner;
    private Stream? _stream;
    private Task? _reader;
    private PresenceContext _context = PresenceContext.Scene;
    private PresenceOperation _operation;
    private bool _enabled;
    private bool _stop;
    private long _lastActivity = Environment.TickCount64;
    private long _previewUntil;
    private long _nextReconnect;
    private long _lastPublish;
    private int _forceReconnect;
    private int _clearRequested;
    private string? _lastKey;

    public DiscordRichPresenceService(bool enabled, TimeSpan? idleTimeout = null)
    {
        _enabled = enabled;
        _idleTimeout = idleTimeout ?? DefaultIdleTimeout;
    }

    public bool Enabled { get { lock (_gate) return _enabled; } }

    public void Start()
    {
        lock (_gate)
        {
            if (_runner is null)
                _runner = Task.Run(() => RunAsync(_abort.Token));
        }
    }

    public void SetEnabled(bool enabled)
    {
        lock (_gate)
        {
            _enabled = enabled;
            if (enabled) _lastActivity = Environment.TickCount64;
        }
        if (!enabled) Interlocked.Exchange(ref _clearRequested, 1);
        else Interlocked.Exchange(ref _forceReconnect, 1);
    }

    public void SetContext(PresenceContext context) { lock (_gate) _context = context; }
    public void SetBusyOperation(PresenceOperation operation, bool active)
    {
        lock (_gate) _operation = active ? operation : (_operation == operation ? PresenceOperation.None : _operation);
    }
    public void NotifyPreviewing() { lock (_gate) _previewUntil = Environment.TickCount64 + (long)PreviewPresenceDuration.TotalMilliseconds; }
    public void NotifyUserActivity()
    {
        long now = Environment.TickCount64;
        long old = Interlocked.Read(ref _lastActivity);
        if (unchecked(now - old) >= 100 || unchecked(now - old) < 0) Interlocked.Exchange(ref _lastActivity, now);
    }

    public async Task StopAsync(TimeSpan timeout)
    {
        Task? runner;
        lock (_gate) { _enabled = false; _stop = true; runner = _runner; }
        if (runner is null) return;
        if (!ReferenceEquals(await Task.WhenAny(runner, Task.Delay(timeout)), runner))
        {
            _abort.Cancel();
            try { _stream?.Dispose(); } catch { }
        }
        try { await runner.ConfigureAwait(false); } catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        _abort.Cancel();
        _abort.Dispose();
        _writeGate.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (Interlocked.Exchange(ref _clearRequested, 0) != 0 && _stream is not null)
                    await DisconnectAsync(true, token).ConfigureAwait(false);
                bool enabled, stop;
                lock (_gate) { enabled = _enabled; stop = _stop; }
                if (!enabled)
                {
                    if (_stream is not null) await DisconnectAsync(true, token).ConfigureAwait(false);
                    if (stop) break;
                    await Task.Delay(LoopDelay, token).ConfigureAwait(false);
                    continue;
                }
                if (_stream is null)
                {
                    long now = Environment.TickCount64;
                    if (Interlocked.Exchange(ref _forceReconnect, 0) != 0 || now >= Interlocked.Read(ref _nextReconnect))
                        if (!await TryConnectAsync(token).ConfigureAwait(false))
                            Interlocked.Exchange(ref _nextReconnect, now + (long)ReconnectDelay.TotalMilliseconds);
                    await Task.Delay(LoopDelay, token).ConfigureAwait(false);
                    continue;
                }
                if (_reader is { IsCompleted: true })
                {
                    await DisconnectAsync(false, token).ConfigureAwait(false);
                    Interlocked.Exchange(ref _nextReconnect, Environment.TickCount64 + (long)ReconnectDelay.TotalMilliseconds);
                    continue;
                }
                try { await PublishIfChangedAsync(token).ConfigureAwait(false); }
                catch (IOException) { await DisconnectAsync(false, token).ConfigureAwait(false); }
                catch (ObjectDisposedException) { await DisconnectAsync(false, token).ConfigureAwait(false); }
                await Task.Delay(LoopDelay, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Trace.TraceWarning($"Discord Rich Presence error: {ex.Message}"); }
        finally { try { await DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false); } catch { } }
    }

    private static IEnumerable<string> SocketPaths()
    {
        string? explicitPath = Environment.GetEnvironmentVariable("DISCORD_IPC_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath)) { yield return explicitPath; yield break; }
        string? runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtime))
            for (int i = 0; i < 10; i++) yield return Path.Combine(runtime, $"discord-ipc-{i}");

        for (int i = 0; i < 10; i++) yield return $"/tmp/discord-ipc-{i}";
    }

    private async Task<bool> TryConnectAsync(CancellationToken token)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsFreeBSD() && !OperatingSystem.IsMacOS()) return false;
        foreach (string path in SocketPaths())
        {
            // Evita 20 excepciones por ciclo cuando Discord no está corriendo:
            // si el socket no existe en el filesystem, no hay nada a quien conectarse.
            if (!path.StartsWith('@') && !File.Exists(path)) continue;
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(ConnectTimeout);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), cts.Token).ConfigureAwait(false);
                Stream stream = new NetworkStream(socket, ownsSocket: true);
                await DiscordRpcProtocol.WriteFrameAsync(stream, DiscordRpcOpcode.Handshake, DiscordRpcProtocol.BuildHandshakePayload(ApplicationId), token).ConfigureAwait(false);
                using var ready = CancellationTokenSource.CreateLinkedTokenSource(token);
                ready.CancelAfter(ReadyTimeout);
                bool isReady = false;
                while (!ready.IsCancellationRequested)
                {
                    DiscordRpcFrame frame = await DiscordRpcProtocol.ReadFrameAsync(stream, ready.Token).ConfigureAwait(false);
                    if (frame.Opcode == DiscordRpcOpcode.Ping)
                        await DiscordRpcProtocol.WriteFrameAsync(stream, DiscordRpcOpcode.Pong, frame.Payload, ready.Token).ConfigureAwait(false);
                    else if (frame.Opcode == DiscordRpcOpcode.Frame && DiscordRpcProtocol.IsReadyPayload(frame.Payload)) { isReady = true; break; }
                    else if (frame.Opcode == DiscordRpcOpcode.Close) break;
                }
                if (!isReady) { await stream.DisposeAsync(); continue; }
                _stream = stream;
                _reader = Task.Run(() => ReadLoopAsync(stream, token), token);
                _lastKey = null;
                _lastPublish = 0;
                Trace.TraceInformation("Discord Rich Presence connected through Unix socket.");
                return true;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { socket.Dispose(); }
            catch (OperationCanceledException) { socket.Dispose(); throw; }
            catch (Exception ex) { socket.Dispose(); Trace.WriteLine($"Discord IPC {path}: {ex.Message}"); }
            finally { socket.Dispose(); }
        }
        return false;
    }

    private async Task ReadLoopAsync(Stream stream, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                DiscordRpcFrame frame = await DiscordRpcProtocol.ReadFrameAsync(stream, token).ConfigureAwait(false);
                if (frame.Opcode == DiscordRpcOpcode.Ping)
                    await WriteFrameSafeAsync(stream, DiscordRpcOpcode.Pong, frame.Payload, token).ConfigureAwait(false);
                else if (frame.Opcode == DiscordRpcOpcode.Close) return;
                else if (frame.Opcode == DiscordRpcOpcode.Frame &&
                         DiscordRpcProtocol.IsErrorPayload(frame.Payload, out string? message) &&
                         !string.IsNullOrWhiteSpace(message))
                    Trace.TraceWarning($"Discord Rich Presence RPC error: {message}");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (InvalidDataException ex) { Trace.TraceWarning($"Discord IPC invalid frame: {ex.Message}"); }
    }

    private async Task PublishIfChangedAsync(CancellationToken token)
    {
        Stream? stream = _stream;
        if (stream is null) return;
        long now = Environment.TickCount64;
        PresenceContext context; PresenceOperation operation; long preview; bool enabled;
        lock (_gate) { enabled = _enabled; context = _context; operation = _operation; preview = _previewUntil; }
        if (!enabled) return;
        bool idle = IsIdle(operation, now, Interlocked.Read(ref _lastActivity), _idleTimeout);
        DiscordPresenceDescriptor d = ResolvePresence(context, operation, idle, operation == PresenceOperation.None && now < preview);
        string key = $"{d.Details}\n{d.State}\n{d.LargeImage}\n{d.LargeText}";
        if (key == _lastKey || (_lastKey is not null && unchecked(now - _lastPublish) < (long)MinimumPublishInterval.TotalMilliseconds)) return;
        await WriteFrameSafeAsync(stream, DiscordRpcOpcode.Frame,
            DiscordRpcProtocol.BuildSetActivityPayload(Environment.ProcessId, d, _sessionStart, Guid.NewGuid().ToString("N")), token).ConfigureAwait(false);
        _lastKey = key; _lastPublish = now;
    }

    private async Task DisconnectAsync(bool clear, CancellationToken token)
    {
        Stream? stream = _stream; _stream = null; _reader = null; _lastKey = null; _lastPublish = 0;
        if (stream is null) return;
        if (clear)
        {
            try { await WriteFrameSafeAsync(stream, DiscordRpcOpcode.Frame, DiscordRpcProtocol.BuildClearActivityPayload(Environment.ProcessId, Guid.NewGuid().ToString("N")), token).ConfigureAwait(false); } catch { }
        }
        try { await stream.DisposeAsync(); } catch { }
    }

    private async Task WriteFrameSafeAsync(Stream stream, DiscordRpcOpcode opcode, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try { await DiscordRpcProtocol.WriteFrameAsync(stream, opcode, payload, token).ConfigureAwait(false); }
        finally { _writeGate.Release(); }
    }

    internal static bool IsIdle(PresenceOperation operation, long nowTick, long lastActivityTick, TimeSpan timeout)
        => operation == PresenceOperation.None && unchecked(nowTick - lastActivityTick) >= (long)timeout.TotalMilliseconds;

    internal static DiscordPresenceDescriptor ResolvePresence(PresenceContext context, PresenceOperation operation, bool idle, bool previewing)
    {
        if (operation == PresenceOperation.Export) return new("Exporting an animation", "Rendering", LogoAssetKey, "Glyphoré");
        if (idle) return new("Idle", "Glyphoré", IdleAssetKey, "Glyphoré — Idle");
        if (context == PresenceContext.MaskEditing) return new("Editing a mask", "Scene Composer", LogoAssetKey, "Glyphoré");
        if (context == PresenceContext.AsciiTitleStudio) return new("Creating an ASCII title", "ASCII Title Studio", LogoAssetKey, "Glyphoré");
        if (previewing) return new("Previewing an animation", "Glyphoré", LogoAssetKey, "Glyphoré");
        return new("Editing a scene", "Glyphoré", LogoAssetKey, "Glyphoré");
    }
}
