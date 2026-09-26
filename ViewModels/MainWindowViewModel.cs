using System.Collections.ObjectModel;
using System.Threading;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;
using Avalonia.Controls;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DatabaseService _db = new();
    private CancellationTokenSource? _saleCustomerSearchCts;
    private int _saleCustomerSearchVersion;
    public Window? OwnerWindow { get; set; }
    private bool _customersLoaded;
    private bool _invoicesLoaded;
    private bool _suppliersLoaded;
    private bool _overviewLoaded;

    [ObservableProperty] private string selectedMenu = "Tổng quan";
    [ObservableProperty] private int selectedDataTabIndex = 0;
    [ObservableProperty] private string statusMessage = "Sẵn sàng";
    [ObservableProperty] private bool isImporting;
    [ObservableProperty]
    private string importStatus = "";
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
    [ObservableProperty]
    private decimal tienKhachDua = 0;

    [ObservableProperty]
    private decimal tienThoi = 0;

    [ObservableProperty]
    private decimal tienCongNo = 0;

    [ObservableProperty]
    private string trangThaiThanhToan = "";
    [ObservableProperty] private string saleDiscountMode = "Số tiền";
    [ObservableProperty] private KhachHang? selectedKhachHang;
    [ObservableProperty] private NhaCungCap? selectedNhaCungCap;
    [ObservableProperty] private KhachHang? selectedSaleCustomer;
    [ObservableProperty] private string ngaySinhText = "";

    partial void OnSelectedKhachHangChanged(KhachHang? value)
    {
        NgaySinhText = value?.NgaySinh?.ToString("dd/MM/yyyy") ?? "";
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
    private async Task ThemMoiKhachHangAsync()
    {
        try
        {
            var maKhachHangMoi = await _db.TaoMaKhachHangMoiAsync();

            SelectedKhachHang = new KhachHang
            {
                Id = 0,
                MaKhachHang = maKhachHangMoi,
                TenKhachHang = "",
                LoaiKhachHang = "CA_NHAN",
                NhomKhachHang = "Cá nhân",
                TrangThai = true
            };

            NgaySinhText = "";

            StatusMessage =
                $"Đang tạo khách hàng mới - mã {maKhachHangMoi}";
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Không thể tạo mã khách hàng tự động: " + ex.Message;
        }
    }
    [RelayCommand]
    private async Task ThemKhachHangTuFileAsync()
    {
        if (OwnerWindow is null || IsImporting)
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
            IsImporting = true;
            StatusMessage = $"⏳ Đã chọn file \"{file.Name}\". Đang đọc dữ liệu...";
            await Task.Yield();

            var customersFromFile = await Task.Run(() =>
                _db.ParseKhachHangFromFile(file.Path.LocalPath));

            if (customersFromFile.Count == 0)
            {
                StatusMessage = $"❌ File \"{file.Name}\" không có dữ liệu khách hàng hợp lệ.";
                return;
            }

            StatusMessage = $"⏳ Đã đọc {customersFromFile.Count:N0} khách hàng. Đang kiểm tra dữ liệu trùng...";
            await Task.Yield();

            var existingCustomers = await _db.GetKhachHangAsync();
            var duplicateCustomers = customersFromFile
                .Where(customer => IsDuplicateCustomer(customer, existingCustomers))
                .ToList();

            bool replaceExisting = false;

            if (duplicateCustomers.Count > 0)
            {
                var replace = await ShowConfirmationAsync(
                    "Khách hàng bị trùng",
                    $"Phát hiện {duplicateCustomers.Count:N0} khách hàng trong file đã tồn tại.\n\nBạn muốn giữ dữ liệu khách hàng cũ hay thay thế bằng dữ liệu trong file?",
                    "Thay thế",
                    "Giữ khách hàng cũ");

                replaceExisting = replace;

                StatusMessage = replaceExisting
                    ? "⏳ Đã chọn thay thế. Đang nhập và cập nhật dữ liệu..."
                    : "⏳ Đã chọn giữ dữ liệu cũ. Đang bỏ qua khách hàng trùng và nhập khách hàng mới...";
            }
            else
            {
                StatusMessage = $"⏳ Không có khách hàng trùng. Đang nhập {customersFromFile.Count:N0} khách hàng...";
            }

            await Task.Yield();

            var importedCount = await Task.Run(() =>
                _db.ImportKhachHangTuFileAsync(
                    file.Path.LocalPath,
                    customersFromFile,
                    replaceExisting));

            StatusMessage = "⏳ Đang làm mới danh sách khách hàng...";
            await LoadCustomersAsync();

            StatusMessage = replaceExisting && duplicateCustomers.Count > 0
                ? $"✅ Đã nhập {importedCount:N0} khách hàng và thay thế {duplicateCustomers.Count:N0} dữ liệu trùng."
                : $"✅ Đã nhập {importedCount:N0} khách hàng từ file \"{file.Name}\".";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ Không thể nhập khách hàng từ file: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
        }
    }

    private static bool IsDuplicateCustomer(
        KhachHang customer,
        IReadOnlyList<KhachHang> existingCustomers)
    {
        var code = customer.MaKhachHang?.Trim();
        var phone = customer.SoDienThoai?.Trim();

        return existingCustomers.Any(existing =>
            (!string.IsNullOrWhiteSpace(code) &&
             !string.IsNullOrWhiteSpace(existing.MaKhachHang) &&
             existing.MaKhachHang.Equals(code, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(phone) &&
             !string.IsNullOrWhiteSpace(existing.SoDienThoai) &&
             existing.SoDienThoai.Equals(phone, StringComparison.OrdinalIgnoreCase)));
    }

    [RelayCommand]
    private void QuayLaiDanhSachKhachHang()
    {
        SelectedKhachHang = null;
        StatusMessage = "Đã quay lại danh sách khách hàng";
    }

    [RelayCommand]
    private async Task XoaKhachHangAsync()
    {
        if (SelectedKhachHang is null)
            return;

        var customer = SelectedKhachHang;
        var confirmed = await ShowConfirmationAsync(
            "Xóa khách hàng",
            $"Bạn có chắc muốn xóa khách hàng \"{customer.TenKhachHang}\" không? Khách hàng sẽ không còn hiển thị trong danh sách.");

        if (!confirmed)
            return;

        try
        {
            await _db.XoaKhachHangAsync(customer.Id);
            KhachHangs.Remove(customer);
            SaleCustomerSuggestions.Remove(customer);

            if (SelectedSaleCustomer?.Id == customer.Id)
                ChonKhachLe();

            SelectedKhachHang = null;
            StatusMessage = $"Đã xóa khách hàng {customer.TenKhachHang}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể xóa khách hàng: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task LuuKhachHangAsync()
    {
        if (SelectedKhachHang is null)
        {
            await ShowMessageAsync(
                "Thông báo",
                "Vui lòng tạo hoặc chọn khách hàng trước khi lưu.");
            return;
        }

        var customer = SelectedKhachHang;

        // Chỉ bắt buộc TÊN khách hàng
        if (string.IsNullOrWhiteSpace(customer.TenKhachHang))
        {
            await ShowMessageAsync(
                "Thiếu thông tin",
                "Vui lòng nhập tên khách hàng trước khi lưu.");
            return;
        }

        // Ngày sinh không bắt buộc.
        // Nếu có nhập thì phải đúng định dạng.
        if (!string.IsNullOrWhiteSpace(NgaySinhText))
        {
            if (!DateTime.TryParse(NgaySinhText, out var birthday))
            {
                await ShowMessageAsync(
                    "Thông tin không hợp lệ",
                    "Ngày sinh không hợp lệ.\nVui lòng nhập theo dạng dd/MM/yyyy.");
                return;
            }

            customer.NgaySinh = birthday;
        }
        else
        {
            customer.NgaySinh = null;
        }

        try
        {
            if (customer.Id == 0)
            {
                var newId = await _db.ThemKhachHangAsync(customer);

                customer.Id = newId;

                if (!KhachHangs.Contains(customer))
                    KhachHangs.Add(customer);

                SelectedKhachHang = customer;

                await ShowMessageAsync(
                    "Thành công",
                    $"Đã thêm khách hàng:\n{customer.TenKhachHang}");
            }
            else
            {
                await _db.CapNhatKhachHangAsync(customer);

                await ShowMessageAsync(
                    "Thành công",
                    $"Đã cập nhật khách hàng:\n{customer.TenKhachHang}");
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                "Lỗi",
                "Không thể lưu khách hàng:\n" + ex.Message);
        }
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
            "Nhà cung cấp" => LoadSuppliersAsync,
            "Nhập hàng" => null,
            _ => null
        };

        var loaded = menu switch
        {
            "Tổng quan" => _overviewLoaded,
            "Khách hàng" => _customersLoaded,
            "Đơn hàng" => _invoicesLoaded,
            "Nhà cung cấp" => _suppliersLoaded,
            "Nhập hàng" => true,
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
                case "Nhà cung cấp":
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

        try
        {
            // BẬT LOADING
            IsImporting = true;
            ImportStatus = $"Đang chuẩn bị nhập file \"{file.Name}\"...";
            StatusMessage = ImportStatus;

            // Cho giao diện có thời gian hiển thị loading
            await Task.Delay(100);

            ImportStatus = "Đang đọc dữ liệu từ file Excel...";
            StatusMessage = ImportStatus;

            var productsFromFile =
                _db.ParseProductsFromFile(file.Path.LocalPath);

            if (productsFromFile.Count == 0)
            {
                StatusMessage =
                    $"File \"{file.Name}\" không có dữ liệu sản phẩm hợp lệ để nhập.";
                return;
            }

            ImportStatus =
                $"Đã đọc {productsFromFile.Count:N0} sản phẩm. Đang kiểm tra dữ liệu trùng...";

            StatusMessage = ImportStatus;

            await Task.Delay(100);

            var existingProducts = await _db.GetSanPhamAsync();

            var duplicateProducts = productsFromFile
                .Where(product => IsDuplicateProduct(product, existingProducts))
                .Take(5)
                .ToList();

            if (duplicateProducts.Count > 0)
            {
                // TẮT LOADING trước khi hiện hộp thoại
                IsImporting = false;

                var shouldKeepExisting = await ShowConfirmationAsync(
                    "Sản phẩm trùng",
                    $"Phát hiện {duplicateProducts.Count} sản phẩm trong file đã tồn tại trong kho. " +
                    "Bạn muốn giữ dữ liệu hiện có và bỏ qua các sản phẩm trùng này?",
                    "Giữ dữ liệu cũ",
                    "Bỏ qua nhập file");

                if (!shouldKeepExisting)
                {
                    StatusMessage = "Đã bỏ qua nhập file vì phát hiện sản phẩm trùng.";
                    return;
                }

                // BẬT LẠI LOADING sau khi người dùng chọn
                IsImporting = true;
                ImportStatus = "Đang loại bỏ các sản phẩm bị trùng...";
                StatusMessage = ImportStatus;

                await Task.Delay(100);

                productsFromFile = productsFromFile
                    .Where(product => !IsDuplicateProduct(product, existingProducts))
                    .ToList();
            }

            if (productsFromFile.Count == 0)
            {
                StatusMessage =
                    "Tất cả sản phẩm trong file đều trùng với dữ liệu hiện có.";
                return;
            }

            ImportStatus =
                $"Đang nhập {productsFromFile.Count:N0} sản phẩm vào database...";

            StatusMessage = ImportStatus;

            await Task.Delay(100);

            var importedCount =
                await _db.ImportSanPhamTuFileAsync(
                    file.Path.LocalPath,
                    productsFromFile);

            ImportStatus = "Đang cập nhật lại danh sách hàng hóa...";
            StatusMessage = ImportStatus;

            await Task.Delay(100);

            await RefreshProductsAsync();

            StatusMessage = importedCount > 0
                ? $"Đã nhập thành công {importedCount:N0} sản phẩm từ file \"{file.Name}\"."
                : $"File \"{file.Name}\" không có dữ liệu sản phẩm hợp lệ để nhập.";
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Không thể nhập hàng loạt từ file: " + ex.Message;
        }
        finally
        {
            // LUÔN TẮT LOADING
            IsImporting = false;
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
    private async Task ShowMessageAsync(string title, string message)
    {
        if (OwnerWindow is null)
            return;

        var dialog = new Window
        {
            Title = title,
            Width = 430,
            Height = 230,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brushes.White
        };

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brush.Parse("#173F38"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 0, 12)
        };

        var messageText = new TextBlock
        {
            Text = message,
            FontSize = 15,
            Foreground = Avalonia.Media.Brush.Parse("#4F625D"),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            MaxWidth = 350
        };

        var okButton = new Button
        {
            Content = "Đã hiểu",
            Width = 110,
            Height = 38,
            Background = Avalonia.Media.Brush.Parse("#078C7A"),
            Foreground = Avalonia.Media.Brushes.White,
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 20, 0, 0),
            Padding = new Avalonia.Thickness(16, 0)
        };

        var panel = new StackPanel
        {
            Spacing = 0,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        panel.Children.Add(titleText);
        panel.Children.Add(messageText);
        panel.Children.Add(okButton);

        var border = new Border
        {
            Background = Avalonia.Media.Brushes.White,
            CornerRadius = new Avalonia.CornerRadius(12),
            Padding = new Avalonia.Thickness(25),
            Child = panel
        };

        dialog.Content = border;

        okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(OwnerWindow);
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
    public async Task SearchSaleCustomersAsync(string? search)
    {
        try
        {
            var keyword = search?.Trim() ?? string.Empty;

            SaleCustomerSuggestions.Clear();

            if (string.IsNullOrWhiteSpace(keyword) ||
                keyword.Equals("Khách lẻ", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var customers = await _db.GetKhachHangAsync(keyword);

            foreach (var customer in customers.Take(20))
            {
                SaleCustomerSuggestions.Add(customer);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tìm khách hàng: " + ex.Message;
        }
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
    private async Task ThemNhaCungCapAsync()
    {
        if (OwnerWindow is null)
            return;

        try
        {
            // Tự phát sinh mã NCC từ database
            var generatedCode = await _db.TaoMaNhaCungCapMoiAsync();

            var newSupplier = new NhaCungCap
            {
                MaNhaCungCap = generatedCode,
                TenNhaCungCap = string.Empty,
                TrangThai = true
            };

            // Mở giao diện nhập thông tin nhà cung cấp
            var editor = new NhaCungCapEditorWindow(newSupplier);

            var saved = await editor.ShowDialog<bool>(OwnerWindow);

            if (!saved)
                return;

            // Lưu database
            editor.Supplier.Id =
                await _db.ThemNhaCungCapAsync(editor.Supplier);

            // Thêm ngay vào danh sách giao diện
            NhaCungCaps.Add(editor.Supplier);

            _suppliersLoaded = true;

            StatusMessage =
                $"Đã thêm nhà cung cấp {editor.Supplier.TenNhaCungCap}";
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Không thể thêm nhà cung cấp: " + ex.Message;
        }
    }
    [RelayCommand]
    private async Task SuaNhaCungCapAsync(NhaCungCap? supplier)
    {
        if (supplier is null || OwnerWindow is null)
            return;

        var editor = new NhaCungCapEditorWindow(supplier);
        var saved = await editor.ShowDialog<bool>(OwnerWindow);

        SelectedNhaCungCap = null;

        if (!saved)
            return;

        try
        {
            await _db.CapNhatNhaCungCapAsync(editor.Supplier);

            var index = NhaCungCaps.IndexOf(supplier);

            if (index >= 0)
                NhaCungCaps[index] = editor.Supplier;

            StatusMessage =
                $"Đã cập nhật nhà cung cấp {editor.Supplier.TenNhaCungCap}";
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Không thể cập nhật nhà cung cấp: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ThemNhaCungCapTuFileAsync()
    {
        if (OwnerWindow is null || IsImporting)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn file danh sách nhà cung cấp",
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
            StatusMessage = "Bạn chưa chọn file nhà cung cấp.";
            return;
        }

        try
        {
            IsImporting = true;

            ImportStatus = $"Đang đọc file \"{file.Name}\"...";
            StatusMessage = ImportStatus;

            await Task.Yield();

            // Đọc file Excel/CSV
            var suppliersFromFile = await Task.Run(() =>
                _db.ParseNhaCungCapFromFile(file.Path.LocalPath));

            if (suppliersFromFile.Count == 0)
            {
                StatusMessage =
                    $"❌ File \"{file.Name}\" không có dữ liệu nhà cung cấp hợp lệ.";
                return;
            }

            ImportStatus =
                $"Đã đọc {suppliersFromFile.Count:N0} nhà cung cấp. Đang kiểm tra...";

            StatusMessage = ImportStatus;

            await Task.Yield();

            // Lấy danh sách hiện có trong database
            var existingSuppliers = await _db.GetNhaCungCapAsync();

            // Kiểm tra trùng mã nhà cung cấp
            var existingCodes = existingSuppliers
                .Where(x => !string.IsNullOrWhiteSpace(x.MaNhaCungCap))
                .Select(x => x.MaNhaCungCap.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var duplicateCount = suppliersFromFile.Count(x =>
                !string.IsNullOrWhiteSpace(x.MaNhaCungCap) &&
                existingCodes.Contains(x.MaNhaCungCap.Trim()));

            if (duplicateCount > 0)
            {
                // Tắt loading trước khi hiện hộp thoại
                IsImporting = false;

                var keepGoing = await ShowConfirmationAsync(
                    "Nhà cung cấp bị trùng",
                    $"Phát hiện {duplicateCount:N0} nhà cung cấp trong file " +
                    $"đã tồn tại trong database.\n\n" +
                    "Nếu tiếp tục, các mã nhà cung cấp bị trùng sẽ được bỏ qua. " +
                    "Các nhà cung cấp mới vẫn được nhập.",
                    "Tiếp tục nhập",
                    "Hủy");

                if (!keepGoing)
                {
                    StatusMessage = "Đã hủy nhập file nhà cung cấp.";
                    return;
                }

                IsImporting = true;

                ImportStatus = "Đang loại bỏ dữ liệu nhà cung cấp bị trùng...";
                StatusMessage = ImportStatus;

                await Task.Yield();

                suppliersFromFile = suppliersFromFile
                    .Where(x =>
                        string.IsNullOrWhiteSpace(x.MaNhaCungCap) ||
                        !existingCodes.Contains(x.MaNhaCungCap.Trim()))
                    .ToList();
            }

            if (suppliersFromFile.Count == 0)
            {
                StatusMessage =
                    "❌ Tất cả nhà cung cấp trong file đều đã tồn tại.";
                return;
            }

            ImportStatus =
                $"Đang nhập {suppliersFromFile.Count:N0} nhà cung cấp vào database...";

            StatusMessage = ImportStatus;

            await Task.Yield();

            var importedCount = await Task.Run(() =>
                _db.ImportNhaCungCapTuFileAsync(
                    file.Path.LocalPath,
                    suppliersFromFile));

            ImportStatus = "Đang cập nhật danh sách nhà cung cấp...";
            StatusMessage = ImportStatus;

            await LoadSuppliersAsync();

            _suppliersLoaded = true;

            StatusMessage =
                importedCount > 0
                    ? $"✅ Đã nhập thành công {importedCount:N0} nhà cung cấp từ file \"{file.Name}\"."
                    : $"⚠️ Không có nhà cung cấp mới nào được nhập.";
        }
        catch (Exception ex)
        {
            StatusMessage =
                "❌ Không thể nhập nhà cung cấp từ file: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private async Task XoaNhaCungCapAsync(NhaCungCap? supplier)
    {
        if (supplier is null)
            return;

        if (supplier.Id == 0)
        {
            StatusMessage =
                "Nhà cung cấp này chưa có ID trong database.";

            return;
        }

        var confirmed = await ShowConfirmationAsync(
            "Xóa nhà cung cấp",
            $"Bạn có chắc muốn xóa nhà cung cấp " +
            $"\"{supplier.TenNhaCungCap}\" không?");

        if (!confirmed)
            return;

        try
        {
            await _db.XoaNhaCungCapAsync(supplier.Id);

            NhaCungCaps.Remove(supplier);
            SelectedNhaCungCap = null;

            StatusMessage =
                $"Đã xóa nhà cung cấp {supplier.TenNhaCungCap}";
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Không thể xóa nhà cung cấp: " + ex.Message;
        }
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

        TienKhachDua = 0;
        TienThoi = 0;
        TienCongNo = 0;
        TrangThaiThanhToan = "";
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
    private async Task ThemVaoGioHangAsync(SanPham? product)
    {
        if (product is null)
            return;

        // Sản phẩm chưa có giá bán
        if (product.GiaBan <= 0)
        {
            if (OwnerWindow is null)
            {
                StatusMessage = "Không xác định được cửa sổ bán hàng.";
                return;
            }

            try
            {
                var window = new NhapGiaBanWindow();

                var giaBan = await window.ShowDialog<decimal?>(OwnerWindow);

                // Người dùng bấm Hủy hoặc nhập giá không hợp lệ
                if (!giaBan.HasValue || giaBan.Value <= 0)
                {
                    StatusMessage = "Chưa nhập giá bán. Sản phẩm chưa được thêm vào giỏ.";
                    return;
                }

                // Lưu giá bán vào database
                await _db.CapNhatGiaBanSanPhamAsync(
                    product.Id,
                    giaBan.Value);

                // Cập nhật giá của sản phẩm đang hiển thị
                product.GiaBan = giaBan.Value;
            }
            catch (Exception ex)
            {
                StatusMessage = "Không thể cập nhật giá bán: " + ex.Message;
                return;
            }
        }

        // Có giá rồi thì thêm vào giỏ
        var existing = SaleCart.FirstOrDefault(
            item => item.SanPham.Id == product.Id);

        if (existing is not null)
        {
            existing.SoLuong += 1;
        }
        else
        {
            var item = new SaleCartItem
            {
                SanPham = product,
                SoLuong = 1
            };

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

        SaleTotal = Math.Max(
            0,
            SaleSubtotal - SaleDiscountAmount);

        RecalculatePayment();
    }
    partial void OnTienKhachDuaChanged(decimal value)
    {
        RecalculatePayment();
    }
    private void RecalculatePayment()
    {
        var canTra = Math.Max(0, SaleTotal);
        var khachDua = Math.Max(0, TienKhachDua);

        if (khachDua > canTra)
        {
            TienThoi = khachDua - canTra;
            TienCongNo = 0;
            TrangThaiThanhToan = "Đã thanh toán đủ - Tiền thối";
        }
        else if (khachDua < canTra)
        {
            TienThoi = 0;
            TienCongNo = canTra - khachDua;

            if (khachDua == 0)
                TrangThaiThanhToan = "Chưa thanh toán - Công nợ";
            else
                TrangThaiThanhToan = "Thanh toán thiếu - Công nợ";
        }
        else
        {
            TienThoi = 0;
            TienCongNo = 0;
            TrangThaiThanhToan = "Đã thanh toán đủ";
        }
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
    SaleDiscountAmount,
    TienKhachDua);
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
            "Nhà cung cấp" => 3,
            "Nhập hàng" => 4,
            _ => 0
        };

        OnPropertyChanged(nameof(IsTongQuanSelected));
        OnPropertyChanged(nameof(IsDataSectionSelected));

        _ = LoadSelectedSectionAsync(value);
    }

    public bool IsTongQuanSelected => SelectedMenu == "Tổng quan";

    public bool IsDataSectionSelected => !IsTongQuanSelected;

}
