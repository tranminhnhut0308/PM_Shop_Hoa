using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

public sealed partial class MainWindowViewModel
{
    // =========================================================
    // BỘ LỌC HÓA ĐƠN THEO KHOẢNG THỜI GIAN & TRẠNG THÁI (ISSUE #43)
    // =========================================================

    private DateTimeOffset? _invoiceFilterStartDate;
    private DateTimeOffset? _invoiceFilterEndDate;
    private string _selectedInvoiceDatePreset = "Tháng này";
    private bool _dangCapNhatPresetNgay;

    private string _selectedInvoicePaymentStatus = "Tất cả";
    private string _selectedInvoiceStatus = "Tất cả";
    private KhachHangFilterOption? _selectedInvoiceCustomer;
    private HoaDonNhanVienFilterOption? _selectedInvoiceEmployee;
    private int _invoiceSearchVersion;

    private int _invoicePage = 1;
    private int _invoicePageSize = 20;
    private int _invoiceTotalCount;

    [ObservableProperty]
    private bool isInvoiceAdvancedFiltersExpanded;
    [ObservableProperty]
    private string invoiceCustomerSearch = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoInvoices))]
    private bool _isLoadingInvoices;

    public bool HasNoInvoices => !IsLoadingInvoices && HoaDons.Count == 0;

    // --- 1. LỰA CHỌN KHOẢNG THỜI GIAN NHANH ---
    public IReadOnlyList<string> InvoiceDatePresets { get; } =
    [
        "Hôm nay",
        "Hôm qua",
        "7 ngày qua",
        "30 ngày qua",
        "Tuần này",
        "Tháng này",
        "Tháng trước",
        "Quý này",
        "Năm nay",
        "Tùy chọn"
    ];

    public string SelectedInvoiceDatePreset
    {
        get => _selectedInvoiceDatePreset;
        set
        {
            if (SetProperty(ref _selectedInvoiceDatePreset, value))
            {
                if (!_dangCapNhatPresetNgay && !string.IsNullOrWhiteSpace(value) && value != "Tùy chọn")
                {
                    ApDungPresetNgayHoaDon(value, tuDongTim: false);
                }
            }
        }
    }

    public DateTimeOffset? InvoiceFilterStartDate
    {
        get => _invoiceFilterStartDate;
        set
        {
            if (SetProperty(ref _invoiceFilterStartDate, value?.Date))
            {
                OnPropertyChanged(nameof(InvoiceFilterStartDateText));
                if (!_dangCapNhatPresetNgay)
                {
                    _selectedInvoiceDatePreset = "Tùy chọn";
                    OnPropertyChanged(nameof(SelectedInvoiceDatePreset));
                }
            }
        }
    }

    public DateTimeOffset? InvoiceFilterEndDate
    {
        get => _invoiceFilterEndDate;
        set
        {
            if (SetProperty(ref _invoiceFilterEndDate, value?.Date))
            {
                OnPropertyChanged(nameof(InvoiceFilterEndDateText));
                if (!_dangCapNhatPresetNgay)
                {
                    _selectedInvoiceDatePreset = "Tùy chọn";
                    OnPropertyChanged(nameof(SelectedInvoiceDatePreset));
                }
            }
        }
    }

    public string InvoiceFilterStartDateText =>
        InvoiceFilterStartDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Từ ngày";

    public string InvoiceFilterEndDateText =>
        InvoiceFilterEndDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Đến ngày";

    // --- 2. BỘ LỌC TRẠNG THÁI THANH TOÁN & TRẠNG THÁI HÓA ĐƠN ---
    public IReadOnlyList<string> InvoicePaymentStatusOptions { get; } =
    [
        "Tất cả",
        "Đã thanh toán",
        "Thanh toán một phần",
        "Chưa thanh toán",
        "Còn công nợ",
        "Không còn công nợ"
    ];

    public string SelectedInvoicePaymentStatus
    {
        get => _selectedInvoicePaymentStatus;
        set
        {
            if (SetProperty(ref _selectedInvoicePaymentStatus, value))
            {
                OnPropertyChanged(nameof(InvoiceActiveFiltersText));
            }
        }
    }

    public IReadOnlyList<string> InvoiceStatusOptions { get; } =
    [
        "Tất cả",
        "Hoàn thành",
        "Chưa thanh toán",
        "Thanh toán một phần",
        "Đã hủy",
        "Tổng hợp"
    ];

    public string SelectedInvoiceStatus
    {
        get => _selectedInvoiceStatus;
        set
        {
            if (SetProperty(ref _selectedInvoiceStatus, value))
            {
                OnPropertyChanged(nameof(InvoiceActiveFiltersText));
            }
        }
    }

    // --- 3. BỘ LỌC KHÁCH HÀNG ---
    public ObservableCollection<KhachHangFilterOption> InvoiceCustomerOptions { get; } = [];
    public ObservableCollection<KhachHangFilterOption> InvoiceCustomerFilterOptions { get; } = [];

    public KhachHangFilterOption? SelectedInvoiceCustomer
    {
        get => _selectedInvoiceCustomer;
        set
        {
            if (SetProperty(ref _selectedInvoiceCustomer, value))
            {
                OnPropertyChanged(nameof(InvoiceActiveFiltersText));
                CapNhatInvoiceCustomerFilterOptions();
            }
        }
    }

    public ObservableCollection<HoaDonNhanVienFilterOption> InvoiceEmployeeOptions { get; } = [];

    public HoaDonNhanVienFilterOption? SelectedInvoiceEmployee
    {
        get => _selectedInvoiceEmployee;
        set
        {
            if (SetProperty(ref _selectedInvoiceEmployee, value))
            {
                OnPropertyChanged(nameof(InvoiceActiveFiltersText));
            }
        }
    }

    public string InvoiceAdvancedFiltersToggleText =>
        IsInvoiceAdvancedFiltersExpanded ? "Ẩn bộ lọc nâng cao" : "Bộ lọc nâng cao";

    public string InvoiceActiveFiltersText
    {
        get
        {
            var filters = new List<string>();
            if (!string.IsNullOrWhiteSpace(InvoiceSearch))
                filters.Add($"Từ khóa: {InvoiceSearch.Trim()}");
            if (InvoiceFilterStartDate.HasValue || InvoiceFilterEndDate.HasValue)
            {
                var start = InvoiceFilterStartDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "…";
                var end = InvoiceFilterEndDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "…";
                filters.Add($"Thời gian: {start} – {end}");
            }
            if (SelectedInvoiceStatus != "Tất cả")
                filters.Add($"Hóa đơn: {SelectedInvoiceStatus}");
            if (SelectedInvoicePaymentStatus != "Tất cả")
                filters.Add($"Thanh toán: {SelectedInvoicePaymentStatus}");
            if (SelectedInvoiceCustomer is { IsWalkIn: true })
                filters.Add("Khách hàng: Khách lẻ");
            else if (SelectedInvoiceCustomer?.Id is not null)
                filters.Add($"Khách hàng: {SelectedInvoiceCustomer.DisplayText}");
            if (SelectedInvoiceEmployee is { Id: not null })
                filters.Add($"Nhân viên: {SelectedInvoiceEmployee.DisplayText}");

            return filters.Count == 0 ? "Không áp dụng bộ lọc" : string.Join("  •  ", filters);
        }
    }

    partial void OnIsInvoiceAdvancedFiltersExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(InvoiceAdvancedFiltersToggleText));
    }

    partial void OnInvoiceSearchChanged(string value)
    {
        OnPropertyChanged(nameof(InvoiceActiveFiltersText));
    }

    partial void OnInvoiceCustomerSearchChanged(string value) =>
        CapNhatInvoiceCustomerFilterOptions();

    public async Task TaiDanhSachKhachHangLocHoaDonAsync()
    {
        try
        {
            var customers = await _db.GetKhachHangAsync("");
            InvoiceCustomerOptions.Clear();
            InvoiceCustomerOptions.Add(new KhachHangFilterOption
            {
                Id = null,
                DisplayText = "-- Tất cả khách hàng --"
            });
            InvoiceCustomerOptions.Add(new KhachHangFilterOption
            {
                Id = null,
                IsWalkIn = true,
                DisplayText = "Khách lẻ"
            });

            foreach (var kh in customers.OrderBy(c => c.TenKhachHang))
            {
                var code = string.IsNullOrWhiteSpace(kh.MaKhachHang) ? "" : $"[{kh.MaKhachHang}] ";
                var phone = string.IsNullOrWhiteSpace(kh.SoDienThoai) ? "" : $" · {kh.SoDienThoai}";
                InvoiceCustomerOptions.Add(new KhachHangFilterOption
                {
                    Id = kh.Id,
                    DisplayText = $"{code}{kh.TenKhachHang}{phone}"
                });
            }

            _selectedInvoiceCustomer = InvoiceCustomerOptions.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedInvoiceCustomer));
            CapNhatInvoiceCustomerFilterOptions();
            await TaiDanhSachNhanVienLocHoaDonAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("TaiDanhSachKhachHangLocHoaDonAsync error", ex);
        }
    }

    private void CapNhatInvoiceCustomerFilterOptions()
    {
        var query = ProductSearchHelper.ChuanHoa(InvoiceCustomerSearch);
        InvoiceCustomerFilterOptions.Clear();

        foreach (var option in InvoiceCustomerOptions)
        {
            var matches = string.IsNullOrWhiteSpace(query)
                || ProductSearchHelper.ChuanHoa(option.DisplayText).Contains(query, StringComparison.Ordinal);
            var isSelected = option.Id == SelectedInvoiceCustomer?.Id
                && option.IsWalkIn == SelectedInvoiceCustomer?.IsWalkIn;

            if (matches || option.Id is null || isSelected)
                InvoiceCustomerFilterOptions.Add(option);
        }
    }

    private async Task TaiDanhSachNhanVienLocHoaDonAsync()
    {
        try
        {
            var employees = await _db.GetUsersAsync();
            InvoiceEmployeeOptions.Clear();
            InvoiceEmployeeOptions.Add(new HoaDonNhanVienFilterOption
            {
                DisplayText = "-- Tất cả nhân viên --"
            });

            foreach (var employee in employees.OrderBy(user => user.HoTen))
            {
                InvoiceEmployeeOptions.Add(new HoaDonNhanVienFilterOption
                {
                    Id = employee.Id,
                    DisplayText = employee.HoTen
                });
            }

            _selectedInvoiceEmployee = InvoiceEmployeeOptions.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedInvoiceEmployee));
            OnPropertyChanged(nameof(InvoiceActiveFiltersText));
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("TaiDanhSachNhanVienLocHoaDonAsync error", ex);
        }
    }

    // --- 4. PHÂN TRANG (PAGINATION) ---
    public IReadOnlyList<int> InvoicePageSizeOptions { get; } = [20, 50, 100];

    public int InvoicePage
    {
        get => _invoicePage;
        set
        {
            if (SetProperty(ref _invoicePage, Math.Max(1, value)))
            {
                CapNhatPhanTrangHoaDon();
            }
        }
    }

    public int InvoicePageSize
    {
        get => _invoicePageSize;
        set
        {
            if (SetProperty(ref _invoicePageSize, value > 0 ? value : 20))
            {
                InvoicePage = 1;
                _ = SearchInvoicesAsync(giuTrang: true);
            }
        }
    }

    public int InvoiceTotalCount
    {
        get => _invoiceTotalCount;
        set
        {
            if (SetProperty(ref _invoiceTotalCount, Math.Max(0, value)))
            {
                CapNhatPhanTrangHoaDon();
            }
        }
    }

    public int InvoiceTotalPages =>
        InvoicePageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(InvoiceTotalCount / (double)InvoicePageSize));

    public bool CanInvoiceTrangTruoc => InvoicePage > 1;
    public bool CanInvoiceTrangSau => InvoicePage < InvoiceTotalPages;

    public string InvoiceRangeText
    {
        get
        {
            if (InvoiceTotalCount <= 0)
                return "0 hóa đơn";

            var from = (InvoicePage - 1) * InvoicePageSize + 1;
            var to = Math.Min(InvoicePage * InvoicePageSize, InvoiceTotalCount);
            return $"Hiển thị {from:N0} - {to:N0} trên {InvoiceTotalCount:N0} hóa đơn";
        }
    }

    private void CapNhatPhanTrangHoaDon()
    {
        OnPropertyChanged(nameof(InvoiceTotalPages));
        OnPropertyChanged(nameof(CanInvoiceTrangTruoc));
        OnPropertyChanged(nameof(CanInvoiceTrangSau));
        OnPropertyChanged(nameof(InvoiceRangeText));
    }

    [RelayCommand]
    private async Task InvoiceTrangDauAsync()
    {
        if (InvoicePage > 1)
        {
            InvoicePage = 1;
            await SearchInvoicesAsync(giuTrang: true);
        }
    }

    [RelayCommand]
    private async Task InvoiceTrangTruocAsync()
    {
        if (CanInvoiceTrangTruoc)
        {
            InvoicePage--;
            await SearchInvoicesAsync(giuTrang: true);
        }
    }

    [RelayCommand]
    private async Task InvoiceTrangSauAsync()
    {
        if (CanInvoiceTrangSau)
        {
            InvoicePage++;
            await SearchInvoicesAsync(giuTrang: true);
        }
    }

    [RelayCommand]
    private async Task InvoiceTrangCuoiAsync()
    {
        if (InvoicePage < InvoiceTotalPages)
        {
            InvoicePage = InvoiceTotalPages;
            await SearchInvoicesAsync(giuTrang: true);
        }
    }

    // --- 5. THỐNG KÊ TỔNG HỢP (SUMMARY METRICS) ---
    [ObservableProperty] private int invoiceTongSoLuong;
    [ObservableProperty] private decimal invoiceTongTienDoanhThu;
    [ObservableProperty] private decimal invoiceTongDaThanhToan;
    [ObservableProperty] private decimal invoiceTongConNo;
    [ObservableProperty] private int invoiceTongDaHuy;

    public string InvoiceTongSoLuongText => $"{InvoiceTongSoLuong:N0} hóa đơn";

    public string InvoiceSummaryText =>
        $"Doanh thu: {InvoiceTongTienDoanhThu:N0} đ   •   Đã thu: {InvoiceTongDaThanhToan:N0} đ   •   Còn nợ: {InvoiceTongConNo:N0} đ"
        + (InvoiceTongDaHuy > 0 ? $"   (Loại trừ {InvoiceTongDaHuy} HĐ đã hủy)" : "");

    // --- 6. XỬ LÝ PRESET NGÀY ---
    public void ApDungPresetNgayHoaDon(string preset, bool tuDongTim = true)
    {
        var today = DateTime.Today;
        _dangCapNhatPresetNgay = true;
        try
        {
            switch (preset)
            {
                case "Hôm nay":
                    InvoiceFilterStartDate = new DateTimeOffset(today);
                    InvoiceFilterEndDate = new DateTimeOffset(today);
                    break;

                case "Hôm qua":
                    var yesterday = today.AddDays(-1);
                    InvoiceFilterStartDate = new DateTimeOffset(yesterday);
                    InvoiceFilterEndDate = new DateTimeOffset(yesterday);
                    break;

                case "7 ngày qua":
                    InvoiceFilterStartDate = new DateTimeOffset(today.AddDays(-6));
                    InvoiceFilterEndDate = new DateTimeOffset(today);
                    break;

                case "30 ngày qua":
                    InvoiceFilterStartDate = new DateTimeOffset(today.AddDays(-29));
                    InvoiceFilterEndDate = new DateTimeOffset(today);
                    break;

                case "Tuần này":
                    var firstDayOfWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                    InvoiceFilterStartDate = new DateTimeOffset(firstDayOfWeek);
                    InvoiceFilterEndDate = new DateTimeOffset(firstDayOfWeek.AddDays(6));
                    break;

                case "Tháng này":
                    var firstThisMonth = new DateTime(today.Year, today.Month, 1);
                    var lastThisMonth = firstThisMonth.AddMonths(1).AddDays(-1);
                    InvoiceFilterStartDate = new DateTimeOffset(firstThisMonth);
                    InvoiceFilterEndDate = new DateTimeOffset(lastThisMonth);
                    break;

                case "Tháng trước":
                    var firstLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    var lastLastMonth = new DateTime(today.Year, today.Month, 1).AddDays(-1);
                    InvoiceFilterStartDate = new DateTimeOffset(firstLastMonth);
                    InvoiceFilterEndDate = new DateTimeOffset(lastLastMonth);
                    break;

                case "Quý này":
                    int q = (today.Month - 1) / 3 + 1;
                    int firstMonthOfQ = (q - 1) * 3 + 1;
                    var firstDayQ = new DateTime(today.Year, firstMonthOfQ, 1);
                    var lastDayQ = firstDayQ.AddMonths(3).AddDays(-1);
                    InvoiceFilterStartDate = new DateTimeOffset(firstDayQ);
                    InvoiceFilterEndDate = new DateTimeOffset(lastDayQ);
                    break;

                case "Năm nay":
                    InvoiceFilterStartDate = new DateTimeOffset(new DateTime(today.Year, 1, 1));
                    InvoiceFilterEndDate = new DateTimeOffset(new DateTime(today.Year, 12, 31));
                    break;

                case "Tùy chọn":
                default:
                    break;
            }

            _selectedInvoiceDatePreset = preset;
            OnPropertyChanged(nameof(SelectedInvoiceDatePreset));
        }
        finally
        {
            _dangCapNhatPresetNgay = false;
        }

        if (tuDongTim)
        {
            InvoicePage = 1;
            _ = SearchInvoicesAsync(giuTrang: true);
        }

        OnPropertyChanged(nameof(InvoiceActiveFiltersText));
    }

    [RelayCommand]
    private async Task XoaTuKhoaHoaDonAsync()
    {
        InvoiceSearch = "";
        await SearchInvoicesAsync();
    }

    [RelayCommand]
    private async Task LamMoiDanhSachHoaDonAsync()
    {
        await SearchInvoicesAsync(giuTrang: true);
    }

    // --- 7. TÌM KIẾM VÀ TRUY VẤN DỮ LIỆU ---
    [RelayCommand]
    public async Task SearchInvoicesAsync()
    {
        InvoicePage = 1;
        await SearchInvoicesAsync(giuTrang: true);
    }

    public async Task SearchInvoicesAsync(bool giuTrang)
    {
        // 1. Kiểm tra ngày bắt đầu > ngày kết thúc
        if (InvoiceFilterStartDate.HasValue && InvoiceFilterEndDate.HasValue
            && InvoiceFilterStartDate.Value.Date > InvoiceFilterEndDate.Value.Date)
        {
            StatusMessage = "⚠️ Ngày bắt đầu không được lớn hơn ngày kết thúc. Vui lòng chọn lại.";
            await ShowMessageAsync(
                "Khoảng thời gian không hợp lệ",
                "Ngày bắt đầu không được lớn hơn ngày kết thúc. Vui lòng kiểm tra lại khoảng thời gian lọc.");
            return;
        }

        if (!giuTrang)
        {
            InvoicePage = 1;
        }

        var searchVersion = ++_invoiceSearchVersion;
        IsLoadingInvoices = true;
        OnPropertyChanged(nameof(HasNoInvoices));

        try
        {
            HoaDons.Clear();

            var tuNgay = InvoiceFilterStartDate?.Date;
            var denNgay = InvoiceFilterEndDate?.Date;

            var ttThanhToan = SelectedInvoicePaymentStatus switch
            {
                "Đã thanh toán" => "DA_THANH_TOAN",
                "Thanh toán một phần" => "THANH_TOAN_MOT_PHAN",
                "Chưa thanh toán" => "CHUA_THANH_TOAN",
                "Còn công nợ" => "CON_NO",
                "Không còn công nợ" => "KHONG_CON_NO",
                _ => null
            };

            var ttHoaDon = SelectedInvoiceStatus switch
            {
                "Hoàn thành" => "HOAN_THANH",
                "Chưa thanh toán" => "CHUA_THANH_TOAN",
                "Thanh toán một phần" => "THANH_TOAN_MOT_PHAN",
                "Đã hủy" => "DA_HUY",
                "Tổng hợp" => "TONG_HOP",
                _ => null
            };

            var khId = SelectedInvoiceCustomer?.Id;
            var khachLe = SelectedInvoiceCustomer?.IsWalkIn == true;
            var nhanVienId = SelectedInvoiceEmployee?.Id;

            var result = await _db.GetHoaDonPagedAsync(
                search: InvoiceSearch,
                tuNgay: tuNgay,
                denNgay: denNgay,
                trangThaiThanhToan: ttThanhToan,
                trangThaiHoaDon: ttHoaDon,
                khachHangId: khId,
                nhanVienId: nhanVienId,
                khachLe: khachLe,
                page: InvoicePage,
                pageSize: InvoicePageSize);

            if (searchVersion != _invoiceSearchVersion)
                return;

            InvoiceTotalCount = result.TongSoLuong;

            // Nếu trang hiện tại vượt quá tổng số trang sau khi lọc, đưa về trang 1
            if (result.Items.Count == 0 && result.TongSoLuong > 0 && InvoicePage > InvoiceTotalPages)
            {
                InvoicePage = 1;
                result = await _db.GetHoaDonPagedAsync(
                    search: InvoiceSearch,
                    tuNgay: tuNgay,
                    denNgay: denNgay,
                    trangThaiThanhToan: ttThanhToan,
                    trangThaiHoaDon: ttHoaDon,
                    khachHangId: khId,
                    nhanVienId: nhanVienId,
                    khachLe: khachLe,
                    page: 1,
                    pageSize: InvoicePageSize);
                InvoiceTotalCount = result.TongSoLuong;
            }

            foreach (var item in result.Items)
            {
                HoaDons.Add(item);
            }

            InvoiceTongSoLuong = result.TongSoLuong;
            InvoiceTongTienDoanhThu = result.TongDoanhThu;
            InvoiceTongDaThanhToan = result.TongDaThanhToan;
            InvoiceTongConNo = result.TongConNo;
            InvoiceTongDaHuy = result.TongDaHuy;

            OnPropertyChanged(nameof(InvoiceTongSoLuongText));
            OnPropertyChanged(nameof(InvoiceSummaryText));
            CapNhatPhanTrangHoaDon();

            StatusMessage = $"Tìm thấy {result.TongSoLuong:N0} hóa đơn phù hợp.";
        }
        catch (Exception ex)
        {
            if (searchVersion != _invoiceSearchVersion)
                return;

            HoaDons.Clear();
            InvoiceTotalCount = 0;
            InvoiceTongSoLuong = 0;
            InvoiceTongTienDoanhThu = 0m;
            InvoiceTongDaThanhToan = 0m;
            InvoiceTongConNo = 0m;
            InvoiceTongDaHuy = 0;

            OnPropertyChanged(nameof(InvoiceTongSoLuongText));
            OnPropertyChanged(nameof(InvoiceSummaryText));
            CapNhatPhanTrangHoaDon();

            StatusMessage = "Lỗi tìm kiếm hóa đơn: " + ex.Message;
            SafeLog.WriteError("SearchInvoicesAsync error", ex);
        }
        finally
        {
            if (searchVersion == _invoiceSearchVersion)
            {
                IsLoadingInvoices = false;
                OnPropertyChanged(nameof(HasNoInvoices));
            }
        }
    }

    // --- 8. ĐẶT LẠI TOÀN BỘ BỘ LỌC VỀ MẶC ĐỊNH ---
    [RelayCommand]
    public async Task ClearInvoiceSearchAsync()
    {
        InvoiceSearch = "";
        _dangCapNhatPresetNgay = true;
        _selectedInvoiceDatePreset = "Tùy chọn";
        InvoiceFilterStartDate = null;
        InvoiceFilterEndDate = null;
        _dangCapNhatPresetNgay = false;
        OnPropertyChanged(nameof(SelectedInvoiceDatePreset));

        _selectedInvoicePaymentStatus = "Tất cả";
        OnPropertyChanged(nameof(SelectedInvoicePaymentStatus));

        _selectedInvoiceStatus = "Tất cả";
        OnPropertyChanged(nameof(SelectedInvoiceStatus));

        _selectedInvoiceCustomer = InvoiceCustomerOptions.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedInvoiceCustomer));
        InvoiceCustomerSearch = "";
        CapNhatInvoiceCustomerFilterOptions();

        _selectedInvoiceEmployee = InvoiceEmployeeOptions.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedInvoiceEmployee));

        OnPropertyChanged(nameof(InvoiceActiveFiltersText));
        InvoicePage = 1;

        await SearchInvoicesAsync(giuTrang: true);
        StatusMessage = "Đã đặt lại toàn bộ bộ lọc hóa đơn về mặc định.";
    }
}
