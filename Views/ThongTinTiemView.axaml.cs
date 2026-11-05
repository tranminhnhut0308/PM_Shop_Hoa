using Avalonia.Controls;
using ShopHoa.Models;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

/// <summary>
/// Màn "Thông tin tiệm".
/// <para>
/// UserControl này không tự đặt DataContext: <c>MainWindow.axaml</c> gán
/// <c>DataContext="{Binding ThongTinTiem}"</c> từ <c>MainWindowViewModel</c>,
/// đúng như cách màn "Tổng quan" và "Báo cáo cuối ngày" đang làm.
/// </para>
/// </summary>
public partial class ThongTinTiemView : UserControl
{
    public ThongTinTiemView()
    {
        InitializeComponent();
    }

    private void SuaKenh_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ChayLenhKenh(sender, (viewModel, channel) => viewModel.SuaKenhCommand.Execute(channel));

    private void XoaKenh_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ChayLenhKenh(sender, (viewModel, channel) => viewModel.XoaKenhCommand.Execute(channel));

    private void DuaKenhLen_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ChayLenhKenh(sender, (viewModel, channel) => viewModel.DuaKenhLenCommand.Execute(channel));

    private void DuaKenhXuong_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ChayLenhKenh(sender, (viewModel, channel) => viewModel.DuaKenhXuongCommand.Execute(channel));

    private void ChayLenhKenh(
        object? sender,
        Action<ThongTinTiemViewModel, KenhLienHe> chay)
    {
        if (sender is Button { DataContext: KenhLienHe channel }
            && DataContext is ThongTinTiemViewModel viewModel)
            chay(viewModel, channel);
    }
}