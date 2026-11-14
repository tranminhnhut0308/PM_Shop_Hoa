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
    private static readonly SemaphoreSlim _khoaHuyThanhToanCongNoNcc = new(1, 1);

    /// <summary>
    /// Hủy giao dịch thanh toán trong lịch sử công nợ nhà cung cấp (Issue #111).
    /// Hoàn tác toàn bộ số tiền đã ghi nhận vào công nợ nhà cung cấp, các phiếu nhập phân bổ,
    /// khoản trả trước (nếu thanh toán dư) và sổ quỹ (phiếu chi) trong một database transaction.
    /// Chỉ tài khoản Quản trị (Admin) mới có quyền thực hiện.
    /// </summary>
    public Task<HuyThanhToanCongNoNhaCungCapKetQua> HuyThanhToanCongNoNhaCungCapAsync(
        uint supplierId,
        string maGiaoDich,
        string lyDoHuy,
        uint? nguoiHuyId = null,
        string? tenNguoiHuy = null)
        => HuyThanhToanCongNoNhaCungCapAsync(maGiaoDich, lyDoHuy, nguoiHuyId, tenNguoiHuy);

    /// <summary>
    /// Hủy giao dịch thanh toán trong lịch sử công nợ nhà cung cấp (Issue #111).
    /// </summary>
    public async Task<HuyThanhToanCongNoNhaCungCapKetQua> HuyThanhToanCongNoNhaCungCapAsync(
        string maGiaoDich,
        string lyDoHuy,
        uint? nguoiHuyId = null,
        string? tenNguoiHuy = null)
    {
        // 1. Kiểm tra quyền Admin ở tầng nghiệp vụ
        Quyen.RequireAdmin(
            Session.CurrentUser,
            "Chỉ tài khoản Quản trị (Admin) mới có quyền hủy phiếu thanh toán công nợ nhà cung cấp.");

        if (string.IsNullOrWhiteSpace(maGiaoDich))
            throw new ArgumentException("Mã giao dịch thanh toán không được để trống.", nameof(maGiaoDich));

        if (string.IsNullOrWhiteSpace(lyDoHuy))
            throw new ArgumentException("Vui lòng nhập lý do hủy phiếu thanh toán.", nameof(lyDoHuy));

        await EnsureBangLichSuThanhToanNhaCungCapAsync();
        await EnsurePhieuQuyCancelColumnsAsync();
        await EnsurePhieuNhapTrackingColumnsAsync();
        await EnsureAuditTableAsync();

        await _khoaHuyThanhToanCongNoNcc.WaitAsync();
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

                // 2. Khóa và kiểm tra giao dịch trong lich_su_thanh_toan_cong_no_nha_cung_cap
                const string selectPaymentSql = """
                SELECT
                    id AS Id,
                    nha_cung_cap_id AS NhaCungCapId,
                    ma_nha_cung_cap AS MaNhaCungCap,
                    ten_nha_cung_cap AS TenNhaCungCap,
                    don_vi_nhan AS DonViNhan,
                    ma_giao_dich AS MaGiaoDich,
                    so_tien AS SoTien,
                    cong_no_truoc AS CongNoTruoc,
                    cong_no_sau AS CongNoSau,
                    thoi_gian AS ThoiGian,
                    phuong_thuc AS PhuongThuc,
                    ma_tham_chieu AS MaThamChieu,
                    ghi_chu AS GhiChu,
                    trang_thai AS TrangThai,
                    thoi_gian_huy AS ThoiGianHuy,
                    ly_do_huy AS LyDoHuy,
                    nguoi_huy_id AS NguoiHuyId
                FROM lich_su_thanh_toan_cong_no_nha_cung_cap
                WHERE ma_giao_dich = @MaGiaoDich
                LIMIT 1
                FOR UPDATE;
                """;

                var record = await connection.QueryFirstOrDefaultAsync<ThanhToanCongNoNhaCungCap>(
                    selectPaymentSql,
                    new { MaGiaoDich = maGiaoDich.Trim() },
                    transaction);

                // Nếu không tìm thấy bằng ma_giao_dich, tìm kiếm qua thanh_toan_nha_cung_cap (trường hợp mã thanh toán TTNCC...)
                if (record is null)
                {
                    const string fallbackFindSql = """
                    SELECT
                        ls.id AS Id,
                        ls.nha_cung_cap_id AS NhaCungCapId,
                        ls.ma_nha_cung_cap AS MaNhaCungCap,
                        ls.ten_nha_cung_cap AS TenNhaCungCap,
                        ls.don_vi_nhan AS DonViNhan,
                        ls.ma_giao_dich AS MaGiaoDich,
                        ls.so_tien AS SoTien,
                        ls.cong_no_truoc AS CongNoTruoc,
                        ls.cong_no_sau AS CongNoSau,
                        ls.thoi_gian AS ThoiGian,
                        ls.phuong_thuc AS PhuongThuc,
                        ls.ma_tham_chieu AS MaThamChieu,
                        ls.ghi_chu AS GhiChu,
                        ls.trang_thai AS TrangThai,
                        ls.thoi_gian_huy AS ThoiGianHuy,
                        ls.ly_do_huy AS LyDoHuy,
                        ls.nguoi_huy_id AS NguoiHuyId
                    FROM lich_su_thanh_toan_cong_no_nha_cung_cap ls
                    INNER JOIN thanh_toan_nha_cung_cap tt ON tt.ma_giao_dich = ls.ma_giao_dich
                    WHERE tt.ma_thanh_toan = @MaCanTim
                    LIMIT 1
                    FOR UPDATE;
                    """;

                    record = await connection.QueryFirstOrDefaultAsync<ThanhToanCongNoNhaCungCap>(
                        fallbackFindSql,
                        new { MaCanTim = maGiaoDich.Trim() },
                        transaction);
                }

                if (record is null)
                    throw new InvalidOperationException($"Không tìm thấy giao dịch thanh toán {maGiaoDich} trong cơ sở dữ liệu.");

                if (string.Equals(record.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Giao dịch thanh toán {maGiaoDich} đã bị hủy trước đó.");

                // 3. Khóa và đọc thông tin nhà cung cấp
                const string selectSupplierSql = """
                SELECT
                    id AS Id,
                    ma_nha_cung_cap AS MaNhaCungCap,
                    ten_nha_cung_cap AS TenNhaCungCap,
                    chi_nhanh_id AS ChiNhanhId,
                    COALESCE(so_du_tra_truoc, 0) AS SoDuTraTruoc
                FROM nha_cung_cap
                WHERE id = @NhaCungCapId
                LIMIT 1
                FOR UPDATE;
                """;

                var supplier = await connection.QueryFirstOrDefaultAsync<(uint Id, string? MaNhaCungCap, string? TenNhaCungCap, uint? ChiNhanhId, decimal SoDuTraTruoc)>(
                    selectSupplierSql,
                    new { NhaCungCapId = record.NhaCungCapId },
                    transaction);

                if (supplier.Id == 0)
                    throw new InvalidOperationException($"Không tìm thấy nhà cung cấp (ID {record.NhaCungCapId}) của giao dịch thanh toán {maGiaoDich}.");

                uint chiNhanhId = supplier.ChiNhanhId ?? (Session.CurrentUser?.ChiNhanhId ?? 1);
                string maNhaCungCap = supplier.MaNhaCungCap ?? record.MaNhaCungCap ?? "";
                string tenNhaCungCap = supplier.TenNhaCungCap ?? record.TenNhaCungCap ?? "Nhà cung cấp";

                // 4. Kiểm tra tiền thanh toán dư (trả trước NCC) phát sinh từ giao dịch này
                const string checkOverpaymentSql = """
                SELECT COALESCE(SUM(so_tien), 0)
                FROM lich_su_tra_truoc_ncc
                WHERE nha_cung_cap_id = @NhaCungCapId
                  AND (ma_chung_tu = @MaGiaoDich OR ma_giao_dich = @MaGiaoDich)
                  AND loai_giao_dich = 'THANH_TOAN_DU';
                """;

                var tienThanhToanDu = await connection.ExecuteScalarAsync<decimal>(
                    checkOverpaymentSql,
                    new { NhaCungCapId = record.NhaCungCapId, MaGiaoDich = record.MaGiaoDich },
                    transaction);

                decimal tienHoanTacTraTruoc = 0m;
                decimal soDuTraTruocTruoc = supplier.SoDuTraTruoc;
                decimal soDuTraTruocSau = supplier.SoDuTraTruoc;

                if (tienThanhToanDu > 0)
                {
                    if (supplier.SoDuTraTruoc < tienThanhToanDu)
                    {
                        throw new InvalidOperationException(
                            $"Không thể hủy giao dịch thanh toán này vì khoản tiền thanh toán dư ({tienThanhToanDu:N0} đ) " +
                            $"đã được sử dụng một phần hoặc toàn bộ để cấn trừ vào các phiếu nhập sau đó. " +
                            $"Số dư trả trước hiện tại của nhà cung cấp chỉ còn {supplier.SoDuTraTruoc:N0} đ.");
                    }

                    tienHoanTacTraTruoc = tienThanhToanDu;
                    soDuTraTruocSau = supplier.SoDuTraTruoc - tienThanhToanDu;

                    await connection.ExecuteAsync(
                        """
                        UPDATE nha_cung_cap
                        SET so_du_tra_truoc = GREATEST(0, so_du_tra_truoc - @TienDu)
                        WHERE id = @NhaCungCapId;
                        """,
                        new { TienDu = tienThanhToanDu, NhaCungCapId = record.NhaCungCapId },
                        transaction);

                    var maGdTraTruoc = await SinhMaGiaoDichTraTruocTrongGiaoDichAsync(
                        connection,
                        transaction,
                        "HTNCC",
                        "lich_su_tra_truoc_ncc");

                    await GhiLichSuTraTruocNhaCungCapTrongGiaoDichAsync(
                        connection,
                        transaction,
                        record.NhaCungCapId,
                        maNhaCungCap,
                        maGdTraTruoc,
                        "HOAN_TRA",
                        "PHIEU_CHI",
                        record.MaGiaoDich,
                        tienThanhToanDu,
                        soDuTraTruocTruoc,
                        soDuTraTruocSau,
                        record.PhuongThuc,
                        now,
                        userName,
                        $"Hoàn tác số dư trả trước do hủy phiếu thanh toán {record.MaGiaoDich}. Lý do: {lyDoHuy.Trim()}");
                }

                // 5. Hoàn tác các phân bổ phiếu nhập trong thanh_toan_nha_cung_cap và phieu_nhap_hang
                const string selectAllocationsSql = """
                SELECT
                    tt.id AS Id,
                    tt.phieu_nhap_id AS PhieuNhapId,
                    tt.so_tien AS SoTien,
                    p.ma_phieu_nhap AS MaPhieuNhap,
                    p.tong_tien_nhap AS TongTienNhap,
                    p.da_thanh_toan AS DaThanhToan,
                    p.con_no AS ConNo,
                    p.trang_thai AS TrangThai
                FROM thanh_toan_nha_cung_cap tt
                INNER JOIN phieu_nhap_hang p ON p.id = tt.phieu_nhap_id
                WHERE (tt.ma_giao_dich = @MaGiaoDich
                       OR tt.ma_thanh_toan = @MaGiaoDich
                       OR tt.ghi_chu LIKE CONCAT('%[Giao dịch ', @MaGiaoDich, ']%')
                       OR tt.ghi_chu LIKE CONCAT('%', @MaGiaoDich, '%'))
                  AND tt.trang_thai = 'HOAN_THANH'
                FOR UPDATE;
                """;

                var allocations = (await connection.QueryAsync<(
                    ulong Id,
                    uint PhieuNhapId,
                    decimal SoTien,
                    string MaPhieuNhap,
                    decimal TongTienNhap,
                    decimal DaThanhToan,
                    decimal ConNo,
                    string TrangThai)>(
                    selectAllocationsSql,
                    new { MaGiaoDich = record.MaGiaoDich },
                    transaction)).AsList();

                var soPhieuNhapHoanTac = 0;
                foreach (var alloc in allocations)
                {
                    if (!string.Equals(alloc.TrangThai, "DA_HUY", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(alloc.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase))
                    {
                        await connection.ExecuteAsync(
                            """
                            UPDATE phieu_nhap_hang
                            SET da_thanh_toan = GREATEST(0, da_thanh_toan - @SoTien),
                                con_no = LEAST(tong_tien_nhap, con_no + @SoTien),
                                ngay_cap_nhat = @Now
                            WHERE id = @PhieuNhapId;
                            """,
                            new { SoTien = alloc.SoTien, Now = now, PhieuNhapId = alloc.PhieuNhapId },
                            transaction);

                        soPhieuNhapHoanTac++;
                    }
                }

                // Cập nhật trạng thái dòng phân bổ trong thanh_toan_nha_cung_cap thành HUY
                await connection.ExecuteAsync(
                    """
                    UPDATE thanh_toan_nha_cung_cap
                    SET trang_thai = 'HUY',
                        thoi_gian_huy = @Now,
                        nguoi_huy_id = @UserId,
                        ly_do_huy = @LyDoHuy
                    WHERE (ma_giao_dich = @MaGiaoDich
                           OR ma_thanh_toan = @MaGiaoDich
                           OR ghi_chu LIKE CONCAT('%[Giao dịch ', @MaGiaoDich, ']%')
                           OR ghi_chu LIKE CONCAT('%', @MaGiaoDich, '%'))
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

                // 6. Hoàn tác công nợ trong sổ cái lich_su_cong_no_nha_cung_cap (ghi nợ = tăng nợ phải trả lại)
                var tienHoanTacCongNo = Math.Max(0m, record.SoTien - tienHoanTacTraTruoc);
                decimal congNoTruocKhiHuy = 0m;
                decimal congNoSauKhiHuy = 0m;

                if (tienHoanTacCongNo > 0)
                {
                    const string currentDebtSql = """
                    SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                    FROM lich_su_cong_no_nha_cung_cap
                    WHERE nha_cung_cap_id = @NhaCungCapId
                    FOR UPDATE;
                    """;

                    congNoTruocKhiHuy = await connection.ExecuteScalarAsync<decimal>(
                        currentDebtSql,
                        new { NhaCungCapId = record.NhaCungCapId },
                        transaction);

                    // Nếu chưa có dòng sổ cái nào, đọc nợ từ các phiếu nhập còn nợ hiện có
                    if (congNoTruocKhiHuy == 0m)
                    {
                        var countLedger = await connection.ExecuteScalarAsync<long>(
                            "SELECT COUNT(*) FROM lich_su_cong_no_nha_cung_cap WHERE nha_cung_cap_id = @NccId;",
                            new { NccId = record.NhaCungCapId },
                            transaction);

                        if (countLedger == 0)
                        {
                            congNoTruocKhiHuy = await connection.ExecuteScalarAsync<decimal>(
                                """
                                SELECT COALESCE(SUM(con_no), 0)
                                FROM phieu_nhap_hang
                                WHERE nha_cung_cap_id = @NccId
                                  AND trang_thai NOT IN ('LUU_TAM', 'HUY', 'DA_HUY');
                                """,
                                new { NccId = record.NhaCungCapId },
                                transaction);
                        }
                    }

                    congNoSauKhiHuy = congNoTruocKhiHuy + tienHoanTacCongNo;

                    const string insertReversalLedgerSql = """
                    INSERT INTO lich_su_cong_no_nha_cung_cap
                    (nha_cung_cap_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                     ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                    VALUES
                    (@NccId, @ChiNhanhId, 'HUY_THANH_TOAN', 'PHIEU_CHI', @MaChungTu,
                     @GhiNo, 0, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu);
                    """;

                    await connection.ExecuteAsync(
                        insertReversalLedgerSql,
                        new
                        {
                            NccId = record.NhaCungCapId,
                            ChiNhanhId = chiNhanhId,
                            MaChungTu = record.MaGiaoDich,
                            GhiNo = tienHoanTacCongNo,
                            SoDu = congNoSauKhiHuy,
                            ThoiGian = now,
                            NguoiThucHienId = userId,
                            GhiChu = $"Hoàn tác thanh toán công nợ do hủy giao dịch {record.MaGiaoDich}. Lý do: {lyDoHuy.Trim()}"
                        },
                        transaction);
                }
                else
                {
                    const string currentDebtSql = """
                    SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                    FROM lich_su_cong_no_nha_cung_cap
                    WHERE nha_cung_cap_id = @NhaCungCapId;
                    """;

                    congNoTruocKhiHuy = await connection.ExecuteScalarAsync<decimal>(
                        currentDebtSql,
                        new { NhaCungCapId = record.NhaCungCapId },
                        transaction);
                    congNoSauKhiHuy = congNoTruocKhiHuy;
                }

                // 7. Cập nhật phiếu chi trong sổ quỹ (phieu_chi) thành HUY
                await connection.ExecuteAsync(
                    """
                    UPDATE phieu_chi
                    SET trang_thai = 'HUY',
                        thoi_gian_huy = @Now,
                        nguoi_huy_id = @UserId,
                        ly_do_huy = @LyDoHuy
                    WHERE (ma_phieu_chi = @MaGiaoDich
                           OR (phieu_nhap_hang_id IS NOT NULL AND phieu_nhap_hang_id IN (
                               SELECT phieu_nhap_id FROM thanh_toan_nha_cung_cap WHERE ma_thanh_toan = @MaGiaoDich
                           )))
                      AND trang_thai = 'HOAN_THANH';
                    """,
                    new
                    {
                        MaGiaoDich = record.MaGiaoDich,
                        Now = now,
                        UserId = userId,
                        LyDoHuy = $"Hủy theo giao dịch thanh toán công nợ NCC {record.MaGiaoDich}: {lyDoHuy.Trim()}"
                    },
                    transaction);

                // 8. Cập nhật trạng thái phiếu trong lich_su_thanh_toan_cong_no_nha_cung_cap thành HUY
                await connection.ExecuteAsync(
                    """
                    UPDATE lich_su_thanh_toan_cong_no_nha_cung_cap
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
                    "Hủy thanh toán NCC",
                    "NHA_CUNG_CAP",
                    record.NhaCungCapId,
                    record.MaGiaoDich,
                    $"Hủy phiếu chi thanh toán công nợ {record.MaGiaoDich} của NCC {tenNhaCungCap}: {record.SoTien:N0} đ. Lý do: {lyDoHuy.Trim()}",
                    record.SoTien,
                    truongThayDoi: "Trạng thái phiếu thanh toán NCC, Công nợ NCC",
                    giaTriTruoc: $"HOÀN THÀNH | Công nợ NCC: {Math.Max(0m, congNoTruocKhiHuy):N0} đ",
                    giaTriSau: $"ĐÃ HỦY | Công nợ NCC: {Math.Max(0m, congNoSauKhiHuy):N0} đ",
                    connection: connection,
                    transaction: transaction);

                await transaction.CommitAsync();
                daCommit = true;

                return new HuyThanhToanCongNoNhaCungCapKetQua
                {
                    MaGiaoDich = record.MaGiaoDich,
                    NhaCungCapId = record.NhaCungCapId,
                    MaNhaCungCap = maNhaCungCap,
                    TenNhaCungCap = tenNhaCungCap,
                    SoTien = record.SoTien,
                    TienHoanTacCongNo = tienHoanTacCongNo,
                    TienHoanTacTraTruoc = tienHoanTacTraTruoc,
                    CongNoTruocKhiHuy = Math.Max(0m, congNoTruocKhiHuy),
                    CongNoSauKhiHuy = Math.Max(0m, congNoSauKhiHuy),
                    SoDuTraTruocTruocKhiHuy = soDuTraTruocTruoc,
                    SoDuTraTruocSauKhiHuy = soDuTraTruocSau,
                    SoPhieuNhapHoanTac = soPhieuNhapHoanTac,
                    ThoiGianHuy = now,
                    LyDoHuy = lyDoHuy.Trim(),
                    NguoiHuy = userName,
                    PhuongThuc = record.PhuongThuc,
                    PhieuChiDaHuy = true
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
                        SafeLog.WriteError("Rollback HuyThanhToanCongNoNhaCungCapAsync thất bại", rollbackEx);
                    }
                }

                SafeLog.WriteError($"Lỗi HuyThanhToanCongNoNhaCungCapAsync cho {maGiaoDich}", ex);
                throw;
            }
        }
        finally
        {
            _khoaHuyThanhToanCongNoNcc.Release();
        }
    }
}
