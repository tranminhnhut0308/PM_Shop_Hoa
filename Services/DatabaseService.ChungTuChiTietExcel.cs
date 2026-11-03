using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Phần mở rộng của DatabaseService phục vụ truy vấn chi tiết chứng từ (Hóa đơn, Phiếu nhập)
/// để xuất Excel nhiều sheet (Tổng quan + Chi tiết mặt hàng + Tổng hợp theo sản phẩm) theo Issue #63.
/// </summary>
public partial class DatabaseService
{
    /// <summary>
    /// Lấy chi tiết toàn bộ các mặt hàng của danh sách hóa đơn theo ID (tương thích ngược).
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
    /// Lấy chi tiết toàn bộ các mặt hàng của danh sách phiếu nhập theo ID (tương thích ngược).
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
            COALESCE(NULLIF(TRIM(ct.don_vi_tinh_excel), ''), dvt.ten_don_vi, '') AS DonViTinh,
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

    /// <summary>
    /// Truy vấn toàn bộ dữ liệu xuất Excel trang Hóa đơn theo Issue #63:
    /// - Hỗ trợ lọc theo khoảng thời gian và các bộ lọc tìm kiếm, khách hàng, trạng thái
    /// - Trả về dữ liệu Tổng quan, Chi tiết từng mặt hàng và Tổng hợp sản phẩm
    /// </summary>
    public async Task<(
        IReadOnlyList<HoaDonTongQuanXuatDong> TongQuan,
        IReadOnlyList<HoaDonChiTietXuatDong> ChiTiet,
        IReadOnlyList<HoaDonTongHopSanPhamDong> TongHop)> GetHoaDonXuatExcelDataAsync(
        XuatExcelYeuCau yeuCau,
        string? searchKeyword = null,
        uint? khachHangId = null,
        string? trangThaiThanhToan = null,
        string? trangThaiHoaDon = null)
    {
        await using var connection = await OpenAsync();

        var tuNgay = yeuCau.TuNgay.Date;
        var denNgay = yeuCau.DenNgay.Date.AddDays(1).AddTicks(-1);

        var whereSb = new StringBuilder("WHERE hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban <= @DenNgay");
        var parameters = new DynamicParameters();
        parameters.Add("TuNgay", tuNgay);
        parameters.Add("DenNgay", denNgay);

        if (yeuCau.PhamViLoc == PhamViLocXuat.TheoBoLocHienTai)
        {
            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var kw = $"%{searchKeyword.Trim()}%";
                whereSb.Append(" AND (hd.ma_hoa_don LIKE @Kw OR kh.ten_khach_hang LIKE @Kw OR kh.so_dien_thoai LIKE @Kw OR hd.so_dien_thoai LIKE @Kw)");
                parameters.Add("Kw", kw);
            }

            if (khachHangId.HasValue && khachHangId.Value > 0)
            {
                whereSb.Append(" AND hd.khach_hang_id = @KhachHangId");
                parameters.Add("KhachHangId", khachHangId.Value);
            }

            if (!string.IsNullOrWhiteSpace(trangThaiThanhToan) && trangThaiThanhToan != "Tất cả")
            {
                var ttCode = trangThaiThanhToan switch
                {
                    "Đã thanh toán" => "DA_THANH_TOAN",
                    "Thanh toán một phần" => "THANH_TOAN_MOT_PHAN",
                    "Chưa thanh toán" => "CHUA_THANH_TOAN",
                    _ => trangThaiThanhToan
                };
                whereSb.Append(" AND hd.trang_thai_thanh_toan = @TrangThaiThanhToan");
                parameters.Add("TrangThaiThanhToan", ttCode);
            }

            if (!string.IsNullOrWhiteSpace(trangThaiHoaDon) && trangThaiHoaDon != "Tất cả")
            {
                var hdCode = trangThaiHoaDon switch
                {
                    "Hoàn thành" => "HOAN_THANH",
                    "Đã hủy" => "DA_HUY",
                    _ => trangThaiHoaDon
                };

                if (hdCode == "DA_HUY")
                    whereSb.Append(" AND hd.trang_thai_hoa_don IN ('DA_HUY', 'HUY')");
                else
                    whereSb.Append(" AND hd.trang_thai_hoa_don = @TrangThaiHoaDon");

                parameters.Add("TrangThaiHoaDon", hdCode);
            }
        }

        var whereClause = whereSb.ToString();

        // 1. TỔNG QUAN HÓA ĐƠN
        List<HoaDonTongQuanXuatDong> listTongQuan = new();
        if (yeuCau.LoaiBaoCao == LoaiBaoCaoXuat.TongQuan || yeuCau.LoaiBaoCao == LoaiBaoCaoXuat.DayDu)
        {
            var sqlTongQuan = $"""
            SELECT
                hd.id AS Id,
                hd.ma_hoa_don AS MaHoaDon,
                hd.thoi_gian_ban AS NgayLapHoaDon,
                COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
                COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
                COALESCE(NULLIF(TRIM(hd.so_dien_thoai), ''), kh.so_dien_thoai, '') AS SoDienThoai,
                COALESCE(sub_ct.TongMatHang, 0) AS TongSoMatHang,
                COALESCE(sub_ct.TongSoLuong, 0) AS TongSoLuongSanPham,
                hd.tong_tien_hang AS TongTienHang,
                hd.giam_gia AS GiamGia,
                hd.khach_can_tra AS TongThanhToan,
                hd.khach_da_thanh_toan AS DaThanhToan,
                hd.con_no AS ConNo,
                CASE
                    WHEN hd.con_no <= 0 THEN 'Đã thanh toán'
                    WHEN hd.khach_da_thanh_toan > 0 THEN 'Một phần'
                    ELSE 'Chưa thanh toán'
                END AS PhuongThucThanhToan,
                CASE
                    WHEN hd.trang_thai_thanh_toan = 'DA_THANH_TOAN' THEN 'Đã thanh toán'
                    WHEN hd.trang_thai_thanh_toan = 'THANH_TOAN_MOT_PHAN' THEN 'Thanh toán một phần'
                    WHEN hd.trang_thai_thanh_toan = 'CHUA_THANH_TOAN' THEN 'Chưa thanh toán'
                    WHEN hd.con_no <= 0 THEN 'Đã thanh toán'
                    WHEN hd.khach_da_thanh_toan > 0 THEN 'Thanh toán một phần'
                    ELSE 'Chưa thanh toán'
                END AS TrangThaiThanhToan,
                CASE
                    WHEN hd.trang_thai_hoa_don IN ('DA_HUY', 'HUY') THEN 'Đã hủy'
                    WHEN hd.trang_thai_hoa_don = 'TONG_HOP' THEN 'Tổng hợp'
                    ELSE 'Hoàn thành'
                END AS TrangThaiHoaDon,
                COALESCE(nd.ho_ten, '') AS NguoiTao,
                COALESCE(hd.ghi_chu, '') AS GhiChu,
                CASE WHEN hd.trang_thai_hoa_don IN ('DA_HUY', 'HUY') THEN 1 ELSE 0 END AS LaHoaDonDaHuy
            FROM hoa_don_ban_hang hd
            LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
            LEFT JOIN nguoi_dung nd ON nd.id = hd.nguoi_ban_id
            LEFT JOIN (
                SELECT
                    hoa_don_id,
                    COUNT(id) AS TongMatHang,
                    SUM(so_luong) AS TongSoLuong
                FROM chi_tiet_hoa_don_ban_hang
                GROUP BY hoa_don_id
            ) sub_ct ON sub_ct.hoa_don_id = hd.id
            {whereClause}
            ORDER BY hd.thoi_gian_ban DESC, hd.id DESC;
            """;

            var res = await connection.QueryAsync<HoaDonTongQuanXuatDong>(sqlTongQuan, parameters);
            listTongQuan = res.AsList();
            for (var i = 0; i < listTongQuan.Count; i++)
            {
                listTongQuan[i].Stt = i + 1;
            }
        }

        // 2. CHI TIẾT MẶT HÀNG
        List<HoaDonChiTietXuatDong> listChiTiet = new();
        if (yeuCau.LoaiBaoCao == LoaiBaoCaoXuat.ChiTiet || yeuCau.LoaiBaoCao == LoaiBaoCaoXuat.DayDu)
        {
            var sqlChiTiet = $"""
            SELECT
                ct.hoa_don_id AS HoaDonId,
                hd.ma_hoa_don AS MaHoaDon,
                hd.thoi_gian_ban AS NgayLapHoaDon,
                COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
                COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
                COALESCE(ct.ma_san_pham, '') AS MaHang,
                COALESCE(ct.ten_san_pham, '') AS TenHang,
                COALESCE(nsp.ten_nhom, sp.nhom_hang_3_cap_excel, '') AS NhomHang,
                COALESCE(ct.don_vi_tinh, '') AS DonViTinh,
                ct.so_luong AS SoLuong,
                ct.don_gia AS DonGiaBan,
                ct.giam_gia AS GiamGia,
                ct.thanh_tien AS ThanhTien,
                COALESCE(ct.ghi_chu, '') AS GhiChu,
                CASE WHEN hd.trang_thai_hoa_don IN ('DA_HUY', 'HUY') THEN 1 ELSE 0 END AS LaHoaDonDaHuy
            FROM chi_tiet_hoa_don_ban_hang ct
            INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
            LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
            LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            {whereClause}
            ORDER BY hd.thoi_gian_ban DESC, ct.hoa_don_id DESC, ct.id ASC;
            """;

            var res = await connection.QueryAsync<HoaDonChiTietXuatDong>(sqlChiTiet, parameters);
            listChiTiet = res.AsList();
            for (var i = 0; i < listChiTiet.Count; i++)
            {
                listChiTiet[i].Stt = i + 1;
            }
        }

        // 3. TỔNG HỢP THEO SẢN PHẨM (Chỉ tạo khi xuất Đầy đủ)
        // Không tính hóa đơn đã hủy vào tổng hợp hợp lệ
        List<HoaDonTongHopSanPhamDong> listTongHop = new();
        if (yeuCau.LoaiBaoCao == LoaiBaoCaoXuat.DayDu)
        {
            var sqlTongHop = $"""
            SELECT
                COALESCE(ct.ma_san_pham, '') AS MaHang,
                COALESCE(ct.ten_san_pham, '') AS TenHang,
                COALESCE(nsp.ten_nhom, sp.nhom_hang_3_cap_excel, '') AS NhomHang,
                COALESCE(ct.don_vi_tinh, '') AS DonViTinh,
                SUM(ct.so_luong) AS TongSoLuongBan,
                SUM(ct.so_luong * ct.don_gia) AS DoanhThu,
                SUM(ct.thanh_tien) AS DoanhThuSauGiamGia
            FROM chi_tiet_hoa_don_ban_hang ct
            INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
            LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
            {whereClause}
              AND hd.trang_thai_hoa_don NOT IN ('DA_HUY', 'HUY')
            GROUP BY ct.ma_san_pham, ct.ten_san_pham, NhomHang, ct.don_vi_tinh
            ORDER BY TongSoLuongBan DESC, DoanhThuSauGiamGia DESC;
            """;

            var res = await connection.QueryAsync<HoaDonTongHopSanPhamDong>(sqlTongHop, parameters);
            listTongHop = res.AsList();
            for (var i = 0; i < listTongHop.Count; i++)
            {
                listTongHop[i].Stt = i + 1;
            }
        }

        return (listTongQuan, listChiTiet, listTongHop);
    }

    /// <summary>
    /// Truy vấn toàn bộ dữ liệu xuất Excel trang Phiếu nhập hàng theo Issue #63:
    /// - Hỗ trợ lọc theo khoảng thời gian và các bộ lọc tìm kiếm, nhà cung cấp, trạng thái
    /// - Bảo toàn dữ liệu lịch sử snapshot đơn vị tính và hệ số quy đổi
    /// - Trả về dữ liệu Tổng quan, Chi tiết từng mặt hàng và Tổng hợp sản phẩm
    /// </summary>
    public async Task<(
        IReadOnlyList<PhieuNhapTongQuanXuatDong> TongQuan,
        IReadOnlyList<PhieuNhapChiTietXuatDong> ChiTiet,
        IReadOnlyList<PhieuNhapTongHopSanPhamDong> TongHop)> GetPhieuNhapXuatExcelDataAsync(
        XuatExcelYeuCau yeuCau,
        string? searchKeyword = null,
        uint? nhaCungCapId = null,
        string? trangThaiThanhToan = null,
        string? trangThaiPhieu = null)
    {
        await using var connection = await OpenAsync();

        var tuNgay = yeuCau.TuNgay.Date;
        var denNgay = yeuCau.DenNgay.Date.AddDays(1).AddTicks(-1);

        var whereSb = new StringBuilder("WHERE p.thoi_gian_nhap >= @TuNgay AND p.thoi_gian_nhap <= @DenNgay");
        var parameters = new DynamicParameters();
        parameters.Add("TuNgay", tuNgay);
        parameters.Add("DenNgay", denNgay);

        if (yeuCau.PhamViLoc == PhamViLocXuat.TheoBoLocHienTai)
        {
            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var kw = $"%{searchKeyword.Trim()}%";
                whereSb.Append(" AND (p.ma_phieu_nhap LIKE @Kw OR p.so_hoa_don_dau_vao LIKE @Kw OR ncc.ten_nha_cung_cap LIKE @Kw OR p.ten_nha_cung_cap_excel LIKE @Kw OR ncc.so_dien_thoai LIKE @Kw)");
                parameters.Add("Kw", kw);
            }

            if (nhaCungCapId.HasValue && nhaCungCapId.Value > 0)
            {
                whereSb.Append(" AND p.nha_cung_cap_id = @NhaCungCapId");
                parameters.Add("NhaCungCapId", nhaCungCapId.Value);
            }

            if (!string.IsNullOrWhiteSpace(trangThaiThanhToan) && trangThaiThanhToan != "Tất cả")
            {
                switch (trangThaiThanhToan)
                {
                    case "Đã thanh toán":
                        whereSb.Append(" AND (p.con_no <= 0 OR p.trang_thai = 'HOAN_THANH')");
                        break;
                    case "Thanh toán một phần":
                        whereSb.Append(" AND (p.da_thanh_toan > 0 AND p.con_no > 0)");
                        break;
                    case "Chưa thanh toán":
                        whereSb.Append(" AND (p.da_thanh_toan = 0 AND p.con_no > 0)");
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(trangThaiPhieu) && trangThaiPhieu != "Tất cả")
            {
                if (trangThaiPhieu == "Đã hủy")
                    whereSb.Append(" AND p.trang_thai IN ('DA_HUY', 'HUY')");
                else if (trangThaiPhieu == "Phiếu tạm")
                    whereSb.Append(" AND p.trang_thai = 'LUU_TAM'");
                else if (trangThaiPhieu == "Hoàn thành")
                    whereSb.Append(" AND p.trang_thai IN ('DA_NHAP_HANG', 'HOAN_THANH')");
                else
                {
                    whereSb.Append(" AND p.trang_thai = @TrangThaiPhieu");
                    parameters.Add("TrangThaiPhieu", trangThaiPhieu);
                }
            }
        }

        var whereClause = whereSb.ToString();

        // 1. TỔNG QUAN PHIẾU NHẬP
        List<PhieuNhapTongQuanXuatDong> listTongQuan = new();
        if (yeuCau.LoaiBaoCao == LoaiBaoCaoXuat.TongQuan || yeuCau.LoaiBaoCao == LoaiBaoCaoXuat.DayDu)
        {
            var sqlTongQuan = $"""
            SELECT
                p.id AS Id,
                p.ma_phieu_nhap AS MaPhieuNhap,
                p.thoi_gian_nhap AS NgayNhapHang,
                p.ngay_hoa_don_dau_vao AS NgayHoaDonDauVao,
                COALESCE(p.so_hoa_don_dau_vao, '') AS SoHoaDonDauVao,
                COALESCE(ncc.ma_nha_cung_cap, p.ma_nha_cung_cap_excel, '') AS MaNhaCungCap,
                COALESCE(ncc.ten_nha_cung_cap, p.ten_nha_cung_cap_excel, '') AS TenNhaCungCap,
                COALESCE(ncc.so_dien_thoai, p.dien_thoai_ncc_excel, '') AS SoDienThoai,
                COALESCE(sub_ct.TongMatHang, 0) AS TongSoMatHang,
                COALESCE(sub_ct.TongSoLuong, 0) AS TongSoLuongNhap,
                p.tong_tien_hang AS TongTienHang,
                p.giam_gia AS GiamGia,
                p.chi_phi_khac AS ChiPhiKhac,
                p.tong_tien_nhap AS TongThanhToan,
                p.da_thanh_toan AS DaThanhToan,
                p.con_no AS ConNo,
                CASE
                    WHEN p.phuong_thuc_thanh_toan = 'CHUYEN_KHOAN' THEN 'Chuyển khoản'
                    WHEN p.phuong_thuc_thanh_toan = 'TIEN_MAT' THEN 'Tiền mặt'
                    ELSE COALESCE(p.phuong_thuc_thanh_toan, 'Tiền mặt')
                END AS PhuongThucThanhToan,
                CASE
                    WHEN p.con_no <= 0 THEN 'Đã thanh toán'
                    WHEN p.da_thanh_toan > 0 THEN 'Thanh toán một phần'
                    ELSE 'Chưa thanh toán'
                END AS TrangThaiThanhToan,
                CASE
                    WHEN p.trang_thai IN ('DA_HUY', 'HUY') THEN 'Đã hủy'
                    WHEN p.trang_thai = 'LUU_TAM' THEN 'Phiếu tạm'
                    ELSE 'Hoàn thành'
                END AS TrangThaiPhieuNhap,
                COALESCE(nd.ho_ten, p.nguoi_nhap_excel, p.nguoi_tao_excel, '') AS NguoiTao,
                COALESCE(p.ghi_chu, '') AS GhiChu,
                CASE WHEN p.trang_thai IN ('DA_HUY', 'HUY') THEN 1 ELSE 0 END AS LaPhieuDaHuy
            FROM phieu_nhap_hang p
            LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
            LEFT JOIN nguoi_dung nd ON nd.id = p.nguoi_nhap_id
            LEFT JOIN (
                SELECT
                    phieu_nhap_id,
                    COUNT(id) AS TongMatHang,
                    SUM(so_luong) AS TongSoLuong
                FROM chi_tiet_phieu_nhap_hang
                GROUP BY phieu_nhap_id
            ) sub_ct ON sub_ct.phieu_nhap_id = p.id
            {whereClause}
            ORDER BY p.thoi_gian_nhap DESC, p.id DESC;
            """;

            var res = await connection.QueryAsync<PhieuNhapTongQuanXuatDong>(sqlTongQuan, parameters);
            listTongQuan = res.AsList();
            for (var i = 0; i < listTongQuan.Count; i++)
            {
                listTongQuan[i].Stt = i + 1;
            }
        }

        // 2. CHI TIẾT MẶT HÀNG
        List<PhieuNhapChiTietXuatDong> listChiTiet = new();
        if (yeuCau.LoaiBaoCao == LoaiBaoCaoXuat.ChiTiet || yeuCau.LoaiBaoCao == LoaiBaoCaoXuat.DayDu)
        {
            var sqlChiTiet = $"""
            SELECT
                ct.phieu_nhap_id AS PhieuNhapId,
                p.ma_phieu_nhap AS MaPhieuNhap,
                p.thoi_gian_nhap AS NgayNhapHang,
                COALESCE(ncc.ma_nha_cung_cap, p.ma_nha_cung_cap_excel, '') AS MaNhaCungCap,
                COALESCE(ncc.ten_nha_cung_cap, p.ten_nha_cung_cap_excel, '') AS TenNhaCungCap,
                COALESCE(ct.ma_san_pham, '') AS MaHang,
                COALESCE(ct.ten_san_pham, '') AS TenHang,
                COALESCE(nsp.ten_nhom, sp.nhom_hang_3_cap_excel, '') AS NhomHang,
                COALESCE(NULLIF(TRIM(ct.don_vi_tinh_excel), ''), dvt.ten_don_vi, '') AS DonViTinhNhap,
                COALESCE(NULLIF(sp.quy_doi, 0), 1) AS HeSoQuyDoi,
                ct.so_luong AS SoLuongNhap,
                (ct.so_luong * COALESCE(NULLIF(sp.quy_doi, 0), 1)) AS SoLuongQuyDoi,
                ct.don_gia_nhap AS DonGiaNhap,
                ct.giam_gia AS GiamGia,
                ct.thanh_tien AS ThanhTien,
                CASE WHEN p.trang_thai IN ('DA_HUY', 'HUY') THEN 1 ELSE 0 END AS LaPhieuDaHuy
            FROM chi_tiet_phieu_nhap_hang ct
            INNER JOIN phieu_nhap_hang p ON p.id = ct.phieu_nhap_id
            LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
            LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            {whereClause}
            ORDER BY p.thoi_gian_nhap DESC, ct.phieu_nhap_id DESC, ct.id ASC;
            """;

            var res = await connection.QueryAsync<PhieuNhapChiTietXuatDong>(sqlChiTiet, parameters);
            listChiTiet = res.AsList();
            for (var i = 0; i < listChiTiet.Count; i++)
            {
                listChiTiet[i].Stt = i + 1;
            }
        }

        // 3. TỔNG HỢP THEO SẢN PHẨM (Chỉ tạo khi xuất Đầy đủ)
        // Không tính phiếu đã hủy vào tổng hợp hợp lệ
        List<PhieuNhapTongHopSanPhamDong> listTongHop = new();
        if (yeuCau.LoaiBaoCao == LoaiBaoCaoXuat.DayDu)
        {
            var sqlTongHop = $"""
            SELECT
                COALESCE(ct.ma_san_pham, '') AS MaHang,
                COALESCE(ct.ten_san_pham, '') AS TenHang,
                COALESCE(nsp.ten_nhom, sp.nhom_hang_3_cap_excel, '') AS NhomHang,
                COALESCE(NULLIF(TRIM(ct.don_vi_tinh_excel), ''), dvt.ten_don_vi, '') AS DonViTinh,
                SUM(ct.so_luong) AS TongSoLuongNhap,
                SUM(ct.thanh_tien) AS TongTienNhap
            FROM chi_tiet_phieu_nhap_hang ct
            INNER JOIN phieu_nhap_hang p ON p.id = ct.phieu_nhap_id
            LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
            LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            {whereClause}
              AND p.trang_thai NOT IN ('DA_HUY', 'HUY')
            GROUP BY ct.ma_san_pham, ct.ten_san_pham, NhomHang, DonViTinh
            ORDER BY TongSoLuongNhap DESC, TongTienNhap DESC;
            """;

            var res = await connection.QueryAsync<PhieuNhapTongHopSanPhamDong>(sqlTongHop, parameters);
            listTongHop = res.AsList();
            for (var i = 0; i < listTongHop.Count; i++)
            {
                listTongHop[i].Stt = i + 1;
            }
        }

        return (listTongQuan, listChiTiet, listTongHop);
    }
}
