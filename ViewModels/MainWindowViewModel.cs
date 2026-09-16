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
    private bool _overviewLoaded;

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
    public ObservableCollection<SanPham> ProductSearchResults { get; } = new();
    public ObservableCollection<SaleCartItem> SaleCart { get; } = new();
    public ObservableCollection<KhachHang> SaleCustomerSuggestions { get; } = new();
    public IReadOnlyList<string> DiscountModes { get; } = ["Số tiền", "Phần trăm"];

    [ObservableProperty] private string saleSearchText = "";
    [ObservableProperty] private bool isSalesPanelVisible;
    [ObservableProperty] private string saleCustomerName = "Khách lẻ";
    [ObservableProperty] private decimal saleTotal;
    [ObservableProperty] private decimal saleSubtotal;
    [ObservableProperty] private decimal saleDiscountInput;
    [ObservableProperty] private decimal saleDiscountAmount;
    [ObservableProperty] private string saleDiscountMode = "Số tiền";
    [ObservableProperty] private KhachHang? selectedKhachHang;
    [ObservableProperty] private KhachHang? selectedSaleCustomer;

    public async Task SearchSaleCustomersAsync(string search)
    {
        try
        {
            SaleCustomerSuggestions.Clear();

            if (string.IsNullOrWhiteSpace(search))
                return;

            foreach (var customer in await _db.GetKhachHangAsync(search.Trim()))
                SaleCustomerSuggestions.Add(customer);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tìm khách hàng bán hàng: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ChonKhachHangBan(KhachHang? customer)
    {
        if (customer is null)
            return;

        SelectedSaleCustomer = customer;
        SaleCustomerName = customer.TenKhachHang;
        SaleCustomerSuggestions.Clear();
    }

    [RelayCommand]
    private void ChonKhachLe()
    {
        SelectedSaleCustomer = null;
        SaleCustomerName = "Khách lẻ";
        SaleCustomerSuggestions.Clear();
    }

    [RelayCommand]
    private async Task MoDanhSachKhachBanAsync()
    {
        try
        {
            SaleCustomerSuggestions.Clear();
            foreach (var customer in await _db.GetKhachHangAsync())
                SaleCustomerSuggestions.Add(customer);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải danh sách khách hàng: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task TimKiemKhachHangAsync()
    {
        await SearchCustomersAsync();
    }

    [RelayCommand]
    private void ThemMoiKhachHang()
    {
        SelectedKhachHang = new KhachHang
        {
            MaKhachHang = "",
            TenKhachHang = "",
            NhomKhachHang = "Cá nhân"
        };
        StatusMessage = "Đang tạo khách hàng mới";
    }

    [RelayCommand]
    private async Task ThemKhachHangTuFileAsync()
    {
        if (OwnerWindow is null)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn file danh sách khách hàng",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Excel / CSV")
                    {
                        Patterns = ["*.xlsx", "*.xls", "*.csv"]
                    }
                ]
            });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            StatusMessage = "Bạn chưa chọn file để nhập khách hàng hàng loạt.";
            return;
        }

        try
        {
            var customersFromFile = _db.ParseKhachHangFromFile(file.Path.LocalPath);
            if (customersFromFile.Count == 0)
            {
                StatusMessage = $"File \"{file.Name}\" không có dữ liệu khách hàng hợp lệ.";
                return;
            }

            var importedCount = await _db.ImportKhachHangTuFileAsync(file.Path.LocalPath, customersFromFile);
            await LoadCustomersAsync();

            StatusMessage = importedCount > 0
                ? $"Đã nhập thành công {importedCount} khách hàng từ file \"{file.Name}\"."
                : $"File \"{file.Name}\" không có dữ liệu khách hàng hợp lệ để nhập.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể nhập khách hàng từ file: " + ex.Message;
        }
    }

    [RelayCommand]
    private void QuayLaiDanhSachKhachHang()
    {
        SelectedKhachHang = null;
        StatusMessage = "Đã quay lại danh sách khách hàng";
    }

    [RelayCommand]
    private void XoaKhachHang()
    {
        if (SelectedKhachHang is null)
            return;

        var name = SelectedKhachHang.TenKhachHang;
        KhachHangs.Remove(SelectedKhachHang);
        SelectedKhachHang = null;
        StatusMessage = $"Đã xóa khách hàng {name}";
    }

    [RelayCommand]
    private void ChinhSuaKhachHang()
    {
        if (SelectedKhachHang is null)
            return;

        StatusMessage = $"Đang chỉnh sửa khách hàng: {SelectedKhachHang.TenKhachHang}";
    }

    [RelayCommand]
    private void NgungHoatDongKhachHang()
    {
        if (SelectedKhachHang is null)
            return;

        StatusMessage = $"Khách hàng {SelectedKhachHang.TenKhachHang} đã ngừng hoạt động";
    }

    private bool _isInitialized;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        await LoadAsync();
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
        if (menu == "Hàng hóa")
            return;

        Func<Task>? load = menu switch
        {
            "Tổng quan" => LoadOverviewAsync,
            "Khách hàng" => LoadCustomersAsync,
            "Đơn hàng" => LoadInvoicesAsync,
            "Mua hàng" => LoadSuppliersAsync,
            _ => null
        };

        var loaded = menu switch
        {
            "Tổng quan" => _overviewLoaded,
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
                case "Tổng quan":
                    _overviewLoaded = true;
                    break;
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

    private async Task LoadOverviewAsync()
    {
        var customersTask = LoadCustomersAsync();
        var invoicesTask = LoadInvoicesAsync();
        var suppliersTask = LoadSuppliersAsync();

        await Task.WhenAll(customersTask, invoicesTask, suppliersTask);
    }

    private async Task LoadCustomersAsync()
    {
        var customers = await _db.GetKhachHangAsync();

        KhachHangs.Clear();
        foreach (var item in customers)
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
    private async Task SearchProductsAsync(string? searchText)
    {
        try
        {
            ProductSearch = searchText ?? string.Empty;
            await RefreshProductsAsync(ProductSearch);
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

    [RelayCommand]
    private async Task ThemSanPhamTuFileAsync()
    {
        if (OwnerWindow is null)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn file danh sách hàng hóa",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Excel / CSV")
                    {
                        Patterns = ["*.xlsx", "*.xls", "*.csv"]
                    }
                ]
            });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            StatusMessage = "Bạn chưa chọn file để nhập hàng loạt.";
            return;
        }

        StatusMessage = $"Đã chọn file \"{file.Name}\". Đang kiểm tra dữ liệu trước khi nhập...";

        try
        {
            var productsFromFile = _db.ParseProductsFromFile(file.Path.LocalPath);
            if (productsFromFile.Count == 0)
            {
                StatusMessage = $"File \"{file.Name}\" không có dữ liệu sản phẩm hợp lệ để nhập.";
                return;
            }

            var existingProducts = await _db.GetSanPhamAsync();
            var duplicateProducts = productsFromFile
                .Where(product => IsDuplicateProduct(product, existingProducts))
                .Take(5)
                .ToList();

            if (duplicateProducts.Count > 0)
            {
                var shouldKeepExisting = await ShowConfirmationAsync(
                    "Sản phẩm trùng",
                    $"Phát hiện {duplicateProducts.Count} sản phẩm trong file đã tồn tại trong kho. Bạn muốn giữ dữ liệu hiện có và bỏ qua các sản phẩm trùng này?",
                    "Giữ dữ liệu cũ",
                    "Bỏ qua nhập file");

                if (!shouldKeepExisting)
                {
                    StatusMessage = "Đã bỏ qua nhập file vì phát hiện sản phẩm trùng.";
                    return;
                }

                productsFromFile = productsFromFile
                    .Where(product => !IsDuplicateProduct(product, existingProducts))
                    .ToList();
            }

            if (productsFromFile.Count == 0)
            {
                StatusMessage = "Tất cả sản phẩm trong file đều trùng với dữ liệu hiện có.";
                return;
            }

            var importedCount = await _db.ImportSanPhamTuFileAsync(file.Path.LocalPath, productsFromFile);
            await RefreshProductsAsync();

            StatusMessage = importedCount > 0
                ? $"Đã nhập thành công {importedCount} sản phẩm từ file \"{file.Name}\"."
                : $"File \"{file.Name}\" không có dữ liệu sản phẩm hợp lệ để nhập.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể nhập hàng loạt từ file: " + ex.Message;
        }
    }

    private async Task RefreshProductsAsync(string? searchText = null)
    {
        var products = await _db.GetSanPhamAsync(searchText ?? ProductSearch);

        if (!string.IsNullOrWhiteSpace(ProductFilter))
        {
            products = products
                .Where(product => MatchesFilter(product, ProductFilter))
                .ToList();
        }

        SanPhams.Clear();
        foreach (var product in products)
        {
            if (!string.IsNullOrWhiteSpace(product.HinhAnh) && File.Exists(product.HinhAnh))
                product.HinhAnhBitmap = new Bitmap(product.HinhAnh);

            SanPhams.Add(product);
        }
    }

    private static bool MatchesFilter(SanPham product, string filter)
    {
        var normalizedFilter = NormalizeText(filter);

        if (string.IsNullOrWhiteSpace(normalizedFilter))
            return true;

        var haystacks = new[]
        {
            product.LoaiHang,
            product.NhomHangBaCap,
            product.TenSanPham,
            product.NhomSanPham,
            product.DonViTinh
        };

        return haystacks
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(value => NormalizeText(value).Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .Replace("/", " ")
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace("  ", " ");
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message)
        => await ShowConfirmationAsync(title, message, "Xóa", "Hủy");

    private async Task<bool> ShowConfirmationAsync(string title, string message, string confirmButtonText, string cancelButtonText)
    {
        if (OwnerWindow is null)
            return false;

        var dialog = new ConfirmationWindow(title, message, confirmButtonText, cancelButtonText);
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

    private static bool IsDuplicateProduct(SanPham product, IReadOnlyList<SanPham> existingProducts)
    {
        var trimmedMaSanPham = product.MaSanPham.Trim();
        var trimmedMaVach = product.MaVach?.Trim();
        var trimmedTenSanPham = product.TenSanPham.Trim();

        return existingProducts.Any(existing =>
            (!string.IsNullOrWhiteSpace(trimmedMaSanPham) &&
                existing.MaSanPham.Equals(trimmedMaSanPham, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(trimmedMaVach) &&
                !string.IsNullOrWhiteSpace(existing.MaVach) &&
                existing.MaVach.Equals(trimmedMaVach, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(trimmedTenSanPham) &&
                existing.TenSanPham.Equals(trimmedTenSanPham, StringComparison.OrdinalIgnoreCase)));
    }

    [RelayCommand]
    private async Task SearchCustomersAsync()
    {
        try
        {
            var customers = await _db.GetKhachHangAsync(CustomerSearch);

            KhachHangs.Clear();
            foreach (var item in customers)
                KhachHangs.Add(item);

            _customersLoaded = true;
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
    private async Task TimKiemHoaBanAsync(string? keyword)
    {
        var search = keyword ?? SaleSearchText ?? "";
        var products = await _db.GetSanPhamAsync(search);

        ProductSearchResults.Clear();
        foreach (var product in products)
            ProductSearchResults.Add(product);
    }

    [RelayCommand]
    private void MoGiaoDienBanHang()
    {
        IsSalesPanelVisible = true;
        SaleCustomerName = "Khách lẻ";
        SaleSearchText = "";
        SaleCart.Clear();
        ProductSearchResults.Clear();
        SaleTotal = 0;
        SaleSubtotal = 0;
        SaleDiscountInput = 0;
        SaleDiscountAmount = 0;
        SaleDiscountMode = "Số tiền";
    }

    [RelayCommand]
    private void DongGiaoDienBanHang()
    {
        IsSalesPanelVisible = false;
        SaleCart.Clear();
        ProductSearchResults.Clear();
        SaleTotal = 0;
        SaleSubtotal = 0;
        SaleDiscountInput = 0;
        SaleDiscountAmount = 0;
    }

    [RelayCommand]
    private void ThemVaoGioHang(SanPham? product)
    {
        if (product is null)
            return;

        var existing = SaleCart.FirstOrDefault(item => item.SanPham.Id == product.Id);
        if (existing is not null)
        {
            existing.SoLuong += 1;
        }
        else
        {
            var item = new SaleCartItem { SanPham = product, SoLuong = 1 };
            item.QuantityChanged += RecalculateSaleTotal;
            SaleCart.Add(item);
        }

        RecalculateSaleTotal();
    }

    [RelayCommand]
    private void CapNhatTongTienBanHang()
    {
        RecalculateSaleTotal();
    }

    [RelayCommand]
    private void TangSoLuong(SaleCartItem? item)
    {
        if (item is null)
            return;

        item.SoLuong += 1;
        RecalculateSaleTotal();
    }

    [RelayCommand]
    private void GiamSoLuong(SaleCartItem? item)
    {
        if (item is null)
            return;

        item.SoLuong = Math.Max(1, item.SoLuong - 1);
        RecalculateSaleTotal();
    }

    [RelayCommand]
    private void XoaKhoiGioHang(SaleCartItem? item)
    {
        if (item is null)
            return;

        item.QuantityChanged -= RecalculateSaleTotal;
        SaleCart.Remove(item);
        RecalculateSaleTotal();
    }

    private void RecalculateSaleTotal()
    {
        SaleSubtotal = SaleCart.Sum(item => item.ThanhTien);
        SaleDiscountAmount = SaleDiscountMode == "Phần trăm"
            ? SaleSubtotal * Math.Clamp(SaleDiscountInput, 0, 100) / 100
            : Math.Clamp(SaleDiscountInput, 0, SaleSubtotal);
        SaleTotal = Math.Max(0, SaleSubtotal - SaleDiscountAmount);
    }

    partial void OnSaleDiscountInputChanged(decimal value) => RecalculateSaleTotal();

    partial void OnSaleDiscountModeChanged(string value) => RecalculateSaleTotal();

    [RelayCommand]
    private async Task ThanhToanAsync()
    {
        if (SaleCart.Count == 0)
        {
            StatusMessage = "Vui lòng chọn ít nhất một sản phẩm trước khi thanh toán.";
            return;
        }

        try
        {
            var receipt = await _db.TaoHoaDonBanHangAsync(
                SaleCart.ToList(),
                SelectedSaleCustomer,
                SaleDiscountAmount);

            if (OwnerWindow is not null)
            {
                var invoiceWindow = new InvoiceWindow(receipt);
                await invoiceWindow.ShowDialog(OwnerWindow);
            }

            SaleCart.Clear();
            SaleCustomerSuggestions.Clear();
            SaleTotal = 0;
            SaleSubtotal = 0;
            SaleDiscountInput = 0;
            SaleDiscountAmount = 0;
            _invoicesLoaded = false;
            StatusMessage = $"Đã tạo hóa đơn {receipt.MaHoaDon}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể thanh toán: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ChonMenu(string menu)
    {
        IsSalesPanelVisible = false;
        SelectedMenu = menu;
    }

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
