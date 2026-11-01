using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class LichSuThanhToanKhachHangWindow : Window
{
    private readonly DatabaseService _database;
    private readonly uint _customerId;
    private readonly string _customerCode;
    private readonly string _customerName;
    private IReadOnlyList<ThanhToanCongNoKhachHang> _records = [];
    private IReadOnlyList<ThanhToanCongNoKhachHang> _visibleRecords = [];

    public LichSuThanhToanKhachHangWindow()
        : this(new DatabaseService(), 0, "", "Khách hàng")
    {
    }

    public LichSuThanhToanKhachHangWindow(
        DatabaseService database,
        uint customerId,
        string customerCode,
        string customerName)
    {
        _database = database;
        _customerId = customerId;
        _customerCode = customerCode;
        _customerName = customerName;

        InitializeComponent();
        TxtKhachHang.Text = $"{customerCode} - {customerName}";
        LichSuGrid.ItemsSource = Array.Empty<ThanhToanCongNoKhachHang>();
        Loaded += async (_, _) => await NapDuLieuAsync();
        TimKiemBox.TextChanged += (_, _) => LocTheoTuKhoa();
    }

    private async Task NapDuLieuAsync()
    {
        TxtTrangThai.Text = "";
        try
        {
            DateTime? tuNgay = TuNgayPicker.SelectedDate?.DateTime.Date;
            DateTime? denNgay = DenNgayPicker.SelectedDate?.DateTime.Date.AddDays(1);
            _records = await _database.GetLichSuThanhToanCongNoKhachHangAsync(
                _customerId,
                tuNgay,
                denNgay);
            LocTheoTuKhoa();
        }
        catch (Exception ex)
        {
            TxtTrangThai.Text = "Không tải được lịch sử: " + ex.Message;
        }
    }

    private async void OnFilterClick(object? sender, RoutedEventArgs e)
    {
        if (TuNgayPicker.SelectedDate is { } tuNgay
            && DenNgayPicker.SelectedDate is { } denNgay
            && tuNgay.Date > denNgay.Date)
        {
            TxtTrangThai.Text = "Ngày bắt đầu không được sau ngày kết thúc.";
            return;
        }

        await NapDuLieuAsync();
    }

    private async void OnLamMoiClick(object? sender, RoutedEventArgs e)
    {
        TuNgayPicker.SelectedDate = null;
        DenNgayPicker.SelectedDate = null;
        TimKiemBox.Text = "";
        await NapDuLieuAsync();
    }

    private void LocTheoTuKhoa()
    {
        var keyword = TimKiemBox.Text?.Trim();
        _visibleRecords = string.IsNullOrWhiteSpace(keyword)
            ? _records
            : _records.Where(record =>
                record.MaGiaoDich.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(record.MaThamChieu) && record.MaThamChieu.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || record.PhuongThucHienThi.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || record.NguoiThucHien.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (record.GhiChu?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

        LichSuGrid.ItemsSource = _visibleRecords;
        TxtTongTien.Text = $"Tổng đã thanh toán: {_visibleRecords.Where(record => !record.DaHuy).Sum(record => record.SoTien):N0} đ";
        TxtTongGiaoDich.Text = $"Tổng số giao dịch: {_visibleRecords.Count:N0}";
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        OnDetailClick(sender, e);
    }

    private async void OnDetailClick(object? sender, RoutedEventArgs e)
    {
        if (LichSuGrid.SelectedItem is not ThanhToanCongNoKhachHang record)
        {
            TxtTrangThai.Text = "Vui lòng chọn một giao dịch cần xem chi tiết.";
            return;
        }

        try
        {
            TxtTrangThai.Text = "";
            var allocations = await _database.GetPhanBoThanhToanKhachHangAsync(record.MaGiaoDich);

            var detail = new StringBuilder();
            detail.AppendLine("══════════ THÔNG TIN GIAO DỊCH ══════════");
            detail.AppendLine($"Mã giao dịch / Phiếu thu: {record.MaGiaoDich}");
            detail.AppendLine("Loại giao dịch: Thu nợ khách hàng (Thanh toán công nợ)");
            detail.AppendLine($"Khách hàng: {_customerCode} - {_customerName}");
            detail.AppendLine($"Thời gian: {record.ThoiGianText}");
            detail.AppendLine($"Phương thức thanh toán: {record.PhuongThucHienThi}");
            detail.AppendLine($"Mã tham chiếu / chứng từ: {record.MaThamChieuHienThi}");
            detail.AppendLine($"Người thực hiện: {record.NguoiThucHien}");
            detail.AppendLine($"Ghi chú: {record.GhiChuHienThi}");
            detail.AppendLine();

            detail.AppendLine("══════════ TÌNH TRẠNG CÔNG NỢ ══════════");
            detail.AppendLine($"Công nợ trước thanh toán: {record.CongNoTruoc:N0} đ");
            detail.AppendLine($"Số tiền thanh toán:       {record.SoTien:N0} đ");
            detail.AppendLine($"Công nợ sau thanh toán:   {record.CongNoSau:N0} đ");
            detail.AppendLine();

            if (record.DaHuy)
            {
                detail.AppendLine("══════════ THÔNG TIN HỦY GIAO DỊCH ══════════");
                detail.AppendLine("Trạng thái: ĐÃ HỦY (Đã hoàn tác công nợ)");
                detail.AppendLine($"Người hủy: {record.NguoiHuy}");
                detail.AppendLine($"Thời gian hủy: {record.ThoiGianHuyText}");
                detail.AppendLine($"Lý do hủy: {record.LyDoHuy}");
                detail.AppendLine();
            }
            else
            {
                detail.AppendLine("Trạng thái: Hoàn thành");
                detail.AppendLine($"Liên kết Sổ quỹ: Phiếu thu {record.MaGiaoDich} (Đã ghi vào sổ quỹ)");
                detail.AppendLine();
            }

            detail.AppendLine("══════════ PHÂN BỔ VÀO HÓA ĐƠN BÁN HÀNG ══════════");
            if (allocations.Count > 0)
            {
                for (int i = 0; i < allocations.Count; i++)
                {
                    var a = allocations[i];
                    detail.AppendLine($"  {i + 1}. Hóa đơn {a.MaHoaDon}: {a.SoTien:N0} đ");
                }
                var allocatedTotal = allocations.Sum(a => a.SoTien);
                detail.AppendLine($"Tổng tiền đã phân bổ: {allocatedTotal:N0} đ ({allocations.Count} hóa đơn)");
                if (record.SoTien > allocatedTotal)
                {
                    detail.AppendLine($"Số tiền thanh toán chưa gắn hóa đơn: {record.SoTien - allocatedTotal:N0} đ");
                }
            }
            else
            {
                detail.AppendLine("Không có dữ liệu phân bổ hóa đơn cho giao dịch này (thu công nợ tổng).");
            }

            var dialog = new ConfirmationWindow(
                "Chi tiết giao dịch thanh toán khách hàng",
                detail.ToString(),
                "Đóng");
            await dialog.ShowDialog<bool>(this);
        }
        catch (Exception ex)
        {
            TxtTrangThai.Text = "Không đọc được chi tiết giao dịch: " + ex.Message;
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất lịch sử thanh toán công nợ",
                SuggestedFileName = $"LichSuThanhToan_{_customerCode}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                DefaultExtension = "xlsx",
                ShowOverwritePrompt = true,
                FileTypeChoices = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
            });

            if (file is null)
                return;

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Lịch sử thanh toán");
            var headers = new[]
            {
                "Mã giao dịch", "Mã khách hàng", "Ngày giờ", "Công nợ trước",
                "Số tiền thanh toán", "Công nợ sau", "Phương thức",
                "Mã tham chiếu", "Trạng thái", "Người thực hiện", "Ghi chú"
            };

            for (var index = 0; index < headers.Length; index++)
                sheet.Cell(1, index + 1).Value = headers[index];

            for (var index = 0; index < _visibleRecords.Count; index++)
            {
                var record = _visibleRecords[index];
                var row = index + 2;
                sheet.Cell(row, 1).Value = record.MaGiaoDich;
                sheet.Cell(row, 2).Value = record.MaKhachHang;
                sheet.Cell(row, 3).Value = record.ThoiGian;
                sheet.Cell(row, 4).Value = record.CongNoTruoc;
                sheet.Cell(row, 5).Value = record.SoTien;
                sheet.Cell(row, 6).Value = record.CongNoSau;
                sheet.Cell(row, 7).Value = record.PhuongThucHienThi;
                sheet.Cell(row, 8).Value = record.MaThamChieuHienThi;
                sheet.Cell(row, 9).Value = record.TrangThaiHienThi;
                sheet.Cell(row, 10).Value = record.NguoiThucHien;
                sheet.Cell(row, 11).Value = record.GhiChu ?? "";
            }

            sheet.Row(1).Style.Font.Bold = true;
            sheet.Column(3).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            sheet.Columns(4, 6).Style.NumberFormat.Format = "#,##0";
            sheet.Columns().AdjustToContents();
            workbook.SaveAs(file.Path.LocalPath);
            TxtTrangThai.Text = "Đã xuất lịch sử thanh toán.";
        }
        catch (Exception ex)
        {
            TxtTrangThai.Text = "Không xuất được Excel: " + ex.Message;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
