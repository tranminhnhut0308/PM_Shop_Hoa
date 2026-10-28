namespace ShopHoa.Models;

public sealed class ThanhToanCongNoNhaCungCapYeuCau
{
    public decimal SoTien { get; init; }
    public string PhuongThuc { get; init; } = "TIEN_MAT";
    public string? MaThamChieu { get; init; }
    public string? GhiChu { get; init; }
    public DateTime ThoiGian { get; init; }
}

public sealed class ThanhToanCongNoNhaCungCapKetQua
{
    public uint NhaCungCapId { get; init; }
    public string MaNhaCungCap { get; init; } = "";
    public string TenNhaCungCap { get; init; } = "";
    public string MaGiaoDich { get; init; } = "";
    public decimal SoTienThanhToan { get; init; }
    public decimal CongNoTruoc { get; init; }
    public decimal CongNoSau { get; init; }
    public int SoPhieuNhapDuocPhanBo { get; init; }
    public IReadOnlyList<PhanBoThanhToanNhaCungCap> PhanBo { get; init; } = [];
}

public sealed class PhanBoThanhToanNhaCungCap
{
    public string MaPhieuNhap { get; set; } = "";
    public decimal SoTien { get; set; }
}

public sealed class ThanhToanCongNoNhaCungCap
{
    public ulong Id { get; set; }
    public uint NhaCungCapId { get; set; }
    public string MaNhaCungCap { get; set; } = "";
    public string TenNhaCungCap { get; set; } = "";
    public string DonViNhan { get; set; } = "";
    public string MaGiaoDich { get; set; } = "";
    public DateTime ThoiGian { get; set; }
    public decimal CongNoTruoc { get; set; }
    public decimal SoTien { get; set; }
    public decimal CongNoSau { get; set; }
    public string PhuongThuc { get; set; } = "";
    public string? MaThamChieu { get; set; }
    public string NguoiThucHien { get; set; } = "";
    public string? GhiChu { get; set; }

    public string ThoiGianText => ThoiGian.ToString("dd/MM/yyyy HH:mm");
    public string CongNoTruocText => $"{CongNoTruoc:N0} đ";
    public string SoTienText => $"{SoTien:N0} đ";
    public string CongNoSauText => $"{CongNoSau:N0} đ";
    public string PhuongThucHienThi => PhuongThuc.Trim().ToUpperInvariant() switch
    {
        "TIEN_MAT" => "Tiền mặt",
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Chuyển khoản",
        "THE" or "THE_TIN_DUNG" => "Thẻ",
        "VI_DIEN_TU" or "VI" => "Ví điện tử",
        _ => string.IsNullOrWhiteSpace(PhuongThuc) ? "Chưa ghi nhận" : PhuongThuc
    };
    public string MaThamChieuHienThi => string.IsNullOrWhiteSpace(MaThamChieu) ? "—" : MaThamChieu.Trim();
    public string GhiChuHienThi => string.IsNullOrWhiteSpace(GhiChu) ? "—" : GhiChu.Trim();
}

public sealed class NhaCungCapThanhToanLuaChon
{
    public uint? Id { get; init; }
    public string TenHienThi { get; init; } = "";
}
