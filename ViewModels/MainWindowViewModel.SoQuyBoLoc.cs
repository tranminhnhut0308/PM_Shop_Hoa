using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

/// <summary>
/// Bộ lọc sổ quỹ (phần ViewModel).
///
/// Nguyên tắc: mọi thay đổi bộ lọc chỉ cập nhật trạng thái trong bộ nhớ, sau đó
/// nạp lại DANH SÁCH và THỐNG KÊ bằng cùng một câu lọc. Không bao giờ lấy tổng
/// toàn bộ bảng rồi mới lọc ở UI, vì sổ quỹ có thể có hàng chục nghìn phiếu.
/// </summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>Mã khoảng thời gian: xem toàn bộ lịch sử.</summary>
    private const string SoQuyTgTatCa = "TAT_CA";

    private const string SoQuyTgHomNay = "HOM_NAY";
    private const string SoQuyTgHomQua = "HOM_QUA";
    private const string SoQuyTg7Ngay = "7_NGAY";
    private const string SoQuyTg30Ngay = "30_NGAY";
    private const string SoQuyTgThangNay = "THANG_NAY";
    private const string SoQuyTgThangTruoc = "THANG_TRUOC";
    private const string SoQuyTgTuyChinh = "TUY_CHINH";

    /// <summary>Số phút bị hoãn khi người dùng đang gõ / đổi ô tùy chỉnh.</summary>
    private const int SoQuyLocDebounceMs = 300;

    private CancellationTokenSource? _soQuyLocCts;

    // ================================================================
    // DANH SÁCH LỰA CHỌN (lấy từ database, không hard-code)
    // ================================================================

    /// <summary>Quỹ: danh sách phương thức thực tế có trong bảng phieu_chi.</summary>
    public ObservableCollection<SoQuyLuaChon> SoQuyQuyOptions { get; } = new();

    /// <summary>Các khoảng thời gian cố định + tùy chỉnh. Mặc định là "Toàn thời gian".</summary>
    public IReadOnlyList<SoQuyLuaChon> SoQuyKhoangThoiGianOptions { get; } =
    [
        new(SoQuyTgTatCa, "Toàn thời gian"),
        new(SoQuyTgHomNay, "Hôm nay"),
        new(SoQuyTgHomQua, "Hôm qua"),
        new(SoQuyTg7Ngay, "7 ngày qua"),
        new(SoQuyTg30Ngay, "30 ngày qua"),
        new(SoQuyTgThangNay, "Tháng này"),
        new(SoQuyTgThangTruoc, "Tháng trước"),
        new(SoQuyTgTuyChinh, "Tùy chỉnh")
    ];

    /// <summary>Mặc định sổ quỹ luôn xem toàn bộ lịch sử.</summary>
    private SoQuyLuaChon? SoQuyKhoangThoiGianMacDinh => SoQuyKhoangThoiGianOptions[0];

    /// <summary>Loại thu/chi lấy từ bảng nhom_chi_phi (rỗng nếu hệ thống chưa có loại nào).</summary>
    public ObservableCollection<SoQuyLuaChonItem> SoQuyLoaiThuChiOptions { get; } = new();

    /// <summary>Trạng thái chứng từ lấy từ cột trang_thai của bảng phieu_chi.</summary>
    public ObservableCollection<SoQuyLuaChonItem> SoQuyTrangThaiOptions { get; } = new();

    /// <summary>Người tạo phiếu lấy từ bảng nguoi_dung.</summary>
    public ObservableCollection<SoQuyLuaChonItem> SoQuyNguoiTaoOptions { get; } = new();

    // ================================================================
    // TRẠNG THÁI BỘ LỌC
    // ================================================================

    /// <summary>Quỹ đang chọn; null = tất cả các quỹ.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoQuyLocDangChonQuy))]
    private SoQuyLuaChon? soQuyLocQuy;

    /// <summary>Khoảng thời gian đang chọn (mã trong <see cref="SoQuyKhoangThoiGianOptions"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoQuyLocTuyChinh))]
    private SoQuyLuaChon? soQuyLocKhoangThoiGian = null;

    /// <summary>Ngày bắt đầu, chỉ dùng khi khoảng thời gian = "Tùy chỉnh".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoQuyLocTuyChinh))]
    private DateTime? soQuyLocTuNgay = DateTime.Today;

    /// <summary>Ngày kết thúc (bao gồm cả ngày này), chỉ dùng khi tùy chỉnh.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoQuyLocTuyChinh))]
    private DateTime? soQuyLocDenNgay = DateTime.Today;

    /// <summary>0 = cả thu và chi, 1 = chỉ phiếu thu, 2 = chỉ phiếu chi.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoQuyLocChiThuText))]
    [NotifyPropertyChangedFor(nameof(SoQuyLocChiChiText))]
    [NotifyPropertyChangedFor(nameof(SoQuyLocChiTatCaText))]
    [NotifyPropertyChangedFor(nameof(SoQuyLocChonTatCa))]
    [NotifyPropertyChangedFor(nameof(SoQuyLocChonThu))]
    [NotifyPropertyChangedFor(nameof(SoQuyLocChonChi))]
    private int soQuyLocLoaiChungTu = SoQuyBoLoc.TatCaLoaiChungTu;

    /// <summary>Số tiền nhỏ nhất (dạng chuỗi để phân biệt được "chưa nhập" với 0).</summary>
    [ObservableProperty] private string soQuyLocTienTuText = "";

    /// <summary>Số tiền lớn nhất.</summary>
    [ObservableProperty] private string soQuyLocTienDenText = "";

    /// <summary>true khi đang chọn khoảng thời gian tùy chỉnh (để hiện 2 ô ngày).</summary>
    public bool SoQuyLocTuyChinh => SoQuyLocKhoangThoiGian?.GiaTri == SoQuyTgTuyChinh;

    /// <summary>Quỹ đang chọn, hiển thị trên tiêu đề bộ lọc.</summary>
    public string SoQuyLocDangChonQuy =>
        SoQuyLocQuy is null || SoQuyLocQuy.GiaTri == ""
            ? "Tất cả quỹ"
            : SoQuyLocQuy.HienThi;

    public string SoQuyLocChiTatCaText => SoQuyLocLoaiChungTu == SoQuyBoLoc.TatCaLoaiChungTu
        ? "#087F6E"
        : "#667085";

    public string SoQuyLocChiThuText => SoQuyLocLoaiChungTu == SoQuyBoLoc.ChiPhieuThu
        ? "#1877F2"
        : "#667085";

    public string SoQuyLocChiChiText => SoQuyLocLoaiChungTu == SoQuyBoLoc.ChiPhieuChi
        ? "#D92D20"
        : "#667085";

    // ================================================================
    // BA TRẠNG THÁI TICK CỦA NHÓM "LOẠI CHỨNG TỪ"
    //
    // Ba nút này loại trừ lẫn nhau: chỉ được tick nhiều nhất một nút.
    // Tick nhiều nút cùng lúc thì coi như "Tất cả" (không lọc), vì người
    // dùng bỏ/chọn nhanh cả 3 nút là muốn xem toàn bộ.
    // ================================================================

    /// <summary>Đang ở chế độ xem cả phiếu thu và phiếu chi.</summary>
    public bool SoQuyLocChonTatCa
    {
        get => SoQuyLocLoaiChungTu == SoQuyBoLoc.TatCaLoaiChungTu;
        set => ChonLoaiChungTuSoQuy(SoQuyBoLoc.TatCaLoaiChungTu, value);
    }

    /// <summary>Chỉ xem phiếu thu.</summary>
    public bool SoQuyLocChonThu
    {
        get => SoQuyLocLoaiChungTu == SoQuyBoLoc.ChiPhieuThu;
        set => ChonLoaiChungTuSoQuy(SoQuyBoLoc.ChiPhieuThu, value);
    }

    /// <summary>Chỉ xem phiếu chi.</summary>
    public bool SoQuyLocChonChi
    {
        get => SoQuyLocLoaiChungTu == SoQuyBoLoc.ChiPhieuChi;
        set => ChonLoaiChungTuSoQuy(SoQuyBoLoc.ChiPhieuChi, value);
    }

    /// <summary>
    /// Nhóm "Loại chứng từ" hoạt động như 3 nút chọn loại trừ nhau: tick nút nào
    /// thì nút đó được chọn và hai nút còn lại tự bỏ tick. Bỏ tick nút đang
    /// chọn thì bỏ qua, vì luôn phải có ít nhất một lựa chọn.
    /// </summary>
    private void ChonLoaiChungTuSoQuy(int giaTri, bool tick)
    {
        if (tick)
            SoQuyLocLoaiChungTu = giaTri;
    }

    // ================================================================
    // ẨN/HIỆN NHÓM ĐIỀU KIỆN THEO DỮ LIỆU THẬT
    //
    // Database chưa có loại thu chi / trạng thái / người tạo nào thì ẩn luôn
    // nhóm đó, thay vì hiện ra ô trống cho người dùng bấm vào.
    // ================================================================

    /// <summary>true khi bảng nhom_chi_phi có ít nhất một loại đang hoạt động.</summary>
    public bool SoQuyCoLoaiThuChi => SoQuyLoaiThuChiOptions.Count > 0;

    /// <summary>true khi cột trang_thai có ít nhất một giá trị thực tế.</summary>
    public bool SoQuyCoTrangThai => SoQuyTrangThaiOptions.Count > 0;

    /// <summary>true khi bảng nguoi_dung có ít nhất một người dùng.</summary>
    public bool SoQuyCoNguoiTao => SoQuyNguoiTaoOptions.Count > 0;

    /// <summary>Bộ lọc hiện tại, dựng từ trạng thái trên để đưa xuống SQL.</summary>
    public SoQuyBoLoc SoQuyLocHienTai => TaoBoLocSoQuy();

    /// <summary>Chỉ báo cho ViewModel biết đang bộ lọc để tự làm mới dữ liệu.</summary>
    public bool CoSoQuyBoLoc => TaoBoLocSoQuy().CoLoc;

    // ================================================================
    // NẠP DANH SÁCH LỰA CHỌN
    // ================================================================

    /// <summary>
    /// Nạp các danh sách lựa chọn cho bộ lọc. Gọi 1 lần khi vào màn hình sổ quỹ.
    /// Danh sách nào database chưa có dữ liệu thì để rỗng, tuyệt đối không bịa thêm.
    /// </summary>
    private async Task TaiSoQuyBoLocOptionsAsync()
    {
        var quy = await _db.GetSoQuyQuyOptionsAsync();
        var loai = await _db.GetSoQuyLoaiThuChiOptionsAsync();
        var trangThai = await _db.GetSoQuyTrangThaiOptionsAsync();
        var nguoiTao = await _db.GetSoQuyNguoiTaoOptionsAsync();

        // Giữ lại quỹ đang chọn trước khi nạp lại danh sách (danh sách mới là
        // các object mới nên phải tìm lại theo mã).
        var maQuyDangChon = SoQuyLocQuy?.GiaTri ?? "";

        SoQuyQuyOptions.Clear();

        // Rỗng = xem tất cả các quỹ, luôn có sẵn ở đầu danh sách.
        SoQuyQuyOptions.Add(new SoQuyLuaChon("", "Tất cả quỹ"));

        foreach (var item in quy)
        {
            if (SoQuyQuyOptions.Any(o => o.GiaTri == item.GiaTri))
                continue;

            SoQuyQuyOptions.Add(item);
        }

        // Quỹ đang chọn có thể không còn tồn tại (đã bị xoá ở nơi khác) -> về "tất cả".
        SoQuyLocQuy = SoQuyQuyOptions.FirstOrDefault(o => o.GiaTri == maQuyDangChon)
                      ?? SoQuyQuyOptions[0];

        // Lần đầu vào màn hình thì chọn "Toàn thời gian" cho ComboBox khoảng thời gian.
        if (SoQuyLocKhoangThoiGian is null
            || !SoQuyKhoangThoiGianOptions.Any(o => o.GiaTri == SoQuyLocKhoangThoiGian.GiaTri))
            SoQuyLocKhoangThoiGian = SoQuyKhoangThoiGianMacDinh;

        ThayTheLuaChon(SoQuyLoaiThuChiOptions, loai);
        ThayTheLuaChon(SoQuyTrangThaiOptions, trangThai);
        ThayTheLuaChon(SoQuyNguoiTaoOptions, nguoiTao);

        // Danh sách vừa thay => các nhóm điều kiện phải ẩn/hiện lại.
        OnPropertyChanged(nameof(SoQuyCoLoaiThuChi));
        OnPropertyChanged(nameof(SoQuyCoTrangThai));
        OnPropertyChanged(nameof(SoQuyCoNguoiTao));
    }

    /// <summary>
    /// Nạp lại danh sách lựa chọn mà vẫn giữ nguyên trạng thái đang tick
    /// (để bộ lọc không bị mất khi dữ liệu danh mục thay đổi).
    /// </summary>
    private void ThayTheLuaChon(
        ObservableCollection<SoQuyLuaChonItem> dich,
        IReadOnlyList<SoQuyLuaChonItem> moi)
    {
        var dangChon = dich
            .Where(i => i.IsChecked)
            .Select(i => i.GiaTri)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var cu in dich)
            cu.PropertyChanged -= OnSoQuyLuaChonItemChanged;

        dich.Clear();

        foreach (var item in moi)
        {
            var moiItem = new SoQuyLuaChonItem(item.GiaTri, item.HienThi)
            {
                IsChecked = dangChon.Contains(item.GiaTri)
            };

            // Tick mục nào thì áp lại bộ lọc ngay.
            moiItem.PropertyChanged += OnSoQuyLuaChonItemChanged;
            dich.Add(moiItem);
        }
    }

    private void OnSoQuyLuaChonItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SoQuyLuaChonItem.IsChecked))
            BaoLocSoQuyThayDoi();
    }

    /// <summary>Gộp các mục đang tick thành chuỗi id phân tách bằng dấu phẩy cho SQL.</summary>
    private static string GopGiaTriDaChon(ObservableCollection<SoQuyLuaChonItem> items)
        => string.Join(
            ",",
            items
                .Where(i => i.IsChecked)
                .Select(i => i.GiaTri)
                .Where(v => !string.IsNullOrWhiteSpace(v)));

    // ================================================================
    // DỰNG BỘ LỌC + ÁP DỤNG
    // ================================================================

    private SoQuyBoLoc TaoBoLocSoQuy()
    {
        var homNay = DateTime.Today;

        DateTime? tuNgay = null;
        DateTime? denNgay = null;

        switch (SoQuyLocKhoangThoiGian?.GiaTri)
        {
            case SoQuyTgHomNay:
                tuNgay = homNay;
                denNgay = homNay.AddDays(1);
                break;

            case SoQuyTgHomQua:
                tuNgay = homNay.AddDays(-1);
                denNgay = homNay;
                break;

            case SoQuyTg7Ngay:
                // 7 ngày gồm cả hôm nay.
                tuNgay = homNay.AddDays(-6);
                denNgay = homNay.AddDays(1);
                break;

            case SoQuyTg30Ngay:
                tuNgay = homNay.AddDays(-29);
                denNgay = homNay.AddDays(1);
                break;

            case SoQuyTgThangNay:
                tuNgay = new DateTime(homNay.Year, homNay.Month, 1);
                denNgay = homNay.AddDays(1);
                break;

            case SoQuyTgThangTruoc:
                var dauThang = new DateTime(homNay.Year, homNay.Month, 1);
                tuNgay = dauThang.AddMonths(-1);
                denNgay = dauThang;
                break;

            case SoQuyTgTuyChinh:
                // Ô "đến ngày" là ngày bao gồm, nên lấy 00:00:00 của ngày kế
                // tiếp rồi so sánh "<" để không bỏ sót phiếu trong ngày đó.
                // Trước hết phải đảo nếu người dùng nhập ngược (đến < từ),
                // nếu không sẽ thành khoảng rỗng hoặc lệch 1 ngày.
                if (SoQuyLocTuNgay.HasValue && SoQuyLocDenNgay.HasValue
                    && SoQuyLocTuNgay.Value.Date > SoQuyLocDenNgay.Value.Date)
                    (SoQuyLocTuNgay, SoQuyLocDenNgay) = (SoQuyLocDenNgay, SoQuyLocTuNgay);

                tuNgay = SoQuyLocTuNgay?.Date;
                denNgay = SoQuyLocDenNgay?.Date.AddDays(1);
                break;
        }

        // Các khoảng cố định phía trên luôn đúng thứ tự; đảo ở đây để phòng
        // trường hợp ngày tùy chỉnh vừa được người dùng sửa.
        if (tuNgay.HasValue && denNgay.HasValue && tuNgay > denNgay)
            (tuNgay, denNgay) = (denNgay, tuNgay);

        return new SoQuyBoLoc
        {
            TimKiem = SoQuySearchText,
            LoaiChungTu = SoQuyLocLoaiChungTu,
            PhuongThuc = SoQuyLocQuy?.GiaTri ?? "",
            TuNgay = tuNgay,
            DenNgay = denNgay,
            LoaiThuChiIds = GopGiaTriDaChon(SoQuyLoaiThuChiOptions),
            TrangThaiList = GopGiaTriDaChon(SoQuyTrangThaiOptions),
            NguoiTaoIds = GopGiaTriDaChon(SoQuyNguoiTaoOptions),
            TienTu = ParseTienLoc(SoQuyLocTienTuText),
            TienDen = ParseTienLoc(SoQuyLocTienDenText)
        };
    }

    /// <summary>Đọc ô nhập khoảng tiền; nhập rác hoặc để trống thì coi như không lọc.</summary>
    private static decimal? ParseTienLoc(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return MoneyInput.Parse(text) > 0 ? MoneyInput.Parse(text) : null;
    }

    /// <summary>Được gọi mỗi khi có bất kỳ điều kiện lọc nào thay đổi.</summary>
    partial void OnSoQuyLocQuyChanged(SoQuyLuaChon? value) => BaoLocSoQuyThayDoi();

    partial void OnSoQuyLocKhoangThoiGianChanged(SoQuyLuaChon? value) => BaoLocSoQuyThayDoi();

    partial void OnSoQuyLocTuNgayChanged(DateTime? value) => BaoLocSoQuyThayDoi();

    partial void OnSoQuyLocDenNgayChanged(DateTime? value) => BaoLocSoQuyThayDoi();

    partial void OnSoQuyLocLoaiChungTuChanged(int value) => BaoLocSoQuyThayDoi();

    partial void OnSoQuyLocTienTuTextChanged(string value) => BaoLocSoQuyThayDoi();

    partial void OnSoQuyLocTienDenTextChanged(string value) => BaoLocSoQuyThayDoi();

    /// <summary>
    /// Hoãn 300ms rồi nạp lại. Nhờ vậy tick nhiều checkbox hay gõ nhiều ký tự
    /// chỉ tốn một lần truy vấn thay vì mỗi lần bấm một lần.
    /// </summary>
    private void BaoLocSoQuyThayDoi()
    {
        OnPropertyChanged(nameof(CoSoQuyBoLoc));

        var cts = new CancellationTokenSource();
        _soQuyLocCts?.Cancel();
        _soQuyLocCts = cts;

        _ = DebounceBaoLocSoQuyAsync(cts.Token);
    }

    private async Task DebounceBaoLocSoQuyAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SoQuyLocDebounceMs, token);

            SoQuyPage = 1;

            await TaiLaiSoQuyAsync();
        }
        catch (OperationCanceledException)
        {
            // Người dùng thay đổi tiếp => huỷ lần nạp cũ.
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể áp dụng bộ lọc sổ quỹ: " + ex.Message;
        }
    }

    /// <summary>Xóa sạch bộ lọc, trở về toàn thời gian và xem toàn bộ phiếu.</summary>
    [RelayCommand]
    private void XoaBoLocSoQuy()
    {
        _soQuyLocCts?.Cancel();

        SoQuySearchText = "";
        SoQuyLocQuy = SoQuyQuyOptions.FirstOrDefault();
        SoQuyLocKhoangThoiGian = SoQuyKhoangThoiGianOptions.First();
        SoQuyLocTuNgay = DateTime.Today;
        SoQuyLocDenNgay = DateTime.Today;
        SoQuyLocLoaiChungTu = SoQuyBoLoc.TatCaLoaiChungTu;
        SoQuyLocTienTuText = "";
        SoQuyLocTienDenText = "";

        foreach (var item in SoQuyLoaiThuChiOptions)
            item.IsChecked = false;

        foreach (var item in SoQuyTrangThaiOptions)
            item.IsChecked = false;

        foreach (var item in SoQuyNguoiTaoOptions)
            item.IsChecked = false;

        SoQuyPage = 1;

        StatusMessage = "Đã xóa bộ lọc sổ quỹ.";

        _ = TaiLaiSoQuyAsync();
    }

    /// <summary>Nạp lại danh sách phiếu và thống kê theo bộ lọc hiện tại.</summary>
    private async Task TaiLaiSoQuyAsync()
    {
        await Task.WhenAll(LoadSoQuyPhieuAsync(), LoadSoQuyTongHopAsync());
    }
}
