using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
/// Quản lý NHIỀU phiếu nhập hàng nháp cùng lúc + tự động lưu / khôi phục (issue #56).
///
/// Thiết kế:
/// - Mỗi phiếu là một <see cref="PhieuNhapTab"/> với <see cref="PhieuNhapDraftState"/> riêng
///   (ID riêng, danh sách dòng riêng, NCC / chi phí / thanh toán / ghi chú riêng).
/// - Các thuộc tính PhieuNhap* sẵn có của ViewModel là "bản đang hiển thị" của phiếu đang chọn.
///   Chuyển phiếu = chụp trạng thái phiếu cũ vào State rồi nạp State của phiếu mới
///   (không truy vấn database, không dùng chung đối tượng giữa các phiếu).
/// - Mọi thay đổi được ghi xuống bảng phieu_nhap_nhap sau khi người dùng ngừng thao tác
///   ~0,6 giây (debounce), nên không ghi mỗi phím gõ và vẫn không mất dữ liệu khi tắt app.
/// - Phiếu nháp KHÔNG đụng tới tồn kho / công nợ / sổ quỹ; chỉ "Hoàn thành" mới ghi nhận.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private const int PhieuNhapAutoSaveDelayMs = 600;
    private const int PhieuNhapToiDaSoPhieu = 20;

    public ObservableCollection<PhieuNhapTab> PhieuNhapTabs { get; } = new();

    [ObservableProperty] private PhieuNhapTab? activePhieuNhapTab;

    private bool _phieuNhapSessionReady;
    private bool _dangNapPhieuNhap;
    private bool _phieuNhapDirty;
    private long _phieuNhapSeq;
    private CancellationTokenSource? _phieuNhapAutoSaveCts;
    private readonly SemaphoreSlim _phieuNhapInitLock = new(1, 1);
    private readonly SemaphoreSlim _phieuNhapSaveLock = new(1, 1);
    private readonly Dictionary<string, long> _phieuNhapDaGhiSeq = new();

    /// <summary>Mã hiển thị: phiếu nháp chưa có mã chính thức, mã được sinh khi hoàn thành.</summary>
    public string PhieuNhapMaHienThi =>
        string.IsNullOrWhiteSpace(PhieuNhapMa) ? "Tự sinh khi hoàn thành" : PhieuNhapMa;

    public bool CoNhieuPhieuNhap => PhieuNhapTabs.Count > 1;

    /// <summary>Còn thay đổi chưa ghi xuống database (dùng khi đóng cửa sổ).</summary>
    public bool HasPendingPhieuNhapDraft => _phieuNhapDirty;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(PhieuNhapMa):
                OnPropertyChanged(nameof(PhieuNhapMaHienThi));
                break;

            case nameof(SelectedPhieuNhapNcc):
                if (!_dangNapPhieuNhap && ActivePhieuNhapTab is { } tab)
                    tab.TenNhaCungCap = SelectedPhieuNhapNcc?.TenNhaCungCap;
                SchedulePhieuNhapAutoSave();
                break;

            case nameof(PhieuNhapSoHoaDonDauVao):
            case nameof(PhieuNhapGhiChu):
            case nameof(PhieuNhapNgayNhap):
            case nameof(PhieuNhapChiPhiKhac):
            case nameof(PhieuNhapDaThanhToan):
            case nameof(PhieuNhapPhuongThuc):
                SchedulePhieuNhapAutoSave();
                break;
        }
    }

    // ------------------------------------------------------------
    // KHỞI TẠO / KHÔI PHỤC
    // ------------------------------------------------------------

    /// <summary>
    /// Mở màn Nhập hàng. Lần đầu trong phiên: nạp các phiếu nháp từ database (khôi phục sau khi
    /// mở lại phần mềm). Các lần sau chỉ hiển thị lại phiếu đang thao tác, KHÔNG reset.
    /// </summary>
    private async Task EnsurePhieuNhapSessionAsync()
    {
        IsNhapHangFormVisible = true;

        if (_phieuNhapSessionReady)
            return;

        await _phieuNhapInitLock.WaitAsync();
        try
        {
            if (_phieuNhapSessionReady)
                return;

            PhieuNhapNhanVien = Session.CurrentUser?.HoTen ?? "Chưa đăng nhập";
            await LoadPhieuNhapDonViTinhOptionsAsync();

            var restored = new List<PhieuNhapDraftState>();
            try
            {
                var records = await _db.GetPhieuNhapNhapDangSoanAsync();
                foreach (var record in records)
                {
                    try
                    {
                        var state = JsonSerializer.Deserialize<PhieuNhapDraftState>(record.DuLieu);
                        if (state is null)
                            continue;

                        state.DraftId = record.DraftId;
                        state.SoThuTu = record.SoThuTu > 0 ? record.SoThuTu : state.SoThuTu;
                        restored.Add(state);
                    }
                    catch (Exception ex)
                    {
                        SafeLog.WriteError("Loi doc phieu nhap nhap " + record.DraftId, ex);
                    }
                }

                await LamMoiSanPhamTrongPhieuNhapNhapAsync(restored);
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Loi khoi phuc phieu nhap nhap", ex);
                StatusMessage = "Không khôi phục được phiếu nhập nháp: " + ex.Message;
            }

            PhieuNhapTabs.Clear();
            foreach (var state in restored)
                PhieuNhapTabs.Add(TaoTab(state));

            if (PhieuNhapTabs.Count == 0)
                PhieuNhapTabs.Add(TaoTab(new PhieuNhapDraftState { SoThuTu = 1 }));

            _phieuNhapSessionReady = true;
            OnPropertyChanged(nameof(CoNhieuPhieuNhap));
            KichHoatTab(PhieuNhapTabs[0]);

            if (restored.Count > 0)
                StatusMessage = $"Đã khôi phục {restored.Count} phiếu nhập nháp.";
        }
        finally
        {
            _phieuNhapInitLock.Release();
        }
    }

    private static PhieuNhapTab TaoTab(PhieuNhapDraftState state)
    {
        var tab = new PhieuNhapTab { State = state };
        tab.CapNhatTuState();
        return tab;
    }

    /// <summary>
    /// Sau khi mở lại phần mềm, thông tin hàng hóa trong bản nháp có thể đã cũ
    /// (tên / ĐVT / giá bán sửa ở nơi khác) nên đọc lại từ danh mục, mỗi mã hàng đúng một lần.
    /// Số lượng, giá nhập, giảm giá của dòng nháp được giữ nguyên.
    /// </summary>
    private async Task LamMoiSanPhamTrongPhieuNhapNhapAsync(List<PhieuNhapDraftState> states)
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
                SafeLog.WriteError("Loi lam moi hang hoa trong phieu nhap nhap", ex);
                continue;
            }

            if (product is null)
                continue;

            foreach (var line in states.SelectMany(s => s.Lines).Where(l => l.SanPhamId == id))
                CapNhatDongNhapTuSanPham(line, product);
        }
    }

    private static void CapNhatDongNhapTuSanPham(PhieuNhapDraftLine line, SanPham product)
    {
        line.MaSanPham = product.MaSanPham;
        line.TenSanPham = product.TenSanPham;
        line.DonViTinh = product.DonViTinh;
        line.DonViTinhId = product.DonViTinhId;
        line.GiaVon = product.GiaVon;
        line.GiaBan = product.GiaBan;
        line.TonKho = product.TonKho;

        if (product.DonViTinhId is > 0 || !string.IsNullOrWhiteSpace(product.DonViTinh))
        {
            line.ChoPhepChonDonViTinh = false;
            line.DonViTinhChonId = null;
        }
    }

    // ------------------------------------------------------------
    // CHỤP / NẠP TRẠNG THÁI PHIẾU ĐANG HIỂN THỊ
    // ------------------------------------------------------------

    private PhieuNhapDraftState ChupPhieuNhapDangMo(PhieuNhapTab tab)
    {
        var state = tab.State;
        state.NhaCungCapId = SelectedPhieuNhapNcc?.Id ?? 0;
        state.MaNhaCungCap = SelectedPhieuNhapNcc?.MaNhaCungCap;
        state.TenNhaCungCap = SelectedPhieuNhapNcc?.TenNhaCungCap;
        state.NgayNhap = PhieuNhapNgayNhap;
        state.SoHoaDonDauVao = PhieuNhapSoHoaDonDauVao ?? "";
        state.GhiChu = PhieuNhapGhiChu ?? "";
        state.ChiPhiKhac = PhieuNhapChiPhiKhac;
        state.DaThanhToan = PhieuNhapDaThanhToan;
        state.PhuongThuc = PhieuNhapPhuongThuc;

        // Danh sách dòng mới hoàn toàn: không chia sẻ tham chiếu với phiếu khác.
        state.Lines = PhieuNhapItems.Select(item => new PhieuNhapDraftLine
        {
            SanPhamId = item.SanPham.Id,
            MaSanPham = item.SanPham.MaSanPham,
            TenSanPham = item.SanPham.TenSanPham,
            DonViTinh = item.SanPham.DonViTinh,
            DonViTinhId = item.SanPham.DonViTinhId,
            GiaVon = item.SanPham.GiaVon,
            GiaBan = item.SanPham.GiaBan,
            TonKho = item.SanPham.TonKho,
            SoLuong = item.SoLuong,
            DonGiaNhap = item.DonGiaNhap,
            GiamGia = item.GiamGia,
            DonViTinhChonId = item.SelectedDonViTinh is { Id: > 0 } ? item.SelectedDonViTinh.Id : null,
            ChoPhepChonDonViTinh = item.ChoPhepChonDonViTinh
        }).ToList();

        tab.CapNhatTuState();
        return state;
    }

    private void NapPhieuNhapVaoManHinh(PhieuNhapDraftState state)
    {
        var truoc = _dangNapPhieuNhap;
        _dangNapPhieuNhap = true;
        try
        {
            BoDangKyPhieuNhapItems();
            PhieuNhapItems.Clear();

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

                var row = new ChiTietPhieuNhapHang(product, PhieuNhapDonViTinhOptions)
                {
                    SoLuong = line.SoLuong,
                    DonGiaNhap = line.DonGiaNhap,
                    GiamGia = line.GiamGia,
                    ChoPhepChonDonViTinh = line.ChoPhepChonDonViTinh
                };

                if (line.DonViTinhChonId is > 0)
                    row.SelectedDonViTinh = PhieuNhapDonViTinhOptions.FirstOrDefault(o => o.Id == line.DonViTinhChonId);

                row.PropertyChanged += ChiTietPhieuNhapHang_OnPropertyChanged;
                PhieuNhapItems.Add(row);
            }

            SelectedPhieuNhapNcc = state.NhaCungCapId == 0
                ? null
                : new NhaCungCap
                {
                    Id = state.NhaCungCapId,
                    MaNhaCungCap = state.MaNhaCungCap ?? "",
                    TenNhaCungCap = state.TenNhaCungCap ?? ""
                };

            ImportSupplierSearch = state.TenNhaCungCap ?? "";
            PhieuNhapSupplierSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));

            PhieuNhapNgayNhap = state.NgayNhap ?? DateTime.Now;
            PhieuNhapSoHoaDonDauVao = state.SoHoaDonDauVao;
            PhieuNhapGhiChu = state.GhiChu;
            PhieuNhapChiPhiKhac = state.ChiPhiKhac;
            PhieuNhapDaThanhToan = state.DaThanhToan;
            PhieuNhapPhuongThuc = string.IsNullOrWhiteSpace(state.PhuongThuc) ? "Tiền mặt" : state.PhuongThuc;
            PhieuNhapMa = "";
            _phieuNhapId = 0;
            ImportProductSearch = "";

            RecalculatePhieuNhapTongTienHang();
            RecalculatePhieuNhapTotal();

            // Gợi ý hàng hóa dùng chung cho cả màn hình: chỉ ẩn các hàng đã có trong phiếu này,
            // không truy vấn lại database mỗi lần chuyển phiếu.
            var daChon = PhieuNhapItems.Select(i => i.SanPham.Id).ToHashSet();
            foreach (var goiY in PhieuNhapProductSuggestions.Where(p => daChon.Contains(p.Id)).ToList())
                PhieuNhapProductSuggestions.Remove(goiY);
            OnPropertyChanged(nameof(HasPhieuNhapSuggestions));
        }
        finally
        {
            _dangNapPhieuNhap = truoc;
        }
    }

    private void KichHoatTab(PhieuNhapTab tab)
    {
        foreach (var t in PhieuNhapTabs)
            t.IsActive = ReferenceEquals(t, tab);

        ActivePhieuNhapTab = tab;
        NapPhieuNhapVaoManHinh(tab.State);
    }

    // ------------------------------------------------------------
    // TỰ ĐỘNG LƯU (DEBOUNCE)
    // ------------------------------------------------------------

    private void SchedulePhieuNhapAutoSave()
    {
        if (_dangNapPhieuNhap || !_phieuNhapSessionReady || ActivePhieuNhapTab is null)
            return;

        _phieuNhapDirty = true;

        _phieuNhapAutoSaveCts?.Cancel();
        _phieuNhapAutoSaveCts?.Dispose();
        var cts = new CancellationTokenSource();
        _phieuNhapAutoSaveCts = cts;

        _ = ChayAutoSavePhieuNhapAsync(cts.Token);
    }

    private async Task ChayAutoSavePhieuNhapAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(PhieuNhapAutoSaveDelayMs, token);
            await FlushPhieuNhapDraftAsync();
        }
        catch (OperationCanceledException)
        {
            // Người dùng còn đang thao tác tiếp: bỏ lần lưu này, lần sau sẽ lưu bản mới nhất.
        }
    }

    /// <summary>
    /// Chụp phiếu đang mở và ghi xuống database ngay (nếu có thay đổi hoặc <paramref name="force"/>).
    /// Dữ liệu được chụp + serialize đồng bộ trên UI thread nên bản ghi luôn khớp thời điểm gọi.
    /// </summary>
    public async Task<bool> FlushPhieuNhapDraftAsync(bool force = false)
    {
        var tab = ActivePhieuNhapTab;
        if (tab is null || !_phieuNhapSessionReady)
            return true;

        if (!_phieuNhapDirty && !force)
            return true;

        _phieuNhapAutoSaveCts?.Cancel();
        _phieuNhapDirty = false;

        var state = ChupPhieuNhapDangMo(tab);
        var json = JsonSerializer.Serialize(state);
        var seq = ++_phieuNhapSeq;
        var empty = state.IsEmpty;
        var draftId = state.DraftId;

        try
        {
            await GhiPhieuNhapNhapAsync(state, json, seq, empty);
            return true;
        }
        catch (Exception ex)
        {
            _phieuNhapDirty = true;
            SafeLog.WriteError("Loi luu nhap phieu nhap " + draftId, ex);
            StatusMessage = "Không lưu được phiếu nhập nháp: " + ex.Message;
            return false;
        }
    }

    private async Task GhiPhieuNhapNhapAsync(PhieuNhapDraftState state, string json, long seq, bool empty)
    {
        await _phieuNhapSaveLock.WaitAsync();
        try
        {
            // Bỏ qua nếu đã có bản mới hơn (hoặc phiếu đã đóng / hoàn tất) được ghi trước.
            if (_phieuNhapDaGhiSeq.TryGetValue(state.DraftId, out var daGhi) && daGhi > seq)
                return;

            if (empty)
                await _db.XoaPhieuNhapNhapAsync(state.DraftId);
            else
                await _db.LuuPhieuNhapNhapAsync(state, json);

            _phieuNhapDaGhiSeq[state.DraftId] = seq;
        }
        finally
        {
            _phieuNhapSaveLock.Release();
        }
    }

    private void HuyAutoSavePhieuNhap()
    {
        _phieuNhapAutoSaveCts?.Cancel();
        _phieuNhapDirty = false;
    }

    // ------------------------------------------------------------
    // CÁC LỆNH: TẠO / CHUYỂN / LƯU NHÁP / RESET / ĐÓNG
    // ------------------------------------------------------------

    [RelayCommand]
    private async Task TaoPhieuNhapMoiAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.PurchasesCreate, "Bạn không có quyền tạo phiếu nhập hàng."))
            return;

        await EnsurePhieuNhapSessionAsync();

        if (IsSavingPhieuNhap)
            return;

        if (PhieuNhapTabs.Count >= PhieuNhapToiDaSoPhieu)
        {
            await ShowMessageAsync("Thông báo", $"Chỉ mở tối đa {PhieuNhapToiDaSoPhieu} phiếu nhập cùng lúc. Hãy hoàn thành hoặc đóng bớt phiếu.");
            return;
        }

        // Lưu phiếu đang mở trước khi rời đi (không chờ database).
        _ = FlushPhieuNhapDraftAsync();

        var soThuTu = PhieuNhapTabs.Count == 0 ? 1 : PhieuNhapTabs.Max(t => t.State.SoThuTu) + 1;
        var tab = TaoTab(new PhieuNhapDraftState { SoThuTu = soThuTu });
        PhieuNhapTabs.Add(tab);
        OnPropertyChanged(nameof(CoNhieuPhieuNhap));
        KichHoatTab(tab);

        StatusMessage = $"Đã tạo {tab.TieuDe}.";
    }

    [RelayCommand]
    private void ChuyenPhieuNhap(PhieuNhapTab? tab)
    {
        if (tab is null || ReferenceEquals(tab, ActivePhieuNhapTab) || IsSavingPhieuNhap)
            return;

        if (!PhieuNhapTabs.Contains(tab))
            return;

        // Chụp + ghi phiếu cũ (phần chụp chạy đồng bộ ngay trong lời gọi này),
        // sau đó nạp phiếu mới từ bộ nhớ: phiếu cũ được giữ nguyên.
        _ = FlushPhieuNhapDraftAsync();
        KichHoatTab(tab);
    }

    [RelayCommand]
    private async Task LuuNhapPhieuNhapAsync()
    {
        var ok = await FlushPhieuNhapDraftAsync(force: true);
        if (ok)
            StatusMessage = ActivePhieuNhapTab is { State.IsEmpty: true }
                ? "Phiếu nhập đang trống nên chưa có gì để lưu nháp."
                : "Đã lưu nháp phiếu nhập. Có thể đóng phần mềm và quay lại tiếp tục sau.";
    }

    /// <summary>Reset: xóa dữ liệu của ĐÚNG phiếu đang chọn (có xác nhận), các phiếu khác giữ nguyên.</summary>
    [RelayCommand]
    private async Task ResetPhieuNhapAsync()
    {
        var tab = ActivePhieuNhapTab;
        if (tab is null || IsSavingPhieuNhap)
            return;

        var confirmed = await ShowConfirmationAsync(
            "Reset phiếu nhập",
            $"Bạn có chắc muốn xóa toàn bộ dữ liệu của \"{tab.TieuDe}\" và làm mới thành phiếu trống?\n" +
            "Các phiếu nhập khác không bị ảnh hưởng. Sản phẩm trong danh mục không bị xóa.",
            "Reset phiếu",
            "Hủy");

        if (!confirmed)
        {
            StatusMessage = "Đã hủy reset phiếu nhập.";
            return;
        }

        await ThayPhieuBangPhieuTrongAsync(tab);
        StatusMessage = "Đã reset phiếu nhập hiện tại.";
    }

    /// <summary>Đóng một thẻ phiếu nhập (xóa bản nháp của phiếu đó, không ảnh hưởng phiếu khác).</summary>
    [RelayCommand]
    private async Task DongPhieuNhapAsync(PhieuNhapTab? tab)
    {
        if (tab is null || IsSavingPhieuNhap || !PhieuNhapTabs.Contains(tab))
            return;

        var coDuLieu = ReferenceEquals(tab, ActivePhieuNhapTab)
            ? PhieuNhapItems.Count > 0 || SelectedPhieuNhapNcc is not null
            : !tab.State.IsEmpty;

        if (coDuLieu)
        {
            var confirmed = await ShowConfirmationAsync(
                "Đóng phiếu nhập",
                $"Đóng \"{tab.TieuDe}\"? Dữ liệu nháp của phiếu này sẽ bị xóa.",
                "Đóng phiếu",
                "Hủy");

            if (!confirmed)
                return;
        }

        if (PhieuNhapTabs.Count == 1)
        {
            await ThayPhieuBangPhieuTrongAsync(tab);
            return;
        }

        await XoaTabAsync(tab);
    }

    /// <summary>Thay thẻ bằng một phiếu trống mới (ID mới), xóa bản nháp cũ trong database.</summary>
    private async Task ThayPhieuBangPhieuTrongAsync(PhieuNhapTab tab)
    {
        var cuId = tab.DraftId;
        var soThuTu = tab.State.SoThuTu;

        if (ReferenceEquals(tab, ActivePhieuNhapTab))
            HuyAutoSavePhieuNhap();

        tab.State = new PhieuNhapDraftState { SoThuTu = soThuTu };
        tab.CapNhatTuState();

        if (ReferenceEquals(tab, ActivePhieuNhapTab))
            NapPhieuNhapVaoManHinh(tab.State);

        await XoaBanNhapAsync(cuId);
    }

    private async Task XoaTabAsync(PhieuNhapTab tab)
    {
        var index = PhieuNhapTabs.IndexOf(tab);
        var laTabDangMo = ReferenceEquals(tab, ActivePhieuNhapTab);

        if (laTabDangMo)
            HuyAutoSavePhieuNhap();

        PhieuNhapTabs.Remove(tab);
        OnPropertyChanged(nameof(CoNhieuPhieuNhap));

        if (laTabDangMo && PhieuNhapTabs.Count > 0)
            KichHoatTab(PhieuNhapTabs[Math.Min(index, PhieuNhapTabs.Count - 1)]);

        await XoaBanNhapAsync(tab.DraftId);
    }

    private async Task XoaBanNhapAsync(string draftId)
    {
        await _phieuNhapSaveLock.WaitAsync();
        try
        {
            // Chặn mọi lần ghi muộn của phiếu đã đóng làm "sống lại" bản nháp.
            _phieuNhapDaGhiSeq[draftId] = long.MaxValue;
            await _db.XoaPhieuNhapNhapAsync(draftId);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi xoa phieu nhap nhap " + draftId, ex);
            StatusMessage = "Không xóa được bản nháp trong database: " + ex.Message;
        }
        finally
        {
            _phieuNhapSaveLock.Release();
        }
    }

    /// <summary>
    /// Sau khi hoàn thành phiếu: chỉ đóng đúng thẻ vừa hoàn thành. Các phiếu khác giữ nguyên;
    /// nếu không còn phiếu nào thì mở một phiếu trống mới.
    /// </summary>
    private void DongTabSauKhiHoanThanh(PhieuNhapTab tab)
    {
        _phieuNhapDaGhiSeq[tab.DraftId] = long.MaxValue; // bản nháp đã chuyển sang DA_HOAN_TAT trong transaction
        HuyAutoSavePhieuNhap();
        _phieuNhapId = 0;

        var index = Math.Max(0, PhieuNhapTabs.IndexOf(tab));
        PhieuNhapTabs.Remove(tab);

        if (PhieuNhapTabs.Count == 0)
        {
            var moi = TaoTab(new PhieuNhapDraftState { SoThuTu = 1 });
            PhieuNhapTabs.Add(moi);
            OnPropertyChanged(nameof(CoNhieuPhieuNhap));
            KichHoatTab(moi);
            return;
        }

        OnPropertyChanged(nameof(CoNhieuPhieuNhap));
        KichHoatTab(PhieuNhapTabs[Math.Min(index, PhieuNhapTabs.Count - 1)]);
    }

    // ------------------------------------------------------------
    // MỞ CHI TIẾT HÀNG HÓA NGAY TẠI TRANG NHẬP HÀNG
    // ------------------------------------------------------------

    /// <summary>
    /// Nhấn mã hàng / tên hàng trong bảng nhập để xem / sửa danh mục hàng hóa.
    /// - Cửa sổ làm việc trên BẢN SAO đọc mới từ database, nên hủy thì phiếu không đổi gì.
    /// - Lưu xong chỉ cập nhật thông tin danh mục của dòng (mã, tên, ĐVT, giá bán...).
    ///   Số lượng, giá nhập của dòng và các phiếu nhập cũ KHÔNG bị đổi; riêng khi giá vốn danh mục
    ///   đổi thì hỏi người dùng có muốn áp giá mới cho dòng này hay không.
    /// </summary>
    [RelayCommand]
    private async Task MoChiTietSanPhamTuPhieuNhapAsync(ChiTietPhieuNhapHang? dong)
    {
        if (dong is null || dong.SanPham.Id == 0 || OwnerWindow is null)
            return;

        var coQuyenSua = CoQuyen(PermissionKeys.ProductsEdit);
        var coQuyenXem = CoQuyen(PermissionKeys.ProductsDetail);

        if (!coQuyenSua && !coQuyenXem)
        {
            await KiemTraQuyenAsync(PermissionKeys.ProductsDetail, "Bạn không có quyền xem chi tiết hàng hóa.");
            return;
        }

        try
        {
            var sanPham = await _db.GetSanPhamByIdAsync(dong.SanPham.Id);
            if (sanPham is null)
            {
                StatusMessage = "Không tìm thấy hàng hóa trong danh mục (có thể đã bị xóa).";
                return;
            }

            var giaVonCu = sanPham.GiaVon;
            var (groups, units) = await LoadProductOptionsAsync();

            var editor = new ProductEditorWindow(
                sanPham,
                groups,
                units,
                duocSuaGiaBan: DuocSuaGiaBan,
                duocSuaGiaVon: CoQuyen(PermissionKeys.ProductsUpdateCost),
                duocDieuChinhTonKho: CoQuyen(PermissionKeys.ProductsAdjustStock),
                cheDoXem: !coQuyenSua);

            var saved = await editor.ShowDialog<bool>(OwnerWindow);
            if (!saved || !coQuyenSua)
                return;

            await _db.CapNhatSanPhamAsync(sanPham);

            var moi = await _db.GetSanPhamByIdAsync(sanPham.Id) ?? sanPham;
            DongBoGiaBan(moi.Id, moi.GiaBan);
            CapNhatHangHoaTrongPhieuNhap(moi);

            // Cập nhật luôn dòng trong trang Hàng hóa nếu đang có
            var idx = SanPhams.ToList().FindIndex(p => p.Id == moi.Id);
            if (idx >= 0)
            {
                var banSao = await _db.GetSanPhamByIdAsync(moi.Id);
                if (banSao is not null)
                {
                    if (!string.IsNullOrWhiteSpace(banSao.HinhAnh) && System.IO.File.Exists(banSao.HinhAnh))
                        banSao.HinhAnhBitmap = new Avalonia.Media.Imaging.Bitmap(banSao.HinhAnh);
                    SanPhams[idx] = banSao;
                    OnPropertyChanged(nameof(IsSanPhamListEmpty));
                }
            }

            if (moi.GiaVon != giaVonCu && dong.DonGiaNhap != moi.GiaVon)
            {
                var apDung = await ShowConfirmationAsync(
                    "Giá vốn hàng hóa đã thay đổi",
                    $"Giá vốn của \"{moi.TenSanPham}\" đã đổi từ {giaVonCu:N0} đ thành {moi.GiaVon:N0} đ.\n" +
                    $"Giá nhập của dòng này trong phiếu đang là {dong.DonGiaNhap:N0} đ.\n" +
                    "Bạn có muốn đổi giá nhập của dòng này theo giá vốn mới không?",
                    "Đổi giá nhập",
                    "Giữ nguyên giá nhập");

                if (apDung)
                    dong.DonGiaNhap = moi.GiaVon;
            }

            StatusMessage = $"Đã cập nhật hàng hóa {moi.MaSanPham}. Phiếu nhập vẫn giữ nguyên số lượng và giá nhập.";
            SchedulePhieuNhapAutoSave();
            _ = LoadNhomSanPhamAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Mo chi tiet hang hoa tu phieu nhap", ex);
            StatusMessage = "Không thể cập nhật hàng hóa: " + SafeLog.Redact(ex.Message);
        }
    }

    private static SanPham TomTatSanPham(SanPham p) => new()
    {
        Id = p.Id,
        MaSanPham = p.MaSanPham,
        MaVach = p.MaVach,
        TenSanPham = p.TenSanPham,
        NhomSanPhamId = p.NhomSanPhamId,
        DonViTinhId = p.DonViTinhId,
        DonViTinh = p.DonViTinh,
        GiaBan = p.GiaBan,
        GiaVon = p.GiaVon,
        TonKho = p.TonKho,
        NhomSanPham = p.NhomSanPham,
        GhiChu = p.GhiChu
    };

    /// <summary>
    /// Đồng bộ thông tin danh mục của một hàng hóa vào MỌI phiếu nhập đang mở (phiếu đang hiển thị
    /// và các phiếu nháp khác). Chỉ đổi thông tin danh mục, không đụng số lượng / giá nhập / giảm giá.
    /// </summary>
    private void CapNhatHangHoaTrongPhieuNhap(SanPham moi)
    {
        foreach (var dong in PhieuNhapItems.Where(i => i.SanPham.Id == moi.Id))
            dong.CapNhatSanPham(TomTatSanPham(moi));

        foreach (var tab in PhieuNhapTabs.Where(t => !ReferenceEquals(t, ActivePhieuNhapTab)))
        {
            var thayDoi = false;
            foreach (var line in tab.State.Lines.Where(l => l.SanPhamId == moi.Id))
            {
                CapNhatDongNhapTuSanPham(line, moi);
                thayDoi = true;
            }

            if (thayDoi)
                _ = LuuTabKhongMoAsync(tab);
        }
    }

    private async Task LuuTabKhongMoAsync(PhieuNhapTab tab)
    {
        try
        {
            var json = JsonSerializer.Serialize(tab.State);
            await GhiPhieuNhapNhapAsync(tab.State, json, ++_phieuNhapSeq, tab.State.IsEmpty);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi luu phieu nhap nhap (tab khong mo)", ex);
        }
    }
}
