using Microsoft.Win32;

namespace SnapText;

/// <summary>Color palette matching the SnapText design tokens (light / dark).</summary>
public sealed class Theme
{
    public bool IsDark { get; }

    public Color Bg, Chrome, Sidebar, Surface, SurfaceAlt, Border, BorderStrong;
    public Color Text, TextMuted, TextDim;
    public Color Accent, AccentSoft, AccentText;
    public Color Danger, DangerSoft;
    public Color Success, SuccessSoft;

    public static readonly Font FontUi       = new("Segoe UI", 9.5f);
    public static readonly Font FontUiSmall  = new("Segoe UI", 8f);
    public static readonly Font FontUiMedium = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font FontTitle    = new("Segoe UI", 12f, FontStyle.Bold);
    public static readonly Font FontSection  = new("Segoe UI", 7.5f, FontStyle.Bold);
    public static readonly Font FontMono     = new("Consolas", 9f);
    public static readonly Font FontMonoSmall = new("Consolas", 8f);

    private Theme(bool dark)
    {
        IsDark = dark;
        if (dark)
        {
            Bg           = FromHex("#15161b");
            Chrome       = FromHex("#1d1e24");
            Sidebar      = FromHex("#17181d");
            Surface      = FromHex("#1f2026");
            SurfaceAlt   = FromHex("#22232a");
            Border       = FromHex("#2c2d35");
            BorderStrong = FromHex("#3a3b45");
            Text         = FromHex("#ececf0");
            TextMuted    = FromHex("#a4a5af");
            TextDim      = FromHex("#73747e");
            Accent       = FromHex("#7d80f0");
            AccentSoft   = FromHex("#2f3057");
            AccentText   = FromHex("#0d0e12");
            Danger       = FromHex("#e77b6d");
            DangerSoft   = FromHex("#4a2b28");
            Success      = FromHex("#4fc07e");
            SuccessSoft  = FromHex("#22392c");
        }
        else
        {
            Bg           = FromHex("#f4f4f6");
            Chrome       = FromHex("#ebebef");
            Sidebar      = FromHex("#f4f4f6");
            Surface      = FromHex("#ffffff");
            SurfaceAlt   = FromHex("#fafafc");
            Border       = FromHex("#e3e3e8");
            BorderStrong = FromHex("#d4d4dc");
            Text         = FromHex("#15161b");
            TextMuted    = FromHex("#62636c");
            TextDim      = FromHex("#8b8c95");
            Accent       = FromHex("#5b5bd6");
            AccentSoft   = FromHex("#e9e9fb");
            AccentText   = FromHex("#ffffff");
            Danger       = FromHex("#d4493c");
            DangerSoft   = FromHex("#fbe9e7");
            Success      = FromHex("#2f9e5f");
            SuccessSoft  = FromHex("#e3f5ea");
        }
    }

    public static readonly Theme Light = new(false);
    public static readonly Theme Dark  = new(true);

    /// <summary>Resolve "Light" | "Dark" | "System" to a palette.</summary>
    public static Theme Resolve(string name) => name switch
    {
        "Light" => Light,
        "Dark"  => Dark,
        _       => SystemIsDark() ? Dark : Light,
    };

    public static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int i && i == 0;
        }
        catch { return false; }
    }

    public static Color FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromArgb(
            Convert.ToInt32(hex[..2], 16),
            Convert.ToInt32(hex[2..4], 16),
            Convert.ToInt32(hex[4..6], 16));
    }
}
