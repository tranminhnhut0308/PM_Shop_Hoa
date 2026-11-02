using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Lưu / khôi phục giỏ hàng bán nháp (bảng don_ban_nhap) - Issue #50.
/// Đơn nháp chỉ là dữ liệu soạn thảo: không đụng tới tồn kho, công nợ, sổ quỹ.
/// </summary>
public sealed partial class DatabaseService
{
    public const string TrangThaiBanDangSoan = "DANG_SOAN";
    public const string TrangThaiBanDaHoanTat = "DA_HOAN_TAT";

    private bool _daDamBaoBangDonBanNhap;
    private readonly SemaphoreSlim _khoaBangDonBanNhap = new(1, 1);

    public async Task EnsureDonBanNhapTableAsync()
    {
        if (_daDamBaoBangDonBanNhap)
            return;

        await _khoaBangDonBanNhap.WaitAsync();
        try
        {
            if (_daDamBaoBangDonBanNhap)
                return;

            await using var connection = await OpenAsync();
            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS don_ban_nhap (
                    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                    cart_id         CHAR(32)        NOT NULL COMMENT 'ID riêng của từng giỏ hàng nháp',
                    nguoi_dung_id   INT UNSIGNED    NOT NULL,
                    chi_nhanh_id    INT UNSIGNED    NULL,
                    so_thu_tu       INT             NOT NULL DEFAULT 1,
                    khach_hang_id   INT UNSIGNED    NULL,
                    so_dong         INT             NOT NULL DEFAULT 0,
                    tong_tien       DECIMAL(15,2)   NOT NULL DEFAULT 0,
                    du_lieu         LONGTEXT        NOT NULL COMMENT 'JSON: khách hàng, dòng hàng, giảm giá, thanh toán, ghi chú',
                    phien_ban       INT UNSIGNED    NOT NULL DEFAULT 1,
                    trang_thai      VARCHAR(20)     NOT NULL DEFAULT 'DANG_SOAN' COMMENT 'DANG_SOAN | DA_HOAN_TAT',
                    hoa_don_id      INT UNSIGNED    NULL COMMENT 'Hóa đơn chính thức sau khi thanh toán',
                    ngay_tao        DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ngay_cap_nhat   DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    PRIMARY KEY (id),
                    UNIQUE KEY uk_dbn_cart (cart_id),
                    KEY idx_dbn_user (nguoi_dung_id, trang_thai)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                """);

            _daDamBaoBangDonBanNhap = true;
        }
        finally
        {
            _khoaBangDonBanNhap.Release();
        }
    }

    /// <summary>Đọc các giỏ hàng nháp đang soạn của người dùng hiện tại (khôi phục khi mở lại phần mềm).</summary>
    public async Task<List<SaleCartDraftRecord>> GetDonBanNhapDangSoanAsync()
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SalesView, "Bạn không có quyền mở màn hình bán hàng.");
        var userId = Session.CurrentUser!.Id;

        await EnsureDonBanNhapTableAsync();
        await using var connection = await OpenAsync();

        var rows = await connection.QueryAsync<SaleCartDraftRecord>(
            """
            SELECT cart_id    AS CartId,
                   so_thu_tu  AS SoThuTu,
                   du_lieu    AS DuLieu,
                   phien_ban  AS PhienBan
            FROM don_ban_nhap
            WHERE nguoi_dung_id = @UserId
              AND trang_thai = 'DANG_SOAN'
            ORDER BY so_thu_tu, id;
            """,
            new { UserId = userId });

        return rows.AsList();
    }

    /// <summary>
    /// Ghi (upsert theo cart_id) một giỏ hàng nháp. Không bao giờ tạo bản ghi trùng cho cùng một giỏ
    /// và không ghi đè đơn đã hoàn tất.
    /// </summary>
    public async Task LuuDonBanNhapAsync(SaleCartDraftState state, string json, decimal tongTien)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SalesView, "Bạn không có quyền mở màn hình bán hàng.");
        var user = Session.CurrentUser!;

        await EnsureDonBanNhapTableAsync();
        await using var connection = await OpenAsync();

        await connection.ExecuteAsync(
            """
            INSERT INTO don_ban_nhap
                (cart_id, nguoi_dung_id, chi_nhanh_id, so_thu_tu, khach_hang_id, so_dong, tong_tien, du_lieu)
            VALUES
                (@CartId, @UserId, @ChiNhanhId, @SoThuTu, @KhachHangId, @SoDong, @TongTien, @DuLieu)
            ON DUPLICATE KEY UPDATE
                so_thu_tu     = IF(trang_thai = 'DANG_SOAN', VALUES(so_thu_tu), so_thu_tu),
                khach_hang_id = IF(trang_thai = 'DANG_SOAN', VALUES(khach_hang_id), khach_hang_id),
                so_dong       = IF(trang_thai = 'DANG_SOAN', VALUES(so_dong), so_dong),
                tong_tien     = IF(trang_thai = 'DANG_SOAN', VALUES(tong_tien), tong_tien),
                du_lieu       = IF(trang_thai = 'DANG_SOAN', VALUES(du_lieu), du_lieu),
                phien_ban     = IF(trang_thai = 'DANG_SOAN', phien_ban + 1, phien_ban);
            """,
            new
            {
                state.CartId,
                UserId = user.Id,
                ChiNhanhId = user.ChiNhanhId,
                state.SoThuTu,
                KhachHangId = state.KhachHangId == 0 ? (uint?)null : state.KhachHangId,
                SoDong = state.Lines.Count,
                TongTien = tongTien,
                DuLieu = json
            });
    }

    /// <summary>Xóa một giỏ hàng nháp (Đóng đơn / Reset). Không đụng tới giỏ hàng khác.</summary>
    public async Task XoaDonBanNhapAsync(string cartId)
    {
        if (string.IsNullOrWhiteSpace(cartId))
            return;

        await EnsureDonBanNhapTableAsync();
        await using var connection = await OpenAsync();

        await connection.ExecuteAsync(
            """
            DELETE FROM don_ban_nhap
            WHERE cart_id = @CartId
              AND nguoi_dung_id = @UserId
              AND trang_thai = 'DANG_SOAN';
            """,
            new { CartId = cartId, UserId = Session.CurrentUser?.Id ?? 0u });
    }

    /// <summary>Đánh dấu giỏ hàng nháp là đã hoàn tất sau khi thanh toán thành công.</summary>
    public async Task ChuyenDonBanNhapSangHoanTatAsync(string cartId, uint hoaDonId)
    {
        if (string.IsNullOrWhiteSpace(cartId))
            return;

        await EnsureDonBanNhapTableAsync();
        await using var connection = await OpenAsync();

        await connection.ExecuteAsync(
            """
            UPDATE don_ban_nhap
            SET trang_thai = 'DA_HOAN_TAT',
                hoa_don_id = @HoaDonId
            WHERE cart_id = @CartId
              AND nguoi_dung_id = @UserId
              AND trang_thai = 'DANG_SOAN';
            """,
            new { CartId = cartId, HoaDonId = hoaDonId, UserId = Session.CurrentUser?.Id ?? 0u });
    }

    /// <summary>
    /// Lấy số lượng tồn kho thực tế mới nhất từ database cho một sản phẩm tại chi nhánh hiện tại.
    /// Dùng để kiểm tra tồn kho tại thời điểm thanh toán tránh bán vượt tồn giữa nhiều đơn hàng.
    /// </summary>
    public async Task<decimal> LayTonKhoThucTeAsync(uint sanPhamId)
    {
        if (sanPhamId == 0)
            return 0m;

        var branchId = Session.CurrentUser?.ChiNhanhId ?? 1;
        await using var connection = await OpenAsync();

        return await connection.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0)
            FROM san_pham sp
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id AND tk.chi_nhanh_id = @ChiNhanhId
            WHERE sp.id = @SanPhamId
            LIMIT 1;
            """,
            new { SanPhamId = sanPhamId, ChiNhanhId = branchId });
    }
}
