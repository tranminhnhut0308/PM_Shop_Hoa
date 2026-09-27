using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShopHoa.Views;

public partial class ThanhToanConfirmWindow : Window
{
    public ThanhToanConfirmWindow()
    {
        InitializeComponent();
    }

    public ThanhToanConfirmWindow(string tenKhach, decimal tongTien, decimal khachDua, decimal tienThoi, decimal congNo) : this()
    {
        TxtKhachHang.Text = tenKhach;
        TxtTongTien.Text = $"{tongTien:N0} đ";
        TxtTienKhachDua.Text = $"{khachDua:N0} đ";
        TxtTienThoi.Text = $"{tienThoi:N0} đ";
        TxtCongNo.Text = $"{congNo:N0} đ";
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}