Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:OptimizationStateRoot = Join-Path $env:ProgramData "BounaDevEnvironment\optimization"
$script:OptimizationStatePath = Join-Path $script:OptimizationStateRoot "state.json"

function Ensure-OptimizationStateRoot { New-Item -ItemType Directory -Force -Path $script:OptimizationStateRoot | Out-Null }

function Get-OptimizationState {
    Ensure-OptimizationStateRoot
    if(-not (Test-Path -LiteralPath $script:OptimizationStatePath -PathType Leaf)){
        return [pscustomobject]@{schemaVersion=3;applied=@();updatedAtUtc=$null;appxRemoved=@()}
    }
    try{return Get-Content $script:OptimizationStatePath -Raw|ConvertFrom-Json}catch{throw "État d'optimisation invalide : $($_.Exception.Message)"}
}

function Save-OptimizationState { param([Parameter(Mandatory)]$State);Ensure-OptimizationStateRoot;$State|ConvertTo-Json -Depth 12|Set-Content $script:OptimizationStatePath -Encoding UTF8 }

function Set-OptimizationRegistryValue {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][int]$Value)
    if(-not(Test-Path $Path)){New-Item -Path $Path -Force|Out-Null}
    $exists=$false;$oldValue=$null
    try{$oldValue=(Get-ItemProperty -LiteralPath $Path -Name $Name -ErrorAction Stop).$Name;$exists=$true}catch{}
    [pscustomobject]@{path=$Path;name=$Name;existed=$exists;oldValue=$oldValue;newValue=$Value}
}

function Apply-OptimizationRegistryValue { param([Parameter(Mandatory)]$Change);New-Item -Path $Change.path -Force|Out-Null;New-ItemProperty -LiteralPath $Change.path -Name $Change.name -PropertyType DWord -Value $Change.newValue -Force|Out-Null }

function Restore-OptimizationRegistryValue {
    param([Parameter(Mandatory)]$Change)
    $currentExists=$false;$currentValue=$null
    try{$currentValue=(Get-ItemProperty -LiteralPath $Change.path -Name $Change.name -ErrorAction Stop).$($Change.name);$currentExists=$true}catch{}
    if($currentExists -and [int]$currentValue -ne [int]$Change.newValue){Write-Warning "Rollback ignoré pour $($Change.path)\$($Change.name) : la valeur actuelle a été modifiée depuis l'application.";return $false}
    if(-not $currentExists -and $Change.existed){Write-Warning "Rollback impossible pour $($Change.path)\$($Change.name) : la valeur appliquée n'existe plus.";return $false}
    if($Change.existed){New-Item -Path $Change.path -Force|Out-Null;New-ItemProperty -LiteralPath $Change.path -Name $Change.name -PropertyType DWord -Value ([int]$Change.oldValue) -Force|Out-Null}
    elseif(Test-Path $Change.path){Remove-ItemProperty -LiteralPath $Change.path -Name $Change.name -ErrorAction SilentlyContinue}
    return $true
}
function Test-OptimizationRegistryValueCurrent {
    param([Parameter(Mandatory)]$Change)
    try {
        $current=(Get-ItemProperty -LiteralPath $Change.path -Name $Change.name -ErrorAction Stop).$($Change.name)
        return [int]$current -eq [int]$Change.newValue
    } catch {
        return $false
    }
}

function Test-OptimizationEntryCurrent {
    param([Parameter(Mandatory)]$Entry)
    foreach($change in @($Entry.changes)){
        if(-not (Test-OptimizationRegistryValueCurrent -Change $change)){ return $false }
    }
    if($Entry.powerPlanAfter -and (Get-ActivePowerSchemeGuid) -ne [string]$Entry.powerPlanAfter){ return $false }
    return $true
}

function Test-OptimizationEntryRollbackSafe {
    param([Parameter(Mandatory)]$Entry)
    foreach($change in @($Entry.changes)){
        if(-not (Test-OptimizationRegistryValueCurrent -Change $change)){ return $false }
    }
    if($Entry.powerPlanAfter -and (Get-ActivePowerSchemeGuid) -ne [string]$Entry.powerPlanAfter){ return $false }
    return $true
}

function Get-ActivePowerSchemeGuid {
    $raw=powercfg /GETACTIVESCHEME 2>&1|Out-String
    if($raw -match '([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})'){return $Matches[1]}
    return $null
}

function Set-HighPerformancePowerPlan {
    $previous=Get-ActivePowerSchemeGuid
    $target="8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
    if($previous -eq $target){return [pscustomobject]@{before=$previous;after=$target}}
    powercfg /SETACTIVE SCHEME_MIN|Out-Null
    if($LASTEXITCODE -ne 0){throw "Impossible d'activer le plan Hautes performances (code $LASTEXITCODE)."}
    return [pscustomobject]@{before=$previous;after=$target}
}

function Restore-PowerPlan {
    param([string]$Before,[string]$After)
    if(-not $Before){return $true}
    $active=Get-ActivePowerSchemeGuid
    if($After -and $active -ne $After){Write-Warning "Rollback du plan d'alimentation ignoré : le plan actif a été modifié depuis l'optimisation.";return $false}
    powercfg /SETACTIVE $Before|Out-Null
    if($LASTEXITCODE -ne 0){Write-Warning "Impossible de restaurer le plan d'alimentation $Before.";return $false}
    return $true
}
function Get-WindowsOptimizationCatalog {
    @(
        [pscustomobject]@{id="privacy-tailored-experiences";name="Désactiver les expériences personnalisées";level="safe";impact="Privacy";risk="Faible";reversible=$true;description="Réduit les contenus personnalisés fondés sur les données de diagnostic.";changes=@(@{path="HKCU:\Software\Policies\Microsoft\Windows\CloudContent";name="DisableTailoredExperiencesWithDiagnosticData";value=1})},
        [pscustomobject]@{id="privacy-advertising-id";name="Désactiver l'identifiant publicitaire";level="safe";impact="Privacy";risk="Faible";reversible=$true;description="Désactive l'identifiant publicitaire du profil utilisateur.";changes=@(@{path="HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo";name="Enabled";value=0})},
        [pscustomobject]@{id="performance-game-mode";name="Activer le Mode Jeu";level="safe";impact="Gaming";risk="Faible";reversible=$true;description="Laisse Windows utiliser son Mode Jeu pour les applications de jeu.";changes=@(@{path="HKCU:\Software\Microsoft\GameBar";name="AutoGameModeEnabled";value=1})},
        [pscustomobject]@{id="privacy-activity-feed";name="Désactiver la synchronisation d'activité";level="advanced";impact="Privacy";risk="Modéré";reversible=$true;description="Désactive les stratégies de publication/téléversement d'activité utilisateur.";changes=@(@{path="HKCU:\Software\Policies\Microsoft\Windows\System";name="PublishUserActivities";value=0},@{path="HKCU:\Software\Policies\Microsoft\Windows\System";name="UploadUserActivities";value=0})},
        [pscustomobject]@{id="performance-content-delivery";name="Réduire les contenus et suggestions Windows";level="advanced";impact="UX";risk="Modéré";reversible=$true;description="Réduit les suggestions et installations de contenus promotionnels Windows.";changes=@(@{path="HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";name="ContentDeliveryAllowed";value=0},@{path="HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";name="OemPreInstalledAppsEnabled";value=0},@{path="HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";name="PreInstalledAppsEnabled";value=0},@{path="HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";name="SilentInstalledAppsEnabled";value=0})},
        [pscustomobject]@{id="performance-high-power";name="Activer le plan Hautes performances";level="advanced";impact="Performance";risk="Modéré";reversible=$true;description="Privilégie les performances générales au détriment potentiel de l'autonomie sur portable.";action="power-high-performance"},
        [pscustomobject]@{id="aggressive-gamedvr";name="Désactiver l'enregistrement Game DVR";level="aggressive";impact="Gaming";risk="Élevé";reversible=$true;description="Supprime l'enregistrement en arrière-plan et les captures automatiques.";changes=@(@{path="HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR";name="AppCaptureEnabled";value=0},@{path="HKCU:\System\GameConfigStore";name="GameDVR_Enabled";value=0})}
    )
}

function Test-OptimizationCatalogItemCurrent {
    param([Parameter(Mandatory)]$Item)
    foreach($definition in @($Item.changes)){
        try {
            $current=(Get-ItemProperty -LiteralPath $definition.path -Name $definition.name -ErrorAction Stop).$($definition.name)
            if([int]$current -ne [int]$definition.value){ return $false }
        } catch {
            return $false
        }
    }
    if($Item.action -eq "power-high-performance"){
        return (Get-ActivePowerSchemeGuid) -eq "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
    }
    return $true
}

function Get-WindowsOptimizationBaseline {
    $computer=Get-CimInstance Win32_ComputerSystem
    $os=Get-CimInstance Win32_OperatingSystem
    $cpu=Get-CimInstance Win32_Processor | Select-Object -First 1
    $volumes=@(Get-Volume -ErrorAction SilentlyContinue | Where-Object DriveLetter | Select-Object DriveLetter,FileSystem,HealthStatus,Size,SizeRemaining)
    $startup=@()
    $runKeys=@(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run",
        "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
    )
    foreach($key in $runKeys){
        if(Test-Path $key){
            $props=Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
            if($props){
                foreach($p in $props.PSObject.Properties | Where-Object Name -notmatch "^PS"){
                    $startup+=[pscustomobject]@{Source=$key;Name=$p.Name;Command=[string]$p.Value}
                }
            }
        }
    }
    foreach($folder in @(
        [Environment]::GetFolderPath("Startup"),
        [Environment]::GetFolderPath("CommonStartup")
    )){
        if($folder -and (Test-Path $folder)){
            Get-ChildItem -LiteralPath $folder -Force -ErrorAction SilentlyContinue | ForEach-Object {
                $startup+=[pscustomobject]@{Source=$folder;Name=$_.Name;Command=$_.FullName}
            }
        }
    }

    [pscustomobject]@{
        TimestampUtc=(Get-Date).ToUniversalTime().ToString("o")
        ComputerName=$env:COMPUTERNAME
        Windows=[string]$os.Caption
        Build=[string]$os.BuildNumber
        Architecture=[string]$os.OSArchitecture
        UptimeHours=[math]::Round(((Get-Date)-$os.LastBootUpTime).TotalHours,1)
        Cpu=[string]$cpu.Name
        CpuCores=[int]$computer.NumberOfLogicalProcessors
        MemoryGB=[math]::Round($computer.TotalPhysicalMemory/1GB,1)
        ActivePowerPlan=(Get-ActivePowerSchemeGuid)
        Volumes=@($volumes)
        StartupEntries=@($startup)
    }
}

function Show-WindowsOptimizationBaseline {
    $baseline=Get-WindowsOptimizationBaseline
    Write-Host ""
    Write-Host "📊 ÉTAT DU PC — BASELINE AVANT OPTIMISATION" -ForegroundColor Cyan
    Write-Host ("  Machine       : {0}" -f $baseline.ComputerName)
    Write-Host ("  Windows       : {0} (build {1})" -f $baseline.Windows,$baseline.Build)
    Write-Host ("  Architecture  : {0}" -f $baseline.Architecture)
    Write-Host ("  CPU           : {0} — {1} threads logiques" -f $baseline.Cpu,$baseline.CpuCores)
    Write-Host ("  Mémoire       : {0} Go" -f $baseline.MemoryGB)
    Write-Host ("  Uptime        : {0} h" -f $baseline.UptimeHours)
    Write-Host ("  Plan énergie  : {0}" -f $(if($baseline.ActivePowerPlan){$baseline.ActivePowerPlan}else{"inconnu"}))
    Write-Host ""
    Write-Host "  Stockage" -ForegroundColor Yellow
    foreach($volume in $baseline.Volumes){
        $free=[math]::Round($volume.SizeRemaining/1GB,1)
        $total=[math]::Round($volume.Size/1GB,1)
        $pct=if($volume.Size -gt 0){[math]::Round(($volume.SizeRemaining/$volume.Size)*100,0)}else{0}
        Write-Host ("    {0}: {1} Go libres / {2} Go — {3}% — {4}" -f $volume.DriveLetter,$free,$total,$pct,$volume.HealthStatus)
    }
    Write-Host ""
    Write-Host ("  Démarrage : {0} entrée(s) détectée(s)" -f $baseline.StartupEntries.Count) -ForegroundColor Yellow
    foreach($entry in $baseline.StartupEntries | Sort-Object Name){
        Write-Host ("    • {0}" -f $entry.Name)
    }
    Write-Host ""
    Write-Host "ℹ️ Cette analyse est en lecture seule : aucune optimisation ni suppression n'est effectuée." -ForegroundColor Green
    return $baseline
}

function Show-WindowsOptimizationPlan {
    param([ValidateSet("safe","advanced","aggressive")][string]$Level="safe")
    $state=Get-OptimizationState
    $catalog=@(Get-WindowsOptimizationCatalog|Where-Object{$_.level -eq "safe" -or ($Level -in @("advanced","aggressive") -and $_.level -eq "advanced") -or ($Level -eq "aggressive" -and $_.level -eq "aggressive")})
    Write-Host ""
    Write-Host "📋 PLAN D’OPTIMISATION — $($Level.ToUpperInvariant())" -ForegroundColor Cyan
    foreach($item in $catalog){
        $applied=@($state.applied|Where-Object id -eq $item.id|Select-Object -First 1)
        if($applied.Count -gt 0){
            $status=if(Test-OptimizationEntryCurrent -Entry $applied[0]){"DÉJÀ APPLIQUÉ"}else{"MODIFIÉ DEPUIS L'APPLICATION"}
        } elseif(Test-OptimizationCatalogItemCurrent -Item $item) {
            $status="DÉJÀ CONFORME"
        } else {
            $status="À APPLIQUER"
        }
        $reversible=if($item.reversible){"réversible"}else{"non réversible"}
        Write-Host ("  {0,-38} {1,-28} {2,-10} {3}" -f $item.name,$status,$item.impact,$reversible)
        Write-Host ("    Risque: {0} — {1}" -f $item.risk,$item.description) -ForegroundColor DarkGray
        if($status -eq "MODIFIÉ DEPUIS L'APPLICATION"){
            Write-Host "    ⚠️ Aucune valeur ne sera écrasée automatiquement ; le rollback restera protégé." -ForegroundColor Yellow
        }
    }
    Write-Host ""
    Write-Host "Aucun changement n'est effectué par cet aperçu." -ForegroundColor Yellow
}
function Invoke-WindowsOptimization {
    param([ValidateSet("safe","advanced","aggressive")][string]$Level="safe",[switch]$Rollback)
    if(-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw "L'optimisation Windows doit être exécutée dans PowerShell administrateur."}
    $state=Get-OptimizationState

    if($Rollback){
        $remaining=[System.Collections.Generic.List[object]]::new()
        foreach($entry in @($state.applied) | Sort-Object {[array]::IndexOf(@($state.applied), $_)} -Descending){
            if(-not (Test-OptimizationEntryRollbackSafe -Entry $entry)){
                Write-Warning "$($entry.name) : restauration ignorée car une valeur a été modifiée depuis l'application."
                [void]$remaining.Add($entry)
                continue
            }
            $ok=$true
            foreach($change in @($entry.changes)){
                if(-not (Restore-OptimizationRegistryValue -Change $change)){ $ok=$false }
            }
            if($ok -and $entry.powerPlanBefore){
                $ok=Restore-PowerPlan ([string]$entry.powerPlanBefore) ([string]$entry.powerPlanAfter)
            }
            if($ok){ Write-Host "↩ $($entry.id) restauré." -ForegroundColor Yellow }
            else { [void]$remaining.Add($entry) }
        }
        Save-OptimizationState ([pscustomobject]@{
            schemaVersion=3
            applied=@($remaining)
            updatedAtUtc=(Get-Date).ToUniversalTime().ToString("o")
            appxRemoved=@($state.appxRemoved)
        })
        if($remaining.Count -eq 0){
            Write-Host "✅ Optimisations réversibles restaurées." -ForegroundColor Green
        } else {
            Write-Warning "$($remaining.Count) optimisation(s) restent enregistrées car leur état a changé manuellement."
        }
        return
    }

    $catalog=@(Get-WindowsOptimizationCatalog|Where-Object{$_.level -eq "safe" -or ($Level -in @("advanced","aggressive") -and $_.level -eq "advanced") -or ($Level -eq "aggressive" -and $_.level -eq "aggressive")})
    foreach($item in $catalog){
        $existing=@($state.applied|Where-Object id -eq $item.id|Select-Object -First 1)
        if($existing.Count -gt 0){
            if(Test-OptimizationEntryCurrent -Entry $existing[0]){
                Write-Host "• $($item.name) — déjà appliqué et conforme." -ForegroundColor DarkGray
            } else {
                Write-Warning "$($item.name) — modification détectée depuis la dernière application ; aucune valeur n'est écrasée automatiquement."
            }
            continue
        }

        $currentDefinitions=@($item.changes)
        $registryAlreadyCurrent=$true
        foreach($definition in $currentDefinitions){
            try {
                $current=(Get-ItemProperty -LiteralPath $definition.path -Name $definition.name -ErrorAction Stop).$($definition.name)
                if([int]$current -ne [int]$definition.value){$registryAlreadyCurrent=$false;break}
            } catch {$registryAlreadyCurrent=$false;break}
        }
        $powerAlreadyCurrent=if($item.action -eq "power-high-performance"){(Get-ActivePowerSchemeGuid) -eq "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"}else{$true}

        if($registryAlreadyCurrent -and $powerAlreadyCurrent){
            Write-Host "• $($item.name) — déjà conforme ; aucune modification effectuée." -ForegroundColor DarkGray
            continue
        }

        $changes=@();$powerPlanBefore=$null;$powerPlanAfter=$null
        foreach($definition in $currentDefinitions){
            $change=Set-OptimizationRegistryValue $definition.path $definition.name $definition.value
            Apply-OptimizationRegistryValue $change
            $changes+=$change
        }
        if($item.action -eq "power-high-performance"){
            $power=Set-HighPerformancePowerPlan
            $powerPlanBefore=$power.before
            $powerPlanAfter=$power.after
        }
        $state.applied+=[pscustomobject]@{
            id=$item.id
            name=$item.name
            level=$item.level
            changes=$changes
            powerPlanBefore=$powerPlanBefore
            powerPlanAfter=$powerPlanAfter
            appliedAtUtc=(Get-Date).ToUniversalTime().ToString("o")
        }
        Write-Host "✓ $($item.name)" -ForegroundColor Green
    }
    $state.updatedAtUtc=(Get-Date).ToUniversalTime().ToString("o")
    Save-OptimizationState $state
    Write-Host "✅ Optimisation $Level terminée. Les changements réversibles sont enregistrés." -ForegroundColor Green
}

function Get-AggressiveAppxAllowlist {
    @(
        "Microsoft.BingNews",
        "Microsoft.BingWeather",
        "Microsoft.GetHelp",
        "Microsoft.Getstarted",
        "Microsoft.MicrosoftSolitaireCollection",
        "Microsoft.People",
        "Microsoft.Todos",
        "Microsoft.WindowsFeedbackHub",
        "Clipchamp.Clipchamp"
    )
}

function Invoke-AggressiveAppxDebloat {
    if(-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw "Le debloat AppX nécessite PowerShell administrateur."}
    $allow=@(Get-AggressiveAppxAllowlist)
    $candidates=@(Get-AppxPackage -AllUsers|Where-Object{$allow -contains $_.Name -and -not $_.NonRemovable}|Sort-Object Name -Unique)
    if($candidates.Count -eq 0){Write-Host "Aucun paquet AppX de l'allowlist n'est présent ou supprimable.";return}
    Write-Host "";Write-Host "⚠️ DEBLOAT APPX AGRESSIF — ACTION IRRÉVERSIBLE AUTOMATIQUEMENT" -ForegroundColor Yellow
    foreach($p in $candidates){Write-Host ("  • {0} — {1}" -f $p.Name,$p.PackageFullName)}
    Write-Host "Microsoft Store n'est jamais ciblé." -ForegroundColor DarkGray
    if((Read-Host "Tapez DEBLOAT pour confirmer").Trim() -ne "DEBLOAT"){Write-Host "Annulé.";return}
    $state=Get-OptimizationState
    foreach($p in $candidates){
        try{
            Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction Stop
            $state.appxRemoved+=[pscustomobject]@{name=$p.Name;packageFullName=$p.PackageFullName;removedAtUtc=(Get-Date).ToUniversalTime().ToString("o")}
            Write-Host "✓ Supprimé : $($p.Name)" -ForegroundColor Green
        }catch{Write-Warning "Impossible de supprimer $($p.Name) : $($_.Exception.Message)"}
    }
    $state.updatedAtUtc=(Get-Date).ToUniversalTime().ToString("o");Save-OptimizationState $state
    Write-Host "ℹ️ La restauration automatique des AppX n'est pas promise : une réinstallation officielle est requise." -ForegroundColor Yellow
}

function Show-WindowsOptimizationMenu {
    while($true){
        Write-Host "";Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Cyan;Write-Host "║        🧹 OPTIMISATION WINDOWS               ║" -ForegroundColor Cyan;Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Cyan;Write-Host ""
        Write-Host "  [1] 📊 Analyser l'état du PC — lecture seule"
        Write-Host "  [2] 👀 Aperçu du plan Safe"
        Write-Host "  [3] 🛡️ Appliquer Safe"
        Write-Host "  [4] ⚙️ Appliquer Advanced — Safe + performance générale"
        Write-Host "  [5] 🔥 Appliquer Aggressive — Advanced + réglages plus radicaux"
        Write-Host "  [6] 🧨 Debloat AppX — allowlist explicite"
        Write-Host "  [7] ↩️ Restaurer les optimisations réversibles"
        Write-Host "  [Q] Retour";Write-Host ""
        try{switch((Read-Host "Votre choix").Trim().ToUpperInvariant()){"1"{Show-WindowsOptimizationBaseline;[void](Read-Host "ENTER")}"2"{Show-WindowsOptimizationPlan -Level safe;[void](Read-Host "ENTER")}"3"{Invoke-WindowsOptimization -Level safe;[void](Read-Host "ENTER")}"4"{Invoke-WindowsOptimization -Level advanced;[void](Read-Host "ENTER")}"5"{Invoke-WindowsOptimization -Level aggressive;[void](Read-Host "ENTER")}"6"{Invoke-AggressiveAppxDebloat;[void](Read-Host "ENTER")}"7"{Invoke-WindowsOptimization -Rollback;[void](Read-Host "ENTER")}"Q"{return}default{Write-Warning "Choix invalide."}}}catch{Write-Error $_.Exception.Message;[void](Read-Host "ENTER")}
    }
}
