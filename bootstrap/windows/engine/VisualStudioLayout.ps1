Set-StrictMode -Version Latest

function Get-VisualStudioBootstrapperVersion {
    param([Parameter(Mandatory)][string]$Path)
    $version = (Get-Item -LiteralPath $Path).VersionInfo.ProductVersion
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Unable to determine Visual Studio bootstrapper product version: $Path"
    }
    if ($version -match '^(\d+\.\d+\.\d+(?:\.\d+)?)') { return $Matches[1] }
    throw "Unrecognized Visual Studio bootstrapper product version '$version'."
}

function Invoke-VisualStudioLayoutInstall {
    param([Parameter(Mandatory)][string]$ConfigPath)

    $sourceUrl = "https://aka.ms/vs/stable/vs_buildtools.exe"
    $tempRoot = Join-Path $env:TEMP ("bouna-vs-" + [guid]::NewGuid().ToString("N"))
    $bootstrapper = Join-Path $tempRoot "vs_buildtools.exe"

    try {
        New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
        Write-Host "[visual-studio] Downloading current stable Build Tools bootstrapper..."
        Invoke-WebRequest -Uri $sourceUrl -OutFile $bootstrapper -UseBasicParsing

        $version = Get-VisualStudioBootstrapperVersion -Path $bootstrapper
        Write-Host "[visual-studio] Online install: Build Tools $version"

        $vswhere = @(
            (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe"),
            (Join-Path ([Environment]::GetEnvironmentVariable("ProgramFiles(x86)")) "Microsoft Visual Studio\Installer\vswhere.exe")
        ) | Where-Object { Test-Path $_ } | Select-Object -First 1

        $instances = @()
        if ($vswhere) {
            $raw = & $vswhere -latest -products "*" -format json 2>$null
            if ($raw) { $instances = @($raw | ConvertFrom-Json) }
        }

        if ($instances.Count -gt 0) {
            $installPath = [string]$instances[0].installationPath
            $arguments = @(
                "--installPath", $installPath,
                "--config", $ConfigPath,
                "--passive",
                "--norestart"
            )
        } else {
            $arguments = @(
                "--config", $ConfigPath,
                "--passive",
                "--norestart"
            )
        }

        $process = Start-Process -FilePath $bootstrapper -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -eq 3010) { return 10 }
        if ($process.ExitCode -ne 0) {
            throw "Visual Studio installation failed with exit code $($process.ExitCode)."
        }
        return 0
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
