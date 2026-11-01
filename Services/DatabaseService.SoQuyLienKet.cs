using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    private bool? _hasPhieuQuyCancelColumns;
    private readonly SemaphoreSlim _khoaEnsureCancelColumns = new(1, 1);
    private readonly SemaphoreSlim _khoaHuyPhieuQuy = new(1, 1);

    /// <summary>
    /// Bảo đảm các bảng liên quan đến sổ quỹ, thanh toán và công nợ
    /// có đầy đủ các cột phục vụ hủy phiếu và lưu dấu vết kiểm toán (Issue #52).
    /// </summary>
    public async Task<bool> EnsurePhieuQuyCancelColumnsAsync()
    {
        if (_hasPhieuQuyCancelColumns.HasValue)
            return _hasPhieuQuyCancelColumns.Value;

        await _khoaEnsureCancelColumns.WaitAsync();
        try
        {
            if (_hasPhieuQuyCancelColumns.HasValue)
                return _hasPhieuQuyCancelColumns.Value;

            await using var connection = await OpenAsync();

            // 1. phieu_chi: nguoi_huy_id, thoi_gian_huy, ly_do_huy
            await EnsureColumnExistsAsync(connection, "phieu_chi", "nguoi_huy_id", "int(10) unsigned DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "phieu_chi", "thoi_gian_huy", "datetime DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "phieu_chi", "ly_do_huy", "text DEFAULT NULL");

            // 2. thanh_toan: trang_thai, nguoi_huy_id, thoi_gian_huy, ly_do_huy, ma_giao_dich
            await EnsureColumnExistsAsync(connection, "thanh_toan", "trang_thai", "varchar(20) NOT NULL DEFAULT 'HOAN_THANH'");
            await EnsureColumnExistsAsync(connection, "thanh_toan", "ma_giao_dich", "varchar(100) DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "thanh_toan", "nguoi_huy_id", "int(10) unsigned DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "thanh_toan", "thoi_gian_huy", "datetime DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "thanh_toan", "ly_do_huy", "text DEFAULT NULL");

            // 3. thanh_toan_nha_cung_cap: ma_giao_dich, trang_thai, nguoi_huy_id, thoi_gian_huy, ly_do_huy
            await EnsureColumnExistsAsync(connection, "thanh_toan_nha_cung_cap", "ma_giao_dich", "varchar(100) DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "thanh_toan_nha_cung_cap", "trang_thai", "varchar(20) NOT NULL DEFAULT 'HOAN_THANH'");
            await EnsureColumnExistsAsync(connection, "thanh_toan_nha_cung_cap", "nguoi_huy_id", "int(10) unsigned DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "thanh_toan_nha_cung_cap", "thoi_gian_huy", "datetime DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "thanh_toan_nha_cung_cap", "ly_do_huy", "text DEFAULT NULL");

            // 4. lich_su_thanh_toan_cong_no_khach_hang: nguoi_huy_id, thoi_gian_huy, ly_do_huy
            await EnsureBangLichSuThanhToanKhachHangAsync();
            await EnsureColumnExistsAsync(connection, "lich_su_thanh_toan_cong_no_khach_hang", "nguoi_huy_id", "int(10) unsigned DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "lich_su_thanh_toan_cong_no_khach_hang", "thoi_gian_huy", "datetime DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "lich_su_thanh_toan_cong_no_khach_hang", "ly_do_huy", "text DEFAULT NULL");

            // 5. lich_su_thanh_toan_cong_no_nha_cung_cap: nguoi_huy_id, thoi_gian_huy, ly_do_huy
            await EnsureBangLichSuThanhToanNhaCungCapAsync();
            await EnsureColumnExistsAsync(connection, "lich_su_thanh_toan_cong_no_nha_cung_cap", "nguoi_huy_id", "int(10) unsigned DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "lich_su_thanh_toan_cong_no_nha_cung_cap", "thoi_gian_huy", "datetime DEFAULT NULL");
            await EnsureColumnExistsAsync(connection, "lich_su_thanh_toan_cong_no_nha_cung_cap", "ly_do_huy", "text DEFAULT NULL");

            _hasPhieuQuyCancelColumns = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("EnsurePhieuQuyCancelColumnsAsync error", ex);
            _hasPhieuQuyCancelColumns = false;
        }
        finally
        {
            _khoaEnsureCancelColumns.Release();
        }

        return _hasPhieuQuyCancelColumns.Value;
    }

    private static async Task EnsureColumnExistsAsync(
        MySqlConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        const string checkSql = """
            SELECT COUNT(1) > 0 FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = @TableName
              AND column_name = @ColumnName;
        """;
        var exists = await connection.ExecuteScalarAsync<bool>(
            checkSql,
            new { TableName = tableName, ColumnName = columnName });
        if (!exists)
        {
            try
            {
                await connection.ExecuteAsync($"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {columnDefinition};");
            }
            catch (MySqlException ex) when (ex.Number == 1060)
            {
                // Cột đã tồn tại do tiến trình khác thêm đồng thời
            }
        }
    }

    private sealed class PhieuChiHuyRow
    {
        public uint Id { get; set; }
        public string MaPhieuChi { get; set; } = "";
        public string? LoaiPhieu { get; set; }
        public uint ChiNhanhId { get; set; }
        public uint? NhomChiPhiId { get; set; }
        public decimal SoTien { get; set; }
        public string PhuongThuc { get; set; } = "";
        public string? NoiDung { get; set; }
        public string? NguoiNopNhan { get; set; }
        public string? GhiChu { get; set; }
        public DateTime ThoiGianChi { get; set; }
        public string TrangThai { get; set; } = "";
        public uint? HoaDonBanHangId { get; set; }
        public string? MaHoaDon { get; set; }
        public uint? PhieuNhapHangId { get; set; }
        public string? MaPhieuNhap { get; set; }
        public uint? NguoiHuyId { get; set; }
        public DateTime? ThoiGianHuy { get; set; }
        public string? LyDoHuy { get; set; }
    }

    /// <summary>
    /// Đọc thông tin chi tiết đầy đủ của một phiếu thu/chi kèm toàn bộ chứng từ liên quan
    /// và lịch sử hủy phiếu (Issue #52 - Phần I).
    /// </summary>
    public async Task<SoQuyChiTiet?> GetSoQuyChiTietDayDuAsync(string maPhieu)
    {
        if (string.IsNullOrWhiteSpace(maPhieu))
            return null;

        var maCanTim = maPhieu.Trim();

        await EnsurePhieuQuyExtraColumnsAsync();
        await EnsurePhieuQuyHoaDonColumnsAsync();
        await EnsurePhieuQuyPhieuNhapColumnsAsync();
        await EnsurePhieuQuyCancelColumnsAsync();

        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            pc.id AS Id,
            pc.ma_phieu_chi AS MaPhieuChi,
            pc.loai_phieu AS LoaiPhieu,
            pc.so_tien AS SoTien,
            COALESCE(pc.noi_dung, '') AS NoiDung,
            COALESCE(pc.nguoi_nop_nhan, '') AS NguoiNopNhan,
            COALESCE(pc.ghi_chu, '') AS GhiChu,
            pc.thoi_gian_chi AS ThoiGianChi,
            COALESCE(nd.ho_ten, '') AS NguoiTao,
            pc.trang_thai AS TrangThai,
            COALESCE(ncp.ten_nhom, '') AS TenLoai,
            pc.nhom_chi_phi_id AS NhomChiPhiId,
            COALESCE(pc.phuong_thuc, '') AS PhuongThuc,
            pc.hoa_don_ban_hang_id AS HoaDonBanHangId,
            pc.ma_hoa_don AS MaHoaDon,
            pc.phieu_nhap_hang_id AS PhieuNhapHangId,
            pc.ma_phieu_nhap AS MaPhieuNhap,
            pc.nguoi_huy_id AS NguoiHuyId,
            pc.thoi_gian_huy AS ThoiGianHuy,
            pc.ly_do_huy AS LyDoHuy,
            COALESCE(nd_huy.ho_ten, nd_huy.ten_dang_nhap, '') AS NguoiHuy
        FROM phieu_chi pc
        LEFT JOIN nguoi_dung nd ON nd.id = pc.nguoi_thuc_hien_id
        LEFT JOIN nhom_chi_phi ncp ON ncp.id = pc.nhom_chi_phi_id
        LEFT JOIN nguoi_dung nd_huy ON nd_huy.id = pc.nguoi_huy_id
        WHERE pc.ma_phieu_chi = @MaPhieu
        LIMIT 1;
        """;

        var row = await connection.QueryFirstOrDefaultAsync(sql, new { MaPhieu = maCanTim });
        if (row is null)
            return null;

        string ma = row.MaPhieuChi?.ToString() ?? maCanTim;
        string? loaiPhieu = row.LoaiPhieu?.ToString();
        var laThu = LaPhieuThu(loaiPhieu, ma);

        var chiTiet = new SoQuyChiTiet
        {
            MaPhieu = ma,
            LaThu = laThu,
            ThoiGian = row.ThoiGianChi is DateTime dt ? dt : DateTime.Now,
            NguoiNopNhan = row.NguoiNopNhan?.ToString() ?? "",
            SoTien = ToDecimal(row.SoTien),
            NoiDung = row.NoiDung?.ToString() ?? "",
            GhiChu = row.GhiChu?.ToString() ?? "",
            NguoiTao = row.NguoiTao?.ToString() ?? "",
            TrangThai = row.TrangThai?.ToString() ?? "",
            TenLoai = row.TenLoai?.ToString() ?? "",
            NhomChiPhiId = row.NhomChiPhiId is not null ? Convert.ToUInt32(row.NhomChiPhiId) : null,
            PhuongThuc = row.PhuongThuc?.ToString() ?? "",
            NguoiHuy = row.NguoiHuy?.ToString(),
            ThoiGianHuy = row.ThoiGianHuy is DateTime dtHuy ? dtHuy : null,
            LyDoHuy = row.LyDoHuy?.ToString()
        };

        string? maHd = row.MaHoaDon?.ToString();
        uint? hdId = row.HoaDonBanHangId is not null ? Convert.ToUInt32(row.HoaDonBanHangId) : null;
        string? maPn = row.MaPhieuNhap?.ToString();
        uint? pnId = row.PhieuNhapHangId is not null ? Convert.ToUInt32(row.PhieuNhapHangId) : null;

        if (laThu)
        {
            // ========================================================
            // PHIẾU THU: TÌM CHỨNG TỪ LIÊN QUAN
            // ========================================================

            // 1. Kiểm tra xem có giao dịch thanh toán công nợ khách hàng (gộp/phân bổ nhiều hóa đơn)
            var lsttKhach = await connection.QueryFirstOrDefaultAsync(
                """
                SELECT
                    ls.khach_hang_id AS KhachHangId,
                    ls.cong_no_truoc AS CongNoTruoc,
                    ls.cong_no_sau AS CongNoSau,
                    ls.ghi_chu AS GhiChu,
                    ls.thoi_gian AS ThoiGian,
                    ls.thoi_gian_huy AS ThoiGianHuy,
                    ls.ly_do_huy AS LyDoHuy,
                    COALESCE(nd_huy.ho_ten, nd_huy.ten_dang_nhap, '') AS NguoiHuy
                FROM lich_su_thanh_toan_cong_no_khach_hang ls
                LEFT JOIN nguoi_dung nd_huy ON nd_huy.id = ls.nguoi_huy_id
                WHERE ls.ma_giao_dich = @MaPhieu
                LIMIT 1;
                """,
                new { MaPhieu = ma });

            if (lsttKhach is not null)
            {
                chiTiet.LaThanhToanCongNo = true;
                chiTiet.CongNoTruoc = ToDecimal(lsttKhach.CongNoTruoc);
                chiTiet.CongNoSau = ToDecimal(lsttKhach.CongNoSau);

                if (chiTiet.DaHuy && string.IsNullOrWhiteSpace(chiTiet.LyDoHuy))
                {
                    chiTiet.LyDoHuy = lsttKhach.LyDoHuy?.ToString();
                    chiTiet.ThoiGianHuy = lsttKhach.ThoiGianHuy is DateTime dtLsh ? dtLsh : null;
                    chiTiet.NguoiHuy = lsttKhach.NguoiHuy?.ToString();
                }

                // Lấy tất cả hóa đơn được phân bổ từ bảng thanh_toan
                var allocations = (await connection.QueryAsync(
                    """
                    SELECT
                        hd.ma_hoa_don AS MaHoaDon,
                        COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách hàng') AS DoiTac,
                        tt.so_tien AS SoTienLienQuan,
                        hd.khach_can_tra AS TongTienChungTu,
                        hd.thoi_gian_ban AS NgayChungTu,
                        hd.trang_thai_hoa_don AS TrangThaiChungTu
                    FROM thanh_toan tt
                    INNER JOIN hoa_don_ban_hang hd ON hd.id = tt.hoa_don_id
                    LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
                    WHERE (tt.ma_giao_dich = @MaPhieu OR tt.ghi_chu LIKE CONCAT('%[Phiếu thu ', @MaPhieu, ']%'))
                    ORDER BY hd.thoi_gian_ban ASC, hd.id ASC;
                    """,
                    new { MaPhieu = ma })).AsList();

                foreach (var item in allocations)
                {
                    chiTiet.DanhSachChungTuLienQuan.Add(new SoQuyChungTuLienQuan
                    {
                        LoaiChungTu = "Hóa đơn bán hàng",
                        LaHoaDon = true,
                        MaChungTu = item.MaHoaDon?.ToString() ?? "",
                        DoiTac = item.DoiTac?.ToString() ?? "",
                        SoTienLienQuan = ToDecimal(item.SoTienLienQuan),
                        TongTienChungTu = ToDecimal(item.TongTienChungTu),
                        NgayChungTu = item.NgayChungTu is DateTime dtHd ? dtHd : null,
                        TrangThaiChungTu = item.TrangThaiChungTu?.ToString() ?? "HOAN_THANH"
                    });
                }
            }

            // 2. Nếu chưa có danh sách phân bổ, kiểm tra hóa đơn liên kết trực tiếp (bán hàng hoặc thu nợ 1 hóa đơn)
            if (chiTiet.DanhSachChungTuLienQuan.Count == 0 && (!string.IsNullOrWhiteSpace(maHd) || hdId is > 0))
            {
                var hdRow = await connection.QueryFirstOrDefaultAsync(
                    """
                    SELECT
                        hd.id AS HoaDonId,
                        hd.ma_hoa_don AS MaHoaDon,
                        hd.khach_hang_id AS KhachHangId,
                        COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
                        COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
                        hd.khach_can_tra AS TongTienChungTu,
                        hd.khach_da_thanh_toan AS KhachDaThanhToan,
                        hd.thoi_gian_ban AS NgayChungTu,
                        hd.trang_thai_hoa_don AS TrangThaiChungTu
                    FROM hoa_don_ban_hang hd
                    LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
                    WHERE (hd.id = @HoaDonId OR hd.ma_hoa_don = @MaHoaDon)
                    LIMIT 1;
                    """,
                    new { HoaDonId = hdId, MaHoaDon = maHd });

                if (hdRow is not null)
                {
                    uint? khId = hdRow.KhachHangId is not null ? Convert.ToUInt32(hdRow.KhachHangId) : null;
                    string maKh = hdRow.MaKhachHang?.ToString() ?? "";
                    string tenKh = hdRow.TenKhachHang?.ToString() ?? "";

                    var laKhachLe = khId == null || khId == 0
                                    || string.Equals(maKh, "KL", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(tenKh, "Khách lẻ", StringComparison.OrdinalIgnoreCase);

                    chiTiet.LaKhachLe = laKhachLe;
                    chiTiet.MaHoaDonKhachLe = hdRow.MaHoaDon?.ToString();

                    chiTiet.DanhSachChungTuLienQuan.Add(new SoQuyChungTuLienQuan
                    {
                        LoaiChungTu = "Hóa đơn bán hàng",
                        LaHoaDon = true,
                        MaChungTu = hdRow.MaHoaDon?.ToString() ?? (maHd ?? ""),
                        DoiTac = tenKh,
                        SoTienLienQuan = chiTiet.SoTien,
                        TongTienChungTu = ToDecimal(hdRow.TongTienChungTu),
                        NgayChungTu = hdRow.NgayChungTu is DateTime dtHd ? dtHd : null,
                        TrangThaiChungTu = hdRow.TrangThaiChungTu?.ToString() ?? "HOAN_THANH"
                    });
                }
            }

            // 3. Fallback: Tìm qua bảng thanh_toan nếu có ghi chú gắn mã phiếu
            if (chiTiet.DanhSachChungTuLienQuan.Count == 0)
            {
                var fallbackAllocs = (await connection.QueryAsync(
                    """
                    SELECT
                        hd.ma_hoa_don AS MaHoaDon,
                        COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách hàng') AS DoiTac,
                        tt.so_tien AS SoTienLienQuan,
                        hd.khach_can_tra AS TongTienChungTu,
                        hd.thoi_gian_ban AS NgayChungTu,
                        hd.trang_thai_hoa_don AS TrangThaiChungTu
                    FROM thanh_toan tt
                    INNER JOIN hoa_don_ban_hang hd ON hd.id = tt.hoa_don_id
                    LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
                    WHERE (tt.ma_giao_dich = @MaPhieu OR tt.ghi_chu LIKE CONCAT('%[Phiếu thu ', @MaPhieu, ']%'))
                    ORDER BY hd.thoi_gian_ban ASC, hd.id ASC;
                    """,
                    new { MaPhieu = ma })).AsList();

                foreach (var item in fallbackAllocs)
                {
                    chiTiet.DanhSachChungTuLienQuan.Add(new SoQuyChungTuLienQuan
                    {
                        LoaiChungTu = "Hóa đơn bán hàng",
                        LaHoaDon = true,
                        MaChungTu = item.MaHoaDon?.ToString() ?? "",
                        DoiTac = item.DoiTac?.ToString() ?? "",
                        SoTienLienQuan = ToDecimal(item.SoTienLienQuan),
                        TongTienChungTu = ToDecimal(item.TongTienChungTu),
                        NgayChungTu = item.NgayChungTu is DateTime dtHd ? dtHd : null,
                        TrangThaiChungTu = item.TrangThaiChungTu?.ToString() ?? "HOAN_THANH"
                    });
                }
            }
        }
        else
        {
            // ========================================================
            // PHIẾU CHI: TÌM CHỨNG TỪ LIÊN QUAN
            // ========================================================

            // 1. Kiểm tra xem có giao dịch thanh toán công nợ nhà cung cấp (gộp nhiều phiếu nhập)
            var lsttNcc = await connection.QueryFirstOrDefaultAsync(
                """
                SELECT
                    ls.nha_cung_cap_id AS NhaCungCapId,
                    ls.ten_nha_cung_cap AS TenNhaCungCap,
                    ls.cong_no_truoc AS CongNoTruoc,
                    ls.cong_no_sau AS CongNoSau,
                    ls.ghi_chu AS GhiChu,
                    ls.thoi_gian AS ThoiGian,
                    ls.thoi_gian_huy AS ThoiGianHuy,
                    ls.ly_do_huy AS LyDoHuy,
                    COALESCE(nd_huy.ho_ten, nd_huy.ten_dang_nhap, '') AS NguoiHuy
                FROM lich_su_thanh_toan_cong_no_nha_cung_cap ls
                LEFT JOIN nguoi_dung nd_huy ON nd_huy.id = ls.nguoi_huy_id
                WHERE ls.ma_giao_dich = @MaPhieu
                LIMIT 1;
                """,
                new { MaPhieu = ma });

            if (lsttNcc is not null)
            {
                chiTiet.LaThanhToanCongNo = true;
                chiTiet.CongNoTruoc = ToDecimal(lsttNcc.CongNoTruoc);
                chiTiet.CongNoSau = ToDecimal(lsttNcc.CongNoSau);

                if (chiTiet.DaHuy && string.IsNullOrWhiteSpace(chiTiet.LyDoHuy))
                {
                    chiTiet.LyDoHuy = lsttNcc.LyDoHuy?.ToString();
                    chiTiet.ThoiGianHuy = lsttNcc.ThoiGianHuy is DateTime dtLsh ? dtLsh : null;
                    chiTiet.NguoiHuy = lsttNcc.NguoiHuy?.ToString();
                }

                // Lấy tất cả phiếu nhập được phân bổ
                var allocations = (await connection.QueryAsync(
                    """
                    SELECT
                        p.ma_phieu_nhap AS MaPhieuNhap,
                        COALESCE(NULLIF(TRIM(ncc.ten_nha_cung_cap), ''), 'Nhà cung cấp') AS DoiTac,
                        t.so_tien AS SoTienLienQuan,
                        p.tong_tien_nhap AS TongTienChungTu,
                        p.thoi_gian_nhap AS NgayChungTu,
                        p.trang_thai AS TrangThaiChungTu
                    FROM thanh_toan_nha_cung_cap t
                    INNER JOIN phieu_nhap_hang p ON p.id = t.phieu_nhap_id
                    LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
                    WHERE (t.ma_giao_dich = @MaPhieu OR t.ghi_chu LIKE CONCAT('%[Giao dịch ', @MaPhieu, ']%') OR t.ghi_chu LIKE CONCAT('%', @MaPhieu, '%'))
                    ORDER BY p.thoi_gian_nhap ASC, p.id ASC;
                    """,
                    new { MaPhieu = ma })).AsList();

                foreach (var item in allocations)
                {
                    chiTiet.DanhSachChungTuLienQuan.Add(new SoQuyChungTuLienQuan
                    {
                        LoaiChungTu = "Phiếu nhập hàng",
                        LaPhieuNhap = true,
                        MaChungTu = item.MaPhieuNhap?.ToString() ?? "",
                        DoiTac = item.DoiTac?.ToString() ?? "",
                        SoTienLienQuan = ToDecimal(item.SoTienLienQuan),
                        TongTienChungTu = ToDecimal(item.TongTienChungTu),
                        NgayChungTu = item.NgayChungTu is DateTime dtPn ? dtPn : null,
                        TrangThaiChungTu = item.TrangThaiChungTu?.ToString() ?? "HOAN_THANH"
                    });
                }
            }

            // 2. Nếu chưa có danh sách phân bổ, kiểm tra phiếu nhập liên kết trực tiếp
            if (chiTiet.DanhSachChungTuLienQuan.Count == 0 && (!string.IsNullOrWhiteSpace(maPn) || pnId is > 0))
            {
                var pnRow = await connection.QueryFirstOrDefaultAsync(
                    """
                    SELECT
                        p.id AS PhieuNhapId,
                        p.ma_phieu_nhap AS MaPhieuNhap,
                        COALESCE(NULLIF(TRIM(ncc.ten_nha_cung_cap), ''), 'Nhà cung cấp') AS TenNhaCungCap,
                        p.tong_tien_nhap AS TongTienChungTu,
                        p.da_thanh_toan AS DaThanhToan,
                        p.thoi_gian_nhap AS NgayChungTu,
                        p.trang_thai AS TrangThaiChungTu
                    FROM phieu_nhap_hang p
                    LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
                    WHERE (p.id = @PhieuNhapId OR p.ma_phieu_nhap = @MaPhieuNhap)
                    LIMIT 1;
                    """,
                    new { PhieuNhapId = pnId, MaPhieuNhap = maPn });

                if (pnRow is not null)
                {
                    chiTiet.DanhSachChungTuLienQuan.Add(new SoQuyChungTuLienQuan
                    {
                        LoaiChungTu = "Phiếu nhập hàng",
                        LaPhieuNhap = true,
                        MaChungTu = pnRow.MaPhieuNhap?.ToString() ?? (maPn ?? ""),
                        DoiTac = pnRow.TenNhaCungCap?.ToString() ?? "Nhà cung cấp",
                        SoTienLienQuan = chiTiet.SoTien,
                        TongTienChungTu = ToDecimal(pnRow.TongTienChungTu),
                        NgayChungTu = pnRow.NgayChungTu is DateTime dtPn ? dtPn : null,
                        TrangThaiChungTu = pnRow.TrangThaiChungTu?.ToString() ?? "HOAN_THANH"
                    });
                }
            }

            // 3. Kiểm tra xem có phải phiếu chi hoàn tiền khi hủy hóa đơn
            if (chiTiet.DanhSachChungTuLienQuan.Count == 0 && !string.IsNullOrWhiteSpace(maHd))
            {
                var hdRow = await connection.QueryFirstOrDefaultAsync(
                    """
                    SELECT
                        hd.ma_hoa_don AS MaHoaDon,
                        COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách hàng') AS TenKhachHang,
                        hd.khach_can_tra AS TongTienChungTu,
                        hd.thoi_gian_ban AS NgayChungTu,
                        hd.trang_thai_hoa_don AS TrangThaiChungTu
                    FROM hoa_don_ban_hang hd
                    LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
                    WHERE hd.ma_hoa_don = @MaHoaDon
                    LIMIT 1;
                    """,
                    new { MaHoaDon = maHd });

                if (hdRow is not null)
                {
                    chiTiet.DanhSachChungTuLienQuan.Add(new SoQuyChungTuLienQuan
                    {
                        LoaiChungTu = "Hóa đơn được hoàn tiền",
                        LaHoaDon = true,
                        MaChungTu = hdRow.MaHoaDon?.ToString() ?? maHd,
                        DoiTac = hdRow.TenKhachHang?.ToString() ?? "Khách hàng",
                        SoTienLienQuan = chiTiet.SoTien,
                        TongTienChungTu = ToDecimal(hdRow.TongTienChungTu),
                        NgayChungTu = hdRow.NgayChungTu is DateTime dtHd ? dtHd : null,
                        TrangThaiChungTu = hdRow.TrangThaiChungTu?.ToString() ?? "DA_HUY"
                    });
                }
            }
        }

        return chiTiet;
    }

    /// <summary>
    /// Hủy phiếu thu hoặc phiếu chi với quy trình hoàn tác tài chính toàn vẹn:
    /// cập nhật công nợ, số tiền thanh toán, trạng thái chứng từ gốc và lịch sử thanh toán
    /// trong một database transaction duy nhất (Issue #52 - Phần II & III & VII).
    /// </summary>
    public async Task<int> HuyPhieuQuyChiTietAsync(string maPhieu, string lyDoHuy, uint? userId)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CashBookCancel, "Bạn không có quyền hủy phiếu thu/chi.");

        if (string.IsNullOrWhiteSpace(maPhieu))
            throw new ArgumentException("Mã phiếu không được để trống.", nameof(maPhieu));

        if (string.IsNullOrWhiteSpace(lyDoHuy))
            throw new ArgumentException("Vui lòng nhập lý do hủy phiếu.", nameof(lyDoHuy));

        await EnsurePhieuQuyCancelColumnsAsync();
        await EnsurePhieuQuyHoaDonColumnsAsync();
        await EnsurePhieuQuyPhieuNhapColumnsAsync();

        await _khoaHuyPhieuQuy.WaitAsync();
        try
        {
            await using var connection = await OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. Khoá dòng phieu_chi để chống xử lý đồng thời
                var phieu = await connection.QueryFirstOrDefaultAsync<PhieuChiHuyRow>(
                    """
                    SELECT
                        id AS Id,
                        ma_phieu_chi AS MaPhieuChi,
                        loai_phieu AS LoaiPhieu,
                        chi_nhanh_id AS ChiNhanhId,
                        nhom_chi_phi_id AS NhomChiPhiId,
                        so_tien AS SoTien,
                        phuong_thuc AS PhuongThuc,
                        noi_dung AS NoiDung,
                        nguoi_nop_nhan AS NguoiNopNhan,
                        ghi_chu AS GhiChu,
                        thoi_gian_chi AS ThoiGianChi,
                        trang_thai AS TrangThai,
                        hoa_don_ban_hang_id AS HoaDonBanHangId,
                        ma_hoa_don AS MaHoaDon,
                        phieu_nhap_hang_id AS PhieuNhapHangId,
                        ma_phieu_nhap AS MaPhieuNhap
                    FROM phieu_chi
                    WHERE ma_phieu_chi = @MaPhieu
                    LIMIT 1
                    FOR UPDATE;
                    """,
                    new { MaPhieu = maPhieu.Trim() },
                    transaction);

                if (phieu is null)
                    throw new InvalidOperationException($"Không tìm thấy phiếu {maPhieu} trong database.");

                if (string.Equals(phieu.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Phiếu {maPhieu} đã bị hủy trước đó.");

                var laThu = LaPhieuThu(phieu.LoaiPhieu, phieu.MaPhieuChi);
                var now = DateTime.Now;
                var soTienPhieu = phieu.SoTien;
                var userHuyId = userId ?? Session.CurrentUser?.Id;

                if (laThu)
                {
                    // ========================================================
                    // HỦY PHIẾU THU & HOÀN TÁC CÔNG NỢ KHÁCH HÀNG (PHẦN II)
                    // ========================================================

                    string? maHoaDon = phieu.MaHoaDon;
                    uint? hoaDonId = phieu.HoaDonBanHangId;

                    if (string.IsNullOrWhiteSpace(maHoaDon) && hoaDonId is > 0)
                    {
                        maHoaDon = await connection.ExecuteScalarAsync<string>(
                            "SELECT ma_hoa_don FROM hoa_don_ban_hang WHERE id = @Id LIMIT 1;",
                            new { Id = hoaDonId },
                            transaction);
                    }

                    if (!string.IsNullOrWhiteSpace(maHoaDon))
                    {
                        // Kiểm tra khách lẻ
                        var khachInfo = await connection.QueryFirstOrDefaultAsync<(uint? KhachHangId, string? MaKhachHang, string? TenKhachHang, string? TrangThaiHoaDon)>(
                            """
                            SELECT
                                hd.khach_hang_id AS KhachHangId,
                                kh.ma_khach_hang AS MaKhachHang,
                                kh.ten_khach_hang AS TenKhachHang,
                                hd.trang_thai_hoa_don AS TrangThaiHoaDon
                            FROM hoa_don_ban_hang hd
                            LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
                            WHERE hd.ma_hoa_don = @MaHoaDon
                            LIMIT 1
                            FOR UPDATE;
                            """,
                            new { MaHoaDon = maHoaDon },
                            transaction);

                        var laKhachLe = khachInfo.KhachHangId == null || khachInfo.KhachHangId == 0
                                        || string.Equals(khachInfo.MaKhachHang, "KL", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(khachInfo.TenKhachHang, "Khách lẻ", StringComparison.OrdinalIgnoreCase);

                        if (laKhachLe)
                        {
                            // Requirement 8: Phiếu thu của khách lẻ không cho phép hủy độc lập
                            throw new InvalidOperationException(
                                "Phiếu thu này thuộc hóa đơn của khách lẻ. Để hủy giao dịch, vui lòng thực hiện hủy hóa đơn liên quan. Phiếu thu sẽ được hủy đồng thời.");
                        }

                        // Hoàn tác số tiền thanh toán của hóa đơn
                        await connection.ExecuteAsync(
                            """
                            UPDATE hoa_don_ban_hang
                            SET khach_da_thanh_toan = GREATEST(0, khach_da_thanh_toan - @SoTien),
                                con_no = LEAST(khach_can_tra, con_no + @SoTien),
                                trang_thai_thanh_toan = CASE
                                    WHEN (con_no + @SoTien) >= khach_can_tra THEN 'CHUA_THANH_TOAN'
                                    ELSE 'CONG_NO'
                                END,
                                ngay_cap_nhat = @Now
                            WHERE ma_hoa_don = @MaHoaDon;
                            """,
                            new { SoTien = soTienPhieu, Now = now, MaHoaDon = maHoaDon },
                            transaction);

                        // Cập nhật trạng thái trong thanh_toan
                        await connection.ExecuteAsync(
                            """
                            UPDATE thanh_toan
                            SET trang_thai = 'HUY',
                                thoi_gian_huy = @Now,
                                nguoi_huy_id = @UserId,
                                ly_do_huy = @LyDoHuy
                            WHERE (ma_giao_dich = @MaPhieu
                                   OR ghi_chu LIKE CONCAT('%[Phiếu thu ', @MaPhieu, ']%')
                                   OR (hoa_don_id = (SELECT id FROM hoa_don_ban_hang WHERE ma_hoa_don = @MaHoaDon LIMIT 1) AND so_tien = @SoTien))
                              AND trang_thai = 'HOAN_THANH';
                            """,
                            new
                            {
                                MaPhieu = phieu.MaPhieuChi,
                                MaHoaDon = maHoaDon,
                                SoTien = soTienPhieu,
                                Now = now,
                                UserId = userHuyId,
                                LyDoHuy = lyDoHuy.Trim()
                            },
                            transaction);

                        // Khôi phục công nợ khách hàng (ghi nợ = tăng nợ lại)
                        if (khachInfo.KhachHangId is > 0)
                        {
                            var soDuCu = await connection.ExecuteScalarAsync<decimal>(
                                """
                                SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                                FROM lich_su_cong_no_khach_hang
                                WHERE khach_hang_id = @KhachHangId
                                FOR UPDATE;
                                """,
                                new { KhachHangId = khachInfo.KhachHangId.Value },
                                transaction);

                            var soDuMoi = soDuCu + soTienPhieu;

                            await connection.ExecuteAsync(
                                """
                                INSERT INTO lich_su_cong_no_khach_hang
                                (khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                                 ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                                VALUES
                                (@KhachHangId, @ChiNhanhId, 'HUY_THANH_TOAN', 'PHIEU_THU', @MaChungTu,
                                 @GhiNo, 0, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu);
                                """,
                                new
                                {
                                    KhachHangId = khachInfo.KhachHangId.Value,
                                    ChiNhanhId = phieu.ChiNhanhId,
                                    MaChungTu = phieu.MaPhieuChi,
                                    GhiNo = soTienPhieu,
                                    SoDu = soDuMoi,
                                    ThoiGian = now,
                                    NguoiThucHienId = userHuyId,
                                    GhiChu = $"Hoàn tác thanh toán hóa đơn {maHoaDon} do hủy phiếu thu {phieu.MaPhieuChi}. Lý do: {lyDoHuy.Trim()}"
                                },
                                transaction);
                        }

                        // Cập nhật lich_su_thanh_toan_cong_no_khach_hang nếu có
                        await connection.ExecuteAsync(
                            """
                            UPDATE lich_su_thanh_toan_cong_no_khach_hang
                            SET trang_thai = 'HUY',
                                thoi_gian_huy = @Now,
                                nguoi_huy_id = @UserId,
                                ly_do_huy = @LyDoHuy
                            WHERE ma_giao_dich = @MaPhieu;
                            """,
                            new { MaPhieu = phieu.MaPhieuChi, Now = now, UserId = userHuyId, LyDoHuy = lyDoHuy.Trim() },
                            transaction);
                    }
                    else
                    {
                        // Trường hợp thanh toán công nợ khách hàng (phân bổ nhiều hóa đơn)
                        var lstt = await connection.QueryFirstOrDefaultAsync<(long Id, uint KhachHangId)>(
                            """
                            SELECT id AS Id, khach_hang_id AS KhachHangId
                            FROM lich_su_thanh_toan_cong_no_khach_hang
                            WHERE ma_giao_dich = @MaPhieu
                            LIMIT 1
                            FOR UPDATE;
                            """,
                            new { MaPhieu = phieu.MaPhieuChi },
                            transaction);

                        // Lấy các hóa đơn phân bổ
                        var allocations = (await connection.QueryAsync<(uint HoaDonId, decimal SoTien)>(
                            """
                            SELECT hoa_don_id AS HoaDonId, so_tien AS SoTien
                            FROM thanh_toan
                            WHERE (ma_giao_dich = @MaPhieu OR ghi_chu LIKE CONCAT('%[Phiếu thu ', @MaPhieu, ']%'))
                              AND trang_thai = 'HOAN_THANH'
                            FOR UPDATE;
                            """,
                            new { MaPhieu = phieu.MaPhieuChi },
                            transaction)).AsList();

                        foreach (var alloc in allocations)
                        {
                            await connection.ExecuteAsync(
                                """
                                UPDATE hoa_don_ban_hang
                                SET khach_da_thanh_toan = GREATEST(0, khach_da_thanh_toan - @SoTien),
                                    con_no = LEAST(khach_can_tra, con_no + @SoTien),
                                    trang_thai_thanh_toan = CASE
                                        WHEN (con_no + @SoTien) >= khach_can_tra THEN 'CHUA_THANH_TOAN'
                                        ELSE 'CONG_NO'
                                    END,
                                    ngay_cap_nhat = @Now
                                WHERE id = @HoaDonId;
                                """,
                                new { SoTien = alloc.SoTien, Now = now, HoaDonId = alloc.HoaDonId },
                                transaction);
                        }

                        // Đánh dấu dòng thanh_toan phân bổ là HUY
                        await connection.ExecuteAsync(
                            """
                            UPDATE thanh_toan
                            SET trang_thai = 'HUY',
                                thoi_gian_huy = @Now,
                                nguoi_huy_id = @UserId,
                                ly_do_huy = @LyDoHuy
                            WHERE (ma_giao_dich = @MaPhieu OR ghi_chu LIKE CONCAT('%[Phiếu thu ', @MaPhieu, ']%'))
                              AND trang_thai = 'HOAN_THANH';
                            """,
                            new { MaPhieu = phieu.MaPhieuChi, Now = now, UserId = userHuyId, LyDoHuy = lyDoHuy.Trim() },
                            transaction);

                        // Khôi phục công nợ tổng của khách hàng
                        if (lstt.KhachHangId > 0)
                        {
                            var soDuCu = await connection.ExecuteScalarAsync<decimal>(
                                """
                                SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                                FROM lich_su_cong_no_khach_hang
                                WHERE khach_hang_id = @KhachHangId
                                FOR UPDATE;
                                """,
                                new { KhachHangId = lstt.KhachHangId },
                                transaction);

                            var soDuMoi = soDuCu + soTienPhieu;

                            await connection.ExecuteAsync(
                                """
                                INSERT INTO lich_su_cong_no_khach_hang
                                (khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                                 ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                                VALUES
                                (@KhachHangId, @ChiNhanhId, 'HUY_THANH_TOAN', 'PHIEU_THU', @MaChungTu,
                                 @GhiNo, 0, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu);
                                """,
                                new
                                {
                                    KhachHangId = lstt.KhachHangId,
                                    ChiNhanhId = phieu.ChiNhanhId,
                                    MaChungTu = phieu.MaPhieuChi,
                                    GhiNo = soTienPhieu,
                                    SoDu = soDuMoi,
                                    ThoiGian = now,
                                    NguoiThucHienId = userHuyId,
                                    GhiChu = $"Hoàn tác thanh toán công nợ do hủy phiếu thu {phieu.MaPhieuChi}. Lý do: {lyDoHuy.Trim()}"
                                },
                                transaction);

                            await connection.ExecuteAsync(
                                """
                                UPDATE lich_su_thanh_toan_cong_no_khach_hang
                                SET trang_thai = 'HUY',
                                    thoi_gian_huy = @Now,
                                    nguoi_huy_id = @UserId,
                                    ly_do_huy = @LyDoHuy
                                WHERE id = @Id;
                                """,
                                new { Id = lstt.Id, Now = now, UserId = userHuyId, LyDoHuy = lyDoHuy.Trim() },
                                transaction);
                        }
                    }
                }
                else
                {
                    // ========================================================
                    // HỦY PHIẾU CHI & HOÀN TÁC CÔNG NỢ NHÀ CUNG CẤP (PHẦN III)
                    // ========================================================

                    string? maPhieuNhap = phieu.MaPhieuNhap;
                    uint? phieuNhapId = phieu.PhieuNhapHangId;

                    if (string.IsNullOrWhiteSpace(maPhieuNhap) && phieuNhapId is > 0)
                    {
                        maPhieuNhap = await connection.ExecuteScalarAsync<string>(
                            "SELECT ma_phieu_nhap FROM phieu_nhap_hang WHERE id = @Id LIMIT 1;",
                            new { Id = phieuNhapId },
                            transaction);
                    }

                    if (!string.IsNullOrWhiteSpace(maPhieuNhap))
                    {
                        var pnInfo = await connection.QueryFirstOrDefaultAsync<(uint Id, uint? NhaCungCapId)>(
                            """
                            SELECT id AS Id, nha_cung_cap_id AS NhaCungCapId
                            FROM phieu_nhap_hang
                            WHERE ma_phieu_nhap = @Ma
                            LIMIT 1
                            FOR UPDATE;
                            """,
                            new { Ma = maPhieuNhap },
                            transaction);

                        // Hoàn tác số tiền thanh toán của phiếu nhập
                        await connection.ExecuteAsync(
                            """
                            UPDATE phieu_nhap_hang
                            SET da_thanh_toan = GREATEST(0, da_thanh_toan - @SoTien),
                                con_no = LEAST(tong_tien_nhap, con_no + @SoTien),
                                ngay_cap_nhat = @Now
                            WHERE ma_phieu_nhap = @MaPhieuNhap;
                            """,
                            new { SoTien = soTienPhieu, Now = now, MaPhieuNhap = maPhieuNhap },
                            transaction);

                        // Cập nhật thanh_toan_nha_cung_cap
                        await connection.ExecuteAsync(
                            """
                            UPDATE thanh_toan_nha_cung_cap
                            SET trang_thai = 'HUY',
                                thoi_gian_huy = @Now,
                                nguoi_huy_id = @UserId,
                                ly_do_huy = @LyDoHuy
                            WHERE (ma_giao_dich = @MaPhieu
                                   OR ghi_chu LIKE CONCAT('%[Giao dịch ', @MaPhieu, ']%')
                                   OR ghi_chu LIKE CONCAT('%', @MaPhieu, '%')
                                   OR (phieu_nhap_id = @PnId AND so_tien = @SoTien))
                              AND trang_thai = 'HOAN_THANH';
                            """,
                            new
                            {
                                MaPhieu = phieu.MaPhieuChi,
                                PnId = pnInfo.Id,
                                SoTien = soTienPhieu,
                                Now = now,
                                UserId = userHuyId,
                                LyDoHuy = lyDoHuy.Trim()
                            },
                            transaction);

                        // Khôi phục công nợ nhà cung cấp (ghi có = tăng nợ phải trả lại)
                        if (pnInfo.NhaCungCapId is > 0)
                        {
                            var soDuCu = await connection.ExecuteScalarAsync<decimal>(
                                """
                                SELECT COALESCE(SUM(ghi_co - ghi_no), 0)
                                FROM lich_su_cong_no_nha_cung_cap
                                WHERE nha_cung_cap_id = @NccId
                                FOR UPDATE;
                                """,
                                new { NccId = pnInfo.NhaCungCapId.Value },
                                transaction);

                            var soDuMoi = soDuCu + soTienPhieu;

                            await connection.ExecuteAsync(
                                """
                                INSERT INTO lich_su_cong_no_nha_cung_cap
                                (nha_cung_cap_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                                 ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                                VALUES
                                (@NccId, @ChiNhanhId, 'HUY_THANH_TOAN', 'PHIEU_CHI', @MaChungTu,
                                 0, @GhiCo, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu);
                                """,
                                new
                                {
                                    NccId = pnInfo.NhaCungCapId.Value,
                                    ChiNhanhId = phieu.ChiNhanhId,
                                    MaChungTu = phieu.MaPhieuChi,
                                    GhiCo = soTienPhieu,
                                    SoDu = soDuMoi,
                                    ThoiGian = now,
                                    NguoiThucHienId = userHuyId,
                                    GhiChu = $"Hoàn tác thanh toán phiếu nhập {maPhieuNhap} do hủy phiếu chi {phieu.MaPhieuChi}. Lý do: {lyDoHuy.Trim()}"
                                },
                                transaction);
                        }

                        // Cập nhật lich_su_thanh_toan_cong_no_nha_cung_cap
                        await connection.ExecuteAsync(
                            """
                            UPDATE lich_su_thanh_toan_cong_no_nha_cung_cap
                            SET trang_thai = 'HUY',
                                thoi_gian_huy = @Now,
                                nguoi_huy_id = @UserId,
                                ly_do_huy = @LyDoHuy
                            WHERE ma_giao_dich = @MaPhieu;
                            """,
                            new { MaPhieu = phieu.MaPhieuChi, Now = now, UserId = userHuyId, LyDoHuy = lyDoHuy.Trim() },
                            transaction);
                    }
                    else
                    {
                        // Trường hợp thanh toán công nợ nhà cung cấp phân bổ nhiều phiếu nhập
                        var lstt = await connection.QueryFirstOrDefaultAsync<(long Id, uint NhaCungCapId)>(
                            """
                            SELECT id AS Id, nha_cung_cap_id AS NhaCungCapId
                            FROM lich_su_thanh_toan_cong_no_nha_cung_cap
                            WHERE ma_giao_dich = @MaPhieu
                            LIMIT 1
                            FOR UPDATE;
                            """,
                            new { MaPhieu = phieu.MaPhieuChi },
                            transaction);

                        // Lấy các phiếu nhập phân bổ
                        var allocations = (await connection.QueryAsync<(uint PhieuNhapId, decimal SoTien)>(
                            """
                            SELECT phieu_nhap_id AS PhieuNhapId, so_tien AS SoTien
                            FROM thanh_toan_nha_cung_cap
                            WHERE (ma_giao_dich = @MaPhieu
                                   OR ghi_chu LIKE CONCAT('%[Giao dịch ', @MaPhieu, ']%')
                                   OR ghi_chu LIKE CONCAT('%', @MaPhieu, '%'))
                              AND trang_thai = 'HOAN_THANH'
                            FOR UPDATE;
                            """,
                            new { MaPhieu = phieu.MaPhieuChi },
                            transaction)).AsList();

                        foreach (var alloc in allocations)
                        {
                            await connection.ExecuteAsync(
                                """
                                UPDATE phieu_nhap_hang
                                SET da_thanh_toan = GREATEST(0, da_thanh_toan - @SoTien),
                                    con_no = LEAST(tong_tien_nhap, con_no + @SoTien),
                                    ngay_cap_nhat = @Now
                                WHERE id = @PhieuNhapId;
                                """,
                                new { SoTien = alloc.SoTien, Now = now, PhieuNhapId = alloc.PhieuNhapId },
                                transaction);
                        }

                        // Cập nhật thanh_toan_nha_cung_cap thành HUY
                        await connection.ExecuteAsync(
                            """
                            UPDATE thanh_toan_nha_cung_cap
                            SET trang_thai = 'HUY',
                                thoi_gian_huy = @Now,
                                nguoi_huy_id = @UserId,
                                ly_do_huy = @LyDoHuy
                            WHERE (ma_giao_dich = @MaPhieu
                                   OR ghi_chu LIKE CONCAT('%[Giao dịch ', @MaPhieu, ']%')
                                   OR ghi_chu LIKE CONCAT('%', @MaPhieu, '%'))
                              AND trang_thai = 'HOAN_THANH';
                            """,
                            new { MaPhieu = phieu.MaPhieuChi, Now = now, UserId = userHuyId, LyDoHuy = lyDoHuy.Trim() },
                            transaction);

                        // Khôi phục công nợ nhà cung cấp
                        if (lstt.NhaCungCapId > 0)
                        {
                            var soDuCu = await connection.ExecuteScalarAsync<decimal>(
                                """
                                SELECT COALESCE(SUM(ghi_co - ghi_no), 0)
                                FROM lich_su_cong_no_nha_cung_cap
                                WHERE nha_cung_cap_id = @NccId
                                FOR UPDATE;
                                """,
                                new { NccId = lstt.NhaCungCapId },
                                transaction);

                            var soDuMoi = soDuCu + soTienPhieu;

                            await connection.ExecuteAsync(
                                """
                                INSERT INTO lich_su_cong_no_nha_cung_cap
                                (nha_cung_cap_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                                 ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                                VALUES
                                (@NccId, @ChiNhanhId, 'HUY_THANH_TOAN', 'PHIEU_CHI', @MaChungTu,
                                 0, @GhiCo, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu);
                                """,
                                new
                                {
                                    NccId = lstt.NhaCungCapId,
                                    ChiNhanhId = phieu.ChiNhanhId,
                                    MaChungTu = phieu.MaPhieuChi,
                                    GhiCo = soTienPhieu,
                                    SoDu = soDuMoi,
                                    ThoiGian = now,
                                    NguoiThucHienId = userHuyId,
                                    GhiChu = $"Hoàn tác thanh toán công nợ NCC do hủy phiếu chi {phieu.MaPhieuChi}. Lý do: {lyDoHuy.Trim()}"
                                },
                                transaction);

                            await connection.ExecuteAsync(
                                """
                                UPDATE lich_su_thanh_toan_cong_no_nha_cung_cap
                                SET trang_thai = 'HUY',
                                    thoi_gian_huy = @Now,
                                    nguoi_huy_id = @UserId,
                                    ly_do_huy = @LyDoHuy
                                WHERE id = @Id;
                                """,
                                new { Id = lstt.Id, Now = now, UserId = userHuyId, LyDoHuy = lyDoHuy.Trim() },
                                transaction);
                        }
                    }
                }

                // Cập nhật trạng thái phiếu quỹ thành HUY
                var affected = await connection.ExecuteAsync(
                    """
                    UPDATE phieu_chi
                    SET trang_thai = 'HUY',
                        thoi_gian_huy = @Now,
                        nguoi_huy_id = @UserId,
                        ly_do_huy = @LyDoHuy
                    WHERE ma_phieu_chi = @MaPhieu
                      AND trang_thai = 'HOAN_THANH';
                    """,
                    new
                    {
                        MaPhieu = phieu.MaPhieuChi,
                        Now = now,
                        UserId = userHuyId,
                        LyDoHuy = lyDoHuy.Trim()
                    },
                    transaction);

                await transaction.CommitAsync();

                _ = LogSubAccountActivityAsync(
                    "Hủy sổ quỹ",
                    "SO_QUY",
                    phieu.Id,
                    phieu.MaPhieuChi,
                    $"Hủy phiếu {phieu.MaPhieuChi} ({soTienPhieu:N0} đ). Lý do: {lyDoHuy.Trim()}",
                    soTienPhieu);

                return affected;
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { }
                SafeLog.WriteError($"Lỗi HuyPhieuQuyChiTietAsync cho {maPhieu}", ex);
                throw;
            }
        }
        finally
        {
            _khoaHuyPhieuQuy.Release();
        }
    }

    /// <summary>
    /// Lấy danh sách phân bổ thanh toán công nợ của khách hàng theo mã giao dịch (mã phiếu thu).
    /// </summary>
    public async Task<IReadOnlyList<PhanBoThanhToanKhachHang>> GetPhanBoThanhToanKhachHangAsync(string maGiaoDich)
    {
        if (string.IsNullOrWhiteSpace(maGiaoDich))
            return Array.Empty<PhanBoThanhToanKhachHang>();

        const string sql = """
        SELECT
            hd.ma_hoa_don AS MaHoaDon,
            tt.so_tien AS SoTien
        FROM thanh_toan tt
        INNER JOIN hoa_don_ban_hang hd ON hd.id = tt.hoa_don_id
        WHERE (tt.ma_giao_dich = @MaGiaoDich OR tt.ghi_chu LIKE CONCAT('%[Phiếu thu ', @MaGiaoDich, ']%'))
        ORDER BY hd.thoi_gian_ban, hd.id;
        """;

        await using var connection = await OpenAsync();
        return (await connection.QueryAsync<PhanBoThanhToanKhachHang>(
            sql,
            new { MaGiaoDich = maGiaoDich.Trim() })).AsList();
    }
}
