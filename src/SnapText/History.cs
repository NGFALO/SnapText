using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SnapText;

/// <summary>One capture: a PNG + TXT pair stored in Pictures\SnapText.</summary>
public sealed class HistoryEntry : IDisposable
{
    public required DateTime Timestamp { get; init; }
    public required string Text { get; set; }
    public required string ImagePath { get; init; }
    public required string TextPath { get; init; }
    public Bitmap? Thumbnail { get; private set; }

    public string TimeLabel
    {
        get
        {
            var delta = DateTime.Now - Timestamp;
            if (delta < TimeSpan.FromMinutes(1)) return "Just now";
            if (delta < TimeSpan.FromHours(1)) return $"{(int)delta.TotalMinutes} min ago";
            if (Timestamp.Date == DateTime.Today) return $"Today, {Timestamp:HH:mm}";
            if (Timestamp.Date == DateTime.Today.AddDays(-1)) return $"Yesterday, {Timestamp:HH:mm}";
            return Timestamp.ToString("MMM d, HH:mm");
        }
    }

    public void LoadThumbnail(int size = 0)
    {
        try
        {
            if (size <= 0) size = Ui.S(44);
            if (!File.Exists(ImagePath)) return;
            using var src = new Bitmap(ImagePath);
            var thumb = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(thumb);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            // cover-fit: fill the square, crop overflow
            double f = Math.Max((double)size / src.Width, (double)size / src.Height);
            int w = Math.Max(1, (int)(src.Width * f)), h = Math.Max(1, (int)(src.Height * f));
            g.DrawImage(src, (size - w) / 2, (size - h) / 2, w, h);
            Thumbnail = thumb;
        }
        catch { }
    }

    public void DeleteFiles()
    {
        try { if (File.Exists(ImagePath)) File.Delete(ImagePath); } catch { }
        try { if (File.Exists(TextPath)) File.Delete(TextPath); } catch { }
    }

    public void Dispose()
    {
        Thumbnail?.Dispose();
        Thumbnail = null;
    }
}

/// <summary>
/// Persists captures as PNG + TXT pairs in a folder inside the user's
/// Pictures directory (outside the app folder, as per the app rules).
/// </summary>
public static class HistoryStore
{
    public static string Dir
    {
        get
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrEmpty(pictures))
                pictures = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
            return Path.Combine(pictures, "SnapText");
        }
    }

    public static HistoryEntry Save(Bitmap image, string text, DateTime timestamp)
    {
        Directory.CreateDirectory(Dir);
        string stamp = timestamp.ToString("yyyy-MM-dd_HH-mm-ss");
        string baseName = stamp;
        int n = 1;
        while (File.Exists(Path.Combine(Dir, baseName + ".png")))
            baseName = $"{stamp}_{++n}";

        string imgPath = Path.Combine(Dir, baseName + ".png");
        string txtPath = Path.Combine(Dir, baseName + ".txt");
        try { image.Save(imgPath, ImageFormat.Png); } catch { }
        try { File.WriteAllText(txtPath, text); } catch { }

        var entry = new HistoryEntry { Timestamp = timestamp, Text = text, ImagePath = imgPath, TextPath = txtPath };
        entry.LoadThumbnail();
        return entry;
    }

    /// <summary>Rebuild history from the captures folder (newest first).</summary>
    public static List<HistoryEntry> LoadAll(int limit = 200)
    {
        var list = new List<HistoryEntry>();
        try
        {
            if (!Directory.Exists(Dir)) return list;
            var files = new DirectoryInfo(Dir).GetFiles("*.png")
                .OrderByDescending(f => f.LastWriteTime)
                .Take(limit);
            foreach (var f in files)
            {
                string txtPath = Path.ChangeExtension(f.FullName, ".txt");
                string text = "";
                try { if (File.Exists(txtPath)) text = File.ReadAllText(txtPath); } catch { }
                var entry = new HistoryEntry
                {
                    Timestamp = f.LastWriteTime,
                    Text = text,
                    ImagePath = f.FullName,
                    TextPath = txtPath,
                };
                entry.LoadThumbnail();
                list.Add(entry);
            }
        }
        catch { }
        return list;
    }
}
