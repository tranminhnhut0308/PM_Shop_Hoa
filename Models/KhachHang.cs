namespace ShopHoa.Models;

public sealed class KhachHang
{
    public uint Id { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string LoaiKhachHang { get; set; } = "CA_NHAN";
    public string? SoDienThoai { get; set; }
    public DateTime? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? Email { get; set; }
    public string? Facebook { get; set; }
    public string? DiaChi { get; set; }
    public string? PhuongXa { get; set; }
    public string? QuanHuyen { get; set; }
    public string? TinhThanh { get; set; }
    public string? NhomKhachHang { get; set; }
    public string? MaSoThue { get; set; }
    public string? TenNguoiMuaHoaDon { get; set; }
    public string? TenCongTy { get; set; }
    public string? DiaChiXuatHoaDon { get; set; }
    public string? GhiChu { get; set; }
    public bool TrangThai { get; set; } = true;
}
