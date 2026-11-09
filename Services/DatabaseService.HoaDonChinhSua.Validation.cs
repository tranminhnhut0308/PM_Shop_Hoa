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
    private async Task<HoaDonEditState> PrepareHoaDonEditStateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditRequest request)
    {
        var header = await LoadLockedInvoiceForEditAsync(connection, transaction, request);
        var oldItems = await LoadInvoiceEditItemsAsync(connection, transaction, request.HoaDonId);
        ValidateOldInvoiceItems(oldItems);

        await EnsureNoInvoiceReturnsAsync(connection, transaction, request.HoaDonId);
        await EnsureNoStartedDeliveryAsync(connection, transaction, request.HoaDonId);
        await EnsureNoInvoiceRefundsAsync(connection, transaction, header);
        await ValidateRecordedPaymentsAsync(connection, transaction, header);

        var totals = CalculateHoaDonEditTotals(request, header);
        await ValidateInvoiceDebtAsync(connection, transaction, header);

        if (header.ChiNhanhId == 0)
            throw new InvalidOperationException(
                "Hóa đơn không có thông tin chi nhánh; không thể xác minh lịch sử tồn kho để sửa an toàn.");
        var branchId = header.ChiNhanhId;
        var (oldQuantities, newQuantities) = BuildInvoiceEditQuantities(oldItems, request.Items);

        await ValidateInvoiceStockHistoryAsync(
            connection,
            transaction,
            header.MaHoaDon,
            branchId,
            oldQuantities);

        var (products, stock) = await LockInvoiceProductsAndStockAsync(
            connection,
            transaction,
            branchId,
            oldQuantities.Keys.Union(newQuantities.Keys));

        return new HoaDonEditState
        {
            Request = request,
            Header = header,
            OldItems = oldItems,
            OldQuantities = oldQuantities,
            NewQuantities = newQuantities,
            Products = products,
            Stock = stock,
            ItemsTotal = totals.ItemsTotal,
            InvoiceDiscount = totals.InvoiceDiscount,
            AmountDue = totals.AmountDue,
            NewDebt = totals.NewDebt,
            BranchId = branchId,
            UserId = Session.CurrentUser?.Id,
            Now = DateTime.Now
        };
    }

    private static (Dictionary<uint, decimal> Old, Dictionary<uint, decimal> New) BuildInvoiceEditQuantities(
        IEnumerable<HoaDonItemDbRow> oldItems,
        IEnumerable<HoaDonItemEditModel> newItems)
    {
        var oldQuantities = oldItems
            .GroupBy(item => item.SanPhamId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.SoLuong));
        var newQuantities = newItems
            .GroupBy(item => item.SanPhamId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.SoLuong));
        return (oldQuantities, newQuantities);
    }

    private static HoaDonEditTotals CalculateHoaDonEditTotals(
        HoaDonEditRequest request,
        HoaDonHienTaiRow header)
    {
        var itemsTotal = request.Items.Sum(item =>
            Math.Round(item.SoLuong * item.DonGia - item.GiamGia, 2, MidpointRounding.AwayFromZero));
        if (request.GiamGia > itemsTotal)
            throw new InvalidOperationException("Giảm giá hóa đơn không được lớn hơn tổng tiền hàng.");

        var amountDue = itemsTotal - request.GiamGia;
        if (amountDue > MoneyInput.GiaTienToiDa)
            throw new InvalidOperationException("Tổng tiền hóa đơn vượt quá giới hạn lưu trữ.");
        if (amountDue < header.KhachDaThanhToan)
            throw new InvalidOperationException(
                "Tổng tiền mới thấp hơn số tiền khách đã thanh toán. Không thể tự động hoàn tiền; hãy hủy hóa đơn theo quy trình hoặc lập chứng từ điều chỉnh.");

        var newDebt = amountDue - header.KhachDaThanhToan;
        if (!header.KhachHangId.HasValue && newDebt > 0)
            throw new InvalidOperationException("Không thể tạo công nợ cho hóa đơn khách lẻ.");

        return new HoaDonEditTotals(itemsTotal, request.GiamGia, amountDue, newDebt);
    }

    private static async Task<HoaDonHienTaiRow> LoadLockedInvoiceForEditAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditRequest request)
    {
        const string sql = """
        SELECT id AS Id, ma_hoa_don AS MaHoaDon, chi_nhanh_id AS ChiNhanhId,
               khach_hang_id AS KhachHangId, tong_tien_hang AS TongTienHang,
               giam_gia AS GiamGia, thu_khac AS ThuKhac, khach_can_tra AS KhachCanTra,
               khach_da_thanh_toan AS KhachDaThanhToan, con_no AS ConNo,
               cong_no_da_ghi_nhan AS CongNoDaGhiNhan,
               trang_thai_hoa_don AS TrangThaiHoaDon
        FROM hoa_don_ban_hang
        WHERE id = @Id
        FOR UPDATE;
        """;
        var header = await connection.QueryFirstOrDefaultAsync<HoaDonHienTaiRow>(
            sql, new { Id = request.HoaDonId }, transaction);
        if (header is null)
            throw new InvalidOperationException($"Không tìm thấy hóa đơn #{request.HoaDonId}.");
        if (LaTrangThaiHoaDonDaHuy(header.TrangThaiHoaDon))
            throw new InvalidOperationException("Hóa đơn này đã bị hủy, không thể chỉnh sửa.");
        if (string.Equals(header.TrangThaiHoaDon, "TONG_HOP", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Hóa đơn tổng hợp không thể chỉnh sửa trực tiếp.");
        if (!string.IsNullOrWhiteSpace(request.MaHoaDon)
            && !string.Equals(request.MaHoaDon, header.MaHoaDon, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Mã hóa đơn đã thay đổi. Vui lòng đóng và mở lại hóa đơn trước khi sửa.");
        }

        var debtFromTotals = Math.Max(0m, header.KhachCanTra - header.KhachDaThanhToan);
        if (header.KhachDaThanhToan < 0
            || header.ConNo < 0
            || Math.Abs(header.ConNo - debtFromTotals) > 0.01m)
        {
            throw new InvalidOperationException(
                "Số tiền đã trả/còn nợ trên hóa đơn không nhất quán; vui lòng đối soát trước khi sửa.");
        }

        return header;
    }

    private static async Task<List<HoaDonItemDbRow>> LoadInvoiceEditItemsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint invoiceId)
    {
        const string sql = """
        SELECT san_pham_id AS SanPhamId,
               COALESCE(ma_san_pham, '') AS MaSanPham,
               ten_san_pham AS TenSanPham,
               COALESCE(NULLIF(TRIM(don_vi_tinh), ''), 'Bó') AS DonViTinh,
               so_luong AS SoLuong, don_gia AS DonGia,
               COALESCE(giam_gia, 0) AS GiamGia, thanh_tien AS ThanhTien,
               gia_von AS GiaVon, ghi_chu AS GhiChu
        FROM chi_tiet_hoa_don_ban_hang
        WHERE hoa_don_id = @HoaDonId
        FOR UPDATE;
        """;
        var items = await connection.QueryAsync<HoaDonItemDbRow>(
            sql, new { HoaDonId = invoiceId }, transaction);
        return items.ToList();
    }

    private static void ValidateOldInvoiceItems(IReadOnlyCollection<HoaDonItemDbRow> items)
    {
        if (items.Count == 0 || items.Any(item => item.SanPhamId == 0 || item.SoLuong <= 0))
            throw new InvalidOperationException(
                "Hóa đơn thiếu chi tiết hoặc dữ liệu số lượng cũ không hợp lệ; không thể xác định an toàn phần tồn kho cần hoàn tác.");
    }

    private static async Task EnsureNoInvoiceReturnsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint invoiceId)
    {
        var count = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM phieu_tra_hang
            WHERE hoa_don_id = @HoaDonId;
            """,
            new { HoaDonId = invoiceId },
            transaction);
        if (count > 0)
            throw new InvalidOperationException(
                "Hóa đơn đã có lịch sử phiếu trả hàng. Để không làm mất liên kết chi tiết trả hàng, hãy xử lý theo quy trình hủy/điều chỉnh.");
    }

    private static async Task EnsureNoStartedDeliveryAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint invoiceId)
    {
        var tableExists = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'giao_hang';
            """,
            transaction: transaction);
        if (tableExists == 0)
            return;

        var started = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM giao_hang
            WHERE hoa_don_id = @HoaDonId
              AND UPPER(COALESCE(trang_thai, '')) NOT IN ('CHO_GIAO', 'DA_HUY', 'HUY');
            """,
            new { HoaDonId = invoiceId },
            transaction);
        if (started > 0)
            throw new InvalidOperationException(
                "Đơn giao hàng đã bắt đầu hoặc hoàn tất. Không thể sửa an toàn; hãy xử lý theo quy trình hủy/điều chỉnh.");
    }

    private static async Task EnsureNoInvoiceRefundsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonHienTaiRow header)
    {
        var columns = (await connection.QueryAsync<string>(
            """
            SELECT LOWER(COLUMN_NAME)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi';
            """,
            transaction: transaction)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (columns.Count == 0 || !columns.Contains("loai_phieu"))
            return;

        var links = new List<string>();
        if (columns.Contains("hoa_don_ban_hang_id"))
            links.Add("hoa_don_ban_hang_id = @HoaDonId");
        if (columns.Contains("ma_hoa_don"))
            links.Add("ma_hoa_don = @MaHoaDon");
        if (links.Count == 0)
            return;

        var statusFilter = columns.Contains("trang_thai")
            ? "AND UPPER(COALESCE(trang_thai, 'HOAN_THANH')) = 'HOAN_THANH'"
            : "";
        var count = await connection.ExecuteScalarAsync<long>(
            $"""
            SELECT COUNT(*)
            FROM phieu_chi
            WHERE ({string.Join(" OR ", links)})
              AND UPPER(COALESCE(loai_phieu, 'THU')) IN ('CHI', 'PHIEU_CHI')
              {statusFilter};
            """,
            new { HoaDonId = header.Id, header.MaHoaDon },
            transaction);
        if (count > 0)
            throw new InvalidOperationException(
                "Hóa đơn đã có phiếu chi/hoàn tiền liên quan. Không thể sửa an toàn; hãy xử lý theo quy trình hủy/điều chỉnh.");
    }

    private static async Task ValidateRecordedPaymentsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonHienTaiRow header)
    {
        var total = await connection.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(so_tien), 0) FROM thanh_toan WHERE hoa_don_id = @HoaDonId;",
            new { HoaDonId = header.Id },
            transaction);
        if (total < 0 || total > header.KhachDaThanhToan)
            throw new InvalidOperationException(
                "Lịch sử thanh toán không khớp với số tiền đã ghi trên hóa đơn; không thể sửa an toàn.");
    }

    private static async Task ValidateInvoiceDebtAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonHienTaiRow header)
    {
        if (header.KhachHangId is not uint customerId || customerId == 0)
        {
            if (header.ConNo > 0)
                throw new InvalidOperationException("Hóa đơn khách lẻ đang có công nợ; vui lòng đối soát trước khi sửa.");
            return;
        }

        await connection.ExecuteScalarAsync<uint?>(
            "SELECT id FROM khach_hang WHERE id = @Id LIMIT 1 FOR UPDATE;",
            new { Id = customerId },
            transaction);
        if (header.CongNoDaGhiNhan == 0)
        {
            if (header.ConNo > 0)
                throw new InvalidOperationException(
                    "Hóa đơn cũ còn nợ nhưng chưa có dấu xác nhận công nợ đã ghi nhận. Vui lòng đối soát trước khi sửa.");
            return;
        }

        var recordedDebt = await connection.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
            FROM lich_su_cong_no_khach_hang
            WHERE khach_hang_id = @KhachHangId
              AND loai_chung_tu = 'HOA_DON'
              AND ma_chung_tu = @MaHoaDon;
            """,
            new { KhachHangId = customerId, header.MaHoaDon },
            transaction);
        if (Math.Abs(recordedDebt - header.ConNo) > 0.01m)
            throw new InvalidOperationException(
                "Lịch sử công nợ của hóa đơn không khớp với số còn nợ hiện tại. Vui lòng đối soát trước khi sửa.");
    }

    private static async Task ValidateInvoiceStockHistoryAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string invoiceCode,
        uint branchId,
        IReadOnlyDictionary<uint, decimal> oldQuantities)
    {
        var rows = await connection.QueryAsync<HoaDonTonKhoLedgerRow>(
            """
            SELECT san_pham_id AS SanPhamId,
                   SUM(CASE
                       WHEN UPPER(loai_giao_dich) IN ('NHAP', 'NHAP_HOAN_TRA') THEN so_luong
                       WHEN UPPER(loai_giao_dich) IN ('XUAT', 'XUAT_BAN_HANG') THEN -so_luong
                       ELSE 0
                   END) AS BienDong
            FROM lich_su_nhap_xuat_kho
            WHERE chi_nhanh_id = @ChiNhanhId
              AND ma_chung_tu = @MaHoaDon
              AND UPPER(COALESCE(loai_chung_tu, '')) IN
                  ('HOA_DON_BAN_HANG', 'HOA_DON', 'DIEU_CHINH_HOA_DON', 'HUY_HOA_DON')
            GROUP BY san_pham_id;
            """,
            new { ChiNhanhId = branchId, MaHoaDon = invoiceCode },
            transaction);
        var movements = rows.ToDictionary(row => row.SanPhamId, row => row.BienDong);

        if (oldQuantities.Any(item =>
                !movements.TryGetValue(item.Key, out var movement) || movement != -item.Value)
            || movements.Any(movement =>
                !oldQuantities.ContainsKey(movement.Key) && movement.Value != 0))
        {
            throw new InvalidOperationException(
                "Lịch sử tồn kho của hóa đơn không khớp với các chi tiết hiện tại. " +
                "Để bảo vệ dữ liệu, hóa đơn này không thể sửa trực tiếp; hãy hủy theo quy trình hoặc lập hóa đơn điều chỉnh.");
        }
    }

    private static async Task<(Dictionary<uint, SanPhamSuaHoaDonRow>, Dictionary<uint, TonKhoSuaHoaDonRow>)>
        LockInvoiceProductsAndStockAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            uint branchId,
            IEnumerable<uint> productIds)
    {
        var products = new Dictionary<uint, SanPhamSuaHoaDonRow>();
        var stock = new Dictionary<uint, TonKhoSuaHoaDonRow>();

        foreach (var productId in productIds.OrderBy(id => id))
        {
            var product = await connection.QueryFirstOrDefaultAsync<SanPhamSuaHoaDonRow>(
                """
                SELECT id AS Id, ma_san_pham AS MaSanPham,
                       ten_san_pham AS TenSanPham, gia_von AS GiaVon
                FROM san_pham
                WHERE id = @Id
                FOR UPDATE;
                """,
                new { Id = productId },
                transaction);
            if (product is null)
                throw new InvalidOperationException($"Không tìm thấy sản phẩm ID {productId} trong danh mục.");

            var inventory = await connection.QueryFirstOrDefaultAsync<TonKhoSuaHoaDonRow>(
                """
                SELECT so_luong_ton AS SoLuongTon, gia_von_binh_quan AS GiaVonBinhQuan
                FROM ton_kho
                WHERE chi_nhanh_id = @ChiNhanhId AND san_pham_id = @SanPhamId
                FOR UPDATE;
                """,
                new { ChiNhanhId = branchId, SanPhamId = productId },
                transaction);
            if (inventory is null)
                throw new InvalidOperationException(
                    $"Chưa có bản ghi tồn kho cho sản phẩm ID {productId} tại chi nhánh này; không thể sửa an toàn.");

            products.Add(productId, product);
            stock.Add(productId, inventory);
        }

        return (products, stock);
    }
}
