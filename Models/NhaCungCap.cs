namespace ShopHoa.Models;

public sealed class NhaCungCap
{
    public uint Id { get; set; }

    public string MaNhaCungCap { get; set; } = "";

    public string TenNhaCungCap { get; set; } = "";

    public string? SoDienThoai { get; set; }

    public string? Email { get; set; }

    public string? DiaChi { get; set; }

    public string? KhuVuc { get; set; }

    public string? PhuongXa { get; set; }

    public string? MaSoThue { get; set; }

    public string? CanCuocCongDan { get; set; }

    public string? NhomNhaCungCap { get; set; }

    public string? CongTy { get; set; }

    public string? GhiChu { get; set; }

    public string? TrangThai { get; set; } = "Đang hoạt động";

    public decimal TongMua { get; set; }

    public decimal ConNo { get; set; }

    public string? NguoiTao { get; set; }

    public DateTime? NgayTao { get; set; }
}