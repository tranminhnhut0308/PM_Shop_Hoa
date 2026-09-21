namespace ShopHoa.Models;

public sealed class NhaCungCap
{
    public uint Id { get; set; }

    public string MaNhaCungCap { get; set; } = "";

    public string TenNhaCungCap { get; set; } = "";

    public string? SoDienThoai { get; set; }

    public string? Email { get; set; }

    public string? DiaChi { get; set; }

    public string? PhuongXa { get; set; }

    public string? QuanHuyen { get; set; }

    public string? TinhThanh { get; set; }

    public string? MaSoThue { get; set; }

    public string? NguoiLienHe { get; set; }

    public string? GhiChu { get; set; }

    public bool TrangThai { get; set; } = true;

    public decimal ConNo { get; set; }

    public DateTime? NgayTao { get; set; }

    public string TrangThaiHienThi => TrangThai ? "Đang hoạt động" : "Ngừng hoạt động";
}
