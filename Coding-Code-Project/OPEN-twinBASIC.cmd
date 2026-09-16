@echo off
REM One-click: opens twinBASIC. Then: Import from VBP... (row 2, col 3) -> Open
REM -> pick ..\Coding.vbp -> wait for tB Services READY -> press F5.
REM The reversed app opens with NO login and NO database (BYPASS_LOGIN=True).
cd /d "%~dp0"
start "" "twinBASIC\twinBASIC.exe"
