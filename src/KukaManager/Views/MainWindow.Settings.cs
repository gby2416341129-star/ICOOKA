using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using KukaManager.Design;
using KukaManager.Models;
using KukaManager.Services;
using KukaManager.Utils;
using static KukaManager.Utils.Value;

namespace KukaManager.Views;

public sealed partial class MainWindow
{
    private void SettingsPage()
    {
        var p=new StackPanel();p.Children.Add(SectionTitle("账户与本机数据"));p.Children.Add(WindowChrome.Text($"管理员：{_store.Setting("admin_name")} · 账号：{_store.Setting("admin_username")}",13,null,Theme.Brush(Theme.MutedColor)));
        var r1=new WrapPanel{Margin=new Thickness(0,12,0,0)};r1.Children.Add(Btn("修改管理员账户",EditAccount,true));r1.Children.Add(Btn("数据库完整性检查",CheckDatabase));r1.Children.Add(Btn("打开数据目录",OpenDataFolder));p.Children.Add(r1);_body.Children.Add(SectionCard(p,new Thickness(24)));

        var ex=new StackPanel();ex.Children.Add(SectionTitle("Excel 交换与学员导入"));ex.Children.Add(WindowChrome.Text("全量 Excel 用于人工查看、迁移和恢复；恢复前自动备份当前数据库。",13,null,Theme.Brush(Theme.MutedColor)));var er=new WrapPanel{Margin=new Thickness(0,12,0,0)};er.Children.Add(Btn("导出全量 Excel",ExportFull,true));er.Children.Add(Btn("从全量 Excel 恢复",RestoreFullExcel));er.Children.Add(Btn("下载学员导入模板",StudentTemplate));er.Children.Add(Btn("批量导入学员",ImportStudents));ex.Children.Add(er);_body.Children.Add(SectionCard(ex,new Thickness(24)));

        var db=new StackPanel();db.Children.Add(SectionTitle("数据库备份 / 恢复"));db.Children.Add(WindowChrome.Text("应用内部会保留最近备份。也可以另存一个 .sqlite3 文件到你指定的位置。",13,null,Theme.Brush(Theme.MutedColor)));var dr=new WrapPanel{Margin=new Thickness(0,12,0,0)};dr.Children.Add(Btn("立即内部备份",()=>{_store.Backup();MessageBox.Show(this,"备份完成。","酷咔");},true));dr.Children.Add(Btn("另存数据库备份",BackupAs));dr.Children.Add(Btn("从数据库备份恢复",RestoreDatabase));db.Children.Add(dr);_body.Children.Add(SectionCard(db,new Thickness(24)));

        var status=new StackPanel();status.Children.Add(SectionTitle("当前数据库"));var counts=new List<string>();foreach(var t in _store.BusinessTables){try{counts.Add($"{t}: {L(_store.One($"SELECT COUNT(*) n FROM {t}")??new(),"n")}");}catch{}}status.Children.Add(WindowChrome.Text("文件："+AppPaths.DatabasePath+"\n"+string.Join("  ·  ",counts),12,null,Theme.Brush(Theme.MutedColor)));_body.Children.Add(SectionCard(status,new Thickness(24)));

        var danger=new StackPanel();danger.Children.Add(SectionTitle("卸载"));danger.Children.Add(WindowChrome.Text("卸载时可删除酷咔自己拥有的本地数据库、日志和备份；你手工导出到其他位置的 Excel / 数据库不会被删除。",13,null,Theme.Brush(Theme.MutedColor)));danger.Children.Add(Btn("启动卸载程序",LaunchUninstaller));_body.Children.Add(SectionCard(danger,new Thickness(24)));
    }

    private void EditAccount()
    {
        var w = new AccountWindow(this, _store);
        if (w.ShowDialog() == true)
        {
            MessageBox.Show(this, "管理员账户已更新。", "酷咔");
            Navigate(_page);
        }
    }
    private void CheckDatabase(){_store.IntegrityCheck();MessageBox.Show(this,"SQLite quick_check 与外键检查通过。","数据库正常",MessageBoxButton.OK,MessageBoxImage.Information);}
    private void OpenDataFolder(){AppPaths.EnsureDirectories();Process.Start(new ProcessStartInfo("explorer.exe",AppPaths.DataDirectory){UseShellExecute=true});}
    private void ExportFull(){var path=FileDialogService.Save("导出全量 Excel","Excel 工作簿 (*.xlsx)|*.xlsx",$"酷咔全量数据-{DateTime.Today:yyyy-MM-dd}.xlsx");if(path is null)return;_exchange.ExportFull(path,_week);MessageBox.Show(this,"全量 Excel 已导出。","酷咔");}
    private void RestoreFullExcel(){var path=FileDialogService.Open("从酷咔全量 Excel 恢复","Excel 工作簿 (*.xlsx)|*.xlsx");if(path is null)return;var data=_exchange.ReadFull(path);if(MessageBox.Show(this,"恢复将完整替换当前业务数据。系统会先自动备份现有数据库。确认继续？","确认恢复",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;_store.RestoreSnapshot(data);MessageBox.Show(this,"恢复完成。","酷咔");Navigate("工作台");}
    private void StudentTemplate(){var path=FileDialogService.Save("保存学员导入模板","Excel 工作簿 (*.xlsx)|*.xlsx","酷咔学员导入模板.xlsx");if(path is null)return;_exchange.StudentTemplate(path);MessageBox.Show(this,"模板已生成。","酷咔");}
    private void ImportStudents(){var path=FileDialogService.Open("批量导入学员","Excel 工作簿 (*.xlsx)|*.xlsx");if(path is null)return;var result=_exchange.ReadStudents(path);if(result.Rows.Count==0){MessageBox.Show(this,$"没有可新增记录；重复跳过 {result.Skipped} 条。","酷咔");return;}if(MessageBox.Show(this,$"将新增 {result.Rows.Count} 个学员，重复跳过 {result.Skipped} 条。继续？","确认导入",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;_exchange.ImportStudents(result.Rows);MessageBox.Show(this,"导入完成。","酷咔");Navigate("学员档案");}
    private void BackupAs(){var path=FileDialogService.Save("另存数据库备份","SQLite 数据库 (*.sqlite3)|*.sqlite3",$"kuka-{DateTime.Now:yyyyMMdd-HHmmss}.sqlite3");if(path is null)return;_store.Backup(path);MessageBox.Show(this,"数据库备份已保存。","酷咔");}
    private void RestoreDatabase(){var path=FileDialogService.Open("选择数据库备份","SQLite 数据库 (*.sqlite3)|*.sqlite3|所有文件 (*.*)|*.*");if(path is null)return;if(MessageBox.Show(this,"恢复会覆盖当前数据库，系统会先自动备份当前数据。确认继续？","恢复数据库",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;_store.RestoreDatabase(path);MessageBox.Show(this,"数据库已恢复。建议重新启动酷咔，以确保所有页面状态重新加载。","恢复完成");Navigate("工作台");}
    private void LaunchUninstaller(){var exe=Path.Combine(AppContext.BaseDirectory,"Uninstall-KukaManager.exe");var cmd=Path.Combine(AppContext.BaseDirectory,"Uninstall-KukaManager.cmd");if(File.Exists(exe))Process.Start(new ProcessStartInfo(exe){UseShellExecute=true});else if(File.Exists(cmd))Process.Start(new ProcessStartInfo(cmd){UseShellExecute=true});else throw new InvalidOperationException("当前运行目录没有找到卸载程序。若是开发/便携构建，请直接删除程序目录；应用数据位于："+AppPaths.DataDirectory);}
}
