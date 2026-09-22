Set-StrictMode -Version Latest

function Write-SectionTitle {
    param([Parameter(Mandatory)][string]$Title, [string]$Subtitle)
    Write-Host ""
    Write-Host ("◆ {0}" -f $Title) -ForegroundColor Cyan
    if ($Subtitle) { Write-Host ("  {0}" -f $Subtitle) -ForegroundColor DarkGray }
}

function Get-InteractionDecision {
    param([Parameter(Mandatory)]$Component, [int]$Index, [int]$Total)
    while ($true) {
        Write-Host ""
        Write-Host ("┌─ [{0}/{1}] {2}" -f $Index, $Total, $Component.name) -ForegroundColor Cyan
        Write-Host "│  État : " -NoNewline
        Write-Host "prêt" -ForegroundColor Green
        Write-Host "│"
        Write-Host "│  [C] " -NoNewline -ForegroundColor Green
        Write-Host "Continuer    " -NoNewline
        Write-Host "[S] " -NoNewline -ForegroundColor Yellow
        Write-Host "Ignorer    " -NoNewline
        Write-Host "[P] " -NoNewline -ForegroundColor Cyan
        Write-Host "Pause    " -NoNewline
        Write-Host "[Q] " -NoNewline -ForegroundColor Red
        Write-Host "Quitter"
        Write-Host "└──────────────────────────────────────────────"
        $choice = (Read-Host "Action").Trim().ToUpperInvariant()
        switch ($choice) {
            "C" { return "continue" }
            "S" { return "skip" }
            "P" {
                Write-Host "⏸ Pause. Appuyez sur ENTER pour continuer." -ForegroundColor Yellow
                [void](Read-Host)
            }
            "Q" { return "quit" }
            default { Write-Warning "Choix invalide. Utilisez C, S, P ou Q." }
        }
    }
}

function Get-FailureDecision {
    param([Parameter(Mandatory)]$Component, [Parameter(Mandatory)]$Result)
    while ($true) {
        Write-Host ""
        Write-Host ("┌─ ❌ $($Component.name) : échec") -ForegroundColor Red
        Write-Host ("│  $($Result.error)") -ForegroundColor Red
        Write-Host "│"
        Write-Host "│  [R] " -NoNewline -ForegroundColor Green
        Write-Host "Réessayer    " -NoNewline
        Write-Host "[S] " -NoNewline -ForegroundColor Yellow
        Write-Host "Ignorer    " -NoNewline
        Write-Host "[L] " -NoNewline -ForegroundColor Cyan
        Write-Host "Journal"
        Write-Host "│  [P] " -NoNewline -ForegroundColor Cyan
        Write-Host "Pause        " -NoNewline
        Write-Host "[Q] " -NoNewline -ForegroundColor Red
        Write-Host "Quitter"
        Write-Host "└──────────────────────────────────────────────"
        $choice = (Read-Host "Action").Trim().ToUpperInvariant()
        switch ($choice) {
            "R" { return "retry" }
            "S" { return "skip" }
            "L" {
                if ($Result.log -and (Test-Path $Result.log)) {
                    Write-SectionTitle "Journal" "80 dernières lignes"
                    Get-Content -LiteralPath $Result.log -Tail 80
                } else {
                    Write-Warning "Aucun journal disponible pour cette étape."
                }
            }
            "P" {
                Write-Host "⏸ Pause. Appuyez sur ENTER pour continuer." -ForegroundColor Yellow
                [void](Read-Host)
            }
            "Q" { return "quit" }
            default { Write-Warning "Choix invalide. Utilisez R, S, L, P ou Q." }
        }
    }
}

function Write-ResumeState {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Mode, [Parameter(Mandatory)][string]$Selection, [Parameter(Mandatory)][int]$NextIndex)
    [ordered]@{
        schemaVersion = 1
        mode = $Mode
        selection = $Selection
        nextIndex = $NextIndex
        updatedAt = (Get-Date).ToUniversalTime().ToString("o")
    } | ConvertTo-Json | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Read-ResumeState {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json } catch { return $null }
}

function Remove-ResumeState {
    param([Parameter(Mandatory)][string]$Path)
    if (Test-Path -LiteralPath $Path -PathType Leaf) { Remove-Item -LiteralPath $Path -Force }
}
