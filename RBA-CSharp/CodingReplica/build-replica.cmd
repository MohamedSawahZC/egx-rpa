@echo off
setlocal
set ROOT=%~dp0
set OUT=%ROOT%..\CodingReplica-Setup
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%OUT%" mkdir "%OUT%"
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:"%OUT%\CodingReplica.exe" ^
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  "%ROOT%CodingReplica.cs"
if errorlevel 1 exit /b 1
echo READY: %OUT%\CodingReplica.exe
exit /b 0
