@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"
chcp 65001 >nul 2>&1
title KukaManager C# Builder - DO NOT CLOSE
cls
echo ============================================================
echo   KukaManager C# / .NET 10 Build and Run - v1.0.5
echo ============================================================
echo.
echo This window will stay open even when an error occurs.
echo Build log: %CD%\build.log
echo.

set "RC=1"
where powershell.exe >nul 2>&1
if errorlevel 1 (
  echo [ERROR] powershell.exe was not found.
  echo [ERROR] powershell.exe was not found.>build.log
  goto :fail
)

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-and-run-v2.ps1"
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" goto :fail

echo.
echo ============================================================
echo BUILD SUCCEEDED
echo The C# app has been published under dist\KukaManager
echo ============================================================
echo.
pause
exit /b 0

:fail
echo.
echo ============================================================
echo BUILD FAILED - error code %RC%
echo ============================================================
echo.
echo build.log will now be opened in Notepad.
if exist "%~dp0build.log" start "" notepad.exe "%~dp0build.log"
echo.
pause
exit /b 1
