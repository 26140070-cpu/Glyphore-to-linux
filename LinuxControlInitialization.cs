using Forms = Majorsilence.Forms;

namespace Glyphore;

internal static class LinuxControlInitialization
{
    internal static void Defer(Forms.Control control, Action apply, string _)
    {
        try { apply(); }
        catch (Exception ex) { LinuxDiagnostics.Exception($"{control.GetType().Name}.property", ex); }
    }

    internal static void OnHandleReady(Forms.Control control, Action apply, string _)
    {
        try { apply(); }
        catch (Exception ex) { LinuxDiagnostics.Exception($"{control.GetType().Name}.property", ex); }
    }

    internal static bool IsHandleCreated(Forms.Control control)
    {
        // Majorsilence.Forms controls are safe to configure before native realization.
        return true;
    }

    internal static void InvalidateWhenReady(Forms.Control control)
    {
        try { control.Invalidate(); } catch { }
    }
}
