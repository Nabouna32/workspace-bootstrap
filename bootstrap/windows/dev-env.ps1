[CmdletBinding()]
param(
    [switch]$PlanOnly,
    [switch]$PrepareCache,
    [switch]$CacheOnly,
    [string]$ExportCacheTo,
    [ValidateSet("base","development","development-extended","gaming")]
    [string]$Profile,
    [ValidateSet("baseline","optimization-plan-safe","optimization-apply-safe","optimization-rollback")]
    [string]$Command
)

$ErrorActionPreference = "Stop"

function Get-PowerShell7Path {
    $command = Get-Command pwsh.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $candidate = Join-Path $env:ProgramFiles "PowerShell\7\pwsh.exe"
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    return $null
}

$pwsh = Get-PowerShell7Path
if (-not $pwsh) {
    Write-Host "PowerShell 7 n'est pas installé. Installation depuis le cache local via l'installeur MSI officiel..." -ForegroundColor Yellow
    $bootstrapArgs = @("-NoProfile","-ExecutionPolicy","Bypass","-File",(Join-Path $PSScriptRoot "Bootstrap-PowerShell.ps1"))
    if ($CacheOnly) { $bootstrapArgs += "-CacheOnly" }
    & powershell.exe @bootstrapArgs
    if ($LASTEXITCODE -ne 0) { throw "L'installation de PowerShell 7 a échoué (code $LASTEXITCODE)." }
    $pwsh = Get-PowerShell7Path
}
if (-not $pwsh) { throw "PowerShell 7 est introuvable après son installation." }

$core = Join-Path $PSScriptRoot "dev-env-core.ps1"
if (-not (Test-Path -LiteralPath $core -PathType Leaf)) { throw "Bootstrap PowerShell 7 introuvable : $core" }

$arguments = @("-NoProfile","-ExecutionPolicy","Bypass","-File",$core)
if ($PlanOnly) { $arguments += "-PlanOnly" }
if ($PrepareCache) { $arguments += "-PrepareCache" }
if ($CacheOnly) { $arguments += "-CacheOnly" }
if ($ExportCacheTo) { $arguments += @("-ExportCacheTo",$ExportCacheTo) }
if ($Profile) { $arguments += @("-Profile",$Profile) }
if ($Command) { $arguments += @("-Command",$Command) }

& $pwsh @arguments
exit $LASTEXITCODE
