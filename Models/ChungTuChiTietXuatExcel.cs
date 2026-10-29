using System;

namespace ShopHoa.Models;

/// <summary>
/// Dòng chi tiết mặt hàng bán trong hóa đơn phục vụ xuất báo cáo Excel và đối chiếu.
/// </summary>
public sealed class HoaDonChiTietDong
{
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = "";
    public DateTime ThoiGianBan { get; set; }
    public string? MaKhachHang { get; set; }
    public string? TenKhachHang { get; set; }
    public string? NguoiBan { get; set; }

    public string? MaSanPham { get; set; }
    public string TenSanPham { get; set; } = "";
    public string? DonViTinh { get; set; }
    public decimal SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThanhTien { get; set; }
    public string? GhiChu { get; set; }
}

/// <summary>
/// Dòng chi tiết mặt hàng nhập trong phiếu nhập phục vụ xuất báo cáo Excel và đối chiếu.
/// </summary>
public sealed class PhieuNhapChiTietDong
{
    public uint PhieuNhapId { get; set; }
    public string MaPhieuNhap { get; set; } = "";
    public DateTime ThoiGianNhap { get; set; }
    public string? MaNhaCungCap { get; set; }
    public string? TenNhaCungCap { get; set; }
    public string? NguoiNhap { get; set; }

    public string? MaSanPham { get; set; }
    public string TenSanPham { get; set; } = "";
    public string? DonViTinh { get; set; }
    public decimal SoLuong { get; set; }
    public decimal DonGiaNhap { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThanhTien { get; set; }
    public string? GhiChu { get; set; }
}
