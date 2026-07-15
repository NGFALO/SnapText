using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace SnapText;

public sealed class AppSettings
{
    public const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;

    public const uint DefaultModifiers = MOD_CONTROL;
    public static readonly Keys DefaultKey = Keys.J;

    public bool Enabled          { get; set; } = true;
    public uint HotkeyModifiers  { get; set; } = DefaultModifiers;
    public Keys HotkeyKey        { get; set; } = DefaultKey;
    public bool CopyToClipboard  { get; set; } = true;
    public bool ShowNotification { get; set; } = true;
    public bool RunWithWindows   { get; set; } = false;
    public string Theme          { get; set; } = "System";
    public bool IsFirstRun       { get; set; } = true;

    [JsonIgnore]
    public string HotkeyText => FormatHotkey(HotkeyModifiers, HotkeyKey);

    public static string FormatHotkey(uint mods, Keys key)
    {
        var parts = new List<string>();
        if ((mods & MOD_WIN) != 0) parts.Add("Win");
        if ((mods & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mods & MOD_ALT) != 0) parts.Add("Alt");
        if ((mods & MOD_SHIFT) != 0) parts.Add("Shift");
        parts.Add(KeyName(key));
        return string.Join("+", parts);
    }

    public static string KeyName(Keys k) => k switch
    {
        Keys.PrintScreen => "PrtScr",
        Keys.OemSemicolon => ";", Keys.Oemcomma => ",", Keys.OemPeriod => ".",
        Keys.OemMinus => "-", Keys.Oemplus => "=",
        Keys.OemOpenBrackets => "[", Keys.OemCloseBrackets => "]",
        Keys.OemBackslash or Keys.OemPipe => "\\", Keys.OemQuestion => "/",
        Keys.Oemtilde => "`", Keys.OemQuotes => "'",
        Keys.D0 => "0", Keys.D1 => "1", Keys.D2 => "2", Keys.D3 => "3", Keys.D4 => "4",
        Keys.D5 => "5", Keys.D6 => "6", Keys.D7 => "7", Keys.D8 => "8", Keys.D9 => "9",
        _ => k.ToString(),
    };

    // ── Persistence: JSON file next to the exe (portable) ──
    public static string AppDir =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    private static string SettingsPath => Path.Combine(AppDir, "SnapText.settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOpts)
                       ?? new AppSettings();
        }
        catch { /* corrupted file -> defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try { File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts)); }
        catch { /* read-only location -> settings just won't persist */ }
    }
}

public static class StartupHelper
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void SetRunWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (enable && Environment.ProcessPath is string exe)
                key.SetValue("SnapText", $"\"{exe}\"");
            else
                key.DeleteValue("SnapText", throwOnMissingValue: false);
        }
        catch { }
    }
}
