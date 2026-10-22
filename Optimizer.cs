using WindowsOptimizer.Modules;

namespace WindowsOptimizer;

static class Optimizer
{
    public static SavedState Apply(AppConfig cfg, out string summary)
    {
        var s = new SavedState { AppliedAt = DateTime.Now };
        StateStore.Save(s);
        var lines = new List<string>();
        Log.Write("=== Activando Modo Trabajo ===");

        // Servicios primero: así un servicio (ej. AnyDesk) no vuelve a abrir su programa después de cerrarlo.
        Step("Servicios", () => Services.Apply(cfg, s), s, lines);
        Step("Programas", () => Processes.CloseApps(cfg, s), s, lines);
        Step("Tareas", () => ScheduledTasks.Apply(cfg, s), s, lines);
        Step("Energia", () => PowerPlan.Apply(s), s, lines);
        Step("Registro", () => RegistryTweaks.Apply(cfg, s), s, lines);
        Step("Efectos", () => VisualEffects.Apply(cfg, s), s, lines);
        Step("Defender", () => Defender.Apply(cfg, s), s, lines);
        Step("Explorer", () => Explorer.Apply(cfg, s), s, lines);
        if (cfg.FreeMemory) Step("Memoria", () => Memory.Free(cfg), s, lines);

        summary = string.Join("\n", lines);
        return s;
    }

    public static string Restore(SavedState s)
    {
        Log.Write("=== Restaurando Modo Tradicional ===");
        var lines = new List<string>();
        // Orden inverso al de Apply.
        Step("Explorer", () => { Explorer.Restore(s); return ""; }, null, lines);
        Step("Defender", () => { Defender.Restore(s); return ""; }, null, lines);
        Step("Efectos", () => { VisualEffects.Restore(s); return ""; }, null, lines);
        Step("Registro", () => { RegistryTweaks.Restore(s); return ""; }, null, lines);
        Step("Energía", () => { PowerPlan.Restore(s); return ""; }, null, lines);
        Step("Tareas", () => { ScheduledTasks.Restore(s); return ""; }, null, lines);
        Step("Servicios", () => { Services.Restore(s); return L.T("sum.restarted", s.Services.Count(b => b.WasRunning)); }, null, lines);
        Step("Programas", () => { Processes.RelaunchApps(s); return ""; }, null, lines);
        StateStore.Delete();
        lines.Add(L.T("sum.restored"));
        return string.Join("\n", lines);
    }

    static void Step(string name, Func<string> action, SavedState? s, List<string> lines)
    {
        try
        {
            var r = action();
            if (!string.IsNullOrEmpty(r)) lines.Add(r);
            Log.Write($"{name}: {(string.IsNullOrEmpty(r) ? "ok" : r)}");
        }
        catch (Exception ex) { Log.Write($"{name} ERROR: {ex}"); }
        finally { if (s != null) StateStore.Save(s); }
    }
}
