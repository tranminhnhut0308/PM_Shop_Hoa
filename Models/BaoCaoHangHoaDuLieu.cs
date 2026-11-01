using System;
using System.Collections.Generic;
using System.Globalization;
using ShopHoa.Services;

namespace ShopHoa.Models;

/// <summary>Dòng tổng hợp của báo cáo hàng hóa trong khoảng thời gian đang xem (Issue #48 - Nhóm I).</summary>
public sealed class BaoCaoHangHoaTongHop
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }

    /// <summary>Tổng số mặt hàng trong danh mục hệ thống.</summary>
    public int TongSoMatHang { get; set; }

    /// <summary>Số mặt hàng đang kinh doanh.</summary>
    public int SoMatHangDangKinhDoanh { get; set; }

    /// <summary>Số mặt hàng đã ngừng kinh doanh.</summary>
    public int SoMatHangNgungKinhDoanh { get; set; }

    /// <summary>Số mặt hàng có phát sinh bán trong kỳ.</summary>
    public int SoMaHang { get; set; }

    /// <summary>Tổng số lượng hàng đã bán trong kỳ.</summary>
    public decimal SoLuongBan { get; set; }

    /// <summary>Tổng doanh thu bán hàng (thực tế từ hóa đơn hoàn thành).</summary>
    public decimal DoanhThu { get; set; }

    /// <summary>Tổng giảm giá hóa đơn trong kỳ.</summary>
    public decimal TongGiamGia { get; set; }

    /// <summary>Tổng giá vốn = SUM(gia_von đơn vị × số lượng).</summary>
    public decimal GiaVon { get; set; }

    /// <summary>Lợi nhuận gộp = DoanhThu - GiaVon.</summary>
    public decimal LoiNhuan => DoanhThu - GiaVon;

    /// <summary>Tỷ suất lợi nhuận gộp trên doanh thu (0..100%).</summary>
    public double TyLeLoiNhuan => DoanhThu <= 0 ? 0 : (double)(LoiNhuan / DoanhThu * 100m);

    /// <summary>Tổng số lượng tồn kho hiện tại.</summary>
    public decimal TongSoLuongTon { get; set; }

    /// <summary>Giá trị tồn kho hiện tại = SUM(ton_kho × gia_von).</summary>
    public decimal GiaTriTonKhoHienTai { get; set; }

    /// <summary>Số mặt hàng hết hàng (tồn kho <= 0).</summary>
    public int SoMatHangHetHang { get; set; }

    /// <summary>Số mặt hàng sắp hết hàng (tồn > 0 và <= tồn tối thiểu).</summary>
    public int SoMatHangSapHetHang { get; set; }

    /// <summary>Số mặt hàng tồn kho nhiều (tồn >= tồn tối đa).</summary>
    public int SoMatHangTonKhoNhieu { get; set; }

    public string TongSoMatHangHienThi => TongSoMatHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoMatHangDangKinhDoanhHienThi => SoMatHangDangKinhDoanh.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoMatHangNgungKinhDoanhHienThi => SoMatHangNgungKinhDoanh.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoMaHangHienThi => SoMaHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoLuongBanHienThi => SoLuongBan.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TongGiamGiaHienThi => TongGiamGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string GiaVonHienThi => GiaVon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string LoiNhuanHienThi => LoiNhuan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyLeLoiNhuanHienThi => TyLeLoiNhuan.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) + "%";
    public string TongSoLuongTonHienThi => TongSoLuongTon.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string GiaTriTonKhoHienThi => GiaTriTonKhoHienTai.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string SoMatHangHetHangHienThi => SoMatHangHetHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoMatHangSapHetHangHienThi => SoMatHangSapHetHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoMatHangTonKhoNhieuHienThi => SoMatHangTonKhoNhieu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string TenFileXuat() => ReportExportService.TaoTenFile("BaoCaoHangHoa");
}

/// <summary>Dòng top 10 sản phẩm (Issue #48 - Nhóm II & V).</summary>
public sealed class BaoCaoHangHoaTop
{
    public int Stt { get; set; }
    public string MaHang { get; set; } = string.Empty;
    public string TenHang { get; set; } = string.Empty;
    public string DonViTinh { get; set; } = string.Empty;
    public decimal SoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal GiaVon { get; set; }
    public decimal LoiNhuan => DoanhThu - GiaVon;
    public double TyLeLoiNhuan => DoanhThu <= 0 ? 0 : (double)(LoiNhuan / DoanhThu * 100m);

    /// <summary>Chiều dài thanh so với dòng cao nhất (0..100).</summary>
    public double TyLeBar { get; set; }

    /// <summary>Bề rộng tối đa của thanh (px) — XAML lấy hằng này làm mốc.</summary>
    public const double BeRongToiDa = 260;

    /// <summary>Bề rộng thanh (px) để XAML bind thẳng, không cần converter.</summary>
    public double RongBar => TyLeBar <= 0 ? 0 : Math.Max(6, TyLeBar / 100d * BeRongToiDa);

    public string SoLuongBanHienThi => SoLuongBan.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string GiaVonHienThi => GiaVon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string LoiNhuanHienThi => LoiNhuan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyLeBarHienThi => TyLeBar.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) + "%";
}

/// <summary>Dòng doanh thu theo nhóm hàng (Issue #48 - Nhóm III).</summary>
public sealed class BaoCaoHangHoaNhom
{
    public int Stt { get; set; }
    public string TenNhom { get; set; } = string.Empty;
    public int SoMaHang { get; set; }
    public decimal SoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }

    /// <summary>Độ lớn so với nhóm cao nhất (0..100) — dùng cho thanh bar.</summary>
    public double TyLeBar { get; set; }

    /// <summary>Tỉ trọng doanh thu của nhóm này trong tổng kỳ (0..100).</summary>
    public double TyLePhanTram { get; set; }

    public const double BeRongToiDa = 260;

    public double RongBar => TyLeBar <= 0 ? 0 : Math.Max(6, TyLeBar / 100d * BeRongToiDa);

    public string SoMaHangHienThi => SoMaHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoLuongBanHienThi => SoLuongBan.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyLePhanTramHienThi => TyLePhanTram.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) + "%";
}

/// <summary>Dòng thống kê tồn kho hàng hóa (Issue #48 - Nhóm IV).</summary>
public sealed class BaoCaoHangHoaTonKho
{
    public int Stt { get; set; }
    public string MaHang { get; set; } = string.Empty;
    public string TenHang { get; set; } = string.Empty;
    public string DonViTinh { get; set; } = string.Empty;
    public string NhomHang { get; set; } = string.Empty;
    public decimal TonKho { get; set; }
    public decimal TonToiThieu { get; set; }
    public decimal TonToiDa { get; set; }
    public decimal GiaVon { get; set; }
    public decimal GiaTriTon => TonKho * GiaVon;

    public string TrangThaiTon => TonKho <= 0
        ? "Hết hàng"
        : (TonToiThieu > 0 && TonKho <= TonToiThieu)
            ? "Sắp hết hàng"
            : (TonToiDa > 0 && TonKho >= TonToiDa)
                ? "Tồn nhiều"
                : "Bình thường";

    public string MauTrangThai => TonKho <= 0
        ? "#DC2626"
        : (TonToiThieu > 0 && TonKho <= TonToiThieu)
            ? "#D97706"
            : (TonToiDa > 0 && TonKho >= TonToiDa)
                ? "#2563EB"
                : "#059669";

    public string MauNenTrangThai => TonKho <= 0
        ? "#FEF2F2"
        : (TonToiThieu > 0 && TonKho <= TonToiThieu)
            ? "#FFFBEB"
            : (TonToiDa > 0 && TonKho >= TonToiDa)
                ? "#EFF6FF"
                : "#ECFDF5";

    public string TonKhoHienThi => TonKho.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string GiaVonHienThi => GiaVon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string GiaTriTonHienThi => GiaTriTon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}

/// <summary>Dòng bảng chi tiết (một mã hàng trong khoảng đang xem).</summary>
public sealed class BaoCaoHangHoaChiTiet
{
    public int Stt { get; set; }
    public string MaHang { get; set; } = string.Empty;
    public string TenHang { get; set; } = string.Empty;
    public string NhomHang { get; set; } = string.Empty;
    public decimal SoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal GiaVon { get; set; }

    public decimal LoiNhuan => DoanhThu - GiaVon;
    public double TyLeLoiNhuan => DoanhThu <= 0 ? 0 : (double)(LoiNhuan / DoanhThu * 100m);

    /// <summary>Lợi nhuận không âm — bind vào class "ln" của XAML (màu xanh).</summary>
    public bool LoiNhuanDuong => LoiNhuan >= 0;

    /// <summary>Lợi nhuận âm — bind vào class "lo" của XAML (màu cảnh báo).</summary>
    public bool LoiNhuanAm => LoiNhuan < 0;

    public string SoLuongBanHienThi => SoLuongBan.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string GiaVonHienThi => GiaVon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string LoiNhuanHienThi => LoiNhuan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyLeLoiNhuanHienThi => TyLeLoiNhuan.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) + "%";
}

/// <summary>Tùy chọn nhóm hàng cho bộ lọc bên trái (gồm "Tất cả" và "Chưa phân nhóm").</summary>
public sealed class BaoCaoHangHoaNhomOption
{
    /// <summary>-1 = tất cả, 0 = sản phẩm chưa gán nhóm, &gt; 0 = id nhóm thật.</summary>
    public int Id { get; set; } = -1;

    public string TenNhom { get; set; } = string.Empty;

    public string TenHienThi { get; set; } = string.Empty;
}

/// <summary>Toàn bộ dữ liệu màn "Báo cáo hàng hóa" cho một lần tải (Issue #48).</summary>
public sealed class BaoCaoHangHoaDuLieu
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public BaoCaoHangHoaTongHop TongHop { get; set; } = new();
    public List<BaoCaoHangHoaTop> TopDoanhThu { get; set; } = new();
    public List<BaoCaoHangHoaTop> TopSoLuong { get; set; } = new();
    public List<BaoCaoHangHoaTop> TopBanCham { get; set; } = new();
    public List<BaoCaoHangHoaNhom> NhomHang { get; set; } = new();
    public List<BaoCaoHangHoaTonKho> TonKhoList { get; set; } = new();
    public List<BaoCaoHangHoaTonKho> HangKhongBan { get; set; } = new();
    public List<BaoCaoHangHoaChiTiet> ChiTiet { get; set; } = new();
}
