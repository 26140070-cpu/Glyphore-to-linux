using System.Diagnostics;

namespace Glyphore;

/// <summary>
/// En Wayland la barra de título la pinta KWin/GNOME (decoración del servidor, SSD)
/// con el esquema de color del sistema; la app NO puede teñirla. Con GLYPHORE_CSD=1
/// la app extiende su área cliente y dibuja su propia barra oscura (decoración de
/// cliente), igual en KDE/GNOME/todas las distros. Falla de forma silenciosa (SSD)
/// si el backend no lo soporta.
/// </summary>
internal static class LinuxWindowChrome
{
    public static void TryEnableClientSideDecorations()
    {
        if (Environment.GetEnvironmentVariable("GLYPHORE_CSD") is not "1") return;
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (Avalonia.Application.Current?.ApplicationLifetime
                        is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life) return;
                    if (life.MainWindow is not { } window) return;
                    window.ExtendClientAreaToDecorationsHint = true;
                    window.ExtendClientAreaTitleBarHeightHint = 36;
                    window.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x10, 0x10, 0x18));
                    window.SystemDecorations = Avalonia.Controls.SystemDecorations.BorderOnly;
                }
                catch (Exception ex) { Trace.WriteLine($"CSD unavailable: {ex.Message}"); }
            });
        }
        catch { }
    }
}
