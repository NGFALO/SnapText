using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace SnapText;

/// <summary>
/// Text extraction via the OCR engine built into Windows 10/11
/// (Windows.Media.Ocr) — nothing to install, no network calls.
/// </summary>
public static class OcrService
{
    public static async Task<string> RecognizeAsync(Bitmap bmp)
    {
        try
        {
            var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                         ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
            if (engine == null) return "";

            // The engine rejects images above MaxImageDimension — scale down if needed.
            int maxDim = (int)OcrEngine.MaxImageDimension;
            Bitmap work = bmp;
            bool scaled = false;
            if (bmp.Width > maxDim || bmp.Height > maxDim)
            {
                double f = Math.Min((double)maxDim / bmp.Width, (double)maxDim / bmp.Height);
                work = new Bitmap(bmp, new Size(
                    Math.Max(1, (int)(bmp.Width * f)),
                    Math.Max(1, (int)(bmp.Height * f))));
                scaled = true;
            }

            try
            {
                using var soft = ToSoftwareBitmap(work);
                var result = await engine.RecognizeAsync(soft);

                var sb = new StringBuilder();
                foreach (var line in result.Lines)
                    sb.AppendLine(line.Text);
                return sb.ToString().TrimEnd();
            }
            finally
            {
                if (scaled) work.Dispose();
            }
        }
        catch
        {
            return "";
        }
    }

    /// <summary>GDI Bitmap → WinRT SoftwareBitmap, fully in memory (no temp file).</summary>
    private static SoftwareBitmap ToSoftwareBitmap(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            // 32bppArgb rows are always tightly packed (stride == width * 4).
            var bytes = new byte[Math.Abs(data.Stride) * bmp.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            var soft = new SoftwareBitmap(BitmapPixelFormat.Bgra8, bmp.Width, bmp.Height, BitmapAlphaMode.Ignore);
            soft.CopyFromBuffer(bytes.AsBuffer());
            return soft;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
