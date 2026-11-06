using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class LichSuThanhToanKhachHangWindow
{
    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await ChonFileXuatAsync();
            if (file is null)
                return;

            var tuNgay = TuNgayPicker.SelectedDate?.Date;
            var denNgay = DenNgayPicker.SelectedDate?.Date.AddDays(1);
            var keyword = TimKiemBox.Text?.Trim() ?? "";
            var (summary, records) = await TaiToanBoDuLieuXuatAsync(tuNgay, denNgay, keyword);
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Lịch sử thanh toán");
            DienThongTinBaoCao(sheet, summary);
            DienDanhSachGiaoDich(sheet, records);
            DinhDangBaoCao(sheet);
            workbook.SaveAs(file.Path.LocalPath);
            TxtDateError.Text = "Đã xuất lịch sử thanh toán.";
            TxtDateError.Foreground = Avalonia.Media.Brushes.SeaGreen;
            TxtDateError.IsVisible = true;
        }
        catch (Exception ex)
        {
            TxtDateError.Text = "Không xuất được Excel: " + ex.Message;
            TxtDateError.Foreground = Avalonia.Media.Brushes.Firebrick;
            TxtDateError.IsVisible = true;
        }
    }

    private Task<IStorageFile?> ChonFileXuatAsync() =>
        StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Xuất lịch sử thanh toán công nợ",
            SuggestedFileName = $"LichSuThanhToan_{_customerCode}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            DefaultExtension = "xlsx",
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        });

    private async Task<(ThanhToanCongNoLichSuThongKe Summary, IReadOnlyList<ThanhToanCongNoKhachHang> Records)> TaiToanBoDuLieuXuatAsync(
        DateTime? tuNgay,
        DateTime? denNgay,
        string keyword)
    {
        var summary = await _database.GetThongKeLichSuThanhToanCongNoKhachHangAsync(
            _customerId, tuNgay, denNgay, keyword);
        var records = new List<ThanhToanCongNoKhachHang>();
        const int batchSize = 500;
        for (var offset = 0; offset < summary.TongSoGiaoDich; offset += batchSize)
        {
            var batch = await _database.GetLichSuThanhToanCongNoKhachHangAsync(
                _customerId, tuNgay, denNgay, keyword, batchSize, offset);
            if (batch.Count == 0)
                break;
            records.AddRange(batch);
        }

        return (summary, records);
    }

    private void DienThongTinBaoCao(IXLWorksheet sheet, ThanhToanCongNoLichSuThongKe summary)
    {
        sheet.Cell(1, 1).Value = "LỊCH SỬ THANH TOÁN CÔNG NỢ KHÁCH HÀNG";
        sheet.Cell(2, 1).Value = $"Khách hàng: {_customerCode} - {_customerName}";
        sheet.Cell(3, 1).Value =
            $"Khoảng thời gian: {TuNgayPicker.SelectedDate?.ToString("dd/MM/yyyy") ?? "Tất cả"}"
            + $" - {DenNgayPicker.SelectedDate?.ToString("dd/MM/yyyy") ?? "Tất cả"}";
        sheet.Cell(4, 1).Value =
            $"Tổng đã thanh toán: {summary.TongTienDaThanhToan:N0} đ"
            + $" | Tổng số giao dịch: {summary.TongSoGiaoDich:N0}"
            + $" | Công nợ trước: {summary.CongNoTruocThanhToan:N0} đ"
            + $" | Công nợ sau: {summary.CongNoSauThanhToan:N0} đ";

        var headers = LayTieuDeBaoCao();
        for (var index = 0; index < headers.Length; index++)
            sheet.Cell(6, index + 1).Value = headers[index];
    }

    private static void DienDanhSachGiaoDich(
        IXLWorksheet sheet,
        IReadOnlyList<ThanhToanCongNoKhachHang> records)
    {
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            var row = index + 7;
            sheet.Cell(row, 1).Value = index + 1;
            sheet.Cell(row, 2).Value = record.MaGiaoDich;
            sheet.Cell(row, 3).Value = record.MaKhachHang;
            sheet.Cell(row, 4).Value = record.ThoiGian;
            sheet.Cell(row, 5).Value = record.CongNoTruoc;
            sheet.Cell(row, 6).Value = record.SoTien;
            sheet.Cell(row, 7).Value = record.CongNoSau;
            sheet.Cell(row, 8).Value = record.PhuongThucHienThi;
            sheet.Cell(row, 9).Value = record.TrangThaiHienThi;
            sheet.Cell(row, 10).Value = record.MaThamChieuHienThi;
            sheet.Cell(row, 11).Value = record.NguoiThucHien;
            sheet.Cell(row, 12).Value = record.GhiChu ?? "";
        }
    }

    private static string[] LayTieuDeBaoCao() =>
    [
        "STT", "Mã giao dịch", "Mã khách hàng", "Ngày giờ", "Công nợ trước",
        "Số tiền thanh toán", "Công nợ sau", "Phương thức",
        "Trạng thái", "Mã tham chiếu", "Người thực hiện", "Ghi chú"
    ];

    private static void DinhDangBaoCao(IXLWorksheet sheet)
    {
        var lastColumn = LayTieuDeBaoCao().Length;
        sheet.Range(1, 1, 1, lastColumn).Merge();
        sheet.Range(1, 1, 1, lastColumn).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, lastColumn).Style.Font.FontSize = 16;
        sheet.Range(6, 1, 6, lastColumn).Style.Font.Bold = true;
        sheet.Range(6, 1, 6, lastColumn).Style.Fill.BackgroundColor =
            XLColor.FromHtml("#EAF5F2");
        sheet.Column(4).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        sheet.Columns(5, 7).Style.NumberFormat.Format = "#,##0";
        sheet.Columns().AdjustToContents();
    }
}
