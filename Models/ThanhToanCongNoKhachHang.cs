namespace ShopHoa.Models;

public sealed class PhanBoThanhToanKhachHang
{
    public string MaHoaDon { get; set; } = "";
    public decimal SoTien { get; set; }
}

public sealed class ThanhToanCongNoKhachHang
{
    public ulong Id { get; set; }
    public uint KhachHangId { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string MaGiaoDich { get; set; } = "";
    public DateTime ThoiGian { get; set; }
    public decimal CongNoTruoc { get; set; }
    public decimal SoTien { get; set; }
    public decimal CongNoSau { get; set; }
    public string PhuongThuc { get; set; } = "";
    public string? MaThamChieu { get; set; }
    public string NguoiThucHien { get; set; } = "";
    public string? GhiChu { get; set; }

    public string TrangThai { get; set; } = "HOAN_THANH";
    public bool DaHuy => TrangThai.Equals("HUY", StringComparison.OrdinalIgnoreCase);
    public string TrangThaiHienThi => DaHuy ? "Đã hủy" : "Hoàn thành";
    public bool CoTheHuy => !DaHuy;
    public string ToolTipHuy => DaHuy ? "Phiếu này đã bị hủy, không thể hủy lại." : "Hủy phiếu thanh toán này và hoàn tác công nợ";
    public double OpacityNutHuy => DaHuy ? 0.35 : 1.0;
    public int SoThuTuHienThi { get; set; }

    public string? NguoiHuy { get; set; }
    public DateTime? ThoiGianHuy { get; set; }
    public string ThoiGianHuyText => ThoiGianHuy?.ToString("dd/MM/yyyy HH:mm") ?? "—";
    public string? LyDoHuy { get; set; }

    public string ThoiGianText => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    public string PhuongThucHienThi => PhuongThuc.Trim().ToUpperInvariant() switch
    {
        "TIEN_MAT" => "Tiền mặt",
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Chuyển khoản",
        "THE" or "THE_TIN_DUNG" => "Thẻ",
        "VI_DIEN_TU" or "VI" => "Ví điện tử",
        _ => string.IsNullOrWhiteSpace(PhuongThuc) ? "Chưa ghi nhận" : PhuongThuc
    };

    public string MaThamChieuHienThi => string.IsNullOrWhiteSpace(MaThamChieu) ? "—" : MaThamChieu.Trim();
    public string NoiDungThanhToan =>
        string.IsNullOrWhiteSpace(GhiChu) ? "Thu công nợ khách hàng" : GhiChu.Trim();
    public string CongNoTruocText => $"{CongNoTruoc:N0} đ";
    public string SoTienText => $"{SoTien:N0} đ";
    public string CongNoSauText => $"{CongNoSau:N0} đ";
    public string GhiChuHienThi => string.IsNullOrWhiteSpace(GhiChu) ? "—" : GhiChu.Trim();
}
