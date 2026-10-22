# build-package.ps1 - Master build, publish, and packaging script for iNetOMeter
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [switch]$SkipTests,
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$RootDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$PublishDir = Join-Path $RootDir "publish\win-x64"
$DistDir = Join-Path $RootDir "dist"
$AssetsDir = Join-Path $RootDir "assets"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  iNetOMeter Release Packaging Script (v$Version)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# Stop any running instances to avoid file locks
Get-Process -Name "iNetOMeter" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 1. Run Automated Tests
if (-not $SkipTests) {
    Write-Host "`n[1/5] Running automated tests..." -ForegroundColor Yellow
    $testResult = & dotnet run --project (Join-Path $RootDir "tests\iNetOMeter.Tests.csproj") -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Automated test suite failed with exit code $LASTEXITCODE. Aborting packaging."
        exit $LASTEXITCODE
    }
    Write-Host "[OK] All automated tests passed." -ForegroundColor Green
} else {
    Write-Host "`n[1/5] Skipping tests as requested." -ForegroundColor DarkGray
}

# 2. Clean previous publish & dist directories
Write-Host "`n[2/5] Cleaning output directories..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item -Path $PublishDir -Recurse -Force }
if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir -Force | Out-Null }

# 3. Publish win-x64 binary
if ($SelfContained) {
    Write-Host "`n[3/5] Publishing self-contained win-x64 binary..." -ForegroundColor Yellow
    $publishArgs = @(
        "publish",
        (Join-Path $RootDir "src\iNetOMeter.csproj"),
        "-c", $Configuration,
        "-r", "win-x64",
        "--self-contained", "true",
        "-o", $PublishDir,
        "/p:PublishTrimmed=false"
    )
} else {
    Write-Host "`n[3/5] Publishing lightweight framework-dependent win-x64 binary..." -ForegroundColor Yellow
    $publishArgs = @(
        "publish",
        (Join-Path $RootDir "src\iNetOMeter.csproj"),
        "-c", $Configuration,
        "-r", "win-x64",
        "--self-contained", "false",
        "-o", $PublishDir
    )
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# Remove any PDB debug symbols from publish directory
Get-ChildItem -Path $PublishDir -Filter "*.pdb" -Recurse | Remove-Item -Force

# Copy documentation and assets into publish directory
Copy-Item (Join-Path $RootDir "LICENSE") -Destination $PublishDir -Force
Copy-Item (Join-Path $RootDir "THIRD-PARTY-NOTICES.md") -Destination $PublishDir -Force
Copy-Item (Join-Path $RootDir "QUICKSTART.md") -Destination $PublishDir -Force
if (Test-Path (Join-Path $RootDir "README.md")) { Copy-Item (Join-Path $RootDir "README.md") -Destination $PublishDir -Force }
if (Test-Path (Join-Path $RootDir "CHANGELOG.md")) { Copy-Item (Join-Path $RootDir "CHANGELOG.md") -Destination $PublishDir -Force }

$publishAssets = Join-Path $PublishDir "assets"
if (-not (Test-Path $publishAssets)) { New-Item -ItemType Directory -Path $publishAssets -Force | Out-Null }
Copy-Item (Join-Path $AssetsDir "app.ico") -Destination $publishAssets -Force
Copy-Item (Join-Path $AssetsDir "logo.svg") -Destination $publishAssets -Force

# Copy installer scripts into publish directory for standalone setup convenience
Copy-Item (Join-Path $PSScriptRoot "scripts\Install-iNetOMeter.ps1") -Destination $PublishDir -Force
Copy-Item (Join-Path $PSScriptRoot "scripts\Uninstall-iNetOMeter.ps1") -Destination $PublishDir -Force

Write-Host "[OK] Publish completed." -ForegroundColor Green

# 4. Create Versioned Portable ZIP
Write-Host "`n[4/5] Creating portable ZIP archive..." -ForegroundColor Yellow
$zipFileName = "iNetOMeter-$Version-win-x64-portable.zip"
$zipPath = Join-Path $DistDir $zipFileName

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($PublishDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$zipSizeMb = [Math]::Round(((Get-Item $zipPath).Length / 1MB), 2)
Write-Host "[OK] Created portable ZIP: $zipPath ($zipSizeMb MB)" -ForegroundColor Green

# 5. Check for Inno Setup compiler (ISCC.exe)
Write-Host "`n[5/5] Checking for Inno Setup installer compiler..." -ForegroundColor Yellow
$isccPath = $null
$isccCandidates = @(
    "iscc",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
)

foreach ($c in $isccCandidates) {
    if (Get-Command $c -ErrorAction SilentlyContinue) {
        $isccPath = $c
        break
    }
    if (Test-Path $c) {
        $isccPath = $c
        break
    }
}

if ($isccPath) {
    Write-Host "Compiling Windows installer using Inno Setup: $isccPath..." -ForegroundColor Cyan
    $issFile = Join-Path $PSScriptRoot "inno-setup\iNetOMeter.iss"
    & $isccPath $issFile
    if ($LASTEXITCODE -eq 0) {
        $installerPath = Join-Path $DistDir "iNetOMeter-$Version-win-x64-Setup.exe"
        if (Test-Path $installerPath) {
            $installerSizeMb = [Math]::Round(((Get-Item $installerPath).Length / 1MB), 2)
            Write-Host "[OK] Windows Installer compiled: $installerPath ($installerSizeMb MB)" -ForegroundColor Green
        }
    }
} else {
    Write-Host "[INFO] Inno Setup compiler (ISCC.exe) is not installed." -ForegroundColor Yellow
    Write-Host "       The installer script is ready at: packaging\inno-setup\iNetOMeter.iss" -ForegroundColor Gray
    Write-Host "       To build the standalone .exe installer: install Inno Setup (winget install jrsoftware.InnoSetup)" -ForegroundColor Gray
    Write-Host "       Alternatively, use the included packaging\scripts\Install-iNetOMeter.ps1 for zero-dependency installation." -ForegroundColor Gray
}

# Archive Content Verification
Write-Host "`n==================================================" -ForegroundColor Cyan
Write-Host "  Archive Content Verification" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$zipArchive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
$entryNames = $zipArchive.Entries | ForEach-Object { $_.FullName }

$requiredEntries = @(
    "iNetOMeter.exe",
    "iNetOMeter.dll",
    "LICENSE",
    "THIRD-PARTY-NOTICES.md",
    "QUICKSTART.md",
    "README.md",
    "CHANGELOG.md",
    "assets/app.ico",
    "Install-iNetOMeter.ps1",
    "Uninstall-iNetOMeter.ps1"
)

$hasMissing = $false
foreach ($req in $requiredEntries) {
    $matched = $entryNames -contains $req -or ($entryNames -contains ($req.Replace('/', '\')))
    if ($matched) {
        Write-Host " [PASS] $req present in ZIP" -ForegroundColor Green
    } else {
        Write-Host " [FAIL] Missing required entry: $req" -ForegroundColor Red
        $hasMissing = $true
    }
}

# Verify no PDBs or personal data
$hasPdbs = ($entryNames | Where-Object { $_ -like "*.pdb" }).Count -gt 0
$hasUserData = ($entryNames | Where-Object { $_ -like "*history.json*" -or $_ -like "*settings.json*" }).Count -gt 0

if (-not $hasPdbs) {
    Write-Host " [PASS] Zero .pdb debug symbol files in release package" -ForegroundColor Green
} else {
    Write-Host " [FAIL] .pdb files detected in package" -ForegroundColor Red
}

if (-not $hasUserData) {
    Write-Host " [PASS] Zero user data/history/settings files in release package" -ForegroundColor Green
} else {
    Write-Host " [FAIL] User data files detected in package" -ForegroundColor Red
}

$zipArchive.Dispose()

Write-Host "`nRelease packaging completed successfully!" -ForegroundColor Green
Write-Host "Artifact: $zipPath ($zipSizeMb MB)" -ForegroundColor Cyan
