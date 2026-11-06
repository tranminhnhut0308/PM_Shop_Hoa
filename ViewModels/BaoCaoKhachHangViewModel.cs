using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

public enum BaoCaoKhachHangKieuHienThi { BieuDo, BaoCao }

public enum BaoCaoKhachHangMocQuanTam { BanHang, CongNo, SoDonHang, GiaTriMuaHang }

/// <summary>
/// ViewModel cho màn "Báo cáo khách hàng": thẻ thống kê (tổng khách hàng,
/// doanh số, số đơn, công nợ), biểu đồ top khách hàng và bảng chi tiết có
/// sắp xếp + phân trang.
/// <para>
/// Giữ instance sống suốt phiên như <see cref="BaoCaoBanHangViewModel"/>;
/// không tải dữ liệu trong constructor — <see cref="MainWindowViewModel"/>
/// chỉ gọi <see cref="TaiLaiAsync"/> khi người dùng mở màn lần đầu.
/// </para>
/// <para>
/// Mọi truy vấn tính trong SQL (GROUP BY / SUM / LIMIT) nên đổi bộ lọc là
/// query lại đúng phần dữ liệu cần, không kéo toàn bảng về UI.
/// </para>
/// </summary>
public partial class BaoCaoKhachHangViewModel : ObservableObject
{
    /// <summary>Nhãn RadioButton "Tùy chỉnh" — chỉ hiện ô chọn ngày, chờ bấm "Áp dụng".</summary>
    private const string NhanTuyChinh = "Tùy chỉnh";

    private readonly BaoCaoKhachHangService _service;

    /// <summary>Đã gán xong giá trị mặc định trong constructor → mới bắt đầu tự tải.</summary>
    private bool _khoiTaoXong;

    /// <summary>Đang gán nhiều bộ lọc một lần (chọn khoảng thời gian) → không tự tải từng property.</summary>
    private bool _dangDatBoLoc;

    /// <summary>Đang tự điều chỉnh trang cho vừa dữ liệu → không tự tải lại (tránh lặp).</summary>
    private bool _dangDieuChinhTrang;

    private bool _dangDongBoSapXepNhanh;

    private CancellationTokenSource? _treCts;
    private int _phienBan;

    public BaoCaoKhachHangViewModel(DatabaseService db)
    {
        _service = new BaoCaoKhachHangService(db.TaoKetNoiAsync);

        KieuHienThi = BaoCaoKhachHangKieuHienThi.BieuDo;
        MocQuanTamChon = MocQuanTamOptions[0];
        MocQuanTam = BaoCaoKhachHangMocQuanTam.BanHang;
        SapXepKho = string.Empty;
        SapXepTang = false;
        TimKiem = string.Empty;
        SoDongTrang = 15;
        Trang = 1;

        // Mở màn lần đầu mặc định "Tháng này" — đủ dữ liệu để thấy khách hàng
        // ngay, tránh màn trống với khoảng "Tuần này" quá ngắn.
        DatKhoangThangNay();

        // Không tải dữ liệu trong constructor: MainWindowViewModel chỉ gọi
        // TaiLaiAsync() khi người dùng mở màn lần đầu.
        _khoiTaoXong = true;
    }

    // ============================================================
    // TRẠNG THÁI / CHẾ ĐỘ HIỂN THỊ
    // ============================================================

    [ObservableProperty] private BaoCaoKhachHangKieuHienThi kieuHienThi;
    [ObservableProperty] private BaoCaoKhachHangMocQuanTam mocQuanTam;
    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    /// <summary>RadioButton "Biểu đồ" bind TwoWay vào chế độ hiển thị.</summary>
    public bool HienThiBieuDo
    {
        get => KieuHienThi == BaoCaoKhachHangKieuHienThi.BieuDo;
        set { if (value) KieuHienThi = BaoCaoKhachHangKieuHienThi.BieuDo; }
    }

    /// <summary>RadioButton "Báo cáo" bind TwoWay vào chế độ hiển thị.</summary>
    public bool HienThiBaoCao
    {
        get => KieuHienThi == BaoCaoKhachHangKieuHienThi.BaoCao;
        set { if (value) KieuHienThi = BaoCaoKhachHangKieuHienThi.BaoCao; }
    }

    partial void OnKieuHienThiChanged(BaoCaoKhachHangKieuHienThi value)
    {
        OnPropertyChanged(nameof(HienThiBieuDo));
        OnPropertyChanged(nameof(HienThiBaoCao));
    }

    // ============================================================
    // BỘ LỌC THỜI GIAN
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime tuNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime denNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    [NotifyPropertyChangedFor(nameof(DangChonTuanNay))]
    [NotifyPropertyChangedFor(nameof(DangChonThangNay))]
    [NotifyPropertyChangedFor(nameof(DangChonQuyNay))]
    [NotifyPropertyChangedFor(nameof(DangChonNamNay))]
    [NotifyPropertyChangedFor(nameof(DangChonTuyChinh))]
    private string khoangThoiGian = "Tháng này";

    public bool DangChonTuanNay => KhoangThoiGian == "Tuần này";
    public bool DangChonThangNay => KhoangThoiGian == "Tháng này";
    public bool DangChonQuyNay => KhoangThoiGian == "Quý này";
    public bool DangChonNamNay => KhoangThoiGian == "Năm nay";
    public bool DangChonTuyChinh => KhoangThoiGian == NhanTuyChinh;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? tuNgayChon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? denNgayChon;

    public bool CoKhoangTuyChinh => TuNgayChon is not null && DenNgayChon is not null;

    public string KhoangHienThi => $"Khoảng thời gian: {TuNgay:dd/MM/yyyy} - {DenNgay:dd/MM/yyyy}";

    // ============================================================
    // BỘ LỌC KHÁCH HÀNG / MỐI QUAN TÂM
    // ============================================================

    /// <summary>Tìm theo mã, tên hoặc số điện thoại (chờ nút "Áp dụng" / Enter).</summary>
    [ObservableProperty] private string timKiem = string.Empty;

    public ObservableCollection<string> MocQuanTamOptions { get; } = new()
    {
        "Bán hàng",
        "Công nợ",
        "Số lượng đơn hàng",
        "Giá trị mua hàng"
    };

    [ObservableProperty] private string? mocQuanTamChon;

    public ObservableCollection<string> SapXepNhanhOptions { get; } = new()
    {
        "Tên khách hàng A → Z",
        "Tên khách hàng Z → A",
        "Số đơn hàng tăng dần",
        "Số đơn hàng giảm dần",
        "Tổng tiền hàng cao nhất",
        "Tổng tiền hàng thấp nhất",
        "Công nợ cao nhất",
        "Công nợ thấp nhất",
        "Đã thanh toán cao nhất",
        "Đã thanh toán thấp nhất"
    };

    [ObservableProperty] private string? sapXepNhanhChon;

    // ============================================================
    // DỮ LIỆU
    // ============================================================

    [ObservableProperty] private BaoCaoKhachHangDuLieu duLieu = new();

    public BaoCaoKhachHangTongHopKy TongHop => DuLieu.TongHop;

    public ObservableCollection<BaoCaoKhachHangTop> TopItems { get; } = new();

    public ObservableCollection<BaoCaoKhachHangDong> DongTrangItems { get; } = new();

    public bool CoTop => TopItems.Count > 0;
    public bool CoBang => DongTrangItems.Count > 0;
    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);

    /// <summary>Tiêu đề biểu đồ thay đổi theo "Mối quan tâm" đang chọn.</summary>
    public string TenBieuDo => MocQuanTam switch
    {
        BaoCaoKhachHangMocQuanTam.CongNo => "Top khách hàng công nợ lớn nhất",
        BaoCaoKhachHangMocQuanTam.SoDonHang => "Top khách hàng có nhiều đơn nhất",
        BaoCaoKhachHangMocQuanTam.GiaTriMuaHang => "Top khách hàng giá trị mua lớn nhất",
        _ => "Top khách hàng mua nhiều nhất"
    };

    public string TenBieuDoPhu => MocQuanTam switch
    {
        BaoCaoKhachHangMocQuanTam.CongNo =>
            "Công nợ phát sinh trong khoảng thời gian đang chọn, sắp xếp giảm dần.",
        BaoCaoKhachHangMocQuanTam.SoDonHang =>
            "Số đơn hàng của khách trong khoảng thời gian đang chọn, sắp xếp giảm dần.",
        BaoCaoKhachHangMocQuanTam.GiaTriMuaHang =>
            "Giá trị hàng đã mua sau giảm giá, sắp xếp giảm dần.",
        _ => "Tổng tiền hàng trong khoảng thời gian đang chọn, sắp xếp giảm dần."
    };

    private void ThongBaoCoGiaTri()
    {
        OnPropertyChanged(nameof(TongHop));
        OnPropertyChanged(nameof(CoTop));
        OnPropertyChanged(nameof(CoBang));
        OnPropertyChanged(nameof(TenBieuDo));
        OnPropertyChanged(nameof(TenBieuDoPhu));
        CapNhatPhanTrang();
    }

    private static void DoDanhSachMoi<T>(ObservableCollection<T> dich, System.Collections.Generic.IEnumerable<T> nguon)
    {
        dich.Clear();
        foreach (var x in nguon)
            dich.Add(x);
    }

    // ============================================================
    // BẢNG CHI TIẾT: SẮP XẾP / PHÂN TRANG
    // ============================================================

    [ObservableProperty] private string sapXepKho = "TongTienHang";
    [ObservableProperty] private bool sapXepTang;
    [ObservableProperty] private int trang = 1;
    [ObservableProperty] private int soDongTrang = 15;

    public ObservableCollection<int> SoDongTrangOptions { get; } = new() { 10, 15, 25, 50 };

    public int TongSoDong => TongHop.TongKhachHang;

    public int TongSoTrang =>
        SoDongTrang <= 0 ? 0 : (int)Math.Ceiling(TongSoDong / (double)SoDongTrang);

    /// <summary>Ví dụ: "1 - 15 trong 367 khách hàng".</summary>
    public string VanBanPhanTrang
    {
        get
        {
            if (TongSoDong == 0)
                return "0 khách hàng";

            var tu = (Trang - 1) * SoDongTrang + 1;
            var den = Math.Min(tu + SoDongTrang - 1, TongSoDong);
            return $"{tu} - {den} trong {TongSoDong} khách hàng";
        }
    }

    public bool CanTrangTruoc => Trang > 1;
    public bool CanTrangSau => Trang < TongSoTrang;

    /// <summary>Trạng thái và tooltip sắp xếp của từng tiêu đề cột.</summary>
    public string DanhDauMa => DanhDau("MaKhachHang");
    public string DanhDauTen => DanhDau("TenKhachHang");
    public string DanhDauSdt => DanhDau("SoDienThoai");
    public string DanhDauSoDon => DanhDau("SoDonHang");
    public string DanhDauTongTien => DanhDau("TongTienHang");
    public string DanhDauGiamGia => DanhDau("GiamGia");
    public string DanhDauDaThanhToan => DanhDau("DaThanhToan");
    public string DanhDauCongNo => DanhDau("CongNo");
    public string DanhDauGiaTriMua => DanhDau("GiaTriMuaHang");

    public bool DangSapXepMa => DangSapXep("MaKhachHang");
    public bool DangSapXepTen => DangSapXep("TenKhachHang");
    public bool DangSapXepSdt => DangSapXep("SoDienThoai");
    public bool DangSapXepSoDon => DangSapXep("SoDonHang");
    public bool DangSapXepTongTien => DangSapXep("TongTienHang");
    public bool DangSapXepGiamGia => DangSapXep("GiamGia");
    public bool DangSapXepDaThanhToan => DangSapXep("DaThanhToan");
    public bool DangSapXepCongNo => DangSapXep("CongNo");
    public bool DangSapXepGiaTriMua => DangSapXep("GiaTriMuaHang");

    public string TipSapXepMa => TaoTipSapXep("MaKhachHang", "mã khách hàng");
    public string TipSapXepTen => TaoTipSapXep("TenKhachHang", "tên khách hàng");
    public string TipSapXepSdt => TaoTipSapXep("SoDienThoai", "số điện thoại");
    public string TipSapXepSoDon => TaoTipSapXep("SoDonHang", "số đơn hàng");
    public string TipSapXepTongTien => TaoTipSapXep("TongTienHang", "tổng tiền hàng");
    public string TipSapXepGiamGia => TaoTipSapXep("GiamGia", "giảm giá");
    public string TipSapXepDaThanhToan => TaoTipSapXep("DaThanhToan", "đã thanh toán");
    public string TipSapXepCongNo => TaoTipSapXep("CongNo", "công nợ");
    public string TipSapXepGiaTriMua => TaoTipSapXep("GiaTriMuaHang", "giá trị mua hàng");

    private string DanhDau(string cot) =>
        !DangSapXep(cot) ? "↕" : SapXepTang ? "↑" : "↓";

    private bool DangSapXep(string cot) =>
        string.Equals(SapXepKho, cot, StringComparison.OrdinalIgnoreCase);

    private string TaoTipSapXep(string cot, string tenCot)
    {
        if (!DangSapXep(cot))
            return $"Nhấn để sắp xếp {tenCot} {(LaCotChu(cot) ? "A → Z" : "tăng dần")}.";

        return SapXepTang
            ? $"Đang sắp xếp {tenCot} tăng dần. Nhấn để chuyển giảm dần."
            : $"Đang sắp xếp {tenCot} giảm dần. Nhấn để trở về thứ tự mặc định.";
    }

    private static bool LaCotChu(string cot) =>
        cot is "MaKhachHang" or "TenKhachHang" or "SoDienThoai";

    private void ThongBaoDauCot()
    {
        OnPropertyChanged(nameof(DanhDauMa));
        OnPropertyChanged(nameof(DanhDauTen));
        OnPropertyChanged(nameof(DanhDauSdt));
        OnPropertyChanged(nameof(DanhDauSoDon));
        OnPropertyChanged(nameof(DanhDauTongTien));
        OnPropertyChanged(nameof(DanhDauGiamGia));
        OnPropertyChanged(nameof(DanhDauDaThanhToan));
        OnPropertyChanged(nameof(DanhDauCongNo));
        OnPropertyChanged(nameof(DanhDauGiaTriMua));
        OnPropertyChanged(nameof(DangSapXepMa));
        OnPropertyChanged(nameof(DangSapXepTen));
        OnPropertyChanged(nameof(DangSapXepSdt));
        OnPropertyChanged(nameof(DangSapXepSoDon));
        OnPropertyChanged(nameof(DangSapXepTongTien));
        OnPropertyChanged(nameof(DangSapXepGiamGia));
        OnPropertyChanged(nameof(DangSapXepDaThanhToan));
        OnPropertyChanged(nameof(DangSapXepCongNo));
        OnPropertyChanged(nameof(DangSapXepGiaTriMua));
        OnPropertyChanged(nameof(TipSapXepMa));
        OnPropertyChanged(nameof(TipSapXepTen));
        OnPropertyChanged(nameof(TipSapXepSdt));
        OnPropertyChanged(nameof(TipSapXepSoDon));
        OnPropertyChanged(nameof(TipSapXepTongTien));
        OnPropertyChanged(nameof(TipSapXepGiamGia));
        OnPropertyChanged(nameof(TipSapXepDaThanhToan));
        OnPropertyChanged(nameof(TipSapXepCongNo));
        OnPropertyChanged(nameof(TipSapXepGiaTriMua));
    }

    private void CapNhatPhanTrang()
    {
        OnPropertyChanged(nameof(TongSoDong));
        OnPropertyChanged(nameof(TongSoTrang));
        OnPropertyChanged(nameof(VanBanPhanTrang));
        OnPropertyChanged(nameof(CanTrangTruoc));
        OnPropertyChanged(nameof(CanTrangSau));
    }

    partial void OnSapXepKhoChanged(string value)
    {
        ThongBaoDauCot();
        DongBoLuaChonSapXepNhanh();
    }

    partial void OnSapXepTangChanged(bool value)
    {
        ThongBaoDauCot();
        DongBoLuaChonSapXepNhanh();
    }

    partial void OnSapXepNhanhChonChanged(string? value)
    {
        if (!_dangDongBoSapXepNhanh)
            _ = ApDungSapXepNhanhAsync(value);
    }

    partial void OnTrangChanged(int value)
    {
        if (_khoiTaoXong && !_dangDieuChinhTrang && !_dangDatBoLoc)
            _ = TaiDuLieuAsync();
    }

    partial void OnSoDongTrangChanged(int value)
    {
        if (!_khoiTaoXong || _dangDieuChinhTrang)
            return;

        if (Trang != 1)
            DatTrang(1);

        _ = TaiDuLieuAsync();
    }

    partial void OnTimKiemChanged(string value)
    {
        if (!_khoiTaoXong || _dangDatBoLoc)
            return;

        // Mỗi lần đổi từ khóa thì quay về trang đầu rồi query lại (debounce để
        // không query khi người dùng còn đang gõ).
        if (Trang != 1)
            DatTrang(1);
        YeuCauTaiCoTre();
    }

    partial void OnMocQuanTamChonChanged(string? value)
    {
        if (!_khoiTaoXong || _dangDatBoLoc)
            return;

        MocQuanTam = DoiMocQuanTam(value);

        // Đổi mối quan tâm thì biểu đồ và bảng cùng dùng tiêu chí mới.
        SapXepKho = string.Empty;
        SapXepTang = false;

        if (Trang != 1)
            DatTrang(1);

        _ = TaiDuLieuAsync();
    }

    partial void OnTuNgayChonChanged(DateTimeOffset? value) => YeuCauTaiNgayCoTre();
    partial void OnDenNgayChonChanged(DateTimeOffset? value) => YeuCauTaiNgayCoTre();

    /// <summary>Chỉ tự tải khi đang ở chế độ "Tùy chỉnh" và cả hai ngày đã chọn.</summary>
    private void YeuCauTaiNgayCoTre()
    {
        if (!_khoiTaoXong || _dangDatBoLoc)
            return;

        if (KhoangThoiGian != NhanTuyChinh || TuNgayChon is null || DenNgayChon is null)
            return;

        // Đồng bộ khoảng đang áp dụng theo ngày vừa chọn (đổi thứ tự nếu ngược)
        // rồi mới hẹn tải — trước đây chỉ cập nhật khi bấm "Áp dụng" nên lần tự
        // tải lại dùng khoảng ngày cũ.
        var tu = TuNgayChon.Value.Date;
        var den = DenNgayChon.Value.Date;
        if (den < tu)
            (tu, den) = (den, tu);

        _dangDatBoLoc = true;
        try
        {
            TuNgay = tu;
            DenNgay = den;
            TuNgayChon = new DateTimeOffset(tu);
            DenNgayChon = new DateTimeOffset(den);
        }
        finally
        {
            _dangDatBoLoc = false;
        }

        YeuCauTaiCoTre();
    }

    // ============================================================
    // TẢI DỮ LIỆU
    // ============================================================

    [RelayCommand]
    public async Task TaiLaiAsync() => await TaiDuLieuAsync();

    private async Task TaiDuLieuAsync()
    {
        // Bỏ lần tải đang hẹn giờ (gõ tìm kiếm / chọn ngày) — người dùng vừa
        // tự bấm tải với bộ lọc hiện tại nên bản hẹn giờ kia không cần nữa.
        _treCts?.Cancel();

        int phien = ++_phienBan;

        try
        {
            DangTai = true;
            ThongBaoLoi = string.Empty;

            if (DenNgay < TuNgay)
                DenNgay = TuNgay;

            var duLieu = await _service.TaiDuLieuAsync(
                TuNgay,
                DenNgay,
                LocTimKiem(),
                SapXepKho,
                SapXepTang,
                KhoaCuaMocQuanTam(MocQuanTam),
                Trang,
                SoDongTrang,
                default);

            // Có lần tải mới hơn đang chạy → bỏ qua kết quả cũ này.
            if (phien != _phienBan)
                return;

            DuLieu = duLieu;

            // Trang vượt quá dữ liệu (bớt bộ lọc/search) → lùi về trang cuối.
            int tongTrang = TongSoTrang;
            if (tongTrang > 0 && Trang > tongTrang)
            {
                DatTrang(tongTrang);
                await TaiDuLieuAsync();
                return;
            }

            DoDanhSachMoi(TopItems, DuLieu.Top);
            DoDanhSachMoi(DongTrangItems, DuLieu.DongTrang);
            ThongBaoCoGiaTri();
        }
        catch (Exception ex)
        {
            if (phien == _phienBan)
                ThongBaoLoi = "Không thể tải báo cáo khách hàng: " + ex.Message;
        }
        finally
        {
            if (phien == _phienBan)
                DangTai = false;
        }
    }

    /// <summary>Query lại sau 400ms — dùng cho gõ tìm kiếm và chọn ngày.</summary>
    private void YeuCauTaiCoTre()
    {
        if (!_khoiTaoXong || _dangDatBoLoc)
            return;

        _treCts?.Cancel();
        _treCts?.Dispose();
        _treCts = new CancellationTokenSource();
        _ = TaiTreAsync(_treCts.Token);
    }

    private async Task TaiTreAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(400, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
            return;

        await TaiDuLieuAsync();
    }

    /// <summary>Gán trang mới mà không kích hoạt tải (tải do nơi gọi tự thực hiện).</summary>
    private void DatTrang(int trang)
    {
        if (Trang == trang)
            return;

        _dangDieuChinhTrang = true;
        try
        {
            Trang = trang;
        }
        finally
        {
            _dangDieuChinhTrang = false;
        }
    }

    /// <summary>Giá trị ô tìm kiếm → tham số SQL (null = không lọc theo khách hàng).</summary>
    private string? LocTimKiem()
    {
        var v = TimKiem?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    // ============================================================
    // LỆNH BỘ LỌC
    // ============================================================

    [RelayCommand]
    private async Task ChonKhoangThoiGianAsync(string? nhan)
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
            case NhanTuyChinh:
                // Chỉ hiện vùng chọn ngày, dữ liệu tải khi cả hai ngày đã chọn
                // hoặc khi bấm "Áp dụng".
                KhoangThoiGian = NhanTuyChinh;
                return;
            default:
                return;
        }

        if (Trang != 1)
            DatTrang(1);

        await TaiDuLieuAsync();
    }

    /// <summary>Áp dụng toàn bộ bộ lọc (ngày tùy chỉnh + tìm kiếm khách hàng).</summary>
    [RelayCommand]
    private async Task ApDungAsync()
    {
        if (KhoangThoiGian == NhanTuyChinh && TuNgayChon is not null && DenNgayChon is not null)
        {
            var tu = TuNgayChon.Value.Date;
            var den = DenNgayChon.Value.Date;
            if (den < tu)
                (tu, den) = (den, tu);

            _dangDatBoLoc = true;
            try
            {
                TuNgay = tu;
                DenNgay = den;
                TuNgayChon = new DateTimeOffset(tu);
                DenNgayChon = new DateTimeOffset(den);
            }
            finally
            {
                _dangDatBoLoc = false;
            }
        }

        if (Trang != 1)
            DatTrang(1);

        await TaiDuLieuAsync();
    }

    /// <summary>Đặt lại toàn bộ bộ lọc, đưa màn về trạng thái mặc định (tháng này).</summary>
    [RelayCommand]
    private async Task DatLaiAsync()
    {
        _dangDatBoLoc = true;
        try
        {
            TimKiem = string.Empty;
            MocQuanTamChon = MocQuanTamOptions[0];
            MocQuanTam = BaoCaoKhachHangMocQuanTam.BanHang;
            SapXepKho = string.Empty;
            SapXepTang = false;
            DatKhoangThangNay();
        }
        finally
        {
            _dangDatBoLoc = false;
        }

        if (Trang != 1)
            DatTrang(1);

        await TaiDuLieuAsync();
    }

    private void DatKhoangTuanNay()
    {
        var homNay = DateTime.Today;
        var soNgayDaQua = ((int)homNay.DayOfWeek + 6) % 7; // tuần bắt đầu từ thứ Hai
        DatKhoang(homNay.AddDays(-soNgayDaQua), homNay, "Tuần này");
    }

    private void DatKhoangThangNay()
    {
        var homNay = DateTime.Today;
        DatKhoang(new DateTime(homNay.Year, homNay.Month, 1), homNay, "Tháng này");
    }

    private void DatKhoangQuyNay()
    {
        var homNay = DateTime.Today;
        int dauQuy = ((homNay.Month - 1) / 3) * 3 + 1;
        DatKhoang(new DateTime(homNay.Year, dauQuy, 1), homNay, "Quý này");
    }

    private void DatKhoangNamNay()
    {
        var homNay = DateTime.Today;
        DatKhoang(new DateTime(homNay.Year, 1, 1), homNay, "Năm nay");
    }

    private void DatKhoang(DateTime tu, DateTime den, string nhan)
    {
        _dangDatBoLoc = true;
        try
        {
            TuNgay = tu;
            DenNgay = den;
            TuNgayChon = new DateTimeOffset(tu);
            DenNgayChon = new DateTimeOffset(den);
            KhoangThoiGian = nhan;
        }
        finally
        {
            _dangDatBoLoc = false;
        }
    }

    private static BaoCaoKhachHangMocQuanTam DoiMocQuanTam(string? ten) => ten switch
    {
        "Công nợ" => BaoCaoKhachHangMocQuanTam.CongNo,
        "Số lượng đơn hàng" => BaoCaoKhachHangMocQuanTam.SoDonHang,
        "Giá trị mua hàng" => BaoCaoKhachHangMocQuanTam.GiaTriMuaHang,
        _ => BaoCaoKhachHangMocQuanTam.BanHang
    };

    /// <summary>Khoá sắp xếp/metric của mối quan tâm → khoá whitelist trong service.</summary>
    private static string KhoaCuaMocQuanTam(BaoCaoKhachHangMocQuanTam moc) => moc switch
    {
        BaoCaoKhachHangMocQuanTam.CongNo => "CongNo",
        BaoCaoKhachHangMocQuanTam.SoDonHang => "SoDonHang",
        BaoCaoKhachHangMocQuanTam.GiaTriMuaHang => "GiaTriMuaHang",
        _ => "TongTienHang"
    };

    private void DongBoLuaChonSapXepNhanh()
    {
        _dangDongBoSapXepNhanh = true;
        try
        {
            SapXepNhanhChon = (SapXepKho, SapXepTang) switch
            {
                ("TenKhachHang", true) => SapXepNhanhOptions[0],
                ("TenKhachHang", false) => SapXepNhanhOptions[1],
                ("SoDonHang", true) => SapXepNhanhOptions[2],
                ("SoDonHang", false) => SapXepNhanhOptions[3],
                ("TongTienHang", false) => SapXepNhanhOptions[4],
                ("TongTienHang", true) => SapXepNhanhOptions[5],
                ("CongNo", false) => SapXepNhanhOptions[6],
                ("CongNo", true) => SapXepNhanhOptions[7],
                ("DaThanhToan", false) => SapXepNhanhOptions[8],
                ("DaThanhToan", true) => SapXepNhanhOptions[9],
                _ => null
            };
        }
        finally
        {
            _dangDongBoSapXepNhanh = false;
        }
    }

    private async Task ApDungSapXepNhanhAsync(string? luaChon)
    {
        string? cot;
        bool tang;
        switch (luaChon)
        {
            case "Tên khách hàng A → Z": (cot, tang) = ("TenKhachHang", true); break;
            case "Tên khách hàng Z → A": (cot, tang) = ("TenKhachHang", false); break;
            case "Số đơn hàng tăng dần": (cot, tang) = ("SoDonHang", true); break;
            case "Số đơn hàng giảm dần": (cot, tang) = ("SoDonHang", false); break;
            case "Tổng tiền hàng cao nhất": (cot, tang) = ("TongTienHang", false); break;
            case "Tổng tiền hàng thấp nhất": (cot, tang) = ("TongTienHang", true); break;
            case "Công nợ cao nhất": (cot, tang) = ("CongNo", false); break;
            case "Công nợ thấp nhất": (cot, tang) = ("CongNo", true); break;
            case "Đã thanh toán cao nhất": (cot, tang) = ("DaThanhToan", false); break;
            case "Đã thanh toán thấp nhất": (cot, tang) = ("DaThanhToan", true); break;
            default: return;
        }

        SapXepKho = cot;
        SapXepTang = tang;
        if (Trang != 1)
            DatTrang(1);

        await TaiDuLieuAsync();
    }

    // ============================================================
    // LỆNH KHÁC
    // ============================================================

    [RelayCommand]
    private void ChonBieuDo() => KieuHienThi = BaoCaoKhachHangKieuHienThi.BieuDo;

    [RelayCommand]
    private void ChonBaoCao() => KieuHienThi = BaoCaoKhachHangKieuHienThi.BaoCao;

    /// <summary>
    /// Xuất Excel toàn bộ khách hàng khớp bộ lọc hiện tại (không phụ thuộc trang
    /// đang xem): truy vấn lại với cỡ trang bằng tổng số khách hàng.
    /// </summary>
    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        try
        {
            DangTai = true;
            ThongBaoLoi = string.Empty;

            var soDong = Math.Max(1, TongHop.TongKhachHang);

            var duLieu = await _service.TaiDuLieuAsync(
                TuNgay,
                DenNgay,
                LocTimKiem(),
                SapXepKho,
                SapXepTang,
                KhoaCuaMocQuanTam(MocQuanTam),
                1,
                soDong,
                default);

            var tenBaoCao = "Báo cáo khách hàng";
            var khoangThoiGian = $"{TuNgay:dd/MM/yyyy} - {DenNgay:dd/MM/yyyy}";
            var tenFile = ReportExportService.TaoTenFile("BaoCaoKhachHang");

            await ReportExportService.ExportReportAsync(
                ownerWindow: ReportExportService.LayCuaSoChinh(),
                reportTitle: tenBaoCao,
                suggestedFileName: tenFile,
                generateWorkbook: () => BaoCaoKhachHangExcel.TaoWorkbook(duLieu),
                recordCount: duLieu.DongTrang.Count,
                statusCallback: msg => ThongBaoLoi = msg,
                reportPeriod: khoangThoiGian);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi xuat Excel bao cao khach hang", ex);
            ThongBaoLoi = "Không xuất được file Excel: " + ex.Message;
        }
        finally
        {
            DangTai = false;
        }
    }

    [RelayCommand]
    private async Task SapXepAsync(string? cot)
    {
        if (string.IsNullOrWhiteSpace(cot))
            return;

        if (string.Equals(SapXepKho, cot, StringComparison.OrdinalIgnoreCase))
        {
            if (SapXepTang)
                SapXepTang = false;
            else
                SapXepKho = string.Empty;
        }
        else
        {
            SapXepKho = cot;
            SapXepTang = LaCotChu(cot);
        }

        if (Trang != 1)
            DatTrang(1);

        await TaiDuLieuAsync();
    }

    [RelayCommand] private void TrangDau() { Trang = 1; }
    [RelayCommand] private void TrangTruoc() { if (Trang > 1) Trang--; }
    [RelayCommand] private void TrangSau() { if (Trang < TongSoTrang) Trang++; }
    [RelayCommand] private void TrangCuoi() { var t = Math.Max(1, TongSoTrang); if (Trang != t) Trang = t; }
}
