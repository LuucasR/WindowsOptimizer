using System.ServiceProcess;

namespace WindowsOptimizer.Modules;

static class Services
{
    const int Disabled = 4;
    static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    public static string Apply(AppConfig cfg, SavedState s)
    {
        var all = ServiceController.GetServices();
        var backups = new Dictionary<string, ServiceBackup>(StringComparer.OrdinalIgnoreCase);
        var toStop = new List<ServiceController>();

        // Paso 1: decidir y respaldar (antes de tocar nada).
        foreach (var sc in all)
        {
            var name = sc.ServiceName;
            if (!Glob.Any(cfg.ServicesToStop, name) || Glob.Any(cfg.ProtectedServices, name)) continue;
            try
            {
                bool running = sc.Status != ServiceControllerStatus.Stopped;
                if (running)
                {
                    // Detener un servicio detiene sus dependientes: hay que respetarlos y respaldarlos también.
                    var deps = sc.DependentServices.Where(d => d.Status != ServiceControllerStatus.Stopped).ToList();
                    var blocked = deps.FirstOrDefault(d => Glob.Any(cfg.ProtectedServices, d.ServiceName));
                    if (blocked != null)
                    {
                        Log.Write($"Servicio {name} omitido: {blocked.ServiceName} (protegido) depende de él");
                        continue;
                    }
                    foreach (var d in deps)
                        backups.TryAdd(d.ServiceName, new ServiceBackup(d.ServiceName, null, true));
                    toStop.Add(sc);
                }
                backups[name] = new ServiceBackup(name, (int)sc.StartType, running);
            }
            catch (Exception ex) { Log.Write($"Servicio {name}: {ex.Message}"); }
        }

        s.Services = backups.Values.ToList();
        StateStore.Save(s);

        // Paso 2: deshabilitar inicio y detener en paralelo.
        foreach (var b in s.Services.Where(b => b.StartType is int st && st != Disabled))
        {
            try { Native.SetServiceStartType(b.Name, Disabled); }
            catch (Exception ex) { Log.Write($"No se pudo deshabilitar {b.Name}: {ex.Message}"); }
        }

        int stopped = 0;
        Parallel.ForEach(toStop, sc =>
        {
            try
            {
                sc.Refresh();
                if (sc.Status == ServiceControllerStatus.Stopped) return;
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, Wait);
                Interlocked.Increment(ref stopped);
            }
            catch (Exception ex) { Log.Write($"No se pudo detener {sc.ServiceName}: {ex.InnerException?.Message ?? ex.Message}"); }
        });

        return L.T("sum.services", stopped);
    }

    public static void Restore(SavedState s)
    {
        foreach (var b in s.Services)
        {
            if (b.StartType is not int st) continue;
            try { Native.SetServiceStartType(b.Name, st); }
            catch (Exception ex) { Log.Write($"No se pudo restaurar inicio de {b.Name}: {ex.Message}"); }
        }

        Parallel.ForEach(s.Services.Where(b => b.WasRunning), b =>
        {
            try
            {
                using var sc = new ServiceController(b.Name);
                if (sc.Status != ServiceControllerStatus.Stopped) return;
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, Wait);
            }
            catch (Exception ex) { Log.Write($"No se pudo iniciar {b.Name}: {ex.InnerException?.Message ?? ex.Message}"); }
        });
    }
}
