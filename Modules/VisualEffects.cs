namespace WindowsOptimizer.Modules;

// Efectos que se aplican al instante vía SystemParametersInfo (el registro solo no basta).
static class VisualEffects
{
    // (nombre, SPI_GET*, SPI_SET*, el valor va en uiParam en vez de pvParam)
    static readonly (string Name, uint Get, uint Set, bool ViaUiParam)[] Flags =
    {
        ("ClientAreaAnimation", 0x1042, 0x1043, false),
        ("MenuAnimation", 0x1002, 0x1003, false),
        ("ComboBoxAnimation", 0x1004, 0x1005, false),
        ("ListBoxSmoothScrolling", 0x1006, 0x1007, false),
        ("MenuFade", 0x1012, 0x1013, false),
        ("SelectionFade", 0x1014, 0x1015, false),
        ("TooltipAnimation", 0x1016, 0x1017, false),
        ("TooltipFade", 0x1018, 0x1019, false),
        ("CursorShadow", 0x101A, 0x101B, false),
        ("DropShadow", 0x1024, 0x1025, false),
        ("DragFullWindows", 0x0026, 0x0025, true),
    };
    const uint SPI_GETANIMATION = 0x0048, SPI_SETANIMATION = 0x0049;
    const string MinAnimate = "MinAnimate";

    public static string Apply(AppConfig cfg, SavedState s)
    {
        if (!cfg.VisualEffects) return "";

        foreach (var f in Flags)
        {
            int v = 0;
            if (Native.SystemParametersInfo(f.Get, 0, ref v, 0)) s.Spi[f.Name] = v;
        }
        var ai = new Native.ANIMATIONINFO { cbSize = 8 };
        if (Native.SystemParametersInfo(SPI_GETANIMATION, 8, ref ai, 0)) s.Spi[MinAnimate] = ai.iMinAnimate;
        StateStore.Save(s);

        foreach (var f in Flags.Where(f => s.Spi.ContainsKey(f.Name))) Set(f, 0);
        if (s.Spi.ContainsKey(MinAnimate)) SetAnimation(0);
        return L.T("sum.visual");
    }

    public static void Restore(SavedState s)
    {
        foreach (var f in Flags)
            if (s.Spi.TryGetValue(f.Name, out int v)) Set(f, v);
        if (s.Spi.TryGetValue(MinAnimate, out int a)) SetAnimation(a);
    }

    static void Set((string Name, uint Get, uint Set, bool ViaUiParam) f, int value)
    {
        if (f.ViaUiParam) Native.SystemParametersInfo(f.Set, (uint)value, IntPtr.Zero, Native.SPIF_UPDATE_AND_BROADCAST);
        else Native.SystemParametersInfo(f.Set, 0, (IntPtr)value, Native.SPIF_UPDATE_AND_BROADCAST);
    }

    static void SetAnimation(int value)
    {
        var ai = new Native.ANIMATIONINFO { cbSize = 8, iMinAnimate = value };
        Native.SystemParametersInfo(SPI_SETANIMATION, 8, ref ai, Native.SPIF_UPDATE_AND_BROADCAST);
    }
}
