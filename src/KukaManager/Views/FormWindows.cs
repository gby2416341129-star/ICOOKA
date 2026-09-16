using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KukaManager.Data;
using KukaManager.Design;
using KukaManager.Models;
using KukaManager.UI.Components;
using KukaManager.UI.Layout;
using KukaManager.Utils;
using static KukaManager.Utils.Value;

namespace KukaManager.Views;

public sealed class GenericFormWindow : Window
{
    private readonly Dictionary<string, (FormField Field, FrameworkElement Control)> _controls = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> Result { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public GenericFormWindow(Window owner, string title, IReadOnlyList<FormField> fields, IDictionary<string, object?>? initial = null, string helpText = "")
    {
        Owner = owner; Title = title; Width = 680; MaxHeight = 720; MinHeight = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Brush(Theme.BackgroundColor); SizeToContent = SizeToContent.Height;
        var main = new DockPanel { Margin = new Thickness(24) };
        var foot = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "取消", MinWidth = 90, Margin = new Thickness(0, 0, 10, 0) }; cancel.Click += (_, _) => DialogResult = false;
        var save = WindowChrome.PrimaryButton("保存"); save.Click += Save; foot.Children.Add(cancel); foot.Children.Add(save); DockPanel.SetDock(foot, Dock.Bottom); main.Children.Add(foot);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var body = new StackPanel();
        body.Children.Add(WindowChrome.Text(title, 25, FontWeights.Bold));
        if (!string.IsNullOrWhiteSpace(helpText))
        {
            body.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(241, 247, 246)), BorderBrush = Theme.Brush(Theme.BorderColor), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(13), Margin = new Thickness(0, 10, 0, 18), Child = WindowChrome.Text(helpText, 13, null, Theme.Brush(Theme.MutedColor)) });
        }
        else body.Children.Add(new Border { Height = 18 });

        foreach (var f in fields)
        {
            var value = initial is not null && initial.TryGetValue(f.Key, out var iv) ? iv : f.DefaultValue;
            var c = BuildControl(f, value);
            body.Children.Add(WindowChrome.Field(f.Label, c));
            _controls[f.Key] = (f, c);
        }
        scroll.Content = WindowChrome.Card(body, new Thickness(24)); main.Children.Add(scroll); Content = KukaWindowFrame.Wrap(this,main,title);
    }

    private static FrameworkElement BuildControl(FormField f, object? value)
    {
        switch (f.Kind)
        {
            case FormFieldKind.LongText:
                return new TextBox { Text = Convert.ToString(value) ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 104, VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            case FormFieldKind.Choice:
            case FormFieldKind.Reference:
                var combo = new ComboBox { DisplayMemberPath = nameof(FormChoice.Text), SelectedValuePath = nameof(FormChoice.Value) };
                if (f.Choices is not null) foreach (var x in f.Choices) combo.Items.Add(x);
                if (value is not null)
                {
                    foreach (var item in combo.Items.Cast<FormChoice>()) if (EqualsAsString(item.Value, value)) { combo.SelectedItem = item; break; }
                }
                if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
                return combo;
            case FormFieldKind.Date:
                DateTime dt = DateTime.Today;
                if (value is DateTime d) dt = d; else DateTime.TryParse(Convert.ToString(value), out dt);
                return new DatePicker { SelectedDate = dt == default ? DateTime.Today : dt, SelectedDateFormat = DatePickerFormat.Long };
            case FormFieldKind.Check:
                return new CheckBox { IsChecked = value is bool b ? b : Convert.ToString(value) is "1" or "True" or "true", Content = "启用", VerticalContentAlignment = VerticalAlignment.Center };
            case FormFieldKind.Money:
                return new TextBox { Text = value is null ? "0.00" : Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("0.00", CultureInfo.InvariantCulture) };
            case FormFieldKind.Integer:
                return new TextBox { Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0" };
            default:
                return new TextBox { Text = Convert.ToString(value) ?? "" };
        }
    }

    private static bool EqualsAsString(object? a, object? b) => string.Equals(Convert.ToString(a, CultureInfo.InvariantCulture), Convert.ToString(b, CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private void Save(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _controls)
            {
                var (f, c) = kv.Value;
                object? value = c switch
                {
                    TextBox t when f.Kind == FormFieldKind.Integer => int.TryParse(t.Text.Trim(), out var n) ? n : throw new InvalidOperationException($"{f.Label} 必须是整数"),
                    TextBox t when f.Kind == FormFieldKind.Money => decimal.TryParse(t.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var m) || decimal.TryParse(t.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out m) ? m : throw new InvalidOperationException($"{f.Label} 金额格式不正确"),
                    TextBox t => t.Text.Trim(),
                    ComboBox cb => (cb.SelectedItem as FormChoice)?.Value,
                    DatePicker dp => dp.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
                    CheckBox ch => ch.IsChecked == true ? 1 : 0,
                    _ => null
                };
                if (f.Required && (value is null || string.IsNullOrWhiteSpace(Convert.ToString(value)))) throw new InvalidOperationException($"请填写 / 选择：{f.Label.Replace(" *", "")}");
                result[kv.Key] = value;
            }
            Result = result; DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "请检查", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}

public sealed class TextPromptWindow : Window
{
    private readonly TextBox _text;
    public string Value => _text.Text.Trim();
    public TextPromptWindow(Window owner, string title, string label, string initial = "", bool multiline = false)
    {
        Owner = owner; Title = title; Width = 520; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Brush(Theme.BackgroundColor);
        var p = new StackPanel(); p.Children.Add(WindowChrome.Text(title, 23, FontWeights.Bold));
        _text = new TextBox { Text = initial, AcceptsReturn = multiline, TextWrapping = TextWrapping.Wrap, Height = multiline ? 120 : double.NaN, Margin = new Thickness(0, 8, 0, 16) };
        p.Children.Add(WindowChrome.Text(label, 13, FontWeights.SemiBold, Theme.Brush(Theme.MutedColor))); p.Children.Add(_text);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; var c = new Button { Content = "取消", MinWidth = 90, Margin = new Thickness(0,0,10,0) }; c.Click += (_,_) => DialogResult=false; var ok=WindowChrome.PrimaryButton("确定"); ok.Click += (_,_)=>DialogResult=true; row.Children.Add(c);row.Children.Add(ok);p.Children.Add(row);
        var host=new Grid { Margin = new Thickness(24), Children = { WindowChrome.Card(p) } };
        Content = KukaWindowFrame.Wrap(this,host,title);
    }
}

public sealed class StudentHistoryWindow : Window
{
    public StudentHistoryWindow(Window owner, KukaStore store, string studentId)
    {
        Owner = owner; var s=store.Get("students",studentId); Title = S(s,"name")+" · 班级与缴费历史"; Width=1180;Height=650;MinWidth=900;MinHeight=520;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Theme.Brush(Theme.BackgroundColor);
        var panel=new DockPanel{Margin=new Thickness(24)};var top=new StackPanel{Margin=new Thickness(0,0,0,16)};top.Children.Add(WindowChrome.Text(S(s,"name")+" · 班级与缴费历史",25,FontWeights.SemiBold));top.Children.Add(WindowChrome.Text("缴费关联、换班和升年级都会保留历史阶段，不覆盖旧记录。",13,null,Theme.Brush(Theme.MutedColor)));DockPanel.SetDock(top,Dock.Top);panel.Children.Add(top);
        var rows=store.StudentAcademicHistory(studentId);var table=GridUtil.Build(["阶段开始","阶段结束","课程","班级","上课时间","老师","教室","来源","缴费日期","缴费金额","户头 / 摘要"],rows.Select(x=>new object?[]{S(x,"started_on"),string.IsNullOrWhiteSpace(S(x,"ended_on"))?"至今":S(x,"ended_on"),S(x,"course"),S(x,"class_name"),"星期"+"一二三四五六日"[I(x,"weekday")]+" "+S(x,"start_time")+"–"+S(x,"end_time"),S(x,"teacher"),S(x,"room"),S(x,"source"),S(x,"payment_date"),L(x,"income_fen")>0?"¥"+Money(L(x,"income_fen")):"—",(string.IsNullOrWhiteSpace(S(x,"account"))?"":S(x,"account")+" / ")+S(x,"payment_summary")}),null); panel.Children.Add(table);Content=KukaWindowFrame.Wrap(this,panel,Title,true);
    }
}

public static class GridUtil
{
    public static DataGrid Build(IReadOnlyList<string> headers, IEnumerable<object?[]> rows, IEnumerable<string>? ids, double minHeight = 260)
    {
        var rowList = rows.ToList();
        var table = new DataTable();
        if(ids is not null)table.Columns.Add("_id",typeof(string));
        foreach(var h in headers)table.Columns.Add(h,typeof(string));
        var idList=ids?.ToList();
        var ri=0;
        foreach(var row in rowList)
        {
            var dr=table.NewRow();var offset=0;
            if(idList is not null){dr[0]=ri<idList.Count?idList[ri]:"";offset=1;}
            for(var i=0;i<headers.Count;i++)dr[i+offset]=Convert.ToString(row.ElementAtOrDefault(i))??"";
            table.Rows.Add(dr);ri++;
        }
        if(rowList.Count==0)
        {
            var empty=table.NewRow();
            var offset=0;
            if(idList is not null){empty[0]="";offset=1;}
            empty[offset]="暂无数据";
            for(var i=1;i<headers.Count;i++)empty[i+offset]="—";
            table.Rows.Add(empty);
        }

        var grid=new DataGrid
        {
            ItemsSource=table.DefaultView,
            MinHeight=minHeight,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            UseLayoutRounding=true,
            SnapsToDevicePixels=true,
            EnableRowVirtualization=true,
            EnableColumnVirtualization=true
        };
        TextOptions.SetTextFormattingMode(grid,TextFormattingMode.Display);
        TextOptions.SetTextHintingMode(grid,TextHintingMode.Fixed);
        TextOptions.SetTextRenderingMode(grid,TextRenderingMode.ClearType);
        ScrollCoordinator.Enable(grid);

        grid.AutoGeneratingColumn+=(_,e)=>
        {
            if(e.PropertyName=="_id"){e.Column.Visibility=Visibility.Collapsed;return;}
            e.Column.Header=e.PropertyName;
            if(headers.Count<=5)
            {
                e.Column.MinWidth=110;
                e.Column.Width=new DataGridLength(1,DataGridLengthUnitType.Star);
            }
            else
            {
                var w=SuggestedWidth(e.PropertyName);
                e.Column.MinWidth=w;
                e.Column.Width=new DataGridLength(w,DataGridLengthUnitType.Pixel);
                e.Column.MaxWidth=Math.Max(w,420);
            }
            if(e.Column is DataGridTextColumn textColumn)
            {
                var style=new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.FontFamilyProperty,Theme.BodyFont));
                style.Setters.Add(new Setter(TextBlock.FontSizeProperty,13d));
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty,Theme.Brush(Theme.TextColor)));
                style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center));
                style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(16,0,16,0)));
                style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.NoWrap));
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));
                style.Setters.Add(new Setter(FrameworkElement.SnapsToDevicePixelsProperty,true));
                textColumn.ElementStyle=style;
            }
        };
        return grid;
    }

    private static double SuggestedWidth(string header)
    {
        if(header.Contains("摘要")||header.Contains("地址")||header.Contains("备注")||header.Contains("原因")||header.Contains("详情"))return 220;
        if(header.Contains("课程 / 班级")||header.Contains("班级 / 学员")||header.Contains("户头 / 摘要"))return 180;
        if(header.Contains("日期")||header.Contains("月份")||header.Contains("时间"))return 118;
        if(header.Contains("电话")||header.Contains("微信"))return 132;
        if(header.Contains("金额")||header.Contains("收入")||header.Contains("支出")||header.Contains("余额")||header.Contains("手续费")||header.Contains("净现金"))return 118;
        if(header.Contains("状态")||header.Contains("星期")||header.Contains("笔数")||header.Contains("课次")||header.Contains("年龄"))return 86;
        if(header.Contains("姓名")||header.Contains("老师")||header.Contains("学员")||header.Contains("课程")||header.Contains("班级")||header.Contains("户头"))return 128;
        return 112;
    }

    public static string? SelectedId(DataGrid? grid)
    {
        if(grid?.SelectedItem is not DataRowView r || !r.Row.Table.Columns.Contains("_id"))return null;
        var id=Convert.ToString(r["_id"]);
        return string.IsNullOrWhiteSpace(id)?null:id;
    }
}
