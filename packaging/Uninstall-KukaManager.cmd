@echo off
setlocal
chcp 65001 >nul
title 卸载酷咔管理系统
set "APP_DIR=%~dp0"
set "DATA_DIR=%LOCALAPPDATA%\KukaManager"

echo.
echo  酷咔管理系统卸载程序
echo  ======================
echo.
echo  程序目录：%APP_DIR%
echo  数据目录：%DATA_DIR%
echo.
choice /C YN /N /M "确认卸载程序？[Y/N] "
if errorlevel 2 exit /b 0

taskkill /IM KukaManager.exe /F >nul 2>nul
echo.
choice /C YN /N /M "是否保留数据库、备份和日志？[Y=保留/N=删除] "
if errorlevel 2 (
  if exist "%DATA_DIR%" rmdir /S /Q "%DATA_DIR%"
  echo  本机业务数据已删除。
) else (
  echo  本机业务数据已保留。
)

echo  正在删除程序文件...
start "" /min powershell.exe -NoProfile -WindowStyle Hidden -Command "Start-Sleep -Milliseconds 800; Remove-Item -LiteralPath '%APP_DIR%' -Recurse -Force"
exit /b 0
