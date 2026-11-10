using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class LichSuThanhToanKhachHangWindow : Window
{
    private readonly DatabaseService _database;
    private readonly uint _customerId;
    private readonly string _customerCode;
    private readonly string _customerName;
    private IReadOnlyList<ThanhToanCongNoKhachHang> _visibleRecords = [];
    private ThanhToanCongNoLichSuThongKe _summary = new();
    private string _appliedKeyword = "";
    private int _totalCount;
    private int _currentPage = 1;
    private int _pageSize = 20;
    private bool _initializingPageSize;
    private bool _updatingDateRange;
    private int _loadVersion;

    public bool CoThayDoi { get; private set; }

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
        TuNgayPicker.SelectedDateChanged += (_, _) => OnStartDateChanged();
        DenNgayPicker.SelectedDateChanged += (_, _) => OnEndDateChanged();
    }

    private async Task NapDuLieuAsync()
    {
        var loadVersion = ++_loadVersion;
        BatDauTaiDuLieu();
        try
        {
            await TaiTrangHienTaiAsync(loadVersion);
        }
        catch (Exception ex)
        {
            if (loadVersion != _loadVersion)
                return;

            HienThiLoiTaiDuLieu(ex);
        }
        finally
        {
            if (loadVersion == _loadVersion)
                KetThucTaiDuLieu();
        }
    }

    private void BatDauTaiDuLieu()
    {
        TxtDateError.IsVisible = false;
        EmptyState.IsVisible = false;
        LoadingIndicator.IsVisible = true;
        LoadingState.IsVisible = true;
        LichSuGrid.IsEnabled = false;
    }

    private async Task TaiTrangHienTaiAsync(int loadVersion)
    {
        var tuNgay = TuNgayPicker.SelectedDate?.Date;
        var denNgay = DenNgayPicker.SelectedDate?.Date.AddDays(1);
        var (summary, records) = await TaiTrangAsync(
            tuNgay, denNgay, _appliedKeyword, _currentPage, _pageSize);
        if (loadVersion == _loadVersion)
            HienThiTrang(summary, records);
    }

    private void HienThiLoiTaiDuLieu(Exception exception)
    {
        XoaDuLieuTrang();
        TxtDateError.Text = "Không tải được lịch sử: " + exception.Message;
        TxtDateError.IsVisible = true;
    }

    private void KetThucTaiDuLieu()
    {
        LoadingIndicator.IsVisible = false;
        LoadingState.IsVisible = false;
        LichSuGrid.IsEnabled = true;
        EmptyState.IsVisible = _totalCount == 0 && !TxtDateError.IsVisible;
    }

    private async Task<(ThanhToanCongNoLichSuThongKe Summary, IReadOnlyList<ThanhToanCongNoKhachHang> Records)> TaiTrangAsync(
            DateTime? tuNgay,
            DateTime? denNgay,
            string keyword,
            int page,
            int pageSize)
    {
        var summaryTask = _database.GetThongKeLichSuThanhToanCongNoKhachHangAsync(
            _customerId, tuNgay, denNgay, keyword);
        var recordsTask = _database.GetLichSuThanhToanCongNoKhachHangAsync(
            _customerId, tuNgay, denNgay, keyword, pageSize, (page - 1) * pageSize);
        await Task.WhenAll(summaryTask, recordsTask);
        return (await summaryTask, await recordsTask);
    }

    private void HienThiTrang(
            ThanhToanCongNoLichSuThongKe summary,
            IReadOnlyList<ThanhToanCongNoKhachHang> records)
    {
        _summary = summary;
        _totalCount = summary.TongSoGiaoDich;
        _currentPage = Math.Clamp(_currentPage, 1, TongSoTrang);
        _visibleRecords = records;
        var firstIndex = (_currentPage - 1) * _pageSize;
        for (var index = 0; index < records.Count; index++)
            records[index].SoThuTuHienThi = firstIndex + index + 1;
        CapNhatThongKe();
        CapNhatTrangHienTai();
    }

    private void XoaDuLieuTrang()
    {
        _visibleRecords = [];
        _totalCount = 0;
        _summary = new ThanhToanCongNoLichSuThongKe();
        CapNhatThongKe();
        CapNhatTrangHienTai();
    }

    private async void OnFilterClick(object? sender, RoutedEventArgs e)
    {
        if (!KiemTraKhoangNgay())
            return;

        _appliedKeyword = TimKiemBox.Text?.Trim() ?? "";
        _currentPage = 1;
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
        _appliedKeyword = "";
        _currentPage = 1;
        await NapDuLieuAsync();
    }

    private void CapNhatThongKe()
    {
        TxtTongTien.Text = $"{_summary.TongTienDaThanhToan:N0} đ";
        TxtTongGiaoDich.Text = $"{_summary.TongSoGiaoDich:N0}";
        TxtCongNoTruoc.Text = $"{_summary.CongNoTruocThanhToan:N0} đ";
        TxtCongNoSau.Text = $"{_summary.CongNoSauThanhToan:N0} đ";
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

    private async void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (KiemTraKhoangNgay())
        {
            _appliedKeyword = TimKiemBox.Text?.Trim() ?? "";
            _currentPage = 1;
            await NapDuLieuAsync();
        }
        e.Handled = true;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = LichSuGrid.SelectedItem as ThanhToanCongNoKhachHang;
        BtnDetail.IsEnabled = selected is not null;
        BtnHuy.IsEnabled = selected is { CoTheHuy: true };
    }

    private void OnRowDetailClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ThanhToanCongNoKhachHang record })
        {
            LichSuGrid.SelectedItem = record;
            OnDetailClick(sender, e);
        }
    }

    private void OnRowHuyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ThanhToanCongNoKhachHang record })
        {
            LichSuGrid.SelectedItem = record;
            OnHuyClick(sender, e);
        }
    }

    private async void OnHuyClick(object? sender, RoutedEventArgs e)
    {
        if (LichSuGrid.SelectedItem is not ThanhToanCongNoKhachHang record)
            return;

        if (record.DaHuy)
        {
            var dialogBao = new ConfirmationWindow(
                "Không thể hủy phiếu",
                $"Phiếu thanh toán {record.MaGiaoDich} đã được hủy trước đó vào lúc {record.ThoiGianHuyText}.",
                "Đóng");
            await dialogBao.ShowDialog<bool>(this);
            return;
        }

        var huyWindow = new HuyThanhToanCongNoWindow(record, $"{_customerCode} - {_customerName}");
        var lyDo = await huyWindow.ShowDialog<string?>(this);

        if (string.IsNullOrWhiteSpace(lyDo))
            return;

        await ThucHienHuyPhieuAsync(record, lyDo);
    }

    private async Task ThucHienHuyPhieuAsync(ThanhToanCongNoKhachHang record, string lyDo)
    {
        try
        {
            var ketQua = await _database.HuyThanhToanCongNoKhachHangAsync(
                record.MaGiaoDich,
                lyDo,
                Session.CurrentUser?.Id,
                Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap);

            CoThayDoi = true;

            var noiDungThanhCong = new StringBuilder();
            noiDungThanhCong.AppendLine($"Đã hủy thành công phiếu thanh toán {record.MaGiaoDich}.");
            noiDungThanhCong.AppendLine($"Số tiền hoàn tác: {ketQua.TienHoanTacCongNo:N0} đ");
            noiDungThanhCong.AppendLine($"Công nợ mới của khách hàng: {ketQua.CongNoSauKhiHuy:N0} đ");
            if (ketQua.SoHoaDonHoanTac > 0)
                noiDungThanhCong.AppendLine($"Số hóa đơn khôi phục nợ: {ketQua.SoHoaDonHoanTac}");
            if (ketQua.TienHoanTacTraTruoc > 0)
                noiDungThanhCong.AppendLine($"Số dư trả trước đã trừ lại: {ketQua.TienHoanTacTraTruoc:N0} đ");
            noiDungThanhCong.AppendLine("Phiếu thu trong sổ quỹ đã được chuyển sang trạng thái Đã hủy.");

            var successDialog = new ConfirmationWindow(
                "Hủy phiếu thanh toán thành công",
                noiDungThanhCong.ToString().TrimEnd(),
                "Đồng ý");
            await successDialog.ShowDialog<bool>(this);

            await NapDuLieuAsync();
        }
        catch (Exception ex)
        {
            var errDialog = new ConfirmationWindow(
                "Lỗi khi hủy phiếu thanh toán",
                "Đã xảy ra lỗi: " + ex.Message,
                "Đóng");
            await errDialog.ShowDialog<bool>(this);
        }
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

    private async void OnLastPageClick(object? sender, RoutedEventArgs e) =>
        await ChuyenTrangAsync(TongSoTrang);

    private int TongSoTrang => Math.Max(1, (int)Math.Ceiling(_totalCount / (double)_pageSize));

    private async Task ChuyenTrangAsync(int page)
    {
        _currentPage = Math.Clamp(page, 1, TongSoTrang);
        await NapDuLieuAsync();
    }

    private void CapNhatTrangHienTai()
    {
        var skip = (_currentPage - 1) * _pageSize;
        LichSuGrid.ItemsSource = _visibleRecords;
        LichSuGrid.SelectedItem = null;
        BtnDetail.IsEnabled = false;
        BtnHuy.IsEnabled = false;
        TxtPage.Text = $"Trang {_currentPage:N0} / {TongSoTrang:N0}";
        var first = _totalCount == 0 ? 0 : skip + 1;
        var last = Math.Min(skip + _visibleRecords.Count, _totalCount);
        TxtResultRange.Text = $"Hiển thị {first:N0}–{last:N0} / {_totalCount:N0} giao dịch";
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

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
