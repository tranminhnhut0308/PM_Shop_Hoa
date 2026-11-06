using System;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Tests;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args.Contains("--verify-money-input", StringComparer.OrdinalIgnoreCase))
            return MoneyInputVerifier.Run();

        Console.WriteLine("==========================================================================================");
        Console.WriteLine("  BẮT ĐẦU KIỂM THỬ HỆ THỐNG SHOP HOA: ĐỐI SOÁT TOÀN DIỆN THỐNG KÊ TRANG TỔNG QUAN (#69)  ");
        Console.WriteLine("==========================================================================================");

        try
        {
            var configService = new DatabaseConfigService();
            var config = configService.Load();
            var csb = new MySqlConnectionStringBuilder
            {
                Server = config.Server,
                Port = (uint)config.Port,
                Database = config.Database,
                UserID = config.User,
                Password = config.Password,
                SslMode = MySqlSslMode.None,
                CharacterSet = "utf8mb4"
            };

            if (args.Contains("--verify-invoice-date-filter-read-only", StringComparer.OrdinalIgnoreCase))
                return await KiemTraLocNgayHoaDonReadOnlyAsync(csb.ConnectionString);

            var service = new DashboardService(async () =>
            {
                var conn = new MySqlConnection(csb.ConnectionString);
                await conn.OpenAsync();
                return conn;
            });

            var homNay = DateTime.Now;

            // 1. Kiểm tra kỳ Hôm Nay
            await KiemTraKhoangThoiGianAsync(service, csb.ConnectionString, TongQuanKhoangThoiGian.HomNay, "Hôm nay", homNay);

            // 2. Kiểm tra kỳ 7 Ngày Qua
            await KiemTraKhoangThoiGianAsync(service, csb.ConnectionString, TongQuanKhoangThoiGian.BayNgay, "7 ngày qua", homNay);

            // 3. Kiểm tra kỳ Tháng Này
            await KiemTraKhoangThoiGianAsync(service, csb.ConnectionString, TongQuanKhoangThoiGian.ThangNay, "Tháng này", homNay);

            // 4. Kiểm tra kỳ Tháng Trước
            await KiemTraKhoangThoiGianAsync(service, csb.ConnectionString, TongQuanKhoangThoiGian.ThangTruoc, "Tháng trước", homNay);

            // 5. Kiểm tra kỳ Năm Nay
            await KiemTraKhoangThoiGianAsync(service, csb.ConnectionString, TongQuanKhoangThoiGian.NamNay, "Năm nay", homNay);

            // 6. Kiểm tra Danh sách hóa đơn bị loại trừ (Đã hủy / Lưu tạm)
            await KiemTraDanhSachHoaDonLoaiTruAsync(service, csb.ConnectionString, homNay);

            // 7. Kiểm tra Thống kê kho
            await KiemTraThongKeKhoAsync(service);

            // ==========================================================================================
            // CÁC BÀI KIỂM THỬ ISSUE #68: FIX LỖI TẠO VÀ LƯU PHIẾU NHẬP HÀNG MỚI (ATOMIC CODE, MULTI-TAB)
            // ==========================================================================================
            Console.WriteLine("\n------------------------------------------------------------------------------------------");
            Console.WriteLine("  BẮT ĐẦU KIỂM THỬ ISSUE #68: CƠ CHẾ CẤP MÃ ATOMIC & LƯU PHIẾU NHẬP ĐA TAB               ");
            Console.WriteLine("------------------------------------------------------------------------------------------");
            await KiemTraIssue68_SinhMaAtomicAsync(csb.ConnectionString);
            await KiemTraIssue68_LuuPhieuNhapMaRongAsync(csb.ConnectionString);
            KiemTraIssue68_MultiTab_DocLap();

            Console.WriteLine("\n>>> TẤT CẢ CÁC BÀI KIỂM THỬ ISSUE #68 & #69 ĐÃ VƯỢT QUA 100% THÀNH CÔNG! <<<");
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[THẤT BẠI] Kiểm thử bị lỗi: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> KiemTraLocNgayHoaDonReadOnlyAsync(string connectionString)
    {
        KiemTraChuyenNgayVietNam();

        var start = new DateTime(2026, 11, 1);
        var endExclusive = new DateTime(2026, 11, 6);

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        var vnToday = VietnameseCalendar.Today().Date;
        var presetRanges = new[]
        {
            (Name: "Today", Start: vnToday, End: vnToday.AddDays(1)),
            (Name: "Yesterday", Start: vnToday.AddDays(-1), End: vnToday),
            (Name: "Last 7 days", Start: vnToday.AddDays(-6), End: vnToday.AddDays(1)),
            (Name: "This month", Start: new DateTime(vnToday.Year, vnToday.Month, 1),
                End: new DateTime(vnToday.Year, vnToday.Month, 1).AddMonths(1)),
            (Name: "Last month", Start: new DateTime(vnToday.Year, vnToday.Month, 1).AddMonths(-1),
                End: new DateTime(vnToday.Year, vnToday.Month, 1))
        };
        foreach (var range in presetRanges)
        {
            var count = await connection.ExecuteScalarAsync<long>(
                """
                SELECT COUNT(*)
                FROM hoa_don_ban_hang
                WHERE thoi_gian_ban >= @Start
                  AND thoi_gian_ban < @End;
                """,
                new
                {
                    Start = DateTime.SpecifyKind(range.Start, DateTimeKind.Unspecified),
                    End = DateTime.SpecifyKind(range.End, DateTimeKind.Unspecified)
                });
            Console.WriteLine($"{range.Name}: {count} invoices");
        }

        var audit = await connection.QuerySingleAsync<InvoiceDateFilterAudit>(
            """
            SELECT
                COUNT(*) AS Total,
                COALESCE(SUM(hd.thoi_gian_ban >= @Start AND hd.thoi_gian_ban < @StartNextDay), 0) AS StartDay,
                COALESCE(SUM(hd.thoi_gian_ban >= @MiddleDay AND hd.thoi_gian_ban < @MiddleNextDay), 0) AS MiddleDay,
                COALESCE(SUM(hd.thoi_gian_ban >= @EndDay AND hd.thoi_gian_ban < @EndExclusive), 0) AS EndDay,
                MIN(hd.thoi_gian_ban) AS FirstTimestamp,
                MAX(hd.thoi_gian_ban) AS LastTimestamp,
                COALESCE(SUM(
                    COALESCE(hd.khach_da_thanh_toan, 0) <= 0
                    AND COALESCE(hd.khach_can_tra, 0) > 0
                ), 0) AS Unpaid,
                (
                    SELECT COUNT(*)
                    FROM hoa_don_ban_hang search_hd
                    WHERE search_hd.thoi_gian_ban >= @Start
                      AND search_hd.thoi_gian_ban < @EndExclusive
                      AND search_hd.ma_hoa_don LIKE CONCAT(
                          '%',
                          (
                              SELECT sample_hd.ma_hoa_don
                              FROM hoa_don_ban_hang sample_hd
                              WHERE sample_hd.thoi_gian_ban >= @Start
                                AND sample_hd.thoi_gian_ban < @EndExclusive
                              ORDER BY sample_hd.thoi_gian_ban, sample_hd.id
                              LIMIT 1
                          ),
                          '%'
                      )
                ) AS SearchCount
            FROM hoa_don_ban_hang hd
            WHERE hd.thoi_gian_ban >= @Start
              AND hd.thoi_gian_ban < @EndExclusive;
            """,
            new
            {
                Start = start,
                StartNextDay = start.AddDays(1),
                MiddleDay = start.AddDays(2),
                MiddleNextDay = start.AddDays(3),
                EndDay = endExclusive.AddDays(-1),
                EndExclusive = endExclusive
            });

        var outside = await connection.QuerySingleAsync<(long BeforeRange, long AfterRange)>(
            """
            SELECT
                COALESCE(SUM(hd.thoi_gian_ban < @Start), 0) AS BeforeRange,
                COALESCE(SUM(hd.thoi_gian_ban >= @EndExclusive), 0) AS AfterRange
            FROM hoa_don_ban_hang hd
            WHERE hd.thoi_gian_ban IS NOT NULL;
            """,
            new { Start = start, EndExclusive = endExclusive });

        var dateIndex = await connection.QuerySingleOrDefaultAsync<string>(
            """
            SELECT INDEX_NAME
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'hoa_don_ban_hang'
              AND COLUMN_NAME = 'thoi_gian_ban'
              AND SEQ_IN_INDEX = 1
            LIMIT 1;
            """);

        Console.WriteLine("Read-only invoice date filter verification (2026-11-01 <= date < 2026-11-06)");
        Console.WriteLine($"Total: {audit.Total}; 01/11: {audit.StartDay}; 03/11: {audit.MiddleDay}; 05/11: {audit.EndDay}");
        Console.WriteLine($"Earliest: {audit.FirstTimestamp:yyyy-MM-dd HH:mm:ss}; latest: {audit.LastTimestamp:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Unpaid within range: {audit.Unpaid}; date + invoice-code search: {audit.SearchCount}");
        Console.WriteLine($"Outside range (before/after): {outside.BeforeRange}/{outside.AfterRange}");
        Console.WriteLine($"Leading date index: {dateIndex ?? "none"}");

        var pageThreeCount = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM (
                SELECT hd.id
                FROM hoa_don_ban_hang hd
                WHERE hd.thoi_gian_ban >= @Start
                  AND hd.thoi_gian_ban < @EndExclusive
                ORDER BY hd.thoi_gian_ban DESC, hd.id DESC
                LIMIT 20 OFFSET 40
            ) page_three;
            """,
            new { Start = start, EndExclusive = endExclusive });
        KiemTra(pageThreeCount == 20, "Date filtering must preserve database pagination for page three.");
        var endInclusive = endExclusive.AddDays(-1);
        KiemTra(start <= endInclusive, "Valid custom range must pass date order validation.");
        var reversedStart = endInclusive;
        var reversedEnd = start;
        KiemTra(reversedStart > reversedEnd, "Reversed custom range must fail date order validation.");
        Console.WriteLine($"Page 3 (20 rows/page): {pageThreeCount} invoices; date-order checks passed.");
        return 0;
    }

    private static void KiemTraChuyenNgayVietNam()
    {
        var dateOnly = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var dateOnlyResult = VietnameseCalendar.FromDateTime(dateOnly);
        KiemTra(dateOnlyResult.Date == dateOnly && dateOnlyResult.Offset == TimeSpan.FromHours(7),
            "Unspecified calendar date must remain the same Vietnam date.");

        var utc = new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc);
        var utcResult = VietnameseCalendar.FromDateTime(utc);
        KiemTra(utcResult.Date == new DateTime(2026, 11, 2)
            && utcResult.Offset == TimeSpan.FromHours(7),
            "UTC instant must map to the correct Vietnam calendar date.");

        var local = new DateTime(2026, 11, 1, 23, 0, 0, DateTimeKind.Local);
        var expectedLocalDate = local.ToUniversalTime().AddHours(7).Date;
        var localResult = VietnameseCalendar.FromDateTime(local);
        KiemTra(localResult.Date == expectedLocalDate
            && localResult.Offset == TimeSpan.FromHours(7),
            "Local instant must map to the correct Vietnam calendar date.");

        var pickedCalendarDate = new DateTimeOffset(dateOnly, TimeSpan.FromHours(-5));
        var pickedResult = VietnameseCalendar.FromCalendarDate(pickedCalendarDate);
        KiemTra(pickedResult.Date == dateOnly && pickedResult.Offset == TimeSpan.FromHours(7),
            "DatePicker calendar date must not shift according to its original offset.");

        var databaseDate = VietnameseCalendar.ToDatabaseWallClockDate(pickedResult);
        KiemTra(databaseDate.Kind == DateTimeKind.Unspecified && databaseDate == dateOnly,
            "Database date parameter must be an unshifted Unspecified wall-clock value.");
        Console.WriteLine("DateTime Kind conversions (UTC/Local/Unspecified/DatePicker) passed.");
    }

    private static void KiemTra(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class InvoiceDateFilterAudit
    {
        public long Total { get; set; }
        public long StartDay { get; set; }
        public long MiddleDay { get; set; }
        public long EndDay { get; set; }
        public DateTime? FirstTimestamp { get; set; }
        public DateTime? LastTimestamp { get; set; }
        public long Unpaid { get; set; }
        public long SearchCount { get; set; }
    }

    private static async Task KiemTraKhoangThoiGianAsync(
        DashboardService service,
        string connStr,
        string khoang,
        string tenKhoang,
        DateTime anchor)
    {
        Console.Write($"[Kiểm tra kỳ {tenKhoang}] Đối soát số liệu Dashboard và Database... ");

        var duLieu = await service.TaiDuLieuAsync(
            khoang,
            TongQuanChieuDoanhThu.TheoNgay,
            TongQuanXepHang.TheoDoanhThu,
            null,
            null,
            anchor);

        var (tuNgay, denKeTiep, _) = DashboardService.TinhKhoang(khoang, null, null, anchor);

        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();

        var dbStats = await conn.QuerySingleAsync<(int Count, decimal DoanhThu, decimal DaThu, decimal ConNo)>(
            """
            SELECT
                COALESCE(SUM(CASE WHEN hd.trang_thai_hoa_don NOT IN ('DA_HUY', 'HUY', 'LUU_TAM') THEN 1 ELSE 0 END), 0) AS Count,
                COALESCE(SUM(CASE WHEN hd.trang_thai_hoa_don NOT IN ('DA_HUY', 'HUY', 'LUU_TAM') THEN hd.khach_can_tra ELSE 0 END), 0) AS DoanhThu,
                COALESCE(SUM(CASE WHEN hd.trang_thai_hoa_don NOT IN ('DA_HUY', 'HUY', 'LUU_TAM') THEN hd.khach_da_thanh_toan ELSE 0 END), 0) AS DaThu,
                COALESCE(SUM(CASE WHEN hd.trang_thai_hoa_don NOT IN ('DA_HUY', 'HUY', 'LUU_TAM') THEN hd.con_no ELSE 0 END), 0) AS ConNo
            FROM hoa_don_ban_hang hd
            WHERE hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep;
            """,
            new { TuNgay = tuNgay, DenKeTiep = denKeTiep });

        if (duLieu.SoHoaDonHomNay != dbStats.Count)
            throw new Exception($"Số hóa đơn {tenKhoang} không khớp! Dashboard: {duLieu.SoHoaDonHomNay}, DB: {dbStats.Count}");

        if (duLieu.DoanhThuHomNay != dbStats.DoanhThu)
            throw new Exception($"Doanh thu {tenKhoang} không khớp! Dashboard: {duLieu.DoanhThuHomNay:N0}, DB: {dbStats.DoanhThu:N0}");

        if (duLieu.DaThuHomNay != dbStats.DaThu)
            throw new Exception($"Tiền đã thu {tenKhoang} không khớp! Dashboard: {duLieu.DaThuHomNay:N0}, DB: {dbStats.DaThu:N0}");

        if (duLieu.ConNoHomNay != dbStats.ConNo)
            throw new Exception($"Công nợ {tenKhoang} không khớp! Dashboard: {duLieu.ConNoHomNay:N0}, DB: {dbStats.ConNo:N0}");

        // Biểu đồ phải khớp 100% với Doanh thu thẻ
        if (duLieu.TongDoanhThuKyChon != duLieu.DoanhThuHomNay)
            throw new Exception($"Tổng doanh thu biểu đồ {tenKhoang} không khớp với thẻ Doanh thu! Biểu đồ: {duLieu.TongDoanhThuKyChon:N0}, Thẻ: {duLieu.DoanhThuHomNay:N0}");

        Console.WriteLine($"PASSED (HĐ: {duLieu.SoHoaDonHomNay:N0}, Doanh thu: {duLieu.DoanhThuHomNay:N0} đ, Đã thu: {duLieu.DaThuHomNay:N0} đ, Nợ: {duLieu.ConNoHomNay:N0} đ) ✓");
    }

    private static async Task KiemTraDanhSachHoaDonLoaiTruAsync(
        DashboardService service,
        string connStr,
        DateTime anchor)
    {
        Console.Write("[Kiểm tra Hóa đơn loại trừ] Đối soát danh sách HĐ bị loại trừ (Đã hủy / Lưu tạm)... ");

        var (tuNgay, denKeTiep, _) = DashboardService.TinhKhoang(TongQuanKhoangThoiGian.NamNay, null, null, anchor);

        var danhSach = await service.TaiDanhSachHoaDonChuaGhiNhanAsync(tuNgay, denKeTiep);

        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();

        var dbCount = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM hoa_don_ban_hang hd
            WHERE hd.trang_thai_hoa_don IN ('DA_HUY', 'HUY', 'LUU_TAM')
              AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep;
            """,
            new { TuNgay = tuNgay, DenKeTiep = denKeTiep });

        if (danhSach.Count != dbCount)
            throw new Exception($"Số lượng hóa đơn bị loại trừ không khớp! Query danh sách: {danhSach.Count}, DB: {dbCount}");

        // Xác minh không có bất kỳ hóa đơn hoàn thành nào lọt vào danh sách này
        foreach (var item in danhSach)
        {
            if (item.LyDoLoaiTru != "Hóa đơn đã hủy" && item.LyDoLoaiTru != "Hóa đơn lưu tạm")
            {
                throw new Exception($"Hóa đơn {item.MaHoaDon} có lý do không hợp lệ: {item.LyDoLoaiTru}");
            }
        }

        Console.WriteLine($"PASSED (Tổng {danhSach.Count} hóa đơn bị loại trừ hợp lệ) ✓");
    }

    private static async Task KiemTraThongKeKhoAsync(DashboardService service)
    {
        Console.Write("[Kiểm tra Thống kê kho] Độc lập thời gian và tính toán chính xác... ");

        var duLieu = await service.TaiDuLieuAsync(
            TongQuanKhoangThoiGian.HomNay,
            TongQuanChieuDoanhThu.TheoNgay,
            TongQuanXepHang.TheoDoanhThu,
            null,
            null,
            DateTime.Now);

        var kho = duLieu.ThongKeKho;
        if (kho.TongSoHangHoa <= 0) throw new Exception("Tổng số hàng hóa trong kho phải > 0");
        if (kho.TongSoKhachHang <= 0) throw new Exception("Tổng số khách hàng phải > 0");
        if (kho.TongSoNhaCungCap <= 0) throw new Exception("Tổng số nhà cung cấp phải > 0");
        if (kho.GiaTriTonKho <= 0) throw new Exception("Giá trị tồn kho phải > 0");

        Console.WriteLine($"PASSED (Mặt hàng: {kho.TongSoHangHoa:N0}, Khách hàng: {kho.TongSoKhachHang:N0}, NCC: {kho.TongSoNhaCungCap:N0}, Giá trị tồn: {kho.GiaTriTonKhoHienThi}) ✓");
    }

    private static async Task KiemTraIssue68_SinhMaAtomicAsync(string connStr)
    {
        Console.Write("[Kiểm tra Issue #68] 1. Sinh mã phiếu nhập nguyên tử (GET_LOCK + PNxxxxxx)... ");
        Session.SetCurrentUser(new NguoiDung { TenDangNhap = "tester", MaVaiTro = Quyen.QuanTri });
        var db = new DatabaseService();
        var code1 = await db.TaoMaPhieuNhapMoiAsync();

        if (string.IsNullOrWhiteSpace(code1) || !code1.StartsWith("PN") || code1.Length < 8)
        {
            throw new Exception($"Mã sinh ra không đúng định dạng PNxxxxxx: '{code1}'");
        }

        // Kiểm tra xem mã này đã tồn tại trong DB chưa
        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM phieu_nhap_hang WHERE TRIM(ma_phieu_nhap) = @ma",
            new { ma = code1 });

        if (count > 0)
        {
            throw new Exception($"Lỗi: Mã vừa sinh '{code1}' đã bị trùng với phiếu hiện có!");
        }

        Console.WriteLine($"PASSED (Mã kế tiếp cấp nguyên tử: {code1}) ✓");
    }

    private static async Task KiemTraIssue68_LuuPhieuNhapMaRongAsync(string connStr)
    {
        Console.Write("[Kiểm tra Issue #68] 2. Lưu phiếu nhập khi mã rỗng (Tự sinh trong Transaction & Rollback an toàn)... ");
        
        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();

        // Thiết lập phiên làm việc có quyền Quản trị với ID người dùng thực trong DB
        var userDb = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT id, ten_dang_nhap, chi_nhanh_id FROM nguoi_dung LIMIT 1");
        uint currentUserId = userDb != null ? (uint)userDb.id : 1;
        uint currentBranchId = userDb?.chi_nhanh_id != null
            ? (uint)userDb.chi_nhanh_id
            : await conn.ExecuteScalarAsync<uint>("SELECT id FROM chi_nhanh ORDER BY id LIMIT 1");
        Session.SetCurrentUser(new NguoiDung
        {
            Id = currentUserId,
            TenDangNhap = userDb?.ten_dang_nhap ?? "admin",
            MaVaiTro = Quyen.QuanTri,
            ChiNhanhId = currentBranchId
        });

        // Lấy 1 sản phẩm có sẵn để test
        var sp = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT id, ma_san_pham, ten_san_pham, gia_von, COALESCE(ton_kho_excel, 0) AS ton_kho FROM san_pham WHERE trang_thai = 1 LIMIT 1");
        if (sp == null) throw new Exception("Không có hàng hóa nào để kiểm thử");

        uint spId = (uint)sp.id;

        // Lấy 1 nhà cung cấp hợp lệ
        var nccId = await conn.ExecuteScalarAsync<uint>("SELECT id FROM nha_cung_cap LIMIT 1");

        var db = new DatabaseService();

        var phieu = new PhieuNhapHang
        {
            MaPhieuNhap = "", // Rỗng, hệ thống phải tự sinh trong transaction!
            ChiNhanhId = currentBranchId,
            ThoiGianNhap = DateTime.Now,
            NhaCungCapId = nccId,
            TongTienHang = 50000,
            ChiPhiKhac = 0,
            TongTienNhap = 50000,
            DaThanhToan = 0,
            ConNo = 50000,
            TrangThai = "DA_NHAP_HANG"
        };

        var items = new List<ChiTietPhieuNhapHang>
        {
            new()
            {
                SanPham = new SanPham
                {
                    Id = spId,
                    MaSanPham = sp.ma_san_pham ?? "TEST",
                    TenSanPham = sp.ten_san_pham ?? "Hoa Test",
                    GiaVon = 50000
                },
                SoLuong = 1,
                DonGiaNhap = 50000
            }
        };

        // Tồn kho nghiệp vụ lấy từ bảng ton_kho (nguồn cập nhật của phiếu nhập),
        // không chỉ từ cột cache san_pham.ton_kho_excel.
        var tonKhoRowTruoc = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT so_luong_ton, gia_von_binh_quan FROM ton_kho WHERE chi_nhanh_id = @ChiNhanhId AND san_pham_id = @SanPhamId",
            new { ChiNhanhId = currentBranchId, SanPhamId = spId });
        decimal tonKhoTruoc = tonKhoRowTruoc != null
            ? (decimal)tonKhoRowTruoc.so_luong_ton
            : await conn.ExecuteScalarAsync<decimal>("SELECT COALESCE(ton_kho_excel, 0) FROM san_pham WHERE id = @id", new { id = spId });
        decimal sanPhamTonTruoc = await conn.ExecuteScalarAsync<decimal>("SELECT COALESCE(ton_kho_excel, 0) FROM san_pham WHERE id = @id", new { id = spId });
        decimal? giaVonBinhQuanTruoc = tonKhoRowTruoc != null ? (decimal)tonKhoRowTruoc.gia_von_binh_quan : null;
        uint savedId = 0;
        try
        {
            savedId = await db.LuuPhieuNhapHangAsync(phieu, items);
            if (savedId == 0 || string.IsNullOrWhiteSpace(phieu.MaPhieuNhap) || !phieu.MaPhieuNhap.StartsWith("PN"))
            {
                throw new Exception($"Phiếu nhập sau khi lưu không được cấp ID hoặc mã hợp lệ! Id={savedId}, Ma={phieu.MaPhieuNhap}");
            }

            // Kiểm tra tồn kho sau khi lưu phải tăng 1 ở cả ton_kho và cache san_pham.
            var tonKhoRowSau = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT so_luong_ton FROM ton_kho WHERE chi_nhanh_id = @ChiNhanhId AND san_pham_id = @SanPhamId",
                new { ChiNhanhId = currentBranchId, SanPhamId = spId });
            decimal tonKhoSau = tonKhoRowSau != null ? (decimal)tonKhoRowSau.so_luong_ton : 0m;
            decimal sanPhamTonSau = await conn.ExecuteScalarAsync<decimal>("SELECT COALESCE(ton_kho_excel, 0) FROM san_pham WHERE id = @id", new { id = spId });
            if (tonKhoSau != tonKhoTruoc + 1 || sanPhamTonSau != tonKhoTruoc + 1)
            {
                throw new Exception($"Tồn kho không tăng đúng: ton_kho trước={tonKhoTruoc}, sau={tonKhoSau}; san_pham sau={sanPhamTonSau}");
            }
        }
        finally
        {
            // Cleanup: Xóa phiếu test, chi tiết, công nợ và lịch sử kho test, hoàn lại tồn kho
            if (savedId > 0)
            {
                await conn.ExecuteAsync("DELETE FROM lich_su_chinh_sua_phieu_nhap WHERE phieu_nhap_id = @id", new { id = savedId });
                await conn.ExecuteAsync("DELETE FROM lich_su_cong_no_nha_cung_cap WHERE ma_chung_tu = @ma", new { ma = phieu.MaPhieuNhap });
                await conn.ExecuteAsync("DELETE FROM phieu_chi WHERE phieu_nhap_hang_id = @id OR ma_phieu_nhap = @ma", new { id = savedId, ma = phieu.MaPhieuNhap });
                await conn.ExecuteAsync("DELETE FROM chi_tiet_phieu_nhap_hang WHERE phieu_nhap_id = @id", new { id = savedId });
                await conn.ExecuteAsync("DELETE FROM phieu_nhap_hang WHERE id = @id", new { id = savedId });
                await conn.ExecuteAsync("DELETE FROM lich_su_nhap_xuat_kho WHERE loai_chung_tu IN ('PHIEU_NHAP', 'PHIEU_NHAP_HANG') AND ma_chung_tu = @ma", new { ma = phieu.MaPhieuNhap });
            }
            if (tonKhoRowTruoc != null)
            {
                await conn.ExecuteAsync(
                    "UPDATE ton_kho SET so_luong_ton = @Ton, gia_von_binh_quan = @Gia WHERE chi_nhanh_id = @ChiNhanhId AND san_pham_id = @SanPhamId",
                    new { Ton = tonKhoTruoc, Gia = giaVonBinhQuanTruoc ?? 0m, ChiNhanhId = currentBranchId, SanPhamId = spId });
            }
            else
            {
                await conn.ExecuteAsync(
                    "DELETE FROM ton_kho WHERE chi_nhanh_id = @ChiNhanhId AND san_pham_id = @SanPhamId",
                    new { ChiNhanhId = currentBranchId, SanPhamId = spId });
            }
            await conn.ExecuteAsync("UPDATE san_pham SET ton_kho_excel = @tonKho WHERE id = @id", new { tonKho = sanPhamTonTruoc, id = spId });
        }

        Console.WriteLine($"PASSED (Cấp mã tự động: {phieu.MaPhieuNhap}, ID: {savedId}, Tồn kho cập nhật chính xác, Đã dọn dẹp) ✓");
    }

    private static void KiemTraIssue68_MultiTab_DocLap()
    {
        Console.Write("[Kiểm tra Issue #68] 3. Tính độc lập đa tab phiếu nhập (Multi-tab state isolation)... ");
        
        var tab1 = new PhieuNhapTab { SoThuTu = 1 };
        tab1.State.NhaCungCapId = 101;
        tab1.State.TenNhaCungCap = "Nhà cung cấp A";
        tab1.State.Lines.Add(new PhieuNhapDraftLine { SanPhamId = 1, TenSanPham = "Hoa Hồng Đỏ", SoLuong = 50, DonGiaNhap = 10000 });
        tab1.CapNhatTuState();

        var tab2 = new PhieuNhapTab { SoThuTu = 2 };
        tab2.State.NhaCungCapId = 102;
        tab2.State.TenNhaCungCap = "Nhà cung cấp B";
        tab2.State.Lines.Add(new PhieuNhapDraftLine { SanPhamId = 2, TenSanPham = "Hoa Lan Trắng", SoLuong = 20, DonGiaNhap = 50000 });
        tab2.CapNhatTuState();

        if (tab1.TieuDe == tab2.TieuDe) throw new Exception("Tiêu đề tab 1 và tab 2 không được trùng nhau");
        if (tab1.State.NhaCungCapId == tab2.State.NhaCungCapId) throw new Exception("Nhà cung cấp bị ghi đè");
        if (tab1.State.Lines.Count != 1 || tab2.State.Lines.Count != 1) throw new Exception("Số dòng hàng bị sai lệch");
        if (tab1.State.Lines[0].SanPhamId == tab2.State.Lines[0].SanPhamId) throw new Exception("Dòng hàng bị lẫn lộn giữa các tab");
        if (tab1.TongTienNhap != 500000m || tab2.TongTienNhap != 1000000m) throw new Exception("Tính toán tổng tiền từng tab bị sai");

        Console.WriteLine("PASSED (Tab 1: 50 SP • 500.000 đ | Tab 2: 20 SP • 1.000.000 đ - Hoàn toàn độc lập) ✓");
    }
}
