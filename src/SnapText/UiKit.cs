using System.Drawing.Drawing2D;

namespace SnapText;

/// <summary>Global DPI scale for fixed pixel dimensions (fonts scale on their own).</summary>
public static class Ui
{
    public static float Factor { get; private set; } = 1f;
    public static void SetScale(int deviceDpi) => Factor = deviceDpi / 96f;
    /// <summary>Scale a design-pixel value to device pixels.</summary>
    public static int S(int v) => (int)MathF.Round(v * Factor);
}

/// <summary>Drawing helpers + line icons (24-unit viewbox, like the design's SVG set).</summary>
public static class UiKit
{
    public static GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        if (radius <= 0) { p.AddRectangle(r); return p; }
        int d = radius * 2;
        d = Math.Min(d, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRounded(Graphics g, Color color, Rectangle r, int radius)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var b = new SolidBrush(color);
        using var p = RoundedPath(r, radius);
        g.FillPath(b, p);
    }

    public static void StrokeRounded(Graphics g, Color color, Rectangle r, int radius, float width = 1f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, width);
        using var p = RoundedPath(r, radius);
        g.DrawPath(pen, p);
    }

    /// <summary>Draw a line icon centered in <paramref name="bounds"/>.</summary>
    public static void DrawIcon(Graphics g, string name, Rectangle bounds, Color color, float strokeWidth = 1.6f)
    {
        float s = Math.Min(bounds.Width, bounds.Height) / 24f;
        float ox = bounds.X + (bounds.Width - 24f * s) / 2f;
        float oy = bounds.Y + (bounds.Height - 24f * s) / 2f;
        PointF P(float x, float y) => new(ox + x * s, oy + y * s);
        RectangleF R(float x, float y, float w, float h) => new(ox + x * s, oy + y * s, w * s, h * s);

        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, strokeWidth * s)
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);

        void Line(float x1, float y1, float x2, float y2) => g.DrawLine(pen, P(x1, y1), P(x2, y2));
        void Poly(params PointF[] pts) => g.DrawLines(pen, pts);
        void Circle(float cx, float cy, float r) => g.DrawEllipse(pen, R(cx - r, cy - r, r * 2, r * 2));

        switch (name)
        {
            case "bolt": // filled lightning
                g.FillPolygon(brush, new[] { P(13, 2), P(4, 14), P(11, 14), P(10, 22), P(19, 10), P(12, 10) });
                break;

            case "capture": // corner brackets + inner rect
                Poly(P(3, 9), P(3, 6.5f), P(4.5f, 4.5f), P(6.5f, 3), P(9, 3));
                Poly(P(15, 3), P(17.5f, 3), P(19.5f, 4.5f), P(21, 6.5f), P(21, 9));
                Poly(P(3, 15), P(3, 17.5f), P(4.5f, 19.5f), P(6.5f, 21), P(9, 21));
                Poly(P(15, 21), P(17.5f, 21), P(19.5f, 19.5f), P(21, 17.5f), P(21, 15));
                g.DrawPath(pen, RoundedPath(Rectangle.Round(R(8, 8, 8, 8)), (int)(1 * s)));
                break;

            case "clock":
                Circle(12, 12, 9);
                Poly(P(12, 7), P(12, 12), P(15, 14));
                break;

            case "settings": // gear: hub + ring + teeth
                Circle(12, 12, 3.2f);
                Circle(12, 12, 7.2f);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    float c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                    Line(12 + c * 7.2f, 12 + sn * 7.2f, 12 + c * 9.8f, 12 + sn * 9.8f);
                }
                break;

            case "copy":
                g.DrawPath(pen, RoundedPath(Rectangle.Round(R(9, 9, 11, 11)), (int)(2 * s)));
                Poly(P(5, 15), P(4, 15), P(2.6f, 13.6f), P(2.6f, 4.6f), P(4, 3), P(13, 3), P(14.4f, 4.4f), P(14.4f, 5));
                break;

            case "image":
                g.DrawPath(pen, RoundedPath(Rectangle.Round(R(3, 3, 18, 18)), (int)(2 * s)));
                Circle(9, 9, 1.6f);
                Poly(P(21, 15), P(16.5f, 10.5f), P(7, 20));
                break;

            case "trash":
                Line(3, 6, 21, 6);
                Poly(P(8, 6), P(8, 4.5f), P(9.5f, 3), P(14.5f, 3), P(16, 4.5f), P(16, 6));
                Poly(P(5, 6), P(5.6f, 20), P(7, 21.4f), P(17, 21.4f), P(18.4f, 20), P(19, 6));
                Line(10, 11, 10, 17);
                Line(14, 11, 14, 17);
                break;

            case "check":
                Poly(P(20, 6), P(9, 17), P(4, 12));
                break;

            case "search":
                Circle(11, 11, 7);
                Line(16.2f, 16.2f, 21, 21);
                break;

            case "kbd":
                g.DrawPath(pen, RoundedPath(Rectangle.Round(R(2, 6, 20, 12)), (int)(2 * s)));
                foreach (float x in new[] { 6f, 10f, 14f, 18f })
                    Line(x, 10, x + 0.02f, 10);
                Line(6, 14, 18, 14);
                break;

            case "folder":
                Poly(P(3, 19), P(3, 5), P(4.5f, 4), P(9, 4), P(11, 7), P(19.5f, 7), P(21, 8.5f), P(21, 19), P(19.5f, 20), P(4.5f, 20), P(3, 19));
                break;

            case "close":
                Line(6, 6, 18, 18);
                Line(18, 6, 6, 18);
                break;
        }
        g.SmoothingMode = old;
    }
}

/// <summary>Owner-drawn rounded button: accent-filled, outlined, or ghost.</summary>
public sealed class NiceButton : Control
{
    public enum Kind { Accent, Outline, Ghost }

    public Kind Style { get; set; } = Kind.Outline;
    public string? IconName { get; set; }
    public string? BadgeText { get; set; }   // e.g. "Ctrl+J" chip on the accent button
    public int Radius { get; set; } = 7;
    public bool DangerHover { get; set; }

    private Theme _theme = Theme.Light;
    private bool _hover;

    public NiceButton()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
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
        int Radius = Ui.S(this.Radius);

        Color bg, fg, border = Color.Empty;
        switch (Style)
        {
            case Kind.Accent:
                bg = _hover ? ControlPaint.Light(t.Accent, 0.08f) : t.Accent;
                fg = t.AccentText;
                break;
            case Kind.Outline:
                bg = _hover ? t.SurfaceAlt : t.Surface;
                fg = DangerHover && _hover ? t.Danger : t.Text;
                border = _hover ? t.BorderStrong : t.Border;
                break;
            default: // Ghost
                bg = _hover ? t.SurfaceAlt : Color.Empty;
                fg = DangerHover && _hover ? t.Danger : (_hover ? t.Text : t.TextMuted);
                break;
        }

        if (bg != Color.Empty) UiKit.FillRounded(g, bg, rect, Radius);
        if (border != Color.Empty) UiKit.StrokeRounded(g, border, rect, Radius);

        // content: [icon] text [badge]; with a badge, icon+text left-align and the badge sits right
        int iconSize = Ui.S(15);
        var textSize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        int badgeW = 0;
        if (BadgeText != null)
            badgeW = TextRenderer.MeasureText(BadgeText, Theme.FontMonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width + Ui.S(10);

        int contentW = (IconName != null ? iconSize + Ui.S(6) : 0) + textSize.Width;
        int x = BadgeText != null ? Ui.S(8) : (Width - contentW) / 2;

        if (IconName != null)
        {
            UiKit.DrawIcon(g, IconName, new Rectangle(x, (Height - iconSize) / 2, iconSize, iconSize), fg);
            x += iconSize + Ui.S(6);
        }
        int textRight = BadgeText != null ? Width - badgeW - Ui.S(12) : Width;
        TextRenderer.DrawText(g, Text, Font,
            new Rectangle(x, (Height - textSize.Height) / 2, Math.Max(10, textRight - x), textSize.Height), fg,
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        if (BadgeText != null)
        {
            int bh = Ui.S(18);
            var br = new Rectangle(Width - badgeW - Ui.S(8), (Height - bh) / 2, badgeW, bh);
            UiKit.FillRounded(g, Color.FromArgb(46, 255, 255, 255), br, Ui.S(4));
            TextRenderer.DrawText(g, BadgeText, Theme.FontMonoSmall, br, Color.FromArgb(fg.R, fg.G, fg.B),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}

/// <summary>Small square icon-only button (copy / open image / delete on cards).</summary>
public sealed class IconButton : Control
{
    public string IconName { get; set; } = "copy";
    public bool Danger { get; set; }
    public bool ShowSuccess { get; set; }

    private Theme _theme = Theme.Light;
    private bool _hover;

    public IconButton()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Size = new Size(Ui.S(30), Ui.S(30));
    }

    public void ApplyTheme(Theme t) { _theme = t; Invalidate(); }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var t = _theme;
        Color fg = ShowSuccess ? t.Success
                 : Danger ? t.Danger
                 : _hover ? t.Text : t.TextMuted;
        Color bg = ShowSuccess ? t.SuccessSoft
                 : _hover ? (Danger ? t.DangerSoft : t.SurfaceAlt)
                 : Color.Empty;
        if (bg != Color.Empty)
            UiKit.FillRounded(e.Graphics, bg, new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(6));
        int isz = Ui.S(15);
        UiKit.DrawIcon(e.Graphics, ShowSuccess ? "check" : IconName,
            new Rectangle((Width - isz) / 2, (Height - isz) / 2, isz, isz), fg);
    }
}

/// <summary>Design-style toggle switch (36 × 22).</summary>
public sealed class ToggleSwitch : Control
{
    private bool _checked;
    public event EventHandler? CheckedChanged;
    private Theme _theme = Theme.Light;

    public bool Checked
    {
        get => _checked;
        set { if (_checked == value) return; _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }

    /// <summary>Set state without firing CheckedChanged (for initialization).</summary>
    public void SetCheckedSilently(bool value) { _checked = value; Invalidate(); }

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(Ui.S(36), Ui.S(22));
        Cursor = Cursors.Hand;
    }

    public void ApplyTheme(Theme t) { _theme = t; Invalidate(); }

    protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var t = _theme;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int w = Ui.S(36), h = Ui.S(22), pad = Ui.S(2);
        int kd = h - pad * 2;
        UiKit.FillRounded(g, _checked ? t.Accent : t.BorderStrong, new Rectangle(0, 0, w, h), h / 2);
        int kx = _checked ? w - pad - kd : pad;
        using var knob = new SolidBrush(Color.White);
        g.FillEllipse(knob, kx, pad, kd, kd);
        using var shadow = new Pen(Color.FromArgb(40, 0, 0, 0));
        g.DrawEllipse(shadow, kx, pad, kd, kd);
    }
}

/// <summary>Segmented control (Light / Dark / System).</summary>
public sealed class Segmented : Control
{
    public string[] Options { get; set; } = [];
    private string _value = "";
    public event Action<string>? ValueChanged;
    private Theme _theme = Theme.Light;
    private int _hoverIndex = -1;

    public string Value
    {
        get => _value;
        set { _value = value; Invalidate(); }
    }

    public Segmented()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = Ui.S(30);
        Cursor = Cursors.Hand;
        Font = Theme.FontUi;
    }

    public void ApplyTheme(Theme t) { _theme = t; Invalidate(); }

    private int SegWidth => Options.Length == 0 ? 0 : (Width - Ui.S(4)) / Options.Length;

    private int IndexAt(Point p)
    {
        if (SegWidth <= 0) return -1;
        int i = (p.X - Ui.S(2)) / SegWidth;
        return i >= 0 && i < Options.Length ? i : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int i = IndexAt(e.Location);
        if (i != _hoverIndex) { _hoverIndex = i; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int i = IndexAt(e.Location);
        if (i >= 0 && Options[i] != _value)
        {
            _value = Options[i];
            Invalidate();
            ValueChanged?.Invoke(_value);
        }
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var t = _theme;
        UiKit.FillRounded(g, t.SurfaceAlt, new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(7));
        UiKit.StrokeRounded(g, t.Border, new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(7));

        for (int i = 0; i < Options.Length; i++)
        {
            int p2 = Ui.S(2);
            var r = new Rectangle(p2 + i * SegWidth, p2, SegWidth, Height - p2 * 2 - 1);
            bool active = Options[i] == _value;
            if (active)
            {
                UiKit.FillRounded(g, t.Surface, r, Ui.S(5));
                UiKit.StrokeRounded(g, t.Border, r, Ui.S(5));
            }
            else if (i == _hoverIndex)
            {
                UiKit.FillRounded(g, t.Surface, r, Ui.S(5));
            }
            TextRenderer.DrawText(g, Options[i], Font, r,
                active ? t.Text : t.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

/// <summary>Rounded search field with a magnifier icon.</summary>
public sealed class SearchBox : Panel
{
    public readonly TextBox Input = new();
    private Theme _theme = Theme.Light;

    public SearchBox()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Size = new Size(Ui.S(220), Ui.S(30));
        Input.BorderStyle = BorderStyle.None;
        Input.Font = Theme.FontUi;
        Controls.Add(Input);
        LayoutInput();
    }

    private void LayoutInput()
    {
        Input.SetBounds(Ui.S(30), (Height - Input.PreferredHeight) / 2, Width - Ui.S(40), Input.PreferredHeight);
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); LayoutInput(); }

    public void ApplyTheme(Theme t)
    {
        _theme = t;
        BackColor = t.Surface;
        Input.BackColor = t.Surface;
        Input.ForeColor = t.Text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        UiKit.StrokeRounded(e.Graphics, _theme.Border, new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(7));
        int isz = Ui.S(14);
        UiKit.DrawIcon(e.Graphics, "search", new Rectangle(Ui.S(9), (Height - isz) / 2, isz, isz), _theme.TextMuted);
    }
}

/// <summary>
/// Click-to-record hotkey field. KeyDown tracks held keys; a non-modifier
/// keypress commits the combo (modifiers read from the held set).
/// </summary>
public sealed class HotkeyBox : Control
{
    private readonly AppSettings _settings;
    private bool _recording;
    private bool _hover;
    private Theme _theme = Theme.Light;
    public event Action? HotkeyCommitted;

    public HotkeyBox(AppSettings settings)
    {
        _settings = settings;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.Selectable
               | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(Ui.S(180), Ui.S(30));
        Cursor = Cursors.Hand;
        Font = Theme.FontUi;
        TabStop = true;
    }

    public void ApplyTheme(Theme t) { _theme = t; Invalidate(); }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        _recording = true;
        Focus();
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _recording = false;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
        if (!_recording) return;

        if (e.KeyCode == Keys.Escape) { _recording = false; Invalidate(); return; }
        if (IsModifier(e.KeyCode)) { Invalidate(); return; }

        uint mods = 0;
        if (e.Control) mods |= AppSettings.MOD_CONTROL;
        if (e.Alt) mods |= AppSettings.MOD_ALT;
        if (e.Shift) mods |= AppSettings.MOD_SHIFT;

        // Require at least one modifier unless it's a function key / PrintScreen
        bool bareAllowed = e.KeyCode is >= Keys.F1 and <= Keys.F24 or Keys.PrintScreen;
        if (mods == 0 && !bareAllowed) return;

        _settings.HotkeyModifiers = mods;
        _settings.HotkeyKey = e.KeyCode;
        _settings.Save();
        _recording = false;
        Invalidate();
        HotkeyCommitted?.Invoke();
    }

    private static bool IsModifier(Keys k) => k is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
        or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
        or Keys.Menu or Keys.LMenu or Keys.RMenu or Keys.LWin or Keys.RWin;

    public void RefreshDisplay() => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var t = _theme;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        UiKit.FillRounded(g, _recording ? t.AccentSoft : t.Surface, rect, Ui.S(7));
        UiKit.StrokeRounded(g, _recording ? t.Accent : (_hover ? t.BorderStrong : t.Border), rect, Ui.S(7));

        int isz = Ui.S(14);
        UiKit.DrawIcon(g, "kbd", new Rectangle(Ui.S(9), (Height - isz) / 2, isz, isz), t.TextMuted);

        string text = _recording ? "Press a combo…" : _settings.HotkeyText.Replace("+", " + ");
        var font = _recording ? Theme.FontUi : Theme.FontMono;
        TextRenderer.DrawText(g, text, font,
            new Rectangle(Ui.S(30), 0, Width - Ui.S(34), Height),
            _recording ? t.Accent : t.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }
}
