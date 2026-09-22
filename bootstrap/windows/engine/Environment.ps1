Set-StrictMode -Version Latest

function Get-DevRoot {
    return (Join-Path $env:SystemDrive "dev")
}

function Get-ProjectsRoot {
    return (Join-Path (Get-DevRoot) "Projects")
}

function Get-SdkRoot {
    return (Join-Path (Get-DevRoot) "SDK")
}

function Get-ToolsRoot {
    return (Join-Path (Get-DevRoot) "Tools")
}

function Refresh-ProcessEnvironment {
    $machine = [Environment]::GetEnvironmentVariable("Path","Machine")
    $user = [Environment]::GetEnvironmentVariable("Path","User")
    $parts = @($machine -split ';') + @($user -split ';') | Where-Object { $_ -and $_.Trim() }
    $unique = [System.Collections.Generic.List[string]]::new()
    foreach ($part in $parts) {
        if (-not ($unique -contains $part)) { [void]$unique.Add($part) }
    }
    $env:Path = $unique -join ';'
}

function Test-PendingReboot {
    $keys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"
    )
    foreach ($key in $keys) { if (Test-Path $key) { return $true } }
    return $false
}
