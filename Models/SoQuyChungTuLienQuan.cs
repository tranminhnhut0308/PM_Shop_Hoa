namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một chứng từ liên quan (Hóa đơn bán hàng, Phiếu nhập hàng...)
/// được liên kết với phiếu thu / phiếu chi trong Sổ quỹ.
/// </summary>
public class SoQuyChungTuLienQuan
{
    /// <summary>Ví dụ: "Hóa đơn bán hàng", "Phiếu nhập hàng", "Thanh toán công nợ"</summary>
    public string LoaiChungTu { get; set; } = "";

    /// <summary>Mã chứng từ: HD..., PN...</summary>
    public string MaChungTu { get; set; } = "";

    /// <summary>Tên khách hàng hoặc Nhà cung cấp</summary>
    public string DoiTac { get; set; } = "";

    /// <summary>Số tiền liên quan hoặc số tiền phân bổ cho chứng từ này</summary>
    public decimal SoTienLienQuan { get; set; }

    public string SoTienLienQuanHienThi => $"{SoTienLienQuan:N0} đ";

    /// <summary>Tổng giá trị của chứng từ gốc (tổng hóa đơn, tổng phiếu nhập)</summary>
    public decimal TongTienChungTu { get; set; }

    public string TongTienChungTuHienThi => $"{TongTienChungTu:N0} đ";

    public DateTime? NgayChungTu { get; set; }

    public string NgayChungTuHienThi => NgayChungTu?.ToString("dd/MM/yyyy HH:mm") ?? "";

    /// <summary>Trạng thái chứng từ trong database: HOAN_THANH, HUY, CONG_NO...</summary>
    public string TrangThaiChungTu { get; set; } = "";

    public string TrangThaiChungTuHienThi => TrangThaiChungTu switch
    {
        "HUY" or "DA_HUY" => "Đã hủy",
        "HOAN_THANH" => "Hoàn thành",
        "CONG_NO" => "Còn nợ",
        "DA_THANH_TOAN" => "Đã thanh toán",
        _ => string.IsNullOrWhiteSpace(TrangThaiChungTu) ? "Bình thường" : TrangThaiChungTu
    };

    public bool DaHuy => TrangThaiChungTu.Equals("HUY", StringComparison.OrdinalIgnoreCase)
                         || TrangThaiChungTu.Equals("DA_HUY", StringComparison.OrdinalIgnoreCase);

    public string MauTrangThai => DaHuy ? "#D92D20" : "#087F6E";

    public string MauNenTrangThai => DaHuy ? "#FEF3F2" : "#E8F6F1";

    public bool LaHoaDon { get; set; }

    public bool LaPhieuNhap { get; set; }

    /// <summary>Nhãn nút bấm: "Xem hóa đơn" hoặc "Xem phiếu nhập"</summary>
    public string NhanNutXem => LaHoaDon ? "Xem hóa đơn" : (LaPhieuNhap ? "Xem phiếu nhập" : "Xem chi tiết");
}
