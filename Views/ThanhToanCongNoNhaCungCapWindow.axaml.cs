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

    public ThanhToanCongNoNhaCungCapWindow()
        : this("", "Nhà cung cấp", 0m, "")
    {
    }

    public ThanhToanCongNoNhaCungCapWindow(
        string maNhaCungCap,
        string tenNhaCungCap,
        decimal conNo,
        string nguoiThucHien)
    {
        InitializeComponent();
        _conNo = conNo;
        TxtMaNhaCungCap.Text = maNhaCungCap;
        TxtNhaCungCap.Text = tenNhaCungCap;
        TxtCongNo.Text = $"{conNo:N0} đ";
        TxtNguoiThucHien.Text = nguoiThucHien;
        CboPhuongThuc.ItemsSource = PhuongThucOptions;
        CboPhuongThuc.SelectedIndex = 0;
        TienThanhToanBox.Value = conNo;
        NgayThanhToanPicker.SelectedDate = DateTimeOffset.Now.Date;
        GioThanhToanBox.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        TienThanhToanBox.ValueChanged += (_, _) => CapNhatCongNoSau();
        CapNhatCongNoSau();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var amount = TienThanhToanBox.Value;
        if (amount <= 0)
        {
            HienLoi("Số tiền thanh toán phải lớn hơn 0.");
            return;
        }

        if (amount > _conNo)
        {
            HienLoi($"Số tiền thanh toán ({amount:N0} đ) vượt quá công nợ hiện tại ({_conNo:N0} đ).");
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

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void CapNhatCongNoSau()
        => TxtCongNoSau.Text = $"{Math.Max(0m, _conNo - TienThanhToanBox.Value):N0} đ";

    private void HienLoi(string message)
    {
        TxtLoi.Text = message;
        TxtLoi.IsVisible = true;
    }
}
