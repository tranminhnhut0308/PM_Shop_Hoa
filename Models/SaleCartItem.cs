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

    public decimal ThanhTien => SoLuong * SanPham.GiaBan;
    public decimal TonConLai => Math.Max(0, SanPham.TonKho - SoLuong);

    public void CapNhatThanhTien() => OnPropertyChanged(nameof(ThanhTien));
}