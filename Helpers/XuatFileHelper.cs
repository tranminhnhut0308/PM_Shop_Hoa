using System.Diagnostics;

namespace ShopHoa.Helpers;

/// <summary>
/// Tiện ích dùng chung cho các luồng xuất file: kiểm tra quyền ghi, ghi file an
/// toàn qua file tạm và mở file/thư mục bằng shell của Windows.
/// </summary>
public static class XuatFileHelper
{
    /// <summary>
    /// Kiểm tra thư mục đích có tồn tại và tài khoản đang chạy có quyền ghi hay
    /// không. Không tạo/ghi đè file đích, chỉ thử tạo một file tạm rồi xóa đi.
    /// </summary>
    public static bool KiemTraThuMucGhiDuoc(string duongDanFile, out string? thongBaoLoi)
    {
        thongBaoLoi = null;

        try
        {
            var thuMuc = Path.GetDirectoryName(duongDanFile);
            if (string.IsNullOrWhiteSpace(thuMuc))
            {
                thongBaoLoi = "Đường dẫn lưu file không hợp lệ.";
                return false;
            }

            if (!Directory.Exists(thuMuc))
            {
                thongBaoLoi = $"Thư mục không tồn tại: {thuMuc}";
                return false;
            }

            if (File.Exists(duongDanFile)
                && File.GetAttributes(duongDanFile).HasFlag(FileAttributes.ReadOnly))
            {
                thongBaoLoi =
                    "File đã tồn tại và đang ở chế độ chỉ đọc. " +
                    "Hãy bỏ thuộc tính chỉ đọc hoặc chọn tên file khác.";
                return false;
            }

            var fileTam = Path.Combine(
                thuMuc,
                $".shophoa_kiemtra_quyen_ghi_{Guid.NewGuid():N}.tmp");

            try
            {
                using var stream = new FileStream(
                    fileTam,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);
                stream.WriteByte(0);
            }
            finally
            {
                TryDelete(fileTam);
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            thongBaoLoi =
                "Tài khoản chạy phần mềm không có quyền ghi vào thư mục đã chọn. " +
                "Chi tiết: " + ex.Message;
            return false;
        }
        catch (IOException ex)
        {
            thongBaoLoi = "Không truy cập được thư mục đã chọn. Chi tiết: " + ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            thongBaoLoi = "Không kiểm tra được quyền ghi file. Chi tiết: " + ex.Message;
            return false;
        }
    }

    /// <summary>Tạo đường dẫn file tạm cùng thư mục với file đích.</summary>
    public static string TaoDuongDanTam(string duongDanDich)
    {
        var thuMuc = Path.GetDirectoryName(duongDanDich) ?? Path.GetTempPath();
        var ten = Path.GetFileNameWithoutExtension(duongDanDich);
        return Path.Combine(thuMuc, $".{ten}_{Guid.NewGuid():N}.tmp");
    }

    /// <summary>Thay thế file đích bằng file tạm sau khi ghi thành công.</summary>
    public static void GhiDeFile(string fileTam, string duongDanDich)
        => File.Move(fileTam, duongDanDich, overwrite: true);

    /// <summary>Xóa file tạm, bỏ qua lỗi nếu file đã được chuyển đi.</summary>
    public static void XoaFileTam(string? duongDan)
    {
        if (string.IsNullOrWhiteSpace(duongDan))
            return;

        TryDelete(duongDan);
    }

    /// <summary>Mở file bằng ứng dụng mặc định của Windows.</summary>
    public static void MoFile(string duongDan)
    {
        if (!File.Exists(duongDan))
            throw new FileNotFoundException("File không tồn tại: " + duongDan, duongDan);

        // Kiểm tra nhanh quyền đọc/lock trước khi giao cho shell mở file.
        using (File.Open(duongDan, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
        }

        Process.Start(new ProcessStartInfo(duongDan)
        {
            UseShellExecute = true
        });
    }

    /// <summary>Mở thư mục chứa file và chọn sẵn file đó trong Explorer.</summary>
    public static void MoThuMucChuaFile(string duongDan)
    {
        if (!File.Exists(duongDan))
            throw new FileNotFoundException("File không tồn tại: " + duongDan, duongDan);

        var thuMuc = Path.GetDirectoryName(duongDan);
        if (string.IsNullOrWhiteSpace(thuMuc) || !Directory.Exists(thuMuc))
            throw new DirectoryNotFoundException(
                "Thư mục chứa file không tồn tại: " + thuMuc);

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                Arguments = $"/select,\"{duongDan}\"",
                UseShellExecute = true
            });
            return;
        }

        Process.Start(new ProcessStartInfo(thuMuc)
        {
            UseShellExecute = true
        });
    }

    private static void TryDelete(string duongDan)
    {
        try
        {
            if (File.Exists(duongDan))
                File.Delete(duongDan);
        }
        catch
        {
            // Không để lỗi dọn file tạm che mất kết quả xuất file chính.
        }
    }
}
