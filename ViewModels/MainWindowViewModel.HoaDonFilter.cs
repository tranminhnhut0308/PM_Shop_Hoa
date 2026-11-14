using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

public sealed record InvoiceFilterChip(string Key, string Text);

public sealed partial class MainWindowViewModel
{
    // =========================================================
    // BỘ LỌC HÓA ĐƠN THEO KHOẢNG THỜI GIAN & TRẠNG THÁI (ISSUE #43)
    // =========================================================

    private DateTimeOffset? _invoiceFilterStartDate;
    private DateTimeOffset? _invoiceFilterEndDate;
    private DateTime? _invoiceCustomStartDateDraft;
    private DateTime? _invoiceCustomEndDateDraft;
    private string _selectedInvoiceDatePreset = "Mọi ngày";
    private string _invoiceDatePresetBeforeCustom = "Mọi ngày";
    private bool _dangCapNhatPresetNgay;
    private bool _dangHuyChonNgayTuyChon;
    private bool _dangSuaKhoangNgayTuyChon;

    private string _selectedInvoicePaymentStatus = "Tất cả";
    private string _selectedInvoiceStatus = "Tất cả";
    private KhachHangFilterOption? _selectedInvoiceCustomer;
    private HoaDonNhanVienFilterOption? _selectedInvoiceEmployee;
    private int _invoiceSearchVersion;
    private CancellationTokenSource? _invoiceSearchDebounce;

    private int _invoicePage = 1;
    private int _invoicePageSize = 20;
    private int _invoiceTotalCount;

    [ObservableProperty]
    private bool isInvoiceAdvancedFiltersExpanded;
    [ObservableProperty]
    private string invoiceCustomerSearch = "";

    [ObservableProperty]
    private bool isInvoiceCustomDatePickerOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoInvoices))]
    private bool _isLoadingInvoices;

    public bool HasNoInvoices => !IsLoadingInvoices && HoaDons.Count == 0;

    // --- 1. LỰA CHỌN KHOẢNG THỜI GIAN NHANH ---
    public IReadOnlyList<string> InvoiceDatePresets { get; } =
    [
        "Mọi ngày",
        "Hôm nay",
        "Hôm qua",
        "7 ngày qua",
        "Tháng này",
        "Tháng trước",
        "Tùy chỉnh"
    ];

    public string SelectedInvoiceDatePreset
    {
        get => _selectedInvoiceDatePreset;
        set
        {
            var previousPreset = _selectedInvoiceDatePreset;
            if (SetProperty(ref _selectedInvoiceDatePreset, value))
            {
                if (!_dangCapNhatPresetNgay && !_dangHuyChonNgayTuyChon && value == "Tùy chỉnh")
                {
                    _invoiceDatePresetBeforeCustom = previousPreset;
                    var homNay = NgayVietNamHomNay();
                    InvoiceCustomStartDateDraft = (InvoiceFilterStartDate ?? InvoiceFilterEndDate ?? homNay).Date;
                    InvoiceCustomEndDateDraft = (InvoiceFilterEndDate ?? InvoiceFilterStartDate ?? homNay).Date;
                    IsInvoiceCustomDatePickerOpen = true;
                }
                else if (!_dangCapNhatPresetNgay && !_dangHuyChonNgayTuyChon
                    && !string.IsNullOrWhiteSpace(value))
                {
                    ApDungPresetNgayHoaDon(value);
                }
                OnPropertyChanged(nameof(IsInvoiceCustomDatePreset));
                ThongBaoPresetNgayHoaDon();
                OnPropertyChanged(nameof(InvoiceDateFilterText));
                OnPropertyChanged(nameof(InvoiceDateFilterDisplayText));
                OnPropertyChanged(nameof(InvoiceActiveFiltersText));
                OnPropertyChanged(nameof(InvoiceDateCompactText));
                OnPropertyChanged(nameof(HasInvoiceDateFilter));
            }
        }
    }

    // Kiểu DateTime? khớp chính xác CalendarDatePicker.SelectedDate (Avalonia 11)
    // để binding hai chiều không phải ép kiểu DateTime <-> DateTimeOffset (issue #106).
    public DateTime? InvoiceCustomStartDateDraft
    {
        get => _invoiceCustomStartDateDraft;
        set => SetProperty(
            ref _invoiceCustomStartDateDraft,
            value.HasValue ? value.Value.Date : null);
    }

    public DateTime? InvoiceCustomEndDateDraft
    {
        get => _invoiceCustomEndDateDraft;
        set => SetProperty(
            ref _invoiceCustomEndDateDraft,
            value.HasValue ? value.Value.Date : null);
    }

    public DateTimeOffset? InvoiceFilterStartDate
    {
        get => _invoiceFilterStartDate;
        set
        {
            if (SetProperty(
                ref _invoiceFilterStartDate,
                value.HasValue ? VietnameseCalendar.FromCalendarDate(value.Value) : null))
            {
                OnPropertyChanged(nameof(InvoiceFilterStartDateText));
                OnPropertyChanged(nameof(InvoiceDateFilterText));
                OnPropertyChanged(nameof(InvoiceDateFilterDisplayText));
                OnPropertyChanged(nameof(InvoiceActiveFiltersText));
                OnPropertyChanged(nameof(InvoiceDateCompactText));
                OnPropertyChanged(nameof(HasInvoiceDateFilter));
                if (!_dangCapNhatPresetNgay)
                {
                    _selectedInvoiceDatePreset = "Tùy chỉnh";
                    OnPropertyChanged(nameof(SelectedInvoiceDatePreset));
                    OnPropertyChanged(nameof(IsInvoiceCustomDatePreset));
                    OnPropertyChanged(nameof(InvoiceDateFilterDisplayText));
                    if (InvoiceFilterStartDate.HasValue && InvoiceFilterEndDate.HasValue)
                    {
                        InvoicePage = 1;
                        _ = SearchInvoicesAsync(giuTrang: true);
                    }
                }
            }
        }
    }

    public DateTimeOffset? InvoiceFilterEndDate
    {
        get => _invoiceFilterEndDate;
        set
        {
            if (SetProperty(
                ref _invoiceFilterEndDate,
                value.HasValue ? VietnameseCalendar.FromCalendarDate(value.Value) : null))
            {
                OnPropertyChanged(nameof(InvoiceFilterEndDateText));
                OnPropertyChanged(nameof(InvoiceDateFilterText));
                OnPropertyChanged(nameof(InvoiceDateFilterDisplayText));
                OnPropertyChanged(nameof(InvoiceActiveFiltersText));
                OnPropertyChanged(nameof(InvoiceDateCompactText));
                OnPropertyChanged(nameof(HasInvoiceDateFilter));
                if (!_dangCapNhatPresetNgay)
                {
                    _selectedInvoiceDatePreset = "Tùy chỉnh";
                    OnPropertyChanged(nameof(SelectedInvoiceDatePreset));
                    OnPropertyChanged(nameof(IsInvoiceCustomDatePreset));
                    OnPropertyChanged(nameof(InvoiceDateFilterDisplayText));
                    if (InvoiceFilterStartDate.HasValue && InvoiceFilterEndDate.HasValue)
                    {
                        InvoicePage = 1;
                        _ = SearchInvoicesAsync(giuTrang: true);
                    }
                }
            }
        }
    }

    public string InvoiceFilterStartDateText =>
        InvoiceFilterStartDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Từ ngày";

    public string InvoiceFilterEndDateText =>
        InvoiceFilterEndDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Đến ngày";

    public bool IsInvoiceCustomDatePreset => SelectedInvoiceDatePreset == "Tùy chỉnh";
    public bool HasInvoiceDateFilter => InvoiceFilterStartDate.HasValue || InvoiceFilterEndDate.HasValue;

    public bool IsInvoiceDatePresetAll => SelectedInvoiceDatePreset == "Mọi ngày";
    public bool IsInvoiceDatePresetToday => SelectedInvoiceDatePreset == "Hôm nay";
    public bool IsInvoiceDatePresetYesterday => SelectedInvoiceDatePreset == "Hôm qua";
    public bool IsInvoiceDatePresetLast7Days =>
        SelectedInvoiceDatePreset is "7 ngày qua" or "7 ngày gần nhất";
    public bool IsInvoiceDatePresetThisMonth => SelectedInvoiceDatePreset == "Tháng này";
    public bool IsInvoiceDatePresetLastMonth => SelectedInvoiceDatePreset == "Tháng trước";

    private void ThongBaoPresetNgayHoaDon()
    {
        OnPropertyChanged(nameof(IsInvoiceDatePresetAll));
        OnPropertyChanged(nameof(IsInvoiceDatePresetToday));
        OnPropertyChanged(nameof(IsInvoiceDatePresetYesterday));
        OnPropertyChanged(nameof(IsInvoiceDatePresetLast7Days));
        OnPropertyChanged(nameof(IsInvoiceDatePresetThisMonth));
        OnPropertyChanged(nameof(IsInvoiceDatePresetLastMonth));
    }

    [RelayCommand]
    private void ChonPresetNgayHoaDon(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
            return;

        SelectedInvoiceDatePreset = preset;
    }

    private static DateTimeOffset NgayVietNamHomNay() => VietnameseCalendar.Today();

    [RelayCommand]
    private async Task ApDungKhoangNgayHoaDonAsync()
    {
        if (!InvoiceCustomStartDateDraft.HasValue || !InvoiceCustomEndDateDraft.HasValue)
        {
            StatusMessage = "Vui lòng chọn cả ngày bắt đầu và ngày kết thúc.";
            return;
        }

        if (InvoiceCustomStartDateDraft.Value.Date > InvoiceCustomEndDateDraft.Value.Date)
        {
            StatusMessage = "Ngày bắt đầu không được lớn hơn ngày kết thúc.";
            return;
        }

        _dangCapNhatPresetNgay = true;
        InvoiceFilterStartDate = VietnameseCalendar.FromDateTime(InvoiceCustomStartDateDraft.Value);
        InvoiceFilterEndDate = VietnameseCalendar.FromDateTime(InvoiceCustomEndDateDraft.Value);
        _selectedInvoiceDatePreset = "Tùy chỉnh";
        _dangCapNhatPresetNgay = false;
        OnPropertyChanged(nameof(SelectedInvoiceDatePreset));
        OnPropertyChanged(nameof(InvoiceDateFilterText));
        OnPropertyChanged(nameof(InvoiceDateFilterDisplayText));
        OnPropertyChanged(nameof(InvoiceDateCompactText));

        _dangSuaKhoangNgayTuyChon = false;
        IsInvoiceCustomDatePickerOpen = false;
        HuyTimHoaDonDebounce();
        InvoicePage = 1;
        await SearchInvoicesAsync(giuTrang: true);
    }

    [RelayCommand]
    private async Task DatLaiKhoangNgayHoaDonAsync()
    {
        _dangCapNhatPresetNgay = true;
        InvoiceFilterStartDate = null;
        InvoiceFilterEndDate = null;
        _selectedInvoiceDatePreset = "Mọi ngày";
        _dangCapNhatPresetNgay = false;
        InvoiceCustomStartDateDraft = null;
        InvoiceCustomEndDateDraft = null;
        OnPropertyChanged(nameof(SelectedInvoiceDatePreset));
        OnPropertyChanged(nameof(IsInvoiceCustomDatePreset));
        ThongBaoPresetNgayHoaDon();
        OnPropertyChanged(nameof(InvoiceDateFilterText));
        OnPropertyChanged(nameof(InvoiceDateFilterDisplayText));
        OnPropertyChanged(nameof(InvoiceDateCompactText));

        _dangSuaKhoangNgayTuyChon = false;
        IsInvoiceCustomDatePickerOpen = false;
        HuyTimHoaDonDebounce();
        InvoicePage = 1;
        await SearchInvoicesAsync(giuTrang: true);
    }

    [RelayCommand]
    private void HuyChonKhoangNgayHoaDon()
    {
        if (!_dangSuaKhoangNgayTuyChon)
        {
            _dangHuyChonNgayTuyChon = true;
            SelectedInvoiceDatePreset = _invoiceDatePresetBeforeCustom;
            _dangHuyChonNgayTuyChon = false;
        }

        _dangSuaKhoangNgayTuyChon = false;
        IsInvoiceCustomDatePickerOpen = false;
    }

    [RelayCommand]
    private void MoChonKhoangNgayHoaDon()
    {
        if (!IsInvoiceCustomDatePreset)
            return;

        InvoiceCustomStartDateDraft = InvoiceFilterStartDate?.Date;
        InvoiceCustomEndDateDraft = InvoiceFilterEndDate?.Date;
        _dangSuaKhoangNgayTuyChon = true;
        IsInvoiceCustomDatePickerOpen = true;
    }

    public string InvoiceDateFilterText
    {
        get
        {
            if (IsInvoiceCustomDatePreset
                && (InvoiceFilterStartDate is null || InvoiceFilterEndDate is null))
                return "Khoảng thời gian tùy chỉnh chưa đầy đủ";

            if (InvoiceFilterStartDate is null && InvoiceFilterEndDate is null)
                return "Khoảng thời gian: Mọi ngày";

            var start = InvoiceFilterStartDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "không giới hạn";
            var end = InvoiceFilterEndDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "không giới hạn";
            return $"Khoảng thời gian đang lọc: {start} – {end}";
        }
    }

    public string InvoiceDateFilterDisplayText
    {
        get
        {
            var today = NgayVietNamHomNay().Date;
            return SelectedInvoiceDatePreset switch
            {
                "Hôm nay" => $"Hôm nay ({today:dd/MM/yyyy})",
                "Hôm qua" => $"Hôm qua ({today.AddDays(-1):dd/MM/yyyy})",
                "7 ngày qua" or "7 ngày gần nhất" => $"7 ngày gần nhất ({today.AddDays(-6):dd/MM} – {today:dd/MM/yyyy})",
                "Tháng này" => $"Tháng này ({today:MM/yyyy})",
                "Tháng trước" => $"Tháng trước ({today.AddMonths(-1):MM/yyyy})",
                "Tùy chỉnh" or "Tùy chọn" when InvoiceFilterStartDate.HasValue && InvoiceFilterEndDate.HasValue =>
                    InvoiceFilterStartDate.Value.Date == InvoiceFilterEndDate.Value.Date
                        ? $"{InvoiceFilterStartDate.Value:dd/MM/yyyy}"
                        : $"{InvoiceFilterStartDate.Value:dd/MM/yyyy} – {InvoiceFilterEndDate.Value:dd/MM/yyyy}",
                "Mọi ngày" => "Tất cả thời gian",
                _ => SelectedInvoiceDatePreset
            };
        }
    }

    [ObservableProperty]
    private string invoiceFilterMinTotal = "";

    [ObservableProperty]
    private string invoiceFilterMaxTotal = "";

    // --- 2. BỘ LỌC TRẠNG THÁI THANH TOÁN & TRẠNG THÁI HÓA ĐƠN ---
    public IReadOnlyList<string> InvoicePaymentStatusOptions { get; } =
    [
        "Tất cả",
        "Đã thanh toán",
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
                OnPropertyChanged(nameof(IsInvoicePaymentAll));
                OnPropertyChanged(nameof(IsInvoicePaymentPaid));
                OnPropertyChanged(nameof(IsInvoicePaymentUnpaid));
                OnPropertyChanged(nameof(IsInvoicePaymentPartial));
                CapNhatInvoiceActiveFilterChips();
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
                ThongBaoTrangThaiHoaDon();
                CapNhatInvoiceActiveFilterChips();
            }
        }
    }

    public bool IsInvoiceFilterStatusAll => SelectedInvoiceStatus == "Tất cả";
    public bool IsInvoiceFilterStatusCompleted => SelectedInvoiceStatus == "Hoàn thành";
    public bool IsInvoiceFilterStatusUnpaid => SelectedInvoiceStatus == "Chưa thanh toán";
    public bool IsInvoiceFilterStatusPartial => SelectedInvoiceStatus == "Thanh toán một phần";
    public bool IsInvoiceFilterStatusCanceled => SelectedInvoiceStatus == "Đã hủy";

    private void ThongBaoTrangThaiHoaDon()
    {
        OnPropertyChanged(nameof(IsInvoiceFilterStatusAll));
        OnPropertyChanged(nameof(IsInvoiceFilterStatusCompleted));
        OnPropertyChanged(nameof(IsInvoiceFilterStatusUnpaid));
        OnPropertyChanged(nameof(IsInvoiceFilterStatusPartial));
        OnPropertyChanged(nameof(IsInvoiceFilterStatusCanceled));
    }

    [RelayCommand]
    private async Task LocTrangThaiHoaDonAsync(string? status)
    {
        SelectedInvoiceStatus = string.IsNullOrWhiteSpace(status) ? "Tất cả" : status;
        await SearchInvoicesAsync();
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
                OnPropertyChanged(nameof(HasSelectedInvoiceCustomer));
                OnPropertyChanged(nameof(SelectedInvoiceCustomerDisplayText));
                OnPropertyChanged(nameof(HienThiGoiYKhachHangLoc));
                OnPropertyChanged(nameof(InvoiceActiveFiltersText));
                CapNhatInvoiceCustomerFilterOptions();
                CapNhatInvoiceActiveFilterChips();
            }
        }
    }

    private int _invoiceCustomerSelectedIndex = -1;
    public int InvoiceCustomerSelectedIndex
    {
        get => _invoiceCustomerSelectedIndex;
        set
        {
            if (SetProperty(ref _invoiceCustomerSelectedIndex, value))
            {
                OnPropertyChanged(nameof(SelectedInvoiceCustomerInList));
            }
        }
    }

    public KhachHangFilterOption? SelectedInvoiceCustomerInList =>
        InvoiceCustomerSelectedIndex >= 0 && InvoiceCustomerSelectedIndex < InvoiceCustomerFilterOptions.Count
            ? InvoiceCustomerFilterOptions[InvoiceCustomerSelectedIndex]
            : null;

    private bool _isInvoiceCustomerSuggestionsClosed;
    public bool IsInvoiceCustomerSuggestionsClosed
    {
        get => _isInvoiceCustomerSuggestionsClosed;
        set
        {
            if (SetProperty(ref _isInvoiceCustomerSuggestionsClosed, value))
            {
                OnPropertyChanged(nameof(HienThiGoiYKhachHangLoc));
            }
        }
    }

    private bool _isInvoiceCustomerSearchFocused;
    public bool IsInvoiceCustomerSearchFocused
    {
        get => _isInvoiceCustomerSearchFocused;
        set
        {
            if (SetProperty(ref _isInvoiceCustomerSearchFocused, value))
            {
                OnPropertyChanged(nameof(HienThiGoiYKhachHangLoc));
            }
        }
    }

    public bool HasSelectedInvoiceCustomer =>
        SelectedInvoiceCustomer != null && (SelectedInvoiceCustomer.Id != null || SelectedInvoiceCustomer.IsWalkIn);

    public string SelectedInvoiceCustomerDisplayText
    {
        get
        {
            if (SelectedInvoiceCustomer is null)
                return "";
            if (SelectedInvoiceCustomer.IsWalkIn)
                return "Khách lẻ";
            return SelectedInvoiceCustomer.DisplayText;
        }
    }

    public bool HienThiGoiYKhachHangLoc =>
        !HasSelectedInvoiceCustomer
        && !IsInvoiceCustomerSuggestionsClosed
        && (!string.IsNullOrWhiteSpace(InvoiceCustomerSearch) || _isInvoiceCustomerSearchFocused)
        && InvoiceCustomerFilterOptions.Count > 0;

    [RelayCommand]
    public void ChonKhachHangHoaDon(KhachHangFilterOption? option)
    {
        if (option is null) return;
        SelectedInvoiceCustomer = option;
        InvoiceCustomerSearch = "";
        IsInvoiceCustomerSuggestionsClosed = true;
        InvoiceCustomerSelectedIndex = -1;
        OnPropertyChanged(nameof(HasSelectedInvoiceCustomer));
        OnPropertyChanged(nameof(SelectedInvoiceCustomerDisplayText));
        OnPropertyChanged(nameof(HienThiGoiYKhachHangLoc));
    }

    [RelayCommand]
    public void BoChonKhachHangHoaDon()
    {
        SelectedInvoiceCustomer = InvoiceCustomerOptions.FirstOrDefault();
        InvoiceCustomerSearch = "";
        IsInvoiceCustomerSuggestionsClosed = true;
        InvoiceCustomerSelectedIndex = -1;
        CapNhatInvoiceCustomerFilterOptions();
        OnPropertyChanged(nameof(HasSelectedInvoiceCustomer));
        OnPropertyChanged(nameof(SelectedInvoiceCustomerDisplayText));
        OnPropertyChanged(nameof(HienThiGoiYKhachHangLoc));
    }

    public void DiChuyenChonKhachHang(int delta)
    {
        if (InvoiceCustomerFilterOptions.Count == 0)
            return;

        var current = InvoiceCustomerSelectedIndex;
        if (current < 0)
            current = delta > 0 ? -1 : InvoiceCustomerFilterOptions.Count;

        InvoiceCustomerSelectedIndex = Math.Clamp(current + delta, 0, InvoiceCustomerFilterOptions.Count - 1);
        IsInvoiceCustomerSuggestionsClosed = false;
        OnPropertyChanged(nameof(HienThiGoiYKhachHangLoc));
    }

    public void DongGoiYKhachHangHoaDon()
    {
        IsInvoiceCustomerSuggestionsClosed = true;
        InvoiceCustomerSelectedIndex = -1;
        OnPropertyChanged(nameof(HienThiGoiYKhachHangLoc));
    }

    public ObservableCollection<HoaDonNhanVienFilterOption> InvoiceEmployeeOptions { get; } = [];
    public ObservableCollection<InvoiceFilterChip> InvoiceActiveFilterChips { get; } = [];
    public bool HasAppliedInvoiceFilters => InvoiceActiveFilterChips.Count > 0;
    public string InvoiceAdvancedFilterCountText =>
        InvoiceAdvancedFilterCount > 0
            ? $"Bộ lọc nâng cao ({InvoiceAdvancedFilterCount})"
            : "Bộ lọc nâng cao";

    public string InvoiceDateCompactText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SelectedInvoiceDatePreset)
                || SelectedInvoiceDatePreset == "Mọi ngày")
                return "📅 Mọi ngày";

            var start = InvoiceFilterStartDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "…";
            var end = InvoiceFilterEndDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "…";
            return InvoiceFilterStartDate == InvoiceFilterEndDate
                ? $"📅 {start} | {SelectedInvoiceDatePreset}"
                : $"📅 {start} – {end} | {SelectedInvoiceDatePreset}";
        }
    }

    public int InvoiceAdvancedFilterCount =>
        (SelectedInvoiceCustomer?.Id is not null || SelectedInvoiceCustomer?.IsWalkIn == true ? 1 : 0)
        + (SelectedInvoiceEmployee?.Id is not null ? 1 : 0)
        + (!string.IsNullOrWhiteSpace(InvoiceFilterMinTotal) ? 1 : 0)
        + (!string.IsNullOrWhiteSpace(InvoiceFilterMaxTotal) ? 1 : 0);

    public bool HasInvoiceAdvancedFilters => InvoiceAdvancedFilterCount > 0;

    public bool IsInvoicePaymentAll => SelectedInvoicePaymentStatus == "Tất cả";
    public bool IsInvoicePaymentPaid => SelectedInvoicePaymentStatus == "Đã thanh toán";
    public bool IsInvoicePaymentUnpaid =>
        SelectedInvoicePaymentStatus is "Chưa thanh toán" or "Còn công nợ";
    public bool IsInvoicePaymentPartial => SelectedInvoicePaymentStatus == "Thanh toán một phần";

    [RelayCommand]
    private async Task LocThanhToanHoaDonAsync(string? status)
    {
        SelectedInvoicePaymentStatus = string.IsNullOrWhiteSpace(status) ? "Tất cả" : status;
        await SearchInvoicesAsync();
    }

    [RelayCommand]
    private async Task ApDungBoLocHoaDonAsync()
    {
        IsInvoiceAdvancedFiltersExpanded = false;
        await SearchInvoicesAsync();
    }

    [RelayCommand]
    private void DongBoLocHoaDon()
        => IsInvoiceAdvancedFiltersExpanded = false;

    [RelayCommand]
    private async Task XoaNoiDungTimHoaDonAsync()
    {
        InvoiceSearch = "";
        HuyTimHoaDonDebounce();
        await SearchInvoicesAsync();
    }

    [RelayCommand]
    private void ToggleBoLocHoaDon()
    {
        IsInvoiceAdvancedFiltersExpanded = !IsInvoiceAdvancedFiltersExpanded;
    }

    [RelayCommand]
    private async Task XoaChipLocHoaDonAsync(string? key)
    {
        switch (key)
        {
            case "search":
                InvoiceSearch = "";
                HuyTimHoaDonDebounce();
                break;
            case "date":
                _dangCapNhatPresetNgay = true;
                _selectedInvoiceDatePreset = "Mọi ngày";
                InvoiceFilterStartDate = null;
                InvoiceFilterEndDate = null;
                _dangCapNhatPresetNgay = false;
                InvoiceCustomStartDateDraft = null;
                InvoiceCustomEndDateDraft = null;
                IsInvoiceCustomDatePickerOpen = false;
                OnPropertyChanged(nameof(SelectedInvoiceDatePreset));
                OnPropertyChanged(nameof(IsInvoiceCustomDatePreset));
                ThongBaoPresetNgayHoaDon();
                break;
            case "invoice-status":
                SelectedInvoiceStatus = "Tất cả";
                break;
            case "payment-status":
                SelectedInvoicePaymentStatus = "Tất cả";
                break;
            case "customer":
                SelectedInvoiceCustomer = InvoiceCustomerOptions.FirstOrDefault();
                break;
            case "employee":
                SelectedInvoiceEmployee = InvoiceEmployeeOptions.FirstOrDefault();
                break;
            case "min-total":
                InvoiceFilterMinTotal = "";
                break;
            case "max-total":
                InvoiceFilterMaxTotal = "";
                break;
            default:
                return;
        }

        await SearchInvoicesAsync();
    }

    private void CapNhatInvoiceActiveFilterChips()
    {
        InvoiceActiveFilterChips.Clear();
        if (SelectedInvoiceStatus != "Tất cả")
            InvoiceActiveFilterChips.Add(new("invoice-status", $"Hóa đơn: {SelectedInvoiceStatus}"));

        if (SelectedInvoiceCustomer is { IsWalkIn: true })
            InvoiceActiveFilterChips.Add(new("customer", "Khách hàng: Khách lẻ"));
        else if (SelectedInvoiceCustomer?.Id is not null)
            InvoiceActiveFilterChips.Add(new("customer", $"Khách hàng: {SelectedInvoiceCustomer.DisplayText}"));

        if (SelectedInvoiceEmployee?.Id is not null)
            InvoiceActiveFilterChips.Add(new("employee", $"Nhân viên: {SelectedInvoiceEmployee.DisplayText}"));
        if (!string.IsNullOrWhiteSpace(InvoiceFilterMinTotal))
            InvoiceActiveFilterChips.Add(new("min-total", $"Từ: {InvoiceFilterMinTotal.Trim()} đ"));
        if (!string.IsNullOrWhiteSpace(InvoiceFilterMaxTotal))
            InvoiceActiveFilterChips.Add(new("max-total", $"Đến: {InvoiceFilterMaxTotal.Trim()} đ"));
        if (SelectedInvoicePaymentStatus != "Tất cả")
            InvoiceActiveFilterChips.Add(new("payment-status", $"Thanh toán: {SelectedInvoicePaymentStatus}"));

        OnPropertyChanged(nameof(InvoiceAdvancedFilterCount));
        OnPropertyChanged(nameof(HasInvoiceAdvancedFilters));
        OnPropertyChanged(nameof(HasAppliedInvoiceFilters));
        OnPropertyChanged(nameof(InvoiceAdvancedFilterCountText));
        OnPropertyChanged(nameof(InvoiceDateCompactText));
        OnPropertyChanged(nameof(IsInvoicePaymentAll));
        OnPropertyChanged(nameof(IsInvoicePaymentPaid));
        OnPropertyChanged(nameof(IsInvoicePaymentUnpaid));
        OnPropertyChanged(nameof(IsInvoicePaymentPartial));
    }

    public HoaDonNhanVienFilterOption? SelectedInvoiceEmployee
    {
        get => _selectedInvoiceEmployee;
        set
        {
            if (SetProperty(ref _selectedInvoiceEmployee, value))
            {
                OnPropertyChanged(nameof(InvoiceActiveFiltersText));
                CapNhatInvoiceActiveFilterChips();
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
            if (!string.IsNullOrWhiteSpace(InvoiceFilterMinTotal))
                filters.Add($"Từ tiền: {InvoiceFilterMinTotal.Trim()}");
            if (!string.IsNullOrWhiteSpace(InvoiceFilterMaxTotal))
                filters.Add($"Đến tiền: {InvoiceFilterMaxTotal.Trim()}");
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
        DatLichTimHoaDon();
    }

    partial void OnInvoiceFilterMinTotalChanged(string value)
    {
        OnPropertyChanged(nameof(InvoiceActiveFiltersText));
        CapNhatInvoiceActiveFilterChips();
    }

    partial void OnInvoiceFilterMaxTotalChanged(string value)
    {
        OnPropertyChanged(nameof(InvoiceActiveFiltersText));
        CapNhatInvoiceActiveFilterChips();
    }

    private void DatLichTimHoaDon()
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _invoiceSearchDebounce, next);
        previous?.Cancel();
        _ = TimHoaDonSauDebounceAsync(next);
    }

    private async Task TimHoaDonSauDebounceAsync(CancellationTokenSource source)
    {
        try
        {
            await Task.Delay(350, source.Token);
            if (!source.IsCancellationRequested)
            {
                if (IsInvoiceCustomDatePreset
                    && (!InvoiceFilterStartDate.HasValue || !InvoiceFilterEndDate.HasValue))
                    return;

                InvoicePage = 1;
                await SearchInvoicesAsync(giuTrang: true);
            }
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.CompareExchange(ref _invoiceSearchDebounce, null, source);
            source.Dispose();
        }
    }

    partial void OnInvoiceCustomerSearchChanged(string value)
    {
        IsInvoiceCustomerSuggestionsClosed = false;
        CapNhatInvoiceCustomerFilterOptions();
    }

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
                TenKhachHang = "Khách lẻ",
                DisplayText = "Khách lẻ"
            });

            foreach (var kh in customers.OrderBy(c => c.TenKhachHang))
            {
                var code = string.IsNullOrWhiteSpace(kh.MaKhachHang) ? "" : $"[{kh.MaKhachHang}] ";
                var phone = string.IsNullOrWhiteSpace(kh.SoDienThoai) ? "" : $" · {kh.SoDienThoai}";
                InvoiceCustomerOptions.Add(new KhachHangFilterOption
                {
                    Id = kh.Id,
                    TenKhachHang = kh.TenKhachHang ?? "",
                    MaKhachHang = kh.MaKhachHang ?? "",
                    SoDienThoai = kh.SoDienThoai ?? "",
                    DisplayText = $"{code}{kh.TenKhachHang}{phone}"
                });
            }

            _selectedInvoiceCustomer = InvoiceCustomerOptions.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedInvoiceCustomer));
            OnPropertyChanged(nameof(HasSelectedInvoiceCustomer));
            OnPropertyChanged(nameof(SelectedInvoiceCustomerDisplayText));
            CapNhatInvoiceCustomerFilterOptions();
            await TaiDanhSachNhanVienLocHoaDonAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("TaiDanhSachKhachHangLocHoaDonAsync error", ex);
        }
    }

    public void CapNhatInvoiceCustomerFilterOptions()
    {
        var query = ProductSearchHelper.ChuanHoa(InvoiceCustomerSearch);
        InvoiceCustomerFilterOptions.Clear();

        foreach (var option in InvoiceCustomerOptions)
        {
            var matches = string.IsNullOrWhiteSpace(query)
                || ProductSearchHelper.ChuanHoa(option.DisplayText).Contains(query, StringComparison.Ordinal)
                || ProductSearchHelper.ChuanHoa(option.TenKhachHang).Contains(query, StringComparison.Ordinal)
                || ProductSearchHelper.ChuanHoa(option.MaKhachHang).Contains(query, StringComparison.Ordinal)
                || ProductSearchHelper.ChuanHoa(option.SoDienThoai).Contains(query, StringComparison.Ordinal);

            if (matches || option.Id is null)
                InvoiceCustomerFilterOptions.Add(option);
        }

        InvoiceCustomerSelectedIndex = InvoiceCustomerFilterOptions.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(HienThiGoiYKhachHangLoc));
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
        var today = NgayVietNamHomNay().Date;
        _dangCapNhatPresetNgay = true;
        try
        {
            switch (preset)
            {
                case "Hôm nay":
                    InvoiceFilterStartDate = VietnameseCalendar.FromDateTime(today);
                    InvoiceFilterEndDate = VietnameseCalendar.FromDateTime(today);
                    break;

                case "Hôm qua":
                    var yesterday = today.AddDays(-1);
                    InvoiceFilterStartDate = VietnameseCalendar.FromDateTime(yesterday);
                    InvoiceFilterEndDate = VietnameseCalendar.FromDateTime(yesterday);
                    break;

                case "7 ngày qua":
                case "7 ngày gần nhất":
                    InvoiceFilterStartDate = VietnameseCalendar.FromDateTime(today.AddDays(-6));
                    InvoiceFilterEndDate = VietnameseCalendar.FromDateTime(today);
                    break;

                case "30 ngày qua":
                    InvoiceFilterStartDate = VietnameseCalendar.FromDateTime(today.AddDays(-29));
                    InvoiceFilterEndDate = VietnameseCalendar.FromDateTime(today);
                    break;

                case "Tuần này":
                    var firstDayOfWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                    InvoiceFilterStartDate = VietnameseCalendar.FromDateTime(firstDayOfWeek);
                    InvoiceFilterEndDate = VietnameseCalendar.FromDateTime(firstDayOfWeek.AddDays(6));
                    break;

                case "Tháng này":
                    var firstThisMonth = new DateTime(today.Year, today.Month, 1);
                    var lastThisMonth = firstThisMonth.AddMonths(1).AddDays(-1);
                    InvoiceFilterStartDate = VietnameseCalendar.FromDateTime(firstThisMonth);
                    InvoiceFilterEndDate = VietnameseCalendar.FromDateTime(lastThisMonth);
                    break;

                case "Tháng trước":
                    var firstLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    var lastLastMonth = new DateTime(today.Year, today.Month, 1).AddDays(-1);
                    InvoiceFilterStartDate = VietnameseCalendar.FromDateTime(firstLastMonth);
                    InvoiceFilterEndDate = VietnameseCalendar.FromDateTime(lastLastMonth);
                    break;

                case "Quý này":
                    int q = (today.Month - 1) / 3 + 1;
                    int firstMonthOfQ = (q - 1) * 3 + 1;
                    var firstDayQ = new DateTime(today.Year, firstMonthOfQ, 1);
                    var lastDayQ = firstDayQ.AddMonths(3).AddDays(-1);
                    InvoiceFilterStartDate = VietnameseCalendar.FromDateTime(firstDayQ);
                    InvoiceFilterEndDate = VietnameseCalendar.FromDateTime(lastDayQ);
                    break;

                case "Năm nay":
                    InvoiceFilterStartDate = VietnameseCalendar.FromDateTime(new DateTime(today.Year, 1, 1));
                    InvoiceFilterEndDate = VietnameseCalendar.FromDateTime(new DateTime(today.Year, 12, 31));
                    break;

                case "Mọi ngày":
                    InvoiceFilterStartDate = null;
                    InvoiceFilterEndDate = null;
                    break;

                case "Tùy chỉnh":
                case "Tùy chọn":
                default:
                    break;
            }

            _selectedInvoiceDatePreset = preset;
            OnPropertyChanged(nameof(SelectedInvoiceDatePreset));
            OnPropertyChanged(nameof(IsInvoiceCustomDatePreset));
            ThongBaoPresetNgayHoaDon();
        }
        finally
        {
            _dangCapNhatPresetNgay = false;
        }

        if (tuDongTim)
        {
            HuyTimHoaDonDebounce();
            InvoicePage = 1;
            _ = SearchInvoicesAsync(giuTrang: true);
        }

        OnPropertyChanged(nameof(InvoiceActiveFiltersText));
        OnPropertyChanged(nameof(InvoiceDateFilterText));
        OnPropertyChanged(nameof(InvoiceDateFilterDisplayText));
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
        HuyTimHoaDonDebounce();
        InvoicePage = 1;
        await SearchInvoicesAsync(giuTrang: true);
    }

    private void HuyTimHoaDonDebounce()
    {
        Interlocked.Exchange(ref _invoiceSearchDebounce, null)?.Cancel();
    }

    public async Task SearchInvoicesAsync(bool giuTrang)
    {
        // 1. Với khoảng ngày tùy chỉnh, luôn áp dụng giá trị đang chọn trong DatePicker
        //    trước khi truy vấn để tránh tìm kiếm bằng khoảng ngày cũ.
        if (IsInvoiceCustomDatePreset)
        {
            if (!InvoiceCustomStartDateDraft.HasValue || !InvoiceCustomEndDateDraft.HasValue)
            {
                StatusMessage = "Vui lòng chọn cả ngày bắt đầu và ngày kết thúc cho khoảng tùy chỉnh.";
                await ShowMessageAsync(
                    "Thiếu khoảng thời gian",
                    "Khi chọn Tùy chọn, vui lòng nhập cả ngày bắt đầu và ngày kết thúc.");
                return;
            }

            if (InvoiceCustomStartDateDraft.Value.Date > InvoiceCustomEndDateDraft.Value.Date)
            {
                StatusMessage = "⚠️ Ngày bắt đầu không được lớn hơn ngày kết thúc. Vui lòng chọn lại.";
                await ShowMessageAsync(
                    "Khoảng thời gian không hợp lệ",
                    "Ngày bắt đầu không được lớn hơn ngày kết thúc. Vui lòng kiểm tra lại khoảng thời gian lọc.");
                return;
            }

            _dangCapNhatPresetNgay = true;
            try
            {
                InvoiceFilterStartDate = InvoiceCustomStartDateDraft.HasValue
                    ? VietnameseCalendar.FromDateTime(InvoiceCustomStartDateDraft.Value)
                    : null;
                InvoiceFilterEndDate = InvoiceCustomEndDateDraft.HasValue
                    ? VietnameseCalendar.FromDateTime(InvoiceCustomEndDateDraft.Value)
                    : null;
            }
            finally
            {
                _dangCapNhatPresetNgay = false;
            }
        }

        if (InvoiceFilterStartDate.HasValue && InvoiceFilterEndDate.HasValue
            && InvoiceFilterStartDate.Value.Date > InvoiceFilterEndDate.Value.Date)
        {
            StatusMessage = "⚠️ Ngày bắt đầu không được lớn hơn ngày kết thúc. Vui lòng chọn lại.";
            await ShowMessageAsync(
                "Khoảng thời gian không hợp lệ",
                "Ngày bắt đầu không được lớn hơn ngày kết thúc. Vui lòng kiểm tra lại khoảng thời gian lọc.");
            return;
        }

        if (!TryGetInvoiceTotalRange(out var tongTienTu, out var tongTienDen))
            return;

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

            var tuNgay = InvoiceFilterStartDate.HasValue
                ? VietnameseCalendar.ToDatabaseWallClockDate(InvoiceFilterStartDate.Value)
                : (DateTime?)null;
            var denNgay = InvoiceFilterEndDate.HasValue
                ? VietnameseCalendar.ToDatabaseWallClockDate(InvoiceFilterEndDate.Value)
                : (DateTime?)null;

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
                pageSize: InvoicePageSize,
                tongTienTu: tongTienTu,
                tongTienDen: tongTienDen);

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
                    pageSize: InvoicePageSize,
                    tongTienTu: tongTienTu,
                    tongTienDen: tongTienDen);
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
            CapNhatInvoiceActiveFilterChips();

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

    public static bool TryParseMoney(string? input, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var clean = input.Trim()
            .Replace("đ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("VND", "", StringComparison.OrdinalIgnoreCase)
            .Replace("VNĐ", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "");

        if (clean.Length == 0)
            return false;

        if (clean.Contains('-'))
            return false;

        if (clean.All(char.IsDigit))
        {
            if (decimal.TryParse(clean, NumberStyles.None, CultureInfo.InvariantCulture, out var plainVal))
            {
                result = plainVal;
                return true;
            }
            return false;
        }

        if (clean.Any(c => !char.IsDigit(c) && c != '.' && c != ','))
            return false;

        if (clean.Contains('.') && !clean.Contains(','))
        {
            var parts = clean.Split('.');
            if (parts.Length > 1 && parts.Skip(1).All(p => p.Length == 3))
            {
                var noDots = clean.Replace(".", "");
                if (decimal.TryParse(noDots, NumberStyles.None, CultureInfo.InvariantCulture, out var v))
                {
                    result = v;
                    return true;
                }
            }

            if (decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out var vInv))
            {
                result = vInv;
                return true;
            }
        }

        if (clean.Contains(',') && !clean.Contains('.'))
        {
            var parts = clean.Split(',');
            if (parts.Length > 1 && parts.Skip(1).All(p => p.Length == 3))
            {
                var noCommas = clean.Replace(",", "");
                if (decimal.TryParse(noCommas, NumberStyles.None, CultureInfo.InvariantCulture, out var v))
                {
                    result = v;
                    return true;
                }
            }

            var vi = CultureInfo.GetCultureInfo("vi-VN");
            if (decimal.TryParse(clean, NumberStyles.Number, vi, out var vVi))
            {
                result = vVi;
                return true;
            }
        }

        var lastDot = clean.LastIndexOf('.');
        var lastComma = clean.LastIndexOf(',');
        if (lastDot > lastComma)
        {
            var normalized = clean.Replace(",", "");
            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
        }
        else
        {
            var normalized = clean.Replace(".", "").Replace(",", ".");
            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
        }

        return false;
    }

    public bool TryGetInvoiceTotalRange(out decimal? minimum, out decimal? maximum)
    {
        minimum = null;
        maximum = null;

        if (!string.IsNullOrWhiteSpace(InvoiceFilterMinTotal))
        {
            if (!TryParseMoney(InvoiceFilterMinTotal, out var value))
            {
                StatusMessage = "Giá trị “Từ tiền” không hợp lệ.";
                return false;
            }
            if (value < 0)
            {
                StatusMessage = "Giá trị lọc tiền không được âm.";
                return false;
            }
            minimum = value;
        }

        if (!string.IsNullOrWhiteSpace(InvoiceFilterMaxTotal))
        {
            if (!TryParseMoney(InvoiceFilterMaxTotal, out var value))
            {
                StatusMessage = "Giá trị “Đến tiền” không hợp lệ.";
                return false;
            }
            if (value < 0)
            {
                StatusMessage = "Giá trị lọc tiền không được âm.";
                return false;
            }
            maximum = value;
        }

        if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
        {
            StatusMessage = "Giá trị “Từ tiền” không được lớn hơn “Đến tiền”.";
            return false;
        }

        return true;
    }

    // --- 8. ĐẶT LẠI TOÀN BỘ BỘ LỌC VỀ MẶC ĐỊNH ---
    [RelayCommand]
    public async Task ClearInvoiceSearchAsync()
    {
        IsInvoiceAdvancedFiltersExpanded = false;
        IsInvoiceCustomDatePickerOpen = false;
        HuyTimHoaDonDebounce();
        InvoiceSearch = "";
        HuyTimHoaDonDebounce();
        _dangCapNhatPresetNgay = true;
        _selectedInvoiceDatePreset = "Mọi ngày";
        InvoiceFilterStartDate = null;
        InvoiceFilterEndDate = null;
        InvoiceCustomStartDateDraft = null;
        InvoiceCustomEndDateDraft = null;
        _dangCapNhatPresetNgay = false;
        OnPropertyChanged(nameof(SelectedInvoiceDatePreset));
        OnPropertyChanged(nameof(IsInvoiceCustomDatePreset));
        OnPropertyChanged(nameof(InvoiceDateFilterDisplayText));
        ThongBaoPresetNgayHoaDon();
        InvoiceFilterMinTotal = "";
        InvoiceFilterMaxTotal = "";

        _selectedInvoicePaymentStatus = "Tất cả";
        OnPropertyChanged(nameof(SelectedInvoicePaymentStatus));

        _selectedInvoiceStatus = "Tất cả";
        OnPropertyChanged(nameof(SelectedInvoiceStatus));
        ThongBaoTrangThaiHoaDon();

        _selectedInvoiceCustomer = InvoiceCustomerOptions.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedInvoiceCustomer));
        OnPropertyChanged(nameof(HasSelectedInvoiceCustomer));
        OnPropertyChanged(nameof(SelectedInvoiceCustomerDisplayText));
        InvoiceCustomerSearch = "";
        IsInvoiceCustomerSuggestionsClosed = true;
        InvoiceCustomerSelectedIndex = -1;
        CapNhatInvoiceCustomerFilterOptions();

        _selectedInvoiceEmployee = InvoiceEmployeeOptions.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedInvoiceEmployee));

        OnPropertyChanged(nameof(InvoiceActiveFiltersText));
        CapNhatInvoiceActiveFilterChips();
        InvoicePage = 1;

        await SearchInvoicesAsync(giuTrang: true);
        StatusMessage = "Đã đặt lại toàn bộ bộ lọc hóa đơn về mặc định.";
    }
}
