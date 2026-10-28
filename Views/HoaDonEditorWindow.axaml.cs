using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly string _maHoaDon;
    private readonly DatabaseService _db = new();
    private HoaDonEditRequest? _editRequest;
    private ObservableCollection<HoaDonItemEditModel> _items = [];
    private List<CustomerOption> _customerOptions = [];
    private IReadOnlyList<SanPham> _availableProducts = [];
    private decimal _khachDaThanhToanGoc;
    private decimal _conNoGoc;

    public HoaDonBanHangReceipt? ResultReceipt { get; private set; }

    public HoaDonEditorWindow() : this("")
    {
    }

    public HoaDonEditorWindow(string maHoaDon)
    {
        InitializeComponent();
        _maHoaDon = maHoaDon;
        TxtMaHoaDon.Text = $"#{maHoaDon}";

        Loaded += async (_, _) => await KhoiTaoAsync();
    }

    private async Task KhoiTaoAsync()
    {
        try
        {
            // 1. Tải dữ liệu khách hàng để gợi ý
            var customers = await _db.GetKhachHangOptionsAsync();
            _customerOptions = customers ?? [];
            AcbKhachHang.ItemsSource = _customerOptions.Select(c => c.DisplayText).ToList();

            // 2. Tải danh sách sản phẩm để thêm
            _availableProducts = await _db.GetSanPhamAsync() ?? [];
            AcbSanPham.ItemsSource = _availableProducts.Select(p => $"{p.MaSanPham} - {p.TenSanPham} (Giá: {p.GiaBan:N0} đ | Tồn: {p.TonKho:N0})").ToList();

            // 3. Tải dữ liệu hóa đơn cần sửa
            _editRequest = await _db.GetHoaDonDeChinhSuaAsync(_maHoaDon);
            if (_editRequest == null)
            {
                TxtError.Text = $"Không tìm thấy hóa đơn {_maHoaDon}.";
                TxtError.IsVisible = true;
                BtnLuu.IsEnabled = false;
                return;
            }

            if (_editRequest.LaHoaDonTongHop)
            {
                TxtError.Text = "Hóa đơn tổng hợp không được chỉnh sửa. Vui lòng chỉnh sửa hóa đơn gốc trước khi tổng hợp.";
                TxtError.IsVisible = true;
                BtnLuu.IsEnabled = false;
                return;
            }

            if (!_editRequest.CoTheDoiKhachHang)
            {
                AcbKhachHang.IsEnabled = false;
                AcbKhachHang.Watermark = "Hóa đơn đã phát sinh thanh toán nên không thể đổi khách hàng.";
            }

            // Gán dữ liệu lên form
            DpThoiGianBan.SelectedDate = new DateTimeOffset(_editRequest.ThoiGianBan);
            TxtSoDienThoai.Text = _editRequest.SoDienThoai ?? "";
            TxtGhiChu.Text = _editRequest.GhiChu ?? "";

            if (_editRequest.KhachHangId.HasValue && _editRequest.KhachHangId > 0)
            {
                var match = _customerOptions.FirstOrDefault(c => c.Id == _editRequest.KhachHangId.Value);
                AcbKhachHang.Text = match != null ? match.DisplayText : _editRequest.TenKhachHang;
            }
            else
            {
                AcbKhachHang.Text = _editRequest.TenKhachHang ?? "Khách lẻ";
            }

            // Đọc số tiền đã thanh toán từ database để hiển thị
            var receipt = await _db.GetHoaDonBanHangTheoMaAsync(_maHoaDon);
            if (receipt != null)
            {
                _khachDaThanhToanGoc = receipt.KhachDaThanhToan;
                _conNoGoc = receipt.ConNo;
                TxtKhachDaTra.Text = $"{_khachDaThanhToanGoc:N0} đ";
            }

            NumGiamGia.Value = _editRequest.GiamGiaHoaDon;

            _items = new ObservableCollection<HoaDonItemEditModel>(_editRequest.Items);
            foreach (var item in _items)
            {
                item.PropertyChanged += Item_PropertyChanged;
            }

            GridItems.ItemsSource = _items;
            TinhToanLai();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("KhoiTao HoaDonEditorWindow that bai", ex);
            TxtError.Text = "Lỗi tải hóa đơn: " + ex.Message;
            TxtError.IsVisible = true;
        }
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HoaDonItemEditModel.SoLuong)
            or nameof(HoaDonItemEditModel.DonGia)
            or nameof(HoaDonItemEditModel.GiamGia)
            or nameof(HoaDonItemEditModel.ThanhTien))
        {
            TinhToanLai();
        }
    }

    private void NumGiamGia_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        TinhToanLai();
    }

    private void TinhToanLai()
    {
        if (_items == null) return;

        decimal tongTienHang = _items.Sum(x => x.ThanhTien);
        TxtTongTienHang.Text = $"{tongTienHang:N0} đ";

        decimal giamGia = NumGiamGia.Value.HasValue ? Math.Clamp(NumGiamGia.Value.Value, 0m, tongTienHang) : 0m;
        decimal khachCanTra = Math.Max(0m, tongTienHang - giamGia);
        TxtKhachCanTra.Text = $"{khachCanTra:N0} đ";

        decimal conNoMoi = Math.Max(0m, khachCanTra - _khachDaThanhToanGoc);
        TxtConNoMoi.Text = $"{conNoMoi:N0} đ";

        decimal chenhLechNo = conNoMoi - _conNoGoc;
        if (chenhLechNo > 0)
        {
            TxtChenhLechNo.Text = $"(Tăng nợ +{chenhLechNo:N0} đ)";
        }
        else if (chenhLechNo < 0)
        {
            TxtChenhLechNo.Text = $"(Giảm nợ {chenhLechNo:N0} đ)";
        }
        else
        {
            TxtChenhLechNo.Text = "(Không đổi nợ)";
        }
    }

    private void BtnThemSanPham_OnClick(object? sender, RoutedEventArgs e)
    {
        var text = AcbSanPham.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        // Tìm sản phẩm khớp
        var sp = _availableProducts.FirstOrDefault(p =>
            p.MaSanPham.Equals(text, StringComparison.OrdinalIgnoreCase) ||
            p.TenSanPham.Equals(text, StringComparison.OrdinalIgnoreCase) ||
            $"{p.MaSanPham} - {p.TenSanPham}".StartsWith(text, StringComparison.OrdinalIgnoreCase));

        if (sp == null)
        {
            // Thử tìm theo mã tách ra từ chuỗi hiển thị
            var parts = text.Split('-');
            if (parts.Length > 0)
            {
                var ma = parts[0].Trim();
                sp = _availableProducts.FirstOrDefault(p => p.MaSanPham.Equals(ma, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (sp == null)
        {
            TxtError.Text = "Không tìm thấy sản phẩm tương ứng trong danh mục.";
            TxtError.IsVisible = true;
            return;
        }

        TxtError.IsVisible = false;

        // Nếu sản phẩm đã có trong danh sách thì tăng số lượng lên 1
        var existing = _items.FirstOrDefault(x => x.SanPhamId == sp.Id);
        if (existing != null)
        {
            existing.SoLuong += 1;
        }
        else
        {
            var newItem = new HoaDonItemEditModel
            {
                Id = 0,
                SanPhamId = sp.Id,
                MaSanPham = sp.MaSanPham,
                TenSanPham = sp.TenSanPham,
                DonViTinh = sp.DonViTinh ?? "Cái",
                SoLuong = 1,
                DonGia = sp.GiaBan,
                GiaVon = sp.GiaVon,
                GiamGia = 0,
                SoLuongBanDau = 0
            };
            newItem.PropertyChanged += Item_PropertyChanged;
            _items.Add(newItem);
        }

        AcbSanPham.Text = "";
        TinhToanLai();
    }

    private void BtnXoaItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is HoaDonItemEditModel item)
        {
            item.PropertyChanged -= Item_PropertyChanged;
            _items.Remove(item);
            TinhToanLai();
        }
    }

    private async void BtnLuu_OnClick(object? sender, RoutedEventArgs e)
    {
        TxtError.IsVisible = false;

        if (_items.Count == 0)
        {
            TxtError.Text = "Hóa đơn phải có ít nhất 1 sản phẩm.";
            TxtError.IsVisible = true;
            return;
        }

        var lyDo = TxtLyDoChinhSua.Text?.Trim();
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            TxtError.Text = "Vui lòng nhập lý do chỉnh sửa hóa đơn (bắt buộc).";
            TxtError.IsVisible = true;
            TxtLyDoChinhSua.Focus();
            return;
        }

        // Tìm khách hàng nếu có chọn
        uint? selectedKhachHangId = null;
        var textKhach = AcbKhachHang.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(textKhach) && !textKhach.Equals("Khách lẻ", StringComparison.OrdinalIgnoreCase))
        {
            var match = _customerOptions.FirstOrDefault(c =>
                c.DisplayText.Equals(textKhach, StringComparison.OrdinalIgnoreCase) ||
                c.TenKhachHang.Equals(textKhach, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                selectedKhachHangId = match.Id;
            }
            else
            {
                // Giữ lại id cũ nếu tên khớp
                if (_editRequest?.KhachHangId != null && _editRequest.TenKhachHang != null &&
                    _editRequest.TenKhachHang.Equals(textKhach, StringComparison.OrdinalIgnoreCase))
                {
                    selectedKhachHangId = _editRequest.KhachHangId;
                }
            }
        }

        // Tính công nợ dự kiến
        decimal tongTienHang = _items.Sum(x => x.ThanhTien);
        decimal giamGia = NumGiamGia.Value.HasValue ? Math.Clamp(NumGiamGia.Value.Value, 0m, tongTienHang) : 0m;
        decimal khachCanTra = Math.Max(0m, tongTienHang - giamGia);
        decimal conNoMoi = Math.Max(0m, khachCanTra - _khachDaThanhToanGoc);

        if (conNoMoi > 0 && selectedKhachHangId == null)
        {
            TxtError.Text = $"Hóa đơn còn nợ ({conNoMoi:N0} đ). Khách lẻ không được phép ghi nợ! Vui lòng chọn khách hàng có trong danh bạ.";
            TxtError.IsVisible = true;
            return;
        }

        var thoiGianBan = DpThoiGianBan.SelectedDate?.DateTime ?? DateTime.Now;

        var request = new HoaDonEditRequest
        {
            HoaDonId = _editRequest!.HoaDonId,
            MaHoaDon = _maHoaDon,
            PhienBan = _editRequest.PhienBan,
            ThoiGianBan = thoiGianBan,
            KhachHangId = selectedKhachHangId,
            TenKhachHang = textKhach,
            SoDienThoai = TxtSoDienThoai.Text?.Trim(),
            DiaChi = _editRequest.DiaChi,
            GiamGiaHoaDon = giamGia,
            GhiChu = TxtGhiChu.Text?.Trim(),
            LyDoChinhSua = lyDo,
            Items = _items.ToList()
        };

        BtnLuu.IsEnabled = false;

        try
        {
            ResultReceipt = await _db.LuuChinhSuaHoaDonAsync(request);
            Close(ResultReceipt);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("LuuChinhSuaHoaDonAsync that bai", ex);
            TxtError.Text = "Lỗi khi lưu: " + ex.Message;
            TxtError.IsVisible = true;
            BtnLuu.IsEnabled = true;
        }
    }

    private void BtnHuy_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
