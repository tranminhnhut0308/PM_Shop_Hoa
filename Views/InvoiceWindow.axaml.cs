using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class InvoiceWindow : Window
{
    private readonly Func<HoaDonBanHangReceipt, Window, Task<HoaDonBanHangReceipt?>>? _thuCongNo;
    private readonly Func<HuyHoaDonResult, Task>? _onHoaDonCancelled;

    public InvoiceWindow() : this(new HoaDonBanHangReceipt())
    {
    }

    public InvoiceWindow(HoaDonBanHangReceipt receipt)
        : this(receipt, (Func<HoaDonBanHangReceipt, Window, Task<HoaDonBanHangReceipt?>>?)null, null)
    {
    }

    public InvoiceWindow(
        HoaDonBanHangReceipt receipt,
        Func<HoaDonBanHangReceipt, Window, Task<HoaDonBanHangReceipt?>>? thuCongNo,
        Func<HuyHoaDonResult, Task>? onHoaDonCancelled = null)
    {
        InitializeComponent();
        _thuCongNo = thuCongNo;
        _onHoaDonCancelled = onHoaDonCancelled;
        CapNhatGiaoDienTheoReceipt(receipt);
    }

    public InvoiceWindow(
        HoaDonBanHangReceipt receipt,
        Func<HoaDonBanHangReceipt, Task<HoaDonBanHangReceipt?>>? thuCongNo)
        : this(receipt, thuCongNo == null ? null : (r, _) => thuCongNo(r), null)
    {
    }

    private void CapNhatGiaoDienTheoReceipt(HoaDonBanHangReceipt receipt)
    {
        DataContext = null;
        DataContext = receipt;
        CapNhatNutThanhToan(receipt);
        PrintButton.IsVisible = !receipt.LaHoaDonTongHop;
        CancelInvoiceButton.IsVisible = !receipt.LaHoaDonDaHuy && DatabaseService.CoQuyenHuyHoaDonHienTai();
    }

    private void CapNhatNutThanhToan(HoaDonBanHangReceipt receipt)
    {
        // Hóa đơn đã hủy hoặc đã trả đủ (ConNo <= 0) không được phép thu nợ.
        ThanhToanCongNoButton.IsVisible = !receipt.LaHoaDonDaHuy && receipt.ConNo > 0;
    }

    private async void CancelInvoiceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HoaDonBanHangReceipt receipt)
            return;

        if (!DatabaseService.CoQuyenHuyHoaDonHienTai())
        {
            await HienThiThongBaoAsync("Thông báo", "Bạn không có quyền hủy hóa đơn bán hàng.");
            return;
        }

        if (receipt.LaHoaDonDaHuy)
        {
            await HienThiThongBaoAsync("Thông báo", $"Hóa đơn {receipt.MaHoaDon} đã ở trạng thái Đã hủy.");
            return;
        }

        var dialog = new HuyHoaDonWindow(receipt.MaHoaDon);
        var ketQua = await dialog.ShowDialog<HuyHoaDonResult?>(this);
        if (ketQua is null)
            return;

        if (_onHoaDonCancelled is not null)
        {
            await _onHoaDonCancelled(ketQua);
        }

        if (ketQua.CheDoHuy == CheDoHuyHoaDon.HuyVaTaoHoaDonTuongTu)
        {
            // Đóng cửa sổ chi tiết hóa đơn để nhân viên làm việc ngay trên đơn nháp tại màn Bán hàng
            Close();
            return;
        }

        if (ketQua.ReceiptDaHuy is not null)
        {
            ketQua.ReceiptDaHuy.ThongTinTiem = receipt.ThongTinTiem;
            CapNhatGiaoDienTheoReceipt(ketQua.ReceiptDaHuy);
        }

        await HienThiThongBaoAsync(
            "Đã hủy hóa đơn",
            $"Hóa đơn {ketQua.MaHoaDon} đã được chuyển sang trạng thái Đã hủy và hoàn tác dữ liệu liên quan.");
    }

    private async void XemHoaDonThayThe_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HoaDonBanHangReceipt receipt || string.IsNullOrWhiteSpace(receipt.MaHoaDonThayThe))
            return;

        await ChuyenSangXemHoaDonKhacAsync(receipt.MaHoaDonThayThe, receipt.ThongTinTiem);
    }

    private async void XemHoaDonGoc_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HoaDonBanHangReceipt receipt || string.IsNullOrWhiteSpace(receipt.MaHoaDonGoc))
            return;

        await ChuyenSangXemHoaDonKhacAsync(receipt.MaHoaDonGoc, receipt.ThongTinTiem);
    }

    private async Task ChuyenSangXemHoaDonKhacAsync(string maHoaDon, ThongTinTiem thongTinTiem)
    {
        try
        {
            var db = new DatabaseService();
            var other = await db.GetHoaDonBanHangTheoMaAsync(maHoaDon);
            if (other is null)
            {
                await HienThiThongBaoAsync("Thông báo", $"Không tìm thấy hóa đơn {maHoaDon}.");
                return;
            }

            other.ThongTinTiem = thongTinTiem;
            CapNhatGiaoDienTheoReceipt(other);
        }
        catch (Exception ex)
        {
            await HienThiThongBaoAsync("Lỗi", $"Không thể mở hóa đơn {maHoaDon}: {ex.Message}");
        }
    }

    private async void ThanhToanCongNoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HoaDonBanHangReceipt receipt || receipt.LaHoaDonDaHuy || receipt.ConNo <= 0)
            return;

        ThanhToanCongNoButton.IsEnabled = false;

        try
        {
            HoaDonBanHangReceipt? capNhat;

            if (_thuCongNo is not null)
            {
                capNhat = await _thuCongNo(receipt, this);
            }
            else
            {
                capNhat = await ThuCongNoMacDinhAsync(receipt);
            }

            if (capNhat is not null)
            {
                CapNhatGiaoDienTheoReceipt(capNhat);
            }
        }
        finally
        {
            ThanhToanCongNoButton.IsEnabled = true;
        }
    }

    private async Task<HoaDonBanHangReceipt?> ThuCongNoMacDinhAsync(HoaDonBanHangReceipt receipt)
    {
        if (receipt.LaHoaDonDaHuy || receipt.ConNo <= 0)
            return null;

        if (!Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CustomersDebtPay)
            && !Quyen.IsAdmin(Session.CurrentUser))
        {
            await HienThiThongBaoAsync("Thông báo", "Bạn không có quyền thu công nợ khách hàng.");
            return null;
        }

        var hoaDon = new HoaDon
        {
            MaHoaDon = receipt.MaHoaDon,
            TenKhachHang = receipt.TenKhachHang,
            KhachCanTra = receipt.KhachCanTra,
            KhachDaThanhToan = receipt.KhachDaThanhToan,
            ConNo = receipt.ConNo
        };

        var dialog = new ThuNoHoaDonWindow(hoaDon);
        var ketQua = await dialog.ShowDialog<ThuNoHoaDonKetQua?>(this);

        if (ketQua is null)
            return null;

        try
        {
            var db = new DatabaseService();
            var capNhat = await db.ThuNoHoaDonAsync(
                hoaDon.MaHoaDon,
                ketQua.SoTien,
                ketQua.PhuongThuc,
                ketQua.GhiChu);

            capNhat.ThongTinTiem = receipt.ThongTinTiem;

            var thongBaoPhieu = string.IsNullOrWhiteSpace(capNhat.MaPhieuThu)
                ? ""
                : $"\nMã phiếu thu: {capNhat.MaPhieuThu} (đã ghi vào Sổ quỹ).";

            await HienThiThongBaoAsync(
                "Thu nợ thành công",
                $"Đã thu {ketQua.SoTien:N0} đ cho hóa đơn {hoaDon.MaHoaDon}.\n"
                + $"Còn nợ: {capNhat.ConNo:N0} đ.{thongBaoPhieu}");

            return capNhat;
        }
        catch (Exception ex)
        {
            await HienThiThongBaoAsync("Không thu được công nợ", ex.Message);
            SafeLog.WriteError("Loi thu no hoa don mac dinh trong InvoiceWindow", ex);
            return null;
        }
    }

    private async Task HienThiThongBaoAsync(string title, string message)
    {
        var window = new Window
        {
            Title = title,
            Width = 460,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var okButton = new Button
        {
            Content = "Đã hiểu",
            Width = 100,
            Height = 36,
            Background = Avalonia.Media.Brush.Parse("#087F6E"),
            Foreground = Avalonia.Media.Brushes.White,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 16, 0, 0)
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 10
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14
        });
        panel.Children.Add(okButton);

        window.Content = panel;
        okButton.Click += (_, _) => window.Close();

        await window.ShowDialog(this);
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PrintButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HoaDonBanHangReceipt receipt)
            return;

        try
        {
            var duongDan = HoaDonPrintService.LuuFileTam(receipt);

            Process.Start(new ProcessStartInfo
            {
                FileName = duongDan,
                UseShellExecute = true
            });

            PrintStatus.IsVisible = false;
        }
        catch (Exception ex)
        {
            PrintStatus.Text = "Không mở được bản in: " + ex.Message;
            PrintStatus.IsVisible = true;
        }
    }
}
