namespace ShopHoa.Models;

public sealed class NguoiDung
{
    public uint Id { get; set; }
    public string MaNguoiDung { get; set; } = "";
    public string TenDangNhap { get; set; } = "";
    public string MatKhau { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public uint? ChiNhanhId { get; set; }
    public string? TenChiNhanh { get; set; }
    public string? VaiTro { get; set; }
}
