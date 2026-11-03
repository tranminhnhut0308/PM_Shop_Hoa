using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Quản lý lưu trữ tập tin chứng từ đính kèm của phiếu nhập hàng.
/// Đảm bảo xác thực file signature thực tế (magic bytes), giới hạn dung lượng,
/// sinh tên ngẫu nhiên tránh đè và phân cấp theo thư mục năm/tháng.
/// </summary>
public static class ChungTuStorageService
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    public static string GetBaseStoragePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "ShopHoa", "Attachments", "PhieuNhap");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetTempStoragePath()
    {
        var dir = Path.Combine(GetBaseStoragePath(), "_temp");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    public static bool KiemTraDinhDangVaDungLuong(
        string filePath,
        out string format,
        out string? errorMessage)
    {
        format = "";
        errorMessage = null;

        if (!File.Exists(filePath))
        {
            errorMessage = "Tập tin không tồn tại.";
            return false;
        }

        var ext = Path.GetExtension(filePath);
        if (!AllowedExtensions.Contains(ext))
        {
            errorMessage = $"Định dạng tập tin '{ext}' không được hỗ trợ. Chỉ chấp nhận JPG, JPEG, PNG, PDF.";
            return false;
        }

        var fi = new FileInfo(filePath);
        if (fi.Length > MaxFileSizeBytes)
        {
            errorMessage = $"Dung lượng file ({fi.Length / (1024.0 * 1024.0):F2} MB) vượt quá giới hạn tối đa cho phép 10MB.";
            return false;
        }

        // Kiểm tra Magic Bytes / Signature thực tế
        try
        {
            using var fs = File.OpenRead(filePath);
            var header = new byte[8];
            var read = fs.Read(header, 0, header.Length);
            if (read < 4)
            {
                errorMessage = "Tập tin rỗng hoặc bị lỗi.";
                return false;
            }

            // PDF signature: 25 50 44 46 (%PDF)
            if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
            {
                format = "PDF";
                return true;
            }

            // JPEG signature: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                format = "JPG";
                return true;
            }

            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                format = "PNG";
                return true;
            }

            errorMessage = "Nội dung file không đúng với định dạng khai báo (magic bytes không hợp lệ).";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = "Lỗi khi kiểm tra tập tin: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Sao chép file nguồn vào thư mục tạm của ứng dụng để phục vụ xem trước và chỉnh sửa an toàn.
    /// </summary>
    public static async Task<ChungTuItemModel> LuuFileVaoThuMucTamAsync(
        string sourceFilePath,
        string loaiChungTu,
        string nguoiTaiLen,
        string? ghiChu = null)
    {
        if (!KiemTraDinhDangVaDungLuong(sourceFilePath, out var format, out var error))
        {
            throw new InvalidOperationException(error ?? "File không hợp lệ.");
        }

        var fileName = Path.GetFileName(sourceFilePath);
        var ext = Path.GetExtension(sourceFilePath);
        var tempFileName = $"temp_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
        var tempFilePath = Path.Combine(GetTempStoragePath(), tempFileName);

        await using (var sourceStream = File.OpenRead(sourceFilePath))
        await using (var targetStream = File.Create(tempFilePath))
        {
            await sourceStream.CopyToAsync(targetStream);
        }

        var fi = new FileInfo(tempFilePath);

        return new ChungTuItemModel
        {
            TempId = Guid.NewGuid().ToString("N"),
            TenFileGoc = fileName,
            DuongDanNguon = sourceFilePath,
            DuongDanTam = tempFilePath,
            DinhDang = format,
            DungLuong = fi.Length,
            LoaiChungTu = loaiChungTu,
            GhiChu = ghiChu,
            NguoiTaiLen = nguoiTaiLen,
            ThoiGianTaiLen = DateTime.Now
        };
    }

    /// <summary>
    /// Chuyển file từ thư mục tạm sang thư mục lưu trữ vĩnh viễn theo năm/tháng khi lưu phiếu nhập chính thức.
    /// Trả về đường dẫn tương đối (vd: 2026/11/PN000001_abc.jpg) và tên file lưu.
    /// </summary>
    public static async Task<(string relativePath, string savedFileName, long size)> ChuyenFileChinhThucAsync(
        string tempOrSourceFilePath,
        string maPhieuNhap,
        string tenFileGoc)
    {
        var ext = Path.GetExtension(tenFileGoc);
        if (string.IsNullOrWhiteSpace(ext))
            ext = Path.GetExtension(tempOrSourceFilePath);

        var subFolder = DateTime.Now.ToString("yyyy/MM");
        var physicalFolder = Path.Combine(GetBaseStoragePath(), subFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(physicalFolder))
            Directory.CreateDirectory(physicalFolder);

        var safeMaPhieu = string.IsNullOrWhiteSpace(maPhieuNhap) ? "PN" : maPhieuNhap.Trim();
        var uniqueFileName = $"{safeMaPhieu}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
        var destPhysicalPath = Path.Combine(physicalFolder, uniqueFileName);

        await using (var src = File.OpenRead(tempOrSourceFilePath))
        await using (var dst = File.Create(destPhysicalPath))
        {
            await src.CopyToAsync(dst);
        }

        var relativePath = $"{subFolder}/{uniqueFileName}";
        var size = new FileInfo(destPhysicalPath).Length;

        // Xóa file tạm nếu là file trong _temp
        try
        {
            if (tempOrSourceFilePath.Contains("_temp", StringComparison.OrdinalIgnoreCase) && File.Exists(tempOrSourceFilePath))
                File.Delete(tempOrSourceFilePath);
        }
        catch { }

        return (relativePath, uniqueFileName, size);
    }

    /// <summary>
    /// Lấy đường dẫn vật lý tuyệt đối từ đường dẫn tương đối đã lưu trong CSDL.
    /// </summary>
    public static string LayDuongDanTuyetDoi(string duongDanTuongDoi)
    {
        if (Path.IsPathRooted(duongDanTuongDoi) && File.Exists(duongDanTuongDoi))
            return duongDanTuongDoi;

        var normalized = duongDanTuongDoi.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        return Path.Combine(GetBaseStoragePath(), normalized);
    }
}
