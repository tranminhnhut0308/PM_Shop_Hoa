using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ShopHoa.Views;

/// <summary>Hành động người dùng chọn sau khi xuất file thành công.</summary>
public enum XuatFileHanhDong
{
    Dong,
    MoFile,
    MoThuMuc
}

/// <summary>
/// Hộp thoại sau khi xuất file thành công, cho phép mở file, mở thư mục chứa
/// file hoặc đóng. File đã được đóng hoàn toàn trước khi hộp thoại này hiện ra.
/// </summary>
public partial class XuatFileThanhCongWindow : Window
{
    public XuatFileThanhCongWindow()
    {
        InitializeComponent();
    }

    public XuatFileThanhCongWindow(string noiDung, string duongDanFile)
        : this()
    {
        MessageTextBlock.Text = noiDung;
        PathTextBlock.Text = duongDanFile;
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(XuatFileHanhDong.Dong);

    private void OpenFolderButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(XuatFileHanhDong.MoThuMuc);

    private void OpenFileButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(XuatFileHanhDong.MoFile);
}
