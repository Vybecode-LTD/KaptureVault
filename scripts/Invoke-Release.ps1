# KaptureVault Release Script
# Invoked by Claude when the user says "release it"
#
# Usage:
#   .\scripts\Invoke-Release.ps1 -BumpType minor    # 1.0.0 -> 1.0.1  (default)
#   .\scripts\Invoke-Release.ps1 -BumpType major    # 1.0.0 -> 1.1.0
#
# Flags: -SkipPush    commit + tag locally but don't push (no release is created)
#
# This script does NOT create the GitHub Release. It builds + packages the installer,
# bumps the version, updates CHANGELOG.md, commits, and pushes. The push of the new
# releases/latest/*.exe triggers .github/workflows/auto-release.yml, which is the SINGLE
# release creator: it VirusTotal-scans the installer and creates the GitHub Release (with
# the VT link in the notes). The kapture.tools website then reads the latest release and
# CHANGELOG.md live from GitHub (download.js / changelog.js) — nothing is pushed to it.

param(
    [ValidateSet("minor", "major")]
    [string]$BumpType = "minor",
    [switch]$SkipPush
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot | Split-Path -Parent

# ── Code signing (Azure Trusted Signing via signtool + the Microsoft.Trusted.Signing.Client dlib) ──
# Account/profile live in scripts\trusted-signing.json (gitignored — copy from .example.json). Auth is
# DefaultAzureCredential: `az login` (or AZURE_TENANT_ID/CLIENT_ID/CLIENT_SECRET) with the account's
# "Trusted Signing Certificate Profile Signer" role. Signs + verifies; throws on any failure.
$SignMeta = Join-Path $Root "scripts\trusted-signing.json"

function Invoke-Sign($Path) {
    if (-not (Test-Path $SignMeta)) {
        throw "Signing metadata not found: $SignMeta`n  Copy scripts\trusted-signing.example.json -> scripts\trusted-signing.json and fill in your Trusted Signing endpoint/account/profile."
    }
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $signtool) { throw "signtool.exe not found - install the Windows 10/11 SDK." }
    $dlib = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.trusted.signing.client" -Recurse -Filter Azure.CodeSigning.Dlib.dll -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $dlib) { throw "Trusted Signing dlib not found - install the Microsoft.Trusted.Signing.Client NuGet package." }

    Write-Host "    Signing $(Split-Path $Path -Leaf)..." -ForegroundColor DarkGray
    & $signtool sign /v /fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256 /dlib $dlib /dmdf $SignMeta $Path
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $Path (exit $LASTEXITCODE)" }
    & $signtool verify /pa $Path
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $Path (exit $LASTEXITCODE)" }
}

# 1. Read current version
$CsprojPath = Join-Path $Root "KaptureVault.csproj"
$IssPath    = Join-Path $Root "installer\KaptureVaultSetup.iss"

$CsprojContent = Get-Content $CsprojPath -Raw
if ($CsprojContent -notmatch '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    throw "Could not find <Version>X.Y.Z</Version> in $CsprojPath"
}
$VerMajor = [int]$Matches[1]
$VerMinor = [int]$Matches[2]
$VerPatch = [int]$Matches[3]

# 2. Increment version
if ($BumpType -eq "major") {
    $VerMinor++
    $VerPatch = 0
} else {
    $VerPatch++
}
$NewVersion = "$VerMajor.$VerMinor.$VerPatch"

Write-Host ""
Write-Host "KaptureVault Release v$NewVersion ($BumpType)" -ForegroundColor Cyan
Write-Host "----------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# 3. Update .csproj and .iss
Write-Host "[1/5] Updating version to $NewVersion..." -ForegroundColor Yellow

$CsprojContent = $CsprojContent -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$NewVersion</Version>"
Set-Content $CsprojPath $CsprojContent -Encoding utf8

$IssContent = Get-Content $IssPath -Raw
$IssContent = $IssContent -replace '#define MyAppVersion\s+"[\d\.]+"', "#define MyAppVersion `"$NewVersion`""
Set-Content $IssPath $IssContent -Encoding utf8

Write-Host "    .csproj and installer/.iss updated." -ForegroundColor Green

# 4. dotnet publish
Write-Host "[2/5] Publishing release build..." -ForegroundColor Yellow

Push-Location $Root
try {
    dotnet publish KaptureVault.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish/win-x64" --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
} finally {
    Pop-Location
}
Write-Host "    Publish complete." -ForegroundColor Green

# Sign the published single-file exe BEFORE Inno Setup packages it (so the installed app is signed too).
Invoke-Sign (Join-Path $Root "publish\win-x64\KaptureVault.exe")
Write-Host "    Published exe signed." -ForegroundColor Green

# 5. Inno Setup
Write-Host "[3/5] Building installer..." -ForegroundColor Yellow

$ISCC = "C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $ISCC)) { throw "Inno Setup not found at: $ISCC" }

& $ISCC (Join-Path $Root "installer\KaptureVaultSetup.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed (exit $LASTEXITCODE)" }

$InstallerName = "KaptureVaultSetup-$NewVersion-x64.exe"
$InstallerSrc  = Join-Path $Root "installer\output\$InstallerName"
if (-not (Test-Path $InstallerSrc)) { throw "Installer not found: $InstallerSrc" }
Write-Host "    Installer built: $InstallerName" -ForegroundColor Green

# Sign the installer itself (this is the file users download — Authenticode + SmartScreen reputation).
Invoke-Sign $InstallerSrc
Write-Host "    Installer signed." -ForegroundColor Green

# 6. Copy to releases/latest/
Write-Host "[4/5] Copying to releases/latest/..." -ForegroundColor Yellow

$ReleasesDir   = Join-Path $Root "releases\latest"
$InstallerDest = Join-Path $ReleasesDir $InstallerName

if (-not (Test-Path $ReleasesDir)) { New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null }
Get-ChildItem $ReleasesDir -Filter "*.exe" | Remove-Item -Force -ErrorAction SilentlyContinue
Copy-Item $InstallerSrc $InstallerDest -Force

Write-Host "    Copied to releases/latest/$InstallerName" -ForegroundColor Green

# 7. Git commit + tag + push
Write-Host "[5/5] Committing, tagging, and pushing..." -ForegroundColor Yellow

Push-Location $Root
try {
    $changelog = Join-Path $Root "CHANGELOG.md"
    if (Test-Path $changelog) { git add "CHANGELOG.md" }
    git add "KaptureVault.csproj" "installer/KaptureVaultSetup.iss" "releases/latest/"
    git commit -m "release: v$NewVersion"
    git tag "v$NewVersion"

    if (-not $SkipPush) {
        git push
        git push --tags
        Write-Host "    Pushed to origin." -ForegroundColor Green
    } else {
        Write-Host "    Committed and tagged locally (push skipped)." -ForegroundColor Yellow
    }
} finally {
    Pop-Location
}

# The GitHub Release is created by .github/workflows/auto-release.yml, which triggers
# on the push of releases/latest/*.exe above. No `gh release create` here — that would
# race the workflow and pre-empt its VirusTotal scan + richer release notes.

Write-Host ""
Write-Host "----------------------------------------------------" -ForegroundColor DarkGray
if ($SkipPush) {
    Write-Host "v$NewVersion built + committed locally (push skipped)." -ForegroundColor Yellow
    Write-Host "  Installer : releases/latest/$InstallerName" -ForegroundColor White
    Write-Host "  No release will be created until you push (the workflow triggers on the installer push)." -ForegroundColor White
} else {
    Write-Host "v$NewVersion pushed. auto-release.yml will VirusTotal-scan + create the GitHub Release." -ForegroundColor Green
    Write-Host "  Installer : releases/latest/$InstallerName" -ForegroundColor White
    Write-Host "  Release   : https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v$NewVersion  (created by CI, ~30s)" -ForegroundColor White
    Write-Host "  Actions   : https://github.com/Vybecode-LTD/KaptureVault/actions" -ForegroundColor White
}
Write-Host ""
