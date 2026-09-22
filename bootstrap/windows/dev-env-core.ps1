[CmdletBinding()]
param(
    [switch]$PlanOnly,
    [switch]$PrepareCache,
    [switch]$CacheOnly,
    [string]$ExportCacheTo,
    [switch]$OperationWorker,
    [string]$OperationId,
    [ValidateSet("base","development","development-extended","gaming")]
    [string]$Profile,
    [ValidateSet("baseline","provisioning-profiles","provisioning-plan","provisioning-start","provisioning-status","optimization-plan-safe","optimization-apply-safe","optimization-rollback")]
    [string]$Command
)

Set-StrictMode -Version Latest

$Root = $PSScriptRoot
$Engine = Join-Path $Root "engine"

. (Join-Path $Engine "Logging.ps1")
. (Join-Path $Engine "Diagnostics.ps1")
. (Join-Path $Engine "Optimization.ps1")
. (Join-Path $Engine "Interaction.ps1")
. (Join-Path $Engine "Component.ps1")
. (Join-Path $Engine "Prerequisite.ps1")
. (Join-Path $Engine "Check.ps1")
. (Join-Path $Engine "Runner.ps1")
. (Join-Path $Engine "Update.ps1")
. (Join-Path $Engine "Maintenance.ps1")
. (Join-Path $Engine "Environment.ps1")
. (Join-Path $Engine "Inventory.ps1")
. (Join-Path $Engine "Desktop.ps1")

$stateRoot = Join-Path $env:ProgramData "WorkspaceBootstrap"

if (-not $PlanOnly) {
    New-Item -ItemType Directory -Force -Path $stateRoot | Out-Null
}

$catalog = @(Get-ComponentCatalog (Join-Path $Root "components"))
$profiles = @(Get-ChildItem (Join-Path $Root "profiles") -Filter "*.json" |
    ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json })

function Export-InstallerCache {
    param([Parameter(Mandatory)][string]$Destination)

    . (Join-Path $Root "Cache.ps1")
    $source = Initialize-DevEnvironmentCache
    if (-not (Test-Path -LiteralPath (Join-Path $source "installers") -PathType Container)) {
        throw "Le cache des installateurs n'existe pas encore : $source"
    }

    $sourceFullPath = [IO.Path]::GetFullPath($source).TrimEnd("\\")
    $destinationFullPath = [IO.Path]::GetFullPath($Destination).TrimEnd("\\")
    if ($destinationFullPath.Equals($sourceFullPath, [System.StringComparison]::OrdinalIgnoreCase) -or $destinationFullPath.StartsWith($sourceFullPath + "\\", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "La destination d'export doit être distincte du cache source : $source"
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Write-Host "📦 Export du cache vers : $Destination" -ForegroundColor Cyan
    Copy-Item -LiteralPath (Join-Path $source "installers") -Destination $Destination -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $source "metadata") -Destination $Destination -Recurse -Force

    $manifest = [pscustomobject]@{
        schemaVersion = 1
        exportedAt = (Get-Date).ToUniversalTime().ToString("o")
        source = $source
        installers = @(Get-ChildItem -LiteralPath (Join-Path $source "metadata") -Filter "*.json" -File -Recurse |
            ForEach-Object {
                try { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json } catch { $null }
            } | Where-Object { $_ })
    }
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $Destination "cache-manifest.json") -Encoding UTF8
    Write-Host "✅ Export terminé. Conservez ce dossier sur un support non formaté avant la réinstallation de Windows." -ForegroundColor Green
}

function Invoke-PrepareInstallerCache {
    . (Join-Path $Root "Cache.ps1")
    $components = @($catalog | Where-Object {
        $_.PSObject.Properties.Name -contains "packageId" -and -not [string]::IsNullOrWhiteSpace([string]$_.packageId)
    })
    Write-Host ""
    Write-Host "📦 PRÉPARATION DU CACHE LOCAL DES INSTALLATEURS" -ForegroundColor Cyan
    Write-Host "Cache : $(Get-DevEnvironmentCacheRoot)" -ForegroundColor DarkGray
    Write-Host "La dernière version stable est résolue depuis la source officielle du composant quand elle est déclarée ; WinGet n’est utilisé que pour les composants sans source officielle." -ForegroundColor DarkGray
    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($component in $components) {
        $installerType = if ($component.PSObject.Properties.Name -contains "installerType") { [string]$component.installerType } else { $null }
        $locale = if ($component.PSObject.Properties.Name -contains "locale") { [string]$component.locale } else { "fr-FR" }
        $architecture = if ($component.PSObject.Properties.Name -contains "architecture") { [string]$component.architecture } else { "x64" }
        try {
            $metadata = Get-CachedInstaller -Component $component -PackageId ([string]$component.packageId) -Version $null -InstallerType $installerType -Locale $locale -Architecture $architecture -DownloadIfMissing
            if (-not $metadata) { throw "Aucun installateur disponible pour '$($component.name)'." }
            Write-Host ("  ✓ {0} {1} — {2} [{3}]" -f $component.name, $metadata.version, $metadata.fileName, $metadata.source) -ForegroundColor Green
        } catch {
            $message = "Échec cache {0} : {1}" -f $component.name, $_.Exception.Message
            $failures.Add($message)
            Write-Error $message
        }
    }

    if ($failures.Count -gt 0) {
        throw "La préparation du cache a échoué pour $($failures.Count) composant(s)."
    }

    Write-Host "✅ Préparation du cache terminée." -ForegroundColor Green
}

function Get-ProfileById {
    param([Parameter(Mandatory)][string]$Id)
    $profile = $profiles | Where-Object id -eq $Id | Select-Object -First 1
    if (-not $profile) { throw "Unknown profile: $Id" }
    return $profile
}

function Select-InstallProfile {
    param([switch]$AllowCancel)

    $baseProfiles = @($profiles | Where-Object { $_.id -ne "maintenance" })

    while ($true) {
        Write-Host ""
        Write-Host "🧩 PROFIL D’INSTALLATION" -ForegroundColor Cyan
        Write-Host "Choisissez le profil à installer ou vérifier :" -ForegroundColor DarkGray
        Write-Host ""

        for ($i = 0; $i -lt $baseProfiles.Count; $i++) {
            Write-Host ("[{0}] {1} - {2}" -f ($i + 1), $baseProfiles[$i].name, $baseProfiles[$i].description)
        }

        if ($AllowCancel) { Write-Host "[Q] Retour" }

        $choice = (Read-Host "Votre choix").Trim()
        if ($AllowCancel -and $choice -ieq "q") { return $null }

        if ($choice -match '^\d+$') {
            $index = [int]$choice - 1
            if ($index -ge 0 -and $index -lt $baseProfiles.Count) {
                return $baseProfiles[$index]
            }
        }

        Write-Warning "Choix invalide."
    }
}

function Show-Plan {
    param([Parameter(Mandatory)]$Plan)

    Write-Host ""
    Write-Host "📋 Étapes prévues" -ForegroundColor Cyan
    for ($i = 0; $i -lt $Plan.Count; $i++) {
        Write-Host ("  {0,2}. {1}" -f ($i + 1), $Plan[$i].name)
    }
}

function Invoke-InteractivePlan {
    param(
        [Parameter(Mandatory)]$Plan,
        [Parameter(Mandatory)][string]$Mode,
        [switch]$CacheOnly
    )

    Ensure-BootstrapPrerequisites

    $statePath = Join-Path $stateRoot "resume.json"
    $resumeState = Read-ResumeState $statePath
    $resumeIndex = 0
    $selection = $Plan.id -join ","

    if ($resumeState -and [string]$resumeState.selection -eq $selection) {
        Write-Host ""
        Write-Host "↻ Une opération interrompue est disponible." -ForegroundColor Yellow
        Write-Host "[1] Reprendre à l'étape $([int]$resumeState.nextIndex + 1)" -ForegroundColor Green
        Write-Host "[2] Recommencer" -ForegroundColor Yellow
        $resumeChoice = (Read-Host "Votre choix").Trim()

        if ($resumeChoice -eq "1") {
            $resumeIndex = [int]$resumeState.nextIndex
        } else {
            Remove-ResumeState $statePath
        }
    }

    Show-Plan $Plan

    Write-Host ""
    Write-Host "[1] ▶ Commencer" -ForegroundColor Green
    Write-Host "[2] Annuler" -ForegroundColor Yellow
    if ((Read-Host "Votre choix").Trim() -ne "1") {
        Write-Host "Opération annulée." -ForegroundColor Yellow
        return
    }

    $run = New-BootstrapRun -LogsRoot (Join-Path $Root "logs") -Mode $Mode -Selection $selection
    $steps = [System.Collections.Generic.List[object]]::new()
    $componentStates = @{}
    $exitCode = 0


    try {
        for ($i = $resumeIndex; $i -lt $Plan.Count; $i++) {
            $component = $Plan[$i]
            $decision = Get-InteractionDecision -Component $component -Index ($i + 1) -Total $Plan.Count

            if ($decision -eq "quit") {
                Write-ResumeState -Path $statePath -Mode $Mode -Selection $selection -NextIndex $i
                $exitCode = 4
                break
            }

            if ($decision -eq "skip") {
                [void]$steps.Add([pscustomobject]@{ id = $component.id; status = "skipped"; exitCode = 0 })
                Write-ResumeState -Path $statePath -Mode $Mode -Selection $selection -NextIndex ($i + 1)
                continue
            }

            while ($true) {
                Write-Host ""
                Write-Host ("═══ 🔧 ÉTAPE {0}/{1} : {2} ═══" -f ($i + 1), $Plan.Count, $component.name) -ForegroundColor Cyan

                $result = Invoke-Component $component $Root $run.Path $componentStates -ForceRepair:($Mode -eq "repair") -CacheOnly:$CacheOnly

                if ($result.status -eq "passed") {
                    $componentStates[$component.id] = "OK"
                    [void]$steps.Add($result)
                    Write-ResumeState -Path $statePath -Mode $Mode -Selection $selection -NextIndex ($i + 1)
                    break
                }

                $componentStates[$component.id] = $result.status.ToUpperInvariant()

                if ($result.status -eq "reboot-required") {
                    Write-Host ""
                    Write-Host "🔄 Un redémarrage de Windows est nécessaire." -ForegroundColor Yellow
                    Write-Host "L'état a été sauvegardé. Relancez l'outil après le redémarrage." -ForegroundColor DarkGray
                    Write-ResumeState -Path $statePath -Mode $Mode -Selection $selection -NextIndex $i
                    $exitCode = 10
                    break
                }

                $action = Get-FailureDecision -Component $component -Result $result
                if ($action -eq "retry") { continue }

                if ($action -eq "skip") {
                    [void]$steps.Add([pscustomobject]@{
                        id = $component.id
                        status = "skipped-after-failure"
                        exitCode = 0
                        error = $result.error
                    })
                    Write-ResumeState -Path $statePath -Mode $Mode -Selection $selection -NextIndex ($i + 1)
                    break
                }

                Write-ResumeState -Path $statePath -Mode $Mode -Selection $selection -NextIndex $i
                $exitCode = 4
                break
            }

            if ($exitCode) { break }
        }
    }
    finally {
        Write-RunSummary $run @($steps) $exitCode
    }

    if ($exitCode -eq 0) {
        Remove-ResumeState $statePath
        Write-Host ""
        Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Green
        Write-Host "║          ✅ Opération terminée               ║" -ForegroundColor Green
        Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Green
    }
}

function Show-ProvisioningPlan {
    param(
        [Parameter(Mandatory)][object[]]$Plan,
        [Parameter(Mandatory)][string]$ProfileId,
        [Parameter(Mandatory)][hashtable]$Inventory
    )

    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║              📋 PLAN DE PROVISIONNEMENT — SIMULATION (DRY-RUN)           ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Profil : $ProfileId" -ForegroundColor White
    Write-Host "Mode   : lecture seule — aucune installation, modification ou téléchargement d’installateur." -ForegroundColor Yellow
    Write-Host ""

    for ($i = 0; $i -lt $Plan.Count; $i++) {
        $component = $Plan[$i]
        $state = Get-ComponentInventoryState -Inventory $Inventory -Component $component
        $action = switch ($state.state) {
            "MISSING" { "INSTALLER" }
            "OUTDATED" { "METTRE À JOUR" }
            "CONFIG-INCOMPLETE" { "RÉPARER LA CONFIGURATION" }
            "REPAIRABLE" { "RÉPARER" }
            "CURRENT" { "AUCUNE ACTION" }
            "INSTALLED" { "VERSION NON VÉRIFIÉE" }
            default { "BLOQUÉ — CONTRÔLE EN ÉCHEC" }
        }
        $stateLabel = switch ($state.state) {
            "CURRENT" { "À JOUR" }
            "INSTALLED" { "INSTALLÉ" }
            "MISSING" { "ABSENT" }
            "OUTDATED" { "MISE À JOUR DISPONIBLE" }
            "CONFIG-INCOMPLETE" { "CONFIGURATION INCOMPLÈTE" }
            "REPAIRABLE" { "RÉPARATION POSSIBLE" }
            default { "ÉCHEC" }
        }
        Write-Host ("  {0,2}. {1,-28} {2,-20} {3}" -f ($i + 1), $component.name, $stateLabel, $action)
        Write-Host ("      {0}" -f $state.message) -ForegroundColor DarkGray
    }

    Write-Host ""
    Write-Host "ℹ️  Ce dry-run ne lance aucun installateur et ne modifie pas le système." -ForegroundColor Green
    Write-Host "   Le dry-run peut interroger WinGet pour vérifier les mises à jour ; il ne lance aucun installateur et ne modifie pas le système." -ForegroundColor DarkGray
}

function Invoke-Profile {
    param(
        [Parameter(Mandatory)][string]$ProfileId,
        [Parameter(Mandatory)][string]$Mode,
        [switch]$CacheOnly
    )

    $profile = Get-ProfileById $ProfileId
    $plan = @(Resolve-ComponentPlan $catalog @($profile.components))
    Invoke-InteractivePlan -Plan $plan -Mode $Mode -CacheOnly:$CacheOnly
}

function Show-ProfileCheck {
    $profile = Select-InstallProfile -AllowCancel
    if (-not $profile) { return }

    $plan = @(Resolve-ComponentPlan $catalog @($profile.components))
    Show-Plan $plan

    Write-Host ""
    Write-Host "Légende : À JOUR=conforme et à jour selon le contrôle disponible, INSTALLÉ=présent mais version non vérifiée, MISE À JOUR DISPONIBLE=version plus récente disponible, ABSENT=non installé, CONFIGURATION INCOMPLÈTE=configuration ou prérequis incomplets, RÉPARATION POSSIBLE=contrôle en échec mais réparation disponible, ÉCHEC=contrôle impossible." -ForegroundColor DarkGray
    Write-Host ""
    foreach ($component in $plan) {
        $result = Get-ComponentState -Component $component
        $label = switch ($result.state) {
            "CURRENT" { "✓ À JOUR" }
            "INSTALLED" { "• INSTALLÉ" }
            "OUTDATED" { "↻ MISE À JOUR" }
            "MISSING" { "✗ ABSENT" }
            "CONFIG-INCOMPLETE" { "⚠ CONFIGURATION INCOMPLÈTE" }
            "REPAIRABLE" { "🩹 RÉPARATION POSSIBLE" }
            default { "✖ ÉCHEC" }
        }
        Write-Host ("  {0,-28} {1,-22} {2}" -f $component.name, $label, $result.message)
    }

    [void](Read-Host "Appuyez sur ENTER pour revenir au menu")
}

function Show-MainMenu {
    while ($true) {
        Write-Host ""
        Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Cyan
        Write-Host "║      🛠️  Workspace Bootstrap — Windows     ║" -ForegroundColor Cyan
        Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Cyan
        Write-Host ""
        $updatePreview = Show-UpdateStatus
        Write-Host ""
        Write-Host "  [1] 🧱 Installer un profil PC" -ForegroundColor White
        Write-Host "  [2] 💻 Ajouter l'environnement développeur" -ForegroundColor White
        Write-Host "  [3] 🧰 Ajouter le développement étendu" -ForegroundColor White
        Write-Host "  [4] 🎮 Ajouter l'environnement gaming" -ForegroundColor White
        Write-Host "  [5] 🔎 Vérifier un profil" -ForegroundColor White
        Write-Host "  [6] 🩹 Réparer un profil" -ForegroundColor White
        Write-Host "  [7] 🧹 Optimisation / nettoyage Windows" -ForegroundColor White
        Write-Host "  [8] 🔧 Maintenance Windows" -ForegroundColor White
        Write-Host "  [9] 📦 Afficher les composants" -ForegroundColor White
        Write-Host "  [10] 🔄 Mettre à jour les logiciels installés" -ForegroundColor White
        Write-Host "  [11] 📦 Préparer / actualiser le cache des installateurs" -ForegroundColor White
        Write-Host "  [Q] 🚪 Quitter" -ForegroundColor DarkGray
        Write-Host ""

        $choice = (Read-Host "Votre choix").Trim().ToUpperInvariant()
        if ($choice -eq "1") {
            $profile = Select-InstallProfile
            Invoke-Profile -ProfileId $profile.id -Mode "install" -CacheOnly:$CacheOnly
        } elseif ($choice -eq "2") {
            Invoke-Profile -ProfileId "development" -Mode "install" -CacheOnly:$CacheOnly
        } elseif ($choice -eq "3") {
            Invoke-Profile -ProfileId "development-extended" -Mode "install" -CacheOnly:$CacheOnly
        } elseif ($choice -eq "4") {
            Invoke-Profile -ProfileId "gaming" -Mode "install" -CacheOnly:$CacheOnly
        } elseif ($choice -eq "5") {
            Show-ProfileCheck
        } elseif ($choice -eq "6") {
            $profile = Select-InstallProfile -AllowCancel
            if ($profile) { Invoke-Profile -ProfileId $profile.id -Mode "repair" }
        } elseif ($choice -eq "7") {
            Show-WindowsOptimizationMenu
        } elseif ($choice -eq "8") {
            Show-WindowsMaintenanceMenu
        } elseif ($choice -eq "9") {
            Write-Host ""
            Write-Host "📦 COMPOSANTS DISPONIBLES" -ForegroundColor Cyan
            Write-Host "=========================" -ForegroundColor DarkGray
            foreach ($component in $catalog) {
                Write-Host ("  {0,-24} {1}" -f $component.id, $component.name)
            }
            [void](Read-Host "Appuyez sur ENTER pour revenir au menu")
        } elseif ($choice -eq "10") {
            try { Invoke-UpdateAllInstalled } catch { Write-Error $_.Exception.Message }
            [void](Read-Host "Appuyez sur ENTER pour revenir au menu")
        } elseif ($choice -eq "11") {
            try { Invoke-PrepareInstallerCache } catch { Write-Error $_.Exception.Message }
            [void](Read-Host "Appuyez sur ENTER pour revenir au menu")
        } elseif ($choice -eq "Q") {
            return
        } else {
            Write-Warning "Choix invalide."
        }
    }
}

function Get-DesktopOperationPath {
    param([Parameter(Mandatory)][string]$Id)
    return (Join-Path $stateRoot ("operation-" + $Id + ".json"))
}

function Write-DesktopOperationState {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][hashtable]$State
    )
    $path = Get-DesktopOperationPath $Id
    $State.updatedAt = (Get-Date).ToUniversalTime().ToString("o")
    $tempPath = $path + ".tmp"
    $State | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $tempPath -Encoding UTF8
    Move-Item -LiteralPath $tempPath -Destination $path -Force
}

function Read-DesktopOperationState {
    param([Parameter(Mandatory)][string]$Id)
    $path = Get-DesktopOperationPath $Id
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Get-DesktopOperationHistory {
    param([int]$Limit = 20)

    $files = @(Get-ChildItem -LiteralPath $stateRoot -Filter "operation-*.json" -File -ErrorAction SilentlyContinue)
    @(
        foreach ($file in $files) {
            try {
                Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
            } catch {
                continue
            }
        }
    ) | Sort-Object { [DateTimeOffset]$_.updatedAt } -Descending | Select-Object -First $Limit
}


function Invoke-DesktopProvisioningWorker {
    param([Parameter(Mandatory)][string]$Id, [Parameter(Mandatory)][string]$ProfileId, [switch]$CacheOnly)

    $state = Read-DesktopOperationState $Id
    if (-not $state) { exit 1 }

    try {
        $profile = Get-ProfileById $ProfileId
        $plan = @(Resolve-ComponentPlan $catalog @($profile.components))
        $run = New-BootstrapRun -LogsRoot (Join-Path $Root "logs") -Mode "install" -Selection ($plan.id -join ",")
        $state.runId = $run.Id
        Write-DesktopOperationState -Id $Id -State $state
        $componentStates = @{}
        $resumeIndex = 0
        if ($state.PSObject.Properties.Name -contains "nextIndex" -and $null -ne $state.nextIndex) {
            $resumeIndex = [int]$state.nextIndex
        }
        if ($resumeIndex -lt 0 -or $resumeIndex -gt $plan.Count) {
            throw "L'état de reprise contient une étape invalide : $resumeIndex."
        }

        $state.workerPid = $PID
        $state.status = "running"
        $state.phase = "starting"
        $state.canResume = $false
        Write-DesktopOperationState -Id $Id -State $state

        for ($i = $resumeIndex; $i -lt $plan.Count; $i++) {
            $component = $plan[$i]
            $state.status = "running"
            $state.phase = "component"
            $state.currentComponentId = $component.id
            $state.currentComponentName = $component.name
            $state.completed = $i
            $state.total = $plan.Count
            $state.nextIndex = $i
            $state.percent = if ($plan.Count -eq 0) { 100 } else { [math]::Floor(($i / $plan.Count) * 100) }
            $state.messageKey = "component.running"
            Write-DesktopOperationState -Id $Id -State $state

            $result = Invoke-Component $component $Root $run.Path $componentStates -CacheOnly:$CacheOnly
            if ($result.status -eq "passed") {
                $componentStates[$component.id] = "OK"
                $state.completed = $i + 1
                $state.nextIndex = $i + 1
                $state.percent = if ($plan.Count -eq 0) { 100 } else { [math]::Floor((($i + 1) / $plan.Count) * 100) }
                $state.messageKey = "component.completed"
                Write-DesktopOperationState -Id $Id -State $state
                continue
            }

            if ($result.status -eq "reboot-required") {
                Write-ResumeState -Path (Join-Path $stateRoot "resume.json") -Mode "install" -Selection ($plan.id -join ",") -NextIndex $i
                $state.status = "reboot-required"
                $state.phase = "reboot"
                $state.error = $result.error
                $state.canResume = $true
                $state.messageKey = "operation.reboot-required"
                $state.workerPid = $null
                Write-DesktopOperationState -Id $Id -State $state
                exit 10
            }

            $state.status = "failed"
            $state.phase = "error"
            $state.error = $result.error
            $state.messageKey = "component.failed"
            $state.canResume = $true
            $state.nextIndex = $i
            $state.workerPid = $null
            Write-DesktopOperationState -Id $Id -State $state
            exit 4
        }

        Remove-ResumeState (Join-Path $stateRoot "resume.json")
        $state.status = "completed"
        $state.phase = "completed"
        $state.completed = $plan.Count
        $state.total = $plan.Count
        $state.nextIndex = $plan.Count
        $state.percent = 100
        $state.messageKey = "operation.completed"
        $state.canResume = $false
        $state.workerPid = $null
        Write-DesktopOperationState -Id $Id -State $state
        exit 0
    } catch {
        $state.status = "failed"
        $state.phase = "error"
        $state.error = $_.Exception.Message
        $state.messageKey = "operation.failed"
        $state.canResume = $true
        $state.workerPid = $null
        Write-DesktopOperationState -Id $Id -State $state
        exit 1
    }
}

if ($OperationWorker) {
    if ([string]::IsNullOrWhiteSpace($OperationId) -or [string]::IsNullOrWhiteSpace($Profile)) { exit 2 }
    Invoke-DesktopProvisioningWorker -Id $OperationId -ProfileId $Profile
}

if ($Command) {
    $response = Invoke-DesktopCommand -Command $Command -ProfileId $Profile
    Write-Output $response

    try {
        $parsed = $response | ConvertFrom-Json
        exit ([int]$parsed.exitCode)
    } catch {
        exit 1
    }
}
if ($CacheOnly -and -not $Profile) { throw "-CacheOnly nécessite -Profile pour sélectionner l'environnement à restaurer." }

if ($PrepareCache) {
    Invoke-PrepareInstallerCache
    exit 0
}

if ($ExportCacheTo) {
    Export-InstallerCache -Destination $ExportCacheTo
    exit 0
}

if ($PlanOnly) {
    $selectedProfiles = if ($Profile) {
        @((Get-ProfileById $Profile))
    } else {
        @($profiles | Where-Object { $_.id -ne "maintenance" })
    }

    $plans = @{}
    $allComponents = [System.Collections.Generic.List[object]]::new()

    foreach ($candidate in $selectedProfiles) {
        $plan = @(Resolve-ComponentPlan $catalog @($candidate.components))
        $plans[$candidate.id] = $plan
        foreach ($component in $plan) {
            [void]$allComponents.Add($component)
        }
    }

    $inventory = Get-ComponentInventory -Components @($allComponents)

    foreach ($candidate in $selectedProfiles) {
        Show-ProvisioningPlan -Plan $plans[$candidate.id] -ProfileId $candidate.id -Inventory $inventory
    }
    exit 0
}

if ($Profile) {
    Invoke-Profile -ProfileId $Profile -Mode "install" -CacheOnly:$CacheOnly
    exit 0
}

Show-MainMenu
