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

    public ThuNoKhachHangWindow()
        : this("", "Khách hàng", 0m, 0m, Array.Empty<SoQuyLuaChonItem>(), null)
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

        // Thêm 2 dòng này
        TxtSoTienThu = this.FindControl<TextBox>("TxtSoTienThu")
            ?? throw new InvalidOperationException("Không tìm thấy TxtSoTienThu");

        CboPhuongThuc.ItemsSource = DanhSachPhuongThuc;
        CboPhuongThuc.SelectedIndex = 0;

        var danhSachNguoiThu = nguoiThuOptions ?? Array.Empty<SoQuyLuaChonItem>();
        CboNguoiThu.ItemsSource = danhSachNguoiThu;
        _nguoiThucHienIdMacDinh = nguoiThucHienIdMacDinh;
        CboNguoiThu.SelectedItem = danhSachNguoiThu.FirstOrDefault(option =>
            uint.TryParse(option.GiaTri, out var id) && id == nguoiThucHienIdMacDinh);

        _conNo = Math.Max(0m, conNo);
        _soDuTraTruoc = Math.Max(0m, soDuTraTruoc);

        TxtMaKhachHang.Text = string.IsNullOrWhiteSpace(maKhachHang) ? "—" : maKhachHang;
        TxtKhachHang.Text = string.IsNullOrWhiteSpace(tenKhachHang) ? "Khách lẻ" : tenKhachHang;
        TxtConNo.Text = $"{conNo:N0} đ";
        TxtSoDuTraTruocHienTai.Text = $"{soDuTraTruoc:N0} đ";

        // Không điền sẵn số tiền
        TxtSoTienThu.Text = "";
        TxtSoTienThu.TextChanged += TxtSoTienThu_TextChanged;

        NgayThanhToanPicker.SelectedDate = DateTimeOffset.Now.Date;
        GioThanhToanBox.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);

        CapNhatConNoSauThu();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (!TryDocSoTien(TxtSoTienThu.Text, out var soTien) || soTien <= 0m)
        {
            HienLoi("Vui lòng nhập số tiền thu hợp lệ và lớn hơn 0.");
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
            MaThamChieu = string.IsNullOrWhiteSpace(TxtMaThamChieu.Text)
                ? null
                : TxtMaThamChieu.Text.Trim(),
            GhiChu = string.IsNullOrWhiteSpace(TxtGhiChu.Text)
                ? null
                : TxtGhiChu.Text.Trim(),
            ThoiGian = ngay.Date.Add(gio),
            NguoiThucHienId = nguoiThucHienId
        };

        Close(ketQua);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void CapNhatConNoSauThu()
    {
        // Chỉ đọc, không ghi ngược → an toàn với IME tiếng Việt
        var soTien = TryDocSoTien(TxtSoTienThu.Text, out var value) ? value : 0m;

        var congNoDuocThanhToan = Math.Min(soTien, _conNo);
        var tienDu = Math.Max(0m, soTien - _conNo);
        var conNoSauThu = Math.Max(0m, _conNo - soTien);
        var soDuTraTruocSau = _soDuTraTruoc + tienDu;

        TxtCongNoDuocThanhToan.Text = $"{congNoDuocThanhToan:N0} đ";
        TxtTienThanhToanDu.Text = $"{tienDu:N0} đ";
        TxtConNoSauThu.Text = $"{conNoSauThu:N0} đ";
        TxtConNoSauThu.Foreground = conNoSauThu > 0m
            ? Avalonia.Media.Brush.Parse("#D64545")
            : Avalonia.Media.Brush.Parse("#087F6E");
        TxtSoDuTraTruocSau.Text = $"{soDuTraTruocSau:N0} đ";
    }

    private static bool TryDocSoTien(string? text, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var digits = new string(text.Where(char.IsDigit).ToArray());
        var chiCoKyTuHopLe = text.All(c =>
            char.IsDigit(c) || char.IsWhiteSpace(c) ||
            c == '.' || c == ',' || c == '\'');

        return chiCoKyTuHopLe
               && digits.Length > 0
               && decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out amount);
    }

    private void HienLoi(string thongDiep)
    {
        TxtLoi.Text = thongDiep;
        TxtLoi.IsVisible = true;
    }

    private void TxtSoTienThu_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;

        var oldText = tb.Text ?? "";
        var digits = LayChuSo(oldText);

        if (digits.Length == 0)
        {
            if (tb.Text != "")
                tb.Text = "";
            CapNhatConNoSauThu();
            return;
        }

        var formatted = FormatSoTien(digits);

        // Đã đúng format rồi thì không làm gì
        if (tb.Text == formatted)
        {
            CapNhatConNoSauThu();
            return;
        }

        // Giữ vị trí con trỏ theo số chữ số
        int caret = tb.CaretIndex;
        int digitsBeforeCaret = LayChuSo(oldText.Substring(0, Math.Min(caret, oldText.Length))).Length;

        tb.Text = formatted;

        int newCaret = 0;
        int digitCount = 0;
        for (int i = 0; i < formatted.Length; i++)
        {
            if (char.IsDigit(formatted[i]))
                digitCount++;

            if (digitCount >= digitsBeforeCaret)
            {
                newCaret = i + 1;
                break;
            }
        }

        tb.CaretIndex = Math.Min(newCaret, formatted.Length);
        CapNhatConNoSauThu();
    }

    private static string LayChuSo(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return new string(text.Where(char.IsDigit).ToArray());
    }

    private static string FormatSoTien(string digits)
    {
        if (string.IsNullOrEmpty(digits)) return "";

        if (!decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return digits;

        return number.ToString("N0", new CultureInfo("vi-VN")); // → 3.000
    }
}