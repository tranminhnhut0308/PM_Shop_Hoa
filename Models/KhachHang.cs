namespace ShopHoa.Models;

public sealed class KhachHang
{
    public uint Id { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }
    public string? NhomKhachHang { get; set; }
}
