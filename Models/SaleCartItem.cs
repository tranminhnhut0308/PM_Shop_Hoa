using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

public partial class SaleCartItem : ObservableObject
{
    public event Action? QuantityChanged;
    public event Action<SaleCartItem>? QuantityExceeded;

    public required SanPham SanPham { get; set; }

    private decimal _soLuong = 1;
    private decimal? _donGia;
    private string? _donViTinh;
    private decimal _giamGia;

    public decimal SoLuong
    {
        get => _soLuong;
        set
        {
            var safeValue = value <= 0 ? 1 : value;
            var biGioiHan = false;

            if (SanPham != null && safeValue > SanPham.TonKho)
            {
                safeValue = SanPham.TonKho;
                biGioiHan = true;
            }

            // Làm tròn nhẹ nếu gần như số nguyên (tránh 37.0000001)
            if (Math.Abs(safeValue - Math.Round(safeValue)) < 0.0001m)
                safeValue = Math.Round(safeValue);

            var changed = SetProperty(ref _soLuong, safeValue);

            if (biGioiHan)
            {
                QuantityExceeded?.Invoke(this);
                // Ép TextBox cập nhật dù giá trị field không đổi so với lần trước
                OnPropertyChanged(nameof(SoLuong));
            }
            else if (changed)
            {
                OnPropertyChanged(nameof(SoLuong));
            }

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
        get => _donGia ?? SanPham?.GiaBan ?? 0m;
        set
        {
            var safePrice = Math.Max(0m, value);
            if (_donGia == safePrice)
                return;

            _donGia = safePrice;
            OnPropertyChanged(nameof(DonGia));
            OnPropertyChanged(nameof(ThanhTien));
            QuantityChanged?.Invoke();
        }
    }

    public string DonViTinh
    {
        get => _donViTinh ?? (string.IsNullOrWhiteSpace(SanPham?.DonViTinh) ? "Bó" : SanPham.DonViTinh);
        set
        {
            var dvt = string.IsNullOrWhiteSpace(value) ? "Bó" : value.Trim();
            if (_donViTinh == dvt)
                return;

            _donViTinh = dvt;
            OnPropertyChanged(nameof(DonViTinh));
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
    public decimal TonConLai => Math.Max(0, (SanPham?.TonKho ?? 0m) - SoLuong);

    public void CapNhatThanhTien()
    {
        OnPropertyChanged(nameof(DonGia));
        OnPropertyChanged(nameof(DonViTinh));
        OnPropertyChanged(nameof(ThanhTien));
        OnPropertyChanged(nameof(TonConLai));
    }
}
