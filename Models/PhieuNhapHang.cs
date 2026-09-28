using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Phiếu nhập hàng (đầu vào từ nhà cung cấp).
/// Ánh xạ trực tiếp với bảng phieu_nhap_hang.
/// </summary>
public sealed partial class PhieuNhapHang : ObservableObject
{
    public uint Id { get; set; }

    public string MaPhieuNhap { get; set; } = "";

    public uint ChiNhanhId { get; set; }

    public uint? NhaCungCapId { get; set; }

    public string? TenNhaCungCap { get; set; }

    /// <summary>Mã nhà cung cấp, chỉ dùng cho báo cáo (join từ bảng nha_cung_cap).</summary>
    public string? MaNhaCungCap { get; set; }

    public uint? NguoiNhapId { get; set; }

    public string? TenNhanVien { get; set; }

    public DateTime ThoiGianNhap { get; set; } = DateTime.Now;

    public decimal TongTienHang { get; set; }

    public decimal ChiPhiKhac { get; set; }

    public decimal TongTienNhap { get; set; }

    public decimal DaThanhToan { get; set; }

    public decimal ConNo { get; set; }

    /// <summary>
    /// DA_NHAP_HANG = đã cập nhật tồn kho,
    /// LUU_TAM = phiếu nháp, chưa động vào tồn kho.
    /// </summary>
    public string TrangThai { get; set; } = "DA_NHAP_HANG";

    public string? GhiChu { get; set; }

    public string? SoHoaDonDauVao { get; set; }

    public string? MaDatHangNhap { get; set; }

    public string TrangThaiHienThi => TrangThai == "LUU_TAM"
        ? "Lưu tạm"
        : "Đã nhập hàng";

    // ============================================================
    // THÔNG TIN CHỈ DÙNG CHO BÁO CÁO
    // ============================================================

    /// <summary>Định dạng dd/MM/yyyy HH:mm cho cột "Thời gian" trong báo cáo.</summary>
    public string ThoiGianNhapText => ThoiGianNhap.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Checkbox chọn dòng trong báo cáo (chỉ giữ trong bộ nhớ, không lưu database).</summary>
    [ObservableProperty]
    private bool isSelected;

    /// <summary>Đánh dấu yêu thích (chỉ giữ trong bộ nhớ, không lưu database).</summary>
    [ObservableProperty]
    private bool isYeuThich;

    private static readonly IBrush NenDaNhapHang = new SolidColorBrush(Color.Parse("#E8F6F1"));
    private static readonly IBrush ChuDaNhapHang = new SolidColorBrush(Color.Parse("#087F6E"));
    private static readonly IBrush NenLuuTam = new SolidColorBrush(Color.Parse("#FFF7E8"));
    private static readonly IBrush ChuLuuTam = new SolidColorBrush(Color.Parse("#B06A00"));

    /// <summary>Màu nền badge trạng thái: xanh nhạt khi đã nhập hàng, vàng nhạt khi lưu tạm.</summary>
    public IBrush TrangThaiBadgeBrush => TrangThai == "LUU_TAM"
        ? NenLuuTam
        : NenDaNhapHang;

    /// <summary>Màu chữ badge trạng thái.</summary>
    public IBrush TrangThaiBadgeForeground => TrangThai == "LUU_TAM"
        ? ChuLuuTam
        : ChuDaNhapHang;
}
