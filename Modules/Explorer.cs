using System.Diagnostics;

namespace WindowsOptimizer.Modules;

static class Explorer
{
    public static string Apply(AppConfig cfg, SavedState s)
    {
        if (!cfg.KillExplorer) return "";
        s.ExplorerKilled = true;
        StateStore.Save(s);

        foreach (var p in Process.GetProcessesByName("explorer"))
            using (p) try { p.Kill(); } catch { }
        return L.T("sum.explorer");
    }

    public static void Restore(SavedState s)
    {
        if (!s.ExplorerKilled || Process.GetProcessesByName("explorer").Length > 0) return;
        // Se lanza vía tarea limitada para que el escritorio no quede corriendo como administrador.
        Shell.RunViaLimitedTask("WindowsOptimizer_Explorer", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
        for (int i = 0; i < 50 && Process.GetProcessesByName("explorer").Length == 0; i++) Thread.Sleep(100);
        Thread.Sleep(1500); // dar tiempo a que la barra de tareas arranque antes de reabrir apps
    }
}
