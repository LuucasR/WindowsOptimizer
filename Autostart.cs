using System.Security;
using System.Security.Principal;
using System.Text;

namespace WindowsOptimizer;

static class TaskXml
{
    // Sin límite de ejecución (schtasks por defecto mata la tarea a las 72 h) y sin restricciones de batería.
    public static string Build(string command, bool logonTrigger, bool highest)
    {
        var user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        var trigger = logonTrigger ? $"<LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId></LogonTrigger>" : "";
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <Triggers>{trigger}</Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>{(highest ? "HighestAvailable" : "LeastPrivilege")}</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>5</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec><Command>{SecurityElement.Escape(command)}</Command></Exec>
              </Actions>
            </Task>
            """;
    }
}

static class Autostart
{
    const string TaskName = App.Name;
    const string LegacyTaskName = "ModoTrabajo";

    public static bool IsEnabled() => Shell.Run("schtasks", "/query", "/tn", TaskName).Code == 0;

    // Si estaba activado el inicio automático con el nombre viejo, se recrea apuntando al exe nuevo.
    public static void MigrateLegacy()
    {
        if (Shell.Run("schtasks", "/query", "/tn", LegacyTaskName).Code != 0) return;
        Shell.Run("schtasks", "/delete", "/tn", LegacyTaskName, "/f");
        try { Enable(); } catch (Exception ex) { Log.Write("No se pudo migrar el inicio automático: " + ex.Message); }
    }

    public static void Enable()
    {
        var xml = Path.Combine(Path.GetTempPath(), App.Name + "_autostart.xml");
        File.WriteAllText(xml, TaskXml.Build(Environment.ProcessPath!, logonTrigger: true, highest: true), Encoding.Unicode);
        var (code, output) = Shell.Run("schtasks", "/create", "/tn", TaskName, "/xml", xml, "/f");
        try { File.Delete(xml); } catch { }
        if (code != 0) throw new InvalidOperationException(output);
    }

    public static void Disable() => Shell.Run("schtasks", "/delete", "/tn", TaskName, "/f");
}
