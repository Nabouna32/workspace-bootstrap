[CmdletBinding()]
param([string]$Root=(Join-Path $PSScriptRoot "..\bootstrap\windows"))
$ErrorActionPreference="Stop"
if(-not (Get-Command powershell.exe -ErrorAction SilentlyContinue)){ throw "Windows PowerShell is required for syntax validation." }
$files=Get-ChildItem -LiteralPath $Root -Recurse -Filter "*.ps1" -File
$errors=@()
foreach($file in $files){
  $tokens=$null;$parseErrors=$null
  [void][System.Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$parseErrors)
  if($parseErrors.Count){$errors += [pscustomobject]@{Path=$file.FullName;Errors=$parseErrors}}
}
if($errors.Count){$errors|ForEach-Object{Write-Host "Syntax error: $($_.Path)";$_.Errors|ForEach-Object{Write-Host "  $($_.Message)"}};exit 1}
Write-Host "PowerShell syntax validation: OK ($($files.Count) files)."
exit 0