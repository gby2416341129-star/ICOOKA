@echo off
setlocal EnableExtensions
cd /d "%~dp0"
call "%~dp0BUILD.cmd"
exit /b %ERRORLEVEL%
