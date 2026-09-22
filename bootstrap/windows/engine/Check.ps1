Set-StrictMode -Version Latest

function Test-CheckSet {
    param(
        [Parameter(Mandatory=$true)]$Checks,
        [Parameter(Mandatory=$true)][string]$Scope
    )

    foreach($check in @($Checks)){
        $type=[string]$check.type
        switch($type){
            "command" {
                $command=Get-Command ([string]$check.name) -ErrorAction SilentlyContinue
                if(-not $command){
                    return [pscustomobject]@{status="MISSING";message="La commande '$($check.name)' est indisponible.";scope=$Scope}
                }
            }
            "command-version" {
                $command=Get-Command ([string]$check.name) -ErrorAction SilentlyContinue
                if(-not $command){
                    return [pscustomobject]@{status="MISSING";message="La commande '$($check.name)' est indisponible.";scope=$Scope}
                }
                try {
                    $raw=& $command.Source @($check.arguments) 2>$null | Select-Object -First 1
                    if($raw -match '(\d+(?:\.\d+){1,3})'){
                        $installed=[version]$Matches[1]
                        $minimum=[version][string]$check.minimum
                        if($installed -lt $minimum){
                            return [pscustomobject]@{status="OUTDATED";message="$($check.name) $installed est inférieur au minimum requis $minimum.";scope=$Scope;installedVersion=$installed;minimumVersion=$minimum}
                        }
                    } else {
                        return [pscustomobject]@{status="FAILED";message="Impossible de déterminer la version de $($check.name) à partir de la sortie de la commande.";scope=$Scope}
                    }
                } catch {
                    return [pscustomobject]@{status="FAILED";message="Impossible de déterminer la version de $($check.name) : $($_.Exception.Message)";scope=$Scope}
                }
            }
            "winget-package" {
                if(-not (Get-Command winget -ErrorAction SilentlyContinue)){
                    return [pscustomobject]@{status="MISSING";message="WinGet est indisponible.";scope=$Scope}
                }
                $packageId=[string]$check.id
                $raw=& winget list --id $packageId --exact --source winget --accept-source-agreements --disable-interactivity 2>&1 | Out-String
                if($LASTEXITCODE -ne 0 -or $raw -match "(?i)No installed package found|Aucun package installé"){
                    return [pscustomobject]@{status="MISSING";message="Le paquet WinGet '$packageId' n'est pas installé.";scope=$Scope}
                }
            }
            "file-any" {
                $found=$false
                foreach($path in @($check.paths)){
                    # Component manifests may use Windows environment variables in paths
                    # (for example %ProgramFiles%). Resolve them before checking the file.
                    $resolvedPath=[Environment]::ExpandEnvironmentVariables([string]$path)
                    if(Test-Path -LiteralPath $resolvedPath -PathType Leaf){$found=$true;break}
                }
                if(-not $found){
                    return [pscustomobject]@{status="MISSING";message="Aucun des fichiers requis n'a été trouvé.";scope=$Scope}
                }
            }
            "wsl-distro" {
                if(-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)){
                    return [pscustomobject]@{status="MISSING";message="wsl.exe est indisponible.";scope=$Scope}
                }
                $distros=@(wsl.exe --list --quiet 2>$null) | ForEach-Object { $_.Trim([char]0).Trim() } | Where-Object {$_}
                if(-not ($distros -contains [string]$check.name)){
                    return [pscustomobject]@{status="MISSING";message="La distribution WSL '$($check.name)' n'est pas installée.";scope=$Scope}
                }
            }
            "vs-required" {
                $vswhereCandidates=@(
                    (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe"),
                    (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe")
                )
                $vswhere=$vswhereCandidates|Where-Object{Test-Path $_}|Select-Object -First 1
                if(-not $vswhere){
                    return [pscustomobject]@{status="MISSING";message="vswhere.exe de Visual Studio est introuvable.";scope=$Scope}
                }
                foreach($required in @($check.components)){
                    $raw=& $vswhere -latest -products * -requires $required -format json 2>$null
                    if(-not $raw){
                        return [pscustomobject]@{status="OUTDATED";message="Le composant Visual Studio '$required' est manquant.";scope=$Scope}
                    }
                }
                $compiler=$false
                $instances=& $vswhere -latest -products * -format json 2>$null | ConvertFrom-Json
                foreach($instance in @($instances)){
                    if($instance.installationPath){
                        $root=Join-Path $instance.installationPath "VCToolsMSVC"
                        if(Test-Path $root){
                            $compiler=Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
                                ForEach-Object {Join-Path $_.FullName "bin\Hostx64\x64\cl.exe"} |
                                Where-Object {Test-Path $_}|Select-Object -First 1
                            if($compiler){break}
                        }
                    }
                }
                if(-not $compiler){
                    return [pscustomobject]@{status="OUTDATED";message="Aucun compilateur MSVC x64 fonctionnel n'a été trouvé.";scope=$Scope}
                }
            }
            default {
                throw "Unknown component check type '$type'."
            }
        }
    }

    return [pscustomobject]@{status="OK";message="Les contrôles « $Scope » sont satisfaits.";scope=$Scope}
}

$script:WingetUpdateCache = @{}

function Test-WingetPackageUpdate {
    param(
        [Parameter(Mandatory=$true)][string]$PackageId
    )

    if ($script:WingetUpdateCache.ContainsKey($PackageId)) {
        return $script:WingetUpdateCache[$PackageId]
    }

    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        $result = [pscustomobject]@{
            available = $false
            updateAvailable = $false
            message = "WinGet n'est pas disponible pour vérifier les mises à jour."
        }
        $script:WingetUpdateCache[$PackageId] = $result
        return $result
    }

    try {
        $raw = (& winget upgrade --id $PackageId --exact --source winget --accept-source-agreements --disable-interactivity 2>&1 | Out-String)
        $exitCode = $LASTEXITCODE

        if ($raw -match "(?i)No applicable upgrade found|No applicable upgrade|Aucune mise à jour applicable|Aucune mise à jour trouvée") {
            $result = [pscustomobject]@{
                available = $true
                updateAvailable = $false
                message = "Aucune mise à jour WinGet disponible."
            }
        } elseif ($exitCode -eq 0) {
            $line = @($raw -split [Environment]::NewLine | Where-Object {
                $_ -match [regex]::Escape($PackageId)
            } | Select-Object -First 1)

            $installedVersion = $null
            $availableVersion = $null
            if ($line -and $line[0] -match [regex]::Escape($PackageId) + "\s+(\S+)\s+(\S+)") {
                $installedVersion = $Matches[1]
                $availableVersion = $Matches[2]
            }

            $message = if ($installedVersion -and $availableVersion) {
                "Mise à jour disponible : $installedVersion → $availableVersion."
            } else {
                "Une mise à jour WinGet est disponible."
            }

            $result = [pscustomobject]@{
                available = $true
                updateAvailable = $true
                installedVersion = $installedVersion
                availableVersion = $availableVersion
                message = $message
            }
        } else {
            $result = [pscustomobject]@{
                available = $false
                updateAvailable = $false
                message = "Vérification WinGet indisponible pour ce composant (code $exitCode)."
            }
        }
    } catch {
        $result = [pscustomobject]@{
            available = $false
            updateAvailable = $false
            message = "Vérification WinGet indisponible : $($_.Exception.Message)"
        }
    }

    $script:WingetUpdateCache[$PackageId] = $result
    return $result
}

function Test-ComponentCheck {
    param([Parameter(Mandatory=$true)]$Component)

    $checks=@(if($Component.PSObject.Properties.Name -contains "checks"){$Component.checks}else{@()})
    $result=Test-CheckSet -Checks $checks -Scope "baseline"
    if($result.status -ne "OK"){
        return $result
    }

    $configurationChecks=@(if($Component.PSObject.Properties.Name -contains "configurationChecks"){$Component.configurationChecks}else{@()})
    if($configurationChecks.Count -gt 0){
        $configuration=Test-CheckSet -Checks $configurationChecks -Scope "configuration"
        if($configuration.status -ne "OK"){
            return [pscustomobject]@{
                status=$configuration.status
                message=$configuration.message
                scope="configuration"
            }
        }
    }

    return [pscustomobject]@{status="OK";message="Les prérequis et la configuration du composant sont présents.";scope="baseline"}
}

function Get-ComponentState {
    param(
        [Parameter(Mandatory=$true)]$Component
    )

    try {
        $result=Test-ComponentCheck -Component $Component
    } catch {
        return [pscustomobject]@{
            state="FAILED"
            status="FAILED"
            message="Le contrôle du composant a échoué : $($_.Exception.Message)"
            repairable=$false
        }
    }

    if ($result.scope -eq "configuration" -and $result.status -ne "OK") {
        return [pscustomobject]@{
            state="CONFIG-INCOMPLETE"
            status=$result.status
            message=$result.message
            repairable=$true
        }
    }

    switch($result.status){
        "MISSING" {
            return [pscustomobject]@{state="MISSING";status="MISSING";message=$result.message;repairable=$true}
        }
        "OUTDATED" {
            return [pscustomobject]@{state="OUTDATED";status="OUTDATED";message=$result.message;repairable=$true}
        }
        "FAILED" {
            return [pscustomobject]@{state="REPAIRABLE";status="FAILED";message=$result.message;repairable=$true}
        }
        "OK" {
            $latestPolicy = [string]$Component.versionPolicy -in @("latest-stable","latest-stable-compatible")
            $wingetManaged = ($Component.PSObject.Properties.Name -contains "packageManager") -and [string]$Component.packageManager -eq "winget" -and
                ($Component.PSObject.Properties.Name -contains "packageId") -and
                -not [string]::IsNullOrWhiteSpace([string]$Component.packageId)

            if (-not $latestPolicy) {
                return [pscustomobject]@{
                    state="CURRENT"
                    status="OK"
                    message=$result.message
                    repairable=$false
                    freshness="local-check-only"
                }
            }

            if ($wingetManaged) {
                $update = Test-WingetPackageUpdate -PackageId ([string]$Component.packageId)
                if ($update.available -and $update.updateAvailable) {
                    return [pscustomobject]@{
                        state="OUTDATED"
                        status="OUTDATED"
                        message=$update.message
                        repairable=$true
                        freshness="remote-update-check"
                        installedVersion=$update.installedVersion
                        availableVersion=$update.availableVersion
                    }
                }

                if ($update.available -and -not $update.updateAvailable) {
                    return [pscustomobject]@{
                        state="CURRENT"
                        status="OK"
                        message="$($result.message) $($update.message)"
                        repairable=$false
                        freshness="remote-update-check"
                    }
                }

                return [pscustomobject]@{
                    state="INSTALLED"
                    status="OK"
                    message="$($result.message) $($update.message)"
                    repairable=$false
                    freshness="update-check-unavailable"
                }
            }

            return [pscustomobject]@{
                state="INSTALLED"
                status="OK"
                message=$result.message
                repairable=$false
                freshness="local-check-only"
            }
        }
        default {
            return [pscustomobject]@{state="FAILED";status="FAILED";message="État de contrôle inconnu pour le composant : '$($result.status)'.";repairable=$false}
        }
    }
}
