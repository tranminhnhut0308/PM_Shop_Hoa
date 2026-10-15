using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class InvoiceWindow : Window
{
    /// <summary>
    /// Hàm thu công nợ cho hóa đơn đang xem. Trả về hóa đơn đã đọc lại sau khi
    /// thu để cửa sổ cập nhật số nợ và trạng thái nút. Null khi không thu (hủy
    /// hoặc lỗi). Không truyền gì thì cửa sổ không hiện nút thu công nợ.
    /// </summary>
    private readonly Func<HoaDonBanHangReceipt, Window, Task<HoaDonBanHangReceipt?>>? _thuCongNo;

    public InvoiceWindow() : this(new HoaDonBanHangReceipt())
    {
    }

    public InvoiceWindow(HoaDonBanHangReceipt receipt)
        : this(receipt, (Func<HoaDonBanHangReceipt, Window, Task<HoaDonBanHangReceipt?>>?)null)
    {
    }

    public InvoiceWindow(
        HoaDonBanHangReceipt receipt,
        Func<HoaDonBanHangReceipt, Window, Task<HoaDonBanHangReceipt?>>? thuCongNo)
    {
        InitializeComponent();
        _thuCongNo = thuCongNo;
        DataContext = receipt;
        CapNhatNutThanhToan(receipt);
    }

    public InvoiceWindow(
        HoaDonBanHangReceipt receipt,
        Func<HoaDonBanHangReceipt, Task<HoaDonBanHangReceipt?>>? thuCongNo)
        : this(receipt, thuCongNo == null ? null : (r, _) => thuCongNo(r))
    {
    }

    private void CapNhatNutThanhToan(HoaDonBanHangReceipt receipt)
    {
        ThanhToanCongNoButton.IsVisible = _thuCongNo is not null && receipt.ConNo > 0;
    }

    private async void ThanhToanCongNoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_thuCongNo is null || DataContext is not HoaDonBanHangReceipt receipt)
            return;

        ThanhToanCongNoButton.IsEnabled = false;

        try
        {
            var capNhat = await _thuCongNo(receipt, this);

            if (capNhat is not null)
            {
                DataContext = null;
                DataContext = capNhat;
                CapNhatNutThanhToan(capNhat);
            }
        }
        finally
        {
            ThanhToanCongNoButton.IsEnabled = true;
        }
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PrintButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HoaDonBanHangReceipt receipt)
            return;

        try
        {
            var duongDan = HoaDonPrintService.LuuFileTam(receipt);

            Process.Start(new ProcessStartInfo
            {
                FileName = duongDan,
                UseShellExecute = true
            });

            PrintStatus.IsVisible = false;
        }
        catch (Exception ex)
        {
            PrintStatus.Text = "Không mở được bản in: " + ex.Message;
            PrintStatus.IsVisible = true;
        }
    }
}
