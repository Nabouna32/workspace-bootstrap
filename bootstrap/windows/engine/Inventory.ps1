Set-StrictMode -Version Latest

function Get-ComponentInventory {
    param(
        [Parameter(Mandatory=$true)][object[]]$Components
    )

    $inventory = @{}
    $uniqueComponents = @(
        $Components |
            Sort-Object id |
            Group-Object -Property id |
            ForEach-Object { $_.Group | Select-Object -First 1 }
    )
    $total = $uniqueComponents.Count
    $completed = 0

    Write-Host ""
    Write-Host "🔎 Vérification de l'inventaire des composants..." -ForegroundColor Cyan

    foreach ($component in $uniqueComponents) {
        $completed++
        $percent = if ($total -gt 0) {
            [math]::Round(($completed / $total) * 100)
        } else {
            100
        }

        Write-Progress -Id 1 -Activity "Vérification de l'environnement" -Status ("{0}/{1} — {2}" -f $completed, $total, $component.name) -PercentComplete $percent
        Write-Host ("  [{0,3}%] Vérification : {1}" -f $percent, $component.name) -ForegroundColor DarkGray

        $inventory[[string]$component.id] = Get-ComponentState -Component $component
    }

    Write-Progress -Id 1 -Activity "Vérification de l'environnement" -Completed
    Write-Host ("✓ Inventaire terminé : {0} composant(s) vérifié(s)." -f $total) -ForegroundColor Green
    Write-Host ""

    return $inventory
}

function Get-ComponentInventoryState {
    param(
        [Parameter(Mandatory=$true)][hashtable]$Inventory,
        [Parameter(Mandatory=$true)]$Component
    )

    $id = [string]$Component.id
    if (-not $Inventory.ContainsKey($id)) {
        throw "Component '$id' is missing from the provisioning inventory."
    }

    return $Inventory[$id]
}
