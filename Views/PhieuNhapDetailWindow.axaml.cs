using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class PhieuNhapDetailWindow : Window
{
    public PhieuNhapDetailWindow()
        : this(new PhieuNhapHangDetail())
    {
    }

    public PhieuNhapDetailWindow(PhieuNhapHangDetail detail)
    {
        InitializeComponent();
        DataContext = detail;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
