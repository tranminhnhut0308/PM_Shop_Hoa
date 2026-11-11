using System.Globalization;
using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    /// <summary>
    /// ISSUE #95: Kiểm tra các ràng buộc dữ liệu, lịch sử giao dịch và công nợ của khách hàng
    /// trước khi quyết định xóa cứng hay chuyển sang ngừng hoạt động.
    /// </summary>
    public async Task<ThongTinKiemTraXoaDoiTac> KiemTraXoaKhachHangAsync(uint customerId)
    {
        await using var connection = await OpenAsync();

        var kh = await connection.QueryFirstOrDefaultAsync<KhachHang>(
            "SELECT id AS Id, ma_khach_hang AS MaKhachHang, ten_khach_hang AS TenKhachHang, trang_thai AS TrangThai, COALESCE(so_du_tra_truoc, 0) AS SoDuTraTruoc FROM khach_hang WHERE id = @Id;",
            new { Id = customerId });

        if (kh == null)
            throw new InvalidOperationException("Khách hàng không tồn tại trong hệ thống.");

        var coBangTraHang = await HasBangTraHangAsync();
        var coCotKiotViet = await DamBaoCotDuLieuKiotVietAsync();
        var tienExpr = TaoBieuThucTienKhachHang(coBangTraHang, "kh.id", coCotKiotViet);
        var sqlCongNo = $"SELECT COALESCE({tienExpr.NoHienTai}, 0) FROM khach_hang kh WHERE kh.id = @Id;";
        var congNo = await connection.ExecuteScalarAsync<decimal>(sqlCongNo, new { Id = customerId });

        var soHoaDon = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM hoa_don_ban_hang WHERE khach_hang_id = @Id;",
            new { Id = customerId });

        var soCongNo = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM lich_su_cong_no_khach_hang WHERE khach_hang_id = @Id;",
            new { Id = customerId });

        var soThanhToan = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM thanh_toan WHERE khach_hang_id = @Id;",
            new { Id = customerId });

        var soLsThanhToan = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM lich_su_thanh_toan_cong_no_khach_hang WHERE khach_hang_id = @Id;",
            new { Id = customerId });

        var soTraTruoc = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM lich_su_tra_truoc_khach_hang WHERE khach_hang_id = @Id;",
            new { Id = customerId });

        var soTraHang = 0;
        if (coBangTraHang)
        {
            soTraHang = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM phieu_tra_hang WHERE khach_hang_id = @Id;",
                new { Id = customerId });
        }

        var tongGiaoDich = soHoaDon + soCongNo + soThanhToan + soLsThanhToan + soTraTruoc + soTraHang;

        return new ThongTinKiemTraXoaDoiTac
        {
            CoDuLieuGiaoDich = tongGiaoDich > 0,
            CongNoHienTai = congNo,
            SoDuTraTruoc = kh.SoDuTraTruoc,
            SoHoaDonHoacPhieuNhap = soHoaDon,
            SoGiaoDichLienQuan = tongGiaoDich,
            DaNgungHoatDong = !kh.TrangThai
        };
    }

    /// <summary>
    /// ISSUE #95: Xóa khách hàng theo hướng an toàn dữ liệu:
    /// 1. Chỉ Admin mới được thực hiện.
    /// 2. Còn công nợ -> Không cho xóa cứng. Nếu xác nhận, chuyển sang ngừng hoạt động để tiếp tục thu hồi nợ.
    /// 3. Đã có giao dịch liên quan -> Chuyển sang ngừng hoạt động để bảo toàn lịch sử.
    /// 4. Chưa từng phát sinh giao dịch nào -> Xóa cứng khỏi database.
    /// </summary>
    public async Task<KetQuaXoaDoiTac> XoaKhachHangSafeAsync(uint customerId, bool xacNhanNgungHoatDong = false)
    {
        Quyen.RequireAdmin(Session.CurrentUser, "Chỉ tài khoản Quản trị (Admin) mới có quyền thực hiện thao tác xóa hoặc ngừng hoạt động khách hàng.");

        var kiemTra = await KiemTraXoaKhachHangAsync(customerId);
        await using var connection = await OpenAsync();

        // 1. Còn công nợ hoặc còn số dư trả trước
        if (kiemTra.CongNoHienTai > 0 || kiemTra.SoDuTraTruoc > 0)
        {
            if (!xacNhanNgungHoatDong)
            {
                return KetQuaXoaDoiTac.ConCongNo;
            }

            var affectedRows = await connection.ExecuteAsync(
                "UPDATE khach_hang SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id;",
                new { Id = customerId });

            if (affectedRows == 0)
                throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã ngừng hoạt động.");

            _ = LogSubAccountActivityAsync("Ngừng hoạt động khách hàng", "KHACH_HANG", customerId, null,
                $"Chuyển khách hàng sang ngừng hoạt động do còn nợ ({kiemTra.CongNoHienTai:N0} đ, id={customerId})");

            return KetQuaXoaDoiTac.DaChuyenNgungHoatDong;
        }

        // 2. Không còn nợ nhưng đã từng phát sinh giao dịch -> Bảo toàn lịch sử
        if (kiemTra.CoDuLieuGiaoDich)
        {
            var affectedRows = await connection.ExecuteAsync(
                "UPDATE khach_hang SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id;",
                new { Id = customerId });

            if (affectedRows == 0)
                throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã ngừng hoạt động.");

            _ = LogSubAccountActivityAsync("Ngừng hoạt động khách hàng", "KHACH_HANG", customerId, null,
                $"Chuyển khách hàng sang ngừng hoạt động để bảo toàn {kiemTra.SoHoaDonHoacPhieuNhap} hóa đơn và {kiemTra.SoGiaoDichLienQuan} giao dịch liên quan (id={customerId})");

            return KetQuaXoaDoiTac.DaChuyenNgungHoatDong;
        }

        // 3. Chưa phát sinh bất kỳ giao dịch nào và không nợ -> XÓA CỨNG
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync(
                "DELETE FROM don_ban_nhap WHERE khach_hang_id = @Id;",
                new { Id = customerId }, tx);

            var affected = await connection.ExecuteAsync(
                "DELETE FROM khach_hang WHERE id = @Id;",
                new { Id = customerId }, tx);

            if (affected == 0)
                throw new InvalidOperationException("Khách hàng không tồn tại.");

            await tx.CommitAsync();

            _ = LogSubAccountActivityAsync("Xóa khách hàng", "KHACH_HANG", customerId, null,
                $"Đã xóa vĩnh viễn khách hàng chưa phát sinh giao dịch (id={customerId})");

            return KetQuaXoaDoiTac.DaXoaVinhVien;
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            await tx.RollbackAsync();
            await connection.ExecuteAsync(
                "UPDATE khach_hang SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id;",
                new { Id = customerId });

            return KetQuaXoaDoiTac.DaChuyenNgungHoatDong;
        }
    }

    /// <summary>
    /// Kích hoạt lại khách hàng đã bị ngừng hoạt động. Chỉ Admin mới được thực hiện.
    /// </summary>
    public async Task KichHoatLaiKhachHangAsync(uint customerId)
    {
        Quyen.RequireAdmin(Session.CurrentUser, "Chỉ tài khoản Quản trị (Admin) mới có quyền kích hoạt lại khách hàng.");
        await using var connection = await OpenAsync();

        var affected = await connection.ExecuteAsync(
            "UPDATE khach_hang SET trang_thai = 1, ngay_cap_nhat = NOW() WHERE id = @Id;",
            new { Id = customerId });

        if (affected == 0)
            throw new InvalidOperationException("Khách hàng không tồn tại.");

        _ = LogSubAccountActivityAsync("Kích hoạt lại khách hàng", "KHACH_HANG", customerId, null,
            $"Kích hoạt lại trạng thái hoạt động cho khách hàng (id={customerId})");
    }

    /// <summary>
    /// ISSUE #95: Kiểm tra các ràng buộc dữ liệu, lịch sử giao dịch và công nợ của nhà cung cấp
    /// trước khi quyết định xóa cứng hay chuyển sang ngừng hoạt động.
    /// </summary>
    public async Task<ThongTinKiemTraXoaDoiTac> KiemTraXoaNhaCungCapAsync(uint supplierId)
    {
        await using var connection = await OpenAsync();

        var ncc = await connection.QueryFirstOrDefaultAsync<NhaCungCap>(
            "SELECT id AS Id, ma_nha_cung_cap AS MaNhaCungCap, ten_nha_cung_cap AS TenNhaCungCap, trang_thai AS TrangThai, COALESCE(so_du_tra_truoc, 0) AS SoDuTraTruoc FROM nha_cung_cap WHERE id = @Id;",
            new { Id = supplierId });

        if (ncc == null)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại trong hệ thống.");

        var coCotKiotViet = await DamBaoCotDuLieuKiotVietNhaCungCapAsync();
        var tienExpr = TaoBieuThucTienNhaCungCap("ncc.id", coCotKiotViet);
        var sqlCongNo = $"SELECT COALESCE({tienExpr.NoHienTai}, 0) FROM nha_cung_cap ncc WHERE ncc.id = @Id;";
        var congNo = await connection.ExecuteScalarAsync<decimal>(sqlCongNo, new { Id = supplierId });

        var soPhieuNhap = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM phieu_nhap_hang WHERE nha_cung_cap_id = @Id;",
            new { Id = supplierId });

        var soCongNo = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM lich_su_cong_no_nha_cung_cap WHERE nha_cung_cap_id = @Id;",
            new { Id = supplierId });

        var soThanhToan = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM thanh_toan WHERE nha_cung_cap_id = @Id;",
            new { Id = supplierId });

        var soLsThanhToan = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM lich_su_thanh_toan_nha_cung_cap WHERE nha_cung_cap_id = @Id;",
            new { Id = supplierId });

        var soTraTruoc = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM lich_su_tra_truoc_nha_cung_cap WHERE nha_cung_cap_id = @Id;",
            new { Id = supplierId });

        var tongGiaoDich = soPhieuNhap + soCongNo + soThanhToan + soLsThanhToan + soTraTruoc;

        return new ThongTinKiemTraXoaDoiTac
        {
            CoDuLieuGiaoDich = tongGiaoDich > 0,
            CongNoHienTai = congNo,
            SoDuTraTruoc = ncc.SoDuTraTruoc,
            SoHoaDonHoacPhieuNhap = soPhieuNhap,
            SoGiaoDichLienQuan = tongGiaoDich,
            DaNgungHoatDong = !ncc.TrangThai
        };
    }

    /// <summary>
    /// ISSUE #95: Xóa nhà cung cấp theo hướng an toàn dữ liệu:
    /// 1. Chỉ Admin mới được thực hiện.
    /// 2. Còn công nợ -> Không cho xóa cứng. Nếu xác nhận, chuyển sang ngừng hoạt động để tiếp tục thanh toán nợ.
    /// 3. Đã có giao dịch liên quan -> Chuyển sang ngừng hoạt động để bảo toàn lịch sử.
    /// 4. Chưa từng phát sinh giao dịch nào -> Xóa cứng khỏi database.
    /// </summary>
    public async Task<KetQuaXoaDoiTac> XoaNhaCungCapSafeAsync(uint supplierId, bool xacNhanNgungHoatDong = false)
    {
        Quyen.RequireAdmin(Session.CurrentUser, "Chỉ tài khoản Quản trị (Admin) mới có quyền thực hiện thao tác xóa hoặc ngừng hoạt động nhà cung cấp.");

        var kiemTra = await KiemTraXoaNhaCungCapAsync(supplierId);
        await using var connection = await OpenAsync();

        // 1. Còn công nợ hoặc còn số dư trả trước
        if (kiemTra.CongNoHienTai > 0 || kiemTra.SoDuTraTruoc > 0)
        {
            if (!xacNhanNgungHoatDong)
            {
                return KetQuaXoaDoiTac.ConCongNo;
            }

            var affectedRows = await connection.ExecuteAsync(
                "UPDATE nha_cung_cap SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id;",
                new { Id = supplierId });

            if (affectedRows == 0)
                throw new InvalidOperationException("Nhà cung cấp không tồn tại hoặc đã ngừng hoạt động.");

            _ = LogSubAccountActivityAsync("Ngừng hoạt động nhà cung cấp", "NHA_CUNG_CAP", supplierId, null,
                $"Chuyển nhà cung cấp sang ngừng hoạt động do còn nợ ({kiemTra.CongNoHienTai:N0} đ, id={supplierId})");

            return KetQuaXoaDoiTac.DaChuyenNgungHoatDong;
        }

        // 2. Không còn nợ nhưng đã từng phát sinh giao dịch -> Bảo toàn lịch sử
        if (kiemTra.CoDuLieuGiaoDich)
        {
            var affectedRows = await connection.ExecuteAsync(
                "UPDATE nha_cung_cap SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id;",
                new { Id = supplierId });

            if (affectedRows == 0)
                throw new InvalidOperationException("Nhà cung cấp không tồn tại hoặc đã ngừng hoạt động.");

            _ = LogSubAccountActivityAsync("Ngừng hoạt động nhà cung cấp", "NHA_CUNG_CAP", supplierId, null,
                $"Chuyển nhà cung cấp sang ngừng hoạt động để bảo toàn {kiemTra.SoHoaDonHoacPhieuNhap} phiếu nhập và {kiemTra.SoGiaoDichLienQuan} giao dịch liên quan (id={supplierId})");

            return KetQuaXoaDoiTac.DaChuyenNgungHoatDong;
        }

        // 3. Chưa phát sinh bất kỳ giao dịch nào và không nợ -> XÓA CỨNG
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync(
                "DELETE FROM phieu_nhap_draft WHERE nha_cung_cap_id = @Id;",
                new { Id = supplierId }, tx);

            var affected = await connection.ExecuteAsync(
                "DELETE FROM nha_cung_cap WHERE id = @Id;",
                new { Id = supplierId }, tx);

            if (affected == 0)
                throw new InvalidOperationException("Nhà cung cấp không tồn tại.");

            await tx.CommitAsync();

            _ = LogSubAccountActivityAsync("Xóa nhà cung cấp", "NHA_CUNG_CAP", supplierId, null,
                $"Đã xóa vĩnh viễn nhà cung cấp chưa phát sinh giao dịch (id={supplierId})");

            return KetQuaXoaDoiTac.DaXoaVinhVien;
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            await tx.RollbackAsync();
            await connection.ExecuteAsync(
                "UPDATE nha_cung_cap SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id;",
                new { Id = supplierId });

            return KetQuaXoaDoiTac.DaChuyenNgungHoatDong;
        }
    }

    /// <summary>
    /// Kích hoạt lại nhà cung cấp đã bị ngừng hoạt động. Chỉ Admin mới được thực hiện.
    /// </summary>
    public async Task KichHoatLaiNhaCungCapAsync(uint supplierId)
    {
        Quyen.RequireAdmin(Session.CurrentUser, "Chỉ tài khoản Quản trị (Admin) mới có quyền kích hoạt lại nhà cung cấp.");
        await using var connection = await OpenAsync();

        var affected = await connection.ExecuteAsync(
            "UPDATE nha_cung_cap SET trang_thai = 1, ngay_cap_nhat = NOW() WHERE id = @Id;",
            new { Id = supplierId });

        if (affected == 0)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại.");

        _ = LogSubAccountActivityAsync("Kích hoạt lại nhà cung cấp", "NHA_CUNG_CAP", supplierId, null,
            $"Kích hoạt lại trạng thái hoạt động cho nhà cung cấp (id={supplierId})");
    }
}
