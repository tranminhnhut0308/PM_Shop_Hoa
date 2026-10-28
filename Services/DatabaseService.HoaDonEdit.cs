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
    private bool _daDamBaoBangLichSuChinhSuaHoaDon;
    private readonly SemaphoreSlim _khoaDamBaoBangLichSuChinhSuaHoaDon = new(1, 1);

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
            throw new InvalidOperationException(
                "Không thể chuẩn bị bảng lịch sử chỉnh sửa hóa đơn. Vui lòng kiểm tra quyền database.",
                ex);
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

    public async Task<HoaDonEditRequest?> GetHoaDonDeChinhSuaAsync(string maHoaDon)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.InvoicesEdit,
            "Bạn không có quyền chỉnh sửa hóa đơn bán hàng.");

        if (string.IsNullOrWhiteSpace(maHoaDon))
            return null;

        await EnsureHoaDonContactColumnsAsync();
        await using var connection = await OpenAsync();

        var header = await connection.QuerySingleOrDefaultAsync<HoaDonEditHeaderRow>(
            """
            SELECT
                hd.id AS HoaDonId,
                hd.ma_hoa_don AS MaHoaDon,
                hd.chi_nhanh_id AS ChiNhanhId,
                DATE_FORMAT(hd.ngay_cap_nhat, '%Y-%m-%d %H:%i:%s') AS PhienBan,
                hd.khach_hang_id AS KhachHangId,
                COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
                COALESCE(hd.so_dien_thoai, kh.so_dien_thoai, '') AS SoDienThoai,
                COALESCE(hd.dia_chi, kh.dia_chi, '') AS DiaChi,
                hd.thoi_gian_ban AS ThoiGianBan,
                hd.tong_tien_hang AS TongTienHang,
                hd.giam_gia AS GiamGia,
                hd.khach_can_tra AS KhachCanTra,
                hd.khach_da_thanh_toan AS KhachDaThanhToan,
                hd.con_no AS ConNo,
                hd.cong_no_da_ghi_nhan AS CongNoDaGhiNhan,
                hd.trang_thai_hoa_don AS TrangThaiHoaDon,
                hd.ghi_chu AS GhiChu,
                (
                    SELECT COUNT(*)
                    FROM thanh_toan tt
                    WHERE tt.hoa_don_id = hd.id
                ) AS SoLanThanhToan
            FROM hoa_don_ban_hang hd
            LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
            WHERE hd.ma_hoa_don = @MaHoaDon
            LIMIT 1;
            """,
            new { MaHoaDon = maHoaDon.Trim() });

        if (header is null)
            return null;

        var items = (await connection.QueryAsync<HoaDonItemEditModel>(
            """
            SELECT
                ct.id AS Id,
                ct.san_pham_id AS SanPhamId,
                COALESCE(ct.ma_san_pham, '') AS MaSanPham,
                ct.ten_san_pham AS TenSanPham,
                COALESCE(ct.don_vi_tinh, '') AS DonViTinh,
                ct.so_luong AS SoLuong,
                ct.don_gia AS DonGia,
                ct.giam_gia AS GiamGia,
                ct.gia_von AS GiaVon,
                ct.ghi_chu AS GhiChu
            FROM chi_tiet_hoa_don_ban_hang ct
            WHERE ct.hoa_don_id = @HoaDonId
            ORDER BY ct.id;
            """,
            new { HoaDonId = header.HoaDonId })).ToList();

        foreach (var item in items)
            item.SoLuongBanDau = item.SoLuong;

        return new HoaDonEditRequest
        {
            HoaDonId = header.HoaDonId,
            MaHoaDon = header.MaHoaDon,
            PhienBan = header.PhienBan,
            LaHoaDonTongHop = string.Equals(
                header.TrangThaiHoaDon,
                "TONG_HOP",
                StringComparison.OrdinalIgnoreCase),
            ThoiGianBan = header.ThoiGianBan,
            KhachHangId = header.KhachHangId,
            TenKhachHang = header.TenKhachHang,
            SoDienThoai = header.SoDienThoai,
            DiaChi = header.DiaChi,
            GiamGiaHoaDon = header.GiamGia,
            GhiChu = header.GhiChu,
            KhachDaThanhToan = header.KhachDaThanhToan,
            ConNo = header.ConNo,
            SoLanThanhToan = header.SoLanThanhToan,
            CongNoDaGhiNhan = header.CongNoDaGhiNhan,
            Items = items
        };
    }

    public async Task<HoaDonBanHangReceipt> LuuChinhSuaHoaDonAsync(HoaDonEditRequest req)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.InvoicesEdit,
            "Bạn không có quyền chỉnh sửa hóa đơn bán hàng.");

        if (req is null)
            throw new InvalidOperationException("Dữ liệu chỉnh sửa hóa đơn không hợp lệ.");

        if (string.IsNullOrWhiteSpace(req.MaHoaDon))
            throw new InvalidOperationException("Mã hóa đơn không hợp lệ.");

        var lyDo = (req.LyDoChinhSua ?? "").Trim();
        if (lyDo.Length == 0)
            throw new InvalidOperationException("Vui lòng nhập lý do chỉnh sửa hóa đơn.");

        if (lyDo.Length > 500)
            throw new InvalidOperationException("Lý do chỉnh sửa không được vượt quá 500 ký tự.");

        if (req.Items is null || req.Items.Count == 0)
            throw new InvalidOperationException("Hóa đơn phải có ít nhất 1 sản phẩm.");

        var items = new List<HoaDonItemEditModel>(req.Items.Count);

        foreach (var item in req.Items)
        {
            if (item is null || item.SanPhamId == 0)
                throw new InvalidOperationException("Có dòng sản phẩm chưa được chọn.");

            if (item.SoLuong <= 0)
                throw new InvalidOperationException(
                    $"Số lượng của \"{item.TenSanPham}\" phải lớn hơn 0.");

            if (item.SoLuong > 999999999999.999m)
                throw new InvalidOperationException(
                    $"Số lượng của \"{item.TenSanPham}\" vượt quá giới hạn cho phép.");

            if (item.DonGia <= 0 || item.DonGia > MoneyInput.GiaTienToiDa)
                throw new InvalidOperationException(
                    $"Đơn giá của \"{item.TenSanPham}\" phải lớn hơn 0 và trong giới hạn cho phép.");

            if (item.GiamGia < 0 || item.GiamGia > item.SoLuong * item.DonGia)
                throw new InvalidOperationException(
                    $"Giảm giá của \"{item.TenSanPham}\" không hợp lệ.");

            if (items.Any(x => x.Id > 0 && x.Id == item.Id))
                throw new InvalidOperationException("Dữ liệu chỉnh sửa có dòng sản phẩm bị lặp.");

            if (items.Any(x => x.SanPhamId == item.SanPhamId))
                throw new InvalidOperationException(
                    "Mỗi sản phẩm chỉ được xuất hiện một lần trên hóa đơn.");

            items.Add(new HoaDonItemEditModel
            {
                Id = item.Id,
                SanPhamId = item.SanPhamId,
                MaSanPham = item.MaSanPham ?? "",
                TenSanPham = item.TenSanPham ?? "",
                DonViTinh = item.DonViTinh ?? "",
                SoLuong = Math.Round(item.SoLuong, 3, MidpointRounding.AwayFromZero),
                DonGia = Math.Round(item.DonGia, 2, MidpointRounding.AwayFromZero),
                GiamGia = Math.Round(item.GiamGia, 2, MidpointRounding.AwayFromZero),
                GiaVon = item.GiaVon,
                GhiChu = item.GhiChu
            });
        }

        var daCoBangLichSu = await EnsureLichSuChinhSuaHoaDonTableAsync();
        if (!daCoBangLichSu)
            throw new InvalidOperationException("Không thể chuẩn bị bảng lịch sử chỉnh sửa hóa đơn.");

        await EnsureHoaDonContactColumnsAsync();

        var now = DateTime.Now;
        var user = Session.CurrentUser;
        var userId = user?.Id;
        var userName = string.IsNullOrWhiteSpace(user?.HoTen)
            ? (user?.TenDangNhap ?? "Hệ thống")
            : user.HoTen;

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var daCommit = false;

        try
        {
            var oldHd = await connection.QuerySingleOrDefaultAsync<HoaDonEditHeaderRow>(
                """
                SELECT
                    hd.id AS HoaDonId,
                    hd.ma_hoa_don AS MaHoaDon,
                    hd.chi_nhanh_id AS ChiNhanhId,
                    DATE_FORMAT(hd.ngay_cap_nhat, '%Y-%m-%d %H:%i:%s') AS PhienBan,
                    hd.khach_hang_id AS KhachHangId,
                    COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
                    COALESCE(hd.so_dien_thoai, kh.so_dien_thoai, '') AS SoDienThoai,
                    COALESCE(hd.dia_chi, kh.dia_chi, '') AS DiaChi,
                    hd.thoi_gian_ban AS ThoiGianBan,
                    hd.tong_tien_hang AS TongTienHang,
                    hd.giam_gia AS GiamGia,
                    hd.khach_can_tra AS KhachCanTra,
                    hd.khach_da_thanh_toan AS KhachDaThanhToan,
                    hd.con_no AS ConNo,
                    hd.cong_no_da_ghi_nhan AS CongNoDaGhiNhan,
                    hd.trang_thai_hoa_don AS TrangThaiHoaDon,
                    hd.ghi_chu AS GhiChu,
                    (
                        SELECT COUNT(*)
                        FROM thanh_toan tt
                        WHERE tt.hoa_don_id = hd.id
                    ) AS SoLanThanhToan
                FROM hoa_don_ban_hang hd
                LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
                WHERE hd.id = @HoaDonId
                  AND hd.ma_hoa_don = @MaHoaDon
                LIMIT 1
                FOR UPDATE;
                """,
                new
                {
                    HoaDonId = req.HoaDonId,
                    MaHoaDon = req.MaHoaDon.Trim()
                },
                transaction);

            if (oldHd is null)
                throw new InvalidOperationException($"Không tìm thấy hóa đơn {req.MaHoaDon} để chỉnh sửa.");

            if (string.Equals(oldHd.TrangThaiHoaDon, "TONG_HOP", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Hóa đơn tổng hợp không được chỉnh sửa. Vui lòng chỉnh sửa hóa đơn gốc trước khi tổng hợp.");

            if (!string.Equals(oldHd.PhienBan, req.PhienBan, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Hóa đơn đã được thay đổi bởi người khác. Vui lòng đóng cửa sổ chỉnh sửa và mở lại để xem dữ liệu mới nhất.");

            var soPhieuTraHang = await connection.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM phieu_tra_hang
                WHERE hoa_don_id = @HoaDonId
                  AND trang_thai = 'HOAN_THANH';
                """,
                new { HoaDonId = oldHd.HoaDonId },
                transaction);

            if (soPhieuTraHang > 0)
                throw new InvalidOperationException(
                    "Hóa đơn đã phát sinh phiếu trả hàng hoàn thành nên không thể chỉnh sửa. " +
                    "Vui lòng xử lý phiếu trả hàng trước.");

            var oldItems = (await connection.QueryAsync<HoaDonItemEditModel>(
                """
                SELECT
                    ct.id AS Id,
                    ct.san_pham_id AS SanPhamId,
                    COALESCE(ct.ma_san_pham, '') AS MaSanPham,
                    ct.ten_san_pham AS TenSanPham,
                    COALESCE(ct.don_vi_tinh, '') AS DonViTinh,
                    ct.so_luong AS SoLuong,
                    ct.don_gia AS DonGia,
                    ct.giam_gia AS GiamGia,
                    ct.gia_von AS GiaVon,
                    ct.ghi_chu AS GhiChu
                FROM chi_tiet_hoa_don_ban_hang ct
                WHERE ct.hoa_don_id = @HoaDonId
                ORDER BY ct.id
                FOR UPDATE;
                """,
                new { HoaDonId = oldHd.HoaDonId },
                transaction)).ToList();

            var oldItemsById = oldItems.ToDictionary(x => x.Id);

            foreach (var item in items.Where(x => x.Id > 0))
            {
                if (!oldItemsById.TryGetValue(item.Id, out var oldItem))
                    throw new InvalidOperationException(
                        "Một dòng sản phẩm không còn thuộc hóa đơn này. Vui lòng mở lại cửa sổ chỉnh sửa.");

                if (oldItem.SanPhamId != item.SanPhamId)
                    throw new InvalidOperationException(
                        "Không thể đổi sản phẩm của dòng đã có. Vui lòng xóa dòng cũ và thêm sản phẩm mới.");
            }

            var productIds = oldItems.Select(x => x.SanPhamId)
                .Concat(items.Select(x => x.SanPhamId))
                .Distinct()
                .ToList();

            var productRows = (await connection.QueryAsync<HoaDonEditProductRow>(
                """
                SELECT
                    sp.id AS Id,
                    sp.ma_san_pham AS MaSanPham,
                    sp.ten_san_pham AS TenSanPham,
                    dvt.ten_don_vi AS DonViTinh,
                    sp.gia_von AS GiaVon
                FROM san_pham sp
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                WHERE sp.id IN @Ids;
                """,
                new { Ids = productIds },
                transaction)).ToDictionary(x => x.Id);

            var newItems = new List<HoaDonItemEditModel>(items.Count);

            foreach (var item in items)
            {
                oldItemsById.TryGetValue(item.Id, out var oldItem);
                productRows.TryGetValue(item.SanPhamId, out var product);

                if (oldItem is null && product is null)
                    throw new InvalidOperationException(
                        $"Sản phẩm có mã #{item.SanPhamId} không còn tồn tại trong hệ thống.");

                var thanhTien = Math.Round(
                    Math.Max(0, item.SoLuong * item.DonGia - item.GiamGia),
                    2,
                    MidpointRounding.AwayFromZero);

                newItems.Add(new HoaDonItemEditModel
                {
                    Id = item.Id,
                    SanPhamId = item.SanPhamId,
                    MaSanPham = oldItem?.MaSanPham ?? product?.MaSanPham ?? item.MaSanPham ?? "",
                    TenSanPham = oldItem?.TenSanPham ?? product?.TenSanPham ?? item.TenSanPham ?? "",
                    DonViTinh = oldItem?.DonViTinh ?? product?.DonViTinh ?? item.DonViTinh ?? "",
                    SoLuong = item.SoLuong,
                    DonGia = item.DonGia,
                    GiamGia = item.GiamGia,
                    GiaVon = oldItem?.GiaVon ?? product?.GiaVon ?? item.GiaVon,
                    GhiChu = oldItem?.GhiChu ?? item.GhiChu,
                    SoLuongBanDau = oldItem?.SoLuongBanDau ?? 0
                });
            }

            var newTongTienHang = Math.Round(
                newItems.Sum(x => Math.Max(0, x.SoLuong * x.DonGia - x.GiamGia)),
                2,
                MidpointRounding.AwayFromZero);

            var newGiamGia = Math.Round(req.GiamGiaHoaDon, 2, MidpointRounding.AwayFromZero);
            if (newGiamGia < 0 || newGiamGia > newTongTienHang)
                throw new InvalidOperationException("Giảm giá hóa đơn không hợp lệ hoặc vượt quá tổng tiền hàng.");

            var newKhachCanTra = Math.Max(0, newTongTienHang - newGiamGia);
            var khachDaThanhToan = Math.Max(0, oldHd.KhachDaThanhToan);
            var newConNo = Math.Max(0, newKhachCanTra - khachDaThanhToan);
            var newTraThua = Math.Max(0, khachDaThanhToan - newKhachCanTra);

            var newKhachHangId = req.KhachHangId is > 0 ? req.KhachHangId : null;
            var doiKhachHang = newKhachHangId != oldHd.KhachHangId;

            if (doiKhachHang && (oldHd.SoLanThanhToan > 0 || khachDaThanhToan > 0))
                throw new InvalidOperationException(
                    "Không thể đổi khách hàng vì hóa đơn đã phát sinh thanh toán. " +
                    "Vui lòng điều chỉnh công nợ theo nghiệp vụ hiện có.");

            string? newTenKhachHang = null;
            if (newKhachHangId is > 0)
            {
                newTenKhachHang = await connection.ExecuteScalarAsync<string?>(
                    "SELECT ten_khach_hang FROM khach_hang WHERE id = @Id LIMIT 1;",
                    new { Id = newKhachHangId },
                    transaction);

                if (string.IsNullOrWhiteSpace(newTenKhachHang))
                    throw new InvalidOperationException("Khách hàng được chọn không còn tồn tại trong hệ thống.");
            }

            if (newConNo > 0 && newKhachHangId is null)
                throw new InvalidOperationException(
                    $"Hóa đơn còn nợ ({newConNo:N0} đ). Khách lẻ không được phép ghi nợ. " +
                    "Vui lòng chọn khách hàng hoặc tăng số tiền đã thanh toán.");

            var oldQtyByProduct = oldItems
                .GroupBy(x => x.SanPhamId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SoLuong));

            var newQtyByProduct = newItems
                .GroupBy(x => x.SanPhamId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SoLuong));

            var productAdjustments = oldQtyByProduct.Keys
                .Concat(newQtyByProduct.Keys)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            foreach (var productId in productAdjustments)
            {
                oldQtyByProduct.TryGetValue(productId, out var oldQty);
                newQtyByProduct.TryGetValue(productId, out var newQty);
                var deltaQty = newQty - oldQty;

                if (deltaQty == 0)
                    continue;

                productRows.TryGetValue(productId, out var product);
                var tenSanPham = newItems.FirstOrDefault(x => x.SanPhamId == productId)?.TenSanPham
                    ?? oldItems.FirstOrDefault(x => x.SanPhamId == productId)?.TenSanPham
                    ?? $"Sản phẩm #{productId}";

                await DieuChinhTonKhoKhiSuaHoaDonAsync(
                    connection,
                    transaction,
                    oldHd.ChiNhanhId,
                    userId,
                    oldHd.MaHoaDon,
                    productId,
                    tenSanPham,
                    deltaQty,
                    product?.GiaVon ?? 0,
                    lyDo);
            }

            var newItemIds = newItems
                .Where(x => x.Id > 0)
                .Select(x => x.Id)
                .ToHashSet();

            var deleteIds = oldItems
                .Where(x => !newItemIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToList();

            if (deleteIds.Count > 0)
            {
                var deleted = await connection.ExecuteAsync(
                    """
                    DELETE FROM chi_tiet_hoa_don_ban_hang
                    WHERE hoa_don_id = @HoaDonId
                      AND id IN @Ids;
                    """,
                    new
                    {
                        HoaDonId = oldHd.HoaDonId,
                        Ids = deleteIds
                    },
                    transaction);

                if (deleted != deleteIds.Count)
                    throw new InvalidOperationException(
                        "Không xóa được đầy đủ các dòng sản phẩm cũ của hóa đơn.");
            }

            foreach (var item in newItems)
            {
                var thanhTien = Math.Round(
                    Math.Max(0, item.SoLuong * item.DonGia - item.GiamGia),
                    2,
                    MidpointRounding.AwayFromZero);

                if (item.Id > 0)
                {
                    var updated = await connection.ExecuteAsync(
                        """
                        UPDATE chi_tiet_hoa_don_ban_hang
                        SET so_luong = @SoLuong,
                            don_gia = @DonGia,
                            giam_gia = @GiamGia,
                            thanh_tien = @ThanhTien
                        WHERE id = @Id
                          AND hoa_don_id = @HoaDonId;
                        """,
                        new
                        {
                            item.SoLuong,
                            item.DonGia,
                            item.GiamGia,
                            ThanhTien = thanhTien,
                            item.Id,
                            HoaDonId = oldHd.HoaDonId
                        },
                        transaction);

                    if (updated != 1)
                        throw new InvalidOperationException(
                            $"Không cập nhật được dòng sản phẩm \"{item.TenSanPham}\".");
                }
                else
                {
                    var newId = await connection.ExecuteScalarAsync<uint>(
                        """
                        INSERT INTO chi_tiet_hoa_don_ban_hang
                        (
                            hoa_don_id, san_pham_id, ma_san_pham, ten_san_pham,
                            don_vi_tinh, so_luong, don_gia, giam_gia, thanh_tien, gia_von, ghi_chu
                        )
                        VALUES
                        (
                            @HoaDonId, @SanPhamId, @MaSanPham, @TenSanPham,
                            @DonViTinh, @SoLuong, @DonGia, @GiamGia, @ThanhTien, @GiaVon, @GhiChu
                        );
                        SELECT LAST_INSERT_ID();
                        """,
                        new
                        {
                            HoaDonId = oldHd.HoaDonId,
                            item.SanPhamId,
                            item.MaSanPham,
                            item.TenSanPham,
                            item.DonViTinh,
                            item.SoLuong,
                            item.DonGia,
                            item.GiamGia,
                            ThanhTien = thanhTien,
                            item.GiaVon,
                            item.GhiChu
                        },
                        transaction);

                    if (newId == 0)
                        throw new InvalidOperationException(
                            $"Không thêm được dòng sản phẩm \"{item.TenSanPham}\" vào hóa đơn.");

                    item.Id = newId;
                }
            }

            var oldConNo = Math.Max(0, oldHd.ConNo);
            var oldTraThua = Math.Max(0, khachDaThanhToan - Math.Max(0, oldHd.KhachCanTra));
            var oldNet = oldConNo - oldTraThua;
            var newNet = newConNo - newTraThua;

            if (oldHd.CongNoDaGhiNhan == 1)
            {
                if (doiKhachHang)
                {
                    if (oldHd.KhachHangId is > 0 && oldNet != 0)
                    {
                        await GhiDieuChinhCongNoKhachHangAsync(
                            connection,
                            transaction,
                            oldHd.KhachHangId.Value,
                            oldHd.ChiNhanhId,
                            oldHd.MaHoaDon,
                            -oldNet,
                            userId,
                            now,
                            lyDo);
                    }

                    if (newKhachHangId is > 0 && newNet != 0)
                    {
                        await GhiDieuChinhCongNoKhachHangAsync(
                            connection,
                            transaction,
                            newKhachHangId.Value,
                            oldHd.ChiNhanhId,
                            oldHd.MaHoaDon,
                            newNet,
                            userId,
                            now,
                            lyDo);
                    }
                }
                else if (newKhachHangId is > 0 && newNet != oldNet)
                {
                    await GhiDieuChinhCongNoKhachHangAsync(
                        connection,
                        transaction,
                        newKhachHangId.Value,
                        oldHd.ChiNhanhId,
                        oldHd.MaHoaDon,
                        newNet - oldNet,
                        userId,
                        now,
                        lyDo);
                }
            }

            var newTrangThaiThanhToan = newConNo > 0 ? "CONG_NO" : "DA_THANH_TOAN";

            await connection.ExecuteAsync(
                """
                UPDATE hoa_don_ban_hang
                SET khach_hang_id = @KhachHangId,
                    thoi_gian_ban = @ThoiGianBan,
                    tong_tien_hang = @TongTienHang,
                    giam_gia = @GiamGia,
                    khach_can_tra = @KhachCanTra,
                    con_no = @ConNo,
                    trang_thai_thanh_toan = @TrangThaiThanhToan,
                    ghi_chu = @GhiChu,
                    so_dien_thoai = @SoDienThoai,
                    dia_chi = @DiaChi,
                    ngay_cap_nhat = @NgayCapNhat
                WHERE id = @HoaDonId;
                """,
                new
                {
                    KhachHangId = newKhachHangId,
                    ThoiGianBan = req.ThoiGianBan == default ? oldHd.ThoiGianBan : req.ThoiGianBan,
                    TongTienHang = newTongTienHang,
                    GiamGia = newGiamGia,
                    KhachCanTra = newKhachCanTra,
                    ConNo = newConNo,
                    TrangThaiThanhToan = newTrangThaiThanhToan,
                    GhiChu = BlankToNull(req.GhiChu),
                    SoDienThoai = BlankToNull(req.SoDienThoai),
                    DiaChi = BlankToNull(req.DiaChi),
                    NgayCapNhat = now,
                    HoaDonId = oldHd.HoaDonId
                },
                transaction);

            var oldSnapshot = new HoaDonSnapshot
            {
                HoaDonId = oldHd.HoaDonId,
                MaHoaDon = oldHd.MaHoaDon,
                KhachHangId = oldHd.KhachHangId,
                TenKhachHang = string.IsNullOrWhiteSpace(oldHd.TenKhachHang)
                    ? "Khách lẻ"
                    : oldHd.TenKhachHang,
                ThoiGianBan = oldHd.ThoiGianBan,
                TongTienHang = oldHd.TongTienHang,
                GiamGia = oldHd.GiamGia,
                KhachCanTra = oldHd.KhachCanTra,
                KhachDaThanhToan = khachDaThanhToan,
                ConNo = oldConNo,
                GhiChu = oldHd.GhiChu,
                Items = oldItems.Select(x => new HoaDonItemSnapshot
                {
                    SanPhamId = x.SanPhamId,
                    MaSanPham = x.MaSanPham,
                    TenSanPham = x.TenSanPham,
                    DonViTinh = x.DonViTinh,
                    SoLuong = x.SoLuong,
                    DonGia = x.DonGia,
                    GiamGia = x.GiamGia,
                    ThanhTien = Math.Round(
                        Math.Max(0, x.SoLuong * x.DonGia - x.GiamGia),
                        2,
                        MidpointRounding.AwayFromZero),
                    GiaVon = x.GiaVon,
                    GhiChu = x.GhiChu
                }).ToList()
            };

            var newSnapshot = new HoaDonSnapshot
            {
                HoaDonId = oldHd.HoaDonId,
                MaHoaDon = oldHd.MaHoaDon,
                KhachHangId = newKhachHangId,
                TenKhachHang = newKhachHangId is null
                    ? "Khách lẻ"
                    : (string.IsNullOrWhiteSpace(newTenKhachHang) ? "Khách hàng" : newTenKhachHang),
                ThoiGianBan = req.ThoiGianBan == default ? oldHd.ThoiGianBan : req.ThoiGianBan,
                TongTienHang = newTongTienHang,
                GiamGia = newGiamGia,
                KhachCanTra = newKhachCanTra,
                KhachDaThanhToan = khachDaThanhToan,
                ConNo = newConNo,
                GhiChu = BlankToNull(req.GhiChu),
                Items = newItems.Select(x => new HoaDonItemSnapshot
                {
                    SanPhamId = x.SanPhamId,
                    MaSanPham = x.MaSanPham,
                    TenSanPham = x.TenSanPham,
                    DonViTinh = x.DonViTinh,
                    SoLuong = x.SoLuong,
                    DonGia = x.DonGia,
                    GiamGia = x.GiamGia,
                    ThanhTien = Math.Round(
                        Math.Max(0, x.SoLuong * x.DonGia - x.GiamGia),
                        2,
                        MidpointRounding.AwayFromZero),
                    GiaVon = x.GiaVon,
                    GhiChu = x.GhiChu
                }).ToList()
            };

            var diffSummary = TaoMoTaThayDoiHoaDon(oldSnapshot, newSnapshot);
            if (diffSummary.Count == 0)
                throw new InvalidOperationException("Không có thông tin nào thay đổi trên hóa đơn.");

            var chiTietThayDoi = string.Join("; ", diffSummary);
            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            };

            await connection.ExecuteAsync(
                """
                INSERT INTO lich_su_chinh_sua_hoa_don
                (
                    hoa_don_id, ma_hoa_don, thoi_gian, nguoi_thuc_hien_id, ten_nguoi_thuc_hien,
                    ly_do, tong_tien_truoc, tong_tien_sau, giam_gia_truoc, giam_gia_sau,
                    khach_can_tra_truoc, khach_can_tra_sau, khach_da_tra, con_no_truoc, con_no_sau,
                    khach_hang_id_truoc, khach_hang_id_sau, ten_khach_hang_truoc, ten_khach_hang_sau,
                    thoi_gian_ban_truoc, thoi_gian_ban_sau, du_lieu_truoc_json, du_lieu_sau_json, chi_tiet_thay_doi
                )
                VALUES
                (
                    @HoaDonId, @MaHoaDon, @ThoiGian, @NguoiThucHienId, @TenNguoiThucHien,
                    @LyDo, @TongTienTruoc, @TongTienSau, @GiamGiaTruoc, @GiamGiaSau,
                    @KhachCanTraTruoc, @KhachCanTraSau, @KhachDaTra, @ConNoTruoc, @ConNoSau,
                    @KhachHangIdTruoc, @KhachHangIdSau, @TenKhachHangTruoc, @TenKhachHangSau,
                    @ThoiGianBanTruoc, @ThoiGianBanSau, @DuLieuTruocJson, @DuLieuSauJson, @ChiTietThayDoi
                );
                """,
                new
                {
                    HoaDonId = oldHd.HoaDonId,
                    MaHoaDon = oldHd.MaHoaDon,
                    ThoiGian = now,
                    NguoiThucHienId = userId,
                    TenNguoiThucHien = userName,
                    LyDo = lyDo,
                    TongTienTruoc = oldHd.TongTienHang,
                    TongTienSau = newTongTienHang,
                    GiamGiaTruoc = oldHd.GiamGia,
                    GiamGiaSau = newGiamGia,
                    KhachCanTraTruoc = oldHd.KhachCanTra,
                    KhachCanTraSau = newKhachCanTra,
                    KhachDaTra = khachDaThanhToan,
                    ConNoTruoc = oldConNo,
                    ConNoSau = newConNo,
                    KhachHangIdTruoc = oldHd.KhachHangId,
                    KhachHangIdSau = newKhachHangId,
                    TenKhachHangTruoc = oldSnapshot.TenKhachHang,
                    TenKhachHangSau = newSnapshot.TenKhachHang,
                    ThoiGianBanTruoc = oldHd.ThoiGianBan,
                    ThoiGianBanSau = newSnapshot.ThoiGianBan,
                    DuLieuTruocJson = JsonSerializer.Serialize(oldSnapshot, jsonOptions),
                    DuLieuSauJson = JsonSerializer.Serialize(newSnapshot, jsonOptions),
                    ChiTietThayDoi = chiTietThayDoi
                },
                transaction);

            await transaction.CommitAsync();
            daCommit = true;

            _ = LogSubAccountActivityAsync(
                "Sửa hóa đơn",
                "HOA_DON",
                oldHd.HoaDonId,
                oldHd.MaHoaDon,
                $"Sửa hóa đơn {oldHd.MaHoaDon}: {chiTietThayDoi}. Lý do: {lyDo}",
                newKhachCanTra);

            var receipt = await GetHoaDonBanHangTheoMaAsync(oldHd.MaHoaDon);
            if (receipt is null)
                throw new InvalidOperationException(
                    $"Chỉnh sửa thành công nhưng không đọc lại được hóa đơn {oldHd.MaHoaDon}.");

            return receipt;
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
                    SafeLog.WriteError("Rollback chỉnh sửa hóa đơn thất bại", rollbackEx);
                }
            }

            SafeLog.WriteError($"Lỗi LuuChinhSuaHoaDonAsync cho {req.MaHoaDon}", ex);
            throw;
        }
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

    private async Task DieuChinhTonKhoKhiSuaHoaDonAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint chiNhanhId,
        uint? nguoiThucHienId,
        string maHoaDon,
        uint sanPhamId,
        string tenSanPham,
        decimal chenhLech,
        decimal giaVonSanPham,
        string lyDo)
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

        decimal tonTruoc;
        decimal giaVonCu;
        var coBanGhiTon = banGhiTon.Count > 0;

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
                LIMIT 1;
                """,
                new { Id = sanPhamId },
                transaction);

            if (spRow != null)
            {
                tonTruoc = ToDecimal(spRow.TonKho);
                giaVonCu = ToDecimal(spRow.GiaVon);
                coBanGhiTon = true;
            }
            else
            {
                tonTruoc = 0;
                giaVonCu = 0;
            }
        }

        var xuatKho = chenhLech > 0;
        var soLuongDieuChinh = Math.Abs(chenhLech);

        if (xuatKho && tonTruoc < soLuongDieuChinh)
        {
            throw new InvalidOperationException(
                $"Sản phẩm \"{tenSanPham}\" chỉ còn tồn kho {SoLuongFormat.HienThi(tonTruoc)}, " +
                $"không đủ để xuất thêm {SoLuongFormat.HienThi(soLuongDieuChinh)}.");
        }

        if (!coBanGhiTon && !xuatKho)
            tonTruoc = 0;

        var tonSau = tonTruoc - chenhLech;

        await connection.ExecuteAsync(
            """
            INSERT INTO ton_kho
            (
                chi_nhanh_id, san_pham_id, so_luong_ton, gia_von_binh_quan, ngay_cap_nhat
            )
            VALUES
            (
                @ChiNhanhId, @SanPhamId, @SoLuongTon, @GiaVonBinhQuan, NOW()
            )
            ON DUPLICATE KEY UPDATE
                so_luong_ton = @SoLuongTon,
                ngay_cap_nhat = NOW();
            """,
            new
            {
                ChiNhanhId = chiNhanhId,
                SanPhamId = sanPhamId,
                SoLuongTon = tonSau,
                GiaVonBinhQuan = giaVonCu > 0 ? giaVonCu : giaVonSanPham
            },
            transaction);

        await connection.ExecuteAsync(
            """
            UPDATE san_pham
            SET ton_kho_excel = @SoLuongTon,
                ngay_cap_nhat = NOW()
            WHERE id = @SanPhamId;
            """,
            new
            {
                SanPhamId = sanPhamId,
                SoLuongTon = tonSau
            },
            transaction);

        await connection.ExecuteAsync(
            """
            INSERT INTO lich_su_nhap_xuat_kho
            (
                chi_nhanh_id, san_pham_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                so_luong, ton_truoc, ton_sau, gia_von, nguoi_thuc_hien_id, thoi_gian, ghi_chu
            )
            VALUES
            (
                @ChiNhanhId, @SanPhamId, @LoaiGiaoDich, 'DIEU_CHINH_HOA_DON', @MaChungTu,
                @SoLuong, @TonTruoc, @TonSau, @GiaVon, @NguoiThucHienId, NOW(), @GhiChu
            );
            """,
            new
            {
                ChiNhanhId = chiNhanhId,
                SanPhamId = sanPhamId,
                LoaiGiaoDich = xuatKho ? "XUAT" : "NHAP",
                MaChungTu = maHoaDon,
                SoLuong = soLuongDieuChinh,
                TonTruoc = tonTruoc,
                TonSau = tonSau,
                GiaVon = giaVonSanPham,
                NguoiThucHienId = nguoiThucHienId,
                GhiChu = $"{(xuatKho ? "Xuất" : "Nhập")} điều chỉnh do sửa hóa đơn {maHoaDon}. Lý do: {lyDo}"
            },
            transaction);
    }

    private static async Task GhiDieuChinhCongNoKhachHangAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint khachHangId,
        uint chiNhanhId,
        string maHoaDon,
        decimal chenhLech,
        uint? nguoiThucHienId,
        DateTime thoiGian,
        string lyDo)
    {
        if (chenhLech == 0)
            return;

        var soDuCu = await connection.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
            FROM lich_su_cong_no_khach_hang
            WHERE khach_hang_id = @KhachHangId
            FOR UPDATE;
            """,
            new { KhachHangId = khachHangId },
            transaction);

        var ghiNo = chenhLech > 0 ? chenhLech : 0;
        var ghiCo = chenhLech < 0 ? -chenhLech : 0;

        await connection.ExecuteAsync(
            """
            INSERT INTO lich_su_cong_no_khach_hang
            (
                khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu
            )
            VALUES
            (
                @KhachHangId, @ChiNhanhId, 'DIEU_CHINH', 'DIEU_CHINH_HOA_DON', @MaChungTu,
                @GhiNo, @GhiCo, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu
            );
            """,
            new
            {
                KhachHangId = khachHangId,
                ChiNhanhId = chiNhanhId,
                MaChungTu = maHoaDon,
                GhiNo = ghiNo,
                GhiCo = ghiCo,
                SoDu = soDuCu + chenhLech,
                ThoiGian = thoiGian,
                NguoiThucHienId = nguoiThucHienId,
                GhiChu = $"Điều chỉnh công nợ do sửa hóa đơn {maHoaDon}. Lý do: {lyDo}"
            },
            transaction);
    }

    private static List<string> TaoMoTaThayDoiHoaDon(
        HoaDonSnapshot truoc,
        HoaDonSnapshot sau)
    {
        var ketQua = new List<string>();

        void Them(string nhan, object? truocValue, object? sauValue)
        {
            var a = truocValue?.ToString() ?? "";
            var b = sauValue?.ToString() ?? "";
            if (!string.Equals(a, b, StringComparison.Ordinal))
                ketQua.Add($"{nhan}: {a} -> {b}");
        }

        string Tien(decimal value) => MoneyInput.Format(value) + " đ";
        string Ngay(DateTime value) => value.ToString("dd/MM/yyyy HH:mm:ss");

        Them("Tổng tiền hàng", Tien(truoc.TongTienHang), Tien(sau.TongTienHang));
        Them("Giảm giá hóa đơn", Tien(truoc.GiamGia), Tien(sau.GiamGia));
        Them("Khách cần trả", Tien(truoc.KhachCanTra), Tien(sau.KhachCanTra));
        Them("Công nợ", Tien(truoc.ConNo), Tien(sau.ConNo));
        Them("Khách hàng", truoc.TenKhachHang ?? "Khách lẻ", sau.TenKhachHang ?? "Khách lẻ");
        Them("Ngày bán", Ngay(truoc.ThoiGianBan), Ngay(sau.ThoiGianBan));
        Them("Ghi chú", truoc.GhiChu ?? "", sau.GhiChu ?? "");

        var productIds = truoc.Items.Select(x => x.SanPhamId)
            .Concat(sau.Items.Select(x => x.SanPhamId))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        foreach (var productId in productIds)
        {
            var oldItem = truoc.Items.FirstOrDefault(x => x.SanPhamId == productId);
            var newItem = sau.Items.FirstOrDefault(x => x.SanPhamId == productId);
            var ten = newItem?.TenSanPham ?? oldItem?.TenSanPham ?? $"Sản phẩm #{productId}";

            Them(
                $"Số lượng {ten}",
                SoLuongFormat.HienThi(oldItem?.SoLuong ?? 0),
                SoLuongFormat.HienThi(newItem?.SoLuong ?? 0));

            Them(
                $"Đơn giá {ten}",
                Tien(oldItem?.DonGia ?? 0),
                Tien(newItem?.DonGia ?? 0));

            Them(
                $"Giảm giá dòng {ten}",
                Tien(oldItem?.GiamGia ?? 0),
                Tien(newItem?.GiamGia ?? 0));

            Them(
                $"Thành tiền {ten}",
                Tien(oldItem?.ThanhTien ?? 0),
                Tien(newItem?.ThanhTien ?? 0));

            Them(
                $"Ghi chú dòng {ten}",
                oldItem?.GhiChu ?? "",
                newItem?.GhiChu ?? "");
        }

        return ketQua;
    }

    private sealed class HoaDonEditHeaderRow
    {
        public uint HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = "";
        public uint ChiNhanhId { get; set; }
        public string PhienBan { get; set; } = "";
        public uint? KhachHangId { get; set; }
        public string TenKhachHang { get; set; } = "";
        public string SoDienThoai { get; set; } = "";
        public string DiaChi { get; set; } = "";
        public DateTime ThoiGianBan { get; set; }
        public decimal TongTienHang { get; set; }
        public decimal GiamGia { get; set; }
        public decimal KhachCanTra { get; set; }
        public decimal KhachDaThanhToan { get; set; }
        public decimal ConNo { get; set; }
        public int CongNoDaGhiNhan { get; set; }
        public string TrangThaiHoaDon { get; set; } = "";
        public string? GhiChu { get; set; }
        public int SoLanThanhToan { get; set; }
    }

    private sealed class HoaDonEditProductRow
    {
        public uint Id { get; set; }
        public string? MaSanPham { get; set; }
        public string TenSanPham { get; set; } = "";
        public string? DonViTinh { get; set; }
        public decimal GiaVon { get; set; }
    }
}
