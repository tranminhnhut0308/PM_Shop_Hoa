using System.Globalization;

namespace ShopHoa.Models;

public sealed class BaoCaoBanHangPhuongThucRow
{
    public string PhuongThuc { get; set; } = string.Empty;
    public string PhuongThucHienThi { get; set; } = string.Empty;
    public int SoGiaoDich { get; set; }
    public decimal SoTien { get; set; }

    /// <summary>Tỷ lệ số tiền của dòng này so với tổng các dòng (0..100).</summary>
    public double TyLePhanTram { get; set; }

    public string SoTienHienThi => SoTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string SoGiaoDichHienThi => SoGiaoDich.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string TyLePhanTramHienThi => TyLePhanTram.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) + "%";
}



public sealed class BaoCaoBanHangDiemThoiGian
{
    public DateTime ThoiGian { get; set; }
    public string NhanXet { get; set; } = string.Empty;
    public decimal DoanhThu { get; set; }
    public int SoHoaDon { get; set; }

    /// <summary>Số tiền đã thu trong điểm thời gian này (dùng cho tooltip biểu đồ).</summary>
    public decimal DaThu { get; set; }

    /// <summary>Công nợ phát sinh trong điểm thời gian này (dùng cho tooltip biểu đồ).</summary>
    public decimal CongNo { get; set; }

    /// <summary>Tỷ lệ chiều cao cột so với điểm cao nhất trong khoảng (0..100).</summary>
    public double TyLeChieuCao { get; set; }

    /// <summary>Bề rộng cột (0..100) — mỗi cột trong biểu đồ rộng đều nhau.</summary>
    public double TyLeBeRong { get; set; } = 100;

    /// <summary>Chiều cao tối đa của vùng vẽ cột (px) — XAML lấy hằng này làm mốc.</summary>
    public const double ChieuCaoToiDa = 190;

    /// <summary>Bề rộng tối đa của một cột (px) để cột không giãn quá rộng.</summary>
    public const double BeRongToiDa = 72;

    /// <summary>Chiều cao cột tính bằng pixel để XAML bind thẳng, không cần converter.</summary>
    public double ChieuCaoCot => DoanhThu <= 0 ? 0 : Math.Max(3, TyLeChieuCao / 100d * ChieuCaoToiDa);

    /// <summary>Bề rộng cột (px); nhiều điểm thì cuộn ngang trong vùng biểu đồ.</summary>
    public double BeRongCot => Math.Max(55, Math.Min(BeRongToiDa, TyLeBeRong / 100d * BeRongToiDa));

    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string DaThuHienThi => DaThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string CongNoHienThi => CongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string SoHoaDonHienThi => SoHoaDon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " hóa đơn";

    /// <summary>Ngày đầy đủ hiển thị trên dòng đầu tooltip (kèm giờ nếu biểu đồ chia theo giờ).</summary>
    public string NgayDayDu => ThoiGian.ToString(NhanXet.Contains(':') ? "dd/MM/yyyy HH:00" : "dd/MM/yyyy");

    public string ToolTip =>
        $"{NgayDayDu}\n" +
        $"Doanh thu: {DoanhThuHienThi}\n" +
        $"Số hóa đơn: {SoHoaDonHienThi}\n" +
        $"Đã thu: {DaThuHienThi}\n" +
        $"Công nợ: {CongNoHienThi}";
}



public sealed class BaoCaoBanHangBangRow
{
    public DateTime ThoiGian { get; set; }
    public string ThoiGianHienThi { get; set; } = string.Empty;
    public int SoHoaDon { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThuKhac { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal DaThu { get; set; }
    public decimal CongNo { get; set; }
    public string SoHoaDonHienThi => SoHoaDon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string TongTienHangHienThi => TongTienHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string GiamGiaHienThi => GiamGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string ThuKhacHienThi => ThuKhac.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string DaThuHienThi => DaThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string CongNoHienThi => CongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
}



public sealed class BaoCaoBanHangChiTietHoaDon
{
    public uint Id { get; set; }
    public string MaHoaDon { get; set; } = string.Empty;
    public DateTime ThoiGianBan { get; set; }
    public string KhachHang { get; set; } = string.Empty;
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThuKhac { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal DaThu { get; set; }
    public decimal CongNo { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public string ThoiGianHienThi => ThoiGianBan.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    public string TongTienHangHienThi => TongTienHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string GiamGiaHienThi => GiamGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string ThuKhacHienThi => ThuKhac.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string DaThuHienThi => DaThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string CongNoHienThi => CongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
}



public sealed class BaoCaoBanHangTopSanPham
{
    public int Stt { get; set; }
    public string SanPham { get; set; } = string.Empty;
    public decimal SoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }
    public string SoLuongBanHienThi => SoLuongBan.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
}



public sealed class BaoCaoBanHangTopKhachHang
{
    public int Stt { get; set; }
    public string KhachHang { get; set; } = string.Empty;
    public int SoHoaDon { get; set; }
    public decimal TongMua { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal CongNo { get; set; }
    public string SoHoaDonHienThi => SoHoaDon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string TongMuaHienThi => TongMua.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string DaThanhToanHienThi => DaThanhToan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string CongNoHienThi => CongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
}



public sealed class BaoCaoBanHangNhomHang
{
    public int Stt { get; set; }
    public string NhomHang { get; set; } = string.Empty;
    public decimal SoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }

    /// <summary>Tỷ lệ doanh thu của nhóm so với tổng (0..100).</summary>
    public double TyLePhanTram { get; set; }

    public string SoLuongBanHienThi => SoLuongBan.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " d";
    public string TyLePhanTramHienThi => TyLePhanTram.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) + "%";
}



public sealed class BaoCaoBanHangTongHop
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public int SoHoaDon { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThuKhac { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal DaThu { get; set; }
    public decimal CongNo { get; set; }
    public decimal GiaTriDonTrungBinh => SoHoaDon <= 0 ? 0m : DoanhThu / SoHoaDon;

    // Nhóm 2: Thống kê thanh toán
    public decimal TienMat { get; set; }
    public decimal ChuyenKhoan { get; set; }
    public decimal TienKhac { get; set; }
    public decimal TongThanhToan => TienMat + ChuyenKhoan + TienKhac;
    public int SoHoaDonDaThanhToan { get; set; }
    public int SoHoaDonThanhToanMotPhan { get; set; }
    public int SoHoaDonChuaThanhToan { get; set; }

    // Nhóm 3: Thống kê công nợ & khách hàng
    public int SoKhachHang { get; set; }
    public int SoKhachHangCoCongNo { get; set; }
    public string KhachHangDoanhThuCaoNhat { get; set; } = string.Empty;
    public string KhachHangCongNoCaoNhat { get; set; } = string.Empty;

    // Nhóm 4: Thống kê sản phẩm & nhóm hàng
    public decimal TongSanPhamBanRa { get; set; }
    public string SanPhamDoanhThuCaoNhat { get; set; } = string.Empty;
    public string SanPhamSoLuongNhieuNhat { get; set; } = string.Empty;
    public string NhomHangDoanhThuCaoNhat { get; set; } = string.Empty;

    public string SoHoaDonHienThi => SoHoaDon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string TongTienHangHienThi => TongTienHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string GiamGiaHienThi => GiamGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string ThuKhacHienThi => ThuKhac.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string DaThuHienThi => DaThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string CongNoHienThi => CongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string GiaTriDonTrungBinhHienThi => GiaTriDonTrungBinh.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string TienMatHienThi => TienMat.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string ChuyenKhoanHienThi => ChuyenKhoan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TienKhacHienThi => TienKhac.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TongThanhToanHienThi => TongThanhToan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string SoHoaDonDaThanhToanHienThi => SoHoaDonDaThanhToan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoHoaDonThanhToanMotPhanHienThi => SoHoaDonThanhToanMotPhan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoHoaDonChuaThanhToanHienThi => SoHoaDonChuaThanhToan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoKhachHangHienThi => SoKhachHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoKhachHangCoCongNoHienThi => SoKhachHangCoCongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string TongSanPhamBanRaHienThi => TongSanPhamBanRa.ToString("N2", CultureInfo.GetCultureInfo("vi-VN"));

    public string TenFileXuat() => ShopHoa.Helpers.ReportFileNamingHelper.TaoTenFileBanHang();
}



public sealed class BaoCaoBanHangDuLieu
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public BaoCaoBanHangTongHop TongHop { get; set; } = new();
    public List<BaoCaoBanHangDiemThoiGian> BieuDo { get; set; } = new();
    public List<BaoCaoBanHangBangRow> BangDuLieu { get; set; } = new();
    public List<BaoCaoBanHangChiTietHoaDon> ChiTietHoaDon { get; set; } = new();
    public List<BaoCaoBanHangTopSanPham> TopSanPham { get; set; } = new();
    public List<BaoCaoBanHangTopKhachHang> TopKhachHang { get; set; } = new();
    public List<BaoCaoBanHangNhomHang> NhomHang { get; set; } = new();
    public List<BaoCaoBanHangPhuongThucRow> PhuongThucThanhToan { get; set; } = new();
}


