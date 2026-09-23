param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot
)

$ErrorActionPreference = 'Stop'

$desktop = Join-Path $PackageRoot 'WorkspaceControl\WorkspaceControl.exe'
$cli = Join-Path $PackageRoot 'WorkspaceBootstrap.Cli.exe'
$catalog = Join-Path $PackageRoot 'bootstrap\windows\components\catalog.json'
$profiles = Join-Path $PackageRoot 'bootstrap\windows\profiles'
$readme = Join-Path $PackageRoot 'README.md'
$license = Join-Path $PackageRoot 'LICENSE'
$englishResources = Join-Path $PackageRoot 'WorkspaceControl\Strings\en-US\Resources.resw'
$frenchResources = Join-Path $PackageRoot 'WorkspaceControl\Strings\fr-FR\Resources.resw'

$requiredFiles = @(
    $desktop,
    $cli,
    $catalog,
    $readme,
    $license,
    $englishResources,
    $frenchResources
)

foreach ($path in $requiredFiles) {
    if (-not (Test-Path $path -PathType Leaf)) {
        throw "Published package is missing required file: $path"
    }
}

if (-not (Test-Path $profiles -PathType Container)) {
    throw "Published package is missing the bundled profiles directory: $profiles"
}

Write-Host "Running published CLI capability smoke test..."
& $cli capabilities
if ($LASTEXITCODE -ne 0) {
    throw "Published CLI smoke test failed with exit code $LASTEXITCODE."
}

Write-Host "Launching published WinUI desktop application..."
$process = Start-Process -FilePath $desktop -WorkingDirectory (Split-Path $desktop) -PassThru

try {
    Start-Sleep -Seconds 10

    if ($process.HasExited) {
        throw "Published WinUI desktop process exited during the launch smoke test with code $($process.ExitCode)."
    }

    Write-Host "Published WinUI desktop application remained running for the launch smoke test."
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
