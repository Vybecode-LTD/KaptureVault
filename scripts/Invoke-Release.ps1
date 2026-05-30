# KaptureVault Release Script
# Invoked by Claude when the user says "release it"
#
# Usage:
#   .\scripts\Invoke-Release.ps1 -BumpType minor    # 1.0.0 -> 1.0.1  (default)
#   .\scripts\Invoke-Release.ps1 -BumpType major    # 1.0.0 -> 1.1.0
#
# Flags: -SkipGitHub  skip gh release create
#        -SkipPush    skip git push

param(
    [ValidateSet("minor", "major")]
    [string]$BumpType = "minor",
    [switch]$SkipGitHub,
    [switch]$SkipPush
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot | Split-Path -Parent

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
Write-Host "[1/6] Updating version to $NewVersion..." -ForegroundColor Yellow

$CsprojContent = $CsprojContent -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$NewVersion</Version>"
Set-Content $CsprojPath $CsprojContent -Encoding utf8

$IssContent = Get-Content $IssPath -Raw
$IssContent = $IssContent -replace '#define MyAppVersion\s+"[\d\.]+"', "#define MyAppVersion `"$NewVersion`""
Set-Content $IssPath $IssContent -Encoding utf8

Write-Host "    .csproj and installer/.iss updated." -ForegroundColor Green

# 4. dotnet publish
Write-Host "[2/6] Publishing release build..." -ForegroundColor Yellow

Push-Location $Root
try {
    dotnet publish KaptureVault.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish/win-x64" --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
} finally {
    Pop-Location
}
Write-Host "    Publish complete." -ForegroundColor Green

# 5. Inno Setup
Write-Host "[3/6] Building installer..." -ForegroundColor Yellow

$ISCC = "C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $ISCC)) { throw "Inno Setup not found at: $ISCC" }

& $ISCC (Join-Path $Root "installer\KaptureVaultSetup.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed (exit $LASTEXITCODE)" }

$InstallerName = "KaptureVaultSetup-$NewVersion-x64.exe"
$InstallerSrc  = Join-Path $Root "installer\output\$InstallerName"
if (-not (Test-Path $InstallerSrc)) { throw "Installer not found: $InstallerSrc" }
Write-Host "    Installer built: $InstallerName" -ForegroundColor Green

# 6. Copy to releases/latest/
Write-Host "[4/6] Copying to releases/latest/..." -ForegroundColor Yellow

$ReleasesDir   = Join-Path $Root "releases\latest"
$InstallerDest = Join-Path $ReleasesDir $InstallerName

if (-not (Test-Path $ReleasesDir)) { New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null }
Get-ChildItem $ReleasesDir -Filter "*.exe" | Remove-Item -Force -ErrorAction SilentlyContinue
Copy-Item $InstallerSrc $InstallerDest -Force

Write-Host "    Copied to releases/latest/$InstallerName" -ForegroundColor Green

# 7. Git commit + tag + push
Write-Host "[5/6] Committing and tagging..." -ForegroundColor Yellow

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

# 8. GitHub Release
if (-not $SkipGitHub) {
    Write-Host "[6/6] Creating GitHub release..." -ForegroundColor Yellow
    gh release create "v$NewVersion" $InstallerDest --title "KaptureVault v$NewVersion" --notes "KaptureVault v$NewVersion" --latest
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed (exit $LASTEXITCODE)" }
    Write-Host "    GitHub release v$NewVersion created." -ForegroundColor Green
} else {
    Write-Host "[6/6] Skipped GitHub release creation (-SkipGitHub)." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "----------------------------------------------------" -ForegroundColor DarkGray
Write-Host "Release v$NewVersion complete!" -ForegroundColor Green
Write-Host "  Installer : releases/latest/$InstallerName" -ForegroundColor White
Write-Host "  GitHub    : https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v$NewVersion" -ForegroundColor White
Write-Host ""
