[CmdletBinding()]
param()
$ErrorActionPreference="Stop"
$root=Split-Path $PSScriptRoot -Parent
$update=Get-Content (Join-Path $root "bootstrap\windows\engine\Update.ps1") -Raw
$maintenance=Get-Content (Join-Path $root "bootstrap\windows\engine\Maintenance.ps1") -Raw
$launcher=Get-Content (Join-Path $root "bootstrap\windows\dev-env-core.ps1") -Raw
$runner=Get-Content (Join-Path $root "bootstrap\windows\engine\Runner.ps1") -Raw
$flutter=Get-Content (Join-Path $root "bootstrap\windows\45-flutter.ps1") -Raw
$errors=[System.Collections.Generic.List[string]]::new()
function Assert-Contains([string]$text,[string]$pattern,[string]$description){if($text -notmatch $pattern){[void]$errors.Add($description)}}
function Assert-NotContains([string]$text,[string]$pattern,[string]$description){if($text -match $pattern){[void]$errors.Add($description)}}
Assert-Contains $launcher 'Update\.ps1' 'Main launcher does not load update engine.'
Assert-Contains $launcher 'Maintenance\.ps1' 'Main launcher does not load maintenance engine.'
Assert-Contains $launcher '\[8\].*Maintenance' 'Main menu has no Maintenance Center action.'
Assert-Contains $launcher '\[10\].*Mettre à jour' 'Main menu has no update-all action.'
Assert-Contains $launcher 'Show-UpdateStatus' 'Main menu does not expose update status.'
Assert-Contains $maintenance 'ms-settings:windowsupdate' 'Maintenance Center cannot open Windows Update.'
Assert-Contains $maintenance 'DISM.exe' 'Maintenance Center lacks DISM.'
Assert-Contains $maintenance 'sfc.exe' 'Maintenance Center lacks SFC.'
Assert-Contains $maintenance 'StartComponentCleanup' 'Maintenance Center lacks Component Store cleanup.'
Assert-Contains $maintenance 'wsl.exe' 'Maintenance Center lacks WSL health checks.'
Assert-Contains $maintenance 'docker version' 'Maintenance Center lacks Docker health checks.'
Assert-Contains $maintenance 'adb version' 'Maintenance Center lacks Android health checks.'
Assert-Contains $maintenance 'flutter --version' 'Maintenance Center lacks Flutter health checks.'
Assert-Contains $maintenance 'winsock reset' 'Maintenance Center lacks explicit Winsock repair.'
Assert-Contains $maintenance 'New-MaintenanceReport' 'Maintenance Center lacks report generation.'
Assert-Contains $maintenance '/dev/kvm' 'Maintenance Center lacks KVM health checks.'
Assert-NotContains $runner 'DEV_ENV_CACHE_ROOT|DEV_ENV_OFFLINE|\-Offline|Artifact|cacheMode' 'Runner still depends on removed cache/offline system.'
Assert-Contains $flutter 'releases_windows\.json' 'Flutter installer does not resolve official stable manifest.'
Assert-Contains $flutter 'Get-FileHash.*SHA256' 'Flutter installer does not verify checksum.'
Assert-NotContains $flutter 'Cache\.ps1|Artifact\.ps1|DEV_ENV_OFFLINE' 'Flutter installer still depends on removed cache/offline workflow.'
Assert-Contains $update 'winget upgrade --all' 'Update engine does not update WinGet-managed applications.'
Assert-Contains $update '\$flutter\.Source.*upgrade' 'Update engine does not update Flutter.'
Assert-Contains $update 'Windows Update, pilotes' 'Update scope is not explicit.'
Assert-NotContains $update 'DEV_ENV_OFFLINE|Mode offline' 'Update engine still contains offline mode.'
foreach($path in @("bootstrap\windows\engine\Artifact.ps1","bootstrap\windows\engine\Cache.ps1","bootstrap\windows\engine\Cleanup.ps1","tests\Test-Cache.ps1","docs\CACHE-POLICY.md","bootstrap\windows\components\windows-cleanup","bootstrap\windows\profiles\maintenance.json")){if(Test-Path (Join-Path $root $path)){[void]$errors.Add("Removed obsolete path remains: $path")}}
if($errors.Count){$errors|ForEach-Object{Write-Error $_};exit 1}
Write-Host "Windows maintenance workflow validation: PASS"
exit 0
