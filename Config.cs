using System.Text.Encodings.Web;
using System.Text.Json;

namespace WindowsOptimizer;

sealed class AppConfig
{
    // Al subir este número, las listas por defecto nuevas se suman a la config del usuario (sin borrar lo suyo).
    const int CurrentVersion = 2;

    public int Version { get; set; }
    public string? Language { get; set; }   // "es", "en", "zh"; vacío = idioma de Windows
    public List<string> ServicesToStop { get; set; } = new();
    public List<string> ProtectedServices { get; set; } = new();
    public List<string> ScheduledTasksToDisable { get; set; } = new();
    public List<string> ProcessesToClose { get; set; } = new();
    public List<string> RelaunchOnRestore { get; set; } = new();
    public List<string> ProcessesToPrioritize { get; set; } = new();
    public List<string> DefenderExclusionPaths { get; set; } = new();
    public bool VisualEffects { get; set; } = true;
    public bool DisableBackgroundApps { get; set; } = true;
    public bool DisableNotifications { get; set; } = true;
    public bool DisableDefenderRealtime { get; set; } = true;
    public bool FreeMemory { get; set; } = true;
    public bool PurgeStandbyFull { get; set; }
    public bool KillExplorer { get; set; }

    static readonly JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppConfig Defaults() => JsonSerializer.Deserialize<AppConfig>(DefaultJson, Options)!;

    public void Save() =>
        File.WriteAllText(Paths.Config,
            "// Editado desde WindowsOptimizer (menú > Editar configuración). Se admiten comentarios //\n"
            + JsonSerializer.Serialize(this, SaveOptions));

    public static AppConfig Load()
    {
        if (!File.Exists(Paths.Config)) File.WriteAllText(Paths.Config, DefaultJson);
        try
        {
            var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Paths.Config), Options)
                      ?? throw new InvalidDataException("config vacío");
            if (cfg.Version < CurrentVersion) cfg.MergeDefaults();
            return cfg;
        }
        catch (Exception ex)
        {
            Log.Write("config.json inválido, usando valores por defecto: " + ex.Message);
            MessageBox.Show(L.T("err.config", ex.Message), App.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return Defaults();
        }
    }

    void MergeDefaults()
    {
        var d = Defaults();
        static List<string> Union(List<string> mine, List<string> defaults) =>
            mine.Concat(defaults).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        ServicesToStop = Union(ServicesToStop, d.ServicesToStop);
        ProtectedServices = Union(ProtectedServices, d.ProtectedServices);
        ScheduledTasksToDisable = Union(ScheduledTasksToDisable, d.ScheduledTasksToDisable);
        ProcessesToClose = Union(ProcessesToClose, d.ProcessesToClose);
        // Un servicio que ahora es protegido no debe quedar en la lista de detener.
        ServicesToStop.RemoveAll(s => !s.EndsWith('*') && Glob.Any(ProtectedServices, s));
        Language ??= d.Language;
        Version = CurrentVersion;
        try { Save(); Log.Write($"Config actualizada a la versión {CurrentVersion}"); }
        catch (Exception ex) { Log.Write("No se pudo guardar la config migrada: " + ex.Message); }
    }

    const string DefaultJson = """
{
  // =====================================================================
  //  WindowsOptimizer - configuración
  //  Lo más cómodo es editarla desde el menú del icono > "Editar configuración...".
  //  Todo lo que se cambia se guarda antes y se restaura al volver a Tradicional.
  // =====================================================================
  "Version": 2,
  "Language": "es",   // "es" = Español, "en" = English, "zh" = 中文

  // Servicios que se DETIENEN y se ponen en "Deshabilitado" mientras dure el modo.
  // Un "*" al final es comodín (ej: "OneSyncSvc*" cubre "OneSyncSvc_1a2b3").
  "ServicesToStop": [
    // Telemetría, diagnóstico e informes de errores
    "DiagTrack", "dmwappushservice", "diagnosticshub.standardcollector.service", "diagsvc",
    "DPS", "WdiServiceHost", "WdiSystemHost", "WerSvc", "wercplsupport", "PcaSvc", "TroubleshootingSvc",
    "InventorySvc", "wuqisvc", "whesvc", "ADPSvc", "pla", "PerfHost", "wmiApSrv",
    // IA / Copilot / asistentes de Windows 11
    "WSAIFabricSvc", "AarSvc*", "MicrosoftCopilotElevationService",
    // Windows Update, Store y descargas en segundo plano
    "wuauserv", "UsoSvc", "WaaSMedicSvc", "BITS", "DoSvc", "InstallService", "PushToInstall", "TrustedInstaller",
    // Indexación, precarga, desfragmentación, copias de seguridad
    "WSearch", "SysMain", "defragsvc", "fhsvc", "SDRSVC", "wbengine", "CloudBackupRestoreSvc*", "VSS", "swprv",
    // Sincronización, contactos, mensajes, dispositivos conectados, teléfono
    "CDPSvc", "CDPUserSvc*", "OneSyncSvc*", "PimIndexMaintenanceSvc*", "UnistoreSvc*", "UserDataSvc*",
    "MessagingService*", "PhoneSvc", "SmsRouter", "TapiSrv", "WPDBusEnum", "TrkWks",
    // Mapas, ubicación, sensores, cartera, demo, Insider, control parental, huella, tinta, idioma
    "MapsBroker", "lfsvc", "SensorService", "SensrSvc", "SensorDataService", "WalletService", "SEMgrSvc",
    "RetailDemo", "wisvc", "WpcMonSvc", "WbioSrvc", "PenService*", "LxpSvc", "DsSvc",
    "DisplayEnhancementService", "ShellHWDetection", "StorSvc", "W32Time", "QWAVE", "BcastDVRUserService*",
    // Impresora, escáner, fax
    "Spooler", "PrintNotify", "PrintDeviceConfigurationService", "PrintScanBrokerService", "PrintWorkflowUserSvc*",
    "StiSvc", "WiaRpc", "Fax",
    // Red: descubrimiento, compartir, escritorio remoto de Windows, móvil, tarjetas inteligentes
    "SSDPSrv", "upnphost", "FDResPub", "fdPHost", "lmhosts", "lltdsvc", "iphlpsvc", "IpxlatCfgSvc", "SharedAccess",
    "icssvc", "ALG", "NcdAutoSetup", "WFDSConMgrSvc", "wcncsvc", "WwanSvc", "autotimesvc", "WebClient",
    "RemoteRegistry", "WinRM", "RpcLocator", "SessionEnv", "TermService", "UmRdpService", "WMPNetworkSvc",
    "AJRouter", "LanmanServer", "SNMPTrap", "MSiSCSI", "SCardSvr", "ScDeviceEnum", "SCPolicySvc", "CertPropSvc",
    // Empresa / administración remota / Hyper-V invitado (esta PC no es una máquina virtual)
    "DmEnrollmentSvc", "EntAppSvc", "workfolderssvc", "Wecsvc", "MSDTC", "AxInstSV", "embeddedmode",
    "NaturalAuthentication", "wlpasvc", "vmic*",
    // Actualizadores y ayudantes de otros programas
    "edgeupdate", "edgeupdatem", "MicrosoftEdgeElevationService",
    "gupdate", "gupdatem", "GoogleUpdater*", "GoogleChromeElevationService",
    "brave", "bravem", "BraveElevationService",
    "AsusUpdateCheck", "iCUEUpdateService", "EpicGamesUpdater", "battlenet_helpersvc", "UpcElevationService",
    "LGHUBUpdaterService", "VSInstallerElevationService", "VSStandardCollectorService*", "JetBrainsEtwHost*",
    // Programas de terceros que corren siempre sin hacer falta
    "AnyDesk", "TeamViewer", "Apple Mobile Device Service", "Bonjour Service",
    "DFWSIDService", "ElevationService", "Wondershare*", "WirelessBackupService",
    "RLHostService", "SQLWriter", "logi_lamparray_service"
    // Base de datos local: descomenta si no la usas mientras trabajas
    // , "postgresql*"
    // Xbox: descomenta SOLO si no usas Game Pass / app Xbox
    // , "XblAuthManager", "XblGameSave", "XboxNetApiSvc"
  ],

  // Servicios que NUNCA se tocan (y no aparecen en el editor), aunque algo de arriba dependa de ellos.
  "ProtectedServices": [
    // Núcleo de Windows
    "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "SamSs", "Power", "PlugPlay", "ProfSvc", "UserManager",
    "StateRepository", "AppXSvc", "CoreMessagingRegistrar", "SystemEventsBroker", "TimeBrokerSvc",
    "BrokerInfrastructure", "Schedule", "EventLog", "EventSystem", "Winmgmt", "gpsvc", "SENS", "Themes",
    "FontCache", "DispBrokerDesktopSvc", "TextInputManagementService", "UdkUserSvc*", "cbdhsvc*",
    "Appinfo", "KeyIso", "VaultSvc", "CryptSvc", "TokenBroker", "wlidsvc", "NgcSvc", "NgcCtnrSvc",
    "ClipSVC", "LicenseManager", "sppsvc", "camsvc", "msiserver", "DeviceInstall", "DevQueryBroker",
    "DeviceAssociationService", "WpnService", "WpnUserService*", "BDESVC",
    // Red e internet
    "Dhcp", "Dnscache", "nsi", "BFE", "mpssvc", "LanmanWorkstation", "WlanSvc", "Netman", "netprofm",
    "NlaSvc", "Wcmsvc", "NcbService", "WinHttpAutoProxySvc", "RasMan", "IKEEXT", "PolicyAgent", "ProtonVPN*",
    // Seguridad (Windows no deja detenerlos de todas formas)
    "WinDefend", "MDCoreSvc", "WdNisSvc", "SecurityHealthService", "wscsvc", "webthreatdef*",
    // Audio, cámara, Bluetooth, mandos, periféricos, GPU
    "Audiosrv", "AudioEndpointBuilder", "RtkAudioUniversalService", "DTSAPO3Service", "FrameServer*",
    "BthServ", "BTAGService", "BthAvctpSvc", "BluetoothUserService*", "RtkBtManServ", "RmSvc", "hidserv",
    "XboxGipSvc", "GameInput*", "AMD*", "Corsair*", "Parsec*",
    // Juegos, launchers y anti-cheats
    "GamingServices*", "Steam Client Service", "EpicOnlineServices", "EABackgroundService",
    "EasyAntiCheat*", "BEService", "vgc", "FACEIT*"
  ],

  // Tareas programadas que se deshabilitan (y se reactivan al restaurar). "*" = comodín en el nombre.
  "ScheduledTasksToDisable": [
    "\\Microsoft\\Windows\\Application Experience\\*",
    "\\Microsoft\\Windows\\Customer Experience Improvement Program\\*",
    "\\Microsoft\\Windows\\Feedback\\Siuf\\*",
    "\\Microsoft\\Windows\\Flighting\\*",
    "\\Microsoft\\Windows\\CloudExperienceHost\\*",
    "\\Microsoft\\Windows\\Autochk\\*",
    "\\Microsoft\\Windows\\Defrag\\*",
    "\\Microsoft\\Windows\\Diagnosis\\*",
    "\\Microsoft\\Windows\\DiskDiagnostic\\*",
    "\\Microsoft\\Windows\\DiskFootprint\\*",
    "\\Microsoft\\Windows\\Maintenance\\*",
    "\\Microsoft\\Windows\\Maps\\*",
    "\\Microsoft\\Windows\\Power Efficiency Diagnostics\\*",
    "\\Microsoft\\Windows\\Registry\\RegIdleBackup",
    "\\Microsoft\\Windows\\Windows Error Reporting\\*",
    "\\Microsoft\\Windows\\WindowsUpdate\\*",
    "\\Microsoft\\Windows\\InstallService\\*",
    "\\Microsoft\\Windows\\UpdateOrchestrator\\*",
    "\\MicrosoftEdgeUpdateTask*",
    "\\GoogleSystem\\GoogleUpdater\\*",
    "\\BraveSoftwareUpdateTask*"
  ],

  // Programas que se cierran (nombre del .exe SIN ".exe").
  "ProcessesToClose": [
    // Microsoft
    "OneDrive", "Widgets", "WidgetService", "PhoneExperienceHost", "YourPhone", "CrossDeviceService",
    "CrossDeviceResume", "Copilot", "ms-teams", "Teams", "GameBar", "GameBarFTServer", "SkypeApp",
    "HxTsr", "HxOutlook", "olk", "Cortana", "CompatTelRunner", "DeviceCensus", "MicrosoftEdgeUpdate",
    // Actualizadores y utilidades de fondo
    "GoogleUpdate", "BraveUpdate", "AMDInstallManager", "AppleMobileDeviceProcess", "iTunesHelper", "adb",
    // Acceso remoto (Parsec no se toca)
    "AnyDesk", "TeamViewer"
    // Añade los tuyos, por ejemplo:
    // , "chrome", "msedge", "brave", "Discord", "Spotify", "steamwebhelper", "EpicGamesLauncher"
  ],

  // De los cerrados, cuáles se vuelven a abrir al volver a Tradicional.
  "RelaunchOnRestore": [ "OneDrive", "Discord", "Spotify", "Steam" ],

  // Programas con prioridad ALTA de CPU mientras estés en Modo Trabajo (se revisa cada 5 s).
  "ProcessesToPrioritize": [ "UnrealEditor", "UE4Editor", "blender", "Houdini", "3dsmax", "maya" ],

  // Carpetas que Windows Defender no escaneará durante el modo.
  "DefenderExclusionPaths": [ ],

  "VisualEffects": true,            // sin animaciones, sombras ni transparencias
  "DisableBackgroundApps": true,    // apps de la Store en segundo plano
  "DisableNotifications": true,     // notificaciones emergentes
  "DisableDefenderRealtime": true,  // requiere "Protección contra alteraciones" desactivada en Seguridad de Windows
  "FreeMemory": true,               // vacía la RAM de procesos en segundo plano al activar
  "PurgeStandbyFull": false,        // true = vacía toda la caché de RAM (la próxima carga de proyectos será más lenta)
  "KillExplorer": false             // cierra la barra de tareas/escritorio. Ctrl+Alt+W para volver
}
""";
}
