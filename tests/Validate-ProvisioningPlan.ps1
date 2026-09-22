[CmdletBinding()]
param()
$ErrorActionPreference="Stop"
$root=Split-Path $PSScriptRoot -Parent
$launcher=Get-Content (Join-Path $root "bootstrap\windows\dev-env-core.ps1") -Raw
$errors=[System.Collections.Generic.List[string]]::new()
function Assert-Contains([string]$text,[string]$pattern,[string]$description){if($text -notmatch $pattern){[void]$errors.Add($description)}}
function Assert-NotContains([string]$text,[string]$pattern,[string]$description){if($text -match $pattern){[void]$errors.Add($description)}}
Assert-Contains $launcher '\[switch\]\$PlanOnly' 'Launcher does not expose the read-only plan mode.'
Assert-Contains $launcher '\[string\]\$Profile' 'Launcher does not expose profile selection for plan mode.'
Assert-Contains $launcher 'Show-ProvisioningPlan' 'Launcher does not implement provisioning plan output.'
Assert-Contains $launcher 'lecture seule' 'Dry-run does not declare read-only behavior.'
Assert-Contains $launcher 'Get-ComponentState -Component \$component' 'Dry-run does not inspect component state.'
Assert-Contains $launcher 'if \(-not \$PlanOnly\)' 'State directory creation is not guarded against plan-only mode.'
Assert-Contains $launcher 'if \(\$PlanOnly\)' 'Launcher does not branch before interactive provisioning when plan-only is requested.'
$stateRootIndex=$launcher.IndexOf('$stateRoot =')
$stateCreateIndex=$launcher.IndexOf('New-Item -ItemType Directory -Force -Path $stateRoot')
if($stateRootIndex -lt 0 -or $stateCreateIndex -lt 0 -or $stateCreateIndex -lt $stateRootIndex){[void]$errors.Add('State directory initialization is not placed after the plan-only guard.')}
$planStart=$launcher.IndexOf("function Show-ProvisioningPlan")
$planEnd=$launcher.IndexOf("function Invoke-Profile",$planStart)
if($planStart -lt 0 -or $planEnd -le $planStart){[void]$errors.Add('Could not isolate the provisioning plan function.')}
else {
    $planFunction=$launcher.Substring($planStart,$planEnd-$planStart)
    Assert-NotContains $planFunction 'Invoke-Component' 'Dry-run function must not invoke component installers.'
}
if($errors.Count){$errors|ForEach-Object{Write-Error $_};exit 1}
Write-Host "Provisioning dry-run validation: PASS"
exit 0
