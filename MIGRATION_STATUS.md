# 酷咔 C# 迁移状态（1.0.6）

目标基线：Python/PySide6 0.7.x。

## Windows 实机编译验证

本版本已改用 Windows GitHub Actions 作为交付门禁，不再由用户电脑充当编译器。

验证环境：Windows Server 2025 x64 + .NET SDK 10.0.401。

验证结果：
- 主 C# / WPF 工程：Build succeeded，0 Warning，0 Error。
- Smoke Tests 工程：Build succeeded，0 Warning，0 Error。
- 迁移 Smoke Tests：12 / 12 PASS。
- win-x64 self-contained：Publish succeeded。
- KukaManager.exe：成功生成，PE32+ Windows GUI x86-64。
- Windows 启动测试：进程启动并持续运行 5 秒，没有启动即崩溃。
- Windows 发布包：已由 CI 产出并校验。

Smoke Tests 覆盖：管理员登录、课程目录、学员 CRUD、老师/课程、报名与课时余额、班级成员、固定排课→考勤→自动课消、财务余额锚点、户头合并与余额重算、缴费→班级历史、班级/周课表关联、SQLite integrity/foreign keys。

## 已迁移范围

主页面与核心业务入口：工作台、学员档案、报名与费用、周排课表、班级管理、出勤与补课、课消账本、学习成长、课程与老师、财务中心、操作日志、数据与设置。

数据层继续兼容原有 SQLite 文件，并包含旧 schema 自动迁移、课程/老师/学员/报名/班级/排课/考勤/课消/补课/成长、财务导入批次/财务流水/学员匹配/户头合并/课程与班级关联/学生班级历史等表和迁移逻辑。

旧 Python 0.7.x 非 GUI 回归基线：45 passed（作为行为迁移基线）。

## 1.0.6 修复

- 补齐 System.IO 相关编译依赖。
- 修正 FinanceExcelService 文件扩展名判断及级联类型错误。
- 修正 WindowChrome.Field 的 FrameworkElement/Control 类型约束。
- 修正 MainWindow 空引用警告。
- 修正 Smoke Tests System.IO 引用。
- Self-contained / win-x64 仅在最终 publish 阶段启用，测试项目可正常引用主工程。
