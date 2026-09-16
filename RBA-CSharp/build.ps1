# =============================================================================
#  build.ps1  --  compile the full RBA WinForms app (no PowerShell at runtime)
#
#  Output:  ..\RBA\RBA.exe   (replaces the old powershell launcher)
#  Requires: .NET Framework csc.exe (ships with Windows)
# =============================================================================
$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$OutDir = Join-Path (Split-Path $Root -Parent) 'RBA'
$OutExe = Join-Path $OutDir 'RBA.exe'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) { throw 'csc.exe not found' }

$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path $fw)) { $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }

$sources = @()
$sources += Get-ChildItem (Join-Path $Root 'Core\*.cs') | ForEach-Object { $_.FullName }
$sources += Get-ChildItem (Join-Path $Root 'Ui\*.cs') | ForEach-Object { $_.FullName }
if ($sources.Count -lt 5) { throw "expected Core+Ui sources under $Root" }

$icon = Join-Path $OutDir 'assets\robot.ico'
$iconArgs = @()
if (Test-Path $icon) { $iconArgs = @("/win32icon:$icon") }

$refs = @(
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:$fw\System.IO.Compression.dll",
    "/reference:$fw\System.IO.Compression.FileSystem.dll",
    "/reference:$fw\System.Web.Extensions.dll"
)

Write-Host "Compiling $($sources.Count) sources -> $OutExe"
& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    ("/out:$OutExe") `
    @iconArgs `
    @refs `
    @sources

if ($LASTEXITCODE -ne 0) { throw "RBA build failed (exit $LASTEXITCODE)" }
$len = (Get-Item $OutExe).Length
Write-Host ("RBA.exe built ({0:N0} bytes) - compiled GUI, no PowerShell host" -f $len) -ForegroundColor Green
