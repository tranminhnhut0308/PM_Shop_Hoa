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
        _conNo = conNo;
        _soDuTraTruoc = soDuTraTruoc;
        TxtMaNhaCungCap.Text = maNhaCungCap;
        TxtNhaCungCap.Text = tenNhaCungCap;
        TxtCongNo.Text = $"{conNo:N0} đ";
        TxtSoDuTraTruocHienTai.Text = $"{soDuTraTruoc:N0} đ";
        TxtNguoiThucHien.Text = nguoiThucHien;
        CboPhuongThuc.ItemsSource = PhuongThucOptions;
        CboPhuongThuc.SelectedIndex = 0;
        TienThanhToanBox.Value = Math.Max(0m, conNo);
        NgayThanhToanPicker.SelectedDate = DateTimeOffset.Now.Date;
        GioThanhToanBox.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        TienThanhToanBox.ValueChanged += (_, _) => CapNhatCongNoSau();
        CapNhatCongNoSau();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;

        try
        {
            var amount = TienThanhToanBox.Value;
            if (amount <= 0)
            {
                HienLoi("Số tiền thanh toán phải lớn hơn 0.");
                if (btn != null) btn.IsEnabled = true;
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
                if (btn != null) btn.IsEnabled = true;
                return;
            }

            Close(new ThanhToanCongNoNhaCungCapYeuCau
            {
                SoTien = amount,
                PhuongThuc = (CboPhuongThuc.SelectedItem as PhuongThucOption)?.Ma ?? "TIEN_MAT",
                MaThamChieu = string.IsNullOrWhiteSpace(TxtMaThamChieu.Text)
                    ? null
                    : TxtMaThamChieu.Text.Trim(),
                GhiChu = string.IsNullOrWhiteSpace(TxtGhiChu.Text) ? null : TxtGhiChu.Text.Trim(),
                ThoiGian = date.Date.Add(time)
            });
        }
        catch
        {
            if (btn != null) btn.IsEnabled = true;
            throw;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void CapNhatCongNoSau()
    {
        var amount = TienThanhToanBox.Value;
        var congNoDuocThanhToan = Math.Min(amount, Math.Max(0m, _conNo));
        var tienDu = Math.Max(0m, amount - Math.Max(0m, _conNo));
        var conNoSau = Math.Max(0m, _conNo - amount);
        var soDuTraTruocSau = _soDuTraTruoc + tienDu;

        TxtCongNoDuocThanhToan.Text = $"{congNoDuocThanhToan:N0} đ";
        TxtTienThanhToanDu.Text = $"{tienDu:N0} đ";
        TxtCongNoSau.Text = $"{conNoSau:N0} đ";
        TxtCongNoSau.Foreground = conNoSau > 0
            ? Avalonia.Media.Brush.Parse("#D64545")
            : Avalonia.Media.Brush.Parse("#087F6E");

        TxtSoDuTraTruocSau.Text = $"{soDuTraTruocSau:N0} đ";
    }

    private void HienLoi(string message)
    {
        TxtLoi.Text = message;
        TxtLoi.IsVisible = true;
    }
}
