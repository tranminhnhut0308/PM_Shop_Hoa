using System;
using System.Collections.Generic;
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
            await connection.ExecuteAsync(
                """
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
                """);
            _daDamBaoLichSuSuaHoaDonSchema = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi khi tạo bảng lich_su_sua_hoa_don", ex);
            throw;
        }
        finally
        {
            _khoaLichSuSuaHoaDonSchema.Release();
        }
    }

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

    public async Task<HoaDonBanHangReceipt> ChinhSuaHoaDonBanHangAsync(HoaDonEditRequest request)
    {
        ValidateHoaDonEditRequest(request);
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.InvoicesEdit,
            "Bạn không có quyền chỉnh sửa hóa đơn bán hàng.");

        await EnsureLichSuSuaHoaDonSchemaAsync();
        await EnsureAuditTableAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var daCommit = false;

        try
        {
            var state = await PrepareHoaDonEditStateAsync(connection, transaction, request);
            await ApplyHoaDonEditStockAsync(connection, transaction, state);
            await ReplaceHoaDonEditItemsAsync(connection, transaction, state);
            await UpdateHoaDonEditFinancialsAsync(connection, transaction, state);
            await SaveHoaDonEditHistoryAsync(connection, transaction, state);
            await WriteHoaDonEditAuditAsync(connection, transaction, state);

            await transaction.CommitAsync();
            daCommit = true;

            return await GetHoaDonBanHangTheoMaAsync(state.Header.MaHoaDon)
                ?? throw new InvalidOperationException(
                    $"Đã cập nhật hóa đơn {state.Header.MaHoaDon} nhưng không tải lại được dữ liệu.");
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
                    SafeLog.WriteError("Không rollback được giao dịch chỉnh sửa hóa đơn", rollbackEx);
                }
            }

            SafeLog.WriteError("Lỗi khi chỉnh sửa hóa đơn bán hàng", ex);
            throw;
        }
    }

    internal static void ValidateHoaDonEditRequest(HoaDonEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.HoaDonId == 0)
            throw new InvalidOperationException("Thiếu mã hóa đơn cần chỉnh sửa.");
        if (string.IsNullOrWhiteSpace(request.LyDoSua))
            request.LyDoSua = "Không cung cấp lý do";
        else
            request.LyDoSua = request.LyDoSua.Trim();
        if (request.Items is null || request.Items.Count == 0)
            throw new InvalidOperationException("Hóa đơn phải có ít nhất một sản phẩm.");
        if (request.Items.Any(item =>
                item.SanPhamId == 0 ||
                item.SoLuong <= 0 ||
                decimal.Round(item.SoLuong, 3) != item.SoLuong ||
                item.DonGia < 0 ||
                item.DonGia > MoneyInput.GiaTienToiDa ||
                item.GiamGia < 0 ||
                item.GiamGia > item.SoLuong * item.DonGia))
        {
            throw new InvalidOperationException("Thông tin sản phẩm, số lượng, đơn giá hoặc giảm giá không hợp lệ.");
        }

        if (request.Items.GroupBy(item => item.SanPhamId).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Mỗi sản phẩm chỉ được xuất hiện một lần trong hóa đơn.");
        if (request.GiamGia < 0 || request.GiamGia > MoneyInput.GiaTienToiDa)
            throw new InvalidOperationException("Giảm giá hóa đơn không hợp lệ.");
    }

    private sealed class HoaDonEditState
    {
        public required HoaDonEditRequest Request { get; init; }
        public required HoaDonHienTaiRow Header { get; init; }
        public required List<HoaDonItemDbRow> OldItems { get; init; }
        public required Dictionary<uint, decimal> OldQuantities { get; init; }
        public required Dictionary<uint, decimal> NewQuantities { get; init; }
        public required Dictionary<uint, SanPhamSuaHoaDonRow> Products { get; init; }
        public required Dictionary<uint, TonKhoSuaHoaDonRow> Stock { get; init; }
        public required decimal ItemsTotal { get; init; }
        public required decimal InvoiceDiscount { get; init; }
        public required decimal AmountDue { get; init; }
        public required decimal NewDebt { get; init; }
        public required uint BranchId { get; init; }
        public uint? UserId { get; init; }
        public required DateTime Now { get; init; }
    }

    private sealed record HoaDonEditTotals(
        decimal ItemsTotal,
        decimal InvoiceDiscount,
        decimal AmountDue,
        decimal NewDebt);

    private sealed class HoaDonHienTaiRow
    {
        public uint Id { get; set; }
        public string MaHoaDon { get; set; } = "";
        public uint ChiNhanhId { get; set; }
        public uint? KhachHangId { get; set; }
        public decimal TongTienHang { get; set; }
        public decimal GiamGia { get; set; }
        public decimal ThuKhac { get; set; }
        public decimal KhachCanTra { get; set; }
        public decimal KhachDaThanhToan { get; set; }
        public decimal ConNo { get; set; }
        public int CongNoDaGhiNhan { get; set; }
        public string TrangThaiHoaDon { get; set; } = "";
    }

    private sealed class HoaDonItemDbRow
    {
        public uint SanPhamId { get; set; }
        public string MaSanPham { get; set; } = "";
        public string TenSanPham { get; set; } = "";
        public string DonViTinh { get; set; } = "";
        public decimal SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal GiamGia { get; set; }
        public decimal ThanhTien { get; set; }
        public decimal GiaVon { get; set; }
        public string? GhiChu { get; set; }
    }

    private sealed class HoaDonTonKhoLedgerRow
    {
        public uint SanPhamId { get; set; }
        public decimal BienDong { get; set; }
    }

    private sealed class SanPhamSuaHoaDonRow
    {
        public uint Id { get; set; }
        public string MaSanPham { get; set; } = "";
        public string TenSanPham { get; set; } = "";
        public decimal GiaVon { get; set; }
    }

    private sealed class TonKhoSuaHoaDonRow
    {
        public decimal SoLuongTon { get; set; }
        public decimal GiaVonBinhQuan { get; set; }
    }
}
