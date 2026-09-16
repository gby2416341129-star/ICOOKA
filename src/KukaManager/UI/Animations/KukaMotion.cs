using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KukaManager.UI.Animations;

public static class KukaMotion
{
    private static readonly Duration PageDuration = new(TimeSpan.FromMilliseconds(165));

    public static void Reveal(FrameworkElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 0;
        var transform = new TranslateTransform(0, 6);
        element.RenderTransform = transform;
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, PageDuration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(6, 0, PageDuration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }
}
