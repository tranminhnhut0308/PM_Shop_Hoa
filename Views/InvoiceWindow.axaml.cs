using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class InvoiceWindow : Window
{
    public InvoiceWindow() : this(new HoaDonBanHangReceipt())
    {
    }

    public InvoiceWindow(HoaDonBanHangReceipt receipt)
    {
        InitializeComponent();
        DataContext = receipt;
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
