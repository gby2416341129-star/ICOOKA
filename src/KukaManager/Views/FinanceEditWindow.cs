using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KukaManager.Data;
using KukaManager.Design;
using KukaManager.Models;
using KukaManager.UI.Components;
using KukaManager.Utils;
using static KukaManager.Utils.Value;

namespace KukaManager.Views;

public sealed class FinanceEditWindow : Window
{
    private readonly KukaStore _store;
    private readonly Dictionary<string,object?>? _tx;
    private readonly DatePicker _date = new();
    private readonly TextBox _account = new();
    private readonly ComboBox _direction = new();
    private readonly TextBox _amount = new();
    private readonly TextBox _fee = new();
    private readonly TextBox _balance = new();
    private readonly TextBox _summary = new();
    private readonly ComboBox _student = new();
    private readonly ComboBox _match = new();
    private readonly ComboBox _course = new();
    private readonly ComboBox _class = new();
    private readonly TextBlock _classInfo = new();
    private readonly ComboBox _nature = new();
    private readonly TextBox _category = new();
    private readonly TextBox _note = new();
    private string? _initialClass;
    public Dictionary<string,object?> Result { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public FinanceEditWindow(Window owner,KukaStore store,Dictionary<string,object?>? tx=null,string defaultAccount="")
    {
        Owner=owner;_store=store;_tx=tx;Title=tx is null?"新增财务流水":"编辑财务流水";Width=760;Height=850;MinHeight=650;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Theme.Brush(Theme.BackgroundColor);
        var root=new DockPanel{Margin=new Thickness(24)};var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};var cancel=new Button{Content="取消",MinWidth=90,Margin=new Thickness(0,0,10,0)};cancel.Click+=(_,_)=>DialogResult=false;var save=WindowChrome.PrimaryButton("保存");save.Click+=Save;footer.Children.Add(cancel);footer.Children.Add(save);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var body=new StackPanel();body.Children.Add(WindowChrome.Text(tx is null?"新增财务流水":"编辑财务流水",26,FontWeights.SemiBold));body.Children.Add(WindowChrome.Text(tx is null?"课程只能从课程目录选择；班级可暂且无。选择班级后自动关联老师、上课时间、班级成员与周排课。":"可以修改导入流水的业务字段；原始 Excel 证据、来源行和 raw JSON 不删除。",13,null,Theme.Brush(Theme.MutedColor)));body.Children.Add(new Border{Height=18});
        Add(body,"日期 *",_date); Add(body,"户头 / 账户 *",_account);
        _direction.Items.Add(new FormChoice("收入 / 借","借"));_direction.Items.Add(new FormChoice("支出 / 贷","贷"));_direction.DisplayMemberPath="Text";_direction.SelectedValuePath="Value";Add(body,"资金方向 *",_direction);
        var moneyRow=new Grid{Margin=new Thickness(0,0,0,20)};moneyRow.ColumnDefinitions.Add(new ColumnDefinition());moneyRow.ColumnDefinitions.Add(new ColumnDefinition());var a=FieldHost("金额 *",_amount);var f=FieldHost("手续费",_fee);Grid.SetColumn(f,1);f.Margin=new Thickness(12,0,0,0);moneyRow.Children.Add(a);moneyRow.Children.Add(f);body.Children.Add(moneyRow);
        Add(body,"可确认余额（元，可留空）",_balance);Add(body,"摘要 / 学员姓名 *",_summary);
        ConfigureCombo(_student);_student.Items.Add(new FormChoice("暂且无",null));foreach(var r in _store.Rows("SELECT * FROM students ORDER BY active DESC,name"))_student.Items.Add(new FormChoice(S(r,"name")+(B(r,"active")?"":"（已归档）"),S(r,"id")));Add(body,"关联学员",_student);
        ConfigureCombo(_match);_match.Items.Add(new FormChoice("待匹配","unmatched"));_match.Items.Add(new FormChoice("非学员流水","nonstudent"));Add(body,"无学员时的匹配状态",_match);
        ConfigureCombo(_course);_course.Items.Add(new FormChoice("暂且无",null));var currentCourse=tx is null?null:S(tx,"course_id");foreach(var r in _store.Rows("SELECT * FROM courses WHERE active=1 OR id=? ORDER BY active DESC,category,name",currentCourse??""))_course.Items.Add(new FormChoice($"{S(r,"name")} · {S(r,"category")}"+(B(r,"active")?"":"（已停用）"),S(r,"id")));_course.SelectionChanged+=(_,_)=>ReloadClasses();Add(body,"课程（来自课程目录）",_course);
        ConfigureCombo(_class);_class.SelectionChanged+=(_,_)=>RefreshClassInfo();Add(body,"班级（可暂且无）",_class);_classInfo.TextWrapping=TextWrapping.Wrap;_classInfo.Foreground=Theme.Brush(Theme.MutedColor);body.Children.Add(new Border{Background=new SolidColorBrush(Color.FromRgb(245,249,247)),BorderBrush=Theme.Brush(Theme.SoftBorderColor),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(14),Margin=new Thickness(0,0,0,20),Child=_classInfo});
        ConfigureCombo(_nature);foreach(var x in new[]{new FormChoice("经营收入","operating_income"),new FormChoice("经营支出","operating_expense"),new FormChoice("内部划转","internal_transfer"),new FormChoice("退款","refund"),new FormChoice("其他收入","other_income"),new FormChoice("待分类","unknown")})_nature.Items.Add(x);Add(body,"经营性质",_nature);Add(body,"财务分类",_category);_note.AcceptsReturn=true;_note.TextWrapping=TextWrapping.Wrap;_note.VerticalContentAlignment=VerticalAlignment.Top;_note.Height=104;Add(body,"备注",_note);
        scroll.Content=WindowChrome.Card(body,new Thickness(30));root.Children.Add(scroll);Content=KukaWindowFrame.Wrap(this,root,Title,true);LoadInitial(defaultAccount);
    }

    private static void ConfigureCombo(ComboBox c){c.DisplayMemberPath=nameof(FormChoice.Text);c.SelectedValuePath=nameof(FormChoice.Value);}
    private static FrameworkElement FieldHost(string label,Control c){var p=new StackPanel();p.Children.Add(WindowChrome.Text(label,13,FontWeights.SemiBold,Theme.Brush(Theme.MutedColor)));c.Margin=new Thickness(0,8,0,0);p.Children.Add(c);return p;}
    private static void Add(Panel p,string label,Control c){var h=FieldHost(label,c);h.Margin=new Thickness(0,0,0,20);p.Children.Add(h);}
    private static void SelectValue(ComboBox c,object? value){foreach(var item in c.Items.Cast<FormChoice>())if(string.Equals(Convert.ToString(item.Value),Convert.ToString(value),StringComparison.Ordinal)){c.SelectedItem=item;return;}if(c.Items.Count>0)c.SelectedIndex=0;}
    private string? Selected(ComboBox c)=>(c.SelectedItem as FormChoice)?.Value?.ToString();

    private void LoadInitial(string defaultAccount)
    {
        var t=_tx;
        if(t is null)_date.SelectedDate=DateTime.Today;else if(DateTime.TryParse(S(t,"tx_date"),out var dt))_date.SelectedDate=dt;
        _account.Text=t is null?defaultAccount:S(t,"source_sheet");SelectValue(_direction,t is null?"借":S(t,"direction"));var gross=t is null?0:Math.Max(L(t,"income_fen"),L(t,"expense_fen"));_amount.Text=(gross/100m).ToString("0.00",CultureInfo.InvariantCulture);_fee.Text=((t is null?0:L(t,"fee_fen"))/100m).ToString("0.00",CultureInfo.InvariantCulture);_balance.Text=t is null||t["balance_fen"] is null?"":(L(t,"balance_fen")/100m).ToString("0.00",CultureInfo.InvariantCulture);_summary.Text=t is null?"":S(t,"summary");_category.Text=t is null?"学费/培训费":S(t,"category");_note.Text=t is null?"":S(t,"raw_note");
        string? currentStudent=null;if(t is not null){var al=_store.One("SELECT student_id FROM finance_allocations WHERE transaction_id=? LIMIT 1",S(t,"id"));currentStudent=al is null?null:S(al,"student_id");}SelectValue(_student,currentStudent);SelectValue(_match,t is not null&&!string.IsNullOrEmpty(currentStudent)?"unmatched":t is null?"unmatched":S(t,"match_status"));SelectValue(_course,t is null?null:S(t,"course_id"));_initialClass=t is null?null:S(t,"class_id");ReloadClasses();SelectValue(_nature,t is null?"operating_income":S(t,"nature"));
    }

    private void ReloadClasses()
    {
        var selected=_initialClass??Selected(_class);var course=Selected(_course);_class.Items.Clear();_class.Items.Add(new FormChoice("暂且无（不加入排课）",null));if(!string.IsNullOrWhiteSpace(course))foreach(var r in _store.Rows("SELECT c.*,t.name teacher FROM classes c JOIN teachers t ON t.id=c.teacher_id WHERE c.course_id=? AND (c.active=1 OR c.id=?) ORDER BY c.active DESC,c.end_date DESC,c.name",course,selected??""))_class.Items.Add(new FormChoice(S(r,"name")+(B(r,"active")?"":"（已结束/停用）"),S(r,"id")));SelectValue(_class,selected);_initialClass=null;RefreshClassInfo();
    }

    private void RefreshClassInfo()
    {
        var id=Selected(_class);if(string.IsNullOrWhiteSpace(id)){_classInfo.Text=Selected(_course) is not null&&_class.Items.Count==1?"该课程暂无可选班级。请先到“班级管理”创建班级；当前缴费可先保存为暂且无班级。":"暂未选择班级。不会自动加入班级和周排课；以后编辑这笔缴费再选择班级即可形成历史关联。";return;}var c=_store.One("SELECT c.*,t.name teacher FROM classes c JOIN teachers t ON t.id=c.teacher_id WHERE c.id=?",id);if(c is null)return;_classInfo.Text=$"{S(c,"name")} · {S(c,"teacher")}\n每周{"一二三四五六日"[I(c,"weekday")]} {S(c,"start_time")}–{S(c,"end_time")} · 教室 {(string.IsNullOrWhiteSpace(S(c,"room"))?"未设置":S(c,"room"))} · {S(c,"start_date")} 至 {S(c,"end_date")}";
    }

    private void Save(object? sender,RoutedEventArgs e)
    {
        try
        {
            if(!_date.SelectedDate.HasValue)throw new InvalidOperationException("请选择日期");if(string.IsNullOrWhiteSpace(_account.Text))throw new InvalidOperationException("请填写户头 / 账户");if(string.IsNullOrWhiteSpace(_summary.Text))throw new InvalidOperationException("请填写摘要 / 学员姓名");
            if(!decimal.TryParse(_amount.Text.Trim(),NumberStyles.Number,CultureInfo.InvariantCulture,out var amount)&&!decimal.TryParse(_amount.Text.Trim(),out amount))throw new InvalidOperationException("金额格式不正确");if(amount<=0)throw new InvalidOperationException("金额必须大于 0");if(!decimal.TryParse(string.IsNullOrWhiteSpace(_fee.Text)?"0":_fee.Text.Trim(),NumberStyles.Number,CultureInfo.InvariantCulture,out var fee)&&!decimal.TryParse(_fee.Text.Trim(),out fee))throw new InvalidOperationException("手续费格式不正确");
            var student=Selected(_student);var course=Selected(_course);var klass=Selected(_class);if(klass is not null&&student is null)throw new InvalidOperationException("选择班级后必须关联学员");if(klass is not null&&course is null)throw new InvalidOperationException("选择班级前必须选择课程");
            Result=new(StringComparer.OrdinalIgnoreCase){{"tx_date",_date.SelectedDate.Value.ToString("yyyy-MM-dd")},{"source_sheet",_account.Text.Trim()},{"direction",Selected(_direction)},{"amount_fen",Cents(amount)},{"fee_fen",Cents(fee)},{"balance",_balance.Text.Trim()},{"summary",_summary.Text.Trim()},{"student_id",student},{"match_status",Selected(_match)},{"course_id",course},{"class_id",klass},{"nature",Selected(_nature)},{"category",_category.Text.Trim()},{"raw_note",_note.Text.Trim()},{"nonstudent",Selected(_match)=="nonstudent"}};DialogResult=true;
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,"请检查",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
}
