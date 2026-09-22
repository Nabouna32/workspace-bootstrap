[CmdletBinding()]
param()
$ErrorActionPreference="Stop"
$root=Split-Path $PSScriptRoot -Parent
$optimization=Get-Content (Join-Path $root "bootstrap\windows\engine\Optimization.ps1") -Raw
$launcher=Get-Content (Join-Path $root "bootstrap\windows\dev-env-core.ps1") -Raw
$errors=[System.Collections.Generic.List[string]]::new()
function Assert-Contains([string]$text,[string]$pattern,[string]$description){if($text -notmatch $pattern){[void]$errors.Add($description)}}
function Assert-NotContains([string]$text,[string]$pattern,[string]$description){if($text -match $pattern){[void]$errors.Add($description)}}
Assert-Contains $optimization 'Get-WindowsOptimizationCatalog' 'Optimization catalog is missing.'
Assert-Contains $optimization 'ValidateSet\("safe","advanced","aggressive"\)' 'Optimization levels are not explicit.'
Assert-Contains $optimization 'Rollback' 'Optimization rollback is missing.'
Assert-Contains $optimization 'Show-WindowsOptimizationPlan' 'Optimization plan preview is missing.'
Assert-Contains $optimization 'risk=' 'Optimization risk metadata is missing.'
Assert-Contains $optimization 'powerPlanAfter' 'Power plan rollback guard is missing.'
Assert-Contains $optimization 'ProgramData.*BounaDevEnvironment.*optimization' 'Optimization state is not persisted.'
Assert-Contains $optimization 'DisableTailoredExperiencesWithDiagnosticData' 'Safe privacy optimization is missing.'
Assert-Contains $optimization 'AdvertisingInfo' 'Advertising ID optimization is missing.'
Assert-Contains $optimization 'performance-high-power' 'General performance optimization is missing.'
Assert-Contains $optimization 'Get-AggressiveAppxAllowlist' 'Aggressive debloat allowlist is missing.'
Assert-Contains $optimization 'Remove-AppxPackage' 'Explicit AppX debloat action is missing.'
Assert-Contains $optimization 'Microsoft.BingNews' 'Aggressive AppX allowlist is missing expected consumer package.'
Assert-NotContains $optimization 'Stop-Service|schtasks.*delete' 'Optimization must not disable services or scheduled tasks blindly.'
Assert-Contains $launcher 'Optimization\.ps1' 'Main launcher does not load optimization engine.'
Assert-Contains $launcher 'Maintenance\.ps1' 'Main launcher does not load maintenance engine.'
Assert-Contains $launcher 'development-extended' 'Main launcher does not expose the extended development profile.'
Assert-Contains $launcher 'Show-WindowsMaintenanceMenu' 'Main launcher does not expose Maintenance Center.'
foreach($path in @("bootstrap\windows\engine\Artifact.ps1","bootstrap\windows\engine\Cache.ps1","bootstrap\windows\engine\Cleanup.ps1","tests\Test-Cache.ps1","docs\CACHE-POLICY.md","bootstrap\windows\components\windows-cleanup")){if(Test-Path (Join-Path $root $path)){[void]$errors.Add("Obsolete cache/cleanup path remains: $path")}}
if($errors.Count){$errors|ForEach-Object{Write-Error $_};exit 1}
Write-Host "Windows optimization validation: PASS"
exit 0
