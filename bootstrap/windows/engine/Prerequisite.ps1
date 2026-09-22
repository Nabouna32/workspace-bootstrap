Set-StrictMode -Version Latest

function Ensure-BootstrapPrerequisites {
    $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){
        throw "Administrator privileges are required to provision this machine."
    }

    $os = Get-CimInstance Win32_OperatingSystem
    if([int]$os.BuildNumber -lt 22000){
        throw "Windows 11 is required. Detected build $($os.BuildNumber)."
    }

    if(-not (Get-Command winget -ErrorAction SilentlyContinue)){
        Write-Host "WinGet is not available. Attempting to register Microsoft App Installer..." -ForegroundColor Yellow
        try {
            Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe -ErrorAction Stop
        } catch {
            Write-Warning "Automatic WinGet registration failed: $($_.Exception.Message)"
        }

        $machinePath=[Environment]::GetEnvironmentVariable("Path","Machine")
        $userPath=[Environment]::GetEnvironmentVariable("Path","User")
        $env:Path=((@($machinePath -split ';')+@($userPath -split ';'))|Where-Object{$_ -and $_.Trim()}|Select-Object -Unique)-join ';'
    }

    if(-not (Get-Command winget -ErrorAction SilentlyContinue)){
        throw "WinGet is unavailable. Microsoft App Installer must be installed/registered before provisioning can continue."
    }

    Write-Host "Bootstrap prerequisites: administrator + Windows 11 + WinGet = OK."
}

function Test-Prerequisite {
    param(
        [Parameter(Mandatory=$true)]$Requirement,
        [Parameter(Mandatory=$true)][hashtable]$Context
    )

    $type = [string]$Requirement.type
    switch ($type) {
        "admin" {
            $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
            return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        }
        "windows-build" {
            $os = Get-CimInstance Win32_OperatingSystem
            return ([int]$os.BuildNumber -ge [int]$Requirement.minimum)
        }
        "command" {
            return [bool](Get-Command ([string]$Requirement.name) -ErrorAction SilentlyContinue)
        }
        "component" {
            return ($Context.ComponentStates[[string]$Requirement.name] -eq "OK")
        }
        "file" {
            return (Test-Path -LiteralPath ([string]$Requirement.path))
        }
        default {
            throw "Unknown prerequisite type '$type'."
        }
    }
}

function Get-PrerequisiteFailureMessage {
    param([Parameter(Mandatory=$true)]$Requirement)
    $type=[string]$Requirement.type
    switch ($type) {
        "admin" { return "Administrator privileges are required." }
        "windows-build" { return "Windows build $($Requirement.minimum) or newer is required." }
        "command" { return "Required command '$($Requirement.name)' is not available." }
        "component" { return "Required component '$($Requirement.name)' is not compliant." }
        "file" { return "Required file '$($Requirement.path)' was not found." }
        default { return "Unknown prerequisite type '$type'." }
    }
}

function Test-ComponentPrerequisites {
    param(
        [Parameter(Mandatory=$true)]$Component,
        [Parameter(Mandatory=$true)][hashtable]$Context
    )
    foreach ($requirement in @(if($Component.PSObject.Properties.Name -contains "prerequisites"){$Component.prerequisites}else{@()})) {
        if (-not (Test-Prerequisite $requirement $Context)) {
            return [pscustomobject]@{
                status="BLOCKED"
                requirement=$requirement
                message=(Get-PrerequisiteFailureMessage $requirement)
            }
        }
    }
    return [pscustomobject]@{ status="OK"; requirement=$null; message=$null }
}
