using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

public partial class SaleCartItem : ObservableObject
{
    public event Action? QuantityChanged;
    public event Action<SaleCartItem>? QuantityExceeded;
    public event Action<SaleCartItem>? DonViTinhChanged;

    public required SanPham SanPham { get; set; }

    private decimal _soLuong = 1;
    private decimal? _donGia;
    private string? _donViTinh;
    private ProductOption? _selectedDonViTinh;
    private decimal _giamGia;

    public decimal HeSoQuyDoi
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SanPham?.MaDvtCoBan) || SanPham.QuyDoi <= 0 || DangDungDonViCoBas)
                return 1m;

            return SanPham.QuyDoi;
        }
    }

    public bool DangDungDonViCoBas
    {
        get
        {
            var maCoBan = SanPham?.MaDvtCoBan;
            if (string.IsNullOrWhiteSpace(maCoBan))
                return true;

            if (_selectedDonViTinh is not null)
                return string.Equals(_selectedDonViTinh.Ma, maCoBan, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(_selectedDonViTinh.Name, maCoBan, StringComparison.OrdinalIgnoreCase);

            return string.Equals(_donViTinh, maCoBan, StringComparison.OrdinalIgnoreCase)
                || string.Equals(SanPham?.DonViTinh, maCoBan, StringComparison.OrdinalIgnoreCase);
        }
    }

    public decimal SoLuongQuyDoi => Math.Max(0m, SoLuong * HeSoQuyDoi);

    public decimal SoLuong
    {
        get => _soLuong;
        set
        {
            var safeValue = value <= 0 ? 1 : value;
            var biGioiHan = false;

            var heSo = HeSoQuyDoi > 0 ? HeSoQuyDoi : 1m;
            var tonTheoDonVi = SanPham is not null && SanPham.TonKho > 0
                ? SanPham.TonKho / heSo
                : decimal.MaxValue;

            if (SanPham != null && SanPham.TonKho > 0 && safeValue > tonTheoDonVi)
            {
                safeValue = tonTheoDonVi;
                biGioiHan = true;
            }

            // Làm tròn nhẹ nếu gần như số nguyên (tránh 37.0000001)
            if (Math.Abs(safeValue - Math.Round(safeValue)) < 0.0001m)
                safeValue = Math.Round(safeValue);

            var changed = SetProperty(ref _soLuong, safeValue);

            if (biGioiHan)
            {
                QuantityExceeded?.Invoke(this);
                if (!changed)
                {
                    // Ép TextBox cập nhật dù giá trị field không đổi so với lần trước
                    OnPropertyChanged(nameof(SoLuong));
                }
            }

            OnPropertyChanged(nameof(SoLuongQuyDoi));
            OnPropertyChanged(nameof(ThanhTien));
            OnPropertyChanged(nameof(TonConLai));
            QuantityChanged?.Invoke();
        }
    }

    /// <summary>
    /// Đơn giá áp dụng riêng cho dòng sản phẩm này trong giỏ hàng.
    /// Mặc định lấy theo giá bán niêm yết của sản phẩm. Khi nhân viên điều chỉnh giá
    /// trên đơn thì chỉ lưu riêng cho dòng này, KHÔNG làm ảnh hưởng các giỏ hàng khác
    /// hay danh mục gốc.
    /// </summary>
    public decimal DonGia
    {
        get
        {
            var donGiaCoSo = _donGia ?? SanPham?.GiaBan ?? 0m;
            var heSo = HeSoQuyDoi > 0 ? HeSoQuyDoi : 1m;
            return Math.Max(0m, donGiaCoSo * heSo);
        }
        set
        {
            var heSo = HeSoQuyDoi > 0 ? HeSoQuyDoi : 1m;
            var donGiaCoSo = Math.Max(0m, value) / heSo;
            if (_donGia == donGiaCoSo)
                return;

            _donGia = donGiaCoSo;
            OnPropertyChanged(nameof(DonGia));
            OnPropertyChanged(nameof(ThanhTien));
            QuantityChanged?.Invoke();
        }
    }

    public ProductOption? SelectedDonViTinh
    {
        get => _selectedDonViTinh;
        set
        {
            if (SetProperty(ref _selectedDonViTinh, value))
                DonViTinh = value?.Name ?? "";
        }
    }

    public string DonViTinh
    {
        get => _donViTinh ?? (string.IsNullOrWhiteSpace(SanPham?.DonViTinh) ? "" : SanPham.DonViTinh);
        set
        {
            var dvt = string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
            if (_donViTinh == dvt)
                return;

            _donViTinh = dvt;
            OnPropertyChanged(nameof(DonViTinh));
            CapNhatThongTinQuyDoi();
        }
    }

    public decimal GiamGia
    {
        get => _giamGia;
        set
        {
            var safeDiscount = Math.Max(0m, value);
            if (_giamGia == safeDiscount)
                return;

            _giamGia = safeDiscount;
            OnPropertyChanged(nameof(GiamGia));
            OnPropertyChanged(nameof(ThanhTien));
            QuantityChanged?.Invoke();
        }
    }

    public decimal ThanhTien => Math.Max(0m, SoLuong * DonGia - GiamGia);

    public decimal TonConLai
    {
        get
        {
            var heSo = HeSoQuyDoi > 0 ? HeSoQuyDoi : 1m;
            return Math.Max(0m, (SanPham?.TonKho ?? 0m) / heSo - SoLuong);
        }
    }

    private void CapNhatThongTinQuyDoi()
    {
        OnPropertyChanged(nameof(HeSoQuyDoi));
        OnPropertyChanged(nameof(DangDungDonViCoBas));
        OnPropertyChanged(nameof(SoLuongQuyDoi));
        OnPropertyChanged(nameof(DonGia));
        OnPropertyChanged(nameof(ThanhTien));
        OnPropertyChanged(nameof(TonConLai));
        QuantityChanged?.Invoke();
        DonViTinhChanged?.Invoke(this);
    }

    public void CapNhatThanhTien()
    {
        OnPropertyChanged(nameof(HeSoQuyDoi));
        OnPropertyChanged(nameof(DangDungDonViCoBas));
        OnPropertyChanged(nameof(SoLuongQuyDoi));
        OnPropertyChanged(nameof(DonGia));
        OnPropertyChanged(nameof(DonViTinh));
        OnPropertyChanged(nameof(ThanhTien));
        OnPropertyChanged(nameof(TonConLai));
    }

    public void DatDonViTinh(ProductOption? option, string? maDvtCoBas = null, decimal? quyDoi = null)
    {
        if (!string.IsNullOrWhiteSpace(maDvtCoBas))
            SanPham.MaDvtCoBan = maDvtCoBas.Trim();

        if (quyDoi is > 0)
            SanPham.QuyDoi = quyDoi.Value;

        if (option is not null)
        {
            SanPham.DonViTinhId = option.Id;
            SanPham.DonViTinh = option.Name;
            SanPham.TenDonViTinh = option.Name;
        }

        SelectedDonViTinh = option;
        CapNhatThanhTien();
    }

}
