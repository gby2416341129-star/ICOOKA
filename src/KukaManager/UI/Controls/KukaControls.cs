using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using KukaManager.Design;
using KukaManager.Models;

namespace KukaManager.UI.Controls;

public enum KukaButtonKind { Primary, Secondary, Danger, Icon }

internal sealed class RoundedClipBorder : Border
{
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        var radius = Math.Max(CornerRadius.TopLeft, CornerRadius.BottomRight);
        Clip = new RectangleGeometry(new Rect(RenderSize), radius, radius);
    }
}

public static class KukaControls
{
    public static Button Button(string text, KukaButtonKind kind = KukaButtonKind.Secondary)
    {
        var button = new Button { Content = text, MinWidth = kind == KukaButtonKind.Icon ? 40 : 88, Height = 42 };
        var key = kind switch
        {
            KukaButtonKind.Primary => "PrimaryButtonStyle",
            KukaButtonKind.Danger => "DangerButtonStyle",
            KukaButtonKind.Icon => "IconButtonStyle",
            _ => "SecondaryButtonStyle"
        };
        if (Application.Current?.Resources[key] is Style style) button.Style = style;
        return button;
    }

    public static TextBlock Text(string text, double size = 14, FontWeight? weight = null, Brush? color = null)
    {
        var block = new TextBlock
        {
            FontFamily = size >= 20 ? Design.Theme.DisplayFont : Design.Theme.BodyFont,
            Text = text,
            FontSize = Math.Round(size),
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = color ?? Design.Theme.Brush(Design.Theme.TextColor),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        TextOptions.SetTextFormattingMode(block, TextFormattingMode.Display);
        TextOptions.SetTextHintingMode(block, TextHintingMode.Fixed);
        TextOptions.SetTextRenderingMode(block, TextRenderingMode.ClearType);
        return block;
    }

    public static TextBlock Icon(string glyph, double size = 18, Brush? color = null) => new()
    {
        Text = glyph,
        FontFamily = Design.Theme.IconFont,
        FontSize = Math.Round(size),
        Foreground = color ?? Design.Theme.Brush(Design.Theme.MutedColor),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        SnapsToDevicePixels = true,
        UseLayoutRounding = true
    };

    public static Border Card(UIElement child, Thickness? padding = null, double radius = 14) => new RoundedClipBorder
    {
        Background = Design.Theme.Brush(Design.Theme.SurfaceColor),
        BorderBrush = Design.Theme.Brush(Design.Theme.SoftBorderColor),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        Padding = padding ?? new Thickness(24),
        UseLayoutRounding = true,
        SnapsToDevicePixels = true,
        Child = child
    };

    public static FrameworkElement Field(string label, FrameworkElement control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        var caption = Text(label, 13, FontWeights.SemiBold, Design.Theme.Brush(Design.Theme.MutedColor));
        caption.Margin = new Thickness(0, 0, 0, 0);
        panel.Children.Add(caption);
        control.Margin = new Thickness(0, 8, 0, 0);
        control.VerticalAlignment = VerticalAlignment.Center;
        panel.Children.Add(control);
        return panel;
    }

    public static ComboBox ChoiceBox(IEnumerable<FormChoice> choices, object? selectedValue = null)
    {
        var box = new ComboBox
        {
            DisplayMemberPath = nameof(FormChoice.Text),
            SelectedValuePath = nameof(FormChoice.Value)
        };
        foreach (var choice in choices) box.Items.Add(choice);
        box.SelectedValue = selectedValue;
        if (box.SelectedIndex < 0 && box.Items.Count > 0) box.SelectedIndex = 0;
        return box;
    }
}
