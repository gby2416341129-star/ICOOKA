using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using KukaManager.UI.Controls;
using KukaManager.Utils;
using AppTheme = KukaManager.Design.Theme;

namespace KukaManager.UI.Components;

public static class KukaWindowFrame
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    public static Grid Wrap(Window window, UIElement content, string title, bool canMaximize = false, TextBlock? livePageTitle = null)
    {
        Configure(window, canMaximize);

        var root = new Grid { Background = AppTheme.Brush(AppTheme.BackgroundColor) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = Header(window, title, canMaximize, livePageTitle);
        root.Children.Add(header);
        Grid.SetRow(content, 1);
        root.Children.Add(content);
        return root;
    }

    public static Border Header(Window window, string title, bool canMaximize, TextBlock? livePageTitle = null)
    {
        var header = new Border
        {
            Height = 52,
            Background = AppTheme.Brush(AppTheme.SurfaceColor),
            BorderBrush = AppTheme.Brush(AppTheme.SoftBorderColor),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dragArea = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Arrow };
        dragArea.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2 && canMaximize) ToggleMaximize(window);
            else if (e.LeftButton == MouseButtonState.Pressed) window.DragMove();
        };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
        titleRow.Children.Add(BrandMark(28));
        var appName = KukaControls.Text(title, 13, FontWeights.SemiBold);
        appName.Margin = new Thickness(10, 0, 0, 0);
        titleRow.Children.Add(appName);
        if (livePageTitle is not null)
        {
            var separator = KukaControls.Text("/", 12, null, AppTheme.Brush(AppTheme.MutedColor));
            separator.Margin = new Thickness(10, 0, 10, 0);
            titleRow.Children.Add(separator);
            titleRow.Children.Add(livePageTitle);
        }
        var version = KukaControls.Text("v" + AppPaths.Version, 11, null, AppTheme.Brush(AppTheme.MutedColor));
        version.Margin = new Thickness(12, 1, 0, 0);
        titleRow.Children.Add(version);
        dragArea.Children.Add(titleRow);
        grid.Children.Add(dragArea);

        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        var minimize = CaptionButton("\uE921", "最小化");
        minimize.Click += (_, _) => window.WindowState = WindowState.Minimized;
        controls.Children.Add(minimize);
        if (canMaximize)
        {
            var maximize = CaptionButton("\uE922", "最大化 / 还原");
            maximize.Click += (_, _) => ToggleMaximize(window);
            window.StateChanged += (_, _) => maximize.Content = KukaControls.Icon(window.WindowState == WindowState.Maximized ? "\uE923" : "\uE922", 11, AppTheme.Brush(AppTheme.TextColor));
            controls.Children.Add(maximize);
        }
        var close = CaptionButton("\uE8BB", "关闭", true);
        close.Click += (_, _) => window.Close();
        controls.Children.Add(close);
        Grid.SetColumn(controls, 1);
        grid.Children.Add(controls);
        header.Child = grid;
        return header;
    }

    public static Border BrandMark(double size = 32)
    {
        var letter = KukaControls.Text("K", Math.Round(size * .48), FontWeights.SemiBold, Brushes.White);
        letter.HorizontalAlignment = HorizontalAlignment.Center;
        letter.TextAlignment = TextAlignment.Center;
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(Math.Max(8, size * .28)),
            Background = AppTheme.Brush(AppTheme.BrandOrangeColor),
            Child = letter,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static void Configure(Window window, bool canMaximize)
    {
        window.WindowStyle = WindowStyle.None;
        window.AllowsTransparency = false;
        window.ResizeMode = canMaximize ? ResizeMode.CanResize : ResizeMode.NoResize;
        window.UseLayoutRounding = true;
        window.SnapsToDevicePixels = true;
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = canMaximize ? new Thickness(7) : new Thickness(0),
            CornerRadius = new CornerRadius(12),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        });
        window.SourceInitialized += (_, _) =>
        {
            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                var corner = 2;
                var light = 0;
                _ = DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref corner, sizeof(int));
                _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref light, sizeof(int));
            }
            catch { }
        };
    }

    private static Button CaptionButton(string glyph, string toolTip, bool close = false)
    {
        var button = KukaControls.Button(string.Empty, KukaButtonKind.Icon);
        button.Width = 48;
        button.Height = 42;
        button.MinWidth = 48;
        button.Margin = new Thickness(0, 5, 2, 5);
        button.ToolTip = toolTip;
        button.Content = KukaControls.Icon(glyph, 11, AppTheme.Brush(AppTheme.TextColor));
        if (close) button.Tag = "close";
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        return button;
    }

    private static void ToggleMaximize(Window window) =>
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
