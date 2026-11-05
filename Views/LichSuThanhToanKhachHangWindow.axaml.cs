using System;
using System.Collections.Generic;
using System.Globalization;
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
    private int _currentPage = 1;
    private int _pageSize = 20;
    private bool _initializingPageSize;
    private bool _updatingDateRange;
    private int _loadVersion;

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
        _initializingPageSize = true;
        CboPageSize.ItemsSource = new[] { 10, 20, 50, 100 };
        CboPageSize.SelectedItem = _pageSize;
        _initializingPageSize = false;
        Loaded += async (_, _) => await NapDuLieuAsync();
        TimKiemBox.TextChanged += (_, _) => LocTheoTuKhoa();
        TuNgayPicker.SelectedDateChanged += (_, _) => OnStartDateChanged();
        DenNgayPicker.SelectedDateChanged += (_, _) => OnEndDateChanged();
    }

    private async Task NapDuLieuAsync()
    {
        var loadVersion = ++_loadVersion;
        TxtDateError.IsVisible = false;
        EmptyState.IsVisible = false;
        LoadingIndicator.IsVisible = true;
        LoadingState.IsVisible = true;
        LichSuGrid.IsEnabled = false;
        var loadFailed = false;
        DateTime? tuNgay = TuNgayPicker.SelectedDate?.Date;
        DateTime? denNgay = DenNgayPicker.SelectedDate?.Date.AddDays(1);
        try
        {
            var records = await _database.GetLichSuThanhToanCongNoKhachHangAsync(
                _customerId,
                tuNgay,
                denNgay);
            if (loadVersion != _loadVersion)
                return;

            _records = records;
            LocTheoTuKhoa();
        }
        catch (Exception ex)
        {
            if (loadVersion != _loadVersion)
                return;

            loadFailed = true;
            _records = [];
            LocTheoTuKhoa();
            TxtDateError.Text = "Không tải được lịch sử: " + ex.Message;
            TxtDateError.IsVisible = true;
        }
        finally
        {
            if (loadVersion == _loadVersion)
            {
                LoadingIndicator.IsVisible = false;
                LoadingState.IsVisible = false;
                LichSuGrid.IsEnabled = true;
                EmptyState.IsVisible = _visibleRecords.Count == 0 && !loadFailed;
            }
        }
    }

    private async void OnFilterClick(object? sender, RoutedEventArgs e)
    {
        if (!KiemTraKhoangNgay())
            return;

        await NapDuLieuAsync();
    }

    private async void OnLamMoiClick(object? sender, RoutedEventArgs e)
    {
        _updatingDateRange = true;
        TuNgayPicker.SelectedDate = null;
        DenNgayPicker.SelectedDate = null;
        _updatingDateRange = false;
        TimKiemBox.Text = "";
        TxtDateError.IsVisible = false;
        await NapDuLieuAsync();
    }

    private void LocTheoTuKhoa()
    {
        var keyword = ChuanHoaTuKhoa(TimKiemBox.Text);
        _visibleRecords = string.IsNullOrWhiteSpace(keyword)
            ? _records
            : _records.Where(record =>
                ChuanHoaTuKhoa(record.MaGiaoDich).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || ChuanHoaTuKhoa(record.MaThamChieu).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || ChuanHoaTuKhoa(record.PhuongThucHienThi).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || ChuanHoaTuKhoa(record.NguoiThucHien).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || ChuanHoaTuKhoa(record.GhiChu).Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

        _currentPage = 1;
        TxtTongTien.Text = $"{_visibleRecords.Where(record => !record.DaHuy).Sum(record => record.SoTien):N0} đ";
        TxtTongGiaoDich.Text = $"{_visibleRecords.Count:N0}";
        TxtTrangThai.Text =
            $"{_visibleRecords.Count(record => !record.DaHuy):N0} thành công  /  "
            + $"{_visibleRecords.Count(record => record.DaHuy):N0} đã hủy";
        CapNhatTrangHienTai();
        EmptyState.IsVisible = _visibleRecords.Count == 0 && !LoadingIndicator.IsVisible;
    }

    private bool KiemTraKhoangNgay()
    {
        var from = TuNgayPicker.SelectedDate?.Date;
        var to = DenNgayPicker.SelectedDate?.Date;
        if ((!string.IsNullOrWhiteSpace(TuNgayPicker.Text) && from is null)
            || (!string.IsNullOrWhiteSpace(DenNgayPicker.Text) && to is null))
        {
            TxtDateError.Text = "Ngày nhập không hợp lệ. Hãy chọn ngày trên lịch hoặc nhập theo DD/MM/YYYY.";
            TxtDateError.IsVisible = true;
            return false;
        }

        if (from is not null && to is not null && from > to)
        {
            TxtDateError.Text = "Ngày bắt đầu không được lớn hơn ngày kết thúc.";
            TxtDateError.IsVisible = true;
            return false;
        }

        TxtDateError.IsVisible = false;
        return true;
    }

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
        await NapDuLieuAsync();
    }

    private void OnStartDateChanged()
    {
        if (_updatingDateRange)
            return;

        if (TuNgayPicker.SelectedDate is { } from
            && DenNgayPicker.SelectedDate is { } to
            && from.Date > to.Date)
        {
            _updatingDateRange = true;
            DenNgayPicker.SelectedDate = from.Date;
            _updatingDateRange = false;
        }

        TxtDateError.IsVisible = false;
    }

    private void OnEndDateChanged()
    {
        if (_updatingDateRange)
            return;

        if (TuNgayPicker.SelectedDate is { } from
            && DenNgayPicker.SelectedDate is { } to
            && to.Date < from.Date)
        {
            _updatingDateRange = true;
            TuNgayPicker.SelectedDate = to.Date;
            _updatingDateRange = false;
        }

        TxtDateError.IsVisible = false;
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        LocTheoTuKhoa();
        e.Handled = true;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        BtnDetail.IsEnabled = LichSuGrid.SelectedItem is ThanhToanCongNoKhachHang;
    }

    private void OnRowDetailClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ThanhToanCongNoKhachHang record })
        {
            LichSuGrid.SelectedItem = record;
            OnDetailClick(sender, e);
        }
    }

    private void OnPageSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initializingPageSize || CboPageSize.SelectedItem is not int pageSize)
            return;

        _pageSize = pageSize;
        _currentPage = 1;
        CapNhatTrangHienTai();
    }

    private void OnFirstPageClick(object? sender, RoutedEventArgs e) => ChuyenTrang(1);

    private void OnPreviousPageClick(object? sender, RoutedEventArgs e) => ChuyenTrang(_currentPage - 1);

    private void OnNextPageClick(object? sender, RoutedEventArgs e) => ChuyenTrang(_currentPage + 1);

    private void OnLastPageClick(object? sender, RoutedEventArgs e) =>
        ChuyenTrang(TongSoTrang);

    private int TongSoTrang => Math.Max(1, (int)Math.Ceiling(_visibleRecords.Count / (double)_pageSize));

    private void ChuyenTrang(int page)
    {
        _currentPage = Math.Clamp(page, 1, TongSoTrang);
        CapNhatTrangHienTai();
    }

    private void CapNhatTrangHienTai()
    {
        var skip = (_currentPage - 1) * _pageSize;
        var pageRecords = _visibleRecords.Skip(skip).Take(_pageSize).ToArray();
        for (var index = 0; index < pageRecords.Length; index++)
            pageRecords[index].SoThuTuHienThi = skip + index + 1;

        LichSuGrid.ItemsSource = pageRecords;
        LichSuGrid.SelectedItem = null;
        BtnDetail.IsEnabled = false;
        TxtPage.Text = $"Trang {_currentPage:N0} / {TongSoTrang:N0}";
        var first = _visibleRecords.Count == 0 ? 0 : skip + 1;
        var last = Math.Min(skip + _pageSize, _visibleRecords.Count);
        TxtResultRange.Text = $"Hiển thị {first:N0}–{last:N0} / {_visibleRecords.Count:N0} giao dịch";
    }

    private static string ChuanHoaTuKhoa(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Replace('đ', 'd').Replace('Đ', 'D')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LichSuGrid.SelectedItem is not null)
            OnDetailClick(sender, e);
    }

    private async void OnDetailClick(object? sender, RoutedEventArgs e)
    {
        if (LichSuGrid.SelectedItem is not ThanhToanCongNoKhachHang record)
        {
            return;
        }

        try
        {
            var allocations = await _database.GetPhanBoThanhToanKhachHangAsync(record.MaGiaoDich);
            var dialog = new ConfirmationWindow(
                "Chi tiết giao dịch thanh toán khách hàng",
                TaoNoiDungChiTiet(record, allocations),
                "Đóng");
            await dialog.ShowDialog<bool>(this);
        }
        catch (Exception ex)
        {
            TxtDateError.Text = "Không đọc được chi tiết giao dịch: " + ex.Message;
            TxtDateError.IsVisible = true;
        }
    }

    private string TaoNoiDungChiTiet(
        ThanhToanCongNoKhachHang record,
        IReadOnlyList<PhanBoThanhToanKhachHang> allocations)
    {
        var detail = new StringBuilder();
        ThemThongTinGiaoDich(detail, record);
        ThemThongTinCongNo(detail, record);
        ThemThongTinHuy(detail, record);
        ThemPhanBoHoaDon(detail, record, allocations);
        return detail.ToString();
    }

    private void ThemThongTinGiaoDich(StringBuilder detail, ThanhToanCongNoKhachHang record)
    {
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
    }

    private static void ThemThongTinCongNo(StringBuilder detail, ThanhToanCongNoKhachHang record)
    {
        detail.AppendLine("══════════ TÌNH TRẠNG CÔNG NỢ ══════════");
        detail.AppendLine($"Công nợ trước thanh toán: {record.CongNoTruoc:N0} đ");
        detail.AppendLine($"Số tiền thanh toán:       {record.SoTien:N0} đ");
        detail.AppendLine($"Công nợ sau thanh toán:   {record.CongNoSau:N0} đ");
        detail.AppendLine($"Trạng thái giao dịch: {record.TrangThaiHienThi}");
        detail.AppendLine();
    }

    private static void ThemThongTinHuy(StringBuilder detail, ThanhToanCongNoKhachHang record)
    {
        if (record.DaHuy)
        {
            detail.AppendLine("══════════ THÔNG TIN HỦY GIAO DỊCH ══════════");
            detail.AppendLine("Trạng thái: ĐÃ HỦY (Đã hoàn tác công nợ)");
            detail.AppendLine($"Người hủy: {record.NguoiHuy}");
            detail.AppendLine($"Thời gian hủy: {record.ThoiGianHuyText}");
            detail.AppendLine($"Lý do hủy: {record.LyDoHuy}");
        }
        else
        {
            detail.AppendLine("Trạng thái: Hoàn thành");
            detail.AppendLine($"Liên kết Sổ quỹ: Phiếu thu {record.MaGiaoDich} (Đã ghi vào sổ quỹ)");
        }

        detail.AppendLine();
    }

    private static void ThemPhanBoHoaDon(
        StringBuilder detail,
        ThanhToanCongNoKhachHang record,
        IReadOnlyList<PhanBoThanhToanKhachHang> allocations)
    {
        detail.AppendLine("══════════ PHÂN BỔ VÀO HÓA ĐƠN BÁN HÀNG ══════════");
        for (var index = 0; index < allocations.Count; index++)
        {
            var allocation = allocations[index];
            detail.AppendLine($"  {index + 1}. Hóa đơn {allocation.MaHoaDon}: {allocation.SoTien:N0} đ");
        }

        if (allocations.Count == 0)
        {
            detail.AppendLine("Không có dữ liệu phân bổ hóa đơn cho giao dịch này (thu công nợ tổng).");
            return;
        }

        var allocatedTotal = allocations.Sum(allocation => allocation.SoTien);
        detail.AppendLine($"Tổng tiền đã phân bổ: {allocatedTotal:N0} đ ({allocations.Count} hóa đơn)");
        if (record.SoTien > allocatedTotal)
            detail.AppendLine($"Số tiền thanh toán chưa gắn hóa đơn: {record.SoTien - allocatedTotal:N0} đ");
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await ChonFileXuatAsync();
            if (file is null)
                return;

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Lịch sử thanh toán");
            DienThongTinBaoCao(sheet);
            DienDanhSachGiaoDich(sheet);
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

    private void DienThongTinBaoCao(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "LỊCH SỬ THANH TOÁN CÔNG NỢ KHÁCH HÀNG";
        sheet.Cell(2, 1).Value = $"Khách hàng: {_customerCode} - {_customerName}";
        sheet.Cell(3, 1).Value =
            $"Khoảng thời gian: {TuNgayPicker.SelectedDate?.ToString("dd/MM/yyyy") ?? "Tất cả"}"
            + $" - {DenNgayPicker.SelectedDate?.ToString("dd/MM/yyyy") ?? "Tất cả"}";
        sheet.Cell(4, 1).Value =
            $"Tổng đã thanh toán: {_visibleRecords.Where(record => !record.DaHuy).Sum(record => record.SoTien):N0} đ"
            + $" | Tổng số giao dịch: {_visibleRecords.Count:N0}";

        var headers = LayTieuDeBaoCao();
        for (var index = 0; index < headers.Length; index++)
            sheet.Cell(6, index + 1).Value = headers[index];
    }

    private void DienDanhSachGiaoDich(IXLWorksheet sheet)
    {
        for (var index = 0; index < _visibleRecords.Count; index++)
        {
            var record = _visibleRecords[index];
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

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
