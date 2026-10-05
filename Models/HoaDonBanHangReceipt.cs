namespace ShopHoa.Models;

public sealed class HoaDonBanHangReceipt
{
    /// <summary>Khoá chính của hóa đơn trong database (0 nghĩa là chưa lưu).</summary>
    public uint HoaDonId { get; set; }

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

    /// <summary>Tiền thối đã lưu xuống database (cột thu_khac).</summary>
    public decimal ThuKhac { get; set; }

    /// <summary>
    /// Các cảnh báo phát sinh khi lưu đơn (bán vượt tồn kho, sản phẩm chưa có
    /// số tồn...). Hóa đơn vẫn được lưu nhưng người dùng phải được báo rõ.
    /// </summary>
    public List<string> CanhBao { get; set; } = [];

    public bool CoCanhBao => CanhBao.Count > 0;
}

public sealed class HoaDonBanHangReceiptItem
{
    public string TenSanPham { get; set; } = "";
    public decimal SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }
}
