using System;

namespace ShopHoa.Models;

/// <summary>
/// Kết quả sau khi thực hiện hủy phiếu thanh toán công nợ khách hàng (Issue #104).
/// </summary>
public sealed class HuyThanhToanCongNoKetQua
{
    public bool ThanhCong { get; set; } = true;
    public string ThongBao { get; set; } = "";
    public string MaGiaoDich { get; set; } = "";
    public uint KhachHangId { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public decimal SoTien { get; set; }
    public decimal TienHoanTacCongNo { get; set; }
    public decimal TienHoanTacTraTruoc { get; set; }
    public decimal CongNoTruocKhiHuy { get; set; }
    public decimal CongNoSauKhiHuy { get; set; }
    public decimal SoDuTraTruocTruocKhiHuy { get; set; }
    public decimal SoDuTraTruocSauKhiHuy { get; set; }
    public int SoHoaDonHoanTac { get; set; }
    public DateTime ThoiGianHuy { get; set; }
    public string? LyDoHuy { get; set; }
    public string? NguoiHuy { get; set; }
    public bool PhieuChiDaHuy { get; set; } = true;

    // Các thuộc tính tiện ích (aliases)
    public decimal SoTienHoanTac => TienHoanTacCongNo;
    public decimal CongNoMoi => CongNoSauKhiHuy;
    public decimal SoTienTraTruocTru => TienHoanTacTraTruoc;
    public int SoLuongHoaDonHoanTac => SoHoaDonHoanTac;
}

/// <summary>
/// Kết quả sau khi thực hiện hủy phiếu thanh toán công nợ nhà cung cấp (Issue #111).
/// </summary>
public sealed class HuyThanhToanCongNoNhaCungCapKetQua
{
    public bool ThanhCong { get; set; } = true;
    public string ThongBao { get; set; } = "";
    public string MaGiaoDich { get; set; } = "";
    public uint NhaCungCapId { get; set; }
    public string MaNhaCungCap { get; set; } = "";
    public string TenNhaCungCap { get; set; } = "";
    public decimal SoTien { get; set; }
    public decimal TienHoanTacCongNo { get; set; }
    public decimal TienHoanTacTraTruoc { get; set; }
    public decimal CongNoTruocKhiHuy { get; set; }
    public decimal CongNoSauKhiHuy { get; set; }
    public decimal SoDuTraTruocTruocKhiHuy { get; set; }
    public decimal SoDuTraTruocSauKhiHuy { get; set; }
    public int SoPhieuNhapHoanTac { get; set; }
    public DateTime ThoiGianHuy { get; set; }
    public string? LyDoHuy { get; set; }
    public string? NguoiHuy { get; set; }
    public string PhuongThuc { get; set; } = "";
    public bool PhieuChiDaHuy { get; set; } = true;

    // Các thuộc tính tiện ích (aliases)
    public decimal SoTienHoanTac => TienHoanTacCongNo;
    public decimal CongNoMoi => CongNoSauKhiHuy;
    public decimal SoTienTraTruocTru => TienHoanTacTraTruoc;
    public int SoLuongPhieuNhapHoanTac => SoPhieuNhapHoanTac;
}
