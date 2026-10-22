using System.Diagnostics;
using System.Drawing.Drawing2D;
using WindowsOptimizer.Modules;

namespace WindowsOptimizer;

sealed class TrayApp : ApplicationContext
{
    readonly NotifyIcon tray;
    readonly ToolStripMenuItem miTraditional, miWork, miReapply, miFreeRam, miSettings, miLog, miAutostart, miLanguage, miExit;
    readonly Icon traditionalIcon = MakeIcon(Color.FromArgb(46, 160, 67), bolt: false);
    readonly Icon workIcon = MakeIcon(Color.FromArgb(240, 130, 20), bolt: true);
    readonly System.Windows.Forms.Timer boostTimer = new() { Interval = 5000 };
    readonly HotkeyWindow hotkey = new();
    readonly HashSet<int> boosted = new();
    AppConfig cfg = AppConfig.Load();
    SavedState? state;
    bool busy;
    DateTime lastTooltip;

    bool IsWork => state != null;

    public TrayApp()
    {
        L.Set(cfg.Language);
        miTraditional = new ToolStripMenuItem("", null, async (_, _) => await SetWorkAsync(false));
        miWork = new ToolStripMenuItem("", null, async (_, _) => await SetWorkAsync(true));
        miReapply = new ToolStripMenuItem("", null, async (_, _) => await ReapplyAsync());
        miFreeRam = new ToolStripMenuItem("", null, async (_, _) => await FreeRamAsync());
        miSettings = new ToolStripMenuItem("", null, (_, _) => EditConfig());
        miLog = new ToolStripMenuItem("", null, (_, _) => OpenFile(Paths.LogFile));
        miAutostart = new ToolStripMenuItem("", null, (_, _) => ToggleAutostart());
        miLanguage = new ToolStripMenuItem("");
        foreach (var (code, name) in L.Languages)
            miLanguage.DropDownItems.Add(new ToolStripMenuItem(name, null, (_, _) => ChangeLanguage(code)) { Tag = code });
        miExit = new ToolStripMenuItem("", null, async (_, _) => await ExitAsync());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            miTraditional, miWork, miReapply,
            new ToolStripSeparator(),
            miFreeRam, miSettings, miLog, miAutostart, miLanguage,
            new ToolStripSeparator(),
            miExit,
        });
        menu.Opening += (_, _) => miAutostart.Checked = Autostart.IsEnabled();
        ApplyTexts();

        tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += async (_, _) => await SetWorkAsync(!IsWork);
        tray.MouseMove += (_, _) => RefreshTooltip();

        boostTimer.Tick += (_, _) => Processes.BoostPriorities(cfg, boosted);
        hotkey.Pressed += async () => await SetWorkAsync(!IsWork);

        Task.Run(Autostart.MigrateLegacy);
        RecoverPreviousSession();
        UpdateUi();
    }

    void ApplyTexts()
    {
        miTraditional.Text = L.T("mode.traditional");
        miWork.Text = L.T("mode.work");
        miReapply.Text = L.T("menu.reapply");
        miFreeRam.Text = L.T("menu.freeRam");
        miSettings.Text = L.T("menu.settings");
        miLog.Text = L.T("menu.log");
        miAutostart.Text = L.T("menu.autostart");
        miLanguage.Text = L.T("menu.language") + " / Language / 语言";
        miExit.Text = L.T("menu.exit");
        foreach (ToolStripMenuItem item in miLanguage.DropDownItems)
            item.Checked = (string)item.Tag! == L.Lang;
    }

    void ChangeLanguage(string code)
    {
        L.Set(code);
        try
        {
            var c = AppConfig.Load();
            c.Language = code;
            c.Save();
            cfg = c;
        }
        catch (Exception ex) { Log.Write("No se pudo guardar el idioma: " + ex.Message); }
        ApplyTexts();
        UpdateUi();
    }

    // Si la app se cerró o la PC se reinició estando en Modo Trabajo, el snapshot sigue en disco.
    void RecoverPreviousSession()
    {
        if (!StateStore.Exists) return;
        var previous = StateStore.Load();
        if (previous == null) { StateStore.Delete(); return; }

        var answer = MessageBox.Show(
            L.T("ask.recover", previous.AppliedAt.ToString("g")),
            App.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        state = previous;
        if (answer == DialogResult.Yes) _ = SetWorkAsync(false);
        else boostTimer.Start();
    }

    async Task SetWorkAsync(bool on)
    {
        if (busy || on == IsWork) return;
        busy = true;
        UpdateUi();
        try
        {
            string summary;
            if (on)
            {
                cfg = AppConfig.Load();
                var c = cfg;
                (state, summary) = await Task.Run(() => (Optimizer.Apply(c, out var sm), sm));
                Processes.BoostPriorities(cfg, boosted);
                boostTimer.Start();
            }
            else
            {
                boostTimer.Stop();
                var s = state!;
                summary = await Task.Run(() => Optimizer.Restore(s));
                Processes.ResetPriorities(boosted);
                state = null;
            }
            tray.ShowBalloonTip(6000, on ? L.T("balloon.workOn") : L.T("mode.traditional"), Truncate(summary, 250), ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Log.Write("Cambio de modo ERROR: " + ex);
            tray.ShowBalloonTip(6000, App.Name, L.T("error", ex.Message), ToolTipIcon.Error);
        }
        finally
        {
            busy = false;
            UpdateUi();
            TrimSelf();
        }
    }

    // Restaura todo y vuelve a activar el Modo Trabajo leyendo la config actual.
    async Task ReapplyAsync()
    {
        if (busy || !IsWork) return;
        await SetWorkAsync(false);
        if (!IsWork) await SetWorkAsync(true);
    }

    async Task FreeRamAsync()
    {
        if (busy) return;
        var c = cfg;
        var r = await Task.Run(() => Memory.Free(c));
        tray.ShowBalloonTip(4000, L.T("balloon.memory"), r, ToolTipIcon.Info);
    }

    ConfigForm? configForm;

    async void EditConfig()
    {
        if (configForm != null) { configForm.Activate(); return; }
        bool saved;
        using (configForm = new ConfigForm(AppConfig.Load()))
        {
            saved = configForm.ShowDialog() == DialogResult.OK;
        }
        configForm = null;
        if (saved)
        {
            cfg = AppConfig.Load();
            if (IsWork && MessageBox.Show(L.T("ask.reapply"),
                    App.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                await ReapplyAsync();
        }
        TrimSelf();
    }

    void ToggleAutostart()
    {
        try
        {
            if (Autostart.IsEnabled()) Autostart.Disable(); else Autostart.Enable();
        }
        catch (Exception ex)
        {
            MessageBox.Show(L.T("err.autostart", ex.Message), App.Name);
        }
    }

    async Task ExitAsync()
    {
        if (busy) return;
        if (IsWork)
        {
            var r = MessageBox.Show(L.T("ask.exit"),
                App.Name, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) return;
            if (r == DialogResult.Yes) await SetWorkAsync(false);
        }
        hotkey.Dispose();
        tray.Visible = false;
        tray.Dispose();
        ExitThread();
    }

    void UpdateUi()
    {
        miTraditional.Checked = !IsWork;
        miWork.Checked = IsWork;
        miTraditional.Enabled = miWork.Enabled = miFreeRam.Enabled = !busy;
        miReapply.Enabled = IsWork && !busy;
        tray.Icon = IsWork ? workIcon : traditionalIcon;
        lastTooltip = DateTime.MinValue;
        RefreshTooltip();
    }

    void RefreshTooltip()
    {
        if ((DateTime.Now - lastTooltip).TotalSeconds < 2) return;
        lastTooltip = DateTime.Now;
        tray.Text = busy
            ? L.T("tip.busy")
            : L.T("tip.status", L.T(IsWork ? "mode.work.short" : "mode.traditional.short"), Native.AvailableRamGb());
    }

    static void OpenFile(string path)
    {
        if (!File.Exists(path)) File.WriteAllText(path, "");
        Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { path }, UseShellExecute = false })?.Dispose();
    }

    static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    static void TrimSelf()
    {
        GC.Collect();
        using var me = Process.GetCurrentProcess();
        Native.EmptyWorkingSet(me.Handle);
    }

    static Icon MakeIcon(Color color, bool bolt)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var fill = new SolidBrush(color);
            g.FillEllipse(fill, 1, 1, 30, 30);
            if (bolt)
            {
                PointF[] p = { new(18, 4), new(8, 18), new(15, 18), new(12, 28), new(24, 13), new(17, 13), new(20, 4) };
                g.FillPolygon(Brushes.White, p);
            }
            else
            {
                using var pen = new Pen(Color.White, 3.5f);
                g.DrawEllipse(pen, 10, 10, 12, 12);
            }
        }
        var h = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(h).Clone();
        Native.DestroyIcon(h);
        return icon;
    }
}

// Ventana invisible solo para recibir el atajo global Ctrl+Alt+W.
sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int WM_HOTKEY = 0x0312, Id = 1;
    public event Action? Pressed;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams { Parent = (IntPtr)(-3) /* HWND_MESSAGE */ });
        if (!Native.RegisterHotKey(Handle, Id, 0x0001 | 0x0002 | 0x4000 /* ALT|CTRL|NOREPEAT */, (uint)Keys.W))
            Log.Write("No se pudo registrar Ctrl+Alt+W (otra app lo usa)");
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY) Pressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Native.UnregisterHotKey(Handle, Id);
        DestroyHandle();
    }
}
