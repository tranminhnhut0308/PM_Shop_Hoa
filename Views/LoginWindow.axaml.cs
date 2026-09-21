using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        DataContext = new LoginViewModel();
    }

    private async void OnDatabaseSettingsClick(
        object? sender,
        RoutedEventArgs e)
    {
        // Mở cửa sổ nhập mật khẩu bảo vệ
        var passwordWindow = new SettingsPasswordWindow(this);

        var result = await passwordWindow.ShowDialog<bool>(this);

        // Sai hoặc hủy
        if (!result)
            return;

        // Đúng mật khẩu → mở cài đặt Database
        var settingsWindow = new DatabaseSettingsWindow(this);

        await settingsWindow.ShowDialog(this);
    }
}