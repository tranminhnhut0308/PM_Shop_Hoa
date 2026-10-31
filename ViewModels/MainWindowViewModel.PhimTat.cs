using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

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
    /// Không xử lý ở màn hình Bán hàng vì thao tác đó sẽ xóa giỏ hàng hiện tại.
    /// </summary>
    [RelayCommand]
    private async Task TaoMoiTheoNguCanhAsync()
    {
        if (IsSalesPanelVisible)
            return;

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

            case "Nhập hàng":
                if (IsNhapHangFormVisible)
                    await TaoPhieuNhapMoiAsync();
                else
                    await MoPhieuNhapHangAsync();
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
            case "phieu-nhap-hang":
                await LoadBaoCaoPhieuNhapAsync();
                break;

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

    /// <summary>Esc: đóng màn hình Bán hàng nhưng hỏi lại nếu giỏ còn hàng.</summary>
    [RelayCommand]
    private async Task DongBanHangAnToanAsync()
    {
        if (!IsSalesPanelVisible)
            return;

        if (SaleCart.Count > 0)
        {
            var dong = await ShowConfirmationAsync(
                "Đóng màn hình Bán hàng",
                $"Giỏ hàng đang có {SaleCart.Count:N0} dòng chưa thanh toán. "
                + "Đóng màn hình sẽ xóa giỏ hàng hiện tại. Bạn có chắc muốn đóng?",
                "Đóng và xóa giỏ",
                "Ở lại");

            if (!dong)
                return;
        }

        DongGiaoDienBanHang();
        StatusMessage = "Đã đóng màn hình Bán hàng.";
    }
}
