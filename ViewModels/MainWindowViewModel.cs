using System.Collections.ObjectModel;
using System.Threading;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;
using Avalonia.Controls;
using System.Globalization;

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
    private CancellationTokenSource? _baoCaoPhieuNhapSearchCts;
    private int _baoCaoPhieuNhapLoadVersion;
    private bool _baoCaoPhieuNhapLoaded;

    [ObservableProperty] private string selectedMenu = "Tổng quan";
    [ObservableProperty] private int selectedDataTabIndex = 0;
    [ObservableProperty] private string selectedReportKey = "";
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
        try
        {
            switch (menu)
            {
                case "Tổng quan":
                    await LoadOverviewAsync();
                    _overviewLoaded = true;
                    break;

                case "Khách hàng":
                    await LoadCustomersAsync();
                    _customersLoaded = true;
                    break;

                case "Hóa đơn":
                    // Mỗi lần vào Hóa đơn đều đọc lại database
                    await LoadInvoicesAsync();
                    _invoicesLoaded = true;
                    break;

                case "Nhà cung cấp":
                    await LoadSuppliersAsync();
                    _suppliersLoaded = true;
                    break;

                case "Nhập hàng":
                    // Mỗi lần bấm menu "Nhập hàng" phải khởi tạo phiếu mới:
                    // sinh mã phiếu, gán nhân viên/ngày/trạng thái và nạp danh sách hàng hóa.
                    // Thiếu bước này thì PhieuNhapMa rỗng => lưu phiếu sẽ lỗi
                    // "Duplicate entry '' for key 'uk_pnh_ma'".
                    await MoPhieuNhapHangAsync();
                    break;

                case "Sổ quỹ":
                    await LoadSoQuyPhieuAsync();
                    await LoadSoQuyTongHopAsync();
                    break;

                case "Báo cáo":
                    // Báo cáo chưa chọn mục nào thì mở mặc định báo cáo phiếu nhập
                    if (string.IsNullOrEmpty(SelectedReportKey))
                        SelectedReportKey = BaoCaoMenuItems.FirstOrDefault()?.Key ?? "";

                    // Chỉ tải lần đầu khi vào màn, các lần sau giữ nguyên trang/tìm kiếm đang xem
                    if (SelectedReportKey == "phieu-nhap-hang" && !_baoCaoPhieuNhapLoaded)
                    {
                        _baoCaoPhieuNhapLoaded = true;
                        await LoadBaoCaoPhieuNhapAsync();
                    }

                    break;
            }

            StatusMessage = $"Đã tải {menu.ToLowerInvariant()}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể tải {menu}: {ex.Message}";
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
        try
        {
            var invoices = await _db.GetHoaDonAsync();

            HoaDons.Clear();

            foreach (var item in invoices)
                HoaDons.Add(item);

            StatusMessage = $"Đã tải {HoaDons.Count} hóa đơn";
        }
        catch (Exception ex)
        {
            HoaDons.Clear();

            StatusMessage = $"Lỗi tải hóa đơn: {ex.Message}";
        }
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
    private async Task ClearInvoiceSearchAsync()
    {
        InvoiceSearch = "";

        try
        {
            HoaDons.Clear();

            foreach (var item in await _db.GetHoaDonAsync())
                HoaDons.Add(item);

            StatusMessage = $"Đã tải {HoaDons.Count} hóa đơn";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải hóa đơn: " + ex.Message;
        }
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
        var supplier = await HienThiFormThemNhaCungCapAsync();

        if (supplier is null)
            return;

        StatusMessage =
            $"Đã thêm nhà cung cấp {supplier.TenNhaCungCap}";
    }

    /// <summary>
    /// Mở form thêm nhà cung cấp dùng chung cho cả module quản lý
    /// và màn lập phiếu nhập. Trả về nhà cung cấp vừa lưu, null nếu người dùng hủy.
    /// </summary>
    private async Task<NhaCungCap?> HienThiFormThemNhaCungCapAsync()
    {
        if (OwnerWindow is null)
            return null;

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
            return null;

        // Lưu database
        editor.Supplier.Id =
            await _db.ThemNhaCungCapAsync(editor.Supplier);

        // Thêm ngay vào danh sách giao diện
        NhaCungCaps.Add(editor.Supplier);

        _suppliersLoaded = true;

        return editor.Supplier;
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
    private async Task MoGiaoDienBanHangAsync()
    {
        IsSalesPanelVisible = true;
        SaleCustomerName = "Khách lẻ";
        SaleSearchText = "";
        SaleCart.Clear();

        ProductSearchResults.Clear();

        // TẢI DANH SÁCH SẢN PHẨM KHI MỞ BÁN HÀNG
        try
        {
            var products = await _db.GetSanPhamAsync();

            foreach (var product in products)
                ProductSearchResults.Add(product);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải danh sách sản phẩm: " + ex.Message;
        }

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

    // ============================================================
    // BÁO CÁO
    // ============================================================

    /// <summary>
    /// Danh sách các báo cáo trong nhóm menu "Báo cáo".
    /// Muốn thêm báo cáo mới (bán hàng, tồn kho, doanh thu...) chỉ cần
    /// thêm một phần tử nữa vào đây, giao diện tự hiển thị theo.
    /// </summary>
    public ObservableCollection<BaoCaoMenuItem> BaoCaoMenuItems { get; } = new()
    {
        new BaoCaoMenuItem
        {
            Key = "phieu-nhap-hang",
            TieuDe = "Báo cáo phiếu nhập hàng",
            BieuTuong = "📥"
        }
    };

    public ObservableCollection<PhieuNhapHang> BaoCaoPhieuNhaps { get; } = new();

    [ObservableProperty] private string baoCaoPhieuNhapSearch = "";
    [ObservableProperty] private int baoCaoPhieuNhapPage = 1;
    [ObservableProperty] private int baoCaoPhieuNhapPageSize = 15;
    [ObservableProperty] private int baoCaoPhieuNhapTotalCount;
    [ObservableProperty] private bool isLoadingBaoCaoPhieuNhap;

    public int BaoCaoPhieuNhapTotalPages =>
        BaoCaoPhieuNhapPageSize <= 0
            ? 0
            : (int)Math.Ceiling(BaoCaoPhieuNhapTotalCount / (double)BaoCaoPhieuNhapPageSize);

    /// <summary>Ví dụ: "1 - 15 trong 367 giao dịch"</summary>
    public string BaoCaoPhieuNhapRangeText
    {
        get
        {
            if (BaoCaoPhieuNhapTotalCount == 0)
                return "0 giao dịch";

            var from = (BaoCaoPhieuNhapPage - 1) * BaoCaoPhieuNhapPageSize + 1;
            var to = Math.Min(from + BaoCaoPhieuNhapPageSize - 1, BaoCaoPhieuNhapTotalCount);

            return $"{from} - {to} trong {BaoCaoPhieuNhapTotalCount} giao dịch";
        }
    }

    public bool HasBaoCaoPhieuNhapData => BaoCaoPhieuNhaps.Count > 0;

    public bool IsBaoCaoPhieuNhapEmpty => BaoCaoPhieuNhaps.Count == 0;

    /// <summary>Trạng thái ô "chọn tất cả" ở đầu bảng: đã chọn / chọn một phần / chưa chọn.</summary>
    public bool? IsBaoCaoPhieuNhapAllSelected
    {
        get
        {
            if (BaoCaoPhieuNhaps.Count == 0)
                return false;

            var soDaChon = BaoCaoPhieuNhaps.Count(x => x.IsSelected);

            if (soDaChon == 0)
                return false;

            return soDaChon == BaoCaoPhieuNhaps.Count ? true : null;
        }
    }

    public int BaoCaoPhieuNhapSelectedCount => BaoCaoPhieuNhaps.Count(x => x.IsSelected);

    public bool CanBaoCaoPhieuNhapTrangTruoc => BaoCaoPhieuNhapPage > 1;

    public bool CanBaoCaoPhieuNhapTrangSau => BaoCaoPhieuNhapPage < BaoCaoPhieuNhapTotalPages;

    public bool IsBaoCaoSectionSelected => SelectedMenu == "Báo cáo";

    public bool IsBaoCaoPhieuNhapSelected =>
        SelectedMenu == "Báo cáo" && SelectedReportKey == "phieu-nhap-hang";

    partial void OnBaoCaoPhieuNhapSearchChanged(string value)
    {
        // Mỗi lần tìm kiếm luôn quay về trang đầu
        if (BaoCaoPhieuNhapPage != 1)
            BaoCaoPhieuNhapPage = 1;
    }

    partial void OnBaoCaoPhieuNhapPageChanged(int value)
    {
        OnPropertyChanged(nameof(BaoCaoPhieuNhapTotalPages));
        OnPropertyChanged(nameof(BaoCaoPhieuNhapRangeText));
        OnPropertyChanged(nameof(CanBaoCaoPhieuNhapTrangTruoc));
        OnPropertyChanged(nameof(CanBaoCaoPhieuNhapTrangSau));
    }

    partial void OnBaoCaoPhieuNhapPageSizeChanged(int value)
    {
        if (BaoCaoPhieuNhapPage != 1)
            BaoCaoPhieuNhapPage = 1;
    }

    partial void OnBaoCaoPhieuNhapTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(BaoCaoPhieuNhapTotalPages));
        OnPropertyChanged(nameof(BaoCaoPhieuNhapRangeText));
    }

    partial void OnSelectedReportKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapSelected));
    }

    [RelayCommand]
    private void ChonBaoCao(BaoCaoMenuItem? item)
    {
        if (item is null)
            return;

        SelectedMenu = "Báo cáo";
        SelectedReportKey = item.Key;

        // Mỗi báo cáo có danh sách riêng
        BaoCaoPhieuNhapPage = 1;

        if (item.Key == "phieu-nhap-hang" && !_baoCaoPhieuNhapLoaded)
        {
            _baoCaoPhieuNhapLoaded = true;
            _ = LoadBaoCaoPhieuNhapAsync();
        }

        StatusMessage = item.TieuDe;
    }

    [RelayCommand]
    private async Task LoadBaoCaoPhieuNhapAsync()
    {
        if (IsLoadingBaoCaoPhieuNhap)
            return;

        IsLoadingBaoCaoPhieuNhap = true;

        // Version guard: bỏ qua kết quả của lần tải cũ nếu người dùng đã tìm/chuyển trang mới
        var version = ++_baoCaoPhieuNhapLoadVersion;

        try
        {
            var page = BaoCaoPhieuNhapPage;
            var (items, tongSoBanGhi) = await _db.GetPhieuNhapHangPagedAsync(
                BaoCaoPhieuNhapSearch,
                page,
                BaoCaoPhieuNhapPageSize);

            if (version != _baoCaoPhieuNhapLoadVersion)
                return;

            // Trang vượt quá số trang thực tế (ví dụ sau khi tìm kiếm) -> quay về trang cuối
            var totalPages = BaoCaoPhieuNhapPageSize <= 0
                ? 0
                : (int)Math.Ceiling(tongSoBanGhi / (double)BaoCaoPhieuNhapPageSize);

            if (items.Count == 0 && totalPages > 0 && page > totalPages)
            {
                BaoCaoPhieuNhapPage = totalPages;
                IsLoadingBaoCaoPhieuNhap = false;
                await LoadBaoCaoPhieuNhapAsync();
                return;
            }

            BaoCaoPhieuNhaps.Clear();

            foreach (var item in items)
                BaoCaoPhieuNhaps.Add(item);

            BaoCaoPhieuNhapTotalCount = tongSoBanGhi;

            OnPropertyChanged(nameof(HasBaoCaoPhieuNhapData));
            OnPropertyChanged(nameof(IsBaoCaoPhieuNhapEmpty));
            OnPropertyChanged(nameof(BaoCaoPhieuNhapRangeText));
            OnPropertyChanged(nameof(CanBaoCaoPhieuNhapTrangTruoc));
            OnPropertyChanged(nameof(CanBaoCaoPhieuNhapTrangSau));
            OnPropertyChanged(nameof(IsBaoCaoPhieuNhapAllSelected));
            OnPropertyChanged(nameof(BaoCaoPhieuNhapSelectedCount));

            StatusMessage = BaoCaoPhieuNhaps.Count == 0
                ? "Không có dữ liệu phiếu nhập phù hợp"
                : $"Đã tải {BaoCaoPhieuNhaps.Count} phiếu nhập (trang {BaoCaoPhieuNhapPage}/{Math.Max(1, totalPages)})";
        }
        catch (Exception ex)
        {
            if (version == _baoCaoPhieuNhapLoadVersion)
                StatusMessage = "Không thể tải báo cáo phiếu nhập: " + ex.Message;
        }
        finally
        {
            if (version == _baoCaoPhieuNhapLoadVersion)
                IsLoadingBaoCaoPhieuNhap = false;
        }
    }

    [RelayCommand]
    private async Task TimKiemBaoCaoPhieuNhapAsync()
    {
        BaoCaoPhieuNhapPage = 1;
        await LoadBaoCaoPhieuNhapAsync();
    }

    /// <summary>
    /// Tìm kiếm khi người dùng gõ: chống debounce để không gọi database liên tục.
    /// </summary>
    public void TimKiemBaoCaoPhieuNhapTuDong()
    {
        _baoCaoPhieuNhapSearchCts?.Cancel();
        _baoCaoPhieuNhapSearchCts?.Dispose();
        _baoCaoPhieuNhapSearchCts = new CancellationTokenSource();
        var token = _baoCaoPhieuNhapSearchCts.Token;

        _ = DebounceBaoCaoPhieuNhapAsync(token);
    }

    private async Task DebounceBaoCaoPhieuNhapAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);

            if (token.IsCancellationRequested)
                return;

            await TimKiemBaoCaoPhieuNhapAsync();
        }
        catch (OperationCanceledException)
        {
            // Người dùng gõ tiếp -> huỷ lần tìm kiếm cũ
        }
    }

    [RelayCommand]
    private async Task BaoCaoPhieuNhapTrangDauAsync()
    {
        BaoCaoPhieuNhapPage = 1;
        await LoadBaoCaoPhieuNhapAsync();
    }

    [RelayCommand]
    private async Task BaoCaoPhieuNhapTrangCuoiAsync()
    {
        BaoCaoPhieuNhapPage = Math.Max(1, BaoCaoPhieuNhapTotalPages);
        await LoadBaoCaoPhieuNhapAsync();
    }

    [RelayCommand]
    private async Task BaoCaoPhieuNhapTrangTruocAsync()
    {
        if (BaoCaoPhieuNhapPage > 1)
            BaoCaoPhieuNhapPage--;

        await LoadBaoCaoPhieuNhapAsync();
    }

    [RelayCommand]
    private async Task BaoCaoPhieuNhapTrangSauAsync()
    {
        if (BaoCaoPhieuNhapPage < BaoCaoPhieuNhapTotalPages)
            BaoCaoPhieuNhapPage++;

        await LoadBaoCaoPhieuNhapAsync();
    }

    [RelayCommand]
    private void ChonTatCaBaoCaoPhieuNhap(bool? isChecked)
    {
        foreach (var item in BaoCaoPhieuNhaps)
            item.IsSelected = isChecked == true;

        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapAllSelected));
        OnPropertyChanged(nameof(BaoCaoPhieuNhapSelectedCount));
    }

    [RelayCommand]
    private static void BatYeuThichBaoCaoPhieuNhap(PhieuNhapHang? item)
    {
        if (item is null)
            return;

        item.IsYeuThich = !item.IsYeuThich;
    }

    /// <summary>Đồng bộ lại trạng thái ô "chọn tất cả" khi người dùng bấm checkbox của một dòng.</summary>
    public void CapNhatTrangThaiChonTatCaBaoCaoPhieuNhap()
    {
        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapAllSelected));
        OnPropertyChanged(nameof(BaoCaoPhieuNhapSelectedCount));
    }

    [RelayCommand]
    private async Task MoGiaoDienNhapHangTuBaoCaoAsync()
    {
        // Nút "+ Nhập hàng" trong báo cáo: chuyển sang màn lập phiếu nhập hàng
        await MoGiaoDienNhapHangAsync();
    }

    // ============================================================
    // PHIẾU NHẬP HÀNG
    // ============================================================

    public ObservableCollection<ChiTietPhieuNhapHang> PhieuNhapItems { get; } = new();
    public ObservableCollection<SanPham> PhieuNhapProductSuggestions { get; } = new();
    public ObservableCollection<NhaCungCap> PhieuNhapSupplierSuggestions { get; } = new();

    [ObservableProperty] private bool isNhapHangFormVisible;
    [ObservableProperty] private string importProductSearch = "";
    [ObservableProperty] private string importSupplierSearch = "";
    [ObservableProperty] private string phieuNhapMa = "";
    [ObservableProperty] private string phieuNhapSoHoaDonDauVao = "";
    [ObservableProperty] private string phieuNhapGhiChu = "";
    [ObservableProperty] private string phieuNhapNhanVien = "";
    [ObservableProperty] private NhaCungCap? selectedPhieuNhapNcc;
    [ObservableProperty] private DateTime? phieuNhapNgayNhap = DateTime.Now;
    [ObservableProperty] private decimal phieuNhapTongTienHang;
    [ObservableProperty] private decimal phieuNhapChiPhiKhac;
    [ObservableProperty] private decimal phieuNhapTongTienNhap;
    [ObservableProperty] private decimal phieuNhapDaThanhToan;

    [ObservableProperty] private bool isSavingPhieuNhap;

    /// <summary>Ngày nhập luôn dùng kiểu DateTime vì cột thoi_gian_nhap trong database là DATETIME.</summary>
    [ObservableProperty] private bool isPhieuNhapCalendarOpen;

    private uint _phieuNhapId;

    public string PhieuNhapTongTienHangText => PhieuNhapTongTienHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string PhieuNhapTongTienNhapText => PhieuNhapTongTienNhap.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Ngày nhập hiển thị theo định dạng Việt Nam dd/MM/yyyy HH:mm.</summary>
    public string PhieuNhapNgayNhapText => PhieuNhapNgayNhap?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                                           ?? string.Empty;

    /// <summary>Công nợ còn phải trả cho nhà cung cấp, khớp với cột con_no trong database.</summary>
    public decimal PhieuNhapConNo => Math.Max(0, PhieuNhapTongTienNhap - PhieuNhapDaThanhToan);

    public string PhieuNhapConNoText => PhieuNhapConNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public bool HasPhieuNhapItems => PhieuNhapItems.Count > 0;

    public bool IsPhieuNhapEmpty => PhieuNhapItems.Count == 0;

    public bool HasPhieuNhapSuggestions => PhieuNhapProductSuggestions.Count > 0;

    public bool HasPhieuNhapSupplierSuggestions => PhieuNhapSupplierSuggestions.Count > 0;

    public bool IsNhapHangListVisible => !IsNhapHangFormVisible;

    public bool CanSavePhieuNhap => !IsSavingPhieuNhap;

    partial void OnIsNhapHangFormVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsNhapHangListVisible));

    partial void OnPhieuNhapTongTienHangChanged(decimal value)
    {
        RecalculatePhieuNhapTotal();

        OnPropertyChanged(nameof(PhieuNhapTongTienHangText));
        OnPropertyChanged(nameof(PhieuNhapTongTienNhapText));
    }

    partial void OnPhieuNhapTongTienNhapChanged(decimal value)
    {
        OnPropertyChanged(nameof(PhieuNhapTongTienNhapText));
        OnPropertyChanged(nameof(PhieuNhapConNoText));
    }

    partial void OnPhieuNhapChiPhiKhacChanged(decimal value) => RecalculatePhieuNhapTotal();

    partial void OnPhieuNhapDaThanhToanChanged(decimal value) =>
        OnPropertyChanged(nameof(PhieuNhapConNoText));

    partial void OnIsSavingPhieuNhapChanged(bool value) => OnPropertyChanged(nameof(CanSavePhieuNhap));

    partial void OnPhieuNhapNgayNhapChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(PhieuNhapNgayNhapText));

        // Chọn xong ngày thì đóng lịch luôn
        IsPhieuNhapCalendarOpen = false;
    }

    [RelayCommand]
    private void MoLichPhieuNhap() => IsPhieuNhapCalendarOpen = !IsPhieuNhapCalendarOpen;

    private void RecalculatePhieuNhapTotal()
    {
        PhieuNhapTongTienNhap = PhieuNhapTongTienHang + PhieuNhapChiPhiKhac;
    }

    [RelayCommand]
    private async Task MoPhieuNhapHangAsync()
    {
        IsNhapHangFormVisible = true;

        _phieuNhapId = 0;

        BoDangKyPhieuNhapItems();

        PhieuNhapItems.Clear();
        PhieuNhapProductSuggestions.Clear();
        PhieuNhapSupplierSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));
        PhieuNhapSoHoaDonDauVao = "";
        PhieuNhapGhiChu = "";
        PhieuNhapChiPhiKhac = 0;
        PhieuNhapDaThanhToan = 0;
        ImportProductSearch = "";
        ImportSupplierSearch = "";
        SelectedPhieuNhapNcc = null;

        PhieuNhapNhanVien = Session.CurrentUser?.HoTen ?? "Chưa đăng nhập";
        PhieuNhapNgayNhap = DateTime.Now;

        try
        {
            PhieuNhapMa = await _db.TaoMaPhieuNhapMoiAsync();
        }
        catch (Exception ex)
        {
            // Không nuốt lỗi: in ra console/debug để thấy đúng lỗi SQL/connection.
            // Tuyệt đối không gán chuỗi tạm vào PhieuNhapMa vì màn hình sẽ lưu
            // thẳng giá trị này xuống phieu_nhap_hang (làm hỏng dữ liệu mã phiếu).
            Console.WriteLine("========== LỖI TẠO MÃ PHIẾU NHẬP ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("==========================================");

            PhieuNhapMa = "";
            StatusMessage =
                "Không thể tạo mã phiếu nhập tự động: " + ex.Message
                + " (xem console để biết chi tiết lỗi SQL/kết nối)";
        }

        RecalculatePhieuNhapTongTienHang();

        await LoadPhieuNhapProductSuggestionsAsync();
    }

    [RelayCommand]
    private async Task MoGiaoDienNhapHangAsync()
    {
        // Nếu đang đứng ở màn Nhập hàng thì SelectedMenu không đổi,
        // LoadSelectedSectionAsync sẽ không chạy nên phải tự khởi tạo phiếu mới.
        if (SelectedMenu == "Nhập hàng")
        {
            await MoPhieuNhapHangAsync();
            return;
        }

        // SelectedMenu đổi => case "Nhập hàng" trong LoadSelectedSectionAsync gọi
        // MoPhieuNhapHangAsync() giúp, không gọi ở đây để tránh chạy 2 lần.
        SelectedMenu = "Nhập hàng";
    }

    [RelayCommand]
    private async Task DongPhieuNhapHangAsync()
    {
        if (PhieuNhapItems.Count > 0)
        {
            var confirmed = await ShowConfirmationAsync(
                "Thoát lập phiếu nhập",
                "Phiếu nhập đang có dữ liệu chưa được lưu. Bạn có chắc muốn thoát?",
                "Thoát",
                "Tiếp tục nhập");

            if (!confirmed)
                return;
        }

        BoDangKyPhieuNhapItems();

        RecalculatePhieuNhapTongTienHang();
        OnPropertyChanged(nameof(HasPhieuNhapSuggestions));

        _phieuNhapId = 0;

        IsNhapHangFormVisible = false;
    }

    private void BoDangKyPhieuNhapItems()
    {
        foreach (var item in PhieuNhapItems)
            item.PropertyChanged -= ChiTietPhieuNhapHang_OnPropertyChanged;
    }

    [RelayCommand]
    private async Task TimKiemSanPhamNhapAsync(string? keyword)
    {
        // Không gán đè khi lệnh được gọi không có tham số,
        // nếu không TextBox sẽ bị xoá ngay khi vừa gõ ký tự đầu tiên.
        if (keyword is not null && keyword != ImportProductSearch)
            ImportProductSearch = keyword;

        try
        {
            await LoadPhieuNhapProductSuggestionsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tìm hàng hóa: " + ex.Message;
        }
    }

    private async Task LoadPhieuNhapProductSuggestionsAsync()
    {
        var keyword = ImportProductSearch.Trim();

        // Rút gọn danh sách gợi ý: sản phẩm đã có trong phiếu thì ẩn đi
        var sanPhamDaChon = PhieuNhapItems
            .Select(item => item.SanPham.Id)
            .ToHashSet();

        var products = await _db.GetSanPhamAsync(keyword);

        PhieuNhapProductSuggestions.Clear();

        foreach (var product in products
                     .Where(product => !sanPhamDaChon.Contains(product.Id))
                     .Take(20))
        {
            PhieuNhapProductSuggestions.Add(product);
        }

        OnPropertyChanged(nameof(HasPhieuNhapSuggestions));
    }

    [RelayCommand]
    private void ThemSanPhamVaoPhieuNhap(SanPham? product)
    {
        if (product is null)
            return;

        var existing = PhieuNhapItems
            .FirstOrDefault(item => item.SanPham.Id == product.Id);

        if (existing is not null)
        {
            existing.SoLuong += 1;
            RecalculatePhieuNhapTongTienHang();
            StatusMessage = $"Đã tăng số lượng {product.TenSanPham} lên {existing.SoLuong:N0}";
            return;
        }

        var dongMoi = new ChiTietPhieuNhapHang(product);

        // Nghe thay đổi số lượng / giá nhập / giảm giá để tính lại tổng realtime
        dongMoi.PropertyChanged += ChiTietPhieuNhapHang_OnPropertyChanged;

        PhieuNhapItems.Add(dongMoi);

        PhieuNhapProductSuggestions.Remove(product);

        OnPropertyChanged(nameof(HasPhieuNhapSuggestions));

        RecalculatePhieuNhapTongTienHang();

        StatusMessage = $"Đã thêm {product.TenSanPham} vào phiếu nhập";
    }

    private void ChiTietPhieuNhapHang_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChiTietPhieuNhapHang.SoLuong)
            or nameof(ChiTietPhieuNhapHang.DonGiaNhap)
            or nameof(ChiTietPhieuNhapHang.GiamGia)
            or nameof(ChiTietPhieuNhapHang.ThanhTien))
        {
            RecalculatePhieuNhapTongTienHang();
        }
    }

    [RelayCommand]
    private void XoaDongPhieuNhap(ChiTietPhieuNhapHang? item)
    {
        if (item is null)
            return;

        item.PropertyChanged -= ChiTietPhieuNhapHang_OnPropertyChanged;

        PhieuNhapItems.Remove(item);

        RecalculatePhieuNhapTongTienHang();
    }

    [RelayCommand]
    private async Task XoaTatPhieuNhapAsync()
    {
        if (PhieuNhapItems.Count == 0)
        {
            StatusMessage = "Phiếu nhập không có dòng hàng nào để xóa.";
            return;
        }

        var soDong = PhieuNhapItems.Count;

        var confirmed = await ShowConfirmationAsync(
            "Xóa tất cả dòng hàng",
            $"Bạn có chắc muốn xóa tất cả hàng hóa trong phiếu nhập? ({soDong} dòng sẽ bị xóa khỏi phiếu, sản phẩm trong database không bị ảnh hưởng)",
            "Xóa tất cả",
            "Hủy");

        if (!confirmed)
        {
            StatusMessage = "Đã hủy xóa tất cả dòng hàng.";
            return;
        }

        BoDangKyPhieuNhapItems();
        PhieuNhapItems.Clear();

        // Tổng tiền hàng = 0, Tổng tiền nhập = Chi phí khác, Còn phải trả = Tổng tiền nhập - Đã thanh toán
        RecalculatePhieuNhapTongTienHang();
        RecalculatePhieuNhapTotal();

        StatusMessage = $"Đã xóa toàn bộ {soDong} dòng hàng khỏi phiếu nhập";
    }

    [RelayCommand]
    private void TangSoLuongNhap(ChiTietPhieuNhapHang? item)
    {
        if (item is null)
            return;

        item.SoLuong += 1;
        RecalculatePhieuNhapTongTienHang();
    }

    [RelayCommand]
    private void GiamSoLuongNhap(ChiTietPhieuNhapHang? item)
    {
        if (item is null)
            return;

        item.SoLuong = Math.Max(0, item.SoLuong - 1);
        RecalculatePhieuNhapTongTienHang();
    }

    private void RecalculatePhieuNhapTongTienHang()
    {
        PhieuNhapTongTienHang = PhieuNhapItems.Sum(item => item.ThanhTien);

        for (var i = 0; i < PhieuNhapItems.Count; i++)
            PhieuNhapItems[i].Index = i + 1;

        OnPropertyChanged(nameof(HasPhieuNhapItems));
        OnPropertyChanged(nameof(IsPhieuNhapEmpty));
    }

    /// <summary>
    /// Ô tìm nhà cung cấp đang chứa đúng tên NCC vừa chọn
    /// thì không cần tìm lại lần nữa.
    /// </summary>
    private bool DaChonNhaCungCapNhap =>
        SelectedPhieuNhapNcc is not null &&
        ImportSupplierSearch.Trim()
            .Equals(SelectedPhieuNhapNcc.TenNhaCungCap, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private async Task TimKiemNhaCungCapNhapAsync(string? keyword)
    {
        // Không gán đè khi lệnh được gọi không có tham số,
        // nếu không TextBox sẽ bị xoá ngay khi vừa gõ ký tự đầu tiên.
        if (keyword is not null && keyword != ImportSupplierSearch)
            ImportSupplierSearch = keyword;

        try
        {
            PhieuNhapSupplierSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));

            if (string.IsNullOrWhiteSpace(ImportSupplierSearch))
                return;

            // Đã chọn xong NCC thì không tìm lại, tránh mở lại danh sách gợi ý
            if (DaChonNhaCungCapNhap)
                return;

            var suppliers = await _db.GetNhaCungCapAsync(ImportSupplierSearch);

            foreach (var supplier in suppliers.Take(20))
                PhieuNhapSupplierSuggestions.Add(supplier);
                OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tìm nhà cung cấp: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ChonNhaCungCapNhap(NhaCungCap? supplier)
    {
        if (supplier is null)
            return;

        SelectedPhieuNhapNcc = supplier;
        ImportSupplierSearch = supplier.TenNhaCungCap;

        PhieuNhapSupplierSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));

        StatusMessage = $"Đã chọn nhà cung cấp {supplier.TenNhaCungCap}";
    }

    [RelayCommand]
    private async Task ThemNhaCungCapNhapNhanhAsync()
    {
        try
        {
            // Tái sử dụng đúng form thêm nhà cung cấp sẵn có
            var supplier = await HienThiFormThemNhaCungCapAsync();

            if (supplier is null)
                return;

            // Tự động chọn nhà cung cấp vừa thêm cho phiếu nhập
            SelectedPhieuNhapNcc = supplier;
            ImportSupplierSearch = supplier.TenNhaCungCap;
            PhieuNhapSupplierSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));

            StatusMessage = $"Đã thêm và chọn nhà cung cấp {supplier.TenNhaCungCap}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể thêm nhà cung cấp: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task HoanThanhPhieuNhapAsync()
    {
        if (IsSavingPhieuNhap)
            return;

        // ------------------------------------------
        // VALIDATE
        // ------------------------------------------

        // 1. Phiếu phải có ít nhất 1 hàng hóa
        if (PhieuNhapItems.Count == 0)
        {
            StatusMessage = "Phiếu nhập chưa có hàng hóa.";
            return;
        }

        // 2. Số lượng mỗi hàng phải > 0
        var dongSoLuongSai = PhieuNhapItems
            .Where(item => item.SanPham.Id != 0 && item.SoLuong <= 0)
            .ToList();

        if (dongSoLuongSai.Count > 0)
        {
            StatusMessage = dongSoLuongSai.Count == 1
                ? $"Vui lòng kiểm tra số lượng của hàng \"{dongSoLuongSai[0].SanPham.TenSanPham}\" (phải lớn hơn 0)."
                : $"Vui lòng kiểm tra số lượng hàng hóa: còn {dongSoLuongSai.Count} dòng chưa nhập số lượng.";
            return;
        }

        // 3. Giá nhập phải hợp lệ (không âm)
        var dongGiaSai = PhieuNhapItems
            .Where(item => item.SanPham.Id != 0 && item.DonGiaNhap < 0)
            .ToList();

        if (dongGiaSai.Count > 0)
        {
            StatusMessage = dongGiaSai.Count == 1
                ? $"Vui lòng kiểm tra giá nhập của hàng \"{dongGiaSai[0].SanPham.TenSanPham}\"."
                : $"Vui lòng kiểm tra giá nhập: còn {dongGiaSai.Count} dòng có giá nhập không hợp lệ.";
            return;
        }

        // Chỉ lấy những dòng thực sự gắn với một sản phẩm trong database
        var dongHopLe = PhieuNhapItems
            .Where(item => item.SanPham.Id != 0 && item.SoLuong > 0)
            .ToList();

        if (dongHopLe.Count == 0)
        {
            StatusMessage = "Phiếu nhập chưa có hàng hóa hợp lệ nào để hoàn thành.";
            return;
        }

        // 4. Nhà cung cấp là trường bắt buộc khi hoàn thành phiếu
        if (SelectedPhieuNhapNcc is null || SelectedPhieuNhapNcc.Id == 0)
        {
            StatusMessage = "Vui lòng chọn nhà cung cấp.";
            return;
        }

        // 5. Ngày nhập hợp lệ
        if (PhieuNhapNgayNhap is null)
        {
            StatusMessage = "Vui lòng chọn ngày nhập.";
            return;
        }

        // 6. Mã phiếu phải sinh được trước khi lưu
        if (string.IsNullOrWhiteSpace(PhieuNhapMa))
        {
            StatusMessage = "Chưa tạo được mã phiếu nhập, vui lòng mở lại màn hình và thử lại.";
            return;
        }

        var confirmed = await ShowConfirmationAsync(
            "Hoàn thành phiếu nhập",
            "Tồn kho sẽ được cộng thêm số lượng vừa nhập và giá vốn được cập nhật. Bạn có chắc muốn tiếp tục?",
            "Hoàn thành",
            "Hủy");

        if (!confirmed)
            return;

        IsSavingPhieuNhap = true;

        try
        {
            var phieu = new PhieuNhapHang
            {
                Id = _phieuNhapId,
                MaPhieuNhap = PhieuNhapMa,
                NhaCungCapId = SelectedPhieuNhapNcc.Id,
                TenNhaCungCap = SelectedPhieuNhapNcc.TenNhaCungCap,
                ThoiGianNhap = PhieuNhapNgayNhap.Value,
                ChiPhiKhac = PhieuNhapChiPhiKhac,
                DaThanhToan = PhieuNhapDaThanhToan,
                GhiChu = PhieuNhapGhiChu,
                SoHoaDonDauVao = PhieuNhapSoHoaDonDauVao
            };

            // Lưu header + chi tiết + tồn kho + lịch sử trong một transaction.
            // Service tự rollback toàn bộ nếu bất kỳ bước nào lỗi.
            _phieuNhapId = await _db.LuuPhieuNhapHangAsync(phieu, dongHopLe);

            // Phiếu vừa lưu phải xuất hiện trong "Báo cáo → Báo cáo phiếu nhập hàng"
            // nên đánh dấu cần tải lại thay vì giữ cache cũ.
            _baoCaoPhieuNhapLoaded = false;

            // Tồn kho và giá vốn đã đổi -> nạp lại danh sách sản phẩm
            await LoadProductsAsync();

            StatusMessage =
                $"✅ Đã hoàn thành phiếu nhập {PhieuNhapMa} - tổng {PhieuNhapTongTienNhapText} đ";

            // Quay về danh sách phiếu nhập theo flow hiện tại của project
            IsNhapHangFormVisible = false;

            BoDangKyPhieuNhapItems();
            PhieuNhapItems.Clear();
            RecalculatePhieuNhapTongTienHang();
            RecalculatePhieuNhapTotal();
            _phieuNhapId = 0;
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể hoàn thành phiếu nhập: " + ex.Message;
        }
        finally
        {
            IsSavingPhieuNhap = false;
        }
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
    partial void OnSaleDiscountInputChanged(decimal value)
    {
        if (SaleDiscountMode == "Số tiền")
        {
            var maxDiscount = Math.Max(0, SaleSubtotal);

            if (value > maxDiscount)
            {
                SaleDiscountInput = maxDiscount;
                return;
            }
        }

        RecalculateSaleTotal();
    }

    partial void OnSaleDiscountModeChanged(string value) => RecalculateSaleTotal();

    [RelayCommand]
    private async Task ThanhToanAsync()
    {
        if (SaleCart.Count == 0)
        {
            StatusMessage = "Vui lòng chọn ít nhất một sản phẩm trước khi thanh toán.";
            return;
        }

        var tenKhachHang = SelectedSaleCustomer?.TenKhachHang ?? "Khách lẻ";

        // Mở popup xác nhận đẹp mắt thay vì hộp thoại chữ thô
        bool xacNhan = false;
        if (OwnerWindow is not null)
        {
            var confirmWindow = new ThanhToanConfirmWindow(
                tenKhachHang,
                SaleTotal,
                TienKhachDua,
                TienThoi,
                TienCongNo
            );
            xacNhan = await confirmWindow.ShowDialog<bool>(OwnerWindow);
        }

        if (!xacNhan)
        {
            StatusMessage = "Đã hủy thanh toán.";
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
            SelectedMenu = "Hóa đơn";
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
            "Hóa đơn" => 2,
            "Nhà cung cấp" => 3,
            "Nhập hàng" => 4,
            "Sổ quỹ" => 5,
            _ => 0
        };

        // Rời khỏi màn lập phiếu nhập thì đóng lại lịch đang mở
        if (value != "Nhập hàng")
            IsPhieuNhapCalendarOpen = false;

        OnPropertyChanged(nameof(IsTongQuanSelected));
        OnPropertyChanged(nameof(IsDataSectionSelected));
        OnPropertyChanged(nameof(IsBaoCaoSectionSelected));
        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapSelected));

        _ = LoadSelectedSectionAsync(value);
    }

    public bool IsTongQuanSelected => SelectedMenu == "Tổng quan";

    // Báo cáo có vùng nội dung riêng nên không dùng chung TabControl dữ liệu
    public bool IsDataSectionSelected => !IsTongQuanSelected && !IsBaoCaoSectionSelected;

    // ================================================================
    // SỔ QUỸ
    //  - Tự sinh mã phiếu thu (PTxxxx) / phiếu chi (PCxxxx) khi mở form.
    //  - Tìm kiếm theo mã phiếu (realtime) + phân trang theo kết quả tìm kiếm.
    // ================================================================
    private CancellationTokenSource? _soQuySearchCts;
    private int _soQuyLoadVersion;

    public ObservableCollection<SoQuyGiaoDich> SoQuyGiaoDichs { get; } = new();

    public IReadOnlyList<int> SoQuyPageSizeOptions { get; } = [10, 15, 25, 50, 100];

    [ObservableProperty] private string soQuySearchText = "";
    [ObservableProperty] private int soQuyPageSize = 10;
    [ObservableProperty] private int soQuyPage = 1;
    [ObservableProperty] private int soQuyTotalCount;

    // 4 thẻ tổng quan (đã định dạng sẵn để hiển thị)
    [ObservableProperty] private string soQuyQuyDauKyText = "0 đ";
    [ObservableProperty] private string soQuyTongThuText = "0 đ";
    [ObservableProperty] private string soQuyTongChiText = "0 đ";
    [ObservableProperty] private string soQuyTonQuyText = "0 đ";

    /// <summary>true khi người dùng đang gõ từ khóa (dùng để đổi nội dung Empty State).</summary>
    public bool IsSoQuyDangTimKiem => !string.IsNullOrWhiteSpace(SoQuySearchText);

    public bool HasSoQuyData => SoQuyGiaoDichs.Count > 0;

    public int SoQuyTotalPages =>
        SoQuyPageSize <= 0
            ? 0
            : (int)Math.Ceiling(SoQuyTotalCount / (double)SoQuyPageSize);

    public bool CanSoQuyTrangSau => SoQuyPage < SoQuyTotalPages;

    public bool CanSoQuyTrangTruoc => SoQuyPage > 1;

    /// <summary>Ví dụ: "1 - 3 trong 3 giao dịch"</summary>
    public string SoQuyRangeText
    {
        get
        {
            if (SoQuyTotalCount == 0)
                return "0 giao dịch";

            var from = (SoQuyPage - 1) * SoQuyPageSize + 1;
            var to = Math.Min(from + SoQuyPageSize - 1, SoQuyTotalCount);

            return $"{from} - {to} trong {SoQuyTotalCount} giao dịch";
        }
    }

    /// <summary>Nội dung Empty State: khác nhau giữa "chưa có phiếu nào" và "không tìm thấy phiếu phù hợp".</summary>
    public string SoQuyEmptyStateText =>
        IsSoQuyDangTimKiem
            ? $"Không tìm thấy phiếu phù hợp với: \"{SoQuySearchText.Trim()}\""
            : "Chưa có giao dịch nào trong sổ quỹ";

    // ---------- Tìm kiếm realtime ----------

    partial void OnSoQuySearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSoQuyDangTimKiem));
        OnPropertyChanged(nameof(SoQuyEmptyStateText));

        // Lùi 300ms để không gọi database liên tục khi người dùng đang gõ.
        var cts = new CancellationTokenSource();
        _soQuySearchCts?.Cancel();
        _soQuySearchCts = cts;

        _ = DebounceSoQuySearchAsync(cts.Token);
    }

    private async Task DebounceSoQuySearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);

            // Tìm kiếm luôn quay về trang 1.
            SoQuyPage = 1;

            await LoadSoQuyPhieuAsync();
        }
        catch (OperationCanceledException)
        {
            // Người dùng gõ tiếp => kết quả cũ bị huỷ, không cần làm gì.
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tìm kiếm sổ quỹ: " + ex.Message;
        }
    }

    [RelayCommand]
    private void XoaTimKiemSoQuy()
    {
        _soQuySearchCts?.Cancel();
        SoQuySearchText = "";
        SoQuyPage = 1;
        _ = LoadSoQuyPhieuAsync();
    }

    // ---------- Phân trang ----------

    partial void OnSoQuyPageSizeChanged(int value)
    {
        SoQuyPage = 1;
        RaiseSoQuyPaginationChanged();
        _ = LoadSoQuyPhieuAsync();
    }

    partial void OnSoQuyPageChanged(int value)
    {
        RaiseSoQuyPaginationChanged();
        _ = LoadSoQuyPhieuAsync();
    }

    partial void OnSoQuyTotalCountChanged(int value) => RaiseSoQuyPaginationChanged();

    private void RaiseSoQuyPaginationChanged()
    {
        OnPropertyChanged(nameof(SoQuyTotalPages));
        OnPropertyChanged(nameof(SoQuyRangeText));
        OnPropertyChanged(nameof(CanSoQuyTrangSau));
        OnPropertyChanged(nameof(CanSoQuyTrangTruoc));
    }

    [RelayCommand]
    private void SoQuyTrangDau() => SoQuyPage = 1;

    [RelayCommand]
    private void SoQuyTrangCuoi() => SoQuyPage = Math.Max(1, SoQuyTotalPages);

    [RelayCommand]
    private void SoQuyTrangSau()
    {
        if (CanSoQuyTrangSau)
            SoQuyPage++;
    }

    [RelayCommand]
    private void SoQuyTrangTruoc()
    {
        if (CanSoQuyTrangTruoc)
            SoQuyPage--;
    }

    /// <summary>Nạp trang hiện tại của sổ quỹ theo từ khóa tìm kiếm đang gõ.</summary>
    private async Task LoadSoQuyPhieuAsync()
    {
        var version = ++_soQuyLoadVersion;

        try
        {
            var (items, tongSoBanGhi) = await _db.GetSoQuyPhieuPagedAsync(
                SoQuySearchText,
                SoQuyPage,
                SoQuyPageSize);

            // Bỏ qua kết quả cũ nếu người dùng vừa gõ thêm ký tự hoặc chuyển trang.
            if (version != _soQuyLoadVersion)
                return;

            SoQuyGiaoDichs.Clear();
            foreach (var item in items)
                SoQuyGiaoDichs.Add(item);

            // Trang có thể vượt quá tổng số trang sau khi vừa tìm kiếm.
            if (SoQuyTotalPages > 0 && SoQuyPage > SoQuyTotalPages)
                SoQuyPage = SoQuyTotalPages;

            SoQuyTotalCount = tongSoBanGhi;
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải sổ quỹ: " + ex.Message;
        }
    }

    // ---------- Modal tạo phiếu thu / phiếu chi ----------
    [ObservableProperty] private bool isSoQuyPhieuThuVisible;
    [ObservableProperty] private bool isSoQuyPhieuChiVisible;
    [ObservableProperty] private bool isSoQuyPhieuDangLuu;

    [ObservableProperty] private string soQuyPhieuMa = "";
    [ObservableProperty] private DateTime soQuyPhieuThoiGian = DateTime.Now;
    [ObservableProperty] private string soQuyPhieuNguoiThucHien = "";
    [ObservableProperty] private string soQuyPhieuDoiTuong = "";
    [ObservableProperty] private string soQuyPhieuTenNguoi = "";
    [ObservableProperty] private decimal soQuyPhieuSoTien;
    [ObservableProperty] private string soQuyPhieuGhiChu = "";
    [ObservableProperty] private bool soQuyPhieuHachToan = true;

    public IReadOnlyList<string> SoQuyLoaiThuOptions { get; } =
        ["Thu tiền mặt", "Thu công nợ", "Thu khác"];

    public IReadOnlyList<string> SoQuyLoaiChiOptions { get; } =
        ["Chi tiền mặt", "Chi công nợ", "Chi khác"];

    [ObservableProperty] private string soQuyPhieuThuLoai = "Thu tiền mặt";

    [ObservableProperty] private string soQuyPhieuChiLoai = "Chi tiền mặt";

    // ================================================================
    // VALIDATION FORM PHIẾU THU / PHIẾU CHI
    // Chỉ hiển thị lỗi sau khi người dùng đã chạm vào ô hoặc đã bấm Lưu
    // (SoQuyFormDaCham), nên form vừa mở không hiện lỗi.
    // ================================================================

    [ObservableProperty] private bool soQuyFormDaCham;

    [ObservableProperty] private bool soQuyLoiMaPhieu;
    [ObservableProperty] private bool soQuyLoiThoiGian;
    [ObservableProperty] private bool soQuyLoiLoai;
    [ObservableProperty] private bool soQuyLoiNguoiThucHien;
    [ObservableProperty] private bool soQuyLoiTenNguoi;
    [ObservableProperty] private bool soQuyLoiSoTien;

    public string SoQuyLoiMaPhieuText => "Vui lòng tạo lại mã phiếu.";
    public string SoQuyLoiThoiGianText => "Vui lòng nhập thời gian hợp lệ.";
    public string SoQuyLoiLoaiText => "Vui lòng chọn loại phiếu.";
    public string SoQuyLoiNguoiThucHienText => "Vui lòng nhập người thu.";
    public string SoQuyLoiNguoiThucHienChiText => "Vui lòng nhập người chi.";
    public string SoQuyLoiTenNguoiText => "Vui lòng nhập tên người nộp.";
    public string SoQuyLoiTenNguoiChiText => "Vui lòng nhập tên người nhận.";
    public string SoQuyLoiSoTienText => "Vui lòng nhập số tiền lớn hơn 0.";

    /// <summary>Loại phiếu đang mở: true = phiếu thu, false = phiếu chi.</summary>
    private bool DangTaoPhieuThu => IsSoQuyPhieuThuVisible;

    // ---- Thuộc tính hiển thị (chỉ báo lỗi sau khi đã tương tác / bấm Lưu) ----

    private static readonly IBrush VienMacMacDinh = new SolidColorBrush(Color.Parse("#D8E5E0"));
    private static readonly IBrush VienMacLoi = new SolidColorBrush(Color.Parse("#F04438"));

    public bool SoQuyHienLoiMaPhieu => SoQuyFormDaCham && SoQuyLoiMaPhieu;
    public bool SoQuyHienLoiThoiGian => SoQuyFormDaCham && SoQuyLoiThoiGian;
    public bool SoQuyHienLoiLoai => SoQuyFormDaCham && SoQuyLoiLoai;
    public bool SoQuyHienLoiNguoiThucHien => SoQuyFormDaCham && SoQuyLoiNguoiThucHien;
    public bool SoQuyHienLoiTenNguoi => SoQuyFormDaCham && SoQuyLoiTenNguoi;
    public bool SoQuyHienLoiSoTien => SoQuyFormDaCham && SoQuyLoiSoTien;

    public IBrush SoQuyVienMaPhieu => SoQuyHienLoiMaPhieu ? VienMacLoi : VienMacMacDinh;
    public IBrush SoQuyVienThoiGian => SoQuyHienLoiThoiGian ? VienMacLoi : VienMacMacDinh;
    public IBrush SoQuyVienLoai => SoQuyHienLoiLoai ? VienMacLoi : VienMacMacDinh;
    public IBrush SoQuyVienNguoiThucHien => SoQuyHienLoiNguoiThucHien ? VienMacLoi : VienMacMacDinh;
    public IBrush SoQuyVienTenNguoi => SoQuyHienLoiTenNguoi ? VienMacLoi : VienMacMacDinh;
    public IBrush SoQuyVienSoTien => SoQuyHienLoiSoTien ? VienMacLoi : VienMacMacDinh;

    public string SoQuyLoiNguoiThucHienHienThi =>
        DangTaoPhieuThu ? SoQuyLoiNguoiThucHienText : SoQuyLoiNguoiThucHienChiText;

    public string SoQuyLoiTenNguoiHienThi =>
        DangTaoPhieuThu ? SoQuyLoiTenNguoiText : SoQuyLoiTenNguoiChiText;

    /// <summary>Thời gian hiển thị dạng dd/MM/yyyy HH:mm, người dùng không tự nhập.</summary>
    public string SoQuyPhieuThoiGianText =>
        SoQuyPhieuThoiGian == default ? "" : SoQuyPhieuThoiGian.ToString("dd/MM/yyyy HH:mm");

    /// <summary>
    /// Gộp nội dung form để ghi vào cột noi_dung (kiểu text) của phiếu.
    /// Bảng phieu_chi không có cột riêng cho "người nộp/nhận", nên tên người nộp/nhận
    /// được đặt lên đầu để cột "NGƯỜI NỘP / NHẬN" của bảng sổ quỹ hiển thị đúng,
    /// phần đối tượng/ghi chú nối phía sau.
    /// </summary>
    private string SoQuyNoiDungPhieu
    {
        get
        {
            var nguoi = (SoQuyPhieuTenNguoi ?? "").Trim();
            var ghiChu = (SoQuyPhieuDoiTuong ?? "").Trim();
            var note = (SoQuyPhieuGhiChu ?? "").Trim();

            if (ghiChu.Length > 0)
                note = note.Length > 0 ? $"{ghiChu} - {note}" : ghiChu;

            if (note.Length == 0)
                return nguoi;

            return nguoi.Length > 0 ? $"{nguoi} - {note}" : note;
        }
    }

    /// <summary>Số tiền không được âm (MoneyTextBox có thể trả về số âm nếu người dùng gõ "-").</summary>
    partial void OnSoQuyPhieuSoTienChanged(decimal value)
    {
        if (value < 0)
            SoQuyPhieuSoTien = 0;

        SoQuyLoiSoTien = value <= 0;
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuNguoiThucHienChanged(string value)
    {
        SoQuyFormDaCham = true;
        SoQuyLoiNguoiThucHien = string.IsNullOrWhiteSpace(value);
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuTenNguoiChanged(string value)
    {
        SoQuyFormDaCham = true;
        SoQuyLoiTenNguoi = string.IsNullOrWhiteSpace(value);
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuMaChanged(string value)
    {
        SoQuyLoiMaPhieu = string.IsNullOrWhiteSpace(value);
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuThoiGianChanged(DateTime value)
    {
        SoQuyLoiThoiGian = value == default;
        OnPropertyChanged(nameof(SoQuyPhieuThoiGianText));
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuThuLoaiChanged(string value)
    {
        SoQuyLoiLoai = string.IsNullOrWhiteSpace(value);
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuChiLoaiChanged(string value)
    {
        SoQuyLoiLoai = string.IsNullOrWhiteSpace(value);
        RaiseSoQuyValidation();
    }

    partial void OnIsSoQuyPhieuThuVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(SoQuyLoiNguoiThucHienHienThi));
        OnPropertyChanged(nameof(SoQuyLoiTenNguoiHienThi));
    }

    /// <summary>Báo lại toàn bộ thuộc tính hiển thị lỗi sau khi trạng thái validation đổi.</summary>
    private void RaiseSoQuyValidation()
    {
        OnPropertyChanged(nameof(SoQuyHienLoiMaPhieu));
        OnPropertyChanged(nameof(SoQuyHienLoiThoiGian));
        OnPropertyChanged(nameof(SoQuyHienLoiLoai));
        OnPropertyChanged(nameof(SoQuyHienLoiNguoiThucHien));
        OnPropertyChanged(nameof(SoQuyHienLoiTenNguoi));
        OnPropertyChanged(nameof(SoQuyHienLoiSoTien));
        OnPropertyChanged(nameof(SoQuyVienMaPhieu));
        OnPropertyChanged(nameof(SoQuyVienThoiGian));
        OnPropertyChanged(nameof(SoQuyVienLoai));
        OnPropertyChanged(nameof(SoQuyVienNguoiThucHien));
        OnPropertyChanged(nameof(SoQuyVienTenNguoi));
        OnPropertyChanged(nameof(SoQuyVienSoTien));
    }

    /// <summary>Kiểm tra toàn bộ trường bắt buộc. Trả về true khi dữ liệu hợp lệ.</summary>
    private bool KiemTraFormPhieu(bool laPhieuThu)
    {
        var loai = laPhieuThu ? SoQuyPhieuThuLoai : SoQuyPhieuChiLoai;

        SoQuyLoiMaPhieu = string.IsNullOrWhiteSpace(SoQuyPhieuMa);
        SoQuyLoiThoiGian = SoQuyPhieuThoiGian == default;
        SoQuyLoiLoai = string.IsNullOrWhiteSpace(loai);
        SoQuyLoiNguoiThucHien = string.IsNullOrWhiteSpace(SoQuyPhieuNguoiThucHien);
        SoQuyLoiTenNguoi = string.IsNullOrWhiteSpace(SoQuyPhieuTenNguoi);
        SoQuyLoiSoTien = SoQuyPhieuSoTien <= 0;

        RaiseSoQuyValidation();

        return !SoQuyLoiMaPhieu
               && !SoQuyLoiThoiGian
               && !SoQuyLoiLoai
               && !SoQuyLoiNguoiThucHien
               && !SoQuyLoiTenNguoi
               && !SoQuyLoiSoTien;
    }

    [RelayCommand]
    private async Task LuuSoQuyPhieuThuAsync()
    {
        await LuuSoQuyPhieuAsync(true);
    }

    [RelayCommand]
    private async Task LuuSoQuyPhieuChiAsync()
    {
        await LuuSoQuyPhieuAsync(false);
    }

    /// <summary>Lưu &amp; In: hiện dùng chung luồng lưu, chờ phần in phiếu.</summary>
    [RelayCommand]
    private Task LuuVaInSoQuyPhieuThuAsync() => LuuSoQuyPhieuAsync(true);

    [RelayCommand]
    private Task LuuVaInSoQuyPhieuChiAsync() => LuuSoQuyPhieuAsync(false);

    private async Task LuuSoQuyPhieuAsync(bool laPhieuThu)
    {
        if (IsSoQuyPhieuDangLuu)
            return;

        // Bấm Lưu là bắt đầu hiển thị lỗi cho toàn bộ form.
        SoQuyFormDaCham = true;

        if (!KiemTraFormPhieu(laPhieuThu))
        {
            StatusMessage = laPhieuThu
                ? "Không thể lưu phiếu thu: vui lòng kiểm tra các ô được đánh dấu đỏ."
                : "Không thể lưu phiếu chi: vui lòng kiểm tra các ô được đánh dấu đỏ.";

            return;
        }

        IsSoQuyPhieuDangLuu = true;

        var tenLoai = laPhieuThu ? "thu" : "chi";

        try
        {
            await _db.LuuPhieuQuyAsync(
                SoQuyPhieuMa,
                SoQuyPhieuSoTien,
                SoQuyPhieuThoiGian,
                SoQuyPhieuNguoiThucHien,
                SoQuyNoiDungPhieu);

            StatusMessage = $"Đã lưu phiếu {tenLoai} thành công.";

            // Đóng modal rồi nạp lại danh sách + tổng hợp để thấy phiếu vừa lưu ngay.
            ResetSoQuyPhieuForm();

            await LoadSoQuyPhieuAsync();
            await LoadSoQuyTongHopAsync();
        }
        catch (Exception ex)
        {
            // Không nuốt lỗi: in ra console để còn tra được chi tiết SQL/kết nối.
            Console.WriteLine($"========== LỖI LƯU PHIẾU {tenLoai.ToUpperInvariant()} SỔ QUỸ ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("======================================================");

            StatusMessage = $"Không thể lưu phiếu {tenLoai}. Vui lòng thử lại. "
                            + $"(Chi tiết: {ex.Message})";
        }
        finally
        {
            IsSoQuyPhieuDangLuu = false;
        }
    }

    /// <summary>Cập nhật 4 thẻ tổng quan của sổ quỹ (gọi sau khi lưu hoặc khi mở màn hình).</summary>
    private async Task LoadSoQuyTongHopAsync()
    {
        try
        {
            var (tongThu, tongChi) = await _db.GetSoQuyTongHopAsync();

            SoQuyTongThuText = FormatSoQuyTien(tongThu);
            SoQuyTongChiText = FormatSoQuyTien(tongChi);

            // Quỹ đầu kỳ do ngoài quyền hạn sổ quỹ quản lý: hiển thị 0 đ,
            // tồn quỹ = tổng thu - tổng chi.
            SoQuyQuyDauKyText = FormatSoQuyTien(0m);
            SoQuyTonQuyText = FormatSoQuyTien(tongThu - tongChi);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải tổng hợp sổ quỹ: " + ex.Message;
        }
    }

    private static string FormatSoQuyTien(decimal value) => MoneyInput.Format(value) + " đ";

    [RelayCommand]
    private async Task MoSoQuyPhieuThuAsync()
    {
        await MoSoQuyPhieuFormAsync("PT");

        IsSoQuyPhieuThuVisible = true;
    }

    [RelayCommand]
    private async Task MoSoQuyPhieuChiAsync()
    {
        await MoSoQuyPhieuFormAsync("PC");

        IsSoQuyPhieuChiVisible = true;
    }

    [RelayCommand]
    private void DongSoQuyPhieu() => ResetSoQuyPhieuForm();

    /// <summary>Chuẩn bị form và tự sinh mã phiếu (PTxxxx / PCxxxx) cho người dùng.</summary>
    private async Task MoSoQuyPhieuFormAsync(string tienTo)
    {
        ResetSoQuyPhieuForm();

        // Thời gian lấy tự động, người dùng không phải nhập tay.
        SoQuyPhieuThoiGian = DateTime.Now;

        SoQuyPhieuMa = await _db.TaoMaPhieuQuyMoiAsync(tienTo);
    }

    private void ResetSoQuyPhieuForm()
    {
        IsSoQuyPhieuThuVisible = false;
        IsSoQuyPhieuChiVisible = false;
        SoQuyPhieuMa = "";
        SoQuyPhieuTenNguoi = "";
        SoQuyPhieuSoTien = 0m;
        SoQuyPhieuGhiChu = "";
        SoQuyPhieuDoiTuong = "";

        // Mở lại form ở trạng thái sạch: không hiện lỗi cho tới khi người dùng thao tác.
        SoQuyFormDaCham = false;
        SoQuyLoiNguoiThucHien = false;
        SoQuyLoiTenNguoi = false;
        RaiseSoQuyValidation();
    }
}
