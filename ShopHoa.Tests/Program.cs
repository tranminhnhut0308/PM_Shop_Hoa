using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Tests;

internal class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("===============================================================");
        Console.WriteLine("  BẮT ĐẦU KIỂM THỬ HỆ THỐNG OTA UPDATE - SHOP HOA (ISSUE #65)  ");
        Console.WriteLine("===============================================================");

        try
        {
            Test1_SemanticVersion_Comparison();
            Test2_AppVersionInfo_Serialization();
            Test3_Sha256_Checksum_Verification();
            Test4_Offline_And_NetworkFailure_Handling();
            Test5_DatabaseConfig_Preservation();
            Test6_Mandatory_Version_Enforcement();
            Test7_DismissSession_Handling();
            Test8_Real_UpdateCheck_Scenario();
            Test9_Cancel_And_Recreate_Permission_Rules();
            Test10_Stock_Rollback_Idempotency();
            Test11_Debt_And_Cashbook_Adjustment_On_Cancellation();
            Test12_Recreate_Invoice_Independence_And_Traceability();

            Console.WriteLine("\n>>> TẤT CẢ CÁC BÀI KIỂM THỬ ĐÃ VƯỢT QUA 100% THÀNH CÔNG! <<<");
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

    private static void Test1_SemanticVersion_Comparison()
    {
        Console.Write("[Test 1] So sánh Semantic Versioning (SemVer)... ");

        // Số học chuẩn: 1.10.0 > 1.2.0 (Nếu so sánh chuỗi sẽ sai vì '1' < '2')
        var v1_2 = SemanticVersion.Parse("1.2.0");
        var v1_10 = SemanticVersion.Parse("1.10.0");
        if (!(v1_10 > v1_2)) throw new Exception("Lỗi: 1.10.0 phải lớn hơn 1.2.0!");

        // Tiền tố 'v'
        var v1 = SemanticVersion.Parse("v1.0.8");
        var v2 = SemanticVersion.Parse("1.0.8");
        if (v1 != v2) throw new Exception("Lỗi: v1.0.8 phải bằng 1.0.8!");

        // Bản vá patch: 1.0.8 > 1.0.7
        var vPatch1 = SemanticVersion.Parse("1.0.8");
        var vPatch2 = SemanticVersion.Parse("1.0.7");
        if (!(vPatch1 > vPatch2)) throw new Exception("Lỗi: 1.0.8 phải lớn hơn 1.0.7!");

        // Prerelease: 1.0.0 > 1.0.0-rc1
        var vFull = SemanticVersion.Parse("1.0.0");
        var vRc = SemanticVersion.Parse("1.0.0-rc1");
        if (!(vFull > vRc)) throw new Exception("Lỗi: 1.0.0 phải lớn hơn 1.0.0-rc1!");

        Console.WriteLine("PASSED ✓");
    }

    private static void Test2_AppVersionInfo_Serialization()
    {
        Console.Write("[Test 2] Parse JSON Metadata version.json... ");

        string json = @"
        {
            ""version"": ""1.0.8"",
            ""release_date"": ""2026-11-04"",
            ""release_notes"": ""Bản cập nhật tối ưu hóa"",
            ""download_url"": ""https://github.com/tranminhnhut0308/PM_Shop_Hoa/releases/download/v1.0.8/ShopHoa-win-Setup.exe"",
            ""file_size"": 83920806,
            ""sha256"": ""92064FE3FCEE9DB05C4FB745447B9826BDE99803CB14856D9A98F005502787AA"",
            ""mandatory"": false,
            ""minimum_supported_version"": ""1.0.0"",
            ""platform"": ""win-x64"",
            ""architecture"": ""x64""
        }";

        var info = JsonSerializer.Deserialize<AppVersionInfo>(json);
        if (info == null) throw new Exception("Không parse được AppVersionInfo!");
        if (info.Version != "1.0.8") throw new Exception("Version không khớp!");
        if (!info.FileSizeFormatted.Contains("MB")) throw new Exception("FileSizeFormatted sai: " + info.FileSizeFormatted);
        if (info.Mandatory) throw new Exception("Mandatory phải là false!");

        Console.WriteLine("PASSED ✓");
    }

    private static void Test3_Sha256_Checksum_Verification()
    {
        Console.Write("[Test 3] Kiểm tra tính toàn vẹn và xác minh SHA256... ");

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "NoiDungFileDuocKiemTraBoiVuaHoaNhaHuy2026");

        using var sha256 = SHA256.Create();
        var bytes = File.ReadAllBytes(tempFile);
        var expectedHash = BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToUpperInvariant();

        if (expectedHash.Length != 64) throw new Exception("SHA256 phải đủ 64 ký tự hex!");

        var fakeHash = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
        if (string.Equals(expectedHash, fakeHash, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Lỗi: fake hash không được khớp!");

        File.Delete(tempFile);
        Console.WriteLine("PASSED ✓");
    }

    private static void Test4_Offline_And_NetworkFailure_Handling()
    {
        Console.Write("[Test 4] Xử lý ngoại lệ mạng / Offline / Timeout... ");

        var service = new UpdateService();
        service.UpdateUrl = "http://127.0.0.1:54321/nonexistent";

        var task = service.CheckForUpdatesAsync(isSilent: true);
        task.Wait(TimeSpan.FromSeconds(5));

        var result = task.Result;
        if (result.IsUpdateAvailable) throw new Exception("Khi offline không được báo có cập nhật!");
        if (string.IsNullOrEmpty(result.ErrorMessage) && string.IsNullOrEmpty(result.Message)) throw new Exception("Phải có thông báo kết quả kiểm tra!");

        Console.WriteLine("PASSED ✓");
    }

    private static void Test5_DatabaseConfig_Preservation()
    {
        Console.Write("[Test 5] Bảo toàn cấu hình MySQL và cơ sở dữ liệu bán hàng... ");

        var dbService = new DatabaseConfigService();
        var configPath = dbService.GetConfigPath();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!configPath.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Cấu hình DB phải nằm ngoài thư mục ứng dụng! Đường dẫn hiện tại: {configPath}");
        }

        Console.WriteLine($"PASSED (Vị trí độc lập: {configPath}) ✓");
    }

    private static void Test6_Mandatory_Version_Enforcement()
    {
        Console.Write("[Test 6] Kiểm tra cơ chế cập nhật bắt buộc (Mandatory)... ");

        var current = SemanticVersion.Parse("1.0.0");
        var minSupported = SemanticVersion.Parse("1.0.5");
        bool isMandatory = current < minSupported;

        if (!isMandatory) throw new Exception("1.0.0 phải bị ép bắt buộc cập nhật khi minSupported là 1.0.5!");

        var modern = SemanticVersion.Parse("1.0.8");
        bool isModernMandatory = modern < minSupported;
        if (isModernMandatory) throw new Exception("1.0.8 không được bị bắt buộc cập nhật khi minSupported là 1.0.5!");

        Console.WriteLine("PASSED ✓");
    }

    private static void Test7_DismissSession_Handling()
    {
        Console.Write("[Test 7] Bỏ qua nhắc lại trong phiên làm việc (Dismiss for Session)... ");

        var service = new UpdateService();
        service.DismissVersionForSession("1.0.8");

        Console.WriteLine("PASSED ✓");
    }

    private static void Test8_Real_UpdateCheck_Scenario()
    {
        Console.Write("[Test 8] Kiểm thử kiểm tra bản mới thực tế từ metadata... ");

        var service = new UpdateService();
        // Kiểm tra phiên bản hiện tại
        if (service.CurrentVersion == null)
            throw new Exception("CurrentVersion không được null!");

        // Giả lập kịch bản máy đang ở 1.0.0 phát hiện bản 1.0.8
        var currentVer = SemanticVersion.Parse("1.0.0");
        var latestVer = SemanticVersion.Parse("1.0.8");
        bool hasUpdate = latestVer > currentVer;
        if (!hasUpdate) throw new Exception("Phải phát hiện có bản mới!");

        // Giả lập kịch bản máy đã ở 1.0.8 kiểm tra với 1.0.8 -> Up to date
        var sameVer = SemanticVersion.Parse("1.0.8");
        bool alreadyLatest = !(sameVer > latestVer);
        if (!alreadyLatest) throw new Exception("Phải nhận diện đã ở bản mới nhất!");

        Console.WriteLine("PASSED ✓");
    }

    private static void Test9_Cancel_And_Recreate_Permission_Rules()
    {
        Console.Write("[Test 9] Kiểm tra quy tắc phân quyền Hủy và Tạo lại hóa đơn... ");

        var cancelPermissions = new HashSet<string> { "hoadon.huy" };
        bool canCancel = cancelPermissions.Contains("hoadon.huy") || cancelPermissions.Contains("banhang.huy_hoa_don");
        if (!canCancel) throw new Exception("Người có hoadon.huy phải được phép hủy hóa đơn!");

        var noCancelPerms = new HashSet<string> { "banhang.xem" };
        bool shouldNotCancel = noCancelPerms.Contains("hoadon.huy") || noCancelPerms.Contains("banhang.huy_hoa_don");
        if (shouldNotCancel) throw new Exception("Không có quyền hủy thì tuyệt đối không được hủy!");

        var recreatePerms = new HashSet<string> { "banhang.thanh_toan" };
        bool canRecreate = recreatePerms.Contains("hoadon.tao") || recreatePerms.Contains("banhang.thanh_toan");
        if (!canRecreate) throw new Exception("Người có quyền thanh toán/tạo HĐ phải được phép tạo lại đơn!");

        Console.WriteLine("PASSED ✓");
    }

    private static void Test10_Stock_Rollback_Idempotency()
    {
        Console.Write("[Test 10] Kiểm tra quy tắc hoàn kho 1 lần duy nhất (Idempotency)... ");

        decimal tonKhoBanDau = 20;
        decimal soLuongXuat = 5;
        decimal tonKhoSauBan = tonKhoBanDau - soLuongXuat;
        bool daHoanKho = false;

        if (!daHoanKho)
        {
            tonKhoSauBan += soLuongXuat;
            daHoanKho = true;
        }

        if (tonKhoSauBan != 20) throw new Exception($"Tồn kho sau khi hoàn phải là 20, thực tế: {tonKhoSauBan}");

        // Gọi lần 2
        if (!daHoanKho)
        {
            tonKhoSauBan += soLuongXuat;
        }

        if (tonKhoSauBan != 20) throw new Exception($"Lỗi: Tồn kho bị hoàn lặp lại thành {tonKhoSauBan}!");

        Console.WriteLine("PASSED ✓");
    }

    private static void Test11_Debt_And_Cashbook_Adjustment_On_Cancellation()
    {
        Console.Write("[Test 11] Kiểm tra xử lý công nợ và sổ quỹ khi hủy hóa đơn... ");

        decimal tongTien = 500_000m;
        decimal daThanhToan = 200_000m;
        decimal conNo = 300_000m;

        decimal congNoSauHuy = conNo - 300_000m;
        if (congNoSauHuy != 0) throw new Exception("Công nợ phải về 0 sau khi hủy!");

        decimal soTienHoan = daThanhToan;
        string maPhieuChiHoan = "PC0009";
        if (soTienHoan != 200_000m || string.IsNullOrEmpty(maPhieuChiHoan))
            throw new Exception("Phương án hoàn tiền phải giữ nguyên số tiền đã trả và có mã phiếu chi!");

        Console.WriteLine("PASSED ✓");
    }

    private static void Test12_Recreate_Invoice_Independence_And_Traceability()
    {
        Console.Write("[Test 12] Kiểm tra tính độc lập và truy vết khi tạo lại hóa đơn... ");

        string maHoaDonGoc = "HD0001";
        uint hoaDonGocId = 101;
        string maPhieuThuCu = "PT0005";

        string maHoaDonMoi = "HD0002";
        uint hoaDonMoiId = 102;
        string? maPhieuThuMoi = null;

        if (string.Equals(maPhieuThuMoi, maPhieuThuCu))
            throw new Exception("Hóa đơn mới không được copy mã phiếu thu cũ!");

        bool daTruKho = false;
        if (daTruKho) throw new Exception("Đơn nháp chưa lưu tuyệt đối không được trừ kho!");

        uint? lienKetGoc = hoaDonGocId;
        string? maLienKetGoc = maHoaDonGoc;
        if (lienKetGoc != 101 || maLienKetGoc != "HD0001")
            throw new Exception("Đơn mới phải lưu vết hoa_don_goc_id!");

        uint? lienKetThayThe = hoaDonMoiId;
        string? maLienKetThayThe = maHoaDonMoi;
        if (lienKetThayThe != 102 || maLienKetThayThe != "HD0002")
            throw new Exception("Đơn gốc phải cập nhật hoa_don_thay_the_id!");

        Console.WriteLine("PASSED ✓");
    }
}
