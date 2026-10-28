using Avalonia.Controls;
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

    private void LocTheoTuKhoa()
    {
        var keyword = TimKiemBox.Text?.Trim();
        _visibleRecords = string.IsNullOrWhiteSpace(keyword)
            ? _records
            : _records.Where(record =>
                record.MaGiaoDich.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || record.PhuongThucHienThi.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || record.NguoiThucHien.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (record.GhiChu?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

        LichSuGrid.ItemsSource = _visibleRecords;
        TxtTongTien.Text = $"Tổng đã thanh toán: {_visibleRecords.Sum(record => record.SoTien):N0} đ";
        TxtTongGiaoDich.Text = $"Tổng số giao dịch: {_visibleRecords.Count:N0}";
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
                "Người thực hiện", "Ghi chú"
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
                sheet.Cell(row, 8).Value = record.NguoiThucHien;
                sheet.Cell(row, 9).Value = record.GhiChu ?? "";
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
