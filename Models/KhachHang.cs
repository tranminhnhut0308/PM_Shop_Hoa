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
    /// <summary>Tên nhóm khách hàng, lấy từ bảng nhom_khach_hang khi đọc danh sách.</summary>
    public string? NhomKhachHang { get; set; }

    /// <summary>Khóa nhóm khách hàng, dùng khi thêm/sửa để lưu đúng nhóm đã chọn.</summary>
    public uint? NhomKhachHangId { get; set; }

    /// <summary>
    /// Nợ hiện tại của khách hàng, tính từ sổ cái công nợ
    /// (SUM(ghi_no - ghi_co)), nếu chưa có sổ cái thì lấy SUM(con_no) của hóa đơn.
    /// Chỉ dùng khi hiển thị danh sách, không ghi xuống database.
    /// </summary>
    public decimal NoHienTai { get; set; }

    /// <summary>Tổng tiền bán của khách hàng (SUM(khach_can_tra) hóa đơn đã hoàn thành).</summary>
    public decimal TongBan { get; set; }

    /// <summary>Tổng tiền bán trừ các phiếu trả hàng đã hoàn thành.</summary>
    public decimal TongBanTruTraHang { get; set; }

    public string? MaSoThue { get; set; }
    public string? TenNguoiMuaHoaDon { get; set; }
    public string? TenCongTy { get; set; }
    public string? DiaChiXuatHoaDon { get; set; }
    public string? GhiChu { get; set; }
    public bool TrangThai { get; set; } = true;
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }
}
