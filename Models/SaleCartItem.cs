using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

public partial class SaleCartItem : ObservableObject
{
    public event Action? QuantityChanged;
    public event Action<SaleCartItem>? QuantityExceeded;

    public required SanPham SanPham { get; set; }

    private decimal _soLuong = 1;

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

    public decimal DonGia
    {
        get => SanPham?.GiaBan ?? 0m;
        set
        {
            if (SanPham is null)
                return;

            var safePrice = Math.Max(0m, value);
            if (SanPham.GiaBan == safePrice)
                return;

            SanPham.GiaBan = safePrice;
            OnPropertyChanged(nameof(DonGia));
            OnPropertyChanged(nameof(ThanhTien));
            QuantityChanged?.Invoke();
        }
    }

    public string DonViTinh
    {
        get => string.IsNullOrWhiteSpace(SanPham?.DonViTinh) ? "Bó" : SanPham.DonViTinh;
        set
        {
            if (SanPham is null)
                return;

            var dvt = string.IsNullOrWhiteSpace(value) ? "Bó" : value.Trim();
            if (SanPham.DonViTinh == dvt)
                return;

            SanPham.DonViTinh = dvt;
            OnPropertyChanged(nameof(DonViTinh));
        }
    }

    public decimal ThanhTien => SoLuong * (SanPham?.GiaBan ?? 0m);
    public decimal TonConLai => Math.Max(0, (SanPham?.TonKho ?? 0m) - SoLuong);

    public void CapNhatThanhTien()
    {
        OnPropertyChanged(nameof(DonGia));
        OnPropertyChanged(nameof(DonViTinh));
        OnPropertyChanged(nameof(ThanhTien));
    }
}
