using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SnapText;

public sealed class MainForm : Form
{
    public const string AppTitle = "SnapText";
    public const string Version = "1.0.0";

    private readonly AppSettings _settings;
    private readonly List<HistoryEntry> _history = new();
    private Theme T;

    public event Action? CaptureRequested;
    public event Action? HotkeySettingsChanged;
    public event Action? ThemeChanged;

    // themed controls re-styled on theme switch
    private readonly List<Action<Theme>> _themed = new();
    // actions that re-read settings into UI state (used when the tray changes them)
    private readonly List<Action> _sync = new();

    // sidebar
    private Panel _sidebar = null!;
    private NiceButton _captureBtn = null!;
    private NavButton _navHistory = null!, _navSettings = null!;
    private Label _libraryLabel = null!;
    private StatusFooter _footer = null!;

    // pages
    private Panel _content = null!;
    private Panel _historyPage = null!, _settingsPage = null!;
    private Label _historyTitle = null!, _historyCount = null!;
    private SearchBox _search = null!;
    private NiceButton _clearAllBtn = null!;
    private Panel _historyScroll = null!;
    private FlowLayoutPanel _historyList = null!;
    private EmptyState _empty = null!;
    private HotkeyBox _hotkeyBox = null!;

    public MainForm(AppSettings settings)
    {
        _settings = settings;
        T = Theme.Resolve(settings.Theme);

        Text = AppTitle;
        Size = new Size(Ui.S(940), Ui.S(640));
        MinimumSize = new Size(Ui.S(760), Ui.S(500));
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.FontUi;
        DoubleBuffered = true;
        Icon = AppIcon.Create(T.Accent);

        BuildSidebar();
        BuildHistoryPage();
        BuildSettingsPage();
        ShowPage(0);
        ApplyTheme();

        foreach (var entry in HistoryStore.LoadAll())
            AddHistoryCard(entry, append: true);
        RefreshHistoryUi();
    }

    // ─── layout ────────────────────────────────────────────────

    private void BuildSidebar()
    {
        _content = new Panel { Dock = DockStyle.Fill };
        _sidebar = new Panel { Dock = DockStyle.Left, Width = Ui.S(200) };
        _sidebar.Paint += (s, e) =>
        {
            using var pen = new Pen(T.Border);
            e.Graphics.DrawLine(pen, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
        };
        Controls.Add(_content);
        Controls.Add(_sidebar);

        _captureBtn = new NiceButton
        {
            Text = "New capture",
            IconName = "capture",
            Style = NiceButton.Kind.Accent,
            BadgeText = _settings.HotkeyText,
            Height = Ui.S(36),
            Radius = 8,
        };
        _captureBtn.Click += (s, e) => CaptureRequested?.Invoke();
        _sidebar.Controls.Add(_captureBtn);

        _libraryLabel = new Label
        {
            Text = "LIBRARY",
            Font = Theme.FontSection,
            AutoSize = true,
        };
        _sidebar.Controls.Add(_libraryLabel);

        _navHistory = new NavButton { Text = "History", IconName = "clock" };
        _navHistory.Click += (s, e) => ShowPage(0);
        _sidebar.Controls.Add(_navHistory);

        _navSettings = new NavButton { Text = "Settings", IconName = "settings" };
        _navSettings.Click += (s, e) => ShowPage(1);
        _sidebar.Controls.Add(_navSettings);

        _footer = new StatusFooter(_settings);
        _sidebar.Controls.Add(_footer);

        _sidebar.Layout += (s, e) => LayoutSidebar();
        _themed.Add(t =>
        {
            _sidebar.BackColor = t.Sidebar;
            _captureBtn.ApplyTheme(t);
            _libraryLabel.ForeColor = t.TextDim;
            _libraryLabel.BackColor = t.Sidebar;
            _navHistory.ApplyTheme(t);
            _navSettings.ApplyTheme(t);
            _footer.ApplyTheme(t);
        });
    }

    private void LayoutSidebar()
    {
        int pad = Ui.S(10);
        int w = _sidebar.ClientSize.Width - pad * 2;
        int y = Ui.S(14);
        _captureBtn.SetBounds(pad, y, w, Ui.S(36)); y += Ui.S(36 + 18);
        _libraryLabel.Location = new Point(Ui.S(20), y); y += _libraryLabel.Height + Ui.S(6);
        _navHistory.SetBounds(pad, y, w, Ui.S(32));
        _footer.SetBounds(pad, _sidebar.ClientSize.Height - Ui.S(66), w, Ui.S(56));
        _navSettings.SetBounds(pad, _footer.Top - Ui.S(38), w, Ui.S(32));
    }

    private void BuildHistoryPage()
    {
        _historyPage = new Panel { Dock = DockStyle.Fill };
        _content.Controls.Add(_historyPage);

        var header = new Panel { Dock = DockStyle.Top, Height = Ui.S(66) };
        _historyPage.Controls.Add(header);
        header.Paint += (s, e) =>
        {
            using var pen = new Pen(T.Border);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        _historyTitle = new Label { Text = "History", Font = Theme.FontTitle, AutoSize = true, Location = new Point(Ui.S(24), Ui.S(12)) };
        _historyCount = new Label { Text = "0 captures · stored locally", Font = Theme.FontUiSmall, AutoSize = true, Location = new Point(Ui.S(25), Ui.S(38)) };
        header.Controls.Add(_historyTitle);
        header.Controls.Add(_historyCount);

        _search = new SearchBox();
        _search.Input.PlaceholderText = "Search captures…";
        _search.Input.TextChanged += (s, e) => RefreshHistoryUi();
        header.Controls.Add(_search);

        _clearAllBtn = new NiceButton
        {
            Text = "Clear all",
            IconName = "trash",
            Style = NiceButton.Kind.Outline,
            DangerHover = true,
            Size = new Size(Ui.S(96), Ui.S(30)),
        };
        _clearAllBtn.Click += (s, e) => ClearAll();
        header.Controls.Add(_clearAllBtn);

        header.Layout += (s, e) =>
        {
            _clearAllBtn.Location = new Point(header.ClientSize.Width - Ui.S(24) - _clearAllBtn.Width, Ui.S(18));
            _search.Location = new Point(_clearAllBtn.Left - Ui.S(10) - _search.Width, Ui.S(18));
        };

        _historyScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _historyList = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(Ui.S(24), Ui.S(12), Ui.S(24), Ui.S(20)),
            Location = new Point(0, 0),
        };
        _historyScroll.Controls.Add(_historyList);
        _historyPage.Controls.Add(_historyScroll);
        _historyScroll.BringToFront();

        _empty = new EmptyState(_settings) { Visible = false };
        _historyScroll.Controls.Add(_empty);
        // Layout fires on every pass (including the initial dock), unlike SizeChanged
        _historyScroll.Layout += (s, e) => { ResizeCards(); CenterEmpty(); };

        _themed.Add(t =>
        {
            _historyPage.BackColor = t.Bg;
            header.BackColor = t.Bg;
            _historyTitle.ForeColor = t.Text; _historyTitle.BackColor = t.Bg;
            _historyCount.ForeColor = t.TextMuted; _historyCount.BackColor = t.Bg;
            _search.ApplyTheme(t);
            _clearAllBtn.ApplyTheme(t);
            _historyScroll.BackColor = t.Bg;
            _historyList.BackColor = t.Bg;
            _empty.ApplyTheme(t);
            foreach (var card in _historyList.Controls.OfType<HistoryCard>()) card.ApplyTheme(t);
            header.Invalidate();
        });
    }

    private void CenterEmpty()
    {
        _empty.Location = new Point(
            Math.Max(0, (_historyScroll.ClientSize.Width - _empty.Width) / 2),
            Math.Max(Ui.S(20), (_historyScroll.ClientSize.Height - _empty.Height) / 2 - Ui.S(20)));
    }

    private void ResizeCards()
    {
        int w = _historyScroll.ClientSize.Width - _historyList.Padding.Horizontal;
        if (w < 200) return;
        _historyList.SuspendLayout();
        foreach (var card in _historyList.Controls.OfType<HistoryCard>())
            card.Width = w;
        _historyList.ResumeLayout();
    }

    private void BuildSettingsPage()
    {
        _settingsPage = new Panel { Dock = DockStyle.Fill };
        _content.Controls.Add(_settingsPage);

        var header = new Panel { Dock = DockStyle.Top, Height = Ui.S(66) };
        var title = new Label { Text = "Settings", Font = Theme.FontTitle, AutoSize = true, Location = new Point(Ui.S(24), Ui.S(12)) };
        var sub = new Label { Text = "Configuration is stored next to the app", Font = Theme.FontUiSmall, AutoSize = true, Location = new Point(Ui.S(25), Ui.S(38)) };
        header.Controls.Add(title);
        header.Controls.Add(sub);
        header.Paint += (s, e) =>
        {
            using var pen = new Pen(T.Border);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };
        _settingsPage.Controls.Add(header);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _settingsPage.Controls.Add(scroll);
        scroll.BringToFront();

        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = new Point(Ui.S(24), Ui.S(4)),
            MaximumSize = new Size(Ui.S(640), 0),
        };
        scroll.Controls.Add(stack);

        var sectionLabels = new List<Label>();
        Label Section(string text)
        {
            var lbl = new Label
            {
                Text = text.ToUpperInvariant(),
                Font = Theme.FontSection,
                AutoSize = true,
                Margin = new Padding(0, Ui.S(22), 0, Ui.S(8)),
            };
            sectionLabels.Add(lbl);
            stack.Controls.Add(lbl);
            return lbl;
        }

        var rows = new List<SettingRow>();
        SettingRow Row(string label, string? desc, Control ctrl, Control? extra = null)
        {
            var row = new SettingRow(label, desc, ctrl, extra) { Width = Ui.S(640) };
            rows.Add(row);
            stack.Controls.Add(row);
            return row;
        }

        // ── Capture ──
        Section("Capture");

        _hotkeyBox = new HotkeyBox(_settings);
        _hotkeyBox.HotkeyCommitted += OnHotkeyChanged;
        var resetBtn = new NiceButton { Text = "Reset", Style = NiceButton.Kind.Outline, Size = new Size(Ui.S(64), Ui.S(30)) };
        resetBtn.Click += (s, e) =>
        {
            _settings.HotkeyModifiers = AppSettings.DefaultModifiers;
            _settings.HotkeyKey = AppSettings.DefaultKey;
            _settings.Save();
            _hotkeyBox.RefreshDisplay();
            OnHotkeyChanged();
        };
        Row("Capture hotkey", "Click the field, then press your combo", _hotkeyBox, resetBtn);

        var enabledToggle = new ToggleSwitch();
        enabledToggle.SetCheckedSilently(_settings.Enabled);
        enabledToggle.CheckedChanged += (s, e) =>
        {
            _settings.Enabled = enabledToggle.Checked;
            _settings.Save();
            OnHotkeyChanged();
        };
        Row("Hotkey enabled", "Toggle the global keyboard shortcut", enabledToggle);
        _sync.Add(() => { enabledToggle.SetCheckedSilently(_settings.Enabled); _footer.Invalidate(); });

        var copyToggle = new ToggleSwitch();
        copyToggle.SetCheckedSilently(_settings.CopyToClipboard);
        copyToggle.CheckedChanged += (s, e) => { _settings.CopyToClipboard = copyToggle.Checked; _settings.Save(); };
        Row("Copy to clipboard", "Auto-copy extracted text after each capture", copyToggle);

        var notifyToggle = new ToggleSwitch();
        notifyToggle.SetCheckedSilently(_settings.ShowNotification);
        notifyToggle.CheckedChanged += (s, e) => { _settings.ShowNotification = notifyToggle.Checked; _settings.Save(); };
        Row("Show notification", "Tray balloon when capture completes", notifyToggle);

        // ── System ──
        Section("System");

        var runToggle = new ToggleSwitch();
        runToggle.SetCheckedSilently(_settings.RunWithWindows);
        runToggle.CheckedChanged += (s, e) =>
        {
            _settings.RunWithWindows = runToggle.Checked;
            _settings.Save();
            StartupHelper.SetRunWithWindows(runToggle.Checked);
        };
        Row("Run with Windows", "Start SnapText automatically at sign-in", runToggle);

        var themeSeg = new Segmented { Options = ["Light", "Dark", "System"], Value = _settings.Theme, Size = new Size(Ui.S(200), Ui.S(30)) };
        themeSeg.ValueChanged += v =>
        {
            _settings.Theme = v;
            _settings.Save();
            ApplyTheme();
            ThemeChanged?.Invoke();
        };
        Row("Theme", "Match Windows, or pick one", themeSeg);

        var openFolderBtn = new NiceButton { Text = "Open folder", IconName = "folder", Style = NiceButton.Kind.Outline, Size = new Size(Ui.S(132), Ui.S(30)) };
        openFolderBtn.Click += (s, e) =>
        {
            try
            {
                Directory.CreateDirectory(HistoryStore.Dir);
                Process.Start(new ProcessStartInfo(HistoryStore.Dir) { UseShellExecute = true });
            }
            catch { }
        };
        Row("Captures folder", HistoryStore.Dir, openFolderBtn);

        // ── About ──
        Section("About");
        var about = new AboutCard { Width = Ui.S(640), Height = Ui.S(68), Margin = new Padding(0, 0, 0, 0) };
        stack.Controls.Add(about);

        _themed.Add(t =>
        {
            _settingsPage.BackColor = t.Bg;
            header.BackColor = t.Bg;
            title.ForeColor = t.Text; title.BackColor = t.Bg;
            sub.ForeColor = t.TextMuted; sub.BackColor = t.Bg;
            scroll.BackColor = t.Bg;
            stack.BackColor = t.Bg;
            foreach (var lbl in sectionLabels) { lbl.ForeColor = t.TextDim; lbl.BackColor = t.Bg; }
            foreach (var row in rows) row.ApplyTheme(t);
            _hotkeyBox.ApplyTheme(t);
            resetBtn.ApplyTheme(t);
            enabledToggle.ApplyTheme(t); copyToggle.ApplyTheme(t);
            notifyToggle.ApplyTheme(t); runToggle.ApplyTheme(t);
            themeSeg.ApplyTheme(t);
            openFolderBtn.ApplyTheme(t);
            about.ApplyTheme(t);
            header.Invalidate();
        });
    }

    private void OnHotkeyChanged()
    {
        _captureBtn.BadgeText = _settings.HotkeyText;
        _captureBtn.Invalidate();
        _footer.Invalidate();
        _empty.Invalidate();
        HotkeySettingsChanged?.Invoke();
    }

    /// <summary>Re-read settings into the UI (after the tray menu changed them).</summary>
    public void SyncFromSettings()
    {
        foreach (var a in _sync) a();
    }

    /// <summary>Handle the cross-instance "show yourself" broadcast.</summary>
    protected override void WndProc(ref Message m)
    {
        if (Program.ShowMeMessage != 0 && m.Msg == Program.ShowMeMessage)
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
            return;
        }
        base.WndProc(ref m);
    }

    // ─── pages / theme ────────────────────────────────────────

    public void ShowPage(int index)
    {
        _historyPage.Visible = index == 0;
        _settingsPage.Visible = index == 1;
        _navHistory.Active = index == 0;
        _navSettings.Active = index == 1;
    }

    public void ApplyTheme()
    {
        T = Theme.Resolve(_settings.Theme);
        BackColor = T.Bg;
        foreach (var apply in _themed) apply(T);
        ApplyTitleBarTheme();
        Invalidate(true);
    }

    private void ApplyTitleBarTheme()
    {
        try
        {
            int dark = T.IsDark ? 1 : 0;
            DwmSetWindowAttribute(Handle, 20 /*DWMWA_USE_IMMERSIVE_DARK_MODE*/, ref dark, sizeof(int));
        }
        catch { }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTitleBarTheme();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // ─── history ──────────────────────────────────────────────

    public void AddHistory(HistoryEntry entry)
    {
        if (InvokeRequired) { Invoke(() => AddHistory(entry)); return; }
        AddHistoryCard(entry, append: false);
        RefreshHistoryUi();
    }

    private void AddHistoryCard(HistoryEntry entry, bool append)
    {
        if (append) _history.Add(entry); else _history.Insert(0, entry);

        var card = new HistoryCard(entry, T)
        {
            Width = Math.Max(300, _historyScroll.ClientSize.Width - _historyList.Padding.Horizontal),
        };
        card.DeleteRequested += () =>
        {
            using var dlg = new ConfirmDialog(T,
                "Remove this capture?",
                "The text and image will be deleted from disk. This can't be undone.");
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            entry.DeleteFiles();
            _history.Remove(entry);
            _historyList.Controls.Remove(card);
            card.Dispose();
            entry.Dispose();
            RefreshHistoryUi();
        };
        _historyList.Controls.Add(card);
        if (!append) _historyList.Controls.SetChildIndex(card, 0);
    }

    private void ClearAll()
    {
        if (_history.Count == 0) return;
        using var dlg = new ConfirmDialog(T,
            $"Remove all {_history.Count} captures?",
            "All saved text and images will be deleted from disk. This can't be undone.");
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        foreach (var entry in _history) { entry.DeleteFiles(); entry.Dispose(); }
        _history.Clear();
        foreach (var card in _historyList.Controls.OfType<HistoryCard>().ToList())
        { _historyList.Controls.Remove(card); card.Dispose(); }
        RefreshHistoryUi();
    }

    private void RefreshHistoryUi()
    {
        string query = _search.Input.Text.Trim();
        bool anyVisible = false;
        _historyList.SuspendLayout();
        foreach (var card in _historyList.Controls.OfType<HistoryCard>())
        {
            bool match = query.Length == 0 ||
                card.Entry.Text.Contains(query, StringComparison.OrdinalIgnoreCase);
            card.Visible = match;
            anyVisible |= match;
        }
        _historyList.ResumeLayout();

        _historyCount.Text = $"{_history.Count} capture{(_history.Count == 1 ? "" : "s")} · stored locally";
        _empty.Visible = !anyVisible;
        if (_empty.Visible) { CenterEmpty(); _empty.BringToFront(); }
        ResizeCards();
    }
}

// ─── sidebar pieces ───────────────────────────────────────────

/// <summary>Sidebar navigation item (icon + label, active = raised surface).</summary>
public sealed class NavButton : Control
{
    public string IconName { get; set; } = "clock";
    private bool _active, _hover;
    private Theme _theme = Theme.Light;

    public bool Active
    {
        get => _active;
        set { _active = value; Invalidate(); }
    }

    public NavButton()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Height = Ui.S(32);
        Font = Theme.FontUi;
    }

    public void ApplyTheme(Theme t) { _theme = t; Invalidate(); }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var t = _theme;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (_active)
        {
            UiKit.FillRounded(g, t.Surface, rect, Ui.S(6));
            UiKit.StrokeRounded(g, t.Border, rect, Ui.S(6));
        }
        else if (_hover)
        {
            UiKit.FillRounded(g, t.SurfaceAlt, rect, Ui.S(6));
        }
        Color fg = _active ? t.Text : t.TextMuted;
        int isz = Ui.S(15);
        UiKit.DrawIcon(g, IconName, new Rectangle(Ui.S(10), (Height - isz) / 2, isz, isz), fg);
        TextRenderer.DrawText(g, Text,
            _active ? Theme.FontUiMedium : Font,
            new Rectangle(Ui.S(32), 0, Width - Ui.S(34), Height), fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

/// <summary>Sidebar footer: status dot + hotkey state + version line.</summary>
public sealed class StatusFooter : Control
{
    private readonly AppSettings _settings;
    private Theme _theme = Theme.Light;

    public StatusFooter(AppSettings settings)
    {
        _settings = settings;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = Ui.S(56);
    }

    public void ApplyTheme(Theme t) { _theme = t; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var t = _theme;
        using var borderPen = new Pen(t.Border);
        g.DrawLine(borderPen, 0, 0, Width, 0);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Color dot = _settings.Enabled ? t.Success : t.TextDim;
        using (var b = new SolidBrush(dot))
            g.FillEllipse(b, Ui.S(10), Ui.S(18), Ui.S(7), Ui.S(7));

        string status = _settings.Enabled ? "Listening for hotkey" : "Hotkey disabled";
        TextRenderer.DrawText(g, status, Theme.FontUiSmall,
            new Rectangle(Ui.S(24), Ui.S(10), Width - Ui.S(24), Ui.S(22)), t.TextMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        TextRenderer.DrawText(g, $"v{MainForm.Version} · MIT · open source", Theme.FontUiSmall,
            new Rectangle(Ui.S(10), Ui.S(34), Width - Ui.S(10), Ui.S(18)), t.TextDim,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

/// <summary>Centered "No captures yet" state.</summary>
public sealed class EmptyState : Control
{
    private readonly AppSettings _settings;
    private Theme _theme = Theme.Light;

    public EmptyState(AppSettings settings)
    {
        _settings = settings;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(Ui.S(340), Ui.S(180));
    }

    public void ApplyTheme(Theme t) { _theme = t; BackColor = t.Bg; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var t = _theme;

        int bs = Ui.S(56);
        var box = new Rectangle((Width - bs) / 2, 0, bs, bs);
        UiKit.FillRounded(g, t.Surface, box, Ui.S(14));
        UiKit.StrokeRounded(g, t.Border, box, Ui.S(14));
        UiKit.DrawIcon(g, "capture", new Rectangle(box.X + Ui.S(15), box.Y + Ui.S(15), Ui.S(26), Ui.S(26)), t.TextDim);

        TextRenderer.DrawText(g, "No captures yet", Theme.FontUiMedium,
            new Rectangle(0, Ui.S(68), Width, Ui.S(22)), t.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

        string hint = $"Press {_settings.HotkeyText.Replace("+", " + ")} anywhere to grab text from your screen.";
        TextRenderer.DrawText(g, hint, Theme.FontUiSmall,
            new Rectangle(Ui.S(10), Ui.S(94), Width - Ui.S(20), Ui.S(40)), t.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
    }
}

// ─── settings pieces ──────────────────────────────────────────

/// <summary>Label + description on the left, control on the right, hairline below.</summary>
public sealed class SettingRow : Panel
{
    private readonly Label _label, _desc;
    private readonly Control _ctrl;
    private readonly Control? _extra;
    private Theme _theme = Theme.Light;

    public SettingRow(string label, string? desc, Control ctrl, Control? extra = null)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        Height = Ui.S(58);
        Margin = new Padding(0);
        _ctrl = ctrl;
        _extra = extra;

        _label = new Label { Text = label, Font = Theme.FontUi, AutoSize = true, Location = new Point(0, Ui.S(10)) };
        _desc = new Label { Text = desc ?? "", Font = Theme.FontUiSmall, AutoSize = true, Location = new Point(0, Ui.S(30)), MaximumSize = new Size(Ui.S(400), 0) };
        Controls.Add(_label);
        Controls.Add(_desc);
        Controls.Add(ctrl);
        if (extra != null) Controls.Add(extra);
        Layout += (s, e) => DoLayout();
    }

    private void DoLayout()
    {
        int right = Width - Ui.S(4);
        if (_extra != null)
        {
            _extra.Location = new Point(right - _extra.Width, (Height - _extra.Height) / 2 - Ui.S(2));
            right = _extra.Left - Ui.S(8);
        }
        _ctrl.Location = new Point(right - _ctrl.Width, (Height - _ctrl.Height) / 2 - Ui.S(2));
    }

    public void ApplyTheme(Theme t)
    {
        _theme = t;
        BackColor = t.Bg;
        _label.ForeColor = t.Text; _label.BackColor = t.Bg;
        _desc.ForeColor = t.TextMuted; _desc.BackColor = t.Bg;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(_theme.Border);
        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }
}

/// <summary>About card: logo, version, license line, "open folder" affordance.</summary>
public sealed class AboutCard : Control
{
    private Theme _theme = Theme.Light;

    public AboutCard()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = Ui.S(68);
        Cursor = Cursors.Hand;
        Click += (s, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppSettings.AppDir) { UseShellExecute = true }); }
            catch { }
        };
    }

    public void ApplyTheme(Theme t) { _theme = t; BackColor = t.Bg; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var t = _theme;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        UiKit.FillRounded(g, t.Surface, rect, Ui.S(10));
        UiKit.StrokeRounded(g, t.Border, rect, Ui.S(10));

        int ls = Ui.S(40);
        var logo = new Rectangle(Ui.S(14), (Height - ls) / 2, ls, ls);
        UiKit.FillRounded(g, t.Accent, logo, Ui.S(10));
        UiKit.DrawIcon(g, "bolt", new Rectangle(logo.X + Ui.S(10), logo.Y + Ui.S(10), Ui.S(20), Ui.S(20)), t.AccentText);

        TextRenderer.DrawText(g, $"SnapText {MainForm.Version}", Theme.FontUiMedium,
            new Point(Ui.S(66), Ui.S(15)), t.Text, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "Open source · MIT License · No telemetry, no network calls", Theme.FontUiSmall,
            new Point(Ui.S(66), Ui.S(36)), t.TextMuted, TextFormatFlags.NoPadding);
    }
}

// ─── history card ─────────────────────────────────────────────

public sealed class HistoryCard : Panel
{
    public HistoryEntry Entry { get; }
    public event Action? DeleteRequested;

    private Theme _theme;
    private bool _hover;
    private readonly IconButton _copyBtn, _imageBtn, _trashBtn;
    private readonly System.Windows.Forms.Timer _copiedTimer = new() { Interval = 1500 };

    private static int Pad => Ui.S(14);
    private static int ThumbSize => Ui.S(44);
    private static int CardHeight => Ui.S(104);

    public HistoryCard(HistoryEntry entry, Theme theme)
    {
        Entry = entry;
        _theme = theme;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = CardHeight;
        Margin = new Padding(0, 0, 0, Ui.S(8));

        _copyBtn = new IconButton { IconName = "copy" };
        _copyBtn.Click += (s, e) => CopyText();
        _imageBtn = new IconButton { IconName = "image" };
        _imageBtn.Click += (s, e) => OpenImage();
        _trashBtn = new IconButton { IconName = "trash", Danger = true };
        _trashBtn.Click += (s, e) => DeleteRequested?.Invoke();
        Controls.Add(_copyBtn);
        Controls.Add(_imageBtn);
        Controls.Add(_trashBtn);

        _copiedTimer.Tick += (s, e) =>
        {
            _copiedTimer.Stop();
            _copyBtn.ShowSuccess = false;
            _copyBtn.Invalidate();
        };

        ApplyTheme(theme);
        Layout += (s, e) => DoLayout();
        DoLayout();
    }

    private void DoLayout()
    {
        int b = Ui.S(30);
        int x = Width - Pad - b;
        _copyBtn.SetBounds(x, Pad - Ui.S(4), b, b);
        _imageBtn.SetBounds(x, Pad + Ui.S(28), b, b);
        _trashBtn.SetBounds(x, Pad + Ui.S(60), b, b);
    }

    private void CopyText()
    {
        if (!string.IsNullOrWhiteSpace(Entry.Text))
            try { Clipboard.SetText(Entry.Text); } catch { }
        _copyBtn.ShowSuccess = true;
        _copyBtn.Invalidate();
        _copiedTimer.Stop();
        _copiedTimer.Start();
    }

    private void OpenImage()
    {
        if (!File.Exists(Entry.ImagePath)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Entry.ImagePath) { UseShellExecute = true }); }
        catch { }
    }

    public void ApplyTheme(Theme t)
    {
        _theme = t;
        BackColor = t.Bg;
        _copyBtn.ApplyTheme(t);
        _imageBtn.ApplyTheme(t);
        _trashBtn.ApplyTheme(t);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e)
    {
        if (!ClientRectangle.Contains(PointToClient(MousePosition))) { _hover = false; Invalidate(); }
        base.OnMouseLeave(e);
    }

    private static bool LooksLikeCode(string text) =>
        text.Contains("Error", StringComparison.Ordinal) || text.Contains('{') ||
        text.Contains("</") || text.StartsWith("curl ", StringComparison.Ordinal) ||
        text.Contains("://");

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var t = _theme;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        UiKit.FillRounded(g, t.Surface, rect, Ui.S(10));
        UiKit.StrokeRounded(g, _hover ? t.BorderStrong : t.Border, rect, Ui.S(10));

        int textX = Pad;

        // thumbnail
        var thumbRect = new Rectangle(Pad, Pad, ThumbSize, ThumbSize);
        if (Entry.Thumbnail != null)
        {
            using var path = UiKit.RoundedPath(thumbRect, Ui.S(6));
            var old = g.Clip;
            g.SetClip(path);
            g.DrawImage(Entry.Thumbnail, thumbRect);
            g.Clip = old;
            UiKit.StrokeRounded(g, t.Border, thumbRect, Ui.S(6));
        }
        else
        {
            UiKit.FillRounded(g, t.SurfaceAlt, thumbRect, Ui.S(6));
            UiKit.StrokeRounded(g, t.Border, thumbRect, Ui.S(6));
            UiKit.DrawIcon(g, "image", new Rectangle(thumbRect.X + Ui.S(13), thumbRect.Y + Ui.S(13), Ui.S(18), Ui.S(18)), t.TextDim);
        }
        textX += ThumbSize + Ui.S(12);

        // meta line: time · N chars
        string meta = Entry.TimeLabel;
        TextRenderer.DrawText(g, meta, Theme.FontUiSmall, new Point(textX, Pad - 1), t.TextMuted, TextFormatFlags.NoPadding);
        int metaW = TextRenderer.MeasureText(meta, Theme.FontUiSmall).Width;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var dotBrush = new SolidBrush(t.TextDim))
            g.FillEllipse(dotBrush, textX + metaW + Ui.S(6), Pad + Ui.S(4), Ui.S(3), Ui.S(3));
        TextRenderer.DrawText(g, $"{Entry.Text.Length} chars", Theme.FontMonoSmall,
            new Point(textX + metaW + Ui.S(14), Pad - 1), t.TextMuted, TextFormatFlags.NoPadding);

        // text preview (up to 3 lines)
        string preview = string.IsNullOrWhiteSpace(Entry.Text) ? "(no text detected)" : Entry.Text;
        var font = LooksLikeCode(preview) ? Theme.FontMono : Theme.FontUi;
        var textRect = new Rectangle(textX, Pad + Ui.S(18), Width - textX - Pad - Ui.S(38), Height - Pad * 2 - Ui.S(16));
        TextRenderer.DrawText(g, preview, font, textRect,
            string.IsNullOrWhiteSpace(Entry.Text) ? t.TextDim : t.Text,
            TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding
            | TextFormatFlags.TextBoxControl);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _copiedTimer.Dispose();
        base.Dispose(disposing);
    }
}

// ─── confirm dialog ───────────────────────────────────────────

public sealed class ConfirmDialog : Form
{
    public ConfirmDialog(Theme t, string title, string message)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(Ui.S(320), Ui.S(150));
        BackColor = t.Surface;
        ShowInTaskbar = false;
        KeyPreview = true;

        var titleLbl = new Label
        {
            Text = title, Font = Theme.FontUiMedium, ForeColor = t.Text, BackColor = t.Surface,
            AutoSize = false, Bounds = new Rectangle(Ui.S(18), Ui.S(16), Ui.S(284), Ui.S(20)),
        };
        var msgLbl = new Label
        {
            Text = message, Font = Theme.FontUiSmall, ForeColor = t.TextMuted, BackColor = t.Surface,
            AutoSize = false, Bounds = new Rectangle(Ui.S(18), Ui.S(40), Ui.S(284), Ui.S(48)),
        };

        var cancel = new NiceButton { Text = "Cancel", Style = NiceButton.Kind.Outline, Size = new Size(Ui.S(74), Ui.S(30)) };
        cancel.ApplyTheme(t);
        cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var remove = new NiceButton { Text = "Remove", Style = NiceButton.Kind.Accent, Size = new Size(Ui.S(84), Ui.S(30)) };
        remove.ApplyTheme(t);
        remove.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

        remove.Location = new Point(Width - Ui.S(18) - remove.Width, Height - Ui.S(44));
        cancel.Location = new Point(remove.Left - Ui.S(8) - cancel.Width, Height - Ui.S(44));

        Controls.Add(titleLbl);
        Controls.Add(msgLbl);
        Controls.Add(cancel);
        Controls.Add(remove);

        // red accent for the destructive action
        RemakeDanger(remove, t);

        Paint += (s, e) =>
        {
            using var pen = new Pen(t.BorderStrong);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
    }

    private static void RemakeDanger(NiceButton btn, Theme t)
    {
        // paint over the accent style with danger colors via a custom theme clone
        btn.Paint += (s, e) =>
        {
            var g = e.Graphics;
            UiKit.FillRounded(g, t.Danger, new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 7);
            TextRenderer.DrawText(g, btn.Text, Theme.FontUiMedium,
                new Rectangle(0, 0, btn.Width, btn.Height), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        };
    }
}

// ─── app icon (drawn at runtime; icon.ico next to the exe overrides) ──

public static class AppIcon
{
    public static Icon Create(Color accent)
    {
        var icoPath = Path.Combine(AppSettings.AppDir, "icon.ico");
        if (File.Exists(icoPath))
            try { return new Icon(icoPath); } catch { }

        try
        {
            using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                UiKit.FillRounded(g, accent, new Rectangle(0, 0, 31, 31), 8);
                UiKit.DrawIcon(g, "bolt", new Rectangle(6, 6, 20, 20), Color.White);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
        catch { return SystemIcons.Application; }
    }
}
