[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
. (Join-Path $root "engine\VisualStudioLayout.ps1")
$configPath = [IO.Path]::GetFullPath((Join-Path $root "..\config\windows\buildtools.vsconfig"))
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { throw "Visual Studio configuration not found: $configPath" }
Write-Host "🛠️  Visual Studio Build Tools — toolchain Windows"
$result = Invoke-VisualStudioLayoutInstall -ConfigPath $configPath
if ($result -eq 10) { Write-Host "🔄 Visual Studio demande un redémarrage." -ForegroundColor Yellow; exit 10 }
$vswhere = @(
  (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe"),
  (Join-Path ([Environment]::GetEnvironmentVariable("ProgramFiles(x86)")) "Microsoft Visual Studio\Installer\vswhere.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $vswhere) { throw "vswhere.exe introuvable après installation." }
$instance = & $vswhere -latest -products "*" -requires Microsoft.VisualStudio.Workload.VCTools -format json 2>$null | ConvertFrom-Json
if (-not $instance) { throw "Le workload C++ Visual Studio n'est pas détecté." }
Write-Host "✅ Visual Studio Build Tools prêt : $($instance.installationPath)"
exit 0
