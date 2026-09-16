KukaManager C# 1.0.6 builder fix

修复：PowerShell 自动变量 $args 与构建函数参数名冲突，导致 dotnet restore 实际收到 0 个参数，只显示 dotnet Usage。

请完整解压后双击 START_HERE.cmd。
构建失败时窗口会保留，并自动打开 build.log。

KukaManager C# 1.0.2 Launcher Fix

1. Extract the ZIP completely to a normal local folder.
2. Double-click START_HERE.cmd.
3. Keep the console window open while it downloads/restores/builds.
4. On success, the app will be published to dist\KukaManager\KukaManager.exe and launched automatically.
5. On failure, build.log will be created and opened in Notepad.

Fix in 1.0.2:
- Removed the broken nested cmd.exe /K quoting used by 1.0.1.
- START_HERE.cmd now directly CALLs BUILD.cmd, which works with folders containing Chinese characters and spaces.
