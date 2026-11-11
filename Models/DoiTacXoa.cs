namespace ShopHoa.Models;

/// <summary>
/// Kết quả sau khi thực hiện thao tác xóa hoặc ngừng hoạt động đối tác (khách hàng / nhà cung cấp).
/// </summary>
public enum KetQuaXoaDoiTac
{
    /// <summary>Chưa phát sinh giao dịch nào và không có nợ -> Đã xóa vĩnh viễn khỏi database.</summary>
    DaXoaVinhVien,

    /// <summary>Đã có giao dịch liên quan hoặc vướng khóa ngoại -> Đã chuyển sang ngừng hoạt động để bảo toàn dữ liệu.</summary>
    DaChuyenNgungHoatDong,

    /// <summary>Còn công nợ hoặc còn số dư trả trước và người dùng chưa đồng ý chuyển ngừng hoạt động.</summary>
    ConCongNo,

    /// <summary>Thao tác bị hủy bởi người dùng.</summary>
    DaHuy
}

/// <summary>
/// Thông tin kiểm tra ràng buộc trước khi xóa khách hàng hoặc nhà cung cấp.
/// </summary>
public sealed class ThongTinKiemTraXoaDoiTac
{
    public bool CoDuLieuGiaoDich { get; set; }
    public decimal CongNoHienTai { get; set; }
    public decimal SoDuTraTruoc { get; set; }
    public int SoHoaDonHoacPhieuNhap { get; set; }
    public int SoGiaoDichLienQuan { get; set; }
    public bool DaNgungHoatDong { get; set; }
}
