using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ShopHoa.Helpers;
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
    private bool _dangDinhDangSoTien;
    private string _soTienHopLeCuoi = string.Empty;

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
        TxtSoTienThu.GotFocus += (s, e) => Dispatcher.UIThread.Post(() => {
            if (!string.IsNullOrEmpty(TxtSoTienThu.Text)) TxtSoTienThu.SelectAll();
        }, DispatcherPriority.Input);
        TxtSoTienThu.AddHandler(InputElement.PointerReleasedEvent, (s, e) => {
            if (e.InitialPressMouseButton == MouseButton.Left)
                Dispatcher.UIThread.Post(() => {
                    if (!string.IsNullOrEmpty(TxtSoTienThu.Text)) TxtSoTienThu.SelectAll();
                }, DispatcherPriority.Input);
        }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

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
        var conNoSauThu = _conNo - soTien;
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
        if (string.IsNullOrWhiteSpace(text) || text.Contains(','))
            return false;

        return text.Any(char.IsDigit)
               && MoneyInput.TryParse(text, false, out amount, out _);
    }

    private void HienLoi(string thongDiep)
    {
        TxtLoi.Text = thongDiep;
        TxtLoi.IsVisible = true;
    }

    private void TxtSoTienThu_TextChanged(object? sender, TextChangedEventArgs e)
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

            CapNhatConNoSauThu();
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

        CapNhatConNoSauThu();
    }
}