using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

/// <summary>
/// Phần xuất Excel cho báo cáo cuối ngày.
/// <para>
/// File gồm đúng 6 sheet: Tổng quan, Hóa đơn bán hàng, Sản phẩm bán chạy,
/// Thu chi, Công nợ khách hàng, Nhập hàng. Tên file là
/// <c>BaoCaoCuoiNgay_yyyyMMdd.xlsx</c> theo ngày đang xem trên màn hình.
/// </para>
/// <para>
/// Quy ước dùng chung với phần xuất nhà cung cấp: tiêu đề in đậm nền xanh, cột
/// tiền ghi bằng số thập phân thuần (người dùng cộng/lọc được), ngày ghi bằng
/// DateTime, cột tự co theo nội dung và khoá dòng tiêu đề.
/// </para>
/// </summary>
public sealed partial class BaoCaoCuoiNgayViewModel
{
    /// <summary>
    /// Xuất báo cáo của ngày đang xem ra file Excel do người dùng chọn.
    /// </summary>
    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        var cuaSo = LayCuaSoChinh();

        if (cuaSo is null)
            return;

        // Đặt tên file trước để hiện sẵn trong hộp thoại lưu.
        var ngay = (NgayChon?.DateTime ?? DateTime.Today).Date;
        var tenFileMacDinh = new BaoCaoCuoiNgayDuLieu { Ngay = ngay }.TenFileXuat();

        // Ưu tiên mở hộp thoại tại thư mục Tài liệu để tránh mặc định rơi vào
        // thư mục hệ thống (ví dụ C:\) nơi tài khoản thường không có quyền ghi.
        IStorageFolder? thuMucBatDau = null;
        try
        {
            thuMucBatDau = await cuaSo.StorageProvider.TryGetWellKnownFolderAsync(
                WellKnownFolder.Documents);
        }
        catch
        {
            // Không lấy được thư mục gợi ý thì để hệ điều hành tự chọn.
        }

        var file = await cuaSo.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Xuất báo cáo cuối ngày",

                SuggestedFileName = tenFileMacDinh,

                SuggestedStartLocation = thuMucBatDau,

                DefaultExtension = "xlsx",

                // Không tự động ghi đè file đang tồn tại.
                ShowOverwritePrompt = true,

                FileTypeChoices =
                [
                    new FilePickerFileType("Excel")
                    {
                        Patterns = ["*.xlsx"]
                    }
                ]
            });

        if (file is null)
        {
            CapNhatTrangThai?.Invoke("Bạn đã hủy xuất file.");
            return;
        }

        var duongDanFile = file.TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(duongDanFile))
        {
            CapNhatTrangThai?.Invoke("Xuất file thất bại: không xác định được đường dẫn lưu file.");
            await HienThiThongBaoAsync(
                "Xuất file thất bại",
                "Không xác định được đường dẫn lưu file. " +
                "Vui lòng chọn lại vị trí lưu trên ổ đĩa cục bộ.");
            return;
        }

        // Kiểm tra thư mục/quyền ghi trước khi tải và ghi dữ liệu.
        if (!XuatFileHelper.KiemTraThuMucGhiDuoc(duongDanFile, out var loiThuMuc))
        {
            CapNhatTrangThai?.Invoke("Xuất file thất bại: " + loiThuMuc);
            await HienThiThongBaoAsync(
                "Xuất file thất bại",
                "Không thể lưu file vào thư mục đã chọn.\n\n" +
                $"Đường dẫn:\n{duongDanFile}\n\n" +
                $"Nguyên nhân:\n{loiThuMuc}");
            return;
        }

        try
        {
            CapNhatTrangThai?.Invoke("Đang xuất báo cáo cuối ngày...");

            // Tải lại dữ liệu để file khớp chính xác với ngày đang chọn.
            await TaiDuLieuAsync();

            if (CoLoi)
            {
                await HienThiThongBaoAsync(
                    "Xuất file thất bại",
                    "Không tải được dữ liệu nên không thể xuất file.\n\n" +
                    "Chi tiết:\n" + ThongBaoLoi);

                CapNhatTrangThai?.Invoke("Không thể xuất file do lỗi tải dữ liệu.");
                return;
            }

            var duLieu = new BaoCaoCuoiNgayDuLieu
            {
                Ngay = ngay,
                NgayKeTiep = ngay.AddDays(1),
                TongKetBanHang = TongKetBanHang,
                SoQuyTongHop = SoQuyTongHop,
                NhapHang = NhapHang,
                KhachHangTongHop = KhachHangTongHop,
                CongNoTongHop = CongNoTongHop,
                DoanhThuTheoGio = [.. DoanhThuTheoGio],
                PhuongThucThanhToan = [.. PhuongThucThanhToan],
                TopSanPham = [.. TopSanPham],
                TopKhachHang = [.. TopKhachHang],
                CongNoPhatSinh = [.. CongNoPhatSinh],
                ThuChi = [.. ThuChi]
            };

            // Ghi ra file tạm cùng thư mục rồi mới thay thế file đích. Cách này
            // bảo đảm file cũ không bị hỏng nếu quá trình ghi gặp lỗi, đồng thời
            // mọi luồng ghi đều được đóng trước khi file được mở.
            var fileTam = XuatFileHelper.TaoDuongDanTam(duongDanFile);

            try
            {
                await Task.Run(() =>
                {
                    using var workbook = BaoCaoCuoiNgayExcel.TaoWorkbook(duLieu);

                    using var stream = new FileStream(
                        fileTam,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None);

                    workbook.SaveAs(stream);
                    stream.Flush(flushToDisk: true);
                });

                XuatFileHelper.GhiDeFile(fileTam, duongDanFile);
            }
            finally
            {
                XuatFileHelper.XoaFileTam(fileTam);
            }

            // Chỉ báo thành công khi file đích thực sự đã được tạo hoàn chỉnh.
            if (!File.Exists(duongDanFile) || new FileInfo(duongDanFile).Length == 0)
                throw new IOException("File chưa được tạo hoàn chỉnh.");

            CapNhatTrangThai?.Invoke(
                $"✅ Đã xuất báo cáo ngày {duLieu.NgayHienThi} ra file " +
                $"{Path.GetFileName(duongDanFile)}.");

            var hanhDong = await new XuatFileThanhCongWindow(
                $"Đã xuất báo cáo cuối ngày {duLieu.NgayHienThi}.",
                duongDanFile).ShowDialog<XuatFileHanhDong>(cuaSo);

            switch (hanhDong)
            {
                case XuatFileHanhDong.MoFile:
                    await MoFileDaXuatAsync(duongDanFile);
                    break;

                case XuatFileHanhDong.MoThuMuc:
                    await MoThuMucDaXuatAsync(duongDanFile);
                    break;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            SafeLog.WriteError("Loi xuat file bao cao cuoi ngay", ex);
            await BaoLoiXuatFileAsync(
                duongDanFile,
                "Tài khoản chạy phần mềm không có quyền ghi file tại vị trí đã chọn.\n" +
                "Chi tiết: " + ex.Message);
        }
        catch (IOException ex)
        {
            SafeLog.WriteError("Loi xuat file bao cao cuoi ngay", ex);
            await BaoLoiXuatFileAsync(
                duongDanFile,
                "Không ghi được file. File có thể đang được mở bởi Excel hoặc " +
                "bị một tiến trình khác giữ lại.\n" +
                "Hãy đóng file đang mở rồi thử xuất lại.\n\n" +
                "Chi tiết: " + ex.Message);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi xuat file bao cao cuoi ngay", ex);
            await BaoLoiXuatFileAsync(
                duongDanFile,
                "Không thể xuất báo cáo cuối ngày.\n" +
                "Chi tiết: " + ex.Message);
        }
    }

    private async Task HienThiThongBaoAsync(string tieuDe, string noiDung)
    {
        if (HienThiHopNhat is not null)
            await HienThiHopNhat(tieuDe, noiDung);
    }

    private async Task BaoLoiXuatFileAsync(string duongDanFile, string chiTiet)
    {
        CapNhatTrangThai?.Invoke("Xuất file thất bại: " + chiTiet);

        await HienThiThongBaoAsync(
            "Xuất file thất bại",
            "Báo cáo chưa được lưu thành công.\n\n" +
            $"Đường dẫn đã chọn:\n{duongDanFile}\n\n" +
            chiTiet);
    }

    private async Task MoFileDaXuatAsync(string duongDanFile)
    {
        try
        {
            XuatFileHelper.MoFile(duongDanFile);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi mo file bao cao cuoi ngay", ex);
            CapNhatTrangThai?.Invoke("Không mở được file: " + ex.Message);

            await HienThiThongBaoAsync(
                "Không mở được file",
                "File đã xuất thành công nhưng không mở được bằng ứng dụng mặc định.\n\n" +
                $"Đường dẫn file:\n{duongDanFile}\n\n" +
                "Bạn có thể mở thủ công đường dẫn trên bằng File Explorer.\n\n" +
                $"Chi tiết lỗi:\n{ex.Message}");
        }
    }

    private async Task MoThuMucDaXuatAsync(string duongDanFile)
    {
        try
        {
            XuatFileHelper.MoThuMucChuaFile(duongDanFile);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi mo thu muc file bao cao cuoi ngay", ex);
            CapNhatTrangThai?.Invoke("Không mở được thư mục chứa file: " + ex.Message);

            await HienThiThongBaoAsync(
                "Không mở được thư mục",
                "File đã xuất thành công nhưng không mở được thư mục chứa file.\n\n" +
                $"Đường dẫn file:\n{duongDanFile}\n\n" +
                $"Chi tiết lỗi:\n{ex.Message}");
        }
    }
}
