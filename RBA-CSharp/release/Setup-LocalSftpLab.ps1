# Seed C:\RPA-SFTP\inbox with 5 KYC clients (fill missing columns). Console shows counts only.
# File saved as UTF-8 BOM so PowerShell parses reliably (ASCII address fillers).
param(
    [string]$KycSource = "$env:USERPROFILE\Downloads\kyc_export.csv",
    [string]$SftpRoot = "C:\RPA-SFTP",
    [int]$MaxClients = 5
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $KycSource)) {
  throw "KYC source not found: $KycSource"
}
$inbox = Join-Path $SftpRoot "inbox"
$docs = Join-Path $inbox "docs"
New-Item -ItemType Directory -Force -Path $inbox, $docs | Out-Null

$rows = @(Import-Csv -LiteralPath $KycSource -Encoding UTF8 | Select-Object -First $MaxClients)
if ($rows.Count -lt 1) { throw "KYC source has no rows" }

$streets = @("Nile St","University St","Haram St","Faisal St","Gomhoreya St")
$areas = @("Maadi","Nasr City","Dokki","Heliopolis","Agouza")
$i = 0
foreach ($r in $rows) {
  $i++
  $addrProp = $r.PSObject.Properties["Address"]
  if ($null -eq $addrProp -or [string]::IsNullOrWhiteSpace([string]$addrProp.Value)) {
    $r.Address = ("Bldg {0} {1} - {2} - Cairo" -f (10+$i), $streets[($i-1)%5], $areas[($i*3)%5])
  }
  $nid = ([string]$r."ID Number") -replace "\D",""
  if ([string]::IsNullOrWhiteSpace([string]$r."Date of Birth") -and $nid.Length -eq 14) {
    $century = if ($nid[0] -eq "2") { 1900 } else { 2000 }
    $yy = [int]$nid.Substring(1,2); $mm = [int]$nid.Substring(3,2); $dd = [int]$nid.Substring(5,2)
    try { $r."Date of Birth" = (Get-Date -Year ($century+$yy) -Month $mm -Day $dd).ToString("dd/MM/yyyy") } catch {}
  }
  if ([string]::IsNullOrWhiteSpace([string]$r.Gender) -and $nid.Length -eq 14) {
    $g = [int][string]$nid[12]
    $r.Gender = if ($g % 2 -eq 1) { "1" } else { "2" }
  }
  if ([string]::IsNullOrWhiteSpace([string]$r.Occupation)) { $r.Occupation = "Employee" }
  if ([string]::IsNullOrWhiteSpace([string]$r.Email)) { $r.Email = "client$i@example.com" }
  if ([string]::IsNullOrWhiteSpace([string]$r."Identity Issue Date")) { $r."Identity Issue Date" = "01/01/2020" }
            if ([string]::IsNullOrWhiteSpace([string]$r."Identity Expiry Date")) { $r."Identity Expiry Date" = "01/01/2030" }
  # Lab classify: first suspended, rest new (smoke can add without UI classify).
  $status = if ($i -eq 1) { "SUSPENDED" } else { "NEW" }
  if ($null -eq $r.PSObject.Properties["Status"]) {
    $r | Add-Member -NotePropertyName Status -NotePropertyValue $status
  } else {
    $r.Status = $status
  }
  if ($nid.Length -ge 10) {
    $dir = Join-Path $docs $nid
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $bytes = [byte[]](0xFF,0xD8,0xFF,0xD9)
    [IO.File]::WriteAllBytes((Join-Path $dir "01.jpg"), $bytes)
    [IO.File]::WriteAllBytes((Join-Path $dir "02.jpg"), $bytes)
  }
}
$out = Join-Path $inbox "kyc_export.csv"
$rows | Export-Csv -LiteralPath $out -NoTypeInformation -Encoding UTF8
Write-Host ("Seeded {0} clients -> {1}" -f $rows.Count, $out)

$trk = Join-Path $env:LOCALAPPDATA "RPA-V1\state\tracking.csv"
if (Test-Path $trk) {
  "NationalId,Name,Status,NIN,CODE12,RejectReason,Notes,SourceFile,LastUpdated" | Set-Content $trk -Encoding UTF8
  Write-Host "Cleared tracking.csv"
}
$cache = Join-Path $env:LOCALAPPDATA "RPA-V1\SftpCache"
if (Test-Path $cache) { Remove-Item $cache -Recurse -Force; Write-Host "Cleared SftpCache" }
$ts = Join-Path $env:LOCALAPPDATA "RPA-V1\Sample\Test-Save-Client.csv"
if (Test-Path $ts) { Remove-Item $ts -Force; Write-Host "Removed Test-Save-Client.csv" }
$sel = Join-Path $env:LOCALAPPDATA "RPA-V1\config\selectors.csv"
if (Test-Path $sel) { Remove-Item $sel -Force; Write-Host "Cleared selectors (will auto-map on CodingReplica)" }

$cfgDir = Join-Path $env:LOCALAPPDATA "RPA-V1\config"
New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
$cfgPath = Join-Path $cfgDir "bot-config.json"
# release -> RBA-CSharp -> workspace
$projRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$replica = Join-Path $projRoot "RBA-CSharp\CodingReplica-Setup\CodingReplica.exe"
if (-not (Test-Path $replica)) {
  $replica = Join-Path $projRoot "CodingReplica-Setup\CodingReplica.exe"
}
$docsCache = Join-Path $env:LOCALAPPDATA "RPA-V1\SftpCache\docs"
$template = Join-Path $PSScriptRoot "bot-config.template.json"
$text = [IO.File]::ReadAllText($template, [Text.UTF8Encoding]::new($true))
$text = $text.Replace("__SHEET__", "").Replace("__DOCS__", $docsCache.Replace("\","\\")).Replace("__CODING_REPLICA__", $replica.Replace("\","\\"))
[IO.File]::WriteAllText($cfgPath, $text, [Text.UTF8Encoding]::new($true))
# Lab keeps local mirror; production defaults live in the template for non-lab installs.
try {
  $j = Get-Content -LiteralPath $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
  $j.sftpHost = "local"
  $j.sftpUser = ""
  $j.sftpPassword = ""
  ($j | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $cfgPath -Encoding UTF8
  Write-Host "Lab bot-config forced sftpHost=local"
} catch {
  Write-Host "WARN: could not force local SFTP in lab config: $($_.Exception.Message)"
}
Write-Host ("Wrote bot-config (replica exists={0})" -f (Test-Path $replica))
Write-Host "Done. Start CodingReplica.exe then RPA-V1.exe"
