using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DatabaseService _db = new();
    public Window? OwnerWindow { get; set; }
    private bool _customersLoaded;
    private bool _invoicesLoaded;
    private bool _suppliersLoaded;

    [ObservableProperty] private string selectedMenu = "Hàng hóa";
    [ObservableProperty] private int selectedDataTabIndex;
    [ObservableProperty] private string statusMessage = "Sẵn sàng";
    [ObservableProperty] private string productSearch = "";
    [ObservableProperty] private string customerSearch = "";
    [ObservableProperty] private string invoiceSearch = "";
    [ObservableProperty] private string supplierSearch = "";
    [ObservableProperty] private string productFilter = "";
    private bool _sortByPrice;

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
        StatusMessage = "Đang tải dữ liệu...";

        var error = await LoadSafelyAsync("sản phẩm", LoadProductsAsync);

        StatusMessage = error is null
            ? "Đã kết nối database"
            : $"Đã tải {SanPhams.Count} sản phẩm. Lỗi: {error}";
    }

    private static async Task<string?> LoadSafelyAsync(
        string dataName,
        Func<Task> load)
    {
        try
        {
            await load();
            return null;
        }
        catch (Exception ex)
        {
            return dataName + ": " + ex.Message;
        }
    }

    private async Task LoadSelectedSectionAsync(string menu)
    {
        if (menu == "Tổng quan" || menu == "Hàng hóa")
            return;

        Func<Task>? load = menu switch
        {
            "Khách hàng" => LoadCustomersAsync,
            "Đơn hàng" => LoadInvoicesAsync,
            "Mua hàng" => LoadSuppliersAsync,
            _ => null
        };

        var loaded = menu switch
        {
            "Khách hàng" => _customersLoaded,
            "Đơn hàng" => _invoicesLoaded,
            "Mua hàng" => _suppliersLoaded,
            _ => true
        };

        if (loaded || load is null)
            return;

        StatusMessage = $"Đang tải {menu.ToLowerInvariant()}...";
        var error = await LoadSafelyAsync(menu.ToLowerInvariant(), load);

        if (error is null)
        {
            switch (menu)
            {
                case "Khách hàng":
                    _customersLoaded = true;
                    break;
                case "Đơn hàng":
                    _invoicesLoaded = true;
                    break;
                case "Mua hàng":
                    _suppliersLoaded = true;
                    break;
            }

            StatusMessage = $"Đã tải {menu.ToLowerInvariant()}";
        }
        else
        {
            StatusMessage = "Không thể tải " + error;
        }
    }

    private async Task LoadProductsAsync()
    {
        SanPhams.Clear();
        foreach (var item in await _db.GetSanPhamAsync())
        {
            if (!string.IsNullOrWhiteSpace(item.HinhAnh) && File.Exists(item.HinhAnh))
                item.HinhAnhBitmap = new Bitmap(item.HinhAnh);

            SanPhams.Add(item);
        }
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

    [RelayCommand]
    private async Task SearchProductsAsync()
    {
        try
        {
            await RefreshProductsAsync();
            StatusMessage = $"Tìm thấy {SanPhams.Count} sản phẩm";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task LocSanPhamAsync(string? filter)
    {
        ProductFilter = filter ?? "";
        await RefreshProductsAsync();
        StatusMessage = string.IsNullOrWhiteSpace(ProductFilter)
            ? $"Hiển thị {SanPhams.Count} sản phẩm"
            : $"Lọc được {SanPhams.Count} sản phẩm";
    }

    [RelayCommand]
    private void SapXepSanPham()
    {
        _sortByPrice = !_sortByPrice;
        var sorted = _sortByPrice
            ? SanPhams.OrderBy(product => product.GiaBan).ToList()
            : SanPhams.OrderBy(product => product.TenSanPham).ToList();

        SanPhams.Clear();
        foreach (var product in sorted)
            SanPhams.Add(product);

        StatusMessage = _sortByPrice ? "Đã sắp xếp theo giá tăng dần" : "Đã sắp xếp theo tên";
    }

    [RelayCommand]
    private async Task XoaSanPhamAsync(SanPham? product)
    {
        if (product is null || product.Id == 0)
        {
            StatusMessage = "Sản phẩm này chưa có mã dữ liệu để xóa.";
            return;
        }

        try
        {
            var confirmed = await ShowConfirmationAsync(
                "Xóa sản phẩm",
                $"Bạn có chắc muốn xóa sản phẩm \"{product.TenSanPham}\" không?");

            if (!confirmed)
                return;

            await _db.XoaSanPhamAsync(product.Id);
            SanPhams.Remove(product);
            StatusMessage = $"Đã xóa sản phẩm {product.MaSanPham}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể xóa sản phẩm: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task SuaSanPhamAsync(SanPham? product)
    {
        if (product is null)
        {
            StatusMessage = "Vui lòng chọn sản phẩm cần sửa.";
            return;
        }

        var (groups, units) = await LoadProductOptionsAsync();
        var editor = new ProductEditorWindow(product, groups, units);
        var saved = OwnerWindow is not null && await editor.ShowDialog<bool>(OwnerWindow);
        if (!saved)
            return;

        try
        {
            await _db.CapNhatSanPhamAsync(product);
            StatusMessage = $"Đã cập nhật sản phẩm {product.MaSanPham}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể cập nhật sản phẩm: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task CapNhatHinhAnhAsync(SanPham? product)
    {
        if (product is null || product.Id == 0 || OwnerWindow is null)
        {
            StatusMessage = "Không thể mở chọn ảnh cho sản phẩm này.";
            return;
        }

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn ảnh sản phẩm",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Ảnh")
                    {
                        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp"]
                    }
                ]
            });

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        var imagePath = file.Path.LocalPath;

        try
        {
            await _db.CapNhatHinhAnhSanPhamAsync(product.Id, imagePath);
            product.HinhAnh = imagePath;
            product.HinhAnhBitmap = new Bitmap(imagePath);
            StatusMessage = $"Đã cập nhật ảnh cho {product.MaSanPham}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể cập nhật ảnh: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ThemSanPhamAsync()
    {
        if (OwnerWindow is null)
            return;

        var (groups, units) = await LoadProductOptionsAsync();
        var editor = new ProductEditorWindow(null, groups, units);
        if (!await editor.ShowDialog<bool>(OwnerWindow))
            return;

        try
        {
            editor.Product.Id = await _db.ThemSanPhamAsync(editor.Product);
            SanPhams.Add(editor.Product);
            StatusMessage = $"Đã thêm sản phẩm {editor.Product.MaSanPham}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể thêm sản phẩm: " + ex.Message;
        }
    }

    private async Task RefreshProductsAsync()
    {
        var products = await _db.GetSanPhamAsync(ProductSearch);

        if (!string.IsNullOrWhiteSpace(ProductFilter))
        {
            products = products.Where(product =>
                string.Equals(product.LoaiHang, ProductFilter, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(product.NhomHangBaCap, ProductFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        SanPhams.Clear();
        foreach (var product in products)
        {
            if (!string.IsNullOrWhiteSpace(product.HinhAnh) && File.Exists(product.HinhAnh))
                product.HinhAnhBitmap = new Bitmap(product.HinhAnh);

            SanPhams.Add(product);
        }
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        if (OwnerWindow is null)
            return false;

        var dialog = new ConfirmationWindow(title, message);
        return await dialog.ShowDialog<bool>(OwnerWindow);
    }

    private async Task<(IReadOnlyList<ProductOption> Groups, IReadOnlyList<ProductOption> Units)> LoadProductOptionsAsync()
    {
        try
        {
            var groupsTask = _db.GetNhomSanPhamAsync();
            var unitsTask = _db.GetDonViTinhAsync();
            await Task.WhenAll(groupsTask, unitsTask);
            return (await groupsTask, await unitsTask);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải nhóm hàng/đơn vị tính: " + ex.Message;
            return (Array.Empty<ProductOption>(), Array.Empty<ProductOption>());
        }
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

    partial void OnSelectedMenuChanged(string value)
    {
        SelectedDataTabIndex = value switch
        {
            "Khách hàng" => 1,
            "Đơn hàng" => 2,
            "Mua hàng" => 3,
            _ => 0
        };

        OnPropertyChanged(nameof(IsTongQuanSelected));
        OnPropertyChanged(nameof(IsDataSectionSelected));
        _ = LoadSelectedSectionAsync(value);
    }

    public bool IsTongQuanSelected => SelectedMenu == "Tổng quan";

    public bool IsDataSectionSelected => !IsTongQuanSelected;

}
