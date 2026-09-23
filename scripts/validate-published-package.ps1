param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [string]$DiagnosticsDirectory
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

$werKey = 'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\WorkspaceControl.exe'
$werConfigured = $false
$werCreated = $false
$dumpFilesBefore = @()

if ($DiagnosticsDirectory) {
    if (-not (Test-Path $DiagnosticsDirectory -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $DiagnosticsDirectory | Out-Null
    }

    $werDumpDirectory = Join-Path $DiagnosticsDirectory 'dumps'
    New-Item -ItemType Directory -Force -Path $werDumpDirectory | Out-Null
    $dumpFilesBefore = @(Get-ChildItem -Path $werDumpDirectory -Filter '*.dmp' -File -ErrorAction SilentlyContinue)

    try {
        if (Test-Path $werKey) {
            Remove-Item -Path $werKey -Recurse -Force
        }

        New-Item -Path $werKey -Force | Out-Null
        New-ItemProperty -Path $werKey -Name 'DumpFolder' -PropertyType ExpandString -Value $werDumpDirectory -Force | Out-Null
        New-ItemProperty -Path $werKey -Name 'DumpType' -PropertyType DWord -Value 2 -Force | Out-Null
        New-ItemProperty -Path $werKey -Name 'DumpCount' -PropertyType DWord -Value 1 -Force | Out-Null
        $werConfigured = $true
        $werCreated = $true
        Write-Host "Configured WER local dump capture: $werDumpDirectory"
    }
    catch {
        Write-Warning "Could not configure WER local dump capture: $($_.Exception.Message)"
    }
}

$process = $null

try {
    Write-Host "Launching published WinUI desktop application..."
    $process = Start-Process -FilePath $desktop -WorkingDirectory (Split-Path $desktop) -PassThru

    Start-Sleep -Seconds 10

    if ($process.HasExited) {
        if ($DiagnosticsDirectory -and $werConfigured) {
            Start-Sleep -Seconds 3
            $dumpFilesAfter = @(Get-ChildItem -Path (Join-Path $DiagnosticsDirectory 'dumps') -Filter '*.dmp' -File -ErrorAction SilentlyContinue)
            $newDump = $dumpFilesAfter | Where-Object {
                $before = $dumpFilesBefore | Where-Object { $_.FullName -eq $_.FullName }
                $_.LastWriteTimeUtc -ge $process.StartTime.ToUniversalTime()
            } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1

            if ($newDump) {
                Write-Host "Captured native crash dump: $($newDump.FullName)"

                $cdbCandidates = @(
                    (Get-Command cdb.exe -ErrorAction SilentlyContinue).Source,
                    "$env:ProgramFiles\Windows Kits\10\Debuggers\x64\cdb.exe",
                    "$env:ProgramFiles(x86)\Windows Kits\10\Debuggers\x64\cdb.exe"
                ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique

                if ($cdbCandidates.Count -gt 0) {
                    $analysisPath = Join-Path $DiagnosticsDirectory 'cdb-analysis.txt'
                    & $cdbCandidates[0] -z $newDump.FullName -c ".symfix; .reload; !analyze -v; lm; q" 2>&1 |
                        Tee-Object -FilePath $analysisPath
                }
                else {
                    Write-Warning "CDB was not found on the runner; the native crash dump will be uploaded for offline analysis."
                }
            }
            else {
                Write-Warning "The application exited but WER did not produce a local dump."
            }
        }

        throw "Published WinUI desktop process exited during the launch smoke test with code $($process.ExitCode)."
    }

    Write-Host "Published WinUI desktop application remained running for the launch smoke test."
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }

    if ($werCreated) {
        Remove-Item -Path $werKey -Recurse -Force -ErrorAction SilentlyContinue
    }
}
