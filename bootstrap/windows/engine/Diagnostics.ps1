Set-StrictMode -Version Latest
function New-SanitizedDiagnosticsBundle { param([Parameter(Mandatory=$true)][string]$RunPath,[int]$MaxFileBytes=1048576,[int]$MaxTotalBytes=5242880)
  . (Join-Path $PSScriptRoot "Logging.ps1"); $out=Join-Path $RunPath "diagnostics"; New-Item -ItemType Directory -Force -Path $out|Out-Null; $total=0
  Get-ChildItem $RunPath -File -Recurse|Where-Object{$_.FullName -notlike "$out*"}|Sort-Object FullName|ForEach-Object{
    if($total -ge $MaxTotalBytes){return}; $rel=$_.FullName.Substring($RunPath.Length).TrimStart(''); $dest=Join-Path $out $rel
    New-Item -ItemType Directory -Force -Path (Split-Path $dest)|Out-Null; $text=ConvertTo-RedactedText ([IO.File]::ReadAllText($_.FullName))
    $bytes=[Text.Encoding]::UTF8.GetBytes($text); if($bytes.Length -gt $MaxFileBytes){$text=[Text.Encoding]::UTF8.GetString($bytes,0,$MaxFileBytes)+[Environment]::NewLine+"[TRUNCATED]"}
    $bytes=[Text.Encoding]::UTF8.GetBytes($text); if(($total+$bytes.Length) -le $MaxTotalBytes){[IO.File]::WriteAllText($dest,$text,[Text.UTF8Encoding]::new($false));$total+=$bytes.Length}
  }; [pscustomobject]@{Path=$out;Bytes=$total;MaxBytes=$MaxTotalBytes} }