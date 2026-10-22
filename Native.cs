using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WindowsOptimizer;

static class Native
{
    // ---- SystemParametersInfo ----
    public const uint SPIF_UPDATE_AND_BROADCAST = 0x01 | 0x02;

    [StructLayout(LayoutKind.Sequential)]
    public struct ANIMATIONINFO { public uint cbSize; public int iMinAnimate; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uiParam, ref int pvParam, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uiParam, IntPtr pvParam, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uiParam, ref ANIMATIONINFO pvParam, uint winIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string? lParam, uint flags, uint timeout, out IntPtr result);

    public static void BroadcastSettingChange(string? area) =>
        SendMessageTimeout((IntPtr)0xFFFF, 0x001A /*WM_SETTINGCHANGE*/, IntPtr.Zero, area, 0x0002 /*SMTO_ABORTIFHUNG*/, 1000, out _);

    // ---- Hotkeys / iconos / ventanas ----
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int pid);

    // ---- Memoria ----
    public const uint PROCESS_SET_QUOTA = 0x0100;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;

    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("psapi.dll")] public static extern bool EmptyWorkingSet(IntPtr h);
    [DllImport("ntdll.dll")] public static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

    public static double AvailableRamGb()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref m) ? m.ullAvailPhys / 1073741824.0 : 0;
    }

    // ---- Privilegios ----
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct TOKEN_PRIVILEGES { public uint Count; public long Luid; public uint Attributes; }

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool LookupPrivilegeValue(string? system, string name, out long luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES state, int len, IntPtr prev, IntPtr retLen);

    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();

    public static bool EnablePrivilege(string name)
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x0020 /*ADJUST*/ | 0x0008 /*QUERY*/, out var token)) return false;
        try
        {
            if (!LookupPrivilegeValue(null, name, out long luid)) return false;
            var tp = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attributes = 0x2 /*ENABLED*/ };
            return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero)
                   && Marshal.GetLastWin32Error() == 0;
        }
        finally { CloseHandle(token); }
    }

    // ---- Servicios ----
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr OpenSCManager(string? machine, string? db, uint access);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr OpenService(IntPtr scm, string name, uint access);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool ChangeServiceConfig(IntPtr svc, uint type, uint start, uint error, string? bin, string? group,
        IntPtr tag, string? deps, string? user, string? pwd, string? display);

    [DllImport("advapi32.dll")] static extern bool CloseServiceHandle(IntPtr h);

    public static void SetServiceStartType(string name, int startType)
    {
        const uint NO_CHANGE = 0xFFFFFFFF;
        var scm = OpenSCManager(null, null, 0x0001 /*CONNECT*/);
        if (scm == IntPtr.Zero) throw new Win32Exception();
        try
        {
            var svc = OpenService(scm, name, 0x0002 /*SERVICE_CHANGE_CONFIG*/);
            if (svc == IntPtr.Zero) throw new Win32Exception();
            try
            {
                if (!ChangeServiceConfig(svc, NO_CHANGE, (uint)startType, NO_CHANGE, null, null, IntPtr.Zero, null, null, null, null))
                    throw new Win32Exception();
            }
            finally { CloseServiceHandle(svc); }
        }
        finally { CloseServiceHandle(scm); }
    }
}
