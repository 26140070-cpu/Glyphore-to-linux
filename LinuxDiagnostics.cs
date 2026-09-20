using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Glyphore;

/// <summary>
/// Verbose Linux startup diagnostics. Enable with --diagnostic or
/// GLYPHORE_DIAGNOSTIC=1. Logs are written both to stderr and to a file.
/// </summary>
internal static class LinuxDiagnostics
{
    private static readonly object Sync = new();
    private static string? _logPath;
    private static int _firstChanceCount;

    public static bool Enabled { get; private set; }
    public static bool StartupTraceActive { get; private set; }
    public static string? LogPath => _logPath;

    public static void Initialize(string[] args)
    {
        Enabled = string.Equals(Environment.GetEnvironmentVariable("GLYPHORE_DIAGNOSTIC"), "1", StringComparison.OrdinalIgnoreCase)
                  || args.Any(a => string.Equals(a, "--diagnostic", StringComparison.OrdinalIgnoreCase));
        if (!Enabled) return;

        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glyphore");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "diagnostic.log");
        }
        catch
        {
            _logPath = Path.Combine(AppContext.BaseDirectory, "glyphore-diagnostic.log");
        }

        StartupTraceActive = true;
        AppDomain.CurrentDomain.FirstChanceException += HandleFirstChanceException;

        Log("=== Glyphoré Linux diagnostic start ===");
        Log($"UTC: {DateTimeOffset.UtcNow:O}");
        Log($"PID: {Environment.ProcessId}");
        Log($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        Log($"OS: {RuntimeInformation.OSDescription}");
        Log($"Framework: {RuntimeInformation.FrameworkDescription}");
        Log($"Assembly: {Assembly.GetExecutingAssembly().Location}");
        Log($"Base directory: {AppContext.BaseDirectory}");
        Log($"Args: {string.Join(" | ", args)}");
        LogEnvironment();
    }

    public static void Phase(string message) => Log($"[PHASE] {message}");

    public static void EndStartupTrace()
    {
        // Kept for source compatibility. Diagnostics remain active until process end
        // so failures in Application.Run, preview creation, exports and dialogs are visible.
        StartupTraceActive = true;
        Log("[PHASE] Startup exception trace remains active for full process");
    }

    private static void HandleFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        if (!Enabled || !StartupTraceActive) return;
        if (Interlocked.Increment(ref _firstChanceCount) > 500) return;

        Exception ex = e.Exception;
        string type = ex.GetType().FullName ?? ex.GetType().Name;
        string? location = ex.TargetSite?.DeclaringType?.FullName;
        Log($"[FIRST-CHANCE #{_firstChanceCount}] {type}: {ex.Message}");
        if (!string.IsNullOrWhiteSpace(location)) Log($"[FIRST-CHANCE-SITE] {location}.{ex.TargetSite?.Name}");
        if (!string.IsNullOrWhiteSpace(ex.StackTrace)) Log(ex.StackTrace!);
    }

    public static void LogEnvironment()
    {
        string[] names =
        [
            "XDG_SESSION_TYPE", "DISPLAY", "WAYLAND_DISPLAY", "GDK_BACKEND",
            "GTK_THEME", "LANG", "LC_ALL", "LIBGL_DEBUG", "MESA_DEBUG",
            "MESA_LOADER_DRIVER_OVERRIDE", "__GLX_VENDOR_LIBRARY_NAME"
        ];
        foreach (string name in names)
            Log($"ENV {name}={Environment.GetEnvironmentVariable(name) ?? "<unset>"}");
    }

    public static void Exception(string phase, Exception ex)
    {
        Log($"[EXCEPTION] {phase}: {ex.GetType().FullName}: {ex.Message}");
        Log(ex.ToString());
        if (ex.InnerException is not null)
            Log($"[INNER] {ex.InnerException}");
    }

    public static void Action(string phase, Action action)
    {
        try
        {
            action();
            Phase($"OK {phase}");
        }
        catch (Exception ex)
        {
            Exception(phase, ex);
            throw;
        }
    }

    public static void Log(string message)
    {
        if (!Enabled) return;
        string line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}";
        try { Console.Error.WriteLine(line); } catch { }
        try
        {
            lock (Sync)
            {
                if (!string.IsNullOrWhiteSpace(_logPath))
                    File.AppendAllText(_logPath!, line + Environment.NewLine);
            }
        }
        catch { }
    }

    public static void End(int code)
    {
        if (!Enabled) return;
        Log($"=== Glyphoré Linux diagnostic end; exit={code} ===");
        if (!string.IsNullOrWhiteSpace(_logPath))
            try { Console.Error.WriteLine($"Diagnostic log: {_logPath}"); } catch { }
    }
}
