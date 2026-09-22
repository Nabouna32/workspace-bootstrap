Set-StrictMode -Version Latest

function Get-WingetPackageVersion {
    param([Parameter(Mandatory)][string]$PackageId,[string]$Source = "winget")
    $output = & winget show --id $PackageId --exact --source $Source --accept-source-agreements 2>&1
    if ($LASTEXITCODE -ne 0) { throw "WinGet could not resolve package $PackageId (exit code $LASTEXITCODE)." }
    $text = $output -join [Environment]::NewLine
    $match = $text | Select-String -Pattern "(?im)^\s*Version\s*:\s*([^\s]+)"
    if (-not $match) { throw "WinGet output for $PackageId did not contain a parseable Version field." }
    return $match.Matches[0].Groups[1].Value.Trim()
}

function Get-InstallerFileFromDirectory {
    param([Parameter(Mandatory)][string]$Directory)
    $files = @(Get-ChildItem -LiteralPath $Directory -File -Recurse | Where-Object { $_.Extension -in @(".exe",".msi",".msix",".msixbundle",".appx",".appxbundle") })
    if ($files.Count -eq 0) { throw "WinGet downloaded no supported installer artifact in $Directory." }
    if ($files.Count -gt 1) {
        $preferred = $files | Where-Object { $_.Extension -in @(".exe",".msi") } | Select-Object -First 1
        if ($preferred) { return $preferred.FullName }
        throw "WinGet downloaded multiple installer artifacts; deterministic selection is unsafe."
    }
    return $files[0].FullName
}


function Get-InstalledComponentVersion {
    param([Parameter(Mandatory)]$Component)
    if (-not ($Component.PSObject.Properties.Name -contains "versionCommand")) { return $null }
    $command = Get-Command ([string]$Component.versionCommand) -ErrorAction SilentlyContinue
    if (-not $command) { return $null }
    $arguments = @($Component.versionArguments)
    $output = & $command.Source @arguments 2>$null | Select-Object -First 1
    if ($output -match "(\d+(?:\.\d+){1,3})") { return [version]$Matches[1] }
    return $null
}
function Get-WingetCachedInstaller {
    param([Parameter(Mandatory)]$Component,[Parameter(Mandatory)][string]$CacheRoot)
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { throw "winget is unavailable." }
    $packageId = [string]$Component.packageId
    $source = if ($Component.source) {[string]$Component.source} else {"winget"}
    $version = Get-WingetPackageVersion -PackageId $packageId -Source $source
    $installedVersion = Get-InstalledComponentVersion -Component $Component
    if ($Component.versionPolicy -eq "latest-stable" -and $installedVersion) {
        if ($installedVersion -ge [version]$version) {
            Write-Host "[$($Component.id)] Installed version $installedVersion already satisfies resolved stable version $version; no installer action required."
            return [pscustomobject]@{Version=$version;Path=$null;Manifest=$null;AlreadyCompliant=$true}
        }
    }
    $versionRoot = Get-CacheVersionRoot $CacheRoot $Component.id $version
    $manifestPath = Join-Path $versionRoot "artifact.json"
    if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $artifact = Join-Path $versionRoot ([string]$manifest.fileName)
        if ((Test-Path -LiteralPath $artifact -PathType Leaf) -and $manifest.sha256) {
            $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifact).Hash
            if ($actual -ieq [string]$manifest.sha256) {
                Write-Host "[$($Component.id)] Cache HIT: $version / $($manifest.fileName)"
                return [pscustomobject]@{Version=$version;Path=$artifact;Manifest=$manifest}
            }
            Write-Warning "[$($Component.id)] Cached artifact hash mismatch; invalidating cache entry."
            Remove-Item -LiteralPath $artifact -Force -ErrorAction SilentlyContinue
        }
    }
    $staging = Join-Path $CacheRoot (Join-Path ".staging" ([guid]::NewGuid().ToString()))
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    try {
        Write-Host "[$($Component.id)] Cache MISS: downloading $packageId $version with WinGet..."
        $args = @("download","--id",$packageId,"--exact","--version",$version,"--source",$source,"--architecture","x64","--scope","machine","--skip-dependencies","--accept-package-agreements","--accept-source-agreements","--skip-license","--disable-interactivity","--download-directory",$staging)
        & winget @args
        if ($LASTEXITCODE -ne 0) { throw "WinGet failed to download $packageId $version (exit code $LASTEXITCODE)." }
        $downloaded = Get-InstallerFileFromDirectory -Directory $staging
        $sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $downloaded).Hash
        $destinationRoot = Get-CacheVersionRoot $CacheRoot $Component.id $version
        New-Item -ItemType Directory -Force -Path $destinationRoot | Out-Null
        $destination = Join-Path $destinationRoot (Split-Path -Leaf $downloaded)
        Move-Item -LiteralPath $downloaded -Destination $destination -Force
        Write-CacheManifest -Root $CacheRoot -Component $Component.id -Version $version -FileName (Split-Path -Leaf $destination) -Sha256 $sha256 -Source "winget" -Architecture "x64" | Out-Null
        Write-Host "[$($Component.id)] Cached installer: $destination"
        return [pscustomobject]@{Version=$version;Path=$destination;Manifest=(Get-Content (Join-Path $destinationRoot "artifact.json") -Raw | ConvertFrom-Json)}
    } finally {
        if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

function Install-CachedInstaller {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)]$Component)
    $type = ([string]$Component.installerType).ToLowerInvariant()
    $args = @($Component.installArguments)
    switch ($type) {
        "msi" { $process = Start-Process -FilePath "msiexec.exe" -ArgumentList (@("/i",$Path)+$args) -Wait -PassThru; return $process.ExitCode }
        "exe" { $process = Start-Process -FilePath $Path -ArgumentList $args -Wait -PassThru; return $process.ExitCode }
        default { throw "Unsupported cached installer type $type for $($Component.id)." }
    }
}

function Invoke-WingetCachedPackage {
    param([Parameter(Mandatory)]$Component,[Parameter(Mandatory)][string]$CacheRoot)
    $cached = Get-WingetCachedInstaller -Component $Component -CacheRoot $CacheRoot
    if ($cached.PSObject.Properties.Name -contains "AlreadyCompliant" -and $cached.AlreadyCompliant) { return 0 }
    Write-Host "[$($Component.id)] Installing cached artifact version $($cached.Version)..."
    $code = Install-CachedInstaller -Path $cached.Path -Component $Component
    if ($code -eq 3010) { return 10 }
    if ($code -ne 0) { throw "Cached installer for $($Component.id) failed with exit code $code." }
    return 0
}