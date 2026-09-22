$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$cachePath = Join-Path $root "bootstrap\windows\Cache.ps1"
$installerPath = Join-Path $root "bootstrap\windows\Install-WingetPackage.ps1"
$launcherPath = Join-Path $root "bootstrap\windows\dev-env.ps1"
$corePath = Join-Path $root "bootstrap\windows\dev-env-core.ps1"

foreach ($path in @($cachePath, $installerPath, $launcherPath, $corePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing installer-cache contract file: $path"
    }
}

$cache = Get-Content -LiteralPath $cachePath -Raw
$installer = Get-Content -LiteralPath $installerPath -Raw
$launcher = Get-Content -LiteralPath $launcherPath -Raw
$core = Get-Content -LiteralPath $corePath -Raw

$requiredCacheContracts = @(
    "function Get-LatestCachedMetadata",
    "[switch]$CacheOnly",
    "relativePath",
    "Test-OfficialInstallerSignature",
    "Get-MsiProductVersion",
    "Save-CachedPackageMetadata",
    "function Test-CacheSourceProvenance",
    "sourceDefinition"
)

foreach ($contract in $requiredCacheContracts) {
    if ($cache -notmatch [regex]::Escape($contract)) {
        throw "Installer cache contract missing: $contract"
    }
}

if ($installer -notmatch [regex]::Escape("[switch]$CacheOnly")) {
    throw "Install-WingetPackage.ps1 must expose explicit cache-only mode."
}


foreach ($contract in @("-CacheOnly", "-ExportCacheTo")) {
    if ($launcher -notmatch [regex]::Escape($contract)) {
        throw "Windows launcher does not forward $contract."
    }
}

if ($core -notmatch [regex]::Escape("[switch]$CacheOnly")) {
    throw "Core bootstrap must expose cache-only mode."
}
if ($core -match "BounaDevEnvironment|BOUNA-PC") {
    throw "Legacy machine-specific state or identity remains in the Windows core bootstrap."
}

$componentFiles = Get-ChildItem (Join-Path $root "bootstrap\windows\components") -Recurse -Filter "component.json" -File
$officialCount = 0
foreach ($file in $componentFiles) {
    $component = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    if ($component.PSObject.Properties.Name -contains "officialSource" -and $component.officialSource) {
        $officialCount++
        if (-not $component.officialSource.type) {
            throw "Official source type missing: $($file.FullName)"
        }
    }
}

if ($officialCount -lt 5) {
    throw "Expected at least five components with declared official installer sources; found $officialCount."
}

Write-Host "Installer cache contract validation: OK ($officialCount official-source components)."
