Set-StrictMode -Version Latest

function Get-ComponentCatalog {
  param(
    [Parameter(Mandatory=$true)][string]$ComponentsRoot,
    [ValidateSet("windows","wsl","linux")][string]$Platform="windows"
  )

  if(-not (Test-Path -LiteralPath $ComponentsRoot -PathType Container)){
    throw "Component catalog root not found: $ComponentsRoot"
  }

  $components=@(
    Get-ChildItem -LiteralPath $ComponentsRoot -Filter "component.json" -Recurse -File |
      Sort-Object FullName |
      ForEach-Object {
        $component=Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
        if(-not $component.id){
          throw "Component manifest has no id: $($_.FullName)"
        }
        if(@($component.platforms) -notcontains $Platform){
          return
        }
        $component
      }
  )

  $duplicates=@(
    $components |
      Group-Object -Property id |
      Where-Object Count -gt 1
  )
  if($duplicates.Count){
    $ids=$duplicates.Name -join ", "
    throw "Duplicate component IDs for platform '$Platform': $ids"
  }

  @($components | Sort-Object id)
}

function Resolve-ComponentPlan {
  param(
    [Parameter(Mandatory=$true)][object[]]$Catalog,
    [Parameter(Mandatory=$true)][string[]]$Requested
  )

  $map=@{}
  foreach($c in $Catalog){
    if($map.ContainsKey($c.id)){
      throw "Duplicate component ID '$($c.id)' in catalog."
    }
    $map[$c.id]=$c
  }

  $ordered=[System.Collections.Generic.List[string]]::new()
  $visiting=@{}
  $visited=@{}

  function Visit([string]$id){
    if(-not $map.ContainsKey($id)){throw "Unknown component: $id"}
    if($visiting.ContainsKey($id)){throw "Dependency cycle detected at '$id'"}
    if($visited.ContainsKey($id)){return}

    $visiting[$id]=$true
    foreach($d in @($map[$id].dependencies)){
      if($d){Visit $d}
    }
    $visiting.Remove($id)
    $visited[$id]=$true
    [void]$ordered.Add($id)
  }

  foreach($id in $Requested){Visit $id}
  @($ordered | ForEach-Object {$map[$_]})
}

function Get-ComponentInstallerPath {
  param(
    [Parameter(Mandatory=$true)][string]$Root,
    [Parameter(Mandatory=$true)]$Component
  )

  $p=Join-Path $Root $Component.installer
  if(Test-Path -LiteralPath $p -PathType Leaf){$p}else{$null}
}
