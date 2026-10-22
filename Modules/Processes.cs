using System.Diagnostics;

namespace WindowsOptimizer.Modules;

static class Processes
{
    static string Normalize(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    // Procesos de fondo que nunca deben reabrirse: lanzarlos "a mano" abre ventanas que el usuario no pidió
    // (ej. AMDInstallManager abre el instalador de AMD). Windows o su servicio los arranca solo cuando hace falta.
    static readonly string[] NeverRelaunch =
    {
        "Widgets", "WidgetService", "PhoneExperienceHost", "YourPhone", "CrossDeviceService", "CrossDeviceResume",
        "Copilot", "GameBar", "GameBarFTServer", "HxTsr", "HxOutlook", "Cortana", "CompatTelRunner", "DeviceCensus",
        "AppleMobileDeviceProcess", "iTunesHelper", "adb", "AnyDesk", "TeamViewer",
    };
    static readonly string[] NeverRelaunchWords = { "update", "install", "setup", "helper", "service", "crash" };

    public static bool CanRelaunch(string name)
    {
        name = Normalize(name);
        return !NeverRelaunch.Contains(name, StringComparer.OrdinalIgnoreCase)
               && !NeverRelaunchWords.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    public static string CloseApps(AppConfig cfg, SavedState s)
    {
        var victims = new List<Process>();
        foreach (var name in cfg.ProcessesToClose.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var procs = Process.GetProcessesByName(name);
            if (procs.Length == 0) continue;

            if (CanRelaunch(name) && cfg.RelaunchOnRestore.Any(r => Normalize(r).Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                var path = procs.Select(TryPath).FirstOrDefault(p => p != null);
                if (path != null) s.ClosedApps.Add(new ClosedApp(name, path));
            }
            victims.AddRange(procs);
        }
        StateStore.Save(s);

        // Primero se pide amablemente que cierren; a los 2 s se fuerza.
        foreach (var p in victims)
            try { if (p.MainWindowHandle != IntPtr.Zero) p.CloseMainWindow(); } catch { }

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && victims.Any(p => !HasExited(p))) Thread.Sleep(100);

        foreach (var p in victims.Where(p => !HasExited(p)))
            try { p.Kill(entireProcessTree: true); } catch { }

        int count = victims.Select(p => p.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        foreach (var p in victims) p.Dispose();
        return count > 0 ? L.T("sum.apps", count) : "";
    }

    public static void RelaunchApps(SavedState s)
    {
        foreach (var app in s.ClosedApps)
        {
            if (!CanRelaunch(app.Name) || Process.GetProcessesByName(app.Name).Length > 0 || !File.Exists(app.Path)) continue;
            try { Shell.StartUnelevated(app.Path); }
            catch (Exception ex) { Log.Write($"No se pudo reabrir {app.Name}: {ex.Message}"); }
        }
    }

    // Se llama periódicamente en Modo Trabajo para capturar juegos/apps abiertos después.
    public static void BoostPriorities(AppConfig cfg, HashSet<int> boosted)
    {
        foreach (var name in cfg.ProcessesToPrioritize.Select(Normalize))
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                using (p)
                {
                    try
                    {
                        if (p.PriorityClass != ProcessPriorityClass.High) p.PriorityClass = ProcessPriorityClass.High;
                        boosted.Add(p.Id);
                    }
                    catch { } // anti-cheats o procesos protegidos
                }
            }
        }
    }

    public static void ResetPriorities(HashSet<int> boosted)
    {
        foreach (var id in boosted)
        {
            try
            {
                using var p = Process.GetProcessById(id);
                if (p.PriorityClass == ProcessPriorityClass.High) p.PriorityClass = ProcessPriorityClass.Normal;
            }
            catch { }
        }
        boosted.Clear();
    }

    static string? TryPath(Process p)
    {
        try { return p.MainModule?.FileName; } catch { return null; }
    }

    static bool HasExited(Process p)
    {
        try { return p.HasExited; } catch { return true; }
    }
}
