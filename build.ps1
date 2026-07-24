# Builds the portable SnapText release into dist\SnapText (zip that folder to share).
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet publish src/SnapText/SnapText.csproj -c Release -o dist/SnapText
Copy-Item README.md, LICENSE, src/SnapText/icon.ico dist/SnapText/

Write-Host ""
Write-Host "Done -> dist\SnapText" -ForegroundColor Green
Get-ChildItem dist/SnapText | Format-Table Name, @{n = "Size (MB)"; e = { [math]::Round($_.Length / 1MB, 1) } }
