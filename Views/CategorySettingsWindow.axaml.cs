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

        var newId = await _dbService.ThemNhomSanPhamAsync(name);
        Groups.Add(new ProductOption { Id = newId, Name = name });
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

        var newId = await _dbService.ThemDonViTinhAsync(name);
        Units.Add(new ProductOption { Id = newId, Name = name });
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