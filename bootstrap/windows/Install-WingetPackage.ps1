param(
    [object]$Component,
    [string]$PackageId,
    [string]$InstallerType,
    [string]$Locale = "fr-FR",
    [string]$Architecture = "x64",
    [string[]]$InstallerArguments = @(),
    [string]$OfficialRepository,
    [string]$OfficialAssetRegex,
    [string]$OfficialVersionRegex,
    [string]$OfficialSourceType,
    [switch]$CacheOnly
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Cache.ps1")

if (-not $Component) { $Component = [pscustomobject]@{ name = $PackageId; packageId = $PackageId } }
if (-not $PackageId) { $PackageId = [string]$Component.packageId }

if ($OfficialRepository -and $OfficialAssetRegex) {
    $Component.officialSource = [pscustomobject]@{
        type = "github-release"
        repository = $OfficialRepository
        assetRegex = $OfficialAssetRegex
        versionRegex = $OfficialVersionRegex
    }
} elseif ($OfficialSourceType -and -not ($Component.PSObject.Properties.Name -contains "officialSource")) {
    $Component.officialSource = [pscustomobject]@{ type = $OfficialSourceType }
}

$metadata = Get-CachedInstaller -Component $Component -PackageId $PackageId -Version $null -InstallerType $InstallerType -Locale $Locale -Architecture $Architecture -DownloadIfMissing:(-not $CacheOnly) -CacheOnly:$CacheOnly

if (-not $metadata) {
    if ($CacheOnly) {
        throw "Aucun installateur vérifié n'est disponible dans le cache pour '$PackageId' ($Locale/$Architecture)."
    }
    throw "Aucun installateur officiel ou cache valide n'est disponible pour '$PackageId'."
}

if ($InstallerArguments.Count -eq 0 -and $Component.PSObject.Properties.Name -contains "installArguments") {
    $InstallerArguments = @($Component.installArguments | ForEach-Object { [string]$_ })
}

$code = Invoke-CachedInstaller -Metadata $metadata -Arguments $InstallerArguments -ComponentName ([string]$Component.name)
if ($code -ne 0) { throw "L'installateur de '$($Component.name)' a échoué avec le code $code." }
exit 0
