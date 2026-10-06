using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using System.Threading;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;
using System.Globalization;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DatabaseService _db = new();
    private CancellationTokenSource? _customerSearchCts;
    private int _customerLoadVersion;
    private Task? _customerLoadTask;
    private CancellationTokenSource? _productSearchCts;
    private int _productLoadVersion;
    public Window? OwnerWindow { get; set; }
    private bool _customersLoaded;
    private bool _invoicesLoaded;
    private bool _overviewLoaded;
    private CancellationTokenSource? _baoCaoPhieuNhapSearchCts;
    private int _baoCaoPhieuNhapLoadVersion;
    private bool _baoCaoPhieuNhapLoaded;
    private bool _baoCaoBanHangLoaded;
    private bool _baoCaoCuoiNgayLoaded;

    /// <summary>
    /// MÃ n "Tá»•ng quan" cÃ³ ViewModel riÃªng. Giá»¯ má»™t instance sá»‘ng suá»‘t phiÃªn Ä‘á»ƒ
    /// giá»¯ nguyÃªn bá»™ lá»c ngÆ°á»i dÃ¹ng Ä‘ang chá»n khi chuyá»ƒn sang mÃ n khÃ¡c rá»“i quay láº¡i.
    /// </summary>
    public DashboardViewModel Dashboard { get; }

    /// <summary>
    /// MÃ n "BÃ¡o cÃ¡o cuá»‘i ngÃ y" cÅ©ng giá»¯ má»™t instance sá»‘ng suá»‘t phiÃªn Ä‘á»ƒ nhá»› ngÃ y
    /// Ä‘ang xem giá»¯a cÃ¡c láº§n chuyá»ƒn mÃ n.
    /// </summary>
    public BaoCaoBanHangViewModel BaoCaoBanHang { get; }
    public BaoCaoCuoiNgayViewModel BaoCaoCuoiNgay { get; }

    /// <summary>
    /// Nguồn dữ liệu thông tin tiệm dùng chung. Mọi mẫu hóa đơn và màn "Thông
    /// tin tiệm" đều đọc/ghi qua đúng service này nên không tự tạo bản sao riêng.
    /// </summary>
    private readonly StoreInfoService _storeInfo;

    /// <summary>Màn "Thông tin tiệm". Giữ instance sống suốt phiên như các màn khác.</summary>
    public ThongTinTiemViewModel ThongTinTiem { get; }

    public MainWindowViewModel()
    {
        Dashboard = new DashboardViewModel(_db);
<<<<<<< HEAD
        _storeInfo = new StoreInfoService(_db.TaoKetNoiAsync);
=======
        BaoCaoBanHang = new BaoCaoBanHangViewModel(_db);
>>>>>>> origin/main
        BaoCaoCuoiNgay = new BaoCaoCuoiNgayViewModel(_db)
        {
            // Cá»­a sá»• chÃ­nh chá»‰ cÃ³ sau khi ViewModel Ä‘Æ°á»£c táº¡o, nÃªn cÃ¡c callback
            // Ä‘á»c nÃ³ theo thá»i Ä‘iá»ƒm dÃ¹ng thay vÃ¬ chá»¥p giÃ¡ trá»‹ lÃºc khá»Ÿi táº¡o.
            LayCuaSoChinh = () => OwnerWindow,
            CapNhatTrangThai = message => StatusMessage = message,
            HienThiHopNhat = (tieuDe, noiDung) => ShowMessageAsync(tieuDe, noiDung)
        };

<<<<<<< HEAD
        ThongTinTiem = new ThongTinTiemViewModel(_storeInfo)
        {
            LayCuaSoChinh = () => OwnerWindow,
            CapNhatTrangThai = message => StatusMessage = message,
            HienThiHopNhat = (tieuDe, noiDung) => ShowMessageAsync(tieuDe, noiDung),
            // Nút Lưu khoá theo quyền SettingsEdit: người chỉ được xem thì vẫn
            // thấy thông tin hiện tại nhưng không sửa được.
            CoTheChinhSua = () => CanEditStoreInfo
        };

        // Giỏ hàng thêm/bớt dòng thì trạng thái nút "Thanh toán" phải cập nhật
        // theo, nếu không nút sẽ mở khi giỏ hàng vẫn còn trống.
        SaleCart.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanThanhToan));
    }

    /// <summary>
    /// Đọc thông tin tiệm để đưa vào hóa đơn. Gọi mỗi lần mở hóa đơn nên hóa
    /// đơn luôn thấy thông tin vừa sửa ở màn "Thông tin tiệm".
    /// </summary>
    private async Task<ThongTinTiem> LayThongTinTiemChoHoaDonAsync()
        => await _storeInfo.LayThongTinKemLogoAsync();

    [ObservableProperty] private string selectedMenu = "Tổng quan";
=======
        // Giá» hÃ ng thÃªm/bá»›t dÃ²ng thÃ¬ tráº¡ng thÃ¡i nÃºt "Thanh toÃ¡n" pháº£i cáº­p nháº­t
        // theo, náº¿u khÃ´ng nÃºt sáº½ má»Ÿ khi giá» hÃ ng váº«n cÃ²n trá»‘ng.
        SaleCart.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanThanhToan));
    }

    [ObservableProperty] private string selectedMenu = "Tá»•ng quan";
>>>>>>> origin/main
    [ObservableProperty] private int selectedDataTabIndex = 0;
    [ObservableProperty] private string selectedReportKey = "";
    [ObservableProperty] private string statusMessage = "Sáºµn sÃ ng";
    [ObservableProperty] private bool isImporting;
    [ObservableProperty]
    private string importStatus = "";

    /// <summary>
    /// Thanh tiáº¿n Ä‘á»™ xÃ¡c Ä‘á»‹nh 0-100 cho cÃ¡c luá»“ng import biáº¿t trÆ°á»›c tá»•ng sá»‘
    /// Ä‘Æ¡n vá»‹ cÃ´ng viá»‡c. Luá»“ng khÃ´ng biáº¿t trÆ°á»›c thÃ¬ Ä‘á»ƒ false Ä‘á»ƒ hiá»‡n thanh
    /// cháº¡y vÃ´ háº¡n nhÆ° cÅ©.
    /// </summary>
    [ObservableProperty] private bool importHasProgress;

    [ObservableProperty] private double importProgress;
    [ObservableProperty] private string productSearch = "";
    [ObservableProperty] private string customerSearch = "";
    [ObservableProperty] private string invoiceSearch = "";
    [ObservableProperty] private string supplierSearch = "";
    [ObservableProperty] private string productFilter = "";
    private bool _sortByPrice;

    public ObservableCollection<SanPham> SanPhams { get; } = new();

    /// <summary>
    /// Tá»•ng sá»‘ hÃ ng hÃ³a trong database khá»›p tá»« khÃ³a Ä‘ang gÃµ.
    /// ÄÆ°á»£c MySQL Ä‘áº¿m trÃªn toÃ n bá»™ báº£ng san_pham, khÃ´ng phá»¥ thuá»™c sá»‘ dÃ²ng Ä‘ang hiá»ƒn thá»‹.
    /// </summary>
    [ObservableProperty] private int productTotalCount;

    public bool IsSanPhamListEmpty => SanPhams.Count == 0;

    /// <summary>DÃ²ng tiÃªu Ä‘á» hiá»ƒn thá»‹ khi báº£ng hÃ ng hÃ³a rá»—ng, Ä‘á»•i theo tá»« khÃ³a Ä‘ang tÃ¬m.</summary>
    public string SanPhamEmptyText =>
        string.IsNullOrWhiteSpace(ProductSearch)
            ? "ChÆ°a cÃ³ hÃ ng hÃ³a nÃ o trong há»‡ thá»‘ng"
            : $"KhÃ´ng tÃ¬m tháº¥y hÃ ng hÃ³a vá»›i tá»« khÃ³a \"{ProductSearch.Trim()}\"";

    public string SanPhamEmptyHint =>
        string.IsNullOrWhiteSpace(ProductSearch)
            ? "Báº¥m â€œï¼‹ Nháº­p hÃ ngâ€ hoáº·c â€œâ‡© Import fileâ€ Ä‘á»ƒ thÃªm hÃ ng hÃ³a."
            : "Thá»­ tá»« khÃ³a khÃ¡c hoáº·c xÃ³a ná»™i dung Ã´ tÃ¬m kiáº¿m.";

    /// <summary>
    /// Ã” tÃ¬m kiáº¿m hÃ ng hÃ³a bind TwoWay nÃªn khi ngÆ°á»i dÃ¹ng gÃµ thÃ¬ SearchText Ä‘á»•i.
    /// Tá»± tÃ¬m láº¡i ngay á»Ÿ Ä‘Ã¢y, khÃ´ng cáº§n nháº¥n Enter vÃ  khÃ´ng cáº§n báº¥m nÃºt tÃ¬m kiáº¿m.
    /// </summary>
    partial void OnProductSearchChanged(string value)
    {
        OnPropertyChanged(nameof(SanPhamEmptyText));
        OnPropertyChanged(nameof(SanPhamEmptyHint));

        TimKiemSanPhamTuDong();
    }

    /// <summary>CÃ¡c dÃ²ng khÃ¡ch hÃ ng cá»§a trang hiá»‡n táº¡i (Ä‘Ã£ phÃ¢n trang).</summary>
    public ObservableCollection<KhachHangRowViewModel> CustomerRows { get; } = new();

    public ObservableCollection<HoaDon> HoaDons { get; } = new();
    public ObservableCollection<SanPham> ProductSearchResults { get; } = new();
    public ObservableCollection<SaleCartItem> SaleCart { get; } = new();
    public ObservableCollection<KhachHang> SaleCustomerSuggestions { get; } = new();
    public IReadOnlyList<string> DiscountModes { get; } = ["Sá»‘ tiá»n", "Pháº§n trÄƒm"];

    [ObservableProperty] private string saleSearchText = "";
    [ObservableProperty] private bool isSalesPanelVisible;
    [ObservableProperty] private string saleCustomerName = "KhÃ¡ch láº»";
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
    [ObservableProperty] private string saleDiscountMode = "Sá»‘ tiá»n";
    [ObservableProperty] private NhaCungCap? selectedNhaCungCap;
    [ObservableProperty] private KhachHang? selectedSaleCustomer;

    // =========================================================
    // TRáº NG THÃI ÄÆ N HÃ€NG ÄANG BÃN
    // =========================================================

    /// <summary>
    /// KhoÃ¡ chá»‘ng báº¥m nÃºt "Thanh toÃ¡n" nhiá»u láº§n / gá»i lá»‡nh chá»“ng nhau.
    /// TrÆ°á»›c Ä‘Ã¢y lá»‡nh thanh toÃ¡n khÃ´ng cÃ³ cá» nÃ y nÃªn báº¥m hai láº§n lÃ  táº¡o hai hÃ³a Ä‘Æ¡n.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanThanhToan))]
    [NotifyPropertyChangedFor(nameof(TrangThaiXuLyThanhToan))]
    private bool isThanhToanDangXuLy;

    /// <summary>CÃ³ thá»ƒ báº¥m "Thanh toÃ¡n" hay khÃ´ng.</summary>
    public bool CanThanhToan => !IsThanhToanDangXuLy && SaleCart.Count > 0;

    /// <summary>Chá»¯ hiá»ƒn thá»‹ trÃªn nÃºt thanh toÃ¡n khi Ä‘ang xá»­ lÃ½.</summary>
    public string TrangThaiXuLyThanhToan =>
        IsThanhToanDangXuLy ? "Äang lÆ°u..." : "Thanh toÃ¡n";

    /// <summary>MÃ£ hÃ³a Ä‘Æ¡n cá»§a Ä‘Æ¡n Ä‘ang bÃ¡n (xem trÆ°á»›c, do database sinh khi lÆ°u).</summary>
    [ObservableProperty] private string saleMaHoaDonHienThi = "";

    // =========================================================
    // DANH SÃCH KHÃCH HÃ€NG: PHÃ‚N TRANG + Má»ž Rá»˜NG CHI TIáº¾T
    // =========================================================

    /// <summary>Trang Ä‘ang xem (báº¯t Ä‘áº§u tá»« 1).</summary>
    [ObservableProperty] private int customerPage = 1;

    /// <summary>Sá»‘ khÃ¡ch hÃ ng hiá»ƒn thá»‹ trÃªn má»™t trang.</summary>
    [ObservableProperty] private int customerPageSize = 20;

    /// <summary>Tá»•ng sá»‘ khÃ¡ch hÃ ng khá»›p Ä‘iá»u kiá»‡n tÃ¬m kiáº¿m (trÃªn toÃ n database).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomerTotalPages))]
    [NotifyPropertyChangedFor(nameof(CustomerRangeText))]
    [NotifyPropertyChangedFor(nameof(CanCustomerTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanCustomerTrangSau))]
    [NotifyPropertyChangedFor(nameof(HasCustomerRows))]
    [NotifyPropertyChangedFor(nameof(IsCustomerListEmpty))]
    private int customerTotalCount;

    /// <summary>
    /// Tá»•ng sá»‘ khÃ¡ch hÃ ng Ä‘ang cÃ³ trong há»‡ thá»‘ng, khÃ´ng phá»¥ thuá»™c tá»« khÃ³a tÃ¬m kiáº¿m.
    /// ÄÃ¢y lÃ  con sá»‘ hiá»ƒn thá»‹ trÃªn tháº» "Tá»•ng khÃ¡ch hÃ ng".
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomerRangeText))]
    private int customerAllCount;

    /// <summary>Sá»‘ khÃ¡ch hÃ ng má»›i phÃ¡t sinh trong khoáº£ng sá»‘ ngÃ y gáº§n nháº¥t.</summary>
    [ObservableProperty] private int customerNewCount;

    /// <summary>
    /// Chá»¯ dÆ°á»›i tháº» "KhÃ¡ch hÃ ng má»›i". Láº¥y tá»« Ä‘Ãºng háº±ng sá»‘ mÃ  query dÃ¹ng Ä‘á»ƒ Ä‘áº¿m
    /// Ä‘á»ƒ chá»¯ nÃ y khÃ´ng bao giá» lá»‡ch vá»›i sá»‘ liá»‡u Ä‘ang hiá»ƒn thá»‹.
    /// </summary>
    public string CustomerNewCountCaption =>
        $"PhÃ¡t sinh trong {DatabaseService.SoNgayKhachMoiMacDinh} ngÃ y gáº§n nháº¥t";

    /// <summary>Tá»•ng ná»£ hiá»‡n táº¡i cá»§a toÃ n bá»™ khÃ¡ch hÃ ng (cÃ¹ng cÃ¡ch tÃ­nh vá»›i cá»™t tiá»n trong báº£ng).</summary>
    [ObservableProperty] private decimal customerTotalDebt;

    /// <summary>Tá»•ng tiá»n bÃ¡n cá»§a toÃ n bá»™ khÃ¡ch hÃ ng.</summary>
    [ObservableProperty] private decimal customerTotalSales;

    /// <summary>Checkbox "chá»n táº¥t cáº£" cá»§a báº£ng danh sÃ¡ch.</summary>
    [ObservableProperty] private bool isAllCustomerRowsSelected;

    /// <summary>Äang Ã¡p tráº¡ng thÃ¡i "chá»n táº¥t cáº£" xuá»‘ng cÃ¡c dÃ²ng, trÃ¡nh cáº­p nháº­t ngÆ°á»£c.</summary>
    private bool _dangApDungChonTatCa;

    [ObservableProperty] private bool isLoadingCustomers;

    /// <summary>Id cá»§a khÃ¡ch hÃ ng Ä‘ang má»Ÿ vÃ¹ng chi tiáº¿t. null = khÃ´ng cÃ³ khÃ¡ch hÃ ng nÃ o Ä‘ang má»Ÿ.</summary>
    [ObservableProperty] private uint? selectedCustomerId;

    /// <summary>DÃ²ng khÃ¡ch hÃ ng Ä‘ang má»Ÿ chi tiáº¿t.</summary>
    [ObservableProperty] private KhachHangRowViewModel? selectedCustomer;

    public int CustomerTotalPages =>
        CustomerPageSize <= 0
            ? 0
            : (int)Math.Ceiling(CustomerTotalCount / (double)CustomerPageSize);

    /// <summary>
    /// VÃ­ dá»¥ khÃ´ng tÃ¬m kiáº¿m: "1 - 20 trong 850 khÃ¡ch hÃ ng".
    /// Khi Ä‘ang tÃ¬m kiáº¿m: "1 - 20 trong 29 khÃ¡ch hÃ ng tÃ¬m theo \"Thá»§y\" (tá»•ng 850)".
    /// </summary>
    public string CustomerRangeText
    {
        get
        {
            var dangTimKiem = !string.IsNullOrWhiteSpace(CustomerSearch);

            if (CustomerTotalCount == 0)
                return dangTimKiem
                    ? $"0 khÃ¡ch hÃ ng tÃ¬m theo \"{CustomerSearch.Trim()}\" (tá»•ng {CustomerAllCount:N0})"
                    : "0 khÃ¡ch hÃ ng";

            var from = (CustomerPage - 1) * CustomerPageSize + 1;
            var to = Math.Min(from + CustomerPageSize - 1, CustomerTotalCount);

            if (dangTimKiem)
                return $"{from} - {to} trong {CustomerTotalCount:N0} khÃ¡ch hÃ ng tÃ¬m theo \"{CustomerSearch.Trim()}\" (tá»•ng {CustomerAllCount:N0})";

            return $"{from} - {to} trong {CustomerTotalCount:N0} khÃ¡ch hÃ ng";
        }
    }

    public bool HasCustomerRows => CustomerRows.Count > 0;

    public bool IsCustomerListEmpty => CustomerRows.Count == 0;

    /// <summary>ThÃ´ng bÃ¡o khi danh sÃ¡ch rá»—ng, thay Ä‘á»•i theo tá»« khÃ³a Ä‘ang tÃ¬m.</summary>
    public string CustomerEmptyText =>
        string.IsNullOrWhiteSpace(CustomerSearch)
            ? "ChÆ°a cÃ³ khÃ¡ch hÃ ng nÃ o trong há»‡ thá»‘ng"
            : $"KhÃ´ng tÃ¬m tháº¥y khÃ¡ch hÃ ng vá»›i tá»« khÃ³a \"{CustomerSearch.Trim()}\"";

    public string CustomerEmptyHint =>
        string.IsNullOrWhiteSpace(CustomerSearch)
            ? "Báº¥m â€œï¼‹ ThÃªm má»›iâ€ Ä‘á»ƒ táº¡o khÃ¡ch hÃ ng Ä‘áº§u tiÃªn."
            : "Thá»­ tá»« khÃ³a khÃ¡c hoáº·c xÃ³a ná»™i dung Ã´ tÃ¬m kiáº¿m.";

    partial void OnCustomerSearchChanged(string value)
    {
        OnPropertyChanged(nameof(CustomerEmptyText));
        OnPropertyChanged(nameof(CustomerEmptyHint));
    }

    public bool CanCustomerTrangTruoc => CustomerPage > 1;

    public bool CanCustomerTrangSau => CustomerPage < CustomerTotalPages;

    public string CustomerTotalDebtText => MoneyInput.Format(CustomerTotalDebt) + " Ä‘";

    public string CustomerTotalSalesText => MoneyInput.Format(CustomerTotalSales) + " Ä‘";

    /// <summary>Sá»‘ khÃ¡ch hÃ ng Ä‘ang Ä‘Æ°á»£c tick trÃªn trang hiá»‡n táº¡i.</summary>
    public int CustomerSelectedCount => CustomerRows.Count(row => row.IsSelected);

    public string CustomerSelectedCountText =>
        CustomerSelectedCount == 0
            ? "ChÆ°a chá»n khÃ¡ch hÃ ng nÃ o"
            : $"ÄÃ£ chá»n {CustomerSelectedCount:N0} khÃ¡ch hÃ ng";

    public bool HasSelectedCustomerRows => CustomerSelectedCount > 0;

    /// <summary>
    /// Khi ngÆ°á»i dÃ¹ng tick Ã´ "chá»n táº¥t cáº£" á»Ÿ tiÃªu Ä‘á» báº£ng thÃ¬ Ã¡p dá»¥ng
    /// tráº¡ng thÃ¡i Ä‘Ã³ cho táº¥t cáº£ dÃ²ng cá»§a trang Ä‘ang xem.
    /// </summary>
    partial void OnIsAllCustomerRowsSelectedChanged(bool value)
    {
        if (_dangApDungChonTatCa || CustomerRows.Count == 0)
            return;

        // Cá» nÃ y trÃ¡nh viá»‡c tá»«ng dÃ²ng bÃ¡o láº¡i ngÆ°á»£c láº¡i lÃ m Ä‘áº£o tráº¡ng thÃ¡i
        // cá»§a Ã´ "chá»n táº¥t cáº£" giá»¯a chá»«ng lÃºc Ä‘ang Ã¡p dá»¥ng.
        _dangApDungChonTatCa = true;

        try
        {
            foreach (var row in CustomerRows)
                row.IsSelected = value;
        }
        finally
        {
            _dangApDungChonTatCa = false;
        }

        CapNhatTrangThaiChonCustomer();
    }

    /// <summary>Tick/bá» tick toÃ n bá»™ khÃ¡ch hÃ ng cá»§a trang Ä‘ang xem.</summary>
    [RelayCommand]
    private void ChonTatCaCustomerRows()
        => IsAllCustomerRowsSelected = !IsAllCustomerRowsSelected;

    /// <summary>Bá» tick toÃ n bá»™ khÃ¡ch hÃ ng Ä‘ang chá»n.</summary>
    [RelayCommand]
    private void BoChonTatCaCustomerRows()
    {
        foreach (var row in CustomerRows)
            row.IsSelected = false;

        IsAllCustomerRowsSelected = false;
        CapNhatTrangThaiChonCustomer();
    }

    private void CapNhatTrangThaiChonCustomer()
    {
        OnPropertyChanged(nameof(CustomerSelectedCount));
        OnPropertyChanged(nameof(CustomerSelectedCountText));
        OnPropertyChanged(nameof(HasSelectedCustomerRows));
    }

    /// <summary>KhÃ¡ch hÃ ng Ä‘ang má»Ÿ chi tiáº¿t (dáº¡ng model, phá»¥c vá»¥ nghiá»‡p vá»¥).</summary>
    public KhachHang? ExpandedCustomer => SelectedCustomer?.Customer;

    partial void OnCustomerPageChanged(int value)
    {
        OnPropertyChanged(nameof(CustomerTotalPages));
        OnPropertyChanged(nameof(CustomerRangeText));
        OnPropertyChanged(nameof(CanCustomerTrangTruoc));
        OnPropertyChanged(nameof(CanCustomerTrangSau));
    }

    partial void OnCustomerPageSizeChanged(int value)
    {
        if (CustomerPage != 1)
            CustomerPage = 1;

        OnPropertyChanged(nameof(CustomerTotalPages));
        OnPropertyChanged(nameof(CustomerRangeText));
        OnPropertyChanged(nameof(CanCustomerTrangTruoc));
        OnPropertyChanged(nameof(CanCustomerTrangSau));
    }

    partial void OnSelectedCustomerChanged(KhachHangRowViewModel? value)
        => OnPropertyChanged(nameof(ExpandedCustomer));

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
        SaleCustomerName = "KhÃ¡ch láº»";
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
            StatusMessage = "KhÃ´ng thá»ƒ táº£i danh sÃ¡ch khÃ¡ch hÃ ng: " + ex.Message;
        }
    }

    /// <summary>
    /// TÃ¬m kiáº¿m khÃ¡ch hÃ ng (nÃºt "TÃ¬m kiáº¿m"): luÃ´n quay vá» trang Ä‘áº§u.
    /// </summary>
    [RelayCommand]
    private async Task TimKiemKhachHangAsync()
    {
        CustomerPage = 1;

        await LoadCustomersAsync();
    }

    /// <summary>
    /// Táº£i láº¡i trang Ä‘áº§u sau khi ngÆ°á»i dÃ¹ng Ä‘á»•i sá»‘ báº£n ghi má»—i trang.
    /// </summary>
    public Task TaiLaiDanhSachKhachHangAsync() => TimKiemKhachHangAsync();

    /// <summary>
    /// TÃ¬m kiáº¿m realtime khi ngÆ°á»i dÃ¹ng gÃµ: chá»‘ng debounce 350ms
    /// Ä‘á»ƒ khÃ´ng gá»i database liÃªn tá»¥c.
    /// </summary>
    public void TimKiemKhachHangTuDong()
    {
        _customerSearchCts?.Cancel();
        _customerSearchCts?.Dispose();
        _customerSearchCts = new CancellationTokenSource();
        var token = _customerSearchCts.Token;

        _ = DebounceCustomerSearchAsync(token);
    }

    private async Task DebounceCustomerSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);

            if (token.IsCancellationRequested)
                return;

            await TimKiemKhachHangAsync();
        }
        catch (OperationCanceledException)
        {
            // NgÆ°á»i dÃ¹ng gÃµ tiáº¿p -> huá»· láº§n tÃ¬m kiáº¿m cÅ©
        }
    }

    // =========================================================
    // Má»ž / ÄÃ“NG VÃ™NG CHI TIáº¾T NGAY DÆ¯á»šI DÃ’NG
    // =========================================================

    /// <summary>
    /// NgÆ°á»i dÃ¹ng click vÃ o má»™t dÃ²ng khÃ¡ch hÃ ng:
    /// click khÃ¡c dÃ²ng Ä‘ang má»Ÿ -> chuyá»ƒn sang dÃ²ng má»›i (Ä‘Ã³ng dÃ²ng cÅ©, má»Ÿ dÃ²ng má»›i);
    /// click Ä‘Ãºng dÃ²ng Ä‘ang má»Ÿ -> Ä‘Ã³ng láº¡i. Chá»‰ má»™t khÃ¡ch hÃ ng má»Ÿ táº¡i má»™t thá»i Ä‘iá»ƒm.
    /// </summary>
    [RelayCommand]
    private async Task MoRongDongKhachHangAsync(KhachHangRowViewModel? row)
    {
        if (row is null)
            return;

        if (SelectedCustomerId == row.CustomerId)
        {
            DongChiTietKhachHang();

            StatusMessage = "ÄÃ£ Ä‘Ã³ng chi tiáº¿t khÃ¡ch hÃ ng";

            return;
        }

        await MoChiTietKhachHangAsync(row);
    }

    private async Task MoChiTietKhachHangAsync(KhachHangRowViewModel row)
    {
        // ÄÃ³ng vÃ¹ng chi tiáº¿t cá»§a khÃ¡ch hÃ ng Ä‘ang má»Ÿ trÆ°á»›c (náº¿u cÃ³)
        if (SelectedCustomer is not null && SelectedCustomer.CustomerId != row.CustomerId)
        {
            SelectedCustomer.IsExpanded = false;
            SelectedCustomer.Detail = null;
        }

        SelectedCustomer = row;
        SelectedCustomerId = row.CustomerId;
        row.IsExpanded = true;

        var detail = row.Detail ?? new KhachHangDetailViewModel();
        row.Detail = detail;
        detail.SelectedTab = "thongtin";
        detail.IsLoading = true;

        StatusMessage = $"Äang táº£i chi tiáº¿t {row.TenKhachHang}...";

        try
        {
            // Chá»‰ Ä‘á»c Ä‘Ãºng dá»¯ liá»‡u cá»§a khÃ¡ch hÃ ng Ä‘ang xem
            var full = await _db.GetKhachHangByIdAsync(row.CustomerId);

            if (full is null)
            {
                detail.IsLoading = false;
                StatusMessage = "KhÃ´ng tÃ¬m tháº¥y khÃ¡ch hÃ ng trong database";

                return;
            }

            if (SelectedCustomerId != row.CustomerId)
                return;

            row.NapLai(full);
            detail.Customer = full;

            var historyTask = _db.GetLichSuMuaHangCuaKhachHangAsync(row.CustomerId);
            var statsTask = _db.GetThongKeMuaHangCuaKhachHangAsync(row.CustomerId);

            await Task.WhenAll(historyTask, statsTask);

            if (SelectedCustomerId != row.CustomerId)
                return;

            detail.NapLichSuMuaHang(await historyTask);
            detail.ThongKe = await statsTask;
            detail.IsLoading = false;

            StatusMessage = full.TenKhachHang;
        }
        catch (Exception ex)
        {
            detail.IsLoading = false;
            StatusMessage = "KhÃ´ng thá»ƒ táº£i chi tiáº¿t khÃ¡ch hÃ ng: " + ex.Message;
        }
    }

    private void DongChiTietKhachHang()
    {
        if (SelectedCustomer is not null)
        {
            SelectedCustomer.IsExpanded = false;
            SelectedCustomer.Detail = null;
        }

        SelectedCustomer = null;
        SelectedCustomerId = null;
    }

    // =========================================================
    // PHÃ‚N TRANG DANH SÃCH KHÃCH HÃ€NG
    // =========================================================

    [RelayCommand]
    private async Task CustomerTrangDauAsync()
    {
        CustomerPage = 1;

        await LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task CustomerTrangCuoiAsync()
    {
        CustomerPage = Math.Max(1, CustomerTotalPages);

        await LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task CustomerTrangTruocAsync()
    {
        if (CustomerPage > 1)
            CustomerPage--;

        await LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task CustomerTrangSauAsync()
    {
        if (CustomerPage < CustomerTotalPages)
            CustomerPage++;

        await LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task ThemMoiKhachHangAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CustomersCreate, "Báº¡n khÃ´ng cÃ³ quyá»n thÃªm khÃ¡ch hÃ ng."))
        {
            return;
        }
        if (OwnerWindow is null)
            return;

        try
        {
            var maKhachHangMoi = await _db.TaoMaKhachHangMoiAsync();

            var editor = new KhachHangEditorWindow(
                new KhachHang
                {
                    Id = 0,
                    MaKhachHang = maKhachHangMoi,
                    TenKhachHang = "",
                    LoaiKhachHang = "CA_NHAN",
                    TrangThai = true
                },
                await LoadNhomKhachHangOptionsAsync(),
                isEditMode: false);

            if (!await editor.ShowDialog<bool>(OwnerWindow))
                return;

            var newId = await _db.ThemKhachHangAsync(editor.Customer);

            editor.Customer.Id = newId;

            // KhÃ¡ch má»›i tÃªn khÃ´ng rá»—ng, cÃ³ thá»ƒ náº±m á»Ÿ trang khÃ¡c -> táº£i láº¡i trang Ä‘áº§u
            CustomerPage = 1;
            await LoadCustomersAsync();

            StatusMessage = $"ÄÃ£ thÃªm khÃ¡ch hÃ ng: {editor.Customer.TenKhachHang}";
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ thÃªm khÃ¡ch hÃ ng: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ThemKhachHangTuFileAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CustomersImport, "Báº¡n khÃ´ng cÃ³ quyá»n import khÃ¡ch hÃ ng."))
        {
            return;
        }
        if (OwnerWindow is null || IsImporting)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chá»n file danh sÃ¡ch khÃ¡ch hÃ ng",
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
            StatusMessage = "Báº¡n chÆ°a chá»n file Ä‘á»ƒ nháº­p khÃ¡ch hÃ ng hÃ ng loáº¡t.";
            return;
        }

        try
        {
            IsImporting = true;
            StatusMessage = $"â³ ÄÃ£ chá»n file \"{file.Name}\". Äang Ä‘á»c dá»¯ liá»‡u...";
            await Task.Yield();

            var doc = await Task.Run(() => _db.DocKhachHangTuFile(file.Path.LocalPath));

            if (doc.KhachHangs.Count == 0)
            {
                StatusMessage = $"âŒ File \"{file.Name}\" khÃ´ng cÃ³ dá»¯ liá»‡u khÃ¡ch hÃ ng há»£p lá»‡.";
                return;
            }

            StatusMessage =
                $"â³ ÄÃ£ Ä‘á»c {doc.SoDongExcel:N0} dÃ²ng, há»£p lá»‡ {doc.SoKhachHangHopLe:N0} khÃ¡ch hÃ ng. Äang kiá»ƒm tra dá»¯ liá»‡u trÃ¹ng...";
            await Task.Yield();

            // Chá»‰ náº¡p mÃ£ vÃ  sá»‘ Ä‘iá»‡n thoáº¡i Ä‘ang cÃ³ Ä‘á»ƒ Ä‘áº¿m trÃ¹ng, khÃ´ng táº£i cáº£ danh sÃ¡ch lÃªn giao diá»‡n.
            var existingKeys = await _db.LayKhoaTrungKhopKhachHangAsync();
            var duplicateCustomers = doc.KhachHangs
                .Where(customer => IsDuplicateCustomer(customer, existingKeys))
                .ToList();

            bool replaceExisting = false;

            if (duplicateCustomers.Count > 0)
            {
                var replace = await ShowConfirmationAsync(
                    "KhÃ¡ch hÃ ng bá»‹ trÃ¹ng",
                    $"PhÃ¡t hiá»‡n {duplicateCustomers.Count:N0} khÃ¡ch hÃ ng trong file Ä‘Ã£ tá»“n táº¡i.\n\nBáº¡n muá»‘n giá»¯ dá»¯ liá»‡u khÃ¡ch hÃ ng cÅ© hay thay tháº¿ báº±ng dá»¯ liá»‡u trong file?",
                    "Thay tháº¿",
                    "Giá»¯ khÃ¡ch hÃ ng cÅ©");

                replaceExisting = replace;

                StatusMessage = replaceExisting
                    ? "â³ ÄÃ£ chá»n thay tháº¿. Äang nháº­p vÃ  cáº­p nháº­t dá»¯ liá»‡u..."
                    : "â³ ÄÃ£ chá»n giá»¯ dá»¯ liá»‡u cÅ©. Äang bá» qua khÃ¡ch hÃ ng trÃ¹ng vÃ  nháº­p khÃ¡ch hÃ ng má»›i...";
            }
            else
            {
                StatusMessage = $"â³ KhÃ´ng cÃ³ khÃ¡ch hÃ ng trÃ¹ng. Äang nháº­p {doc.SoKhachHangHopLe:N0} khÃ¡ch hÃ ng...";
            }

            await Task.Yield();

            var result = await Task.Run(() =>
                _db.ImportKhachHangTuFileAsync(file.Path.LocalPath, doc, replaceExisting));

            StatusMessage = "â³ Äang lÃ m má»›i danh sÃ¡ch khÃ¡ch hÃ ng...";

            // XÃ³a tá»« khÃ³a tÃ¬m kiáº¿m cÃ²n sÃ³t láº¡i Ä‘á»ƒ tháº» "Tá»•ng khÃ¡ch hÃ ng" vÃ  báº£ng
            // hiá»ƒn thá»‹ Ä‘Ãºng toÃ n bá»™ khÃ¡ch hÃ ng sau khi nháº­p.
            CustomerSearch = string.Empty;
            CustomerPage = 1;
            await LoadCustomersAsync();

            var thongBao = new System.Text.StringBuilder();
            thongBao.Append($"âœ… {file.Name}: Ä‘á»c {result.SoDongExcel:N0} dÃ²ng, "
                            + $"thÃªm má»›i {result.SoThemMoi:N0}, cáº­p nháº­t {result.SoCapNhat:N0}");

            if (result.SoBoQua > 0)
                thongBao.Append($", giá»¯ nguyÃªn {result.SoBoQua:N0}");

            thongBao.Append($", lá»—i {result.SoLoi:N0}");

            if (result.SoNhomKhachHangKhongKhop > 0)
                thongBao.Append($" | {result.SoNhomKhachHangKhongKhop:N0} nhÃ³m khÃ¡ch hÃ ng chÆ°a cÃ³ trong pháº§n má»m");

            if (result.SoNguoiTaoKhongKhop > 0)
                thongBao.Append($" | {result.SoNguoiTaoKhongKhop:N0} ngÆ°á»i táº¡o chÆ°a cÃ³ tÃ i khoáº£n");

            thongBao.Append($" ({result.ThoiGian.TotalSeconds:F1}s)");

            StatusMessage = thongBao.ToString();
        }
        catch (Exception ex)
        {
            StatusMessage = "âŒ KhÃ´ng thá»ƒ nháº­p khÃ¡ch hÃ ng tá»« file: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
        }
    }

    private static bool IsDuplicateCustomer(KhachHang customer, IReadOnlySet<string> existingKeys)
    {
        var code = customer.MaKhachHang?.Trim();
        var phone = customer.SoDienThoai?.Trim();

        if (!string.IsNullOrWhiteSpace(code) && existingKeys.Contains("ma:" + code))
            return true;

        return !string.IsNullOrWhiteSpace(phone) && existingKeys.Contains("dt:" + phone);
    }

    /// <summary>
    /// NÃºt "Chá»‰nh sá»­a" á»Ÿ cuá»‘i vÃ¹ng chi tiáº¿t: má»Ÿ form sá»­a khÃ¡ch hÃ ng Ä‘ang chá»n.
    /// Sau khi lÆ°u chá»‰ refresh láº¡i Ä‘Ãºng dÃ²ng Ä‘Ã³, khÃ´ng táº£i láº¡i cáº£ á»©ng dá»¥ng.
    /// </summary>
    [RelayCommand]
    private async Task SuaKhachHangAsync(KhachHangRowViewModel? row)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CustomersEdit, "Báº¡n khÃ´ng cÃ³ quyá»n sá»­a khÃ¡ch hÃ ng."))
        {
            return;
        }
        if (row is null || OwnerWindow is null)
            return;

        var customer = await _db.GetKhachHangByIdAsync(row.CustomerId);

        if (customer is null)
        {
            StatusMessage = "KhÃ´ng tÃ¬m tháº¥y khÃ¡ch hÃ ng Ä‘á»ƒ sá»­a";

            return;
        }

        var editor = new KhachHangEditorWindow(
            customer,
            await LoadNhomKhachHangOptionsAsync(),
            isEditMode: true);

        if (!await editor.ShowDialog<bool>(OwnerWindow))
            return;

        try
        {
            await _db.CapNhatKhachHangAsync(editor.Customer);

            // Refresh Ä‘Ãºng dÃ²ng vá»«a sá»­a, vÃ¹ng chi tiáº¿t váº«n má»Ÿ
            var updated = await _db.GetKhachHangByIdAsync(row.CustomerId);

            if (updated is null)
            {
                await LoadCustomersAsync();

                return;
            }

            row.NapLai(updated);

            if (row.Detail is not null)
                row.Detail.Customer = updated;

            StatusMessage = $"ÄÃ£ cáº­p nháº­t khÃ¡ch hÃ ng: {updated.TenKhachHang}";
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ cáº­p nháº­t khÃ¡ch hÃ ng: " + ex.Message;
        }
    }

    private async Task<IReadOnlyList<NhomKhachHang>> LoadNhomKhachHangOptionsAsync()
    {
        try
        {
            return await _db.GetNhomKhachHangAsync();
        }
        catch
        {
            return Array.Empty<NhomKhachHang>();
        }
    }

    /// <summary>
    /// NÃºt "XÃ³a" á»Ÿ cuá»‘i vÃ¹ng chi tiáº¿t: xÃ¡c nháº­n trÆ°á»›c, xÃ³a xong cáº­p nháº­t
    /// danh sÃ¡ch ngay mÃ  khÃ´ng táº£i láº¡i toÃ n bá»™ á»©ng dá»¥ng.
    /// </summary>
    [RelayCommand]
    private async Task XoaKhachHangAsync(KhachHangRowViewModel? row)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CustomersDelete, "Báº¡n khÃ´ng cÃ³ quyá»n xÃ³a khÃ¡ch hÃ ng."))
        {
            return;
        }
        if (row is null)
            return;

        var customer = row.Customer;

        var confirmed = await ShowConfirmationAsync(
            "XÃ³a khÃ¡ch hÃ ng",
            $"Báº¡n cÃ³ cháº¯c muá»‘n xÃ³a khÃ¡ch hÃ ng \"{row.TenKhachHang}\" khÃ´ng? KhÃ¡ch hÃ ng sáº½ khÃ´ng cÃ²n hiá»ƒn thá»‹ trong danh sÃ¡ch.");

        if (!confirmed)
            return;

        var customerId = row.CustomerId;

        try
        {
            await _db.XoaKhachHangAsync(customerId);

            var suggestion = SaleCustomerSuggestions
                .FirstOrDefault(item => item.Id == customerId);

            if (suggestion is not null)
                SaleCustomerSuggestions.Remove(suggestion);

            if (SelectedSaleCustomer?.Id == customerId)
                ChonKhachLe();

            if (SelectedCustomerId == customerId)
                DongChiTietKhachHang();

            CustomerRows.Remove(row);

            OnPropertyChanged(nameof(HasCustomerRows));
            OnPropertyChanged(nameof(IsCustomerListEmpty));
            OnPropertyChanged(nameof(CustomerRangeText));
            OnPropertyChanged(nameof(CanCustomerTrangSau));

            // Trang cuá»‘i vá»«a bá»‹ rá»—ng -> lÃ¹i vá» trang trÆ°á»›c Ä‘á»ƒ khÃ´ng bá»‹ trang tráº¯ng
            if (CustomerRows.Count == 0 && CustomerPage > 1)
                CustomerPage--;

            // Bá»‘n tháº» thá»‘ng kÃª pháº£i Ä‘á»c láº¡i tá»« database thay vÃ¬ tá»± trá»« tay:
            // "KhÃ¡ch hÃ ng má»›i" chá»‰ giáº£m khi khÃ¡ch vá»«a xÃ³a náº±m trong khoáº£ng ngÃ y
            // Ä‘ang Ä‘áº¿m, cÃ²n "Ná»£ hiá»‡n táº¡i"/"Tá»•ng bÃ¡n" cÃ²n phá»¥ thuá»™c hÃ³a Ä‘Æ¡n, sá»• cÃ¡i
            // cÃ´ng ná»£ vÃ  phiáº¿u tráº£ hÃ ng nÃªn khÃ´ng thá»ƒ suy ra tá»« thao tÃ¡c xÃ³a.
            await LoadCustomersAsync();

            StatusMessage = $"ÄÃ£ xÃ³a khÃ¡ch hÃ ng {customer.TenKhachHang}";
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ xÃ³a khÃ¡ch hÃ ng: " + ex.Message;
        }
    }

    private bool _isInitialized;

    // =========================================================
    // THÃ”NG TIN NGÆ¯á»œI DÃ™NG ÄANG ÄÄ‚NG NHáº¬P
    // Láº¥y tá»« Session, khÃ´ng gáº¯n cá»©ng tÃ i khoáº£n nÃ o trong giao diá»‡n.
    // =========================================================

    public string TenDangNhapHienThi =>
        string.IsNullOrWhiteSpace(Session.CurrentUser?.TenDangNhap)
            ? "ChÆ°a Ä‘Äƒng nháº­p"
            : Session.CurrentUser!.TenDangNhap;

    public string HoTenHienThi =>
        string.IsNullOrWhiteSpace(Session.CurrentUser?.HoTen)
            ? TenDangNhapHienThi
            : Session.CurrentUser!.HoTen;

    public string VaiTroHienThi =>
        string.IsNullOrWhiteSpace(Session.CurrentUser?.VaiTro)
            ? "NhÃ¢n viÃªn"
            : Session.CurrentUser!.VaiTro;

    public string ChuiChuCaiHienThi
    {
        get
        {
            var name = Session.CurrentUser is null
                ? ""
                : (Session.CurrentUser.HoTen ?? "").Trim();

            if (string.IsNullOrEmpty(name))
            {
                name = Session.CurrentUser?.TenDangNhap?.Trim() ?? "";
            }

            if (string.IsNullOrEmpty(name))
                return "?";

            return name[..1].ToUpperInvariant();
        }
    }

    public string ChaoMungNguoiDung =>
        $"ChÃ o má»«ng trá»Ÿ láº¡i, {HoTenHienThi}";

    public bool DaDangNhap => Session.IsLoggedIn;

    // =========================================================
    // ÄÄ‚NG XUáº¤T
    // =========================================================

    /// <summary>
    /// Káº¿t thÃºc phiÃªn hiá»‡n táº¡i rá»“i má»Ÿ láº¡i cá»­a sá»• Ä‘Äƒng nháº­p.
    ///
    /// - KhÃ´ng Ä‘á»¥ng tá»›i database nÃªn tÃ i khoáº£n váº«n cÃ²n nguyÃªn.
    /// - XoÃ¡ thÃ´ng tin Ä‘Äƒng nháº­p Ä‘Ã£ ghi nhá»› Ä‘á»ƒ láº§n sau má»Ÿ app
    ///   KHÃ”NG tá»± Ä‘á»™ng Ä‘Äƒng nháº­p láº¡i (Ä‘Ãºng nhÆ° Chrome/Firefox).
    ///   Checkbox "Ghi nhá»› Ä‘Äƒng nháº­p" váº«n Ä‘Æ°á»£c giá»¯, chá»‰ máº¥t credential.
    /// </summary>
    [RelayCommand]
    private async Task DangXuatAsync()
    {
        if (Session.IsLoggedIn &&
            OwnerWindow is not null)
        {
            var confirm = new ConfirmationWindow(
                "ÄÄƒng xuáº¥t",
                "Báº¡n cÃ³ cháº¯c cháº¯n muá»‘n Ä‘Äƒng xuáº¥t?",
                "ÄÄƒng xuáº¥t",
                "Há»§y");

            var confirmed = await confirm.ShowDialog<bool>(OwnerWindow);

            if (!confirmed)
                return;
        }

        Session.Logout();

        new LoginCredentialStore().Clear();

        if (OwnerWindow is not null)
        {
            var loginWindow = new LoginWindow();

            loginWindow.Show();

            OwnerWindow.Close();
        }
    }

    public async Task InitializeAsync()
    {
        OnPropertyChanged(nameof(TenDangNhapHienThi));
        OnPropertyChanged(nameof(HoTenHienThi));
        OnPropertyChanged(nameof(VaiTroHienThi));
        OnPropertyChanged(nameof(ChuiChuCaiHienThi));
        OnPropertyChanged(nameof(ChaoMungNguoiDung));
        OnPropertyChanged(nameof(DaDangNhap));

        if (_isInitialized)
            return;

        _isInitialized = true;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = "Äang táº£i dá»¯ liá»‡u...";

        var error = await LoadSafelyAsync("sáº£n pháº©m", LoadProductsAsync);

        StatusMessage = error is null
            ? "ÄÃ£ káº¿t ná»‘i database"
            : $"ÄÃ£ táº£i {SanPhams.Count} sáº£n pháº©m. Lá»—i: {error}";
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
                case "Tá»•ng quan":
                    await LoadOverviewAsync();
                    _overviewLoaded = true;
                    break;

                case "KhÃ¡ch hÃ ng":
                    await LoadCustomersAsync();
                    _customersLoaded = true;
                    break;

                case "HÃ³a Ä‘Æ¡n":
                    // Má»—i láº§n vÃ o HÃ³a Ä‘Æ¡n Ä‘á»u Ä‘á»c láº¡i database
                    await LoadInvoicesAsync();
                    _invoicesLoaded = true;
                    break;

                case "NhÃ  cung cáº¥p":
                    await LoadSuppliersAsync();
                    break;

                case "Nháº­p hÃ ng":
                    // Má»—i láº§n báº¥m menu "Nháº­p hÃ ng" pháº£i khá»Ÿi táº¡o phiáº¿u má»›i:
                    // sinh mÃ£ phiáº¿u, gÃ¡n nhÃ¢n viÃªn/ngÃ y/tráº¡ng thÃ¡i vÃ  náº¡p danh sÃ¡ch hÃ ng hÃ³a.
                    // Thiáº¿u bÆ°á»›c nÃ y thÃ¬ PhieuNhapMa rá»—ng => lÆ°u phiáº¿u sáº½ lá»—i
                    // "Duplicate entry '' for key 'uk_pnh_ma'".
                    await MoPhieuNhapHangAsync();
                    break;

                case "Sá»• quá»¹":
                    await TaiSoQuyBoLocOptionsAsync();
                    await TaiLaiSoQuyAsync();
                    break;

<<<<<<< HEAD
                case "Thông tin tiệm":
                    // Luôn đọc lại database mỗi lần mở màn để thấy đúng thông
                    // tin đã lưu, kể cả khi vừa sửa xong ở phiên trước.
                    await ThongTinTiem.TaiLaiCommand.ExecuteAsync(null);
                    break;

                case "Báo cáo":
                    // Báo cáo chưa chọn mục nào thì mở mặc định báo cáo phiếu nhập
=======
                case "BÃ¡o cÃ¡o":
                    // BÃ¡o cÃ¡o chÆ°a chá»n má»¥c nÃ o thÃ¬ má»Ÿ máº·c Ä‘á»‹nh bÃ¡o cÃ¡o phiáº¿u nháº­p
>>>>>>> origin/main
                    if (string.IsNullOrEmpty(SelectedReportKey))
                        SelectedReportKey = BaoCaoMenuItems.FirstOrDefault()?.Key ?? "";

                    // Chá»‰ táº£i láº§n Ä‘áº§u khi vÃ o mÃ n, cÃ¡c láº§n sau giá»¯ nguyÃªn trang/tÃ¬m kiáº¿m Ä‘ang xem
                    if (SelectedReportKey == "phieu-nhap-hang" && !_baoCaoPhieuNhapLoaded)
                    {
                        _baoCaoPhieuNhapLoaded = true;
                        await LoadBaoCaoPhieuNhapAsync();
                    }

                    break;
            }

            StatusMessage = $"ÄÃ£ táº£i {menu.ToLowerInvariant()}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"KhÃ´ng thá»ƒ táº£i {menu}: {ex.Message}";
        }
    }
    private async Task LoadProductsAsync()
    {
        // DÃ¹ng chung version guard vá»›i tÃ¬m kiáº¿m: náº¿u ngÆ°á»i dÃ¹ng gÃµ tá»« khÃ³a ngay
        // lÃºc vá»«a vÃ o mÃ n hÃ¬nh thÃ¬ káº¿t quáº£ táº£i Ä‘áº§u tiÃªn bá»‹ bá», khÃ´ng ghi Ä‘Ã¨
        // danh sÃ¡ch Ä‘ang lá»c theo tá»« khÃ³a.
        var version = ++_productLoadVersion;

        var (products, tongSoKhop) = await _db.GetSanPhamPagedAsync(
            ProductSearch,
            1,
            DatabaseService.SoDongSanPhamToiDa);

        if (version != _productLoadVersion)
            return;

        SanPhams.Clear();
        foreach (var item in products)
        {
            if (!string.IsNullOrWhiteSpace(item.HinhAnh) && File.Exists(item.HinhAnh))
                item.HinhAnhBitmap = new Bitmap(item.HinhAnh);

            SanPhams.Add(item);
        }

        ProductTotalCount = tongSoKhop;

        OnPropertyChanged(nameof(IsSanPhamListEmpty));
    }

    private async Task LoadOverviewAsync()
    {
        // Dashboard tá»± táº£i sá»‘ liá»‡u cá»§a nÃ³ (COUNT/SUM/GROUP BY á»Ÿ táº§ng SQL).
        // CÃ¡c danh sÃ¡ch dÃ¹ng chung vá»›i cÃ¡c tab khÃ¡c khÃ´ng cÃ²n cáº§n náº¡p á»Ÿ Ä‘Ã¢y
        // vÃ¬ mÃ n Tá»•ng quan khÃ´ng hiá»ƒn thá»‹ chÃºng ná»¯a.
        await Dashboard.TaiDuLieuAsync();
    }

    /// <summary>
    /// LÃ m má»›i sá»‘ liá»‡u Tá»•ng quan sau khi ghi xong má»™t nghiá»‡p vá»¥ (bÃ¡n hÃ ng, nháº­p
    /// hÃ ng, ghi sá»• quá»¹). Chá»‰ náº¡p láº¡i khi mÃ n Ä‘ang má»Ÿ Ä‘á»ƒ khÃ´ng tá»‘n truy váº¥n
    /// ná»n, nhÆ°ng dá»¯ liá»‡u váº«n luÃ´n sáºµn sÃ ng khi ngÆ°á»i dÃ¹ng quay láº¡i mÃ n nÃ y.
    /// </summary>
    public async Task LamMoiTongQuanNeuDangMoAsync()
    {
        if (IsTongQuanSelected)
            await Dashboard.TaiDuLieuAsync();
    }

    /// <summary>
    /// Táº£i trang khÃ¡ch hÃ ng hiá»‡n táº¡i.
    /// Chá»‰ Ä‘á»c Ä‘Ãºng dá»¯ liá»‡u cá»§a trang Ä‘ang xem (LIMIT/OFFSET) nÃªn khÃ´ng bá»‹ ngháº½n
    /// khi database cÃ³ hÃ ng nghÃ¬n khÃ¡ch hÃ ng.
    /// </summary>
    private async Task LoadCustomersAsync()
    {
        // Chá» láº§n táº£i Ä‘ang cháº¡y xong Ä‘á»ƒ khÃ´ng bá» sÃ³t tá»« khÃ³a tÃ¬m kiáº¿m má»›i nháº¥t
        if (_customerLoadTask is { IsCompleted: false })
        {
            try
            {
                await _customerLoadTask;
            }
            catch
            {
                // Lá»—i cá»§a láº§n táº£i trÆ°á»›c Ä‘Ã£ Ä‘Æ°á»£c bÃ¡o trong hÃ m bÃªn trong
            }
        }

        _customerLoadTask = LoadCustomersCoreAsync();

        await _customerLoadTask;
    }

    private async Task LoadCustomersCoreAsync()
    {
        IsLoadingCustomers = true;

        // Version guard: bá» qua káº¿t quáº£ cá»§a láº§n táº£i cÅ© náº¿u ngÆ°á»i dÃ¹ng Ä‘Ã£ tÃ¬m/chuyá»ƒn trang má»›i
        var version = ++_customerLoadVersion;

        try
        {
            var page = CustomerPage;
            var (items, tongSoBanGhi) = await _db.GetKhachHangPagedAsync(
                CustomerSearch,
                page,
                CustomerPageSize);

            if (version != _customerLoadVersion)
                return;

            var totalPages = CustomerPageSize <= 0
                ? 0
                : (int)Math.Ceiling(tongSoBanGhi / (double)CustomerPageSize);

            // Trang vÆ°á»£t quÃ¡ sá»‘ trang thá»±c táº¿ (vÃ­ dá»¥ sau khi tÃ¬m kiáº¿m) -> quay vá» trang cuá»‘i
            if (items.Count == 0 && totalPages > 0 && page > totalPages)
            {
                CustomerPage = totalPages;

                await LoadCustomersCoreAsync();

                return;
            }

            // Danh sÃ¡ch Ä‘Æ°á»£c dá»±ng láº¡i tá»« Ä‘áº§u nÃªn Ä‘Ã³ng vÃ¹ng chi tiáº¿t cÅ©,
            // trÃ¡nh tráº¡ng thÃ¡i "Ä‘ang má»Ÿ" trá» vÃ o dÃ²ng khÃ´ng cÃ²n tá»“n táº¡i.
            if (SelectedCustomer is not null)
                DongChiTietKhachHang();

            CustomerRows.Clear();

            foreach (var item in items)
            {
                var row = new KhachHangRowViewModel(item, this);

                row.PropertyChanged += OnCustomerRowPropertyChanged;

                CustomerRows.Add(row);
            }

            // Sang trang má»›i thÃ¬ bá» cÃ¡c tick cÅ© Ä‘á»ƒ khÃ´ng chá»n nháº§m sang khÃ¡ch hÃ ng khÃ¡c.
            IsAllCustomerRowsSelected = false;
            CapNhatTrangThaiChonCustomer();

            CustomerTotalCount = tongSoBanGhi;

            var thongKeTask = _db.GetThongKeDanhSachKhachHangAsync();

            var thongKe = await thongKeTask;

            CustomerAllCount = thongKe.TongSoKhachHang;
            CustomerNewCount = thongKe.SoKhachMoi;
            CustomerTotalDebt = thongKe.TongNoHienTai;
            CustomerTotalSales = thongKe.TongBan;

            OnPropertyChanged(nameof(CustomerTotalDebtText));
            OnPropertyChanged(nameof(CustomerTotalSalesText));
            OnPropertyChanged(nameof(HasCustomerRows));
            OnPropertyChanged(nameof(IsCustomerListEmpty));
            OnPropertyChanged(nameof(CustomerRangeText));
            OnPropertyChanged(nameof(CanCustomerTrangTruoc));
            OnPropertyChanged(nameof(CanCustomerTrangSau));

            _customersLoaded = true;

            StatusMessage = CustomerRows.Count == 0
                ? (string.IsNullOrWhiteSpace(CustomerSearch)
                    ? "ChÆ°a cÃ³ khÃ¡ch hÃ ng nÃ o trong há»‡ thá»‘ng"
                    : "KhÃ´ng tÃ¬m tháº¥y khÃ¡ch hÃ ng phÃ¹ há»£p")
                : $"{CustomerRangeText} - trang {CustomerPage}/{Math.Max(1, totalPages)}";
        }
        catch (Exception ex)
        {
            if (version == _customerLoadVersion)
                StatusMessage = "KhÃ´ng thá»ƒ táº£i danh sÃ¡ch khÃ¡ch hÃ ng: " + ex.Message;
        }
        finally
        {
            if (version == _customerLoadVersion)
                IsLoadingCustomers = false;
        }
    }

    /// <summary>
    /// Khi ngÆ°á»i dÃ¹ng tick má»™t dÃ²ng thÃ¬ checkbox "chá»n táº¥t cáº£" vÃ  sá»‘ khÃ¡ch hÃ ng
    /// Ä‘ang chá»n pháº£i cáº­p nháº­t theo.
    /// </summary>
    private void OnCustomerRowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(KhachHangRowViewModel.IsSelected))
            return;

        if (_dangApDungChonTatCa)
            return;

        var chonTatCa = CustomerRows.Count > 0
                        && CustomerRows.All(row => row.IsSelected);

        if (chonTatCa != IsAllCustomerRowsSelected)
            IsAllCustomerRowsSelected = chonTatCa;

        CapNhatTrangThaiChonCustomer();
    }

    private async Task LoadInvoicesAsync()
    {
        try
        {
            var invoices = await _db.GetHoaDonAsync();

            HoaDons.Clear();

            foreach (var item in invoices)
                HoaDons.Add(item);

            StatusMessage = $"ÄÃ£ táº£i {HoaDons.Count} hÃ³a Ä‘Æ¡n";
        }
        catch (Exception ex)
        {
            HoaDons.Clear();

            StatusMessage = $"Lá»—i táº£i hÃ³a Ä‘Æ¡n: {ex.Message}";
        }
    }

    private async Task LoadSuppliersAsync()
        => await LoadSupplierPageAsync();

    /// <summary>
    /// TÃ¬m kiáº¿m hÃ ng hÃ³a theo tá»« khÃ³a (nÃºt "TÃ¬m kiáº¿m").
    /// Ã” tÃ¬m kiáº¿m Ä‘Ã£ tá»± tÃ¬m realtime nÃªn lá»‡nh nÃ y chá»‰ phá»¥c vá»¥ khi cáº§n tÃ¬m láº¡i ngay.
    /// </summary>
    [RelayCommand]
    private async Task SearchProductsAsync(string? searchText)
    {
        if (searchText is not null)
            ProductSearch = searchText;

        // GÃ¡n SearchText á»Ÿ trÃªn Ä‘Ã£ lÃªn lá»‹ch má»™t láº§n tÃ¬m kiáº¿m tá»± Ä‘á»™ng,
        // huá»· Ä‘i á»Ÿ Ä‘Ã¢y Ä‘á»ƒ khÃ´ng cháº¡y truy váº¥n hai láº§n.
        HuyDongTimKiemSanPham();

        await RefreshProductsAsync();
    }

    /// <summary>
    /// TÃ¬m kiáº¿m realtime: chá» ngÆ°á»i dÃ¹ng ngá»«ng gÃµ 300ms má»›i gá»i database,
    /// trÃ¡nh má»—i kÃ½ tá»± láº¡i cháº¡y má»™t cÃ¢u truy váº¥n lÃ m giao diá»‡n bá»‹ giáº­t.
    /// </summary>
    public void TimKiemSanPhamTuDong()
    {
        HuyDongTimKiemSanPham();

        var cts = new CancellationTokenSource();
        _productSearchCts = cts;

        _ = TimKiemSanPhamSauDebounceAsync(cts.Token);
    }

    private async Task TimKiemSanPhamSauDebounceAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);

            if (token.IsCancellationRequested)
                return;

            await RefreshProductsAsync();
        }
        catch (OperationCanceledException)
        {
            // NgÆ°á»i dÃ¹ng gÃµ tiáº¿p: huá»· láº§n tÃ¬m kiáº¿m trÆ°á»›c, khÃ´ng cáº§n bÃ¡o lá»—i.
        }
    }

    private void HuyDongTimKiemSanPham()
    {
        if (_productSearchCts is null)
            return;

        _productSearchCts.Cancel();
        _productSearchCts.Dispose();
        _productSearchCts = null;
    }

    [RelayCommand]
    private async Task LocSanPhamAsync(string? filter)
    {
        ProductFilter = filter ?? "";
        await RefreshProductsAsync();
        StatusMessage = string.IsNullOrWhiteSpace(ProductFilter)
            ? $"Hiá»ƒn thá»‹ {SanPhams.Count} sáº£n pháº©m"
            : $"Lá»c Ä‘Æ°á»£c {SanPhams.Count} sáº£n pháº©m";
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

        StatusMessage = _sortByPrice ? "ÄÃ£ sáº¯p xáº¿p theo giÃ¡ tÄƒng dáº§n" : "ÄÃ£ sáº¯p xáº¿p theo tÃªn";
    }

    // =========================================================
    // HÃ€NG HÃ“A: CHá»ˆNH GIÃ BÃN
    // =========================================================

    /// <summary>
    /// NgÆ°á»i dÃ¹ng hiá»‡n táº¡i cÃ³ quyá»n sá»­a hÃ ng hÃ³a / giÃ¡ bÃ¡n khÃ´ng.
    /// Láº¥y tá»« vai trÃ² trong database (vai_tro.ma_vai_tro), khÃ´ng gáº¯n cá»©ng tÃ i khoáº£n.
    /// </summary>
    public bool DuocSuaGiaBan => Quyen.DuocSuaHangHoa(Session.CurrentUser);

    /// <summary>
    /// Má»Ÿ Ã´ nháº­p giÃ¡ ngay táº¡i dÃ²ng sáº£n pháº©m (chá»‰nh nhanh).
    /// Chá»‰ má»™t dÃ²ng Ä‘Æ°á»£c má»Ÿ táº¡i má»™t thá»i Ä‘iá»ƒm Ä‘á»ƒ khÃ´ng láº«n giÃ¡ cá»§a sáº£n pháº©m khÃ¡c.
    /// </summary>
    [RelayCommand(CanExecute = nameof(DuocSuaGiaBan))]
    private void BatDauChinhGiaBan(SanPham? product)
    {
        if (!DuocSuaGiaBan)
        {
            StatusMessage = "Báº¡n khÃ´ng cÃ³ quyá»n chá»‰nh giÃ¡ bÃ¡n hÃ ng hÃ³a.";
            return;
        }

        if (product is null || product.Id == 0)
        {
            StatusMessage = "Sáº£n pháº©m nÃ y chÆ°a cÃ³ mÃ£ dá»¯ liá»‡u Ä‘á»ƒ chá»‰nh giÃ¡ bÃ¡n.";
            return;
        }

        // ÄÃ³ng cÃ¡c dÃ²ng Ä‘ang má»Ÿ khÃ¡c, trÃ¡nh sá»­a nháº§m sang sáº£n pháº©m khÃ¡c
        foreach (var item in SanPhams)
        {
            if (!ReferenceEquals(item, product))
                item.DongGiaBanNhanh();
        }

        // Náº¡p Ä‘Ãºng giÃ¡ cá»§a sáº£n pháº©m Ä‘ang chá»n, bá» dáº¥u phÃ¢n cÃ¡ch cho dá»… gÃµ
        product.DangChinhGia = true;
        product.GiaBanDangNhap = MoneyInput.ToRawText(product.GiaBan);
        product.LoiGiaBanNhanh = null;
        product.DangLuGiaBan = false;
    }

    /// <summary>Nháº¥n âœ“: validate, UPDATE Ä‘Ãºng má»™t dÃ²ng, rá»“i Ä‘Ã³ng Ã´ nháº­p.</summary>
    [RelayCommand(CanExecute = nameof(DuocSuaGiaBan))]
    private async Task LuuGiaBanNhanhAsync(SanPham? product)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsUpdatePrice, "Báº¡n khÃ´ng cÃ³ quyá»n cáº­p nháº­t giÃ¡ bÃ¡n hÃ ng hÃ³a."))
        {
            return;
        }
        if (product is null)
            return;

        if (!DuocSuaGiaBan)
        {
            product.LoiGiaBanNhanh = "Báº¡n khÃ´ng cÃ³ quyá»n chá»‰nh giÃ¡ bÃ¡n hÃ ng hÃ³a.";
            return;
        }

        // Validate ngay trÃªn dÃ²ng, chÆ°a Ä‘á»¥ng database
        if (!MoneyInput.TryParse(
                product.GiaBanDangNhap,
                true,
                out var giaBan,
                out var loi))
        {
            product.LoiGiaBanNhanh = loi;
            return;
        }

        if (giaBan == product.GiaBan)
        {
            product.DongGiaBanNhanh();
            StatusMessage = "GiÃ¡ bÃ¡n khÃ´ng thay Ä‘á»•i.";
            return;
        }

        product.DangLuGiaBan = true;
        product.LoiGiaBanNhanh = null;

        try
        {
            var updated =
                await _db.CapNhatGiaBanSanPhamAsync(product.Id, giaBan);

            // Sáº£n pháº©m Ä‘Ã£ bá»‹ ngÆ°á»i khÃ¡c xÃ³a: Ä‘Ã³ng Ã´ nháº­p, bá» dÃ²ng, táº£i láº¡i danh sÃ¡ch
            if (!updated)
            {
                product.DongGiaBanNhanh();
                SanPhams.Remove(product);

                await RefreshProductsAsync();

                // NÃ³i sau khi táº£i láº¡i danh sÃ¡ch Ä‘á»ƒ khÃ´ng bá»‹ thÃ´ng Ä‘iá»‡p
                // "Ä‘Ã£ táº£i N sáº£n pháº©m" cá»§a láº§n táº£i nÃ y ghi Ä‘Ã¨ máº¥t.
                StatusMessage = "Sáº£n pháº©m khÃ´ng cÃ²n tá»“n táº¡i trong há»‡ thá»‘ng.";
                return;
            }

            DongBoGiaBan(product.Id, giaBan);

            product.DongGiaBanNhanh();
            StatusMessage = "Cáº­p nháº­t giÃ¡ bÃ¡n thÃ nh cÃ´ng";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Cáº­p nháº­t giÃ¡ bÃ¡n sáº£n pháº©m", ex);

            var message = SafeLog.Redact(ex.Message);

            product.LoiGiaBanNhanh = message;
            StatusMessage = "KhÃ´ng thá»ƒ cáº­p nháº­t giÃ¡ bÃ¡n: " + message;
        }
        finally
        {
            product.DangLuGiaBan = false;
        }
    }

    /// <summary>
    /// Nháº¥n âœ•: Ä‘Ã³ng Ã´ nháº­p vÃ  giá»¯ nguyÃªn giÃ¡ cÅ© trong database
    /// (khÃ´ng gá»i UPDATE, khÃ´ng Ä‘á»¥ng vÃ o SanPham.GiaBan).
    /// </summary>
    [RelayCommand]
    private void HuyChinhGiaBan(SanPham? product)
    {
        if (product is null)
            return;

        product.DongGiaBanNhanh();

        StatusMessage = "ÄÃ£ há»§y chá»‰nh giÃ¡ bÃ¡n.";
    }

    /// <summary>
    /// Äá»“ng bá»™ giÃ¡ bÃ¡n má»›i cho má»i báº£n sao cá»§a sáº£n pháº©m Ä‘ang náº¡p trong bá»™ nhá»›
    /// (danh sÃ¡ch hÃ ng hÃ³a, tÃ¬m kiáº¿m bÃ¡n hÃ ng, giá» hÃ ng) Ä‘á»ƒ khÃ´ng mÃ n hÃ¬nh nÃ o
    /// cÃ²n giá»¯ giÃ¡ cÅ©. Chá»‰ cáº­p nháº­t vÃ i Ä‘á»‘i tÆ°á»£ng, khÃ´ng SELECT láº¡i toÃ n báº£ng.
    /// </summary>
    private void DongBoGiaBan(uint productId, decimal giaBan)
    {
        foreach (var item in SanPhams)
        {
            if (item.Id == productId)
                item.GiaBan = giaBan;
        }

        foreach (var item in ProductSearchResults)
        {
            if (item.Id == productId)
                item.GiaBan = giaBan;
        }

        var doiGioHang = false;

        foreach (var item in SaleCart)
        {
            if (item.SanPham.Id != productId)
                continue;

            item.SanPham.GiaBan = giaBan;
            item.CapNhatThanhTien();
            doiGioHang = true;
        }

        if (doiGioHang)
            RecalculateSaleTotal();
    }

    [RelayCommand]
    private async Task XoaSanPhamAsync(SanPham? product)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsDelete, "Báº¡n khÃ´ng cÃ³ quyá»n xÃ³a hÃ ng hÃ³a."))
        {
            return;
        }
        if (product is null || product.Id == 0)
        {
            StatusMessage = "Sáº£n pháº©m nÃ y chÆ°a cÃ³ mÃ£ dá»¯ liá»‡u Ä‘á»ƒ xÃ³a.";
            return;
        }

        try
        {
            var confirmed = await ShowConfirmationAsync(
                "XÃ³a sáº£n pháº©m",
                $"Báº¡n cÃ³ cháº¯c muá»‘n xÃ³a sáº£n pháº©m \"{product.TenSanPham}\" khÃ´ng?");

            if (!confirmed)
                return;

            await _db.XoaSanPhamAsync(product.Id);
            SanPhams.Remove(product);
            StatusMessage = $"ÄÃ£ xÃ³a sáº£n pháº©m {product.MaSanPham}";
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ xÃ³a sáº£n pháº©m: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task SuaSanPhamAsync(SanPham? product)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsEdit, "Báº¡n khÃ´ng cÃ³ quyá»n sá»­a hÃ ng hÃ³a."))
        {
            return;
        }
        if (product is null)
        {
            StatusMessage = "Vui lÃ²ng chá»n sáº£n pháº©m cáº§n sá»­a.";
            return;
        }

        if (product.Id == 0)
        {
            StatusMessage = "Sáº£n pháº©m nÃ y chÆ°a cÃ³ mÃ£ dá»¯ liá»‡u Ä‘á»ƒ sá»­a.";
            return;
        }

        var (groups, units) = await LoadProductOptionsAsync();

        // Ã” giÃ¡ bÃ¡n chá»‰ má»Ÿ khi ngÆ°á»i dÃ¹ng cÃ³ quyá»n sá»­a hÃ ng hÃ³a
        var editor = new ProductEditorWindow(
            product,
            groups,
            units,
            DuocSuaGiaBan);

        var saved = OwnerWindow is not null && await editor.ShowDialog<bool>(OwnerWindow);

        // Há»§y thÃ¬ khÃ´ng gá»i UPDATE: giÃ¡ cÅ© giá»¯ nguyÃªn trong database
        if (!saved)
            return;

        try
        {
            await _db.CapNhatSanPhamAsync(product);

            // Äá»“ng bá»™ giÃ¡ bÃ¡n má»›i sang bÃ¡n hÃ ng / tÃ¬m kiáº¿m / giá» hÃ ng
            DongBoGiaBan(product.Id, product.GiaBan);

            product.DongGiaBanNhanh();
            StatusMessage = $"ÄÃ£ cáº­p nháº­t sáº£n pháº©m {product.MaSanPham}";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Cáº­p nháº­t sáº£n pháº©m", ex);
            StatusMessage = "KhÃ´ng thá»ƒ cáº­p nháº­t sáº£n pháº©m: " + SafeLog.Redact(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CapNhatHinhAnhAsync(SanPham? product)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsEdit, "Báº¡n khÃ´ng cÃ³ quyá»n cáº­p nháº­t hÃ ng hÃ³a."))
        {
            return;
        }
        if (product is null || product.Id == 0 || OwnerWindow is null)
        {
            StatusMessage = "KhÃ´ng thá»ƒ má»Ÿ chá»n áº£nh cho sáº£n pháº©m nÃ y.";
            return;
        }

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chá»n áº£nh sáº£n pháº©m",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("áº¢nh")
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
            StatusMessage = $"ÄÃ£ cáº­p nháº­t áº£nh cho {product.MaSanPham}";
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ cáº­p nháº­t áº£nh: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ThemSanPhamAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsCreate, "Báº¡n khÃ´ng cÃ³ quyá»n thÃªm hÃ ng hÃ³a."))
        {
            return;
        }
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
            StatusMessage = $"ÄÃ£ thÃªm sáº£n pháº©m {editor.Product.MaSanPham}";
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ thÃªm sáº£n pháº©m: " + ex.Message;
        }
    }
    [RelayCommand]
    private async Task ThemSanPhamTuFileAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsImport, "Báº¡n khÃ´ng cÃ³ quyá»n import hÃ ng hÃ³a."))
        {
            return;
        }
        if (OwnerWindow is null)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chá»n file danh sÃ¡ch hÃ ng hÃ³a",
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
            StatusMessage = "Báº¡n chÆ°a chá»n file Ä‘á»ƒ nháº­p hÃ ng loáº¡t.";
            return;
        }

        KetQuaImportSanPham? ketQua = null;

        try
        {
            IsImporting = true;
            ImportStatus = $"Äang Ä‘á»c dá»¯ liá»‡u tá»« file \"{file.Name}\"...";
            StatusMessage = ImportStatus;

            await Task.Delay(100);

            // 1. Äá»ŒC FILE â€” Ä‘á»c háº¿t má»i dÃ²ng, khÃ´ng giá»›i háº¡n 20/30/50/100 dÃ²ng.
            // DÃ²ng Ä‘á»c lá»—i khÃ´ng bá»‹ loáº¡i Ã¢m tháº§m mÃ  Ä‘Æ°á»£c giá»¯ láº¡i Ä‘á»ƒ bÃ¡o trong káº¿t quáº£.
            var productsFromFile = _db.ParseProductsFromFile(file.Path.LocalPath, out var loiDocFile);
            var tongSoDongTrongFile = productsFromFile.Count + loiDocFile.Count;

            if (productsFromFile.Count == 0)
            {
                IsImporting = false;

                // Váº«n pháº£i bÃ¡o cÃ¡o náº¿u má»i dÃ²ng Ä‘á»u lá»—i, khÃ´ng pháº£i chá»‰ nÃ³i "khÃ´ng cÃ³ dá»¯ liá»‡u".
                var ketQuaLoi = new KetQuaImportSanPham
                {
                    TenFile = file.Name,
                    TongSoDongExcel = tongSoDongTrongFile,
                    SoLoi = loiDocFile.Count
                };

                ketQuaLoi.LoiDong.AddRange(loiDocFile);

                if (OwnerWindow is not null && loiDocFile.Count > 0)
                    await KetQuaImportWindow.ShowAsync(OwnerWindow, ketQuaLoi);
                else
                    await ShowConfirmationAsync(
                        "File khÃ´ng cÃ³ dá»¯ liá»‡u",
                        $"File \"{file.Name}\" khÃ´ng cÃ³ sáº£n pháº©m há»£p lá»‡ nÃ o Ä‘á»ƒ nháº­p.",
                        "ÄÃ£ hiá»ƒu",
                        "ÄÃ³ng");

                return;
            }

            ImportStatus = $"ÄÃ£ Ä‘á»c {tongSoDongTrongFile:N0} dÃ²ng"
                + (loiDocFile.Count > 0 ? $", trong Ä‘Ã³ {loiDocFile.Count:N0} dÃ²ng lá»—i" : "")
                + ". Äang kiá»ƒm tra trÃ¹ng...";
            StatusMessage = ImportStatus;

            await Task.Delay(100);

            // 2. PHÃT HIá»†N TRÃ™NG â€” khÃ³a chÃ­nh lÃ  MÃƒ HÃ€NG, kiá»ƒm tra thÃªm MÃƒ Váº CH.
            var sanPhamTrung = await _db.TimSanPhamTrungAsync(productsFromFile);

            var quyetDinh = await HoiQuyetDinhSanPhamTrungAsync(
                productsFromFile,
                sanPhamTrung);

            if (quyetDinh == XuLySanPhamTrung.Huy)
            {
                IsImporting = false;
                StatusMessage = "ÄÃ£ há»§y nháº­p file. KhÃ´ng cÃ³ dá»¯ liá»‡u nÃ o Ä‘Æ°á»£c thay Ä‘á»•i.";
                return;
            }

            // 3. Láº¬P DANH SÃCH THAO TÃC cho tá»«ng dÃ²ng
            var danhSachHang = new List<HangNhapSanPham>(productsFromFile.Count);
            var thongTinTrung = TaoDanhSachHangNhap(
                productsFromFile,
                sanPhamTrung,
                quyetDinh,
                danhSachHang);

            var soGhi = danhSachHang.Count(h => h.hanDong is HanDong.ThemMoi or HanDong.ThayThe);

            if (soGhi == 0)
            {
                IsImporting = false;

                var soGiu = danhSachHang.Count(h => h.hanDong == HanDong.GiuCu);
                StatusMessage =
                    $"ÄÃ£ giá»¯ nguyÃªn {soGiu:N0} sáº£n pháº©m trÃ¹ng, khÃ´ng ghi thÃªm dá»¯ liá»‡u nÃ o.";

                // Váº«n pháº£i bÃ¡o cÃ¡c dÃ²ng lá»—i khi Ä‘á»c file, náº¿u khÃ´ng ngÆ°á»i dÃ¹ng
                // khÃ´ng biáº¿t vÃ¬ sao tá»•ng sá»‘ dÃ²ng khÃ´ng khá»›p vá»›i sá»‘ sáº£n pháº©m nháº­p.
                var ketQuaGiu = new KetQuaImportSanPham
                {
                    TenFile = file.Name,
                    TongSoDongExcel = tongSoDongTrongFile,
                    SoGiuCu = soGiu,
                    SoLoi = loiDocFile.Count
                };

                ketQuaGiu.LoiDong.AddRange(loiDocFile);

                if (OwnerWindow is not null && loiDocFile.Count > 0)
                    await KetQuaImportWindow.ShowAsync(OwnerWindow, ketQuaGiu);

                return;
            }

            // 4. GHI XUá»NG DATABASE trong má»™t transaction
            ImportStatus = $"Äang nháº­p {soGhi:N0} sáº£n pháº©m vÃ o database...";
            StatusMessage = ImportStatus;

            await Task.Delay(100);

            ketQua = await _db.ImportSanPhamAsync(
                            file.Name,
                            danhSachHang,
                            tongSoDongTrongFile,
                            loiDocFile);

            // Sá»‘ giá»¯ cÅ© do ngÆ°á»i dÃ¹ng quyáº¿t Ä‘á»‹nh, khÃ´ng pháº£i do database ghi.
            ketQua.SoGiuCu = thongTinTrung.SoGiuCu;

            // 5. RELOAD Láº I Tá»ª DATABASE, khÃ´ng chá»‰ thÃªm vÃ o collection táº¡m.
            ImportStatus = "Äang táº£i láº¡i danh sÃ¡ch hÃ ng hÃ³a tá»« database...";
            StatusMessage = ImportStatus;

            await Task.Delay(100);

            await RefreshProductsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ nháº­p hÃ ng loáº¡t tá»« file: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
        }

        // 6. BÃO CÃO Káº¾T QUáº¢ cho ngÆ°á»i dÃ¹ng
        if (ketQua is not null && OwnerWindow is not null)
        {
            StatusMessage = ketQua.DaRollback
                ? "Nháº­p file tháº¥t báº¡i, Ä‘Ã£ rollback toÃ n bá»™."
                : $"Nháº­p xong: thÃªm má»›i {ketQua.SoThemMoi:N0}, "
                  + $"thay tháº¿ {ketQua.SoThayThe:N0}, giá»¯ cÅ© {ketQua.SoGiuCu:N0}, "
                  + $"lá»—i {ketQua.SoLoi:N0}.";

            await KetQuaImportWindow.ShowAsync(OwnerWindow, ketQua);
        }
    }

    /// <summary>
    /// Há»i ngÆ°á»i dÃ¹ng xá»­ lÃ½ sáº£n pháº©m trÃ¹ng: giá»¯ cÅ© / thay tháº¿ / há»§y.
    /// Tráº£ vá» lá»±a chá»n, hoáº·c null náº¿u khÃ´ng cÃ³ gÃ¬ trÃ¹ng.
    /// </summary>
    private async Task<XuLySanPhamTrung?> HoiQuyetDinhSanPhamTrungAsync(
        IReadOnlyList<SanPham> productsFromFile,
        IReadOnlyList<SanPham> sanPhamDaCo)
    {
        if (OwnerWindow is null || sanPhamDaCo.Count == 0)
            return null;

        var danhSachTrung = TaoDanhSachSanPhamTrung(productsFromFile, sanPhamDaCo);

        if (danhSachTrung.Count == 0)
            return null;

        // Táº¯t loading Ä‘á»ƒ há»™p thoáº¡i hiá»‡n Ä‘Æ°á»£c trÃªn UI thread.
        IsImporting = false;
        await Task.Delay(50);

        return await SanPhamTrungWindow.ShowAsync(OwnerWindow, danhSachTrung);
    }

    /// <summary>
    /// GhÃ©p tá»«ng sáº£n pháº©m trong file vá»›i báº£n ghi Ä‘ang cÃ³ trong database.
    /// MÃ£ hÃ ng lÃ  khÃ³a chÃ­nh; mÃ£ váº¡ch chá»‰ dÃ¹ng khi khÃ´ng trÃ¹ng mÃ£ hÃ ng.
    /// Cá»‘ tÃ¬nh khÃ´ng so tÃªn hÃ ng vÃ¬ hai mÃ³n khÃ¡c tÃªn dá»… bá»‹ trÃ¹ng tÃªn.
    /// </summary>
    private static List<SanPhamTrung> TaoDanhSachSanPhamTrung(
        IReadOnlyList<SanPham> productsFromFile,
        IReadOnlyList<SanPham> sanPhamDaCo)
    {
        var theoMaSanPham = new Dictionary<string, SanPham>(StringComparer.OrdinalIgnoreCase);
        var theoMaVach = new Dictionary<string, SanPham>(StringComparer.OrdinalIgnoreCase);

        foreach (var sanPham in sanPhamDaCo)
        {
            var maSanPham = sanPham.MaSanPham?.Trim();

            if (!string.IsNullOrWhiteSpace(maSanPham) && !theoMaSanPham.ContainsKey(maSanPham))
                theoMaSanPham[maSanPham] = sanPham;

            var maVach = sanPham.MaVach?.Trim();

            if (!string.IsNullOrWhiteSpace(maVach) && !theoMaVach.ContainsKey(maVach))
                theoMaVach[maVach] = sanPham;
        }

        var result = new List<SanPhamTrung>();
        var daThem = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in productsFromFile)
        {
            var maSanPham = product.MaSanPham?.Trim();
            var maVach = product.MaVach?.Trim();

            SanPham? cu = null;
            var lyDo = string.Empty;

            if (!string.IsNullOrWhiteSpace(maSanPham)
                && theoMaSanPham.TryGetValue(maSanPham, out var theoMa))
            {
                cu = theoMa;
                lyDo = "MÃ£ hÃ ng";
            }
            else if (!string.IsNullOrWhiteSpace(maVach)
                     && theoMaVach.TryGetValue(maVach, out var theoVach))
            {
                cu = theoVach;
                lyDo = "MÃ£ váº¡ch";
            }

            if (cu is null)
                continue;

            // Sáº£n pháº©m trÃ¹ng cáº£ mÃ£ hÃ ng láº«n mÃ£ váº¡ch chá»‰ bÃ¡o má»™t láº§n.
            if (!daThem.Add(cu.Id.ToString()))
                continue;

            result.Add(new SanPhamTrung
            {
                SanPhamCu = cu,
                SanPhamMoi = product,
                SoDongExcel = product.SoDongExcel,
                LyDoTrung = lyDo
            });
        }

        return result;
    }

    /// <summary>
    /// Biáº¿n danh sÃ¡ch sáº£n pháº©m Ä‘á»c tá»« file thÃ nh danh sÃ¡ch quyáº¿t Ä‘á»‹nh cho tá»«ng dÃ²ng:
    /// thÃªm má»›i / thay tháº¿ / giá»¯ cÅ©.
    /// </summary>
    private static (int SoGiuCu, List<SanPhamTrung> DanhSachTrung) TaoDanhSachHangNhap(
        IReadOnlyList<SanPham> productsFromFile,
        IReadOnlyList<SanPham> sanPhamDaCo,
        XuLySanPhamTrung? quyetDinh,
        List<HangNhapSanPham> danhSachHang)
    {
        var theoMaSanPham = new Dictionary<string, SanPham>(StringComparer.OrdinalIgnoreCase);
        var theoMaVach = new Dictionary<string, SanPham>(StringComparer.OrdinalIgnoreCase);

        foreach (var sanPham in sanPhamDaCo)
        {
            var maSanPham = sanPham.MaSanPham?.Trim();

            if (!string.IsNullOrWhiteSpace(maSanPham) && !theoMaSanPham.ContainsKey(maSanPham))
                theoMaSanPham[maSanPham] = sanPham;

            var maVach = sanPham.MaVach?.Trim();

            if (!string.IsNullOrWhiteSpace(maVach) && !theoMaVach.ContainsKey(maVach))
                theoMaVach[maVach] = sanPham;
        }

        var thayThe = quyetDinh == XuLySanPhamTrung.ThayTheBangExcel;
        var danhSachTrung = new List<SanPhamTrung>();
        var soGiuCu = 0;

        foreach (var product in productsFromFile)
        {
            var maSanPham = product.MaSanPham?.Trim();
            var maVach = product.MaVach?.Trim();

            SanPham? cu = null;

            if (!string.IsNullOrWhiteSpace(maSanPham)
                && theoMaSanPham.TryGetValue(maSanPham, out var theoMa))
            {
                cu = theoMa;
            }
            else if (!string.IsNullOrWhiteSpace(maVach)
                     && theoMaVach.TryGetValue(maVach, out var theoVach))
            {
                cu = theoVach;
            }

            if (cu is null)
            {
                danhSachHang.Add(new HangNhapSanPham(product, product.SoDongExcel));
                continue;
            }

            if (thayThe)
            {
                danhSachHang.Add(new HangNhapSanPham(
                    product,
                    product.SoDongExcel,
                    cu.Id,
                    cu,
                    HanDong.ThayThe));
            }
            else
            {
                soGiuCu++;
                danhSachTrung.Add(new SanPhamTrung
                {
                    SanPhamCu = cu,
                    SanPhamMoi = product,
                    SoDongExcel = product.SoDongExcel,
                    LyDoTrung = string.Equals(cu.MaSanPham?.Trim(), maSanPham,
                        StringComparison.OrdinalIgnoreCase)
                        ? "MÃ£ hÃ ng"
                        : "MÃ£ váº¡ch"
                });

                // Giá»¯ cÅ©: KHÃ”NG UPDATE, giá»¯ nguyÃªn toÃ n bá»™ dá»¯ liá»‡u database.
                danhSachHang.Add(new HangNhapSanPham(
                    product,
                    product.SoDongExcel,
                    cu.Id,
                    cu,
                    HanDong.GiuCu));
            }
        }

        return (soGiuCu, danhSachTrung);
    }
    /// <summary>
    /// Táº£i láº¡i danh sÃ¡ch hÃ ng hÃ³a theo tá»« khÃ³a Ä‘ang gÃµ.
    /// Tá»« khÃ³a Ä‘Æ°á»£c MySQL lá»c trÃªn TOÃ€N Bá»˜ báº£ng san_pham rá»“i má»›i cáº¯t báº±ng LIMIT,
    /// nÃªn sáº£n pháº©m náº±m á»Ÿ trang khÃ¡c / ngoÃ i pháº§n Ä‘ang hiá»ƒn thá»‹ váº«n tÃ¬m ra Ä‘Æ°á»£c.
    /// </summary>
    private async Task RefreshProductsAsync()
    {
        // Version guard: ngÆ°á»i dÃ¹ng gÃµ tiáº¿p trong lÃºc chá» database thÃ¬ bá» káº¿t quáº£ cÅ©,
        // trÃ¡nh danh sÃ¡ch bá»‹ nháº£y ngÆ°á»£c vá» tá»« khÃ³a cÅ©.
        var version = ++_productLoadVersion;

        try
        {
            var (products, tongSoKhop) = await _db.GetSanPhamPagedAsync(
                ProductSearch,
                1,
                DatabaseService.SoDongSanPhamToiDa);

            if (version != _productLoadVersion)
                return;

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

            ProductTotalCount = tongSoKhop;

            OnPropertyChanged(nameof(IsSanPhamListEmpty));

            var dangTimKiem = !string.IsNullOrWhiteSpace(ProductSearch);

            StatusMessage = SanPhams.Count == 0
                ? SanPhamEmptyText
                : (dangTimKiem
                    ? SanPhams.Count < tongSoKhop
                        ? $"TÃ¬m tháº¥y {tongSoKhop:N0} sáº£n pháº©m theo \"{ProductSearch.Trim()}\" (Ä‘ang hiá»‡n {SanPhams.Count:N0})"
                        : $"TÃ¬m tháº¥y {tongSoKhop:N0} sáº£n pháº©m theo \"{ProductSearch.Trim()}\""
                    : $"ÄÃ£ táº£i {SanPhams.Count:N0} sáº£n pháº©m");
        }
        catch (Exception ex)
        {
            if (version != _productLoadVersion)
                return;

            SanPhams.Clear();
            ProductTotalCount = 0;

            OnPropertyChanged(nameof(IsSanPhamListEmpty));

            SafeLog.WriteError("Táº£i danh sÃ¡ch hÃ ng hÃ³a", ex);

            StatusMessage =
                "KhÃ´ng thá»ƒ táº£i danh sÃ¡ch hÃ ng hÃ³a: " + SafeLog.Redact(ex.Message);
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
            // Cao theo ná»™i dung: tiÃªu Ä‘á»/nguyÃªn nhÃ¢n dÃ i khÃ´ng bá»‹ cáº¯t chá»¯.
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            MinHeight = 230,
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
            Content = "ÄÃ£ hiá»ƒu",
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
            => await ShowConfirmationAsync(title, message, "XÃ³a", "Há»§y");

    private async Task<bool> ShowConfirmationAsync(string title, string message, string confirmButtonText, string cancelButtonText)
    {
        if (OwnerWindow is null)
            return false;

        var dialog = new ConfirmationWindow(title, message, confirmButtonText, cancelButtonText);
        return await dialog.ShowDialog<bool>(OwnerWindow);
    }

    /// <summary>
    /// BÃ¡o lá»—i khi lÆ°u phiáº¿u nháº­p: hiá»‡n há»™p thoáº¡i "Tháº¥t báº¡i" kÃ¨m nguyÃªn nhÃ¢n vÃ 
    /// giá»¯ nguyÃªn toÃ n bá»™ form Ä‘á»ƒ ngÆ°á»i dÃ¹ng sá»­a rá»“i báº¥m láº¡i.
    /// </summary>
    private async Task BaoLoiLuuPhieuNhapAsync(string lyDo)
    {
        var noiDung = string.IsNullOrWhiteSpace(lyDo)
            ? "Lá»—i khÃ´ng xÃ¡c Ä‘á»‹nh. Vui lÃ²ng thá»­ láº¡i."
            : lyDo.Trim();

        StatusMessage = "âŒ Tháº¥t báº¡i â€“ KhÃ´ng thá»ƒ lÆ°u phiáº¿u nháº­p: " + noiDung;

        await ShowMessageAsync(
            "Tháº¥t báº¡i â€“ KhÃ´ng thá»ƒ lÆ°u phiáº¿u nháº­p.",
            $"{noiDung}\n\nDá»¯ liá»‡u Ä‘ang nháº­p váº«n Ä‘Æ°á»£c giá»¯ nguyÃªn, hÃ£y sá»­a vÃ  báº¥m HoÃ n thÃ nh láº¡i.");
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
            StatusMessage = "KhÃ´ng thá»ƒ táº£i nhÃ³m hÃ ng/Ä‘Æ¡n vá»‹ tÃ­nh: " + ex.Message;
            return (Array.Empty<ProductOption>(), Array.Empty<ProductOption>());
        }
    }

    private static bool IsDuplicateProduct(SanPham product, IReadOnlyList<SanPham> existingProducts)
    {
        // CÃ²n dÃ¹ng cho luá»“ng nháº­p KHÃCH HÃ€NG, giá»¯ nguyÃªn logic cÅ© á»Ÿ Ä‘Ã¢y.
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
                keyword.Equals("KhÃ¡ch láº»", StringComparison.OrdinalIgnoreCase))
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
            StatusMessage = "KhÃ´ng thá»ƒ tÃ¬m khÃ¡ch hÃ ng: " + ex.Message;
        }
    }
    [RelayCommand]
    private async Task SearchInvoicesAsync()
    {
        try
        {
            HoaDons.Clear();
            foreach (var item in await _db.GetHoaDonAsync(InvoiceSearch))
                HoaDons.Add(item);
            StatusMessage = $"TÃ¬m tháº¥y {HoaDons.Count} hÃ³a Ä‘Æ¡n";
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

            StatusMessage = $"ÄÃ£ táº£i {HoaDons.Count} hÃ³a Ä‘Æ¡n";
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ táº£i hÃ³a Ä‘Æ¡n: " + ex.Message;
        }
    }
    [RelayCommand]
    private async Task ThemNhaCungCapAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersCreate, "Báº¡n khÃ´ng cÃ³ quyá»n thÃªm nhÃ  cung cáº¥p."))
        {
            return;
        }
        var supplier = await HienThiFormThemNhaCungCapAsync();

        if (supplier is null)
            return;

        StatusMessage =
            $"ÄÃ£ thÃªm nhÃ  cung cáº¥p {supplier.TenNhaCungCap}";
    }

    /// <summary>
    /// Má»Ÿ form thÃªm nhÃ  cung cáº¥p dÃ¹ng chung cho cáº£ module quáº£n lÃ½
    /// vÃ  mÃ n láº­p phiáº¿u nháº­p. Tráº£ vá» nhÃ  cung cáº¥p vá»«a lÆ°u, null náº¿u ngÆ°á»i dÃ¹ng há»§y.
    /// </summary>
    private async Task<NhaCungCap?> HienThiFormThemNhaCungCapAsync()
    {
        if (OwnerWindow is null)
            return null;

        // Tá»± phÃ¡t sinh mÃ£ NCC tá»« database
        var generatedCode = await _db.TaoMaNhaCungCapMoiAsync();

        var newSupplier = new NhaCungCap
        {
            MaNhaCungCap = generatedCode,
            TenNhaCungCap = string.Empty,
            TrangThai = true
        };

        // Má»Ÿ giao diá»‡n nháº­p thÃ´ng tin nhÃ  cung cáº¥p
        var editor = new NhaCungCapEditorWindow(newSupplier);

        var saved = await editor.ShowDialog<bool>(OwnerWindow);

        if (!saved)
            return null;

        // LÆ°u database
        editor.Supplier.Id =
            await _db.ThemNhaCungCapAsync(editor.Supplier);

        // Danh sÃ¡ch Ä‘ang phÃ¢n trang theo database nÃªn táº£i láº¡i trang hiá»‡n táº¡i
        // thay vÃ¬ thÃªm thá»§ cÃ´ng má»™t dÃ²ng cÃ³ thá»ƒ khÃ´ng thuá»™c trang Ä‘ang xem.
        await LoadSupplierPageAsync();

        return editor.Supplier;
    }
    [RelayCommand]
    private async Task SuaNhaCungCapAsync(NhaCungCap? supplier)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersEdit, "Báº¡n khÃ´ng cÃ³ quyá»n sá»­a nhÃ  cung cáº¥p."))
        {
            return;
        }
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

            StatusMessage =
                $"ÄÃ£ cáº­p nháº­t nhÃ  cung cáº¥p {editor.Supplier.TenNhaCungCap}";

            // Thá»© tá»± hiá»ƒn thá»‹ cÃ³ thá»ƒ Ä‘á»•i theo tÃªn nÃªn táº£i láº¡i Ä‘Ãºng trang Ä‘ang xem.
            await LoadSupplierPageAsync();
        }
        catch (Exception ex)
        {
            StatusMessage =
                "KhÃ´ng thá»ƒ cáº­p nháº­t nhÃ  cung cáº¥p: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ThemNhaCungCapTuFileAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersImport, "Báº¡n khÃ´ng cÃ³ quyá»n import nhÃ  cung cáº¥p."))
        {
            return;
        }
        if (OwnerWindow is null || IsImporting)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chá»n file danh sÃ¡ch nhÃ  cung cáº¥p",
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
            StatusMessage = "Báº¡n chÆ°a chá»n file nhÃ  cung cáº¥p.";
            return;
        }

        try
        {
            IsImporting = true;

            ImportStatus = $"Äang Ä‘á»c file \"{file.Name}\"...";
            StatusMessage = ImportStatus;

            await Task.Yield();

            // Äá»c file Excel/CSV
            var suppliersFromFile = await Task.Run(() =>
                _db.ParseNhaCungCapFromFile(file.Path.LocalPath));

            if (suppliersFromFile.Count == 0)
            {
                StatusMessage =
                    $"âŒ File \"{file.Name}\" khÃ´ng cÃ³ dá»¯ liá»‡u nhÃ  cung cáº¥p há»£p lá»‡.";
                return;
            }

            ImportStatus =
                $"ÄÃ£ Ä‘á»c {suppliersFromFile.Count:N0} nhÃ  cung cáº¥p. Äang kiá»ƒm tra...";

            StatusMessage = ImportStatus;

            await Task.Yield();

            // Chá»‰ há»i database vá» cÃ¡c mÃ£ cÃ³ tháº­t trong file, khÃ´ng táº£i toÃ n bá»™ NCC.
            var existingCodes = await _db.LayMaNhaCungCapDaTonTaiAsync(
                suppliersFromFile.Select(x => x.MaNhaCungCap));

            var duplicateCount = suppliersFromFile.Count(x =>
                !string.IsNullOrWhiteSpace(x.MaNhaCungCap) &&
                existingCodes.Contains(x.MaNhaCungCap.Trim()));

            if (duplicateCount > 0)
            {
                // Táº¯t loading trÆ°á»›c khi hiá»‡n há»™p thoáº¡i
                IsImporting = false;

                var shouldUpdate = await ShowConfirmationAsync(
                    "NhÃ  cung cáº¥p bá»‹ trÃ¹ng",
                    $"PhÃ¡t hiá»‡n {duplicateCount:N0} nhÃ  cung cáº¥p trong file " +
                    $"Ä‘Ã£ tá»“n táº¡i trong database.\n\n" +
                    "\"Cáº­p nháº­t dá»¯ liá»‡u\" sáº½ ghi Ä‘Ã¨ thÃ´ng tin (kÃ¨m sá»‘ tiá»n Ä‘Ã£ mua, " +
                    "cÃ´ng ná»£) theo Ä‘Ãºng file. NgÆ°á»£c láº¡i cÃ¡c mÃ£ bá»‹ trÃ¹ng sáº½ Ä‘Æ°á»£c bá» qua " +
                    "vÃ  chá»‰ nhá»¯ng nhÃ  cung cáº¥p má»›i Ä‘Æ°á»£c nháº­p.",
                    "Cáº­p nháº­t dá»¯ liá»‡u",
                    "Bá» qua nhÃ  cung cáº¥p trÃ¹ng");

                IsImporting = true;

                if (shouldUpdate)
                {
                    ImportStatus = "Äang cáº­p nháº­t dá»¯ liá»‡u nhÃ  cung cáº¥p bá»‹ trÃ¹ng...";
                    StatusMessage = ImportStatus;

                    await Task.Yield();
                }
                else
                {
                    ImportStatus = "Äang loáº¡i bá» dá»¯ liá»‡u nhÃ  cung cáº¥p bá»‹ trÃ¹ng...";
                    StatusMessage = ImportStatus;

                    await Task.Yield();

                    suppliersFromFile = suppliersFromFile
                        .Where(x =>
                            string.IsNullOrWhiteSpace(x.MaNhaCungCap) ||
                            !existingCodes.Contains(x.MaNhaCungCap.Trim()))
                        .ToList();
                }
            }

            if (suppliersFromFile.Count == 0)
            {
                StatusMessage =
                    "âŒ Táº¥t cáº£ nhÃ  cung cáº¥p trong file Ä‘á»u Ä‘Ã£ tá»“n táº¡i.";
                return;
            }

            ImportStatus =
                $"Äang nháº­p {suppliersFromFile.Count:N0} nhÃ  cung cáº¥p vÃ o database...";

            StatusMessage = ImportStatus;

            await Task.Yield();

            // BÃ¡o tiáº¿n Ä‘á»™ theo sá»‘ dÃ²ng Ä‘Ã£ xá»­ lÃ½ Ä‘á»ƒ ngÆ°á»i dÃ¹ng khÃ´ng tÆ°á»Ÿng á»©ng cá»©ng.
            var progress = new Progress<int>(daXuLy =>
            {
                ImportStatus =
                    $"Äang nháº­p dÃ²ng {daXuLy:N0}/{suppliersFromFile.Count:N0}...";
            });

            var ketQua = await Task.Run(() =>
                _db.ImportNhaCungCapTuFileAsync(
                    file.Path.LocalPath,
                    suppliersFromFile,
                    progress,
                    capNhatNhaCungCapDaTonTai: true));

            ImportStatus = "Äang cáº­p nháº­t danh sÃ¡ch nhÃ  cung cáº¥p...";
            StatusMessage = ImportStatus;

            await LoadSuppliersAsync();

            StatusMessage = (ketQua.SoThemMoi > 0 || ketQua.SoCapNhat > 0)
                ? $"âœ… ÄÃ£ nháº­p thÃ nh cÃ´ng {ketQua.SoThemMoi:N0}, cáº­p nháº­t {ketQua.SoCapNhat:N0} nhÃ  cung cáº¥p tá»« file \"{file.Name}\". {ketQua.ThongBaoTatCa}"
                : $"âš ï¸ KhÃ´ng cÃ³ nhÃ  cung cáº¥p má»›i nÃ o Ä‘Æ°á»£c nháº­p. {ketQua.ThongBaoTatCa}";

            if (ketQua.SoTrungTenVaDienThoai > 0)
            {
                StatusMessage +=
                    $" Cáº£nh bÃ¡o: {ketQua.SoTrungTenVaDienThoai:N0} nhÃ  cung cáº¥p " +
                    "trÃ¹ng tÃªn vÃ  sá»‘ Ä‘iá»‡n thoáº¡i vá»›i nhÃ  cung cáº¥p Ä‘Ã£ cÃ³.";
            }

            if (ketQua.DanhSachLoi.Count > 0)
            {
                StatusMessage += " " + string.Join(" | ", ketQua.DanhSachLoi.Take(3));
            }
        }
        catch (Exception ex)
        {
            StatusMessage =
                "âŒ KhÃ´ng thá»ƒ nháº­p nhÃ  cung cáº¥p tá»« file: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private async Task XoaNhaCungCapAsync(NhaCungCap? supplier)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersDelete, "Báº¡n khÃ´ng cÃ³ quyá»n xÃ³a nhÃ  cung cáº¥p."))
        {
            return;
        }
        if (supplier is null)
            return;

        if (supplier.Id == 0)
        {
            StatusMessage =
                "NhÃ  cung cáº¥p nÃ y chÆ°a cÃ³ ID trong database.";

            return;
        }

        var confirmed = await ShowConfirmationAsync(
            "XÃ³a nhÃ  cung cáº¥p",
            $"Báº¡n cÃ³ cháº¯c muá»‘n xÃ³a nhÃ  cung cáº¥p " +
            $"\"{supplier.TenNhaCungCap}\" khÃ´ng?");

        if (!confirmed)
            return;

        try
        {
            await _db.XoaNhaCungCapAsync(supplier.Id);

            SelectedNhaCungCap = null;

            StatusMessage =
                $"ÄÃ£ xÃ³a nhÃ  cung cáº¥p {supplier.TenNhaCungCap}";

            // Táº£i láº¡i trang Ä‘á»ƒ sá»‘ trang, tá»•ng vÃ  thá»‘ng kÃª khá»›p vá»›i database.
            await LoadSupplierPageAsync();
        }
        catch (Exception ex)
        {
            StatusMessage =
                "KhÃ´ng thá»ƒ xÃ³a nhÃ  cung cáº¥p: " + ex.Message;
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

    /// <summary>
    /// Äáº·t láº¡i TOÃ€N Bá»˜ tráº¡ng thÃ¡i cá»§a Ä‘Æ¡n hÃ ng Ä‘ang bÃ¡n.
    /// <para>
    /// Báº£n cÅ© chá»‰ xoÃ¡ SaleCart vÃ  vÃ i Ã´ tá»•ng tiá»n, giá»¯ láº¡i TienKhachDua,
    /// TienThoi, TienCongNo, TrangThaiThanhToan, SelectedSaleCustomer vÃ 
    /// SaleCustomerName â€” nÃªn Ä‘Æ¡n thá»© hai káº¿ thá»«a tiá»n cá»§a Ä‘Æ¡n thá»© nháº¥t vÃ 
    /// khÃ¡ch hÃ ng cá»§a Ä‘Æ¡n trÆ°á»›c. HÃ m nÃ y gom táº¥t cáº£ vÃ o má»™t chá»— Ä‘á»ƒ má»i Ä‘Æ°á»ng
    /// vÃ o (má»Ÿ mÃ n bÃ¡n hÃ ng, Ä‘Ã³ng mÃ n, thanh toÃ¡n xong, báº¥m "ÄÆ¡n má»›i") Ä‘á»u
    /// xoÃ¡ tráº¡ng thÃ¡i cÅ© giá»‘ng nhau.
    /// </para>
    /// </summary>
    private void ResetDonHangVeMacDinh()
    {
        foreach (var item in SaleCart)
            item.QuantityChanged -= RecalculateSaleTotal;

        SaleCart.Clear();
        SaleCustomerSuggestions.Clear();
        ProductSearchResults.Clear();

        SelectedSaleCustomer = null;
        SaleCustomerName = "KhÃ¡ch láº»";
        SaleSearchText = "";

        SaleTotal = 0;
        SaleSubtotal = 0;
        SaleDiscountInput = 0;
        SaleDiscountAmount = 0;
        SaleDiscountMode = "Sá»‘ tiá»n";

        TienKhachDua = 0;
        TienThoi = 0;
        TienCongNo = 0;
        TrangThaiThanhToan = "";

        SaleMaHoaDonHienThi = "";
        IsThanhToanDangXuLy = false;
    }

    /// <summary>
    /// Báº¯t Ä‘áº§u má»™t Ä‘Æ¡n hÃ ng má»›i: xoÃ¡ sáº¡ch Ä‘Æ¡n cÅ© rá»“i táº£i láº¡i danh sÃ¡ch sáº£n pháº©m.
    /// DÃ¹ng chung cho lá»‡nh "ðŸ›’ BÃ¡n hÃ ng" vÃ  lá»‡nh "ÄÆ¡n má»›i".
    /// </summary>
    [RelayCommand]
    private async Task MoGiaoDienBanHangAsync()
    {
if (!await KiemTraQuyenAsync(PermissionKeys.SalesView, "Báº¡n khÃ´ng cÃ³ quyá»n má»Ÿ mÃ n hÃ¬nh bÃ¡n hÃ ng."))
        {
            return;
        }

        ResetDonHangVeMacDinh();

        IsSalesPanelVisible = true;

        StatusMessage = "ÄÃ£ táº¡o Ä‘Æ¡n hÃ ng má»›i. Chá»n sáº£n pháº©m Ä‘á»ƒ báº¯t Ä‘áº§u.";

        // Táº¢I DANH SÃCH Sáº¢N PHáº¨M KHI Má»ž BÃN HÃ€NG
        try
        {
            var products = await _db.GetSanPhamAsync();

            foreach (var product in products)
                ProductSearchResults.Add(product);
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ táº£i danh sÃ¡ch sáº£n pháº©m: " + ex.Message;
        }

        RecalculateSaleTotal();
    }

    /// <summary>NÃºt "ÄÆ¡n má»›i" trÃªn mÃ n bÃ¡n hÃ ng: xoÃ¡ sáº¡ch Ä‘Æ¡n hiá»‡n táº¡i Ä‘á»ƒ bÃ¡n tiáº¿p.</summary>
    [RelayCommand]
    private async Task TaoDonMoiAsync()
    {
        if (IsThanhToanDangXuLy)
            return;

        var owner = OwnerWindow;

        if (owner is null)
        {
            StatusMessage = "KhÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c cá»­a sá»• bÃ¡n hÃ ng.";
            return;
        }

        // ÄÆ¡n Ä‘ang cÃ³ hÃ ng chÆ°a thanh toÃ¡n thÃ¬ pháº£i há»i trÆ°á»›c khi xoÃ¡.
        if (SaleCart.Count > 0)
        {
            var confirm = new ConfirmationWindow(
                "Táº¡o Ä‘Æ¡n má»›i",
                "ÄÆ¡n hÃ ng hiá»‡n táº¡i chÆ°a thanh toÃ¡n. XoÃ¡ Ä‘Æ¡n nÃ y vÃ  báº¯t Ä‘áº§u Ä‘Æ¡n má»›i?",
                "Táº¡o Ä‘Æ¡n má»›i",
                "Huá»·");

            if (!await confirm.ShowDialog<bool>(owner))
            {
                StatusMessage = "ÄÃ£ giá»¯ nguyÃªn Ä‘Æ¡n hÃ ng hiá»‡n táº¡i.";
                return;
            }
        }

        await MoGiaoDienBanHangAsync();
    }

    [RelayCommand]
    private void DongGiaoDienBanHang()
    {
        IsSalesPanelVisible = false;

        ResetDonHangVeMacDinh();
    }

    [RelayCommand]
    private async Task ThemVaoGioHangAsync(SanPham? product)
    {
        if (product is null)
            return;

        // Sáº£n pháº©m chÆ°a cÃ³ giÃ¡ bÃ¡n
        if (product.GiaBan <= 0)
        {
            if (OwnerWindow is null)
            {
                StatusMessage = "KhÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c cá»­a sá»• bÃ¡n hÃ ng.";
                return;
            }

            try
            {
                var window = new NhapGiaBanWindow();

                var giaBan = await window.ShowDialog<decimal?>(OwnerWindow);

                // NgÆ°á»i dÃ¹ng báº¥m Há»§y hoáº·c nháº­p giÃ¡ khÃ´ng há»£p lá»‡
                if (!giaBan.HasValue || giaBan.Value <= 0)
                {
                    StatusMessage = "ChÆ°a nháº­p giÃ¡ bÃ¡n. Sáº£n pháº©m chÆ°a Ä‘Æ°á»£c thÃªm vÃ o giá».";
                    return;
                }

                // LÆ°u giÃ¡ bÃ¡n vÃ o database
                var updated =
                    await _db.CapNhatGiaBanSanPhamAsync(
                        product.Id,
                        giaBan.Value);

                if (!updated)
                {
                    StatusMessage = "Sáº£n pháº©m khÃ´ng cÃ²n tá»“n táº¡i trong há»‡ thá»‘ng.";
                    return;
                }

                // Cáº­p nháº­t giÃ¡ cá»§a sáº£n pháº©m Ä‘ang hiá»ƒn thá»‹ vÃ  cÃ¡c mÃ n hÃ¬nh khÃ¡c
                DongBoGiaBan(product.Id, giaBan.Value);
            }
            catch (Exception ex)
            {
                StatusMessage = "KhÃ´ng thá»ƒ cáº­p nháº­t giÃ¡ bÃ¡n: " + ex.Message;
                return;
            }
        }

        // CÃ³ giÃ¡ rá»“i thÃ¬ thÃªm vÃ o giá»
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

        SaleDiscountAmount = SaleDiscountMode == "Pháº§n trÄƒm"
            ? SaleSubtotal * Math.Clamp(SaleDiscountInput, 0, 100) / 100
            : Math.Clamp(SaleDiscountInput, 0, SaleSubtotal);

        SaleTotal = Math.Max(
            0,
            SaleSubtotal - SaleDiscountAmount);

        RecalculatePayment();
    }

    // ============================================================
    // BÃO CÃO
    // ============================================================

    /// <summary>
    /// Danh sÃ¡ch cÃ¡c bÃ¡o cÃ¡o trong nhÃ³m menu "BÃ¡o cÃ¡o".
    /// Muá»‘n thÃªm bÃ¡o cÃ¡o má»›i (bÃ¡n hÃ ng, tá»“n kho, doanh thu...) chá»‰ cáº§n
    /// thÃªm má»™t pháº§n tá»­ ná»¯a vÃ o Ä‘Ã¢y, giao diá»‡n tá»± hiá»ƒn thá»‹ theo.
    /// </summary>
    public ObservableCollection<BaoCaoMenuItem> BaoCaoMenuItems { get; } = new()
    {
        new BaoCaoMenuItem
        {
            Key = "cuoi-ngay",
            TieuDe = "BÃ¡o cÃ¡o cuá»‘i ngÃ y",
            BieuTuong = "ðŸ—“"
        },
        new BaoCaoMenuItem
        {
            Key = "phieu-nhap-hang",
            TieuDe = "BÃ¡o cÃ¡o phiáº¿u nháº­p hÃ ng",
            BieuTuong = "ðŸ“¥"
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

    /// <summary>VÃ­ dá»¥: "1 - 15 trong 367 giao dá»‹ch"</summary>
    public string BaoCaoPhieuNhapRangeText
    {
        get
        {
            if (BaoCaoPhieuNhapTotalCount == 0)
                return "0 giao dá»‹ch";

            var from = (BaoCaoPhieuNhapPage - 1) * BaoCaoPhieuNhapPageSize + 1;
            var to = Math.Min(from + BaoCaoPhieuNhapPageSize - 1, BaoCaoPhieuNhapTotalCount);

            return $"{from} - {to} trong {BaoCaoPhieuNhapTotalCount} giao dá»‹ch";
        }
    }

    public bool HasBaoCaoPhieuNhapData => BaoCaoPhieuNhaps.Count > 0;

    public bool IsBaoCaoPhieuNhapEmpty => BaoCaoPhieuNhaps.Count == 0;

    /// <summary>Tráº¡ng thÃ¡i Ã´ "chá»n táº¥t cáº£" á»Ÿ Ä‘áº§u báº£ng: Ä‘Ã£ chá»n / chá»n má»™t pháº§n / chÆ°a chá»n.</summary>
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

    public bool IsBaoCaoSectionSelected => SelectedMenu == "BÃ¡o cÃ¡o";

    public bool IsBaoCaoPhieuNhapSelected =>
        SelectedMenu == "BÃ¡o cÃ¡o" && SelectedReportKey == "phieu-nhap-hang";

    public bool IsBaoCaoBanHangSelected => SelectedMenu == "BAo cáo" && SelectedReportKey == "ban-hang";
    public bool IsBaoCaoCuoiNgaySelected =>
        SelectedMenu == "BÃ¡o cÃ¡o" && SelectedReportKey == "cuoi-ngay";

    partial void OnBaoCaoPhieuNhapSearchChanged(string value)
    {
        // Má»—i láº§n tÃ¬m kiáº¿m luÃ´n quay vá» trang Ä‘áº§u
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
        OnPropertyChanged(nameof(IsBaoCaoBanHangSelected));
        OnPropertyChanged(nameof(IsBaoCaoCuoiNgaySelected));
    }

    [RelayCommand]
    private void ChonBaoCao(BaoCaoMenuItem? item)
    {
        if (item is null)
            return;

        SelectedMenu = "BÃ¡o cÃ¡o";
        SelectedReportKey = item.Key;

        // Má»—i bÃ¡o cÃ¡o cÃ³ danh sÃ¡ch riÃªng
        BaoCaoPhieuNhapPage = 1;

        if (item.Key == "phieu-nhap-hang" && !_baoCaoPhieuNhapLoaded)
        {
            _baoCaoPhieuNhapLoaded = true;
            _ = LoadBaoCaoPhieuNhapAsync();
        }

        // BÃ¡o cÃ¡o cuá»‘i ngÃ y chá»‰ táº£i khi má»Ÿ láº§n Ä‘áº§u, sau Ä‘Ã³ giá»¯ nguyÃªn ngÃ y Ä‘ang
        // xem trong suá»‘t phiÃªn (ngÆ°á»i dÃ¹ng tá»± báº¥m "LÃ m má»›i" khi cáº§n).
        if (item.Key == "ban-hang" && !_baoCaoBanHangLoaded) { _baoCaoBanHangLoaded = true; _ = BaoCaoBanHang.TaiLaiAsync(); }
          if (item.Key == "cuoi-ngay" && !_baoCaoCuoiNgayLoaded)
        {
            _baoCaoCuoiNgayLoaded = true;
            _ = BaoCaoCuoiNgay.TaiDuLieuAsync();
        }

        StatusMessage = item.TieuDe;
    }

    [RelayCommand]
    private async Task LoadBaoCaoPhieuNhapAsync()
    {
        if (IsLoadingBaoCaoPhieuNhap)
            return;

        IsLoadingBaoCaoPhieuNhap = true;

        // Version guard: bá» qua káº¿t quáº£ cá»§a láº§n táº£i cÅ© náº¿u ngÆ°á»i dÃ¹ng Ä‘Ã£ tÃ¬m/chuyá»ƒn trang má»›i
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

            // Trang vÆ°á»£t quÃ¡ sá»‘ trang thá»±c táº¿ (vÃ­ dá»¥ sau khi tÃ¬m kiáº¿m) -> quay vá» trang cuá»‘i
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
                ? "KhÃ´ng cÃ³ dá»¯ liá»‡u phiáº¿u nháº­p phÃ¹ há»£p"
                : $"ÄÃ£ táº£i {BaoCaoPhieuNhaps.Count} phiáº¿u nháº­p (trang {BaoCaoPhieuNhapPage}/{Math.Max(1, totalPages)})";
        }
        catch (Exception ex)
        {
            if (version == _baoCaoPhieuNhapLoadVersion)
                StatusMessage = "KhÃ´ng thá»ƒ táº£i bÃ¡o cÃ¡o phiáº¿u nháº­p: " + ex.Message;
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
    /// TÃ¬m kiáº¿m khi ngÆ°á»i dÃ¹ng gÃµ: chá»‘ng debounce Ä‘á»ƒ khÃ´ng gá»i database liÃªn tá»¥c.
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
            // NgÆ°á»i dÃ¹ng gÃµ tiáº¿p -> huá»· láº§n tÃ¬m kiáº¿m cÅ©
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

    /// <summary>Äá»“ng bá»™ láº¡i tráº¡ng thÃ¡i Ã´ "chá»n táº¥t cáº£" khi ngÆ°á»i dÃ¹ng báº¥m checkbox cá»§a má»™t dÃ²ng.</summary>
    public void CapNhatTrangThaiChonTatCaBaoCaoPhieuNhap()
    {
        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapAllSelected));
        OnPropertyChanged(nameof(BaoCaoPhieuNhapSelectedCount));
    }

    [RelayCommand]
    private async Task MoGiaoDienNhapHangTuBaoCaoAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.PurchasesView, "Báº¡n khÃ´ng cÃ³ quyá»n má»Ÿ mÃ n hÃ¬nh nháº­p hÃ ng."))
        {
            return;
        }
        // NÃºt "+ Nháº­p hÃ ng" trong bÃ¡o cÃ¡o: chuyá»ƒn sang mÃ n láº­p phiáº¿u nháº­p hÃ ng
        await MoGiaoDienNhapHangAsync();
    }

    // ============================================================
    // PHIáº¾U NHáº¬P HÃ€NG
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

    /// <summary>NgÃ y nháº­p luÃ´n dÃ¹ng kiá»ƒu DateTime vÃ¬ cá»™t thoi_gian_nhap trong database lÃ  DATETIME.</summary>
    [ObservableProperty] private bool isPhieuNhapCalendarOpen;

    private uint _phieuNhapId;

    public string PhieuNhapTongTienHangText => PhieuNhapTongTienHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string PhieuNhapTongTienNhapText => PhieuNhapTongTienNhap.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>NgÃ y nháº­p hiá»ƒn thá»‹ theo Ä‘á»‹nh dáº¡ng Viá»‡t Nam dd/MM/yyyy HH:mm.</summary>
    public string PhieuNhapNgayNhapText => PhieuNhapNgayNhap?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                                           ?? string.Empty;

    /// <summary>CÃ´ng ná»£ cÃ²n pháº£i tráº£ cho nhÃ  cung cáº¥p, khá»›p vá»›i cá»™t con_no trong database.</summary>
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

        // Chá»n xong ngÃ y thÃ¬ Ä‘Ã³ng lá»‹ch luÃ´n
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
        if (!await KiemTraQuyenAsync(PermissionKeys.PurchasesCreate, "Báº¡n khÃ´ng cÃ³ quyá»n táº¡o phiáº¿u nháº­p hÃ ng."))
        {
            return;
        }
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

        PhieuNhapNhanVien = Session.CurrentUser?.HoTen ?? "ChÆ°a Ä‘Äƒng nháº­p";
        PhieuNhapNgayNhap = DateTime.Now;

        try
        {
            PhieuNhapMa = await _db.TaoMaPhieuNhapMoiAsync();
        }
        catch (Exception ex)
        {
            // KhÃ´ng nuá»‘t lá»—i: in ra console/debug Ä‘á»ƒ tháº¥y Ä‘Ãºng lá»—i SQL/connection.
            // Tuyá»‡t Ä‘á»‘i khÃ´ng gÃ¡n chuá»—i táº¡m vÃ o PhieuNhapMa vÃ¬ mÃ n hÃ¬nh sáº½ lÆ°u
            // tháº³ng giÃ¡ trá»‹ nÃ y xuá»‘ng phieu_nhap_hang (lÃ m há»ng dá»¯ liá»‡u mÃ£ phiáº¿u).
            Console.WriteLine("========== Lá»–I Táº O MÃƒ PHIáº¾U NHáº¬P ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("==========================================");

            PhieuNhapMa = "";
            StatusMessage =
                "KhÃ´ng thá»ƒ táº¡o mÃ£ phiáº¿u nháº­p tá»± Ä‘á»™ng: " + ex.Message
                + " (xem console Ä‘á»ƒ biáº¿t chi tiáº¿t lá»—i SQL/káº¿t ná»‘i)";
        }

        RecalculatePhieuNhapTongTienHang();

        await LoadPhieuNhapProductSuggestionsAsync();
    }

    [RelayCommand]
    private async Task MoGiaoDienNhapHangAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.PurchasesView, "Báº¡n khÃ´ng cÃ³ quyá»n má»Ÿ mÃ n hÃ¬nh nháº­p hÃ ng."))
        {
            return;
        }
        // Náº¿u Ä‘ang Ä‘á»©ng á»Ÿ mÃ n Nháº­p hÃ ng thÃ¬ SelectedMenu khÃ´ng Ä‘á»•i,
        // LoadSelectedSectionAsync sáº½ khÃ´ng cháº¡y nÃªn pháº£i tá»± khá»Ÿi táº¡o phiáº¿u má»›i.
        if (SelectedMenu == "Nháº­p hÃ ng")
        {
            await MoPhieuNhapHangAsync();
            return;
        }

        // SelectedMenu Ä‘á»•i => case "Nháº­p hÃ ng" trong LoadSelectedSectionAsync gá»i
        // MoPhieuNhapHangAsync() giÃºp, khÃ´ng gá»i á»Ÿ Ä‘Ã¢y Ä‘á»ƒ trÃ¡nh cháº¡y 2 láº§n.
        SelectedMenu = "Nháº­p hÃ ng";
    }

    [RelayCommand]
    private async Task DongPhieuNhapHangAsync()
    {
        if (PhieuNhapItems.Count > 0)
        {
            var confirmed = await ShowConfirmationAsync(
                "ThoÃ¡t láº­p phiáº¿u nháº­p",
                "Phiáº¿u nháº­p Ä‘ang cÃ³ dá»¯ liá»‡u chÆ°a Ä‘Æ°á»£c lÆ°u. Báº¡n cÃ³ cháº¯c muá»‘n thoÃ¡t?",
                "ThoÃ¡t",
                "Tiáº¿p tá»¥c nháº­p");

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
        // KhÃ´ng gÃ¡n Ä‘Ã¨ khi lá»‡nh Ä‘Æ°á»£c gá»i khÃ´ng cÃ³ tham sá»‘,
        // náº¿u khÃ´ng TextBox sáº½ bá»‹ xoÃ¡ ngay khi vá»«a gÃµ kÃ½ tá»± Ä‘áº§u tiÃªn.
        if (keyword is not null && keyword != ImportProductSearch)
            ImportProductSearch = keyword;

        try
        {
            await LoadPhieuNhapProductSuggestionsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ tÃ¬m hÃ ng hÃ³a: " + ex.Message;
        }
    }

    private async Task LoadPhieuNhapProductSuggestionsAsync()
    {
        var keyword = ImportProductSearch.Trim();

        // RÃºt gá»n danh sÃ¡ch gá»£i Ã½: sáº£n pháº©m Ä‘Ã£ cÃ³ trong phiáº¿u thÃ¬ áº©n Ä‘i
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
            StatusMessage = $"ÄÃ£ tÄƒng sá»‘ lÆ°á»£ng {product.TenSanPham} lÃªn {existing.SoLuong:N0}";
            return;
        }

        var dongMoi = new ChiTietPhieuNhapHang(product);

        // Nghe thay Ä‘á»•i sá»‘ lÆ°á»£ng / giÃ¡ nháº­p / giáº£m giÃ¡ Ä‘á»ƒ tÃ­nh láº¡i tá»•ng realtime
        dongMoi.PropertyChanged += ChiTietPhieuNhapHang_OnPropertyChanged;

        PhieuNhapItems.Add(dongMoi);

        PhieuNhapProductSuggestions.Remove(product);

        OnPropertyChanged(nameof(HasPhieuNhapSuggestions));

        RecalculatePhieuNhapTongTienHang();

        StatusMessage = $"ÄÃ£ thÃªm {product.TenSanPham} vÃ o phiáº¿u nháº­p";
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
            StatusMessage = "Phiáº¿u nháº­p khÃ´ng cÃ³ dÃ²ng hÃ ng nÃ o Ä‘á»ƒ xÃ³a.";
            return;
        }

        var soDong = PhieuNhapItems.Count;

        var confirmed = await ShowConfirmationAsync(
            "XÃ³a táº¥t cáº£ dÃ²ng hÃ ng",
            $"Báº¡n cÃ³ cháº¯c muá»‘n xÃ³a táº¥t cáº£ hÃ ng hÃ³a trong phiáº¿u nháº­p? ({soDong} dÃ²ng sáº½ bá»‹ xÃ³a khá»i phiáº¿u, sáº£n pháº©m trong database khÃ´ng bá»‹ áº£nh hÆ°á»Ÿng)",
            "XÃ³a táº¥t cáº£",
            "Há»§y");

        if (!confirmed)
        {
            StatusMessage = "ÄÃ£ há»§y xÃ³a táº¥t cáº£ dÃ²ng hÃ ng.";
            return;
        }

        BoDangKyPhieuNhapItems();
        PhieuNhapItems.Clear();

        // Tá»•ng tiá»n hÃ ng = 0, Tá»•ng tiá»n nháº­p = Chi phÃ­ khÃ¡c, CÃ²n pháº£i tráº£ = Tá»•ng tiá»n nháº­p - ÄÃ£ thanh toÃ¡n
        RecalculatePhieuNhapTongTienHang();
        RecalculatePhieuNhapTotal();

        StatusMessage = $"ÄÃ£ xÃ³a toÃ n bá»™ {soDong} dÃ²ng hÃ ng khá»i phiáº¿u nháº­p";
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
    /// Ã” tÃ¬m nhÃ  cung cáº¥p Ä‘ang chá»©a Ä‘Ãºng tÃªn NCC vá»«a chá»n
    /// thÃ¬ khÃ´ng cáº§n tÃ¬m láº¡i láº§n ná»¯a.
    /// </summary>
    private bool DaChonNhaCungCapNhap =>
        SelectedPhieuNhapNcc is not null &&
        ImportSupplierSearch.Trim()
            .Equals(SelectedPhieuNhapNcc.TenNhaCungCap, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private async Task TimKiemNhaCungCapNhapAsync(string? keyword)
    {
        // KhÃ´ng gÃ¡n Ä‘Ã¨ khi lá»‡nh Ä‘Æ°á»£c gá»i khÃ´ng cÃ³ tham sá»‘,
        // náº¿u khÃ´ng TextBox sáº½ bá»‹ xoÃ¡ ngay khi vá»«a gÃµ kÃ½ tá»± Ä‘áº§u tiÃªn.
        if (keyword is not null && keyword != ImportSupplierSearch)
            ImportSupplierSearch = keyword;

        try
        {
            PhieuNhapSupplierSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));

            if (string.IsNullOrWhiteSpace(ImportSupplierSearch))
                return;

            // ÄÃ£ chá»n xong NCC thÃ¬ khÃ´ng tÃ¬m láº¡i, trÃ¡nh má»Ÿ láº¡i danh sÃ¡ch gá»£i Ã½
            if (DaChonNhaCungCapNhap)
                return;

            var suppliers = await _db.GetNhaCungCapAsync(ImportSupplierSearch);

            foreach (var supplier in suppliers.Take(20))
                PhieuNhapSupplierSuggestions.Add(supplier);
            OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ tÃ¬m nhÃ  cung cáº¥p: " + ex.Message;
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

        StatusMessage = $"ÄÃ£ chá»n nhÃ  cung cáº¥p {supplier.TenNhaCungCap}";
    }

    [RelayCommand]
    private async Task ThemNhaCungCapNhapNhanhAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersCreate, "Báº¡n khÃ´ng cÃ³ quyá»n thÃªm nhÃ  cung cáº¥p."))
        {
            return;
        }
        try
        {
            // TÃ¡i sá»­ dá»¥ng Ä‘Ãºng form thÃªm nhÃ  cung cáº¥p sáºµn cÃ³
            var supplier = await HienThiFormThemNhaCungCapAsync();

            if (supplier is null)
                return;

            // Tá»± Ä‘á»™ng chá»n nhÃ  cung cáº¥p vá»«a thÃªm cho phiáº¿u nháº­p
            SelectedPhieuNhapNcc = supplier;
            ImportSupplierSearch = supplier.TenNhaCungCap;
            PhieuNhapSupplierSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));

            StatusMessage = $"ÄÃ£ thÃªm vÃ  chá»n nhÃ  cung cáº¥p {supplier.TenNhaCungCap}";
        }
        catch (Exception ex)
        {
            StatusMessage = "KhÃ´ng thá»ƒ thÃªm nhÃ  cung cáº¥p: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task HoanThanhPhieuNhapAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.PurchasesCreate, "Báº¡n khÃ´ng cÃ³ quyá»n lÆ°u phiáº¿u nháº­p hÃ ng."))
        {
            return;
        }
        if (IsSavingPhieuNhap)
            return;

        // ------------------------------------------
        // VALIDATE
        // Má»i lá»—i nghiá»‡p vá»¥ Ä‘á»u bÃ¡o qua há»™p thoáº¡i vÃ  GIá»® NGUYÃŠN form,
        // khÃ´ng bao giá» xÃ³a dá»¯ liá»‡u Ä‘ang nháº­p.
        // Má»i lá»—i nghiá»‡p vá»¥ Ä‘á»u bÃ¡o qua há»™p thoáº¡i vÃ  GIá»® NGUYÃŠN form,
        // khÃ´ng bao giá» xÃ³a dá»¯ liá»‡u Ä‘ang nháº­p.
        // ------------------------------------------

        // 1. Phiáº¿u pháº£i cÃ³ Ã­t nháº¥t 1 hÃ ng hÃ³a
        if (PhieuNhapItems.Count == 0)
        {
            await BaoLoiLuuPhieuNhapAsync("Phiáº¿u nháº­p chÆ°a cÃ³ hÃ ng hÃ³a.");
            return;
        }

        // 2. Sá»‘ lÆ°á»£ng má»—i hÃ ng pháº£i > 0
        var dongSoLuongSai = PhieuNhapItems
            .Where(item => item.SanPham.Id != 0 && item.SoLuong <= 0)
            .ToList();

        if (dongSoLuongSai.Count > 0)
        {
            await BaoLoiLuuPhieuNhapAsync(dongSoLuongSai.Count == 1
                ? $"Vui lÃ²ng kiá»ƒm tra sá»‘ lÆ°á»£ng cá»§a hÃ ng \"{dongSoLuongSai[0].SanPham.TenSanPham}\" (pháº£i lá»›n hÆ¡n 0)."
                : $"Vui lÃ²ng kiá»ƒm tra sá»‘ lÆ°á»£ng hÃ ng hÃ³a: cÃ²n {dongSoLuongSai.Count} dÃ²ng chÆ°a nháº­p sá»‘ lÆ°á»£ng.");
            return;
        }

        // 3. GiÃ¡ nháº­p pháº£i há»£p lá»‡ (khÃ´ng Ã¢m)
        var dongGiaSai = PhieuNhapItems
            .Where(item => item.SanPham.Id != 0 && item.DonGiaNhap < 0)
            .ToList();

        if (dongGiaSai.Count > 0)
        {
            await BaoLoiLuuPhieuNhapAsync(dongGiaSai.Count == 1
                ? $"Vui lÃ²ng kiá»ƒm tra giÃ¡ nháº­p cá»§a hÃ ng \"{dongGiaSai[0].SanPham.TenSanPham}\"."
                : $"Vui lÃ²ng kiá»ƒm tra giÃ¡ nháº­p: cÃ²n {dongGiaSai.Count} dÃ²ng cÃ³ giÃ¡ nháº­p khÃ´ng há»£p lá»‡.");
            return;
        }

        // Chá»‰ láº¥y nhá»¯ng dÃ²ng thá»±c sá»± gáº¯n vá»›i má»™t sáº£n pháº©m trong database
        var dongHopLe = PhieuNhapItems
            .Where(item => item.SanPham.Id != 0 && item.SoLuong > 0)
            .ToList();

        if (dongHopLe.Count == 0)
        {
            await BaoLoiLuuPhieuNhapAsync("Phiáº¿u nháº­p chÆ°a cÃ³ hÃ ng hÃ³a há»£p lá»‡ nÃ o Ä‘á»ƒ hoÃ n thÃ nh.");
            return;
        }

        // 4. NhÃ  cung cáº¥p lÃ  trÆ°á»ng báº¯t buá»™c khi hoÃ n thÃ nh phiáº¿u
        if (SelectedPhieuNhapNcc is null || SelectedPhieuNhapNcc.Id == 0)
        {
            await BaoLoiLuuPhieuNhapAsync("Vui lÃ²ng chá»n nhÃ  cung cáº¥p.");
            return;
        }

        // 5. NgÃ y nháº­p há»£p lá»‡
        if (PhieuNhapNgayNhap is null)
        {
            await BaoLoiLuuPhieuNhapAsync("Vui lÃ²ng chá»n ngÃ y nháº­p.");
            return;
        }

        // 6. MÃ£ phiáº¿u pháº£i sinh Ä‘Æ°á»£c trÆ°á»›c khi lÆ°u
        if (string.IsNullOrWhiteSpace(PhieuNhapMa))
        {
            await BaoLoiLuuPhieuNhapAsync("ChÆ°a táº¡o Ä‘Æ°á»£c mÃ£ phiáº¿u nháº­p, vui lÃ²ng má»Ÿ láº¡i mÃ n hÃ¬nh vÃ  thá»­ láº¡i.");
            return;
        }

        var confirmed = await ShowConfirmationAsync(
            "HoÃ n thÃ nh phiáº¿u nháº­p",
            "Tá»“n kho sáº½ Ä‘Æ°á»£c cá»™ng thÃªm sá»‘ lÆ°á»£ng vá»«a nháº­p vÃ  giÃ¡ vá»‘n Ä‘Æ°á»£c cáº­p nháº­t. Báº¡n cÃ³ cháº¯c muá»‘n tiáº¿p tá»¥c?",
            "HoÃ n thÃ nh",
            "Há»§y");

        if (!confirmed)
            return;

        IsSavingPhieuNhap = true;

        var maPhieuVuaLuu = PhieuNhapMa;
        var tongTienVuaLuu = PhieuNhapTongTienNhapText;

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

            // LÆ°u header + chi tiáº¿t + tá»“n kho + giÃ¡ vá»‘n + cÃ´ng ná»£ trong Má»˜T transaction.
            // Service tá»± rollback toÃ n bá»™ náº¿u báº¥t ká»³ bÆ°á»›c nÃ o lá»—i.
            _phieuNhapId = await _db.LuuPhieuNhapHangAsync(phieu, dongHopLe);
        }
        catch (Exception ex)
        {
            // LÆ°u tháº¥t báº¡i: KHÃ”NG reset, KHÃ”NG chuyá»ƒn mÃ n hÃ¬nh, giá»¯ nguyÃªn
            // toÃ n bá»™ dá»¯ liá»‡u Ä‘ang nháº­p Ä‘á»ƒ ngÆ°á»i dÃ¹ng sá»­a rá»“i báº¥m láº¡i.
            SafeLog.WriteError("Loi luu phieu nhap hang", ex);

            await BaoLoiLuuPhieuNhapAsync(ex.Message);
            return;
        }
        finally
        {
            IsSavingPhieuNhap = false;
        }

        // ---- ÄÃ£ lÆ°u thÃ nh cÃ´ng ----

        // Phiáº¿u vá»«a lÆ°u pháº£i xuáº¥t hiá»‡n trong "BÃ¡o cÃ¡o â†’ BÃ¡o cÃ¡o phiáº¿u nháº­p hÃ ng"
        // nÃªn Ä‘Ã¡nh dáº¥u cáº§n táº£i láº¡i thay vÃ¬ giá»¯ cache cÅ©.
        _baoCaoPhieuNhapLoaded = false;

        // Dashboard thá»‘ng kÃª phiáº¿u nháº­p trong hoáº¡t Ä‘á»™ng gáº§n Ä‘Ã¢y vÃ  thá»‘ng kÃª kho.
        _ = LamMoiTongQuanNeuDangMoAsync();

        // Tá»“n kho vÃ  giÃ¡ vá»‘n Ä‘Ã£ Ä‘á»•i -> náº¡p láº¡i danh sÃ¡ch sáº£n pháº©m.
        // Bá»c try/catch riÃªng: náº¡p láº¡i tháº¥t báº¡i khÃ´ng Ä‘Æ°á»£c lÃ m há»ng káº¿t quáº£
        // Ä‘Ã£ commit (phiáº¿u váº«n lÆ°u thÃ nh cÃ´ng).
        try
        {
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi tai lai danh sach san pham sau khi nhap hang", ex);
        }

        // BÃ¡o thÃ nh cÃ´ng vÃ  CHá»œ ngÆ°á»i dÃ¹ng Ä‘Ã³ng há»™p thoáº¡i.
        StatusMessage =
            $"âœ… Phiáº¿u nháº­p {maPhieuVuaLuu} Ä‘Ã£ Ä‘Æ°á»£c lÆ°u - tá»•ng {tongTienVuaLuu} Ä‘.";

        await ShowMessageAsync(
            "ThÃ nh cÃ´ng â€“ Phiáº¿u nháº­p Ä‘Ã£ Ä‘Æ°á»£c lÆ°u.",
            $"MÃ£ phiáº¿u: {maPhieuVuaLuu}\nTá»•ng tiá»n nháº­p: {tongTienVuaLuu} Ä‘");

        // Chá»‰ reset SAU khi há»™p thoáº¡i thÃ nh cÃ´ng Ä‘Ã£ Ä‘Ã³ng, vÃ  váº«n giá»¯ nguyÃªn
        // mÃ n hÃ¬nh "Láº­p phiáº¿u nháº­p hÃ ng" (IsNhapHangFormVisible váº«n = true).
        // MoPhieuNhapHangAsync() xÃ³a hÃ ng hÃ³a, nhÃ  cung cáº¥p, sá»‘ hÃ³a Ä‘Æ¡n Ä‘áº§u vÃ o,
        // ghi chÃº, chi phÃ­ khÃ¡c, Ä‘Ã£ thanh toÃ¡n, ngÃ y nháº­p vá» hÃ´m nay,
        // tÃ­nh láº¡i tá»•ng tiá»n vÃ  SINH MÃƒ PHIáº¾U Má»šI tá»« database.
        await MoPhieuNhapHangAsync();

        // MoPhieuNhapHangAsync() Ä‘á»ƒ PhieuNhapMa = "" náº¿u sinh mÃ£ tháº¥t báº¡i
        // (lá»—i SQL/káº¿t ná»‘i). KhÃ´ng Ä‘Æ°á»£c bÃ¡o "sáºµn sÃ ng" khi mÃ£ cÃ²n rá»—ng.
        StatusMessage = string.IsNullOrWhiteSpace(PhieuNhapMa)
            ? $"âœ… ÄÃ£ lÆ°u phiáº¿u nháº­p {maPhieuVuaLuu}. KhÃ´ng táº¡o Ä‘Æ°á»£c mÃ£ phiáº¿u má»›i, vui lÃ²ng má»Ÿ láº¡i mÃ n hÃ¬nh."
            : $"âœ… ÄÃ£ lÆ°u phiáº¿u nháº­p {maPhieuVuaLuu}. Sáºµn sÃ ng láº­p phiáº¿u má»›i: {PhieuNhapMa}";
    }

    partial void OnTienKhachDuaChanged(decimal value)
{
    RecalculatePayment();
}

/// <summary>
/// Tá»•ng tiá»n thay Ä‘á»•i thÃ¬ tiá»n thá»‘i / cÃ´ng ná»£ / tráº¡ng thÃ¡i pháº£i tÃ­nh láº¡i.
/// Báº£n cÅ© chá»‰ gáº¯n hook cho TienKhachDua nÃªn khi tá»•ng tiá»n Ä‘á»•i (thÃªm hÃ ng,
/// Ä‘á»•i sá»‘ lÆ°á»£ng, giáº£m giÃ¡) cÃ¡c Ã´ nÃ y giá»¯ sá»‘ cÅ© â€” vÃ  sau khi thanh toÃ¡n
/// xong chÃºng cÅ©ng khÃ´ng tá»± vá» 0.
/// </summary>
partial void OnSaleTotalChanged(decimal value)
{
    RecalculatePayment();
}

private void RecalculatePayment()
{
    var canTra = Math.Max(0, SaleTotal);
    var khachDua = Math.Max(0, TienKhachDua);

    if (canTra <= 0)
    {
        TienThoi = 0;
        TienCongNo = 0;
        TrangThaiThanhToan = "";
        return;
    }

    if (khachDua > canTra)
    {
        TienThoi = khachDua - canTra;
        TienCongNo = 0;
        TrangThaiThanhToan = "ÄÃ£ thanh toÃ¡n Ä‘á»§ - Tiá»n thá»‘i";
    }
    else if (khachDua < canTra)
    {
        TienThoi = 0;
        TienCongNo = canTra - khachDua;

        if (khachDua == 0)
            TrangThaiThanhToan = "ChÆ°a thanh toÃ¡n - CÃ´ng ná»£";
        else
            TrangThaiThanhToan = "Thanh toÃ¡n thiáº¿u - CÃ´ng ná»£";
    }
    else
    {
        TienThoi = 0;
        TienCongNo = 0;
        TrangThaiThanhToan = "ÄÃ£ thanh toÃ¡n Ä‘á»§";
    }
}
partial void OnSaleDiscountInputChanged(decimal value)
{
    if (SaleDiscountMode == "Sá»‘ tiá»n")
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

/// <summary>
/// Thanh toÃ¡n Ä‘Æ¡n hÃ ng Ä‘ang bÃ¡n.
/// <para>
/// Thá»© tá»± báº¯t buá»™c: khoÃ¡ chá»‘ng báº¥m hai láº§n â†’ kiá»ƒm tra dá»¯ liá»‡u â†’ xÃ¡c nháº­n
/// ngÆ°á»i dÃ¹ng â†’ LÆ¯U VÃ€O DATABASE (hÃ³a Ä‘Æ¡n + chi tiáº¿t + tá»“n kho + cÃ´ng ná»£
/// trong má»™t transaction) â†’ chá»‰ khi lÆ°u thÃ nh cÃ´ng má»›i má»Ÿ hÃ³a Ä‘Æ¡n vÃ  bÃ¡o
/// "thanh toÃ¡n thÃ nh cÃ´ng" â†’ cuá»‘i cÃ¹ng xoÃ¡ sáº¡ch Ä‘Æ¡n cÅ© Ä‘á»ƒ bÃ¡n Ä‘Æ¡n tiáº¿p theo.
/// Náº¿u database lá»—i thÃ¬ rollback toÃ n bá»™, giá»¯ nguyÃªn Ä‘Æ¡n hÃ ng vÃ  bÃ¡o lá»—i
/// tháº­t, tuyá»‡t Ä‘á»‘i khÃ´ng bÃ¡o thÃ nh cÃ´ng giáº£.
/// </para>
/// </summary>
[RelayCommand]
private async Task ThanhToanAsync()
{
if (!await KiemTraQuyenAsync(PermissionKeys.SalesCreate, "Báº¡n khÃ´ng cÃ³ quyá»n bÃ¡n hÃ ng."))
    {
        return;
    }

    // 1. Chá»‘ng báº¥m nÃºt nhiá»u láº§n / gá»i lá»‡nh chá»“ng nhau
    if (IsThanhToanDangXuLy)
        return;

    // 2. Kiá»ƒm tra dá»¯ liá»‡u
    if (SaleCart.Count == 0)
    {
        StatusMessage = "Vui lÃ²ng chá»n Ã­t nháº¥t má»™t sáº£n pháº©m trÆ°á»›c khi thanh toÃ¡n.";
        return;
    }

    var sanPhamChuaCoGia = SaleCart.FirstOrDefault(x => x.SanPham.GiaBan <= 0);

    if (sanPhamChuaCoGia is not null)
    {
        StatusMessage =
            $"Sáº£n pháº©m \"{sanPhamChuaCoGia.SanPham.TenSanPham}\" chÆ°a cÃ³ giÃ¡ bÃ¡n, khÃ´ng thá»ƒ thanh toÃ¡n.";
        return;
    }

    var soLuongSai = SaleCart.FirstOrDefault(x => x.SoLuong <= 0);

    if (soLuongSai is not null)
    {
        StatusMessage =
            $"Sá»‘ lÆ°á»£ng cá»§a \"{soLuongSai.SanPham.TenSanPham}\" pháº£i lá»›n hÆ¡n 0.";
        return;
    }

    if (SaleTotal < 0)
    {
        StatusMessage = "Tá»•ng tiá»n khÃ´ng há»£p lá»‡. Vui lÃ²ng kiá»ƒm tra láº¡i giá» hÃ ng.";
        return;
    }

    if (OwnerWindow is null)
    {
        StatusMessage = "KhÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c cá»­a sá»• bÃ¡n hÃ ng.";
        return;
    }

    var owner = OwnerWindow;

    // Chá»¥p láº¡i dá»¯ liá»‡u Ä‘Æ¡n hÃ ng TRÆ¯á»šC khi má»Ÿ há»™p thoáº¡i, trÃ¡nh viá»‡c ngÆ°á»i
    // dÃ¹ng sá»­a giá» hÃ ng lÃºc Ä‘ang xÃ¡c nháº­n lÃ m lá»‡ch sá»‘ liá»‡u.
    var cartSnapshot = SaleCart.ToList();
    var customer = SelectedSaleCustomer;
    var customerName = customer?.TenKhachHang ?? "KhÃ¡ch láº»";
    var discountSnapshot = SaleDiscountAmount;
    var cashSnapshot = TienKhachDua;

    // 3. XÃ¡c nháº­n thanh toÃ¡n
    var confirmWindow = new ThanhToanConfirmWindow(
        customerName,
        SaleTotal,
        TienKhachDua,
        TienThoi,
        TienCongNo);

    if (!await confirmWindow.ShowDialog<bool>(owner))
    {
        StatusMessage = "ÄÃ£ há»§y thanh toÃ¡n.";
        return;
    }

    // 4. LÆ°u vÃ o database
    HoaDonBanHangReceipt? receipt = null;

    IsThanhToanDangXuLy = true;
    StatusMessage = "Äang lÆ°u hÃ³a Ä‘Æ¡n...";

    try
    {
        receipt = await _db.TaoHoaDonBanHangAsync(
            cartSnapshot,
            customer,
            discountSnapshot,
            cashSnapshot);
    }
    catch (Exception ex)
    {
        // Giao dá»‹ch Ä‘Ã£ Ä‘Æ°á»£c rollback trong DatabaseService: giá»¯ nguyÃªn Ä‘Æ¡n
        // hÃ ng Ä‘á»ƒ ngÆ°á»i dÃ¹ng sá»­a rá»“i thá»­ láº¡i, tuyá»‡t Ä‘á»‘i khÃ´ng bÃ¡o thÃ nh cÃ´ng.
        StatusMessage =
            $"KHÃ”NG thanh toÃ¡n Ä‘Æ°á»£c - dá»¯ liá»‡u chÆ°a Ä‘Æ°á»£c lÆ°u vÃ o database: {ex.Message}";

        SafeLog.WriteError("Loi thanh toan ban hang", ex);

        return;
    }
    finally
    {
        IsThanhToanDangXuLy = false;
    }

    // 5. LÆ°u thÃ nh cÃ´ng má»›i dá»n Ä‘Æ¡n cÅ©, khÃ´ng lÃ m máº¥t dá»¯ liá»‡u Ä‘Æ¡n vá»«a bÃ¡n
    var maHoaDon = receipt.MaHoaDon;

    ResetDonHangVeMacDinh();

    _invoicesLoaded = false;
    ChonMenu("HÃ³a Ä‘Æ¡n");

    // Dashboard pháº£i tháº¥y ngay hÃ³a Ä‘Æ¡n vá»«a táº¡o (doanh thu, sá»‘ hÃ³a Ä‘Æ¡n,
    // hoáº¡t Ä‘á»™ng gáº§n Ä‘Ã¢y, top hÃ ng bÃ¡n cháº¡y, top khÃ¡ch hÃ ng).
    _ = LamMoiTongQuanNeuDangMoAsync();

    // Sá»• quá»¹ tá»± Ä‘á»c láº¡i database má»—i láº§n má»Ÿ menu nÃªn phiáº¿u thu má»›i sáº½
    // hiá»‡n ra ngay khi ngÆ°á»i dÃ¹ng chuyá»ƒn sang tab "Sá»• quá»¹".
    var thongTinPhieuThu = string.IsNullOrWhiteSpace(receipt.MaPhieuThu)
        ? ""
        : $" ÄÃ£ ghi phiáº¿u thu {receipt.MaPhieuThu} vÃ o sá»• quá»¹.";

    var thongBaoThanhToan = receipt.CanhBao.Count > 0
        ? $"ÄÃ£ táº¡o hÃ³a Ä‘Æ¡n {maHoaDon}. {string.Join(" ", receipt.CanhBao)}"
        : $"ÄÃ£ táº¡o hÃ³a Ä‘Æ¡n {maHoaDon} thÃ nh cÃ´ng.";

    StatusMessage = thongBaoThanhToan + thongTinPhieuThu;

    // 6. Hiá»ƒn thá»‹ hÃ³a Ä‘Æ¡n vá»«a bÃ¡n (Ä‘á»c láº¡i tá»« database nÃªn khá»›p dá»¯ liá»‡u Ä‘Ã£ lÆ°u)
    try
    {
        // Gắn thông tin tiệm lúc mở hóa đơn (không phải lúc thanh toán) để người
        // dùng vừa sửa ở màn "Thông tin tiệm" cũng thấy ngay ở hóa đơn này.
        receipt.ThongTinTiem = await LayThongTinTiemChoHoaDonAsync();

        var invoiceWindow = new InvoiceWindow(receipt);
        await invoiceWindow.ShowDialog(owner);
    }
    catch (Exception ex)
    {
        StatusMessage =
            $"ÄÃ£ táº¡o hÃ³a Ä‘Æ¡n {maHoaDon} nhÆ°ng khÃ´ng má»Ÿ Ä‘Æ°á»£c cá»­a sá»• hÃ³a Ä‘Æ¡n: {ex.Message}";

        SafeLog.WriteError("Loi mo cua so hoa don", ex);
    }
}

/// <summary>Má»Ÿ láº¡i má»™t hÃ³a Ä‘Æ¡n Ä‘Ã£ cÃ³ Ä‘á»ƒ xem / in (Ä‘á»c dá»¯ liá»‡u tá»« database).</summary>
[RelayCommand]
private async Task XemHoaDonAsync(HoaDon? hoaDon)
{
    if (hoaDon is null || OwnerWindow is null)
        return;

    var maHoaDon = hoaDon.MaHoaDon;

    if (string.IsNullOrWhiteSpace(maHoaDon))
        return;

    try
    {
        var receipt = await _db.GetHoaDonBanHangTheoMaAsync(maHoaDon);

        if (receipt is null)
        {
            StatusMessage = $"KhÃ´ng tÃ¬m tháº¥y hÃ³a Ä‘Æ¡n {maHoaDon} trong database.";
            return;
        }

        receipt.ThongTinTiem = await LayThongTinTiemChoHoaDonAsync();

        var invoiceWindow = new InvoiceWindow(receipt);
        await invoiceWindow.ShowDialog(OwnerWindow);
    }
    catch (Exception ex)
    {
        StatusMessage = $"KhÃ´ng má»Ÿ Ä‘Æ°á»£c hÃ³a Ä‘Æ¡n {maHoaDon}: {ex.Message}";

        SafeLog.WriteError("Loi xem hoa don", ex);
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
        "KhÃ¡ch hÃ ng" => 1,
        "HÃ³a Ä‘Æ¡n" => 2,
        "NhÃ  cung cáº¥p" => 3,
        "Nháº­p hÃ ng" => 4,
        "Sá»• quá»¹" => 5,
        _ => 0
    };

    // Rá»i khá»i mÃ n láº­p phiáº¿u nháº­p thÃ¬ Ä‘Ã³ng láº¡i lá»‹ch Ä‘ang má»Ÿ
    if (value != "Nháº­p hÃ ng")
        IsPhieuNhapCalendarOpen = false;

    OnPropertyChanged(nameof(IsTongQuanSelected));
    OnPropertyChanged(nameof(IsDataSectionSelected));
    OnPropertyChanged(nameof(IsBaoCaoSectionSelected));
    OnPropertyChanged(nameof(IsBaoCaoPhieuNhapSelected));
<<<<<<< HEAD
    OnPropertyChanged(nameof(IsBaoCaoCuoiNgaySelected));
    OnPropertyChanged(nameof(IsThongTinTiemSelected));
=======
    OnPropertyChanged(nameof(IsBaoCaoBanHangSelected));
        OnPropertyChanged(nameof(IsBaoCaoCuoiNgaySelected));
>>>>>>> origin/main

    _ = LoadSelectedSectionAsync(value);
}

/// <summary>ThÃ´ng bÃ¡o láº¡i toÃ n bá»™ cá» quyá»n chi tiáº¿t sau khi phiÃªn Ä‘Äƒng nháº­p thay Ä‘á»•i.</summary>
public void RaiseAllCanProperties()
{
    string[] names =
    [
        nameof(CanCreateProduct), nameof(CanEditProduct), nameof(CanDeleteProduct),
            nameof(CanImportProduct), nameof(CanExportProduct), nameof(CanUpdateProductPrice),
            nameof(CanCreateSale), nameof(CanEditSale), nameof(CanDeleteSale),
            nameof(CanPaySale), nameof(CanViewSaleHistory),
            nameof(CanPrintInvoice), nameof(CanImportInvoice), nameof(CanCancelInvoice),
            nameof(CanCreateCustomer), nameof(CanEditCustomer), nameof(CanDeleteCustomer), nameof(CanImportCustomer),
            nameof(CanCreateSupplier), nameof(CanEditSupplier), nameof(CanDeleteSupplier),
            nameof(CanImportSupplier), nameof(CanExportSupplier),
            nameof(CanCreatePurchase), nameof(CanEditPurchase), nameof(CanDeletePurchase),
            nameof(CanImportPurchase), nameof(CanExportPurchase), nameof(CanPrintPurchase),
            nameof(CanCreateReceipt), nameof(CanCreatePayment), nameof(CanEditCashBook),
            nameof(CanDeleteCashBook), nameof(CanCancelCashBook),
            nameof(CanImportCashBook), nameof(CanExportCashBook),
            nameof(CanViewStoreInfo), nameof(CanEditStoreInfo),
            nameof(CanCreateUser), nameof(CanEditUser), nameof(CanDeleteUser),
            nameof(CanAssignPermission), nameof(CanEditSettings)
    ];

    foreach (var name in names)
    {
        OnPropertyChanged(name);
    }
}

public bool IsTongQuanSelected => SelectedMenu == "Tá»•ng quan";

<<<<<<< HEAD
// Báo cáo có vùng nội dung riêng nên không dùng chung TabControl dữ liệu
// Màn nào có vùng nội dung riêng thì không được bật TabControl dữ liệu,
// nếu không TabControl sẽ hiện chồng lên màn đó.
public bool IsDataSectionSelected =>
    !IsTongQuanSelected
    && !IsBaoCaoSectionSelected
    && !IsThongTinTiemSelected;

/// <summary>Màn "Thông tin tiệm" có vùng nội dung riêng, không nằm trong TabControl dữ liệu.</summary>
public bool IsThongTinTiemSelected => SelectedMenu == "Thông tin tiệm";

/// <summary>Được phép xem màn thông tin tiệm (dùng chung nhóm quyền Cài đặt).</summary>
public bool CanViewStoreInfo => CanViewSettings;

/// <summary>Được phép sửa thông tin tiệm (cùng nhóm quyền Cài đặt).</summary>
public bool CanEditStoreInfo => CanEditSettings;
=======
// BÃ¡o cÃ¡o cÃ³ vÃ¹ng ná»™i dung riÃªng nÃªn khÃ´ng dÃ¹ng chung TabControl dá»¯ liá»‡u
public bool IsDataSectionSelected => !IsTongQuanSelected && !IsBaoCaoSectionSelected;
>>>>>>> origin/main
public bool CanViewDashboard => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.DashboardView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewProducts => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewPurchases => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.PurchasesView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewInvoices => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.InvoicesView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewSales => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SalesView) || Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SalesCreate) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewCustomers => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CustomersView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewSuppliers => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SuppliersView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewCashBook => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CashBookView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewReports => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ReportsView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewUsers => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersView) || Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersCreate) || Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewSettings => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SettingsView) || Quyen.IsAdmin(Session.CurrentUser);

public bool CanManageUsers
{
    get
    {
        var u = Session.CurrentUser;
        if (u == null) return false;
        if (Quyen.IsAdmin(u)) return true;
        return Session.HasPermission(PermissionKeys.UsersView) || Session.HasPermission(PermissionKeys.UsersAssignPermission) || Session.HasPermission(PermissionKeys.UsersCreate);
    }
}

// ===== Quyá»n theo tá»«ng chá»©c nÄƒng chi tiáº¿t (dÃ¹ng cho IsVisible/IsEnabled cá»§a nÃºt) =====

private static bool CoQuyen(string key) => Quyen.HasPermission(Session.CurrentUser, key);

// HÃ ng hÃ³a
public bool CanCreateProduct => CoQuyen(PermissionKeys.ProductsCreate);
public bool CanEditProduct => CoQuyen(PermissionKeys.ProductsEdit);
public bool CanDeleteProduct => CoQuyen(PermissionKeys.ProductsDelete);
public bool CanImportProduct => CoQuyen(PermissionKeys.ProductsImport);
public bool CanExportProduct => CoQuyen(PermissionKeys.ProductsExport);
public bool CanUpdateProductPrice => CoQuyen(PermissionKeys.ProductsUpdatePrice);

// BÃ¡n hÃ ng / HÃ³a Ä‘Æ¡n
public bool CanCreateSale => CoQuyen(PermissionKeys.SalesCreate);
public bool CanEditSale => CoQuyen(PermissionKeys.SalesEdit);
public bool CanDeleteSale => CoQuyen(PermissionKeys.SalesDelete);
public bool CanPaySale => CoQuyen(PermissionKeys.SalesPayment);
public bool CanViewSaleHistory => CoQuyen(PermissionKeys.SalesHistory);
public bool CanPrintInvoice => CoQuyen(PermissionKeys.InvoicesPrint);
public bool CanImportInvoice => CoQuyen(PermissionKeys.InvoicesImport);
public bool CanCancelInvoice => CoQuyen(PermissionKeys.InvoicesCancel);

// KhÃ¡ch hÃ ng
public bool CanCreateCustomer => CoQuyen(PermissionKeys.CustomersCreate);
public bool CanEditCustomer => CoQuyen(PermissionKeys.CustomersEdit);
public bool CanDeleteCustomer => CoQuyen(PermissionKeys.CustomersDelete);
public bool CanImportCustomer => CoQuyen(PermissionKeys.CustomersImport);

// NhÃ  cung cáº¥p
public bool CanCreateSupplier => CoQuyen(PermissionKeys.SuppliersCreate);
public bool CanEditSupplier => CoQuyen(PermissionKeys.SuppliersEdit);
public bool CanDeleteSupplier => CoQuyen(PermissionKeys.SuppliersDelete);
public bool CanImportSupplier => CoQuyen(PermissionKeys.SuppliersImport);
public bool CanExportSupplier => CoQuyen(PermissionKeys.SuppliersExport);

// Phiáº¿u nháº­p
public bool CanCreatePurchase => CoQuyen(PermissionKeys.PurchasesCreate);
public bool CanEditPurchase => CoQuyen(PermissionKeys.PurchasesEdit);
public bool CanDeletePurchase => CoQuyen(PermissionKeys.PurchasesDelete);
public bool CanImportPurchase => CoQuyen(PermissionKeys.PurchasesImport);
public bool CanExportPurchase => CoQuyen(PermissionKeys.PurchasesExport);
public bool CanPrintPurchase => CoQuyen(PermissionKeys.PurchasesPrint);

// Sá»• quá»¹
public bool CanCreateReceipt => CoQuyen(PermissionKeys.CashBookCreateReceipt);
public bool CanCreatePayment => CoQuyen(PermissionKeys.CashBookCreatePayment);
public bool CanEditCashBook => CoQuyen(PermissionKeys.CashBookEdit);
public bool CanDeleteCashBook => CoQuyen(PermissionKeys.CashBookDelete);
public bool CanCancelCashBook => CoQuyen(PermissionKeys.CashBookCancel);
public bool CanImportCashBook => CoQuyen(PermissionKeys.CashBookImport);
public bool CanExportCashBook => CoQuyen(PermissionKeys.CashBookExport);

// TÃ i khoáº£n
public bool CanCreateUser => CoQuyen(PermissionKeys.UsersCreate);
public bool CanEditUser => CoQuyen(PermissionKeys.UsersEdit);
public bool CanDeleteUser => CoQuyen(PermissionKeys.UsersDelete);
public bool CanAssignPermission => CoQuyen(PermissionKeys.UsersAssignPermission);

// CÃ i Ä‘áº·t
public bool CanEditSettings => CoQuyen(PermissionKeys.SettingsEdit);

/// <summary>Kiá»ƒm tra quyá»n ngay táº¡i thá»i Ä‘iá»ƒm gá»i lá»‡nh (báº¯t Ä‘Æ°á»£c cáº£ trÆ°á»ng há»£p gá»i trá»±c tiáº¿p).</summary>
private async Task<bool> KiemTraQuyenAsync(string key, string message)
{
    if (CoQuyen(key))
    {
        return true;
    }

    await ShowMessageAsync("ThÃ´ng bÃ¡o", message);
    return false;
}

}




