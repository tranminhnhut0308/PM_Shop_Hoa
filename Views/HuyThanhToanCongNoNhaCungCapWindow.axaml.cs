using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class HuyThanhToanCongNoNhaCungCapWindow : Window
{
    private readonly ThanhToanCongNoNhaCungCap _record;

    public HuyThanhToanCongNoNhaCungCapWindow()
    {
        InitializeComponent();
        _record = new ThanhToanCongNoNhaCungCap();
    }

    public HuyThanhToanCongNoNhaCungCapWindow(ThanhToanCongNoNhaCungCap record) : this()
    {
        _record = record ?? throw new ArgumentNullException(nameof(record));
        NapDuLieu();
    }

    private void NapDuLieu()
    {
        TxtMaPhieuBadge.Text = $"#{_record.MaGiaoDich}";
        TxtMaGiaoDich.Text = _record.MaGiaoDich;
        TxtSoTien.Text = _record.SoTienText;
        TxtNhaCungCap.Text = string.IsNullOrWhiteSpace(_record.MaNhaCungCap)
            ? _record.TenNhaCungCap
            : $"{_record.MaNhaCungCap} - {_record.TenNhaCungCap}";
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
            TxtLoiValidate.Text = "Vui lòng nhập lý do hủy giao dịch thanh toán.";
            TxtLoiValidate.IsVisible = true;
            TxtLyDoHuy.Focus();
            return;
        }

        TxtLoiValidate.IsVisible = false;
        Close(lyDo);
    }
}
