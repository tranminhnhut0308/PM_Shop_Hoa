using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class HoaDonChuaGhiNhanWindow : Window
{
    public HoaDonChuaGhiNhanWindow()
    {
        InitializeComponent();
    }

    public HoaDonChuaGhiNhanWindow(IReadOnlyList<TongQuanHoaDonBiLoaiTru> danhSach) : this()
    {
        TxtSoLuongBadge.Text = $"{danhSach.Count:N0} hóa đơn";
        GridHoaDon.ItemsSource = danhSach;
    }

    public static async Task ShowAsync(Window? owner, IReadOnlyList<TongQuanHoaDonBiLoaiTru> danhSach)
    {
        var window = new HoaDonChuaGhiNhanWindow(danhSach);
        if (owner != null)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            window.Show();
        }
    }

    private void DongButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
