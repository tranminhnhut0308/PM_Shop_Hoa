using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

public enum BaoCaoNhaCungCapKieuHienThi { BieuDo, BaoCao }

/// <summary>
/// ViewModel cho màn hình "Báo cáo nhà cung cấp":
/// - Panel bộ lọc bên trái: Kiểu hiển thị (Biểu đồ / Báo cáo), Mối quan tâm, Thời gian, Tìm kiếm nhà cung cấp.
/// - Bên phải: Biểu đồ thanh ngang Top 10 nhà cung cấp nhập hàng nhiều nhất (đã trừ trả hàng) HOẶC Bảng dữ liệu chi tiết có sắp xếp + phân trang.
/// - Không hard-code, truy vấn dữ liệu thực tế từ database với hiệu năng cao.
/// </summary>
public partial class BaoCaoNhaCungCapViewModel : ObservableObject
{
    private const string NhanTuyChinh = "Tùy chỉnh";
    private readonly BaoCaoNhaCungCapService _service;

    private bool _khoiTaoXong;
    private bool _dangDatBoLoc;
    private CancellationTokenSource? _searchCts;
    private int _phienBan;

    public BaoCaoNhaCungCapViewModel(DatabaseService db)
    {
        _service = new BaoCaoNhaCungCapService(db.TaoKetNoiAsync);

        KieuHienThi = BaoCaoNhaCungCapKieuHienThi.BieuDo;
        MocQuanTamChon = MocQuanTamOptions[0];
        SapXepKho = BaoCaoNhaCungCapService.KhoaMacDinh;
        SapXepTang = false;
        TimKiem = string.Empty;
        SoDongTrang = 15;
        Trang = 1;

        // Mặc định chọn "Tuần này" như giao diện tham khảo
        DatKhoangTuanNay();

        _khoiTaoXong = true;
    }

    // ============================================================
    // TRẠNG THÁI / CHẾ ĐỘ HIỂN THỊ
    // ============================================================

    [ObservableProperty] private BaoCaoNhaCungCapKieuHienThi kieuHienThi;
    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    public bool CoLoi => !string.IsNullOrEmpty(ThongBaoLoi);

    /// <summary>Kiểu hiển thị là Biểu đồ.</summary>
    public bool HienThiBieuDo
    {
        get => KieuHienThi == BaoCaoNhaCungCapKieuHienThi.BieuDo;
        set
        {
            if (value)
                KieuHienThi = BaoCaoNhaCungCapKieuHienThi.BieuDo;
        }
    }

    /// <summary>Kiểu hiển thị là Báo cáo.</summary>
    public bool HienThiBaoCao
    {
        get => KieuHienThi == BaoCaoNhaCungCapKieuHienThi.BaoCao;
        set
        {
            if (value)
                KieuHienThi = BaoCaoNhaCungCapKieuHienThi.BaoCao;
        }
    }

    partial void OnKieuHienThiChanged(BaoCaoNhaCungCapKieuHienThi value)
    {
        OnPropertyChanged(nameof(HienThiBieuDo));
        OnPropertyChanged(nameof(HienThiBaoCao));
    }

    [RelayCommand]
    private void ChonBieuDo() => KieuHienThi = BaoCaoNhaCungCapKieuHienThi.BieuDo;

    [RelayCommand]
    private void ChonBaoCao() => KieuHienThi = BaoCaoNhaCungCapKieuHienThi.BaoCao;

    // ============================================================
    // MỐI QUAN TÂM
    // ============================================================

    public IReadOnlyList<string> MocQuanTamOptions { get; } = ["Nhập hàng"];

    [ObservableProperty] private string mocQuanTamChon = "Nhập hàng";

    // ============================================================
    // BỘ LỌC THỜI GIAN
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime tuNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime denNgay;

    public IReadOnlyList<string> PresetThoiGianOptions { get; } = ["Tuần này", "Tháng này", "Quý này", "Năm nay"];

    [ObservableProperty]
    private string presetThoiGianChon = "Tuần này";

    public bool DangChonPresetThoiGian => KhoangThoiGian != NhanTuyChinh;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    [NotifyPropertyChangedFor(nameof(DangChonTuanNay))]
    [NotifyPropertyChangedFor(nameof(DangChonThangNay))]
    [NotifyPropertyChangedFor(nameof(DangChonQuyNay))]
    [NotifyPropertyChangedFor(nameof(DangChonNamNay))]
    [NotifyPropertyChangedFor(nameof(DangChonTuyChinh))]
    [NotifyPropertyChangedFor(nameof(DangChonPresetThoiGian))]
    private string khoangThoiGian = "Tuần này";

    public bool DangChonTuanNay => KhoangThoiGian == "Tuần này";
    public bool DangChonThangNay => KhoangThoiGian == "Tháng này";
    public bool DangChonQuyNay => KhoangThoiGian == "Quý này";
    public bool DangChonNamNay => KhoangThoiGian == "Năm nay";
    public bool DangChonTuyChinh => KhoangThoiGian == NhanTuyChinh;

    partial void OnPresetThoiGianChonChanged(string value)
    {
        if (!_khoiTaoXong || _dangDatBoLoc) return;
        _ = ChonKhoangThoiGianAsync(value);
    }

    [RelayCommand]
    private async Task ChonPresetThoiGianAsync()
    {
        if (KhoangThoiGian == NhanTuyChinh)
        {
            await ChonKhoangThoiGianAsync(PresetThoiGianChon);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? tuNgayChon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? denNgayChon;

    public bool CoKhoangTuyChinh => TuNgayChon is not null && DenNgayChon is not null;

    public string KhoangHienThi => $"Từ {TuNgay:dd/MM/yyyy} đến {DenNgay:dd/MM/yyyy}";

    [RelayCommand]
    private async Task ChonKhoangThoiGianAsync(string? nhan)
    {
        if (string.IsNullOrWhiteSpace(nhan))
            return;

        if (nhan == NhanTuyChinh)
        {
            KhoangThoiGian = NhanTuyChinh;
            TuNgayChon ??= new DateTimeOffset(TuNgay);
            DenNgayChon ??= new DateTimeOffset(DenNgay);
            return;
        }

        if (PresetThoiGianOptions.Contains(nhan))
        {
            PresetThoiGianChon = nhan;
        }

        _dangDatBoLoc = true;
        try
        {
            switch (nhan)
            {
                case "Tuần này":
                    DatKhoangTuanNay();
                    break;
                case "Tháng này":
                    DatKhoangThangNay();
                    break;
                case "Quý này":
                    DatKhoangQuyNay();
                    break;
                case "Năm nay":
                    DatKhoangNamNay();
                    break;
            }
        }
        finally
        {
            _dangDatBoLoc = false;
        }

        Trang = 1;
        await TaiLaiAsync();
    }

    private void DatKhoangTuanNay()
    {
        var homNay = DateTime.Today;
        var soNgayDaQua = ((int)homNay.DayOfWeek + 6) % 7; // Thứ 2 là đầu tuần
        TuNgay = homNay.AddDays(-soNgayDaQua);
        DenNgay = homNay;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
        KhoangThoiGian = "Tuần này";
    }

    private void DatKhoangThangNay()
    {
        var homNay = DateTime.Today;
        TuNgay = new DateTime(homNay.Year, homNay.Month, 1);
        DenNgay = homNay;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
        KhoangThoiGian = "Tháng này";
    }

    private void DatKhoangQuyNay()
    {
        var homNay = DateTime.Today;
        int quy = (homNay.Month - 1) / 3;
        TuNgay = new DateTime(homNay.Year, quy * 3 + 1, 1);
        DenNgay = homNay;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
        KhoangThoiGian = "Quý này";
    }

    private void DatKhoangNamNay()
    {
        var homNay = DateTime.Today;
        TuNgay = new DateTime(homNay.Year, 1, 1);
        DenNgay = homNay;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
        KhoangThoiGian = "Năm nay";
    }

    [RelayCommand]
    private async Task ApDungTuyChinhAsync()
    {
        if (TuNgayChon is null || DenNgayChon is null)
            return;

        var tu = TuNgayChon.Value.Date;
        var den = DenNgayChon.Value.Date;

        if (tu > den)
            (tu, den) = (den, tu);

        _dangDatBoLoc = true;
        TuNgay = tu;
        DenNgay = den;
        _dangDatBoLoc = false;

        Trang = 1;
        await TaiLaiAsync();
    }

    // ============================================================
    // TÌM KIẾM NHÀ CUNG CẤP
    // ============================================================

    [ObservableProperty] private string timKiem = string.Empty;

    partial void OnTimKiemChanged(string value)
    {
        if (!_khoiTaoXong || _dangDatBoLoc)
            return;

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                if (token.IsCancellationRequested)
                    return;

                Trang = 1;
                await TaiLaiAsync(token);
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    // ============================================================
    // SẮP XẾP BẢNG
    // ============================================================

    [ObservableProperty] private string sapXepKho = BaoCaoNhaCungCapService.KhoaMacDinh;
    [ObservableProperty] private bool sapXepTang;

    [RelayCommand]
    private async Task SapXepAsync(string? cot)
    {
        if (string.IsNullOrWhiteSpace(cot))
            return;

        if (string.Equals(SapXepKho, cot, StringComparison.OrdinalIgnoreCase))
        {
            SapXepTang = !SapXepTang;
        }
        else
        {
            SapXepKho = cot;
            SapXepTang = false;
        }

        Trang = 1;
        await TaiLaiAsync();
    }

    public string NhanSapXep(string cot)
    {
        if (!string.Equals(SapXepKho, cot, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return SapXepTang ? " ▲" : " ▼";
    }

    // ============================================================
    // PHÂN TRANG
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoTrang))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    private int trang = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoTrang))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    private int soDongTrang = 15;

    public IReadOnlyList<int> SoDongTrangOptions { get; } = [15, 25, 50, 100];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoTrang))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    private int tongSoBanGhi;

    public int TongSoTrang => SoDongTrang <= 0
        ? 1
        : Math.Max(1, (int)Math.Ceiling(TongSoBanGhi / (double)SoDongTrang));

    public bool CanTrangTruoc => Trang > 1 && !DangTai;
    public bool CanTrangSau => Trang < TongSoTrang && !DangTai;

    public string RangeText
    {
        get
        {
            if (TongSoBanGhi == 0)
                return "0 nhà cung cấp";

            var tu = (Trang - 1) * SoDongTrang + 1;
            var den = Math.Min(tu + SoDongTrang - 1, TongSoBanGhi);
            return $"{tu} - {den} trong {TongSoBanGhi:N0} nhà cung cấp";
        }
    }

    partial void OnSoDongTrangChanged(int value)
    {
        if (!_khoiTaoXong)
            return;

        Trang = 1;
        _ = TaiLaiAsync();
    }

    [RelayCommand]
    private async Task TrangDauAsync()
    {
        if (Trang == 1) return;
        Trang = 1;
        await TaiLaiAsync();
    }

    [RelayCommand]
    private async Task TrangTruocAsync()
    {
        if (Trang <= 1) return;
        Trang--;
        await TaiLaiAsync();
    }

    [RelayCommand]
    private async Task TrangSauAsync()
    {
        if (Trang >= TongSoTrang) return;
        Trang++;
        await TaiLaiAsync();
    }

    [RelayCommand]
    private async Task TrangCuoiAsync()
    {
        if (Trang == TongSoTrang) return;
        Trang = TongSoTrang;
        await TaiLaiAsync();
    }

    // ============================================================
    // DỮ LIỆU HIỂN THỊ
    // ============================================================

    [ObservableProperty] private BaoCaoNhaCungCapTongHop tongHop = new();

    public ObservableCollection<BaoCaoNhaCungCapTop> TopItems { get; } = new();
    public ObservableCollection<BaoCaoNhaCungCapDong> DongTrangs { get; } = new();
    public ObservableCollection<BaoCaoNhaCungCapScaleMark> ScaleMarks { get; } = new();

    [ObservableProperty] private bool coTop;
    [ObservableProperty] private bool coDongTrang;

    public string TieuDeBieuDo => "Top 10 nhà cung cấp nhập hàng nhiều nhất (đã trừ trả hàng)";

    [ObservableProperty] private decimal maxScaleValue = 30_000_000m;
    private string[] _scaleLabels = ["0", "3 tr", "6 tr", "9 tr", "12 tr", "15 tr", "18 tr", "21 tr", "24 tr", "27 tr", "30 tr"];

    public string ScaleMark0 => _scaleLabels.Length > 0 ? _scaleLabels[0] : "0";
    public string ScaleMark1 => _scaleLabels.Length > 1 ? _scaleLabels[1] : "";
    public string ScaleMark2 => _scaleLabels.Length > 2 ? _scaleLabels[2] : "";
    public string ScaleMark3 => _scaleLabels.Length > 3 ? _scaleLabels[3] : "";
    public string ScaleMark4 => _scaleLabels.Length > 4 ? _scaleLabels[4] : "";
    public string ScaleMark5 => _scaleLabels.Length > 5 ? _scaleLabels[5] : "";
    public string ScaleMark6 => _scaleLabels.Length > 6 ? _scaleLabels[6] : "";
    public string ScaleMark7 => _scaleLabels.Length > 7 ? _scaleLabels[7] : "";
    public string ScaleMark8 => _scaleLabels.Length > 8 ? _scaleLabels[8] : "";
    public string ScaleMark9 => _scaleLabels.Length > 9 ? _scaleLabels[9] : "";
    public string ScaleMark10 => _scaleLabels.Length > 10 ? _scaleLabels[10] : "";

    private double _currentPlotWidth = 0;

    public void CapNhatDoRongBieuDo(double plotWidth)
    {
        if (plotWidth <= 20) return;
        _currentPlotWidth = plotWidth;
        if (MaxScaleValue <= 0) return;

        foreach (var item in TopItems)
        {
            item.RongBar = Math.Max(0, (double)(item.GiaTriNhapHang / MaxScaleValue) * plotWidth);
        }
    }

    // ============================================================
    // TẢI DỮ LIỆU
    // ============================================================

    [RelayCommand]
    public async Task TaiLaiAsync(CancellationToken token = default)
    {
        var phienBanHienTai = ++_phienBan;
        DangTai = true;
        ThongBaoLoi = string.Empty;

        try
        {
            var res = await _service.TaiDuLieuAsync(
                TuNgay,
                DenNgay,
                TimKiem,
                SapXepKho,
                SapXepTang,
                Trang,
                SoDongTrang,
                token);

            if (phienBanHienTai != _phienBan)
                return;

            TongHop = res.TongHop;
            TongSoBanGhi = res.TongSoBanGhi;
            MaxScaleValue = res.MaxScaleValue;

            if (res.ScaleLabels is { Length: >= 11 })
            {
                _scaleLabels = res.ScaleLabels;
                for (int i = 0; i <= 10; i++)
                    OnPropertyChanged($"ScaleMark{i}");
            }

            TopItems.Clear();
            foreach (var item in res.Top)
            {
                if (_currentPlotWidth > 20 && MaxScaleValue > 0)
                {
                    item.RongBar = Math.Max(0, (double)(item.GiaTriNhapHang / MaxScaleValue) * _currentPlotWidth);
                }
                TopItems.Add(item);
            }

            DongTrangs.Clear();
            foreach (var item in res.DongTrang)
                DongTrangs.Add(item);

            ScaleMarks.Clear();
            foreach (var m in res.ScaleMarks)
                ScaleMarks.Add(m);

            CoTop = TopItems.Count > 0;
            CoDongTrang = DongTrangs.Count > 0;

            OnPropertyChanged(nameof(RangeText));
            OnPropertyChanged(nameof(TongSoTrang));
            OnPropertyChanged(nameof(CanTrangTruoc));
            OnPropertyChanged(nameof(CanTrangSau));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (phienBanHienTai == _phienBan)
            {
                ThongBaoLoi = "Không thể tải báo cáo nhà cung cấp: " + ex.Message;
            }
        }
        finally
        {
            if (phienBanHienTai == _phienBan)
            {
                DangTai = false;
                OnPropertyChanged(nameof(CanTrangTruoc));
                OnPropertyChanged(nameof(CanTrangSau));
            }
        }
    }
}
