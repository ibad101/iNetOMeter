# Uninstall-iNetOMeter.ps1 - Per-user uninstaller for iNetOMeter
[CmdletBinding()]
param(
    [switch]$RemoveUserData
)

$AppName = "iNetOMeter"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\$AppName"

Write-Host "Uninstalling $AppName..." -ForegroundColor Cyan

# Stop running instance
Get-Process -Name $AppName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800

# Remove Start Menu shortcut
$StartMenuShortcut = Join-Path ([System.IO.Path]::Combine($env:APPDATA, "Microsoft\Windows\Start Menu\Programs")) "$AppName.lnk"
if (Test-Path $StartMenuShortcut) {
    Remove-Item -Path $StartMenuShortcut -Force -ErrorAction SilentlyContinue
}

# Remove Desktop shortcut
$DesktopShortcut = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)) "$AppName.lnk"
if (Test-Path $DesktopShortcut) {
    Remove-Item -Path $DesktopShortcut -Force -ErrorAction SilentlyContinue
}

# Remove Startup shortcut
$StartupShortcut = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)) "$AppName.lnk"
if (Test-Path $StartupShortcut) {
    Remove-Item -Path $StartupShortcut -Force -ErrorAction SilentlyContinue
}

# Remove registry uninstaller entry
$UninstallRegKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
if (Test-Path $UninstallRegKey) {
    Remove-Item -Path $UninstallRegKey -Recurse -Force -ErrorAction SilentlyContinue
}

# Remove installed program files
if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Optional user data cleanup (%LOCALAPPDATA%\iNetOMeter)
if ($RemoveUserData) {
    $DataDir = Join-Path $env:LOCALAPPDATA $AppName
    if (Test-Path $DataDir) {
        Remove-Item -Path $DataDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "User data ($DataDir) removed." -ForegroundColor Yellow
    }
} else {
    Write-Host "User settings and history preserved in %LOCALAPPDATA%\$AppName." -ForegroundColor Gray
}

Write-Host "$AppName has been uninstalled successfully." -ForegroundColor Green
