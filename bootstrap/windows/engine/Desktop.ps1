Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-DesktopCommandResponse {
    param(
        [Parameter(Mandatory)][string]$Command,
        [Parameter(Mandatory)][bool]$Success,
        [Parameter(Mandatory)][int]$ExitCode,
        $Data = $null,
        [string[]]$Messages = @(),
        [string]$ErrorMessage = $null
    )

    [pscustomobject]@{
        schemaVersion = 1
        command = $Command
        success = $Success
        exitCode = $ExitCode
        timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        data = $Data
        messages = @($Messages)
        error = $ErrorMessage
    } | ConvertTo-Json -Depth 12 -Compress
}

function Test-DesktopOperationWorkerAlive {
    param([Parameter(Mandatory)]$State)

    if (-not $State.workerPid) { return $false }

    try {
        $process = Get-Process -Id ([int]$State.workerPid) -ErrorAction Stop
        return -not $process.HasExited
    } catch {
        return $false
    }
}

function Set-DesktopOperationInterruptedIfNeeded {
    param([Parameter(Mandatory)]$State)

    if ([string]$State.status -notin @("starting","running")) { return $State }
    if (-not $State.workerPid) { return $State }
    if (Test-DesktopOperationWorkerAlive -State $State) { return $State }

    $State.status = "failed"
    $State.phase = "error"
    $State.error = "The provisioning worker stopped before the operation completed."
    $State.messageKey = "operation.interrupted"
    $State.canResume = $true
    $State.workerPid = $null
    Write-DesktopOperationState -Id ([string]$State.operationId) -State $State
    return $State
}

function Start-DesktopProvisioningWorker {
    param([Parameter(Mandatory)][string]$OperationId, [Parameter(Mandatory)][string]$ProfileId)

    $core = Join-Path $Root "dev-env-core.ps1"
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $core + '" -Profile "' + $ProfileId + '" -OperationWorker -OperationId "' + $OperationId + '"'
    $process = Start-Process -FilePath "powershell.exe" -ArgumentList $arguments -WorkingDirectory $Root -WindowStyle Hidden -PassThru

    Start-Sleep -Milliseconds 100
    $state = Read-DesktopOperationState $OperationId
    if ($state -and [string]$state.status -in @("starting","running") -and -not $state.workerPid) {
        $state.workerPid = $process.Id
        Write-DesktopOperationState -Id $OperationId -State $state
    }
}

function Get-DesktopCapabilities {
    $capabilities = [System.Collections.Generic.List[string]]::new()
    [void]$capabilities.Add("windows.available")
    [void]$capabilities.Add("powershell.available")

    if (Get-Command "winget.exe" -ErrorAction SilentlyContinue) { [void]$capabilities.Add("winget.available") }
    if (Get-Command "git.exe" -ErrorAction SilentlyContinue) { [void]$capabilities.Add("git.available") }

    $wsl = Get-Command "wsl.exe" -ErrorAction SilentlyContinue
    if ($wsl) {
        try {
            & $wsl.Source --status *> $null
            if ($LASTEXITCODE -eq 0) {
                [void]$capabilities.Add("wsl.available")
            }
        } catch {
            # Capability detection must remain best-effort and must never fail the command.
        }

        try {
            $distros = @(& $wsl.Source --list --quiet 2>$null | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
            if ($distros -match "(?i)^Ubuntu(?:[- ]|$)") {
                [void]$capabilities.Add("wsl.distribution.ubuntu.available")
            }
        } catch {
            # Distribution discovery is optional evidence.
        }
    }

    $rebootPending =
        Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending" -ErrorAction SilentlyContinue -or
        Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired" -ErrorAction SilentlyContinue
    if ($rebootPending) { [void]$capabilities.Add("windows.reboot-required") }

    return @($capabilities | Sort-Object -Unique)
}

function Get-DesktopOperationDetail {
    param([Parameter(Mandatory)][string]$OperationId)

    $state = Read-DesktopOperationState $OperationId
    if (-not $state) { throw "Operation not found: $OperationId" }
    $state = Set-DesktopOperationInterruptedIfNeeded -State $state

    $runId = if ($state.PSObject.Properties.Name -contains "runId") { [string]$state.runId } else { "" }
    $runPath = if ($runId) { Join-Path $Root ("logs\\{0}" -f $runId) } else { $null }
    $logs = @()
    $summary = $null
    if ($runPath -and (Test-Path -LiteralPath $runPath -PathType Container)) {
        $logs = @(Get-ChildItem -LiteralPath $runPath -File -Filter "*.log" -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty Name | Sort-Object)
        $summaryPath = Join-Path $runPath "summary.json"
        if (Test-Path -LiteralPath $summaryPath -PathType Leaf) {
            try { $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json } catch { $summary = $null }
        }
    }

    [pscustomobject]@{
        operation = $state
        runId = $runId
        logs = @($logs)
        summary = $summary
    }
}

function Invoke-DesktopCommand {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("capabilities","baseline","provisioning-profiles","provisioning-plan","provisioning-start","provisioning-status","provisioning-resume","provisioning-recovery","provisioning-history","provisioning-detail","optimization-plan-safe","optimization-apply-safe","optimization-rollback")]
        [string]$Command,
        [string]$ProfileId
    )

    try {
        switch ($Command) {
            "capabilities" {
                $data = Get-DesktopCapabilities
                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data @($data)
            }

            "provisioning-start" {
                if ([string]::IsNullOrWhiteSpace($ProfileId)) { throw "A profile is required to start provisioning." }
                [void](Get-ProfileById $ProfileId)
                $operationId = [guid]::NewGuid().ToString("N")
                $state = @{
                    schemaVersion = 1
                    operationId = $operationId
                    status = "starting"
                    phase = "starting"
                    profileId = $ProfileId
                    currentComponentId = $null
                    currentComponentName = $null
                    completed = 0
                    total = 0
                    percent = 0
                    messageKey = "operation.starting"
                    error = $null
                    canResume = $false
                    nextIndex = 0
                    workerPid = $null
                    updatedAt = $null
                }
                Write-DesktopOperationState -Id $operationId -State $state
                Start-DesktopProvisioningWorker -OperationId $operationId -ProfileId $ProfileId
                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data ([pscustomobject]@{
                    operationId = $operationId
                    status = "starting"
                })
            }

            "provisioning-status" {
                if ([string]::IsNullOrWhiteSpace($ProfileId)) { throw "An operation ID is required." }
                $state = Read-DesktopOperationState $ProfileId
                if (-not $state) { throw "Operation not found: $ProfileId" }
                $state = Set-DesktopOperationInterruptedIfNeeded -State $state
                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data $state

            }

            "provisioning-resume" {
                if ([string]::IsNullOrWhiteSpace($ProfileId)) { throw "An operation ID is required." }
                $state = Read-DesktopOperationState $ProfileId
                if (-not $state) { throw "Operation not found: $ProfileId" }
                $state = Set-DesktopOperationInterruptedIfNeeded -State $state

                if ([string]$state.status -in @("starting","running")) {
                    throw "The operation is already running."
                }
                if (-not [bool]$state.canResume) {
                    throw "This operation cannot be resumed."
                }

                $state.status = "starting"
                $state.phase = "starting"
                $state.error = $null
                $state.messageKey = "operation.resuming"
                $state.workerPid = $null
                Write-DesktopOperationState -Id $ProfileId -State $state
                Start-DesktopProvisioningWorker -OperationId $ProfileId -ProfileId ([string]$state.profileId)

                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data ([pscustomobject]@{
                    operationId = $ProfileId
                    status = "starting"
                })
            }

            "provisioning-recovery" {
                $operationFiles = @(Get-ChildItem -LiteralPath $stateRoot -Filter "operation-*.json" -File -ErrorAction SilentlyContinue)
                $candidates = foreach ($file in $operationFiles) {
                    try {
                        $candidate = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
                        if ($candidate.status -in @("starting","running","failed","reboot-required")) {
                            $candidate = Set-DesktopOperationInterruptedIfNeeded -State $candidate
                            $candidate
                        }
                    } catch {
                        continue
                    }
                }

                $candidate = @($candidates |
                    Sort-Object { [DateTimeOffset]$_.updatedAt } -Descending |
                    Select-Object -First 1)

                if ($candidate.Count -eq 0) {
                    return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data $null
                }

                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data $candidate[0]
            }

            "provisioning-history" {
                $history = @(Get-DesktopOperationHistory -Limit 20)
                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data $history
            }

            "provisioning-detail" {
                if ([string]::IsNullOrWhiteSpace($ProfileId)) { throw "An operation ID is required." }
                $detail = Get-DesktopOperationDetail -OperationId $ProfileId
                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data $detail
            }

            "provisioning-profiles" {
                $data = @(
                    $profiles |
                        Where-Object { $_.id -ne "maintenance" } |
                        ForEach-Object {
                            [pscustomobject]@{
                                id = $_.id
                                name = $_.name
                                description = $_.description
                            }
                        }
                )
                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data $data
            }

            "provisioning-plan" {
                if ([string]::IsNullOrWhiteSpace($ProfileId)) {
                    throw "A profile is required for the provisioning plan."
                }

                $profile = Get-ProfileById $ProfileId
                $plan = @(Resolve-ComponentPlan $catalog @($profile.components))
                $inventory = Get-ComponentInventory -Components @($plan)
                $items = @(
                    $plan | ForEach-Object {
                        $component = $_
                        $state = Get-ComponentInventoryState -Inventory $inventory -Component $component
                        $actionCode = switch ($state.state) {
                            "MISSING" { "install" }
                            "OUTDATED" { "update" }
                            "CONFIG-INCOMPLETE" { "repair-config" }
                            "REPAIRABLE" { "repair" }
                            "CURRENT" { "none" }
                            "INSTALLED" { "version-unverified" }
                            default { "blocked" }
                        }

                        [pscustomobject]@{
                            id = $component.id
                            name = $component.name
                            stateCode = $state.state
                            actionCode = $actionCode
                            message = [string]$state.message
                        }
                    }
                )

                $data = [pscustomobject]@{
                    profile = [pscustomobject]@{
                        id = $profile.id
                        name = $profile.name
                        description = $profile.description
                    }
                    items = @($items)
                }

                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data $data
            }
            "baseline" {
                $data = Get-WindowsOptimizationBaseline
                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data $data
            }

            "optimization-plan-safe" {
                $state = Get-OptimizationState
                $data = @(
                    Get-WindowsOptimizationCatalog |
                        Where-Object level -eq "safe" |
                        ForEach-Object {
                            $item = $_
                            $applied = @($state.applied | Where-Object id -eq $item.id | Select-Object -First 1)
                            $statusCode = if ($applied.Count -gt 0) {
                                if (Test-OptimizationEntryCurrent -Entry $applied[0]) { "applied" } else { "drifted" }
                            } elseif (Test-OptimizationCatalogItemCurrent -Item $item) {
                                "compliant"
                            } else {
                                "pending"
                            }

                            [pscustomobject]@{
                                id = $item.id
                                name = $item.name
                                statusCode = $statusCode
                                impact = $item.impact
                                risk = $item.risk
                                reversible = [bool]$item.reversible
                                description = $item.description
                            }
                        }
                )

                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Data $data
            }

            "optimization-apply-safe" {
                $messages = @(
                    & { Invoke-WindowsOptimization -Level safe } *>&1 | ForEach-Object { [string]$_ }
                )
                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Messages $messages
            }

            "optimization-rollback" {
                $messages = @(
                    & { Invoke-WindowsOptimization -Rollback } *>&1 | ForEach-Object { [string]$_ }
                )
                return ConvertTo-DesktopCommandResponse -Command $Command -Success $true -ExitCode 0 -Messages $messages
            }
        }
    } catch {
        return ConvertTo-DesktopCommandResponse -Command $Command -Success $false -ExitCode 1 -ErrorMessage $_.Exception.Message
    }
}
