using System;
using System.Threading.Tasks;
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

    /// <summary>
    /// Khi cửa sổ vừa mở: đọc trạng thái "Ghi nhớ đăng nhập".
    /// Nếu có credential hợp lệ thì khôi phục phiên và vào thẳng MainWindow.
    /// </summary>
    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is not LoginViewModel viewModel)
            return;

        await viewModel.PhucHoiPhienDaGhiNhoAsync(this);
    }

    private async void OnDatabaseSettingsClick(
        object? sender,
        RoutedEventArgs e)
    {
        // Mở cửa sổ xác thực tài khoản thật trong database
        var passwordWindow = new SettingsPasswordWindow(this);

        var result = await passwordWindow.ShowDialog<bool>(this);

        // Sai tài khoản/mật khẩu hoặc hủy
        if (!result)
            return;

        // Xác thực đúng → mở cài đặt Database
        var settingsWindow = new DatabaseSettingsWindow(this);

        await settingsWindow.ShowDialog(this);
    }
}
