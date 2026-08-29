using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DatabaseService _db = new();

    [ObservableProperty] private string selectedMenu = "Tổng quan";
    [ObservableProperty] private string statusMessage = "Sẵn sàng";
    [ObservableProperty] private string productSearch = "";
    [ObservableProperty] private string customerSearch = "";
    [ObservableProperty] private string invoiceSearch = "";
    [ObservableProperty] private string supplierSearch = "";

    public ObservableCollection<SanPham> SanPhams { get; } = new();
    public ObservableCollection<KhachHang> KhachHangs { get; } = new();
    public ObservableCollection<HoaDon> HoaDons { get; } = new();
    public ObservableCollection<NhaCungCap> NhaCungCaps { get; } = new();

    public MainWindowViewModel()
    {
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            await LoadProductsAsync();
            await LoadCustomersAsync();
            await LoadInvoicesAsync();
            await LoadSuppliersAsync();
            StatusMessage = "Đã kết nối database";
        }
        catch (Exception ex)
        {
            StatusMessage = "Chưa kết nối database: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task SearchProductsAsync()
    {
        try
        {
            SanPhams.Clear();
            foreach (var item in await _db.GetSanPhamAsync(ProductSearch))
                SanPhams.Add(item);
            StatusMessage = $"Tìm thấy {SanPhams.Count} sản phẩm";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task SearchCustomersAsync()
    {
        try
        {
            KhachHangs.Clear();
            foreach (var item in await _db.GetKhachHangAsync(CustomerSearch))
                KhachHangs.Add(item);
            StatusMessage = $"Tìm thấy {KhachHangs.Count} khách hàng";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task SearchInvoicesAsync()
    {
        try
        {
            HoaDons.Clear();
            foreach (var item in await _db.GetHoaDonAsync(InvoiceSearch))
                HoaDons.Add(item);
            StatusMessage = $"Tìm thấy {HoaDons.Count} hóa đơn";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task SearchSuppliersAsync()
    {
        try
        {
            NhaCungCaps.Clear();
            foreach (var item in await _db.GetNhaCungCapAsync(SupplierSearch))
                NhaCungCaps.Add(item);
            StatusMessage = $"Tìm thấy {NhaCungCaps.Count} nhà cung cấp";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private void ChonMenu(string menu) => SelectedMenu = menu;

    private async Task LoadProductsAsync()
    {
        SanPhams.Clear();
        foreach (var item in await _db.GetSanPhamAsync())
            SanPhams.Add(item);
    }

    private async Task LoadCustomersAsync()
    {
        KhachHangs.Clear();
        foreach (var item in await _db.GetKhachHangAsync())
            KhachHangs.Add(item);
    }

    private async Task LoadInvoicesAsync()
    {
        HoaDons.Clear();
        foreach (var item in await _db.GetHoaDonAsync())
            HoaDons.Add(item);
    }

    private async Task LoadSuppliersAsync()
    {
        NhaCungCaps.Clear();
        foreach (var item in await _db.GetNhaCungCapAsync())
            NhaCungCaps.Add(item);
    }
}
