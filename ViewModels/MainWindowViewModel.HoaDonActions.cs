using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// Mở cửa sổ chỉnh sửa hóa đơn trực tiếp từ danh sách hóa đơn
    /// </summary>
    [RelayCommand]
    private async Task ChinhSuaHoaDonAsync(HoaDon? hoaDon)
    {
        if (hoaDon is null || OwnerWindow is null)
            return;

        if (!await KiemTraQuyenAsync(PermissionKeys.InvoicesEdit, "Bạn không có quyền chỉnh sửa hóa đơn."))
            return;

        var maHoaDon = hoaDon.MaHoaDon;
        if (string.IsNullOrWhiteSpace(maHoaDon))
            return;

        try
        {
            var editor = new HoaDonEditorWindow(maHoaDon);
            var updated = await editor.ShowDialog<HoaDonBanHangReceipt?>(OwnerWindow);
            if (updated != null)
            {
                // Tải lại danh sách hóa đơn
                _invoicesLoaded = false;
                await LoadInvoicesAsync();

                // Tải lại danh sách khách hàng nếu công nợ thay đổi
                _customersLoaded = false;
                await LoadCustomersAsync();

                // Cập nhật lại tồn kho hàng hóa nếu đang mở
                _ = LoadProductsAsync();

                _ = LamMoiTongQuanNeuDangMoAsync();

                StatusMessage = $"Đã cập nhật hóa đơn {updated.MaHoaDon} thành công.";
                await ShowMessageAsync("Thành công", $"Đã cập nhật hóa đơn {updated.MaHoaDon}. Lịch sử chỉnh sửa đã được lưu vào hệ thống.");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể chỉnh sửa hóa đơn {maHoaDon}: {ex.Message}";
            SafeLog.WriteError("Loi ChinhSuaHoaDonAsync", ex);
        }
    }

    /// <summary>
    /// Xem lịch sử chỉnh sửa của hóa đơn
    /// </summary>
    [RelayCommand]
    private async Task XemLichSuHoaDonAsync(HoaDon? hoaDon)
    {
        if (hoaDon is null || OwnerWindow is null)
            return;

        var maHoaDon = hoaDon.MaHoaDon;
        if (string.IsNullOrWhiteSpace(maHoaDon))
            return;

        try
        {
            var historyWindow = new HoaDonLichSuWindow(maHoaDon);
            await historyWindow.ShowDialog(OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể mở lịch sử hóa đơn {maHoaDon}: {ex.Message}";
            SafeLog.WriteError("Loi XemLichSuHoaDonAsync", ex);
        }
    }
}
