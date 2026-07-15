# ⚡ SnapText

**Grab text from anywhere on your screen.** Press a hotkey, the screen freezes,
drag a box around the text — SnapText extracts it with OCR and copies it to your
clipboard. Fast, portable, open source.

![MIT License](https://img.shields.io/badge/license-MIT-blue) ![Windows 10/11](https://img.shields.io/badge/platform-Windows%2010%2F11-0078d4)

## Features

- **Ctrl + J** (configurable) freezes the screen for region selection — works from any app
- Text extraction via the **OCR engine built into Windows** — no downloads, no network calls, no telemetry
- Extracted text is **auto-copied to the clipboard** and kept in a searchable history
- Screenshots + text saved to **`Pictures\SnapText`** (PNG + TXT pairs — plain files you own)
- Runs quietly in the **system tray**; optional start with Windows
- **Light / Dark / System** theme
- **Portable**: one exe, one folder, no installation. Settings live in
  `SnapText.settings.json` next to the exe — zip the folder and share it.

## Getting started

1. Grab the `SnapText` folder (or build it — see below) and run `SnapText.exe`.
2. Press **Ctrl + J**, drag over some text, release. Done — the text is on your clipboard.
3. Double-click the tray icon to browse your capture history or change settings.

No admin rights, no installer, no .NET runtime needed (it's self-contained).

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows 10/11.

```powershell
.\build.ps1          # → dist\SnapText\SnapText.exe (portable single file)
```

or manually:

```powershell
dotnet publish src/SnapText/SnapText.csproj -c Release -o dist/SnapText
```

## Where things live

| What | Where |
|---|---|
| App + settings | the folder you put `SnapText.exe` in |
| Screenshots + extracted text | `%USERPROFILE%\Pictures\SnapText\` |
| Custom tray/app icon (optional) | drop an `icon.ico` next to `SnapText.exe` |

## Notes

- OCR languages follow your Windows language packs (Settings → Time & Language).
- Everything runs locally; SnapText makes no network connections.

## License

[MIT](LICENSE)
