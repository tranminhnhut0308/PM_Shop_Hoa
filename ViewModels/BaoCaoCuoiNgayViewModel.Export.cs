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

        var ngay = (NgayChon?.DateTime ?? DateTime.Today).Date;
        var tenFileMacDinh = ReportExportService.TaoTenFile("BaoCaoCuoiNgay");

        try
        {
            CapNhatTrangThai?.Invoke("Đang tải dữ liệu báo cáo cuối ngày...");
            await TaiDuLieuAsync();

            if (CoLoi)
            {
                await HienThiThongBaoAsync(
                    "Xuất file thất bại",
                    "Không tải được dữ liệu nên không thể xuất file.\n\nChi tiết:\n" + ThongBaoLoi);

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

            await ReportExportService.ExportReportAsync(
                ownerWindow: cuaSo,
                reportTitle: $"Báo cáo cuối ngày {duLieu.NgayHienThi}",
                suggestedFileName: tenFileMacDinh,
                generateWorkbook: () => BaoCaoCuoiNgayExcel.TaoWorkbook(duLieu),
                recordCount: TongKetBanHang.SoHoaDon,
                statusCallback: msg => CapNhatTrangThai?.Invoke(msg));
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi xuat file bao cao cuoi ngay", ex);
            await BaoLoiXuatFileAsync(tenFileMacDinh, ex.Message);
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
