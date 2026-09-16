using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using KukaManager.Design;
using KukaManager.Models;
using KukaManager.Services;
using KukaManager.UI.Controls;
using static KukaManager.Utils.Value;

namespace KukaManager.Views;

public sealed partial class MainWindow
{
    private void FinancePage()
    {
        FinanceNavbar();
        switch(_financeView){case "accounts":FinanceAccountsView();break;case "courses":FinanceCoursesView();break;case "transactions":FinanceTransactionsView();break;default:FinanceOverview();break;}
    }

    private void FinanceNavbar()
    {
        var root=new StackPanel();
        var tabs=new WrapPanel{Orientation=Orientation.Horizontal};
        foreach(var tab in new[]{("总资金","overview"),("户头","accounts"),("课程收入","courses"),("全部流水","transactions")})
        {
            var value=tab.Item2;
            var button=KukaControls.Button(tab.Item1);
            var styleKey=value==_financeView?"SegmentSelectedButtonStyle":"SegmentButtonStyle";
            if(Application.Current.Resources[styleKey] is Style style)button.Style=style;
            button.Margin=new Thickness(0);
            button.Click+=(_,_)=>Guard(()=>SetFinanceView(value));
            tabs.Children.Add(button);
        }
        root.Children.Add(new Border{Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243,246,249)),CornerRadius=new CornerRadius(12),Padding=new Thickness(4),HorizontalAlignment=HorizontalAlignment.Left,Child=tabs});

        var divider=new Border{Height=1,Background=Theme.Brush(Theme.SoftBorderColor),Margin=new Thickness(0,16,0,14)};
        root.Children.Add(divider);
        var actionRow=new Grid();
        actionRow.ColumnDefinitions.Add(new ColumnDefinition());
        actionRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var sync=_store.Setting("finance_sync_path");
        var status=WindowChrome.Text(sync.Length>0?"同步文件："+Path.GetFileName(sync)+" · 上次同步 "+_store.Setting("finance_sync_at").Replace('T',' '):"尚未设置同步文件 · 首次同步将创建规范 Excel，不修改原始导入文件",13,null,Theme.Brush(Theme.MutedColor));
        status.VerticalAlignment=VerticalAlignment.Center;
        actionRow.Children.Add(status);
        var actions=new WrapPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        actions.Children.Add(Btn("新增流水",()=>EditFinance(null)));
        actions.Children.Add(Btn("导入 Excel",ImportFinance));
        actions.Children.Add(Btn("同步 Excel",SyncFinance));
        actions.Children.Add(Btn("另存 Excel",ExportFinance,true));
        Grid.SetColumn(actions,1);actionRow.Children.Add(actions);
        root.Children.Add(actionRow);
        _body.Children.Add(SectionCard(root,new Thickness(20,18,12,10)));
    }
    private void SetFinanceView(string v){_financeView=v;_financeTeacherId=null;_financeSearch="";if(v!="accounts"){_financeAccount=null;_financeAccountMonth=null;}if(v!="courses"){_financeCourse=null;_financeCourseMonth=null;}if(v!="transactions"){_financeStudentId=null;_financeTxAccount=null;_financeTxMonth=null;_financeTxCourse=null;}Navigate("财务中心");}

    private void FinanceOverview()
    {
        var s=_store.FinanceSummary();var known=_store.FinanceKnownBalance();MetricRow([("可确认总资金",known.HasValue?Fen(known.Value):"—","有余额锚点的户头当前余额合计"),("累计收入",Fen(s["income_fen"]),s["n"]+" 笔流水"),("累计支出",Fen(s["expense_fen"]),"现金流出"),("手续费",Fen(s["fee_fen"]),"独立费用"),("净现金变动",Fen(s["net_fen"]),"收入－支出－手续费")]);
        MetricRow([("经营收入",Fen(s["operating_income_fen"]),"经营口径"),("经营支出",Fen(s["operating_expense_fen"]),"不含内部划转"),("退款",Fen(s["refund_fen"]),"已分类退款"),("经营净额",Fen(s["operating_net_fen"]),"经营收入－支出－退款－手续费"),("待匹配",s["unmatched"].ToString(),"需要人工确认的流水")]);
        _body.Children.Add(SectionTitle("户头概览"));var ac=_store.FinanceAccountSummary();Table(["户头","笔数","当前余额","累计收入","累计支出","手续费","净现金","最后交易"],ac.Select(a=>new object?[]{a.Account,a.Count,a.CurrentBalanceFen.HasValue?Fen(a.CurrentBalanceFen.Value)+(a.Estimated?"（推算）":""):"—",Fen(a.IncomeFen),Fen(a.ExpenseFen),Fen(a.FeeFen),Fen(a.NetFen),a.LatestDate}),ac.Select(a=>a.Account),Math.Min(430,120+ac.Count*46),id=>{_financeAccount=id;_financeAccountMonth=null;_financeView="accounts";Navigate("财务中心");});
        _body.Children.Add(SectionTitle("月度现金流"));var ms=_store.FinanceMonthlySummary();Table(["月份","笔数","收入","支出","手续费","净现金"],ms.Select(r=>new object?[]{MonthName(S(r,"month")),L(r,"n"),Fen(L(r,"income_fen")),Fen(L(r,"expense_fen")),Fen(L(r,"fee_fen")),Fen(L(r,"net_fen"))}),null,Math.Min(420,120+ms.Count*46));
        _body.Children.Add(SectionTitle("经营分类"));var cs=_store.FinanceCategorySummary();Table(["经营性质","分类","笔数","收入","支出","手续费","净现金"],cs.Select(r=>new object?[]{NatureName(S(r,"nature")),S(r,"category"),L(r,"n"),Fen(L(r,"income_fen")),Fen(L(r,"expense_fen")),Fen(L(r,"fee_fen")),Fen(L(r,"net_fen"))}),null,Math.Min(430,120+cs.Count*46));
    }

    private void FinanceAccountsView()
    {
        if(string.IsNullOrWhiteSpace(_financeAccount))
        {
            var head=new DockPanel();head.Children.Add(SectionTitle("户头"));var b=Btn("合并户头",()=>MergeAccounts(null));DockPanel.SetDock(b,Dock.Right);head.Children.Add(b);_body.Children.Add(head);_body.Children.Add(SectionNote("每个户头独立统计。重命名为已存在名称会要求确认并进行逻辑合并；原始 Excel 追溯信息仍保留。"));
            var ac=_store.FinanceAccountSummary();Table(["户头","流水笔数","当前余额","累计收入","累计支出","手续费","净现金变动","最后交易"],ac.Select(a=>new object?[]{a.Account,a.Count,a.CurrentBalanceFen.HasValue?Fen(a.CurrentBalanceFen.Value)+(a.Estimated?"（推算）":""):"—",Fen(a.IncomeFen),Fen(a.ExpenseFen),Fen(a.FeeFen),Fen(a.NetFen),a.LatestDate}),ac.Select(a=>a.Account),Math.Min(610,130+ac.Count*46),id=>{_financeAccount=id;_financeAccountMonth=null;Navigate("财务中心");});return;
        }
        var account=_financeAccount;var info=_store.FinanceAccountSummary().FirstOrDefault(x=>x.Account==account);if(info is null){_financeAccount=null;Navigate("财务中心");return;}var top=new DockPanel();var back=Btn("← 返回全部户头",()=>{_financeAccount=null;_financeAccountMonth=null;Navigate("财务中心");});DockPanel.SetDock(back,Dock.Left);top.Children.Add(back);var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(Btn("合并到其他户头",()=>MergeAccounts(account)));actions.Children.Add(Btn("重命名户头",RenameAccount,true));DockPanel.SetDock(actions,Dock.Right);top.Children.Add(actions);_body.Children.Add(top);_body.Children.Add(SectionTitle(account));var summary=_store.FinanceSummary(account:account);var sub=info.Estimated?$"最近确认 {info.BalanceDate}：{(info.LatestBalanceFen.HasValue?Fen(info.LatestBalanceFen.Value):"—")}；之后净变动 {(info.PostBalanceNetFen>=0?"+":"-")}{Fen(Math.Abs(info.PostBalanceNetFen))}":"来自最近一条带余额的流水";MetricRow([(info.Estimated?"当前推算余额":"最新确认余额",info.CurrentBalanceFen.HasValue?Fen(info.CurrentBalanceFen.Value):"—",sub),("累计收入",Fen(summary["income_fen"]),summary["n"]+" 笔流水"),("累计支出",Fen(summary["expense_fen"]),"现金流出"),("手续费",Fen(summary["fee_fen"]),"独立费用"),("净现金变动",Fen(summary["net_fen"]),"累计口径")]);
        var ms=_store.FinanceMonthlySummary(account:account);_body.Children.Add(SectionTitle("按月查看"));Table(["月份","笔数","收入","支出","手续费","净现金变动"],ms.Select(r=>new object?[]{MonthName(S(r,"month")),L(r,"n"),Fen(L(r,"income_fen")),Fen(L(r,"expense_fen")),Fen(L(r,"fee_fen")),Fen(L(r,"net_fen"))}),ms.Select(r=>S(r,"month")),Math.Min(420,120+ms.Count*46),m=>{_financeAccountMonth=m;Navigate("财务中心");});
        if(!string.IsNullOrWhiteSpace(_financeAccountMonth)){_body.Children.Add(SectionTitle(MonthName(_financeAccountMonth)+" · "+account+" 明细"));FinanceRecordActions();FinanceTransactionGrid(account:account,month:_financeAccountMonth);}
    }

    private void MergeAccounts(string? source)
    {
        var ac=_store.FinanceAccountSummary().Select(x=>x.Account).ToList();if(ac.Count<2)throw new InvalidOperationException("至少需要两个户头才能合并");source=source??(_financeAccount is not null&&ac.Contains(_financeAccount)?_financeAccount:ac[0]);var targets=ac.Where(x=>x!=source).ToList();var d=EditForm("合并财务户头",[new("source","要合并的户头",FormFieldKind.Choice,source,ac.Select(x=>new FormChoice(x,x)).ToList(),true),new("target","保留的目标户头",FormFieldKind.Choice,targets[0],targets.Select(x=>new FormChoice(x,x)).ToList(),true)],null,"合并后所有统计只显示目标户头；以后再次导入源名称也会自动归入目标户头。原始 Excel 行和 raw JSON 保留。");if(d is null)return;var src=Convert.ToString(d["source"])!;var target=Convert.ToString(d["target"])!;if(src==target)throw new InvalidOperationException("源户头和目标户头不能相同");if(MessageBox.Show(this,$"确认将“{src}”合并到“{target}”？","确认合并",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;_store.MergeFinanceAccounts(src,target);if(_financeAccount==src)_financeAccount=target;_financeAccountMonth=null;Navigate("财务中心");
    }
    private void RenameAccount(){if(string.IsNullOrWhiteSpace(_financeAccount))return;var d=new TextPromptWindow(this,"重命名户头","新的户头名称：",_financeAccount);if(d.ShowDialog()!=true||string.IsNullOrWhiteSpace(d.Value)||d.Value==_financeAccount)return;var existing=_store.FinanceAccountSummary().Select(x=>x.Account).ToHashSet();if(existing.Contains(d.Value)&&MessageBox.Show(this,$"“{d.Value}”已经存在。继续将合并两个逻辑户头。确定吗？","合并户头",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;var old=_financeAccount;_store.RenameFinanceAccount(old,d.Value);_financeAccount=_store.ResolveFinanceAccountName(d.Value);_financeAccountMonth=null;Navigate("财务中心");}

    private void FinanceCoursesView()
    {
        if(string.IsNullOrWhiteSpace(_financeCourse))
        {
            _body.Children.Add(SectionTitle("课程收入"));_body.Children.Add(SectionNote("分析课程用于历史财务归类；正式课程/班级关联只能从课程目录和班级管理选择。双击课程查看每月收入。"));var cs=_store.FinanceCourseSummary();Table(["标准课程","收入笔数","总收入","手续费","实收净额"],cs.Select(r=>new object?[]{S(r,"course_group"),L(r,"n"),Fen(L(r,"income_fen")),Fen(L(r,"fee_fen")),Fen(L(r,"net_income_fen"))}),cs.Select(r=>S(r,"course_group")),Math.Min(480,120+cs.Count*46),c=>{_financeCourse=c;_financeCourseMonth=null;Navigate("财务中心");});var ts=_store.FinanceTeacherSummary();if(ts.Count>0){_body.Children.Add(SectionTitle("老师收入概览"));Table(["老师","收入笔数","总收入","手续费","实收净额"],ts.Select(r=>new object?[]{S(r,"teacher"),L(r,"n"),Fen(L(r,"income_fen")),Fen(L(r,"fee_fen")),Fen(L(r,"income_fen")-L(r,"fee_fen"))}),null,Math.Min(400,120+ts.Count*46));}return;
        }
        var h=new DockPanel();var back=Btn("← 返回课程收入",()=>{_financeCourse=null;_financeCourseMonth=null;_financeTeacherId=null;_financeSearch="";Navigate("财务中心");});DockPanel.SetDock(back,Dock.Left);h.Children.Add(back);_body.Children.Add(h);_body.Children.Add(SectionTitle(_financeCourse));FinanceCourseFilter();var s=_store.FinanceSummary(courseGroup:_financeCourse,teacherId:_financeTeacherId,search:_financeSearch);MetricRow([("课程总收入",Fen(s["income_fen"]),s["n"]+" 笔符合筛选"),("手续费",Fen(s["fee_fen"]),"该课程相关手续费"),("实收净额",Fen(s["income_fen"]-s["fee_fen"]),"收入－手续费"),("已关联经营收入",Fen(s["operating_income_fen"]),"经营口径")]);var ms=_store.FinanceMonthlySummary(courseGroup:_financeCourse,teacherId:_financeTeacherId,search:_financeSearch);_body.Children.Add(SectionTitle("每月收入"));Table(["月份","收入笔数","总收入","手续费","实收净额"],ms.Select(r=>new object?[]{MonthName(S(r,"month")),L(r,"n"),Fen(L(r,"income_fen")),Fen(L(r,"fee_fen")),Fen(L(r,"income_fen")-L(r,"fee_fen"))}),ms.Select(r=>S(r,"month")),Math.Min(420,120+ms.Count*46),m=>{_financeCourseMonth=m;Navigate("财务中心");});if(!string.IsNullOrWhiteSpace(_financeCourseMonth)){_body.Children.Add(SectionTitle(MonthName(_financeCourseMonth)+" · "+_financeCourse+" 收入明细"));FinanceRecordActions();FinanceTransactionGrid(month:_financeCourseMonth,courseGroup:_financeCourse,teacherId:_financeTeacherId,search:_financeSearch);}
    }

    private void FinanceCourseFilter()
    {
        var row=new WrapPanel{Orientation=Orientation.Horizontal};var teachers=new ComboBox{Width=180,Margin=new Thickness(0,0,8,8),DisplayMemberPath="Text",SelectedValuePath="Value"};teachers.Items.Add(new FormChoice("全部老师",null));foreach(var r in _store.Rows("SELECT * FROM teachers ORDER BY active DESC,name"))teachers.Items.Add(new FormChoice(S(r,"name"),S(r,"id")));SelectCombo(teachers,_financeTeacherId);var search=new TextBox{Width=280,Text=_financeSearch,Margin=new Thickness(0,0,8,8)};row.Children.Add(teachers);row.Children.Add(search);row.Children.Add(Btn("应用筛选",()=>{_financeTeacherId=ComboValue(teachers);_financeSearch=search.Text.Trim();_financeCourseMonth=null;Navigate("财务中心");},true));row.Children.Add(Btn("清除",()=>{_financeTeacherId=null;_financeSearch="";_financeCourseMonth=null;Navigate("财务中心");}));_body.Children.Add(SectionCard(row,new Thickness(20,16,12,8)));
    }

    private void FinanceTransactionsView()
    {
        var student=_financeStudentId;if(student is not null){try{_=_store.Get("students",student);}catch{student=null;_financeStudentId=null;}}FinanceTransactionFilters();var s=_store.FinanceSummary(student,account:_financeTxAccount,month:_financeTxMonth,courseGroup:_financeTxCourse,teacherId:_financeTeacherId,search:_financeSearch);if(student is not null)_body.Children.Add(SectionNote("当前学员："+S(_store.Get("students",student),"name")+" · 搜索和筛选只作用于该学员关联流水",true));MetricRow([("流水笔数",s["n"].ToString(),"当前筛选"),("收入",Fen(s["income_fen"]),"借方 / 收入"),("支出",Fen(s["expense_fen"]),"贷方 / 支出"),("手续费",Fen(s["fee_fen"]),"独立费用"),("净现金",Fen(s["net_fen"]),"当前筛选净变化")]);_body.Children.Add(SectionNote("完整列宽显示；横向滚动查看右侧字段；双击任意流水直接编辑。"));FinanceRecordActions();FinanceTransactionGrid(student,_financeTxAccount,_financeTxMonth,_financeTxCourse,_financeTeacherId,_financeSearch);
    }

    private void FinanceTransactionFilters()
    {
        var row=new WrapPanel{Orientation=Orientation.Horizontal};var accounts=new ComboBox{Width=190,Margin=new Thickness(0,0,8,8),DisplayMemberPath="Text",SelectedValuePath="Value"};accounts.Items.Add(new FormChoice("全部户头",null));foreach(var a in _store.FinanceAccountSummary())accounts.Items.Add(new FormChoice(a.Account,a.Account));SelectCombo(accounts,_financeTxAccount);var months=new ComboBox{Width=145,Margin=new Thickness(0,0,8,8),DisplayMemberPath="Text",SelectedValuePath="Value"};months.Items.Add(new FormChoice("全部月份",null));foreach(var r in _store.FinanceMonthlySummary())months.Items.Add(new FormChoice(MonthName(S(r,"month")),S(r,"month")));SelectCombo(months,_financeTxMonth);var courses=new ComboBox{Width=175,Margin=new Thickness(0,0,8,8),DisplayMemberPath="Text",SelectedValuePath="Value"};courses.Items.Add(new FormChoice("全部课程",null));foreach(var r in _store.FinanceCourseSummary())courses.Items.Add(new FormChoice(S(r,"course_group"),S(r,"course_group")));SelectCombo(courses,_financeTxCourse);var teachers=new ComboBox{Width=155,Margin=new Thickness(0,0,8,8),DisplayMemberPath="Text",SelectedValuePath="Value"};teachers.Items.Add(new FormChoice("全部老师",null));foreach(var r in _store.Rows("SELECT * FROM teachers ORDER BY active DESC,name"))teachers.Items.Add(new FormChoice(S(r,"name"),S(r,"id")));SelectCombo(teachers,_financeTeacherId);var search=new TextBox{Width=250,Margin=new Thickness(0,0,8,8),Text=_financeSearch};row.Children.Add(accounts);row.Children.Add(months);row.Children.Add(courses);row.Children.Add(teachers);row.Children.Add(search);row.Children.Add(Btn("应用",()=>{_financeTxAccount=ComboValue(accounts);_financeTxMonth=ComboValue(months);_financeTxCourse=ComboValue(courses);_financeTeacherId=ComboValue(teachers);_financeSearch=search.Text.Trim();Navigate("财务中心");},true));if(_financeStudentId is not null)row.Children.Add(Btn("退出学员筛选",()=>{_financeStudentId=null;Navigate("财务中心");}));_body.Children.Add(SectionCard(row,new Thickness(20,16,12,8)));
    }

    private void FinanceRecordActions()
    {
        Toolbar([("编辑流水",()=>EditFinance(SelectedId()),false),("查看详情",FinanceDetail,false),("匹配学员",FinanceMatch,false),("课程 / 班级",()=>EditFinance(SelectedId()),false),("经营分类",FinanceClassify,false),("标记非学员",FinanceMarkNonStudent,false),("取消学员关联",FinanceUnlink,false),("删除手工流水",FinanceDeleteManual,true)],false);
    }

    private DataGrid FinanceTransactionGrid(string? studentId=null,string? account=null,string? month=null,string? courseGroup=null,string? teacherId=null,string? search=null)
    {
        var rs=_store.FinanceRows(studentId,10000,account,month,courseGroup,teacherId,search);var rows=rs.Select(r=>new object?[]{S(r,"tx_date"),S(r,"source_sheet"),string.IsNullOrWhiteSpace(S(r,"summary"))?"（未备注）":S(r,"summary"),string.IsNullOrWhiteSpace(S(r,"students"))?"—":S(r,"students"),string.IsNullOrWhiteSpace(S(r,"catalog_course"))?"暂且无":S(r,"catalog_course"),string.IsNullOrWhiteSpace(S(r,"class_name"))?"暂且无":S(r,"class_name"),string.IsNullOrWhiteSpace(S(r,"teacher"))?"—":S(r,"teacher"),L(r,"income_fen")>0?Fen(L(r,"income_fen")):"—",L(r,"expense_fen")>0?Fen(L(r,"expense_fen")):"—",L(r,"fee_fen")>0?Fen(L(r,"fee_fen")):"—",Fen(L(r,"income_fen")-L(r,"expense_fen")-L(r,"fee_fen")),NatureName(S(r,"nature")),MatchName(S(r,"match_status"))});var grid=Table(["日期","户头","摘要 / 学生","关联学员","课程","班级","老师","收入","支出","手续费","净现金","经营性质","匹配"],rows,rs.Select(r=>S(r,"id")),Math.Min(650,180+Math.Min(10,rs.Count)*46),id=>EditFinance(id));grid.MinWidth=1200;return grid;
    }

    private void EditFinance(string? id)
    {
        Dictionary<string,object?>? tx=id is null?null:_store.Get("finance_transactions",id);var defaultAccount=_financeAccount??_store.FinanceAccountSummary().FirstOrDefault()?.Account??"";var d=new FinanceEditWindow(this,_store,tx,defaultAccount);if(d.ShowDialog()!=true)return;if(id is null)_store.AddManualFinance(d.Result);else _store.UpdateFinanceTransaction(id,d.Result);Navigate("财务中心");
    }
    private void ImportFinance(){var path=FileDialogService.Open("导入财务表格","Excel 财务表格 (*.xls;*.xlsx;*.xlsm)|*.xls;*.xlsx;*.xlsm");if(path is null)return;var parsed=_financeExcel.Parse(path);var s=_financeExcel.Summary(parsed.Rows);var warning=parsed.Warnings.Count>0?"\n\n未识别："+string.Join('；',parsed.Warnings):"";var text=$"识别 {parsed.Meta.GetValueOrDefault("sheet_count")} 个工作表、{s["rows"]} 条资金流水。\n借方流入：{Fen(s["income_fen"])}\n贷方流出：{Fen(s["expense_fen"])}\n手续费：{Fen(s["fee_fen"])}\n可自动关联学员：{s["matched"]} 条；待人工匹配：{s["unmatched"]} 条。\n\n导入不会自动增加课时。{warning}";if(MessageBox.Show(this,text,"确认导入财务流水",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;var result=_store.ImportFinanceBatch(parsed.Meta,parsed.Rows);MessageBox.Show(this,$"新增 {result["imported"]} 条；重复跳过 {result["duplicate"]} 条；自动关联 {result["matched"]} 条。","导入完成");_financeView="overview";_financeStudentId=null;Navigate("财务中心");}
    private void ExportFinance(){var path=FileDialogService.Save("另存规范财务 Excel","Excel 工作簿 (*.xlsx)|*.xlsx",$"酷咔规范财务-{DateTime.Today:yyyy-MM-dd}.xlsx");if(path is null)return;_financeExcel.ExportFinance(path);MessageBox.Show(this,"已导出，并设为后续“一键同步 Excel”的目标。","导出完成");Navigate("财务中心");}
    private void SyncFinance(){var path=_store.Setting("finance_sync_path");if(string.IsNullOrWhiteSpace(path))path=FileDialogService.Save("设置财务同步 Excel","Excel 工作簿 (*.xlsx)|*.xlsx",$"酷咔财务同步-{DateTime.Today:yyyy-MM-dd}.xlsx");if(string.IsNullOrWhiteSpace(path))return;try{_financeExcel.ExportFinance(path);MessageBox.Show(this,"同步完成。","财务同步");}catch(IOException){throw new InvalidOperationException("同步文件可能正在被 Excel/WPS 占用。请关闭文件后重试。");}Navigate("财务中心");}

    private void FinanceMatch(){var tx=SelectedId();var opts=_store.Rows("SELECT * FROM students ORDER BY active DESC,name").Select(r=>new FormChoice(S(r,"name")+(B(r,"active")?"":"（已归档）"),S(r,"id"))).ToList();if(opts.Count==0)throw new InvalidOperationException("还没有学员档案");var d=EditForm("匹配财务流水",[new("student_id","关联学员 *",FormFieldKind.Reference,Choices:opts,Required:true)]);if(d is null)return;_store.LinkFinanceStudent(tx,Convert.ToString(d["student_id"])!);Navigate("财务中心");}
    private void FinanceUnlink(){var id=SelectedId();if(MessageBox.Show(this,"取消后该流水会回到“待匹配”。","取消关联",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;_store.UnlinkFinanceStudent(id);Navigate("财务中心");}
    private void FinanceMarkNonStudent(){var id=SelectedId();if(MessageBox.Show(this,"适用于学校课时费、内部转账等不应关联学员的流水。","标记为非学员流水",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;_store.MarkFinanceNonStudent(id);Navigate("财务中心");}
    private void FinanceClassify(){var id=SelectedId();var tx=_store.Get("finance_transactions",id);var choices=new List<FormChoice>{new("经营收入","operating_income"),new("经营支出","operating_expense"),new("内部划转","internal_transfer"),new("退款","refund"),new("其他收入","other_income"),new("待分类","unknown")};var d=EditForm("修改经营分类",[new("nature","经营性质",FormFieldKind.Choice,S(tx,"nature"),choices,true),new("category","分类",FormFieldKind.Text,S(tx,"category"),Required:true)],tx,"借/贷是现金方向；经营性质用于利润分析。内部划转不计经营成本。");if(d is null)return;_store.UpdateFinanceClassification(id,Convert.ToString(d["nature"])!,Convert.ToString(d["category"])!);Navigate("财务中心");}
    private void FinanceDeleteManual(){var id=SelectedId();var tx=_store.Get("finance_transactions",id);if(S(tx,"entry_source")!="manual")throw new InvalidOperationException("Excel 导入流水作为原始凭据不能直接删除");if(MessageBox.Show(this,"确认永久删除这笔手工流水？操作会写入日志。","删除手工流水",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;_store.DeleteManualFinance(id);Navigate("财务中心");}
    private void FinanceDetail(){var id=SelectedId();var tx=_store.FinanceRows(limit:200000).FirstOrDefault(x=>S(x,"id")==id)??_store.Get("finance_transactions",id);var net=L(tx,"income_fen")-L(tx,"expense_fen")-L(tx,"fee_fen");MessageBox.Show(this,$"日期：{S(tx,"tx_date")}\n户头：{S(tx,"source_sheet")}\n摘要：{S(tx,"summary")}\n关联学员：{S(tx,"students")}\n课程：{S(tx,"catalog_course")}\n班级：{S(tx,"class_name")}\n原课程提示：{S(tx,"course_hint")}\n老师：{S(tx,"teacher")}\n\n收入：{Fen(L(tx,"income_fen"))}\n支出：{Fen(L(tx,"expense_fen"))}\n手续费：{Fen(L(tx,"fee_fen"))}\n净现金：{Fen(net)}\n\n经营分类：{S(tx,"category")}\n来源：{(S(tx,"entry_source")=="manual"?"手工录入":"Excel 导入")} · 原行 {I(tx,"source_row")}\n备注：{S(tx,"raw_note")}","财务流水详情");}

    private static string NatureName(string v)=>v switch{"operating_income"=>"经营收入","operating_expense"=>"经营支出","internal_transfer"=>"内部划转","refund"=>"退款","other_income"=>"其他收入","unknown"=>"待分类",_=>v};
    private static string MatchName(string v)=>v switch{"matched"=>"已匹配","unmatched"=>"待匹配","nonstudent"=>"非学员","suggested"=>"建议匹配",_=>v};
    private static string? ComboValue(ComboBox c)=>(c.SelectedItem as FormChoice)?.Value?.ToString();
    private static void SelectCombo(ComboBox c,string? value){foreach(var x in c.Items.Cast<FormChoice>())if(Convert.ToString(x.Value)==value){c.SelectedItem=x;return;}if(c.Items.Count>0)c.SelectedIndex=0;}
}
