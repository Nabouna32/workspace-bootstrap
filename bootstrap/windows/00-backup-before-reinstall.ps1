[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$BackupRoot,
  [switch]$ExportWsl
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null

function Copy-IfExists([string]$Source,[string]$Destination) {
  if (Test-Path $Source) {
    New-Item -ItemType Directory -Force -Path (Split-Path $Destination) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Recurse -Force
  }
}

git config --global --list --show-origin | Out-File -Encoding utf8 "$BackupRoot\git-global.txt"
Copy-IfExists "$HOME\.ssh" "$BackupRoot\ssh"
Copy-IfExists "$HOME\.wslconfig" "$BackupRoot\windows\.wslconfig"

$task = Get-ScheduledTask -TaskName "NabounaLab - Start WSL Dev Environment" -ErrorAction SilentlyContinue
if ($task) {
  Export-ScheduledTask -TaskName $task.TaskName -TaskPath $task.TaskPath -Xml "$BackupRoot\windows\wsl-autostart-task.xml"
}

if (Get-Command winget -ErrorAction SilentlyContinue) {
  winget export -o "$BackupRoot\windows\winget-packages.json" --accept-source-agreements 2>&1 |
    Out-File -Encoding utf8 "$BackupRoot\windows\winget-export.log"
}

wsl --status | Out-File -Encoding utf8 "$BackupRoot\wsl-status.txt"
wsl --list --verbose | Out-File -Encoding utf8 "$BackupRoot\wsl-list.txt"

if ($ExportWsl) {
  $archive = Join-Path $BackupRoot "wsl-Ubuntu.tar"
  wsl --export Ubuntu $archive
}

Write-Host "Backup completed: $BackupRoot"
Write-Host "PRIVATE SSH KEYS stay outside GitHub. Do not commit the backup."
