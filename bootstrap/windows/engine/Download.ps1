Set-StrictMode -Version Latest

function Get-CachedArtifact {
    param(
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$CacheRoot,
        [Parameter(Mandatory)][string]$Component,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$FileName,
        [string]$Sha256
    )
    $destination = Join-Path $CacheRoot (Join-Path $Component (Join-Path $Version $FileName))
    $dir = Split-Path -Parent $destination
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        if ($Sha256) {
            $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash
            if ($actual -ieq $Sha256) { return $destination }
            Remove-Item -LiteralPath $destination -Force
        } else { return $destination }
    }
    $staging = Join-Path $CacheRoot ".staging"
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    $partial = Join-Path $staging ([guid]::NewGuid().ToString() + ".download")
    try {
        Write-Host "Downloading $Component $Version..."
        Invoke-WebRequest -Uri $Url -OutFile $partial -UseBasicParsing
        if ($Sha256) {
            $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $partial).Hash
            if ($actual -ine $Sha256) { throw "SHA256 mismatch. Expected $Sha256, got $actual." }
        }
        Move-Item -LiteralPath $partial -Destination $destination -Force
        return $destination
    } finally {
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue }
    }
}
