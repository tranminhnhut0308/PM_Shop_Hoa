using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class PhieuNhapEditorWindow : Window
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly PhieuNhapEditRequest _request;
    private readonly DatabaseService _db;
    private readonly IReadOnlyList<SanPham> _allProducts;
    private readonly IReadOnlyList<SupplierOption> _suppliers;
    private readonly ObservableCollection<PhieuNhapItemEditModel> _items;

    public bool IsSaved { get; private set; }

    public PhieuNhapEditorWindow()
        : this(new PhieuNhapEditRequest(), null!, [], [])
    {
    }

    public PhieuNhapEditorWindow(
        PhieuNhapEditRequest request,
        DatabaseService db,
        IReadOnlyList<SanPham> allProducts,
        IReadOnlyList<SupplierOption> suppliers)
    {
        InitializeComponent();

        _request = request;
        _db = db;
        _allProducts = allProducts;
        _suppliers = suppliers;
        _items = new ObservableCollection<PhieuNhapItemEditModel>(_request.Items);

        // 1. Header
        TxtMaPhieuNhap.Text = _request.MaPhieuNhap;

        // 2. Ngày nhập
        DpThoiGianNhap.SelectedDate = _request.ThoiGianNhap;

        // 3. Nhà cung cấp
        AcbNhaCungCap.ItemsSource = _suppliers;
        AcbNhaCungCap.ItemFilter = (search, item) =>
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;
            if (item is SupplierOption s)
            {
                return (!string.IsNullOrEmpty(s.DisplayText) && s.DisplayText.Contains(search, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(s.TenNhaCungCap) && s.TenNhaCungCap.Contains(search, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(s.MaNhaCungCap) && s.MaNhaCungCap.Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        };

        var selectedSupplier = _suppliers.FirstOrDefault(s => s.Id == _request.NhaCungCapId);
        if (selectedSupplier != null)
        {
            AcbNhaCungCap.SelectedItem = selectedSupplier;
            AcbNhaCungCap.Text = selectedSupplier.DisplayText;
        }

        // 4. Số HĐ đầu vào, ngày hóa đơn, người giao hàng & Ghi chú
        TxtSoHoaDon.Text = _request.SoHoaDonDauVao ?? "";
        DpNgayHoaDon.SelectedDate = _request.NgayHoaDonDauVao.HasValue
            ? new DateTimeOffset(_request.NgayHoaDonDauVao.Value)
            : null;
        TxtTenNguoiGiaoHang.Text = _request.TenNguoiGiaoHang ?? "";
        TxtSdtNguoiGiaoHang.Text = _request.SdtNguoiGiaoHang ?? "";
        TxtGhiChu.Text = _request.GhiChu ?? "";

        // 5. Phương thức thanh toán
        CbbPhuongThuc.ItemsSource = new[] { "Tiền mặt", "Chuyển khoản" };
        var pt = _request.PhuongThucThanhToan?.ToUpperInvariant() ?? "TIEN_MAT";
        CbbPhuongThuc.SelectedIndex = (pt.Contains("CHUYEN_KHOAN") || pt.Contains("NGAN_HANG") || pt.Contains("BANK")) ? 1 : 0;

        // 6. Số tiền
        NumChiPhiKhac.Value = _request.ChiPhiKhac;
        NumGiamGia.Value = _request.GiamGia;
        NumDaThanhToan.Value = _request.DaThanhToan;

        // 7. Bảng hàng hóa
        GridItems.ItemsSource = _items;

        // 8. Gợi ý sản phẩm
        AcbSanPham.ItemsSource = _allProducts;
        AcbSanPham.ItemFilter = (search, item) =>
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;
            if (item is SanPham sp)
            {
                return (!string.IsNullOrEmpty(sp.TenSanPham) && sp.TenSanPham.Contains(search, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(sp.MaSanPham) && sp.MaSanPham.Contains(search, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(sp.MaVach) && sp.MaVach.Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        };

        AcbSanPham.KeyDown += (s, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                BtnThemSanPham_OnClick(s, e);
            }
        };

        // Lắng nghe thay đổi dòng hàng
        foreach (var item in _items)
        {
            item.PropertyChanged += Item_PropertyChanged;
        }
        _items.CollectionChanged += (_, _) => CapNhatTongTien();

        CapNhatTongTien();

        // Đảm bảo cột phải luôn cuộn lên đầu khi mở cửa sổ
        Loaded += (_, _) =>
        {
            if (SvRightPanel != null)
            {
                SvRightPanel.Offset = Vector.Zero;
            }
            AcbSanPham.Focus();
        };
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PhieuNhapItemEditModel.SoLuong)
            or nameof(PhieuNhapItemEditModel.DonGiaNhap)
            or nameof(PhieuNhapItemEditModel.GiamGia)
            or nameof(PhieuNhapItemEditModel.ThanhTien))
        {
            CapNhatTongTien();
        }
    }

    private void OnCalculationInputsChanged(object? sender, EventArgs e)
    {
        CapNhatTongTien();
    }

    private void CapNhatTongTien()
    {
        if (TxtTongTienHang is null)
            return;

        var tongTienHang = _items.Where(i => i.SoLuong > 0).Sum(i => i.ThanhTien);
        var chiPhiKhac = NumChiPhiKhac?.Value ?? 0m;
        var giamGia = NumGiamGia?.Value ?? 0m;

        var tongTienNhap = Math.Max(0m, tongTienHang - giamGia + chiPhiKhac);
        var daThanhToan = NumDaThanhToan?.Value ?? 0m;
        var conno = Math.Max(0m, tongTienNhap - daThanhToan);

        TxtTongTienHang.Text = tongTienHang.ToString("N0", Vi) + " đ";
        TxtTongTienNhap.Text = tongTienNhap.ToString("N0", Vi) + " đ";
        TxtConNo.Text = conno.ToString("N0", Vi) + " đ";
    }

    private void BtnThemSanPham_OnClick(object? sender, RoutedEventArgs e)
    {
        SanPham? selectedProduct = null;

        if (AcbSanPham.SelectedItem is SanPham sp)
        {
            selectedProduct = sp;
        }
        else if (!string.IsNullOrWhiteSpace(AcbSanPham.Text))
        {
            var text = AcbSanPham.Text.Trim();
            selectedProduct = _allProducts.FirstOrDefault(p =>
                string.Equals(p.MaSanPham, text, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.MaVach, text, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.TenSanPham, text, StringComparison.OrdinalIgnoreCase));
        }

        if (selectedProduct == null)
        {
            HienThiLoi("Vui lòng chọn sản phẩm hợp lệ từ danh sách gợi ý.");
            return;
        }

        AnLoi();

        // Kiểm tra xem sản phẩm đã có trong bảng chưa
        var existingItem = _items.FirstOrDefault(i => i.SanPhamId == selectedProduct.Id);
        if (existingItem != null)
        {
            existingItem.SoLuong += 1;
        }
        else
        {
            var newItem = new PhieuNhapItemEditModel
            {
                Id = 0,
                SanPhamId = selectedProduct.Id,
                MaSanPham = selectedProduct.MaSanPham,
                TenSanPham = selectedProduct.TenSanPham,
                DonViTinh = string.IsNullOrWhiteSpace(selectedProduct.DonViTinh) ? (selectedProduct.TenDonViTinh ?? "Bó") : selectedProduct.DonViTinh,
                DonViTinhId = selectedProduct.DonViTinhId,
                SoLuong = 1,
                DonGiaNhap = selectedProduct.GiaVon,
                GiamGia = 0,
                SoLuongBanDau = 0,
                DonGiaNhapBanDau = selectedProduct.GiaVon,
                GiamGiaBanDau = 0
            };
            newItem.PropertyChanged += Item_PropertyChanged;
            _items.Add(newItem);
        }

        AcbSanPham.SelectedItem = null;
        AcbSanPham.Text = "";
        CapNhatTongTien();
    }

    private void BtnXoaItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PhieuNhapItemEditModel item)
        {
            item.PropertyChanged -= Item_PropertyChanged;
            _items.Remove(item);
            CapNhatTongTien();
        }
    }

    private async void BtnLuu_OnClick(object? sender, RoutedEventArgs e)
    {
        AnLoi();

        // 1. Kiểm tra nhà cung cấp
        SupplierOption? supplier = null;
        if (AcbNhaCungCap.SelectedItem is SupplierOption s)
        {
            supplier = s;
        }
        else if (!string.IsNullOrWhiteSpace(AcbNhaCungCap.Text))
        {
            var txt = AcbNhaCungCap.Text.Trim();
            supplier = _suppliers.FirstOrDefault(x =>
                string.Equals(x.MaNhaCungCap, txt, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.TenNhaCungCap, txt, StringComparison.OrdinalIgnoreCase) ||
                x.DisplayText.Contains(txt, StringComparison.OrdinalIgnoreCase));
        }

        if (supplier is null || supplier.Id == 0)
        {
            HienThiLoi("Vui lòng chọn nhà cung cấp từ danh sách.");
            AcbNhaCungCap.Focus();
            return;
        }

        // 2. Kiểm tra hàng hóa
        var dongHopLe = _items.Where(i => i.SanPhamId != 0 && i.SoLuong > 0).ToList();
        if (dongHopLe.Count == 0)
        {
            HienThiLoi("Phiếu nhập phải có ít nhất 1 mặt hàng với số lượng lớn hơn 0.");
            return;
        }

        // 3. Kiểm tra lý do chỉnh sửa (bắt buộc)
        var lyDo = TxtLyDoChinhSua.Text?.Trim();
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            HienThiLoi("Vui lòng nhập lý do chỉnh sửa phiếu nhập để lưu vết kiểm toán.");
            TxtLyDoChinhSua.Focus();
            return;
        }

        BtnLuu.IsEnabled = false;
        BtnHuy.IsEnabled = false;
        BtnLuu.Content = "Đang lưu...";

        try
        {
            _request.NhaCungCapId = supplier.Id;
            _request.TenNhaCungCap = supplier.TenNhaCungCap;
            _request.MaNhaCungCap = supplier.MaNhaCungCap;

            if (DpThoiGianNhap.SelectedDate.HasValue)
            {
                var d = DpThoiGianNhap.SelectedDate.Value.DateTime;
                _request.ThoiGianNhap = new DateTime(d.Year, d.Month, d.Day, _request.ThoiGianNhap.Hour, _request.ThoiGianNhap.Minute, _request.ThoiGianNhap.Second);
            }

            _request.SoHoaDonDauVao = TxtSoHoaDon.Text?.Trim();
            if (DpNgayHoaDon.SelectedDate.HasValue)
            {
                var ngayHoaDon = DpNgayHoaDon.SelectedDate.Value.DateTime;
                _request.NgayHoaDonDauVao = ngayHoaDon.Date;
            }
            else
            {
                _request.NgayHoaDonDauVao = null;
            }
            _request.TenNguoiGiaoHang = TxtTenNguoiGiaoHang.Text?.Trim();
            _request.SdtNguoiGiaoHang = TxtSdtNguoiGiaoHang.Text?.Trim();
            _request.GhiChu = TxtGhiChu.Text?.Trim();
            _request.PhuongThucThanhToan = CbbPhuongThuc.SelectedIndex == 1 ? "CHUYEN_KHOAN" : "TIEN_MAT";
            _request.ChiPhiKhac = NumChiPhiKhac.Value;
            _request.GiamGia = NumGiamGia.Value;
            _request.DaThanhToan = NumDaThanhToan.Value;
            _request.LyDoChinhSua = lyDo;
            _request.Items = dongHopLe;

            await _db.CapNhatPhieuNhapHangAsync(_request);

            IsSaved = true;
            Close(true);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi cap nhat phieu nhap hang", ex);
            HienThiLoi("Lỗi khi lưu phiếu nhập: " + ex.Message);
            BtnLuu.IsEnabled = true;
            BtnHuy.IsEnabled = true;
            BtnLuu.Content = "💾 Lưu thay đổi";
        }
    }

    private void BtnHuy_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void HienThiLoi(string message)
    {
        TxtError.Text = "⚠️ " + message;
        TxtError.IsVisible = true;
    }

    private void AnLoi()
    {
        TxtError.IsVisible = false;
        TxtError.Text = "";
    }
}
