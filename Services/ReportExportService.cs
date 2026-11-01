using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Views;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ xuất file Excel dùng chung cho toàn bộ các phân hệ báo cáo (Issue #47).
/// Thống nhất quy trình chọn thư mục, đặt tên file, kiểm tra quyền ghi an toàn,
/// ghi an toàn qua file tạm và hiển thị hộp thoại thành công kèm tuỳ chọn mở file/thư mục.
/// </summary>
public static class ReportExportService
{
    /// <summary>
    /// Tạo tên file chuẩn theo quy tắc: {LoaiBaoCao}_yyyyMMdd_HHmmss.xlsx
    /// </summary>
    public static string TaoTenFile(string loaiBaoCao, DateTime? thoiGian = null)
    {
        var dt = thoiGian ?? DateTime.Now;
        var tenSach = loaiBaoCao.Replace(" ", "");
        return $"{tenSach}_{dt:yyyyMMdd_HHmmss}.xlsx";
    }

    /// <summary>Lấy cửa sổ chính của ứng dụng.</summary>
    public static Window? LayCuaSoChinh(Window? owner = null)
    {
        if (owner is not null)
            return owner;

        return Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    /// <summary>
    /// Thực hiện toàn bộ quy trình xuất file Excel và thông báo kết quả.
    /// </summary>
    /// <param name="ownerWindow">Cửa sổ chủ để mở dialog (nếu null sẽ lấy MainWindow)</param>
    /// <param name="reportTitle">Tên tiếng Việt hiển thị của báo cáo (vd: Báo cáo bán hàng)</param>
    /// <param name="suggestedFileName">Tên file gợi ý (vd: BaoCaoBanHang_20261101_153000.xlsx)</param>
    /// <param name="generateWorkbook">Hàm khởi tạo và dựng nội dung XLWorkbook</param>
    /// <param name="recordCount">Số lượng bản ghi chính xuất ra (nếu có)</param>
    /// <param name="statusCallback">Callback cập nhật dòng trạng thái trên màn hình</param>
    /// <returns>Đường dẫn file đã xuất hoặc null nếu hủy/thất bại</returns>
    public static async Task<string?> ExportReportAsync(
        Window? ownerWindow,
        string reportTitle,
        string suggestedFileName,
        Func<XLWorkbook> generateWorkbook,
        int recordCount = 0,
        Action<string>? statusCallback = null)
    {
        ownerWindow = LayCuaSoChinh(ownerWindow);
        if (ownerWindow is null)
        {
            statusCallback?.Invoke("Không tìm thấy cửa sổ chính để xuất file.");
            return null;
        }

        // 1. Thư mục gợi ý: Documents
        IStorageFolder? thuMucBatDau = null;
        try
        {
            thuMucBatDau = await ownerWindow.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);
        }
        catch
        {
            // Bỏ qua nếu OS không hỗ trợ
        }

        // 2. Mở hộp thoại chọn vị trí lưu file (Windows Save File Dialog)
        var file = await ownerWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Xuất Excel {reportTitle}",
            SuggestedFileName = suggestedFileName,
            SuggestedStartLocation = thuMucBatDau,
            DefaultExtension = "xlsx",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("Excel Workbook (*.xlsx)")
                {
                    Patterns = ["*.xlsx"]
                }
            ]
        });

        if (file is null)
        {
            statusCallback?.Invoke("Bạn đã hủy xuất file.");
            return null;
        }

        var duongDanFile = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(duongDanFile))
        {
            statusCallback?.Invoke("Xuất file thất bại: Không xác định được đường dẫn lưu file.");
            var errDlg = new ConfirmationWindow("Xuất file thất bại", "Không xác định được đường dẫn lưu file trên ổ đĩa cục bộ.", "Đóng", "");
            await errDlg.ShowDialog<bool>(ownerWindow);
            return null;
        }

        // 3. Kiểm tra quyền ghi vào thư mục đích
        if (!XuatFileHelper.KiemTraThuMucGhiDuoc(duongDanFile, out var loiThuMuc))
        {
            statusCallback?.Invoke("Xuất file thất bại: " + loiThuMuc);
            var errDlg = new ConfirmationWindow("Xuất file thất bại", $"Không thể lưu file vào thư mục đã chọn.\n\nĐường dẫn:\n{duongDanFile}\n\nNguyên nhân:\n{loiThuMuc}", "Đóng", "");
            await errDlg.ShowDialog<bool>(ownerWindow);
            return null;
        }

        try
        {
            statusCallback?.Invoke($"Đang xuất {reportTitle}...");

            // 4. Tạo workbook và ghi an toàn ra file tạm
            var fileTam = XuatFileHelper.TaoDuongDanTam(duongDanFile);
            try
            {
                await Task.Run(() =>
                {
                    using var wb = generateWorkbook();
                    using var stream = new FileStream(fileTam, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    wb.SaveAs(stream);
                    stream.Flush(flushToDisk: true);
                });

                XuatFileHelper.GhiDeFile(fileTam, duongDanFile);
            }
            finally
            {
                XuatFileHelper.XoaFileTam(fileTam);
            }

            // 5. Kiểm tra tính toàn vẹn của file
            if (!File.Exists(duongDanFile) || new FileInfo(duongDanFile).Length == 0)
                throw new IOException("File chưa được tạo hoàn chỉnh trên đĩa.");

            statusCallback?.Invoke($"✅ Đã xuất {reportTitle} ra file {Path.GetFileName(duongDanFile)}.");

            // 6. Hiển thị hộp thoại thành công chuẩn hóa
            var dialog = new XuatFileThanhCongWindow(
                reportTitle,
                duongDanFile,
                DateTime.Now,
                recordCount);

            var hanhDong = await dialog.ShowDialog<XuatFileHanhDong>(ownerWindow);

            switch (hanhDong)
            {
                case XuatFileHanhDong.MoFile:
                    try
                    {
                        XuatFileHelper.MoFile(duongDanFile);
                    }
                    catch (Exception ex)
                    {
                        SafeLog.WriteError($"Lỗi mở file {duongDanFile}", ex);
                        var errDlg = new ConfirmationWindow(
                            "Không mở được file",
                            $"File đã xuất thành công nhưng không mở được bằng ứng dụng mặc định.\n\nĐường dẫn:\n{duongDanFile}\n\nChi tiết: {ex.Message}",
                            "Đóng", "");
                        await errDlg.ShowDialog<bool>(ownerWindow);
                    }
                    break;

                case XuatFileHanhDong.MoThuMuc:
                    try
                    {
                        XuatFileHelper.MoThuMucChuaFile(duongDanFile);
                    }
                    catch (Exception ex)
                    {
                        SafeLog.WriteError($"Lỗi mở thư mục {duongDanFile}", ex);
                        var errDlg = new ConfirmationWindow(
                            "Không mở được thư mục",
                            $"File đã xuất thành công nhưng không mở được thư mục chứa file.\n\nĐường dẫn:\n{duongDanFile}\n\nChi tiết: {ex.Message}",
                            "Đóng", "");
                        await errDlg.ShowDialog<bool>(ownerWindow);
                    }
                    break;
            }

            return duongDanFile;
        }
        catch (UnauthorizedAccessException ex)
        {
            SafeLog.WriteError($"Lỗi phân quyền xuất file {reportTitle}", ex);
            statusCallback?.Invoke("Xuất file thất bại: Không có quyền ghi file.");
            var errDlg = new ConfirmationWindow(
                "Xuất file thất bại",
                $"Tài khoản hiện tại không có quyền ghi file vào vị trí đã chọn.\n\nChi tiết: {ex.Message}",
                "Đóng", "");
            await errDlg.ShowDialog<bool>(ownerWindow);
            return null;
        }
        catch (IOException ex)
        {
            SafeLog.WriteError($"Lỗi IO khi xuất {reportTitle}", ex);
            statusCallback?.Invoke("Xuất file thất bại: File có thể đang được mở trong ứng dụng khác.");
            var errDlg = new ConfirmationWindow(
                "Xuất file thất bại",
                $"Không ghi được file. File có thể đang được mở bởi Excel hoặc một ứng dụng khác.\n\nHãy đóng file đang mở rồi thử xuất lại.\n\nChi tiết: {ex.Message}",
                "Đóng", "");
            await errDlg.ShowDialog<bool>(ownerWindow);
            return null;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError($"Lỗi xuất file {reportTitle}", ex);
            statusCallback?.Invoke($"Xuất file thất bại: {ex.Message}");
            var errDlg = new ConfirmationWindow(
                "Xuất file thất bại",
                $"Đã xảy ra lỗi trong quá trình xuất {reportTitle}:\n\n{ex.Message}",
                "Đóng", "");
            await errDlg.ShowDialog<bool>(ownerWindow);
            return null;
        }
    }
}
