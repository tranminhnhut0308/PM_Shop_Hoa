using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

public sealed partial class HoaDonItemEditModel : ObservableObject
{
    public uint Id { get; set; } // id trong chi_tiet_hoa_don_ban_hang nếu là dòng cũ, 0 nếu là thêm mới
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public decimal GiaVon { get; set; }

    [ObservableProperty]
    private string _donViTinh = "";

    [ObservableProperty]
    private decimal _soLuong;

    [ObservableProperty]
    private decimal _donGia;

    [ObservableProperty]
    private decimal _giamGia;

    [ObservableProperty]
    private string? _ghiChu;

    public decimal ThanhTien => Math.Max(0m, (SoLuong * DonGia) - GiamGia);

    // Lưu lại số lượng gốc ban đầu lúc mở hóa đơn để dễ tính delta
    public decimal SoLuongBanDau { get; set; }

    partial void OnSoLuongChanged(decimal value)
    {
        OnPropertyChanged(nameof(ThanhTien));
    }

    partial void OnDonGiaChanged(decimal value)
    {
        OnPropertyChanged(nameof(ThanhTien));
    }

    partial void OnGiamGiaChanged(decimal value)
    {
        OnPropertyChanged(nameof(ThanhTien));
    }
}

public sealed class HoaDonEditRequest
{
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = "";
    public string PhienBan { get; set; } = "";
    public bool LaHoaDonTongHop { get; set; }
    public DateTime ThoiGianBan { get; set; }
    public uint? KhachHangId { get; set; }
    public string? TenKhachHang { get; set; }
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public decimal GiamGiaHoaDon { get; set; }
    public string? GhiChu { get; set; }
    public string LyDoChinhSua { get; set; } = "";
    public decimal KhachDaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public int SoLanThanhToan { get; set; }
    public int CongNoDaGhiNhan { get; set; }
    public List<HoaDonItemEditModel> Items { get; set; } = [];

    public bool CoTheDoiKhachHang => SoLanThanhToan == 0 && KhachDaThanhToan <= 0;
}

public sealed class CustomerOption
{
    public uint Id { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string? SoDienThoai { get; set; }

    public string DisplayText =>
        string.IsNullOrWhiteSpace(SoDienThoai)
            ? $"{TenKhachHang} ({MaKhachHang})"
            : $"{TenKhachHang} ({MaKhachHang}) - {SoDienThoai}";
}
