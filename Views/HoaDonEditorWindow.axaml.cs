using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

/// <summary>
/// Cửa sổ chỉnh sửa trực tiếp hóa đơn cũ đã bị khóa theo quy định nghiệp vụ (Issues #34, #46).
/// Khi phát hiện hóa đơn sai, người dùng phải Hủy hóa đơn cũ và tạo lại hóa đơn mới.
/// </summary>
public partial class HoaDonEditorWindow : Window
{
    private const string ThongBaoKhoaChinhSua =
        "Chức năng chỉnh sửa trực tiếp hóa đơn đã bị khóa theo quy định nghiệp vụ. " +
        "Khi phát hiện hóa đơn sai, vui lòng sử dụng chức năng 'Hủy hóa đơn' và chọn 'Hủy và tạo hóa đơn tương tự'.";

    public HoaDonBanHangReceipt? ResultReceipt { get; private set; }

    public HoaDonEditorWindow() : this("")
    {
    }

    public HoaDonEditorWindow(string maHoaDon)
    {
        InitializeComponent();
        TxtMaHoaDon.Text = $"#{maHoaDon}";
        BtnLuu.IsEnabled = false;
        BtnLuu.IsVisible = false;
        TxtError.Text = ThongBaoKhoaChinhSua;
        TxtError.IsVisible = true;
    }

    private void BtnThemSanPham_OnClick(object? sender, RoutedEventArgs e)
    {
    }

    private void BtnXoaItem_OnClick(object? sender, RoutedEventArgs e)
    {
    }

    private void NumGiamGia_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
    }

    private void BtnXemLichSu_OnClick(object? sender, RoutedEventArgs e)
    {
    }

    private void BtnHuy_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void BtnLuu_OnClick(object? sender, RoutedEventArgs e)
    {
        TxtError.Text = ThongBaoKhoaChinhSua;
        TxtError.IsVisible = true;
        BtnLuu.IsEnabled = false;
    }
}
