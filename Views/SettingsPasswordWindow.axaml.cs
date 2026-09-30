using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Services;

namespace ShopHoa.Views;

/// <summary>
/// Cổng xác thực trước khi mở cấu hình database.
///
/// Không có mật khẩu nào gắn cứng trong source: người dùng phải
/// nhập một tài khoản thật, xác thực y hệt màn đăng nhập
/// (BCrypt + database), tài khoản phải còn hoạt động.
/// </summary>
public partial class SettingsPasswordWindow : Window
{
    private readonly DatabaseService _db = new();

    private bool _dangXuLy;

    public SettingsPasswordWindow(Window? owner = null)
    {
        InitializeComponent();

        if (owner != null)
        {
            Owner = owner;
        }

        UsernameTextBox.Focus();
    }

    // ============================================================
    // XÁC NHẬN
    // ============================================================

    private async void OnConfirmClick(
        object? sender,
        RoutedEventArgs e)
    {
        await CheckCredentialsAsync();
    }

    // ============================================================
    // ENTER ĐỂ XÁC NHẬN
    // ============================================================

    private async void OnCredentialKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;

            await CheckCredentialsAsync();
        }
    }

    // ============================================================
    // KIỂM TRA TÀI KHOẢN
    // ============================================================

    private async Task CheckCredentialsAsync()
    {
        if (_dangXuLy)
            return;

        var username = UsernameTextBox.Text?.Trim() ?? "";

        var password = PasswordTextBox.Text ?? "";

        if (string.IsNullOrWhiteSpace(username))
        {
            ErrorText.Text = "⚠ Vui lòng nhập tên đăng nhập.";

            UsernameTextBox.Focus();

            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ErrorText.Text = "⚠ Vui lòng nhập mật khẩu.";

            PasswordTextBox.Focus();

            return;
        }

        _dangXuLy = true;

        ConfirmButton.IsEnabled = false;

        ErrorText.Text = "Đang xác thực...";

        try
        {
            var user = await _db.DangNhapAsync(username, password);

            if (user is null)
            {
                ErrorText.Text = "❌ Tên đăng nhập hoặc mật khẩu không đúng.";

                PasswordTextBox.SelectAll();

                PasswordTextBox.Focus();

                return;
            }

            // ĐÚNG TÀI KHOẢN
            Close(true);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("DATABASE SETTINGS AUTH ERROR", ex);

            ErrorText.Text =
                "❌ Không thể kết nối database: " +
                SafeLog.Redact(ex.Message);
        }
        finally
        {
            _dangXuLy = false;

            ConfirmButton.IsEnabled = true;
        }
    }

    // ============================================================
    // HỦY
    // ============================================================

    private void OnCancelClick(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }
}
