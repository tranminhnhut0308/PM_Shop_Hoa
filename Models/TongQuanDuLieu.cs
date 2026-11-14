using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Khoảng thời gian dùng để lọc số liệu Dashboard. Giá trị <c>Key</c> được lưu
/// vào ViewModel và truyền xuống SQL nên phải khớp với tên cột trong
/// <see cref="TongQuanKhoangThoiGian"/>.
/// </summary>
public static class TongQuanKhoangThoiGian
{
    public const string HomNay = "HOM_NAY";
    public const string BayNgay = "7_NGAY";
    public const string ThangNay = "THANG_NAY";
    public const string ThangTruoc = "THANG_TRUOC";
    public const string NamNay = "NAM_NAY";
    public const string TuDien = "TU_DIEN";

    public static readonly IReadOnlyList<string> TatCa =
    [
        HomNay,
        BayNgay,
        ThangNay,
        ThangTruoc,
        NamNay,
        TuDien
    ];

    /// <summary>Chuỗi hiển thị trên nút bộ lọc.</summary>
    public static string TenHienThi(string key) => key switch
    {
        HomNay => "Hôm nay",
        BayNgay => "7 ngày",
        ThangNay => "Tháng này",
        ThangTruoc => "Tháng trước",
        NamNay => "Năm nay",
        TuDien => "Tùy chọn",
        _ => "Hôm nay"
    };
}

/// <summary>Cách gom dữ liệu doanh thu trên biểu đồ.</summary>
public static class TongQuanChieuDoanhThu
{
    public const string TheoNgay = "NGAY";
    public const string TheoGio = "GIO";
    public const string TheoThu = "THU";

    public static readonly IReadOnlyList<string> TatCa = [TheoNgay, TheoGio, TheoThu];

    public static string TenHienThi(string key) => key switch
    {
        TheoGio => "Theo giờ",
        TheoThu => "Theo thứ",
        _ => "Theo ngày"
    };
}

/// <summary>Kiểu xếp hạng của các thẻ "Top 10".</summary>
public static class TongQuanXepHang
{
    public const string TheoSoLuong = "SO_LUONG";
    public const string TheoDoanhThu = "DOANH_THU";

    public static readonly IReadOnlyList<string> TatCa = [TheoSoLuong, TheoDoanhThu];

    public static string TenHienThi(string key) =>
        key == TheoDoanhThu ? "Theo doanh thu" : "Theo số lượng";
}

/// <summary>Một điểm dữ liệu trên biểu đồ doanh thu.</summary>
public sealed class TongQuanDiemDoanhThu
{
    /// <summary>Nhãn hiển thị trên trục X (ví dụ "01/10", "14:00", "Thứ 2").</summary>
    public string Nhan { get; set; } = "";

    /// <summary>Nhãn đầy đủ dùng cho tooltip (ví dụ "Thứ hai 01/10/2026").</summary>
    public string NhanDayDu { get; set; } = "";

    public decimal DoanhThu { get; set; }

    public int SoHoaDon { get; set; }

    public bool LaDiemCaoNhat { get; set; }

    public string DoanhThuHienThi => $"{DoanhThu:N0} đ";

    public string SoHoaDonHienThi => $"{SoHoaDon:N0} hóa đơn";

    public string DoanhThuNganGon
    {
        get
        {
            if (DoanhThu <= 0) return "";
            if (DoanhThu >= 1_000_000_000m) return $"{DoanhThu / 1_000_000_000m:0.#} tỷ";
            if (DoanhThu >= 1_000_000m) return $"{DoanhThu / 1_000_000m:0.##} tr";
            if (DoanhThu >= 1_000m) return $"{DoanhThu / 1_000m:0.#}k";
            return $"{DoanhThu:N0}";
        }
    }

    public string SoHoaDonNganGon => SoHoaDon > 0 ? $"{SoHoaDon:N0} HĐ" : "";

    /// <summary>
    /// Tỉ lệ chiều cao cột so với giá trị lớn nhất. Database đã chia tỉ lệ sẵn
    /// để tránh phải load cả danh sách ra RAM rồi tính lại ở client.
    /// </summary>
    public double TyLeChieuCao { get; set; }

    /// <summary>Bề rộng cột (0..100) dùng chung cho cả chế độ giờ và ngày.</summary>
    public double TyLeBeRong { get; set; }

    /// <summary>Bề rộng cột tùy chỉnh theo góc nhìn (ngày/giờ/thứ).</summary>
    public double BeRongCotTuyChinh { get; set; }

    /// <summary>Chiều cao tối đa của vùng vẽ cột (px) — XAML lấy hằng này làm mốc.</summary>
    public const double ChieuCaoToiDa = 105;

    /// <summary>Bề rộng tối đa của một cột (px) để các cột không giãn quá rộng.</summary>
    public const double BeRongToiDa = 90;

    /// <summary>Có doanh thu dương hay không; dùng để ẩn hiện cột và số liệu.</summary>
    public bool CoDoanhThu => DoanhThu > 0;

    /// <summary>Chiều cao cột tính bằng pixel để XAML bind thẳng, không cần converter.</summary>
    public double ChieuCaoCot =>
        DoanhThu <= 0 || TyLeChieuCao <= 0
            ? 0
            : Math.Max(3.0, Math.Round(Math.Clamp(TyLeChieuCao, 0, 100) / 100d * ChieuCaoToiDa, 1));

    /// <summary>Bề rộng cột (px); nhiều điểm thì cuộn ngang trong vùng biểu đồ.</summary>
    public double BeRongCot => BeRongCotTuyChinh > 0
        ? BeRongCotTuyChinh
        : Math.Max(55, Math.Min(BeRongToiDa, TyLeBeRong / 100d * BeRongToiDa));

    public bool CoDuLieu => SoHoaDon > 0 || DoanhThu > 0;

    public string MauCot => DoanhThu <= 0
        ? "Transparent"
        : (LaDiemCaoNhat ? "#047857" : "#0D9488");

    public string MauChuNhan => LaDiemCaoNhat
        ? "#047857"
        : (CoDuLieu ? "#163F38" : "#8A9B95");

    /// <summary>Tooltip đầy đủ khi rê chuột lên cột.</summary>
    public string ToolTipDayDu =>
        $"{NhanDayDu}\n" +
        $"Doanh thu: {DoanhThuHienThi}\n" +
        $"Số hóa đơn: {SoHoaDonHienThi}";
}

/// <summary>Một dòng trong thẻ "Top 10" (hàng bán chạy hoặc khách hàng).</summary>
public sealed class TongQuanHangTop
{
    public int ThuTu { get; set; }

    public string Ten { get; set; } = "";

    public string TenPhu { get; set; } = "";

    public decimal SoLuong { get; set; }

    public decimal DoanhThu { get; set; }

    /// <summary>Tỉ lệ bề rộng thanh ngang (0..100) so với dòng lớn nhất.</summary>
    public double TyLePhanTram { get; set; }

    public double TyLeBeRong => Math.Clamp(TyLePhanTram, 0, 100);

    public double TyLeSoLuong { get; set; }

    public double TyLeSoLuongAnToan => Math.Clamp(TyLeSoLuong, 0, 100);

    public string DoanhThuHienThi => $"{DoanhThu:N0} đ";

    public string SoLuongHienThi => $"{SoLuong:N0}";
}

/// <summary>Một dòng nhật ký hoạt động của tất cả tài khoản (bao gồm Admin và nhân viên).</summary>
public sealed class TongQuanHoatDongTaiKhoanCon
{
    public ulong Id { get; set; }
    public uint? NguoiDungId { get; set; }
    public string TenDangNhap { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string VaiTro { get; set; } = "";
    public string HanhDong { get; set; } = "";
    public string? DoiTuong { get; set; }
    public ulong? DoiTuongId { get; set; }
    public string? MaChungTu { get; set; }
    public string? NoiDung { get; set; }
    public string? TruongThayDoi { get; set; }
    public string? GiaTriTruoc { get; set; }
    public string? GiaTriSau { get; set; }
    public decimal? GiaTri { get; set; }
    public DateTime ThoiGian { get; set; }

    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM/yyyy HH:mm");
    public string GioHienThi => ThoiGian.ToString("HH:mm");
    public string NgayHienThi => ThoiGian.ToString("dd/MM");
    public bool CoGiaTri => GiaTri.HasValue && GiaTri.Value != 0;
    public string GiaTriHienThi => CoGiaTri
        ? $"{Math.Abs(GiaTri!.Value):N0} đ"
        : "";
    public bool CoMaChungTu => !string.IsNullOrWhiteSpace(MaChungTu);
    public string MaChungTuSach => MaChungTu?.Trim() ?? "";
    public string MaChungTuHienThi => CoMaChungTu ? $"Mã phiếu: {MaChungTu!.Trim()}" : "";
    public bool CoNoiDung => !string.IsNullOrWhiteSpace(NoiDung);
    public bool CoChiTietMoTa => CoMaChungTu || CoNoiDung || CoThayDoiChiTiet;

    public bool CoTruongThayDoi => !string.IsNullOrWhiteSpace(TruongThayDoi);
    public bool CoGiaTriTruoc => !string.IsNullOrWhiteSpace(GiaTriTruoc);
    public bool CoGiaTriSau => !string.IsNullOrWhiteSpace(GiaTriSau);
    public bool CoThayDoiChiTiet => CoTruongThayDoi || CoGiaTriTruoc || CoGiaTriSau;

    public bool CoLienKetChungTu
    {
        get
        {
            if (CoMaChungTu)
                return true;

            if (!DoiTuongId.HasValue || DoiTuongId.Value == 0 || string.IsNullOrWhiteSpace(DoiTuong))
                return false;

            var dt = DoiTuong.Trim().ToUpperInvariant();
            return dt is "HOA_DON" or "PHIEU_NHAP" or "SO_QUY" or "CONG_NO_KH" or "CONG_NO_NCC"
                or "SAN_PHAM" or "KHACH_HANG" or "NHA_CUNG_CAP" or "NGUOI_DUNG";
        }
    }

    public string DoiTuongHienThi => (DoiTuong ?? "").Trim().ToUpperInvariant() switch
    {
        "HOA_DON" => "Hóa đơn",
        "PHIEU_NHAP" => "Phiếu nhập",
        "SO_QUY" => "Sổ quỹ",
        "CONG_NO_KH" => "Thu nợ KH",
        "CONG_NO_NCC" => "Thanh toán NCC",
        "SAN_PHAM" => "Sản phẩm",
        "KHACH_HANG" => "Khách hàng",
        "NHA_CUNG_CAP" => "Nhà cung cấp",
        "DANH_MUC" => "Danh mục",
        "DON_VI_TINH" => "Đơn vị tính",
        "NGUOI_DUNG" or "VAI_TRO" => "Tài khoản & Quyền",
        "HE_THONG" => "Hệ thống",
        _ => ""
    };

    public bool CoDoiTuongHienThi => !string.IsNullOrWhiteSpace(DoiTuongHienThi);

    public string NhanNutMoChungTu => CoMaChungTu
        ? MaChungTu!.Trim()
        : (CoDoiTuongHienThi ? $"Mở {DoiTuongHienThi}" : "Mở chi tiết");

    public string TooltipMoChungTu => CoMaChungTu
        ? $"Nhấn để mở trực tiếp chứng từ / dữ liệu {MaChungTu!.Trim()}"
        : $"Nhấn để mở {DoiTuongHienThi}";

    public string ChuCaiDaiDien
    {
        get
        {
            var raw = !string.IsNullOrWhiteSpace(HoTen) ? HoTen.Trim() : TenDangNhap.Trim();
            if (string.IsNullOrEmpty(raw))
                return "?";

            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var target = parts.Length > 0 ? parts[^1] : raw;
            return target[..1].ToUpperInvariant();
        }
    }

    public string MauNenAvatar
    {
        get
        {
            var vt = VaiTroHienThi;
            if (vt.Contains("Quản trị", StringComparison.OrdinalIgnoreCase) || vt.Contains("Admin", StringComparison.OrdinalIgnoreCase))
                return "#FEF3C7";
            if (vt.Contains("Quản lý", StringComparison.OrdinalIgnoreCase))
                return "#F3E8FF";
            if (vt.Contains("kho", StringComparison.OrdinalIgnoreCase))
                return "#FFEDD5";
            if (vt.Contains("Thu ngân", StringComparison.OrdinalIgnoreCase))
                return "#E0F2FE";
            return "#DCFCE7";
        }
    }

    public string MauChuAvatar
    {
        get
        {
            var vt = VaiTroHienThi;
            if (vt.Contains("Quản trị", StringComparison.OrdinalIgnoreCase) || vt.Contains("Admin", StringComparison.OrdinalIgnoreCase))
                return "#92400E";
            if (vt.Contains("Quản lý", StringComparison.OrdinalIgnoreCase))
                return "#6B21A8";
            if (vt.Contains("kho", StringComparison.OrdinalIgnoreCase))
                return "#9A3412";
            if (vt.Contains("Thu ngân", StringComparison.OrdinalIgnoreCase))
                return "#075985";
            return "#166534";
        }
    }

    public string BieuTuongHanhDong
    {
        get
        {
            var hd = HanhDong?.Trim() ?? "";
            if (hd.Contains("Đăng nhập", StringComparison.OrdinalIgnoreCase) || hd.Equals("DANG_NHAP", StringComparison.OrdinalIgnoreCase))
                return "🔐";
            if (hd.Contains("Đăng xuất", StringComparison.OrdinalIgnoreCase) || hd.Equals("DANG_XUAT", StringComparison.OrdinalIgnoreCase))
                return "🚪";
            if (hd.StartsWith("Hủy", StringComparison.OrdinalIgnoreCase) || hd.Equals("HUY_DON", StringComparison.OrdinalIgnoreCase) || hd.Equals("HỦY", StringComparison.OrdinalIgnoreCase))
                return "⛔";
            if (hd.StartsWith("Xóa", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Ẩn", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Khóa", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Ngừng", StringComparison.OrdinalIgnoreCase))
                return "🗑";
            if (hd.Contains("Thanh toán", StringComparison.OrdinalIgnoreCase) || hd.Contains("Thu nợ", StringComparison.OrdinalIgnoreCase) || hd.Contains("Thu tiền", StringComparison.OrdinalIgnoreCase) || hd.Contains("Chi tiền", StringComparison.OrdinalIgnoreCase))
                return "💳";
            if (hd.StartsWith("Sửa", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Chỉnh", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Cập nhật", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Thay đổi", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Phân quyền", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Đặt lại", StringComparison.OrdinalIgnoreCase))
                return "✏️";
            if (hd.StartsWith("Tạo", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Thêm", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Nhập", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Bán", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Khôi phục", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Kích hoạt", StringComparison.OrdinalIgnoreCase) || hd.StartsWith("Mở khóa", StringComparison.OrdinalIgnoreCase))
                return "➕";
            return "📝";
        }
    }

    public string NoiDungSaoChepDayDu
    {
        get
        {
            var lines = new List<string>
            {
                $"Nhân viên: {NguoiThucHienHienThi}",
                $"Vai trò: {VaiTroHienThi}",
                $"Hành động: {HanhDong}",
                $"Thời gian: {ThoiGianHienThi}"
            };

            if (CoMaChungTu)
                lines.Add($"Mã chứng từ: {MaChungTuSach}");
            if (CoDoiTuongHienThi)
                lines.Add($"Phân loại: {DoiTuongHienThi}");
            if (CoGiaTri)
                lines.Add($"Số tiền: {GiaTriHienThi}");
            if (CoNoiDung)
                lines.Add($"Nội dung: {NoiDung!.Trim()}");
            if (CoTruongThayDoi)
                lines.Add($"Trường thay đổi: {TruongThayDoi!.Trim()}");
            if (CoGiaTriTruoc)
                lines.Add($"Giá trị trước:\n{GiaTriTruoc!.Trim()}");
            if (CoGiaTriSau)
                lines.Add($"Giá trị sau:\n{GiaTriSau!.Trim()}");

            return string.Join(Environment.NewLine, lines);
        }
    }

    public string NguoiThucHienHienThi
    {
        get
        {
            var hoTen = HoTen?.Trim() ?? "";
            var tenDangNhap = TenDangNhap?.Trim() ?? "";

            if (!string.IsNullOrEmpty(hoTen) && !string.IsNullOrEmpty(tenDangNhap))
            {
                return string.Equals(hoTen, tenDangNhap, StringComparison.OrdinalIgnoreCase)
                    ? hoTen
                    : $"{hoTen} ({tenDangNhap})";
            }

            if (!string.IsNullOrEmpty(hoTen))
                return hoTen;

            return !string.IsNullOrEmpty(tenDangNhap) ? tenDangNhap : "Tài khoản";
        }
    }

    public string VaiTroHienThi
    {
        get
        {
            if (string.IsNullOrWhiteSpace(VaiTro))
                return "Nhân viên";

            var cacVaiTro = VaiTro
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(ChuanHoaTenVaiTro)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return cacVaiTro.Count > 0 ? string.Join(", ", cacVaiTro) : "Nhân viên";
        }
    }

    private static string ChuanHoaTenVaiTro(string raw)
    {
        var token = raw.Trim();
        return token.ToUpperInvariant() switch
        {
            "QUAN_TRI" or "ADMIN" or "QUAN_TRI_VIEN" => "Quản trị viên",
            "QUAN_LY" => "Quản lý",
            "THU_NGAN" => "Thu ngân",
            "BAN_HANG" or "NHAN_VIEN_BAN_HANG" => "Nhân viên bán hàng",
            "KHO" or "THU_KHO" or "NHAN_VIEN_KHO" => "Nhân viên kho",
            "NHAN_VIEN" => "Nhân viên",
            _ => token
        };
    }

    private (string Fg, string Bg) LayMauVaiTro()
    {
        var text = VaiTroHienThi;
        if (text.Contains("Quản trị", StringComparison.OrdinalIgnoreCase) || text.Contains("Admin", StringComparison.OrdinalIgnoreCase))
            return ("#B45309", "#FEF3C7");
        if (text.Contains("Quản lý", StringComparison.OrdinalIgnoreCase))
            return ("#6D28D9", "#F3E8FF");
        if (text.Contains("Thu ngân", StringComparison.OrdinalIgnoreCase))
            return ("#0284C7", "#E0F2FE");
        if (text.Contains("kho", StringComparison.OrdinalIgnoreCase))
            return ("#C2410C", "#FFF7ED");
        if (text.Contains("bán hàng", StringComparison.OrdinalIgnoreCase))
            return ("#047857", "#ECFDF5");
        return ("#4B5563", "#F3F4F6");
    }

    private (string Fg, string Bg, string Border) LayMauHanhDong()
    {
        var hd = HanhDong?.Trim() ?? "";

        // 1. Đăng nhập / Đăng xuất (Xám xanh trung tính)
        if (hd.Contains("Đăng nhập", StringComparison.OrdinalIgnoreCase) ||
            hd.Equals("DANG_NHAP", StringComparison.OrdinalIgnoreCase) ||
            hd.Contains("Đăng xuất", StringComparison.OrdinalIgnoreCase) ||
            hd.Equals("DANG_XUAT", StringComparison.OrdinalIgnoreCase))
            return ("#334155", "#F1F5F9", "#CBD5E1");

        // 2. Hủy / Khóa / Ngừng hoạt động (Đỏ cảnh báo)
        if (hd.Equals("HUY_DON", StringComparison.OrdinalIgnoreCase) ||
            hd.Equals("HỦY", StringComparison.OrdinalIgnoreCase) ||
            hd.StartsWith("Hủy", StringComparison.OrdinalIgnoreCase) ||
            hd.StartsWith("Khóa", StringComparison.OrdinalIgnoreCase) ||
            hd.StartsWith("Ngừng", StringComparison.OrdinalIgnoreCase))
            return ("#B91C1C", "#FEF2F2", "#FECACA");

        // 3. Xóa / Ẩn (Cam đậm cảnh báo)
        if (hd.StartsWith("Xóa", StringComparison.OrdinalIgnoreCase) ||
            hd.StartsWith("Ẩn", StringComparison.OrdinalIgnoreCase))
            return ("#C2410C", "#FFF7ED", "#FED7AA");

        // 4. Thanh toán / Thu nợ / Sổ quỹ (Tím / Xanh cổ vịt)
        if (hd.Equals("Thu nợ", StringComparison.OrdinalIgnoreCase) ||
            hd.Equals("Thu tiền", StringComparison.OrdinalIgnoreCase) ||
            hd.Equals("THU_TIEN", StringComparison.OrdinalIgnoreCase) ||
            hd.Equals("Chi tiền", StringComparison.OrdinalIgnoreCase) ||
            hd.Equals("CHI_TIEN", StringComparison.OrdinalIgnoreCase) ||
            hd.Contains("Thanh toán", StringComparison.OrdinalIgnoreCase) ||
            hd.Equals("Sổ quỹ", StringComparison.OrdinalIgnoreCase) ||
            hd.Equals("SO_QUY", StringComparison.OrdinalIgnoreCase))
            return ("#5925DC", "#F4F3FF", "#D9D6FE");

        // 5. Chỉnh sửa / Cập nhật / Thay đổi / Phân quyền (Xanh dương)
        if (hd.StartsWith("Sửa", StringComparison.OrdinalIgnoreCase) ||
            hd.StartsWith("Cập nhật", StringComparison.OrdinalIgnoreCase) ||
            hd.StartsWith("Chỉnh", StringComparison.OrdinalIgnoreCase) ||
            hd.StartsWith("Thay đổi", StringComparison.OrdinalIgnoreCase) ||
            hd.StartsWith("Phân quyền", StringComparison.OrdinalIgnoreCase) ||
            hd.StartsWith("Đặt lại", StringComparison.OrdinalIgnoreCase))
            return ("#175CD3", "#EFF8FF", "#B2DDFF");

        // 6. Thêm mới / Tạo / Nhập hàng / Bán hàng / Khôi phục / Mở khóa (Xanh ngọc / Xanh lá)
        return ("#027A48", "#ECFDF3", "#ABEFC6");
    }

    public string MauBadgeVaiTro => LayMauVaiTro().Fg;

    public string MauNenBadgeVaiTro => LayMauVaiTro().Bg;

    public string MauBadgeHanhDong => LayMauHanhDong().Fg;

    public string MauNenBadgeHanhDong => LayMauHanhDong().Bg;

    public string MauVienBadgeHanhDong => LayMauHanhDong().Border;
}

/// <summary>Thông tin một hóa đơn bị loại trừ khỏi KPI chính thức (hóa đơn hủy, lưu tạm).</summary>
public sealed class TongQuanHoaDonBiLoaiTru
{
    public uint Id { get; set; }
    public string MaHoaDon { get; set; } = "";
    public DateTime ThoiGianBan { get; set; }
    public string TenKhachHang { get; set; } = "";
    public decimal TongTienHang { get; set; }
    public decimal KhachCanTra { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public string TrangThaiHoaDon { get; set; } = "";
    public string? LyDoChiTiet { get; set; }

    public string LyDoLoaiTru => TrangThaiHoaDon switch
    {
        "DA_HUY" or "HUY" => "Hóa đơn đã hủy",
        "LUU_TAM" => "Hóa đơn lưu tạm",
        _ => "Chưa ghi nhận"
    };

    public string ThoiGianHienThi => ThoiGianBan.ToString("dd/MM/yyyy HH:mm");
    public string TongTienHangHienThi => $"{TongTienHang:N0} đ";
    public string KhachCanTraHienThi => $"{KhachCanTra:N0} đ";
    public string DaThanhToanHienThi => $"{DaThanhToan:N0} đ";
    public string ConNoHienThi => $"{ConNo:N0} đ";
}

/// <summary>Thống kê kho lấy trực tiếp từ database, không suy diễn ở client.</summary>
public sealed class TongQuanThongKeKho
{
    public int TongSoHangHoa { get; set; }

    public int HangSapHet { get; set; }

    public int HangHetHang { get; set; }

    public int TongSoKhachHang { get; set; }

    public int TongSoNhaCungCap { get; set; }

    public decimal GiaTriTonKho { get; set; }

    public string GiaTriTonKhoHienThi => $"{GiaTriTonKho:N0} đ";
}

/// <summary>Kết quả tải một lần của toàn bộ Dashboard.</summary>
public sealed class TongQuanDuLieu
{
    // --- 5 thẻ "Kết quả bán hàng hôm nay" ---
    public decimal DoanhThuHomNay { get; set; }

    public int SoHoaDonHomNay { get; set; }

    public decimal DaThuHomNay { get; set; }

    public decimal ConNoHomNay { get; set; }

    public int SoDonHangHomNay { get; set; }

    // --- Hóa đơn bị loại trừ hợp lệ khỏi KPI chính thức (hóa đơn hủy, lưu tạm) ---
    public int SoHoaDonChuaGhiNhan { get; set; }

    public decimal TongTienHangChuaGhiNhan { get; set; }

    public decimal DoanhThuChuaGhiNhan { get; set; }

    public decimal DaThuChuaGhiNhan { get; set; }

    public decimal ConNoChuaGhiNhan { get; set; }

    public List<TongQuanHoaDonBiLoaiTru> DanhSachHoaDonChuaGhiNhan { get; set; } = [];

    // --- Thông tin giao dịch gần nhất & kỳ trước (hỗ trợ hiển thị khi kỳ chọn chưa có dữ liệu) ---
    public DateTime? NgayGiaoDichGanNhat { get; set; }
    public string? MaHoaDonGanNhat { get; set; }
    public decimal TongTienHoaDonGanNhat { get; set; }
    public int SoHoaDonThangTruoc { get; set; }
    public decimal DoanhThuThangTruoc { get; set; }

    // --- Biểu đồ doanh thu theo bộ lọc đang chọn ---
    public decimal TongDoanhThuKyChon { get; set; }

    public int TongSoHoaDonKyChon { get; set; }

    public List<TongQuanDiemDoanhThu> BieuDoDoanhThu { get; set; } = [];

    // --- Phân tích chuyên sâu theo Góc nhìn (Theo ngày / Theo giờ / Theo thứ) ---
    public string TieuDeDiemCaoNhat { get; set; } = "MỐC CAO ĐIỂM NHẤT";
    public string GiaTriDiemCaoNhat { get; set; } = "Chưa có phát sinh";
    public string PhuDeDiemCaoNhat { get; set; } = "0 đ • 0 hóa đơn";

    public string TieuDeTrungBinhChieu { get; set; } = "DOANH THU TRUNG BÌNH";
    public string GiaTriTrungBinhChieu { get; set; } = "0 đ";
    public string PhuDeTrungBinhChieu { get; set; } = "Trung bình theo kỳ";

    public string TieuDeMatDoChieu { get; set; } = "MẬT ĐỘ GIAO DỊCH";
    public string GiaTriMatDoChieu { get; set; } = "0";
    public string PhuDeMatDoChieu { get; set; } = "Chưa có đơn hoàn thành";

    // --- Hoạt động tài khoản con (dành riêng cho Admin) ---
    public List<TongQuanHoatDongTaiKhoanCon> HoatDongTaiKhoanCon { get; set; } = [];

    // --- Top 10 ---
    public List<TongQuanHangTop> TopHangBanChay { get; set; } = [];

    public List<TongQuanHangTop> TopKhachHang { get; set; } = [];

    // --- Thống kê kho ---
    public TongQuanThongKeKho ThongKeKho { get; set; } = new();

    // --- Chuỗi hiển thị đã định dạng sẵn để binding không cần StringFormat ---
    public string DoanhThuHomNayHienThi => $"{DoanhThuHomNay:N0} đ";

    public string SoHoaDonHomNayHienThi => $"{SoHoaDonHomNay:N0}";

    public string DaThuHomNayHienThi => $"{DaThuHomNay:N0} đ";

    public string ConNoHomNayHienThi => $"{ConNoHomNay:N0} đ";

    public string SoDonHangHomNayHienThi => $"{SoDonHangHomNay:N0}";

    public string SoHoaDonChuaGhiNhanHienThi => $"{SoHoaDonChuaGhiNhan:N0}";

    public string TongTienHangChuaGhiNhanHienThi => $"{TongTienHangChuaGhiNhan:N0} đ";

    public string DoanhThuChuaGhiNhanHienThi => $"{DoanhThuChuaGhiNhan:N0} đ";

    public string DaThuChuaGhiNhanHienThi => $"{DaThuChuaGhiNhan:N0} đ";

    public string ConNoChuaGhiNhanHienThi => $"{ConNoChuaGhiNhan:N0} đ";

    public string TongDoanhThuKyChonHienThi => $"{TongDoanhThuKyChon:N0} đ";

    public string TongSoHoaDonKyChonHienThi => $"{TongSoHoaDonKyChon:N0} hóa đơn";

    public string ThoiGianHienThi { get; set; } = "";

    public static string NgayHienThi(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
