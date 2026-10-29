using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Phần mở rộng của DatabaseService phục vụ truy vấn chi tiết chứng từ (Hóa đơn, Phiếu nhập)
/// để xuất Excel nhiều sheet (Tổng quan + Chi tiết mặt hàng).
/// </summary>
public partial class DatabaseService
{
    /// <summary>
    /// Lấy chi tiết toàn bộ các mặt hàng của danh sách hóa đơn theo ID.
    /// Không nhân bản tổng tiền hóa đơn vào dòng chi tiết để đảm bảo người dùng cộng tổng số lượng, thành tiền chính xác.
    /// </summary>
    public async Task<IReadOnlyList<HoaDonChiTietDong>> GetChiTietCuaDanhSachHoaDonAsync(IEnumerable<uint> hoaDonIds)
    {
        var idList = hoaDonIds.Distinct().ToList();
        if (idList.Count == 0)
            return Array.Empty<HoaDonChiTietDong>();

        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            ct.hoa_don_id AS HoaDonId,
            hd.ma_hoa_don AS MaHoaDon,
            hd.thoi_gian_ban AS ThoiGianBan,
            COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
            COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
            COALESCE(nd.ho_ten, '') AS NguoiBan,
            COALESCE(ct.ma_san_pham, '') AS MaSanPham,
            ct.ten_san_pham AS TenSanPham,
            COALESCE(ct.don_vi_tinh, '') AS DonViTinh,
            ct.so_luong AS SoLuong,
            ct.don_gia AS DonGia,
            ct.giam_gia AS GiamGia,
            ct.thanh_tien AS ThanhTien,
            ct.ghi_chu AS GhiChu
        FROM chi_tiet_hoa_don_ban_hang ct
        INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        LEFT JOIN nguoi_dung nd ON nd.id = hd.nguoi_ban_id
        WHERE ct.hoa_don_id IN @Ids
        ORDER BY hd.thoi_gian_ban DESC, ct.hoa_don_id DESC, ct.id ASC;
        """;

        var result = await connection.QueryAsync<HoaDonChiTietDong>(sql, new { Ids = idList });
        return result.AsList();
    }

    /// <summary>
    /// Lấy chi tiết toàn bộ các mặt hàng của danh sách phiếu nhập theo ID.
    /// Bao gồm Đơn vị tính, Số lượng, Đơn giá nhập, Giảm giá và Thành tiền.
    /// </summary>
    public async Task<IReadOnlyList<PhieuNhapChiTietDong>> GetChiTietCuaDanhSachPhieuNhapAsync(IEnumerable<uint> phieuNhapIds)
    {
        var idList = phieuNhapIds.Distinct().ToList();
        if (idList.Count == 0)
            return Array.Empty<PhieuNhapChiTietDong>();

        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            ct.phieu_nhap_id AS PhieuNhapId,
            p.ma_phieu_nhap AS MaPhieuNhap,
            p.thoi_gian_nhap AS ThoiGianNhap,
            COALESCE(ncc.ma_nha_cung_cap, '') AS MaNhaCungCap,
            COALESCE(ncc.ten_nha_cung_cap, '') AS TenNhaCungCap,
            COALESCE(nd.ho_ten, '') AS NguoiNhap,
            COALESCE(ct.ma_san_pham, '') AS MaSanPham,
            ct.ten_san_pham AS TenSanPham,
            COALESCE(dvt.ten_don_vi, '') AS DonViTinh,
            ct.so_luong AS SoLuong,
            ct.don_gia_nhap AS DonGiaNhap,
            ct.giam_gia AS GiamGia,
            ct.thanh_tien AS ThanhTien,
            ct.ghi_chu AS GhiChu
        FROM chi_tiet_phieu_nhap_hang ct
        INNER JOIN phieu_nhap_hang p ON p.id = ct.phieu_nhap_id
        LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
        LEFT JOIN nguoi_dung nd ON nd.id = p.nguoi_nhap_id
        LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
        LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
        WHERE ct.phieu_nhap_id IN @Ids
        ORDER BY p.thoi_gian_nhap DESC, ct.phieu_nhap_id DESC, ct.id ASC;
        """;

        var result = await connection.QueryAsync<PhieuNhapChiTietDong>(sql, new { Ids = idList });
        return result.AsList();
    }
}
