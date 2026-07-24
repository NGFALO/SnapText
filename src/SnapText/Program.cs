using System.Runtime.InteropServices;

namespace SnapText;

static class Program
{
    public static int ShowMeMessage { get; private set; }

    [STAThread]
    static void Main()
    {
        ShowMeMessage = Native.RegisterWindowMessage("SnapText.ShowMe");

        using var mutex = new Mutex(true, "SnapText_SingleInstance_Mutex", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // tell the running instance to show its window, then quit
            Native.PostMessage(Native.HWND_BROADCAST, ShowMeMessage, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        ApplicationConfiguration.Initialize();
        Ui.SetScale((int)Native.GetDpiForSystem());
        Application.Run(new SnapTextContext());
    }
}

/// <summary>Tray icon + global hotkey + capture orchestration.</summary>
sealed class SnapTextContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly MainForm _mainForm;
    private readonly NotifyIcon _tray;
    private readonly HotkeyService _hotkey;
    private readonly ToolStripMenuItem _captureItem, _enabledItem;
    private readonly ContextMenuStrip _menu;
    private bool _capturing;
    private bool _exiting;

    public SnapTextContext()
    {
        _settings = AppSettings.Load();
        bool firstRun = _settings.IsFirstRun;
        if (firstRun) { _settings.IsFirstRun = false; _settings.Save(); }

        _mainForm = new MainForm(_settings);
        _mainForm.CaptureRequested += () => _ = CaptureAsync();
        _mainForm.HotkeySettingsChanged += OnHotkeySettingsChanged;
        _mainForm.ThemeChanged += ApplyTrayTheme;
        _mainForm.FormClosing += (s, e) =>
        {
            if (!_exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _mainForm.Hide();
            }
        };
        // make sure a handle exists so hotkeys + the show-me broadcast work while hidden
        _ = _mainForm.Handle;

        // ── tray ──
        var theme = Theme.Resolve(_settings.Theme);
        _tray = new NotifyIcon
        {
            Icon = AppIcon.CreateTray(theme.Accent),
            Text = "SnapText — screen text capture",
            Visible = true,
        };

        _menu = new ContextMenuStrip { ShowImageMargin = false };

        _captureItem = new ToolStripMenuItem("Capture now");
        _captureItem.Font = new Font(_captureItem.Font, FontStyle.Bold);
        _captureItem.Click += (s, e) => _ = CaptureAsync();

        var historyItem = new ToolStripMenuItem("Open history");
        historyItem.Click += (s, e) => { ShowMain(); _mainForm.ShowPage(0); };

        var settingsItem = new ToolStripMenuItem("Settings");
        settingsItem.Click += (s, e) => { ShowMain(); _mainForm.ShowPage(1); };

        _enabledItem = new ToolStripMenuItem("Hotkey enabled") { CheckOnClick = true, Checked = _settings.Enabled };
        _enabledItem.CheckedChanged += (s, e) =>
        {
            _settings.Enabled = _enabledItem.Checked;
            _settings.Save();
            UpdateHotkey();
            _mainForm.SyncFromSettings();
        };

        var quitItem = new ToolStripMenuItem("Quit SnapText");
        quitItem.Click += (s, e) => ExitApp();

        _menu.Items.Add(_captureItem);
        _menu.Items.Add(historyItem);
        _menu.Items.Add(settingsItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_enabledItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(quitItem);
        _tray.ContextMenuStrip = _menu;
        _tray.DoubleClick += (s, e) => ShowMain();
        ApplyTrayTheme();

        // ── hotkey ──
        _hotkey = new HotkeyService(_mainForm);
        _hotkey.Triggered += () => _ = CaptureAsync();
        UpdateHotkey();

        if (firstRun) ShowMain();
    }

    private void OnHotkeySettingsChanged()
    {
        UpdateHotkey();
        _enabledItem.Checked = _settings.Enabled;
        _captureItem.ShortcutKeyDisplayString = _settings.HotkeyText;
    }

    private void UpdateHotkey()
    {
        _hotkey.Unregister();
        if (_settings.Enabled)
        {
            if (!_hotkey.Register(_settings.HotkeyModifiers, _settings.HotkeyKey))
                _tray.ShowBalloonTip(2500, "SnapText",
                    $"Could not register hotkey {_settings.HotkeyText} — it may be in use by another app.",
                    ToolTipIcon.Warning);
        }
        _captureItem.ShortcutKeyDisplayString = _settings.HotkeyText;
    }

    private void ApplyTrayTheme()
    {
        var t = Theme.Resolve(_settings.Theme);
        _menu.Renderer = new ThemedMenuRenderer(t);
        _menu.BackColor = t.Surface;
        foreach (ToolStripItem item in _menu.Items)
            item.ForeColor = t.Text;
    }

    private void ShowMain()
    {
        _mainForm.Show();
        if (_mainForm.WindowState == FormWindowState.Minimized)
            _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.BringToFront();
        _mainForm.Activate();
    }

    private async Task CaptureAsync()
    {
        if (_capturing) return;
        _capturing = true;
        bool wasVisible = _mainForm.Visible;
        try
        {
            if (wasVisible)
            {
                _mainForm.Hide();
                await Task.Delay(200); // let the desktop repaint before freezing it
            }

            var result = await CaptureService.CaptureAsync(_settings);
            if (result == null) return;

            _mainForm.AddHistory(result.Entry);
            if (_settings.ShowNotification)
            {
                string msg = result.HasText
                    ? $"{result.Entry.Text.Length} characters{(_settings.CopyToClipboard ? " · copied to clipboard" : "")}"
                    : "No text detected in the selection.";
                _tray.ShowBalloonTip(1800, result.HasText ? "Text extracted" : "SnapText", msg, ToolTipIcon.None);
            }
        }
        finally
        {
            if (wasVisible) ShowMain();
            _capturing = false;
        }
    }

    private void ExitApp()
    {
        _exiting = true;
        _hotkey.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkey.Dispose();
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>Global hotkey via RegisterHotKey + a message filter for WM_HOTKEY.</summary>
sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xB00F;
    private const uint MOD_NOREPEAT = 0x4000;
    private readonly Form _window;
    private bool _registered;

    public event Action? Triggered;

    public HotkeyService(Form window)
    {
        _window = window;
        Application.AddMessageFilter(new Filter(this));
    }

    public bool Register(uint modifiers, Keys key)
    {
        Unregister();
        _registered = Native.RegisterHotKey(_window.Handle, HotkeyId, modifiers | MOD_NOREPEAT, (uint)key);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        Native.UnregisterHotKey(_window.Handle, HotkeyId);
        _registered = false;
    }

    public void Dispose() => Unregister();

    private sealed class Filter : IMessageFilter
    {
        private readonly HotkeyService _svc;
        public Filter(HotkeyService svc) => _svc = svc;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                _svc.Triggered?.Invoke();
                return true;
            }
            return false;
        }
    }
}

/// <summary>Tray context-menu renderer matching the app theme.</summary>
sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly Theme _t;

    public ThemedMenuRenderer(Theme t) : base(new ThemedColorTable(t)) => _t = t;

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _t.Text : _t.TextDim;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rect = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
        using var b = new SolidBrush(e.Item.Selected ? _t.SurfaceAlt : _t.Surface);
        e.Graphics.FillRectangle(b, e.Item.Selected ? rect : new Rectangle(Point.Empty, e.Item.Size));
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(_t.Border);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = new Rectangle(e.ImageRectangle.X, e.ImageRectangle.Y, 16, 16);
        UiKit.DrawIcon(e.Graphics, "check", r, _t.Accent, 2.2f);
    }

    private sealed class ThemedColorTable : ProfessionalColorTable
    {
        private readonly Theme _t;
        public ThemedColorTable(Theme t) => _t = t;
        public override Color MenuBorder => _t.BorderStrong;
        public override Color ToolStripDropDownBackground => _t.Surface;
        public override Color ImageMarginGradientBegin => _t.Surface;
        public override Color ImageMarginGradientMiddle => _t.Surface;
        public override Color ImageMarginGradientEnd => _t.Surface;
        public override Color MenuItemSelected => _t.SurfaceAlt;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelectedGradientBegin => _t.SurfaceAlt;
        public override Color MenuItemSelectedGradientEnd => _t.SurfaceAlt;
        public override Color SeparatorDark => _t.Border;
    }
}

static class Native
{
    public const int WM_HOTKEY = 0x0312;
    public static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterWindowMessage(string message);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
}
