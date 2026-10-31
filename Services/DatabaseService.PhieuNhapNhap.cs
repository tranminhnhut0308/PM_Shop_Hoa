using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Lưu / khôi phục phiếu nhập nháp (bảng phieu_nhap_nhap).
/// Phiếu nháp chỉ là dữ liệu soạn thảo: không đụng tới tồn kho, công nợ, sổ quỹ.
/// </summary>
public sealed partial class DatabaseService
{
    public const string TrangThaiNhapDangSoan = "DANG_SOAN";
    public const string TrangThaiNhapDaHoanTat = "DA_HOAN_TAT";

    private bool _daDamBaoBangPhieuNhapNhap;
    private readonly SemaphoreSlim _khoaBangPhieuNhapNhap = new(1, 1);

    public async Task EnsurePhieuNhapNhapTableAsync()
    {
        if (_daDamBaoBangPhieuNhapNhap)
            return;

        await _khoaBangPhieuNhapNhap.WaitAsync();
        try
        {
            if (_daDamBaoBangPhieuNhapNhap)
                return;

            await using var connection = await OpenAsync();
            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS phieu_nhap_nhap (
                    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                    draft_id        CHAR(32)        NOT NULL,
                    nguoi_dung_id   INT UNSIGNED    NOT NULL,
                    chi_nhanh_id    INT UNSIGNED    NULL,
                    so_thu_tu       INT             NOT NULL DEFAULT 1,
                    nha_cung_cap_id INT UNSIGNED    NULL,
                    so_dong         INT             NOT NULL DEFAULT 0,
                    du_lieu         LONGTEXT        NOT NULL,
                    phien_ban       INT UNSIGNED    NOT NULL DEFAULT 1,
                    trang_thai      VARCHAR(20)     NOT NULL DEFAULT 'DANG_SOAN',
                    phieu_nhap_id   INT UNSIGNED    NULL,
                    ngay_tao        DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ngay_cap_nhat   DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    PRIMARY KEY (id),
                    UNIQUE KEY uk_pnn_draft (draft_id),
                    KEY idx_pnn_user (nguoi_dung_id, trang_thai)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                """);

            _daDamBaoBangPhieuNhapNhap = true;
        }
        finally
        {
            _khoaBangPhieuNhapNhap.Release();
        }
    }

    /// <summary>Đọc các phiếu nháp đang soạn của người dùng hiện tại (khôi phục khi mở lại phần mềm).</summary>
    public async Task<List<PhieuNhapDraftRecord>> GetPhieuNhapNhapDangSoanAsync()
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.PurchasesCreate, "Bạn không có quyền lập phiếu nhập hàng.");
        var userId = Session.CurrentUser!.Id;

        await EnsurePhieuNhapNhapTableAsync();
        await using var connection = await OpenAsync();

        var rows = await connection.QueryAsync<PhieuNhapDraftRecord>(
            """
            SELECT draft_id   AS DraftId,
                   so_thu_tu  AS SoThuTu,
                   du_lieu    AS DuLieu,
                   phien_ban  AS PhienBan
            FROM phieu_nhap_nhap
            WHERE nguoi_dung_id = @UserId
              AND trang_thai = 'DANG_SOAN'
            ORDER BY so_thu_tu, id;
            """,
            new { UserId = userId });

        return rows.AsList();
    }

    /// <summary>
    /// Ghi (upsert theo draft_id) một phiếu nháp. Không bao giờ tạo bản ghi trùng cho cùng một phiếu
    /// và không ghi đè phiếu đã hoàn tất.
    /// </summary>
    public async Task LuuPhieuNhapNhapAsync(PhieuNhapDraftState state, string json)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.PurchasesCreate, "Bạn không có quyền lập phiếu nhập hàng.");
        var user = Session.CurrentUser!;

        await EnsurePhieuNhapNhapTableAsync();
        await using var connection = await OpenAsync();

        await connection.ExecuteAsync(
            """
            INSERT INTO phieu_nhap_nhap
                (draft_id, nguoi_dung_id, chi_nhanh_id, so_thu_tu, nha_cung_cap_id, so_dong, du_lieu)
            VALUES
                (@DraftId, @UserId, @ChiNhanhId, @SoThuTu, @NhaCungCapId, @SoDong, @DuLieu)
            ON DUPLICATE KEY UPDATE
                so_thu_tu       = IF(trang_thai = 'DANG_SOAN', VALUES(so_thu_tu), so_thu_tu),
                nha_cung_cap_id = IF(trang_thai = 'DANG_SOAN', VALUES(nha_cung_cap_id), nha_cung_cap_id),
                so_dong         = IF(trang_thai = 'DANG_SOAN', VALUES(so_dong), so_dong),
                du_lieu         = IF(trang_thai = 'DANG_SOAN', VALUES(du_lieu), du_lieu),
                phien_ban       = IF(trang_thai = 'DANG_SOAN', phien_ban + 1, phien_ban);
            """,
            new
            {
                state.DraftId,
                UserId = user.Id,
                ChiNhanhId = user.ChiNhanhId,
                state.SoThuTu,
                NhaCungCapId = state.NhaCungCapId == 0 ? (uint?)null : state.NhaCungCapId,
                SoDong = state.Lines.Count,
                DuLieu = json
            });
    }

    /// <summary>Xóa một phiếu nháp đang soạn (Reset / Đóng phiếu). Không đụng tới phiếu nháp khác.</summary>
    public async Task XoaPhieuNhapNhapAsync(string draftId)
    {
        if (string.IsNullOrWhiteSpace(draftId))
            return;

        await EnsurePhieuNhapNhapTableAsync();
        await using var connection = await OpenAsync();

        await connection.ExecuteAsync(
            """
            DELETE FROM phieu_nhap_nhap
            WHERE draft_id = @DraftId
              AND nguoi_dung_id = @UserId
              AND trang_thai = 'DANG_SOAN';
            """,
            new { DraftId = draftId, UserId = Session.CurrentUser?.Id ?? 0u });
    }

    /// <summary>
    /// Khóa dòng nháp trong transaction hoàn tất. Chỉ phiếu đang ở trạng thái DANG_SOAN mới được hoàn tất,
    /// nên bấm "Hoàn thành" nhiều lần (hoặc từ 2 máy) không thể ghi nhận trùng.
    /// </summary>
    private static async Task KhoaPhieuNhapNhapDeHoanTatAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string draftId)
    {
        var trangThai = await connection.ExecuteScalarAsync<string?>(
            "SELECT trang_thai FROM phieu_nhap_nhap WHERE draft_id = @DraftId FOR UPDATE;",
            new { DraftId = draftId },
            transaction);

        if (trangThai is null)
            throw new InvalidOperationException("Không tìm thấy phiếu nhập nháp để hoàn thành. Vui lòng thử lại.");

        if (trangThai != TrangThaiNhapDangSoan)
            throw new InvalidOperationException("Phiếu nhập này đã được hoàn thành trước đó, không thể ghi nhận lại.");
    }

    private static Task DanhDauPhieuNhapNhapHoanTatAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string draftId,
        uint phieuNhapId) =>
        connection.ExecuteAsync(
            """
            UPDATE phieu_nhap_nhap
            SET trang_thai = 'DA_HOAN_TAT', phieu_nhap_id = @PhieuNhapId
            WHERE draft_id = @DraftId;
            """,
            new { DraftId = draftId, PhieuNhapId = phieuNhapId },
            transaction);
}
