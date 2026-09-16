using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using KukaManager.Data;
using KukaManager.Design;
using KukaManager.UI.Components;
using KukaManager.UI.Controls;

namespace KukaManager.Views;

internal static class WindowChrome
{
    public static Border Card(UIElement child, Thickness? padding = null)
        => KukaControls.Card(child, padding);

    public static TextBlock Text(string text, double size = 14, FontWeight? weight = null, Brush? color = null)
    {
        return KukaControls.Text(text, size, weight, color);
    }

    public static TextBlock FluentIcon(string glyph, double size = 18, Brush? color = null)
    {
        return KukaControls.Icon(glyph, size, color);
    }

    public static Button PrimaryButton(string text)
    {
        return KukaControls.Button(text, KukaButtonKind.Primary);
    }

    public static Border Pill(string text, Brush? background = null, Brush? foreground = null)
        => new()
        {
            CornerRadius = new CornerRadius(999),
            Background = background ?? new SolidColorBrush(Color.FromRgb(238, 246, 255)),
            Padding = new Thickness(10, 5, 10, 5),
            UseLayoutRounding = true,
            SnapsToDevicePixels = true,
            Child = Text(text, 12, FontWeights.SemiBold, foreground ?? Theme.Brush(Theme.PrimaryColor))
        };

    public static FrameworkElement Field(string label, FrameworkElement control)
    {
        return KukaControls.Field(label, control);
    }
}


public sealed class SetupWindow : Window
{
    private readonly KukaStore _store;
    private readonly TextBox _name = new();
    private readonly TextBox _user = new() { Text = "admin" };
    private readonly PasswordBox _pass = new() { MinHeight = 42, Padding = new Thickness(11, 8, 11, 8) };
    private readonly PasswordBox _pass2 = new() { MinHeight = 42, Padding = new Thickness(11, 8, 11, 8) };
    public SetupWindow(KukaStore store)
    {
        _store = store; Title = "酷咔管理系统 · 首次初始化"; Width = 560; Height = 720; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Theme.Brush(Theme.BackgroundColor);
        var panel = new StackPanel();
        panel.Children.Add(KukaWindowFrame.BrandMark(56));
        panel.Children.Add(new Border { Height = 18 });
        panel.Children.Add(WindowChrome.Text("酷咔管理系统", 30, FontWeights.Bold));
        panel.Children.Add(WindowChrome.Text("首次运行，请创建本机管理员。账号和数据只保存在当前电脑。", 14, null, Theme.Brush(Theme.MutedColor)));
        panel.Children.Add(new Border { Height = 24 });
        panel.Children.Add(WindowChrome.Field("管理员姓名", _name));
        panel.Children.Add(WindowChrome.Field("登录账号", _user));
        panel.Children.Add(WindowChrome.Field("密码（至少 8 位）", _pass));
        panel.Children.Add(WindowChrome.Field("再次输入密码", _pass2));
        var save = WindowChrome.PrimaryButton("创建并进入"); save.Margin = new Thickness(0, 10, 0, 0); save.Click += Save;
        panel.Children.Add(save);
        var host = new Grid { Margin = new Thickness(34, 26, 34, 34), Children = { WindowChrome.Card(panel, new Thickness(32)) } };
        Content = KukaWindowFrame.Wrap(this, host, "首次初始化");
    }
    private void Save(object? s, RoutedEventArgs e)
    {
        try
        {
            if (_pass.Password != _pass2.Password) throw new InvalidOperationException("两次密码不一致");
            _store.SetupAdmin(_name.Text, _user.Text, _pass.Password); DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "请检查", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}

public sealed class LoginWindow : Window
{
    private readonly KukaStore _store;
    private readonly TextBox _user = new();
    private readonly PasswordBox _pass = new() { MinHeight = 42, Padding = new Thickness(11, 8, 11, 8) };
    public LoginWindow(KukaStore store)
    {
        _store = store; _user.Text = store.Setting("admin_username"); Title = "酷咔管理系统 · 登录"; Width = 520; Height = 590; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Theme.Brush(Theme.BackgroundColor);
        var p = new StackPanel();
        p.Children.Add(KukaWindowFrame.BrandMark(58));
        p.Children.Add(new Border { Height = 18 });
        var heading = WindowChrome.Text("欢迎回来", 30, FontWeights.Bold); heading.HorizontalAlignment = HorizontalAlignment.Center; p.Children.Add(heading);
        var subtitle = WindowChrome.Text("登录酷咔企业运营中心", 14, null, Theme.Brush(Theme.MutedColor)); subtitle.HorizontalAlignment = HorizontalAlignment.Center; p.Children.Add(subtitle);
        p.Children.Add(new Border { Height = 26 }); p.Children.Add(WindowChrome.Field("账号", _user)); p.Children.Add(WindowChrome.Field("密码", _pass));
        var b = WindowChrome.PrimaryButton("登录"); b.Height = 44; b.Margin = new Thickness(0, 8, 0, 0); b.Click += Login; p.Children.Add(b);
        var offline = WindowChrome.Text("●  本机离线 · 数据仅保存在此电脑", 11, FontWeights.SemiBold, Theme.Brush(Theme.SuccessColor)); offline.HorizontalAlignment = HorizontalAlignment.Center; offline.Margin = new Thickness(0, 18, 0, 0); p.Children.Add(offline);
        _pass.KeyDown += (_, ev) => { if (ev.Key == System.Windows.Input.Key.Enter) Login(null, new RoutedEventArgs()); };
        var host = new Grid { Margin = new Thickness(38, 26, 38, 38), Children = { WindowChrome.Card(p, new Thickness(34)) } };
        Content = KukaWindowFrame.Wrap(this, host, "登录");
        Loaded += (_, _) => { _pass.Focus(); };
    }
    private void Login(object? s, RoutedEventArgs e)
    {
        if (_store.VerifyLogin(_user.Text, _pass.Password)) { _store.Actor = _store.Setting("admin_name"); DialogResult = true; return; }
        MessageBox.Show(this, "账号或密码不正确", "登录失败", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

public sealed class AccountWindow : Window
{
    private readonly KukaStore _store;
    private readonly TextBox _name = new();
    private readonly TextBox _user = new();
    private readonly PasswordBox _current = new() { MinHeight = 42, Padding = new Thickness(11, 8, 11, 8) };
    private readonly PasswordBox _newPassword = new() { MinHeight = 42, Padding = new Thickness(11, 8, 11, 8) };
    private readonly PasswordBox _confirm = new() { MinHeight = 42, Padding = new Thickness(11, 8, 11, 8) };

    public AccountWindow(Window owner, KukaStore store)
    {
        _store = store;
        Owner = owner;
        Title = "修改管理员账户";
        Width = 560;
        Height = 730;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Theme.Brush(Theme.BackgroundColor);
        _name.Text = store.Setting("admin_name");
        _user.Text = store.Setting("admin_username");

        var panel = new StackPanel();
        panel.Children.Add(WindowChrome.Text("管理员账户", 27, FontWeights.Bold));
        panel.Children.Add(WindowChrome.Text("修改姓名或账号必须验证当前密码；新密码留空表示保持原密码。", 13, null, Theme.Brush(Theme.MutedColor)));
        panel.Children.Add(new Border { Height = 20 });
        panel.Children.Add(WindowChrome.Field("管理员姓名", _name));
        panel.Children.Add(WindowChrome.Field("登录账号", _user));
        panel.Children.Add(WindowChrome.Field("当前密码", _current));
        panel.Children.Add(WindowChrome.Field("新密码（不修改可留空）", _newPassword));
        panel.Children.Add(WindowChrome.Field("确认新密码", _confirm));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var cancel = new Button { Content = "取消", MinWidth = 90, Margin = new Thickness(0, 0, 10, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = WindowChrome.PrimaryButton("保存");
        save.Click += Save;
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        var host = new Grid { Margin = new Thickness(28), Children = { WindowChrome.Card(panel) } };
        Content = KukaWindowFrame.Wrap(this, host, "管理员账户");
    }

    private void Save(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_newPassword.Password != _confirm.Password) throw new InvalidOperationException("两次新密码不一致");
            _store.ChangeAdmin(_name.Text, _user.Text, _current.Password, _newPassword.Password);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "请检查", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
