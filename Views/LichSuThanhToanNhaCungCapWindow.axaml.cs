using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class LichSuThanhToanNhaCungCapWindow : Window
{
    private readonly DatabaseService _database;
    private readonly uint? _initialSupplierId;
    private IReadOnlyList<ThanhToanCongNoNhaCungCap> _records = [];
    private IReadOnlyList<ThanhToanCongNoNhaCungCap> _visibleRecords = [];

    public LichSuThanhToanNhaCungCapWindow()
        : this(new DatabaseService(), null, "")
    {
    }

    public LichSuThanhToanNhaCungCapWindow(
        DatabaseService database,
        uint? initialSupplierId,
        string supplierName)
    {
        _database = database;
        _initialSupplierId = initialSupplierId;

        InitializeComponent();
        TxtNhaCungCap.Text = string.IsNullOrWhiteSpace(supplierName)
            ? "Lọc theo nhà cung cấp, khoảng ngày và nội dung giao dịch."
            : supplierName;
        LichSuGrid.ItemsSource = Array.Empty<ThanhToanCongNoNhaCungCap>();
        Loaded += async (_, _) => await NapDuLieuAsync();
        TimKiemBox.TextChanged += (_, _) => LocTheoTuKhoa();
    }

    private async Task NapDuLieuAsync()
    {
        TxtTrangThai.Text = "";
        try
        {
            if (CboNhaCungCap.ItemsSource is null)
            {
                var suppliers = await _database.GetNhaCungCapAsync();
                var options = new List<NhaCungCapThanhToanLuaChon>
                {
                    new() { Id = null, TenHienThi = "Tất cả nhà cung cấp" }
                };
                options.AddRange(suppliers.Select(supplier => new NhaCungCapThanhToanLuaChon
                {
                    Id = supplier.Id,
                    TenHienThi = $"{supplier.MaNhaCungCap} - {supplier.TenNhaCungCap}"
                }));
                CboNhaCungCap.ItemsSource = options;
                CboNhaCungCap.SelectedItem = options.FirstOrDefault(
                    option => option.Id == _initialSupplierId) ?? options[0];
            }

            if (TuNgayPicker.SelectedDate is { } tuNgay
                && DenNgayPicker.SelectedDate is { } denNgay
                && tuNgay.Date > denNgay.Date)
            {
                TxtTrangThai.Text = "Ngày bắt đầu không được sau ngày kết thúc.";
                return;
            }

            DateTime? from = TuNgayPicker.SelectedDate?.DateTime.Date;
            DateTime? toExclusive = DenNgayPicker.SelectedDate?.DateTime.Date.AddDays(1);
            var supplierId = (CboNhaCungCap.SelectedItem as NhaCungCapThanhToanLuaChon)?.Id;

            _records = await _database.GetLichSuThanhToanCongNoNhaCungCapAsync(
                supplierId,
                from,
                toExclusive);
            LocTheoTuKhoa();
        }
        catch (Exception ex)
        {
            TxtTrangThai.Text = "Không tải được lịch sử: " + ex.Message;
        }
    }

    private async void OnFilterClick(object? sender, RoutedEventArgs e)
        => await NapDuLieuAsync();

    private void LocTheoTuKhoa()
    {
        var keyword = TimKiemBox.Text?.Trim();
        _visibleRecords = string.IsNullOrWhiteSpace(keyword)
            ? _records
            : _records.Where(record =>
                record.MaGiaoDich.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || record.MaNhaCungCap.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || record.TenNhaCungCap.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (record.MaThamChieu?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (record.GhiChu?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

        LichSuGrid.ItemsSource = _visibleRecords;
        TxtTongTien.Text = $"Tổng đã thanh toán: {_visibleRecords.Sum(record => record.SoTien):N0} đ";
        TxtTongGiaoDich.Text = $"Tổng số giao dịch: {_visibleRecords.Count:N0}";
    }

    private async void OnDetailClick(object? sender, RoutedEventArgs e)
    {
        if (LichSuGrid.SelectedItem is not ThanhToanCongNoNhaCungCap record)
        {
            TxtTrangThai.Text = "Vui lòng chọn một giao dịch cần xem chi tiết.";
            return;
        }

        try
        {
            var allocations = await _database.GetPhanBoThanhToanNhaCungCapAsync(record.MaGiaoDich);
            var detail = new StringBuilder()
                .AppendLine($"Mã giao dịch: {record.MaGiaoDich}")
                .AppendLine($"Nhà cung cấp: {record.MaNhaCungCap} - {record.TenNhaCungCap}")
                .AppendLine($"Thời gian: {record.ThoiGianText}")
                .AppendLine($"Công nợ trước: {record.CongNoTruoc:N0} đ")
                .AppendLine($"Số tiền thanh toán: {record.SoTien:N0} đ")
                .AppendLine($"Công nợ sau: {record.CongNoSau:N0} đ")
                .AppendLine($"Phương thức: {record.PhuongThucHienThi}")
                .AppendLine($"Mã tham chiếu: {record.MaThamChieuHienThi}")
                .AppendLine($"Người thực hiện: {record.NguoiThucHien}")
                .AppendLine($"Ghi chú: {record.GhiChuHienThi}")
                .AppendLine()
                .AppendLine("Phân bổ vào phiếu nhập (phiếu cũ trước):");

            foreach (var allocation in allocations)
                detail.AppendLine($"• {allocation.MaPhieuNhap}: {allocation.SoTien:N0} đ");

            var allocatedTotal = allocations.Sum(allocation => allocation.SoTien);
            if (record.SoTien > allocatedTotal)
                detail.AppendLine($"• Công nợ không gắn phiếu nhập: {record.SoTien - allocatedTotal:N0} đ");
            if (allocations.Count == 0 && record.SoTien <= allocatedTotal)
                detail.Append("Không có dữ liệu phân bổ phiếu nhập cho giao dịch này.");

            var dialog = new ConfirmationWindow(
                "Chi tiết giao dịch thanh toán",
                detail.ToString(),
                "Đóng",
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
                Title = "Xuất lịch sử thanh toán nhà cung cấp",
                SuggestedFileName = $"LichSuThanhToanNCC_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
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
                "Mã giao dịch", "Ngày giờ", "Mã nhà cung cấp", "Tên nhà cung cấp",
                "Đơn vị nhận", "Công nợ trước", "Số tiền thanh toán", "Công nợ sau",
                "Phương thức", "Mã tham chiếu", "Người thực hiện", "Ghi chú"
            };
            for (var index = 0; index < headers.Length; index++)
                sheet.Cell(1, index + 1).Value = headers[index];

            for (var index = 0; index < _visibleRecords.Count; index++)
            {
                var record = _visibleRecords[index];
                var row = index + 2;
                sheet.Cell(row, 1).Value = record.MaGiaoDich;
                sheet.Cell(row, 2).Value = record.ThoiGian;
                sheet.Cell(row, 3).Value = record.MaNhaCungCap;
                sheet.Cell(row, 4).Value = record.TenNhaCungCap;
                sheet.Cell(row, 5).Value = record.DonViNhan;
                sheet.Cell(row, 6).Value = record.CongNoTruoc;
                sheet.Cell(row, 7).Value = record.SoTien;
                sheet.Cell(row, 8).Value = record.CongNoSau;
                sheet.Cell(row, 9).Value = record.PhuongThucHienThi;
                sheet.Cell(row, 10).Value = record.MaThamChieu ?? "";
                sheet.Cell(row, 11).Value = record.NguoiThucHien;
                sheet.Cell(row, 12).Value = record.GhiChu ?? "";
            }

            sheet.Row(1).Style.Font.Bold = true;
            sheet.Column(2).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            sheet.Columns(6, 8).Style.NumberFormat.Format = "#,##0";
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
