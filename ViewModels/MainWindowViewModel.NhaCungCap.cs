using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

/// <summary>
/// Phần ViewModel riêng cho màn hình Nhà cung cấp: tìm kiếm, bộ lọc nâng cao,
/// thống kê, phân trang và mở dòng xem chi tiết.
///
/// Danh sách luôn lấy từ database theo trang (LIMIT/OFFSET) nên dùng được với
/// hàng nghìn nhà cung cấp; vùng chi tiết chỉ được tải khi người dùng bấm vào dòng.
/// </summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>Số nhà cung cấp tải mỗi lần, có thể đổi trong bộ phân trang.</summary>
    private const int SupplierPageSizeMacDinh = 20;

    /// <summary>Giá trị của bộ lọc "còn nợ / không còn nợ / tất cả".</summary>
    public const string CongNoTatCa = "tatca";

    public const string CongNoConNo = "conno";

    public const string CongNoKhongConNo = "khongconno";

    /// <summary>Các dòng nhà cung cấp của trang hiện tại.</summary>
    public ObservableCollection<NhaCungCapRowViewModel> SupplierRows { get; } = new();

    /// <summary>Các lựa chọn của bộ lọc tình trạng công nợ.</summary>
    public IReadOnlyList<SoQuyLuaChonItem> SupplierCongNoOptions { get; } =
    [
        new SoQuyLuaChonItem(CongNoTatCa, "Tất cả"),
        new SoQuyLuaChonItem(CongNoConNo, "Còn nợ"),
        new SoQuyLuaChonItem(CongNoKhongConNo, "Không còn nợ")
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierHasFilter))]
    [NotifyPropertyChangedFor(nameof(SupplierFilterButtonText))]
    private string? supplierCongNoFilter = CongNoTatCa;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierHasFilter)), NotifyPropertyChangedFor(nameof(SupplierFilterButtonText))]
    private string? supplierFilterMa;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierHasFilter)), NotifyPropertyChangedFor(nameof(SupplierFilterButtonText))]
    private string? supplierFilterTen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierHasFilter)), NotifyPropertyChangedFor(nameof(SupplierFilterButtonText))]
    private string? supplierFilterSoDienThoai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierHasFilter)), NotifyPropertyChangedFor(nameof(SupplierFilterButtonText))]
    private string? supplierFilterEmail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierHasFilter)), NotifyPropertyChangedFor(nameof(SupplierFilterButtonText))]
    private string? supplierFilterDiaChi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierHasFilter)), NotifyPropertyChangedFor(nameof(SupplierFilterButtonText))]
    private string? supplierFilterConNoTu;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierHasFilter)), NotifyPropertyChangedFor(nameof(SupplierFilterButtonText))]
    private string? supplierFilterConNoDen;

    [ObservableProperty] private bool isSupplierFilterPanelOpen;

    [ObservableProperty] private bool isLoadingSuppliers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierTotalPages))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangDau))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangCuoi))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangSau))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(SupplierPageText))]
    private int supplierPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierTotalPages))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangDau))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangCuoi))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangSau))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(SupplierPageText))]
    [NotifyPropertyChangedFor(nameof(SupplierRangeText))]
    [NotifyPropertyChangedFor(nameof(HasSupplierRows))]
    [NotifyPropertyChangedFor(nameof(IsSupplierListEmpty))]
    private int supplierTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierTotalPages))]
    [NotifyPropertyChangedFor(nameof(SupplierRangeText))]
    private int supplierAllCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierTotalDebtText))]
    private decimal supplierTotalDebt;

    /// <summary>Số nhà cung cấp còn nợ, đếm cùng bộ lọc đang xem.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierDebtSupplierCaption))]
    private int supplierDebtSupplierCount;

    [ObservableProperty] private int supplierPageSize = SupplierPageSizeMacDinh;

    [ObservableProperty] private NhaCungCapRowViewModel? expandedSupplierRow;

    /// <summary>Số dòng đang được tick chọn ở trang hiện tại.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSupplierRows))]
    [NotifyPropertyChangedFor(nameof(SupplierSelectedCountText))]
    private int supplierSelectedCount;

    [ObservableProperty] private bool isAllSupplierRowsSelected;

    /// <summary>Hủy tìm kiếm realtime khi người dùng gõ tiếp từ khóa mới.</summary>
    private CancellationTokenSource? _supplierSearchCts;

    // ============================================================
    // TEXT HIỂN THỊ
    // ============================================================

    public bool SupplierHasFilter => !string.IsNullOrWhiteSpace(SupplierFilterMa)
        || !string.IsNullOrWhiteSpace(SupplierFilterTen)
        || !string.IsNullOrWhiteSpace(SupplierFilterSoDienThoai)
        || !string.IsNullOrWhiteSpace(SupplierFilterEmail)
        || !string.IsNullOrWhiteSpace(SupplierFilterDiaChi)
        || !string.IsNullOrWhiteSpace(SupplierFilterConNoTu)
        || !string.IsNullOrWhiteSpace(SupplierFilterConNoDen)
        || (!string.IsNullOrWhiteSpace(SupplierCongNoFilter)
            && SupplierCongNoFilter != CongNoTatCa);

    public int SupplierTotalPages
    {
        get
        {
            if (SupplierPageSize <= 0)
                return 1;

            return Math.Max(1, (SupplierTotalCount + SupplierPageSize - 1) / SupplierPageSize);
        }
    }

    public bool CanSupplierTrangDau => SupplierPage > 1;

    public bool CanSupplierTrangCuoi => SupplierPage < SupplierTotalPages;

    public bool CanSupplierTrangSau => SupplierPage < SupplierTotalPages;

    public bool CanSupplierTrangTruoc => SupplierPage > 1;

    /// <summary>Khoảng dòng đang hiển thị, ví dụ "Hiển thị 1-20 trên 137 nhà cung cấp".</summary>
    public string SupplierRangeText
    {
        get
        {
            if (SupplierTotalCount == 0)
                return "Không có nhà cung cấp nào phù hợp";

            var dau = (SupplierPage - 1) * SupplierPageSize + 1;
            var cuoi = Math.Min(SupplierPage * SupplierPageSize, SupplierTotalCount);

            return $"Hiển thị {dau}-{cuoi} trên tổng {SupplierTotalCount:N0} nhà cung cấp";
        }
    }

    public string SupplierTotalDebtText
        => MoneyInput.Format(SupplierTotalDebt) + " đ";

    public string SupplierDebtSupplierCaption
        => $"{SupplierDebtSupplierCount:N0} nhà cung cấp còn nợ";

    /// <summary>Nhãn trang hiện tại, ví dụ "Trang 2 / 7".</summary>
    public string SupplierPageText => $"Trang {SupplierPage} / {SupplierTotalPages}";

    public string SupplierRongText => SupplierHasFilter
        ? "Không tìm thấy nhà cung cấp nào phù hợp với điều kiện lọc"
        : "Chưa có nhà cung cấp nào trong hệ thống";

    public string SupplierRongHint => SupplierHasFilter
        ? "Hãy thử xoá bớt điều kiện lọc hoặc nhập từ khóa khác."
        : "Bấm \"Thêm nhà cung cấp\" hoặc nhập danh sách từ file Excel/CSV.";

    public bool IsSupplierListEmpty => SupplierRows.Count == 0;

    public bool HasSupplierRows => SupplierRows.Count > 0;

    public bool HasSelectedSupplierRows => SupplierSelectedCount > 0;

    public string SupplierSelectedCountText => $"Đã chọn {SupplierSelectedCount} nhà cung cấp";

    /// <summary>Nhãn nút bộ lọc, hiển thị số điều kiện đang bật.</summary>
    public string SupplierFilterButtonText => TaoBoLocNhaCungCap().MoTa;

    /// <summary>Mũi tên của nút mở/đóng khung bộ lọc nâng cao.</summary>
    public string SupplierFilterArrowText => IsSupplierFilterPanelOpen ? "▲" : "▼";

    partial void OnIsSupplierFilterPanelOpenChanged(bool value)
        => OnPropertyChanged(nameof(SupplierFilterArrowText));

    // ============================================================
    // TẢI DANH SÁCH THEO TRANG
    // ============================================================

    /// <summary>
    /// Tải trang nhà cung cấp hiện tại kèm số liệu thống kê.
    /// Mọi điều kiện lọc đều được đẩy xuống SQL.
    /// </summary>
    public async Task LoadSupplierPageAsync()
    {
        if (IsLoadingSuppliers)
            return;

        IsLoadingSuppliers = true;

        try
        {
            var boLoc = TaoBoLocNhaCungCap();

            if (SupplierPageSize <= 0)
                SupplierPageSize = SupplierPageSizeMacDinh;

            // Trang vượt quá số trang thực tế sau khi lọc: đưa về trang cuối.
            var soTrangToiDa = Math.Max(
                1,
                (SupplierTotalCount + SupplierPageSize - 1) / SupplierPageSize);

            if (SupplierPage > soTrangToiDa)
                SupplierPage = soTrangToiDa;

            var thongKe = await _db.GetThongKeDanhSachNhaCungCapAsync(boLoc);
            var duLieu = await _db.GetNhaCungCapPagedAsync(
                boLoc,
                SupplierPage,
                SupplierPageSize);

            SupplierTotalCount = duLieu.TongSoBanGhi;
            SupplierAllCount = thongKe.TongSoNhaCungCap;
            SupplierTotalDebt = thongKe.TongCongNo;
            SupplierDebtSupplierCount = thongKe.SoNhaCungCapConNo;

            SupplierRows.Clear();

            foreach (var ncc in duLieu.Items)
            {
                var dong = new NhaCungCapRowViewModel(ncc, this);
                dong.PropertyChanged += DongNhaCungCap_PropertyChanged;
                SupplierRows.Add(dong);
            }

            // Đóng dòng đang mở rộng vì dữ liệu trang đã được thay mới.
            ExpandedSupplierRow = null;

            SupplierSelectedCount = 0;
        }
        catch (Exception ex)
        {
            SupplierRows.Clear();
            SupplierTotalCount = 0;
            SupplierSelectedCount = 0;

            StatusMessage = $"Lỗi tải danh sách nhà cung cấp: {ex.Message}";
        }
        finally
        {
            IsLoadingSuppliers = false;
        }
    }

    /// <summary>Gom các điều kiện trên giao diện thành đối tượng bộ lọc cho database.</summary>
    private BoLocNhaCungCap TaoBoLocNhaCungCap() => new()
    {
        TimKiem = SupplierSearch?.Trim() ?? "",
        MaNhaCungCap = SupplierFilterMa?.Trim() ?? "",
        TenNhaCungCap = SupplierFilterTen?.Trim() ?? "",
        SoDienThoai = SupplierFilterSoDienThoai?.Trim() ?? "",
        Email = SupplierFilterEmail?.Trim() ?? "",
        DiaChi = SupplierFilterDiaChi?.Trim() ?? "",
        CongNoMode = ChuyenCongNoFilterSangSo(SupplierCongNoFilter),
        ConNoTu = ParseTien(SupplierFilterConNoTu),
        ConNoDen = ParseTien(SupplierFilterConNoDen)
    };

    /// <summary>Đổi lựa chọn trên ComboBox thành mã số mà SQL hiểu.</summary>
    private static int ChuyenCongNoFilterSangSo(string? giaTri) => giaTri switch
    {
        "conno" => BoLocNhaCungCap.CongNoConNo,
        "khongconno" => BoLocNhaCungCap.CongNoKhongConNo,
        _ => BoLocNhaCungCap.CongNoTatCa
    };

    /// <summary>
    /// Đọc số tiền người dùng gõ ở ô khoảng công nợ. Ô trống nghĩa là không lọc
    /// theo khoảng đó (trả về null), không phải lọc theo giá trị 0.
    /// </summary>
    private static decimal? ParseTien(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : MoneyInput.Parse(value);

    // ============================================================
    // LỆNH TÌM KIẾM / LỌC / PHÂN TRANG
    // ============================================================

    /// <summary>Tìm kiếm theo từ khóa và bộ lọc nâng cao, luôn quay về trang 1.</summary>
    [RelayCommand]
    private async Task SearchSuppliersAsync()
    {
        SupplierPage = 1;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private void ToggleSupplierFilter()
        => IsSupplierFilterPanelOpen = !IsSupplierFilterPanelOpen;

    /// <summary>
    /// Tìm kiếm realtime: chờ người dùng ngừng gõ 350ms mới gọi database,
    /// tránh mỗi ký tự lại chạy một câu truy vấn.
    /// </summary>
    public void TimKiemNhaCungCapTuDong()
    {
        _supplierSearchCts?.Cancel();
        _supplierSearchCts?.Dispose();

        var cts = new CancellationTokenSource();
        _supplierSearchCts = cts;

        _ = TimKiemNhaCungCapSauDebounceAsync(cts.Token);
    }

    private async Task TimKiemNhaCungCapSauDebounceAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);

            if (token.IsCancellationRequested)
                return;

            await SearchSuppliersAsync();
        }
        catch (OperationCanceledException)
        {
            // Người dùng gõ tiếp: huỷ lần tìm kiếm trước, không cần báo lỗi.
        }
    }

    /// <summary>Tải lại trang hiện tại, dùng cho nút làm mới.</summary>
    [RelayCommand]
    private async Task RefreshSuppliersAsync()
        => await LoadSupplierPageAsync();

    [RelayCommand]
    private async Task ApplySupplierFilterAsync()
    {
        SupplierPage = 1;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task ClearSupplierFilterAsync()
    {
        SupplierFilterMa = null;
        SupplierFilterTen = null;
        SupplierFilterSoDienThoai = null;
        SupplierFilterEmail = null;
        SupplierFilterDiaChi = null;
        SupplierFilterConNoTu = null;
        SupplierFilterConNoDen = null;
        SupplierCongNoFilter = CongNoTatCa;
        SupplierPage = 1;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task SupplierDenTrangAsync()
    {
        if (!CanSupplierTrangSau)
            return;

        SupplierPage = SupplierTotalPages;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task SupplierTrangDauAsync()
    {
        if (!CanSupplierTrangTruoc)
            return;

        SupplierPage = 1;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task SupplierTrangSauAsync()
    {
        if (!CanSupplierTrangSau)
            return;

        SupplierPage++;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task SupplierTrangTruocAsync()
    {
        if (!CanSupplierTrangTruoc)
            return;

        SupplierPage--;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task SupplierDenTrangNhatAsync(int trang)
    {
        if (trang < 1 || trang > SupplierTotalPages || trang == SupplierPage)
            return;

        SupplierPage = trang;

        await LoadSupplierPageAsync();
    }

    // ============================================================
    // MỞ DÒNG XEM CHI TIẾT
    // ============================================================

    /// <summary>
    /// Mở hoặc đóng vùng chi tiết của một dòng. Chỉ một dòng mở tại một thời điểm,
    /// dữ liệu chi tiết chỉ được tải ở lần mở đầu tiên.
    /// </summary>
    [RelayCommand]
    private async Task ToggleSupplierDetailAsync(NhaCungCapRowViewModel? row)
    {
        if (row is null)
            return;

        if (row.IsExpanded)
        {
            ExpandedSupplierRow = null;
            row.IsExpanded = false;
            return;
        }

        if (ExpandedSupplierRow is not null && !ReferenceEquals(ExpandedSupplierRow, row))
            ExpandedSupplierRow.IsExpanded = false;

        ExpandedSupplierRow = row;
        row.IsExpanded = true;

        if (row.Detail is not null)
            return;

        var detail = new NhaCungCapDetailViewModel
        {
            Supplier = row.Supplier
        };

        row.Detail = detail;

        detail.IsLoading = true;

        try
        {
            var duLieu = await _db.GetNhaCungCapByIdAsync(row.SupplierId);

            if (duLieu is not null)
            {
                detail.Supplier = duLieu;
            }

            var phieuNhap = await _db.GetPhieuNhapCuaNhaCungCapAsync(row.SupplierId, 100);
            detail.NapPhieuNhapHang(phieuNhap);

            var lichSu = await _db.GetLichSuCongNoNhaCungCapAsync(row.SupplierId, 100);
            detail.NapLichSuCongNo(lichSu);

            var thongKe = await _db.GetThongKeTaiChinhNhaCungCapAsync(row.SupplierId);
            detail.ThongKe = thongKe;
        }
        catch (Exception ex)
        {
            detail.ThongKe = null;
            StatusMessage = $"Lỗi tải chi tiết nhà cung cấp: {ex.Message}";
        }
        finally
        {
            detail.IsLoading = false;
        }
    }

    // ============================================================
    // CHỌN DÒNG
    // ============================================================

    private void DongNhaCungCap_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Chỉ cần đếm lại số dòng đã tick, không cần xử lý thuộc tính khác.
        if (e.PropertyName == nameof(NhaCungCapRowViewModel.IsSelected))
        {
            SupplierSelectedCount = SupplierRows.Count(dong => dong.IsSelected);
            CapNhatTrangThaiChonTatCaNhaCungCap();
        }
    }

    /// <summary>Bộ tick chọn tất cả chỉ theo trang hiện tại.</summary>
    partial void OnIsAllSupplierRowsSelectedChanged(bool value)
    {
        foreach (var dong in SupplierRows)
        {
            if (dong.IsSelected != value)
                dong.IsSelected = value;
        }

        SupplierSelectedCount = SupplierRows.Count(dong => dong.IsSelected);
    }

    /// <summary>Giữ checkbox "Chọn tất cả" đồng bộ khi người dùng tick từng dòng.</summary>
    public void CapNhatTrangThaiChonTatCaNhaCungCap()
    {
        IsAllSupplierRowsSelected = SupplierRows.Count > 0
            && SupplierRows.All(dong => dong.IsSelected);
    }

    /// <summary>Bỏ chọn tất cả dòng trên trang hiện tại.</summary>
    [RelayCommand]
    private void BoChonTatCaSupplierRows()
    {
        foreach (var dong in SupplierRows)
            dong.IsSelected = false;

        SupplierSelectedCount = 0;
        IsAllSupplierRowsSelected = false;
    }
}
