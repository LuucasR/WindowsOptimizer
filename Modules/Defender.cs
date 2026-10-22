namespace WindowsOptimizer.Modules;

static class Defender
{
    public static string Apply(AppConfig cfg, SavedState s)
    {
        if (!cfg.DisableDefenderRealtime && cfg.DefenderExclusionPaths.Count == 0) return "";

        var output = Shell.PowerShell($$"""
            try{ $p=Get-MpPreference -ErrorAction Stop }catch{ 'NODEFENDER'; return }
            "WAS=$($p.DisableRealtimeMonitoring)"
            foreach($x in {{Shell.PsArray(cfg.DefenderExclusionPaths)}}){
              if(-not ($p.ExclusionPath -contains $x)){
                try{ Add-MpPreference -ExclusionPath $x -ErrorAction Stop; "ADD=$x" }catch{}
              }
            }
            if(${{cfg.DisableDefenderRealtime.ToString().ToLowerInvariant()}}){
              Set-MpPreference -DisableRealtimeMonitoring $true -ErrorAction SilentlyContinue
            }
            "NOW=$((Get-MpPreference).DisableRealtimeMonitoring)"
            """);

        var lines = output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Contains("NODEFENDER")) return "";

        s.DefenderExclusionsAdded = lines.Where(l => l.StartsWith("ADD=")).Select(l => l[4..]).ToList();
        bool was = lines.Contains("WAS=True"), now = lines.Contains("NOW=True");
        s.DefenderRealtimeChanged = !was && now;

        if (!cfg.DisableDefenderRealtime) return "";
        if (now) return L.T("sum.defender.ok");
        return L.T("sum.defender.fail");
    }

    public static void Restore(SavedState s)
    {
        if (!s.DefenderRealtimeChanged && s.DefenderExclusionsAdded.Count == 0) return;
        Shell.PowerShell($$"""
            if(${{s.DefenderRealtimeChanged.ToString().ToLowerInvariant()}}){ Set-MpPreference -DisableRealtimeMonitoring $false -ErrorAction SilentlyContinue }
            foreach($x in {{Shell.PsArray(s.DefenderExclusionsAdded)}}){ Remove-MpPreference -ExclusionPath $x -ErrorAction SilentlyContinue }
            """);
    }
}
