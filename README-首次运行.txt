酷咔管理系统 · C# / .NET 10 迁移版
===================================

使用方式：
1. 完整解压本 ZIP。
2. 双击：首次运行-自动构建并启动.cmd
3. 第一次需要联网，因为脚本会从微软官方下载 .NET 10.0.401 SDK，并从 NuGet 恢复依赖。
4. 脚本会先备份现有 %LOCALAPPDATA%\KukaManager\kuka.sqlite3，再编译主程序。
5. 编译后先运行迁移 Smoke Tests；只有测试通过才会发布并启动正式 C# 程序。
6. 以后直接运行：dist\KukaManager\KukaManager.exe
   或双击：启动酷咔.cmd / 启动已构建版本.cmd

数据兼容：
- 继续使用 %LOCALAPPDATA%\KukaManager\kuka.sqlite3
- 不要求重新导入现有 Python 0.7.x 数据库
- 第一次启动前自动保存 pre-csharp-时间.sqlite3 备份

技术底座：
- C# 14
- .NET 10 LTS
- WPF
- EF Core 10 + Microsoft.Data.Sqlite / SQLite
- CommunityToolkit.Mvvm
- ExcelDataReader + ClosedXML

说明：
当前 ChatGPT 执行容器本身没有 Windows/.NET SDK，因此包内使用“在你的 Windows 本机首次运行时自动构建”的方式。
脚本不会在 Smoke Tests 失败时启动程序；失败时请把 build.log 发回来，我可按实际编译错误继续修。
