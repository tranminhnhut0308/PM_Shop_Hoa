using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Thẻ thống kê tổng hợp của màn "Báo cáo nhà cung cấp" trong khoảng thời gian đang xem.
/// Mọi con số tính trực tiếp trong SQL từ phiếu nhập hàng và công nợ nhà cung cấp.
/// </summary>
public sealed class BaoCaoNhaCungCapTongHop
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }

    /// <summary>Tổng số nhà cung cấp có phát sinh giao dịch nhập hàng trong kỳ.</summary>
    public int TongNhaCungCap { get; set; }

    /// <summary>Tổng số phiếu nhập hàng trong kỳ.</summary>
    public int SoPhieuNhap { get; set; }

    /// <summary>Tổng tiền nhập hàng trong kỳ (trước khi trừ trả hàng).</summary>
    public decimal TongTienNhap { get; set; }

    /// <summary>Tổng tiền hàng đã trả lại nhà cung cấp trong kỳ.</summary>
    public decimal TienTraHang { get; set; }

    /// <summary>Giá trị nhập hàng thực tế: Tổng tiền nhập - Tiền trả hàng.</summary>
    public decimal GiaTriNhapHang => TongTienNhap - TienTraHang;

    /// <summary>Tổng số tiền đã thanh toán cho nhà cung cấp trong kỳ.</summary>
    public decimal DaThanhToan { get; set; }

    /// <summary>Tổng công nợ hiện tại với các nhà cung cấp.</summary>
    public decimal CongNo { get; set; }

    public string TongNhaCungCapHienThi =>
        TongNhaCungCap.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string SoPhieuNhapHienThi =>
        SoPhieuNhap.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string TongTienNhapHienThi =>
        TongTienNhap.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string TienTraHangHienThi =>
        TienTraHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string GiaTriNhapHangHienThi =>
        GiaTriNhapHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string DaThanhToanHienThi =>
        DaThanhToan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string CongNoHienThi =>
        CongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string KhoangHienThi =>
        $"Khoảng thời gian: {TuNgay:dd/MM/yyyy} - {DenNgay:dd/MM/yyyy}";
}

/// <summary>
/// Một dòng trong biểu đồ "Top 10 nhà cung cấp nhập hàng nhiều nhất (đã trừ trả hàng)".
/// </summary>
public sealed partial class BaoCaoNhaCungCapTop : ObservableObject
{
    public int Stt { get; set; }
    public uint Id { get; set; }
    public string MaNhaCungCap { get; set; } = string.Empty;
    public string TenNhaCungCap { get; set; } = string.Empty;
    public string SoDienThoai { get; set; } = string.Empty;

    /// <summary>Số phiếu nhập trong kỳ.</summary>
    public int SoPhieuNhap { get; set; }

    /// <summary>Tổng tiền nhập hàng trong kỳ.</summary>
    public decimal TongTienNhap { get; set; }

    /// <summary>Giá trị hàng đã trả lại nhà cung cấp trong kỳ.</summary>
    public decimal TienTraHang { get; set; }

    /// <summary>Giá trị nhập hàng thực tế (đã trừ trả hàng).</summary>
    public decimal GiaTriNhapHang { get; set; }

    /// <summary>Đã thanh toán.</summary>
    public decimal DaThanhToan { get; set; }

    /// <summary>Công nợ hiện tại.</summary>
    public decimal CongNo { get; set; }

    /// <summary>Tỷ lệ độ dài thanh biểu đồ (0..100) so với mốc lớn nhất của trục X.</summary>
    [ObservableProperty]
    private double _tyLeBar;

    /// <summary>Bề rộng thanh biểu đồ tính theo pixel để XAML bind trực tiếp.</summary>
    [ObservableProperty]
    private double _rongBar;

    public string GiaTriHienThi =>
        GiaTriNhapHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    /// <summary>Nội dung chi tiết khi rê chuột vào thanh biểu đồ.</summary>
    public string TooltipHienThi =>
        $"{TenNhaCungCap} ({MaNhaCungCap})\n" +
        $"• Số phiếu nhập: {SoPhieuNhap:N0}\n" +
        $"• Tổng tiền nhập: {TongTienNhap:N0} đ\n" +
        $"• Tiền trả hàng: {TienTraHang:N0} đ\n" +
        $"• Thực nhập (đã trừ trả): {GiaTriNhapHang:N0} đ\n" +
        $"• Đã thanh toán: {DaThanhToan:N0} đ\n" +
        $"• Công nợ hiện tại: {CongNo:N0} đ";
}

/// <summary>
/// Một dòng trong bảng dữ liệu báo cáo chi tiết theo nhà cung cấp.
/// </summary>
public sealed class BaoCaoNhaCungCapDong
{
    public int Stt { get; set; }
    public uint Id { get; set; }
    public string MaNhaCungCap { get; set; } = string.Empty;
    public string TenNhaCungCap { get; set; } = string.Empty;
    public string SoDienThoai { get; set; } = string.Empty;

    public int SoPhieuNhap { get; set; }
    public decimal TongTienNhap { get; set; }
    public decimal TienTraHang { get; set; }
    public decimal GiaTriNhapHang { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal CongNo { get; set; }

    public string SoPhieuNhapHienThi =>
        SoPhieuNhap.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string TongTienNhapHienThi =>
        TongTienNhap.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string TienTraHangHienThi =>
        TienTraHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string GiaTriNhapHangHienThi =>
        GiaTriNhapHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string DaThanhToanHienThi =>
        DaThanhToan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string CongNoHienThi =>
        CongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public bool HasCongNo => CongNo > 0;
}

/// <summary>
/// Mốc chia trên trục hoành X của biểu đồ (ví dụ: 0, 3 tr, 6 tr, 9 tr...).
/// </summary>
public sealed class BaoCaoNhaCungCapScaleMark
{
    public decimal Value { get; set; }
    public string Label { get; set; } = string.Empty;
    public double PositionPercent { get; set; }
}

/// <summary>
/// Dữ liệu tổng hợp hoàn chỉnh trả về từ service cho màn hình Báo cáo nhà cung cấp.
/// </summary>
public sealed class BaoCaoNhaCungCapDuLieu
{
    public BaoCaoNhaCungCapTongHop TongHop { get; set; } = new();
    public List<BaoCaoNhaCungCapTop> Top { get; set; } = new();
    public List<BaoCaoNhaCungCapDong> DongTrang { get; set; } = new();
    public List<BaoCaoNhaCungCapScaleMark> ScaleMarks { get; set; } = new();
    public string[] ScaleLabels { get; set; } = new string[11];
    public int TongSoBanGhi { get; set; }
    public decimal MaxScaleValue { get; set; }
}
