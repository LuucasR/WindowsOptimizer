using System.Text.Json;

namespace WindowsOptimizer;

record ServiceBackup(string Name, int? StartType, bool WasRunning);
record RegBackup(string Hive, string Path, string Name, bool Existed, string? Kind, string? Data);
record ClosedApp(string Name, string Path);

// Todo lo necesario para dejar Windows exactamente como estaba.
sealed class SavedState
{
    public DateTime AppliedAt { get; set; }
    public string? PowerScheme { get; set; }
    public List<ServiceBackup> Services { get; set; } = new();
    public List<string> Tasks { get; set; } = new();
    public List<RegBackup> Registry { get; set; } = new();
    public Dictionary<string, int> Spi { get; set; } = new();
    public bool DefenderRealtimeChanged { get; set; }
    public List<string> DefenderExclusionsAdded { get; set; } = new();
    public bool ExplorerKilled { get; set; }
    public List<ClosedApp> ClosedApps { get; set; } = new();
}

static class StateStore
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    static readonly object Gate = new();

    public static bool Exists => File.Exists(Paths.State);

    public static SavedState? Load()
    {
        try { return JsonSerializer.Deserialize<SavedState>(File.ReadAllText(Paths.State)); }
        catch (Exception ex) { Log.Write("state.json ilegible: " + ex.Message); return null; }
    }

    public static void Save(SavedState s)
    {
        lock (Gate)
        {
            var tmp = Paths.State + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(s, Options));
            File.Move(tmp, Paths.State, overwrite: true);
        }
    }

    public static void Delete()
    {
        try { File.Delete(Paths.State); } catch { }
    }
}
