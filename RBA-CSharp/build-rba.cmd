@echo off
REM Build RBA.exe with csc.exe only — no PowerShell scripts.
setlocal
set ROOT=%~dp0
set OUT=%ROOT%..\RBA\RBA.exe
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo csc.exe not found. Install .NET Framework 4.x developer pack / Windows .
  exit /b 1
)
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%FW%\System.Web.Extensions.dll" set FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319

set ICON=
if exist "%ROOT%..\RBA\assets\robot.ico" set ICON=/win32icon:"%ROOT%..\RBA\assets\robot.ico"

echo Compiling RBA.exe ...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:"%OUT%" %ICON% ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:"%FW%\System.IO.Compression.dll" ^
  /reference:"%FW%\System.IO.Compression.FileSystem.dll" ^
  /reference:"%FW%\System.Web.Extensions.dll" ^
  "%ROOT%Core\*.cs" "%ROOT%Ui\*.cs"
if errorlevel 1 exit /b 1
for %%A in ("%OUT%") do echo Built %%~fA (%%~zA bytes) — no PowerShell at runtime
exit /b 0
