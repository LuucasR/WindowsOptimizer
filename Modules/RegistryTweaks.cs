using Microsoft.Win32;

namespace WindowsOptimizer.Modules;

// Valores int => REG_DWORD, string => REG_SZ.
record Tweak(string Hive, string Path, string Name, object Value);

static class RegistryTweaks
{
    const string Cv = @"Software\Microsoft\Windows\CurrentVersion";

    static IEnumerable<Tweak> For(AppConfig cfg)
    {
        // Juego / planificador: siempre
        yield return new("HKCU", @"System\GameConfigStore", "GameDVR_Enabled", 0);
        yield return new("HKCU", $@"{Cv}\GameDVR", "AppCaptureEnabled", 0);
        yield return new("HKCU", @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
        yield return new("HKCU", @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
        yield return new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);
        yield return new("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance", "MaintenanceDisabled", 1);
        // Quantums cortos y prioridad a la ventana en primer plano
        yield return new("HKLM", @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 0x26);

        if (cfg.VisualEffects)
        {
            yield return new("HKCU", $@"{Cv}\Explorer\VisualEffects", "VisualFXSetting", 2);
            yield return new("HKCU", $@"{Cv}\Themes\Personalize", "EnableTransparency", 0);
            yield return new("HKCU", $@"{Cv}\Explorer\Advanced", "TaskbarAnimations", 0);
            yield return new("HKCU", $@"{Cv}\Explorer\Advanced", "ListviewAlphaSelect", 0);
            yield return new("HKCU", $@"{Cv}\Explorer\Advanced", "ListviewShadow", 0);
            yield return new("HKCU", @"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0);
            yield return new("HKCU", @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0");
        }
        if (cfg.DisableBackgroundApps)
        {
            yield return new("HKCU", $@"{Cv}\BackgroundAccessApplications", "GlobalUserDisabled", 1);
            yield return new("HKCU", $@"{Cv}\Search", "BackgroundAppGlobalToggle", 0);
            yield return new("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 2);
        }
        if (cfg.DisableNotifications)
        {
            yield return new("HKCU", $@"{Cv}\PushNotifications", "ToastEnabled", 0);
            yield return new("HKCU", $@"{Cv}\Notifications\Settings", "NOC_GLOBAL_SETTING_TOASTS_ENABLED", 0);
        }
        if (cfg.KillExplorer)
            yield return new("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "AutoRestartShell", 0);
    }

    static RegistryKey Root(string hive) => hive == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;

    public static string Apply(AppConfig cfg, SavedState s)
    {
        var tweaks = For(cfg).ToList();
        var toApply = new List<Tweak>();

        foreach (var t in tweaks)
        {
            try
            {
                using var key = Root(t.Hive).OpenSubKey(t.Path);
                var old = key?.GetValue(t.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (old == null)
                {
                    s.Registry.Add(new RegBackup(t.Hive, t.Path, t.Name, false, null, null));
                }
                else
                {
                    var kind = key!.GetValueKind(t.Name);
                    if (kind is not (RegistryValueKind.DWord or RegistryValueKind.String or RegistryValueKind.ExpandString)) continue;
                    s.Registry.Add(new RegBackup(t.Hive, t.Path, t.Name, true, kind.ToString(), Convert.ToString(old)));
                }
                toApply.Add(t);
            }
            catch (Exception ex) { Log.Write($"Registro {t.Path}\\{t.Name}: {ex.Message}"); }
        }
        StateStore.Save(s);

        foreach (var t in toApply)
        {
            try
            {
                using var key = Root(t.Hive).CreateSubKey(t.Path, writable: true);
                key.SetValue(t.Name, t.Value, t.Value is int ? RegistryValueKind.DWord : RegistryValueKind.String);
            }
            catch (Exception ex) { Log.Write($"Registro {t.Path}\\{t.Name}: {ex.Message}"); }
        }
        Broadcast();
        return "";
    }

    public static void Restore(SavedState s)
    {
        foreach (var b in s.Registry)
        {
            try
            {
                using var key = Root(b.Hive).CreateSubKey(b.Path, writable: true);
                if (!b.Existed) { key.DeleteValue(b.Name, throwOnMissingValue: false); continue; }
                var kind = Enum.Parse<RegistryValueKind>(b.Kind!);
                object value = kind == RegistryValueKind.DWord ? unchecked((int)long.Parse(b.Data!)) : b.Data!;
                key.SetValue(b.Name, value, kind);
            }
            catch (Exception ex) { Log.Write($"Restaurar registro {b.Path}\\{b.Name}: {ex.Message}"); }
        }
        Broadcast();
    }

    static void Broadcast()
    {
        Native.BroadcastSettingChange("ImmersiveColorSet");
        Native.BroadcastSettingChange("WindowMetrics");
        Native.BroadcastSettingChange("Policy");
        Native.BroadcastSettingChange(null);
    }
}
