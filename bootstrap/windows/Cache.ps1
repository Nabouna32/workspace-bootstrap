Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-DevEnvironmentCacheRoot {
    if ($env:DEV_ENVIRONMENT_CACHE) { return $env:DEV_ENVIRONMENT_CACHE }
    return "C:\DevCache"
}

function Initialize-DevEnvironmentCache {
    $root = Get-DevEnvironmentCacheRoot
    @($root, (Join-Path $root "installers"), (Join-Path $root "metadata"), (Join-Path $root "staging")) | ForEach-Object {
        New-Item -ItemType Directory -Force -Path $_ | Out-Null
    }
    return $root
}

function Get-SafeCacheName {
    param([Parameter(Mandatory)][string]$Value)
    return ($Value -replace '[^A-Za-z0-9._-]', '_')
}

function Get-WingetPackageVersion {
    param([Parameter(Mandatory)][string]$PackageId, [string]$InstallerType, [string]$Locale = "fr-FR", [string]$Architecture = "x64")
    $args = @("show","--id",$PackageId,"--exact","--source","winget","--architecture",$Architecture,"--locale",$Locale,"--accept-source-agreements","--disable-interactivity")
    if ($InstallerType) { $args += @("--installer-type",$InstallerType) }
    $output = @(& winget @args 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Impossible de déterminer la dernière version WinGet de '$PackageId'." }
    foreach ($line in $output) {
        if ([string]$line -match '^\s*Version\s*:?\s*(\S+)\s*$') { return $Matches[1] }
    }
    throw "WinGet n'a pas retourné de version exploitable pour '$PackageId'."
}

function Get-CachedPackageMetadata {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$Version,
        [string]$Locale = "fr-FR",
        [string]$Architecture = "x64",
        [string]$InstallerType,
        [string]$Source = "unknown"
    )
    $root = Initialize-DevEnvironmentCache
    $safeId = Get-SafeCacheName $PackageId
    $key = "{0}-{1}-{2}-{3}-{4}" -f (Get-SafeCacheName $Version), (Get-SafeCacheName $Locale), (Get-SafeCacheName $Architecture), (Get-SafeCacheName $(if ($InstallerType) { $InstallerType } else { "auto" })), (Get-SafeCacheName $Source)
    $path = Join-Path $root ("metadata\{0}\{1}.json" -f $safeId, $key)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }

    try {
        $metadata = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($metadata.PSObject.Properties.Name -contains "relativePath" -and $metadata.relativePath) {
            $metadata.path = Join-Path $root ([string]$metadata.relativePath)
        } elseif ($metadata.path -and ([string]$metadata.path -match '\\installers\\')) {
            $relative = ([string]$metadata.path) -replace '^.*?installers\\', 'installers\\'
            $metadata | Add-Member -NotePropertyName relativePath -NotePropertyValue $relative -Force
            $metadata.path = Join-Path $root $relative
        }
        return $metadata
    } catch {
        return $null
    }
}

function Save-CachedPackageMetadata {
    param([Parameter(Mandatory)]$Metadata)
    $root = Initialize-DevEnvironmentCache
    $safeId = Get-SafeCacheName ([string]$Metadata.packageId)
    $directory = Join-Path $root ("metadata\{0}" -f $safeId)
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    if (-not ($Metadata.PSObject.Properties.Name -contains "relativePath") -and $Metadata.path) {
        $absolute = [string]$Metadata.path
        if ($absolute.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
            $relative = $absolute.Substring($root.Length).TrimStart('\')
            $Metadata | Add-Member -NotePropertyName relativePath -NotePropertyValue $relative -Force
        }
    }
    $key = "{0}-{1}-{2}-{3}-{4}" -f (Get-SafeCacheName ([string]$Metadata.version)), (Get-SafeCacheName ([string]$Metadata.locale)), (Get-SafeCacheName ([string]$Metadata.architecture)), (Get-SafeCacheName $(if ($Metadata.installerType) { $Metadata.installerType } else { "auto" })), (Get-SafeCacheName ([string]$Metadata.source))
    $path = Join-Path $directory ("{0}.json" -f $key)
    $Metadata | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Get-GitHubRelease {
    param([Parameter(Mandatory)][string]$Repository)
    $headers = @{ Accept = "application/vnd.github+json"; "User-Agent" = "DevEnvironment/1.0" }
    return Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers -Method Get
}

function Resolve-OfficialSource {
    param([Parameter(Mandatory)]$Component, [Parameter(Mandatory)][string]$Locale, [Parameter(Mandatory)][string]$Architecture)
    if ($Component.PSObject.Properties.Name -notcontains "officialSource") { return $null }
    $source = $Component.officialSource
    if (-not $source) { return $null }

    if ($source.type -eq "github-release") {
        $release = Get-GitHubRelease -Repository ([string]$source.repository)
        $asset = @($release.assets | Where-Object { $_.name -match [string]$source.assetRegex } | Select-Object -First 1)
        if ($asset.Count -ne 1) { throw "La release officielle de '$($Component.name)' ne contient pas l'asset attendu." }
        $version = [string]$release.tag_name
        if ($source.PSObject.Properties.Name -contains "versionRegex" -and $version -match [string]$source.versionRegex) { $version = $Matches[1] }
        elseif ($version.StartsWith("v")) { $version = $version.Substring(1) }
        $digest = $null
        if ($asset.PSObject.Properties.Name -contains "digest" -and $asset.digest) { $digest = ([string]$asset.digest) -replace '^sha256:', '' }
        return [pscustomobject]@{
            version=$version; tag=[string]$release.tag_name; url=[string]$asset.browser_download_url
            fileName=[string]$asset.name; sha256=$digest; source="official"
            sourceRepository=[string]$source.repository; releaseUrl=[string]$release.html_url
            sourceDefinition=[pscustomobject]@{ kind="github-release"; provider="GitHub"; release=[string]$release.tag_name; asset=[string]$asset.name; url=[string]$asset.browser_download_url }
            locale=$Locale; architecture=$Architecture
        }
    }

    if ($source.type -eq "rarlab-localized") {
        if ($Locale -ine "fr-FR") { throw "WinRAR officiel localisé non pris en charge pour la locale '$Locale'." }
        $page = Invoke-WebRequest -Uri "https://www.rarlab.com/download.htm" -UseBasicParsing
        $pattern = "href\s*=\s*['""]([^'""]*winrar-x64-(\d+)fr\.exe)['""]"
        $match = [regex]::Match([string]$page.Content, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if (-not $match.Success) { throw "Impossible de trouver l'installateur WinRAR x64 localisé officiel." }
        $relative = $match.Groups[1].Value
        $versionCode = $match.Groups[2].Value
        if ($versionCode.Length -lt 3) { throw "Version WinRAR officielle inattendue : $versionCode" }
        $version = "{0}.{1}" -f $versionCode.Substring(0,$versionCode.Length-2), $versionCode.Substring($versionCode.Length-2)
        return [pscustomobject]@{
            version=$version; tag=$version; url=([uri]::new([uri]"https://www.rarlab.com/", $relative)).AbsoluteUri
            fileName=[IO.Path]::GetFileName($relative); sha256=$null; source="official"
            sourceRepository="RARLAB"; releaseUrl="https://www.rarlab.com/download.htm"
            sourceDefinition=[pscustomobject]@{ kind="rarlab-localized"; provider="RARLAB"; release=$version; asset=$match.Groups[1].Value; url=([uri]::new([uri]"https://www.rarlab.com/", $relative)).AbsoluteUri }
            locale=$Locale; architecture=$Architecture
        }
    }

    if ($source.type -eq "chrome-enterprise") {
        $history = Invoke-RestMethod -Uri "https://versionhistory.googleapis.com/v1/chrome/platforms/win64/channels/stable/versions?order_by=version%20desc" -Method Get
        $versionEntry = @($history.versions | Where-Object { $_.version -match '^\d+\.\d+\.\d+\.\d+$' } | Select-Object -First 1)
        if ($versionEntry.Count -ne 1) { throw "Impossible de déterminer la dernière version stable de Chrome depuis l'API officielle." }
        return [pscustomobject]@{
            version=[string]$versionEntry[0].version; tag=[string]$versionEntry[0].version
            url="https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi"
            fileName="googlechromestandaloneenterprise64.msi"; sha256=$null; source="official"
            sourceRepository="Google Chrome Enterprise"; releaseUrl="https://chromeenterprise.google/intl/fr_fr/download/"
            sourceDefinition=[pscustomobject]@{ kind="chrome-enterprise"; provider="Google"; release=[string]$versionEntry[0].version; asset="googlechromestandaloneenterprise64.msi"; url="https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi" }
            locale=$Locale; architecture=$Architecture
        }
    }

    return $null
}

function Test-OfficialInstallerSignature {
    param([Parameter(Mandatory)][string]$Path)
    $extension = [IO.Path]::GetExtension($Path).ToLowerInvariant()
    if ($extension -notin @(".exe",".msi")) { return $true }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne "Valid") {
        throw "La signature Authenticode de l'installateur officiel n'est pas valide : $($signature.Status)."
    }
    return $true
}

function Get-MsiProductVersion {
    param([Parameter(Mandatory)][string]$Path)
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $null
    $view = $null
    try {
        $database = $installer.OpenDatabase($Path, 0)
        $view = $database.OpenView("SELECT Value FROM Property WHERE Property = 'ProductVersion'")
        $view.Execute()
        $record = $view.Fetch()
        if (-not $record) { throw "Le MSI ne contient pas de propriété ProductVersion." }
        return [string]$record.StringData(1)
    } finally {
        if ($view) { $view.Close() }
        if ($database) { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) }
        if ($installer) { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) }
    }
}

function Test-CacheSourceProvenance {
    param([Parameter(Mandatory)]$Metadata)
    if ([string]$Metadata.source -eq "official") {
        if (-not $Metadata.sourceDefinition) { return $false }
        foreach ($name in @("kind","provider","release","asset","url")) {
            if ($Metadata.sourceDefinition.PSObject.Properties.Name -notcontains $name) { return $false }
            if ([string]::IsNullOrWhiteSpace([string]$Metadata.sourceDefinition.$name)) { return $false }
        }
    } elseif ([string]$Metadata.source -eq "winget-fallback") {
        if (-not $Metadata.sourceDefinition) { return $false }
        if ([string]$Metadata.sourceDefinition.kind -ne "package-manager-fallback") { return $false }
        if ([string]$Metadata.sourceDefinition.provider -ne "winget") { return $false }
    }
    return $true
}

function Test-CachedMetadata {
    param([Parameter(Mandatory)]$Metadata)
    if (-not (Test-CacheSourceProvenance -Metadata $Metadata)) { return $false }
    if (-not (Test-Path -LiteralPath $Metadata.path -PathType Leaf)) { return $false }
    $actualHash = (Get-FileHash -LiteralPath $Metadata.path -Algorithm SHA256).Hash
    return $actualHash -eq ([string]$Metadata.sha256).Trim()
}

function Get-LatestCachedMetadata {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [string]$InstallerType,
        [string]$Locale = "fr-FR",
        [string]$Architecture = "x64",
        [Parameter(Mandatory)][string]$Source
    )
    $root = Initialize-DevEnvironmentCache
    $directory = Join-Path $root ("metadata\{0}" -f (Get-SafeCacheName $PackageId))
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) { return $null }

    $entries = foreach ($file in Get-ChildItem -LiteralPath $directory -Filter "*.json" -File) {
        try {
            $metadata = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
            if ([string]$metadata.source -ne $Source) { continue }
            if ([string]$metadata.locale -ne $Locale) { continue }
            if ([string]$metadata.architecture -ne $Architecture) { continue }
            if ($InstallerType -and [string]$metadata.installerType -ne $InstallerType) { continue }
            if (-not $metadata.path -and $metadata.relativePath) { $metadata.path = Join-Path $root ([string]$metadata.relativePath) }
            elseif ($metadata.path -and ([string]$metadata.path -match '\\installers\\')) {
                $relative = ([string]$metadata.path) -replace '^.*?installers\\', 'installers\\'
                $metadata | Add-Member -NotePropertyName relativePath -NotePropertyValue $relative -Force
                $metadata.path = Join-Path $root $relative
            }
            if (-not (Test-CachedMetadata -Metadata $metadata)) { continue }
            if ($Source -eq "official") { Test-OfficialInstallerSignature -Path ([string]$metadata.path) | Out-Null }
            [pscustomobject]$metadata
        } catch {
            continue
        }
    }

    return @($entries | Sort-Object @{ Expression = { try { [version]$_.version } catch { [version]"0.0.0.0" } }; Descending = $true }, @{ Expression = { $_.downloadedAt } ; Descending = $true } | Select-Object -First 1)
}

function Get-CachedInstaller {
    param(
        [Parameter(Mandatory)]$Component,
        [Parameter(Mandatory)][string]$PackageId,
        [string]$Version,
        [string]$InstallerType,
        [string]$Locale = "fr-FR",
        [string]$Architecture = "x64",
        [switch]$DownloadIfMissing,
        [switch]$CacheOnly
    )

    $root = Initialize-DevEnvironmentCache
    $safeId = Get-SafeCacheName $PackageId

    if ($CacheOnly) {
        $sourceKind = if ($Component.PSObject.Properties.Name -contains "officialSource" -and $Component.officialSource) { "official" } else { "winget-fallback" }
        $cached = Get-LatestCachedMetadata -PackageId $PackageId -InstallerType $InstallerType -Locale $Locale -Architecture $Architecture -Source $sourceKind
        if ($cached) { return $cached }
        return $null
    }

    $official = Resolve-OfficialSource -Component $Component -Locale $Locale -Architecture $Architecture
    $sourceKind = if ($official) { "official" } else { "winget-fallback" }

    $resolvedVersion = if ($official) { [string]$official.version } elseif ($Version) { $Version } else {
        if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { throw "WinGet est requis pour le fallback de '$PackageId'." }
        Get-WingetPackageVersion -PackageId $PackageId -InstallerType $InstallerType -Locale $Locale -Architecture $Architecture
    }

    $packageDirectory = Join-Path $root ("installers\{0}\{1}" -f $safeId, (Get-SafeCacheName $resolvedVersion))
    New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null

    $metadata = Get-CachedPackageMetadata -PackageId $PackageId -Version $resolvedVersion -Locale $Locale -Architecture $Architecture -InstallerType $InstallerType -Source $sourceKind
    if ($metadata -and (Test-CachedMetadata -Metadata $metadata) -and [string]$metadata.source -eq $sourceKind) {
        if ($sourceKind -eq "official") { Test-OfficialInstallerSignature -Path ([string]$metadata.path) | Out-Null }
        return [pscustomobject]$metadata
    }
    if (-not $DownloadIfMissing) { return $null }

    $staging = Join-Path $root ("staging\{0}-{1}-{2}" -f $safeId,(Get-SafeCacheName $resolvedVersion),([guid]::NewGuid().ToString("N")))
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    try {
        if ($official) {
            $destination = Join-Path $packageDirectory $official.fileName
            $downloadPath = Join-Path $staging $official.fileName
            Write-Host "   ↓ Source officielle : $($Component.name) $resolvedVersion" -ForegroundColor DarkGray
            Invoke-WebRequest -Uri $official.url -OutFile $downloadPath -UseBasicParsing
            $hash = (Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash
            if ($official.sha256 -and $hash -ne $official.sha256) {
                throw "Le SHA-256 fourni par la release officielle ne correspond pas pour '$PackageId'."
            }
            Test-OfficialInstallerSignature -Path $downloadPath | Out-Null

            if ([IO.Path]::GetExtension($downloadPath).ToLowerInvariant() -eq ".msi" -and $Component.officialSource.type -eq "chrome-enterprise") {
                $actualVersion = Get-MsiProductVersion -Path $downloadPath
                if ($actualVersion -ne $resolvedVersion) {
                    throw "La version du MSI téléchargé ($actualVersion) ne correspond pas à la version stable résolue ($resolvedVersion)."
                }
            }

            Move-Item -LiteralPath $downloadPath -Destination $destination -Force
            $metadata = [pscustomobject]@{
                schemaVersion = 3; packageId = $PackageId; version = $resolvedVersion
                installerType = $InstallerType; locale = $Locale; architecture = $Architecture
                path = $destination; relativePath = ("installers\{0}\{1}\{2}" -f $safeId,(Get-SafeCacheName $resolvedVersion),$official.fileName)
                fileName = $official.fileName; sha256 = $hash
                expectedSha256 = $official.sha256; downloadedAt = (Get-Date).ToUniversalTime().ToString("o")
                source = "official"; sourceRepository = $official.sourceRepository
                sourceDefinition = $official.sourceDefinition
                releaseUrl = $official.releaseUrl; downloadUrl = $official.url
            }
        } else {
            if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { throw "WinGet est requis pour le fallback de '$PackageId'." }
            $downloadArgs = @("download","--id",$PackageId,"--exact","--source","winget","--version",$resolvedVersion,"--architecture",$Architecture,"--locale",$Locale,"--scope","machine","--download-directory",$staging,"--skip-license","--accept-source-agreements","--accept-package-agreements","--disable-interactivity")
            if ($InstallerType) { $downloadArgs += @("--installer-type",$InstallerType) }
            Write-Host "   ↓ Fallback WinGet : $PackageId $resolvedVersion" -ForegroundColor DarkYellow
            & winget @downloadArgs
            if ($LASTEXITCODE -ne 0) { throw "WinGet n'a pas pu télécharger '$PackageId $resolvedVersion' (code $LASTEXITCODE)." }
            $candidates = @(Get-ChildItem -LiteralPath $staging -File -Recurse | Where-Object { $_.Extension -in @(".exe",".msi",".msix",".msixbundle",".appx",".appxbundle",".zip") } | Sort-Object Length -Descending)
            if ($candidates.Count -eq 0) { throw "Aucun installateur exploitable n'a été produit pour '$PackageId $resolvedVersion'." }
            $source = $candidates[0]
            $destination = Join-Path $packageDirectory $source.Name
            Copy-Item -LiteralPath $source.FullName -Destination $destination -Force
            $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
            $metadata = [pscustomobject]@{
                schemaVersion = 3; packageId = $PackageId; version = $resolvedVersion
                installerType = $InstallerType; locale = $Locale; architecture = $Architecture
                path = $destination; relativePath = ("installers\{0}\{1}\{2}" -f $safeId,(Get-SafeCacheName $resolvedVersion),$source.Name)
                fileName = $source.Name; sha256 = $hash
                downloadedAt = (Get-Date).ToUniversalTime().ToString("o")
                source = "winget-fallback"
                sourceDefinition = [pscustomobject]@{ kind="package-manager-fallback"; provider="winget"; release=$resolvedVersion; asset=$source.Name; url=$null }
                releaseUrl = $null; downloadUrl = $null
            }
        }
        Save-CachedPackageMetadata -Metadata $metadata
        return $metadata
    } finally {
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-CachedInstaller {
    param([Parameter(Mandatory)]$Metadata, [Parameter(Mandatory)][object[]]$Arguments, [Parameter(Mandatory)][string]$ComponentName)
    if (-not (Test-CachedMetadata -Metadata $Metadata)) { throw "L'installateur en cache est absent ou son SHA-256 ne correspond plus." }
    if ([string]$Metadata.source -eq "official") { Test-OfficialInstallerSignature -Path ([string]$Metadata.path) | Out-Null }
    Write-Host "[$ComponentName] Installateur en cache : $($Metadata.fileName) [$($Metadata.source)]" -ForegroundColor DarkGray
    $extension = [IO.Path]::GetExtension([string]$Metadata.path).ToLowerInvariant()
    if ($extension -eq ".msi") {
        $msiArgs = @("/i", $Metadata.path) + @($Arguments | ForEach-Object { [string]$_ })
        $process = Start-Process -FilePath "msiexec.exe" -ArgumentList $msiArgs -Wait -PassThru
    } elseif ($extension -in @(".msix",".msixbundle",".appx",".appxbundle")) {
        Add-AppxPackage -Path $Metadata.path
        $process = [pscustomobject]@{ ExitCode = 0 }
    } else {
        $process = Start-Process -FilePath $Metadata.path -ArgumentList @($Arguments | ForEach-Object { [string]$_ }) -Wait -PassThru
    }
    return [int]$process.ExitCode
}
