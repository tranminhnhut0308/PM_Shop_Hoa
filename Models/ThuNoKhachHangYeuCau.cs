namespace ShopHoa.Models;

public sealed class ThuNoKhachHangYeuCau
{
    public decimal SoTien { get; init; }
    public string PhuongThuc { get; init; } = "TIEN_MAT";
    public string? GhiChu { get; init; }
    public DateTime ThoiGian { get; init; }
    public uint? NguoiThucHienId { get; init; }
}
