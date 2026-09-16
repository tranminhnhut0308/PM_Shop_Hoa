using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

public partial class SaleCartItem : ObservableObject
{
    public event Action? QuantityChanged;

    public required SanPham SanPham { get; set; }

    [ObservableProperty]
    private decimal soLuong = 1;

    public decimal ThanhTien => SoLuong * SanPham.GiaBan;

    partial void OnSoLuongChanged(decimal value)
    {
        var safeValue = value <= 0 ? 1 : value;
        if (safeValue != value)
            SoLuong = safeValue;

        OnPropertyChanged(nameof(ThanhTien));
        QuantityChanged?.Invoke();
    }
}
