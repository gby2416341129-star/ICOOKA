@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo.
echo ==============================================================
echo   酷咔管理系统 C# / .NET 10 - 首次自动构建并启动
echo ==============================================================
echo.
echo 首次运行需要联网下载微软 .NET 10 SDK 和 NuGet 依赖。
echo 构建完成后会生成 self-contained Windows x64 程序，之后可离线运行。
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-and-run.ps1"
if errorlevel 1 (
  echo.
  echo 构建失败。请把本目录 build.log 发给 ChatGPT。
  pause
  exit /b 1
)
echo.
echo 构建完成，酷咔已启动。
timeout /t 3 >nul
