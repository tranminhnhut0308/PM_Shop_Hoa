using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

/// <summary>
/// Cửa sổ chi tiết giao dịch thanh toán công nợ khách hàng (Issue #102).
/// Thiết kế lại theo phong cách ShopHoa: phân nhóm thông tin khách hàng, chi
/// tiết công nợ, chứng từ liên quan (bấm để mở), kèm In / PDF / Sao chép /
/// Excel. Chỉ đọc dữ liệu giao dịch đã lưu, không thay đổi số tiền nào.
/// </summary>
public partial class ChiTietThanhToanKhachHangWindow : Window
{
    private readonly DatabaseService _database;
    private readonly StoreInfoService _storeInfo;
    private ChiTietThanhToanKhachHang _chiTiet = new();

    public ChiTietThanhToanKhachHangWindow()
        : this(new DatabaseService(), new ChiTietThanhToanKhachHang())
    {
    }

    public ChiTietThanhToanKhachHangWindow(DatabaseService database, ChiTietThanhToanKhachHang chiTiet)
    {
        _database = database;
        _storeInfo = new StoreInfoService(database.TaoKetNoiAsync);
        InitializeComponent();
        Loaded += async (_, _) => await NapThongTinTiemAsync();
        HienThiChiTiet(chiTiet);
    }

    // =========================================================
    // HIỂN THỊ DỮ LIỆU
    // =========================================================

    private void HienThiChiTiet(ChiTietThanhToanKhachHang chiTiet)
    {
        _chiTiet = chiTiet;

        Title = $"Chi tiết thanh toán công nợ — {chiTiet.MaPhieuThu}";

        // Phần 2A: tiêu đề giao dịch.
        TxtLoaiGiaoDich.Text = chiTiet.LoaiGiaoDich;
        TxtMaPhieuThuHeader.Text = chiTiet.MaPhieuThu;
        TxtSoTienHeader.Text = chiTiet.SoTienText;

        if (chiTiet.DaHuy)
        {
            StatusBadge.Background = Brush.Parse("#FEF3F2");
            TxtTrangThaiBadge.Text = "Đã hủy";
            TxtTrangThaiBadge.Foreground = Brush.Parse("#B42318");
        }
        else
        {
            StatusBadge.Background = Brush.Parse("#E7FBF2");
            TxtTrangThaiBadge.Text = "Hoàn thành";
            TxtTrangThaiBadge.Foreground = Brush.Parse("#027A48");
        }

        // Phần 2B: thông tin khách hàng & giao dịch.
        TxtMaKhachHang.Text = HienThi(chiTiet.MaKhachHang);
        TxtTenKhachHang.Text = HienThi(chiTiet.TenKhachHang);
        TxtThoiGian.Text = chiTiet.ThoiGianText;
        TxtPhuongThuc.Text = chiTiet.PhuongThucHienThi;
        TxtNguoiThucHien.Text = HienThi(chiTiet.NguoiThucHien);
        TxtMaThamChieu.Text = chiTiet.ChungTuLienQuans.Count > 0
            ? chiTiet.MaChungTuLienQuanText
            : chiTiet.MaThamChieuHienThi;
        TxtGhiChu.Text = chiTiet.GhiChuHienThi;

        // Phần 2C: chi tiết công nợ — số liệu gốc từ database.
        TxtCongNoTruoc.Text = chiTiet.CongNoTruocText;
        TxtSoTienChiTiet.Text = chiTiet.SoTienText;
        TxtCongNoSau.Text = chiTiet.CongNoSauText;

        if (chiTiet.DaHuy)
        {
            TxtCongNoSau.Foreground = Brush.Parse("#6B7280");
            CongNoBadge.Background = Brush.Parse("#F3F4F6");
            TxtCongNoBadge.Text = "Phiếu đã hủy — công nợ đã hoàn tác";
            TxtCongNoBadge.Foreground = Brush.Parse("#6B7280");
            TxtSoQuy.Text = "Phiếu thu trong sổ quỹ đã chuyển sang Đã hủy";
            TxtSoQuy.Foreground = Brush.Parse("#B42318");
        }
        else if (chiTiet.DaTraHet)
        {
            TxtCongNoSau.Foreground = Brush.Parse("#027A48");
            CongNoBadge.Background = Brush.Parse("#ECFDF3");
            TxtCongNoBadge.Text = "Đã trả hết công nợ";
            TxtCongNoBadge.Foreground = Brush.Parse("#027A48");
            TxtSoQuy.Text = "Đã ghi phiếu thu vào sổ quỹ";
            TxtSoQuy.Foreground = Brush.Parse("#027A48");
        }
        else
        {
            TxtCongNoSau.Foreground = Brush.Parse("#D92D20");
            CongNoBadge.Background = Brush.Parse("#FEF3F2");
            TxtCongNoBadge.Text = $"Còn nợ {chiTiet.CongNoSauText}";
            TxtCongNoBadge.Foreground = Brush.Parse("#B42318");
            TxtSoQuy.Text = "Đã ghi phiếu thu vào sổ quỹ";
            TxtSoQuy.Foreground = Brush.Parse("#027A48");
        }

        // Phần 2D: chứng từ liên quan.
        ChungTuList.ItemsSource = chiTiet.ChungTuLienQuans;
        TxtSoChungTu.Text = chiTiet.ChungTuLienQuans.Count == 1
            ? "1 chứng từ"
            : $"{chiTiet.ChungTuLienQuans.Count} chứng từ";
        TxtKhongChungTu.IsVisible = chiTiet.ChungTuLienQuans.Count == 0;

        // Thông tin hủy phiếu.
        HuyCard.IsVisible = chiTiet.DaHuy;
        TxtNguoiHuy.Text = HienThi(chiTiet.GiaoDich.NguoiHuy);
        TxtThoiGianHuy.Text = chiTiet.GiaoDich.ThoiGianHuyText;
        TxtLyDoHuy.Text = string.IsNullOrWhiteSpace(chiTiet.GiaoDich.LyDoHuy)
            ? "—"
            : chiTiet.GiaoDich.LyDoHuy.Trim();
    }

    private static string HienThi(string? giaTri)
        => string.IsNullOrWhiteSpace(giaTri) ? "—" : giaTri.Trim();

    /// <summary>
    /// Đọc thông tin tiệm để đưa tên cửa hàng vào biên nhận in / PDF / Excel /
    /// nội dung sao chép. Đọc lỗi thì vẫn dùng được mọi chức năng còn lại.
    /// </summary>
    private async Task NapThongTinTiemAsync()
    {
        try
        {
            _chiTiet.ThongTinTiem = await _storeInfo.LayThongTinKemLogoAsync();
        }
        catch
        {
            _chiTiet.ThongTinTiem = new ThongTinTiem();
        }
    }

    // =========================================================
    // IN BIÊN NHẬN (Phần 3)
    // =========================================================

    private void OnInClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var duongDan = PhieuThuPrintService.LuuFileTam(_chiTiet);

            Process.Start(new ProcessStartInfo
            {
                FileName = duongDan,
                UseShellExecute = true
            });

            HienThiTrangThai(
                "Đã mở bản in trong trình duyệt — bấm In (Ctrl+P) để in hoặc chọn \"Lưu dưới dạng PDF\".",
                "#027A48");
        }
        catch (Exception ex)
        {
            HienThiLoi("Không mở được bản in: " + ex.Message);
        }
    }

    // =========================================================
    // XUẤT PDF (Phần 4)
    // =========================================================

    private async void OnPdfClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Xuất biên nhận thanh toán công nợ ra PDF",
            SuggestedFileName = PhieuThuPrintService.TaoTenFilePdf(_chiTiet.MaPhieuThu),
            DefaultExtension = "pdf",
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }]
        });

        if (file is null)
            return;

        BtnPdf.IsEnabled = false;
        HienThiTrangThai("Đang tạo file PDF...", "#61736E");

        try
        {
            var (thanhCong, duongDan, loi) = await PhieuThuPrintService.XuatPdfAsync(
                _chiTiet, file.Path.LocalPath);

            if (thanhCong)
            {
                HienThiTrangThai($"Đã xuất PDF: {duongDan}", "#027A48");
                await NotificationWindow.ShowSuccessAsync(
                    $"Đã xuất biên nhận PDF thành công:\n{duongDan}",
                    "Xuất PDF hoàn tất",
                    this);
            }
            else
            {
                HienThiLoi(loi ?? "Không xuất được PDF.");
                await NotificationWindow.ShowErrorAsync(
                    loi ?? "Không xuất được PDF.",
                    "Không xuất được PDF",
                    this);
            }
        }
        catch (Exception ex)
        {
            HienThiLoi("Không xuất được PDF: " + ex.Message);
            await NotificationWindow.ShowErrorAsync(
                "Không xuất được PDF: " + ex.Message,
                "Lỗi xuất PDF",
                this);
        }
        finally
        {
            BtnPdf.IsEnabled = true;
        }
    }

    // =========================================================
    // SAO CHÉP NỘI DUNG (Phần 5)
    // =========================================================

    private async void OnSaoChepClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard is null)
        {
            HienThiLoi("Không truy cập được bộ nhớ tạm của hệ thống.");
            return;
        }

        try
        {
            await clipboard.SetTextAsync(_chiTiet.TaoNoiDungSaoChep());
            HienThiTrangThai(
                "Đã sao chép nội dung biên nhận — dán vào Zalo / Messenger để gửi khách hàng.",
                "#027A48");
        }
        catch (Exception ex)
        {
            HienThiLoi("Không sao chép được nội dung: " + ex.Message);
        }
    }

    // =========================================================
    // XUẤT EXCEL (Phần 6)
    // =========================================================

    private async void OnExcelClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Xuất chi tiết giao dịch thanh toán ra Excel",
            SuggestedFileName = $"PhieuThu_{_chiTiet.MaPhieuThu}_{DateTime.Now:yyyyMMdd}.xlsx",
            DefaultExtension = "xlsx",
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        });

        if (file is null)
            return;

        BtnExcel.IsEnabled = false;

        try
        {
            using var workbook = PhieuThuExcel.TaoWorkbook(
                _chiTiet,
                _chiTiet.TenCuaHang,
                Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap);
            workbook.SaveAs(file.Path.LocalPath);

            HienThiTrangThai($"Đã xuất Excel: {file.Path.LocalPath}", "#027A48");
            await NotificationWindow.ShowSuccessAsync(
                $"Đã xuất chi tiết giao dịch ra Excel thành công:\n{file.Path.LocalPath}",
                "Xuất Excel hoàn tất",
                this);
        }
        catch (Exception ex)
        {
            HienThiLoi("Không xuất được Excel: " + ex.Message);
            await NotificationWindow.ShowErrorAsync(
                "Không xuất được Excel: " + ex.Message,
                "Lỗi xuất Excel",
                this);
        }
        finally
        {
            BtnExcel.IsEnabled = true;
        }
    }

    // =========================================================
    // MỞ CHỨNG TỪ LIÊN QUAN (Phần 2D)
    // =========================================================

    private async void OnMoChungTuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ChungTuLienQuanItem chungTu })
            return;

        try
        {
            if (chungTu.LaHoaDon)
            {
                var receipt = await _database.GetHoaDonBanHangTheoMaAsync(chungTu.MaChungTu);
                if (receipt is null)
                {
                    await NotificationWindow.ShowWarningAsync(
                        $"Không tìm thấy hóa đơn {chungTu.MaChungTu} trong cơ sở dữ liệu.",
                        "Không tìm thấy chứng từ",
                        this);
                    return;
                }

                receipt.ThongTinTiem = _chiTiet.ThongTinTiem ?? new ThongTinTiem();
                var invoiceWindow = new InvoiceWindow(receipt);
                await invoiceWindow.ShowDialog(this);
                return;
            }

            if (chungTu.LaPhieuNhap)
            {
                var detail = await _database.GetPhieuNhapHangDetailTheoMaAsync(chungTu.MaChungTu);
                if (detail is null)
                {
                    await NotificationWindow.ShowWarningAsync(
                        $"Không tìm thấy phiếu nhập {chungTu.MaChungTu} trong cơ sở dữ liệu.",
                        "Không tìm thấy chứng từ",
                        this);
                    return;
                }

                var phieuNhapWindow = new PhieuNhapDetailWindow(detail);
                await phieuNhapWindow.ShowDialog(this);
                return;
            }

            await NotificationWindow.ShowAsync(
                $"Mã tham chiếu {chungTu.MaChungTu} không phải hóa đơn hay phiếu nhập, không thể mở trực tiếp.",
                "Không mở được chứng từ",
                NotificationType.Info,
                this);
        }
        catch (Exception ex)
        {
            await NotificationWindow.ShowErrorAsync(
                $"Không mở được chứng từ {chungTu.MaChungTu}: {ex.Message}",
                "Lỗi mở chứng từ",
                this);
        }
    }

    // =========================================================
    // TIỆN ÍCH
    // =========================================================

    private void OnDongClick(object? sender, RoutedEventArgs e) => Close();

    private void HienThiTrangThai(string noiDung, string mau)
    {
        TxtTrangThai.Text = noiDung;
        TxtTrangThai.Foreground = Brush.Parse(mau);
    }

    private void HienThiLoi(string noiDung)
    {
        TxtTrangThai.Text = noiDung;
        TxtTrangThai.Foreground = Brush.Parse("#B42318");
    }
}
