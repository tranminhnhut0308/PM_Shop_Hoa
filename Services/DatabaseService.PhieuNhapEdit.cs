using System.Text.Json;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    private static readonly SemaphoreSlim _khoaDamBaoBangLichSuChinhSuaPhieuNhap = new(1, 1);
    private static bool _daDamBaoBangLichSuChinhSuaPhieuNhap;

    /// <summary>
    /// Đảm bảo bảng lich_su_chinh_sua_phieu_nhap tồn tại trong database.
    /// </summary>
    public async Task<bool> EnsureLichSuChinhSuaPhieuNhapTableAsync()
    {
        if (_daDamBaoBangLichSuChinhSuaPhieuNhap)
            return true;

        await _khoaDamBaoBangLichSuChinhSuaPhieuNhap.WaitAsync();
        try
        {
            if (_daDamBaoBangLichSuChinhSuaPhieuNhap)
                return true;

            await using var connection = await OpenAsync();
            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS `lich_su_chinh_sua_phieu_nhap` (
                  `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT,
                  `phieu_nhap_id` int(10) UNSIGNED NOT NULL,
                  `ma_phieu_nhap` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
                  `ten_nguoi_thuc_hien` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
                  `ly_do` text COLLATE utf8mb4_unicode_ci NOT NULL,
                  `tong_tien_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `tong_tien_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `chi_phi_khac_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `chi_phi_khac_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `giam_gia_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `giam_gia_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `da_thanh_toan_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `da_thanh_toan_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `con_no_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `con_no_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
                  `nha_cung_cap_id_truoc` int(10) UNSIGNED DEFAULT NULL,
                  `nha_cung_cap_id_sau` int(10) UNSIGNED DEFAULT NULL,
                  `ten_nha_cung_cap_truoc` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                  `ten_nha_cung_cap_sau` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                  `thoi_gian_nhap_truoc` datetime DEFAULT NULL,
                  `thoi_gian_nhap_sau` datetime DEFAULT NULL,
                  `du_lieu_truoc_json` longtext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                  `du_lieu_sau_json` longtext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                  `chi_tiet_thay_doi` text COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                  PRIMARY KEY (`id`),
                  KEY `idx_lscpnh_phieu_nhap` (`phieu_nhap_id`),
                  KEY `idx_lscpnh_ma_phieu` (`ma_phieu_nhap`),
                  KEY `idx_lscpnh_thoi_gian` (`thoi_gian`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                """);

            _daDamBaoBangLichSuChinhSuaPhieuNhap = true;
            return true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("EnsureLichSuChinhSuaPhieuNhapTableAsync error", ex);
            return false;
        }
        finally
        {
            _khoaDamBaoBangLichSuChinhSuaPhieuNhap.Release();
        }
    }

    /// <summary>
    /// Lấy danh sách lịch sử chỉnh sửa của một phiếu nhập.
    /// </summary>
    public async Task<List<LichSuChinhSuaPhieuNhap>> GetLichSuChinhSuaPhieuNhapAsync(uint phieuNhapId)
    {
        if (phieuNhapId == 0)
            return [];

        await EnsureLichSuChinhSuaPhieuNhapTableAsync();
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            id                     AS Id,
            phieu_nhap_id          AS PhieuNhapId,
            ma_phieu_nhap          AS MaPhieuNhap,
            thoi_gian              AS ThoiGian,
            nguoi_thuc_hien_id     AS NguoiThucHienId,
            ten_nguoi_thuc_hien    AS TenNguoiThucHien,
            ly_do                  AS LyDo,
            tong_tien_truoc        AS TongTienTruoc,
            tong_tien_sau          AS TongTienSau,
            chi_phi_khac_truoc     AS ChiPhiKhacTruoc,
            chi_phi_khac_sau       AS ChiPhiKhacSau,
            giam_gia_truoc         AS GiamGiaTruoc,
            giam_gia_sau           AS GiamGiaSau,
            da_thanh_toan_truoc    AS DaThanhToanTruoc,
            da_thanh_toan_sau      AS DaThanhToanSau,
            con_no_truoc           AS ConNoTruoc,
            con_no_sau             AS ConNoSau,
            nha_cung_cap_id_truoc  AS NhaCungCapIdTruoc,
            nha_cung_cap_id_sau    AS NhaCungCapIdSau,
            ten_nha_cung_cap_truoc AS TenNhaCungCapTruoc,
            ten_nha_cung_cap_sau   AS TenNhaCungCapSau,
            thoi_gian_nhap_truoc   AS ThoiGianNhapTruoc,
            thoi_gian_nhap_sau     AS ThoiGianNhapSau,
            du_lieu_truoc_json     AS DuLieuTruocJson,
            du_lieu_sau_json       AS DuLieuSauJson,
            chi_tiet_thay_doi      AS ChiTietThayDoi
        FROM lich_su_chinh_sua_phieu_nhap
        WHERE phieu_nhap_id = @PhieuNhapId
        ORDER BY thoi_gian DESC, id DESC;
        """;

        var rows = await connection.QueryAsync<LichSuChinhSuaPhieuNhap>(sql, new { PhieuNhapId = phieuNhapId });
        return rows.AsList();
    }

    /// <summary>
    /// Đọc dữ liệu phiếu nhập để nạp vào cửa sổ chỉnh sửa.
    /// </summary>
    public async Task<PhieuNhapEditRequest?> GetPhieuNhapDeChinhSuaAsync(uint phieuNhapId)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.PurchasesEdit,
            "Bạn không có quyền chỉnh sửa phiếu nhập hàng.");

        if (phieuNhapId == 0)
            return null;

        await using var connection = await OpenAsync();

        var hasExtraColumns = await HasPhieuNhapExtraColumnsAsync();
        var hasPhuongThuc = await EnsurePhieuNhapPhuongThucColumnAsync();

        var extraSelect = (hasExtraColumns
            ? ",\n            p.so_hoa_don_dau_vao AS SoHoaDonDauVao,\n            p.ma_dat_hang_nhap   AS MaDatHangNhap,\n            p.ngay_hoa_don_dau_vao AS NgayHoaDonDauVao,\n            p.ten_nguoi_giao_hang AS TenNguoiGiaoHang,\n            p.sdt_nguoi_giao_hang AS SdtNguoiGiaoHang"
            : string.Empty)
            + (hasPhuongThuc
            ? ",\n            COALESCE(p.phuong_thuc_thanh_toan, 'TIEN_MAT') AS PhuongThucThanhToan"
            : string.Empty);

        var headerSql = $"""
        SELECT
            p.id                 AS PhieuNhapId,
            p.ma_phieu_nhap      AS MaPhieuNhap,
            p.chi_nhanh_id       AS ChiNhanhId,
            p.nha_cung_cap_id    AS NhaCungCapId,
            ncc.ma_nha_cung_cap  AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            p.thoi_gian_nhap     AS ThoiGianNhap,
            p.giam_gia           AS GiamGia,
            p.chi_phi_khac       AS ChiPhiKhac,
            p.da_thanh_toan      AS DaThanhToan,
            p.ghi_chu            AS GhiChu{extraSelect}
        FROM phieu_nhap_hang p
        LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
        WHERE p.id = @PhieuNhapId
        LIMIT 1;
        """;

        var request = await connection.QueryFirstOrDefaultAsync<PhieuNhapEditRequest>(headerSql, new { PhieuNhapId = phieuNhapId });
        if (request is null)
            return null;

        const string itemsSql = """
        SELECT
            ct.id           AS Id,
            ct.san_pham_id  AS SanPhamId,
            ct.ma_san_pham  AS MaSanPham,
            ct.ten_san_pham AS TenSanPham,
            sp.don_vi_tinh_id AS DonViTinhId,
            COALESCE(NULLIF(TRIM(ct.don_vi_tinh_excel), ''), dvt.ten_don_vi, '') AS DonViTinh,
            ct.so_luong     AS SoLuong,
            ct.don_gia_nhap AS DonGiaNhap,
            ct.giam_gia     AS GiamGia,
            ct.ghi_chu      AS GhiChu
        FROM chi_tiet_phieu_nhap_hang ct
        LEFT JOIN san_pham    sp  ON sp.id  = ct.san_pham_id
        LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
        WHERE ct.phieu_nhap_id = @PhieuNhapId
        ORDER BY ct.id;
        """;

        var items = (await connection.QueryAsync<PhieuNhapItemEditModel>(itemsSql, new { PhieuNhapId = phieuNhapId })).AsList();
        foreach (var item in items)
        {
            item.SoLuongBanDau = item.SoLuong;
            item.DonGiaNhapBanDau = item.DonGiaNhap;
            item.GiamGiaBanDau = item.GiamGia;
        }

        request.Items = items;
        return request;
    }

    /// <summary>
    /// Lấy danh sách nhà cung cấp dạng SupplierOption dùng cho lựa chọn nhanh.
    /// </summary>
    public async Task<List<SupplierOption>> GetSupplierOptionsAsync()
    {
        var nccs = await GetNhaCungCapAsync();
        return nccs.Select(n => new SupplierOption
        {
            Id = n.Id,
            MaNhaCungCap = n.MaNhaCungCap,
            TenNhaCungCap = n.TenNhaCungCap,
            SoDienThoai = n.SoDienThoai
        }).OrderBy(n => n.TenNhaCungCap, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Cập nhật thông tin phiếu nhập hàng:
    /// - Điều chỉnh tồn kho theo độ chênh lệch (delta = số lượng mới - số lượng cũ).
    /// - Ghi lịch sử nhập/xuất kho tương ứng.
    /// - Cập nhật chi_tiet_phieu_nhap_hang.
    /// - Cập nhật phieu_nhap_hang (tiền hàng, chi phí, giảm giá, công nợ, nhà cung cấp, ngày nhập).
    /// - Cập nhật sổ cái công nợ nhà cung cấp (lich_su_cong_no_nha_cung_cap).
    /// - Cập nhật phiếu chi sổ quỹ (phieu_chi).
    /// - Ghi vết kiểm toán vào lich_su_chinh_sua_phieu_nhap.
    /// Tất cả thực hiện trong MỘT Transaction an toàn.
    /// </summary>
    internal static void ValidatePhieuNhapEditRequest(PhieuNhapEditRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.PhieuNhapId == 0)
            throw new ArgumentException("Mã ID phiếu nhập không hợp lệ.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.LyDoChinhSua))
            request.LyDoChinhSua = "Không cung cấp lý do";
        else
            request.LyDoChinhSua = request.LyDoChinhSua.Trim();

        var dongHopLe = request.Items.Where(i => i.SanPhamId != 0 && i.SoLuong > 0).ToList();
        if (dongHopLe.Count == 0)
            throw new InvalidOperationException("Phiếu nhập phải có ít nhất 1 mặt hàng với số lượng lớn hơn 0.");
    }

    public async Task CapNhatPhieuNhapHangAsync(PhieuNhapEditRequest request)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.PurchasesEdit,
            "Bạn không có quyền chỉnh sửa phiếu nhập hàng.");

        ValidatePhieuNhapEditRequest(request);
        var dongHopLe = request.Items.Where(i => i.SanPhamId != 0 && i.SoLuong > 0).ToList();

        if (!await EnsureLichSuChinhSuaPhieuNhapTableAsync())
            throw new InvalidOperationException("Không thể chuẩn bị bảng nhật ký phiếu nhập; thay đổi chưa được lưu.");
        if (request.ChungTus is { Count: > 0 } && !await EnsureDinhKemPhieuNhapTableAsync())
            throw new InvalidOperationException("Không thể chuẩn bị bảng chứng từ phiếu nhập; thay đổi chưa được lưu.");
        var hasPhuongThucColumn = await EnsurePhieuNhapPhuongThucColumnAsync();
        var hasExtraColumns = await HasPhieuNhapExtraColumnsAsync();
        var hasDvtExcel = await EnsureChiTietPhieuNhapDonViTinhExcelAsync();
        var hasPhieuQuyExtraColumns = await EnsurePhieuQuyExtraColumnsAsync();
        var hasPhieuQuyPhieuNhapColumns = await EnsurePhieuQuyPhieuNhapColumnsAsync();
        await EnsureAuditTableAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var attachmentCleanup = new AttachmentSaveCleanup();

        try
        {
            // 1. KHÓA PHIẾU NHẬP ĐỂ ĐỌC DỮ LIỆU CŨ
            // Chỉ select các cột mở rộng thực sự tồn tại; database thiếu
            // ma_dat_hang_nhap từng làm cả câu UPDATE bị lỗi unknown column.
            var oldExtraSelect = (hasPhuongThucColumn
                    ? ",\n                p.phuong_thuc_thanh_toan AS PhuongThucThanhToan"
                    : string.Empty)
                + (hasExtraColumns
                    ? ",\n                p.so_hoa_don_dau_vao     AS SoHoaDonDauVao,"
                      + "\n                p.ngay_hoa_don_dau_vao   AS NgayHoaDonDauVao,"
                      + "\n                p.ten_nguoi_giao_hang    AS TenNguoiGiaoHang,"
                      + "\n                p.sdt_nguoi_giao_hang    AS SdtNguoiGiaoHang,"
                      + "\n                p.ma_dat_hang_nhap       AS MaDatHangNhap"
                    : string.Empty);

            var selectOldPhieuSql = $"""
            SELECT
                p.id                     AS Id,
                p.ma_phieu_nhap          AS MaPhieuNhap,
                p.chi_nhanh_id           AS ChiNhanhId,
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
                p.ghi_chu                AS GhiChu{oldExtraSelect}
            FROM phieu_nhap_hang p
            LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
            WHERE p.id = @Id
            FOR UPDATE;
            """;

            var oldPhieu = await connection.QueryFirstOrDefaultAsync<PhieuNhapHang>(
                selectOldPhieuSql,
                new { Id = request.PhieuNhapId },
                transaction);

            if (oldPhieu is null)
                throw new InvalidOperationException($"Không tìm thấy phiếu nhập có ID {request.PhieuNhapId}.");

            if (string.Equals(oldPhieu.TrangThai, "DA_HUY", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(oldPhieu.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Phiếu nhập này đã bị hủy, không thể chỉnh sửa.");
            }

            var branchId = oldPhieu.ChiNhanhId != 0 ? oldPhieu.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);

            // 2. ĐỌC CÁC DÒNG CŨ ĐỂ TÍNH DELTA TỒN KHO
            const string selectOldItemsSql = """
            SELECT
                id           AS Id,
                san_pham_id  AS SanPhamId,
                ma_san_pham  AS MaSanPham,
                ten_san_pham AS TenSanPham,
                so_luong     AS SoLuong,
                don_gia_nhap AS DonGiaNhap,
                giam_gia     AS GiamGia,
                thanh_tien   AS ThanhTien
            FROM chi_tiet_phieu_nhap_hang
            WHERE phieu_nhap_id = @PhieuNhapId;
            """;

            var oldItems = (await connection.QueryAsync<PhieuNhapItemEditModel>(
                selectOldItemsSql,
                new { PhieuNhapId = request.PhieuNhapId },
                transaction)).AsList();

            var oldItemsMap = oldItems.ToDictionary(i => i.SanPhamId);
            var newItemsMap = dongHopLe.ToDictionary(i => i.SanPhamId);
            var allProductIds = oldItemsMap.Keys.Union(newItemsMap.Keys).Distinct().ToList();

            var thayDoiLines = new List<string>();

            // 3. ĐIỀU CHỈNH TỒN KHO THEO DELTA
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
                @LoaiGiaoDich,
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

            var auditTruong = new List<string>();
            var auditTruoc = new List<string>();
            var auditSau = new List<string>();

            foreach (var spId in allProductIds)
            {
                var oldQty = oldItemsMap.TryGetValue(spId, out var oItem) ? oItem.SoLuong : 0m;
                var newQty = newItemsMap.TryGetValue(spId, out var nItem) ? nItem.SoLuong : 0m;
                var delta = newQty - oldQty;

                var spName = nItem?.TenSanPham ?? oItem?.TenSanPham ?? $"SP #{spId}";
                var unitPrice = nItem?.DonGiaNhap ?? oItem?.DonGiaNhap ?? 0m;

                if (delta != 0m)
                {
                    // Lấy tồn kho hiện tại
                    var tonRow = await connection.QueryFirstOrDefaultAsync(
                        selectTonSql,
                        new { ChiNhanhId = branchId, SanPhamId = spId },
                        transaction);

                    decimal currentTon = 0m;
                    decimal currentGiaVon = unitPrice;

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
                            new { Id = spId },
                            transaction);
                        if (spRow != null)
                        {
                            currentTon = ToDecimal(spRow.TonKho);
                            currentGiaVon = ToDecimal(spRow.GiaVon);
                        }
                    }

                    var newTon = currentTon + delta;

                    // Cập nhật tồn kho
                    await connection.ExecuteAsync(
                        upsertTonSql,
                        new
                        {
                            ChiNhanhId = branchId,
                            SanPhamId = spId,
                            SoLuongTon = newTon,
                            GiaVonBinhQuan = currentGiaVon
                        },
                        transaction);

                    await connection.ExecuteAsync(
                        updateSpSql,
                        new { SanPhamId = spId, SoLuongTon = newTon },
                        transaction);

                    // Ghi vết kho
                    await connection.ExecuteAsync(
                        insertLichSuKhoSql,
                        new
                        {
                            ChiNhanhId = branchId,
                            SanPhamId = spId,
                            LoaiGiaoDich = delta > 0 ? "NHAP" : "XUAT",
                            MaChungTu = oldPhieu.MaPhieuNhap,
                            SoLuong = Math.Abs(delta),
                            TonTruoc = currentTon,
                            TonSau = newTon,
                            GiaVon = unitPrice,
                            NguoiThucHienId = Session.CurrentUser?.Id,
                            GhiChu = $"Điều chỉnh khi sửa phiếu nhập {oldPhieu.MaPhieuNhap} (SL: {oldQty:0.###} -> {newQty:0.###})"
                        },
                        transaction);

                    thayDoiLines.Add($"• {spName}: Số lượng {oldQty:0.###} → {newQty:0.###} ({(delta > 0 ? "+" : "")}{delta:0.###})");
                    auditTruong.Add($"SL ({spName})");
                    auditTruoc.Add($"Số lượng cũ ({spName}): {oldQty:0.###}");
                    auditSau.Add($"Số lượng mới ({spName}): {newQty:0.###}");
                }

                if (oItem != null && nItem != null && oItem.DonGiaNhap != nItem.DonGiaNhap)
                {
                    thayDoiLines.Add($"• {spName}: Đơn giá {oItem.DonGiaNhap:N0} đ → {nItem.DonGiaNhap:N0} đ");
                    auditTruong.Add($"Giá nhập ({spName})");
                    auditTruoc.Add($"Giá nhập cũ ({spName}): {oItem.DonGiaNhap:N0} đ");
                    auditSau.Add($"Giá nhập mới ({spName}): {nItem.DonGiaNhap:N0} đ");
                }

                if (oItem != null && nItem != null && oItem.GiamGia != nItem.GiamGia)
                {
                    thayDoiLines.Add($"• {spName}: Giảm giá {oItem.GiamGia:N0} đ → {nItem.GiamGia:N0} đ");
                    auditTruong.Add($"Giảm giá SP ({spName})");
                    auditTruoc.Add($"Giảm giá cũ ({spName}): {oItem.GiamGia:N0} đ");
                    auditSau.Add($"Giảm giá mới ({spName}): {nItem.GiamGia:N0} đ");
                }
            }

            // 4. CẬP NHẬT CHI TIẾT PHIẾU NHẬP
            await connection.ExecuteAsync(
                "DELETE FROM chi_tiet_phieu_nhap_hang WHERE phieu_nhap_id = @PhieuNhapId;",
                new { PhieuNhapId = request.PhieuNhapId },
                transaction);

            var dvtColumn = hasDvtExcel ? "don_vi_tinh_excel,\n                " : "";
            var dvtValue = hasDvtExcel ? "@DonViTinh,\n                " : "";
            var insertDetailSql = $"""
            INSERT INTO chi_tiet_phieu_nhap_hang
            (
                phieu_nhap_id,
                san_pham_id,
                ma_san_pham,
                ten_san_pham,
                {dvtColumn}so_luong,
                don_gia_nhap,
                giam_gia,
                thanh_tien,
                ghi_chu
            )
            VALUES
            (
                @PhieuNhapId,
                @SanPhamId,
                @MaSanPham,
                @TenSanPham,
                {dvtValue}@SoLuong,
                @DonGiaNhap,
                @GiamGia,
                @ThanhTien,
                @GhiChu
            );
            """;

            foreach (var item in dongHopLe)
            {
                await connection.ExecuteAsync(
                    insertDetailSql,
                    new
                    {
                        PhieuNhapId = request.PhieuNhapId,
                        item.SanPhamId,
                        item.MaSanPham,
                        item.TenSanPham,
                        DonViTinh = item.DonViTinh,
                        item.SoLuong,
                        item.DonGiaNhap,
                        item.GiamGia,
                        item.ThanhTien,
                        item.GhiChu
                    },
                    transaction);
            }

            // 5. TÍNH TOÁN LẠI TỔNG TIỀN VÀ CÔNG NỢ MỚI
            var newTongTienHang = dongHopLe.Sum(i => i.ThanhTien);
            var newChiPhiKhac = Math.Max(0m, request.ChiPhiKhac);
            var newGiamGia = Math.Max(0m, request.GiamGia);
            var newTongTienNhap = Math.Max(0m, newTongTienHang - newGiamGia + newChiPhiKhac);
            var newDaThanhToan = Math.Clamp(request.DaThanhToan, 0m, newTongTienNhap);
            var newConNo = Math.Max(0m, newTongTienNhap - newDaThanhToan);

            // Kiểm tra thay đổi thông tin chung
            if (oldPhieu.NhaCungCapId != request.NhaCungCapId)
            {
                thayDoiLines.Add($"• Nhà cung cấp: {oldPhieu.TenNhaCungCap ?? "—"} → {request.TenNhaCungCap ?? "—"}");
                auditTruong.Add("Nhà cung cấp");
                auditTruoc.Add($"NCC cũ: {oldPhieu.TenNhaCungCap ?? "—"}");
                auditSau.Add($"NCC mới: {request.TenNhaCungCap ?? "—"}");
            }
            if (oldPhieu.ThoiGianNhap.Date != request.ThoiGianNhap.Date)
            {
                thayDoiLines.Add($"• Ngày nhập: {oldPhieu.ThoiGianNhap:dd/MM/yyyy} → {request.ThoiGianNhap:dd/MM/yyyy}");
                auditTruong.Add("Ngày nhập");
                auditTruoc.Add($"Ngày nhập cũ: {oldPhieu.ThoiGianNhap:dd/MM/yyyy}");
                auditSau.Add($"Ngày nhập mới: {request.ThoiGianNhap:dd/MM/yyyy}");
            }
            if (oldPhieu.TongTienNhap != newTongTienNhap)
            {
                thayDoiLines.Add($"• Tổng tiền nhập: {oldPhieu.TongTienNhap:N0} đ → {newTongTienNhap:N0} đ");
                auditTruong.Add("Tổng tiền nhập");
                auditTruoc.Add($"Tổng tiền cũ: {oldPhieu.TongTienNhap:N0} đ");
                auditSau.Add($"Tổng tiền mới: {newTongTienNhap:N0} đ");
            }
            if (oldPhieu.ChiPhiKhac != newChiPhiKhac)
            {
                thayDoiLines.Add($"• Chi phí khác: {oldPhieu.ChiPhiKhac:N0} đ → {newChiPhiKhac:N0} đ");
                auditTruong.Add("Chi phí khác");
                auditTruoc.Add($"Chi phí cũ: {oldPhieu.ChiPhiKhac:N0} đ");
                auditSau.Add($"Chi phí mới: {newChiPhiKhac:N0} đ");
            }
            if (oldPhieu.GiamGia != newGiamGia)
            {
                thayDoiLines.Add($"• Giảm giá: {oldPhieu.GiamGia:N0} đ → {newGiamGia:N0} đ");
                auditTruong.Add("Giảm giá");
                auditTruoc.Add($"Giảm giá cũ: {oldPhieu.GiamGia:N0} đ");
                auditSau.Add($"Giảm giá mới: {newGiamGia:N0} đ");
            }
            if (oldPhieu.DaThanhToan != newDaThanhToan)
            {
                thayDoiLines.Add($"• Đã thanh toán: {oldPhieu.DaThanhToan:N0} đ → {newDaThanhToan:N0} đ");
                auditTruong.Add("Đã thanh toán");
                auditTruoc.Add($"Đã trả cũ: {oldPhieu.DaThanhToan:N0} đ");
                auditSau.Add($"Đã trả mới: {newDaThanhToan:N0} đ");
            }
            if (oldPhieu.ConNo != newConNo)
            {
                thayDoiLines.Add($"• Còn nợ: {oldPhieu.ConNo:N0} đ → {newConNo:N0} đ");
                auditTruong.Add("Còn nợ");
                auditTruoc.Add($"Còn nợ cũ: {oldPhieu.ConNo:N0} đ");
                auditSau.Add($"Còn nợ mới: {newConNo:N0} đ");
            }
            if (oldPhieu.PhuongThucThanhToan != request.PhuongThucThanhToan)
            {
                thayDoiLines.Add($"• Phương thức thanh toán: {oldPhieu.PhuongThucThanhToan} → {request.PhuongThucThanhToan}");
                auditTruong.Add("Phương thức thanh toán");
                auditTruoc.Add($"Phương thức cũ: {oldPhieu.PhuongThucThanhToan ?? "—"}");
                auditSau.Add($"Phương thức mới: {request.PhuongThucThanhToan ?? "—"}");
            }
            if ((oldPhieu.SoHoaDonDauVao ?? "") != (request.SoHoaDonDauVao ?? ""))
            {
                thayDoiLines.Add($"• Số hóa đơn: '{oldPhieu.SoHoaDonDauVao ?? "—"}' → '{request.SoHoaDonDauVao ?? "—"}'");
                auditTruong.Add("Số hóa đơn đầu vào");
                auditTruoc.Add($"Số HĐ cũ: {oldPhieu.SoHoaDonDauVao ?? "—"}");
                auditSau.Add($"Số HĐ mới: {request.SoHoaDonDauVao ?? "—"}");
            }
            if (oldPhieu.NgayHoaDonDauVao?.Date != request.NgayHoaDonDauVao?.Date)
            {
                thayDoiLines.Add($"• Ngày hóa đơn: {oldPhieu.NgayHoaDonDauVaoHienThi} → {request.NgayHoaDonDauVao?.ToString("dd/MM/yyyy") ?? "—"}");
            }
            if ((oldPhieu.TenNguoiGiaoHang ?? "") != (request.TenNguoiGiaoHang ?? ""))
            {
                thayDoiLines.Add($"• Người giao hàng: '{oldPhieu.TenNguoiGiaoHang ?? "—"}' → '{request.TenNguoiGiaoHang ?? "—"}'");
            }
            if ((oldPhieu.SdtNguoiGiaoHang ?? "") != (request.SdtNguoiGiaoHang ?? ""))
            {
                thayDoiLines.Add($"• SĐT người giao hàng: '{oldPhieu.SdtNguoiGiaoHang ?? "—"}' → '{request.SdtNguoiGiaoHang ?? "—"}'");
            }
            if ((oldPhieu.MaDatHangNhap ?? "") != (request.MaDatHangNhap ?? ""))
            {
                thayDoiLines.Add($"• Mã đặt hàng nhập: '{oldPhieu.MaDatHangNhap ?? "—"}' → '{request.MaDatHangNhap ?? "—"}'");
            }

            // 6. CẬP NHẬT HEADER PHIẾU NHẬP
            var extraAssignments = (hasPhuongThucColumn ? ",\n                phuong_thuc_thanh_toan = @PhuongThucThanhToan" : string.Empty)
                + (hasExtraColumns ? ",\n                so_hoa_don_dau_vao   = @SoHoaDonDauVao,\n                ma_dat_hang_nhap     = @MaDatHangNhap,\n                ngay_hoa_don_dau_vao = @NgayHoaDonDauVao,\n                ten_nguoi_giao_hang  = @TenNguoiGiaoHang,\n                sdt_nguoi_giao_hang  = @SdtNguoiGiaoHang" : string.Empty);

            var updateHeaderSql = $"""
            UPDATE phieu_nhap_hang
            SET nha_cung_cap_id    = @NhaCungCapId,
                thoi_gian_nhap     = @ThoiGianNhap,
                tong_tien_hang     = @TongTienHang,
                giam_gia           = @GiamGia,
                chi_phi_khac       = @ChiPhiKhac,
                tong_tien_nhap     = @TongTienNhap,
                da_thanh_toan      = @DaThanhToan,
                con_no             = @ConNo,
                ghi_chu            = @GhiChu{extraAssignments},
                ngay_cap_nhat      = NOW()
            WHERE id = @Id;
            """;

            await connection.ExecuteAsync(
                updateHeaderSql,
                new
                {
                    Id = request.PhieuNhapId,
                    NhaCungCapId = request.NhaCungCapId is null or 0 ? (uint?)null : request.NhaCungCapId,
                    ThoiGianNhap = request.ThoiGianNhap,
                    TongTienHang = newTongTienHang,
                    GiamGia = newGiamGia,
                    ChiPhiKhac = newChiPhiKhac,
                    TongTienNhap = newTongTienNhap,
                    DaThanhToan = newDaThanhToan,
                    ConNo = newConNo,
                    GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim(),
                    PhuongThucThanhToan = ChuanHoaPhuongThuc(request.PhuongThucThanhToan),
                    SoHoaDonDauVao = string.IsNullOrWhiteSpace(request.SoHoaDonDauVao) ? null : request.SoHoaDonDauVao.Trim(),
                    MaDatHangNhap = string.IsNullOrWhiteSpace(request.MaDatHangNhap) ? null : request.MaDatHangNhap.Trim(),
                    request.NgayHoaDonDauVao,
                    TenNguoiGiaoHang = string.IsNullOrWhiteSpace(request.TenNguoiGiaoHang) ? null : request.TenNguoiGiaoHang.Trim(),
                    SdtNguoiGiaoHang = string.IsNullOrWhiteSpace(request.SdtNguoiGiaoHang) ? null : request.SdtNguoiGiaoHang.Trim()
                },
                transaction);

            // 7. CẬP NHẬT CÔNG NỢ NHÀ CUNG CẤP
            var phieuCapNhat = new PhieuNhapHang
            {
                Id = request.PhieuNhapId,
                MaPhieuNhap = oldPhieu.MaPhieuNhap,
                ChiNhanhId = branchId,
                NhaCungCapId = request.NhaCungCapId is null or 0 ? null : request.NhaCungCapId,
                NguoiNhapId = Session.CurrentUser?.Id ?? oldPhieu.NguoiNhapId,
                ThoiGianNhap = request.ThoiGianNhap,
                TongTienHang = newTongTienHang,
                GiamGia = newGiamGia,
                ChiPhiKhac = newChiPhiKhac,
                TongTienNhap = newTongTienNhap,
                DaThanhToan = newDaThanhToan,
                ConNo = newConNo,
                PhuongThucThanhToan = request.PhuongThucThanhToan
            };

            await GhiCongNoNhaCungCapPhieuNhapAsync(
                connection,
                transaction,
                phieuCapNhat,
                branchId,
                xoaLichSuCu: true);

            // 7.1 LƯU CHỨNG TỪ MỚI (NẾU CÓ)
            if (request.ChungTus != null && request.ChungTus.Count > 0)
            {
                var curUserId = Session.CurrentUser?.Id ?? oldPhieu.NguoiNhapId;
                var curUserName = Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Hệ thống";
                await LuuDinhKemPhieuNhapAsync(
                    connection,
                    transaction,
                    request.PhieuNhapId,
                    oldPhieu.MaPhieuNhap,
                    request.ChungTus,
                    curUserId,
                    curUserName,
                    attachmentCleanup);
            }

            // 8. CẬP NHẬT SỔ QUỸ (PHIẾU CHI)
            await GhiPhieuChiNhapHangAsync(
                connection,
                transaction,
                branchId,
                phieuCapNhat.NguoiNhapId,
                phieuCapNhat,
                hasPhieuQuyExtraColumns,
                hasPhieuQuyPhieuNhapColumns);

            // 9. LƯU VẾT KIỂM TOÁN VÀO lich_su_chinh_sua_phieu_nhap
            var duLieuTruocJson = JsonSerializer.Serialize(new
            {
                oldPhieu.MaPhieuNhap,
                oldPhieu.NhaCungCapId,
                oldPhieu.TenNhaCungCap,
                oldPhieu.ThoiGianNhap,
                oldPhieu.TongTienHang,
                oldPhieu.ChiPhiKhac,
                oldPhieu.GiamGia,
                oldPhieu.TongTienNhap,
                oldPhieu.DaThanhToan,
                oldPhieu.ConNo,
                oldPhieu.PhuongThucThanhToan,
                oldPhieu.GhiChu,
                oldPhieu.SoHoaDonDauVao,
                oldPhieu.NgayHoaDonDauVao,
                oldPhieu.TenNguoiGiaoHang,
                oldPhieu.SdtNguoiGiaoHang,
                oldPhieu.MaDatHangNhap,
                Items = oldItems
            });

            var duLieuSauJson = JsonSerializer.Serialize(new
            {
                oldPhieu.MaPhieuNhap,
                request.NhaCungCapId,
                request.TenNhaCungCap,
                request.ThoiGianNhap,
                TongTienHang = newTongTienHang,
                ChiPhiKhac = newChiPhiKhac,
                GiamGia = newGiamGia,
                TongTienNhap = newTongTienNhap,
                DaThanhToan = newDaThanhToan,
                ConNo = newConNo,
                request.PhuongThucThanhToan,
                request.GhiChu,
                request.SoHoaDonDauVao,
                request.NgayHoaDonDauVao,
                request.TenNguoiGiaoHang,
                request.SdtNguoiGiaoHang,
                request.MaDatHangNhap,
                Items = dongHopLe
            });

            var chiTietThayDoiText = thayDoiLines.Count > 0
                ? string.Join("\n", thayDoiLines)
                : "Cập nhật lại thông tin phiếu nhập";

            const string insertLichSuChinhSuaSql = """
            INSERT INTO lich_su_chinh_sua_phieu_nhap
            (
                phieu_nhap_id,
                ma_phieu_nhap,
                thoi_gian,
                nguoi_thuc_hien_id,
                ten_nguoi_thuc_hien,
                ly_do,
                tong_tien_truoc,
                tong_tien_sau,
                chi_phi_khac_truoc,
                chi_phi_khac_sau,
                giam_gia_truoc,
                giam_gia_sau,
                da_thanh_toan_truoc,
                da_thanh_toan_sau,
                con_no_truoc,
                con_no_sau,
                nha_cung_cap_id_truoc,
                nha_cung_cap_id_sau,
                ten_nha_cung_cap_truoc,
                ten_nha_cung_cap_sau,
                thoi_gian_nhap_truoc,
                thoi_gian_nhap_sau,
                du_lieu_truoc_json,
                du_lieu_sau_json,
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
                @TongTienTruoc,
                @TongTienSau,
                @ChiPhiKhacTruoc,
                @ChiPhiKhacSau,
                @GiamGiaTruoc,
                @GiamGiaSau,
                @DaThanhToanTruoc,
                @DaThanhToanSau,
                @ConNoTruoc,
                @ConNoSau,
                @NhaCungCapIdTruoc,
                @NhaCungCapIdSau,
                @TenNhaCungCapTruoc,
                @TenNhaCungCapSau,
                @ThoiGianNhapTruoc,
                @ThoiGianNhapSau,
                @DuLieuTruocJson,
                @DuLieuSauJson,
                @ChiTietThayDoi
            );
            """;

            await connection.ExecuteAsync(
                insertLichSuChinhSuaSql,
                new
                {
                    PhieuNhapId = request.PhieuNhapId,
                    MaPhieuNhap = oldPhieu.MaPhieuNhap,
                    NguoiThucHienId = Session.CurrentUser?.Id,
                    TenNguoiThucHien = Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Hệ thống",
                    LyDo = string.IsNullOrWhiteSpace(request.LyDoChinhSua) ? "Không cung cấp lý do" : request.LyDoChinhSua.Trim(),
                    TongTienTruoc = oldPhieu.TongTienNhap,
                    TongTienSau = newTongTienNhap,
                    ChiPhiKhacTruoc = oldPhieu.ChiPhiKhac,
                    ChiPhiKhacSau = newChiPhiKhac,
                    GiamGiaTruoc = oldPhieu.GiamGia,
                    GiamGiaSau = newGiamGia,
                    DaThanhToanTruoc = oldPhieu.DaThanhToan,
                    DaThanhToanSau = newDaThanhToan,
                    ConNoTruoc = oldPhieu.ConNo,
                    ConNoSau = newConNo,
                    NhaCungCapIdTruoc = oldPhieu.NhaCungCapId,
                    NhaCungCapIdSau = request.NhaCungCapId,
                    TenNhaCungCapTruoc = oldPhieu.TenNhaCungCap,
                    TenNhaCungCapSau = request.TenNhaCungCap,
                    ThoiGianNhapTruoc = oldPhieu.ThoiGianNhap,
                    ThoiGianNhapSau = request.ThoiGianNhap,
                    DuLieuTruocJson = duLieuTruocJson,
                    DuLieuSauJson = duLieuSauJson,
                    ChiTietThayDoi = chiTietThayDoiText
                },
                transaction);

            await LogSubAccountActivityAsync(
                "Chỉnh sửa phiếu nhập",
                "PHIEU_NHAP",
                request.PhieuNhapId,
                oldPhieu.MaPhieuNhap,
                $"Chỉnh sửa phiếu nhập {oldPhieu.MaPhieuNhap}. Lý do: {(string.IsNullOrWhiteSpace(request.LyDoChinhSua) ? "Không cung cấp lý do" : request.LyDoChinhSua)}" +
                    (string.IsNullOrWhiteSpace(chiTietThayDoiText)
                        ? ""
                        : Environment.NewLine + chiTietThayDoiText),
                newTongTienNhap,
                truongThayDoi: auditTruong.Count > 0 ? string.Join(", ", auditTruong) : "Thông tin phiếu nhập",
                giaTriTruoc: auditTruoc.Count > 0 ? string.Join("\n", auditTruoc) : $"Tổng: {oldPhieu.TongTienNhap:N0} đ",
                giaTriSau: auditSau.Count > 0 ? string.Join("\n", auditSau) : $"Tổng: {newTongTienNhap:N0} đ",
                connection: connection,
                transaction: transaction);

            await transaction.CommitAsync();

            foreach (var tempFile in attachmentCleanup.TempFilesToDeleteOnCommit)
                ChungTuStorageService.XoaFileTamNeuCo(tempFile);
        }
        catch
        {
            await transaction.RollbackAsync();
            foreach (var storedFile in attachmentCleanup.FilesToDeleteOnRollback)
                ChungTuStorageService.XoaFileDaLuuNeuCo(storedFile);
            throw;
        }
    }
}
