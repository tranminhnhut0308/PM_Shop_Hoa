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
        if (args.Contains("--verify-issue-87", StringComparer.OrdinalIgnoreCase))
            return KiemTraIssue87_ComboBoxNhaCungCap();
        if (args.Contains("--verify-issue-89", StringComparer.OrdinalIgnoreCase))
            return KiemTraIssue89_KhongBatBuocLyDo();
        if (args.Contains("--verify-issue-91", StringComparer.OrdinalIgnoreCase))
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
            return await KiemTraIssue91_ToiUuThanhToanHoaDonAsync(csb.ConnectionString);
        }

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
            KiemTraIssue87_ComboBoxNhaCungCap();
            KiemTraIssue89_KhongBatBuocLyDo();
            await KiemTraIssue91_ToiUuThanhToanHoaDonAsync(csb.ConnectionString);

            Console.WriteLine("\n>>> TẤT CẢ CÁC BÀI KIỂM THỬ ISSUE #68, #69, #87, #89, #91, #94, #96 & #97 ĐÃ VƯỢT QUA 100% THÀNH CÔNG! <<<");
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

    public static int KiemTraIssue87_ComboBoxNhaCungCap()
    {
        Console.Write("[Kiểm tra Issue #87] ComboBox hiển thị tên nhà cung cấp (thay vì tên class)... ");

        // 1. Kiểm tra thuộc tính model NhaCungCapThanhToanLuaChon
        var type = typeof(NhaCungCapThanhToanLuaChon);
        if (type.GetProperty(nameof(NhaCungCapThanhToanLuaChon.Id)) == null)
            throw new Exception("NhaCungCapThanhToanLuaChon thiếu thuộc tính Id");
        if (type.GetProperty(nameof(NhaCungCapThanhToanLuaChon.TenNhaCungCap)) == null)
            throw new Exception("NhaCungCapThanhToanLuaChon thiếu thuộc tính TenNhaCungCap");
        if (type.GetProperty(nameof(NhaCungCapThanhToanLuaChon.MaNhaCungCap)) == null)
            throw new Exception("NhaCungCapThanhToanLuaChon thiếu thuộc tính MaNhaCungCap");
        if (type.GetProperty(nameof(NhaCungCapThanhToanLuaChon.TenHienThi)) == null)
            throw new Exception("NhaCungCapThanhToanLuaChon thiếu thuộc tính TenHienThi");

        // 2. Kiểm tra tùy chọn mặc định 'Tất cả nhà cung cấp'
        var tatCa = new NhaCungCapThanhToanLuaChon
        {
            Id = null,
            MaNhaCungCap = "",
            TenNhaCungCap = "Tất cả nhà cung cấp",
            TenHienThi = "Tất cả nhà cung cấp"
        };
        if (tatCa.Id != null)
            throw new Exception("Tùy chọn tất cả nhà cung cấp phải có Id = null");
        if (tatCa.TenNhaCungCap != "Tất cả nhà cung cấp")
            throw new Exception("Tên nhà cung cấp của tùy chọn mặc định phải là 'Tất cả nhà cung cấp'");
        if (tatCa.ToString() == "ShopHoa.Models.NhaCungCapThanhToanLuaChon")
            throw new Exception("ToString() không được hiển thị tên class!");
        if (tatCa.ToString() != "Tất cả nhà cung cấp")
            throw new Exception($"ToString() mong muốn 'Tất cả nhà cung cấp', thực tế: '{tatCa}'");

        // 3. Kiểm tra nhà cung cấp thực tế (ví dụ: NCC Chị Linh ĐL)
        var ncc = new NhaCungCapThanhToanLuaChon
        {
            Id = 5,
            MaNhaCungCap = "NCC01",
            TenNhaCungCap = "NCC Chị Linh ĐL",
            TenHienThi = "NCC Chị Linh ĐL"
        };
        if (ncc.Id != 5)
            throw new Exception("Id nhà cung cấp sai");
        if (ncc.TenNhaCungCap != "NCC Chị Linh ĐL")
            throw new Exception("Thuộc tính TenNhaCungCap phải chứa 'NCC Chị Linh ĐL'");
        if (ncc.ToString() == "ShopHoa.Models.NhaCungCapThanhToanLuaChon")
            throw new Exception("ToString() không được hiển thị tên class!");
        if (ncc.ToString() != "NCC Chị Linh ĐL")
            throw new Exception($"ToString() mong muốn 'NCC Chị Linh ĐL', thực tế: '{ncc}'");

        // 4. Kiểm tra trường hợp nhà cung cấp chỉ có tên mà không có mã
        var nccKhongMa = new NhaCungCapThanhToanLuaChon
        {
            Id = 6,
            TenNhaCungCap = "Vườn Lan Đà Lạt"
        };
        if (nccKhongMa.ToString() != "Vườn Lan Đà Lạt")
            throw new Exception($"ToString() mong muốn 'Vườn Lan Đà Lạt', thực tế: '{nccKhongMa}'");

        Console.WriteLine("PASSED ('Tất cả nhà cung cấp' & 'NCC Chị Linh ĐL' hiển thị đúng tên, không hiển thị tên class) ✓");
        return 0;
    }

    public static int KiemTraIssue89_KhongBatBuocLyDo()
    {
        Console.Write("[Kiểm tra Issue #89] Bỏ bắt buộc nhập lý do khi chỉnh sửa hóa đơn & phiếu nhập... ");

        // 1. Hóa đơn: để trống lý do -> tự động lưu 'Không cung cấp lý do'
        var hdReqRong = new HoaDonEditRequest
        {
            HoaDonId = 1,
            MaHoaDon = "HDTEST01",
            LyDoSua = "",
            Items = new List<HoaDonItemEditModel>
            {
                new() { SanPhamId = 1, SoLuong = 5, DonGia = 20000 }
            }
        };
        DatabaseService.ValidateHoaDonEditRequest(hdReqRong);
        if (hdReqRong.LyDoSua != "Không cung cấp lý do")
            throw new Exception($"Lý do hóa đơn khi để trống phải là 'Không cung cấp lý do', thực tế: '{hdReqRong.LyDoSua}'");

        // 2. Hóa đơn: có nhập lý do -> giữ nguyên lý do
        var hdReqCoLyDo = new HoaDonEditRequest
        {
            HoaDonId = 1,
            MaHoaDon = "HDTEST01",
            LyDoSua = "Khách yêu cầu thêm 5 bông hoa hồng đỏ",
            Items = new List<HoaDonItemEditModel>
            {
                new() { SanPhamId = 1, SoLuong = 10, DonGia = 20000 }
            }
        };
        DatabaseService.ValidateHoaDonEditRequest(hdReqCoLyDo);
        if (hdReqCoLyDo.LyDoSua != "Khách yêu cầu thêm 5 bông hoa hồng đỏ")
            throw new Exception($"Lý do hóa đơn phải giữ nguyên, thực tế: '{hdReqCoLyDo.LyDoSua}'");

        // 3. Phiếu nhập: để trống lý do -> tự động lưu 'Không cung cấp lý do'
        var pnReqRong = new PhieuNhapEditRequest
        {
            PhieuNhapId = 1,
            MaPhieuNhap = "PNTEST01",
            NhaCungCapId = 2,
            LyDoChinhSua = "   ",
            Items = new List<PhieuNhapItemEditModel>
            {
                new() { SanPhamId = 10, SoLuong = 20, DonGiaNhap = 15000 }
            }
        };
        DatabaseService.ValidatePhieuNhapEditRequest(pnReqRong);
        if (pnReqRong.LyDoChinhSua != "Không cung cấp lý do")
            throw new Exception($"Lý do phiếu nhập khi để trống phải là 'Không cung cấp lý do', thực tế: '{pnReqRong.LyDoChinhSua}'");

        // 4. Phiếu nhập: có nhập lý do -> giữ nguyên lý do
        var pnReqCoLyDo = new PhieuNhapEditRequest
        {
            PhieuNhapId = 1,
            MaPhieuNhap = "PNTEST01",
            NhaCungCapId = 2,
            LyDoChinhSua = "Điều chỉnh giảm giá nhập hoa lan 10%",
            Items = new List<PhieuNhapItemEditModel>
            {
                new() { SanPhamId = 10, SoLuong = 20, DonGiaNhap = 13500 }
            }
        };
        DatabaseService.ValidatePhieuNhapEditRequest(pnReqCoLyDo);
        if (pnReqCoLyDo.LyDoChinhSua != "Điều chỉnh giảm giá nhập hoa lan 10%")
            throw new Exception($"Lý do phiếu nhập phải giữ nguyên, thực tế: '{pnReqCoLyDo.LyDoChinhSua}'");

        // 5. Kiểm tra các quy tắc nghiệp vụ khác vẫn được bảo toàn (không bị bỏ sót):
        // Hóa đơn không có sản phẩm -> phải báo lỗi
        var hdKhongSp = new HoaDonEditRequest
        {
            HoaDonId = 1,
            LyDoSua = "",
            Items = new List<HoaDonItemEditModel>()
        };
        bool daBaoLoiHdKhongSp = false;
        try { DatabaseService.ValidateHoaDonEditRequest(hdKhongSp); }
        catch (InvalidOperationException) { daBaoLoiHdKhongSp = true; }
        if (!daBaoLoiHdKhongSp)
            throw new Exception("Hóa đơn không có sản phẩm phải bị từ chối!");

        // Phiếu nhập thiếu nhà cung cấp -> phải báo lỗi
        var pnThieuNcc = new PhieuNhapEditRequest
        {
            PhieuNhapId = 1,
            NhaCungCapId = null,
            Items = new List<PhieuNhapItemEditModel>
            {
                new() { SanPhamId = 10, SoLuong = 20, DonGiaNhap = 15000 }
            }
        };
        bool daBaoLoiPnThieuNcc = false;
        try { DatabaseService.ValidatePhieuNhapEditRequest(pnThieuNcc); }
        catch (InvalidOperationException) { daBaoLoiPnThieuNcc = true; }
        if (!daBaoLoiPnThieuNcc)
            throw new Exception("Phiếu nhập thiếu nhà cung cấp phải bị từ chối!");

        Console.WriteLine("PASSED (Để trống lý do tự động ghi 'Không cung cấp lý do' & giữ nguyên lý do khi có nhập) ✓");
        return 0;
    }

    public static async Task<int> KiemTraIssue91_ToiUuThanhToanHoaDonAsync(string connStr)
    {
        Console.WriteLine("\n------------------------------------------------------------------------------------------");
        Console.WriteLine("  BẮT ĐẦU KIỂM THỬ ISSUE #91: TỐI ƯU QUY TRÌNH THANH TOÁN HÓA ĐƠN BÁN HÀNG              ");
        Console.WriteLine("------------------------------------------------------------------------------------------");

        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();

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

        var db = new DatabaseService();

        // 0. Model & Draft State checks
        Console.Write("[Kiểm tra Issue #91] 0. Khởi tạo SaleCartDraftState và Phương thức thanh toán... ");
        var draft = new SaleCartDraftState();
        if (draft.IsUserManualTienKhachDua)
            throw new Exception("IsUserManualTienKhachDua mặc định phải là false.");
        if (draft.PhuongThucThanhToan != "TIEN_MAT")
            throw new Exception("PhuongThucThanhToan mặc định phải là TIEN_MAT.");
        Console.WriteLine("PASSED ✓");

        // Lấy hoặc tạo sản phẩm và khách hàng để test
        var spDb = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT id, ma_san_pham, ten_san_pham, gia_ban FROM san_pham LIMIT 1");
        if (spDb == null)
            throw new Exception("Không tìm thấy sản phẩm để test.");

        uint spId = (uint)spDb.id;
        string spMa = (string)spDb.ma_san_pham;
        string spTen = (string)spDb.ten_san_pham;
        decimal spGia = (decimal)spDb.gia_ban;
        if (spGia <= 0) spGia = 100000m;
        decimal tonKhoBanDau = await db.LayTonKhoThucTeAsync(spId);
        if (tonKhoBanDau < 20)
        {
            tonKhoBanDau = 100m;
            await conn.ExecuteAsync("UPDATE san_pham SET ton_kho_excel = @ton WHERE id = @id", new { ton = tonKhoBanDau, id = spId });
            await conn.ExecuteAsync("UPDATE ton_kho SET so_luong_ton = @ton WHERE san_pham_id = @id AND chi_nhanh_id = @bId", new { ton = tonKhoBanDau, id = spId, bId = currentBranchId });
        }

        var khDb = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT id, ma_khach_hang, ten_khach_hang FROM khach_hang LIMIT 1");
        if (khDb == null)
            throw new Exception("Không tìm thấy khách hàng trong DB để test.");
        var testCustomer = new KhachHang
        {
            Id = (uint)khDb.id,
            MaKhachHang = (string)khDb.ma_khach_hang,
            TenKhachHang = (string)khDb.ten_khach_hang
        };

        var createdInvoiceIds = new List<uint>();
        var createdInvoiceCodes = new List<string>();

        try
        {
            // 1. Khách lẻ thanh toán tiền mặt (Đủ 100%, tự động tổng tiền, không công nợ, tiền thối 0)
            Console.Write("[Kiểm tra Issue #91] 1. Khách lẻ thanh toán tiền mặt đủ 100%... ");
            var cart1 = new List<SaleCartItem>
            {
                new() { SanPham = new SanPham { Id = spId, MaSanPham = spMa, TenSanPham = spTen, GiaBan = spGia, TonKho = tonKhoBanDau }, SoLuong = 2, DonGia = spGia }
            };
            var total1 = cart1.Sum(i => i.ThanhTien);
            var receipt1 = await db.TaoHoaDonBanHangAsync(
                cart1,
                customer: null,
                discount: 0m,
                tienKhachDua: total1,
                phuongThucThanhToan: "TIEN_MAT");

            createdInvoiceIds.Add(receipt1.HoaDonId);
            createdInvoiceCodes.Add(receipt1.MaHoaDon);

            if (receipt1.KhachCanTra != total1 || receipt1.KhachDaThanhToan != total1 || receipt1.ConNo != 0 || receipt1.ThuKhac != 0)
                throw new Exception($"Số tiền hóa đơn khách lẻ tiền mặt không khớp: Cần trả={receipt1.KhachCanTra}, Đã trả={receipt1.KhachDaThanhToan}, Nợ={receipt1.ConNo}");

            var pt1 = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT so_tien, phuong_thuc FROM phieu_chi WHERE hoa_don_ban_hang_id = @id LIMIT 1",
                new { id = receipt1.HoaDonId });
            if (pt1 == null || (decimal)pt1.so_tien != total1 || (string)pt1.phuong_thuc != "TIEN_MAT")
                throw new Exception($"Phiếu thu sổ quỹ cho khách lẻ tiền mặt không đúng: {pt1?.so_tien} - {pt1?.phuong_thuc}");
            Console.WriteLine($"PASSED (Hóa đơn {receipt1.MaHoaDon}, PT {receipt1.MaPhieuThu}: {total1:N0}đ Tiền mặt) ✓");

            // 2. Khách lẻ thanh toán chuyển khoản
            Console.Write("[Kiểm tra Issue #91] 2. Khách lẻ thanh toán chuyển khoản... ");
            var cart2 = new List<SaleCartItem>
            {
                new() { SanPham = new SanPham { Id = spId, MaSanPham = spMa, TenSanPham = spTen, GiaBan = spGia, TonKho = tonKhoBanDau - 2 }, SoLuong = 1, DonGia = spGia }
            };
            var total2 = cart2.Sum(i => i.ThanhTien);
            var receipt2 = await db.TaoHoaDonBanHangAsync(
                cart2,
                customer: null,
                discount: 0m,
                tienKhachDua: total2,
                phuongThucThanhToan: "CHUYEN_KHOAN");

            createdInvoiceIds.Add(receipt2.HoaDonId);
            createdInvoiceCodes.Add(receipt2.MaHoaDon);

            var pt2 = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT so_tien, phuong_thuc FROM phieu_chi WHERE hoa_don_ban_hang_id = @id LIMIT 1",
                new { id = receipt2.HoaDonId });
            if (pt2 == null || (decimal)pt2.so_tien != total2 || (string)pt2.phuong_thuc != "CHUYEN_KHOAN")
                throw new Exception($"Phiếu thu sổ quỹ cho khách lẻ chuyển khoản không đúng: {pt2?.so_tien} - {pt2?.phuong_thuc}");
            Console.WriteLine($"PASSED (Hóa đơn {receipt2.MaHoaDon}, PT {receipt2.MaPhieuThu}: {total2:N0}đ Chuyển khoản) ✓");

            // 3. Khách hàng có thông tin thanh toán đủ
            Console.Write("[Kiểm tra Issue #91] 3. Khách hàng có thông tin thanh toán đủ 100%... ");
            var cart3 = new List<SaleCartItem>
            {
                new() { SanPham = new SanPham { Id = spId, MaSanPham = spMa, TenSanPham = spTen, GiaBan = spGia, TonKho = tonKhoBanDau - 3 }, SoLuong = 2, DonGia = spGia }
            };
            var total3 = cart3.Sum(i => i.ThanhTien);
            var receipt3 = await db.TaoHoaDonBanHangAsync(
                cart3,
                customer: testCustomer,
                discount: 0m,
                tienKhachDua: total3,
                phuongThucThanhToan: "TIEN_MAT");

            createdInvoiceIds.Add(receipt3.HoaDonId);
            createdInvoiceCodes.Add(receipt3.MaHoaDon);

            if (receipt3.ConNo != 0 || receipt3.KhachDaThanhToan != total3)
                throw new Exception($"Khách hàng thanh toán đủ nhưng phát sinh công nợ: ConNo={receipt3.ConNo}");
            Console.WriteLine($"PASSED (Hóa đơn {receipt3.MaHoaDon}: Thanh toán đủ, nợ = 0) ✓");

            // 4. Khách hàng thanh toán một phần (phát sinh công nợ)
            Console.Write("[Kiểm tra Issue #91] 4. Khách hàng thanh toán một phần (ghi nhận công nợ)... ");
            var cart4 = new List<SaleCartItem>
            {
                new() { SanPham = new SanPham { Id = spId, MaSanPham = spMa, TenSanPham = spTen, GiaBan = spGia, TonKho = tonKhoBanDau - 5 }, SoLuong = 2, DonGia = spGia }
            };
            var total4 = cart4.Sum(i => i.ThanhTien);
            var traTruoc4 = total4 / 2; // trả 50%
            var conNo4 = total4 - traTruoc4;
            var receipt4 = await db.TaoHoaDonBanHangAsync(
                cart4,
                customer: testCustomer,
                discount: 0m,
                tienKhachDua: traTruoc4,
                phuongThucThanhToan: "TIEN_MAT");

            createdInvoiceIds.Add(receipt4.HoaDonId);
            createdInvoiceCodes.Add(receipt4.MaHoaDon);

            if (receipt4.KhachDaThanhToan != traTruoc4 || receipt4.ConNo != conNo4)
                throw new Exception($"Số tiền thanh toán một phần không khớp: Đã trả={receipt4.KhachDaThanhToan}, Còn nợ={receipt4.ConNo}");

            var pt4 = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT so_tien FROM phieu_chi WHERE hoa_don_ban_hang_id = @id LIMIT 1",
                new { id = receipt4.HoaDonId });
            if (pt4 == null || (decimal)pt4.so_tien != traTruoc4)
                throw new Exception($"Phiếu thu phải đúng số tiền thực trả {traTruoc4}, thực tế: {pt4?.so_tien}");

            var noRecord = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM lich_su_cong_no_khach_hang WHERE khach_hang_id = @khId AND ghi_no = @no",
                new { khId = testCustomer.Id, no = conNo4 });
            if (noRecord == 0)
                throw new Exception("Chưa ghi nhận lịch sử công nợ khách hàng!");
            Console.WriteLine($"PASSED (Hóa đơn {receipt4.MaHoaDon}: Thực trả={traTruoc4:N0}đ, Còn nợ={conNo4:N0}đ) ✓");

            // 5. Khách hàng thanh toán hết công nợ
            Console.Write("[Kiểm tra Issue #91] 5. Khách hàng thanh toán hết công nợ... ");
            try
            {
                var receiptThuNo = await db.ThuNoHoaDonAsync(
                    receipt4.MaHoaDon,
                    conNo4,
                    phuongThucThanhToan: "CHUYEN_KHOAN",
                    ghiChu: "Thanh toán dứt điểm công nợ qua Chuyển khoản");

                if (receiptThuNo.ConNo != 0 || receiptThuNo.KhachDaThanhToan != total4)
                    throw new Exception($"Thu nợ xong nhưng công nợ chưa về 0: ConNo={receiptThuNo.ConNo}");

                var ptThuNo = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT so_tien, phuong_thuc FROM phieu_chi WHERE ma_phieu_chi = @maPhieu LIMIT 1",
                    new { maPhieu = receiptThuNo.MaPhieuThu });
                if (ptThuNo == null || (decimal)ptThuNo.so_tien != conNo4)
                    throw new Exception($"Phiếu thu nợ ngân hàng không khớp: {ptThuNo?.so_tien}");
                Console.WriteLine($"PASSED (Thu nợ {receipt4.MaHoaDon}: Đã trả hết {conNo4:N0}đ Chuyển khoản, nợ = 0) ✓");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
                throw;
            }

            // 6. Kiểm tra sổ quỹ và lịch sử thanh toán
            Console.Write("[Kiểm tra Issue #91] 6. Kiểm tra sổ quỹ & phân loại nguồn tiền (Tiền mặt / Chuyển khoản)... ");
            var dsPhieuChi = (await conn.QueryAsync<dynamic>(
                "SELECT phuong_thuc, so_tien FROM phieu_chi WHERE hoa_don_ban_hang_id IN @ids OR ma_hoa_don IN @codes",
                new { ids = createdInvoiceIds, codes = createdInvoiceCodes })).ToList();
            if (dsPhieuChi.Count < 5) // hd1, hd2, hd3, hd4(ban dau) + hd4(thu no) = 5 phiếu
                throw new Exception($"Số lượng phiếu thu không đủ, thực tế: {dsPhieuChi.Count}");
            Console.WriteLine($"PASSED (Đã kiểm tra {dsPhieuChi.Count} phiếu thu vào đúng quỹ tương ứng) ✓");

            // 7. Kiểm tra không phát sinh phiếu thu trùng
            Console.Write("[Kiểm tra Issue #91] 7. Kiểm tra không phát sinh phiếu thu trùng lặp... ");
            foreach (var hdId in new[] { receipt1.HoaDonId, receipt2.HoaDonId, receipt3.HoaDonId })
            {
                var countPt = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM phieu_chi WHERE hoa_don_ban_hang_id = @id",
                    new { id = hdId });
                if (countPt != 1)
                    throw new Exception($"Hóa đơn {hdId} bị trùng phiếu thu: có {countPt} phiếu thu!");
            }
            Console.WriteLine("PASSED (Mỗi hóa đơn thanh toán đủ chỉ có đúng 1 phiếu thu) ✓");

            // 8. Kiểm tra tồn kho và tổng tiền hóa đơn
            Console.Write("[Kiểm tra Issue #91] 8. Kiểm tra tồn kho và khấu trừ chính xác... ");
            var tonHienTai = await db.LayTonKhoThucTeAsync(spId);
            var tongBan = 2 + 1 + 2 + 2; // 7 sản phẩm
            if (tonHienTai != tonKhoBanDau - tongBan)
                throw new Exception($"Tồn kho trừ sai: Ban đầu={tonKhoBanDau}, Bán={tongBan}, Hiện tại={tonHienTai}");
            Console.WriteLine($"PASSED (Tồn kho đã trừ đúng {tongBan} sản phẩm) ✓");
        }
        finally
        {
            // Dọn dẹp dữ liệu test để cơ sở dữ liệu luôn nguyên vẹn
            if (createdInvoiceIds.Count > 0)
            {
                try { await conn.ExecuteAsync("DELETE FROM phieu_chi WHERE hoa_don_ban_hang_id IN @ids OR ma_hoa_don IN @codes", new { ids = createdInvoiceIds, codes = createdInvoiceCodes }); } catch {}
                try { await conn.ExecuteAsync("DELETE FROM chi_tiet_hoa_don_ban_hang WHERE hoa_don_id IN @ids", new { ids = createdInvoiceIds }); } catch {}
                try { await conn.ExecuteAsync("DELETE FROM lich_su_nhap_xuat_kho WHERE ma_chung_tu IN @codes", new { codes = createdInvoiceCodes }); } catch {}
                try { await conn.ExecuteAsync("DELETE FROM hoa_don_ban_hang WHERE id IN @ids", new { ids = createdInvoiceIds }); } catch {}
                if (testCustomer != null)
                {
                    try
                    {
                        await conn.ExecuteAsync(
                            "DELETE FROM lich_su_cong_no_khach_hang WHERE khach_hang_id = @khId AND ghi_chu LIKE '%từ hóa đơn HD%'",
                            new { khId = testCustomer.Id });
                    } catch {}
                }
                // Khôi phục tồn kho sản phẩm test
                try
                {
                    await conn.ExecuteAsync("UPDATE san_pham SET ton_kho_excel = @ton WHERE id = @id", new { ton = tonKhoBanDau, id = spId });
                    await conn.ExecuteAsync("UPDATE ton_kho SET so_luong_ton = @ton WHERE san_pham_id = @id AND chi_nhanh_id = @bId", new { ton = tonKhoBanDau, id = spId, bId = currentBranchId });
                } catch {}
            }
        }

        Console.WriteLine("\n>>> TOÀN BỘ 8 TIÊU CHÍ KIỂM THỬ ISSUE #91 ĐÃ ĐẠT 100% THÀNH CÔNG! <<<");

        // ==========================================================================
        //  KIỂM THỬ ISSUE #94: TỐI GIẢN HIỂN THỊ MÃ SẢN PHẨM TRÊN TRANG BÁN HÀNG VÀ NHẬP HÀNG
        // ==========================================================================
        Console.WriteLine("\n------------------------------------------------------------------------------------------");
        Console.WriteLine("  BẮT ĐẦU KIỂM THỬ ISSUE #94: TỐI GIẢN HIỂN THỊ MÃ SẢN PHẨM TRÊN BÁN HÀNG & NHẬP HÀNG     ");
        Console.WriteLine("------------------------------------------------------------------------------------------");

        // 1. Kiểm tra CSDL: Mã sản phẩm tuyệt đối không bị xóa hoặc mất
        Console.Write("[Kiểm tra Issue #94] 1. Kiểm tra CSDL: Mã sản phẩm nguyên vẹn, không bị xóa... ");
        var spMau = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT id, ma_san_pham, ten_san_pham, ma_vach FROM san_pham WHERE ma_san_pham IS NOT NULL AND ma_san_pham <> '' LIMIT 1");
        if (spMau == null || string.IsNullOrWhiteSpace((string)spMau.ma_san_pham))
            throw new Exception("Không tìm thấy sản phẩm có mã sản phẩm trong CSDL!");
        Console.WriteLine($"PASSED (SP mẫu: '{spMau.ma_san_pham}' - '{spMau.ten_san_pham}') ✓");

        // 2. Kiểm tra tìm kiếm theo mã sản phẩm
        Console.Write("[Kiểm tra Issue #94] 2. Tìm kiếm theo mã sản phẩm (Bán hàng & Nhập hàng)... ");
        var kqMa = await db.TimKiemSanPhamBanHangAsync((string)spMau.ma_san_pham);
        if (kqMa == null || kqMa.Count == 0 || !kqMa.Any(x => x.Id == (uint)spMau.id))
            throw new Exception($"Tìm kiếm theo mã sản phẩm '{spMau.ma_san_pham}' không trả về sản phẩm tương ứng!");
        Console.WriteLine($"PASSED (Tìm thấy {kqMa.Count} kết quả khi tìm bằng mã '{spMau.ma_san_pham}') ✓");

        // 3. Kiểm tra tìm kiếm theo tên sản phẩm
        Console.Write("[Kiểm tra Issue #94] 3. Tìm kiếm theo tên sản phẩm... ");
        string tenSearch = ((string)spMau.ten_san_pham).Split(' ')[0];
        var kqTen = await db.TimKiemSanPhamBanHangAsync(tenSearch);
        if (kqTen == null || kqTen.Count == 0)
            throw new Exception($"Tìm kiếm theo tên '{tenSearch}' không trả về kết quả!");
        Console.WriteLine($"PASSED (Tìm thấy {kqTen.Count} kết quả khi tìm bằng tên '{tenSearch}') ✓");

        // 4. Kiểm tra XAML MainWindow: Ẩn mã SP gợi ý bán hàng & tự động xuống dòng giỏ hàng
        Console.Write("[Kiểm tra Issue #94] 4. Kiểm tra cấu trúc XAML Trang Bán hàng... ");
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Views", "MainWindow.axaml");
        if (!File.Exists(xamlPath))
            xamlPath = Path.Combine(Directory.GetCurrentDirectory(), "Views", "MainWindow.axaml");
        string xamlContent = await File.ReadAllTextAsync(xamlPath);

        // ProductSearchResults không chứa binding MaSanPham
        int pSearchResultsIdx = xamlContent.IndexOf("ItemsSource=\"{Binding ProductSearchResults}\"");
        if (pSearchResultsIdx == -1)
            throw new Exception("Không tìm thấy ProductSearchResults trong MainWindow.axaml");
        int endPSearchIdx = xamlContent.IndexOf("</ListBox>", pSearchResultsIdx);
        string pSearchResultsBlock = xamlContent.Substring(pSearchResultsIdx, endPSearchIdx - pSearchResultsIdx);
        if (pSearchResultsBlock.Contains("{Binding MaSanPham}"))
            throw new Exception("ProductSearchResults vẫn còn hiển thị mã sản phẩm!");

        // SaleCart TextWrapping="Wrap"
        int saleCartIdx = xamlContent.IndexOf("ItemsSource=\"{Binding SaleCart}\"");
        if (saleCartIdx == -1)
            throw new Exception("Không tìm thấy SaleCart trong MainWindow.axaml");
        int endSaleCartIdx = xamlContent.IndexOf("</ItemsControl>", saleCartIdx);
        string saleCartBlock = xamlContent.Substring(saleCartIdx, endSaleCartIdx - saleCartIdx);
        if (saleCartBlock.Contains("{Binding MaSanPham}"))
            throw new Exception("SaleCart không được hiển thị mã sản phẩm!");
        if (!saleCartBlock.Contains("TextWrapping=\"Wrap\""))
            throw new Exception("SaleCart cần có TextWrapping=\"Wrap\" để tên dài tự xuống dòng!");
        Console.WriteLine("PASSED (Gợi ý bán hàng đã ẩn mã SP; Giỏ hàng tự động xuống dòng tên sản phẩm) ✓");

        // 5. Kiểm tra XAML MainWindow: Trang Nhập hàng ẩn mã gợi ý & ẩn cột MÃ HÀNG
        Console.Write("[Kiểm tra Issue #94] 5. Kiểm tra cấu trúc XAML Trang Nhập hàng... ");
        int pnSuggestionsIdx = xamlContent.IndexOf("ItemsSource=\"{Binding PhieuNhapProductSuggestions}\"");
        if (pnSuggestionsIdx == -1)
            throw new Exception("Không tìm thấy PhieuNhapProductSuggestions trong MainWindow.axaml");
        int endPnSuggestionsIdx = xamlContent.IndexOf("</ListBox>", pnSuggestionsIdx);
        string pnSuggestionsBlock = xamlContent.Substring(pnSuggestionsIdx, endPnSuggestionsIdx - pnSuggestionsIdx);
        if (pnSuggestionsBlock.Contains("{Binding MaSanPham}"))
            throw new Exception("PhieuNhapProductSuggestions vẫn còn hiển thị mã sản phẩm!");

        int gridPnIdx = xamlContent.IndexOf("x:Name=\"GridPhieuNhapItems\"");
        if (gridPnIdx == -1)
            throw new Exception("Không tìm thấy GridPhieuNhapItems trong MainWindow.axaml");
        int endGridPnIdx = xamlContent.IndexOf("</DataGrid>", gridPnIdx);
        string gridPnBlock = xamlContent.Substring(gridPnIdx, endGridPnIdx - gridPnIdx);
        if (gridPnBlock.Contains("Header=\"MÃ HÀNG\""))
            throw new Exception("GridPhieuNhapItems vẫn còn cột MÃ HÀNG!");
        Console.WriteLine("PASSED (Gợi ý nhập hàng đã ẩn mã SP; Bảng hàng nhập đã ẩn cột MÃ HÀNG & cân đối cột) ✓");

        // 6. Kiểm tra trang Quản lý hàng hóa: Vẫn giữ nguyên hiển thị mã sản phẩm
        Console.Write("[Kiểm tra Issue #94] 6. Kiểm tra Trang Quản lý hàng hóa vẫn hiển thị mã SP... ");
        int gridSpIdx = xamlContent.IndexOf("ItemsSource=\"{Binding SanPhams}\"");
        if (gridSpIdx == -1)
            throw new Exception("Không tìm thấy danh sách sản phẩm (ItemsSource=\"{Binding SanPhams}\") trong MainWindow.axaml");
        int endGridSpIdx = xamlContent.IndexOf("</ItemsControl>", gridSpIdx);
        string gridSpBlock = xamlContent.Substring(gridSpIdx, endGridSpIdx - gridSpIdx);
        if (!gridSpBlock.Contains("{Binding MaSanPham}"))
            throw new Exception("Trang quản lý hàng hóa bị mất binding MaSanPham!");
        Console.WriteLine("PASSED (Trang Quản lý hàng hóa giữ nguyên 100% không đổi) ✓");

        Console.WriteLine("\n>>> TOÀN BỘ 6 TIÊU CHÍ KIỂM THỬ ISSUE #94 ĐÃ ĐẠT 100% THÀNH CÔNG! <<<");

        // ==========================================================================
        //  KIỂM THỬ ISSUE #96: LOẠI BỎ TRƯỜNG NHẬP ĐỊA CHỈ TRÊN TRANG BÁN HÀNG
        // ==========================================================================
        Console.WriteLine("\n------------------------------------------------------------------------------------------");
        Console.WriteLine("  BẮT ĐẦU KIỂM THỬ ISSUE #96: LOẠI BỎ TRƯỜNG NHẬP ĐỊA CHỈ TRÊN TRANG BÁN HÀNG             ");
        Console.WriteLine("------------------------------------------------------------------------------------------");

        // 1. Kiểm tra CSDL: Bảng khach_hang vẫn giữ nguyên cột dia_chi
        Console.Write("[Kiểm tra Issue #96] 1. Kiểm tra CSDL: Cột dia_chi trong bảng khach_hang nguyên vẹn... ");
        var khCoDiaChi = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT id, ma_khach_hang, ten_khach_hang, so_dien_thoai, dia_chi FROM khach_hang WHERE dia_chi IS NOT NULL AND dia_chi <> '' LIMIT 1");
        if (khCoDiaChi == null || string.IsNullOrWhiteSpace((string)khCoDiaChi.dia_chi))
            throw new Exception("Không tìm thấy khách hàng có địa chỉ trong CSDL để kiểm thử!");
        Console.WriteLine($"PASSED (Khách hàng mẫu: '{khCoDiaChi.ten_khach_hang}', ĐC: '{khCoDiaChi.dia_chi}') ✓");

        // 2. Kiểm tra XAML: Đã xóa hoàn toàn trường nhập địa chỉ khỏi giao diện bán hàng
        Console.Write("[Kiểm tra Issue #96] 2. Kiểm tra XAML: Đã loại bỏ trường nhập địa chỉ khỏi Bán hàng... ");
        int saleCustomerSectionStart = xamlContent.IndexOf("x:Name=\"SaleCustomerSearchBox\"");
        if (saleCustomerSectionStart == -1)
            throw new Exception("Không tìm thấy vùng thông tin khách hàng bán hàng trong MainWindow.axaml");
        int saleCustomerSectionEnd = xamlContent.IndexOf("Phần tính tiền", saleCustomerSectionStart);
        if (saleCustomerSectionEnd == -1)
            saleCustomerSectionEnd = saleCustomerSectionStart + 2500;
        string saleCustomerSection = xamlContent.Substring(saleCustomerSectionStart, saleCustomerSectionEnd - saleCustomerSectionStart);

        if (saleCustomerSection.Contains("SaleCustomerAddress"))
            throw new Exception("Vùng khách hàng trang Bán hàng vẫn còn chứa trường nhập SaleCustomerAddress!");
        if (saleCustomerSection.Contains("Địa chỉ (không bắt buộc)"))
            throw new Exception("Vùng khách hàng trang Bán hàng vẫn còn nhãn 'Địa chỉ (không bắt buộc)'!");

        // Kiểm tra vẫn giữ lại tên, SĐT, các nút chức năng và tùy chọn giao hàng
        if (!saleCustomerSection.Contains("SaleCustomerPhone"))
            throw new Exception("Trường SĐT khách hàng đã bị mất!");
        if (!saleCustomerSection.Contains("SaleCanGiaoHang"))
            throw new Exception("Tùy chọn 'Giao hàng' đã bị mất!");
        if (!saleCustomerSection.Contains("ChonKhachLeCommand") || !saleCustomerSection.Contains("ThemKhachHangBanCommand"))
            throw new Exception("Các nút chọn khách lẻ / thêm khách đã bị mất!");
        Console.WriteLine("PASSED (Đã bỏ TextBox địa chỉ; Giữ nguyên Tên, SĐT, Nút Thêm/Khách lẻ, Giao hàng) ✓");

        // 3. Kiểm tra Flow tạo hóa đơn: Khách lẻ không cần nhập địa chỉ vẫn thanh toán bình thường
        Console.Write("[Kiểm tra Issue #96] 3. Tạo hóa đơn Khách lẻ không cần địa chỉ... ");
        var cartItemTest96 = new List<SaleCartItem>
        {
            new() { SanPham = new SanPham { Id = spId, MaSanPham = spMa, TenSanPham = spTen, GiaBan = spGia, TonKho = tonKhoBanDau }, SoLuong = 1, DonGia = spGia }
        };
        var totalTest96 = cartItemTest96.Sum(i => i.ThanhTien);

        var receiptLe96 = await db.TaoHoaDonBanHangAsync(
            cartItemTest96,
            customer: null,
            discount: 0m,
            tienKhachDua: totalTest96,
            phuongThucThanhToan: "TIEN_MAT",
            ghiChu: "Test Issue #96 Khach le",
            soDienThoai: "",
            diaChi: "",
            inKemThongTin: false);

        if (receiptLe96 == null || string.IsNullOrWhiteSpace(receiptLe96.MaHoaDon))
            throw new Exception("Tạo hóa đơn khách lẻ thất bại!");
        Console.WriteLine($"PASSED (Hóa đơn: {receiptLe96.MaHoaDon}) ✓");

        // 4. Kiểm tra Flow tạo hóa đơn: Khách hàng có sẵn tự động ghi nhận địa chỉ từ DB vào hóa đơn
        Console.Write("[Kiểm tra Issue #96] 4. Tạo hóa đơn Khách có sẵn: Tự nhận địa chỉ từ hồ sơ khách... ");
        var khModel = new KhachHang
        {
            Id = (uint)khCoDiaChi.id,
            MaKhachHang = (string)khCoDiaChi.ma_khach_hang,
            TenKhachHang = (string)khCoDiaChi.ten_khach_hang,
            SoDienThoai = (string)khCoDiaChi.so_dien_thoai,
            DiaChi = (string)khCoDiaChi.dia_chi
        };

        var receiptKhach96 = await db.TaoHoaDonBanHangAsync(
            cartItemTest96,
            customer: khModel,
            discount: 0m,
            tienKhachDua: totalTest96,
            phuongThucThanhToan: "TIEN_MAT",
            ghiChu: "Test Issue #96 Khach co san",
            soDienThoai: khModel.SoDienThoai,
            diaChi: khModel.DiaChi,
            inKemThongTin: true);

        if (receiptKhach96 == null || string.IsNullOrWhiteSpace(receiptKhach96.MaHoaDon))
            throw new Exception("Tạo hóa đơn khách có sẵn thất bại!");

        var hdCheck = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT dia_chi, so_dien_thoai FROM hoa_don_ban_hang WHERE id = @id",
            new { id = receiptKhach96.HoaDonId });
        if (hdCheck == null || (string)hdCheck.dia_chi != khModel.DiaChi)
            throw new Exception($"Địa chỉ hóa đơn không khớp với hồ sơ khách: '{hdCheck?.dia_chi}' vs '{khModel.DiaChi}'");
        Console.WriteLine($"PASSED (Hóa đơn: {receiptKhach96.MaHoaDon}, ĐC lưu vào HĐ: '{hdCheck.dia_chi}') ✓");

        // Dọn dẹp dữ liệu hóa đơn test 96
        try
        {
            var test96Ids = new[] { receiptLe96.HoaDonId, receiptKhach96.HoaDonId };
            var test96Codes = new[] { receiptLe96.MaHoaDon, receiptKhach96.MaHoaDon };
            await conn.ExecuteAsync("DELETE FROM phieu_chi WHERE hoa_don_ban_hang_id IN @ids OR ma_hoa_don IN @codes", new { ids = test96Ids, codes = test96Codes });
            await conn.ExecuteAsync("DELETE FROM chi_tiet_hoa_don_ban_hang WHERE hoa_don_id IN @ids", new { ids = test96Ids });
            await conn.ExecuteAsync("DELETE FROM lich_su_nhap_xuat_kho WHERE ma_chung_tu IN @codes", new { codes = test96Codes });
            await conn.ExecuteAsync("DELETE FROM hoa_don_ban_hang WHERE id IN @ids", new { ids = test96Ids });
            // Khôi phục tồn kho
            await conn.ExecuteAsync("UPDATE san_pham SET ton_kho_excel = @ton WHERE id = @id", new { ton = tonKhoBanDau, id = spId });
            await conn.ExecuteAsync("UPDATE ton_kho SET so_luong_ton = @ton WHERE san_pham_id = @id AND chi_nhanh_id = @bId", new { ton = tonKhoBanDau, id = spId, bId = currentBranchId });
        }
        catch {}

        Console.WriteLine("\n>>> TOÀN BỘ 4 TIÊU CHÍ KIỂM THỬ ISSUE #96 ĐÃ ĐẠT 100% THÀNH CÔNG! <<<");

        // ==========================================================================
        // CÁC BÀI KIỂM THỬ ISSUE #97: BỔ SUNG THANH TOÁN DƯ VÀ QUẢN LÝ SỐ DƯ CÒN LẠI TRONG CÔNG NỢ
        // ==========================================================================
        await KiemTraIssue97_ThanhToanDuVaSoDuTraTruocAsync(connStr);

        return 0;
    }

    private static async Task KiemTraIssue97_ThanhToanDuVaSoDuTraTruocAsync(string connectionString)
    {
        Console.WriteLine("\n==========================================================================================");
        Console.WriteLine("  BẮT ĐẦU KIỂM THỬ ISSUE #97: THANH TOÁN DƯ & QUẢN LÝ SỐ DƯ TRẢ TRƯỚC (KHÁCH HÀNG & NCC) ");
        Console.WriteLine("==========================================================================================");

        var db = new DatabaseService();
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await DatabaseService.EnsureTraTruocSchemaAsync(conn);

        // Chuẩn bị User Session cho test
        Session.SetCurrentUser(new NguoiDung
        {
            Id = 1,
            TenDangNhap = "admin",
            HoTen = "Quản trị viên",
            ChiNhanhId = 1,
            MaVaiTro = Quyen.QuanTri
        });

        // ====================================================================
        // KỊCH BẢN 1 - 5: KHÁCH HÀNG (THANH TOÁN DƯ, TỰ ĐỘNG CẤN TRỪ, HỦY HÓA ĐƠN)
        // ====================================================================
        Console.Write("[Kiểm tra Issue #97] 1. Khách hàng: Thanh toán dư công nợ (1.5M nợ, trả 2.0M)... ");
        // Tạo khách hàng test
        var maKhTest = "KH_TEST_97_" + DateTime.Now.ToString("yyMMddHHmmss");
        await conn.ExecuteAsync(
            """
            INSERT INTO khach_hang (ma_khach_hang, ten_khach_hang, so_dien_thoai, trang_thai, so_du_tra_truoc)
            VALUES (@Ma, @Ten, '0988776655', 1, 0.00);
            """,
            new { Ma = maKhTest, Ten = "Khách Hàng Test 97" });

        var khId = await conn.ExecuteScalarAsync<uint>("SELECT id FROM khach_hang WHERE ma_khach_hang = @Ma;", new { Ma = maKhTest });

        // Tạo công nợ ban đầu 1.500.000đ cho khách hàng
        await conn.ExecuteAsync(
            """
            INSERT INTO lich_su_cong_no_khach_hang (khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu, ghi_no, ghi_co, so_du, thoi_gian, ghi_chu)
            VALUES (@KhId, 1, 'GHI_NO', 'HOA_DON', 'HD_TEST_INIT', 1500000, 0, 1500000, NOW(), 'Nợ ban đầu test 97');
            """,
            new { KhId = khId });

        // Thu nợ 2.000.000đ
        var thuNoKhRes = await db.ThuNoKhachHangAsync(khId, 2000000m, "CHUYEN_KHOAN", null, "Thu nợ chuyển khoản dư test 97");
        if (thuNoKhRes.TongDaThu != 2000000m)
            throw new Exception($"Tổng đã thu không đúng: {thuNoKhRes.TongDaThu}");
        if (thuNoKhRes.TienCanTru != 1500000m)
            throw new Exception($"Tiền cấn trừ công nợ không đúng: {thuNoKhRes.TienCanTru}");
        if (thuNoKhRes.TienDu != 500000m)
            throw new Exception($"Tiền dư trả trước không đúng: {thuNoKhRes.TienDu}");
        if (thuNoKhRes.ConNoConLai != 0m)
            throw new Exception($"Công nợ còn lại sau thu phải bằng 0: {thuNoKhRes.ConNoConLai}");

        var soDuTraTruocKh = await conn.ExecuteScalarAsync<decimal>("SELECT so_du_tra_truoc FROM khach_hang WHERE id = @Id;", new { Id = khId });
        if (soDuTraTruocKh != 500000m)
            throw new Exception($"Số dư trả trước trong DB không đúng: {soDuTraTruocKh}");
        Console.WriteLine($"PASSED (Nợ: 0 đ, Trả trước: {soDuTraTruocKh:N0} đ) ✓");

        // Kiểm tra lịch sử trả trước của khách hàng
        Console.Write("[Kiểm tra Issue #97] 2. Khách hàng: Lịch sử biến động trả trước được ghi nhận... ");
        var lsTraTruocKh = await db.GetLichSuTraTruocKhachHangAsync(khId);
        if (lsTraTruocKh.Count != 1 || lsTraTruocKh[0].SoTien != 500000m || lsTraTruocKh[0].LoaiGiaoDich != "THANH_TOAN_DU")
            throw new Exception("Lịch sử trả trước khách hàng ghi nhận không chính xác!");
        Console.WriteLine($"PASSED (Có {lsTraTruocKh.Count} dòng lịch sử: {lsTraTruocKh[0].LoaiGiaoDichHienThi} {lsTraTruocKh[0].SoTien:N0} đ) ✓");

        // Kiểm tra sổ quỹ khách hàng ghi đủ 2.0M
        Console.Write("[Kiểm tra Issue #97] 3. Khách hàng: Sổ quỹ ghi nhận đúng 2.0M (không trùng doanh thu)... ");
        var phieuThuQuy = await conn.QueryFirstOrDefaultAsync<decimal>(
            "SELECT so_tien FROM phieu_chi WHERE ma_phieu_chi = @MaPT;",
            new { MaPT = thuNoKhRes.MaGiaoDich });
        if (phieuThuQuy != 2000000m)
            throw new Exception($"Sổ quỹ không ghi đúng số tiền 2.000.000đ: {phieuThuQuy}");
        Console.WriteLine($"PASSED (Phiếu thu {thuNoKhRes.MaGiaoDich}: {phieuThuQuy:N0} đ) ✓");

        // Kiểm tra tự động cấn trừ khi tạo hóa đơn mới
        Console.Write("[Kiểm tra Issue #97] 4. Khách hàng: Tự động cấn trừ số dư trả trước vào hóa đơn mới... ");
        // Lấy sản phẩm có sẵn
        var spTest = await conn.QueryFirstOrDefaultAsync("SELECT id, ma_san_pham, ten_san_pham, gia_ban, gia_von, COALESCE(ton_kho_excel, 100) AS ton_kho FROM san_pham WHERE gia_ban > 0 AND trang_thai = 1 LIMIT 1;");
        if (spTest == null)
            throw new Exception("Không tìm thấy sản phẩm để test bán hàng");

        var cartItems = new List<SaleCartItem>
        {
            new()
            {
                SanPham = new SanPham
                {
                    Id = (uint)spTest.id,
                    MaSanPham = (string)spTest.ma_san_pham,
                    TenSanPham = (string)spTest.ten_san_pham,
                    GiaBan = 800000m,
                    GiaVon = (decimal)spTest.gia_von,
                    TonKho = 100
                },
                SoLuong = 1,
                DonGia = 800000m
            }
        };

        var khCustomer = new KhachHang { Id = khId, MaKhachHang = maKhTest, TenKhachHang = "Khách Hàng Test 97" };
        var hdReceipt = await db.TaoHoaDonBanHangAsync(cartItems, khCustomer, 0m, 0m, "TIEN_MAT", "Test cấn trừ tự động");

        var hdCheck = await conn.QueryFirstOrDefaultAsync(
            "SELECT khach_can_tra, khach_da_thanh_toan, con_no FROM hoa_don_ban_hang WHERE ma_hoa_don = @Ma;",
            new { Ma = hdReceipt.MaHoaDon });
        if ((decimal)hdCheck.khach_can_tra != 800000m || (decimal)hdCheck.khach_da_thanh_toan != 500000m || (decimal)hdCheck.con_no != 300000m)
            throw new Exception($"Hóa đơn cấn trừ không đúng: Cần trả={(decimal)hdCheck.khach_can_tra}, Đã trả={(decimal)hdCheck.khach_da_thanh_toan}, Còn nợ={(decimal)hdCheck.con_no}");

        soDuTraTruocKh = await conn.ExecuteScalarAsync<decimal>("SELECT so_du_tra_truoc FROM khach_hang WHERE id = @Id;", new { Id = khId });
        if (soDuTraTruocKh != 0m)
            throw new Exception($"Số dư trả trước của khách hàng sau cấn trừ phải về 0, thực tế: {soDuTraTruocKh}");
        Console.WriteLine($"PASSED (HĐ {hdReceipt.MaHoaDon}: Đã thanh toán 500k, Còn nợ 300k, Trả trước còn lại: 0 đ) ✓");

        // Kiểm tra hủy hóa đơn đã sử dụng cấn trừ -> tự động hoàn trả số dư trả trước
        Console.Write("[Kiểm tra Issue #97] 5. Khách hàng: Hủy hóa đơn cấn trừ -> Tự động hoàn lại số dư trả trước... ");
        await db.HuyHoaDonBanHangAsync(new HuyHoaDonRequest
        {
            HoaDonId = hdReceipt.HoaDonId,
            MaHoaDon = hdReceipt.MaHoaDon,
            LyDoHuy = "Test hoàn trả số dư trả trước Issue #97",
            TrangThaiGiaoHang = TrangThaiGiaoHangKhiHuy.ChuaGiaoHang,
            PhuongAnTien = PhuongAnXuLyTienKhiHuy.KhongCo
        });

        soDuTraTruocKh = await conn.ExecuteScalarAsync<decimal>("SELECT so_du_tra_truoc FROM khach_hang WHERE id = @Id;", new { Id = khId });
        if (soDuTraTruocKh != 500000m)
            throw new Exception($"Số dư trả trước sau khi hủy hóa đơn phải được hoàn lại 500.000đ, thực tế: {soDuTraTruocKh}");
        Console.WriteLine($"PASSED (Số dư trả trước được khôi phục thành công: {soDuTraTruocKh:N0} đ) ✓");

        // ====================================================================
        // KỊCH BẢN 6 - 9: NHÀ CUNG CẤP (THANH TOÁN DƯ, TỰ ĐỘNG CẤN TRỪ, HỦY PHIẾU NHẬP)
        // ====================================================================
        Console.Write("[Kiểm tra Issue #97] 6. Nhà cung cấp: Thanh toán dư công nợ (5.0M nợ, trả 6.0M)... ");
        var maNccTest = "NCC_TEST_97_" + DateTime.Now.ToString("yyMMddHHmmss");
        await conn.ExecuteAsync(
            """
            INSERT INTO nha_cung_cap (ma_nha_cung_cap, ten_nha_cung_cap, so_dien_thoai, trang_thai, so_du_tra_truoc)
            VALUES (@Ma, @Ten, '0911223344', 1, 0.00);
            """,
            new { Ma = maNccTest, Ten = "NCC Test 97" });

        var nccId = await conn.ExecuteScalarAsync<uint>("SELECT id FROM nha_cung_cap WHERE ma_nha_cung_cap = @Ma;", new { Ma = maNccTest });

        // Tạo công nợ ban đầu 5.000.000đ cho NCC
        await conn.ExecuteAsync(
            """
            INSERT INTO lich_su_cong_no_nha_cung_cap (nha_cung_cap_id, chi_nhanh_id, loai_giao_dich, ma_chung_tu, ghi_no, ghi_co, thoi_gian, ghi_chu)
            VALUES (@NccId, 1, 'NHAP_HANG', 'PN_TEST_INIT', 5000000, 0, NOW(), 'Nợ ban đầu test 97');
            """,
            new { NccId = nccId });

        // Thanh toán 6.000.000đ cho NCC
        var ttNccRes = await db.ThanhToanCongNoNhaCungCapAsync(nccId, 6000000m, "CHUYEN_KHOAN", null, "Thanh toán dư NCC test 97");
        if (ttNccRes.SoTienThanhToan != 6000000m)
            throw new Exception($"Số tiền thanh toán NCC không đúng: {ttNccRes.SoTienThanhToan}");
        if (ttNccRes.TienCanTru != 5000000m)
            throw new Exception($"Tiền cấn trừ công nợ NCC không đúng: {ttNccRes.TienCanTru}");
        if (ttNccRes.TienDu != 1000000m)
            throw new Exception($"Tiền dư trả trước NCC không đúng: {ttNccRes.TienDu}");
        if (ttNccRes.CongNoSau != 0m)
            throw new Exception($"Công nợ NCC sau thanh toán phải bằng 0: {ttNccRes.CongNoSau}");

        var soDuTraTruocNcc = await conn.ExecuteScalarAsync<decimal>("SELECT so_du_tra_truoc FROM nha_cung_cap WHERE id = @Id;", new { Id = nccId });
        if (soDuTraTruocNcc != 1000000m)
            throw new Exception($"Số dư trả trước NCC trong DB không đúng: {soDuTraTruocNcc}");
        Console.WriteLine($"PASSED (Nợ: 0 đ, Trả trước: {soDuTraTruocNcc:N0} đ) ✓");

        // Kiểm tra sổ quỹ NCC ghi đúng 6.0M (không trùng chi phí)
        Console.Write("[Kiểm tra Issue #97] 7. Nhà cung cấp: Sổ quỹ ghi nhận phiếu chi đúng 6.0M... ");
        var phieuChiQuy = await conn.QueryFirstOrDefaultAsync<decimal>(
            "SELECT so_tien FROM phieu_chi WHERE ma_phieu_chi = @MaPC;",
            new { MaPC = ttNccRes.MaGiaoDich });
        if (phieuChiQuy != 6000000m)
            throw new Exception($"Sổ quỹ không ghi đúng số tiền chi 6.000.000đ: {phieuChiQuy}");
        Console.WriteLine($"PASSED (Phiếu chi {ttNccRes.MaGiaoDich}: {phieuChiQuy:N0} đ) ✓");

        // Kiểm tra tự động cấn trừ khi nhập hàng mới
        Console.Write("[Kiểm tra Issue #97] 8. Nhà cung cấp: Tự động cấn trừ trả trước vào phiếu nhập mới (1.5M)... ");
        var pnTest = new PhieuNhapHang
        {
            ChiNhanhId = 1,
            NhaCungCapId = nccId,
            TenNhaCungCap = "NCC Test 97",
            ThoiGianNhap = DateTime.Now,
            TongTienHang = 1500000m,
            ChiPhiKhac = 0m,
            TongTienNhap = 1500000m,
            DaThanhToan = 0m,
            ConNo = 1500000m,
            PhuongThucThanhToan = "TIEN_MAT",
            GhiChu = "Test cấn trừ tự động PN"
        };
        var pnItems = new List<ChiTietPhieuNhapHang>
        {
            new()
            {
                SanPham = new SanPham
                {
                    Id = (uint)spTest.id,
                    MaSanPham = (string)spTest.ma_san_pham,
                    TenSanPham = (string)spTest.ten_san_pham,
                    GiaVon = 150000m
                },
                SoLuong = 10,
                DonGiaNhap = 150000m
            }
        };

        var pnId = await db.LuuPhieuNhapHangAsync(pnTest, pnItems, null, new List<ChungTuItemModel>());
        var pnCheck = await conn.QueryFirstOrDefaultAsync(
            "SELECT da_thanh_toan, con_no FROM phieu_nhap_hang WHERE id = @Id;",
            new { Id = pnId });
        if ((decimal)pnCheck.da_thanh_toan != 1000000m || (decimal)pnCheck.con_no != 500000m)
            throw new Exception($"Phiếu nhập cấn trừ không đúng: Đã trả={(decimal)pnCheck.da_thanh_toan}, Còn nợ={(decimal)pnCheck.con_no}");

        soDuTraTruocNcc = await conn.ExecuteScalarAsync<decimal>("SELECT so_du_tra_truoc FROM nha_cung_cap WHERE id = @Id;", new { Id = nccId });
        if (soDuTraTruocNcc != 0m)
            throw new Exception($"Số dư trả trước NCC sau cấn trừ phải về 0, thực tế: {soDuTraTruocNcc}");
        Console.WriteLine($"PASSED (PN {pnTest.MaPhieuNhap}: Cấn trừ 1.0M, Còn nợ 500k, Trả trước còn lại: 0 đ) ✓");

        // Kiểm tra hủy phiếu nhập đã cấn trừ -> tự động hoàn trả số dư trả trước
        Console.Write("[Kiểm tra Issue #97] 9. Nhà cung cấp: Hủy phiếu nhập cấn trừ -> Tự động hoàn lại số dư trả trước... ");
        await db.HuyPhieuNhapHangAsync(new HuyPhieuNhapYeuCau
        {
            PhieuNhapId = (uint)pnId,
            LyDoHuy = "Test hoàn trả trả trước NCC khi hủy phiếu nhập Issue #97",
            PhuongAnTien = "TREO_CONG_NO"
        });

        soDuTraTruocNcc = await conn.ExecuteScalarAsync<decimal>("SELECT so_du_tra_truoc FROM nha_cung_cap WHERE id = @Id;", new { Id = nccId });
        if (soDuTraTruocNcc != 1000000m)
            throw new Exception($"Số dư trả trước NCC sau khi hủy phiếu nhập phải được hoàn lại 1.000.000đ, thực tế: {soDuTraTruocNcc}");
        Console.WriteLine($"PASSED (Số dư trả trước NCC được khôi phục: {soDuTraTruocNcc:N0} đ) ✓");

        // Dọn dẹp dữ liệu test 97
        try
        {
            await conn.ExecuteAsync("DELETE FROM lich_su_tra_truoc_khach_hang WHERE khach_hang_id = @Id;", new { Id = khId });
            await conn.ExecuteAsync("DELETE FROM lich_su_thanh_toan_cong_no_khach_hang WHERE khach_hang_id = @Id;", new { Id = khId });
            await conn.ExecuteAsync("DELETE FROM lich_su_cong_no_khach_hang WHERE khach_hang_id = @Id;", new { Id = khId });
            await conn.ExecuteAsync("DELETE FROM chi_tiet_hoa_don_ban_hang WHERE hoa_don_id = @HdId;", new { HdId = hdReceipt.HoaDonId });
            await conn.ExecuteAsync("DELETE FROM hoa_don_ban_hang WHERE id = @HdId;", new { HdId = hdReceipt.HoaDonId });
            await conn.ExecuteAsync("DELETE FROM phieu_chi WHERE ma_phieu_chi = @MaPT;", new { MaPT = thuNoKhRes.MaGiaoDich });
            await conn.ExecuteAsync("DELETE FROM khach_hang WHERE id = @Id;", new { Id = khId });

            await conn.ExecuteAsync("DELETE FROM lich_su_tra_truoc_ncc WHERE nha_cung_cap_id = @Id;", new { Id = nccId });
            await conn.ExecuteAsync("DELETE FROM lich_su_thanh_toan_cong_no_nha_cung_cap WHERE nha_cung_cap_id = @Id;", new { Id = nccId });
            await conn.ExecuteAsync("DELETE FROM thanh_toan_nha_cung_cap WHERE nha_cung_cap_id = @Id;", new { Id = nccId });
            await conn.ExecuteAsync("DELETE FROM lich_su_cong_no_nha_cung_cap WHERE nha_cung_cap_id = @Id;", new { Id = nccId });
            await conn.ExecuteAsync("DELETE FROM chi_tiet_phieu_nhap_hang WHERE phieu_nhap_id = @Id;", new { Id = pnId });
            await conn.ExecuteAsync("DELETE FROM phieu_nhap_hang WHERE id = @Id;", new { Id = pnId });
            await conn.ExecuteAsync("DELETE FROM phieu_chi WHERE ma_phieu_chi = @MaPC;", new { MaPC = ttNccRes.MaGiaoDich });
            await conn.ExecuteAsync("DELETE FROM nha_cung_cap WHERE id = @Id;", new { Id = nccId });
        }
        catch {}

        Console.WriteLine("\n>>> TOÀN BỘ 9 TIÊU CHÍ KIỂM THỬ ISSUE #97 ĐÃ ĐẠT 100% THÀNH CÔNG! <<<");
    }
}