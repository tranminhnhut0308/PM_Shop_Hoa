using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ShopHoa.Helpers;
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
    private bool _dangDinhDangSoTien;
    private string _soTienHopLeCuoi = string.Empty;

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
        TxtSoTienThanhToan.GotFocus += (s, e) => Dispatcher.UIThread.Post(() => {
            if (!string.IsNullOrEmpty(TxtSoTienThanhToan.Text)) TxtSoTienThanhToan.SelectAll();
        }, DispatcherPriority.Input);
        TxtSoTienThanhToan.AddHandler(InputElement.PointerReleasedEvent, (s, e) => {
            if (e.InitialPressMouseButton == MouseButton.Left)
                Dispatcher.UIThread.Post(() => {
                    if (!string.IsNullOrEmpty(TxtSoTienThanhToan.Text)) TxtSoTienThanhToan.SelectAll();
                }, DispatcherPriority.Input);
        }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

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
        if (string.IsNullOrWhiteSpace(text) || text.Contains(','))
            return false;

        return text.Any(char.IsDigit)
               && MoneyInput.TryParse(text, false, out amount, out _);
    }

    private void HienLoi(string message)
    {
        TxtLoi.Text = message;
        TxtLoi.IsVisible = true;
    }
    private void TxtSoTienThanhToan_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_dangDinhDangSoTien || sender is not TextBox tb)
            return;

        var source = tb.Text ?? string.Empty;
        var transient = MoneyInput.IsTransientComposition(source, tb.CaretIndex);

        if (!MoneyInput.TryFormatEditingText(
                source,
                tb.CaretIndex,
                MoneyInput.GiaTienToiDa,
                out var formatted,
                out var caret,
                allowDecimal: false))
        {
            _dangDinhDangSoTien = true;
            try
            {
                tb.Text = _soTienHopLeCuoi;
                tb.CaretIndex = _soTienHopLeCuoi.Length;
            }
            finally
            {
                _dangDinhDangSoTien = false;
            }

            CapNhatCongNoSau();
            return;
        }

        _dangDinhDangSoTien = true;
        try
        {
            if (tb.Text != formatted)
                tb.Text = formatted;
            tb.CaretIndex = caret;
            if (!transient)
                _soTienHopLeCuoi = formatted;
        }
        finally
        {
            _dangDinhDangSoTien = false;
        }

        CapNhatCongNoSau();
    }
}