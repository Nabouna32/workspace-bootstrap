[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) { throw "wsl.exe is unavailable on this Windows installation." }
# On a fresh Windows installation, wsl.exe exists but the WSL optional component is not installed yet.
# With Stop, native stderr can surface as a terminating NativeCommandError, so probe it explicitly.
$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$distros = @(wsl.exe --list --quiet 2>&1) |
    ForEach-Object { $_.ToString().Trim([char]0).Trim() } |
    Where-Object { $_ }
$wslListExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference

if ($wslListExitCode -ne 0) {
  Write-Host "WSL is not initialized on this Windows installation. Installing WSL and Ubuntu..." -ForegroundColor Yellow
  & wsl.exe --install --distribution Ubuntu --no-launch
  if ($LASTEXITCODE -ne 0) { throw "WSL Ubuntu installation failed (exit code $LASTEXITCODE)." }
  Write-Host ""
  Write-Host "WSL installation is ready. Windows must restart once before Ubuntu can be initialized." -ForegroundColor Yellow
  Write-Host "The bootstrap state is preserved; rerun the tool after Windows restarts." -ForegroundColor DarkGray
  exit 10
}
$ubuntu = $distros | Where-Object { $_ -match "^Ubuntu$" }
if (-not $ubuntu) {
  Write-Host "Ubuntu is not installed. Installing the current Ubuntu distribution through WSL..."
  & wsl.exe --install --distribution Ubuntu --no-launch
  if ($LASTEXITCODE -ne 0) { throw "WSL Ubuntu installation failed (exit code $LASTEXITCODE)." }
  Write-Host ""
  Write-Host "WSL installation is ready. Windows must restart once before Ubuntu can be initialized." -ForegroundColor Yellow
  Write-Host "The bootstrap state is preserved; rerun the tool after Windows restarts." -ForegroundColor DarkGray
  exit 10
}
& wsl.exe --set-default-version 2
if ($LASTEXITCODE -ne 0) { throw "Unable to set WSL 2 as the default version." }
& wsl.exe --set-default Ubuntu
if ($LASTEXITCODE -ne 0) { throw "Unable to set Ubuntu as the default WSL distribution." }
$scriptWindowsPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\wsl\bootstrap.sh"))
if (-not (Test-Path -LiteralPath $scriptWindowsPath -PathType Leaf)) { throw "WSL bootstrap script not found: $scriptWindowsPath" }
$scriptLinuxPath = (& wsl.exe -d Ubuntu -- wslpath -a ($scriptWindowsPath -replace "\\","/") 2>$null | Select-Object -Last 1).Trim()
if (-not $scriptLinuxPath) { throw "Unable to convert the WSL bootstrap path." }
Write-Host ""
Write-Host "🐧 Lancement de la configuration interactive WSL..." -ForegroundColor Cyan
& wsl.exe -d Ubuntu -- bash -lc "bash '$scriptLinuxPath'"
$code = $LASTEXITCODE
if ($code -eq 11) {
  Write-Host "🔄 Redémarrage interne de WSL requis pour appliquer les groupes/permissions/systemd." -ForegroundColor Yellow
  & wsl.exe --shutdown
  if ($LASTEXITCODE -ne 0) { throw "Unable to restart WSL automatically." }
  Start-Sleep -Seconds 2
  & wsl.exe -d Ubuntu -- bash -lc "bash '$scriptLinuxPath'"
  $code = $LASTEXITCODE
}
if ($code -ne 0) { throw "WSL development bootstrap failed with exit code $code." }
Write-Host "WSL/Ubuntu development environment is ready." -ForegroundColor Green

# One CI runner is intentionally provisioned: Linux inside WSL.
# Windows only wakes Ubuntu at user logon so WSL systemd can start the runner in the background.
$runnerStateCommand = 'awk -F= ''/^runner_enabled=/{gsub(/"/,"",$2); print $2}'' ~/.config/dev-environment/wsl-config.env 2>/dev/null | tail -n1'
$runnerEnabled = (& wsl.exe -d Ubuntu -- bash -lc $runnerStateCommand).Trim()
$taskName = "DevEnvironment-WSL-GitHubRunner"
$wslExe = Join-Path $env:SystemRoot "System32\wsl.exe"

if ($runnerEnabled -eq "yes") {
  $taskCommand = '"' + $wslExe + '" -d Ubuntu --exec /bin/true'
  & schtasks.exe /Create /TN $taskName /SC ONLOGON /DELAY 0000:30 /TR $taskCommand /F | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "Unable to create the WSL GitHub runner startup task." }
  Write-Host "GitHub Actions runner: WSL-only; Windows startup task configured." -ForegroundColor Green
} else {
  & schtasks.exe /Delete /TN $taskName /F 2>$null | Out-Null
  Write-Host "GitHub Actions runner disabled; Windows startup task removed." -ForegroundColor DarkGray
}

exit 0