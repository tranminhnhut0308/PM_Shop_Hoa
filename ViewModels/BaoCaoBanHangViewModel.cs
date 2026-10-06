using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;
using ClosedXML.Excel;

namespace ShopHoa.ViewModels;

public enum BaoCaoBanHangKieuHienThi { BieuDo, BaoCao }
public enum BaoCaoBanHangMocQuanTam { ThoiGian, SanPham, KhachHang, PhuongThucThanhToan }

/// <summary>
/// ViewModel cho màn "Báo cáo bán hàng": tổng hợp doanh thu theo khoảng thời gian,
/// biểu đồ, bảng theo ngày, top sản phẩm / khách hàng và phương thức thanh toán.
/// <para>
/// Khác với <see cref="BaoCaoCuoiNgayViewModel"/> (chốt số của MỘT ngày), màn này
/// luôn làm việc trên khoảng <c>TuNgay → DenNgay</c>.
/// </para>
/// </summary>
public partial class BaoCaoBanHangViewModel : ObservableObject
{
    private readonly BaoCaoBanHangService _service;

    public BaoCaoBanHangViewModel(DatabaseService db)
    {
        _service = new BaoCaoBanHangService(db.TaoKetNoiAsync);

        KieuHienThi = BaoCaoBanHangKieuHienThi.BieuDo;
        MocQuanTam = BaoCaoBanHangMocQuanTam.ThoiGian;

        TuNgay = DateTime.Today.AddDays(-6);
        DenNgay = DateTime.Today;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
        KhoangThoiGian = "7 ngày";
        TuyChinh = false;

        // Không tải dữ liệu trong constructor: MainWindowViewModel chỉ gọi
        // TaiLaiAsync() khi người dùng mở màn lần đầu (giống BaoCaoCuoiNgayViewModel),
        // tránh truy vấn database ngay khi khởi động ứng dụng.
    }

    // ============================================================
    // TRẠNG THÁI
    // ============================================================

    [ObservableProperty] private BaoCaoBanHangKieuHienThi kieuHienThi;
    [ObservableProperty] private BaoCaoBanHangMocQuanTam mocQuanTam;
    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    // ============================================================
    // BỘ LỌC KHOẢNG THỜI GIAN
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime tuNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime denNgay;

    /// <summary>Tên khoảng đang xem: "Hôm nay", "Tuần này", "Tùy chọn"...</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    [NotifyPropertyChangedFor(nameof(DangChonHomNay))]
    [NotifyPropertyChangedFor(nameof(DangChonHomQua))]
    [NotifyPropertyChangedFor(nameof(DangChonTuanNay))]
    [NotifyPropertyChangedFor(nameof(DangChonThangNay))]
    [NotifyPropertyChangedFor(nameof(DangChonTuyChinh))]
    private string khoangThoiGian = "7 ngày";

    /// <summary>Cờ cho biết khoảng đang xem do người dùng tự chọn ngày.</summary>
    [ObservableProperty] private bool tuyChinh;

    /// <summary>Đánh dấu nút khoảng thời gian đang được chọn trên header.</summary>
    public bool DangChonHomNay => KhoangThoiGian == "Hôm nay";
    public bool DangChonHomQua => KhoangThoiGian == "Hôm qua";
    public bool DangChonTuanNay => KhoangThoiGian == "Tuần này";
    public bool DangChonThangNay => KhoangThoiGian == "Tháng này";
    public bool DangChonTuyChinh => KhoangThoiGian == "Tùy chọn";

    /// <summary>Từ ngày trên DatePicker (kiểu khớp với DatePicker.SelectedDate).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? tuNgayChon;

    /// <summary>Đến ngày trên DatePicker.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? denNgayChon;

    /// <summary>Có đủ ngày bắt đầu/kết thúc để bấm "Áp dụng" không.</summary>
    public bool CoKhoangTuyChinh => TuNgayChon is not null && DenNgayChon is not null;

    /// <summary>Khoảng thời gian đang xem, ví dụ: "Khoảng thời gian: 30/09/2026 - 06/10/2026".</summary>
    public string KhoangHienThi => $"Khoảng thời gian: {TuNgay:dd/MM/yyyy} - {DenNgay:dd/MM/yyyy}";

    // ============================================================
    // DỮ LIỆU
    // ============================================================

    [ObservableProperty] private BaoCaoBanHangDuLieu duLieu = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoNgayChiTiet))]
    [NotifyPropertyChangedFor(nameof(NgayChiTietHienThi))]
    private DateTime? ngayChiTiet;

    public ObservableCollection<BaoCaoBanHangDiemThoiGian> BieuDoItems => new(DuLieu.BieuDo);
    public ObservableCollection<BaoCaoBanHangBangRow> BangItems => new(DuLieu.BangDuLieu);
    public ObservableCollection<BaoCaoBanHangTopSanPham> TopSanPhamItems => new(DuLieu.TopSanPham);
    public ObservableCollection<BaoCaoBanHangTopKhachHang> TopKhachHangItems => new(DuLieu.TopKhachHang);
    public ObservableCollection<BaoCaoBanHangPhuongThucRow> PhuongThucItems => new(DuLieu.PhuongThucThanhToan);
    public ObservableCollection<BaoCaoBanHangChiTietHoaDon> ChiTietHoaDonList => new(DuLieu.ChiTietHoaDon);

    public BaoCaoBanHangTongHop TongHop => DuLieu.TongHop;

    // ============================================================
    // CỜ HIỂN THỊ
    // ============================================================

    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);
    public bool CoBieuDo => DuLieu.BieuDo.Count > 0;
    public bool CoBang => DuLieu.BangDuLieu.Count > 0;
    public bool CoTopSanPham => DuLieu.TopSanPham.Count > 0;
    public bool CoTopKhachHang => DuLieu.TopKhachHang.Count > 0;
    public bool CoPhuongThuc => DuLieu.PhuongThucThanhToan.Count > 0;
    public bool CoChiTietHoaDon => DuLieu.ChiTietHoaDon.Count > 0;
    public bool CoNgayChiTiet => NgayChiTiet is not null;

    public string NgayChiTietHienThi => NgayChiTiet?.ToString("dd/MM/yyyy") ?? string.Empty;

    /// <summary>Thông báo lại toàn bộ cờ cờ rỗng / có dữ liệu sau mỗi lần tải.</summary>
    private void ThongBaoCoGiaTri()
    {
        OnPropertyChanged(nameof(CoBieuDo));
        OnPropertyChanged(nameof(CoBang));
        OnPropertyChanged(nameof(CoTopSanPham));
        OnPropertyChanged(nameof(CoTopKhachHang));
        OnPropertyChanged(nameof(CoPhuongThuc));
        OnPropertyChanged(nameof(CoChiTietHoaDon));
        OnPropertyChanged(nameof(TongHop));
    }

    // ============================================================
    // TẢI DỮ LIỆU
    // ============================================================

    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        await TaiDuLieuAsync();
    }

    [RelayCommand]
    private async Task TaiDuLieuAsync()
    {
        try
        {
            DangTai = true;
            ThongBaoLoi = string.Empty;

            if (DenNgay < TuNgay)
                DenNgay = TuNgay;

            DuLieu = await _service.TaiDuLieuAsync(TuNgay, DenNgay, 1, TuyChinh, default);

            OnPropertyChanged(nameof(BieuDoItems));
            OnPropertyChanged(nameof(BangItems));
            OnPropertyChanged(nameof(TopSanPhamItems));
            OnPropertyChanged(nameof(TopKhachHangItems));
            OnPropertyChanged(nameof(PhuongThucItems));
            OnPropertyChanged(nameof(ChiTietHoaDonList));
            ThongBaoCoGiaTri();
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không thể tải báo cáo bán hàng: " + ex.Message;
        }
        finally
        {
            DangTai = false;
        }
    }

    // ============================================================
    // LỆNH CHỌN KHOẢNG THỜI GIAN
    // ============================================================

    private async Task DatKhoangAsync(DateTime tu, DateTime den, string nhan)
    {
        TuNgay = tu.Date;
        DenNgay = den.Date;
        TuNgayChon = new DateTimeOffset(tu.Date);
        DenNgayChon = new DateTimeOffset(den.Date);
        KhoangThoiGian = nhan;
        TuyChinh = nhan == "Tùy chọn";

        await TaiDuLieuAsync();
    }

    [RelayCommand]
    private async Task VeHomNayAsync()
    {
        await DatKhoangAsync(DateTime.Today, DateTime.Today, "Hôm nay");
    }

    [RelayCommand]
    private async Task VeNgayQuaAsync()
    {
        var homQua = DateTime.Today.AddDays(-1);
        await DatKhoangAsync(homQua, homQua, "Hôm qua");
    }

    [RelayCommand]
    private async Task VeTuanNayAsync()
    {
        // Tuần bắt đầu từ thứ Hai.
        var homNay = DateTime.Today;
        var soNgayDaQua = ((int)homNay.DayOfWeek + 6) % 7;
        await DatKhoangAsync(homNay.AddDays(-soNgayDaQua), homNay, "Tuần này");
    }

    [RelayCommand]
    private async Task VeThangNayAsync()
    {
        var homNay = DateTime.Today;
        await DatKhoangAsync(new DateTime(homNay.Year, homNay.Month, 1), homNay, "Tháng này");
    }

    [RelayCommand]
    private async Task ApDungTuyChinhAsync()
    {
        if (TuNgayChon is null || DenNgayChon is null)
            return;

        var tu = TuNgayChon.Value.Date;
        var den = DenNgayChon.Value.Date;

        // Người dùng chọn ngược vẫn phải chạy được.
        if (den < tu)
            (tu, den) = (den, tu);

        await DatKhoangAsync(tu, den, "Tùy chọn");
    }

    // ============================================================
    // LỆNH KHÁC
    // ============================================================

    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        try
        {
            var cuaSo = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt
                ? lt.MainWindow
                : null;

            if (cuaSo is null)
                return;

            using (var wb = BaoCaoBanHangExcel.TaoWorkbook(DuLieu))
            {
                var file = await cuaSo.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Xuất Excel báo cáo bán hàng",
                    FileTypeChoices = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } }
                    },
                    SuggestedFileName = DuLieu.TongHop.TenFileXuat()
                });

                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    wb.SaveAs(stream);
                }
            }
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không xuất được file Excel: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task XemChiTietNgayAsync(DateTime ngay)
    {
        try
        {
            NgayChiTiet = ngay.Date;
            DuLieu.ChiTietHoaDon = await _service.TaiChiTietHoaDonAsync(ngay, default);

            OnPropertyChanged(nameof(ChiTietHoaDonList));
            OnPropertyChanged(nameof(CoChiTietHoaDon));
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không tải được chi tiết hóa đơn: " + ex.Message;
        }
    }

    [RelayCommand]
    private void DongChiTiet()
    {
        NgayChiTiet = null;
        DuLieu.ChiTietHoaDon = new System.Collections.Generic.List<BaoCaoBanHangChiTietHoaDon>();

        OnPropertyChanged(nameof(ChiTietHoaDonList));
        OnPropertyChanged(nameof(CoChiTietHoaDon));
    }
}
