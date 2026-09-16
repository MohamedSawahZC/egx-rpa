@echo off
REM Build RPA-V1.exe + Sample from RBA-CSharp (no legacy RBA folder).
setlocal EnableExtensions
set ROOT=%~dp0
set SRC=%ROOT%
set PROJ=%ROOT%..
set STAGE=%ROOT%release\_payload_stage
set ZIP=%ROOT%release\EmbeddedPayload.zip
set OUTDIR=%PROJ%\RPA-V1-Setup
set OUTEXE=%OUTDIR%\RPA-V1.exe
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo csc.exe not found.
  exit /b 1
)
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%FW%\System.Web.Extensions.dll" set FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319

echo.
echo === RPA-V1-Setup payload 80 ===
if not exist "%OUTDIR%" mkdir "%OUTDIR%"
if not exist "%OUTDIR%\Sample" mkdir "%OUTDIR%\Sample"
if not exist "%OUTDIR%\Sample\Docs" mkdir "%OUTDIR%\Sample\Docs"

echo Staging embedded payload ...
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
mkdir "%STAGE%\Sample"
mkdir "%STAGE%\Sample\Docs"
mkdir "%STAGE%\config"
mkdir "%STAGE%\state"

REM Offline test inbox + Sample CSV (3 clients) travel with the EXE.
if exist "%SRC%release\offline-inbox" xcopy /E /I /Y "%SRC%release\offline-inbox" "%STAGE%\OfflineInbox" >nul
if exist "%SRC%release\Test-Save-Client.csv" copy /Y "%SRC%release\Test-Save-Client.csv" "%STAGE%\Sample\Test-Save-Client.csv" >nul
if exist "%SRC%release\Test-Save-Client.csv" copy /Y "%SRC%release\Test-Save-Client.csv" "%OUTDIR%\Sample\Test-Save-Client.csv" >nul
if exist "%STAGE%\OfflineInbox" xcopy /E /I /Y "%STAGE%\OfflineInbox" "%OUTDIR%\OfflineInbox" >nul
if exist "%OUTDIR%\Sample\Docs" xcopy /E /I /Y "%OUTDIR%\Sample\Docs" "%STAGE%\Sample\Docs" >nul
if exist "%STAGE%\OfflineInbox\inbox\docs" if exist "%OUTDIR%\Sample\Docs" xcopy /E /I /Y "%OUTDIR%\Sample\Docs" "%STAGE%\OfflineInbox\inbox\docs" >nul
if exist "%STAGE%\OfflineInbox\inbox\docs" if exist "%OUTDIR%\OfflineInbox\inbox\docs" xcopy /E /I /Y "%STAGE%\OfflineInbox\inbox\docs" "%OUTDIR%\OfflineInbox\inbox\docs" >nul

if exist "%LOCALAPPDATA%\RPA-V1\config\selectors.csv" copy /Y "%LOCALAPPDATA%\RPA-V1\config\selectors.csv" "%STAGE%\config\selectors.csv" >nul
if exist "%LOCALAPPDATA%\RPA-V1\config\settings.json" copy /Y "%LOCALAPPDATA%\RPA-V1\config\settings.json" "%STAGE%\config\settings.json" >nul
if exist "%LOCALAPPDATA%\RPA-V1\config\bot-config.json" copy /Y "%LOCALAPPDATA%\RPA-V1\config\bot-config.json" "%STAGE%\config\bot-config.json" >nul
if not exist "%STAGE%\config\bot-config.json" (
  copy /Y "%SRC%release\bot-config.template.json" "%STAGE%\config\bot-config.json" >nul
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$p=Join-Path '%STAGE%' 'config\bot-config.json'; $j=Get-Content -LiteralPath $p -Raw -Encoding UTF8; $exe=Join-Path '%PROJ%' 'RBA-CSharp\CodingReplica-Setup\CodingReplica.exe'; if (-not (Test-Path $exe)) { $exe=Join-Path '%PROJ%' 'CodingReplica-Setup\CodingReplica.exe' }; $j=$j.Replace('__CODING_REPLICA__',$exe); $j=$j.Replace('__SHEET__',''); $docs=Join-Path $env:LOCALAPPDATA 'RPA-V1\SftpCache\docs'; $j=$j.Replace('__DOCS__',$docs); Set-Content -LiteralPath $p -Value $j -Encoding UTF8"
)
> "%STAGE%\state\tracking.csv" echo NationalId,Status,UpdatedAt,Note

if exist "%ZIP%" del /F /Q "%ZIP%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path (Join-Path '%STAGE%' '*') -DestinationPath '%ZIP%' -Force"
if not exist "%ZIP%" (
  echo Failed to create payload zip
  exit /b 1
)

echo Compiling embedded AskPass helper ...
set ASKPASS=%TEMP%\RpaSftpAskPass.exe
"%CSC%" /nologo /target:exe /optimize+ /platform:anycpu /out:"%ASKPASS%" "%SRC%Tools\RpaSftpAskPass.cs"
if errorlevel 1 (
  echo AskPass compile FAILED.
  exit /b 1
)

echo Compiling RPA-V1.exe ...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:"%OUTEXE%" ^
  /resource:"%ZIP%",RpaUi.EmbeddedPayload ^
  /resource:"%ASKPASS%",RpaUi.SftpAskPass ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:"%FW%\System.Security.dll" ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:"%FW%\System.IO.Compression.dll" ^
  /reference:"%FW%\System.IO.Compression.FileSystem.dll" ^
  /reference:"%FW%\System.Web.Extensions.dll" ^
  "%SRC%Core\*.cs" "%SRC%Ui\*.cs"
if errorlevel 1 (
  echo Compile FAILED.
  exit /b 1
)

if exist "%STAGE%" rmdir /S /Q "%STAGE%"
if exist "%ZIP%" del /F /Q "%ZIP%"
if exist "%ASKPASS%" del /F /Q "%ASKPASS%"
if exist "%OUTDIR%\RpaSftpAskPass.exe" del /F /Q "%OUTDIR%\RpaSftpAskPass.exe"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Compress-Archive -Path (Join-Path '%OUTDIR%' '*') -DestinationPath (Join-Path '%PROJ%' 'RPA-V1-Setup.zip') -Force"

echo.
echo READY:
for %%A in ("%OUTEXE%") do echo   EXE  %%~fA  (%%~zA bytes)
echo   Run: release\Setup-LocalSftpLab.ps1 then start RPA-V1.exe
exit /b 0
