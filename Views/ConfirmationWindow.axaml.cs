using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;

namespace ShopHoa.Views;

public partial class ConfirmationWindow : Window
{
    public ConfirmationWindow() : this("Xác nhận", "Bạn có chắc chắn muốn tiếp tục không?")
    {
    }

    public ConfirmationWindow(string title, string message)
        : this(title, message, "Xác nhận", "Hủy")
    {
    }

    public ConfirmationWindow(string title, string message, string confirmButtonText)
        : this(title, message, confirmButtonText, null)
    {
    }

    public ConfirmationWindow(string title, string message, string confirmButtonText, string? cancelButtonText)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        MessageTextBlock.Text = message;
        ConfirmButton.Content = confirmButtonText;

        if (string.IsNullOrWhiteSpace(cancelButtonText) || string.Equals(confirmButtonText, cancelButtonText, StringComparison.OrdinalIgnoreCase))
        {
            CancelButton.IsVisible = false;
        }
        else
        {
            CancelButton.Content = cancelButtonText;
            CancelButton.IsVisible = true;
        }
    }

    private void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
