using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// Các lệnh phục vụ bộ phím tắt đã công bố trong cửa sổ Trợ giúp.
/// Chỉ đặt ở đây những thao tác an toàn; các nút xóa / hoàn tất vẫn phải
/// bấm trực tiếp để tránh vô tình thay đổi dữ liệu.
/// </summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// Ctrl + N: tạo mới theo màn hình đang mở.
    /// Ở màn hình Bán hàng: tạo thêm một giỏ hàng mới (hỗ trợ nhiều giỏ hàng).
    /// </summary>
    [RelayCommand]
    private async Task TaoMoiTheoNguCanhAsync()
    {
        if (IsSalesPanelVisible)
        {
            await TaoDonBanMoiAsync();
            return;
        }

        switch (SelectedMenu)
        {
            case "Hàng hóa":
                await ThemSanPhamAsync();
                break;

            case "Khách hàng":
                await ThemMoiKhachHangAsync();
                break;

            case "Nhà cung cấp":
                await ThemNhaCungCapAsync();
                break;

            case "Phiếu nhập hàng":
                await MoGiaoDienNhapHangAsync();
                break;

            case "Nhập hàng":
                if (!await KiemTraQuyenAsync(PermissionKeys.PurchasesCreate, "Bạn không có quyền tạo phiếu nhập hàng."))
                    break;

                if (CoDuLieuPhieuNhapDangSoan)
                {
                    var confirmed = await ShowConfirmationAsync(
                        "Tạo phiếu nhập mới",
                        "Phiếu nhập hiện tại đang có dữ liệu chưa hoàn thành. Xóa dữ liệu đang nhập và tạo phiếu mới?",
                        "Tạo phiếu mới",
                        "Hủy");

                    if (!confirmed)
                        break;
                }

                await KhoiTaoPhieuNhapMoiAsync();
                StatusMessage = "Đã tạo phiếu nhập mới.";
                break;

            case "Sổ quỹ":
                await MoSoQuyPhieuThuAsync();
                break;
        }
    }

    /// <summary>F5: làm mới dữ liệu của đúng màn hình đang xem.</summary>
    [RelayCommand]
    private async Task LamMoiKhuVucHienTaiAsync()
    {
        if (IsSalesPanelVisible)
        {
            await TimKiemHoaBanAsync();
            StatusMessage = "Đã làm mới kết quả tìm hàng hóa.";
            return;
        }

        switch (SelectedMenu)
        {
            case "Tổng quan":
                await LoadOverviewAsync();
                StatusMessage = "Đã làm mới tổng quan.";
                break;

            case "Hàng hóa":
                await SearchProductsAsync(ProductSearch);
                StatusMessage = "Đã làm mới danh sách hàng hóa.";
                break;

            case "Khách hàng":
                await TimKiemKhachHangAsync();
                StatusMessage = "Đã làm mới danh sách khách hàng.";
                break;

            case "Hóa đơn":
                await SearchInvoicesAsync();
                StatusMessage = "Đã làm mới danh sách hóa đơn.";
                break;

            case "Nhà cung cấp":
                await RefreshSuppliersAsync();
                StatusMessage = "Đã làm mới danh sách nhà cung cấp.";
                break;

            case "Phiếu nhập hàng":
                await LoadBaoCaoPhieuNhapAsync();
                StatusMessage = "Đã làm mới danh sách phiếu nhập hàng.";
                break;

            case "Nhập hàng":
                if (IsNhapHangFormVisible)
                {
                    await LoadPhieuNhapDonViTinhOptionsAsync();
                    await LoadPhieuNhapProductSuggestionsAsync();
                    StatusMessage = "Đã làm mới dữ liệu gợi ý của phiếu nhập.";
                }
                else
                {
                    StatusMessage = "Danh sách phiếu nhập chưa hỗ trợ làm mới bằng F5.";
                }
                break;

            case "Sổ quỹ":
                await TaiLaiSoQuyAsync();
                StatusMessage = "Đã làm mới sổ quỹ.";
                break;

            case "Báo cáo":
                await LamMoiBaoCaoAsync();
                break;

            case "Thông tin tiệm":
                await ThongTinTiem.TaiLaiCommand.ExecuteAsync(null);
                StatusMessage = "Đã làm mới thông tin tiệm.";
                break;
        }
    }

    private async Task LamMoiBaoCaoAsync()
    {
        switch (SelectedReportKey)
        {
            case "ban-hang":
                await BaoCaoBanHang.TaiLaiAsync();
                break;

            case "hang-hoa":
                await BaoCaoHangHoa.TaiLaiAsync();
                break;

            case "khach-hang":
                await BaoCaoKhachHang.TaiLaiAsync();
                break;

            case "nha-cung-cap":
                await BaoCaoNhaCungCap.TaiLaiAsync();
                break;

            case "cuoi-ngay":
                await BaoCaoCuoiNgay.TaiLaiAsync();
                break;

            default:
                StatusMessage = "Chưa chọn báo cáo để làm mới.";
                return;
        }

        StatusMessage = "Đã làm mới báo cáo đang xem.";
    }

    /// <summary>Esc: đóng màn hình Bán hàng an toàn và lưu nháp các đơn hàng đang soạn.</summary>
    [RelayCommand]
    private async Task DongBanHangAnToanAsync()
    {
        if (!IsSalesPanelVisible)
            return;

        await FlushSaleCartDraftAsync();
        DongGiaoDienBanHang();
        StatusMessage = "Đã tạm đóng màn hình Bán hàng (dữ liệu các đơn hàng được bảo lưu an toàn).";
    }
}
