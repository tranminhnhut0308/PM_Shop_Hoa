using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;

namespace ShopHoa.Models;

public sealed partial class SanPham : ObservableObject
{
    public bool IsSelected { get; set; }
    public uint Id { get; set; }
    public string MaSanPham { get; set; } = "";
    public string? MaVach { get; set; }
    public string TenSanPham { get; set; } = "";
    public uint? NhomSanPhamId { get; set; }
    public uint? DonViTinhId { get; set; }
    public uint? ThuongHieuId { get; set; }
    public decimal GiaBan { get; set; }
    public decimal GiaVon { get; set; }
    public decimal TonKho { get; set; }
    public decimal TonToiThieu { get; set; }
    public decimal TonToiDa { get; set; }
    public decimal KhachDat { get; set; }
    public DateTime? NgayTao { get; set; }
    public string? DuKienHetHang { get; set; }
    public string? DonViTinh { get; set; }
    public string? NhomSanPham { get; set; }
    public string? LoaiHang { get; set; }
    public string? NhomHangBaCap { get; set; }
    public decimal TonKhoExcel { get; set; }
    public string? ViTriKho { get; set; }
    public bool DangKinhDoanh { get; set; }
    public bool DuocBanTrucTiep { get; set; }
    public string? MoTa { get; set; }
    public string? GhiChu { get; set; }

    [ObservableProperty]
    private string? hinhAnh;

    [ObservableProperty]
    private Bitmap? hinhAnhBitmap;
}
