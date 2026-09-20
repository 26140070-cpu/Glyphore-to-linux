using System.Runtime.InteropServices;

namespace Glyphore;

internal static class GtkNative
{
    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void gtk_init(IntPtr argc, IntPtr argv);

    internal static void EnsureInitialized() => gtk_init(IntPtr.Zero, IntPtr.Zero);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_container_add(IntPtr container, IntPtr widget);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_widget_show(IntPtr widget);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_widget_set_hexpand(IntPtr widget, int expand);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_widget_set_vexpand(IntPtr widget, int expand);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_widget_set_can_focus(IntPtr widget, int canFocus);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_widget_add_events(IntPtr widget, int events);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_gl_area_set_required_version(IntPtr area, int major, int minor);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_gl_area_set_auto_render(IntPtr area, int autoRender);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_gl_area_set_has_depth_buffer(IntPtr area, int hasDepth);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_gl_area_set_has_stencil_buffer(IntPtr area, int hasStencil);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_gl_area_make_current(IntPtr area);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_gl_area_queue_render(IntPtr area);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gtk_gl_area_get_error(IntPtr area);

    [DllImport("libepoxy.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "epoxy_get_proc_address")]
    private static extern IntPtr epoxy_get_proc_address([MarshalAs(UnmanagedType.LPStr)] string name);

    internal static IntPtr GetProcAddress(string name) => epoxy_get_proc_address(name);

    internal static void CheckGlAreaError(IntPtr area)
    {
        if (gtk_gl_area_get_error(area) != IntPtr.Zero)
            throw new InvalidOperationException("Gtk.GLArea no pudo crear un contexto OpenGL válido.");
    }

    internal static void QueueRender(IntPtr area)
    {
        if (area != IntPtr.Zero) gtk_gl_area_queue_render(area);
    }

    internal static void MakeCurrent(IntPtr area)
    {
        if (area != IntPtr.Zero) gtk_gl_area_make_current(area);
    }

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void gtk_grab_add(IntPtr widget);

    [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
    private static extern void gtk_grab_remove(IntPtr widget);

    internal static void GtkGrab(IntPtr widget) { if (widget != IntPtr.Zero) gtk_grab_add(widget); }
    internal static void GtkUngrab(IntPtr widget) { if (widget != IntPtr.Zero) gtk_grab_remove(widget); }
}
