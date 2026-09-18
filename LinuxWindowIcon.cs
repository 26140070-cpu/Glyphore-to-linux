using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Glyphore;

internal static class LinuxWindowIcon
{
    private static string? _iconPath;

    public static void Initialize()
    {
        if (!OperatingSystem.IsLinux()) return;
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glyphore");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "Glyphore-Icon-1024.png");
            if (!File.Exists(path))
            {
                using Stream? stream = typeof(LinuxWindowIcon).Assembly.GetManifestResourceStream("Glyphore.BrandIcon.png");
                if (stream is null) return;
                using var file = File.Create(path);
                stream.CopyTo(file);
            }
            _iconPath = path;
            gtk_window_set_default_icon_from_file(path, out IntPtr error);
            if (error != IntPtr.Zero) g_error_free(error);
        }
        catch { }
    }

    public static void Apply(Form form)
    {
        if (!OperatingSystem.IsLinux() || string.IsNullOrEmpty(_iconPath)) return;
        try
        {
            gtk_window_set_icon_from_file(form.Handle, _iconPath!, out IntPtr error);
            if (error != IntPtr.Zero) g_error_free(error);
        }
        catch { }
    }

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void gtk_window_set_default_icon_from_file([MarshalAs(UnmanagedType.LPUTF8Str)] string filename, out IntPtr error);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void gtk_window_set_icon_from_file(IntPtr window, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename, out IntPtr error);

    [DllImport("libglib-2.0.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void g_error_free(IntPtr error);
}
