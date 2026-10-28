namespace ShopHoa.Models;

public sealed class ThanhToanCongNoKhachHang
{
    public ulong Id { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string MaGiaoDich { get; set; } = "";
    public DateTime ThoiGian { get; set; }
    public decimal CongNoTruoc { get; set; }
    public decimal SoTien { get; set; }
    public decimal CongNoSau { get; set; }
    public string PhuongThuc { get; set; } = "";
    public string NguoiThucHien { get; set; } = "";
    public string? GhiChu { get; set; }

    public string ThoiGianText => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    public string PhuongThucHienThi => PhuongThuc.Trim().ToUpperInvariant() switch
    {
        "TIEN_MAT" => "Tiền mặt",
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Chuyển khoản",
        "THE" or "THE_TIN_DUNG" => "Thẻ",
        "VI_DIEN_TU" or "VI" => "Ví điện tử",
        _ => string.IsNullOrWhiteSpace(PhuongThuc) ? "Chưa ghi nhận" : PhuongThuc
    };

    public string CongNoTruocText => $"{CongNoTruoc:N0} đ";
    public string SoTienText => $"{SoTien:N0} đ";
    public string CongNoSauText => $"{CongNoSau:N0} đ";
    public string GhiChuHienThi => string.IsNullOrWhiteSpace(GhiChu) ? "—" : GhiChu.Trim();
}
