namespace ShopHoa.Models;

public sealed class PhieuNhapImportReceipt
{
    public string MaPhieuNhap { get; set; } = "";
    public string TenChiNhanh { get; set; } = "";
    public DateTime ThoiGianNhap { get; set; }
    public string MaNhaCungCap { get; set; } = "";
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ChiPhiNhapTraNcc { get; set; }
    public decimal ChiPhiNhapKhac { get; set; }
    public decimal CanTraNcc { get; set; }
    public decimal DaTraNcc { get; set; }
    public decimal TongSoLuong { get; set; }
    public decimal TongSoMatHang { get; set; }
    public string? GhiChu { get; set; }
    public string? SoHoaDonDauVao { get; set; }
    public List<PhieuNhapImportLine> Items { get; set; } = [];
}

public sealed class PhieuNhapImportLine
{
    public int SoDongExcel { get; set; }
    public string MaSanPham { get; set; } = "";
    public decimal SoLuong { get; set; }
    public decimal DonGiaNhap { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThanhTien { get; set; }
    public string? GhiChu { get; set; }
}

public sealed record PhieuNhapImportIssue(string MaPhieuNhap, string LyDo);

public sealed class PhieuNhapImportPreview
{
    public string TenFile { get; set; } = "";
    public int TongSoDongChiTiet { get; set; }
    public int SoDongChiTietCuaPhieuLoi { get; set; }
    public List<PhieuNhapImportReceipt> PhieuNhap { get; set; } = [];
    public List<PhieuNhapImportIssue> Loi { get; set; } = [];
}
