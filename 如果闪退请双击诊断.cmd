@echo off
setlocal
cd /d "%~dp0"
cls
echo KukaManager launcher diagnostic
echo.
echo If you can see this window, CMD itself is working normally.
echo Folder: %CD%
echo.
where powershell.exe
echo PowerShell errorlevel: %ERRORLEVEL%
where winget.exe
echo Winget errorlevel: %ERRORLEVEL%
where dotnet.exe
echo Dotnet errorlevel: %ERRORLEVEL%
echo.
pause
