using System.Diagnostics;
using System.ServiceProcess;

namespace WindowsOptimizer;

// Editor visual de config.json: listas de programas, servicios, carpetas y opciones.
sealed class ConfigForm : Form
{
    readonly AppConfig cfg;
    readonly CheckedListBox closeList = NewCheckedList();
    readonly ListBox priorityList = NewList();
    readonly ListBox exclusionList = NewList();
    readonly CheckedListBox serviceList = NewCheckedList();
    readonly TextBox serviceFilter = new() { Dock = DockStyle.Top, PlaceholderText = L.T("cfg.search") };
    readonly List<ServiceItem> services = new();
    readonly Dictionary<string, CheckBox> flags = new();
    bool refreshingServices;

    sealed class ServiceItem
    {
        public required string Name { get; init; }
        public required string Display { get; init; }
        public bool Running { get; init; }
        public bool Checked { get; set; }
        public override string ToString() => $"{Display}   ({Name}){(Running ? "   " + L.T("cfg.running") : "")}";
    }

    public ConfigForm(AppConfig cfg)
    {
        this.cfg = cfg;
        Text = L.T("cfg.title");
        Font = SystemFonts.MessageBoxFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(760, 600);
        MinimumSize = new Size(560, 460);
        Icon = App.Icon;
        MinimizeBox = false;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildCloseTab());
        tabs.TabPages.Add(BuildPriorityTab());
        tabs.TabPages.Add(BuildServicesTab());
        tabs.TabPages.Add(BuildOptionsTab());

        var save = new Button { Text = L.T("cfg.save"), AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = L.T("cfg.cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var advanced = new Button { Text = L.T("cfg.json"), AutoSize = true };
        advanced.Click += (_, _) =>
        {
            Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { Paths.Config }, UseShellExecute = false })?.Dispose();
            DialogResult = DialogResult.Cancel;
        };
        save.Click += (_, _) => Collect();
        AcceptButton = save;
        CancelButton = cancel;

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
        bottom.Controls.AddRange(new Control[] { cancel, save, advanced });
        bottom.Controls.Add(new Label
        {
            Text = L.T("cfg.hint"),
            AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 6, 20, 0),
        });

        Controls.Add(tabs);
        Controls.Add(bottom);
    }

    // ---------------- Pestañas ----------------

    TabPage BuildCloseTab()
    {
        foreach (var name in cfg.ProcessesToClose)
            closeList.Items.Add(name, cfg.RelaunchOnRestore.Contains(name, StringComparer.OrdinalIgnoreCase));

        return Tab(L.T("cfg.tab.close"), L.T("cfg.tab.close.desc"),
            closeList, ProgramButtons(name => AddUnique(closeList, name)));
    }

    TabPage BuildPriorityTab()
    {
        foreach (var name in cfg.ProcessesToPrioritize) priorityList.Items.Add(name);
        return Tab(L.T("cfg.tab.priority"), L.T("cfg.tab.priority.desc"),
            priorityList, ProgramButtons(name => AddUnique(priorityList, name)));
    }

    TabPage BuildServicesTab()
    {
        foreach (var sc in ServiceController.GetServices())
        {
            using (sc)
            {
                if (Glob.Any(cfg.ProtectedServices, sc.ServiceName)) continue;
                services.Add(new ServiceItem
                {
                    Name = sc.ServiceName,
                    Display = SafeDisplayName(sc),
                    Running = SafeStatus(sc) == ServiceControllerStatus.Running,
                    Checked = Glob.Any(cfg.ServicesToStop, sc.ServiceName),
                });
            }
        }
        services.Sort((a, b) => b.Checked != a.Checked ? b.Checked.CompareTo(a.Checked) : string.Compare(a.Display, b.Display, StringComparison.CurrentCultureIgnoreCase));

        serviceList.ItemCheck += (_, e) =>
        {
            if (!refreshingServices) ((ServiceItem)serviceList.Items[e.Index]).Checked = e.NewValue == CheckState.Checked;
        };
        serviceFilter.TextChanged += (_, _) => RefreshServices();
        RefreshServices();

        var onlyRunning = new Button { Text = L.T("cfg.onlyRunning"), AutoSize = true };
        onlyRunning.Click += (_, _) => { serviceFilter.Text = L.T("cfg.running"); };
        var recommended = new Button { Text = L.T("cfg.recommended"), AutoSize = true };
        recommended.Click += (_, _) =>
        {
            var defaults = AppConfig.Defaults().ServicesToStop;
            foreach (var s in services) s.Checked = Glob.Any(defaults, s.Name);
            RefreshServices();
        };
        var buttons = Row(onlyRunning, recommended, Note(L.T("cfg.services.note")));

        var page = Tab(L.T("cfg.tab.services"), L.T("cfg.tab.services.desc"), serviceList, buttons);
        page.Controls.Add(serviceFilter);
        serviceFilter.BringToFront();
        serviceList.BringToFront();
        return page;
    }

    TabPage BuildOptionsTab()
    {
        foreach (var p in cfg.DefenderExclusionPaths) exclusionList.Items.Add(p);

        var addFolder = new Button { Text = L.T("cfg.addFolder"), AutoSize = true };
        addFolder.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = L.T("cfg.folderDesc") };
            if (dlg.ShowDialog(this) == DialogResult.OK) AddUnique(exclusionList, dlg.SelectedPath);
        };

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Padding = new Padding(4) };
        AddFlag(options, nameof(AppConfig.VisualEffects), "cfg.opt.visual", cfg.VisualEffects);
        AddFlag(options, nameof(AppConfig.DisableBackgroundApps), "cfg.opt.background", cfg.DisableBackgroundApps);
        AddFlag(options, nameof(AppConfig.DisableNotifications), "cfg.opt.notifications", cfg.DisableNotifications);
        AddFlag(options, nameof(AppConfig.DisableDefenderRealtime), "cfg.opt.defender", cfg.DisableDefenderRealtime);
        AddFlag(options, nameof(AppConfig.FreeMemory), "cfg.opt.freeRam", cfg.FreeMemory);
        AddFlag(options, nameof(AppConfig.PurgeStandbyFull), "cfg.opt.purge", cfg.PurgeStandbyFull);
        AddFlag(options, nameof(AppConfig.KillExplorer), "cfg.opt.explorer", cfg.KillExplorer);
        options.Controls.Add(new Label { Text = L.T("cfg.exclusions"), AutoSize = true, Padding = new Padding(0, 12, 0, 0) });

        var page = Tab(L.T("cfg.tab.options"), "", exclusionList, Row(addFolder, RemoveButton(exclusionList)));
        page.Controls.Add(options);
        exclusionList.BringToFront();
        return page;
    }

    // ---------------- Guardar ----------------

    void Collect()
    {
        cfg.ProcessesToClose = closeList.Items.Cast<string>().ToList();
        // Se conservan entradas de "reabrir" de programas que no están en la lista de cierre (no hacen daño).
        cfg.RelaunchOnRestore = closeList.CheckedItems.Cast<string>()
            .Concat(cfg.RelaunchOnRestore.Where(r => !cfg.ProcessesToClose.Contains(r, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        cfg.ProcessesToPrioritize = priorityList.Items.Cast<string>().ToList();
        cfg.DefenderExclusionPaths = exclusionList.Items.Cast<string>().ToList();
        cfg.ServicesToStop = CollectServices();

        cfg.VisualEffects = flags[nameof(AppConfig.VisualEffects)].Checked;
        cfg.DisableBackgroundApps = flags[nameof(AppConfig.DisableBackgroundApps)].Checked;
        cfg.DisableNotifications = flags[nameof(AppConfig.DisableNotifications)].Checked;
        cfg.DisableDefenderRealtime = flags[nameof(AppConfig.DisableDefenderRealtime)].Checked;
        cfg.FreeMemory = flags[nameof(AppConfig.FreeMemory)].Checked;
        cfg.PurgeStandbyFull = flags[nameof(AppConfig.PurgeStandbyFull)].Checked;
        cfg.KillExplorer = flags[nameof(AppConfig.KillExplorer)].Checked;
        cfg.Save();
    }

    List<string> CollectServices()
    {
        var installed = services.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        // Comodines: se mantienen si todos sus servicios siguen marcados; si no, se expanden a los marcados.
        foreach (var pattern in cfg.ServicesToStop.Where(p => p.EndsWith('*')))
        {
            var matched = services.Where(s => Glob.Match(pattern, s.Name)).ToList();
            if (matched.All(s => s.Checked)) result.Add(pattern);
            else result.AddRange(matched.Where(s => s.Checked).Select(s => s.Name));
        }
        // "Marcar recomendados" puede traer comodines nuevos que no estaban en la config.
        foreach (var pattern in AppConfig.Defaults().ServicesToStop.Where(p => p.EndsWith('*') && !result.Contains(p)))
        {
            var matched = services.Where(s => Glob.Match(pattern, s.Name)).ToList();
            if (matched.Count > 0 && matched.All(s => s.Checked)) result.Add(pattern);
        }
        // Entradas que no son servicios instalados en esta PC (o protegidos): se conservan tal cual.
        result.AddRange(cfg.ServicesToStop.Where(p => !p.EndsWith('*') && !installed.ContainsKey(p)));
        result.AddRange(services.Where(s => s.Checked && !Glob.Any(result, s.Name)).Select(s => s.Name));
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ---------------- Helpers de UI ----------------

    void RefreshServices()
    {
        refreshingServices = true;
        serviceList.BeginUpdate();
        serviceList.Items.Clear();
        var f = serviceFilter.Text.Trim();
        foreach (var s in services.Where(s => f.Length == 0 || s.ToString().Contains(f, StringComparison.OrdinalIgnoreCase)))
            serviceList.Items.Add(s, s.Checked);
        serviceList.EndUpdate();
        refreshingServices = false;
    }

    FlowLayoutPanel ProgramButtons(Action<string> add)
    {
        var running = new Button { Text = L.T("cfg.addRunning"), AutoSize = true };
        running.Click += (_, _) =>
        {
            using var picker = new ProcessPicker();
            if (picker.ShowDialog(this) == DialogResult.OK) foreach (var n in picker.Selected) add(n);
        };

        var browse = new Button { Text = L.T("cfg.browse"), AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = L.T("cfg.exeFilter"), Multiselect = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                foreach (var file in dlg.FileNames) add(Path.GetFileNameWithoutExtension(file));
        };

        var manual = new TextBox { Width = 150, PlaceholderText = "app.exe" };
        var addManual = new Button { Text = L.T("cfg.add"), AutoSize = true };
        addManual.Click += (_, _) =>
        {
            var n = manual.Text.Trim();
            if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n[..^4];
            if (n.Length > 0) add(n);
            manual.Clear();
        };
        manual.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { addManual.PerformClick(); e.SuppressKeyPress = true; } };

        return Row(running, browse, manual, addManual);
    }

    static Button RemoveButton(ListBox list)
    {
        var b = new Button { Text = L.T("cfg.remove"), AutoSize = true, Tag = "remove" };
        b.Click += (_, _) =>
        {
            foreach (var item in list.SelectedItems.Cast<object>().ToList()) list.Items.Remove(item);
        };
        return b;
    }

    TabPage Tab(string title, string description, ListBox list, FlowLayoutPanel buttons)
    {
        if (list != serviceList && !buttons.Controls.OfType<Button>().Any(b => "remove".Equals(b.Tag)))
            buttons.Controls.Add(RemoveButton(list));

        var page = new TabPage(title) { Padding = new Padding(8), UseVisualStyleBackColor = true };
        page.Controls.Add(list);
        page.Controls.Add(buttons);
        if (description.Length > 0)
            page.Controls.Add(new Label { Text = description, Dock = DockStyle.Top, AutoSize = false, Height = 40, ForeColor = SystemColors.GrayText });
        list.BringToFront();
        return page;
    }

    void AddFlag(Control parent, string key, string textKey, bool value)
    {
        var cb = new CheckBox { Text = L.T(textKey), Checked = value, AutoSize = true };
        flags[key] = cb;
        parent.Controls.Add(cb);
    }

    static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        row.Controls.AddRange(controls);
        return row;
    }

    static Label Note(string text) =>
        new() { Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(6, 7, 0, 0) };

    static void AddUnique(ListBox list, string value)
    {
        if (list.Items.Cast<string>().Any(i => i.Equals(value, StringComparison.OrdinalIgnoreCase))) return;
        list.Items.Add(value);
    }

    static CheckedListBox NewCheckedList() => new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
    static ListBox NewList() => new() { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, IntegralHeight = false };

    static string SafeDisplayName(ServiceController sc) { try { return sc.DisplayName; } catch { return sc.ServiceName; } }
    static ServiceControllerStatus SafeStatus(ServiceController sc) { try { return sc.Status; } catch { return ServiceControllerStatus.Stopped; } }
}

// Lista de programas abiertos ahora mismo, para elegirlos con un clic.
sealed class ProcessPicker : Form
{
    readonly ListView view = new()
    {
        Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true,
    };
    public List<string> Selected { get; } = new();

    public ProcessPicker()
    {
        Text = L.T("pick.title");
        Font = SystemFonts.MessageBoxFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(620, 520);
        Icon = App.Icon;
        MinimizeBox = false;

        view.Columns.Add(L.T("pick.program"), 170);
        view.Columns.Add(L.T("pick.window"), 300);
        view.Columns.Add("RAM", 80, HorizontalAlignment.Right);

        int session = Process.GetCurrentProcess().SessionId;
        var procs = Process.GetProcesses();
        var groups = procs
            .Where(p => SafeSession(p) == session)
            .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Name = g.Key,
                Title = g.Select(SafeTitle).FirstOrDefault(t => t.Length > 0) ?? "",
                Path = g.Select(SafePath).FirstOrDefault(t => t.Length > 0) ?? "",
                Ram = g.Sum(SafeRam),
            })
            .Where(g => g.Path.Length > 0 && !g.Path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(g => g.Ram)
            .ToList();
        foreach (var p in procs) p.Dispose();

        foreach (var g in groups)
        {
            var item = new ListViewItem(g.Name);
            item.SubItems.Add(g.Title.Length > 0 ? g.Title : g.Path);
            item.SubItems.Add($"{g.Ram / 1048576.0:0} MB");
            view.Items.Add(item);
        }

        var ok = new Button { Text = L.T("pick.ok"), AutoSize = true, DialogResult = DialogResult.OK };
        ok.Click += (_, _) => Selected.AddRange(view.CheckedItems.Cast<ListViewItem>().Select(i => i.Text));
        var cancel = new Button { Text = L.T("cfg.cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
        bottom.Controls.AddRange(new Control[] { cancel, ok });

        Controls.Add(view);
        Controls.Add(bottom);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    static int SafeSession(Process p) { try { return p.SessionId; } catch { return -1; } }
    static string SafeTitle(Process p) { try { return p.MainWindowTitle; } catch { return ""; } }
    static string SafePath(Process p) { try { return p.MainModule?.FileName ?? ""; } catch { return ""; } }
    static long SafeRam(Process p) { try { return p.WorkingSet64; } catch { return 0; } }
}
