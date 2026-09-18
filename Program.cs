using System.Windows.Forms;

namespace Glyphore;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrash(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrash(e.Exception);
            e.SetObserved();
        };

        if (OperatingSystem.IsLinux()) GtkNative.EnsureInitialized();

        try
        {
            var mainForm = new MainForm();
            LinuxWindowIcon.Initialize();
            Application.Run(mainForm);
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
            try
            {
                MessageBox.Show(ex.ToString(), "Glyphoré", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
            Environment.ExitCode = 1;
        }
    }

    private static void WriteCrash(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glyphore");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"), $"[{DateTimeOffset.Now:u}]\n{ex}\n\n");
        }
        catch { }
    }
}
