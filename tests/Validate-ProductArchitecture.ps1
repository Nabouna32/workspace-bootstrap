[CmdletBinding()]
param()
$ErrorActionPreference="Stop"
$root=Split-Path $PSScriptRoot -Parent
$catalog=Get-Content (Join-Path $root "bootstrap\windows\components\catalog.json") -Raw|ConvertFrom-Json
$profiles=@(Get-ChildItem (Join-Path $root "bootstrap\windows\profiles") -Filter "*.json"|ForEach-Object{Get-Content $_.FullName -Raw|ConvertFrom-Json})
$errors=[System.Collections.Generic.List[string]]::new()
function Assert([bool]$condition,[string]$message){if(-not $condition){[void]$errors.Add($message)}}
$ids=@($catalog.components)
foreach($profile in $profiles){
    Assert ($profile.PSObject.Properties.Name -contains "components") "Profile $($profile.id) has no components list."
    foreach($id in @($profile.components)){Assert ($ids -contains $id) "Profile $($profile.id) references unknown component '$id'."}
}
Assert ($ids -notcontains "windows-cleanup") "Obsolete cleanup component remains in catalog."
Assert (-not (Test-Path (Join-Path $root "bootstrap\windows\engine\Cleanup.ps1"))) "Obsolete Cleanup engine remains."
Assert (-not (Test-Path (Join-Path $root "bootstrap\windows\engine\Cache.ps1"))) "Obsolete Cache engine remains."
Assert (-not (Test-Path (Join-Path $root "bootstrap\windows\engine\Artifact.ps1"))) "Obsolete Artifact engine remains."
Assert ($profiles.id -contains "development-extended") "Development Extended profile is missing."
$extended=($profiles|Where-Object id -eq "development-extended")
foreach($required in @("vscode","intellij-community","python","rustup","go","llvm","temurin21")){Assert (@($extended.components)-contains $required) "Development Extended lacks $required."}
$check=Get-Content (Join-Path $root "bootstrap\windows\engine\Check.ps1") -Raw
Assert ($check -match 'winget-package') "State engine lacks WinGet package checks."
Assert ($check -match 'vswhere\.exe') "Visual Studio detection must use vswhere.exe."

$componentManifests=@(Get-ChildItem (Join-Path $root "bootstrap\windows\components") -Filter "component.json" -Recurse -File)
foreach($manifestFile in $componentManifests){
    $manifestText=Get-Content $manifestFile.FullName -Raw
    Assert ($manifestText -notmatch '"cacheMode"') "Obsolete cacheMode remains in $($manifestFile.FullName)."
}
$bootstrapFiles=@(Get-ChildItem (Join-Path $root "bootstrap") -Recurse -File | Where-Object { $_.Extension -in @(".ps1",".sh",".json") })
foreach($file in $bootstrapFiles){
    $fileText=Get-Content $file.FullName -Raw
    Assert ($fileText -notmatch 'DEV_ENV_(CACHE_ROOT|OFFLINE)') "Obsolete offline/cache environment contract remains in $($file.FullName)."
    Assert ($fileText -notmatch '\-Offline\b') "Obsolete -Offline provisioning switch remains in $($file.FullName)."
}
if($errors.Count){$errors|ForEach-Object{Write-Error $_};exit 1}
Write-Host "Product architecture validation: PASS"
exit 0
