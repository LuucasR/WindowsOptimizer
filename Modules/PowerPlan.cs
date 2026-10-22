using System.Text.RegularExpressions;

namespace WindowsOptimizer.Modules;

static class PowerPlan
{
    const string UltimateTemplate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    static readonly Regex GuidRx = new(@"[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
    static string GuidFile => Path.Combine(Paths.Dir, "ultimate-guid.txt");

    static string? Active()
    {
        var m = GuidRx.Match(Shell.Run("powercfg", "/getactivescheme").Output);
        return m.Success ? m.Value : null;
    }

    public static string Apply(SavedState s)
    {
        s.PowerScheme = Active();
        StateStore.Save(s);

        var target = EnsureUltimate();
        Shell.Run("powercfg", "/setactive", target);
        return L.T(target == HighPerformance ? "sum.power.high" : "sum.power.ultimate");
    }

    public static void Restore(SavedState s)
    {
        if (s.PowerScheme != null) Shell.Run("powercfg", "/setactive", s.PowerScheme);
    }

    // El plan "Máximo rendimiento" viene oculto en Windows; se duplica una vez y se reutiliza.
    static string EnsureUltimate()
    {
        var list = Shell.Run("powercfg", "/list").Output;
        if (File.Exists(GuidFile))
        {
            var saved = File.ReadAllText(GuidFile).Trim();
            if (list.Contains(saved, StringComparison.OrdinalIgnoreCase)) return saved;
        }

        var m = GuidRx.Match(Shell.Run("powercfg", "-duplicatescheme", UltimateTemplate).Output);
        if (m.Success && !m.Value.Equals(UltimateTemplate, StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(GuidFile, m.Value);
            return m.Value;
        }
        return HighPerformance;
    }
}
