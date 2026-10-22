using System.Diagnostics;

namespace WindowsOptimizer.Modules;

static class Memory
{
    const int SystemMemoryListInformation = 80;
    const int MemoryPurgeStandbyList = 4, MemoryPurgeLowPriorityStandbyList = 5;

    public static string Free(AppConfig cfg)
    {
        double before = Native.AvailableRamGb();

        // No se vacía la RAM de lo que estás usando: ventana activa + programas priorizados.
        var keep = new HashSet<string>(cfg.ProcessesToPrioritize, StringComparer.OrdinalIgnoreCase);
        Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out int fgPid);
        int self = Environment.ProcessId;

        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                if (p.Id <= 4 || p.Id == fgPid || p.Id == self || keep.Contains(p.ProcessName)) continue;
                var h = Native.OpenProcess(Native.PROCESS_SET_QUOTA | Native.PROCESS_QUERY_INFORMATION, false, p.Id);
                if (h == IntPtr.Zero) continue;
                Native.EmptyWorkingSet(h);
                Native.CloseHandle(h);
            }
        }

        if (Native.EnablePrivilege("SeProfileSingleProcessPrivilege"))
        {
            int cmd = cfg.PurgeStandbyFull ? MemoryPurgeStandbyList : MemoryPurgeLowPriorityStandbyList;
            Native.NtSetSystemInformation(SystemMemoryListInformation, ref cmd, sizeof(int));
        }

        double after = Native.AvailableRamGb();
        return L.T("sum.ram", before, after);
    }
}
