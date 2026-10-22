using System.Diagnostics;
using System.Text;

namespace WindowsOptimizer;

static class App
{
    public const string Name = "WindowsOptimizer";

    static Icon? icon;
    public static Icon? Icon => icon ??= Environment.ProcessPath is { } p ? Icon.ExtractAssociatedIcon(p) : null;
}

static class Paths
{
    static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    public static readonly string Dir = Path.Combine(ProgramData, App.Name);
    public static string Config => Path.Combine(Dir, "config.json");
    public static string State => Path.Combine(Dir, "state.json");
    public static string LogFile => Path.Combine(Dir, "log.txt");

    static Paths()
    {
        // La app antes se llamaba "ModoTrabajo": se conserva su config y su estado.
        var legacy = Path.Combine(ProgramData, "ModoTrabajo");
        if (!Directory.Exists(Dir) && Directory.Exists(legacy))
        {
            try { Directory.Move(legacy, Dir); } catch { }
        }
        Directory.CreateDirectory(Dir);
    }
}

static class Log
{
    static readonly object Gate = new();

    public static void Write(string msg)
    {
        lock (Gate)
        {
            try
            {
                var fi = new FileInfo(Paths.LogFile);
                if (fi.Exists && fi.Length > 1_000_000) fi.Delete();
                File.AppendAllText(Paths.LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}");
            }
            catch { }
        }
    }
}

static class Glob
{
    // Soporta un único comodín "*" al final del patrón (ej: "OneSyncSvc*").
    public static bool Match(string pattern, string text) =>
        pattern.EndsWith('*')
            ? text.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(pattern, text, StringComparison.OrdinalIgnoreCase);

    public static bool Any(IEnumerable<string> patterns, string text) => patterns.Any(p => Match(p, text));
}

static class Shell
{
    public static (int Code, string Output) Run(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(90_000))
        {
            try { p.Kill(true); } catch { }
            return (-1, "timeout");
        }
        return (p.ExitCode, stdout.Result + stderr.Result);
    }

    public static string PowerShell(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes("$ProgressPreference='SilentlyContinue';" + script));
        return Run("powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded)
            .Output.Trim();
    }

    public static string PsQuote(string s) => "'" + s.Replace("'", "''") + "'";

    public static string PsArray(IEnumerable<string> items) => "@(" + string.Join(",", items.Select(PsQuote)) + ")";

    // Lanza un programa SIN privilegios de administrador (esta app corre elevada).
    // Si explorer está vivo, se le delega el lanzamiento; si no, se usa una tarea programada limitada.
    public static void StartUnelevated(string path)
    {
        if (Process.GetProcessesByName("explorer").Length > 0)
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false })?.Dispose();
            return;
        }
        RunViaLimitedTask("WindowsOptimizer_Run", path);
    }

    public static void RunViaLimitedTask(string taskName, string command)
    {
        var xml = Path.Combine(Path.GetTempPath(), taskName + ".xml");
        File.WriteAllText(xml, TaskXml.Build(command, logonTrigger: false, highest: false), Encoding.Unicode);
        Run("schtasks", "/create", "/tn", taskName, "/xml", xml, "/f");
        Run("schtasks", "/run", "/tn", taskName);
        try { File.Delete(xml); } catch { }
    }
}
