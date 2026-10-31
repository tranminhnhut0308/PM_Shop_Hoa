namespace ShopHoa.Models;

using System.Globalization;

public sealed class HoaDon
{
    public uint Id { get; set; }

    public string MaHoaDon { get; set; } = "";

    public DateTime ThoiGianBan { get; set; }

    public string? MaKhachHang { get; set; }

    public string? TenKhachHang { get; set; }

    public decimal TongTienHang { get; set; }

    public decimal GiamGia { get; set; }

    public decimal ThuKhac { get; set; }

    public decimal KhachCanTra { get; set; }

    public decimal KhachDaThanhToan { get; set; }

    public decimal ConNo { get; set; }

    public string TrangThaiThanhToan { get; set; } = "";

    public string TrangThaiHoaDon { get; set; } = "";

    public bool LaHoaDonTongHop => string.Equals(
        TrangThaiHoaDon,
        "TONG_HOP",
        StringComparison.OrdinalIgnoreCase);

    public bool LaHoaDonDaHuy =>
        string.Equals(TrangThaiHoaDon, "DA_HUY", StringComparison.OrdinalIgnoreCase)
        || string.Equals(TrangThaiHoaDon, "HUY", StringComparison.OrdinalIgnoreCase);

    /// <summary>Ghi chú riêng của hóa đơn (yêu cầu riêng của khách...).</summary>
    public string? GhiChu { get; set; }

    // ============================================================
    // THÔNG TIN HỦY HÓA ĐƠN & TRUY VẾT HÓA ĐƠN THAY THẾ
    // ============================================================

    public string? LyDoHuy { get; set; }
    public uint? NguoiHuyId { get; set; }
    public string? TenNguoiHuy { get; set; }
    public DateTime? ThoiGianHuy { get; set; }
    public string? TrangThaiGiaoHangKhiHuy { get; set; }
    public bool DaHoanKho { get; set; }
    public string? PhuongAnTienKhiHuy { get; set; }
    public decimal SoTienHoan { get; set; }
    public string? MaPhieuChiHoan { get; set; }
    public uint? HoaDonGocId { get; set; }
    public string? MaHoaDonGoc { get; set; }
    public uint? HoaDonThayTheId { get; set; }
    public string? MaHoaDonThayThe { get; set; }

    // ============================================================
    // THÔNG TIN CHỈ DÙNG CHO GIAO DIỆN
    // ============================================================

    /// <summary>Định dạng dd/MM/yyyy HH:mm cho cột "Thời gian".</summary>
    public string ThoiGianBanText =>
        ThoiGianBan.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Hóa đơn này còn dư nợ và chưa bị hủy (dùng để hiện nút Thu nợ / tô màu cảnh báo).</summary>
    public bool ConNoDuong => ConNo > 0 && !LaHoaDonDaHuy;

    /// <summary>Hóa đơn có thể hủy (chưa bị hủy).</summary>
    public bool CoTheHuy => !LaHoaDonDaHuy;

    /// <summary>Có ghi chú để hiển thị kèm hay không.</summary>
    public bool CoGhiChu => !string.IsNullOrWhiteSpace(GhiChu);

    public bool CoLyDoHuy => LaHoaDonDaHuy && !string.IsNullOrWhiteSpace(LyDoHuy);

    public bool CoHoaDonThayThe => !string.IsNullOrWhiteSpace(MaHoaDonThayThe);

    public bool CoHoaDonGoc => !string.IsNullOrWhiteSpace(MaHoaDonGoc);

    public string ThongTinHuyTomTat
    {
        get
        {
            if (!LaHoaDonDaHuy)
                return string.Empty;

            var nguoi = string.IsNullOrWhiteSpace(TenNguoiHuy) ? "Hệ thống" : TenNguoiHuy;
            var thoiGian = ThoiGianHuy.HasValue
                ? ThoiGianHuy.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : "";
            var lyDo = string.IsNullOrWhiteSpace(LyDoHuy) ? "Không rõ" : LyDoHuy;
            var thayThe = CoHoaDonThayThe ? $" → HĐ thay thế: {MaHoaDonThayThe}" : "";

            return string.IsNullOrWhiteSpace(thoiGian)
                ? $"Đã hủy ({nguoi}): {lyDo}{thayThe}"
                : $"Đã hủy lúc {thoiGian} bởi {nguoi} — Lý do: {lyDo}{thayThe}";
        }
    }

    public string TrangThaiHienThi
    {
        get
        {
            if (LaHoaDonDaHuy)
                return "Đã hủy";
            if (LaHoaDonTongHop)
                return "Tổng hợp";
            if (ConNo <= 0)
                return "Hoàn thành";
            if (KhachDaThanhToan > 0)
                return "TT một phần";
            return "Chưa thanh toán";
        }
    }

    public string TrangThaiBadgeBackground
    {
        get
        {
            if (LaHoaDonDaHuy)
                return "#FEE2E2";
            if (ConNo <= 0)
                return "#DCFCE7";
            if (KhachDaThanhToan > 0)
                return "#FEF3C7";
            return "#FFEDD5";
        }
    }

    public string TrangThaiBadgeForeground
    {
        get
        {
            if (LaHoaDonDaHuy)
                return "#B91C1C";
            if (ConNo <= 0)
                return "#15803D";
            if (KhachDaThanhToan > 0)
                return "#B45309";
            return "#C2410C";
        }
    }
}
