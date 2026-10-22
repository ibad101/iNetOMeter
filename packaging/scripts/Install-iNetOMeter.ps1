# Install-iNetOMeter.ps1 - Per-user installer for iNetOMeter (Zero dependencies / No admin required)
[CmdletBinding()]
param(
    [switch]$CreateDesktopShortcut,
    [switch]$StartWithWindows,
    [switch]$LaunchAfterInstall
)

$ErrorActionPreference = "Stop"

$AppName = "iNetOMeter"
$AppVersion = "1.0.0"
$Publisher = "iNetOMeter Open Source Project"

# Determine source directory (publish\win-x64 or adjacent files)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceDir = Join-Path $ScriptDir "..\..\publish\win-x64"
if (-not (Test-Path (Join-Path $SourceDir "iNetOMeter.exe"))) {
    $SourceDir = $ScriptDir
}
if (-not (Test-Path (Join-Path $SourceDir "iNetOMeter.exe"))) {
    Write-Error "Cannot locate iNetOMeter.exe in source directory '$SourceDir'."
    exit 1
}

$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\$AppName"
Write-Host "Installing $AppName v$AppVersion to: $InstallDir" -ForegroundColor Cyan

# Stop running instance if open
Get-Process -Name $AppName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# Create install directory
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Copy program files
Copy-Item -Path "$SourceDir\*" -Destination $InstallDir -Recurse -Force

# Ensure assets are present
$AssetsTarget = Join-Path $InstallDir "assets"
if (-not (Test-Path $AssetsTarget)) {
    New-Item -ItemType Directory -Path $AssetsTarget -Force | Out-Null
}
$AssetsSource = Join-Path $ScriptDir "..\..\assets"
if (Test-Path $AssetsSource) {
    Copy-Item -Path "$AssetsSource\*" -Destination $AssetsTarget -Recurse -Force
}

# Copy uninstall script into install directory
$UninstallScriptTarget = Join-Path $InstallDir "Uninstall-iNetOMeter.ps1"
$UninstallScriptSource = Join-Path $ScriptDir "Uninstall-iNetOMeter.ps1"
if (Test-Path $UninstallScriptSource) {
    Copy-Item -Path $UninstallScriptSource -Destination $UninstallScriptTarget -Force
}

$TargetExe = Join-Path $InstallDir "iNetOMeter.exe"
$AppIcon = Join-Path $InstallDir "assets\app.ico"
if (-not (Test-Path $AppIcon)) { $AppIcon = $TargetExe }

# Create Start Menu shortcut
$StartMenuDir = [System.IO.Path]::Combine($env:APPDATA, "Microsoft\Windows\Start Menu\Programs")
$StartMenuShortcut = Join-Path $StartMenuDir "$AppName.lnk"

$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($StartMenuShortcut)
$shortcut.TargetPath = $TargetExe
$shortcut.WorkingDirectory = $InstallDir
$shortcut.IconLocation = "$AppIcon,0"
$shortcut.Description = "$AppName - Taskbar network speed meter and speed test"
$shortcut.Save()
Write-Host "[OK] Start Menu shortcut created: $StartMenuShortcut" -ForegroundColor Green

# Optional Desktop shortcut
if ($CreateDesktopShortcut) {
    $DesktopDir = [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)
    $DesktopShortcut = Join-Path $DesktopDir "$AppName.lnk"
    $dShortcut = $wsh.CreateShortcut($DesktopShortcut)
    $dShortcut.TargetPath = $TargetExe
    $dShortcut.WorkingDirectory = $InstallDir
    $dShortcut.IconLocation = "$AppIcon,0"
    $dShortcut.Description = "$AppName"
    $dShortcut.Save()
    Write-Host "[OK] Desktop shortcut created: $DesktopShortcut" -ForegroundColor Green
}

# Optional Start with Windows
if ($StartWithWindows) {
    $StartupDir = [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
    $StartupShortcut = Join-Path $StartupDir "$AppName.lnk"
    $sShortcut = $wsh.CreateShortcut($StartupShortcut)
    $sShortcut.TargetPath = $TargetExe
    $sShortcut.WorkingDirectory = $InstallDir
    $sShortcut.IconLocation = "$AppIcon,0"
    $sShortcut.Description = "$AppName"
    $sShortcut.Save()
    Write-Host "[OK] Startup shortcut created: $StartupShortcut" -ForegroundColor Green
}

# Register in Windows Control Panel / Settings (Apps & Features)
$UninstallRegKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
if (-not (Test-Path $UninstallRegKey)) {
    New-Item -Path $UninstallRegKey -Force | Out-Null
}

Set-ItemProperty -Path $UninstallRegKey -Name "DisplayName" -Value $AppName
Set-ItemProperty -Path $UninstallRegKey -Name "DisplayVersion" -Value $AppVersion
Set-ItemProperty -Path $UninstallRegKey -Name "Publisher" -Value $Publisher
Set-ItemProperty -Path $UninstallRegKey -Name "DisplayIcon" -Value "$AppIcon,0"
Set-ItemProperty -Path $UninstallRegKey -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $UninstallRegKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File `"$UninstallScriptTarget`""
Set-ItemProperty -Path $UninstallRegKey -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $UninstallRegKey -Name "NoRepair" -Value 1 -Type DWord

Write-Host "[OK] Registered in Windows Apps & Features" -ForegroundColor Green
Write-Host "`n$AppName v$AppVersion installed successfully!" -ForegroundColor Green

if ($LaunchAfterInstall) {
    Start-Process -FilePath $TargetExe
}
