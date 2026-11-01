using System;
using System.Collections.Generic;
using System.Globalization;
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

    public IReadOnlyList<string> InvoiceDatePresets { get; } =
    [
        "Hôm nay",
        "Hôm qua",
        "7 ngày qua",
        "30 ngày qua",
        "Tuần này",
        "Tuần trước",
        "Tháng này",
        "Tháng trước",
        "Quý này",
        "Năm nay",
        "Tất cả",
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
                    ApDungPresetNgayHoaDon(value, tuDongTim: true);
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

    // Thống kê tổng hợp kết quả lọc hóa đơn
    [ObservableProperty] private int invoiceTongSoLuong;
    [ObservableProperty] private decimal invoiceTongTienDoanhThu;
    [ObservableProperty] private decimal invoiceTongDaThanhToan;
    [ObservableProperty] private decimal invoiceTongConNo;
    [ObservableProperty] private int invoiceTongDaHuy;

    public string InvoiceTongSoLuongText => $"{InvoiceTongSoLuong:N0} hóa đơn";

    public string InvoiceSummaryText =>
        $"Doanh thu: {InvoiceTongTienDoanhThu:N0} đ   •   Đã thu: {InvoiceTongDaThanhToan:N0} đ   •   Còn nợ: {InvoiceTongConNo:N0} đ"
        + (InvoiceTongDaHuy > 0 ? $"   (Loại trừ {InvoiceTongDaHuy} HĐ đã hủy)" : "");

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
                    int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                    var monday = today.AddDays(-diff);
                    InvoiceFilterStartDate = new DateTimeOffset(monday);
                    InvoiceFilterEndDate = new DateTimeOffset(monday.AddDays(6));
                    break;

                case "Tuần trước":
                    int diffW = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                    var lastMonday = today.AddDays(-diffW - 7);
                    InvoiceFilterStartDate = new DateTimeOffset(lastMonday);
                    InvoiceFilterEndDate = new DateTimeOffset(lastMonday.AddDays(6));
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

                case "Tất cả":
                    InvoiceFilterStartDate = null;
                    InvoiceFilterEndDate = null;
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
            _ = SearchInvoicesAsync();
        }
    }

    private void CapNhatThongKeHoaDon()
    {
        int tongSo = HoaDons.Count;
        decimal doanhThu = 0m;
        decimal daThanhToan = 0m;
        decimal conNo = 0m;
        int daHuy = 0;

        foreach (var hd in HoaDons)
        {
            if (hd.LaHoaDonDaHuy)
            {
                daHuy++;
            }
            else
            {
                doanhThu += hd.KhachCanTra;
                daThanhToan += hd.KhachDaThanhToan;
                conNo += hd.ConNo;
            }
        }

        InvoiceTongSoLuong = tongSo;
        InvoiceTongTienDoanhThu = doanhThu;
        InvoiceTongDaThanhToan = daThanhToan;
        InvoiceTongConNo = conNo;
        InvoiceTongDaHuy = daHuy;

        OnPropertyChanged(nameof(InvoiceTongSoLuongText));
        OnPropertyChanged(nameof(InvoiceSummaryText));
    }
}
