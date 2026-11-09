using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

/// <summary>
/// Quản lý NHIỀU GIỎ HÀNG cùng lúc + tự động lưu / khôi phục trạng thái đơn hàng (Issue #50).
///
/// Thiết kế:
/// - Mỗi đơn bán là một <see cref="SaleCartTab"/> với <see cref="SaleCartDraftState"/> riêng biệt
///   (ID giỏ hàng riêng, danh sách sản phẩm riêng, khách hàng riêng, chiết khấu / thanh toán / ghi chú riêng).
/// - Các thuộc tính Sale* sẵn có của ViewModel là "bản đang hiển thị" của giỏ hàng đang chọn.
///   Chuyển đơn = chụp trạng thái đơn cũ vào State rồi nạp State của đơn mới
///   (không chia sẻ tham chiếu giỏ hàng, không làm trộn sản phẩm hay khách hàng giữa các đơn).
/// - Mọi thay đổi được tự động ghi xuống bảng don_ban_nhap sau khi người dùng ngừng thao tác ~0,6 giây (debounce).
/// - Đơn nháp KHÔNG đụng tới tồn kho / công nợ / sổ quỹ; chỉ khi thanh toán thành công mới ghi nhận.
/// - Khi chuyển sang Hàng hóa để sửa giá / thông tin sản phẩm rồi quay lại Bán hàng,
///   toàn bộ giỏ hàng và các đơn đang mở được giữ nguyên 100%.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private const int SaleCartAutoSaveDelayMs = 600;
    private const int SaleCartToiDaSoDon = 20;

    public ObservableCollection<SaleCartTab> SaleCartTabs { get; } = new();

    [ObservableProperty]
    private SaleCartTab? activeSaleCartTab;

    private bool _saleCartSessionReady;
    private bool _dangNapDonBan;
    private bool _saleCartDirty;
    private long _saleCartSeq;
    private CancellationTokenSource? _saleCartAutoSaveCts;
    private readonly SemaphoreSlim _saleCartInitLock = new(1, 1);
    private readonly SemaphoreSlim _saleCartSaveLock = new(1, 1);
    private readonly Dictionary<string, long> _saleCartDaGhiSeq = new();

    public bool CoNhieuDonBan => SaleCartTabs.Count > 1;

    /// <summary>Còn thay đổi chưa ghi xuống database (dùng khi đóng cửa sổ).</summary>
    public bool HasPendingSaleCartDraft => _saleCartDirty;

    // ------------------------------------------------------------
    // KHỞI TẠO / KHÔI PHỤC
    // ------------------------------------------------------------

    /// <summary>
    /// Mở màn hình Bán hàng. Lần đầu trong phiên: nạp các giỏ hàng nháp từ database (khôi phục sau khi
    /// mở lại phần mềm). Các lần sau chỉ hiển thị lại giỏ hàng đang thao tác, KHÔNG reset.
    /// </summary>
    public async Task EnsureSaleCartSessionAsync()
    {
        if (_saleCartSessionReady)
            return;

        await _saleCartInitLock.WaitAsync();
        try
        {
            if (_saleCartSessionReady)
                return;

            var restored = new List<SaleCartDraftState>();
            try
            {
                var records = await _db.GetDonBanNhapDangSoanAsync();
                foreach (var record in records)
                {
                    try
                    {
                        var state = JsonSerializer.Deserialize<SaleCartDraftState>(record.DuLieu);
                        if (state is null)
                            continue;

                        state.CartId = record.CartId;
                        state.SoThuTu = record.SoThuTu > 0 ? record.SoThuTu : state.SoThuTu;
                        restored.Add(state);
                    }
                    catch (Exception ex)
                    {
                        SafeLog.WriteError("Lỗi đọc đơn bán nháp " + record.CartId, ex);
                    }
                }

                await LamMoiSanPhamTrongDonBanNhapAsync(restored);
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi khôi phục đơn bán nháp", ex);
                StatusMessage = "Không khôi phục được đơn bán nháp: " + ex.Message;
            }

            SaleCartTabs.Clear();
            foreach (var state in restored)
                SaleCartTabs.Add(TaoCartTab(state));

            if (SaleCartTabs.Count == 0)
                SaleCartTabs.Add(TaoCartTab(new SaleCartDraftState { SoThuTu = 1 }));

            _saleCartSessionReady = true;
            OnPropertyChanged(nameof(CoNhieuDonBan));
            KichHoatCartTab(SaleCartTabs[0]);

            if (restored.Count > 0)
                StatusMessage = $"Đã khôi phục {restored.Count} giỏ hàng đang chờ xử lý.";
        }
        finally
        {
            _saleCartInitLock.Release();
        }
    }

    private static SaleCartTab TaoCartTab(SaleCartDraftState state)
    {
        var tab = new SaleCartTab { State = state };
        tab.CapNhatTuState();
        return tab;
    }

    /// <summary>
    /// Sau khi mở lại phần mềm, thông tin hàng hóa trong bản nháp (tên, tồn kho, đơn vị tính)
    /// được đọc lại từ database. Đơn giá dòng bán đã chốt, số lượng, giảm giá được giữ nguyên.
    /// </summary>
    private async Task LamMoiSanPhamTrongDonBanNhapAsync(List<SaleCartDraftState> states)
    {
        var ids = states.SelectMany(s => s.Lines).Select(l => l.SanPhamId).Where(id => id != 0).Distinct().ToList();
        foreach (var id in ids)
        {
            SanPham? product;
            try
            {
                product = await _db.GetSanPhamByIdAsync(id);
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi làm mới hàng hóa trong đơn bán nháp", ex);
                continue;
            }

            if (product is null)
                continue;

            foreach (var line in states.SelectMany(s => s.Lines).Where(l => l.SanPhamId == id))
            {
                line.MaSanPham = product.MaSanPham;
                line.TenSanPham = product.TenSanPham;
                line.DonViTinh = string.IsNullOrWhiteSpace(line.DonViTinh) ? product.DonViTinh : line.DonViTinh;
                line.DonViTinhId = product.DonViTinhId;
                line.GiaVon = product.GiaVon;
                line.GiaBan = product.GiaBan;
                line.TonKho = product.TonKho;

                // Nếu đơn giá của dòng chưa được đặt (hoặc <= 0), lấy giá bán của sản phẩm
                if (line.DonGia <= 0)
                    line.DonGia = product.GiaBan;
            }
        }
    }

    // ------------------------------------------------------------
    // CHỤP / NẠP TRẠNG THÁI ĐƠN ĐANG HIỂN THỊ
    // ------------------------------------------------------------

    private SaleCartDraftState ChupDonBanDangMo(SaleCartTab tab)
    {
        var state = tab.State;
        state.KhachHangId = SelectedSaleCustomer?.Id;
        state.MaKhachHang = SelectedSaleCustomer?.MaKhachHang;
        state.TenKhachHang = string.IsNullOrWhiteSpace(SaleCustomerName) ? "Khách lẻ" : SaleCustomerName;
        state.SoDienThoai = SaleCustomerPhone ?? "";
        state.DiaChi = SaleCustomerAddress ?? "";
        state.CanGiaoHang = SaleCanGiaoHang;
        state.InKemSdtDiaChi = SaleInKemSdtDiaChi;
        state.GhiChu = SaleGhiChu ?? "";
        state.DiscountMode = SaleDiscountMode ?? "Số tiền";
        state.DiscountInput = SaleDiscountInput;
        state.DiscountAmount = SaleDiscountAmount;
        state.TienKhachDua = TienKhachDua;
        state.IsUserManualTienKhachDua = _isUserManualTienKhachDua;
        state.PhuongThucThanhToan = SelectedSalePaymentMethod?.Ma ?? "TIEN_MAT";
        state.Subtotal = SaleSubtotal;
        state.TotalAmount = SaleTotal;
        state.UpdatedAt = DateTime.Now;
        state.HoaDonGocBiHuyId = _hoaDonGocBiHuyId > 0 ? (uint)_hoaDonGocBiHuyId : null;
        state.MaHoaDonGocBiHuy = _maHoaDonGocBiHuy;

        // Chụp danh sách dòng sản phẩm: độc lập hoàn toàn, không dùng chung tham chiếu với đơn khác
        state.Lines = SaleCart.Select(item => new SaleCartDraftLine
        {
            SanPhamId = item.SanPham.Id,
            MaSanPham = item.SanPham.MaSanPham,
            TenSanPham = item.SanPham.TenSanPham,
            DonViTinh = item.DonViTinh,
            DonViTinhId = item.SanPham.DonViTinhId,
            GiaVon = item.SanPham.GiaVon,
            GiaBan = item.SanPham.GiaBan,
            DonGia = item.DonGia,
            TonKho = item.SanPham.TonKho,
            SoLuong = item.SoLuong,
            GiamGia = item.GiamGia
        }).ToList();

        tab.CapNhatTuState();
        return state;
    }

    private void NapDonBanVaoManHinh(SaleCartDraftState state)
    {
        var truoc = _dangNapDonBan;
        _dangNapDonBan = true;
        try
        {
            foreach (var item in SaleCart)
            {
                item.QuantityChanged -= RecalculateSaleTotal;
                item.QuantityExceeded -= OnQuantityExceeded;
            }

            SaleCart.Clear();

            foreach (var line in state.Lines)
            {
                var product = new SanPham
                {
                    Id = line.SanPhamId,
                    MaSanPham = line.MaSanPham,
                    TenSanPham = line.TenSanPham,
                    DonViTinh = line.DonViTinh,
                    DonViTinhId = line.DonViTinhId,
                    GiaVon = line.GiaVon,
                    GiaBan = line.GiaBan,
                    TonKho = line.TonKho
                };

                var cartItem = new SaleCartItem
                {
                    SanPham = product,
                    DonGia = line.DonGia > 0 ? line.DonGia : line.GiaBan,
                    DonViTinh = string.IsNullOrWhiteSpace(line.DonViTinh) ? "Bó" : line.DonViTinh,
                    GiamGia = line.GiamGia,
                    SoLuong = line.SoLuong > 0 ? line.SoLuong : 1
                };

                cartItem.QuantityChanged += RecalculateSaleTotal;
                cartItem.QuantityExceeded += OnQuantityExceeded;
                SaleCart.Add(cartItem);
            }

            SelectedSaleCustomer = (state.KhachHangId is > 0)
                ? new KhachHang
                {
                    Id = state.KhachHangId.Value,
                    MaKhachHang = state.MaKhachHang ?? "",
                    TenKhachHang = state.TenKhachHang ?? "",
                    SoDienThoai = state.SoDienThoai,
                    DiaChi = state.DiaChi
                }
                : null;

            SaleCustomerName = string.IsNullOrWhiteSpace(state.TenKhachHang) ? "Khách lẻ" : state.TenKhachHang;
            SaleCustomerPhone = state.SoDienThoai ?? "";
            SaleCustomerAddress = state.DiaChi ?? "";
            SaleCanGiaoHang = state.CanGiaoHang;
            SaleInKemSdtDiaChi = state.InKemSdtDiaChi;
            SaleGhiChu = state.GhiChu ?? "";
            SaleDiscountMode = string.IsNullOrWhiteSpace(state.DiscountMode) ? "Số tiền" : state.DiscountMode;
            SaleDiscountInput = state.DiscountInput;
            SaleDiscountAmount = state.DiscountAmount;
            _isUserManualTienKhachDua = state.IsUserManualTienKhachDua;
            _isUpdatingTienKhachDuaInternally = true;
            TienKhachDua = state.TienKhachDua;
            _isUpdatingTienKhachDuaInternally = false;

            var ptMa = string.IsNullOrWhiteSpace(state.PhuongThucThanhToan) || state.PhuongThucThanhToan == "Tiền mặt"
                ? "TIEN_MAT"
                : state.PhuongThucThanhToan;
            SelectedSalePaymentMethod = SalePaymentMethods.FirstOrDefault(p => p.Ma == ptMa)
                ?? SalePaymentMethods[0];

            _hoaDonGocBiHuyId = state.HoaDonGocBiHuyId;
            _maHoaDonGocBiHuy = state.MaHoaDonGocBiHuy;
            OnPropertyChanged(nameof(HoaDonGocBiHuyId));
            OnPropertyChanged(nameof(MaHoaDonGocBiHuy));
            OnPropertyChanged(nameof(DangTaoLaiTuHoaDonDaHuy));
            OnPropertyChanged(nameof(ThongTinDonNhapTuHoaDonDaHuy));

            SaleCustomerSuggestions.Clear();
            RecalculateSaleTotal();
        }
        finally
        {
            _dangNapDonBan = truoc;
        }
    }

    public void KichHoatCartTab(SaleCartTab tab)
    {
        foreach (var t in SaleCartTabs)
            t.IsActive = ReferenceEquals(t, tab);

        ActiveSaleCartTab = tab;
        NapDonBanVaoManHinh(tab.State);
    }

    // ------------------------------------------------------------
    // TỰ ĐỘNG LƯU (DEBOUNCE)
    // ------------------------------------------------------------

    public void ScheduleSaleCartAutoSave()
    {
        if (_dangNapDonBan || !_saleCartSessionReady || ActiveSaleCartTab is null)
            return;

        // Cập nhật tóm tắt tab đang active ngay lập tức để header hiển thị đúng
        ActiveSaleCartTab.TenKhachHang = string.IsNullOrWhiteSpace(SaleCustomerName) ? "Khách lẻ" : SaleCustomerName;
        var totalQty = (int)Math.Round(SaleCart.Sum(i => i.SoLuong));
        ActiveSaleCartTab.SoLuongSanPham = totalQty > 0 ? totalQty : SaleCart.Count;
        ActiveSaleCartTab.TongTien = SaleTotal;

        _saleCartDirty = true;

        _saleCartAutoSaveCts?.Cancel();
        _saleCartAutoSaveCts?.Dispose();
        var cts = new CancellationTokenSource();
        _saleCartAutoSaveCts = cts;

        _ = ChayAutoSaveSaleCartAsync(cts.Token);
    }

    private async Task ChayAutoSaveSaleCartAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SaleCartAutoSaveDelayMs, token);
            await FlushSaleCartDraftAsync();
        }
        catch (OperationCanceledException)
        {
            // Bị hủy khi người dùng thao tác tiếp
        }
    }

    public async Task<bool> FlushSaleCartDraftAsync(bool force = false)
    {
        var tab = ActiveSaleCartTab;
        if (tab is null || !_saleCartSessionReady)
            return true;

        if (!_saleCartDirty && !force)
            return true;

        _saleCartAutoSaveCts?.Cancel();
        _saleCartDirty = false;

        var state = ChupDonBanDangMo(tab);
        var json = JsonSerializer.Serialize(state);
        var seq = ++_saleCartSeq;
        var empty = state.IsEmpty;
        var cartId = state.CartId;
        var tongTien = tab.TongTien;

        try
        {
            await GhiDonBanNhapAsync(state, json, seq, empty, tongTien);
            return true;
        }
        catch (Exception ex)
        {
            _saleCartDirty = true;
            SafeLog.WriteError("Lỗi lưu nháp đơn bán " + cartId, ex);
            StatusMessage = "Không lưu được đơn bán nháp: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Lưu draft riêng cho một tab cụ thể (dùng khi chuyển tab hoặc tạo tab mới để không nhầm dữ liệu).
    /// </summary>
    public async Task<bool> LuuDraftCuaTabAsync(SaleCartTab tab)
    {
        if (tab is null || !_saleCartSessionReady)
            return true;

        var state = tab.State;
        var json = JsonSerializer.Serialize(state);
        var seq = ++_saleCartSeq;
        var empty = state.IsEmpty;
        var tongTien = tab.TongTien;

        try
        {
            await GhiDonBanNhapAsync(state, json, seq, empty, tongTien);
            return true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi lưu nháp đơn bán " + state.CartId, ex);
            return false;
        }
    }

    private async Task GhiDonBanNhapAsync(SaleCartDraftState state, string json, long seq, bool empty, decimal tongTien)
    {
        await _saleCartSaveLock.WaitAsync();
        try
        {
            if (_saleCartDaGhiSeq.TryGetValue(state.CartId, out var daGhi) && daGhi > seq)
                return;

            if (empty)
                await _db.XoaDonBanNhapAsync(state.CartId);
            else
                await _db.LuuDonBanNhapAsync(state, json, tongTien);

            _saleCartDaGhiSeq[state.CartId] = seq;
        }
        finally
        {
            _saleCartSaveLock.Release();
        }
    }

    public void HuyAutoSaveSaleCart()
    {
        _saleCartAutoSaveCts?.Cancel();
        _saleCartDirty = false;
    }

    // ------------------------------------------------------------
    // CÁC LỆNH QUẢN LÝ GIỎ HÀNG: TẠO MỚI / CHUYỂN / ĐÓNG / RESET
    // ------------------------------------------------------------

    [RelayCommand]
    private async Task TaoDonBanMoiAsync()
    {
        if (IsThanhToanDangXuLy)
            return;

        await EnsureSaleCartSessionAsync();

        if (SaleCartTabs.Count >= SaleCartToiDaSoDon)
        {
            await ShowMessageAsync("Thông báo", $"Chỉ mở tối đa {SaleCartToiDaSoDon} giỏ hàng cùng lúc. Hãy hoàn tất hoặc đóng bớt giỏ hàng.");
            return;
        }

        // Chụp đồng bộ dữ liệu của đơn hiện tại trước khi tạo đơn mới
        if (ActiveSaleCartTab is { } oldTab)
        {
            ChupDonBanDangMo(oldTab);
            _saleCartDirty = false;
            _ = LuuDraftCuaTabAsync(oldTab);
        }

        var soThuTu = SaleCartTabs.Count == 0 ? 1 : SaleCartTabs.Max(t => t.State.SoThuTu) + 1;
        var tab = TaoCartTab(new SaleCartDraftState { SoThuTu = soThuTu });
        SaleCartTabs.Add(tab);
        OnPropertyChanged(nameof(CoNhieuDonBan));
        KichHoatCartTab(tab);

        StatusMessage = $"Đã tạo {tab.TieuDe}.";
    }

    [RelayCommand]
    private void ChuyenDonBan(SaleCartTab? tab)
    {
        if (tab is null || ReferenceEquals(tab, ActiveSaleCartTab) || IsThanhToanDangXuLy)
            return;

        if (!SaleCartTabs.Contains(tab))
            return;

        // Chụp đồng bộ dữ liệu của đơn hiện tại trước khi chuyển đổi
        if (ActiveSaleCartTab is { } oldTab)
        {
            ChupDonBanDangMo(oldTab);
            _saleCartDirty = false;
            _ = LuuDraftCuaTabAsync(oldTab);
        }

        KichHoatCartTab(tab);
    }

    [RelayCommand]
    private async Task DongDonBanAsync(SaleCartTab? tab)
    {
        if (tab is null || IsThanhToanDangXuLy || !SaleCartTabs.Contains(tab))
            return;

        var coDuLieu = ReferenceEquals(tab, ActiveSaleCartTab)
            ? SaleCart.Count > 0 || (SelectedSaleCustomer is not null && SelectedSaleCustomer.Id > 0)
            : !tab.State.IsEmpty;

        if (coDuLieu)
        {
            var confirmed = await ShowConfirmationAsync(
                "Đóng giỏ hàng",
                $"Đóng \"{tab.TieuDe}\"? Dữ liệu chưa thanh toán của giỏ hàng này sẽ bị hủy.",
                "Hủy đơn này",
                "Tiếp tục đơn");

            if (!confirmed)
                return;
        }

        if (SaleCartTabs.Count == 1)
        {
            await ThayDonBangDonTrongAsync(tab);
            StatusMessage = "Đã làm mới giỏ hàng hiện tại.";
            return;
        }

        await XoaCartTabAsync(tab);
        StatusMessage = $"Đã đóng {tab.TieuDe}.";
    }

    [RelayCommand]
    private async Task ResetDonBanHienTaiAsync()
    {
        var tab = ActiveSaleCartTab;
        if (tab is null || IsThanhToanDangXuLy)
            return;

        if (SaleCart.Count > 0 || (SelectedSaleCustomer is not null && SelectedSaleCustomer.Id > 0))
        {
            var confirmed = await ShowConfirmationAsync(
                "Làm mới giỏ hàng",
                $"Bạn có chắc muốn xóa toàn bộ sản phẩm trong \"{tab.TieuDe}\" và làm mới?\n" +
                "Các giỏ hàng khác không bị ảnh hưởng.",
                "Làm mới",
                "Hủy");

            if (!confirmed)
                return;
        }

        await ThayDonBangDonTrongAsync(tab);
        StatusMessage = $"Đã làm mới {tab.TieuDe}.";
    }

    private async Task ThayDonBangDonTrongAsync(SaleCartTab tab)
    {
        var cuId = tab.CartId;
        var soThuTu = tab.State.SoThuTu;

        if (ReferenceEquals(tab, ActiveSaleCartTab))
            HuyAutoSaveSaleCart();

        tab.State = new SaleCartDraftState { SoThuTu = soThuTu };
        tab.CapNhatTuState();

        if (ReferenceEquals(tab, ActiveSaleCartTab))
            NapDonBanVaoManHinh(tab.State);

        await XoaBanNhapDonAsync(cuId);
    }

    private async Task XoaCartTabAsync(SaleCartTab tab)
    {
        var index = SaleCartTabs.IndexOf(tab);
        var laTabDangMo = ReferenceEquals(tab, ActiveSaleCartTab);

        if (laTabDangMo)
            HuyAutoSaveSaleCart();

        SaleCartTabs.Remove(tab);
        OnPropertyChanged(nameof(CoNhieuDonBan));

        if (laTabDangMo && SaleCartTabs.Count > 0)
            KichHoatCartTab(SaleCartTabs[Math.Min(index, SaleCartTabs.Count - 1)]);

        await XoaBanNhapDonAsync(tab.CartId);
    }

    private async Task XoaBanNhapDonAsync(string cartId)
    {
        await _saleCartSaveLock.WaitAsync();
        try
        {
            _saleCartDaGhiSeq[cartId] = long.MaxValue;
            await _db.XoaDonBanNhapAsync(cartId);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xóa đơn bán nháp " + cartId, ex);
        }
        finally
        {
            _saleCartSaveLock.Release();
        }
    }

    /// <summary>
    /// Sau khi thanh toán thành công: chỉ đóng đúng giỏ hàng vừa hoàn tất.
    /// Các giỏ hàng khác giữ nguyên 100%. Nếu không còn giỏ nào thì mở một giỏ trống mới.
    /// </summary>
    public void DongTabSauKhiThanhToan(SaleCartTab tab, uint hoaDonId)
    {
        _saleCartDaGhiSeq[tab.CartId] = long.MaxValue;
        HuyAutoSaveSaleCart();
        _ = _db.ChuyenDonBanNhapSangHoanTatAsync(tab.CartId, hoaDonId);

        var index = Math.Max(0, SaleCartTabs.IndexOf(tab));
        SaleCartTabs.Remove(tab);

        if (SaleCartTabs.Count == 0)
        {
            var moi = TaoCartTab(new SaleCartDraftState { SoThuTu = 1 });
            SaleCartTabs.Add(moi);
            OnPropertyChanged(nameof(CoNhieuDonBan));
            KichHoatCartTab(moi);
            return;
        }

        OnPropertyChanged(nameof(CoNhieuDonBan));
        KichHoatCartTab(SaleCartTabs[Math.Min(index, SaleCartTabs.Count - 1)]);
    }
}
