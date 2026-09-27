namespace ShopHoa.Models;

public sealed class HoaDonBanHangReceipt
{
    public string MaHoaDon { get; set; } = "";
    public DateTime ThoiGianBan { get; set; }
    public string TenKhachHang { get; set; } = "Khách lẻ";
    public string TenNhanVien { get; set; } = "";
    public List<HoaDonBanHangReceiptItem> Items { get; set; } = [];
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal KhachCanTra { get; set; }
    public decimal KhachDaThanhToan { get; set; }
    public decimal TienKhachDua { get; set; }

    public decimal TienThoi { get; set; }
    public decimal ConNo { get; set; }
    public string TrangThaiThanhToan { get; set; } = "";
}

public sealed class HoaDonBanHangReceiptItem
{
    public string TenSanPham { get; set; } = "";
    public decimal SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }
}
