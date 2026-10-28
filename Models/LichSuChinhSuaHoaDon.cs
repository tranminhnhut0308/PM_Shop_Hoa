namespace ShopHoa.Models;

public sealed class LichSuChinhSuaHoaDon
{
    public ulong Id { get; set; }
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = "";
    public DateTime ThoiGian { get; set; }
    public uint? NguoiThucHienId { get; set; }
    public string TenNguoiThucHien { get; set; } = "";
    public string LyDo { get; set; } = "";
    
    public decimal TongTienTruoc { get; set; }
    public decimal TongTienSau { get; set; }
    public decimal GiamGiaTruoc { get; set; }
    public decimal GiamGiaSau { get; set; }
    public decimal KhachCanTraTruoc { get; set; }
    public decimal KhachCanTraSau { get; set; }
    public decimal KhachDaTra { get; set; }
    public decimal ConNoTruoc { get; set; }
    public decimal ConNoSau { get; set; }

    public uint? KhachHangIdTruoc { get; set; }
    public uint? KhachHangIdSau { get; set; }
    public string? TenKhachHangTruoc { get; set; }
    public string? TenKhachHangSau { get; set; }
    public DateTime? ThoiGianBanTruoc { get; set; }
    public DateTime? ThoiGianBanSau { get; set; }

    public string? DuLieuTruocJson { get; set; }
    public string? DuLieuSauJson { get; set; }
    public string? ChiTietThayDoi { get; set; }

    // Helper properties for UI
    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM/yyyy HH:mm:ss");
    public decimal ChenhLechTongTien => TongTienSau - TongTienTruoc;
    public decimal ChenhLechCanTra => KhachCanTraSau - KhachCanTraTruoc;
    public decimal ChenhLechConNo => ConNoSau - ConNoTruoc;
    public string ChenhLechConNoHienThi => ChenhLechConNo switch
    {
        > 0 => $"+{ChenhLechConNo:N0} đ (tăng nợ)",
        < 0 => $"{ChenhLechConNo:N0} đ (giảm nợ)",
        _ => "0 đ"
    };
}

public sealed class HoaDonSnapshot
{
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = "";
    public uint? KhachHangId { get; set; }
    public string? TenKhachHang { get; set; }
    public DateTime ThoiGianBan { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal KhachCanTra { get; set; }
    public decimal KhachDaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public string? GhiChu { get; set; }
    public List<HoaDonItemSnapshot> Items { get; set; } = [];
}

public sealed class HoaDonItemSnapshot
{
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string? DonViTinh { get; set; }
    public decimal SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThanhTien { get; set; }
    public decimal GiaVon { get; set; }
    public string? GhiChu { get; set; }
}
