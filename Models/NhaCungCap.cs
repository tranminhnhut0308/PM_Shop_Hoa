namespace ShopHoa.Models;

public sealed class NhaCungCap
{
    public uint Id { get; set; }
    public string MaNhaCungCap { get; set; } = "";
    public string TenNhaCungCap { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public decimal ConNo { get; set; }
}
