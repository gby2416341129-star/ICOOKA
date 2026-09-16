@echo off
chcp 65001 >nul
set "EXE=%~dp0dist\KukaManager\KukaManager.exe"
if not exist "%EXE%" (
  echo 还没有生成 C# 可执行文件。
  echo 请先双击“首次运行-自动构建并启动.cmd”。
  pause
  exit /b 1
)
start "" "%EXE%"
