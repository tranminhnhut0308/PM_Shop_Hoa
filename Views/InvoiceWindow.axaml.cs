using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

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
}
