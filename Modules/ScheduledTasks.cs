namespace WindowsOptimizer.Modules;

static class ScheduledTasks
{
    // Devuelve líneas "\Ruta\Nombre" de las tareas que estaban activas y se deshabilitaron.
    public static string Apply(AppConfig cfg, SavedState s)
    {
        if (cfg.ScheduledTasksToDisable.Count == 0) return "";
        var script = $$"""
            foreach($p in {{Shell.PsArray(cfg.ScheduledTasksToDisable)}}){
              $i=$p.LastIndexOf('\'); $tp=$p.Substring(0,$i+1); $tn=$p.Substring($i+1)
              $ts=Get-ScheduledTask -TaskPath $tp -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -like $tn -and $_.State -ne 'Disabled' }
              foreach($t in $ts){
                try{ Disable-ScheduledTask -InputObject $t -ErrorAction Stop | Out-Null; "OK=$($t.TaskPath)$($t.TaskName)" }catch{}
              }
            }
            """;
        s.Tasks = Shell.PowerShell(script)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith("OK=")).Select(l => l[3..]).ToList();
        return L.T("sum.tasks", s.Tasks.Count);
    }

    public static void Restore(SavedState s)
    {
        if (s.Tasks.Count == 0) return;
        Shell.PowerShell($$"""
            foreach($p in {{Shell.PsArray(s.Tasks)}}){
              $i=$p.LastIndexOf('\')
              $tn=$p.Substring($i+1)
              Get-ScheduledTask -TaskPath $p.Substring(0,$i+1) -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -eq $tn } | Enable-ScheduledTask -ErrorAction SilentlyContinue | Out-Null
            }
            """);
    }
}
