using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class HoaDonEditorWindow : Window
{
    private readonly HoaDonBanHangReceipt _receipt;
    private readonly DatabaseService _db;
    private readonly IReadOnlyList<SanPham> _allProducts;
    private readonly ObservableCollection<HoaDonItemEditModel> _items;

    public HoaDonBanHangReceipt? ResultReceipt { get; private set; }

    public HoaDonEditorWindow() : this(new HoaDonBanHangReceipt(), new DatabaseService(), [])
    {
    }

    public HoaDonEditorWindow(
        HoaDonBanHangReceipt receipt,
        DatabaseService db,
        IReadOnlyList<SanPham> allProducts)
    {
        InitializeComponent();

        _receipt = receipt;
        _db = db;
        _allProducts = allProducts;

        _items = new ObservableCollection<HoaDonItemEditModel>();

        foreach (var it in _receipt.Items)
        {
            var sp = _allProducts.FirstOrDefault(p => p.Id == it.SanPhamId);
            var editItem = new HoaDonItemEditModel
            {
                Id = it.Id,
                SanPhamId = it.SanPhamId,
                MaSanPham = it.MaSanPham ?? sp?.MaSanPham ?? "",
                TenSanPham = it.TenSanPham,
                DonViTinh = it.DonViTinh,
                SoLuong = it.SoLuong,
                DonGia = it.DonGia,
                GiamGia = it.GiamGia,
                GhiChu = it.GhiChu,
                GiaVon = sp?.GiaVon ?? 0
            };
            editItem.PropertyChanged += (s, e) => TinhToanChiPhi();
            _items.Add(editItem);
        }

        GridItems.ItemsSource = _items;

        // Header info
        TxtMaHoaDon.Text = $"#{_receipt.MaHoaDon}";
        DpThoiGianBan.SelectedDate = _receipt.ThoiGianBan.Date;
        TxtSoDienThoai.Text = _receipt.SoDienThoai ?? "";

        AcbKhachHang.Text = _receipt.TenKhachHang;
        AcbKhachHang.IsEnabled = false; // Cố định khách hàng

        // Sản phẩm gợi ý tìm kiếm
        AcbSanPham.ItemsSource = _allProducts.Select(p => $"{p.MaSanPham} - {p.TenSanPham}").ToList();

        // Ghi chú
        TxtGhiChu.Text = _receipt.GhiChu ?? "";

        // Giảm giá đơn
        NumGiamGia.Value = _receipt.GiamGia;

        // Khách đã trả
        TxtKhachDaTra.Text = $"{_receipt.KhachDaThanhToan:N0} đ";

        TinhToanChiPhi();
    }

    private void TinhToanChiPhi()
    {
        decimal tongTienHang = 0;
        foreach (var item in _items)
        {
            tongTienHang += item.ThanhTien;
        }

        TxtTongTienHang.Text = $"{tongTienHang:N0} đ";

        var giamGia = NumGiamGia.Value;
        var khachCanTra = Math.Max(0, tongTienHang - giamGia);
        TxtKhachCanTra.Text = $"{khachCanTra:N0} đ";

        var daThanhToan = _receipt.KhachDaThanhToan;
        var conNoMoi = Math.Max(0, khachCanTra - daThanhToan);
        TxtConNoMoi.Text = $"{conNoMoi:N0} đ";

        var deltaNo = conNoMoi - _receipt.ConNo;
        if (deltaNo > 0)
        {
            TxtChenhLechNo.Text = $"(Tăng thêm {deltaNo:N0} đ)";
            TxtChenhLechNo.IsVisible = true;
        }
        else if (deltaNo < 0)
        {
            TxtChenhLechNo.Text = $"(Giảm bớt {Math.Abs(deltaNo):N0} đ)";
            TxtChenhLechNo.IsVisible = true;
        }
        else
        {
            TxtChenhLechNo.IsVisible = false;
        }
    }

    private void BtnThemSanPham_OnClick(object? sender, RoutedEventArgs e)
    {
        var text = AcbSanPham.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var parts = text.Split(['-'], 2);
        var keyword = parts[0].Trim();

        var sp = _allProducts.FirstOrDefault(p =>
            string.Equals(p.MaSanPham, keyword, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.TenSanPham, text, StringComparison.OrdinalIgnoreCase));

        if (sp == null)
            return;

        var existing = _items.FirstOrDefault(x => x.SanPhamId == sp.Id);
        if (existing != null)
        {
            existing.SoLuong += 1;
        }
        else
        {
            var newItem = new HoaDonItemEditModel
            {
                SanPhamId = sp.Id,
                MaSanPham = sp.MaSanPham,
                TenSanPham = sp.TenSanPham,
                DonViTinh = sp.DonViTinh ?? "Bó",
                SoLuong = 1,
                DonGia = sp.GiaBan,
                GiamGia = 0,
                GiaVon = sp.GiaVon
            };
            newItem.PropertyChanged += (s, ev) => TinhToanChiPhi();
            _items.Add(newItem);
        }

        AcbSanPham.Text = "";
        TinhToanChiPhi();
    }

    private void BtnXoaItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: HoaDonItemEditModel item })
        {
            _items.Remove(item);
            TinhToanChiPhi();
        }
    }

    private void NumGiamGia_OnValueChanged(object? sender, EventArgs e)
    {
        TinhToanChiPhi();
    }

    private void BtnXemLichSu_OnClick(object? sender, RoutedEventArgs e)
    {
    }

    private void BtnHuy_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void BtnLuu_OnClick(object? sender, RoutedEventArgs e)
    {
        TxtError.IsVisible = false;

        var lyDoSua = TxtLyDoChinhSua.Text?.Trim();
        if (string.IsNullOrWhiteSpace(lyDoSua))
        {
            lyDoSua = "Không cung cấp lý do";
        }

        if (_items.Count == 0)
        {
            TxtError.Text = "Hóa đơn phải có ít nhất 1 sản phẩm.";
            TxtError.IsVisible = true;
            return;
        }

        BtnLuu.IsEnabled = false;
        try
        {
            var request = new HoaDonEditRequest
            {
                HoaDonId = _receipt.HoaDonId,
                MaHoaDon = _receipt.MaHoaDon,
                LyDoSua = lyDoSua,
                GiamGia = NumGiamGia.Value,
                ThuKhac = _receipt.ThuKhac,
                GhiChu = TxtGhiChu.Text?.Trim(),
                Items = _items.ToList()
            };

            var updatedReceipt = await _db.ChinhSuaHoaDonBanHangAsync(request);
            ResultReceipt = updatedReceipt;
            Close(updatedReceipt);
        }
        catch (Exception ex)
        {
            TxtError.Text = $"Lỗi: {ex.Message}";
            TxtError.IsVisible = true;
            BtnLuu.IsEnabled = true;
        }
    }
}
