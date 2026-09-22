[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$checkPath = Join-Path $root "bootstrap\windows\engine\Check.ps1"
. $checkPath

function Assert-State {
    param(
        [string]$Expected,
        [string]$Actual,
        [string]$Description
    )
    if ($Expected -ne $Actual) {
        throw ("{0}: expected '{1}', got '{2}'." -f $Description,$Expected,$Actual)
    }
}

$script:mockStatus = "OK"
$script:mockScope = "baseline"
$script:mockMessage = "mock"

function Test-ComponentCheck {
    param([Parameter(Mandatory=$true)]$Component)
    [pscustomobject]@{
        status = $script:mockStatus
        scope = $script:mockScope
        message = $script:mockMessage
    }
}

$component = [pscustomobject]@{
    id = "mock"
    versionPolicy = "latest-stable"
    installer = "mock.ps1"
}

$script:mockStatus = "MISSING"
$script:mockScope = "baseline"
Assert-State "MISSING" (Get-ComponentState $component).state "Missing component state"

$script:mockStatus = "OUTDATED"
Assert-State "OUTDATED" (Get-ComponentState $component).state "Outdated component state"

$script:mockStatus = "FAILED"
Assert-State "REPAIRABLE" (Get-ComponentState $component).state "Failed-but-repairable component state"

$script:mockStatus = "FAILED"
$script:mockScope = "configuration"
Assert-State "CONFIG-INCOMPLETE" (Get-ComponentState $component).state "Configuration-incomplete state"

$script:mockStatus = "OK"
$script:mockScope = "baseline"
Assert-State "INSTALLED" (Get-ComponentState $component).state "Latest-stable installed state"

$component = [pscustomobject]@{
    id = "mock"
    versionPolicy = "latest-stable"
    installer = "mock.ps1"
    packageManager = "winget"
    packageId = "Example.Package"
}

function Test-WingetPackageUpdate {
    param([string]$PackageId)
    [pscustomobject]@{
        available = $true
        updateAvailable = $false
        message = "Aucune mise à jour WinGet disponible."
    }
}
Assert-State "CURRENT" (Get-ComponentState $component).state "WinGet latest-stable current state"

function Test-WingetPackageUpdate {
    param([string]$PackageId)
    [pscustomobject]@{
        available = $true
        updateAvailable = $true
        installedVersion = "1.0.0"
        availableVersion = "2.0.0"
        message = "Mise à jour disponible : 1.0.0 → 2.0.0."
    }
}
Assert-State "OUTDATED" (Get-ComponentState $component).state "WinGet update-available state"

function Test-WingetPackageUpdate {
    param([string]$PackageId)
    [pscustomobject]@{
        available = $false
        updateAvailable = $false
        message = "Vérification WinGet indisponible."
    }
}
Assert-State "INSTALLED" (Get-ComponentState $component).state "WinGet unavailable state"

$component.versionPolicy = "minimum"
Assert-State "CURRENT" (Get-ComponentState $component).state "Minimum-policy current state"

$script:mockStatus = "UNKNOWN"
Assert-State "FAILED" (Get-ComponentState $component).state "Unknown state fallback"

Write-Host "Windows component state validation: PASS"
exit 0
