using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    public const string TrangThaiHoaDonHoanThanh = "HOAN_THANH";
    public const string TrangThaiHoaDonDaHuy = "DA_HUY";

    private bool _daDamBaoBangLichSuChinhSuaHoaDon;
    private readonly SemaphoreSlim _khoaDamBaoBangLichSuChinhSuaHoaDon = new(1, 1);

    private bool _daDamBaoSchemaHuyHoaDon;
    private readonly SemaphoreSlim _khoaDamBaoSchemaHuyHoaDon = new(1, 1);
    private readonly SemaphoreSlim _khoaXuLyHuyHoaDon = new(1, 1);

    public static bool CoQuyenHuyHoaDonHienTai()
        => Quyen.HasAnyPermission(
            Session.CurrentUser,
            PermissionKeys.InvoicesCancel,
            PermissionKeys.SalesCancel);

    private static void KiemTraQuyenHuyHoaDon()
    {
        if (!CoQuyenHuyHoaDonHienTai())
        {
            throw new UnauthorizedAccessException("Bạn không có quyền hủy hóa đơn bán hàng.");
        }
    }

    public static bool LaTrangThaiHoaDonDaHuy(string? trangThai)
        => string.Equals(trangThai, TrangThaiHoaDonDaHuy, StringComparison.OrdinalIgnoreCase)
           || string.Equals(trangThai, "HUY", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> EnsureHuyHoaDonSchemaAsync()
    {
        if (_daDamBaoSchemaHuyHoaDon)
            return true;

        await _khoaDamBaoSchemaHuyHoaDon.WaitAsync();
        try
        {
            if (_daDamBaoSchemaHuyHoaDon)
                return true;

            await using var connection = await OpenAsync();

            var existingCols = (await connection.QueryAsync<string>(
                """
                SELECT LOWER(COLUMN_NAME)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'hoa_don_ban_hang';
                """)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var alterClauses = new List<string>();

            void AddColIfMissing(string colName, string definition)
            {
                if (!existingCols.Contains(colName))
                    alterClauses.Add($"ADD COLUMN `{colName}` {definition}");
            }

            AddColIfMissing("ly_do_huy", "TEXT NULL");
            AddColIfMissing("nguoi_huy_id", "INT UNSIGNED NULL");
            AddColIfMissing("ten_nguoi_huy", "VARCHAR(150) NULL");
            AddColIfMissing("thoi_gian_huy", "DATETIME NULL");
            AddColIfMissing("trang_thai_giao_hang_khi_huy", "VARCHAR(50) NULL");
            AddColIfMissing("da_hoan_kho", "TINYINT(1) NOT NULL DEFAULT 0");
            AddColIfMissing("phuong_an_tien_khi_huy", "VARCHAR(50) NULL");
            AddColIfMissing("so_tien_hoan", "DECIMAL(15, 2) NOT NULL DEFAULT 0.00");
            AddColIfMissing("ma_phieu_chi_hoan", "VARCHAR(50) NULL");
            AddColIfMissing("hoa_don_goc_id", "INT UNSIGNED NULL");
            AddColIfMissing("ma_hoa_don_goc", "VARCHAR(50) NULL");
            AddColIfMissing("hoa_don_thay_the_id", "INT UNSIGNED NULL");
            AddColIfMissing("ma_hoa_don_thay_the", "VARCHAR(50) NULL");

            if (alterClauses.Count > 0)
            {
                var alterSql = $"ALTER TABLE `hoa_don_ban_hang` {string.Join(", ", alterClauses)};";
                await connection.ExecuteAsync(alterSql);
            }

            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS `lich_su_huy_hoa_don` (
                    `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                    `hoa_don_id` INT UNSIGNED NOT NULL,
                    `ma_hoa_don` VARCHAR(50) NOT NULL,
                    `khach_hang_id` INT UNSIGNED NULL,
                    `ten_khach_hang` VARCHAR(255) NULL,
                    `tong_tien_hoa_don` DECIMAL(15, 2) NOT NULL DEFAULT 0.00,
                    `so_tien_da_thanh_toan` DECIMAL(15, 2) NOT NULL DEFAULT 0.00,
                    `cong_no_khi_huy` DECIMAL(15, 2) NOT NULL DEFAULT 0.00,
                    `trang_thai_giao_hang` VARCHAR(50) NOT NULL DEFAULT 'CHUA_GIAO',
                    `da_hoan_kho` TINYINT(1) NOT NULL DEFAULT 0,
                    `phuong_an_tien` VARCHAR(50) NOT NULL DEFAULT 'KHONG_CO',
                    `so_tien_hoan` DECIMAL(15, 2) NOT NULL DEFAULT 0.00,
                    `phuong_thuc_hoan` VARCHAR(50) NULL,
                    `ma_phieu_chi_hoan` VARCHAR(50) NULL,
                    `che_do_huy` VARCHAR(50) NOT NULL DEFAULT 'CHI_HUY',
                    `hoa_don_thay_the_id` INT UNSIGNED NULL,
                    `ma_hoa_don_thay_the` VARCHAR(50) NULL,
                    `ly_do_huy` TEXT NOT NULL,
                    `nguoi_huy_id` INT UNSIGNED NULL,
                    `ten_nguoi_huy` VARCHAR(150) NULL,
                    `thoi_gian_huy` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `du_lieu_snapshot` LONGTEXT NULL,
                    INDEX `idx_lshhd_hoa_don_id` (`hoa_don_id`),
                    INDEX `idx_lshhd_ma_hoa_don` (`ma_hoa_don`),
                    INDEX `idx_lshhd_thoi_gian_huy` (`thoi_gian_huy`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                """);

            // Đảm bảo nhóm chi phí hoàn tiền khách hàng có sẵn trong nhom_chi_phi
            await connection.ExecuteAsync(
                """
                INSERT INTO nhom_chi_phi (ma_nhom, ten_nhom, ghi_chu, trang_thai)
                SELECT 'HOAN_TIEN_KHACH', 'Hoàn tiền khách hàng', 'Hoàn tiền khi hủy hóa đơn bán hàng', 1
                WHERE NOT EXISTS (SELECT 1 FROM nhom_chi_phi WHERE ma_nhom = 'HOAN_TIEN_KHACH');
                """);

            _daDamBaoSchemaHuyHoaDon = true;
            return true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("EnsureHuyHoaDonSchemaAsync error", ex);
            return false;
        }
        finally
        {
            _khoaDamBaoSchemaHuyHoaDon.Release();
        }
    }

    public async Task<bool> EnsureLichSuChinhSuaHoaDonTableAsync()
    {
        if (_daDamBaoBangLichSuChinhSuaHoaDon)
            return true;

        await _khoaDamBaoBangLichSuChinhSuaHoaDon.WaitAsync();
        try
        {
            if (_daDamBaoBangLichSuChinhSuaHoaDon)
                return true;

            await using var connection = await OpenAsync();
            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS `lich_su_chinh_sua_hoa_don` (
                  `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT,
                  `hoa_don_id` int(10) UNSIGNED NOT NULL,
                  `ma_hoa_don` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
                  `ten_nguoi_thuc_hien` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
                  `ly_do` text COLLATE utf8mb4_unicode_ci NOT NULL,
                  `tong_tien_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `tong_tien_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `giam_gia_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `giam_gia_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `khach_can_tra_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `khach_can_tra_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `khach_da_tra` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `con_no_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `con_no_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `khach_hang_id_truoc` int(10) UNSIGNED DEFAULT NULL,
                  `khach_hang_id_sau` int(10) UNSIGNED DEFAULT NULL,
                  `ten_khach_hang_truoc` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                  `ten_khach_hang_sau` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                  `thoi_gian_ban_truoc` datetime DEFAULT NULL,
                  `thoi_gian_ban_sau` datetime DEFAULT NULL,
                  `du_lieu_truoc_json` longtext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                  `du_lieu_sau_json` longtext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                  `chi_tiet_thay_doi` text COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                  PRIMARY KEY (`id`),
                  KEY `idx_lsshd_hoa_don` (`hoa_don_id`),
                  KEY `idx_lsshd_ma_hoa_don` (`ma_hoa_don`),
                  KEY `idx_lsshd_thoi_gian` (`thoi_gian`),
                  KEY `idx_lsshd_nguoi_thuc_hien` (`nguoi_thuc_hien_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                """);

            _daDamBaoBangLichSuChinhSuaHoaDon = true;
            return true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("EnsureLichSuChinhSuaHoaDonTableAsync error", ex);
            return false;
        }
        finally
        {
            _khoaDamBaoBangLichSuChinhSuaHoaDon.Release();
        }
    }

    public async Task<List<CustomerOption>> GetKhachHangOptionsAsync()
    {
        var khachHangs = await GetKhachHangAsync();
        return khachHangs
            .Select(kh => new CustomerOption
            {
                Id = kh.Id,
                MaKhachHang = kh.MaKhachHang,
                TenKhachHang = kh.TenKhachHang,
                SoDienThoai = kh.SoDienThoai
            })
            .OrderBy(kh => kh.TenKhachHang, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// KHÓA NGHIỆP VỤ: Không cho phép chỉnh sửa trực tiếp hóa đơn đã tạo.
    /// Mọi lời gọi vào phương thức này đều bị chặn ngay tại tầng Service.
    /// </summary>
    public Task<HoaDonEditRequest?> GetHoaDonDeChinhSuaAsync(string maHoaDon)
    {
        throw new InvalidOperationException(
            "Chức năng chỉnh sửa trực tiếp hóa đơn đã bị khóa theo quy định nghiệp vụ. " +
            "Khi phát hiện hóa đơn sai, vui lòng hủy hóa đơn cũ và tạo lại hóa đơn mới.");
    }

    /// <summary>
    /// KHÓA NGHIỆP VỤ: Không cho phép ghi đè / sửa trực tiếp dữ liệu hóa đơn cũ.
    /// Mọi lời gọi vào phương thức này đều bị chặn ngay tại tầng Service.
    /// </summary>
    public Task<HoaDonBanHangReceipt> LuuChinhSuaHoaDonAsync(HoaDonEditRequest req)
    {
        throw new InvalidOperationException(
            "Chức năng chỉnh sửa trực tiếp hóa đơn đã bị khóa theo quy định nghiệp vụ. " +
            "Hóa đơn cũ chỉ được giữ nguyên hoặc chuyển sang trạng thái Đã hủy. " +
            "Muốn thay đổi nội dung đơn hàng phải hủy hóa đơn cũ và tạo hóa đơn mới.");
    }

    /// <summary>
    /// Tải thông tin hóa đơn để hiển thị lên hộp thoại Xác nhận hủy hóa đơn.
    /// </summary>
    public async Task<ThongTinXacNhanHuyHoaDon> GetThongTinXacNhanHuyHoaDonAsync(string maHoaDon)
    {
        KiemTraQuyenHuyHoaDon();

        if (string.IsNullOrWhiteSpace(maHoaDon))
            throw new InvalidOperationException("Thiếu mã hóa đơn cần hủy.");

        var maCanTim = maHoaDon.Trim();

        await EnsureHuyHoaDonSchemaAsync();
        var coCotContact = await EnsureHoaDonContactColumnsAsync();
        var coCotPhieuQuy = await EnsurePhieuQuyHoaDonColumnsAsync();
        var coCotLoaiPhieu = await EnsurePhieuQuyLoaiPhieuColumnAsync();

        await using var connection = await OpenAsync();

        var contactSelect = coCotContact
            ? """
              COALESCE(hd.so_dien_thoai, kh.so_dien_thoai, '') AS SoDienThoai,
              COALESCE(hd.dia_chi, kh.dia_chi, '')             AS DiaChi,
              COALESCE(hd.in_kem_thong_tin, 0)                 AS InKemThongTin
              """
            : """
              COALESCE(kh.so_dien_thoai, '')                   AS SoDienThoai,
              COALESCE(kh.dia_chi, '')                         AS DiaChi,
              0                                                AS InKemThongTin
              """;

        var header = await connection.QueryFirstOrDefaultAsync<HoaDonHuyHeaderRow>(
            $"""
            SELECT
                hd.id AS HoaDonId,
                hd.ma_hoa_don AS MaHoaDon,
                hd.chi_nhanh_id AS ChiNhanhId,
                hd.khach_hang_id AS KhachHangId,
                COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
                COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
                {contactSelect},
                hd.thoi_gian_ban AS ThoiGianBan,
                hd.tong_tien_hang AS TongTienHang,
                hd.giam_gia AS GiamGia,
                hd.thu_khac AS ThuKhac,
                hd.khach_can_tra AS KhachCanTra,
                hd.khach_da_thanh_toan AS KhachDaThanhToan,
                hd.con_no AS ConNo,
                hd.cong_no_da_ghi_nhan AS CongNoDaGhiNhan,
                hd.trang_thai_thanh_toan AS TrangThaiThanhToan,
                hd.trang_thai_hoa_don AS TrangThaiHoaDon,
                hd.ghi_chu AS GhiChu
            FROM hoa_don_ban_hang hd
            LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
            WHERE hd.ma_hoa_don = @MaHoaDon
            LIMIT 1;
            """,
            new { MaHoaDon = maCanTim });

        if (header is null)
            throw new InvalidOperationException($"Không tìm thấy hóa đơn {maCanTim} trong hệ thống.");

        if (LaTrangThaiHoaDonDaHuy(header.TrangThaiHoaDon))
            throw new InvalidOperationException($"Hóa đơn {maCanTim} đã ở trạng thái Đã hủy, không thể hủy lại.");

        var items = (await connection.QueryAsync<ChiTietSanPhamBanSaoHoaDon>(
            """
            SELECT
                ct.id AS Id,
                ct.san_pham_id AS SanPhamId,
                COALESCE(ct.ma_san_pham, '') AS MaSanPham,
                ct.ten_san_pham AS TenSanPham,
                COALESCE(NULLIF(TRIM(ct.don_vi_tinh), ''), 'Bó') AS DonViTinh,
                ct.so_luong AS SoLuong,
                ct.don_gia AS DonGia,
                COALESCE(ct.giam_gia, 0) AS GiamGia,
                ct.thanh_tien AS ThanhTien,
                COALESCE(ct.gia_von, 0) AS GiaVon,
                ct.ghi_chu AS GhiChu
            FROM chi_tiet_hoa_don_ban_hang ct
            WHERE ct.hoa_don_id = @HoaDonId
            ORDER BY ct.id;
            """,
            new { HoaDonId = header.HoaDonId })).ToList();

        var phieuThus = new List<PhieuQuyLienQuanHoaDonRow>();
        if (coCotPhieuQuy)
        {
            var loaiPhieuCol = coCotLoaiPhieu ? "COALESCE(pc.loai_phieu, 'THU')" : "'THU'";
            phieuThus = (await connection.QueryAsync<PhieuQuyLienQuanHoaDonRow>(
                $"""
                SELECT
                    pc.id AS Id,
                    pc.ma_phieu_chi AS MaPhieuChi,
                    {loaiPhieuCol} AS LoaiPhieu,
                    pc.so_tien AS SoTien,
                    COALESCE(pc.phuong_thuc, 'TIEN_MAT') AS PhuongThuc,
                    COALESCE(pc.noi_dung, '') AS NoiDung,
                    pc.ghi_chu AS GhiChu,
                    pc.thoi_gian_chi AS ThoiGianChi,
                    pc.trang_thai AS TrangThai
                FROM phieu_chi pc
                WHERE (pc.hoa_don_ban_hang_id = @HoaDonId OR pc.ma_hoa_don = @MaHoaDon)
                  AND pc.trang_thai = 'HOAN_THANH'
                ORDER BY pc.thoi_gian_chi ASC, pc.id ASC;
                """,
                new { HoaDonId = header.HoaDonId, MaHoaDon = header.MaHoaDon })).ToList();
        }

        // Kiểm tra thông tin giao hàng nếu có trong bảng giao_hang
        var trangThaiGiaoHang = "Đã xuất kho khi bán hàng";
        var daGiaoHang = true;
        try
        {
            var ghTrangThai = await connection.ExecuteScalarAsync<string?>(
                """
                SELECT trang_thai
                FROM giao_hang
                WHERE hoa_don_id = @HoaDonId
                ORDER BY id DESC
                LIMIT 1;
                """,
                new { HoaDonId = header.HoaDonId });

            if (!string.IsNullOrWhiteSpace(ghTrangThai))
            {
                if (string.Equals(ghTrangThai, "CHO_GIAO", StringComparison.OrdinalIgnoreCase))
                {
                    daGiaoHang = false;
                    trangThaiGiaoHang = "Chờ giao hàng (Chưa giao)";
                }
                else if (string.Equals(ghTrangThai, "DANG_GIAO", StringComparison.OrdinalIgnoreCase))
                {
                    daGiaoHang = true;
                    trangThaiGiaoHang = "Đang giao hàng";
                }
                else if (string.Equals(ghTrangThai, "DA_GIAO", StringComparison.OrdinalIgnoreCase))
                {
                    daGiaoHang = true;
                    trangThaiGiaoHang = "Đã giao hàng hoàn tất";
                }
            }
        }
        catch
        {
            // Bảng giao_hang có thể không tồn tại ở một số bản cài đặt cũ
        }

        return new ThongTinXacNhanHuyHoaDon
        {
            HoaDonId = header.HoaDonId,
            MaHoaDon = header.MaHoaDon,
            ChiNhanhId = header.ChiNhanhId == 0 ? 1u : header.ChiNhanhId,
            KhachHangId = header.KhachHangId,
            MaKhachHang = header.MaKhachHang,
            TenKhachHang = header.TenKhachHang,
            SoDienThoai = header.SoDienThoai,
            DiaChi = header.DiaChi,
            InKemThongTin = header.InKemThongTin == 1,
            ThoiGianBan = header.ThoiGianBan,
            TongTienHang = header.TongTienHang,
            GiamGia = header.GiamGia,
            ThuKhac = header.ThuKhac,
            KhachCanTra = header.KhachCanTra,
            KhachDaThanhToan = header.KhachDaThanhToan,
            ConNo = header.ConNo,
            CongNoDaGhiNhan = header.CongNoDaGhiNhan == 1,
            TrangThaiThanhToan = header.TrangThaiThanhToan,
            TrangThaiHoaDon = header.TrangThaiHoaDon,
            GhiChu = header.GhiChu,
            DaGiaoHang = daGiaoHang,
            TrangThaiGiaoHangMoTa = trangThaiGiaoHang,
            Items = items,
            PhieuThus = phieuThus
        };
    }

    /// <summary>
    /// Thực hiện hủy hóa đơn bán hàng trong MỘT transaction duy nhất:
    /// - Chặn hủy trùng / hủy lại hóa đơn đã hủy (SELECT ... FOR UPDATE).
    /// - Xử lý tồn kho: hoàn tác tồn kho nếu chưa xuất hàng hoặc đã nhận lại hàng trả về; không tự cộng kho nếu khách vẫn giữ hàng.
    /// - Xử lý công nợ khách hàng: hoàn tác công nợ còn lại, hỗ trợ chuyển tiền đã thu sang khoản khách trả trước.
    /// - Xử lý sổ quỹ: giữ nguyên phiếu thu cũ, tạo phiếu chi hoàn tiền (PC...) liên kết với hóa đơn gốc khi chọn hoàn tiền.
    /// - Chuyển trạng thái hóa đơn sang DA_HUY, lưu lý do, người hủy, thời gian hủy và lịch sử hủy bất biến.
    /// </summary>
    public async Task<HuyHoaDonResult> HuyHoaDonBanHangAsync(HuyHoaDonRequest req)
    {
        KiemTraQuyenHuyHoaDon();

        if (req is null)
            throw new InvalidOperationException("Yêu cầu hủy hóa đơn không hợp lệ.");

        var maCanTim = (req.MaHoaDon ?? "").Trim();
        if (maCanTim.Length == 0 && req.HoaDonId == 0)
            throw new InvalidOperationException("Thiếu mã hóa đơn cần hủy.");

        var lyDo = (req.LyDoHuy ?? "").Trim();
        if (lyDo.Length == 0)
            throw new InvalidOperationException("Bắt buộc phải nhập lý do hủy hóa đơn.");

        if (lyDo.Length > 500)
            throw new InvalidOperationException("Lý do hủy không được vượt quá 500 ký tự.");

        if (req.TrangThaiGiaoHang == TrangThaiGiaoHangKhiHuy.DaGiaoDaNhanLaiHang
            && !req.XacNhanDaNhanLaiHangTraVe)
        {
            throw new InvalidOperationException(
                "Đối với hóa đơn đã giao hàng, bạn phải tích xác nhận 'Đã nhận lại hàng trả về kho' trước khi cộng lại tồn kho.");
        }

        // Chuẩn bị toàn bộ schema trước khi mở transaction để tránh implicit commit của DDL trong MySQL
        await EnsureHuyHoaDonSchemaAsync();
        var coCotContact = await EnsureHoaDonContactColumnsAsync();
        var coCotPhieuQuy = await EnsurePhieuQuyHoaDonColumnsAsync();
        var coCotLoaiPhieu = await EnsurePhieuQuyLoaiPhieuColumnAsync();
        var coCotExtraPhieuQuy = await EnsurePhieuQuyExtraColumnsAsync();
        await EnsureNhomChiPhiMacDinhAsync();
        await EnsureBangLichSuThanhToanKhachHangAsync();

        await _khoaXuLyHuyHoaDon.WaitAsync();
        try
        {
            var now = DateTime.Now;
            var userId = Session.CurrentUser?.Id;
            var tenNguoiHuy = string.IsNullOrWhiteSpace(Session.CurrentUser?.HoTen)
                ? (Session.CurrentUser?.TenDangNhap ?? "Hệ thống")
                : Session.CurrentUser!.HoTen.Trim();

            await using var connection = await OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var daCommit = false;

            try
            {
                var contactSelect = coCotContact
                    ? """
                      COALESCE(hd.so_dien_thoai, kh.so_dien_thoai, '') AS SoDienThoai,
                      COALESCE(hd.dia_chi, kh.dia_chi, '')             AS DiaChi,
                      COALESCE(hd.in_kem_thong_tin, 0)                 AS InKemThongTin
                      """
                    : """
                      COALESCE(kh.so_dien_thoai, '')                   AS SoDienThoai,
                      COALESCE(kh.dia_chi, '')                         AS DiaChi,
                      0                                                AS InKemThongTin
                      """;

                var whereSql = req.HoaDonId > 0
                    ? "WHERE hd.id = @HoaDonId"
                    : "WHERE hd.ma_hoa_don = @MaHoaDon";

                var header = await connection.QueryFirstOrDefaultAsync<HoaDonHuyHeaderRow>(
                    $"""
                    SELECT
                        hd.id AS HoaDonId,
                        hd.ma_hoa_don AS MaHoaDon,
                        hd.chi_nhanh_id AS ChiNhanhId,
                        hd.khach_hang_id AS KhachHangId,
                        COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
                        COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
                        {contactSelect},
                        hd.thoi_gian_ban AS ThoiGianBan,
                        hd.tong_tien_hang AS TongTienHang,
                        hd.giam_gia AS GiamGia,
                        hd.thu_khac AS ThuKhac,
                        hd.khach_can_tra AS KhachCanTra,
                        hd.khach_da_thanh_toan AS KhachDaThanhToan,
                        hd.con_no AS ConNo,
                        hd.cong_no_da_ghi_nhan AS CongNoDaGhiNhan,
                        hd.trang_thai_thanh_toan AS TrangThaiThanhToan,
                        hd.trang_thai_hoa_don AS TrangThaiHoaDon,
                        hd.ghi_chu AS GhiChu
                    FROM hoa_don_ban_hang hd
                    LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
                    {whereSql}
                    LIMIT 1
                    FOR UPDATE;
                    """,
                    new { req.HoaDonId, MaHoaDon = maCanTim },
                    transaction);

                if (header is null)
                    throw new InvalidOperationException($"Không tìm thấy hóa đơn {(maCanTim.Length > 0 ? maCanTim : $"#{req.HoaDonId}")} trong database.");

                if (LaTrangThaiHoaDonDaHuy(header.TrangThaiHoaDon))
                {
                    throw new InvalidOperationException(
                        $"Hóa đơn {header.MaHoaDon} đã ở trạng thái Đã hủy, không thể hủy lại.");
                }

                var branchId = header.ChiNhanhId == 0
                    ? (Session.CurrentUser?.ChiNhanhId ?? 1u)
                    : header.ChiNhanhId;

                var items = (await connection.QueryAsync<ChiTietSanPhamBanSaoHoaDon>(
                    """
                    SELECT
                        ct.id AS Id,
                        ct.san_pham_id AS SanPhamId,
                        COALESCE(ct.ma_san_pham, '') AS MaSanPham,
                        ct.ten_san_pham AS TenSanPham,
                        COALESCE(NULLIF(TRIM(ct.don_vi_tinh), ''), 'Bó') AS DonViTinh,
                        ct.so_luong AS SoLuong,
                        ct.don_gia AS DonGia,
                        COALESCE(ct.giam_gia, 0) AS GiamGia,
                        ct.thanh_tien AS ThanhTien,
                        COALESCE(ct.gia_von, 0) AS GiaVon,
                        ct.ghi_chu AS GhiChu
                    FROM chi_tiet_hoa_don_ban_hang ct
                    WHERE ct.hoa_don_id = @HoaDonId
                    ORDER BY ct.id
                    FOR UPDATE;
                    """,
                    new { HoaDonId = header.HoaDonId },
                    transaction)).ToList();

                var laHoaDonTongHop = string.Equals(
                    header.TrangThaiHoaDon,
                    "TONG_HOP",
                    StringComparison.OrdinalIgnoreCase);

                // ============================================================
                // 1. XỬ LÝ TỒN KHO KHI HỦY HÓA ĐƠN
                // - Chưa xuất hàng: hoàn tác số lượng đã trừ kho.
                // - Đã giao + xác nhận đã nhận lại hàng trả về: cộng lại kho.
                // - Đã giao + khách vẫn giữ hàng: KHÔNG tự động cộng lại kho.
                // ============================================================
                var canHoanKho = !laHoaDonTongHop && req.TrangThaiGiaoHang switch
                {
                    TrangThaiGiaoHangKhiHuy.ChuaGiaoHang => true,
                    TrangThaiGiaoHangKhiHuy.DaGiaoDaNhanLaiHang => req.XacNhanDaNhanLaiHangTraVe,
                    TrangThaiGiaoHangKhiHuy.DaGiaoKhachVanGiuHang => false,
                    _ => false
                };

                var tongSoLuongHoanKho = 0m;

                if (canHoanKho && items.Count > 0)
                {
                    foreach (var item in items)
                    {
                        if (item.SanPhamId is null or 0 || item.SoLuong <= 0)
                            continue;

                        await HoanTacTonKhoKhiHuyHoaDonAsync(
                            connection,
                            transaction,
                            branchId,
                            userId,
                            header.MaHoaDon,
                            item.SanPhamId.Value,
                            item.TenSanPham,
                            item.SoLuong,
                            item.GiaVon,
                            req.TrangThaiGiaoHang,
                            lyDo,
                            now);

                        tongSoLuongHoanKho += item.SoLuong;
                    }
                }

                // ============================================================
                // 2. XỬ LÝ CÔNG NỢ KHÁCH HÀNG & KHOẢN ĐÃ THANH TOÁN
                // ============================================================
                var congNoDaHoanTac = 0m;
                var soTienHoanKhach = 0m;
                var soTienChuyenTraTruoc = 0m;
                string? maPhieuChiHoan = null;

                var phuongAnTien = header.KhachDaThanhToan <= 0m
                    ? PhuongAnXuLyTienKhiHuy.KhongCo
                    : req.PhuongAnTien;

                if (header.KhachDaThanhToan > 0m
                    && phuongAnTien == PhuongAnXuLyTienKhiHuy.ChuyenKhoanKhachTraTruoc
                    && (header.KhachHangId is null or 0))
                {
                    throw new InvalidOperationException(
                        "Khách lẻ (không có hồ sơ khách hàng) không thể chuyển tiền đã thanh toán thành khoản trả trước. Vui lòng chọn phương án 'Hoàn tiền cho khách'.");
                }

                if (header.KhachHangId is uint khId && khId > 0)
                {
                    // Khóa dòng khách hàng để đồng bộ sổ cái công nợ
                    await connection.ExecuteScalarAsync<uint?>(
                        "SELECT id FROM khach_hang WHERE id = @Id LIMIT 1 FOR UPDATE;",
                        new { Id = khId },
                        transaction);

                    // Nếu khách hàng chưa có dòng nào trong lich_su_cong_no_khach_hang (dữ liệu cũ/import)
                    // nhưng đang có hóa đơn nợ, khởi tạo số dư đầu kỳ từ các hóa đơn HOAN_THANH hiện có
                    // trước khi ghi giảm công nợ do hủy hóa đơn.
                    var soDongLedger = await connection.ExecuteScalarAsync<long>(
                        """
                        SELECT COUNT(*)
                        FROM lich_su_cong_no_khach_hang
                        WHERE khach_hang_id = @KhachHangId;
                        """,
                        new { KhachHangId = khId },
                        transaction);

                    if (soDongLedger == 0)
                    {
                        var tongNoHoaDonHienTai = await connection.ExecuteScalarAsync<decimal>(
                            """
                            SELECT COALESCE(SUM(con_no), 0)
                            FROM hoa_don_ban_hang
                            WHERE khach_hang_id = @KhachHangId
                              AND trang_thai_hoa_don = 'HOAN_THANH'
                              AND cong_no_da_ghi_nhan = 1
                              AND con_no > 0;
                            """,
                            new { KhachHangId = khId },
                            transaction);

                        if (tongNoHoaDonHienTai > 0)
                        {
                            await connection.ExecuteAsync(
                                """
                                INSERT INTO lich_su_cong_no_khach_hang
                                (
                                    khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                                    ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu
                                )
                                VALUES
                                (
                                    @KhachHangId, @ChiNhanhId, 'GHI_NO', 'DIEU_CHINH', NULL,
                                    @GhiNo, 0, @SoDu, @ThoiGian, NULL, @GhiChu
                                );
                                """,
                                new
                                {
                                    KhachHangId = khId,
                                    ChiNhanhId = branchId,
                                    GhiNo = tongNoHoaDonHienTai,
                                    SoDu = tongNoHoaDonHienTai,
                                    ThoiGian = now,
                                    GhiChu = "Số dư công nợ đầu kỳ từ các hóa đơn còn nợ hiện có."
                                },
                                transaction);
                        }
                    }

                    // 2a. Hoàn tác phần công nợ còn lại của hóa đơn (nếu có)
                    if (header.CongNoDaGhiNhan == 1 && header.ConNo > 0)
                    {
                        var soDuCu = await connection.ExecuteScalarAsync<decimal>(
                            """
                            SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                            FROM lich_su_cong_no_khach_hang
                            WHERE khach_hang_id = @KhachHangId
                            FOR UPDATE;
                            """,
                            new { KhachHangId = khId },
                            transaction);

                        var soDuMoi = soDuCu - header.ConNo;

                        await connection.ExecuteAsync(
                            """
                            INSERT INTO lich_su_cong_no_khach_hang
                            (
                                khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                                ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu
                            )
                            VALUES
                            (
                                @KhachHangId, @ChiNhanhId, 'HUY_HOA_DON', 'HOA_DON', @MaChungTu,
                                0, @GhiCo, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu
                            );
                            """,
                            new
                            {
                                KhachHangId = khId,
                                ChiNhanhId = branchId,
                                MaChungTu = header.MaHoaDon,
                                GhiCo = header.ConNo,
                                SoDu = soDuMoi,
                                ThoiGian = now,
                                NguoiThucHienId = userId,
                                GhiChu = $"Hoàn tác công nợ phát sinh do hủy hóa đơn {header.MaHoaDon}. Lý do: {lyDo}"
                            },
                            transaction);

                        congNoDaHoanTac = header.ConNo;
                    }

                    // 2b. Nếu hóa đơn đã thu tiền (> 0) và chọn chuyển sang khoản khách trả trước / giảm công nợ
                    if (header.KhachDaThanhToan > 0 && phuongAnTien == PhuongAnXuLyTienKhiHuy.ChuyenKhoanKhachTraTruoc)
                    {
                        var soDuCu = await connection.ExecuteScalarAsync<decimal>(
                            """
                            SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                            FROM lich_su_cong_no_khach_hang
                            WHERE khach_hang_id = @KhachHangId
                            FOR UPDATE;
                            """,
                            new { KhachHangId = khId },
                            transaction);

                        var soDuMoi = soDuCu - header.KhachDaThanhToan;

                        await connection.ExecuteAsync(
                            """
                            INSERT INTO lich_su_cong_no_khach_hang
                            (
                                khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                                ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu
                            )
                            VALUES
                            (
                                @KhachHangId, @ChiNhanhId, 'KHACH_TRA_TRUOC', 'HUY_HOA_DON', @MaChungTu,
                                0, @GhiCo, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu
                            );
                            """,
                            new
                            {
                                KhachHangId = khId,
                                ChiNhanhId = branchId,
                                MaChungTu = header.MaHoaDon,
                                GhiCo = header.KhachDaThanhToan,
                                SoDu = soDuMoi,
                                ThoiGian = now,
                                NguoiThucHienId = userId,
                                GhiChu = $"Chuyển số tiền đã thu ({header.KhachDaThanhToan:N0} đ) của hóa đơn hủy {header.MaHoaDon} thành khoản khách trả trước / cấn trừ công nợ. Lý do: {lyDo}"
                            },
                            transaction);

                        soTienChuyenTraTruoc = header.KhachDaThanhToan;
                    }
                }

                // ============================================================
                // 3. XỬ LÝ SỔ QUỸ: TẠO PHIẾU CHI HOÀN TIỀN (KHÔNG XÓA PHIẾU THU CŨ)
                // ============================================================
                if (header.KhachDaThanhToan > 0 && phuongAnTien == PhuongAnXuLyTienKhiHuy.HoanTienChoKhach)
                {
                    maPhieuChiHoan = await TaoPhieuChiHoanTienHuyHoaDonTrongGiaoDichAsync(
                        connection,
                        transaction,
                        branchId,
                        userId,
                        header.MaHoaDon,
                        header.TenKhachHang,
                        header.KhachDaThanhToan,
                        req.PhuongThucHoanTien,
                        lyDo,
                        now,
                        coCotPhieuQuy,
                        coCotLoaiPhieu,
                        coCotExtraPhieuQuy);

                    soTienHoanKhach = header.KhachDaThanhToan;
                }

                // ============================================================
                // 4. CẬP NHẬT TRẠNG THÁI HÓA ĐƠN SANG DA_HUY (GIỮ NGUYÊN SỐ LIỆU GỐC)
                // ============================================================
                var maTrangThaiGiaoHang = req.TrangThaiGiaoHang switch
                {
                    TrangThaiGiaoHangKhiHuy.ChuaGiaoHang => "CHUA_GIAO",
                    TrangThaiGiaoHangKhiHuy.DaGiaoDaNhanLaiHang => "DA_GIAO_DA_NHAN_LAI",
                    TrangThaiGiaoHangKhiHuy.DaGiaoKhachVanGiuHang => "DA_GIAO_KHACH_GIU",
                    _ => "CHUA_GIAO"
                };

                var maPhuongAnTien = phuongAnTien switch
                {
                    PhuongAnXuLyTienKhiHuy.HoanTienChoKhach => "HOAN_TIEN",
                    PhuongAnXuLyTienKhiHuy.ChuyenKhoanKhachTraTruoc => "CHUYEN_TRA_TRUOC",
                    _ => "KHONG_CO"
                };

                var maCheDoHuy = req.CheDoHuy == CheDoHuyHoaDon.HuyVaTaoHoaDonTuongTu
                    ? "HUY_VA_TAO_LAI"
                    : "CHI_HUY";

                var affected = await connection.ExecuteAsync(
                    """
                    UPDATE hoa_don_ban_hang
                    SET trang_thai_hoa_don = @TrangThaiHoaDon,
                        ly_do_huy = @LyDoHuy,
                        nguoi_huy_id = @NguoiHuyId,
                        ten_nguoi_huy = @TenNguoiHuy,
                        thoi_gian_huy = @ThoiGianHuy,
                        trang_thai_giao_hang_khi_huy = @TrangThaiGiaoHang,
                        da_hoan_kho = @DaHoanKho,
                        phuong_an_tien_khi_huy = @PhuongAnTien,
                        so_tien_hoan = @SoTienHoan,
                        ma_phieu_chi_hoan = @MaPhieuChiHoan,
                        ngay_cap_nhat = @ThoiGianHuy
                    WHERE id = @HoaDonId
                      AND trang_thai_hoa_don NOT IN ('DA_HUY', 'HUY');
                    """,
                    new
                    {
                        TrangThaiHoaDon = TrangThaiHoaDonDaHuy,
                        LyDoHuy = lyDo,
                        NguoiHuyId = userId,
                        TenNguoiHuy = Truncate150(tenNguoiHuy),
                        ThoiGianHuy = now,
                        TrangThaiGiaoHang = maTrangThaiGiaoHang,
                        DaHoanKho = canHoanKho ? 1 : 0,
                        PhuongAnTien = maPhuongAnTien,
                        SoTienHoan = soTienHoanKhach,
                        MaPhieuChiHoan = maPhieuChiHoan,
                        HoaDonId = header.HoaDonId
                    },
                    transaction);

                if (affected != 1)
                {
                    throw new InvalidOperationException(
                        $"Không thể cập nhật trạng thái hủy cho hóa đơn {header.MaHoaDon} (có thể hóa đơn đã bị hủy ở phiên khác).");
                }

                // Cập nhật trạng thái bảng giao_hang nếu có
                try
                {
                    await connection.ExecuteAsync(
                        """
                        UPDATE giao_hang
                        SET trang_thai = 'DA_HUY',
                            ghi_chu = CONCAT(COALESCE(ghi_chu, ''), ' [Đã hủy hóa đơn: ', @LyDo, ']')
                        WHERE hoa_don_id = @HoaDonId;
                        """,
                        new { HoaDonId = header.HoaDonId, LyDo = lyDo },
                        transaction);
                }
                catch
                {
                    // Bỏ qua nếu bảng giao_hang không tồn tại
                }

                // ============================================================
                // 5. LƯU BẢN GHI KIỂM TOÁN BẤT BIẾN VÀO lich_su_huy_hoa_don
                // ============================================================
                var snapshotJson = JsonSerializer.Serialize(new
                {
                    header.HoaDonId,
                    header.MaHoaDon,
                    header.KhachHangId,
                    header.TenKhachHang,
                    header.ThoiGianBan,
                    header.TongTienHang,
                    header.GiamGia,
                    header.KhachCanTra,
                    header.KhachDaThanhToan,
                    header.ConNo,
                    TrangThaiGiaoHang = maTrangThaiGiaoHang,
                    DaHoanKho = canHoanKho,
                    TongSoLuongHoanKho = tongSoLuongHoanKho,
                    PhuongAnTien = maPhuongAnTien,
                    SoTienHoan = soTienHoanKhach,
                    SoTienChuyenTraTruoc = soTienChuyenTraTruoc,
                    CongNoDaHoanTac = congNoDaHoanTac,
                    MaPhieuChiHoan = maPhieuChiHoan,
                    CheDoHuy = maCheDoHuy,
                    LyDoHuy = lyDo,
                    NguoiHuyId = userId,
                    TenNguoiHuy = tenNguoiHuy,
                    ThoiGianHuy = now,
                    Items = items
                });

                await connection.ExecuteAsync(
                    """
                    INSERT INTO lich_su_huy_hoa_don
                    (
                        hoa_don_id, ma_hoa_don, khach_hang_id, ten_khach_hang,
                        tong_tien_hoa_don, so_tien_da_thanh_toan, cong_no_khi_huy,
                        trang_thai_giao_hang, da_hoan_kho,
                        phuong_an_tien, so_tien_hoan, phuong_thuc_hoan, ma_phieu_chi_hoan,
                        che_do_huy, ly_do_huy, nguoi_huy_id, ten_nguoi_huy, thoi_gian_huy,
                        du_lieu_snapshot
                    )
                    VALUES
                    (
                        @HoaDonId, @MaHoaDon, @KhachHangId, @TenKhachHang,
                        @TongTienHoaDon, @SoTienDaThanhToan, @CongNoKhiHuy,
                        @TrangThaiGiaoHang, @DaHoanKho,
                        @PhuongAnTien, @SoTienHoan, @PhuongThucHoan, @MaPhieuChiHoan,
                        @CheDoHuy, @LyDoHuy, @NguoiHuyId, @TenNguoiHuy, @ThoiGianHuy,
                        @DuLieuSnapshot
                    );
                    """,
                    new
                    {
                        HoaDonId = header.HoaDonId,
                        MaHoaDon = header.MaHoaDon,
                        KhachHangId = header.KhachHangId,
                        TenKhachHang = header.TenKhachHang,
                        TongTienHoaDon = header.KhachCanTra,
                        SoTienDaThanhToan = header.KhachDaThanhToan,
                        CongNoKhiHuy = header.ConNo,
                        TrangThaiGiaoHang = maTrangThaiGiaoHang,
                        DaHoanKho = canHoanKho ? 1 : 0,
                        PhuongAnTien = maPhuongAnTien,
                        SoTienHoan = soTienHoanKhach,
                        PhuongThucHoan = soTienHoanKhach > 0 ? ChuanHoaPhuongThuc(req.PhuongThucHoanTien) : null,
                        MaPhieuChiHoan = maPhieuChiHoan,
                        CheDoHuy = maCheDoHuy,
                        LyDoHuy = lyDo,
                        NguoiHuyId = userId,
                        TenNguoiHuy = Truncate150(tenNguoiHuy),
                        ThoiGianHuy = now,
                        DuLieuSnapshot = snapshotJson
                    },
                    transaction);

                await transaction.CommitAsync();
                daCommit = true;

                var moTaHuy = $"Hủy hóa đơn {header.MaHoaDon} ({header.TenKhachHang}, Tổng: {header.KhachCanTra:N0} đ). Lý do: {lyDo}";
                if (!string.IsNullOrWhiteSpace(maPhieuChiHoan))
                    moTaHuy += $". Phiếu chi hoàn tiền: {maPhieuChiHoan} ({soTienHoanKhach:N0} đ)";
                else if (soTienChuyenTraTruoc > 0)
                    moTaHuy += $". Chuyển khách trả trước: {soTienChuyenTraTruoc:N0} đ";

                _ = LogSubAccountActivityAsync(
                    "Hủy hóa đơn",
                    "HOA_DON",
                    header.HoaDonId,
                    header.MaHoaDon,
                    moTaHuy,
                    header.KhachCanTra);

                var receiptDaHuy = await GetHoaDonBanHangTheoMaAsync(header.MaHoaDon);

                return new HuyHoaDonResult
                {
                    HoaDonId = header.HoaDonId,
                    MaHoaDon = header.MaHoaDon,
                    CheDoHuy = req.CheDoHuy,
                    DaHoanKho = canHoanKho,
                    TongSoLuongHoanKho = tongSoLuongHoanKho,
                    CongNoDaHoanTac = congNoDaHoanTac,
                    SoTienHoanKhach = soTienHoanKhach,
                    SoTienChuyenTraTruoc = soTienChuyenTraTruoc,
                    MaPhieuChiHoan = maPhieuChiHoan,
                    ReceiptDaHuy = receiptDaHuy,
                    DuLieuSaoChep = new ThongTinXacNhanHuyHoaDon
                    {
                        HoaDonId = header.HoaDonId,
                        MaHoaDon = header.MaHoaDon,
                        ChiNhanhId = branchId,
                        KhachHangId = header.KhachHangId,
                        MaKhachHang = header.MaKhachHang,
                        TenKhachHang = header.TenKhachHang,
                        SoDienThoai = header.SoDienThoai,
                        DiaChi = header.DiaChi,
                        InKemThongTin = header.InKemThongTin == 1,
                        ThoiGianBan = header.ThoiGianBan,
                        TongTienHang = header.TongTienHang,
                        GiamGia = header.GiamGia,
                        ThuKhac = header.ThuKhac,
                        KhachCanTra = header.KhachCanTra,
                        KhachDaThanhToan = header.KhachDaThanhToan,
                        ConNo = header.ConNo,
                        CongNoDaGhiNhan = header.CongNoDaGhiNhan == 1,
                        TrangThaiThanhToan = header.TrangThaiThanhToan,
                        TrangThaiHoaDon = TrangThaiHoaDonDaHuy,
                        GhiChu = header.GhiChu,
                        Items = items
                    }
                };
            }
            catch (Exception ex)
            {
                if (!daCommit)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                    }
                    catch (Exception rollbackEx)
                    {
                        SafeLog.WriteError("Rollback HuyHoaDonBanHangAsync thất bại", rollbackEx);
                    }
                }

                SafeLog.WriteError($"Lỗi HuyHoaDonBanHangAsync cho {maCanTim}", ex);
                throw;
            }
        }
        finally
        {
            _khoaXuLyHuyHoaDon.Release();
        }
    }

    private async Task HoanTacTonKhoKhiHuyHoaDonAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint chiNhanhId,
        uint? nguoiThucHienId,
        string maHoaDon,
        uint sanPhamId,
        string tenSanPham,
        decimal soLuongHoan,
        decimal giaVonHoaDon,
        TrangThaiGiaoHangKhiHuy trangThaiGiaoHang,
        string lyDo,
        DateTime now)
    {
        var banGhiTon = (await connection.QueryAsync(
            """
            SELECT so_luong_ton, gia_von_binh_quan
            FROM ton_kho
            WHERE chi_nhanh_id = @ChiNhanhId
              AND san_pham_id = @SanPhamId
            FOR UPDATE;
            """,
            new
            {
                ChiNhanhId = chiNhanhId,
                SanPhamId = sanPhamId
            },
            transaction)).ToList();

        decimal tonTruoc = 0m;
        decimal giaVonCu = 0m;

        if (banGhiTon.FirstOrDefault() is IDictionary<string, object> row)
        {
            tonTruoc = ToDecimal(row.TryGetValue("so_luong_ton", out var soLuong) ? soLuong : null);
            giaVonCu = ToDecimal(row.TryGetValue("gia_von_binh_quan", out var giaVon) ? giaVon : null);
        }
        else
        {
            var spRow = await connection.QueryFirstOrDefaultAsync(
                """
                SELECT COALESCE(ton_kho_excel, 0) AS TonKho, COALESCE(gia_von, 0) AS GiaVon
                FROM san_pham
                WHERE id = @Id
                LIMIT 1
                FOR UPDATE;
                """,
                new { Id = sanPhamId },
                transaction);

            if (spRow is not null)
            {
                tonTruoc = ToDecimal(spRow.TonKho);
                giaVonCu = ToDecimal(spRow.GiaVon);
            }
        }

        var tonSau = tonTruoc + soLuongHoan;
        var giaVonGhiNhan = giaVonCu > 0 ? giaVonCu : giaVonHoaDon;

        await connection.ExecuteAsync(
            """
            INSERT INTO ton_kho
            (
                chi_nhanh_id, san_pham_id, so_luong_ton, gia_von_binh_quan, ngay_cap_nhat
            )
            VALUES
            (
                @ChiNhanhId, @SanPhamId, @SoLuongTon, @GiaVonBinhQuan, @Now
            )
            ON DUPLICATE KEY UPDATE
                so_luong_ton = @SoLuongTon,
                ngay_cap_nhat = @Now;
            """,
            new
            {
                ChiNhanhId = chiNhanhId,
                SanPhamId = sanPhamId,
                SoLuongTon = tonSau,
                GiaVonBinhQuan = giaVonGhiNhan,
                Now = now
            },
            transaction);

        await connection.ExecuteAsync(
            """
            UPDATE san_pham
            SET ton_kho_excel = @SoLuongTon,
                ngay_cap_nhat = @Now
            WHERE id = @SanPhamId;
            """,
            new
            {
                SanPhamId = sanPhamId,
                SoLuongTon = tonSau,
                Now = now
            },
            transaction);

        var ghiChuKho = trangThaiGiaoHang == TrangThaiGiaoHangKhiHuy.ChuaGiaoHang
            ? $"Hoàn tác xuất kho do hủy hóa đơn chưa giao {maHoaDon} ({tenSanPham}). Lý do: {lyDo}"
            : $"Nhập lại kho hàng khách trả về do hủy hóa đơn {maHoaDon} ({tenSanPham}). Lý do: {lyDo}";

        await connection.ExecuteAsync(
            """
            INSERT INTO lich_su_nhap_xuat_kho
            (
                chi_nhanh_id, san_pham_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                so_luong, ton_truoc, ton_sau, gia_von, nguoi_thuc_hien_id, thoi_gian, ghi_chu
            )
            VALUES
            (
                @ChiNhanhId, @SanPhamId, 'NHAP', 'HUY_HOA_DON', @MaChungTu,
                @SoLuong, @TonTruoc, @TonSau, @GiaVon, @NguoiThucHienId, @Now, @GhiChu
            );
            """,
            new
            {
                ChiNhanhId = chiNhanhId,
                SanPhamId = sanPhamId,
                MaChungTu = maHoaDon,
                SoLuong = soLuongHoan,
                TonTruoc = tonTruoc,
                TonSau = tonSau,
                GiaVon = giaVonGhiNhan,
                NguoiThucHienId = nguoiThucHienId,
                Now = now,
                GhiChu = ghiChuKho
            },
            transaction);
    }

    private async Task<string> TaoPhieuChiHoanTienHuyHoaDonTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint chiNhanhId,
        uint? nguoiThucHienId,
        string maHoaDon,
        string tenKhachHang,
        decimal soTienHoan,
        string? phuongThucHoan,
        string lyDoHuy,
        DateTime now,
        bool coCotPhieuQuyHoaDon,
        bool coCotLoaiPhieu,
        bool coCotNguoiNopNhanGhiChu)
    {
        var maPhieuChi = await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);
        var idNhomHoanTien = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "HOAN_TIEN_KHACH");

        var phuongThuc = ChuanHoaPhuongThuc(phuongThucHoan);
        var noiDung = $"Hoàn tiền hủy hóa đơn {maHoaDon}";
        var ghiChu = $"Hoàn tiền cho khách ({tenKhachHang}) do hủy hóa đơn {maHoaDon}. Lý do hủy: {lyDoHuy}";

        // LƯU Ý QUAN TRỌNG: Bảng phieu_chi có UNIQUE INDEX uk_phieu_chi_hoa_don (hoa_don_ban_hang_id).
        // Phiếu thu lúc bán hàng (PT...) đã giữ hoa_don_ban_hang_id = invoiceId và không được xóa.
        // Vì vậy phiếu chi hoàn tiền (PC...) lưu hoa_don_ban_hang_id = NULL và liên kết qua ma_hoa_don = @MaHoaDon.
        var cols = new List<string>
        {
            "ma_phieu_chi",
            "chi_nhanh_id",
            "nhom_chi_phi_id",
            "so_tien",
            "phuong_thuc",
            "noi_dung",
            "thoi_gian_chi",
            "nguoi_thuc_hien_id",
            "trang_thai"
        };

        var vals = new List<string>
        {
            "@MaPhieu",
            "@ChiNhanhId",
            "@NhomChiPhiId",
            "@SoTien",
            "@PhuongThuc",
            "@NoiDung",
            "@ThoiGian",
            "@NguoiTaoId",
            "@TrangThai"
        };

        if (coCotLoaiPhieu)
        {
            cols.Add("loai_phieu");
            vals.Add("'CHI'");
        }

        if (coCotNguoiNopNhanGhiChu)
        {
            cols.Add("nguoi_nop_nhan");
            vals.Add("@NguoiNopNhan");
            cols.Add("ghi_chu");
            vals.Add("@GhiChu");
        }

        if (coCotPhieuQuyHoaDon)
        {
            cols.Add("hoa_don_ban_hang_id");
            vals.Add("NULL");
            cols.Add("ma_hoa_don");
            vals.Add("@MaHoaDon");
        }

        var insertSql = $"""
            INSERT INTO phieu_chi ({string.Join(", ", cols)})
            VALUES ({string.Join(", ", vals)});
            """;

        await connection.ExecuteAsync(
            insertSql,
            new
            {
                MaPhieu = maPhieuChi,
                ChiNhanhId = chiNhanhId,
                NhomChiPhiId = idNhomHoanTien,
                SoTien = soTienHoan,
                PhuongThuc = phuongThuc,
                NoiDung = noiDung,
                NguoiNopNhan = Truncate150(tenKhachHang),
                GhiChu = ghiChu,
                ThoiGian = now,
                NguoiTaoId = nguoiThucHienId,
                TrangThai = TrangThaiPhieuHoanThanh,
                MaHoaDon = maHoaDon
            },
            transaction);

        return maPhieuChi;
    }

    /// <summary>
    /// Ghi nhận liên kết 2 chiều giữa hóa đơn mới vừa tạo và hóa đơn cũ đã hủy
    /// ngay trong cùng transaction tạo hóa đơn mới.
    /// </summary>
    private async Task GhiLienKetHoaDonThayTheTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint hoaDonMoiId,
        string maHoaDonMoi,
        uint hoaDonGocId,
        string? maHoaDonGoc,
        DateTime now)
    {
        if (hoaDonGocId == 0 && string.IsNullOrWhiteSpace(maHoaDonGoc))
            return;

        var gocRow = await connection.QueryFirstOrDefaultAsync<(uint Id, string MaHoaDon)>(
            hoaDonGocId > 0
                ? "SELECT id AS Id, ma_hoa_don AS MaHoaDon FROM hoa_don_ban_hang WHERE id = @Id LIMIT 1 FOR UPDATE;"
                : "SELECT id AS Id, ma_hoa_don AS MaHoaDon FROM hoa_don_ban_hang WHERE ma_hoa_don = @Ma LIMIT 1 FOR UPDATE;",
            new { Id = hoaDonGocId, Ma = (maHoaDonGoc ?? "").Trim() },
            transaction);

        if (gocRow.Id == 0)
            return;

        await connection.ExecuteAsync(
            """
            UPDATE hoa_don_ban_hang
            SET hoa_don_goc_id = @HoaDonGocId,
                ma_hoa_don_goc = @MaHoaDonGoc
            WHERE id = @HoaDonMoiId;
            """,
            new
            {
                HoaDonGocId = gocRow.Id,
                MaHoaDonGoc = gocRow.MaHoaDon,
                HoaDonMoiId = hoaDonMoiId
            },
            transaction);

        await connection.ExecuteAsync(
            """
            UPDATE hoa_don_ban_hang
            SET hoa_don_thay_the_id = @HoaDonMoiId,
                ma_hoa_don_thay_the = @MaHoaDonMoi,
                ngay_cap_nhat = @Now
            WHERE id = @HoaDonGocId;
            """,
            new
            {
                HoaDonMoiId = hoaDonMoiId,
                MaHoaDonMoi = maHoaDonMoi,
                Now = now,
                HoaDonGocId = gocRow.Id
            },
            transaction);

        await connection.ExecuteAsync(
            """
            UPDATE lich_su_huy_hoa_don
            SET hoa_don_thay_the_id = @HoaDonMoiId,
                ma_hoa_don_thay_the = @MaHoaDonMoi
            WHERE hoa_don_id = @HoaDonGocId;
            """,
            new
            {
                HoaDonMoiId = hoaDonMoiId,
                MaHoaDonMoi = maHoaDonMoi,
                HoaDonGocId = gocRow.Id
            },
            transaction);
    }

    /// <summary>
    /// Lấy danh sách các giao dịch sổ quỹ (phiếu thu, phiếu chi hoàn tiền),
    /// điều chỉnh công nợ và hoàn kho liên quan đến một hóa đơn để hiển thị đối soát.
    /// </summary>
    public async Task<List<GiaoDichLienQuanHoaDonItem>> LayGiaoDichLienQuanHoaDonAsync(
        MySqlConnection connection,
        uint hoaDonId,
        string maHoaDon)
    {
        var ketQua = new List<GiaoDichLienQuanHoaDonItem>();
        if (hoaDonId == 0 && string.IsNullOrWhiteSpace(maHoaDon))
            return ketQua;

        try
        {
            var coCotPhieuQuy = await EnsurePhieuQuyHoaDonColumnsAsync();
            if (coCotPhieuQuy)
            {
                var phieus = await connection.QueryAsync(
                    """
                    SELECT
                        ma_phieu_chi AS MaPhieu,
                        so_tien AS SoTien,
                        COALESCE(phuong_thuc, 'TIEN_MAT') AS PhuongThuc,
                        COALESCE(noi_dung, '') AS NoiDung,
                        COALESCE(ghi_chu, '') AS GhiChu,
                        thoi_gian_chi AS ThoiGian
                    FROM phieu_chi
                    WHERE (hoa_don_ban_hang_id = @HoaDonId OR ma_hoa_don = @MaHoaDon)
                      AND trang_thai = 'HOAN_THANH'
                    ORDER BY thoi_gian_chi ASC, id ASC;
                    """,
                    new { HoaDonId = hoaDonId, MaHoaDon = maHoaDon });

                foreach (var p in phieus)
                {
                    string ma = p.MaPhieu?.ToString() ?? "";
                    var laChi = ma.StartsWith("PC", StringComparison.OrdinalIgnoreCase);
                    ketQua.Add(new GiaoDichLienQuanHoaDonItem
                    {
                        LoaiGiaoDich = laChi ? "Phiếu chi hoàn tiền" : "Phiếu thu sổ quỹ",
                        MaGiaoDich = ma,
                        SoTien = ToDecimal(p.SoTien),
                        PhuongThuc = p.PhuongThuc?.ToString() ?? "",
                        ThoiGian = p.ThoiGian is DateTime dt ? dt : DateTime.Now,
                        NoiDung = !string.IsNullOrWhiteSpace(p.GhiChu?.ToString())
                            ? p.GhiChu.ToString()
                            : (p.NoiDung?.ToString() ?? "")
                    });
                }
            }

            var congNos = await connection.QueryAsync(
                """
                SELECT
                    loai_giao_dich AS LoaiGiaoDich,
                    ghi_no AS GhiNo,
                    ghi_co AS GhiCo,
                    thoi_gian AS ThoiGian,
                    COALESCE(ghi_chu, '') AS GhiChu
                FROM lich_su_cong_no_khach_hang
                WHERE ma_chung_tu = @MaHoaDon
                ORDER BY thoi_gian ASC, id ASC;
                """,
                new { MaHoaDon = maHoaDon });

            foreach (var cn in congNos)
            {
                string loai = cn.LoaiGiaoDich?.ToString() ?? "";
                decimal ghiNo = ToDecimal(cn.GhiNo);
                decimal ghiCo = ToDecimal(cn.GhiCo);
                var tenLoai = loai switch
                {
                    "BAN_HANG" => "Ghi nợ bán hàng",
                    "THU_TIEN" => "Thu nợ hóa đơn",
                    "HUY_HOA_DON" => "Hoàn tác công nợ (Hủy HĐ)",
                    "KHACH_TRA_TRUOC" => "Chuyển khách trả trước",
                    _ => "Điều chỉnh công nợ"
                };

                ketQua.Add(new GiaoDichLienQuanHoaDonItem
                {
                    LoaiGiaoDich = tenLoai,
                    MaGiaoDich = maHoaDon,
                    SoTien = ghiNo > 0 ? ghiNo : ghiCo,
                    ThoiGian = cn.ThoiGian is DateTime dt ? dt : DateTime.Now,
                    NoiDung = cn.GhiChu?.ToString() ?? ""
                });
            }

            var khos = await connection.QueryAsync(
                """
                SELECT
                    loai_giao_dich AS LoaiGiaoDich,
                    loai_chung_tu AS LoaiChungTu,
                    so_luong AS SoLuong,
                    thoi_gian AS ThoiGian,
                    COALESCE(ghi_chu, '') AS GhiChu
                FROM lich_su_nhap_xuat_kho
                WHERE ma_chung_tu = @MaHoaDon
                  AND loai_chung_tu = 'HUY_HOA_DON'
                ORDER BY thoi_gian ASC, id ASC;
                """,
                new { MaHoaDon = maHoaDon });

            foreach (var k in khos)
            {
                decimal sl = ToDecimal(k.SoLuong);
                ketQua.Add(new GiaoDichLienQuanHoaDonItem
                {
                    LoaiGiaoDich = "Hoàn nhập kho",
                    MaGiaoDich = $"SL +{SoLuongFormat.HienThi(sl)}",
                    SoTien = 0m,
                    ThoiGian = k.ThoiGian is DateTime dt ? dt : DateTime.Now,
                    NoiDung = k.GhiChu?.ToString() ?? ""
                });
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError($"LayGiaoDichLienQuanHoaDonAsync error for {maHoaDon}", ex);
        }

        return ketQua.OrderBy(x => x.ThoiGian).ToList();
    }

    public async Task<List<LichSuChinhSuaHoaDon>> GetLichSuChinhSuaHoaDonAsync(string maHoaDon)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.InvoicesView,
            "Bạn không có quyền xem lịch sử chỉnh sửa hóa đơn.");

        if (string.IsNullOrWhiteSpace(maHoaDon))
            return [];

        await EnsureLichSuChinhSuaHoaDonTableAsync();
        await using var connection = await OpenAsync();

        var rows = await connection.QueryAsync<LichSuChinhSuaHoaDon>(
            """
            SELECT
                id AS Id,
                hoa_don_id AS HoaDonId,
                ma_hoa_don AS MaHoaDon,
                thoi_gian AS ThoiGian,
                nguoi_thuc_hien_id AS NguoiThucHienId,
                ten_nguoi_thuc_hien AS TenNguoiThucHien,
                ly_do AS LyDo,
                tong_tien_truoc AS TongTienTruoc,
                tong_tien_sau AS TongTienSau,
                giam_gia_truoc AS GiamGiaTruoc,
                giam_gia_sau AS GiamGiaSau,
                khach_can_tra_truoc AS KhachCanTraTruoc,
                khach_can_tra_sau AS KhachCanTraSau,
                khach_da_tra AS KhachDaTra,
                con_no_truoc AS ConNoTruoc,
                con_no_sau AS ConNoSau,
                khach_hang_id_truoc AS KhachHangIdTruoc,
                khach_hang_id_sau AS KhachHangIdSau,
                ten_khach_hang_truoc AS TenKhachHangTruoc,
                ten_khach_hang_sau AS TenKhachHangSau,
                thoi_gian_ban_truoc AS ThoiGianBanTruoc,
                thoi_gian_ban_sau AS ThoiGianBanSau,
                du_lieu_truoc_json AS DuLieuTruocJson,
                du_lieu_sau_json AS DuLieuSauJson,
                chi_tiet_thay_doi AS ChiTietThayDoi
            FROM lich_su_chinh_sua_hoa_don
            WHERE ma_hoa_don = @MaHoaDon
            ORDER BY thoi_gian DESC, id DESC;
            """,
            new { MaHoaDon = maHoaDon.Trim() });

        return rows.ToList();
    }

    private sealed class HoaDonHuyHeaderRow
    {
        public uint HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = "";
        public uint ChiNhanhId { get; set; }
        public uint? KhachHangId { get; set; }
        public string MaKhachHang { get; set; } = "";
        public string TenKhachHang { get; set; } = "";
        public string SoDienThoai { get; set; } = "";
        public string DiaChi { get; set; } = "";
        public int InKemThongTin { get; set; }
        public DateTime ThoiGianBan { get; set; }
        public decimal TongTienHang { get; set; }
        public decimal GiamGia { get; set; }
        public decimal ThuKhac { get; set; }
        public decimal KhachCanTra { get; set; }
        public decimal KhachDaThanhToan { get; set; }
        public decimal ConNo { get; set; }
        public int CongNoDaGhiNhan { get; set; }
        public string TrangThaiThanhToan { get; set; } = "";
        public string TrangThaiHoaDon { get; set; } = "";
        public string? GhiChu { get; set; }
    }
}
