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
    private readonly decimal _soDuTraTruoc;
    private readonly uint? _nguoiThucHienIdMacDinh;

    public ThuNoKhachHangWindow() : this("", "Khách hàng", 0m, 0m, Array.Empty<SoQuyLuaChonItem>(), null)
    {
    }

    public ThuNoKhachHangWindow(
        string maKhachHang,
        string tenKhachHang,
        decimal conNo,
        IReadOnlyList<SoQuyLuaChonItem>? nguoiThuOptions = null,
        uint? nguoiThucHienIdMacDinh = null)
        : this(maKhachHang, tenKhachHang, conNo, 0m, nguoiThuOptions, nguoiThucHienIdMacDinh)
    {
    }

    public ThuNoKhachHangWindow(
        string maKhachHang,
        string tenKhachHang,
        decimal conNo,
        decimal soDuTraTruoc,
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
        _soDuTraTruoc = soDuTraTruoc;

        TxtMaKhachHang.Text = string.IsNullOrWhiteSpace(maKhachHang)
            ? "—"
            : maKhachHang;
        TxtKhachHang.Text = string.IsNullOrWhiteSpace(tenKhachHang)
            ? "Khách lẻ"
            : tenKhachHang;
        TxtConNo.Text = $"{conNo:N0} đ";
        TxtSoDuTraTruocHienTai.Text = $"{soDuTraTruoc:N0} đ";

        // Mặc định thu hết phần còn nợ cho tiện (nếu còn nợ > 0).
        TienThuBox.Value = Math.Max(0m, conNo);
        TienThuBox.ValueChanged += (_, _) => CapNhatConNoSauThu();
        NgayThanhToanPicker.SelectedDate = DateTimeOffset.Now.Date;
        GioThanhToanBox.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        CapNhatConNoSauThu();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;

        try
        {
            var soTien = TienThuBox.Value;

            if (soTien <= 0)
            {
                HienLoi("Số tiền thanh toán phải lớn hơn 0.");
                if (btn != null) btn.IsEnabled = true;
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
                if (btn != null) btn.IsEnabled = true;
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
                MaThamChieu = string.IsNullOrWhiteSpace(TxtMaThamChieu.Text) ? null : TxtMaThamChieu.Text.Trim(),
                GhiChu = string.IsNullOrWhiteSpace(TxtGhiChu.Text) ? null : TxtGhiChu.Text.Trim(),
                ThoiGian = ngay.Date.Add(gio),
                NguoiThucHienId = nguoiThucHienId
            };

            Close(ketQua);
        }
        catch
        {
            if (btn != null) btn.IsEnabled = true;
            throw;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void CapNhatConNoSauThu()
    {
        var soTien = TienThuBox.Value;
        var congNoDuocThanhToan = Math.Min(soTien, Math.Max(0m, _conNo));
        var tienDu = Math.Max(0m, soTien - Math.Max(0m, _conNo));
        var conNoSauThu = Math.Max(0m, _conNo - soTien);
        var soDuTraTruocSau = _soDuTraTruoc + tienDu;

        TxtCongNoDuocThanhToan.Text = $"{congNoDuocThanhToan:N0} đ";
        TxtTienThanhToanDu.Text = $"{tienDu:N0} đ";
        TxtConNoSauThu.Text = $"{conNoSauThu:N0} đ";
        TxtConNoSauThu.Foreground = conNoSauThu > 0
            ? Avalonia.Media.Brush.Parse("#D64545")
            : Avalonia.Media.Brush.Parse("#087F6E");

        TxtSoDuTraTruocSau.Text = $"{soDuTraTruocSau:N0} đ";
    }

    private void HienLoi(string thongDiep)
    {
        TxtLoi.Text = thongDiep;
        TxtLoi.IsVisible = true;
    }
}
