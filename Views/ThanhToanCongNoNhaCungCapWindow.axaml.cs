using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class ThanhToanCongNoNhaCungCapWindow : Window
{
    private static readonly IReadOnlyList<PhuongThucOption> PhuongThucOptions =
    [
        new PhuongThucOption { Ma = "TIEN_MAT", Ten = "Tiền mặt" },
        new PhuongThucOption { Ma = "CHUYEN_KHOAN", Ten = "Chuyển khoản" },
        new PhuongThucOption { Ma = "THE", Ten = "Thẻ" },
        new PhuongThucOption { Ma = "VI_DIEN_TU", Ten = "Ví điện tử" }
    ];

    private readonly decimal _conNo;
    private readonly decimal _soDuTraTruoc;

    public ThanhToanCongNoNhaCungCapWindow()
        : this("", "Nhà cung cấp", 0m, 0m, "")
    {
    }

    public ThanhToanCongNoNhaCungCapWindow(
        string maNhaCungCap,
        string tenNhaCungCap,
        decimal conNo,
        string nguoiThucHien)
        : this(maNhaCungCap, tenNhaCungCap, conNo, 0m, nguoiThucHien)
    {
    }

    public ThanhToanCongNoNhaCungCapWindow(
        string maNhaCungCap,
        string tenNhaCungCap,
        decimal conNo,
        decimal soDuTraTruoc,
        string nguoiThucHien)
    {
        InitializeComponent();

        _conNo = Math.Max(0m, conNo);
        _soDuTraTruoc = Math.Max(0m, soDuTraTruoc);

        TxtMaNhaCungCap.Text = maNhaCungCap;
        TxtNhaCungCap.Text = tenNhaCungCap;
        TxtCongNo.Text = $"{conNo:N0} đ";
        TxtSoDuTraTruocHienTai.Text = $"{soDuTraTruoc:N0} đ";
        TxtNguoiThucHien.Text = nguoiThucHien;

        CboPhuongThuc.ItemsSource = PhuongThucOptions;
        CboPhuongThuc.SelectedIndex = 0;

        TxtSoTienThanhToan.Text = "";
        TxtSoTienThanhToan.TextChanged += TxtSoTienThanhToan_TextChanged;

        NgayThanhToanPicker.SelectedDate = DateTimeOffset.Now.Date;
        GioThanhToanBox.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);

        CapNhatCongNoSau();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;

        if (!TryDocSoTien(TxtSoTienThanhToan.Text, out var amount) || amount <= 0m)
        {
            HienLoi("Vui lòng nhập số tiền thanh toán hợp lệ và lớn hơn 0.");
            return;
        }

        if (NgayThanhToanPicker.SelectedDate is not { } date
            || !TimeSpan.TryParseExact(
                GioThanhToanBox.Text?.Trim(),
                @"hh\:mm",
                CultureInfo.InvariantCulture,
                out var time))
        {
            HienLoi("Vui lòng chọn ngày và nhập giờ hợp lệ theo định dạng HH:mm.");
            return;
        }

        if (btn != null)
            btn.IsEnabled = false;

        try
        {
            Close(new ThanhToanCongNoNhaCungCapYeuCau
            {
                SoTien = amount,
                PhuongThuc = (CboPhuongThuc.SelectedItem as PhuongThucOption)?.Ma ?? "TIEN_MAT",
                MaThamChieu = string.IsNullOrWhiteSpace(TxtMaThamChieu.Text)
                    ? null
                    : TxtMaThamChieu.Text.Trim(),
                GhiChu = string.IsNullOrWhiteSpace(TxtGhiChu.Text)
                    ? null
                    : TxtGhiChu.Text.Trim(),
                ThoiGian = date.Date.Add(time)
            });
        }
        catch
        {
            if (btn != null)
                btn.IsEnabled = true;
            throw;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void CapNhatCongNoSau()
    {
        // Chỉ đọc, KHÔNG ghi ngược vào TextBox → không phá IME tiếng Việt
        var amount = TryDocSoTien(TxtSoTienThanhToan.Text, out var value) ? value : 0m;

        var congNoDuocThanhToan = Math.Min(amount, _conNo);
        var tienDu = Math.Max(0m, amount - _conNo);
        var conNoSau = Math.Max(0m, _conNo - amount);
        var soDuTraTruocSau = _soDuTraTruoc + tienDu;

        TxtCongNoDuocThanhToan.Text = $"{congNoDuocThanhToan:N0} đ";
        TxtTienThanhToanDu.Text = $"{tienDu:N0} đ";
        TxtCongNoSau.Text = $"{conNoSau:N0} đ";
        TxtCongNoSau.Foreground = conNoSau > 0m
            ? Avalonia.Media.Brush.Parse("#D64545")
            : Avalonia.Media.Brush.Parse("#087F6E");

        TxtSoDuTraTruocSau.Text = $"{soDuTraTruocSau:N0} đ";
    }

    private static bool TryDocSoTien(string? text, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Chỉ lấy chữ số, chấp nhận dấu phân nhóm, không sửa chuỗi gốc
        var digits = new string(text.Where(char.IsDigit).ToArray());
        var chiCoKyTuHopLe = text.All(c =>
            char.IsDigit(c) || char.IsWhiteSpace(c) ||
            c == '.' || c == ',' || c == '\'');

        return chiCoKyTuHopLe
               && digits.Length > 0
               && decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out amount);
    }

    private void HienLoi(string message)
    {
        TxtLoi.Text = message;
        TxtLoi.IsVisible = true;
    }
    private void TxtSoTienThanhToan_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;

        var oldText = tb.Text ?? "";
        var digits = LayChuSo(oldText);

        if (digits.Length == 0)
        {
            if (tb.Text != "")
                tb.Text = "";
            CapNhatCongNoSau();
            return;
        }

        var formatted = FormatSoTien(digits);

        if (tb.Text == formatted)
        {
            CapNhatCongNoSau();
            return;
        }

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
        CapNhatCongNoSau();
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

        return number.ToString("N0", new CultureInfo("vi-VN"));
    }
}