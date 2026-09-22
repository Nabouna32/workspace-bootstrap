Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-WingetUpgradePreview {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { return [pscustomobject]@{ available=$false; output="WinGet introuvable."; count=0 } }
    $output = (& winget upgrade --source winget --accept-source-agreements --disable-interactivity 2>&1 | Out-String)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -and $output -notmatch "(?i)No applicable upgrade|Aucune mise à jour applicable") { return [pscustomobject]@{ available=$false; output=$output; count=-1 } }
    $none = $output -match "(?i)No applicable upgrade|Aucune mise à jour applicable"
    $count = if ($none) { 0 } else { @($output -split [Environment]::NewLine | Where-Object { $_ -match "^\s*\S+\.\S+\s+\S" }).Count }
    [pscustomobject]@{ available=$true; output=$output; count=$count }
}

function Show-UpdateStatus {
    $preview = Get-WingetUpgradePreview
    if (-not $preview.available) { Write-Host "🔄 Mises à jour : vérification indisponible" -ForegroundColor Yellow; return $preview }
    if ($preview.count -eq 0) { Write-Host "🔄 Mises à jour : aucune mise à jour WinGet détectée" -ForegroundColor Green }
    else { Write-Host "🔄 Mises à jour : $($preview.count) paquet(s) WinGet potentiellement disponible(s)" -ForegroundColor Yellow }
    return $preview
}

function Invoke-UpdateAllInstalled {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { throw "WinGet est requis pour mettre à jour les applications Windows." }
    Write-Host ""; Write-Host "🔄 MISE À JOUR DES LOGICIELS" -ForegroundColor Cyan
    Write-Host "WinGet va tenter de mettre à jour tous les logiciels qu'il peut gérer." -ForegroundColor DarkGray
    Write-Host "Windows Update, pilotes et logiciels non gérés par WinGet restent hors de cette opération." -ForegroundColor DarkGray; Write-Host ""
    & winget upgrade --source winget --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) { throw "Impossible d'obtenir la liste des mises à jour WinGet." }
    Write-Host ""; Write-Host "[1] Mettre à jour maintenant" -ForegroundColor Green; Write-Host "[2] Annuler" -ForegroundColor Yellow
    if ((Read-Host "Votre choix").Trim() -ne "1") { Write-Host "Mise à jour annulée." -ForegroundColor Yellow; return }
    & winget upgrade --all --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) { throw "La mise à jour WinGet s'est terminée avec le code $LASTEXITCODE." }
    $flutter = Get-Command flutter -ErrorAction SilentlyContinue
    if ($flutter) {
        Write-Host ""; Write-Host "🐦 Flutter détecté : mise à jour via le mécanisme officiel Flutter." -ForegroundColor Cyan
        & $flutter.Source upgrade
        if ($LASTEXITCODE -ne 0) { throw "flutter upgrade a échoué (exit $LASTEXITCODE)." }
    }
    Write-Host ""; Write-Host "✅ Mise à jour terminée." -ForegroundColor Green
}
