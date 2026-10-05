using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Khoảng thời gian dùng để lọc số liệu Dashboard. Giá trị <c>Key</c> được lưu
/// vào ViewModel và truyền xuống SQL nên phải khớp với tên cột trong
/// <see cref="TongQuanKhoangThoiGian"/>.
/// </summary>
public static class TongQuanKhoangThoiGian
{
    public const string HomNay = "HOM_NAY";
    public const string BayNgay = "7_NGAY";
    public const string ThangNay = "THANG_NAY";
    public const string ThangTruoc = "THANG_TRUOC";
    public const string NamNay = "NAM_NAY";
    public const string TuDien = "TU_DIEN";

    public static readonly IReadOnlyList<string> TatCa =
    [
        HomNay,
        BayNgay,
        ThangNay,
        ThangTruoc,
        NamNay,
        TuDien
    ];

    /// <summary>Chuỗi hiển thị trên nút bộ lọc.</summary>
    public static string TenHienThi(string key) => key switch
    {
        HomNay => "Hôm nay",
        BayNgay => "7 ngày",
        ThangNay => "Tháng này",
        ThangTruoc => "Tháng trước",
        NamNay => "Năm nay",
        TuDien => "Tùy chọn",
        _ => "Hôm nay"
    };
}

/// <summary>Cách gom dữ liệu doanh thu trên biểu đồ.</summary>
public static class TongQuanChieuDoanhThu
{
    public const string TheoNgay = "NGAY";
    public const string TheoGio = "GIO";
    public const string TheoThu = "THU";

    public static readonly IReadOnlyList<string> TatCa = [TheoNgay, TheoGio, TheoThu];

    public static string TenHienThi(string key) => key switch
    {
        TheoGio => "Theo giờ",
        TheoThu => "Theo thứ",
        _ => "Theo ngày"
    };
}

/// <summary>Kiểu xếp hạng của các thẻ "Top 10".</summary>
public static class TongQuanXepHang
{
    public const string TheoSoLuong = "SO_LUONG";
    public const string TheoDoanhThu = "DOANH_THU";

    public static readonly IReadOnlyList<string> TatCa = [TheoSoLuong, TheoDoanhThu];

    public static string TenHienThi(string key) =>
        key == TheoDoanhThu ? "Theo doanh thu" : "Theo số lượng";
}

/// <summary>Một điểm dữ liệu trên biểu đồ doanh thu.</summary>
public sealed class TongQuanDiemDoanhThu
{
    /// <summary>Nhãn hiển thị trên trục X (ví dụ "01/10", "14:00", "Thứ 2").</summary>
    public string Nhan { get; set; } = "";

    /// <summary>Nhãn đầy đủ dùng cho tooltip (ví dụ "Thứ hai 01/10/2026").</summary>
    public string NhanDayDu { get; set; } = "";

    public decimal DoanhThu { get; set; }

    public int SoHoaDon { get; set; }

    public string DoanhThuHienThi => $"{DoanhThu:N0} đ";

    public string SoHoaDonHienThi => $"{SoHoaDon:N0} hóa đơn";

    /// <summary>
    /// Tỉ lệ chiều cao cột so với giá trị lớn nhất. Database đã chia tỉ lệ sẵn
    /// để tránh phải load cả danh sách ra RAM rồi tính lại ở client.
    /// </summary>
    public double TyLeChieuCao { get; set; }

    /// <summary>Bề rộng cột (0..100) dùng chung cho cả chế độ giờ và ngày.</summary>
    public double TyLeBeRong { get; set; }

    /// <summary>Chiều cao tối đa của vùng vẽ cột (px) — XAML lấy hằng này làm mốc.</summary>
    public const double ChieuCaoToiDa = 180;

    /// <summary>Bề rộng tối đa của một cột (px) để các cột không giãn quá rộng.</summary>
    public const double BeRongToiDa = 90;

    /// <summary>Chiều cao cột tính bằng pixel để XAML bind thẳng, không cần converter.</summary>
    public double ChieuCaoCot => DoanhThu <= 0 ? 0 : Math.Max(4, TyLeChieuCao / 100d * ChieuCaoToiDa);

    /// <summary>Bề rộng cột (px); nhiều điểm thì cuộn ngang trong vùng biểu đồ.</summary>
    public double BeRongCot => Math.Max(10, Math.Min(BeRongToiDa, TyLeBeRong / 100d * BeRongToiDa));

    public bool CoDuLieu => SoHoaDon > 0 || DoanhThu > 0;
}

/// <summary>Một dòng trong thẻ "Top 10" (hàng bán chạy hoặc khách hàng).</summary>
public sealed class TongQuanHangTop
{
    public int ThuTu { get; set; }

    public string Ten { get; set; } = "";

    public string TenPhu { get; set; } = "";

    public decimal SoLuong { get; set; }

    public decimal DoanhThu { get; set; }

    /// <summary>Tỉ lệ bề rộng thanh ngang (0..100) so với dòng lớn nhất.</summary>
    public double TyLePhanTram { get; set; }

    public double TyLeBeRong => Math.Clamp(TyLePhanTram, 0, 100);

    public string DoanhThuHienThi => $"{DoanhThu:N0} đ";

    public string SoLuongHienThi => $"{SoLuong:N0}";
}

/// <summary>Một dòng trong thẻ "Hoạt động gần đây".</summary>
public sealed class TongQuanHoatDong
{
    public DateTime ThoiGian { get; set; }

    /// <summary>Mã chứng từ (HD..., PN..., PT...).</summary>
    public string MaChungTu { get; set; } = "";

    public string TieuDe { get; set; } = "";

    public string MoTa { get; set; } = "";

    /// <summary>"BAN_HANG" | "NHAP_HANG" | "SO_QUY"</summary>
    public string Loai { get; set; } = "";

    /// <summary>Số tiền liên quan, có thể âm nếu là chi.</summary>
    public decimal GiaTri { get; set; }

    public string GiaTriHienThi => GiaTri == 0 ? "" : $"{Math.Abs(GiaTri):N0} đ";

    public bool LaChi => GiaTri < 0;

    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM HH:mm");

    public string ThoiGianDayDu => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    public string MauGiaTri => LaChi ? "#D64545" : "#087F6E";

    public string MauNenLoai => Loai switch
    {
        "BAN_HANG" => "#EAF9F5",
        "NHAP_HANG" => "#FFF4E5",
        _ => "#EEF2FF"
    };

    public string MauIcon => Loai switch
    {
        "BAN_HANG" => "#087F6E",
        "NHAP_HANG" => "#C27803",
        _ => "#4B5D57"
    };

    public string ChucNangMoRong => $"{TieuDe} {MaChungTu}";
}

/// <summary>Thống kê kho lấy trực tiếp từ database, không suy diễn ở client.</summary>
public sealed class TongQuanThongKeKho
{
    public int TongSoHangHoa { get; set; }

    public int HangSapHet { get; set; }

    public int HangHetHang { get; set; }

    public int TongSoKhachHang { get; set; }

    public int TongSoNhaCungCap { get; set; }

    public decimal GiaTriTonKho { get; set; }

    public string GiaTriTonKhoHienThi => $"{GiaTriTonKho:N0} đ";
}

/// <summary>Kết quả tải một lần của toàn bộ Dashboard.</summary>
public sealed class TongQuanDuLieu
{
    // --- 5 thẻ "Kết quả bán hàng hôm nay" ---
    public decimal DoanhThuHomNay { get; set; }

    public int SoHoaDonHomNay { get; set; }

    public decimal DaThuHomNay { get; set; }

    public decimal ConNoHomNay { get; set; }

    public int SoDonHangHomNay { get; set; }

    // --- Biểu đồ doanh thu theo bộ lọc đang chọn ---
    public decimal TongDoanhThuKyChon { get; set; }

    public int TongSoHoaDonKyChon { get; set; }

    public List<TongQuanDiemDoanhThu> BieuDoDoanhThu { get; set; } = [];

    // --- Hoạt động gần đây ---
    public List<TongQuanHoatDong> HoatDongGanDay { get; set; } = [];

    // --- Top 10 ---
    public List<TongQuanHangTop> TopHangBanChay { get; set; } = [];

    public List<TongQuanHangTop> TopKhachHang { get; set; } = [];

    // --- Thống kê kho ---
    public TongQuanThongKeKho ThongKeKho { get; set; } = new();

    // --- Chuỗi hiển thị đã định dạng sẵn để binding không cần StringFormat ---
    public string DoanhThuHomNayHienThi => $"{DoanhThuHomNay:N0} đ";

    public string SoHoaDonHomNayHienThi => $"{SoHoaDonHomNay:N0}";

    public string DaThuHomNayHienThi => $"{DaThuHomNay:N0} đ";

    public string ConNoHomNayHienThi => $"{ConNoHomNay:N0} đ";

    public string SoDonHangHomNayHienThi => $"{SoDonHangHomNay:N0}";

    public string TongDoanhThuKyChonHienThi => $"{TongDoanhThuKyChon:N0} đ";

    public string TongSoHoaDonKyChonHienThi => $"{TongSoHoaDonKyChon:N0} hóa đơn";

    public string ThoiGianHienThi { get; set; } = "";

    public static string NgayHienThi(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}