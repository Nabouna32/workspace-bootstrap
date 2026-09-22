[CmdletBinding()]
param()
$ErrorActionPreference = "Continue"
$fail = 0
function Test-CommandVersion([string]$Name) {
  $command = Get-Command $Name -ErrorAction SilentlyContinue
  if (-not $command) { Write-Warning "MISSING $Name"; $script:fail=1; return }
  Write-Host "=== $Name ==="; & $command.Source --version
  if ($LASTEXITCODE -ne 0) { Write-Warning "$Name returned exit code $LASTEXITCODE"; $script:fail=1 }
}
Write-Host "=== Windows toolchain verification ==="
Test-CommandVersion "git"; Test-CommandVersion "gh"; Test-CommandVersion "cmake"; Test-CommandVersion "ninja"
$vswhere = @(
  (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe"),
  (Join-Path ([Environment]::GetEnvironmentVariable("ProgramFiles(x86)")) "Microsoft Visual Studio\Installer\vswhere.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $vswhere) { Write-Warning "vswhere.exe is missing."; $fail=1 } else {
  $checks=@("Microsoft.VisualStudio.Workload.VCTools","Microsoft.VisualStudio.Component.VC.Tools.x86.x64","Microsoft.VisualStudio.Component.VC.CMake.Project","Microsoft.VisualStudio.Component.Windows11SDK.26100","Microsoft.VisualStudio.Component.TestTools.BuildTools")
  foreach($component in $checks) {
    $result=& $vswhere -latest -products "*" -requires $component -format json 2>$null | ConvertFrom-Json
    if($result){Write-Host "OK Visual Studio: $component"}else{Write-Warning "Missing Visual Studio component: $component";$fail=1}
  }
  $instance=& $vswhere -latest -products "*" -requires Microsoft.VisualStudio.Workload.VCTools -format json 2>$null | ConvertFrom-Json
  if($instance){
    $msvcRoot=Join-Path $instance.installationPath "VC\Tools\MSVC"
    $compiler=Get-ChildItem $msvcRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | ForEach-Object {Join-Path $_.FullName "bin\Hostx64\x64\cl.exe"} | Where-Object {Test-Path $_} | Select-Object -First 1
    if($compiler){Write-Host "OK MSVC x64 compiler: $compiler"}else{Write-Warning "No usable MSVC x64 compiler found."; $fail=1}
  }
}
$flutter=Get-Command flutter -ErrorAction SilentlyContinue
if($flutter){Write-Host "=== Flutter Windows ==="; & $flutter.Source --version; & $flutter.Source doctor -v; & $flutter.Source devices;if($LASTEXITCODE -ne 0){$fail=1}}else{Write-Warning "Flutter is not installed."; $fail=1}
if(Get-Command wsl.exe -ErrorAction SilentlyContinue){Write-Host "=== WSL ===";wsl.exe --status;wsl.exe --list --verbose;if($LASTEXITCODE -ne 0){$fail=1}}
Write-Host "Android SDK / emulator verification is performed by the WSL bootstrap, not Windows." -ForegroundColor Cyan
exit $fail
