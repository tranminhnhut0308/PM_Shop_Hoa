using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class HuyThanhToanCongNoWindow : Window
{
    private readonly ThanhToanCongNoKhachHang _record;
    private readonly string _customerName;

    public HuyThanhToanCongNoWindow()
    {
        InitializeComponent();
        _record = new ThanhToanCongNoKhachHang();
        _customerName = "";
    }

    public HuyThanhToanCongNoWindow(ThanhToanCongNoKhachHang record, string customerName) : this()
    {
        _record = record ?? throw new ArgumentNullException(nameof(record));
        _customerName = customerName ?? "";
        NapDuLieu();
    }

    private void NapDuLieu()
    {
        TxtMaPhieuBadge.Text = $"#{_record.MaGiaoDich}";
        TxtMaPhieu.Text = _record.MaGiaoDich;
        TxtSoTien.Text = _record.SoTienText;
        TxtKhachHang.Text = string.IsNullOrWhiteSpace(_customerName) ? "Khách hàng" : _customerName;
        TxtThoiGian.Text = _record.ThoiGianText;
        TxtPhuongThuc.Text = _record.PhuongThucHienThi;
        TxtMaThamChieu.Text = _record.MaThamChieuHienThi;
    }

    private void OnBoQuaClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnXacNhanHuyClick(object? sender, RoutedEventArgs e)
    {
        var lyDo = (TxtLyDoHuy.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            // Lý do mặc định khi người dùng không nhập
            lyDo = "Hủy thanh toán công nợ do thao tác sai của nhân viên!!";
        }

        TxtLoiValidate.IsVisible = false;
        Close(lyDo);
    }
}
