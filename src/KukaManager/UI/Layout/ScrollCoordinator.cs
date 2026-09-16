using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KukaManager.UI.Layout;

public static class ScrollCoordinator
{
    public static void Enable(DataGrid grid)
    {
        grid.PreviewMouseWheel += OnGridMouseWheel;
        grid.Loaded += (_, _) =>
        {
            if (FindChild<ScrollViewer>(grid) is { } viewer)
                viewer.PanningMode = PanningMode.Both;
        };
    }

    private static void OnGridMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid grid || FindChild<ScrollViewer>(grid) is not { } viewer) return;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta);
            e.Handled = true;
            return;
        }

        var atTop = viewer.VerticalOffset <= 0;
        var atBottom = viewer.VerticalOffset >= viewer.ScrollableHeight;
        if ((e.Delta > 0 && atTop) || (e.Delta < 0 && atBottom))
        {
            e.Handled = true;
            var parent = FindParent<ScrollViewer>(grid);
            parent?.ScrollToVerticalOffset(parent.VerticalOffset - e.Delta);
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T found) return found;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
