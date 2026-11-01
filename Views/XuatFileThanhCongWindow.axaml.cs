using System;
using System.IO;
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
/// file hoặc đóng. Hiển thị chi tiết tên báo cáo, tên file, đường dẫn, thời gian xuất
/// và số dòng dữ liệu nếu có.
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
        TenBaoCaoTextBlock.Text = "Báo cáo thống kê";
        TenFileTextBlock.Text = Path.GetFileName(duongDanFile);
        PathTextBlock.Text = duongDanFile;
        ThoiGianXuatTextBlock.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        SoDongLabel.IsVisible = false;
        SoDongTextBlock.IsVisible = false;
    }

    public XuatFileThanhCongWindow(
        string tenBaoCao,
        string duongDanFile,
        DateTime? thoiGianXuat = null,
        int? soDongDuLieu = null,
        string? thongDiep = null)
        : this()
    {
        MessageTextBlock.Text = thongDiep ?? $"Đã xuất hoàn tất {tenBaoCao}.";
        TenBaoCaoTextBlock.Text = tenBaoCao;
        TenFileTextBlock.Text = Path.GetFileName(duongDanFile);
        PathTextBlock.Text = duongDanFile;
        ThoiGianXuatTextBlock.Text = (thoiGianXuat ?? DateTime.Now).ToString("dd/MM/yyyy HH:mm:ss");

        if (soDongDuLieu.HasValue && soDongDuLieu.Value > 0)
        {
            SoDongLabel.IsVisible = true;
            SoDongTextBlock.IsVisible = true;
            SoDongTextBlock.Text = $"{soDongDuLieu.Value:N0} dòng";
        }
        else
        {
            SoDongLabel.IsVisible = false;
            SoDongTextBlock.IsVisible = false;
        }
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(XuatFileHanhDong.Dong);

    private void OpenFolderButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(XuatFileHanhDong.MoThuMuc);

    private void OpenFileButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(XuatFileHanhDong.MoFile);
}
