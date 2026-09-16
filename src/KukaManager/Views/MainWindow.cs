using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using KukaManager.Data;
using KukaManager.Design;
using KukaManager.Models;
using KukaManager.Services;
using KukaManager.UI.Animations;
using KukaManager.UI.Components;
using KukaManager.UI.Controls;
using KukaManager.Utils;
using KukaManager.ViewModels;
using static KukaManager.Utils.Value;

namespace KukaManager.Views;

public sealed partial class MainWindow : Window
{
    private readonly KukaStore _store;
    private readonly FinanceExcelService _financeExcel;
    private readonly ExchangeExcelService _exchange;
    private readonly LogService _log;
    private readonly MainViewModel _vm = new();
    private readonly StackPanel _body = new();
    private readonly ScrollViewer _pageScroll = new();
    private readonly TextBlock _headerPageTitle = WindowChrome.Text("工作台", 12, FontWeights.Normal, Theme.Brush(Theme.MutedColor));
    private readonly Dictionary<string,Button> _nav = new();
    private readonly Dictionary<string,Border> _navIndicators = new();
    private DataGrid? _table;
    private DateTime _week;
    private string _page="工作台";

    private string _financeView="overview";
    private string? _financeStudentId;
    private string? _financeAccount;
    private string? _financeAccountMonth;
    private string? _financeCourse;
    private string? _financeCourseMonth;
    private string? _financeTeacherId;
    private string? _financeTxAccount;
    private string? _financeTxMonth;
    private string? _financeTxCourse;
    private string _financeSearch="";

    private static readonly string[] Pages=["工作台","学员档案","报名与费用","周排课表","班级管理","出勤与补课","课消账本","学习成长","课程与老师","财务中心","操作日志","数据与设置"];

    public MainWindow(KukaStore store,FinanceExcelService financeExcel,ExchangeExcelService exchange,LogService log)
    {
        _store=store;_financeExcel=financeExcel;_exchange=exchange;_log=log;_week=StartOfWeek(DateTime.Today);
        Title="酷咔管理系统";Width=1360;Height=860;MinWidth=1080;MinHeight=680;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Theme.Brush(Theme.BackgroundColor);
        KukaWindowFrame.Configure(this,true);
        FontFamily=Theme.BodyFont;UseLayoutRounding=true;SnapsToDevicePixels=true;
        TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);TextOptions.SetTextHintingMode(this,TextHintingMode.Fixed);TextOptions.SetTextRenderingMode(this,TextRenderingMode.ClearType);
        DataContext=_vm;
        Content=BuildShell();
        Loaded+=(_,_)=>Navigate("工作台");
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(30)};timer.Tick+=(_,_)=>Guard(()=>{var n=_store.ProcessDue();if(n>0)Navigate(_page);});timer.Start();
        Closing+=(_,_)=>{try{_store.Backup();}catch(Exception ex){_log.Error(ex,"close backup");}};
    }

    private UIElement BuildShell()
    {
        var root = new Grid { Background = Theme.Brush(Theme.BackgroundColor) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var header=KukaWindowFrame.Header(this,"酷咔管理系统",true,_headerPageTitle);
        Grid.SetRow(header,0);Grid.SetColumnSpan(header,2);root.Children.Add(header);

        var sidebar = new Border
        {
            Background = Theme.Brush(Theme.SidebarColor),
            BorderBrush = Theme.Brush(Theme.BorderColor),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(10, 18, 10, 14)
        };

        var shell = new DockPanel();
        var footer = new Border
        {
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.FromRgb(247, 249, 252)),
            BorderBrush = Theme.Brush(Theme.SoftBorderColor),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(11, 9, 11, 9),
            Margin = new Thickness(2, 10, 2, 0)
        };
        var footerRow = new StackPanel { Orientation = Orientation.Horizontal };
        footerRow.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Theme.Brush(Theme.SuccessColor), Margin = new Thickness(0, 5, 9, 0) });
        var footerText = new StackPanel();
        footerText.Children.Add(WindowChrome.Text("本机离线运行", 12, FontWeights.SemiBold));
        footerText.Children.Add(WindowChrome.Text("数据仅保存在此电脑", 11, null, Theme.Brush(Theme.MutedColor)));
        footerRow.Children.Add(footerText); footer.Child = footerRow; DockPanel.SetDock(footer, Dock.Bottom); shell.Children.Add(footer);

        var nav = new StackPanel();
        var brand = new Grid { Margin = new Thickness(6, 2, 6, 20), Height = 42 };
        brand.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        brand.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var mark = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = Theme.Brush(Theme.BrandOrangeColor), VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = true };
        mark.Child = new TextBlock { Text = "K", FontFamily = Theme.DisplayFont, FontWeight = FontWeights.SemiBold, FontSize = 18, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, SnapsToDevicePixels = true };
        brand.Children.Add(mark);
        var brandText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        brandText.Children.Add(WindowChrome.Text("酷咔", 20, FontWeights.SemiBold));
        brandText.Children.Add(WindowChrome.Text("教育运营中心", 11, FontWeights.Normal, Theme.Brush(Theme.MutedColor)));
        Grid.SetColumn(brandText, 1); brand.Children.Add(brandText); nav.Children.Add(brand);
        AddNavCaption(nav,"概览");

        foreach (var page in Pages)
        {
            if(page=="学员档案")AddNavCaption(nav,"教学运营");
            if(page=="财务中心")AddNavCaption(nav,"经营管理");
            if(page=="操作日志")AddNavCaption(nav,"系统");
            var p = page;
            var b = new Button { Margin = new Thickness(0, 1, 0, 1), Height = 44, ToolTip = PageSubtitle(page) };
            if (Application.Current.Resources["NavButtonStyle"] is Style navStyle) b.Style = navStyle;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var indicator = new Border { Width = 3, Height = 20, CornerRadius = new CornerRadius(2), Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(indicator);
            var icon = WindowChrome.FluentIcon(PageIcon(page), 17, Theme.Brush(Theme.MutedColor)); icon.Width = 24; icon.Margin = new Thickness(1, 0, 0, 0); Grid.SetColumn(icon, 1); row.Children.Add(icon);
            var label = WindowChrome.Text(page, 14, FontWeights.Normal); label.VerticalAlignment = VerticalAlignment.Center; label.Margin = new Thickness(1,0,0,0); Grid.SetColumn(label, 2); row.Children.Add(label);
            b.Content = row;
            b.Click += (_, _) => Navigate(p);
            nav.Children.Add(b); _nav[p] = b; _navIndicators[p] = indicator;
        }

        var navScroll = new ScrollViewer { Content = nav, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        shell.Children.Add(navScroll); sidebar.Child = shell; Grid.SetRow(sidebar,1);Grid.SetColumn(sidebar, 0); root.Children.Add(sidebar);

        _pageScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _pageScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _pageScroll.Background = Theme.Brush(Theme.BackgroundColor);
        _body.MaxWidth = 1480; _body.HorizontalAlignment = HorizontalAlignment.Stretch;
        _pageScroll.Content = new Border { Padding = new Thickness(32, 24, 32, 36), Child = _body };
        Grid.SetRow(_pageScroll,1);Grid.SetColumn(_pageScroll, 1); root.Children.Add(_pageScroll);
        return root;
    }

    private static void AddNavCaption(Panel nav,string text)
    {
        var caption=WindowChrome.Text(text,11,FontWeights.SemiBold,Theme.Brush(Theme.MutedColor));
        caption.Margin=new Thickness(10,9,0,6);
        nav.Children.Add(caption);
    }

    private static string PageIcon(string page) => page switch
    {
        "工作台" => "\uE80F",
        "学员档案" => "\uE77B",
        "报名与费用" => "\uE8C7",
        "周排课表" => "\uE787",
        "班级管理" => "\uE716",
        "出勤与补课" => "\uE73E",
        "课消账本" => "\uE8F1",
        "学习成长" => "\uE7BE",
        "课程与老师" => "\uE779",
        "财务中心" => "\uE8C7",
        "操作日志" => "\uE81C",
        "数据与设置" => "\uE713",
        _ => "\uE10F"
    };

    private static string PageSubtitle(string page) => page switch
    {
        "工作台" => "总览今日业务、学员与课程状态",
        "学员档案" => "查看和维护学员完整资料",
        "报名与费用" => "报名、续费、课包与费用",
        "周排课表" => "本周课程、老师、教室与学员",
        "班级管理" => "班级、成员、时间与老师安排",
        "出勤与补课" => "出勤更正、缺课与补课安排",
        "课消账本" => "每一笔课时与金额变化记录",
        "学习成长" => "课堂成果、评价与阶段目标",
        "课程与老师" => "课程目录与教师档案",
        "财务中心" => "资金、户头、课程收入与流水",
        "操作日志" => "查看关键操作审计记录",
        "数据与设置" => "备份、恢复、导入导出与系统设置",
        _ => "酷咔管理系统"
    };

    private void Navigate(string page)
    {
        Guard(() =>
        {
            _page = page; _vm.CurrentPage = page; _headerPageTitle.Text=page; _body.Children.Clear(); _table = null;
            foreach (var x in _nav)
            {
                var active = x.Key == page;
                x.Value.Background = active ? Theme.Brush(Theme.NavSelectedColor) : Brushes.Transparent;
                if (_navIndicators.TryGetValue(x.Key, out var indicator)) indicator.Background = active ? Theme.Brush(Theme.PrimaryColor) : Brushes.Transparent;
                if (x.Value.Content is Grid g)
                {
                    foreach (var tb in g.Children.OfType<TextBlock>())
                    {
                        if (tb.FontFamily.Source.Contains("MDL2", StringComparison.OrdinalIgnoreCase)) tb.Foreground = active ? Theme.Brush(Theme.PrimaryColor) : Theme.Brush(Theme.MutedColor);
                        else { tb.Foreground = Theme.Brush(Theme.TextColor); tb.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal; }
                    }
                }
            }
            _body.Children.Add(Hero(page));
            switch(page){case "工作台":DashboardPage();break;case "学员档案":StudentsPage();break;case "报名与费用":EnrollmentsPage();break;case "周排课表":CalendarPage();break;case "班级管理":ClassesPage();break;case "出勤与补课":AttendancePage();break;case "课消账本":LedgerPage();break;case "学习成长":GrowthPage();break;case "课程与老师":CatalogPage();break;case "财务中心":FinancePage();break;case "操作日志":AuditPage();break;case "数据与设置":SettingsPage();break;}
            _pageScroll.ScrollToTop();
            KukaMotion.Reveal(_body);
        });
    }

    private UIElement Hero(string page)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 24) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel();
        var title = WindowChrome.Text(page, 30, FontWeights.SemiBold); title.Margin = new Thickness(0, 0, 0, 6); left.Children.Add(title);
        left.Children.Add(WindowChrome.Text(PageSubtitle(page), 13, FontWeights.Normal, Theme.Brush(Theme.MutedColor)));
        grid.Children.Add(left);

        var offline = WindowChrome.Pill("●  本机离线", new SolidColorBrush(Color.FromRgb(238, 248, 243)), Theme.Brush(Theme.SuccessColor));
        offline.HorizontalAlignment = HorizontalAlignment.Right;
        offline.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(offline, 1); grid.Children.Add(offline);
        return grid;
    }

    private static DateTime StartOfWeek(DateTime d)=>d.Date.AddDays(-(((int)d.DayOfWeek+6)%7));
    private static string MonthName(string m)=>DateTime.TryParseExact(m+"-01","yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?d.ToString("yyyy年M月"):m;

    private void Guard(Action action)
    {
        try{action();}
        catch(Exception ex){_log.Error(ex,_page);MessageBox.Show(this,ex.Message,"未完成",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    private T? Guard<T>(Func<T> work)
    {
        try{return work();}catch(Exception ex){_log.Error(ex,_page);MessageBox.Show(this,ex.Message,"未完成",MessageBoxButton.OK,MessageBoxImage.Warning);return default;}
    }

    private Border SectionCard(UIElement content,Thickness? padding=null)
    {
        var b=WindowChrome.Card(content,padding??new Thickness(24));b.Margin=new Thickness(0,0,0,16);return b;
    }
    private TextBlock SectionTitle(string text){var t=WindowChrome.Text(text,20,FontWeights.SemiBold);t.Margin=new Thickness(0,6,0,12);return t;}
    private TextBlock SectionNote(string text, bool accent=false)
    {
        var t=WindowChrome.Text(text,13,accent?FontWeights.SemiBold:FontWeights.Normal,Theme.Brush(accent?Theme.PrimaryColor:Theme.MutedColor));
        t.Margin=new Thickness(0,0,0,14);
        return t;
    }
    private Button Btn(string text,Action action,bool primary=false)
    {
        var dangerous=text.Contains("删除",StringComparison.Ordinal)||text.Contains("卸载",StringComparison.Ordinal)||text.Contains("作废",StringComparison.Ordinal);
        var kind=dangerous?KukaButtonKind.Danger:primary?KukaButtonKind.Primary:KukaButtonKind.Secondary;
        var b=KukaControls.Button(text,kind);b.Margin=new Thickness(0,0,8,8);b.VerticalContentAlignment=VerticalAlignment.Center;b.HorizontalContentAlignment=HorizontalAlignment.Center;b.Click+=(_,_)=>Guard(action);return b;
    }

    private void Toolbar(IEnumerable<(string Text,Action Action,bool Primary)> actions,bool search=false)
    {
        var row = new Grid { VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = search ? new GridLength(360) : new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if(search)
        {
            var searchWrap = new Border { Height = 44, CornerRadius = new CornerRadius(9), Background = Brushes.White, BorderBrush = Theme.Brush(Theme.BorderColor), BorderThickness = new Thickness(1), Padding = new Thickness(12,0,10,0), SnapsToDevicePixels = true };
            var searchGrid = new Grid(); searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); searchGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var ico = WindowChrome.FluentIcon("\uE721", 16, Theme.Brush(Theme.MutedColor)); searchGrid.Children.Add(ico);
            var box = new TextBox { VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), ToolTip = "搜索当前表格", FontSize = 14 };
            if(Application.Current.Resources["SearchTextBoxStyle"] is Style searchStyle)box.Style=searchStyle;
            box.TextChanged += (_,_) => FilterGrid(box.Text); Grid.SetColumn(box,1); searchGrid.Children.Add(box); searchWrap.Child=searchGrid; row.Children.Add(searchWrap);
        }
        var buttons=new WrapPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
        foreach(var a in actions)buttons.Children.Add(Btn(a.Text,a.Action,a.Primary));
        Grid.SetColumn(buttons,1); row.Children.Add(buttons); _body.Children.Add(SectionCard(row,new Thickness(20,16,12,8)));
    }

    private DataGrid Table(IReadOnlyList<string> headers,IEnumerable<object?[]> rows,IEnumerable<string>? ids=null,double height=390,Action<string>? onDouble=null)
    {
        var rowList = rows.ToList();
        var idList = ids?.ToList();
        var desired = 44 + Math.Max(1,rowList.Count) * 48 + 2;
        var actualHeight = Math.Min(height, Math.Max(142, desired));
        var grid=GridUtil.Build(headers,rowList,idList,actualHeight);
        grid.Height=actualHeight;grid.Margin=new Thickness(0);
        if(onDouble is not null)grid.MouseDoubleClick+=(_,_)=>{var id=GridUtil.SelectedId(grid);if(id is not null)Guard(()=>onDouble(id));};
        _table=grid;_body.Children.Add(SectionCard(grid,new Thickness(0)));return grid;
    }

    private string SelectedId()=>GridUtil.SelectedId(_table)??throw new InvalidOperationException("请先选择一条记录");
    private void FilterGrid(string text)
    {
        if(_table?.ItemsSource is not DataView view)return; text=text.Trim().Replace("'","''"); if(text.Length==0){view.RowFilter="";return;} var table=view.Table; if(table is null)return; var parts=table.Columns.Cast<DataColumn>().Where(c=>c.ColumnName!="_id").Select(c=>$"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{text}%'"); try{view.RowFilter=string.Join(" OR ",parts);}catch{view.RowFilter="";}
    }

    private IReadOnlyList<FormChoice> Refs(string table,string? currentId=null,bool includeNone=false)
    {
        if(table is not "students" and not "courses" and not "teachers")throw new InvalidOperationException("不支持的数据引用");var rows=currentId is null?_store.Rows($"SELECT * FROM {table} WHERE active=1 ORDER BY name"):_store.Rows($"SELECT * FROM {table} WHERE active=1 OR id=? ORDER BY active DESC,name",currentId);var list=new List<FormChoice>();if(includeNone)list.Add(new("暂且无",null));foreach(var r in rows){var suffix=B(r,"active")?"":table=="students"?"（已归档）":"（已停用）";list.Add(new(S(r,"name")+suffix,S(r,"id")));}return list;
    }

    private Dictionary<string,object?>? EditForm(string title,IReadOnlyList<FormField> fields,IDictionary<string,object?>? initial=null,string help="")
    {
        var d=new GenericFormWindow(this,title,fields,initial,help);return d.ShowDialog()==true?d.Result:null;
    }

    private static object? V(Dictionary<string,object?> row,string key,object? fallback=null)=>row.TryGetValue(key,out var v)?v:fallback;
    private static string Fen(long v)=>"¥"+Money(v);
}
