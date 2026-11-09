using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Models;
using ShopHoa.Helpers;

namespace ShopHoa.Services;

public partial class DatabaseService
{
    private static bool? _isTraTruocSchemaEnsured;

    /// <summary>
    /// Đảm bảo bảng và cột lưu số dư trả trước (ISSUE #97) tồn tại trong database.
    /// Thêm cột so_du_tra_truoc vào khach_hang, nha_cung_cap và tạo bảng lịch sử biến động trả trước.
    /// </summary>
    public static async Task<bool> EnsureTraTruocSchemaAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction = null)
    {
        if (_isTraTruocSchemaEnsured == true)
            return true;

        try
        {
            // 1. Cột so_du_tra_truoc trong bảng khach_hang
            var hasColKh = await connection.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'khach_hang'
                  AND COLUMN_NAME = 'so_du_tra_truoc';
                """,
                null,
                transaction) > 0;

            if (!hasColKh)
            {
                await connection.ExecuteAsync(
                    """
                    ALTER TABLE khach_hang
                    ADD COLUMN so_du_tra_truoc DECIMAL(15,2) NOT NULL DEFAULT 0.00
                    AFTER nhom_khach_hang_id;
                    """,
                    null,
                    transaction);
            }

            // 2. Cột so_du_tra_truoc trong bảng nha_cung_cap
            var hasColNcc = await connection.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'nha_cung_cap'
                  AND COLUMN_NAME = 'so_du_tra_truoc';
                """,
                null,
                transaction) > 0;

            if (!hasColNcc)
            {
                await connection.ExecuteAsync(
                    """
                    ALTER TABLE nha_cung_cap
                    ADD COLUMN so_du_tra_truoc DECIMAL(15,2) NOT NULL DEFAULT 0.00
                    AFTER trang_thai;
                    """,
                    null,
                    transaction);
            }

            // 3. Bảng lich_su_tra_truoc_khach_hang
            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS lich_su_tra_truoc_khach_hang (
                    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    khach_hang_id INT UNSIGNED NOT NULL,
                    ma_khach_hang VARCHAR(50) NOT NULL,
                    ma_giao_dich VARCHAR(50) NOT NULL,
                    loai_giao_dich VARCHAR(50) NOT NULL,
                    loai_chung_tu VARCHAR(50) NULL,
                    ma_chung_tu VARCHAR(50) NULL,
                    so_tien DECIMAL(15,2) NOT NULL,
                    so_du_truoc DECIMAL(15,2) NOT NULL,
                    so_du_sau DECIMAL(15,2) NOT NULL,
                    phuong_thuc VARCHAR(50) NULL,
                    thoi_gian DATETIME NOT NULL,
                    nguoi_thuc_hien VARCHAR(100) NULL,
                    ghi_chu TEXT NULL,
                    INDEX idx_lstt_kh_id (khach_hang_id),
                    INDEX idx_lstt_kh_time (thoi_gian),
                    INDEX idx_lstt_kh_mgd (ma_giao_dich),
                    INDEX idx_lstt_kh_mct (ma_chung_tu)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                """,
                null,
                transaction);

            // 4. Bảng lich_su_tra_truoc_ncc
            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS lich_su_tra_truoc_ncc (
                    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    nha_cung_cap_id INT UNSIGNED NOT NULL,
                    ma_nha_cung_cap VARCHAR(50) NOT NULL,
                    ma_giao_dich VARCHAR(50) NOT NULL,
                    loai_giao_dich VARCHAR(50) NOT NULL,
                    loai_chung_tu VARCHAR(50) NULL,
                    ma_chung_tu VARCHAR(50) NULL,
                    so_tien DECIMAL(15,2) NOT NULL,
                    so_du_truoc DECIMAL(15,2) NOT NULL,
                    so_du_sau DECIMAL(15,2) NOT NULL,
                    phuong_thuc VARCHAR(50) NULL,
                    thoi_gian DATETIME NOT NULL,
                    nguoi_thuc_hien VARCHAR(100) NULL,
                    ghi_chu TEXT NULL,
                    INDEX idx_lstt_ncc_id (nha_cung_cap_id),
                    INDEX idx_lstt_ncc_time (thoi_gian),
                    INDEX idx_lstt_ncc_mgd (ma_giao_dich),
                    INDEX idx_lstt_ncc_mct (ma_chung_tu)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                """,
                null,
                transaction);

            _isTraTruocSchemaEnsured = true;
            return true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Không thể khởi tạo schema quản lý số dư trả trước", ex);
            return false;
        }
    }

    public async Task<bool> EnsureTraTruocSchemaAsync()
    {
        if (_isTraTruocSchemaEnsured == true)
            return true;

        await using var conn = await OpenAsync();
        return await EnsureTraTruocSchemaAsync(conn);
    }

    /// <summary>
    /// Đọc số dư trả trước hiện tại của khách hàng kèm khoá FOR UPDATE trong transaction.
    /// </summary>
    public static async Task<decimal> LaySoDuTraTruocKhachHangTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint khachHangId)
    {
        return await connection.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(so_du_tra_truoc, 0)
            FROM khach_hang
            WHERE id = @Id
            FOR UPDATE;
            """,
            new { Id = khachHangId },
            transaction);
    }

    /// <summary>
    /// Đọc số dư trả trước hiện tại của nhà cung cấp kèm khoá FOR UPDATE trong transaction.
    /// </summary>
    public static async Task<decimal> LaySoDuTraTruocNhaCungCapTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint nhaCungCapId)
    {
        return await connection.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(so_du_tra_truoc, 0)
            FROM nha_cung_cap
            WHERE id = @Id
            FOR UPDATE;
            """,
            new { Id = nhaCungCapId },
            transaction);
    }

    /// <summary>
    /// Ghi nhận biến động số dư trả trước của khách hàng (thanh toán dư, cấn trừ, hoàn trả) trong transaction.
    /// </summary>
    public static async Task GhiLichSuTraTruocKhachHangTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint khachHangId,
        string maKhachHang,
        string maGiaoDich,
        string loaiGiaoDich,
        string? loaiChungTu,
        string? maChungTu,
        decimal soTien,
        decimal soDuTruoc,
        decimal soDuSau,
        string? phuongThuc,
        DateTime thoiGian,
        string? nguoiThucHien,
        string? ghiChu)
    {
        // 1. Cập nhật số dư trong bảng khach_hang
        await connection.ExecuteAsync(
            """
            UPDATE khach_hang
            SET so_du_tra_truoc = @SoDuSau,
                ngay_cap_nhat = @ThoiGian
            WHERE id = @Id;
            """,
            new
            {
                Id = khachHangId,
                SoDuSau = soDuSau,
                ThoiGian = thoiGian
            },
            transaction);

        // 2. Ghi nhận lịch sử chi tiết
        await connection.ExecuteAsync(
            """
            INSERT INTO lich_su_tra_truoc_khach_hang
            (
                khach_hang_id, ma_khach_hang, ma_giao_dich, loai_giao_dich,
                loai_chung_tu, ma_chung_tu, so_tien, so_du_truoc, so_du_sau,
                phuong_thuc, thoi_gian, nguoi_thuc_hien, ghi_chu
            )
            VALUES
            (
                @KhachHangId, @MaKhachHang, @MaGiaoDich, @LoaiGiaoDich,
                @LoaiChungTu, @MaChungTu, @SoTien, @SoDuTruoc, @SoDuSau,
                @PhuongThuc, @ThoiGian, @NguoiThucHien, @GhiChu
            );
            """,
            new
            {
                KhachHangId = khachHangId,
                MaKhachHang = maKhachHang,
                MaGiaoDich = maGiaoDich,
                LoaiGiaoDich = loaiGiaoDich,
                LoaiChungTu = BlankToNull(loaiChungTu),
                MaChungTu = BlankToNull(maChungTu),
                SoTien = soTien,
                SoDuTruoc = soDuTruoc,
                SoDuSau = soDuSau,
                PhuongThuc = BlankToNull(phuongThuc),
                ThoiGian = thoiGian,
                NguoiThucHien = BlankToNull(nguoiThucHien),
                GhiChu = BlankToNull(ghiChu)
            },
            transaction);
    }

    /// <summary>
    /// Ghi nhận biến động số dư trả trước của nhà cung cấp (thanh toán dư, cấn trừ, hoàn trả) trong transaction.
    /// </summary>
    public static async Task GhiLichSuTraTruocNhaCungCapTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint nhaCungCapId,
        string maNhaCungCap,
        string maGiaoDich,
        string loaiGiaoDich,
        string? loaiChungTu,
        string? maChungTu,
        decimal soTien,
        decimal soDuTruoc,
        decimal soDuSau,
        string? phuongThuc,
        DateTime thoiGian,
        string? nguoiThucHien,
        string? ghiChu)
    {
        // 1. Cập nhật số dư trong bảng nha_cung_cap
        await connection.ExecuteAsync(
            """
            UPDATE nha_cung_cap
            SET so_du_tra_truoc = @SoDuSau
            WHERE id = @Id;
            """,
            new
            {
                Id = nhaCungCapId,
                SoDuSau = soDuSau
            },
            transaction);

        // 2. Ghi nhận lịch sử chi tiết
        await connection.ExecuteAsync(
            """
            INSERT INTO lich_su_tra_truoc_ncc
            (
                nha_cung_cap_id, ma_nha_cung_cap, ma_giao_dich, loai_giao_dich,
                loai_chung_tu, ma_chung_tu, so_tien, so_du_truoc, so_du_sau,
                phuong_thuc, thoi_gian, nguoi_thuc_hien, ghi_chu
            )
            VALUES
            (
                @NhaCungCapId, @MaNhaCungCap, @MaGiaoDich, @LoaiGiaoDich,
                @LoaiChungTu, @MaChungTu, @SoTien, @SoDuTruoc, @SoDuSau,
                @PhuongThuc, @ThoiGian, @NguoiThucHien, @GhiChu
            );
            """,
            new
            {
                NhaCungCapId = nhaCungCapId,
                MaNhaCungCap = maNhaCungCap,
                MaGiaoDich = maGiaoDich,
                LoaiGiaoDich = loaiGiaoDich,
                LoaiChungTu = BlankToNull(loaiChungTu),
                MaChungTu = BlankToNull(maChungTu),
                SoTien = soTien,
                SoDuTruoc = soDuTruoc,
                SoDuSau = soDuSau,
                PhuongThuc = BlankToNull(phuongThuc),
                ThoiGian = thoiGian,
                NguoiThucHien = BlankToNull(nguoiThucHien),
                GhiChu = BlankToNull(ghiChu)
            },
            transaction);
    }

    /// <summary>
    /// Lấy danh sách lịch sử sử dụng tiền trả trước của khách hàng.
    /// </summary>
    public async Task<IReadOnlyList<LichSuTraTruocItem>> GetLichSuTraTruocKhachHangAsync(uint khachHangId)
    {
        await EnsureTraTruocSchemaAsync();
        await using var connection = await OpenAsync();

        var sql = """
            SELECT
                id             AS Id,
                ma_giao_dich   AS MaGiaoDich,
                loai_giao_dich AS LoaiGiaoDich,
                loai_chung_tu  AS LoaiChungTu,
                ma_chung_tu    AS MaChungTu,
                so_tien        AS SoTien,
                so_du_truoc    AS SoDuTruoc,
                so_du_sau      AS SoDuSau,
                phuong_thuc    AS PhuongThuc,
                thoi_gian      AS ThoiGian,
                nguoi_thuc_hien AS NguoiThucHien,
                ghi_chu        AS GhiChu
            FROM lich_su_tra_truoc_khach_hang
            WHERE khach_hang_id = @KhachHangId
            ORDER BY thoi_gian DESC, id DESC;
            """;

        return (await connection.QueryAsync<LichSuTraTruocItem>(sql, new { KhachHangId = khachHangId })).AsList();
    }

    /// <summary>
    /// Lấy danh sách lịch sử sử dụng tiền trả trước của nhà cung cấp.
    /// </summary>
    public async Task<IReadOnlyList<LichSuTraTruocItem>> GetLichSuTraTruocNhaCungCapAsync(uint nhaCungCapId)
    {
        await EnsureTraTruocSchemaAsync();
        await using var connection = await OpenAsync();

        var sql = """
            SELECT
                id             AS Id,
                ma_giao_dich   AS MaGiaoDich,
                loai_giao_dich AS LoaiGiaoDich,
                loai_chung_tu  AS LoaiChungTu,
                ma_chung_tu    AS MaChungTu,
                so_tien        AS SoTien,
                so_du_truoc    AS SoDuTruoc,
                so_du_sau      AS SoDuSau,
                phuong_thuc    AS PhuongThuc,
                thoi_gian      AS ThoiGian,
                nguoi_thuc_hien AS NguoiThucHien,
                ghi_chu        AS GhiChu
            FROM lich_su_tra_truoc_ncc
            WHERE nha_cung_cap_id = @NhaCungCapId
            ORDER BY thoi_gian DESC, id DESC;
            """;

        return (await connection.QueryAsync<LichSuTraTruocItem>(sql, new { NhaCungCapId = nhaCungCapId })).AsList();
    }

    /// <summary>
    /// Sinh mã giao dịch trả trước tự động duy nhất (ví dụ: CTKH-261109-001).
    /// </summary>
    public static async Task<string> SinhMaGiaoDichTraTruocTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string prefix,
        string tableName)
    {
        var datePrefix = DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture);
        var basePattern = $"{prefix}-{datePrefix}-";

        var maxNumber = await connection.ExecuteScalarAsync<int?>(
            $"""
            SELECT MAX(CAST(SUBSTRING(ma_giao_dich, {basePattern.Length + 1}) AS UNSIGNED))
            FROM {tableName}
            WHERE ma_giao_dich LIKE CONCAT(@Pattern, '%');
            """,
            new { Pattern = basePattern },
            transaction) ?? 0;

        return $"{basePattern}{(maxNumber + 1):D3}";
    }
}
