using System.Globalization;

namespace WindowsOptimizer;

// Textos de la interfaz en español, inglés y chino simplificado.
static class L
{
    public static readonly (string Code, string Name)[] Languages = { ("es", "Español"), ("en", "English"), ("zh", "中文") };

    public static string Lang { get; set; } = Detect();

    public static string Detect() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "es" => "es",
        "zh" => "zh",
        _ => "en",
    };

    public static void Set(string? code) =>
        Lang = Languages.Any(l => l.Code == code) ? code! : Detect();

    public static string T(string key, params object[] args)
    {
        if (!Table.TryGetValue(key, out var t)) return key;
        var s = Lang switch { "en" => t[1], "zh" => t[2], _ => t[0] };
        return args.Length == 0 ? s : string.Format(CultureInfo.CurrentCulture, s, args);
    }

    // clave => [español, inglés, chino]
    static readonly Dictionary<string, string[]> Table = new()
    {
        // ---- Menú / bandeja ----
        ["mode.traditional"] = new[] { "Modo Tradicional", "Traditional mode", "传统模式" },
        ["mode.work"] = new[] { "Modo Trabajo", "Work mode", "工作模式" },
        ["mode.traditional.short"] = new[] { "Tradicional", "Traditional", "传统" },
        ["mode.work.short"] = new[] { "Trabajo", "Work", "工作" },
        ["menu.reapply"] = new[] { "Reiniciar Modo Trabajo (aplicar config nueva)", "Restart Work mode (apply new settings)", "重新启动工作模式（应用新设置）" },
        ["menu.freeRam"] = new[] { "Liberar RAM ahora", "Free RAM now", "立即释放内存" },
        ["menu.settings"] = new[] { "Configuración...", "Settings...", "设置..." },
        ["menu.log"] = new[] { "Ver registro", "View log", "查看日志" },
        ["menu.autostart"] = new[] { "Iniciar con Windows", "Start with Windows", "开机自动启动" },
        ["menu.language"] = new[] { "Idioma", "Language", "语言" },
        ["menu.exit"] = new[] { "Salir", "Exit", "退出" },
        ["tip.busy"] = new[] { "WindowsOptimizer · aplicando cambios...", "WindowsOptimizer · applying changes...", "WindowsOptimizer · 正在应用更改..." },
        ["tip.status"] = new[] { "WindowsOptimizer · {0} · RAM libre {1:0.0} GB", "WindowsOptimizer · {0} · free RAM {1:0.0} GB", "WindowsOptimizer · {0} · 可用内存 {1:0.0} GB" },
        ["balloon.workOn"] = new[] { "Modo Trabajo activado", "Work mode enabled", "工作模式已启用" },
        ["balloon.memory"] = new[] { "Memoria", "Memory", "内存" },
        ["error"] = new[] { "Error: {0}", "Error: {0}", "错误：{0}" },
        ["ask.recover"] = new[]
        {
            "El Modo Trabajo quedó activo desde {0}.\n\n¿Restaurar Windows a la normalidad ahora?\n(\"No\" mantiene el Modo Trabajo activo)",
            "Work mode has been active since {0}.\n\nRestore Windows to normal now?\n(\"No\" keeps Work mode active)",
            "工作模式自 {0} 起一直处于启用状态。\n\n现在将 Windows 恢复正常吗？\n（选择“否”将保持工作模式）",
        },
        ["ask.exit"] = new[]
        {
            "Estás en Modo Trabajo. ¿Restaurar Windows antes de salir?\n\n(\"No\" deja todo como está; al volver a abrir la app podrás restaurar)",
            "You are in Work mode. Restore Windows before exiting?\n\n(\"No\" leaves everything as is; you can restore when you reopen the app)",
            "当前处于工作模式。退出前恢复 Windows 吗？\n\n（选择“否”将保持现状，重新打开应用时可以恢复）",
        },
        ["ask.reapply"] = new[]
        {
            "Configuración guardada.\n\n¿Reiniciar el Modo Trabajo ahora para aplicarla?",
            "Settings saved.\n\nRestart Work mode now to apply them?",
            "设置已保存。\n\n现在重新启动工作模式以应用设置吗？",
        },
        ["err.autostart"] = new[] { "No se pudo cambiar el inicio automático:\n{0}", "Could not change start with Windows:\n{0}", "无法更改开机启动：\n{0}" },
        ["err.config"] = new[]
        {
            "config.json tiene un error y se usarán los valores por defecto:\n\n{0}",
            "config.json has an error; default settings will be used:\n\n{0}",
            "config.json 有错误，将使用默认设置：\n\n{0}",
        },

        // ---- Resumen al aplicar / restaurar ----
        ["sum.services"] = new[] { "{0} servicios detenidos", "{0} services stopped", "已停止 {0} 个服务" },
        ["sum.apps"] = new[] { "{0} programas cerrados", "{0} programs closed", "已关闭 {0} 个程序" },
        ["sum.tasks"] = new[] { "{0} tareas programadas pausadas", "{0} scheduled tasks paused", "已暂停 {0} 个计划任务" },
        ["sum.power.ultimate"] = new[] { "Energía: Máximo rendimiento", "Power: Ultimate Performance", "电源：卓越性能" },
        ["sum.power.high"] = new[] { "Energía: Alto rendimiento", "Power: High performance", "电源：高性能" },
        ["sum.visual"] = new[] { "Efectos visuales desactivados", "Visual effects disabled", "已关闭视觉效果" },
        ["sum.defender.ok"] = new[] { "Defender en tiempo real: pausado", "Defender real-time: paused", "Defender 实时保护：已暂停" },
        ["sum.defender.fail"] = new[]
        {
            "Defender: no se pudo pausar (desactiva \"Protección contra alteraciones\" en Seguridad de Windows)",
            "Defender: could not pause (turn off \"Tamper Protection\" in Windows Security)",
            "Defender：无法暂停（请在 Windows 安全中心关闭“篡改防护”）",
        },
        ["sum.explorer"] = new[] { "Explorer cerrado (Ctrl+Alt+W para volver)", "Explorer closed (Ctrl+Alt+W to go back)", "已关闭资源管理器（按 Ctrl+Alt+W 返回）" },
        ["sum.ram"] = new[] { "RAM libre: {0:0.0} → {1:0.0} GB", "Free RAM: {0:0.0} → {1:0.0} GB", "可用内存：{0:0.0} → {1:0.0} GB" },
        ["sum.restarted"] = new[] { "{0} servicios reiniciados", "{0} services restarted", "已重新启动 {0} 个服务" },
        ["sum.restored"] = new[] { "Windows restaurado a su estado normal", "Windows restored to normal", "Windows 已恢复正常" },

        // ---- Ventana de configuración ----
        ["cfg.title"] = new[] { "WindowsOptimizer — Configuración", "WindowsOptimizer — Settings", "WindowsOptimizer — 设置" },
        ["cfg.save"] = new[] { "Guardar", "Save", "保存" },
        ["cfg.cancel"] = new[] { "Cancelar", "Cancel", "取消" },
        ["cfg.json"] = new[] { "Editar JSON a mano...", "Edit JSON manually...", "手动编辑 JSON..." },
        ["cfg.hint"] = new[] { "Los cambios se aplican la próxima vez que actives el Modo Trabajo.", "Changes apply the next time you enable Work mode.", "更改将在下次启用工作模式时生效。" },
        ["cfg.tab.close"] = new[] { "Programas a cerrar", "Programs to close", "要关闭的程序" },
        ["cfg.tab.close.desc"] = new[]
        {
            "TODOS los programas de esta tabla se cierran al activar el Modo Trabajo. La casilla solo indica si se vuelven a abrir al volver a Tradicional.",
            "ALL programs in this table are closed when Work mode is enabled. The checkbox only sets whether they reopen when you go back to Traditional.",
            "启用工作模式时会关闭此表中的所有程序。复选框仅表示返回传统模式时是否重新打开。",
        },
        ["cfg.col.program"] = new[] { "Programa (se cierra)", "Program (gets closed)", "程序（将被关闭）" },
        ["cfg.col.relaunch"] = new[] { "¿Reabrir al volver?", "Reopen afterwards?", "恢复时重新打开？" },
        ["cfg.noRelaunch"] = new[]
        {
            "Proceso de fondo o actualizador: nunca se reabre (Windows lo inicia solo cuando hace falta).",
            "Background process or updater: never reopened (Windows starts it when needed).",
            "后台进程或更新程序：不会重新打开（Windows 会在需要时自动启动）。",
        },
        ["cfg.tab.priority"] = new[] { "Prioridad alta", "High priority", "高优先级" },
        ["cfg.tab.priority.desc"] = new[]
        {
            "Reciben prioridad ALTA de CPU en Modo Trabajo (Unreal, tus juegos, etc.). Se revisa cada 5 s, así que pueden abrirse después.",
            "Get HIGH CPU priority in Work mode (Unreal, your games, etc.). Checked every 5 s, so they can be opened later.",
            "在工作模式下获得较高的 CPU 优先级（Unreal、游戏等）。每 5 秒检查一次，因此可以稍后再打开。",
        },
        ["cfg.tab.services"] = new[] { "Servicios", "Services", "服务" },
        ["cfg.tab.services.desc"] = new[]
        {
            "Servicios de Windows y de otros programas que se detienen en Modo Trabajo.",
            "Windows and third-party services stopped in Work mode.",
            "在工作模式下停止的 Windows 服务和第三方服务。",
        },
        ["cfg.tab.options"] = new[] { "Opciones", "Options", "选项" },
        ["cfg.search"] = new[] { "Buscar servicio...", "Search service...", "搜索服务..." },
        ["cfg.running"] = new[] { "● activo", "● running", "● 运行中" },
        ["cfg.onlyRunning"] = new[] { "Ver solo activos", "Show running only", "仅显示运行中" },
        ["cfg.recommended"] = new[] { "Marcar recomendados", "Select recommended", "选择推荐项" },
        ["cfg.services.note"] = new[]
        {
            "Marcados = se detienen. Los esenciales (audio, red, AMD, Parsec, anti-cheats...) no aparecen.",
            "Checked = stopped. Essential ones (audio, network, AMD, Parsec, anti-cheats...) are hidden.",
            "勾选 = 停止。关键服务（音频、网络、AMD、Parsec、反作弊等）不会显示。",
        },
        ["cfg.addRunning"] = new[] { "Agregar programa abierto...", "Add running program...", "添加正在运行的程序..." },
        ["cfg.browse"] = new[] { "Buscar .exe...", "Browse .exe...", "浏览 .exe..." },
        ["cfg.add"] = new[] { "Agregar", "Add", "添加" },
        ["cfg.remove"] = new[] { "Quitar", "Remove", "移除" },
        ["cfg.exeFilter"] = new[] { "Programas (*.exe)|*.exe", "Programs (*.exe)|*.exe", "程序 (*.exe)|*.exe" },
        ["cfg.addFolder"] = new[] { "Agregar carpeta...", "Add folder...", "添加文件夹..." },
        ["cfg.folderDesc"] = new[] { "Carpeta que Defender no debe escanear", "Folder Defender should not scan", "Defender 不应扫描的文件夹" },
        ["cfg.exclusions"] = new[] { "Carpetas excluidas del antivirus durante el modo:", "Folders excluded from the antivirus during the mode:", "模式期间从杀毒扫描中排除的文件夹：" },
        ["cfg.opt.visual"] = new[] { "Quitar animaciones, sombras y transparencias", "Remove animations, shadows and transparency", "关闭动画、阴影和透明效果" },
        ["cfg.opt.background"] = new[] { "Bloquear apps en segundo plano", "Block background apps", "阻止后台应用" },
        ["cfg.opt.notifications"] = new[] { "Silenciar notificaciones", "Mute notifications", "关闭通知" },
        ["cfg.opt.defender"] = new[]
        {
            "Pausar Defender en tiempo real (requiere \"Protección contra alteraciones\" desactivada)",
            "Pause Defender real-time protection (requires \"Tamper Protection\" off)",
            "暂停 Defender 实时保护（需要关闭“篡改防护”）",
        },
        ["cfg.opt.freeRam"] = new[] { "Liberar RAM de procesos en segundo plano al activar", "Free RAM from background processes when enabling", "启用时释放后台进程的内存" },
        ["cfg.opt.purge"] = new[]
        {
            "Vaciar TODA la caché de RAM (la próxima carga de proyectos será más lenta)",
            "Clear ALL RAM cache (next project load will be slower)",
            "清空全部内存缓存（下次加载项目会变慢）",
        },
        ["cfg.opt.explorer"] = new[] { "Cerrar escritorio y barra de tareas (Ctrl+Alt+W para volver)", "Close desktop and taskbar (Ctrl+Alt+W to go back)", "关闭桌面和任务栏（按 Ctrl+Alt+W 返回）" },

        // ---- Selector de procesos ----
        ["pick.title"] = new[] { "Programas abiertos", "Running programs", "正在运行的程序" },
        ["pick.program"] = new[] { "Programa", "Program", "程序" },
        ["pick.window"] = new[] { "Ventana / ubicación", "Window / location", "窗口 / 位置" },
        ["pick.ok"] = new[] { "Agregar marcados", "Add checked", "添加所选" },
    };
}
