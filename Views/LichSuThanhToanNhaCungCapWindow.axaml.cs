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

public partial class LichSuThanhToanNhaCungCapWindow : Window
{
    private readonly DatabaseService _database;
    private readonly uint? _initialSupplierId;
    private readonly string _supplierCode;
    private readonly string _supplierName;
    private IReadOnlyList<ThanhToanCongNoNhaCungCap> _visibleRecords = [];
    private ThanhToanCongNoLichSuThongKe _summary = new();
    private string _appliedKeyword = "";
    private int _totalCount;
    private int _currentPage = 1;
    private int _pageSize = 20;
    private bool _initializingPageSize;
    private bool _initializingSupplier;
    private bool _updatingDateRange;
    private int _loadVersion;

    public LichSuThanhToanNhaCungCapWindow()
        : this(new DatabaseService(), null, "", "Nhà cung cấp")
    {
    }

    public LichSuThanhToanNhaCungCapWindow(
        DatabaseService database,
        uint? initialSupplierId,
        string supplierName)
        : this(database, initialSupplierId, "", supplierName)
    {
    }

    public LichSuThanhToanNhaCungCapWindow(
        DatabaseService database,
        uint? initialSupplierId,
        string supplierCode,
        string supplierName)
    {
        _database = database;
        _initialSupplierId = initialSupplierId;
        _supplierCode = supplierCode;
        _supplierName = supplierName;

        InitializeComponent();
        TxtNhaCungCap.Text = !initialSupplierId.HasValue && string.IsNullOrWhiteSpace(supplierCode)
            ? (string.IsNullOrWhiteSpace(supplierName) || supplierName == "Nhà cung cấp" ? "Tất cả nhà cung cấp" : supplierName)
            : (string.IsNullOrWhiteSpace(supplierCode)
                ? (string.IsNullOrWhiteSpace(supplierName) ? "Tất cả nhà cung cấp" : supplierName)
                : (supplierName.StartsWith(supplierCode, StringComparison.OrdinalIgnoreCase) ? supplierName : $"{supplierCode} - {supplierName}"));

        LichSuGrid.ItemsSource = Array.Empty<ThanhToanCongNoNhaCungCap>();
        _initializingPageSize = true;
        CboPageSize.ItemsSource = new[] { 10, 20, 50, 100 };
        CboPageSize.SelectedItem = _pageSize;
        _initializingPageSize = false;
        Loaded += async (_, _) => await NapDuLieuAsync();
    }

    private async Task NapDuLieuAsync()
    {
        var loadVersion = ++_loadVersion;
        TxtTrangThai.Text = "";
        try
        {
            await NapDanhSachNhaCungCapAsync();

            if (!KiemTraKhoangNgay())
                return;

            DateTime? from = TuNgayPicker.SelectedDate?.Date;
            DateTime? toExclusive = DenNgayPicker.SelectedDate?.Date.AddDays(1);
            var supplierId = (CboNhaCungCap.SelectedItem as NhaCungCapThanhToanLuaChon)?.Id;
            var (summary, records) = await TaiTrangAsync(
                supplierId, from, toExclusive, _appliedKeyword, _currentPage, _pageSize);
            if (loadVersion == _loadVersion)
                HienThiTrang(summary, records);
        }
        catch (Exception ex)
        {
            if (loadVersion == _loadVersion)
            {
                XoaDuLieuTrang();
                TxtTrangThai.Text = "Không tải được lịch sử: " + ex.Message;
            }
        }
    }

    private async void OnFilterClick(object? sender, RoutedEventArgs e)
    {
        _appliedKeyword = TimKiemBox.Text?.Trim() ?? "";
        _currentPage = 1;
        await NapDuLieuAsync();
    }

    private async void OnLamMoiClick(object? sender, RoutedEventArgs e)
    {
        TuNgayPicker.SelectedDate = null;
        DenNgayPicker.SelectedDate = null;
        TimKiemBox.Text = "";
        if (CboNhaCungCap.ItemsSource is List<NhaCungCapThanhToanLuaChon> options && options.Count > 0)
        {
            _initializingSupplier = true;
            try
            {
                var matched = options.FirstOrDefault(option =>
                {
                    if (_initialSupplierId.HasValue)
                        return option.Id == _initialSupplierId;
                    if (!string.IsNullOrWhiteSpace(_supplierCode) && string.Equals(option.MaNhaCungCap, _supplierCode, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (!string.IsNullOrWhiteSpace(_supplierName) && string.Equals(option.TenNhaCungCap, _supplierName, StringComparison.OrdinalIgnoreCase))
                        return true;
                    return false;
                }) ?? options[0];

                CboNhaCungCap.SelectedItem = matched;
                TxtNhaCungCap.Text = matched.Id.HasValue ? matched.TenNhaCungCap : "Tất cả nhà cung cấp";
            }
            finally
            {
                _initializingSupplier = false;
            }
        }
        _appliedKeyword = "";
        _currentPage = 1;
        await NapDuLieuAsync();
    }

    private async void OnNhaCungCapSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initializingSupplier)
            return;

        if (CboNhaCungCap.SelectedItem is not NhaCungCapThanhToanLuaChon selected)
            return;

        TxtNhaCungCap.Text = selected.Id.HasValue
            ? selected.TenNhaCungCap
            : "Tất cả nhà cung cấp";

        _currentPage = 1;
        await NapDuLieuAsync();
    }

    private async Task NapDanhSachNhaCungCapAsync()
    {
        if (CboNhaCungCap.ItemsSource is not null)
            return;

        _initializingSupplier = true;
        try
        {
            var suppliers = await _database.GetNhaCungCapAsync();
            var options = new List<NhaCungCapThanhToanLuaChon>
            {
                new()
                {
                    Id = null,
                    MaNhaCungCap = "",
                    TenNhaCungCap = "Tất cả nhà cung cấp",
                    TenHienThi = "Tất cả nhà cung cấp"
                }
            };
            options.AddRange(suppliers.Select(supplier =>
            {
                var ten = string.IsNullOrWhiteSpace(supplier.TenNhaCungCap)
                    ? (supplier.MaNhaCungCap ?? "Nhà cung cấp")
                    : supplier.TenNhaCungCap.Trim();
                var ma = (supplier.MaNhaCungCap ?? "").Trim();
                var hienThi = string.IsNullOrWhiteSpace(ma) || ten.Contains(ma, StringComparison.OrdinalIgnoreCase)
                    ? ten
                    : $"{ma} - {ten}";

                return new NhaCungCapThanhToanLuaChon
                {
                    Id = supplier.Id,
                    MaNhaCungCap = ma,
                    TenNhaCungCap = ten,
                    TenHienThi = hienThi
                };
            }));

            CboNhaCungCap.ItemsSource = options;
            var matched = options.FirstOrDefault(option =>
            {
                if (_initialSupplierId.HasValue)
                    return option.Id == _initialSupplierId;
                if (!string.IsNullOrWhiteSpace(_supplierCode) && string.Equals(option.MaNhaCungCap, _supplierCode, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (!string.IsNullOrWhiteSpace(_supplierName) && string.Equals(option.TenNhaCungCap, _supplierName, StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }) ?? options[0];

            CboNhaCungCap.SelectedItem = matched;
        }
        finally
        {
            _initializingSupplier = false;
        }
    }

    private async Task<(ThanhToanCongNoLichSuThongKe Summary, IReadOnlyList<ThanhToanCongNoNhaCungCap> Records)> TaiTrangAsync(
        uint? supplierId,
        DateTime? tuNgay,
        DateTime? denNgay,
        string keyword,
        int page,
        int pageSize)
    {
        var summaryTask = _database.GetThongKeLichSuThanhToanCongNoNhaCungCapAsync(
            supplierId, tuNgay, denNgay, keyword);
        var recordsTask = _database.GetLichSuThanhToanCongNoNhaCungCapAsync(
            supplierId, tuNgay, denNgay, keyword, pageSize, (page - 1) * pageSize);
        await Task.WhenAll(summaryTask, recordsTask);
        return (await summaryTask, await recordsTask);
    }

    private void HienThiTrang(
        ThanhToanCongNoLichSuThongKe summary,
        IReadOnlyList<ThanhToanCongNoNhaCungCap> records)
    {
        _summary = summary;
        _totalCount = summary.TongSoGiaoDich;
        _currentPage = Math.Clamp(_currentPage, 1, TongSoTrang);
        _visibleRecords = records;
        LichSuGrid.ItemsSource = _visibleRecords;
        LichSuGrid.SelectedItem = null;
        BtnDetail.IsEnabled = false;
        CapNhatThongKe();
        CapNhatPhanTrang();
    }

    private void XoaDuLieuTrang()
    {
        _summary = new ThanhToanCongNoLichSuThongKe();
        _totalCount = 0;
        _visibleRecords = [];
        LichSuGrid.ItemsSource = _visibleRecords;
        CapNhatThongKe();
        CapNhatPhanTrang();
    }

    private void CapNhatThongKe()
    {
        TxtTongTien.Text = $"{_summary.TongTienDaThanhToan:N0} đ";
        TxtTongGiaoDich.Text = $"{_summary.TongSoGiaoDich:N0}";
        TxtCongNoTruoc.Text = $"{_summary.CongNoTruocThanhToan:N0} đ";
        TxtCongNoSau.Text = $"{_summary.CongNoSauThanhToan:N0} đ";
    }

    private int TongSoTrang => Math.Max(1, (int)Math.Ceiling(_totalCount / (double)_pageSize));

    private void CapNhatPhanTrang()
    {
        TxtPage.Text = $"Trang {_currentPage:N0} / {TongSoTrang:N0}";
        var first = _totalCount == 0 ? 0 : (_currentPage - 1) * _pageSize + 1;
        var last = Math.Min((_currentPage - 1) * _pageSize + _visibleRecords.Count, _totalCount);
        TxtResultRange.Text = $"Hiển thị {first:N0}–{last:N0} / {_totalCount:N0} giao dịch";
    }

    private async Task ChuyenTrangAsync(int page)
    {
        _currentPage = Math.Clamp(page, 1, TongSoTrang);
        await NapDuLieuAsync();
    }

    private async void OnPageSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initializingPageSize || CboPageSize.SelectedItem is not int pageSize)
            return;
        _pageSize = pageSize;
        _currentPage = 1;
        await NapDuLieuAsync();
    }

    private async void OnFirstPageClick(object? sender, RoutedEventArgs e) => await ChuyenTrangAsync(1);
    private async void OnPreviousPageClick(object? sender, RoutedEventArgs e) => await ChuyenTrangAsync(_currentPage - 1);
    private async void OnNextPageClick(object? sender, RoutedEventArgs e) => await ChuyenTrangAsync(_currentPage + 1);
    private async void OnLastPageClick(object? sender, RoutedEventArgs e) => await ChuyenTrangAsync(TongSoTrang);

    private async void OnQuickDateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string preset })
            return;
        if (preset == "custom")
        {
            TuNgayPicker.Focus();
            TuNgayPicker.IsDropDownOpen = true;
            return;
        }

        var today = DateTime.Today;
        var (from, to) = preset switch
        {
            "today" => (today, today),
            "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
            "last7" => (today.AddDays(-6), today),
            "last30" => (today.AddDays(-29), today),
            "thisMonth" => (new DateTime(today.Year, today.Month, 1), today),
            "lastMonth" => (
                new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            _ => (today, today)
        };
        _updatingDateRange = true;
        TuNgayPicker.SelectedDate = from;
        DenNgayPicker.SelectedDate = to;
        _updatingDateRange = false;
        _appliedKeyword = TimKiemBox.Text?.Trim() ?? "";
        _currentPage = 1;
        await NapDuLieuAsync();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        OnFilterClick(sender, new RoutedEventArgs());
        e.Handled = true;
    }

    private void OnDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingDateRange)
            return;
        if (TuNgayPicker.SelectedDate is { } from
            && DenNgayPicker.SelectedDate is { } to
            && from.Date > to.Date)
        {
            _updatingDateRange = true;
            if (ReferenceEquals(sender, TuNgayPicker))
                DenNgayPicker.SelectedDate = from.Date;
            else
                TuNgayPicker.SelectedDate = to.Date;
            _updatingDateRange = false;
        }
    }

    private bool KiemTraKhoangNgay()
    {
        var from = TuNgayPicker.SelectedDate?.Date;
        var to = DenNgayPicker.SelectedDate?.Date;
        if ((!string.IsNullOrWhiteSpace(TuNgayPicker.Text) && from is null)
            || (!string.IsNullOrWhiteSpace(DenNgayPicker.Text) && to is null))
        {
            TxtTrangThai.Text = "Ngày nhập không hợp lệ. Hãy chọn ngày theo DD/MM/YYYY.";
            return false;
        }

        if (from is not null && to is not null && from > to)
        {
            TxtTrangThai.Text = "Ngày bắt đầu không được sau ngày kết thúc.";
            return false;
        }

        return true;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        BtnDetail.IsEnabled = LichSuGrid.SelectedItem is ThanhToanCongNoNhaCungCap;
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        OnDetailClick(sender, e);
    }

    private void OnRowDetailClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ThanhToanCongNoNhaCungCap record })
        {
            LichSuGrid.SelectedItem = record;
            OnDetailClick(sender, e);
        }
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
            TxtTrangThai.Text = "";
            var allocations = await _database.GetPhanBoThanhToanNhaCungCapAsync(record.MaGiaoDich);
            var dialog = new ConfirmationWindow(
                "Chi tiết giao dịch thanh toán nhà cung cấp",
                TaoNoiDungChiTiet(record, allocations),
                "Đóng");
            await dialog.ShowDialog<bool>(this);
        }
        catch (Exception ex)
        {
            TxtTrangThai.Text = "Không đọc được chi tiết giao dịch: " + ex.Message;
        }
    }

    private static string TaoNoiDungChiTiet(
        ThanhToanCongNoNhaCungCap record,
        IReadOnlyList<PhanBoThanhToanNhaCungCap> allocations)
    {
        var detail = new StringBuilder();
        detail.AppendLine($"Mã giao dịch / Phiếu chi: {record.MaGiaoDich}");
        detail.AppendLine($"Nhà cung cấp: {record.MaNhaCungCap} - {record.TenNhaCungCap}");
        detail.AppendLine($"Đơn vị nhận: {record.DonViNhan}");
        detail.AppendLine($"Ngày giờ thanh toán: {record.ThoiGianText}");
        detail.AppendLine($"Công nợ trước thanh toán: {record.CongNoTruoc:N0} đ");
        detail.AppendLine($"Số tiền thanh toán: {record.SoTien:N0} đ");
        detail.AppendLine($"Công nợ sau thanh toán: {record.CongNoSau:N0} đ");
        detail.AppendLine($"Phương thức thanh toán: {record.PhuongThucHienThi}");
        detail.AppendLine($"Nguồn quỹ: {LayNguonThanhToan(record)}");
        detail.AppendLine($"Mã tham chiếu: {record.MaThamChieuHienThi}");
        detail.AppendLine($"Trạng thái: {record.TrangThaiHienThi}");
        detail.AppendLine($"Người thực hiện: {record.NguoiThucHien}");
        detail.AppendLine($"Nội dung, ghi chú: {record.GhiChuHienThi}");
        if (record.DaHuy)
        {
            detail.AppendLine($"Người hủy: {record.NguoiHuy}");
            detail.AppendLine($"Thời gian hủy: {record.ThoiGianHuyText}");
            detail.AppendLine($"Lý do hủy: {record.LyDoHuy}");
        }

        ThemThongTinPhanBo(detail, record, allocations);
        return detail.ToString();
    }

    private static string LayNguonThanhToan(ThanhToanCongNoNhaCungCap record)
    {
        if (record.DaHuy)
            return $"Giao dịch phiếu chi {record.MaGiaoDich} đã được hủy/hoàn tác";
        return record.PhuongThuc.Trim().Equals("CAN_TRU", StringComparison.OrdinalIgnoreCase)
            ? "Cấn trừ khoản trả trước"
            : $"Sổ quỹ / phiếu chi {record.MaGiaoDich}";
    }

    private static void ThemThongTinPhanBo(
        StringBuilder detail,
        ThanhToanCongNoNhaCungCap record,
        IReadOnlyList<PhanBoThanhToanNhaCungCap> allocations)
    {
        detail.AppendLine();
        detail.AppendLine("Phân bổ vào phiếu nhập hàng:");
        for (var index = 0; index < allocations.Count; index++)
        {
            var allocation = allocations[index];
            detail.AppendLine($"{index + 1}. Phiếu nhập {allocation.MaPhieuNhap}: {allocation.SoTien:N0} đ");
        }

        if (allocations.Count == 0)
        {
            detail.AppendLine("Không có dữ liệu phân bổ phiếu nhập cho giao dịch này.");
            return;
        }

        var allocatedTotal = allocations.Sum(allocation => allocation.SoTien);
        detail.AppendLine($"Tổng tiền đã phân bổ: {allocatedTotal:N0} đ");
        if (record.SoTien > allocatedTotal)
            detail.AppendLine($"Số tiền chưa gắn phiếu nhập: {record.SoTien - allocatedTotal:N0} đ");
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!KiemTraKhoangNgay())
                return;

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

            var supplierId = (CboNhaCungCap.SelectedItem as NhaCungCapThanhToanLuaChon)?.Id;
            var from = TuNgayPicker.SelectedDate?.Date;
            var to = DenNgayPicker.SelectedDate?.Date.AddDays(1);
            var (summary, records) = await TaiToanBoDuLieuXuatAsync(
                supplierId, from, to, _appliedKeyword);
            TaoTepExcel(file.Path.LocalPath, summary, records);
            TxtTrangThai.Text = "Đã xuất lịch sử thanh toán.";
        }
        catch (Exception ex)
        {
            TxtTrangThai.Text = "Không xuất được Excel: " + ex.Message;
        }
    }

    private async Task<(ThanhToanCongNoLichSuThongKe Summary, IReadOnlyList<ThanhToanCongNoNhaCungCap> Records)> TaiToanBoDuLieuXuatAsync(
        uint? supplierId,
        DateTime? from,
        DateTime? to,
        string keyword)
    {
        var summary = await _database.GetThongKeLichSuThanhToanCongNoNhaCungCapAsync(
            supplierId, from, to, keyword);
        var records = new List<ThanhToanCongNoNhaCungCap>();
        const int batchSize = 500;
        for (var offset = 0; offset < summary.TongSoGiaoDich; offset += batchSize)
        {
            var batch = await _database.GetLichSuThanhToanCongNoNhaCungCapAsync(
                supplierId, from, to, keyword, batchSize, offset);
            if (batch.Count == 0)
                break;
            records.AddRange(batch);
        }

        return (summary, records);
    }

    private static void TaoTepExcel(
        string path,
        ThanhToanCongNoLichSuThongKe summary,
        IReadOnlyList<ThanhToanCongNoNhaCungCap> records)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Lịch sử thanh toán");
        var headers = new[]
        {
            "Mã giao dịch", "Ngày giờ", "Mã nhà cung cấp", "Tên nhà cung cấp",
            "Đơn vị nhận", "Công nợ trước", "Số tiền thanh toán", "Công nợ sau",
            "Phương thức", "Mã tham chiếu", "Trạng thái", "Người thực hiện", "Ghi chú"
        };
        for (var index = 0; index < headers.Length; index++)
            sheet.Cell(1, index + 1).Value = headers[index];
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
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
            sheet.Cell(row, 10).Value = record.MaThamChieuHienThi;
            sheet.Cell(row, 11).Value = record.TrangThaiHienThi;
            sheet.Cell(row, 12).Value = record.NguoiThucHien;
            sheet.Cell(row, 13).Value = record.GhiChuHienThi;
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Cell(1, 14).Value = "Tổng số giao dịch";
        sheet.Cell(1, 15).Value = summary.TongSoGiaoDich;
        sheet.Cell(2, 14).Value = "Tổng tiền đã thanh toán";
        sheet.Cell(2, 15).Value = summary.TongTienDaThanhToan;
        sheet.Cell(3, 14).Value = "Công nợ trước thanh toán";
        sheet.Cell(3, 15).Value = summary.CongNoTruocThanhToan;
        sheet.Cell(4, 14).Value = "Công nợ sau thanh toán";
        sheet.Cell(4, 15).Value = summary.CongNoSauThanhToan;
        sheet.Column(2).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        sheet.Columns(6, 8).Style.NumberFormat.Format = "#,##0";
        sheet.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
