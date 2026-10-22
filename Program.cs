namespace WindowsOptimizer;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Local\WindowsOptimizer_SingleInstance", out bool isNew);
        if (!isNew) return;

        Application.ThreadException += (_, e) => Log.Write("UI ERROR: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("FATAL: " + e.ExceptionObject);

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
