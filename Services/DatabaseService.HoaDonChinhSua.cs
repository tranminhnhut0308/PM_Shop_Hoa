using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    private bool _daDamBaoLichSuSuaHoaDonSchema;
    private readonly SemaphoreSlim _khoaLichSuSuaHoaDonSchema = new(1, 1);

    /// <summary>
    /// Đảm bảo bảng lich_su_sua_hoa_don tồn tại trong cơ sở dữ liệu.
    /// </summary>
    public async Task EnsureLichSuSuaHoaDonSchemaAsync()
    {
        if (_daDamBaoLichSuSuaHoaDonSchema)
            return;

        await _khoaLichSuSuaHoaDonSchema.WaitAsync();
        try
        {
            if (_daDamBaoLichSuSuaHoaDonSchema)
                return;

            await using var connection = await OpenAsync();
            const string sql = """
            CREATE TABLE IF NOT EXISTS `lich_su_sua_hoa_don` (
                `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
                `hoa_don_id` int(10) UNSIGNED NOT NULL,
                `ma_hoa_don` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                `nguoi_sua_id` int(10) UNSIGNED DEFAULT NULL,
                `ten_nguoi_sua` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
                `thoi_gian_sua` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `ly_do_sua` text COLLATE utf8mb4_unicode_ci NOT NULL,
                `tong_tien_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
                `tong_tien_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
                `ghi_chu_thay_doi` text COLLATE utf8mb4_unicode_ci NOT NULL,
                `du_lieu_truoc_json` longtext COLLATE utf8mb4_unicode_ci,
                `du_lieu_sau_json` longtext COLLATE utf8mb4_unicode_ci,
                PRIMARY KEY (`id`),
                KEY `idx_hoa_don_id` (`hoa_don_id`),
                KEY `idx_ma_hoa_don` (`ma_hoa_don`),
                KEY `idx_thoi_gian_sua` (`thoi_gian_sua`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;
            await connection.ExecuteAsync(sql);
            _daDamBaoLichSuSuaHoaDonSchema = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi khi tạo bảng lich_su_sua_hoa_don", ex);
        }
        finally
        {
            _khoaLichSuSuaHoaDonSchema.Release();
        }
    }

    /// <summary>
    /// Lấy danh sách lịch sử các lần chỉnh sửa của một hóa đơn.
    /// </summary>
    public async Task<IReadOnlyList<LichSuSuaHoaDon>> GetLichSuSuaHoaDonAsync(uint hoaDonId)
    {
        await EnsureLichSuSuaHoaDonSchemaAsync();
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT 
            id AS Id,
            hoa_don_id AS HoaDonId,
            ma_hoa_don AS MaHoaDon,
            nguoi_sua_id AS NguoiSuaId,
            ten_nguoi_sua AS TenNguoiSua,
            thoi_gian_sua AS ThoiGianSua,
            ly_do_sua AS LyDoSua,
            tong_tien_truoc AS TongTienTruoc,
            tong_tien_sau AS TongTienSau,
            ghi_chu_thay_doi AS GhiChuThayDoi,
            du_lieu_truoc_json AS DuLieuTruocJson,
            du_lieu_sau_json AS DuLieuSauJson
        FROM lich_su_sua_hoa_don
        WHERE hoa_don_id = @HoaDonId
        ORDER BY id DESC;
        """;

        var result = await connection.QueryAsync<LichSuSuaHoaDon>(sql, new { HoaDonId = hoaDonId });
        return result.ToList();
    }

    /// <summary>
    /// Thực hiện chỉnh sửa hóa đơn bán hàng:
    /// 1. Điều chỉnh bù trừ số lượng tồn kho (nếu tăng số lượng mua -> trừ thêm kho; nếu giảm -> cộng trả kho).
    /// 2. Cập nhật các dòng chi_tiet_hoa_don_ban_hang.
    /// 3. Cập nhật tổng tiền, giảm giá, khách cần trả, công nợ trên hoa_don_ban_hang.
    /// 4. Điều chỉnh công nợ khách hàng trong lich_su_cong_no_khach_hang nếu khách cần trả thay đổi.
    /// 5. Ghi nhận lịch sử chỉnh sửa vào lich_su_sua_hoa_don và nhat_ky_hoat_dong.
    /// </summary>
    public async Task<HoaDonBanHangReceipt> ChinhSuaHoaDonBanHangAsync(HoaDonEditRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.LyDoSua))
            throw new InvalidOperationException("Vui lòng nhập lý do chỉnh sửa hóa đơn.");

        if (request.Items == null || request.Items.Count == 0)
            throw new InvalidOperationException("Hóa đơn phải có ít nhất một sản phẩm.");

        await EnsureLichSuSuaHoaDonSchemaAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var daCommit = false;
        try
        {
            // 1. Khoá dòng hóa đơn để đọc dữ liệu hiện tại
            const string lockHdSql = """
            SELECT 
                id AS Id,
                ma_hoa_don AS MaHoaDon,
                chi_nhanh_id AS ChiNhanhId,
                khach_hang_id AS KhachHangId,
                nguoi_ban_id AS NguoiBanId,
                tong_tien_hang AS TongTienHang,
                giam_gia AS GiamGia,
                thu_khac AS ThuKhac,
                khach_can_tra AS KhachCanTra,
                khach_da_thanh_toan AS KhachDaThanhToan,
                con_no AS ConNo,
                trang_thai_thanh_toan AS TrangThaiThanhToan,
                trang_thai_hoa_don AS TrangThaiHoaDon,
                thoi_gian_ban AS ThoiGianBan
            FROM hoa_don_ban_hang
            WHERE id = @Id
            FOR UPDATE;
            """;

            var hdCu = await connection.QueryFirstOrDefaultAsync<HoaDonHienTaiRow>(
                lockHdSql,
                new { Id = request.HoaDonId },
                transaction);

            if (hdCu == null)
                throw new InvalidOperationException($"Không tìm thấy hóa đơn #{request.HoaDonId}.");

            if (string.Equals(hdCu.TrangThaiHoaDon, "DA_HUY", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Hóa đơn này đã bị hủy, không thể chỉnh sửa.");

            // 2. Lấy danh sách chi tiết cũ của hóa đơn
            const string getItemsCuSql = """
            SELECT 
                id AS Id,
                san_pham_id AS SanPhamId,
                ma_san_pham AS MaSanPham,
                ten_san_pham AS TenSanPham,
                don_vi_tinh AS DonViTinh,
                so_luong AS SoLuong,
                don_gia AS DonGia,
                thanh_tien AS ThanhTien,
                gia_von AS GiaVon
            FROM chi_tiet_hoa_don_ban_hang
            WHERE hoa_don_id = @HoaDonId;
            """;

            var itemsCu = (await connection.QueryAsync<HoaDonItemDbRow>(
                getItemsCuSql,
                new { HoaDonId = request.HoaDonId },
                transaction)).ToList();

            // 3. Tính toán chênh lệch tồn kho theo sản phẩm
            // map SanPhamId -> Tổng số lượng cũ
            var mapSoLuongCu = itemsCu
                .GroupBy(x => x.SanPhamId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SoLuong));

            // map SanPhamId -> Tổng số lượng mới
            var mapSoLuongMoi = request.Items
                .GroupBy(x => x.SanPhamId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SoLuong));

            var tatCaSpIds = mapSoLuongCu.Keys.Union(mapSoLuongMoi.Keys).Distinct().ToList();

            var branchId = hdCu.ChiNhanhId > 0 ? hdCu.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);
            var userId = Session.CurrentUser?.Id;
            var now = DateTime.Now;

            foreach (var spId in tatCaSpIds)
            {
                var slCu = mapSoLuongCu.TryGetValue(spId, out var v1) ? v1 : 0m;
                var slMoi = mapSoLuongMoi.TryGetValue(spId, out var v2) ? v2 : 0m;
                var delta = slMoi - slCu; // > 0: bán thêm (cần trừ kho), < 0: giảm mua (hoàn lại kho)

                if (delta == 0)
                    continue;

                // Lấy thông tin sản phẩm và khoá để cập nhật
                var spInfo = await connection.QueryFirstOrDefaultAsync<SanPhamTonKhoRow>(
                    "SELECT id, ma_san_pham, ten_san_pham, ton_kho FROM san_pham WHERE id = @Id FOR UPDATE;",
                    new { Id = spId },
                    transaction);

                if (spInfo != null)
                {
                    var tonKhoTruoc = spInfo.TonKho;
                    var tonKhoSau = tonKhoTruoc - delta;

                    await connection.ExecuteAsync(
                        "UPDATE san_pham SET ton_kho = @TonKhoSau, ngay_cap_nhat = @Now WHERE id = @Id;",
                        new { TonKhoSau = tonKhoSau, Now = now, Id = spId },
                        transaction);

                    // Ghi lịch sử nhập xuất kho
                    var loaiGiaoDich = delta > 0 ? "XUAT_BAN_HANG" : "NHAP_HOAN_TRA";
                    var ghiChuKho = delta > 0
                        ? $"Chỉnh sửa HĐ {hdCu.MaHoaDon}: Bán thêm {Math.Abs(delta):N0}"
                        : $"Chỉnh sửa HĐ {hdCu.MaHoaDon}: Giảm bớt / Trả lại {Math.Abs(delta):N0}";

                    await connection.ExecuteAsync("""
                    INSERT INTO lich_su_nhap_xuat_kho
                    (san_pham_id, chi_nhanh_id, loai_giao_dich, so_luong, ton_truoc, ton_sau, thoi_gian, ma_chung_tu, loai_chung_tu, nguoi_thuc_hien_id, ghi_chu)
                    VALUES
                    (@SanPhamId, @ChiNhanhId, @LoaiGiaoDich, @SoLuong, @TonTruoc, @TonSau, @ThoiGian, @MaChungTu, 'HOA_DON', @NguoiThucHienId, @GhiChu);
                    """, new
                    {
                        SanPhamId = spId,
                        ChiNhanhId = branchId,
                        LoaiGiaoDich = loaiGiaoDich,
                        SoLuong = Math.Abs(delta),
                        TonTruoc = tonKhoTruoc,
                        TonSau = tonKhoSau,
                        ThoiGian = now,
                        MaChungTu = hdCu.MaHoaDon,
                        NguoiThucHienId = userId,
                        GhiChu = ghiChuKho
                    }, transaction);
                }
            }

            // 4. Xóa chi tiết cũ và chèn chi tiết mới
            await connection.ExecuteAsync(
                "DELETE FROM chi_tiet_hoa_don_ban_hang WHERE hoa_don_id = @HoaDonId;",
                new { HoaDonId = request.HoaDonId },
                transaction);

            const string insertDetailSql = """
            INSERT INTO chi_tiet_hoa_don_ban_hang
            (
                hoa_don_id,
                san_pham_id,
                ma_san_pham,
                ten_san_pham,
                don_vi_tinh,
                so_luong,
                don_gia,
                thanh_tien,
                gia_von
            )
            VALUES
            (
                @HoaDonId,
                @SanPhamId,
                @MaSanPham,
                @TenSanPham,
                @DonViTinh,
                @SoLuong,
                @DonGia,
                @ThanhTien,
                @GiaVon
            );
            """;

            decimal tongTienHangMoi = 0;
            foreach (var item in request.Items)
            {
                var thanhTien = Math.Round(item.SoLuong * item.DonGia, 2);
                tongTienHangMoi += thanhTien;

                await connection.ExecuteAsync(insertDetailSql, new
                {
                    HoaDonId = request.HoaDonId,
                    item.SanPhamId,
                    item.MaSanPham,
                    item.TenSanPham,
                    item.DonViTinh,
                    item.SoLuong,
                    item.DonGia,
                    ThanhTien = thanhTien,
                    item.GiaVon
                }, transaction);
            }

            // 5. Tính toán lại tài chính hóa đơn
            var giamGiaMoi = Math.Max(0, request.GiamGia);
            var thuKhacMoi = Math.Max(0, request.ThuKhac);
            var khachCanTraMoi = Math.Max(0, tongTienHangMoi - giamGiaMoi + thuKhacMoi);

            var daThanhToan = hdCu.KhachDaThanhToan;
            var conNoMoi = Math.Max(0, khachCanTraMoi - daThanhToan);
            var trangThaiThanhToanMoi = conNoMoi <= 0 ? "DA_THANH_TOAN" : (daThanhToan > 0 ? "THANH_TOAN_MOT_PHAN" : "CONG_NO");

            // 6. Cập nhật header hóa đơn
            const string updateHeaderSql = """
            UPDATE hoa_don_ban_hang
            SET 
                tong_tien_hang = @TongTienHang,
                giam_gia = @GiamGia,
                thu_khac = @ThuKhac,
                khach_can_tra = @KhachCanTra,
                con_no = @ConNo,
                trang_thai_thanh_toan = @TrangThaiThanhToan,
                ghi_chu = @GhiChu,
                ngay_cap_nhat = @Now
            WHERE id = @Id;
            """;

            await connection.ExecuteAsync(updateHeaderSql, new
            {
                TongTienHang = tongTienHangMoi,
                GiamGia = giamGiaMoi,
                ThuKhac = thuKhacMoi,
                KhachCanTra = khachCanTraMoi,
                ConNo = conNoMoi,
                TrangThaiThanhToan = trangThaiThanhToanMoi,
                GhiChu = BlankToNull(request.GhiChu),
                Now = now,
                Id = request.HoaDonId
            }, transaction);

            // 7. Cập nhật điều chỉnh công nợ khách hàng nếu có khách hàng và số tiền cần trả thay đổi
            var deltaKhachCanTra = khachCanTraMoi - hdCu.KhachCanTra;
            if (hdCu.KhachHangId.HasValue && hdCu.KhachHangId.Value > 0 && deltaKhachCanTra != 0)
            {
                var khachHangId = hdCu.KhachHangId.Value;
                var currentDebtSql = """
                SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                FROM lich_su_cong_no_khach_hang
                WHERE khach_hang_id = @KhachHangId
                FOR UPDATE;
                """;

                var soDuCu = await connection.ExecuteScalarAsync<decimal>(
                    currentDebtSql,
                    new { KhachHangId = khachHangId },
                    transaction);

                var ghiNo = deltaKhachCanTra > 0 ? deltaKhachCanTra : 0m;
                var ghiCo = deltaKhachCanTra < 0 ? Math.Abs(deltaKhachCanTra) : 0m;
                var soDuMoi = soDuCu + deltaKhachCanTra;

                const string debtSql = """
                INSERT INTO lich_su_cong_no_khach_hang
                (khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                 ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                VALUES
                (@KhachHangId, @ChiNhanhId, 'DIEU_CHINH', 'HOA_DON', @MaChungTu,
                 @GhiNo, @GhiCo, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu);
                """;

                await connection.ExecuteAsync(debtSql, new
                {
                    KhachHangId = khachHangId,
                    ChiNhanhId = branchId,
                    MaChungTu = hdCu.MaHoaDon,
                    GhiNo = ghiNo,
                    GhiCo = ghiCo,
                    SoDu = soDuMoi,
                    ThoiGian = now,
                    NguoiThucHienId = userId,
                    GhiChu = $"Điều chỉnh công nợ do sửa hóa đơn {hdCu.MaHoaDon} ({(deltaKhachCanTra > 0 ? "+" : "")}{deltaKhachCanTra:N0} đ). Lý do: {request.LyDoSua}"
                }, transaction);
            }

            // 8. Tạo bản tóm tắt các thay đổi (GhiChuThayDoi)
            var thayDoiList = new List<string>();
            foreach (var spId in tatCaSpIds)
            {
                var itemCu = itemsCu.FirstOrDefault(x => x.SanPhamId == spId);
                var itemMoi = request.Items.FirstOrDefault(x => x.SanPhamId == spId);

                if (itemCu == null && itemMoi != null)
                {
                    thayDoiList.Add($"+ Thêm mặt hàng '{itemMoi.TenSanPham}' SL: {itemMoi.SoLuong:N0} x {itemMoi.DonGia:N0} đ");
                }
                else if (itemCu != null && itemMoi == null)
                {
                    thayDoiList.Add($"- Bỏ mặt hàng '{itemCu.TenSanPham}'");
                }
                else if (itemCu != null && itemMoi != null)
                {
                    var chiTietSua = new List<string>();
                    if (itemCu.SoLuong != itemMoi.SoLuong)
                        chiTietSua.Add($"SL: {itemCu.SoLuong:N0} ➔ {itemMoi.SoLuong:N0}");
                    if (itemCu.DonGia != itemMoi.DonGia)
                        chiTietSua.Add($"Đơn giá: {itemCu.DonGia:N0} ➔ {itemMoi.DonGia:N0} đ");

                    if (chiTietSua.Count > 0)
                        thayDoiList.Add($"• Sửa '{itemMoi.TenSanPham}': {string.Join(", ", chiTietSua)}");
                }
            }

            if (hdCu.GiamGia != giamGiaMoi)
                thayDoiList.Add($"• Giảm giá: {hdCu.GiamGia:N0} ➔ {giamGiaMoi:N0} đ");

            if (hdCu.KhachCanTra != khachCanTraMoi)
                thayDoiList.Add($"• Cần thanh toán: {hdCu.KhachCanTra:N0} ➔ {khachCanTraMoi:N0} đ ({(khachCanTraMoi >= hdCu.KhachCanTra ? "+" : "")}{(khachCanTraMoi - hdCu.KhachCanTra):N0} đ)");

            var ghiChuThayDoiText = string.Join("\n", thayDoiList);

            // Snapshot JSON
            var snapshotCu = itemsCu.Select(x => new HoaDonItemSnapshot
            {
                SanPhamId = x.SanPhamId,
                MaSanPham = x.MaSanPham,
                TenSanPham = x.TenSanPham,
                DonViTinh = x.DonViTinh,
                SoLuong = x.SoLuong,
                DonGia = x.DonGia,
                ThanhTien = x.ThanhTien,
                GiaVon = x.GiaVon
            }).ToList();

            var snapshotMoi = request.Items.Select(x => new HoaDonItemSnapshot
            {
                SanPhamId = x.SanPhamId,
                MaSanPham = x.MaSanPham,
                TenSanPham = x.TenSanPham,
                DonViTinh = x.DonViTinh,
                SoLuong = x.SoLuong,
                DonGia = x.DonGia,
                ThanhTien = x.ThanhTien,
                GiaVon = x.GiaVon
            }).ToList();

            var jsonCu = JsonSerializer.Serialize(snapshotCu);
            var jsonMoi = JsonSerializer.Serialize(snapshotMoi);

            // 9. Lưu vào bảng lich_su_sua_hoa_don
            const string insertHistorySql = """
            INSERT INTO lich_su_sua_hoa_don
            (
                hoa_don_id,
                ma_hoa_don,
                nguoi_sua_id,
                ten_nguoi_sua,
                thoi_gian_sua,
                ly_do_sua,
                tong_tien_truoc,
                tong_tien_sau,
                ghi_chu_thay_doi,
                du_lieu_truoc_json,
                du_lieu_sau_json
            )
            VALUES
            (
                @HoaDonId,
                @MaHoaDon,
                @NguoiSuaId,
                @TenNguoiSua,
                @ThoiGianSua,
                @LyDoSua,
                @TongTienTruoc,
                @TongTienSau,
                @GhiChuThayDoi,
                @DuLieuTruocJson,
                @DuLieuSauJson
            );
            """;

            var tenNguoiSua = Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Hệ thống";
            await connection.ExecuteAsync(insertHistorySql, new
            {
                HoaDonId = request.HoaDonId,
                MaHoaDon = hdCu.MaHoaDon,
                NguoiSuaId = userId,
                TenNguoiSua = tenNguoiSua,
                ThoiGianSua = now,
                LyDoSua = request.LyDoSua,
                TongTienTruoc = hdCu.KhachCanTra,
                TongTienSau = khachCanTraMoi,
                GhiChuThayDoi = ghiChuThayDoiText,
                DuLieuTruocJson = jsonCu,
                DuLieuSauJson = jsonMoi
            }, transaction);

            // 10. Commit transaction
            await transaction.CommitAsync();
            daCommit = true;

            _ = LogSubAccountActivityAsync(
                "Sửa hóa đơn",
                "HOA_DON",
                request.HoaDonId,
                hdCu.MaHoaDon,
                $"Chỉnh sửa hóa đơn {hdCu.MaHoaDon}: {hdCu.KhachCanTra:N0} đ ➔ {khachCanTraMoi:N0} đ. Lý do: {request.LyDoSua}",
                khachCanTraMoi);

            var updatedReceipt = await GetHoaDonBanHangTheoMaAsync(hdCu.MaHoaDon);
            return updatedReceipt ?? throw new InvalidOperationException($"Không tải lại được hóa đơn {hdCu.MaHoaDon} sau khi sửa.");
        }
        catch (Exception ex)
        {
            if (!daCommit)
            {
                try { await transaction.RollbackAsync(); } catch { }
            }
            SafeLog.WriteError("Lỗi khi chỉnh sửa hóa đơn bán hàng", ex);
            throw;
        }
    }

    private sealed class HoaDonHienTaiRow
    {
        public uint Id { get; set; }
        public string MaHoaDon { get; set; } = "";
        public uint ChiNhanhId { get; set; }
        public uint? KhachHangId { get; set; }
        public uint? NguoiBanId { get; set; }
        public decimal TongTienHang { get; set; }
        public decimal GiamGia { get; set; }
        public decimal ThuKhac { get; set; }
        public decimal KhachCanTra { get; set; }
        public decimal KhachDaThanhToan { get; set; }
        public decimal ConNo { get; set; }
        public string TrangThaiThanhToan { get; set; } = "";
        public string TrangThaiHoaDon { get; set; } = "";
        public DateTime ThoiGianBan { get; set; }
    }

    private sealed class HoaDonItemDbRow
    {
        public uint Id { get; set; }
        public uint SanPhamId { get; set; }
        public string MaSanPham { get; set; } = "";
        public string TenSanPham { get; set; } = "";
        public string DonViTinh { get; set; } = "";
        public decimal SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien { get; set; }
        public decimal GiaVon { get; set; }
    }

    private sealed class SanPhamTonKhoRow
    {
        public uint Id { get; set; }
        public string MaSanPham { get; set; } = "";
        public string TenSanPham { get; set; } = "";
        public decimal TonKho { get; set; }
    }
}
