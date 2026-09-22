Set-StrictMode -Version Latest


function Invoke-InstallerWithProgress {
    param(
        [Parameter(Mandatory = $true)][string]$Installer,
        [Parameter(Mandatory = $true)][object[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$ComponentName
    )

    # Run the installer asynchronously so the bootstrap remains visibly alive while
    # long-running installers (Visual Studio, WSL, Flutter, etc.) are working.
    $job = Start-Job -ScriptBlock {
        param($InstallerPath, $InstallerArguments)

        $output = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $InstallerPath @InstallerArguments 2>&1)
        $exitCode = $LASTEXITCODE

        [pscustomobject]@{
            Output = $output
            ExitCode = $exitCode
        }
    } -ArgumentList $Installer, (,$Arguments)

    $spinner = @("|", "/", "-", "\")
    $tick = 0
    $started = Get-Date

    try {
        while ($job.State -in @("NotStarted", "Running")) {
            $elapsed = [int]((Get-Date) - $started).TotalSeconds
            $frame = $spinner[$tick % $spinner.Count]
            Write-Host ("[$ComponentName] {0} Installation en cours... {1}s — patience..." -f $frame, $elapsed)
            $tick++
            Start-Sleep -Milliseconds 500
        }

        Write-Host ("[$ComponentName] ✓ Installation terminée." -f $ComponentName)
        
        if ($job.State -eq "Failed") {
            throw "Le processus d'installation n'a pas pu être exécuté : $($job.ChildJobs[0].JobStateInfo.Reason.Message)"
        }

        $result = Receive-Job -Job $job
        return $result
    }
    finally {
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-Component {
    param(
        [Parameter(Mandatory)]$Component,
        [Parameter(Mandatory = $true)][string]$BootstrapRoot,
        [Parameter(Mandatory = $true)][string]$RunPath,
        [Parameter(Mandatory = $true)][hashtable]$ComponentStates,
        [switch]$ForceRepair,
        [switch]$CacheOnly
    )

    . (Join-Path $PSScriptRoot "Component.ps1")
    . (Join-Path $PSScriptRoot "Logging.ps1")
    . (Join-Path $PSScriptRoot "Prerequisite.ps1")
    . (Join-Path $PSScriptRoot "Check.ps1")

    $installer = Get-ComponentInstallerPath $BootstrapRoot $Component
    if (-not $installer) {
        throw "Installateur introuvable pour '$($Component.id)' : $($Component.installer)"
    }

    $started = Get-Date
    $log = Start-ModuleLog $RunPath ("component-" + $Component.id)

    try {
        $context = @{ ComponentStates = $ComponentStates }
        $prerequisites = Test-ComponentPrerequisites -Component $Component -Context $context

        if ($prerequisites.status -ne "OK") {
            Write-Error "[$($Component.id)] BLOQUÉ : $($prerequisites.message)"
            return [pscustomobject]@{
                id = $Component.id
                status = "blocked"
                exitCode = 2
                duration = ((Get-Date) - $started).TotalSeconds
                log = $log
                error = $prerequisites.message
                prerequisite = $prerequisites.requirement
            }
        }

        $state = Get-ComponentState -Component $Component
        Write-Host "[$($Component.id)] État : $($state.state) — $($state.message)"

        if (-not $ForceRepair -and $state.state -in @("CURRENT","INSTALLED")) {
            Write-Host "[$($Component.id)] Déjà présent et conforme au contrôle local. Aucun installateur nécessaire."
            return [pscustomobject]@{
                id = $Component.id
                status = "passed"
                action = "skipped-compliant"
                exitCode = 0
                duration = ((Get-Date) - $started).TotalSeconds
                log = $log
            }
        }

        Write-Host "[$($Component.id)] DÉMARRAGE"
        Write-Host "[$($Component.id)] Installateur : $installer"

        $installerArguments = @()
        if ($Component.PSObject.Properties.Name -contains "installerParameters" -and $Component.installerParameters) {
            $installerArguments = @($Component.installerParameters | ForEach-Object { [string]$_ })
        }
        if ([IO.Path]::GetFileName($installer) -eq "Install-WingetPackage.ps1") {
            if ($Component.PSObject.Properties.Name -contains "installerType" -and $Component.installerType) { $installerArguments += @("-InstallerType", [string]$Component.installerType) }
            if ($Component.PSObject.Properties.Name -contains "locale" -and $Component.locale) { $installerArguments += @("-Locale", [string]$Component.locale) }
            if ($Component.PSObject.Properties.Name -contains "architecture" -and $Component.architecture) { $installerArguments += @("-Architecture", [string]$Component.architecture) }
            if ($Component.PSObject.Properties.Name -contains "installArguments" -and $Component.installArguments) {
                $installerArguments += "-InstallerArguments"
                $installerArguments += @($Component.installArguments | ForEach-Object { [string]$_ })
            }
            if ($CacheOnly) { $installerArguments += "-CacheOnly" }
            if ($Component.PSObject.Properties.Name -contains "officialSource" -and $Component.officialSource) {
                if ($Component.officialSource.PSObject.Properties.Name -contains "type" -and $Component.officialSource.type) {
                    $installerArguments += @("-OfficialSourceType", [string]$Component.officialSource.type)
                }
                if ($Component.officialSource.PSObject.Properties.Name -contains "repository" -and $Component.officialSource.repository) {
                    $installerArguments += @("-OfficialRepository", [string]$Component.officialSource.repository)
                }
                if ($Component.officialSource.PSObject.Properties.Name -contains "assetRegex" -and $Component.officialSource.assetRegex) {
                    $installerArguments += @("-OfficialAssetRegex", [string]$Component.officialSource.assetRegex)
                }
                if ($Component.officialSource.PSObject.Properties.Name -contains "versionRegex" -and $Component.officialSource.versionRegex) {
                    $installerArguments += @("-OfficialVersionRegex", [string]$Component.officialSource.versionRegex)
                }
            }
        }

        $installerResult = Invoke-InstallerWithProgress -Installer $installer -Arguments $installerArguments -ComponentName $Component.name
        foreach ($line in @($installerResult.Output)) {
            Write-Host ([string]$line)
        }
        $code = $installerResult.ExitCode

        if ($code -eq 10) {
            return [pscustomobject]@{
                id = $Component.id
                status = "reboot-required"
                exitCode = 10
                duration = ((Get-Date) - $started).TotalSeconds
                log = $log
                error = "Le composant nécessite un redémarrage de Windows avant de poursuivre."
            }
        }

        if ($code -ne 0) {
            return [pscustomobject]@{
                id = $Component.id
                status = "failed"
                exitCode = $code
                duration = ((Get-Date) - $started).TotalSeconds
                log = $log
                error = "L'installateur a échoué avec le code $code."
            }
        }

        # Installers can change machine/user PATH. Refresh the current process before post-install checks
        # and before the next dependent component runs (for example Git -> Flutter).
        Refresh-ProcessEnvironment

        $postCheck = Test-ComponentCheck -Component $Component
        if ($postCheck.status -ne "OK") {
            return [pscustomobject]@{
                id = $Component.id
                status = "failed"
                exitCode = 3
                duration = ((Get-Date) - $started).TotalSeconds
                log = $log
                error = "La vérification après installation a échoué : $($postCheck.message)"
            }
        }

        return [pscustomobject]@{
            id = $Component.id
            status = "passed"
            exitCode = 0
            duration = ((Get-Date) - $started).TotalSeconds
            log = $log
        }
    }
    catch {
        return [pscustomobject]@{
            id = $Component.id
            status = "failed"
            exitCode = 1
            duration = ((Get-Date) - $started).TotalSeconds
            log = $log
            error = $_.Exception.Message
        }
    }
    finally {
        Stop-ModuleLog
    }
}
