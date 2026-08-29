namespace ShopHoa.Models;

public sealed class HoaDon
{
    public uint Id { get; set; }
    public string MaHoaDon { get; set; } = "";
    public DateTime ThoiGianBan { get; set; }
    public string? TenKhachHang { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThuKhac { get; set; }
    public decimal KhachCanTra { get; set; }
    public decimal KhachDaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public string TrangThaiThanhToan { get; set; } = "";
}
