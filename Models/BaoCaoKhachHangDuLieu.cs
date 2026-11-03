using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Thẻ thống kê tổng hợp của màn "Báo cáo khách hàng" trong khoảng thời gian
/// đang xem. Mọi con số tính trực tiếp trong SQL trên hóa đơn đã hoàn thành
/// của khách hàng có thông tin (không tính khách lẻ).
/// </summary>
public sealed class BaoCaoKhachHangTongHopKy
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }

    /// <summary>Số khách hàng phát sinh hóa đơn trong kỳ (khoảng khắc phân trang của bảng).</summary>
    public int TongKhachHang { get; set; }

    public int SoDonHang { get; set; }

    /// <summary>SUM(tong_tien_hang) — tổng tiền hàng trước giảm giá.</summary>
    public decimal TongTienHang { get; set; }

    /// <summary>SUM(giam_gia).</summary>
    public decimal GiamGia { get; set; }

    /// <summary>SUM(tong_tien_hang − giam_gia) — giá trị hàng đã mua sau giảm giá (doanh số).</summary>
    public decimal GiaTriMuaHang { get; set; }

    /// <summary>SUM(khach_da_thanh_toan).</summary>
    public decimal DaThanhToan { get; set; }

    /// <summary>SUM(con_no) — công nợ phát sinh trong kỳ.</summary>
    public decimal CongNo { get; set; }

    /// <summary>Số khách hàng còn nợ phát sinh trong kỳ.</summary>
    public int SoKhachConNo { get; set; }

    /// <summary>Số khách hàng đã thanh toán đầy đủ trong kỳ.</summary>
    public int SoKhachDaThanhToanHet => Math.Max(0, TongKhachHang - SoKhachConNo);

    /// <summary>Số khách hàng có phát sinh thanh toán tiền trong kỳ.</summary>
    public int SoKhachCoThanhToan { get; set; }

    /// <summary>Giá trị mua hàng trung bình trên mỗi khách hàng = Doanh số / Số khách hàng.</summary>
    public decimal GiaTriMuaTrungBinh => TongKhachHang > 0 ? GiaTriMuaHang / TongKhachHang : 0m;

    public string TongKhachHangHienThi =>
        TongKhachHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string SoDonHangHienThi =>
        SoDonHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string DoanhSoHienThi =>
        GiaTriMuaHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string CongNoHienThi =>
        CongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string TongTienHangHienThi =>
        TongTienHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string GiamGiaHienThi =>
        GiamGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string DaThanhToanHienThi =>
        DaThanhToan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string GiaTriMuaHangHienThi =>
        GiaTriMuaHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string GiaTriMuaTrungBinhHienThi =>
        GiaTriMuaTrungBinh.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string SoKhachConNoHienThi =>
        SoKhachConNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string SoKhachDaThanhToanHetHienThi =>
        SoKhachDaThanhToanHet.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string SoKhachCoThanhToanHienThi =>
        SoKhachCoThanhToan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string KhoangHienThi =>
        $"Khoảng thời gian: {TuNgay:dd/MM/yyyy} - {DenNgay:dd/MM/yyyy}";

    /// <summary>Tên file gợi ý khi xuất Excel báo cáo khách hàng.</summary>
    public string TenFileXuat() => ShopHoa.Helpers.ReportFileNamingHelper.TaoTenFileKhachHang();
}

/// <summary>
/// Một dòng trong biểu đồ "Top khách hàng". <see cref="GiaTri"/> là giá trị của
/// mối quan tâm đang chọn (tiền hoặc số đơn), <see cref="LaTien"/> quyết định
/// cách định dạng hiển thị.
/// </summary>
public sealed class BaoCaoKhachHangTop
{
    public int Stt { get; set; }
    public uint Id { get; set; }
    public string MaKhachHang { get; set; } = string.Empty;
    public string TenKhachHang { get; set; } = string.Empty;
    public string SoDienThoai { get; set; } = string.Empty;

    /// <summary>Giá trị của mối quan tâm đang chọn (vd: tổng tiền hàng, công nợ…).</summary>
    public decimal GiaTri { get; set; }

    /// <summary>Số đơn hàng của khách trong kỳ — dùng làm dòng phụ trong tooltip.</summary>
    public int SoDonHang { get; set; }

    /// <summary>Tổng số lượng sản phẩm khách hàng đã mua trong kỳ.</summary>
    public decimal TongSoLuongMua { get; set; }

    /// <summary>Các cột tổng hợp của dòng top — service dùng để tính giá trị thanh
    /// theo mối quan tâm đang chọn (Dapper ánh xạ từ SUM trong câu truy vấn).</summary>
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal GiaTriMuaHang { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal CongNo { get; set; }

    /// <summary>Giá trị là tiền (đ) hay là số lượng (đơn hàng).</summary>
    public bool LaTien { get; set; } = true;

    /// <summary>Chiều dài thanh so với dòng cao nhất (0..100).</summary>
    public double TyLeBar { get; set; }

    /// <summary>Bề rộng tối đa của thanh (px) — XAML lấy hằng này làm mốc.</summary>
    public const double BeRongToiDa = 240;

    /// <summary>Bề rộng thanh (px) để XAML bind thẳng, không cần converter.</summary>
    public double RongBar => TyLeBar <= 0 ? 0 : Math.Max(6, TyLeBar / 100d * BeRongToiDa);

    public string GiaTriHienThi => LaTien
        ? GiaTri.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ"
        : GiaTri.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string SoDonHangHienThi =>
        SoDonHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Dòng phụ dưới tên khách trong biểu đồ: mã · số điện thoại.</summary>
    public string MoTaHienThi =>
        string.IsNullOrWhiteSpace(SoDienThoai) ? MaKhachHang
            : string.IsNullOrWhiteSpace(MaKhachHang) ? SoDienThoai
            : $"{MaKhachHang} · {SoDienThoai}";

    /// <summary>Nội dung tooltip khi rê chuột lên dòng/thanh biểu đồ.</summary>
    public string TooltipHienThi => $"{TenKhachHang}: {GiaTriHienThi} ({SoDonHangHienThi} đơn hàng)";
}

/// <summary>Một dòng trong bảng chi tiết báo cáo khách hàng.</summary>
public sealed class BaoCaoKhachHangDong
{
    public int Stt { get; set; }
    public uint Id { get; set; }
    public string MaKhachHang { get; set; } = string.Empty;
    public string TenKhachHang { get; set; } = string.Empty;
    public string SoDienThoai { get; set; } = string.Empty;

    public int SoDonHang { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal GiaTriMuaHang { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal CongNo { get; set; }

    /// <summary>Có công nợ phát sinh trong kỳ — tô đậm cột công nợ cho dễ nhìn.</summary>
    public bool CoCongNo => CongNo > 0;

    public string SoDonHangHienThi =>
        SoDonHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string TongTienHangHienThi =>
        TongTienHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string GiamGiaHienThi =>
        GiamGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string GiaTriMuaHangHienThi =>
        GiaTriMuaHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string DaThanhToanHienThi =>
        DaThanhToan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string CongNoHienThi =>
        CongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}

/// <summary>Dòng phân tích khách hàng theo nhóm.</summary>
public sealed class BaoCaoKhachHangNhom
{
    public int Stt { get; set; }
    public string TenNhom { get; set; } = string.Empty;
    public int SoKhachHang { get; set; }
    public decimal DoanhThu { get; set; }
    public double TyLePhanTram { get; set; }
}

/// <summary>Toàn bộ dữ liệu của một lần tải màn "Báo cáo khách hàng".</summary>
public sealed class BaoCaoKhachHangDuLieu
{
    public BaoCaoKhachHangTongHopKy TongHop { get; set; } = new();

    /// <summary>Top khách hàng cho biểu đồ (tối đa 10 dòng, lấy trực tiếp từ SQL).</summary>
    public List<BaoCaoKhachHangTop> Top { get; set; } = new();

    /// <summary>Top 10 khách hàng theo doanh thu đầy đủ số lượng mua, doanh thu, thanh toán, công nợ.</summary>
    public List<BaoCaoKhachHangTop> Top10DoanhThu { get; set; } = new();

    /// <summary>Danh sách phân tích nhóm khách hàng.</summary>
    public List<BaoCaoKhachHangNhom> NhomKhachHang { get; set; } = new();

    /// <summary>Danh sách khách hàng phục vụ phân tích công nợ.</summary>
    public List<BaoCaoKhachHangDong> DanhSachCongNo { get; set; } = new();

    /// <summary>Các dòng của trang đang xem (phân trang bằng LIMIT/OFFSET trong SQL).</summary>
    public List<BaoCaoKhachHangDong> DongTrang { get; set; } = new();
}
