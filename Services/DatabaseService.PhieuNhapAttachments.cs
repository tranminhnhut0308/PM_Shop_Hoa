using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public partial class DatabaseService
{
    private sealed class AttachmentSaveCleanup
    {
        public List<string> FilesToDeleteOnRollback { get; } = [];
        public List<string> TempFilesToDeleteOnCommit { get; } = [];
    }

    private static readonly SemaphoreSlim _khoaDamBaoDinhKemPhieuNhap = new(1, 1);
    private static bool _daDamBaoDinhKemPhieuNhap;
    private const string InsertDinhKemPhieuNhapSql = """
    INSERT INTO dinh_kem_phieu_nhap
    (
        phieu_nhap_id,
        ma_phieu_nhap,
        ten_file_goc,
        ten_file_luu,
        duong_dan_tuong_doi,
        dinh_dang,
        dung_luong,
        loai_chung_tu,
        ghi_chu,
        nguoi_tai_len_id,
        ten_nguoi_tai_len,
        thoi_gian_tai_len,
        da_xoa
    )
    VALUES
    (
        @PhieuNhapId,
        @MaPhieuNhap,
        @TenFileGoc,
        @TenFileLuu,
        @DuongDanTuongDoi,
        @DinhDang,
        @DungLuong,
        @LoaiChungTu,
        @GhiChu,
        @NguoiTaiLenId,
        @TenNguoiTaiLen,
        NOW(),
        0
    );
    """;

    public async Task<bool> EnsureDinhKemPhieuNhapTableAsync()
    {
        if (_daDamBaoDinhKemPhieuNhap)
            return true;

        await _khoaDamBaoDinhKemPhieuNhap.WaitAsync();
        try
        {
            if (_daDamBaoDinhKemPhieuNhap)
                return true;

            await using var connection = await OpenAsync();

            await CreateDinhKemPhieuNhapTableAsync(connection);
            await EnsureAttachmentColumnsAsync(connection);
            _daDamBaoDinhKemPhieuNhap = true;
            return true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("EnsureDinhKemPhieuNhapTableAsync error", ex);
            throw new InvalidOperationException("Không thể cập nhật cấu trúc bảng chứng từ phiếu nhập.", ex);
        }
        finally
        {
            _khoaDamBaoDinhKemPhieuNhap.Release();
        }
    }

    private static async Task CreateDinhKemPhieuNhapTableAsync(MySqlConnection connection)
    {
        const string createSql = """
            CREATE TABLE IF NOT EXISTS `dinh_kem_phieu_nhap` (
              `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
              `phieu_nhap_id` INT UNSIGNED NOT NULL,
              `ma_phieu_nhap` VARCHAR(50) COLLATE utf8mb4_unicode_ci NOT NULL,
              `ten_file_goc` VARCHAR(255) COLLATE utf8mb4_unicode_ci NOT NULL,
              `ten_file_luu` VARCHAR(255) COLLATE utf8mb4_unicode_ci NOT NULL,
              `duong_dan_tuong_doi` VARCHAR(500) COLLATE utf8mb4_unicode_ci NOT NULL,
              `dinh_dang` VARCHAR(20) COLLATE utf8mb4_unicode_ci NOT NULL,
              `dung_luong` BIGINT UNSIGNED NOT NULL DEFAULT 0,
              `loai_chung_tu` VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOA_DON_GIAY',
              `ghi_chu` TEXT COLLATE utf8mb4_unicode_ci NULL,
              `nguoi_tai_len_id` INT UNSIGNED NULL DEFAULT NULL,
              `ten_nguoi_tai_len` VARCHAR(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
              `thoi_gian_tai_len` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
              `da_xoa` TINYINT(1) NOT NULL DEFAULT 0,
              `thoi_gian_xoa` DATETIME NULL DEFAULT NULL,
              `nguoi_xoa_id` INT UNSIGNED NULL DEFAULT NULL,
              `ten_nguoi_xoa` VARCHAR(150) COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
              `ly_do_xoa` TEXT COLLATE utf8mb4_unicode_ci NULL,
              PRIMARY KEY (`id`),
              KEY `idx_dkpn_phieu_nhap` (`phieu_nhap_id`),
              KEY `idx_dkpn_ma_phieu` (`ma_phieu_nhap`),
              KEY `idx_dkpn_thoi_gian` (`thoi_gian_tai_len`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;
        await connection.ExecuteAsync(createSql);
    }

    private static async Task EnsureAttachmentColumnsAsync(MySqlConnection connection)
    {
        await EnsureAttachmentColumnAsync(connection, "ten_file_goc", "en_file_goc",
            "VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''", copyEmptyValue: true);
        await EnsureAttachmentColumnAsync(connection, "ten_file_luu", "en_file_luu",
            "VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''", copyEmptyValue: true);
        await EnsureAttachmentColumnAsync(connection, "nguoi_tai_len_id", "guoi_tai_len_id",
            "INT UNSIGNED NULL DEFAULT NULL", copyEmptyValue: false);
        await EnsureAttachmentColumnAsync(connection, "ten_nguoi_tai_len", "en_nguoi_tai_len",
            "VARCHAR(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''", copyEmptyValue: true);
        await EnsureAttachmentColumnAsync(connection, "thoi_gian_tai_len", "hoi_gian_tai_len",
            "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP", copyEmptyValue: false);
        await EnsureAttachmentColumnAsync(connection, "thoi_gian_xoa", "hoi_gian_xoa",
            "DATETIME NULL DEFAULT NULL", copyEmptyValue: false);
        await EnsureAttachmentColumnAsync(connection, "nguoi_xoa_id", "guoi_xoa_id",
            "INT UNSIGNED NULL DEFAULT NULL", copyEmptyValue: false);
        await EnsureAttachmentColumnAsync(connection, "ten_nguoi_xoa", "en_nguoi_xoa",
            "VARCHAR(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL", copyEmptyValue: true);
    }

    private static async Task EnsureAttachmentColumnAsync(
        MySqlConnection connection,
        string columnName,
        string? legacyColumnName,
        string definition,
        bool copyEmptyValue)
    {
        static async Task<bool> ColumnExistsAsync(MySqlConnection conn, string name)
        {
            var count = await conn.ExecuteScalarAsync<long>(
                """
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'dinh_kem_phieu_nhap'
                  AND COLUMN_NAME = @Name;
                """,
                new { Name = name });
            return count > 0;
        }

        var targetExists = await ColumnExistsAsync(connection, columnName);
        var legacyExists = !string.IsNullOrWhiteSpace(legacyColumnName)
            && await ColumnExistsAsync(connection, legacyColumnName);

        if (!targetExists)
        {
            var alterSql = legacyExists
                ? $"ALTER TABLE `dinh_kem_phieu_nhap` CHANGE COLUMN `{legacyColumnName}` `{columnName}` {definition};"
                : $"ALTER TABLE `dinh_kem_phieu_nhap` ADD COLUMN `{columnName}` {definition};";
            await connection.ExecuteAsync(alterSql);
            return;
        }

        if (!legacyExists)
            return;

        var emptyCondition = copyEmptyValue
            ? $"(`{columnName}` IS NULL OR `{columnName}` = '')"
            : $"`{columnName}` IS NULL";
        await connection.ExecuteAsync(
            $"UPDATE `dinh_kem_phieu_nhap` SET `{columnName}` = `{legacyColumnName}` " +
            $"WHERE `{legacyColumnName}` IS NOT NULL AND {emptyCondition};");
    }

    /// <summary>
    /// Lấy danh sách chứng từ đính kèm còn hiệu lực (chưa xóa) của một phiếu nhập.
    /// Kèm đường dẫn tuyệt đối để mở/xem trước.
    /// </summary>
    public async Task<IReadOnlyList<DinhKemPhieuNhap>> GetDinhKemPhieuNhapAsync(uint phieuNhapId)
    {
        await EnsureDinhKemPhieuNhapTableAsync();

        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            id                 AS Id,
            phieu_nhap_id      AS PhieuNhapId,
            ma_phieu_nhap      AS MaPhieuNhap,
            ten_file_goc       AS TenFileGoc,
            ten_file_luu       AS TenFileLuu,
            duong_dan_tuong_doi AS DuongDanTuongDoi,
            dinh_dang          AS DinhDang,
            dung_luong         AS DungLuong,
            loai_chung_tu      AS LoaiChungTu,
            ghi_chu            AS GhiChu,
            nguoi_tai_len_id   AS NguoiTaiLenId,
            ten_nguoi_tai_len  AS TenNguoiTaiLen,
            thoi_gian_tai_len  AS ThoiGianTaiLen,
            da_xoa             AS DaXoa,
            thoi_gian_xoa      AS ThoiGianXoa,
            nguoi_xoa_id       AS NguoiXoaId,
            ten_nguoi_xoa      AS TenNguoiXoa,
            ly_do_xoa          AS LyDoXoa
        FROM dinh_kem_phieu_nhap
        WHERE phieu_nhap_id = @PhieuNhapId
          AND da_xoa = 0
        ORDER BY thoi_gian_tai_len ASC, id ASC;
        """;

        var list = (await connection.QueryAsync<DinhKemPhieuNhap>(sql, new { PhieuNhapId = phieuNhapId })).AsList();

        foreach (var item in list)
        {
            item.DuongDanTuyetDoi = ChungTuStorageService.LayDuongDanTuyetDoi(item.DuongDanTuongDoi);
        }

        return list;
    }

    /// <summary>
    /// Lưu các chứng từ đính kèm mới vào CSDL trong transaction của phiếu nhập.
    /// </summary>
    private async Task LuuDinhKemPhieuNhapAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint phieuNhapId,
        string maPhieuNhap,
        IEnumerable<ChungTuItemModel> chungTus,
        uint? userId,
        string userName,
        AttachmentSaveCleanup? cleanup = null)
    {
        await EnsureDinhKemPhieuNhapTableAsync();

        foreach (var ct in chungTus)
            await LuuMotDinhKemPhieuNhapAsync(
                connection, transaction, phieuNhapId, maPhieuNhap, ct, userId, userName, cleanup);
    }

    private static async Task LuuMotDinhKemPhieuNhapAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint phieuNhapId,
        string maPhieuNhap,
        ChungTuItemModel ct,
        uint? userId,
        string userName,
        AttachmentSaveCleanup? cleanup)
    {
        if (ct.IsExisting)
            return;

        var sourceFile = !string.IsNullOrWhiteSpace(ct.DuongDanTam) && File.Exists(ct.DuongDanTam)
            ? ct.DuongDanTam
            : ct.DuongDanNguon;

        if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
            throw new FileNotFoundException(
                $"Không tìm thấy tệp chứng từ '{ct.TenFileGoc}'. Phiếu nhập chưa được lưu.",
                sourceFile);

        var (relativePath, savedFileName, size) = await ChungTuStorageService.ChuyenFileChinhThucAsync(
            sourceFile,
            maPhieuNhap,
            ct.TenFileGoc);

        cleanup?.FilesToDeleteOnRollback.Add(relativePath);
        if (!string.IsNullOrWhiteSpace(ct.DuongDanTam))
            cleanup?.TempFilesToDeleteOnCommit.Add(ct.DuongDanTam);

        await connection.ExecuteAsync(
            InsertDinhKemPhieuNhapSql,
            new
            {
                PhieuNhapId = phieuNhapId,
                MaPhieuNhap = maPhieuNhap,
                ct.TenFileGoc,
                TenFileLuu = savedFileName,
                DuongDanTuongDoi = relativePath,
                ct.DinhDang,
                DungLuong = size,
                ct.LoaiChungTu,
                ct.GhiChu,
                NguoiTaiLenId = userId,
                TenNguoiTaiLen = !string.IsNullOrWhiteSpace(ct.NguoiTaiLen) ? ct.NguoiTaiLen : userName
            },
            transaction);

        if (cleanup is null)
            ChungTuStorageService.XoaFileTamNeuCo(ct.DuongDanTam);
    }

    /// <summary>
    /// Đánh dấu xóa chứng từ và ghi nhận audit trail.
    /// Không xóa cứng file vật lý để bảo toàn lịch sử truy vết.
    /// </summary>
    public async Task XoaDinhKemPhieuNhapAsync(
        ulong dinhKemId,
        string lyDo,
        uint? userId,
        string userName)
    {
        await EnsureDinhKemPhieuNhapTableAsync();
        await EnsureLichSuChinhSuaPhieuNhapTableAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var item = await connection.QueryFirstOrDefaultAsync<DinhKemPhieuNhap>(
                """
                SELECT * FROM dinh_kem_phieu_nhap WHERE id = @Id AND da_xoa = 0 FOR UPDATE;
                """,
                new { Id = dinhKemId },
                transaction);

            if (item is null)
                return;

            const string updateSql = """
            UPDATE dinh_kem_phieu_nhap
            SET da_xoa = 1,
                thoi_gian_xoa = NOW(),
                nguoi_xoa_id = @NguoiXoaId,
                ten_nguoi_xoa = @TenNguoiXoa,
                ly_do_xoa = @LyDoXoa
            WHERE id = @Id;
            """;

            await connection.ExecuteAsync(
                updateSql,
                new
                {
                    Id = dinhKemId,
                    NguoiXoaId = userId,
                    TenNguoiXoa = userName,
                    LyDoXoa = lyDo.Trim()
                },
                transaction);

            // Ghi nhận vào lịch sử thay đổi phiếu nhập
            const string auditSql = """
            INSERT INTO lich_su_chinh_sua_phieu_nhap
            (
                phieu_nhap_id,
                ma_phieu_nhap,
                thoi_gian,
                nguoi_thuc_hien_id,
                ten_nguoi_thuc_hien,
                ly_do,
                chi_tiet_thay_doi
            )
            VALUES
            (
                @PhieuNhapId,
                @MaPhieuNhap,
                NOW(),
                @NguoiThucHienId,
                @TenNguoiThucHien,
                @LyDo,
                @ChiTietThayDoi
            );
            """;

            await connection.ExecuteAsync(
                auditSql,
                new
                {
                    item.PhieuNhapId,
                    item.MaPhieuNhap,
                    NguoiThucHienId = userId,
                    TenNguoiThucHien = userName,
                    LyDo = string.IsNullOrWhiteSpace(lyDo) ? "Xóa chứng từ đính kèm" : lyDo.Trim(),
                    ChiTietThayDoi = $"• Đã xóa chứng từ: {item.TenFileGoc} ({item.DungLuongHienThi}). Lý do: {lyDo.Trim()}"
                },
                transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
