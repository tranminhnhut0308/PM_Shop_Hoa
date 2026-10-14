using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class CategorySettingsWindow : Window
{
    private readonly DatabaseService _dbService;

    public ObservableCollection<ProductOption> Groups { get; } = new();
    public ObservableCollection<ProductOption> Units { get; } = new();

    public CategorySettingsWindow()
    {
        InitializeComponent();
        _dbService = new DatabaseService();

        GroupsListBox.ItemsSource = Groups;
        UnitsListBox.ItemsSource = Units;

        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        var groups = await _dbService.GetNhomSanPhamAsync();
        Groups.Clear();
        foreach (var item in groups)
        {
            Groups.Add(item);
        }

        var units = await _dbService.GetDonViTinhAsync();
        Units.Clear();
        foreach (var item in units)
        {
            Units.Add(item);
        }
    }

    private async void AddGroup_OnClick(object? sender, RoutedEventArgs e)
    {
        var name = NewGroupNameTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        // Nhận về ProductOption thay vì ép kiểu hay gán vào uint
        ProductOption newGroup = await _dbService.ThemNhomSanPhamAsync(name);

        // Thêm trực tiếp đối tượng vào danh sách hiển thị của giao diện
        Groups.Add(newGroup);

        NewGroupNameTextBox.Text = string.Empty;
    }

    private async void DeleteGroup_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ProductOption item)
        {
            await _dbService.XoaNhomSanPhamAsync(item.Id);
            Groups.Remove(item);
        }
    }

    private async void AddUnit_OnClick(object? sender, RoutedEventArgs e)
    {
        var name = NewUnitNameTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        // Nhận trực tiếp đối tượng ProductOption trả về từ database service
        var newOption = await _dbService.ThemDonViTinhAsync(name);

        // Thêm trực tiếp đối tượng đó vào danh sách hiển thị
        Units.Add(newOption);
        NewUnitNameTextBox.Text = string.Empty;
    }

    private async void DeleteUnit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ProductOption item)
        {
            await _dbService.XoaDonViTinhAsync(item.Id);
            Units.Remove(item);
        }
    }
}