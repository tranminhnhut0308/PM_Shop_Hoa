using System;
using System.Collections.Generic;
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
    private static readonly SemaphoreSlim _khoaDamBaoSchemaHuyPhieuNhap = new(1, 1);
    private static bool _daDamBaoSchemaHuyPhieuNhap;

    /// <summary>
    /// Đảm bảo bảng phieu_nhap_hang có các cột lưu vết hủy/khôi phục và bảng kiểm toán lich_su_huy_phieu_nhap.
    /// </summary>
    public async Task<bool> EnsureHuyPhieuNhapSchemaAsync()
    {
        if (_daDamBaoSchemaHuyPhieuNhap)
            return true;

        await _khoaDamBaoSchemaHuyPhieuNhap.WaitAsync();
        try
        {
            if (_daDamBaoSchemaHuyPhieuNhap)
                return true;

            await using var connection = await OpenAsync();

            // 1. Thêm các cột hủy và khôi phục vào bảng phieu_nhap_hang
            static async Task AddColIfMissing(MySqlConnection conn, string tableName, string colName, string colDef)
            {
                var exists = await conn.ExecuteScalarAsync<long>(
                    """
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = @Table
                      AND COLUMN_NAME = @Column;
                    """,
                    new { Table = tableName, Column = colName });

                if (exists == 0)
                {
                    try
                    {
                        await conn.ExecuteAsync($"ALTER TABLE `{tableName}` ADD COLUMN `{colName}` {colDef};");
                    }
                    catch (Exception ex)
                    {
                        SafeLog.WriteError($"Loi them cot {colName} vao bang {tableName}", ex);
                    }
                }
            }

            await AddColIfMissing(connection, "phieu_nhap_hang", "ly_do_huy", "TEXT NULL");
            await AddColIfMissing(connection, "phieu_nhap_hang", "nguoi_huy_id", "INT(10) UNSIGNED NULL");
            await AddColIfMissing(connection, "phieu_nhap_hang", "ten_nguoi_huy", "VARCHAR(150) NULL");
            await AddColIfMissing(connection, "phieu_nhap_hang", "thoi_gian_huy", "DATETIME NULL");
            await AddColIfMissing(connection, "phieu_nhap_hang", "trang_thai_truoc_khi_huy", "VARCHAR(50) NULL");
            await AddColIfMissing(connection, "phieu_nhap_hang", "so_tien_hoan", "DECIMAL(15,2) NOT NULL DEFAULT '0.00'");
            await AddColIfMissing(connection, "phieu_nhap_hang", "phuong_an_tien_khi_huy", "VARCHAR(50) NULL");
            await AddColIfMissing(connection, "phieu_nhap_hang", "ma_phieu_thu_hoan", "VARCHAR(50) NULL");
            await AddColIfMissing(connection, "phieu_nhap_hang", "nguoi_khoi_phuc_id", "INT(10) UNSIGNED NULL");
            await AddColIfMissing(connection, "phieu_nhap_hang", "ten_nguoi_khoi_phuc", "VARCHAR(150) NULL");
            await AddColIfMissing(connection, "phieu_nhap_hang", "thoi_gian_khoi_phuc", "DATETIME NULL");
            await AddColIfMissing(connection, "phieu_nhap_hang", "ly_do_khoi_phuc", "TEXT NULL");

            // 2. Tạo bảng lưu vết kiểm toán bất biến
            const string createAuditSql = """
            CREATE TABLE IF NOT EXISTS `lich_su_huy_phieu_nhap` (
              `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT,
              `phieu_nhap_id` int(10) UNSIGNED NOT NULL,
              `ma_phieu_nhap` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
              `nha_cung_cap_id` int(10) UNSIGNED DEFAULT NULL,
              `ten_nha_cung_cap` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `tong_tien_nhap` decimal(15,2) NOT NULL DEFAULT '0.00',
              `da_thanh_toan` decimal(15,2) NOT NULL DEFAULT '0.00',
              `con_no` decimal(15,2) NOT NULL DEFAULT '0.00',
              `so_tien_hoan` decimal(15,2) NOT NULL DEFAULT '0.00',
              `ma_phieu_thu_hoan` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `hanh_dong` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HUY',
              `ly_do` text COLLATE utf8mb4_unicode_ci NOT NULL,
              `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
              `ten_nguoi_thuc_hien` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
              `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
              `du_lieu_snapshot` longtext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              PRIMARY KEY (`id`),
              KEY `idx_lshpn_phieu_nhap` (`phieu_nhap_id`),
              KEY `idx_lshpn_ma_phieu` (`ma_phieu_nhap`),
              KEY `idx_lshpn_thoi_gian` (`thoi_gian`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;
            await connection.ExecuteAsync(createAuditSql);

            _daDamBaoSchemaHuyPhieuNhap = true;
            return true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("EnsureHuyPhieuNhapSchemaAsync error", ex);
            return false;
        }
        finally
        {
            _khoaDamBaoSchemaHuyPhieuNhap.Release();
        }
    }

    /// <summary>
    /// Thẩm định điều kiện hủy phiếu nhập trước khi mở dialog xác nhận:
    /// Kiểm tra trạng thái phiếu, đọc danh sách mặt hàng và đối chiếu số lượng tồn kho hiện tại.
    /// Nếu có sản phẩm tồn kho hiện tại nhỏ hơn số lượng đã nhập (do đã bán/xuất kho),
    /// báo lỗi chi tiết để chặn hủy hoặc cảnh báo cho người dùng.
    /// </summary>
    public async Task<KiemTraHuyPhieuNhapResult> KiemTraDieuKienHuyPhieuNhapAsync(uint phieuNhapId)
    {
        if (!await EnsureHuyPhieuNhapSchemaAsync())
            throw new InvalidOperationException("Không thể chuẩn bị cấu trúc kiểm tra hủy phiếu nhập.");

        if (phieuNhapId == 0)
            throw new ArgumentException("Mã ID phiếu nhập không hợp lệ.", nameof(phieuNhapId));

        await using var connection = await OpenAsync();

        const string selectPhieuSql = """
        SELECT
            p.id                     AS PhieuNhapId,
            p.chi_nhanh_id           AS ChiNhanhId,
            p.ma_phieu_nhap          AS MaPhieuNhap,
            ncc.ten_nha_cung_cap     AS TenNhaCungCap,
            p.tong_tien_nhap         AS TongTienNhap,
            p.da_thanh_toan          AS DaThanhToan,
            p.con_no                 AS ConNo,
            p.trang_thai             AS TrangThai,
            COALESCE(p.phuong_thuc_thanh_toan, 'TIEN_MAT') AS PhuongThucThanhToan
        FROM phieu_nhap_hang p
        LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
        WHERE p.id = @PhieuNhapId
        LIMIT 1;
        """;

        var phieu = await connection.QueryFirstOrDefaultAsync<dynamic>(
            selectPhieuSql,
            new { PhieuNhapId = phieuNhapId });

        if (phieu is null)
        {
            return new KiemTraHuyPhieuNhapResult
            {
                CoTheHuy = false,
                LyDoKhongTheHuy = "Không tìm thấy phiếu nhập trong cơ sở dữ liệu."
            };
        }

        string trangThai = phieu.TrangThai ?? "";
        if (string.Equals(trangThai, "DA_HUY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trangThai, "HUY", StringComparison.OrdinalIgnoreCase))
        {
            return new KiemTraHuyPhieuNhapResult
            {
                PhieuNhapId = phieuNhapId,
                MaPhieuNhap = phieu.MaPhieuNhap ?? "",
                CoTheHuy = false,
                LyDoKhongTheHuy = "Phiếu nhập này đã ở trạng thái ĐÃ HỦY trước đó."
            };
        }

        if (string.Equals(trangThai, "LUU_TAM", StringComparison.OrdinalIgnoreCase))
        {
            return new KiemTraHuyPhieuNhapResult
            {
                PhieuNhapId = phieuNhapId,
                MaPhieuNhap = phieu.MaPhieuNhap ?? "",
                CoTheHuy = false,
                LyDoKhongTheHuy = "Phiếu nhập lưu tạm chưa cập nhật tồn kho hay công nợ, không cần quy trình hủy nghiệp vụ."
            };
        }

        uint branchId = phieu.ChiNhanhId != 0 ? (uint)phieu.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);

        const string selectItemsSql = """
        SELECT
            ct.san_pham_id  AS SanPhamId,
            ct.ma_san_pham  AS MaSanPham,
            ct.ten_san_pham AS TenSanPham,
            COALESCE(NULLIF(TRIM(ct.don_vi_tinh_excel), ''), dvt.ten_don_vi, 'Bó') AS DonViTinh,
            ct.so_luong     AS SoLuongNhap,
            COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) AS TonKhoHienTai
        FROM chi_tiet_phieu_nhap_hang ct
        LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
        LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
        LEFT JOIN ton_kho tk ON tk.san_pham_id = ct.san_pham_id AND tk.chi_nhanh_id = @BranchId
        WHERE ct.phieu_nhap_id = @PhieuNhapId
        ORDER BY ct.id;
        """;

        var items = (await connection.QueryAsync<MatHangKiemTraHuy>(
            selectItemsSql,
            new { PhieuNhapId = phieuNhapId, BranchId = branchId })).AsList();

        var result = new KiemTraHuyPhieuNhapResult
        {
            PhieuNhapId = phieuNhapId,
            MaPhieuNhap = phieu.MaPhieuNhap ?? "",
            TenNhaCungCap = phieu.TenNhaCungCap ?? "Không có nhà cung cấp",
            TongTienNhap = ToDecimal(phieu.TongTienNhap),
            DaThanhToan = ToDecimal(phieu.DaThanhToan),
            ConNo = ToDecimal(phieu.ConNo),
            PhuongThucThanhToan = phieu.PhuongThucThanhToan ?? "TIEN_MAT",
            MatHangs = items
        };

        if (result.CoMatHangThieuTonKho)
        {
            var thieuNames = string.Join(", ", result.MatHangs.Where(m => !m.DuTonKho).Select(m => $"{m.TenSanPham} (Tồn: {m.TonKhoHienTai:0.###}, Nhập: {m.SoLuongNhap:0.###})"));
            result.CoTheHuy = false;
            result.LyDoKhongTheHuy = $"Không thể hủy phiếu nhập vì số lượng tồn kho hiện tại không đủ để hoàn tác (hàng đã được bán hoặc xuất kho sau khi nhập): {thieuNames}";
        }

        return result;
    }

    /// <summary>
    /// Xử lý Hủy phiếu nhập hàng theo đúng nghiệp vụ kế toán và kho:
    /// 1. Tồn kho: Trừ số lượng tồn kho đã tăng từ phiếu nhập (kiểm tra tồn hiện tại, chặn tồn âm). Ghi nhật ký xuất kho.
    /// 2. Công nợ NCC: Hoàn tác giá trị công nợ phát sinh (ghi có sổ cái công nợ NCC). Không xóa lịch sử cũ.
    /// 3. Sổ quỹ & Thanh toán: Nếu DaThanhToan > 0 và chọn hoàn tiền -> tạo phiếu chi vào sổ quỹ, ghi đối ứng công nợ. Không xóa lịch sử thanh toán cũ.
    /// 4. Trạng thái phiếu: Cập nhật trang_thai = 'DA_HUY', lưu lý do, người hủy, thời gian hủy.
    /// 5. Ghi vết kiểm toán bất biến vào lich_su_huy_phieu_nhap và lich_su_chinh_sua_phieu_nhap.
    /// Toàn bộ thực hiện trong MỘT Transaction an toàn.
    /// </summary>
    public async Task HuyPhieuNhapHangAsync(HuyPhieuNhapYeuCau yeuCau)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.PurchasesDelete,
            "Bạn không có quyền hủy phiếu nhập hàng.");

        if (yeuCau is null)
            throw new ArgumentNullException(nameof(yeuCau));

        if (yeuCau.PhieuNhapId == 0)
            throw new ArgumentException("Mã ID phiếu nhập không hợp lệ.", nameof(yeuCau));

        if (string.IsNullOrWhiteSpace(yeuCau.LyDoHuy))
            throw new InvalidOperationException("Vui lòng nhập lý do hủy phiếu nhập hàng (bắt buộc).");

        if (!await EnsureHuyPhieuNhapSchemaAsync())
            throw new InvalidOperationException("Không thể chuẩn bị cấu trúc lưu lịch sử hủy phiếu nhập.");
        if (!await EnsureLichSuChinhSuaPhieuNhapTableAsync())
            throw new InvalidOperationException("Không thể chuẩn bị bảng lịch sử chỉnh sửa phiếu nhập.");
        var hasPhieuQuyExtraColumns = await EnsurePhieuQuyExtraColumnsAsync();
        var hasPhieuQuyPhieuNhapColumns = await EnsurePhieuQuyPhieuNhapColumnsAsync();

        var userId = yeuCau.NguoiHuyId ?? Session.CurrentUser?.Id;
        var userName = !string.IsNullOrWhiteSpace(yeuCau.TenNguoiHuy)
            ? yeuCau.TenNguoiHuy
            : (Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Hệ thống");
        var thoiGianHuy = DateTime.Now;

        if (yeuCau.PhuongAnTien is not ("HOAN_TIEN_MAT" or "HOAN_CHUYEN_KHOAN" or "TREO_CONG_NO"))
            throw new InvalidOperationException("Phương án xử lý tiền khi hủy phiếu nhập không hợp lệ.");

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Khóa và đọc phiếu nhập cũ
            const string selectPhieuSql = """
            SELECT
                p.id                     AS Id,
                p.chi_nhanh_id           AS ChiNhanhId,
                p.ma_phieu_nhap          AS MaPhieuNhap,
                p.nha_cung_cap_id        AS NhaCungCapId,
                ncc.ten_nha_cung_cap     AS TenNhaCungCap,
                p.thoi_gian_nhap         AS ThoiGianNhap,
                p.tong_tien_hang         AS TongTienHang,
                p.giam_gia               AS GiamGia,
                p.chi_phi_khac           AS ChiPhiKhac,
                p.tong_tien_nhap         AS TongTienNhap,
                p.da_thanh_toan          AS DaThanhToan,
                p.con_no                 AS ConNo,
                p.trang_thai             AS TrangThai,
                p.ghi_chu                AS GhiChu,
                COALESCE(p.phuong_thuc_thanh_toan, 'TIEN_MAT') AS PhuongThucThanhToan
            FROM phieu_nhap_hang p
            LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
            WHERE p.id = @Id
            LIMIT 1
            FOR UPDATE;
            """;

            var phieu = await connection.QueryFirstOrDefaultAsync<PhieuNhapHang>(
                selectPhieuSql,
                new { Id = yeuCau.PhieuNhapId },
                transaction);

            if (phieu is null)
                throw new InvalidOperationException($"Không tìm thấy phiếu nhập có ID {yeuCau.PhieuNhapId}.");

            if (string.Equals(phieu.TrangThai, "DA_HUY", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(phieu.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Phiếu nhập {phieu.MaPhieuNhap} đã bị hủy trước đó.");
            }
            if (string.Equals(phieu.TrangThai, "LUU_TAM", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Phiếu nhập lưu tạm chưa cập nhật tồn kho hay công nợ, không thể hủy theo quy trình nghiệp vụ.");

            var branchId = phieu.ChiNhanhId != 0 ? phieu.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);

            // 2. Đọc chi tiết mặt hàng
            const string selectItemsSql = """
            SELECT
                ct.id           AS Id,
                ct.san_pham_id  AS SanPhamId,
                ct.ma_san_pham  AS MaSanPham,
                ct.ten_san_pham AS TenSanPham,
                COALESCE(NULLIF(TRIM(ct.don_vi_tinh_excel), ''), dvt.ten_don_vi, 'Bó') AS DonViTinh,
                ct.so_luong     AS SoLuong,
                ct.don_gia_nhap AS DonGiaNhap,
                ct.giam_gia     AS GiamGia,
                ct.thanh_tien   AS ThanhTien
            FROM chi_tiet_phieu_nhap_hang ct
            LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            WHERE ct.phieu_nhap_id = @PhieuNhapId;
            """;

            var items = (await connection.QueryAsync<PhieuNhapItemEditModel>(
                selectItemsSql,
                new { PhieuNhapId = yeuCau.PhieuNhapId },
                transaction)).AsList();

            // 3. Xử lý tồn kho: Hoàn tác số lượng đã tăng từ phiếu nhập (trừ kho)
            const string selectTonSql = """
            SELECT so_luong_ton, gia_von_binh_quan
            FROM ton_kho
            WHERE chi_nhanh_id = @ChiNhanhId
              AND san_pham_id = @SanPhamId
            FOR UPDATE;
            """;

            const string upsertTonSql = """
            INSERT INTO ton_kho
            (
                chi_nhanh_id,
                san_pham_id,
                so_luong_ton,
                gia_von_binh_quan,
                ngay_cap_nhat
            )
            VALUES
            (
                @ChiNhanhId,
                @SanPhamId,
                @SoLuongTon,
                @GiaVonBinhQuan,
                NOW()
            )
            ON DUPLICATE KEY UPDATE
                so_luong_ton      = @SoLuongTon,
                gia_von_binh_quan = @GiaVonBinhQuan,
                ngay_cap_nhat     = NOW();
            """;

            const string updateSpSql = """
            UPDATE san_pham
            SET ton_kho_excel = @SoLuongTon,
                ngay_cap_nhat = NOW()
            WHERE id = @SanPhamId;
            """;

            const string insertLichSuKhoSql = """
            INSERT INTO lich_su_nhap_xuat_kho
            (
                chi_nhanh_id,
                san_pham_id,
                loai_giao_dich,
                loai_chung_tu,
                ma_chung_tu,
                so_luong,
                ton_truoc,
                ton_sau,
                gia_von,
                nguoi_thuc_hien_id,
                thoi_gian,
                ghi_chu
            )
            VALUES
            (
                @ChiNhanhId,
                @SanPhamId,
                'XUAT',
                'PHIEU_NHAP_HANG',
                @MaChungTu,
                @SoLuong,
                @TonTruoc,
                @TonSau,
                @GiaVon,
                @NguoiThucHienId,
                NOW(),
                @GhiChu
            );
            """;

            foreach (var item in items)
            {
                if (item.SanPhamId == 0 || item.SoLuong <= 0)
                    continue;

                var tonRow = await connection.QueryFirstOrDefaultAsync(
                    selectTonSql,
                    new { ChiNhanhId = branchId, SanPhamId = item.SanPhamId },
                    transaction);

                decimal currentTon = 0m;
                decimal currentGiaVon = item.DonGiaNhap;

                if (tonRow != null)
                {
                    var dict = (IDictionary<string, object>)tonRow;
                    currentTon = ToDecimal(dict.TryGetValue("so_luong_ton", out var st) ? st : null);
                    currentGiaVon = ToDecimal(dict.TryGetValue("gia_von_binh_quan", out var gv) ? gv : null);
                }
                else
                {
                    var spRow = await connection.QueryFirstOrDefaultAsync(
                        "SELECT COALESCE(ton_kho_excel, 0) AS TonKho, COALESCE(gia_von, 0) AS GiaVon FROM san_pham WHERE id = @Id LIMIT 1;",
                        new { Id = item.SanPhamId },
                        transaction);
                    if (spRow != null)
                    {
                        currentTon = ToDecimal(spRow.TonKho);
                        currentGiaVon = ToDecimal(spRow.GiaVon);
                    }
                }

                // Kiểm tra nếu hoàn tác làm tồn kho âm
                if (currentTon < item.SoLuong)
                {
                    throw new InvalidOperationException(
                        $"Không thể hủy phiếu nhập vì sản phẩm '{item.TenSanPham}' ({item.MaSanPham}) hiện tại chỉ còn tồn {currentTon:0.###} {item.DonViTinh}, không đủ để trừ {item.SoLuong:0.###} từ phiếu nhập (hàng đã được bán hoặc xuất kho).");
                }

                var newTon = currentTon - item.SoLuong;

                await connection.ExecuteAsync(
                    upsertTonSql,
                    new
                    {
                        ChiNhanhId = branchId,
                        SanPhamId = item.SanPhamId,
                        SoLuongTon = newTon,
                        GiaVonBinhQuan = currentGiaVon
                    },
                    transaction);

                await connection.ExecuteAsync(
                    updateSpSql,
                    new { SanPhamId = item.SanPhamId, SoLuongTon = newTon },
                    transaction);

                await connection.ExecuteAsync(
                    insertLichSuKhoSql,
                    new
                    {
                        ChiNhanhId = branchId,
                        SanPhamId = item.SanPhamId,
                        MaChungTu = phieu.MaPhieuNhap,
                        SoLuong = item.SoLuong,
                        TonTruoc = currentTon,
                        TonSau = newTon,
                        GiaVon = item.DonGiaNhap,
                        NguoiThucHienId = userId,
                        GhiChu = $"Hoàn tác tồn kho khi hủy phiếu nhập {phieu.MaPhieuNhap}. Lý do: {yeuCau.LyDoHuy.Trim()}"
                    },
                    transaction);
            }

            // 4. Công nợ nhà cung cấp: Ghi nhận giảm nợ đúng bằng giá trị phiếu nhập đã ghi nợ
            if (phieu.NhaCungCapId is uint nccId && nccId > 0 && phieu.TongTienNhap > 0)
            {
                await GhiMotDongCongNoNhaCungCapAsync(
                    connection,
                    transaction,
                    nccId,
                    branchId,
                    "HUY_NHAP_HANG",
                    phieu.MaPhieuNhap,
                    0, // phat_sinh_no
                    phieu.TongTienNhap, // phat_sinh_co: giảm nợ phải trả
                    DateTime.Now,
                    userId,
                    $"Hủy phiếu nhập {phieu.MaPhieuNhap} (giảm khoản nợ phải trả {phieu.TongTienNhap:N0} đ). Lý do: {yeuCau.LyDoHuy.Trim()}");
            }

            // 5. Thanh toán và sổ quỹ: Xử lý tiền đã thanh toán (nếu có)
            string? maPhieuChiHoan = null;
            decimal soTienHoan = 0m;

            if (phieu.DaThanhToan > 0 &&
                yeuCau.PhuongAnTien is "HOAN_TIEN_MAT" or "HOAN_CHUYEN_KHOAN")
            {
                soTienHoan = phieu.DaThanhToan;
                maPhieuChiHoan = await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);
                var phuongThucHoan = yeuCau.PhuongAnTien == "HOAN_CHUYEN_KHOAN"
                    ? "CHUYEN_KHOAN"
                    : "TIEN_MAT";
                var tenNcc = string.IsNullOrWhiteSpace(phieu.TenNhaCungCap)
                    ? "Nhà cung cấp"
                    : phieu.TenNhaCungCap;
                var idNhomChi = await LayIdNhomChiPhiTheoMaAsync(
                    connection,
                    transaction,
                    "CHI_NHA_CUNG_CAP");
                var noiDung = "Hoàn tiền khi hủy phiếu nhập hàng";
                var ghiChu = $"Hoàn tiền {soTienHoan:N0} đ cho nhà cung cấp {tenNcc} "
                    + $"khi hủy phiếu nhập {phieu.MaPhieuNhap}. Lý do: {yeuCau.LyDoHuy.Trim()}";

                var columns = new List<string>
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
                var values = new List<string>
                {
                    "@MaPhieuChi",
                    "@ChiNhanhId",
                    "@NhomChiPhiId",
                    "@SoTien",
                    "@PhuongThuc",
                    "@NoiDung",
                    "@ThoiGian",
                    "@NguoiThucHienId",
                    "'HOAN_THANH'"
                };

                if (hasPhieuQuyExtraColumns)
                {
                    columns.Add("nguoi_nop_nhan");
                    values.Add("@NguoiNopNhan");
                    columns.Add("ghi_chu");
                    values.Add("@GhiChu");
                }
                else
                {
                    noiDung += $" - {tenNcc}";
                }

                if (hasPhieuQuyPhieuNhapColumns)
                {
                    columns.Add("phieu_nhap_hang_id");
                    values.Add("@PhieuNhapId");
                    columns.Add("ma_phieu_nhap");
                    values.Add("@MaPhieuNhap");
                }
                else
                {
                    noiDung += $" - {phieu.MaPhieuNhap}";
                }

                await connection.ExecuteAsync(
                    $"""
                    INSERT INTO phieu_chi ({string.Join(", ", columns)})
                    VALUES ({string.Join(", ", values)});
                    """,
                    new
                    {
                        MaPhieuChi = maPhieuChiHoan,
                        ChiNhanhId = branchId,
                        NhomChiPhiId = idNhomChi,
                        SoTien = soTienHoan,
                        PhuongThuc = phuongThucHoan,
                        NoiDung = noiDung,
                        ThoiGian = thoiGianHuy,
                        NguoiThucHienId = userId,
                        NguoiNopNhan = tenNcc,
                        GhiChu = ghiChu,
                        PhieuNhapId = yeuCau.PhieuNhapId,
                        MaPhieuNhap = phieu.MaPhieuNhap
                    },
                    transaction);

                if (phieu.NhaCungCapId is uint nccId2 && nccId2 > 0)
                {
                    await GhiMotDongCongNoNhaCungCapAsync(
                        connection,
                        transaction,
                        nccId2,
                        branchId,
                        "HOAN_TIEN_NHAP",
                        phieu.MaPhieuNhap,
                        soTienHoan,
                        0,
                        thoiGianHuy,
                        userId,
                        $"Hoàn tiền {soTienHoan:N0} đ cho nhà cung cấp theo phiếu chi {maPhieuChiHoan}.");
                }
            }
            else if (phieu.DaThanhToan > 0)
            {
                // Nhà cung cấp giữ khoản trả trước; không phát sinh phiếu quỹ mới.
            }

            // 6. Cập nhật trạng thái phiếu nhập thành ĐÃ HỦY
            const string updatePhieuSql = """
            UPDATE phieu_nhap_hang
            SET trang_thai             = 'DA_HUY',
                trang_thai_truoc_khi_huy = @TrangThaiTruoc,
                ly_do_huy              = @LyDoHuy,
                nguoi_huy_id           = @NguoiHuyId,
                ten_nguoi_huy          = @TenNguoiHuy,
                thoi_gian_huy          = @ThoiGianHuy,
                phuong_an_tien_khi_huy = @PhuongAnTien,
                so_tien_hoan           = @SoTienHoan,
                ma_phieu_thu_hoan      = @MaPhieuThuHoan,
                ngay_cap_nhat          = @ThoiGianHuy
            WHERE id = @Id
              AND trang_thai NOT IN ('DA_HUY', 'HUY');
            """;

            var affected = await connection.ExecuteAsync(
                updatePhieuSql,
                new
                {
                    Id = yeuCau.PhieuNhapId,
                    TrangThaiTruoc = phieu.TrangThai,
                    LyDoHuy = yeuCau.LyDoHuy.Trim(),
                    NguoiHuyId = userId,
                    TenNguoiHuy = userName,
                    PhuongAnTien = yeuCau.PhuongAnTien,
                    SoTienHoan = soTienHoan,
                    MaPhieuThuHoan = maPhieuChiHoan,
                    ThoiGianHuy = thoiGianHuy
                },
                transaction);
            if (affected != 1)
                throw new InvalidOperationException("Phiếu nhập đã được hủy hoặc thay đổi trong lúc xử lý.");

            // 7. Ghi vết kiểm toán bất biến vào lich_su_huy_phieu_nhap
            var snapshotJson = JsonSerializer.Serialize(new
            {
                Phieu = phieu,
                Items = items,
                Yeucau = yeuCau,
                MaPhieuChiHoan = maPhieuChiHoan,
                SoTienHoan = soTienHoan
            });

            const string insertAuditSql = """
            INSERT INTO lich_su_huy_phieu_nhap
            (
                phieu_nhap_id, ma_phieu_nhap, nha_cung_cap_id, ten_nha_cung_cap,
                tong_tien_nhap, da_thanh_toan, con_no, so_tien_hoan,
                ma_phieu_thu_hoan, hanh_dong, ly_do, nguoi_thuc_hien_id,
                ten_nguoi_thuc_hien, thoi_gian, du_lieu_snapshot
            )
            VALUES
            (
                @PhieuNhapId, @MaPhieuNhap, @NhaCungCapId, @TenNhaCungCap,
                @TongTienNhap, @DaThanhToan, @ConNo, @SoTienHoan,
                @MaPhieuThuHoan, 'HUY', @LyDo, @NguoiThucHienId,
                @TenNguoiThucHien, NOW(), @DuLieuSnapshot
            );
            """;

            await connection.ExecuteAsync(
                insertAuditSql,
                new
                {
                    PhieuNhapId = yeuCau.PhieuNhapId,
                    MaPhieuNhap = phieu.MaPhieuNhap,
                    NhaCungCapId = phieu.NhaCungCapId,
                    TenNhaCungCap = phieu.TenNhaCungCap,
                    TongTienNhap = phieu.TongTienNhap,
                    DaThanhToan = phieu.DaThanhToan,
                    ConNo = phieu.ConNo,
                    SoTienHoan = soTienHoan,
                    MaPhieuThuHoan = maPhieuChiHoan,
                    LyDo = yeuCau.LyDoHuy.Trim(),
                    NguoiThucHienId = userId,
                    TenNguoiThucHien = userName,
                    DuLieuSnapshot = snapshotJson
                },
                transaction);

            // Ghi vào lich_su_chinh_sua_phieu_nhap để hiển thị trong tab Lịch sử của cửa sổ chi tiết
            const string insertLichSuChinhSuaSql = """
            INSERT INTO lich_su_chinh_sua_phieu_nhap
            (
                phieu_nhap_id, ma_phieu_nhap, thoi_gian, nguoi_thuc_hien_id,
                ten_nguoi_thuc_hien, ly_do, tong_tien_truoc, tong_tien_sau,
                da_thanh_toan_truoc, da_thanh_toan_sau, con_no_truoc, con_no_sau,
                chi_tiet_thay_doi
            )
            VALUES
            (
                @PhieuNhapId, @MaPhieuNhap, NOW(), @NguoiThucHienId,
                @TenNguoiThucHien, @LyDo, @TongTienNhap, 0,
                @DaThanhToan, 0, @ConNo, 0,
                @ChiTietThayDoi
            );
            """;

            await connection.ExecuteAsync(
                insertLichSuChinhSuaSql,
                new
                {
                    PhieuNhapId = yeuCau.PhieuNhapId,
                    MaPhieuNhap = phieu.MaPhieuNhap,
                    NguoiThucHienId = userId,
                    TenNguoiThucHien = userName,
                    LyDo = $"[HỦY PHIẾU NHẬP] {yeuCau.LyDoHuy.Trim()}",
                    TongTienNhap = phieu.TongTienNhap,
                    DaThanhToan = phieu.DaThanhToan,
                    ConNo = phieu.ConNo,
                    ChiTietThayDoi = $"• Trạng thái: {phieu.TrangThai} → ĐÃ HỦY\n• Đã hoàn tác {items.Count} mặt hàng trong kho\n• Hoàn tác công nợ NCC: {phieu.TongTienNhap:N0} đ" + (soTienHoan > 0 ? $"\n• Hoàn tiền: {soTienHoan:N0} đ (Phương án: {yeuCau.PhuongAnTien})" : "")
                },
                transaction);

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackEx)
            {
                SafeLog.WriteError("Không rollback được giao dịch hủy phiếu nhập", rollbackEx);
            }

            SafeLog.WriteError("Lỗi hủy phiếu nhập hàng", ex);
            throw;
        }
    }

    /// <summary>
    /// Khôi phục một phiếu nhập hàng đã bị hủy:
    /// 1. Kiểm tra trạng thái hiện tại (phải là DA_HUY).
    /// 2. Cộng lại tồn kho cho các sản phẩm.
    /// 3. Khôi phục công nợ nhà cung cấp.
    /// 4. Đảo giao dịch hoàn tiền vào đúng chiều Sổ quỹ (phiếu thu nếu lúc hủy đã tạo phiếu chi).
    /// 5. Đổi trạng thái phiếu về DA_NHAP_HANG (hoặc trạng thái trước khi hủy).
    /// 6. Ghi vết kiểm toán vào lich_su_huy_phieu_nhap và lich_su_chinh_sua_phieu_nhap.
    /// Toàn bộ thực hiện trong MỘT Transaction an toàn.
    /// </summary>
    public async Task KhoiPhucPhieuNhapHangAsync(KhoiPhucPhieuNhapYeuCau yeuCau)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.PurchasesCreate,
            "Bạn không có quyền khôi phục phiếu nhập hàng.");

        if (yeuCau is null)
            throw new ArgumentNullException(nameof(yeuCau));

        if (yeuCau.PhieuNhapId == 0)
            throw new ArgumentException("Mã ID phiếu nhập không hợp lệ.", nameof(yeuCau));

        if (!await EnsureHuyPhieuNhapSchemaAsync())
            throw new InvalidOperationException("Không thể chuẩn bị cấu trúc lưu lịch sử hủy phiếu nhập.");
        if (!await EnsureLichSuChinhSuaPhieuNhapTableAsync())
            throw new InvalidOperationException("Không thể chuẩn bị bảng lịch sử chỉnh sửa phiếu nhập.");
        var hasPhieuQuyExtraColumns = await EnsurePhieuQuyExtraColumnsAsync();
        var hasPhieuQuyPhieuNhapColumns = await EnsurePhieuQuyPhieuNhapColumnsAsync();

        var userId = yeuCau.NguoiKhoiPhucId ?? Session.CurrentUser?.Id;
        var userName = !string.IsNullOrWhiteSpace(yeuCau.TenNguoiKhoiPhuc)
            ? yeuCau.TenNguoiKhoiPhuc
            : (Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Hệ thống");

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Khóa và đọc phiếu nhập đã hủy
            const string selectPhieuSql = """
            SELECT
                p.id                     AS Id,
                p.chi_nhanh_id           AS ChiNhanhId,
                p.ma_phieu_nhap          AS MaPhieuNhap,
                p.nha_cung_cap_id        AS NhaCungCapId,
                ncc.ten_nha_cung_cap     AS TenNhaCungCap,
                p.thoi_gian_nhap         AS ThoiGianNhap,
                p.tong_tien_hang         AS TongTienHang,
                p.giam_gia               AS GiamGia,
                p.chi_phi_khac           AS ChiPhiKhac,
                p.tong_tien_nhap         AS TongTienNhap,
                p.da_thanh_toan          AS DaThanhToan,
                p.con_no                 AS ConNo,
                p.trang_thai             AS TrangThai,
                p.trang_thai_truoc_khi_huy AS TrangThaiTruocKhiHuy,
                p.so_tien_hoan           AS SoTienHoan,
                p.ma_phieu_thu_hoan      AS MaPhieuThuHoan,
                p.phuong_an_tien_khi_huy AS PhuongAnTienKhiHuy,
                COALESCE(p.phuong_thuc_thanh_toan, 'TIEN_MAT') AS PhuongThucThanhToan
            FROM phieu_nhap_hang p
            LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
            WHERE p.id = @Id
            LIMIT 1
            FOR UPDATE;
            """;

            var phieu = await connection.QueryFirstOrDefaultAsync<dynamic>(
                selectPhieuSql,
                new { Id = yeuCau.PhieuNhapId },
                transaction);

            if (phieu is null)
                throw new InvalidOperationException($"Không tìm thấy phiếu nhập có ID {yeuCau.PhieuNhapId}.");

            string trangThai = phieu.TrangThai ?? "";
            if (!string.Equals(trangThai, "DA_HUY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(trangThai, "HUY", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Phiếu nhập {phieu.MaPhieuNhap} không ở trạng thái ĐÃ HỦY, không thể khôi phục.");
            }

            uint branchId = phieu.ChiNhanhId != 0 ? (uint)phieu.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);
            decimal tongTienNhap = ToDecimal(phieu.TongTienNhap);
            decimal daThanhToan = ToDecimal(phieu.DaThanhToan);
            decimal conNo = ToDecimal(phieu.ConNo);
            decimal soTienHoan = ToDecimal(phieu.SoTienHoan);
            string? maPhieuThuHoan = phieu.MaPhieuThuHoan;
            string? phuongAnTienKhiHuy = (string?)phieu.PhuongAnTienKhiHuy;
            uint? nccId = phieu.NhaCungCapId != null ? (uint)phieu.NhaCungCapId : null;
            string maPhieuNhap = phieu.MaPhieuNhap ?? "";

            // 2. Đọc chi tiết mặt hàng
            const string selectItemsSql = """
            SELECT
                ct.san_pham_id  AS SanPhamId,
                ct.ma_san_pham  AS MaSanPham,
                ct.ten_san_pham AS TenSanPham,
                COALESCE(NULLIF(TRIM(ct.don_vi_tinh_excel), ''), dvt.ten_don_vi, 'Bó') AS DonViTinh,
                ct.so_luong     AS SoLuong,
                ct.don_gia_nhap AS DonGiaNhap,
                ct.thanh_tien   AS ThanhTien
            FROM chi_tiet_phieu_nhap_hang ct
            LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            WHERE ct.phieu_nhap_id = @PhieuNhapId;
            """;

            var items = (await connection.QueryAsync<PhieuNhapItemEditModel>(
                selectItemsSql,
                new { PhieuNhapId = yeuCau.PhieuNhapId },
                transaction)).AsList();

            // 3. Tồn kho: Cộng lại số lượng tồn kho
            const string selectTonSql = """
            SELECT so_luong_ton, gia_von_binh_quan
            FROM ton_kho
            WHERE chi_nhanh_id = @ChiNhanhId
              AND san_pham_id = @SanPhamId
            FOR UPDATE;
            """;

            const string upsertTonSql = """
            INSERT INTO ton_kho
            (
                chi_nhanh_id,
                san_pham_id,
                so_luong_ton,
                gia_von_binh_quan,
                ngay_cap_nhat
            )
            VALUES
            (
                @ChiNhanhId,
                @SanPhamId,
                @SoLuongTon,
                @GiaVonBinhQuan,
                NOW()
            )
            ON DUPLICATE KEY UPDATE
                so_luong_ton      = @SoLuongTon,
                gia_von_binh_quan = @GiaVonBinhQuan,
                ngay_cap_nhat     = NOW();
            """;

            const string updateSpSql = """
            UPDATE san_pham
            SET ton_kho_excel = @SoLuongTon,
                ngay_cap_nhat = NOW()
            WHERE id = @SanPhamId;
            """;

            const string insertLichSuKhoSql = """
            INSERT INTO lich_su_nhap_xuat_kho
            (
                chi_nhanh_id,
                san_pham_id,
                loai_giao_dich,
                loai_chung_tu,
                ma_chung_tu,
                so_luong,
                ton_truoc,
                ton_sau,
                gia_von,
                nguoi_thuc_hien_id,
                thoi_gian,
                ghi_chu
            )
            VALUES
            (
                @ChiNhanhId,
                @SanPhamId,
                'NHAP',
                'PHIEU_NHAP_HANG',
                @MaChungTu,
                @SoLuong,
                @TonTruoc,
                @TonSau,
                @GiaVon,
                @NguoiThucHienId,
                NOW(),
                @GhiChu
            );
            """;

            foreach (var item in items)
            {
                if (item.SanPhamId == 0 || item.SoLuong <= 0)
                    continue;

                var tonRow = await connection.QueryFirstOrDefaultAsync(
                    selectTonSql,
                    new { ChiNhanhId = branchId, SanPhamId = item.SanPhamId },
                    transaction);

                decimal currentTon = 0m;
                decimal currentGiaVon = item.DonGiaNhap;

                if (tonRow != null)
                {
                    var dict = (IDictionary<string, object>)tonRow;
                    currentTon = ToDecimal(dict.TryGetValue("so_luong_ton", out var st) ? st : null);
                    currentGiaVon = ToDecimal(dict.TryGetValue("gia_von_binh_quan", out var gv) ? gv : null);
                }
                else
                {
                    var spRow = await connection.QueryFirstOrDefaultAsync(
                        "SELECT COALESCE(ton_kho_excel, 0) AS TonKho, COALESCE(gia_von, 0) AS GiaVon FROM san_pham WHERE id = @Id LIMIT 1;",
                        new { Id = item.SanPhamId },
                        transaction);
                    if (spRow != null)
                    {
                        currentTon = ToDecimal(spRow.TonKho);
                        currentGiaVon = ToDecimal(spRow.GiaVon);
                    }
                }

                var newTon = currentTon + item.SoLuong;

                await connection.ExecuteAsync(
                    upsertTonSql,
                    new
                    {
                        ChiNhanhId = branchId,
                        SanPhamId = item.SanPhamId,
                        SoLuongTon = newTon,
                        GiaVonBinhQuan = currentGiaVon
                    },
                    transaction);

                await connection.ExecuteAsync(
                    updateSpSql,
                    new { SanPhamId = item.SanPhamId, SoLuongTon = newTon },
                    transaction);

                await connection.ExecuteAsync(
                    insertLichSuKhoSql,
                    new
                    {
                        ChiNhanhId = branchId,
                        SanPhamId = item.SanPhamId,
                        MaChungTu = maPhieuNhap,
                        SoLuong = item.SoLuong,
                        TonTruoc = currentTon,
                        TonSau = newTon,
                        GiaVon = item.DonGiaNhap,
                        NguoiThucHienId = userId,
                        GhiChu = $"Khôi phục tồn kho cho phiếu nhập {maPhieuNhap}. Lý do: {yeuCau.LyDoKhoiPhuc.Trim()}"
                    },
                    transaction);
            }

            // 4. Công nợ nhà cung cấp: Ghi nhận lại khoản nợ phải trả
            if (nccId is uint ncc && ncc > 0 && tongTienNhap > 0)
            {
                await GhiMotDongCongNoNhaCungCapAsync(
                    connection,
                    transaction,
                    ncc,
                    branchId,
                    "KHOI_PHUC_NHAP_HANG",
                    maPhieuNhap,
                    tongTienNhap, // phat_sinh_no: tăng lại nợ phải trả
                    0,            // phat_sinh_co
                    DateTime.Now,
                    userId,
                    $"Khôi phục phiếu nhập {maPhieuNhap} (khôi phục khoản nợ {tongTienNhap:N0} đ). Lý do: {yeuCau.LyDoKhoiPhuc.Trim()}");
            }

            // 5. Đảo phiếu quỹ hoàn tiền do lần hủy đã tạo.
            if (soTienHoan > 0 && !string.IsNullOrWhiteSpace(maPhieuThuHoan))
            {
                var giaoDichHuyLaPhieuChi = maPhieuThuHoan.StartsWith("PC", StringComparison.OrdinalIgnoreCase);
                var maPhieuQuyMoi = giaoDichHuyLaPhieuChi
                    ? await SinhMaPhieuThuTrongGiaoDichAsync(connection, transaction)
                    : await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);
                var tenNcc = phieu.TenNhaCungCap ?? "Nhà cung cấp";
                var phuongThuc = phuongAnTienKhiHuy == "HOAN_CHUYEN_KHOAN"
                    ? "CHUYEN_KHOAN"
                    : "TIEN_MAT";
                var loaiPhieu = giaoDichHuyLaPhieuChi ? "THU" : "CHI";
                var coCotLoaiPhieu = await connection.ExecuteScalarAsync<bool>(
                    """
                    SELECT COUNT(*) > 0
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'phieu_chi'
                      AND COLUMN_NAME = 'loai_phieu';
                    """,
                    transaction: transaction);
                var columns = new List<string>
                {
                    "ma_phieu_chi",
                    "chi_nhanh_id",
                    "so_tien",
                    "phuong_thuc",
                    "noi_dung",
                    "thoi_gian_chi",
                    "nguoi_thuc_hien_id",
                    "trang_thai"
                };
                var values = new List<string>
                {
                    "@MaPhieuChi",
                    "@ChiNhanhId",
                    "@SoTien",
                    "@PhuongThuc",
                    "@NoiDung",
                    "NOW()",
                    "@NguoiThucHienId",
                    "'HOAN_THANH'"
                };
                if (coCotLoaiPhieu)
                {
                    columns.Add("loai_phieu");
                    values.Add("@LoaiPhieu");
                }
                if (hasPhieuQuyExtraColumns)
                {
                    columns.Add("nguoi_nop_nhan");
                    values.Add("@NguoiNopNhan");
                    columns.Add("ghi_chu");
                    values.Add("@GhiChu");
                }
                if (hasPhieuQuyPhieuNhapColumns)
                {
                    columns.Add("phieu_nhap_hang_id");
                    values.Add("@PhieuNhapId");
                    columns.Add("ma_phieu_nhap");
                    values.Add("@MaPhieuNhap");
                }

                await connection.ExecuteAsync(
                    $"""
                    INSERT INTO phieu_chi ({string.Join(", ", columns)})
                    VALUES ({string.Join(", ", values)});
                    """,
                    new
                    {
                        MaPhieuChi = maPhieuQuyMoi,
                        ChiNhanhId = branchId,
                        SoTien = soTienHoan,
                        PhuongThuc = phuongThuc,
                        NoiDung = giaoDichHuyLaPhieuChi
                            ? $"Nhận lại tiền hoàn khi khôi phục phiếu nhập {maPhieuNhap}"
                            : $"Chi trả lại tiền cho nhà cung cấp khi khôi phục phiếu nhập {maPhieuNhap}",
                        LoaiPhieu = loaiPhieu,
                        NguoiNopNhan = tenNcc,
                        GhiChu = $"Đảo phiếu quỹ {maPhieuThuHoan} ({soTienHoan:N0} đ) khi khôi phục phiếu nhập {maPhieuNhap}. Lý do: {yeuCau.LyDoKhoiPhuc.Trim()}",
                        NguoiThucHienId = userId,
                        PhieuNhapId = yeuCau.PhieuNhapId,
                        MaPhieuNhap = maPhieuNhap
                    },
                    transaction);

                // Ghi giảm nợ NCC tương ứng khoản chi lại này
                if (nccId is uint ncc2 && ncc2 > 0)
                {
                    await GhiMotDongCongNoNhaCungCapAsync(
                        connection,
                        transaction,
                        ncc2,
                        branchId,
                        "THANH_TOAN_NHAP",
                        maPhieuNhap,
                        0,          // phat_sinh_no
                        soTienHoan, // phat_sinh_co: giảm nợ
                        DateTime.Now,
                        userId,
                        $"Đảo giao dịch hoàn tiền {soTienHoan:N0} đ theo phiếu quỹ {maPhieuQuyMoi}");
                }
            }

            // 6. Đổi trạng thái phiếu nhập về DA_NHAP_HANG
            var trangThaiMoi = !string.IsNullOrWhiteSpace((string?)phieu.TrangThaiTruocKhiHuy)
                ? (string)phieu.TrangThaiTruocKhiHuy
                : "DA_NHAP_HANG";

            const string updatePhieuSql = """
            UPDATE phieu_nhap_hang
            SET trang_thai          = @TrangThaiMoi,
                nguoi_khoi_phuc_id  = @NguoiKhoiPhucId,
                ten_nguoi_khoi_phuc = @TenNguoiKhoiPhuc,
                thoi_gian_khoi_phuc = NOW(),
                ly_do_khoi_phuc     = @LyDoKhoiPhuc,
                ngay_cap_nhat       = NOW()
            WHERE id = @Id;
            """;

            await connection.ExecuteAsync(
                updatePhieuSql,
                new
                {
                    Id = yeuCau.PhieuNhapId,
                    TrangThaiMoi = trangThaiMoi,
                    NguoiKhoiPhucId = userId,
                    TenNguoiKhoiPhuc = userName,
                    LyDoKhoiPhuc = yeuCau.LyDoKhoiPhuc.Trim()
                },
                transaction);

            // 7. Ghi vết kiểm toán vào lich_su_huy_phieu_nhap và lich_su_chinh_sua_phieu_nhap
            const string insertAuditSql = """
            INSERT INTO lich_su_huy_phieu_nhap
            (
                phieu_nhap_id, ma_phieu_nhap, nha_cung_cap_id, ten_nha_cung_cap,
                tong_tien_nhap, da_thanh_toan, con_no, so_tien_hoan,
                ma_phieu_thu_hoan, hanh_dong, ly_do, nguoi_thuc_hien_id,
                ten_nguoi_thuc_hien, thoi_gian, du_lieu_snapshot
            )
            VALUES
            (
                @PhieuNhapId, @MaPhieuNhap, @NhaCungCapId, @TenNhaCungCap,
                @TongTienNhap, @DaThanhToan, @ConNo, @SoTienHoan,
                @MaPhieuThuHoan, 'KHOI_PHUC', @LyDo, @NguoiThucHienId,
                @TenNguoiThucHien, NOW(), @DuLieuSnapshot
            );
            """;

            await connection.ExecuteAsync(
                insertAuditSql,
                new
                {
                    PhieuNhapId = yeuCau.PhieuNhapId,
                    MaPhieuNhap = maPhieuNhap,
                    NhaCungCapId = nccId,
                    TenNhaCungCap = (string?)phieu.TenNhaCungCap,
                    TongTienNhap = tongTienNhap,
                    DaThanhToan = daThanhToan,
                    ConNo = conNo,
                    SoTienHoan = soTienHoan,
                    MaPhieuThuHoan = maPhieuThuHoan,
                    LyDo = yeuCau.LyDoKhoiPhuc.Trim(),
                    NguoiThucHienId = userId,
                    TenNguoiThucHien = userName,
                    DuLieuSnapshot = JsonSerializer.Serialize(new { yeuCau, phieu })
                },
                transaction);

            const string insertLichSuChinhSuaSql = """
            INSERT INTO lich_su_chinh_sua_phieu_nhap
            (
                phieu_nhap_id, ma_phieu_nhap, thoi_gian, nguoi_thuc_hien_id,
                ten_nguoi_thuc_hien, ly_do, tong_tien_truoc, tong_tien_sau,
                da_thanh_toan_truoc, da_thanh_toan_sau, con_no_truoc, con_no_sau,
                chi_tiet_thay_doi
            )
            VALUES
            (
                @PhieuNhapId, @MaPhieuNhap, NOW(), @NguoiThucHienId,
                @TenNguoiThucHien, @LyDo, 0, @TongTienNhap,
                0, @DaThanhToan, 0, @ConNo,
                @ChiTietThayDoi
            );
            """;

            await connection.ExecuteAsync(
                insertLichSuChinhSuaSql,
                new
                {
                    PhieuNhapId = yeuCau.PhieuNhapId,
                    MaPhieuNhap = maPhieuNhap,
                    NguoiThucHienId = userId,
                    TenNguoiThucHien = userName,
                    LyDo = $"[KHÔI PHỤC PHIẾU NHẬP] {yeuCau.LyDoKhoiPhuc.Trim()}",
                    TongTienNhap = tongTienNhap,
                    DaThanhToan = daThanhToan,
                    ConNo = conNo,
                    ChiTietThayDoi = $"• Trạng thái: ĐÃ HỦY → {trangThaiMoi}\n• Đã khôi phục {items.Count} mặt hàng vào kho\n• Khôi phục công nợ NCC: {tongTienNhap:N0} đ"
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
