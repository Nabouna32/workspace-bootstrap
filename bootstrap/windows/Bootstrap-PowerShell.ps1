param([switch]$CacheOnly)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Cache.ps1")

$component = [pscustomobject]@{
    name = "PowerShell 7"
    packageId = "Microsoft.PowerShell"
    installerType = "wix"
    locale = "fr-FR"
    architecture = "x64"
    officialSource = [pscustomobject]@{
        type = "github-release"
        repository = "PowerShell/PowerShell"
        assetRegex = "^PowerShell-[0-9.]+-win-x64\.msi$"
        versionRegex = "^v(.+)$"
    }
}

$metadata = Get-CachedInstaller -Component $component -PackageId $component.packageId -Version $null -InstallerType $component.installerType -Locale $component.locale -Architecture $component.architecture -DownloadIfMissing:(-not $CacheOnly) -CacheOnly:$CacheOnly
if (-not $metadata) { throw "Aucun installateur PowerShell vérifié n'est disponible dans le cache." }

$arguments = @("/quiet","ADD_EXPLORER_CONTEXT_MENU_OPENPOWERSHELL=1","ADD_FILE_CONTEXT_MENU_RUNPOWERSHELL=1","ENABLE_PSREMOTING=1","REGISTER_MANIFEST=1","USE_MU=1","ENABLE_MU=1","ADD_PATH=1")
$code = Invoke-CachedInstaller -Metadata $metadata -Arguments $arguments -ComponentName "PowerShell 7"
if ($code -ne 0) { throw "L'installation de PowerShell 7 a échoué avec le code $code." }

$pwsh = Join-Path $env:ProgramFiles "PowerShell\7\pwsh.exe"
if (-not (Test-Path -LiteralPath $pwsh -PathType Leaf)) { throw "PowerShell 7 n'est pas présent après l'installation." }
Write-Host "PowerShell 7 $($metadata.version) installé depuis le cache officiel." -ForegroundColor Green
