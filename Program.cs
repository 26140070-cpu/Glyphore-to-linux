using Majorsilence.Forms;

namespace Glyphore;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        LinuxDiagnostics.Initialize(args);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            LinuxDiagnostics.Exception("AppDomain.CurrentDomain.UnhandledException", ex ?? new Exception(e.ExceptionObject?.ToString()));
            WriteCrash(ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LinuxDiagnostics.Exception("TaskScheduler.UnobservedTaskException", e.Exception);
            WriteCrash(e.Exception);
            e.SetObserved();
        };
        try
        {
            LinuxDiagnostics.Phase("Avalonia/Majorsilence.Forms initialization");

            LinuxDiagnostics.Phase("MainForm construction");
            var mainForm = new MainForm();
            LinuxDiagnostics.Phase("MainForm constructed successfully");
            LinuxDiagnostics.EndStartupTrace();

            LinuxDiagnostics.Phase("Linux window icon initialization");
            LinuxWindowIcon.Initialize();
            LinuxDiagnostics.Phase("Application.Run");
            Application.Run(mainForm);
            LinuxDiagnostics.Phase("Application.Run returned");
        }
        catch (Exception ex)
        {
            LinuxDiagnostics.Exception("Program.Main", ex);
            WriteCrash(ex);
            try
            {
                MessageBox.Show(ex.ToString(), "Glyphoré", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
            Environment.ExitCode = 1;
            LinuxDiagnostics.End(1);
            return;
        }

        LinuxDiagnostics.End(0);
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
