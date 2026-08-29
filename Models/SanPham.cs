namespace ShopHoa.Models;

public sealed class SanPham
{
    public uint Id { get; set; }
    public string MaSanPham { get; set; } = "";
    public string? MaVach { get; set; }
    public string TenSanPham { get; set; } = "";
    public decimal GiaBan { get; set; }
    public decimal GiaVon { get; set; }
    public decimal TonKho { get; set; }
    public decimal TonToiThieu { get; set; }
    public decimal TonToiDa { get; set; }
    public string? DonViTinh { get; set; }
    public string? NhomSanPham { get; set; }
}
