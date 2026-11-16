using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    /// <summary>
    /// Đồng bộ và tính lại công nợ, số dư trả trước và dòng lịch sử thanh toán
    /// cho khách hàng theo các giao dịch còn hiệu lực (Issue #122).
    /// </summary>
    public async Task<(decimal CongNoHienTai, decimal SoDuTraTruoc)> DongBoVaTinhLaiCongNoKhachHangAsync(
        uint khachHangId,
        uint? userId = null)
    {
        if (khachHangId == 0)
            return (0m, 0m);

        await EnsureBangLichSuThanhToanKhachHangAsync();
        await EnsurePhieuQuyCancelColumnsAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await EnsureTraTruocSchemaAsync(connection, transaction);

        try
        {
            var result = await DongBoVaTinhLaiCongNoKhachHangTrongGiaoDichAsync(
                connection,
                transaction,
                khachHangId,
                userId);
            await transaction.CommitAsync();
            return result;
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rbEx)
            {
                SafeLog.WriteError("Rollback DongBoVaTinhLaiCongNoKhachHangAsync thất bại", rbEx);
            }

            SafeLog.WriteError($"Lỗi DongBoVaTinhLaiCongNoKhachHangAsync cho khách {khachHangId}", ex);
            throw;
        }
    }

    /// <summary>
    /// Tính lại toàn bộ công nợ và số dư trả trước trong một database transaction.
    /// Loại bỏ các phiếu đã hủy khỏi phép tính công nợ và tiền trả trước.
    /// Cập nhật lại công nợ trước/sau của các giao dịch còn hiệu lực.
    /// </summary>
    public async Task<(decimal CongNoHienTai, decimal SoDuTraTruoc)> DongBoVaTinhLaiCongNoKhachHangTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint khachHangId,
        uint? userId = null)
    {
        if (khachHangId == 0)
            return (0m, 0m);

        // 1. Khóa và lấy thông tin khách hàng
        const string customerSql = """
        SELECT
            id AS Id,
            ma_khach_hang AS MaKhachHang,
            ten_khach_hang AS TenKhachHang,
            chi_nhanh_id AS ChiNhanhId,
            COALESCE(so_du_tra_truoc, 0) AS SoDuTraTruoc,
            COALESCE(co_du_lieu_kiotviet, 0) AS CoDuLieuKiotViet,
            no_can_thu_hien_tai AS NoCanThuHienTai
        FROM khach_hang
        WHERE id = @KhachHangId
        LIMIT 1
        FOR UPDATE;
        """;

        var khach = await connection.QueryFirstOrDefaultAsync<(
            uint Id,
            string? MaKhachHang,
            string? TenKhachHang,
            uint? ChiNhanhId,
            decimal SoDuTraTruoc,
            bool CoDuLieuKiotViet,
            decimal? NoCanThuHienTai)>(
            customerSql,
            new { KhachHangId = khachHangId },
            transaction);

        if (khach.Id == 0)
            return (0m, 0m);

        // 2. Lấy toàn bộ phiếu thanh toán công nợ theo thứ tự thời gian
        const string paymentSlipsSql = """
        SELECT
            id AS Id,
            ma_giao_dich AS MaGiaoDich,
            so_tien AS SoTien,
            cong_no_truoc AS CongNoTruoc,
            cong_no_sau AS CongNoSau,
            thoi_gian AS ThoiGian,
            phuong_thuc AS PhuongThuc,
            trang_thai AS TrangThai
        FROM lich_su_thanh_toan_cong_no_khach_hang
        WHERE khach_hang_id = @KhachHangId
        ORDER BY thoi_gian ASC, id ASC
        FOR UPDATE;
        """;

        var slips = (await connection.QueryAsync<ThanhToanCongNoKhachHang>(
            paymentSlipsSql,
            new { KhachHangId = khachHangId },
            transaction)).AsList();

        if (slips.Count == 0)
        {
            return (0m, khach.SoDuTraTruoc);
        }

        // 3. Tính lại công nợ chạy (running debt) và số dư trả trước chạy theo các giao dịch CÒN HIỆU LỰC
        // Loại bỏ hoàn toàn các phiếu đã hủy khỏi toàn bộ phép tính công nợ và trả trước
        var validSlips = slips.Where(s => !string.Equals(s.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase)).ToList();

        // Đảm bảo các phiếu đã hủy không còn phân bổ trong bảng thanh_toan
        var cancelledSlips = slips.Where(s => string.Equals(s.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var cSlip in cancelledSlips)
        {
            await connection.ExecuteAsync(
                """
                UPDATE thanh_toan
                SET trang_thai = 'HUY'
                WHERE (ma_giao_dich = @MaGiaoDich OR ghi_chu LIKE CONCAT('%[Phiếu thu ', @MaGiaoDich, ']%'))
                  AND trang_thai = 'HOAN_THANH';
                """,
                new { MaGiaoDich = cSlip.MaGiaoDich },
                transaction);
        }

        decimal initialDebt = slips[0].CongNoTruoc;
        decimal runningDebt = initialDebt;
        decimal runningPrepaid = 0m;

        for (int k = 0; k < validSlips.Count; k++)
        {
            var slip = validSlips[k];

            decimal newCongNoTruoc = runningDebt;
            decimal tienTraNo = Math.Min(slip.SoTien, runningDebt);
            decimal tienDu = Math.Max(0m, slip.SoTien - runningDebt);

            runningDebt -= tienTraNo;
            runningPrepaid += tienDu;
            decimal newCongNoSau = runningDebt;

            // Cập nhật công nợ trước/sau của phiếu hợp lệ nếu thay đổi
            if (slip.CongNoTruoc != newCongNoTruoc || slip.CongNoSau != newCongNoSau)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE lich_su_thanh_toan_cong_no_khach_hang
                    SET cong_no_truoc = @CongNoTruoc,
                        cong_no_sau = @CongNoSau
                    WHERE id = @Id;
                    """,
                    new { CongNoTruoc = newCongNoTruoc, CongNoSau = newCongNoSau, Id = slip.Id },
                    transaction);

                slip.CongNoTruoc = newCongNoTruoc;
                slip.CongNoSau = newCongNoSau;
            }
        }

        // 4. Cập nhật số dư trả trước của khách hàng
        if (khach.SoDuTraTruoc != runningPrepaid)
        {
            decimal diff = runningPrepaid - khach.SoDuTraTruoc;

            await connection.ExecuteAsync(
                """
                UPDATE khach_hang
                SET so_du_tra_truoc = @NewPrepaid
                WHERE id = @KhachHangId;
                """,
                new { NewPrepaid = runningPrepaid, KhachHangId = khachHangId },
                transaction);

            var maGdTraTruoc = await SinhMaGiaoDichTraTruocTrongGiaoDichAsync(
                connection,
                transaction,
                diff < 0 ? "HTKH" : "TTDU-KH",
                "lich_su_tra_truoc_khach_hang");

            var lastValidSlip = slips.LastOrDefault(s => !string.Equals(s.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase));
            var maChungTuRef = lastValidSlip?.MaGiaoDich ?? slips.Last().MaGiaoDich;

            await GhiLichSuTraTruocKhachHangTrongGiaoDichAsync(
                connection,
                transaction,
                khachHangId,
                khach.MaKhachHang ?? "",
                maGdTraTruoc,
                diff < 0 ? "HOAN_TRA" : "THANH_TOAN_DU",
                "PHIEU_THU",
                maChungTuRef,
                Math.Abs(diff),
                khach.SoDuTraTruoc,
                runningPrepaid,
                "HE_THONG",
                DateTime.Now,
                Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Hệ thống",
                "Tính lại số dư trả trước theo các giao dịch còn hiệu lực (Issue #122)");
        }

        // 5. Cập nhật các hóa đơn bán hàng theo tổng số tiền thực trả còn hiệu lực
        decimal tongTienTraNoHopLe = slips
            .Where(s => !string.Equals(s.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase))
            .Sum(s => Math.Min(s.SoTien, s.CongNoTruoc));

        const string invoicesSql = """
        SELECT
            id AS Id,
            chi_nhanh_id AS ChiNhanhId,
            ma_hoa_don AS MaHoaDon,
            khach_can_tra AS KhachCanTra,
            khach_da_thanh_toan AS KhachDaThanhToan,
            con_no AS ConNo,
            trang_thai_thanh_toan AS TrangThaiThanhToan
        FROM hoa_don_ban_hang
        WHERE khach_hang_id = @KhachHangId
          AND trang_thai_hoa_don = 'HOAN_THANH'
        ORDER BY COALESCE(thoi_gian_ban, ngay_tao, NOW()) ASC, id ASC
        FOR UPDATE;
        """;

        var invoices = (await connection.QueryAsync<HoaDonCongNoRow>(
            invoicesSql,
            new { KhachHangId = khachHangId },
            transaction)).AsList();

        decimal conLaiPhanBo = tongTienTraNoHopLe;

        foreach (var inv in invoices)
        {
            // Số tiền đã thu qua các phiếu thu công nợ hợp lệ cho hóa đơn này
            var validAllocated = await connection.ExecuteScalarAsync<decimal>(
                """
                SELECT COALESCE(SUM(so_tien), 0)
                FROM thanh_toan
                WHERE hoa_don_id = @HoaDonId
                  AND trang_thai = 'HOAN_THANH';
                """,
                new { HoaDonId = inv.Id },
                transaction);

            // Số tiền khách đã trả tại quầy lúc bán
            decimal daTraTaiQuay = Math.Max(0m, inv.KhachDaThanhToan - validAllocated);
            decimal conNoChuaThu = Math.Max(0m, inv.KhachCanTra - daTraTaiQuay);

            decimal thuThem = Math.Min(conLaiPhanBo, conNoChuaThu);
            decimal daTraMoi = daTraTaiQuay + thuThem;
            decimal conNoMoi = Math.Max(0m, inv.KhachCanTra - daTraMoi);
            string trangThaiMoi = conNoMoi <= 0 ? "DA_THANH_TOAN" : (daTraMoi > 0 ? "CONG_NO" : "CHUA_THANH_TOAN");

            if (inv.KhachDaThanhToan != daTraMoi || inv.ConNo != conNoMoi || inv.TrangThaiThanhToan != trangThaiMoi)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE hoa_don_ban_hang
                    SET khach_da_thanh_toan = @DaTra,
                        con_no = @ConNo,
                        trang_thai_thanh_toan = @TrangThai,
                        ngay_cap_nhat = NOW()
                    WHERE id = @Id;
                    """,
                    new { DaTra = daTraMoi, ConNo = conNoMoi, TrangThai = trangThaiMoi, Id = inv.Id },
                    transaction);
            }

            conLaiPhanBo -= thuThem;
        }

        // 6. Cập nhật Sổ cái công nợ (lich_su_cong_no_khach_hang) để khớp với runningDebt
        const string currentLedgerDebtSql = """
        SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
        FROM lich_su_cong_no_khach_hang
        WHERE khach_hang_id = @KhachHangId
        FOR UPDATE;
        """;

        var currentLedgerDebt = await connection.ExecuteScalarAsync<decimal>(
            currentLedgerDebtSql,
            new { KhachHangId = khachHangId },
            transaction);

        decimal noKiotVietKhach = (khach.CoDuLieuKiotViet && khach.NoCanThuHienTai.HasValue) ? khach.NoCanThuHienTai.Value : 0m;
        decimal actualCurrentDebt = noKiotVietKhach + currentLedgerDebt;
        decimal diffLedger = actualCurrentDebt - runningDebt;
        if (diffLedger != 0m)
        {
            var lastValidSlip = slips.LastOrDefault(s => !string.Equals(s.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase));
            var maChungTuRef = lastValidSlip?.MaGiaoDich ?? slips.Last().MaGiaoDich;

            if (diffLedger > 0)
            {
                // Sổ cái nợ nhiều hơn thực tế -> ghi có để giảm nợ về đúng runningDebt
                await connection.ExecuteAsync(
                    """
                    INSERT INTO lich_su_cong_no_khach_hang
                    (khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                     ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                    VALUES
                    (@KhachHangId, @ChiNhanhId, 'DIEU_CHINH', 'PHIEU_THU', @MaChungTu,
                     0, @GhiCo, @SoDu, NOW(), @UserId, @GhiChu);
                    """,
                    new
                    {
                        KhachHangId = khachHangId,
                        ChiNhanhId = khach.ChiNhanhId ?? 1,
                        MaChungTu = maChungTuRef,
                        GhiCo = diffLedger,
                        SoDu = runningDebt,
                        UserId = userId,
                        GhiChu = "Điều chỉnh công nợ theo các giao dịch còn hiệu lực sau khi hủy phiếu (Issue #122)"
                    },
                    transaction);
            }
            else
            {
                // Sổ cái nợ ít hơn thực tế -> ghi nợ để tăng nợ lên đúng runningDebt
                await connection.ExecuteAsync(
                    """
                    INSERT INTO lich_su_cong_no_khach_hang
                    (khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                     ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                    VALUES
                    (@KhachHangId, @ChiNhanhId, 'DIEU_CHINH', 'PHIEU_THU', @MaChungTu,
                     @GhiNo, 0, @SoDu, NOW(), @UserId, @GhiChu);
                    """,
                    new
                    {
                        KhachHangId = khachHangId,
                        ChiNhanhId = khach.ChiNhanhId ?? 1,
                        MaChungTu = maChungTuRef,
                        GhiNo = Math.Abs(diffLedger),
                        SoDu = runningDebt,
                        UserId = userId,
                        GhiChu = "Điều chỉnh công nợ theo các giao dịch còn hiệu lực sau khi hủy phiếu (Issue #122)"
                    },
                    transaction);
            }
        }

        return (runningDebt, runningPrepaid);
    }

    /// <summary>
    /// Đồng bộ và tính lại công nợ, số dư trả trước và dòng lịch sử thanh toán
    /// cho nhà cung cấp theo các giao dịch còn hiệu lực (Issue #122).
    /// </summary>
    public async Task<(decimal CongNoHienTai, decimal SoDuTraTruoc)> DongBoVaTinhLaiCongNoNhaCungCapAsync(
        uint nhaCungCapId,
        uint? userId = null)
    {
        if (nhaCungCapId == 0)
            return (0m, 0m);

        await EnsureBangLichSuThanhToanNhaCungCapAsync();
        await EnsurePhieuQuyCancelColumnsAsync();
        await EnsurePhieuNhapTrackingColumnsAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await EnsureTraTruocSchemaAsync(connection, transaction);

        try
        {
            var result = await DongBoVaTinhLaiCongNoNhaCungCapTrongGiaoDichAsync(
                connection,
                transaction,
                nhaCungCapId,
                userId);
            await transaction.CommitAsync();
            return result;
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rbEx)
            {
                SafeLog.WriteError("Rollback DongBoVaTinhLaiCongNoNhaCungCapAsync thất bại", rbEx);
            }

            SafeLog.WriteError($"Lỗi DongBoVaTinhLaiCongNoNhaCungCapAsync cho NCC {nhaCungCapId}", ex);
            throw;
        }
    }

    /// <summary>
    /// Tính lại toàn bộ công nợ và số dư trả trước của nhà cung cấp trong một transaction.
    /// Loại bỏ các phiếu đã hủy khỏi phép tính công nợ và tiền trả trước.
    /// Cập nhật lại công nợ trước/sau của các giao dịch còn hiệu lực.
    /// </summary>
    public async Task<(decimal CongNoHienTai, decimal SoDuTraTruoc)> DongBoVaTinhLaiCongNoNhaCungCapTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint nhaCungCapId,
        uint? userId = null)
    {
        if (nhaCungCapId == 0)
            return (0m, 0m);

        // 1. Khóa và lấy thông tin nhà cung cấp
        const string supplierSql = """
        SELECT
            id AS Id,
            ma_nha_cung_cap AS MaNhaCungCap,
            ten_nha_cung_cap AS TenNhaCungCap,
            chi_nhanh_id AS ChiNhanhId,
            COALESCE(so_du_tra_truoc, 0) AS SoDuTraTruoc,
            COALESCE(co_du_lieu_kiotviet, 0) AS CoDuLieuKiotViet,
            no_can_tra_hien_tai AS NoCanTraHienTai
        FROM nha_cung_cap
        WHERE id = @NhaCungCapId
        LIMIT 1
        FOR UPDATE;
        """;

        var supplier = await connection.QueryFirstOrDefaultAsync<(
            uint Id,
            string? MaNhaCungCap,
            string? TenNhaCungCap,
            uint? ChiNhanhId,
            decimal SoDuTraTruoc,
            bool CoDuLieuKiotViet,
            decimal? NoCanTraHienTai)>(
            supplierSql,
            new { NhaCungCapId = nhaCungCapId },
            transaction);

        if (supplier.Id == 0)
            return (0m, 0m);

        // 2. Lấy toàn bộ giao dịch thanh toán công nợ NCC theo thứ tự thời gian
        const string paymentSlipsSql = """
        SELECT
            id AS Id,
            ma_giao_dich AS MaGiaoDich,
            so_tien AS SoTien,
            cong_no_truoc AS CongNoTruoc,
            cong_no_sau AS CongNoSau,
            thoi_gian AS ThoiGian,
            phuong_thuc AS PhuongThuc,
            trang_thai AS TrangThai
        FROM lich_su_thanh_toan_cong_no_nha_cung_cap
        WHERE nha_cung_cap_id = @NhaCungCapId
        ORDER BY thoi_gian ASC, id ASC
        FOR UPDATE;
        """;

        var slips = (await connection.QueryAsync<ThanhToanCongNoNhaCungCap>(
            paymentSlipsSql,
            new { NhaCungCapId = nhaCungCapId },
            transaction)).AsList();

        if (slips.Count == 0)
        {
            return (0m, supplier.SoDuTraTruoc);
        }

        // 3. Tính lại công nợ chạy và số dư trả trước chạy theo các giao dịch CÒN HIỆU LỰC
        // Loại bỏ hoàn toàn các phiếu đã hủy khỏi toàn bộ phép tính công nợ và trả trước
        var validSlips = slips.Where(s => !string.Equals(s.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase)).ToList();

        // Đảm bảo các phiếu đã hủy không còn phân bổ trong bảng thanh_toan_nha_cung_cap
        var cancelledSlips = slips.Where(s => string.Equals(s.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var cSlip in cancelledSlips)
        {
            await connection.ExecuteAsync(
                """
                UPDATE thanh_toan_nha_cung_cap
                SET trang_thai = 'HUY'
                WHERE (ma_giao_dich = @MaGiaoDich
                       OR ma_thanh_toan = @MaGiaoDich
                       OR ghi_chu LIKE CONCAT('%[Giao dịch ', @MaGiaoDich, ']%')
                       OR ghi_chu LIKE CONCAT('%', @MaGiaoDich, '%'))
                  AND trang_thai = 'HOAN_THANH';
                """,
                new { MaGiaoDich = cSlip.MaGiaoDich },
                transaction);
        }

        decimal initialDebt = slips[0].CongNoTruoc;
        decimal runningDebt = initialDebt;
        decimal runningPrepaid = 0m;

        for (int k = 0; k < validSlips.Count; k++)
        {
            var slip = validSlips[k];

            decimal newCongNoTruoc = runningDebt;
            decimal tienTraNo = Math.Min(slip.SoTien, runningDebt);
            decimal tienDu = Math.Max(0m, slip.SoTien - runningDebt);

            runningDebt -= tienTraNo;
            runningPrepaid += tienDu;
            decimal newCongNoSau = runningDebt;

            if (slip.CongNoTruoc != newCongNoTruoc || slip.CongNoSau != newCongNoSau)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE lich_su_thanh_toan_cong_no_nha_cung_cap
                    SET cong_no_truoc = @CongNoTruoc,
                        cong_no_sau = @CongNoSau
                    WHERE id = @Id;
                    """,
                    new { CongNoTruoc = newCongNoTruoc, CongNoSau = newCongNoSau, Id = slip.Id },
                    transaction);

                slip.CongNoTruoc = newCongNoTruoc;
                slip.CongNoSau = newCongNoSau;
            }
        }

        // 4. Cập nhật số dư trả trước của nhà cung cấp
        if (supplier.SoDuTraTruoc != runningPrepaid)
        {
            decimal diff = runningPrepaid - supplier.SoDuTraTruoc;

            await connection.ExecuteAsync(
                """
                UPDATE nha_cung_cap
                SET so_du_tra_truoc = @NewPrepaid
                WHERE id = @NhaCungCapId;
                """,
                new { NewPrepaid = runningPrepaid, NhaCungCapId = nhaCungCapId },
                transaction);

            var maGdTraTruoc = await SinhMaGiaoDichTraTruocTrongGiaoDichAsync(
                connection,
                transaction,
                diff < 0 ? "HTNCC" : "TTDU-NCC",
                "lich_su_tra_truoc_ncc");

            var lastValidSlip = slips.LastOrDefault(s => !string.Equals(s.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase));
            var maChungTuRef = lastValidSlip?.MaGiaoDich ?? slips.Last().MaGiaoDich;

            await GhiLichSuTraTruocNhaCungCapTrongGiaoDichAsync(
                connection,
                transaction,
                nhaCungCapId,
                supplier.MaNhaCungCap ?? "",
                maGdTraTruoc,
                diff < 0 ? "HOAN_TRA" : "THANH_TOAN_DU",
                "PHIEU_CHI",
                maChungTuRef,
                Math.Abs(diff),
                supplier.SoDuTraTruoc,
                runningPrepaid,
                "HE_THONG",
                DateTime.Now,
                Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Hệ thống",
                "Tính lại số dư trả trước theo các giao dịch còn hiệu lực (Issue #122)");
        }

        // 5. Cập nhật các phiếu nhập hàng theo tổng số tiền thực trả còn hiệu lực
        decimal tongTienTraNoHopLe = slips
            .Where(s => !string.Equals(s.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase))
            .Sum(s => Math.Min(s.SoTien, s.CongNoTruoc));

        const string poSql = """
        SELECT
            id AS Id,
            chi_nhanh_id AS ChiNhanhId,
            ma_phieu_nhap AS MaPhieuNhap,
            tong_tien_nhap AS TongTienNhap,
            da_thanh_toan AS DaThanhToan,
            con_no AS ConNo,
            trang_thai AS TrangThai
        FROM phieu_nhap_hang
        WHERE nha_cung_cap_id = @NhaCungCapId
          AND trang_thai NOT IN ('LUU_TAM', 'HUY', 'DA_HUY')
        ORDER BY COALESCE(thoi_gian_nhap, ngay_tao, NOW()) ASC, id ASC
        FOR UPDATE;
        """;

        var poList = (await connection.QueryAsync<(
            uint Id,
            uint ChiNhanhId,
            string MaPhieuNhap,
            decimal TongTienNhap,
            decimal DaThanhToan,
            decimal ConNo,
            string TrangThai)>(
            poSql,
            new { NhaCungCapId = nhaCungCapId },
            transaction)).AsList();

        decimal conLaiPhanBo = tongTienTraNoHopLe;

        foreach (var po in poList)
        {
            var validAllocated = await connection.ExecuteScalarAsync<decimal>(
                """
                SELECT COALESCE(SUM(so_tien), 0)
                FROM thanh_toan_nha_cung_cap
                WHERE phieu_nhap_id = @PhieuNhapId
                  AND trang_thai = 'HOAN_THANH';
                """,
                new { PhieuNhapId = po.Id },
                transaction);

            decimal daTraBanDau = Math.Max(0m, po.DaThanhToan - validAllocated);
            decimal conNoChuaTra = Math.Max(0m, po.TongTienNhap - daTraBanDau);

            decimal thuThem = Math.Min(conLaiPhanBo, conNoChuaTra);
            decimal daTraMoi = daTraBanDau + thuThem;
            decimal conNoMoi = Math.Max(0m, po.TongTienNhap - daTraMoi);
            string trangThaiMoi = conNoMoi <= 0 ? "HOAN_THANH" : "DA_NHAP_HANG";

            if (po.DaThanhToan != daTraMoi || po.ConNo != conNoMoi || po.TrangThai != trangThaiMoi)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE phieu_nhap_hang
                    SET da_thanh_toan = @DaTra,
                        con_no = @ConNo,
                        trang_thai = @TrangThai,
                        ngay_cap_nhat = NOW()
                    WHERE id = @Id;
                    """,
                    new { DaTra = daTraMoi, ConNo = conNoMoi, TrangThai = trangThaiMoi, Id = po.Id },
                    transaction);
            }

            conLaiPhanBo -= thuThem;
        }

        // 6. Cập nhật Sổ cái công nợ NCC (lich_su_cong_no_nha_cung_cap) để khớp với runningDebt
        const string currentLedgerDebtSql = """
        SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
        FROM lich_su_cong_no_nha_cung_cap
        WHERE nha_cung_cap_id = @NhaCungCapId
        FOR UPDATE;
        """;

        var currentLedgerDebt = await connection.ExecuteScalarAsync<decimal>(
            currentLedgerDebtSql,
            new { NhaCungCapId = nhaCungCapId },
            transaction);

        decimal noKiotVietNcc = (supplier.CoDuLieuKiotViet && supplier.NoCanTraHienTai.HasValue) ? supplier.NoCanTraHienTai.Value : 0m;
        decimal actualCurrentDebt = noKiotVietNcc + currentLedgerDebt;
        decimal diffLedger = actualCurrentDebt - runningDebt;
        if (diffLedger != 0m)
        {
            var lastValidSlip = slips.LastOrDefault(s => !string.Equals(s.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase));
            var maChungTuRef = lastValidSlip?.MaGiaoDich ?? slips.Last().MaGiaoDich;

            if (diffLedger > 0)
            {
                // Sổ cái nợ NCC nhiều hơn thực tế -> ghi có để giảm nợ về đúng runningDebt
                await connection.ExecuteAsync(
                    """
                    INSERT INTO lich_su_cong_no_nha_cung_cap
                    (nha_cung_cap_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                     ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                    VALUES
                    (@NccId, @ChiNhanhId, 'DIEU_CHINH', 'PHIEU_CHI', @MaChungTu,
                     0, @GhiCo, @SoDu, NOW(), @UserId, @GhiChu);
                    """,
                    new
                    {
                        NccId = nhaCungCapId,
                        ChiNhanhId = supplier.ChiNhanhId ?? 1,
                        MaChungTu = maChungTuRef,
                        GhiCo = diffLedger,
                        SoDu = runningDebt,
                        UserId = userId,
                        GhiChu = "Điều chỉnh công nợ theo các giao dịch còn hiệu lực sau khi hủy phiếu (Issue #122)"
                    },
                    transaction);
            }
            else
            {
                // Sổ cái nợ NCC ít hơn thực tế -> ghi nợ để tăng nợ lên đúng runningDebt
                await connection.ExecuteAsync(
                    """
                    INSERT INTO lich_su_cong_no_nha_cung_cap
                    (nha_cung_cap_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
                     ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                    VALUES
                    (@NccId, @ChiNhanhId, 'DIEU_CHINH', 'PHIEU_CHI', @MaChungTu,
                     @GhiNo, 0, @SoDu, NOW(), @UserId, @GhiChu);
                    """,
                    new
                    {
                        NccId = nhaCungCapId,
                        ChiNhanhId = supplier.ChiNhanhId ?? 1,
                        MaChungTu = maChungTuRef,
                        GhiNo = Math.Abs(diffLedger),
                        SoDu = runningDebt,
                        UserId = userId,
                        GhiChu = "Điều chỉnh công nợ theo các giao dịch còn hiệu lực sau khi hủy phiếu (Issue #122)"
                    },
                    transaction);
            }
        }

        return (runningDebt, runningPrepaid);
    }
}
