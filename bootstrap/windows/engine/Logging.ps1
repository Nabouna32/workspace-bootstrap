Set-StrictMode -Version Latest
function New-BootstrapRun { param([Parameter(Mandatory=$true)][string]$LogsRoot,[string]$Mode="install",[string]$Selection="")
  $id=Get-Date -Format "yyyy-MM-dd_HH-mm-ss"; $path=Join-Path $LogsRoot $id; New-Item -ItemType Directory -Force -Path $path|Out-Null
  [pscustomobject]@{Id=$id;Path=$path;Mode=$Mode;Selection=$Selection;StartedAt=(Get-Date).ToString("o")} }
function Start-ModuleLog { param([Parameter(Mandatory=$true)][string]$RunPath,[Parameter(Mandatory=$true)][string]$Module)
  $safe=$Module -replace '[^a-zA-Z0-9._-]','_'; $path=Join-Path $RunPath "$safe.log"; Start-Transcript -Path $path -Append|Out-Null; $path }
function Stop-ModuleLog { try{Stop-Transcript|Out-Null}catch{} }
function ConvertTo-RedactedText { param([Parameter(Mandatory=$true)][string]$Text)
  $r=$Text
  $patterns=@('(?i)(Authorization\s*:\s*Bearer\s+)[^\s"]+','(?i)(Bearer\s+)[A-Za-z0-9._~+/=-]+','(?i)((?:token|access_token|refresh_token|api[_-]?key|password|passwd|secret)\s*[=:]\s*)[^\s,;}"'']+','(?i)gh[pousr]_[A-Za-z0-9_]+')
  foreach($p in $patterns){$r=[regex]::Replace($r,$p,{param($m) if($m.Groups.Count -gt 1){$m.Groups[1].Value+"<REDACTED>"}else{"<REDACTED>"}})}; $r }
function Write-RunSummary { param([Parameter(Mandatory=$true)]$Run,[object[]]$Steps=@(),[int]$ExitCode=0)
  [pscustomobject]@{runId=$Run.Id;mode=$Run.Mode;selection=$Run.Selection;startedAt=$Run.StartedAt;finishedAt=(Get-Date).ToString("o");exitCode=$ExitCode;success=($ExitCode -eq 0);steps=$Steps}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $Run.Path "summary.json") -Encoding UTF8 }