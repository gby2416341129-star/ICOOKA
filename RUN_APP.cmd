@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "EXE=%~dp0dist\KukaManager\KukaManager.exe"
if exist "%EXE%" (
  start "" "%EXE%"
  exit /b 0
)
echo C# executable is not built yet.
echo Starting the builder now...
pause
call "%~dp0BUILD.cmd"
