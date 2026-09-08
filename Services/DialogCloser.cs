using System.Windows;

namespace StorageClient.Redesign.Services;

public static class DialogCloser
{
    public static readonly DependencyProperty DialogResultProperty =
        DependencyProperty.RegisterAttached(
            "DialogResult",
            typeof(bool?),
            typeof(DialogCloser),
            new PropertyMetadata(null, OnDialogResultChanged));

    public static void SetDialogResult(DependencyObject target, bool? value) =>
        target.SetValue(DialogResultProperty, value);

    public static bool? GetDialogResult(DependencyObject target) =>
        (bool?)target.GetValue(DialogResultProperty);

    private static void OnDialogResultChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is Window window && window.IsVisible)
        {
            window.DialogResult = eventArgs.NewValue as bool?;
        }
    }
}
