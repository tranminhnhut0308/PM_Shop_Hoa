using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Một dòng hàng hóa trong phiếu nhập hàng.
/// Số lượng và đơn giá nhập sửa trực tiếp được trên lưới,
/// thành tiền tự tính lại theo từng thay đổi.
/// </summary>
public partial class ChiTietPhieuNhapHang : ObservableObject
{
    public SanPham SanPham { get; set; } = new();

    /// <summary>STT hiển thị trên lưới, được ViewModel đánh số lại sau mỗi thay đổi.</summary>
    [ObservableProperty]
    private int index;

    [ObservableProperty]
    private decimal soLuong = 1;

    /// <summary>Giá nhập - nguồn sự thật dùng để tính Thành tiền và lưu database.</summary>
    [ObservableProperty]
    private decimal donGiaNhap;

    [ObservableProperty]
    private decimal giamGia;

    public string MaSanPham => SanPham.MaSanPham;

    public string TenSanPham => SanPham.TenSanPham;

    public string? DonViTinh => SanPham.DonViTinh;

    public decimal ThanhTien => Math.Max(0, SoLuong * DonGiaNhap - GiamGia);

    public ChiTietPhieuNhapHang()
    {
    }

    public ChiTietPhieuNhapHang(SanPham product)
    {
        SanPham = product;
        DonGiaNhap = product.GiaVon;
    }

    partial void OnSoLuongChanged(decimal value)
    {
        var safeValue = value < 0 ? 0 : value;
        if (safeValue != value)
            SoLuong = safeValue;

        OnPropertyChanged(nameof(ThanhTien));
    }

    partial void OnDonGiaNhapChanged(decimal value)
    {
        if (value < 0)
            DonGiaNhap = 0;

        OnPropertyChanged(nameof(ThanhTien));
    }

    partial void OnGiamGiaChanged(decimal value)
    {
        if (value < 0)
            GiamGia = 0;

        OnPropertyChanged(nameof(ThanhTien));
    }
}
