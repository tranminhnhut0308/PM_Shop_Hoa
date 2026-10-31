using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public sealed partial class MainWindowViewModel
{
    private uint? _hoaDonGocBiHuyId;
    private string? _maHoaDonGocBiHuy;
    private string _invoiceStatusFilter = "TAT_CA";

    public uint? HoaDonGocBiHuyId => _hoaDonGocBiHuyId;
    public string? MaHoaDonGocBiHuy => _maHoaDonGocBiHuy;

    public bool DangTaoLaiTuHoaDonDaHuy => !string.IsNullOrWhiteSpace(_maHoaDonGocBiHuy);

    public string ThongTinDonNhapTuHoaDonDaHuy =>
        DangTaoLaiTuHoaDonDaHuy
            ? $"🔄 Đơn nháp sao chép từ hóa đơn đã hủy #{_maHoaDonGocBiHuy} — Thay đổi thông tin cần thiết rồi bấm Thanh toán để tạo hóa đơn mới."
            : string.Empty;

    public void XoaLienKetHoaDonGocBiHuy()
    {
        _hoaDonGocBiHuyId = null;
        _maHoaDonGocBiHuy = null;
        OnPropertyChanged(nameof(HoaDonGocBiHuyId));
        OnPropertyChanged(nameof(MaHoaDonGocBiHuy));
        OnPropertyChanged(nameof(DangTaoLaiTuHoaDonDaHuy));
        OnPropertyChanged(nameof(ThongTinDonNhapTuHoaDonDaHuy));
    }

    // ============================================================
    // BỘ LỌC TRẠNG THÁI HÓA ĐƠN (Tất cả / Hoàn thành / Chưa TT / TT 1 phần / Đã hủy)
    // ============================================================

    public string InvoiceStatusFilter
    {
        get => _invoiceStatusFilter;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "TAT_CA" : value.Trim().ToUpperInvariant();
            if (_invoiceStatusFilter == normalized)
                return;

            _invoiceStatusFilter = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInvoiceStatusAll));
            OnPropertyChanged(nameof(IsInvoiceStatusHoanThanh));
            OnPropertyChanged(nameof(IsInvoiceStatusChuaThanhToan));
            OnPropertyChanged(nameof(IsInvoiceStatusThanhToanMotPhan));
            OnPropertyChanged(nameof(IsInvoiceStatusDaHuy));
        }
    }

    public bool IsInvoiceStatusAll => InvoiceStatusFilter == "TAT_CA";
    public bool IsInvoiceStatusHoanThanh => InvoiceStatusFilter == "HOAN_THANH";
    public bool IsInvoiceStatusChuaThanhToan => InvoiceStatusFilter == "CHUA_THANH_TOAN";
    public bool IsInvoiceStatusThanhToanMotPhan => InvoiceStatusFilter == "THANH_TOAN_MOT_PHAN";
    public bool IsInvoiceStatusDaHuy => InvoiceStatusFilter == "DA_HUY";

    [RelayCommand]
    private async Task LocHoaDonTheoTrangThaiAsync(string? trangThai)
    {
        InvoiceStatusFilter = trangThai ?? "TAT_CA";
        await LoadInvoicesAsync();
    }

    /// <summary>
    /// KHÓA NGHIỆP VỤ: Chức năng chỉnh sửa trực tiếp hóa đơn cũ đã bị thay thế bằng Hủy hóa đơn và Tạo lại.
    /// </summary>
    [RelayCommand]
    private async Task ChinhSuaHoaDonAsync(HoaDon? hoaDon)
    {
        await ShowMessageAsync(
            "Chức năng đã khóa theo quy định nghiệp vụ",
            "Hệ thống không cho phép chỉnh sửa trực tiếp hóa đơn đã tạo.\n\n" +
            "Khi phát hiện hóa đơn sai, vui lòng chọn 'Hủy hóa đơn' -> 'Hủy và tạo hóa đơn tương tự' để tạo hóa đơn mới từ bản nháp.");
    }

    /// <summary>
    /// Mở hộp thoại xác nhận Hủy hóa đơn (Chỉ hủy hoặc Hủy và tạo hóa đơn tương tự).
    /// </summary>
    [RelayCommand]
    private async Task HuyHoaDonAsync(HoaDon? hoaDon)
    {
        if (hoaDon is null || OwnerWindow is null)
            return;

        if (!DatabaseService.CoQuyenHuyHoaDonHienTai())
        {
            await ShowMessageAsync("Thông báo", "Bạn không có quyền hủy hóa đơn bán hàng.");
            return;
        }

        if (hoaDon.LaHoaDonDaHuy)
        {
            await ShowMessageAsync("Thông báo", $"Hóa đơn {hoaDon.MaHoaDon} đã ở trạng thái Đã hủy, không thể hủy lại.");
            return;
        }

        try
        {
            var dialog = new HuyHoaDonWindow(hoaDon.MaHoaDon);
            var ketQua = await dialog.ShowDialog<HuyHoaDonResult?>(OwnerWindow);
            if (ketQua is null)
                return;

            await XuLySauKhiHuyHoaDonAsync(ketQua);

            if (ketQua.CheDoHuy == CheDoHuyHoaDon.ChiHuy)
            {
                await ShowMessageAsync(
                    "Đã hủy hóa đơn",
                    $"Đã hủy hóa đơn {ketQua.MaHoaDon} thành công và hoàn tác dữ liệu tồn kho, công nợ, sổ quỹ liên quan.");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể hủy hóa đơn {hoaDon.MaHoaDon}: {ex.Message}";
            SafeLog.WriteError("Loi HuyHoaDonAsync", ex);
        }
    }

    public async Task XuLySauKhiHuyHoaDonAsync(HuyHoaDonResult ketQua)
    {
        _invoicesLoaded = false;
        await LoadInvoicesAsync();

        _customersLoaded = false;
        await LoadCustomersAsync();

        _ = LoadProductsAsync();

        if (IsSoQuySelected)
        {
            try
            {
                await TaiLaiSoQuyAsync();
            }
            catch (Exception sqEx)
            {
                SafeLog.WriteError("Loi lam moi so quy sau khi huy hoa don", sqEx);
            }
        }

        _ = LamMoiTongQuanNeuDangMoAsync();

        if (ketQua.CheDoHuy == CheDoHuyHoaDon.HuyVaTaoHoaDonTuongTu)
        {
            await SaoChepHoaDonDaHuySangDonNhapBanHangAsync(ketQua.DuLieuSaoChep);
        }
        else
        {
            StatusMessage = $"Đã hủy hóa đơn {ketQua.MaHoaDon} thành công.";
        }
    }

    /// <summary>
    /// Sao chép thông tin từ hóa đơn vừa hủy sang một đơn nháp mới trên màn Bán hàng.
    /// Lưu ý: Chỉ tạo dữ liệu nháp trên giao diện Bán hàng, tuyệt đối chưa tạo hóa đơn mới
    /// và chưa tác động tồn kho, doanh thu hay công nợ cho đến khi người dùng xác nhận Thanh toán.
    /// </summary>
    public async Task SaoChepHoaDonDaHuySangDonNhapBanHangAsync(ThongTinXacNhanHuyHoaDon nguon)
    {
        ResetDonHangVeMacDinh();

        _hoaDonGocBiHuyId = nguon.HoaDonId;
        _maHoaDonGocBiHuy = nguon.MaHoaDon;
        OnPropertyChanged(nameof(HoaDonGocBiHuyId));
        OnPropertyChanged(nameof(MaHoaDonGocBiHuy));
        OnPropertyChanged(nameof(DangTaoLaiTuHoaDonDaHuy));
        OnPropertyChanged(nameof(ThongTinDonNhapTuHoaDonDaHuy));

        IsSalesPanelVisible = true;

        IReadOnlyList<SanPham> danhSachSanPham = [];
        try
        {
            danhSachSanPham = await _db.GetSanPhamAsync();
            ProductSearchResults.Clear();
            foreach (var sp in danhSachSanPham)
                ProductSearchResults.Add(sp);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi tai danh sach san pham khi sao chep don nhap", ex);
        }

        // 1. Sao chép thông tin khách hàng
        if (nguon.KhachHangId is uint khId && khId > 0)
        {
            KhachHang? khach = null;
            try
            {
                var tatCaKhach = await _db.GetKhachHangAsync();
                khach = tatCaKhach.FirstOrDefault(k => k.Id == khId);
            }
            catch
            {
                // Fallback nếu không đọc được danh sách khách hàng
            }

            khach ??= new KhachHang
            {
                Id = khId,
                MaKhachHang = nguon.MaKhachHang,
                TenKhachHang = nguon.TenKhachHang,
                SoDienThoai = nguon.SoDienThoai ?? "",
                DiaChi = nguon.DiaChi ?? ""
            };

            SelectedSaleCustomer = khach;
            SaleCustomerName = khach.TenKhachHang;
            SaleCustomerPhone = !string.IsNullOrWhiteSpace(nguon.SoDienThoai)
                ? nguon.SoDienThoai
                : (khach.SoDienThoai ?? "");
            SaleCustomerAddress = !string.IsNullOrWhiteSpace(nguon.DiaChi)
                ? nguon.DiaChi
                : TaoDiaChiDayDu(khach);
        }
        else
        {
            SelectedSaleCustomer = null;
            SaleCustomerName = "Khách lẻ";
            SaleCustomerPhone = nguon.SoDienThoai ?? "";
            SaleCustomerAddress = nguon.DiaChi ?? "";
        }

        SaleCustomerSuggestions.Clear();
        SaleInKemSdtDiaChi = nguon.InKemThongTin;
        SaleGhiChu = nguon.GhiChu ?? "";

        // 2. Sao chép danh sách sản phẩm, số lượng, đơn vị tính, giá bán vào đơn nháp
        var canhBaoNhap = new List<string>();
        foreach (var dong in nguon.Items)
        {
            var spHeThong = danhSachSanPham.FirstOrDefault(p =>
                (dong.SanPhamId.HasValue && p.Id == dong.SanPhamId.Value)
                || (!string.IsNullOrWhiteSpace(dong.MaSanPham)
                    && string.Equals(p.MaSanPham, dong.MaSanPham, StringComparison.OrdinalIgnoreCase)));

            if (spHeThong is null)
            {
                canhBaoNhap.Add($"Sản phẩm '{dong.TenSanPham}' không còn trong danh mục.");
                continue;
            }

            var tonHienTai = spHeThong.TonKho;
            if (tonHienTai <= 0)
            {
                canhBaoNhap.Add($"Sản phẩm '{dong.TenSanPham}' hiện có tồn kho = 0.");
                continue;
            }

            // Tạo bản sao SanPham cho giỏ hàng nháp để giữ nguyên đơn giá và đơn vị tính của hóa đơn cũ
            var donGiaSaoChep = dong.DonGia > 0 ? dong.DonGia : spHeThong.GiaBan;
            var spBanSao = new SanPham
            {
                Id = spHeThong.Id,
                MaSanPham = spHeThong.MaSanPham,
                TenSanPham = string.IsNullOrWhiteSpace(dong.TenSanPham) ? spHeThong.TenSanPham : dong.TenSanPham,
                DonViTinh = string.IsNullOrWhiteSpace(dong.DonViTinh) ? spHeThong.DonViTinh : dong.DonViTinh,
                GiaBan = donGiaSaoChep,
                GiaVon = spHeThong.GiaVon > 0 ? spHeThong.GiaVon : dong.GiaVon,
                TonKho = tonHienTai,
                NhomSanPhamId = spHeThong.NhomSanPhamId,
                NhomSanPham = spHeThong.NhomSanPham
            };

            var soLuongNhap = Math.Min(dong.SoLuong, tonHienTai);
            if (soLuongNhap < dong.SoLuong)
            {
                canhBaoNhap.Add($"'{dong.TenSanPham}' giảm SL từ {dong.SoLuong:N0} xuống {soLuongNhap:N0} theo tồn kho hiện tại.");
            }

            var cartItem = new SaleCartItem
            {
                SanPham = spBanSao,
                SoLuong = soLuongNhap
            };

            cartItem.QuantityChanged += RecalculateSaleTotal;
            cartItem.QuantityExceeded += OnQuantityExceeded;
            SaleCart.Add(cartItem);
        }

        // 3. Sao chép giảm giá hóa đơn
        SaleDiscountMode = "Số tiền";
        SaleDiscountInput = nguon.GiamGia;
        RecalculateSaleTotal();

        // Mặc định gợi ý số tiền khách đưa bằng tổng cần trả để nhân viên dễ điều chỉnh
        TienKhachDua = SaleTotal;
        RecalculatePayment();

        var chuoiCanhBao = canhBaoNhap.Count > 0 ? " Lưu ý: " + string.Join("; ", canhBaoNhap) : "";
        StatusMessage =
            $"Đã hủy hóa đơn {nguon.MaHoaDon} và sao chép sang đơn nháp mới. Bạn có thể sửa sản phẩm, số lượng, đơn giá trước khi Lưu.{chuoiCanhBao}";
    }

    /// <summary>
    /// Xem lịch sử chỉnh sửa cũ của hóa đơn (chỉ đọc phục vụ kiểm tra dữ liệu lịch sử).
    /// </summary>
    [RelayCommand]
    private async Task XemLichSuHoaDonAsync(HoaDon? hoaDon)
    {
        if (hoaDon is null || OwnerWindow is null)
            return;

        var maHoaDon = hoaDon.MaHoaDon;
        if (string.IsNullOrWhiteSpace(maHoaDon))
            return;

        try
        {
            var historyWindow = new HoaDonLichSuWindow(maHoaDon);
            await historyWindow.ShowDialog(OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể mở lịch sử hóa đơn {maHoaDon}: {ex.Message}";
            SafeLog.WriteError("Loi XemLichSuHoaDonAsync", ex);
        }
    }
}
