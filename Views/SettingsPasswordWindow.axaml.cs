using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ShopHoa.Views;

public partial class SettingsPasswordWindow : Window
{
    // ============================================================
    // MẬT KHẨU BẢO VỆ CÀI ĐẶT
    // ============================================================

    private const string SettingsPassword = "123456";


    public SettingsPasswordWindow(Window? owner = null)
    {
        InitializeComponent();

        if (owner != null)
        {
            Owner = owner;
        }

        PasswordTextBox.Focus();
    }


    // ============================================================
    // XÁC NHẬN
    // ============================================================

    private void OnConfirmClick(
        object? sender,
        RoutedEventArgs e)
    {
        CheckPassword();
    }


    // ============================================================
    // ENTER ĐỂ XÁC NHẬN
    // ============================================================

    private void OnPasswordKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CheckPassword();
        }
    }


    // ============================================================
    // KIỂM TRA MẬT KHẨU
    // ============================================================

    private void CheckPassword()
    {
        var password =
            PasswordTextBox.Text?.Trim() ?? string.Empty;


        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorText.Text =
                "⚠ Vui lòng nhập mật khẩu.";

            PasswordTextBox.Focus();

            return;
        }


        if (password != SettingsPassword)
        {
            ErrorText.Text =
                "❌ Mật khẩu không đúng.";

            PasswordTextBox.SelectAll();

            PasswordTextBox.Focus();

            return;
        }


        // ĐÚNG MẬT KHẨU
        Close(true);
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