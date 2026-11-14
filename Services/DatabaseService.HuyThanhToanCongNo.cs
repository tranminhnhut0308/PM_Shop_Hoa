using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    private static readonly SemaphoreSlim _khoaHuyThanhToanCongNo = new(1, 1);

    /// <summary>
    /// Hủy phiếu thanh toán trong lịch sử công nợ khách hàng (Issue #104).
    /// Hoàn tác toàn bộ số tiền đã ghi nhận vào công nợ khách hàng, các hóa đơn phân bổ,
    /// khoản trả trước (nếu thanh toán dư) và sổ quỹ trong một database transaction.
    /// </summary>
    public Task<HuyThanhToanCongNoKetQua> HuyThanhToanCongNoKhachHangAsync(
        uint customerId,
        string maGiaoDich,
        string lyDoHuy,
        uint? nguoiHuyId = null,
        string? tenNguoiHuy = null)
        => HuyThanhToanCongNoKhachHangAsync(maGiaoDich, lyDoHuy, nguoiHuyId, tenNguoiHuy);

    /// <summary>
    /// Hủy phiếu thanh toán trong lịch sử công nợ khách hàng (Issue #104).
    /// </summary>
    public async Task<HuyThanhToanCongNoKetQua> HuyThanhToanCongNoKhachHangAsync(
        string maGiaoDich,
        string lyDoHuy,
        uint? nguoiHuyId = null,
        string? tenNguoiHuy = null)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.CustomersDebtPay,
            "Bạn không có quyền hủy phiếu thanh toán công nợ.");

        if (string.IsNullOrWhiteSpace(maGiaoDich))
            throw new ArgumentException("Mã phiếu thanh toán không được để trống.", nameof(maGiaoDich));

        if (string.IsNullOrWhiteSpace(lyDoHuy))
            throw new ArgumentException("Vui lòng nhập lý do hủy phiếu thanh toán.", nameof(lyDoHuy));

        await EnsureBangLichSuThanhToanKhachHangAsync();
        await EnsurePhieuQuyCancelColumnsAsync();
        await EnsureAuditTableAsync();

        await _khoaHuyThanhToanCongNo.WaitAsync();
        try
        {
            await using var connection = await OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await EnsureTraTruocSchemaAsync(connection, transaction);

            var daCommit = false;
            try
            {
                var now = DateTime.Now;
                var userId = nguoiHuyId ?? Session.CurrentUser?.Id;
                var userName = !string.IsNullOrWhiteSpace(tenNguoiHuy)
                    ? tenNguoiHuy.Trim()
                    : (Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Hệ thống");

                // 1. Khóa và kiểm tra phiếu thanh toán trong lich_su_thanh_toan_cong_no_khach_hang
                const string selectPaymentSql = """
                SELECT
                    id AS Id,
                    khach_hang_id AS KhachHangId,
                    ma_giao_dich AS MaGiaoDich,
                    so_tien AS SoTien,
                    cong_no_truoc AS CongNoTruoc,
                    cong_no_sau AS CongNoSau,
                    thoi_gian AS ThoiGian,
                    phuong_thuc AS PhuongThuc,
                    trang_thai AS TrangThai,
                    thoi_gian_huy AS ThoiGianHuy,
                    ly_do_huy AS LyDoHuy,
                    nguoi_huy_id AS NguoiHuyId
                FROM lich_su_thanh_toan_cong_no_khach_hang
                WHERE ma_giao_dich = @MaGiaoDich
                LIMIT 1
                FOR UPDATE;
                """;

                var record = await connection.QueryFirstOrDefaultAsync<ThanhToanCongNoKhachHang>(
                    selectPaymentSql,
                    new { MaGiaoDich = maGiaoDich.Trim() },
                    transaction);

                if (record is null)
                    throw new InvalidOperationException($"Không tìm thấy phiếu thanh toán {maGiaoDich} trong cơ sở dữ liệu.");

                if (string.Equals(record.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Phiếu thanh toán {maGiaoDich} đã bị hủy trước đó.");

                // 2. Khóa và đọc thông tin khách hàng
                const string selectCustomerSql = """
                SELECT
                    id AS Id,
                    ma_khach_hang AS MaKhachHang,
                    ten_khach_hang AS TenKhachHang,
                    chi_nhanh_id AS ChiNhanhId,
                    COALESCE(so_du_tra_truoc, 0) AS SoDuTraTruoc
                FROM khach_hang
                WHERE id = @KhachHangId
                LIMIT 1
                FOR UPDATE;
                """;

                var khach = await connection.QueryFirstOrDefaultAsync<(uint Id, string? MaKhachHang, string? TenKhachHang, uint? ChiNhanhId, decimal SoDuTraTruoc)>(
                    selectCustomerSql,
                    new { KhachHangId = record.KhachHangId },
                    transaction);

                if (khach.Id == 0)
                    throw new InvalidOperationException($"Không tìm thấy khách hàng (ID {record.KhachHangId}) của phiếu thanh toán {maGiaoDich}.");

                uint chiNhanhId = khach.ChiNhanhId ?? (Session.CurrentUser?.ChiNhanhId ?? 1);
                string maKhachHang = khach.MaKhachHang ?? "";
                string tenKhachHang = khach.TenKhachHang ?? "Khách hàng";

                // 3. Kiểm tra tiền thanh toán dư (trả trước - Issue #97) phát sinh từ phiếu này
                const string checkOverpaymentSql = """
                SELECT COALESCE(SUM(so_tien), 0)
                FROM lich_su_tra_truoc_khach_hang
                WHERE khach_hang_id = @KhachHangId
                  AND ma_chung_tu = @MaGiaoDich
                  AND loai_giao_dich = 'THANH_TOAN_DU';
                """;

                var tienThanhToanDu = await connection.ExecuteScalarAsync<decimal>(
                    checkOverpaymentSql,
                    new { KhachHangId = record.KhachHangId, MaGiaoDich = record.MaGiaoDich },
                    transaction);

                decimal tienHoanTacTraTruoc = 0m;
                decimal soDuTraTruocTruoc = khach.SoDuTraTruoc;
                decimal soDuTraTruocSau = khach.SoDuTraTruoc;

                if (tienThanhToanDu > 0)
                {
                    // Kiểm tra xem khách hàng có còn đủ số dư trả trước không
                    // Nếu số dư hiện tại < số dư phát sinh thì khách đã dùng một phần cho hóa đơn mới
                    if (khach.SoDuTraTruoc < tienThanhToanDu)
                    {
                        throw new InvalidOperationException(
                            $"Không thể hủy phiếu thanh toán này vì khoản tiền thanh toán dư ({tienThanhToanDu:N0} đ) " +
                            $"đã được sử dụng một phần hoặc toàn bộ để cấn trừ vào các hóa đơn mua hàng sau đó. " +
                            $"Số dư trả trước hiện tại của khách chỉ còn {khach.SoDuTraTruoc:N0} đ.");
                    }

                    tienHoanTacTraTruoc = tienThanhToanDu;
                    soDuTraTruocSau = khach.SoDuTraTruoc - tienThanhToanDu;

                    // Giảm số dư trả trước của khách hàng
                    await connection.ExecuteAsync(
                        """
                        UPDATE khach_hang
                        SET so_du_tra_truoc = GREATEST(0, so_du_tra_truoc - @TienDu)
                        WHERE id = @KhachHangId;
                        """,
                        new { TienDu = tienThanhToanDu, KhachHangId = record.KhachHangId },
                        transaction);

                    // Ghi lịch sử hoàn tác trả trước
                    var maGdTraTruoc = await SinhMaGiaoDichTraTruocTrongGiaoDichAsync(
                        connection,
                        transaction,
                        "HTKH",
                        "lich_su_tra_truoc_khach_hang");

                    await GhiLichSuTraTruocKhachHangTrongGiaoDichAsync(
                        connection,
                        transaction,
                        record.KhachHangId,
                        maKhachHang,
                        maGdTraTruoc,
                        "HOAN_TRA",
                        "PHIEU_THU",
                        record.MaGiaoDich,
                        tienThanhToanDu,
                        soDuTraTruocTruoc,
                        soDuTraTruocSau,
                        record.PhuongThuc,
                        now,
                        userName,
                        $"Hoàn tác số dư trả trước do hủy phiếu thanh toán {record.MaGiaoDich}. Lý do: {lyDoHuy.Trim()}");
                }

                // 4. Hoàn tác các phân bổ hóa đơn trong bảng thanh_toan và hoa_don_ban_hang
                const string selectAllocationsSql = """
                SELECT
                    tt.id AS Id,
                    tt.hoa_don_id AS HoaDonId,
                    tt.so_tien AS SoTien,
                    hd.ma_hoa_don AS MaHoaDon,
                    hd.khach_can_tra AS KhachCanTra,
                    hd.khach_da_thanh_toan AS KhachDaThanhToan,
                    hd.con_no AS ConNo,
                    hd.trang_thai_hoa_don AS TrangThaiHoaDon
                FROM thanh_toan tt
                INNER JOIN hoa_don_ban_hang hd ON hd.id = tt.hoa_don_id
                WHERE (tt.ma_giao_dich = @MaGiaoDich OR tt.ghi_chu LIKE CONCAT('%[Phiếu thu ', @MaGiaoDich, ']%'))
                  AND tt.trang_thai = 'HOAN_THANH'
                FOR UPDATE;
                """;

                var allocations = (await connection.QueryAsync<(
                    ulong Id,
                    uint HoaDonId,
                    decimal SoTien,
                    string MaHoaDon,
                    decimal KhachCanTra,
                    decimal KhachDaThanhToan,
                    decimal ConNo,
                    string TrangThaiHoaDon)>(
                    selectAllocationsSql,
                    new { MaGiaoDich = record.MaGiaoDich },
                    transaction)).AsList();

                var soHoaDonHoanTac = 0;
                foreach (var alloc in allocations)
                {
                    // Chỉ hoàn tác hóa đơn chưa bị hủy
                    if (!string.Equals(alloc.TrangThaiHoaDon, "DA_HUY", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(alloc.TrangThaiHoaDon, "HUY", StringComparison.OrdinalIgnoreCase))
                    {
                        await connection.ExecuteAsync(
                            """
                            UPDATE hoa_don_ban_hang
                            SET khach_da_thanh_toan = GREATEST(0, khach_da_thanh_toan - @SoTien),
                                con_no = LEAST(khach_can_tra, con_no + @SoTien),
                                trang_thai_thanh_toan = CASE
                                    WHEN (con_no + @SoTien) >= khach_can_tra THEN 'CHUA_THANH_TOAN'
                                    ELSE 'CONG_NO'
                                END,
                                ngay_cap_nhat = @Now
                            WHERE id = @HoaDonId;
                            """,
                            new { SoTien = alloc.SoTien, Now = now, HoaDonId = alloc.HoaDonId },
                            transaction);

                        soHoaDonHoanTac++;
                    }
                }

                // Cập nhật trạng thái dòng phân bổ trong thanh_toan thành HUY
                await connection.ExecuteAsync(
                    """
                    UPDATE thanh_toan
                    SET trang_thai = 'HUY',
                        thoi_gian_huy = @Now,
                        nguoi_huy_id = @UserId,
                        ly_do_huy = @LyDoHuy
                    WHERE (ma_giao_dich = @MaGiaoDich OR ghi_chu LIKE CONCAT('%[Phiếu thu ', @MaGiaoDich, ']%'))
                      AND trang_thai = 'HOAN_THANH';
                    """,
                    new
                    {
                        MaGiaoDich = record.MaGiaoDich,
                        Now = now,
                        UserId = userId,
                        LyDoHuy = lyDoHuy.Trim()
                    },
                    transaction);

                // 5. Hoàn tác công nợ trong sổ cái lich_su_cong_no_khach_hang (ghi nợ = tăng nợ lại)
                var tienHoanTacCongNo = Math.Max(0m, record.SoTien - tienHoanTacTraTruoc);
                decimal congNoTruocKhiHuy = 0m;
                decimal congNoSauKhiHuy = 0m;

                if (tienHoanTacCongNo > 0)
                {
                    const string currentDebtSql = """
                    SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                    FROM lich_su_cong_no_khach_hang
                    WHERE khach_hang_id = @KhachHangId
                    FOR UPDATE;
                    """;

                    congNoTruocKhiHuy = await connection.ExecuteScalarAsync<decimal>(
                        currentDebtSql,
                        new { KhachHangId = record.KhachHangId },
                        transaction);

                    congNoSauKhiHuy = congNoTruocKhiHuy + tienHoanTacCongNo;

                    const string insertReversalLedgerSql = """
                    INSERT INTO lich_su_cong_no_khach_hang
                    (khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                     ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                    VALUES
                    (@KhachHangId, @ChiNhanhId, 'HUY_THANH_TOAN', 'PHIEU_THU', @MaChungTu,
                     @GhiNo, 0, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu);
                    """;

                    await connection.ExecuteAsync(
                        insertReversalLedgerSql,
                        new
                        {
                            KhachHangId = record.KhachHangId,
                            ChiNhanhId = chiNhanhId,
                            MaChungTu = record.MaGiaoDich,
                            GhiNo = tienHoanTacCongNo,
                            SoDu = congNoSauKhiHuy,
                            ThoiGian = now,
                            NguoiThucHienId = userId,
                            GhiChu = $"Hoàn tác thanh toán công nợ do hủy phiếu thu {record.MaGiaoDich}. Lý do: {lyDoHuy.Trim()}"
                        },
                        transaction);
                }
                else
                {
                    const string currentDebtSql = """
                    SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                    FROM lich_su_cong_no_khach_hang
                    WHERE khach_hang_id = @KhachHangId;
                    """;

                    congNoTruocKhiHuy = await connection.ExecuteScalarAsync<decimal>(
                        currentDebtSql,
                        new { KhachHangId = record.KhachHangId },
                        transaction);
                    congNoSauKhiHuy = congNoTruocKhiHuy;
                }

                // 6. Cập nhật phiếu thu trong sổ quỹ (phieu_chi) thành HUY
                await connection.ExecuteAsync(
                    """
                    UPDATE phieu_chi
                    SET trang_thai = 'HUY',
                        thoi_gian_huy = @Now,
                        nguoi_huy_id = @UserId,
                        ly_do_huy = @LyDoHuy
                    WHERE ma_phieu_chi = @MaGiaoDich
                      AND trang_thai = 'HOAN_THANH';
                    """,
                    new
                    {
                        MaGiaoDich = record.MaGiaoDich,
                        Now = now,
                        UserId = userId,
                        LyDoHuy = $"Hủy theo phiếu thanh toán công nợ {record.MaGiaoDich}: {lyDoHuy.Trim()}"
                    },
                    transaction);

                // 7. Cập nhật trạng thái phiếu trong lich_su_thanh_toan_cong_no_khach_hang thành HUY
                await connection.ExecuteAsync(
                    """
                    UPDATE lich_su_thanh_toan_cong_no_khach_hang
                    SET trang_thai = 'HUY',
                        thoi_gian_huy = @Now,
                        nguoi_huy_id = @UserId,
                        ly_do_huy = @LyDoHuy
                    WHERE id = @Id;
                    """,
                    new
                    {
                        Id = record.Id,
                        Now = now,
                        UserId = userId,
                        LyDoHuy = lyDoHuy.Trim()
                    },
                    transaction);

                await LogSubAccountActivityAsync(
                    "Hủy thu nợ",
                    "KHACH_HANG",
                    record.KhachHangId,
                    record.MaGiaoDich,
                    $"Hủy phiếu thu nợ {record.MaGiaoDich} của khách hàng {tenKhachHang}: {record.SoTien:N0} đ. Lý do: {lyDoHuy.Trim()}",
                    record.SoTien,
                    truongThayDoi: "Trạng thái phiếu thu nợ, Công nợ KH",
                    giaTriTruoc: $"HOÀN THÀNH | Công nợ KH: {Math.Max(0m, congNoTruocKhiHuy):N0} đ",
                    giaTriSau: $"ĐÃ HỦY | Công nợ KH: {Math.Max(0m, congNoSauKhiHuy):N0} đ",
                    connection: connection,
                    transaction: transaction);

                await transaction.CommitAsync();
                daCommit = true;

                return new HuyThanhToanCongNoKetQua
                {
                    MaGiaoDich = record.MaGiaoDich,
                    KhachHangId = record.KhachHangId,
                    MaKhachHang = maKhachHang,
                    TenKhachHang = tenKhachHang,
                    SoTien = record.SoTien,
                    TienHoanTacCongNo = tienHoanTacCongNo,
                    TienHoanTacTraTruoc = tienHoanTacTraTruoc,
                    CongNoTruocKhiHuy = Math.Max(0m, congNoTruocKhiHuy),
                    CongNoSauKhiHuy = Math.Max(0m, congNoSauKhiHuy),
                    SoDuTraTruocTruocKhiHuy = soDuTraTruocTruoc,
                    SoDuTraTruocSauKhiHuy = soDuTraTruocSau,
                    SoHoaDonHoanTac = soHoaDonHoanTac,
                    ThoiGianHuy = now,
                    LyDoHuy = lyDoHuy.Trim(),
                    NguoiHuy = userName
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
                        SafeLog.WriteError("Rollback HuyThanhToanCongNoKhachHangAsync thất bại", rollbackEx);
                    }
                }

                SafeLog.WriteError($"Lỗi HuyThanhToanCongNoKhachHangAsync cho {maGiaoDich}", ex);
                throw;
            }
        }
        finally
        {
            _khoaHuyThanhToanCongNo.Release();
        }
    }
}
