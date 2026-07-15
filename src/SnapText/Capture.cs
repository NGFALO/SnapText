using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SnapText;

public sealed class CaptureResult
{
    public required HistoryEntry Entry { get; init; }
    public required bool HasText { get; init; }
}

public static class CaptureService
{
    /// <summary>
    /// Freeze the screen (all monitors), let the user drag-select a region,
    /// OCR it, save PNG+TXT to Pictures\SnapText and return the history entry.
    /// Returns null if the user cancelled or the selection was too small.
    /// </summary>
    public static async Task<CaptureResult?> CaptureAsync(AppSettings settings)
    {
        var bounds = SystemInformation.VirtualScreen;

        var frozen = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(frozen))
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

        Rectangle? selection;
        using (var overlay = new OverlayForm(frozen, bounds))
        {
            var tcs = new TaskCompletionSource<Rectangle?>();
            overlay.SelectionComplete += r => tcs.TrySetResult(r);
            overlay.Show();
            overlay.Activate();
            selection = await tcs.Task;
        }

        if (selection is not { Width: >= 4, Height: >= 4 } rect)
        {
            frozen.Dispose();
            return null;
        }

        // selection is in virtual-screen coordinates -> map into the frozen bitmap
        var crop = Rectangle.Intersect(
            new Rectangle(rect.X - bounds.X, rect.Y - bounds.Y, rect.Width, rect.Height),
            new Rectangle(0, 0, frozen.Width, frozen.Height));
        if (crop.IsEmpty) { frozen.Dispose(); return null; }

        using var cropped = frozen.Clone(crop, PixelFormat.Format32bppArgb);
        frozen.Dispose();

        string text = (await OcrService.RecognizeAsync(cropped)).Trim();

        if (settings.CopyToClipboard && text.Length > 0)
            try { Clipboard.SetText(text); } catch { }

        var entry = HistoryStore.Save(cropped, text, DateTime.Now);
        return new CaptureResult { Entry = entry, HasText = text.Length > 0 };
    }
}

/// <summary>
/// Full-screen frozen-screenshot overlay: dimmed screen, drag to select,
/// live dimension label, Esc to cancel. Matches the design's capture overlay.
/// </summary>
public sealed class OverlayForm : Form
{
    private static readonly Color AccentBorder = Theme.FromHex("#7d80f0");

    private readonly Bitmap _screenshot;
    private readonly Rectangle _virtualBounds;
    private Point _start, _current;
    private bool _dragging;
    private bool _done;

    public event Action<Rectangle?>? SelectionComplete;

    public OverlayForm(Bitmap screenshot, Rectangle virtualBounds)
    {
        _screenshot = screenshot;
        _virtualBounds = virtualBounds;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = virtualBounds;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;
    }

    protected override bool ShowWithoutActivation => false;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Bounds = _virtualBounds; // re-assert: DPI scaling can shift borderless bounds
        Focus();
    }

    private void Finish(Rectangle? result)
    {
        if (_done) return;
        _done = true;
        SelectionComplete?.Invoke(result);
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Finish(null);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Finish(null);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _start = _current = e.Location;
            _dragging = true;
            Invalidate();
        }
        else if (e.Button == MouseButtons.Right)
        {
            Finish(null);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_dragging) return;
        _current = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_dragging || e.Button != MouseButtons.Left) return;
        _dragging = false;
        var sel = SelectionRect();
        // convert client -> virtual screen coordinates
        Finish(new Rectangle(sel.X + _virtualBounds.X, sel.Y + _virtualBounds.Y, sel.Width, sel.Height));
    }

    private Rectangle SelectionRect() => new(
        Math.Min(_start.X, _current.X), Math.Min(_start.Y, _current.Y),
        Math.Abs(_current.X - _start.X), Math.Abs(_current.Y - _start.Y));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.DrawImageUnscaled(_screenshot, 0, 0);

        using var dim = new SolidBrush(Color.FromArgb(140, 0, 0, 0));

        if (_dragging)
        {
            var sel = SelectionRect();

            // dim everything except the selection
            var outside = new Region(ClientRectangle);
            outside.Exclude(sel);
            g.FillRegion(dim, outside);
            outside.Dispose();

            // accent border + white corner handles
            using var pen = new Pen(AccentBorder, Ui.S(2));
            g.DrawRectangle(pen, sel);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var handleBrush = new SolidBrush(Color.White);
            using var handlePen = new Pen(AccentBorder, 1);
            int hs = Ui.S(8);
            foreach (var (hx, hy) in new[] { (sel.Left, sel.Top), (sel.Right, sel.Top), (sel.Left, sel.Bottom), (sel.Right, sel.Bottom) })
            {
                var hr = new Rectangle(hx - hs / 2, hy - hs / 2, hs, hs);
                g.FillRectangle(handleBrush, hr);
                g.DrawRectangle(handlePen, hr);
            }
            g.SmoothingMode = SmoothingMode.Default;

            DrawDimensionLabel(g, sel);
        }
        else
        {
            g.FillRectangle(dim, ClientRectangle);
        }

        DrawHintPill(g);
    }

    private static void DrawDimensionLabel(Graphics g, Rectangle sel)
    {
        string label = $"{sel.Width} × {sel.Height}";
        using var font = new Font("Consolas", 10f * Ui.Factor, FontStyle.Bold);
        var size = g.MeasureString(label, font);
        int x = sel.X;
        int y = sel.Y > (int)size.Height + Ui.S(12) ? sel.Y - (int)size.Height - Ui.S(10) : sel.Y + sel.Height + Ui.S(8);

        var back = new RectangleF(x, y, size.Width + Ui.S(12), size.Height + Ui.S(6));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(Color.FromArgb(216, 0, 0, 0));
        using var path = UiKit.RoundedPath(Rectangle.Round(back), Ui.S(4));
        g.FillPath(bg, path);
        g.DrawString(label, font, Brushes.White, x + Ui.S(6), y + Ui.S(3));
        g.SmoothingMode = SmoothingMode.Default;
    }

    private void DrawHintPill(Graphics g)
    {
        // Bottom-center of the primary screen so it's visible on multi-monitor setups.
        var primary = Screen.PrimaryScreen?.Bounds ?? _virtualBounds;
        int cx = primary.X - _virtualBounds.X + primary.Width / 2;
        int by = primary.Y - _virtualBounds.Y + primary.Height - Ui.S(56);

        string part1 = "Drag to select";
        string kbd = "Esc";
        string part2 = "cancel";

        using var font = new Font("Segoe UI", 9.5f * Ui.Factor);
        using var kbdFont = new Font("Consolas", 8.5f * Ui.Factor);
        var s1 = g.MeasureString(part1, font);
        var sk = g.MeasureString(kbd, kbdFont);
        var s2 = g.MeasureString(part2, font);

        int pad = Ui.S(16), gap = Ui.S(10);
        int w = (int)(pad + s1.Width + gap + 1 + gap + sk.Width + Ui.S(10) + Ui.S(6) + s2.Width + pad);
        int h = Ui.S(38);
        var rect = new Rectangle(cx - w / 2, by, w, h);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new SolidBrush(Color.FromArgb(218, 15, 16, 25)))
        using (var path = UiKit.RoundedPath(rect, h / 2))
            g.FillPath(bg, path);

        float tx = rect.X + pad;
        float ty = rect.Y + (h - s1.Height) / 2f;
        using var dimText = new SolidBrush(Color.FromArgb(190, 255, 255, 255));
        g.DrawString(part1, font, dimText, tx, ty);
        tx += s1.Width + gap;

        using (var sepPen = new Pen(Color.FromArgb(50, 255, 255, 255)))
            g.DrawLine(sepPen, tx, rect.Y + Ui.S(12), tx, rect.Bottom - Ui.S(12));
        tx += gap;

        var kbdRect = new RectangleF(tx, rect.Y + (h - sk.Height - Ui.S(4)) / 2f, sk.Width + Ui.S(10), sk.Height + Ui.S(4));
        using (var kbdBg = new SolidBrush(Color.FromArgb(36, 255, 255, 255)))
        using (var kbdPath = UiKit.RoundedPath(Rectangle.Round(kbdRect), Ui.S(4)))
            g.FillPath(kbdBg, kbdPath);
        g.DrawString(kbd, kbdFont, Brushes.White, tx + Ui.S(5), kbdRect.Y + Ui.S(2));
        tx += kbdRect.Width + Ui.S(6);

        g.DrawString(part2, font, dimText, tx, ty);
        g.SmoothingMode = SmoothingMode.Default;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
