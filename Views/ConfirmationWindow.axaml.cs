using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShopHoa.Views;

public partial class ConfirmationWindow : Window
{
    public ConfirmationWindow() : this("Xác nhận", "Bạn có chắc chắn muốn tiếp tục không?")
    {
    }

    public ConfirmationWindow(string title, string message)
    {
        InitializeComponent();
        Title = title;
        MessageTextBlock.Text = message;
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
