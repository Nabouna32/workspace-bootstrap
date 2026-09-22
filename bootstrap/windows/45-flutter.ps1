$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "engine\Environment.ps1")

$sdkRoot = Get-SdkRoot
$flutterRoot = Join-Path $sdkRoot "flutter"
$flutterCommand = Join-Path $flutterRoot "bin\flutter.bat"
$flutterBin = Join-Path $flutterRoot "bin"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
  throw "Git is required before installing Flutter."
}

New-Item -ItemType Directory -Path $sdkRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath $flutterRoot)) {
  $releasesUrl = "https://storage.googleapis.com/flutter_infra_release/releases/releases_windows.json"
  Write-Host "Téléchargement du stable Flutter officiel..." -ForegroundColor Cyan
  $releaseManifest = Invoke-RestMethod -Uri $releasesUrl -UseBasicParsing
  $currentHash = $releaseManifest.current_release.stable
  $release = $releaseManifest.releases | Where-Object {
    $_.channel -eq "stable" -and $_.hash -eq $currentHash
  } | Select-Object -First 1
  if (-not $release) { throw "Impossible de résoudre la version stable Flutter." }

  $archiveName = Split-Path $release.archive -Leaf
  $tempRoot = Join-Path $env:TEMP ("bouna-flutter-" + [guid]::NewGuid().ToString("N"))
  $archivePath = Join-Path $tempRoot $archiveName
  $stagingRoot = Join-Path $sdkRoot ("flutter-staging-" + [guid]::NewGuid().ToString("N"))
  try {
    New-Item -ItemType Directory -Path $tempRoot,$stagingRoot -Force | Out-Null
    Invoke-WebRequest -Uri "$($releaseManifest.base_url)/$($release.archive)" -OutFile $archivePath -UseBasicParsing
    $actualHash = (Get-FileHash $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $release.sha256.ToLowerInvariant()) {
      throw "Flutter archive SHA-256 mismatch."
    }
    Expand-Archive -Path $archivePath -DestinationPath $stagingRoot -Force
    $stagedFlutterRoot = Join-Path $stagingRoot "flutter"
    if (-not (Test-Path (Join-Path $stagedFlutterRoot "bin\flutter.bat"))) {
      throw "L'archive Flutter ne contient pas flutter\bin\flutter.bat."
    }
    Move-Item -Path $stagedFlutterRoot -Destination $flutterRoot
  } finally {
    Remove-Item $tempRoot,$stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
  }
} else {
  Write-Host "Flutter SDK déjà présent dans $flutterRoot."
}

if (-not (Test-Path -LiteralPath $flutterCommand -PathType Leaf)) {
  throw "Le SDK Flutter est présent mais flutter.bat est introuvable : $flutterCommand"
}

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
$pathEntries = if ($userPath) { @($userPath -split ";" | Where-Object { $_ }) } else { @() }

if ($pathEntries -notcontains $flutterBin) {
  [Environment]::SetEnvironmentVariable("Path", (($pathEntries + $flutterBin) -join ";"), "User")
  Write-Host "Flutter ajouté au PATH utilisateur."
}

if (($env:Path -split ";") -notcontains $flutterBin) {
  $env:Path = "$flutterBin;$env:Path"
}

& $flutterCommand --version
& $flutterCommand config --no-analytics
& $flutterCommand config --enable-windows-desktop
& $flutterCommand precache --windows

Write-Host "Vérification de la toolchain Windows Flutter..."
$doctorOutput = & $flutterCommand doctor -v 2>&1 | Out-String
Write-Host $doctorOutput

if ($doctorOutput -notmatch "Visual Studio") {
  throw "Flutter doctor ne détecte pas Visual Studio Build Tools."
}

$devices = & $flutterCommand devices 2>&1 | Out-String
Write-Host $devices

if ($devices -notmatch "Windows") {
  throw "Flutter ne détecte pas la cible Windows."
}

Write-Host "Flutter Windows est prêt dans $flutterRoot. Le SDK Android reste géré exclusivement dans WSL."
exit 0
