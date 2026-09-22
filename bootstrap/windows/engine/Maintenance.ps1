Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:MaintenanceRoot = Join-Path $env:ProgramData "BounaDevEnvironment\maintenance"

function Test-MaintenanceAdmin {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Invoke-MaintenanceCommand {
    param([Parameter(Mandatory)][string]$FilePath,[Parameter(Mandatory)][string[]]$Arguments,[string]$Title=$FilePath)
    Write-Host ""
    Write-Host "▶ $Title" -ForegroundColor Cyan
    & $FilePath @Arguments
    $code=$LASTEXITCODE
    if($code -ne 0){ Write-Warning "$Title s'est terminé avec le code $code." } else { Write-Host "✓ $Title terminé." -ForegroundColor Green }
    return $code
}

function Get-WindowsUpdateHealth {
    $services=foreach($name in @("wuauserv","bits","cryptsvc")){
        $s=Get-Service -Name $name -ErrorAction SilentlyContinue
        if($s){ [pscustomobject]@{Name=$name;Status=[string]$s.Status;StartType=[string]$s.StartType} }
        else{ [pscustomobject]@{Name=$name;Status="MISSING";StartType="UNKNOWN"} }
    }
    return [pscustomobject]@{Services=@($services);SettingsUri="ms-settings:windowsupdate"}
}

function Show-WindowsUpdateDiagnostics {
    $health=Get-WindowsUpdateHealth
    Write-Host ""
    Write-Host "🪟 WINDOWS UPDATE" -ForegroundColor Cyan
    foreach($s in $health.Services){
        $symbol=if($s.Status -eq "Running"){"✓"}else{"!"}
        Write-Host ("  {0} {1,-10} {2,-12} ({3})" -f $symbol,$s.Name,$s.Status,$s.StartType)
    }
    Write-Host ""
    Write-Host "[1] Ouvrir Windows Update" -ForegroundColor Green
    Write-Host "[2] Relancer les services Windows Update/BITS" -ForegroundColor Yellow
    Write-Host "[Q] Retour" -ForegroundColor DarkGray
    switch((Read-Host "Votre choix").Trim().ToUpperInvariant()){
        "1" { Start-Process "ms-settings:windowsupdate" }
        "2" {
            foreach($name in @("wuauserv","bits","cryptsvc")){
                $s=Get-Service $name -ErrorAction SilentlyContinue
                if($s){
                    if($s.Status -ne "Running"){ Start-Service $name -ErrorAction Continue }
                    else{ Restart-Service $name -Force -ErrorAction Continue }
                }
            }
            Start-Process "ms-settings:windowsupdate"
        }
    }
}

function Invoke-WindowsHealthRepair {
    if(-not (Test-MaintenanceAdmin)){ throw "La maintenance système nécessite PowerShell administrateur." }
    [void](Invoke-MaintenanceCommand "DISM.exe" @("/Online","/Cleanup-Image","/CheckHealth") "DISM — CheckHealth")
    [void](Invoke-MaintenanceCommand "DISM.exe" @("/Online","/Cleanup-Image","/ScanHealth") "DISM — ScanHealth")
    [void](Invoke-MaintenanceCommand "DISM.exe" @("/Online","/Cleanup-Image","/RestoreHealth") "DISM — RestoreHealth")
    [void](Invoke-MaintenanceCommand "sfc.exe" @("/scannow") "SFC — vérification des fichiers système")
}

function Invoke-ComponentStoreMaintenance {
    if(-not (Test-MaintenanceAdmin)){ throw "La maintenance système nécessite PowerShell administrateur." }
    [void](Invoke-MaintenanceCommand "DISM.exe" @("/Online","/Cleanup-Image","/AnalyzeComponentStore") "Analyse du magasin de composants")
    [void](Invoke-MaintenanceCommand "DISM.exe" @("/Online","/Cleanup-Image","/StartComponentCleanup") "Nettoyage du magasin de composants")
}

function Invoke-StorageMaintenance {
    if(-not (Test-MaintenanceAdmin)){ throw "Le nettoyage du stockage nécessite PowerShell administrateur." }
    foreach($path in @($env:TEMP,"$env:LOCALAPPDATA\Temp")){
        if(Test-Path $path){
            Get-ChildItem -LiteralPath $path -Force -ErrorAction SilentlyContinue |
                Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    if(Get-Command cleanmgr.exe -ErrorAction SilentlyContinue){
        [void](Invoke-MaintenanceCommand "cleanmgr.exe" @("/verylowdisk") "Nettoyage Windows")
    }
}

function Invoke-WslHealthCommand {
    param([Parameter(Mandatory)][string]$Command,[Parameter(Mandatory)][string]$Title)

    if(-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)){
        Write-Host "• $Title — wsl.exe indisponible." -ForegroundColor DarkGray
        return 1
    }

    Write-Host "▶ $Title" -ForegroundColor Cyan
    & wsl.exe -d "Ubuntu" -- bash -lc $Command
    $code=$LASTEXITCODE
    if($code -eq 0){ Write-Host "✓ $Title" -ForegroundColor Green }
    else { Write-Warning "$Title a retourné le code $code." }
    return $code
}

function Show-DevelopmentHealth {
    Write-Host ""
    Write-Host "🧰 SANTÉ DE L’ENVIRONNEMENT DE DÉVELOPPEMENT" -ForegroundColor Cyan

    Write-Host "Windows host" -ForegroundColor Yellow
    $windowsChecks=@(
        @{Name="WSL";Command="wsl.exe";Args=@("--status")},
        @{Name="Git";Command="git.exe";Args=@("--version")},
        @{Name="PowerShell";Command="pwsh.exe";Args=@("--version")}
    )
    foreach($check in $windowsChecks){
        $cmd=Get-Command $check.Command -ErrorAction SilentlyContinue
        if($cmd){
            Write-Host "✓ $($check.Name)" -ForegroundColor Green
            & $cmd.Source @($check.Args) 2>&1 | Select-Object -First 2
        } else {
            Write-Host "• $($check.Name) — indisponible côté Windows" -ForegroundColor DarkGray
        }
    }

    Write-Host ""
    Write-Host "WSL-owned toolchain (Ubuntu)" -ForegroundColor Yellow
    [void](Invoke-WslHealthCommand "node --version" "Node.js")
    [void](Invoke-WslHealthCommand "java -version" "Java par défaut")
    [void](Invoke-WslHealthCommand "adb version" "Android ADB")
    [void](Invoke-WslHealthCommand "flutter --version" "Flutter")
    [void](Invoke-WslHealthCommand "docker version" "Docker Engine")
    [void](Invoke-WslHealthCommand "docker compose version" "Docker Compose")
    [void](Invoke-WslHealthCommand "test -e /dev/kvm && stat -c '%A %G' /dev/kvm" "KVM Android Emulator")

    if(Get-Command wsl.exe -ErrorAction SilentlyContinue){
        Write-Host ""
        wsl.exe --list --verbose
    }
}

function Invoke-NetworkDiagnostics {
    Write-Host ""
    Write-Host "🌐 DIAGNOSTIC RÉSEAU" -ForegroundColor Cyan
    ipconfig /all
    Write-Host ""
    Get-DnsClientServerAddress -ErrorAction SilentlyContinue | Format-Table -AutoSize
    foreach($target in @("1.1.1.1","8.8.8.8")){
        $ok=Test-Connection -ComputerName $target -Count 2 -Quiet -ErrorAction SilentlyContinue
        Write-Host "$target : $ok"
    }
}

function Invoke-NetworkRepair {
    if(-not (Test-MaintenanceAdmin)){ throw "La réparation réseau nécessite PowerShell administrateur." }
    Write-Host "Cette action réinitialise Winsock et nécessite un redémarrage." -ForegroundColor Yellow
    if((Read-Host "Tapez RESET pour confirmer").Trim() -ne "RESET"){ return }
    netsh winsock reset
    Write-Host "✓ Winsock réinitialisé." -ForegroundColor Green
}

function New-MaintenanceReport {
    New-Item -ItemType Directory -Force -Path $script:MaintenanceRoot | Out-Null
    $path=Join-Path $script:MaintenanceRoot ("report-{0}.txt" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
    $sections=[System.Collections.Generic.List[string]]::new()
    $sections.Add("BOUNA-PC — Diagnostic report")
    $sections.Add("Generated: $((Get-Date).ToString("o"))")
    $sections.Add("Windows: $([Environment]::OSVersion.VersionString)")
    $sections.Add("=== SYSTEM ===")
    $sections.Add((Get-CimInstance Win32_ComputerSystem | Format-List * | Out-String))
    $sections.Add("=== OS ===")
    $sections.Add((Get-CimInstance Win32_OperatingSystem | Format-List Caption,Version,BuildNumber,OSArchitecture,LastBootUpTime | Out-String))
    $sections.Add("=== POWER ===")
    $sections.Add((powercfg /GETACTIVESCHEME 2>&1 | Out-String))
    $sections.Add("=== WSL ===")
    $sections.Add((wsl.exe --status 2>&1 | Out-String))
    $sections.Add((wsl.exe --list --verbose 2>&1 | Out-String))
    $sections.Add("=== NETWORK ===")
    $sections.Add((ipconfig /all 2>&1 | Out-String))
    $sections.Add("=== UPDATE SERVICES ===")
    $sections.Add((Get-WindowsUpdateHealth | Format-List * | Out-String))
    $sections.Add("=== STORAGE ===")
    $sections.Add((Get-Volume | Where-Object DriveLetter | Select-Object DriveLetter,FileSystemLabel,FileSystem,Size,SizeRemaining,HealthStatus | Format-Table -AutoSize | Out-String))
    [IO.File]::WriteAllText($path,($sections -join [Environment]::NewLine),[Text.UTF8Encoding]::new($false))
    Write-Host "📄 Rapport créé : $path" -ForegroundColor Green
    return $path
}

function Show-WindowsMaintenanceMenu {
    while($true){
        Write-Host ""
        Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Cyan
        Write-Host "║          🔧 MAINTENANCE WINDOWS              ║" -ForegroundColor Cyan
        Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "  [1] 🩺 Diagnostic Windows Update"
        Write-Host "  [2] 🛠️  DISM + SFC — diagnostic/réparation"
        Write-Host "  [3] 🧹 Magasin de composants — analyse/nettoyage"
        Write-Host "  [4] 💾 Nettoyage stockage/temporaire"
        Write-Host "  [5] 🧰 Santé Windows + WSL / Docker / Android / Flutter"
        Write-Host "  [6] 🌐 Diagnostic réseau"
        Write-Host "  [7] 🔧 Réinitialiser Winsock"
        Write-Host "  [8] 📄 Générer un rapport diagnostic"
        Write-Host "  [Q] Retour" -ForegroundColor DarkGray
        Write-Host ""
        try{
            switch((Read-Host "Votre choix").Trim().ToUpperInvariant()){
                "1" { Show-WindowsUpdateDiagnostics }
                "2" { Invoke-WindowsHealthRepair;[void](Read-Host "ENTER") }
                "3" { Invoke-ComponentStoreMaintenance;[void](Read-Host "ENTER") }
                "4" { Invoke-StorageMaintenance;[void](Read-Host "ENTER") }
                "5" { Show-DevelopmentHealth;[void](Read-Host "ENTER") }
                "6" { Invoke-NetworkDiagnostics;[void](Read-Host "ENTER") }
                "7" { Invoke-NetworkRepair;[void](Read-Host "ENTER") }
                "8" { [void](New-MaintenanceReport);[void](Read-Host "ENTER") }
                "Q" { return }
                default { Write-Warning "Choix invalide." }
            }
        }catch{
            Write-Error $_.Exception.Message
            [void](Read-Host "ENTER")
        }
    }
}
