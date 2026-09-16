@echo off
setlocal EnableExtensions
cd /d "%~dp0"
call BUILD.cmd
exit /b %ERRORLEVEL%
