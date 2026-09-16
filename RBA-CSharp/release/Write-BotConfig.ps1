param(
    [Parameter(Mandatory = $true)]
    [string]$OutDir
)

$ErrorActionPreference = "Stop"
$out = (Resolve-Path $OutDir).Path
$template = Join-Path $PSScriptRoot "bot-config.template.json"
if (-not (Test-Path $template)) {
    throw "Missing template: $template"
}

$sheet = ""
$docs = (Join-Path $env:LOCALAPPDATA "RPA-V1\SftpCache\docs")
$replica = Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) "CodingReplica-Setup\CodingReplica.exe"
if (-not (Test-Path $replica)) {
  $replica = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path "CodingReplica-Setup\CodingReplica.exe"
}
$text = [IO.File]::ReadAllText($template, [Text.Encoding]::UTF8)
$text = $text.Replace("__SHEET__", "")
$text = $text.Replace("__DOCS__", $docs.Replace("\", "\\"))
$text = $text.Replace("__CODING_REPLICA__", $replica.Replace("\", "\\"))

$dest = Join-Path $out "config\bot-config.json"
$utf8 = New-Object System.Text.UTF8Encoding $true
[IO.File]::WriteAllText($dest, $text, $utf8)
Write-Host "Wrote $dest"
