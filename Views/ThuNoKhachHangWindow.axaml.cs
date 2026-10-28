using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class ThuNoKhachHangWindow : Window
{
    private static readonly IReadOnlyList<PhuongThucOption> DanhSachPhuongThuc = new[]
    {
        new PhuongThucOption { Ma = "TIEN_MAT", Ten = "Tiền mặt" },
        new PhuongThucOption { Ma = "CHUYEN_KHOAN", Ten = "Chuyển khoản" },
        new PhuongThucOption { Ma = "THE", Ten = "Thẻ" },
        new PhuongThucOption { Ma = "VI_DIEN_TU", Ten = "Ví điện tử" }
    };

    private readonly decimal _conNo;
    private readonly uint? _nguoiThucHienIdMacDinh;

    public ThuNoKhachHangWindow() : this("Khách hàng", 0m, Array.Empty<SoQuyLuaChonItem>(), null)
    {
    }

    public ThuNoKhachHangWindow(
        string tenKhachHang,
        decimal conNo,
        IReadOnlyList<SoQuyLuaChonItem>? nguoiThuOptions = null,
        uint? nguoiThucHienIdMacDinh = null)
    {
        InitializeComponent();

        CboPhuongThuc.ItemsSource = DanhSachPhuongThuc;
        CboPhuongThuc.SelectedIndex = 0;
        CboNguoiThu.ItemsSource = nguoiThuOptions ?? Array.Empty<SoQuyLuaChonItem>();
        _nguoiThucHienIdMacDinh = nguoiThucHienIdMacDinh;
        CboNguoiThu.SelectedItem = (nguoiThuOptions ?? Array.Empty<SoQuyLuaChonItem>())
            .FirstOrDefault(option => uint.TryParse(option.GiaTri, out var id)
                && id == nguoiThucHienIdMacDinh);

        _conNo = conNo;

        TxtKhachHang.Text = string.IsNullOrWhiteSpace(tenKhachHang)
            ? "Khách lẻ"
            : tenKhachHang;
        TxtConNo.Text = $"{conNo:N0} đ";

        // Mặc định thu hết phần còn nợ cho tiện.
        TienThuBox.Value = conNo;
        TienThuBox.ValueChanged += (_, _) => CapNhatConNoSauThu();
        NgayThanhToanPicker.SelectedDate = DateTimeOffset.Now.Date;
        GioThanhToanBox.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        CapNhatConNoSauThu();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var soTien = TienThuBox.Value;

        if (soTien <= 0)
        {
            HienLoi("Số tiền thu phải lớn hơn 0.");
            return;
        }

        if (soTien > _conNo)
        {
            HienLoi($"Số tiền thu ({soTien.ToString("N0", CultureInfo.InvariantCulture)} đ) "
                + $"vượt quá công nợ còn lại ({_conNo.ToString("N0", CultureInfo.InvariantCulture)} đ).");
            return;
        }

        if (NgayThanhToanPicker.SelectedDate is not { } ngay ||
            !TimeSpan.TryParseExact(
                GioThanhToanBox.Text?.Trim(),
                @"hh\:mm",
                CultureInfo.InvariantCulture,
                out var gio))
        {
            HienLoi("Vui lòng chọn ngày và nhập giờ thanh toán hợp lệ theo định dạng HH:mm.");
            return;
        }

        var nguoiThucHienId = CboNguoiThu.SelectedItem is SoQuyLuaChonItem nguoiThu
            && uint.TryParse(nguoiThu.GiaTri, out var selectedId)
                ? selectedId
                : _nguoiThucHienIdMacDinh;

        var ketQua = new ThuNoKhachHangYeuCau
        {
            SoTien = soTien,
            PhuongThuc = (CboPhuongThuc.SelectedItem as PhuongThucOption)?.Ma ?? "TIEN_MAT",
            GhiChu = string.IsNullOrWhiteSpace(TxtGhiChu.Text) ? null : TxtGhiChu.Text.Trim(),
            ThoiGian = ngay.Date.Add(gio),
            NguoiThucHienId = nguoiThucHienId
        };

        Close(ketQua);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void CapNhatConNoSauThu()
    {
        var conNoSauThu = _conNo - TienThuBox.Value;
        TxtConNoSauThu.Text = $"{conNoSauThu:N0} đ";
        TxtConNoSauThu.Foreground = conNoSauThu < 0
            ? Avalonia.Media.Brush.Parse("#D64545")
            : Avalonia.Media.Brush.Parse("#087F6E");
    }

    private void HienLoi(string thongDiep)
    {
        TxtLoi.Text = thongDiep;
        TxtLoi.IsVisible = true;
    }
}
