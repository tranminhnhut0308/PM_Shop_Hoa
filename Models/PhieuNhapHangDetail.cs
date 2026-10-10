using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Dữ liệu chi tiết đầy đủ của một phiếu nhập hàng, dùng cho cửa sổ "Xem chi tiết".
/// Header đọc từ bảng phieu_nhap_hang, các dòng hàng đọc từ
/// chi_tiet_phieu_nhap_hang (nối san_pham để lấy đơn vị tính).
/// Chỉ đọc, không sửa/xóa.
/// </summary>
public sealed class PhieuNhapHangDetail
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Khoá chính phieu_nhap_hang.id.</summary>
    public uint PhieuNhapId { get; set; }

    public string MaPhieuNhap { get; set; } = "";

    public DateTime ThoiGianNhap { get; set; }

    public uint? NhaCungCapId { get; set; }

    public string? MaNhaCungCap { get; set; }

    public string? TenNhaCungCap { get; set; }

    public string? TenNhanVien { get; set; }

    /// <summary>Giảm giá cấp phiếu (cột giam_gia). Hiện nghiệp vụ luôn lưu 0.</summary>
    public decimal GiamGia { get; set; }

    /// <summary>Chi phí khác cộng thêm vào tiền nhập (cột chi_phi_khac).</summary>
    public decimal ChiPhiKhac { get; set; }

    public decimal DaThanhToan { get; set; }

    public string TrangThai { get; set; } = "DA_NHAP_HANG";

    public string? GhiChu { get; set; }

    public string? SoHoaDonDauVao { get; set; }

    public string? MaDatHangNhap { get; set; }

    public List<PhieuNhapHangDetailItem> Items { get; set; } = [];

    // ============================================================
    // TỔNG TIỀN: tính lại từ chính các dòng chi tiết đã đọc,
    // đúng quy tắc nghiệp vụ hiện tại (tổng thành tiền từng dòng).
    // ============================================================

    public decimal TongTienHang => Items.Sum(item => item.ThanhTien);

    public decimal TongTienNhap => TongTienHang + ChiPhiKhac;

    public decimal ConNo => Math.Max(0m, TongTienNhap - DaThanhToan);

    public bool IsLuuTam => string.Equals(TrangThai, "LUU_TAM", StringComparison.OrdinalIgnoreCase);

    public string TrangThaiHienThi => IsLuuTam ? "Lưu tạm" : "Đã nhập hàng";

    public string TieuDe => $"Chi tiết phiếu nhập {MaPhieuNhap}";

    public string ThoiGianNhapText =>
        ThoiGianNhap.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string MaNhaCungCapHienThi =>
        string.IsNullOrWhiteSpace(MaNhaCungCap) ? "—" : MaNhaCungCap!;

    public string TenNhaCungCapHienThi =>
        string.IsNullOrWhiteSpace(TenNhaCungCap) ? "Không có nhà cung cấp" : TenNhaCungCap!;

    public string TenNhanVienHienThi =>
        string.IsNullOrWhiteSpace(TenNhanVien) ? "—" : TenNhanVien!;

    public bool CoSoHoaDon => !string.IsNullOrWhiteSpace(SoHoaDonDauVao);

    public bool CoMaDatHang => !string.IsNullOrWhiteSpace(MaDatHangNhap);

    public bool CoGhiChu => !string.IsNullOrWhiteSpace(GhiChu);

    public bool CoHangHoa => Items.Count > 0;

    public bool HasGiamGia => GiamGia != 0m;

    public bool HasChiPhiKhac => ChiPhiKhac != 0m;

    public string GiamGiaText => GiamGia.ToString("N0", Vi) + " đ";

    public string ChiPhiKhacText => ChiPhiKhac.ToString("N0", Vi) + " đ";

    public string TongTienHangText => TongTienHang.ToString("N0", Vi) + " đ";

    public string TongTienNhapText => TongTienNhap.ToString("N0", Vi) + " đ";

    public string DaThanhToanText => DaThanhToan.ToString("N0", Vi) + " đ";

    public string ConNoText => ConNo.ToString("N0", Vi) + " đ";
}

/// <summary>Một dòng hàng hóa trong phiếu nhập (chi_tiet_phieu_nhap_hang).</summary>
public sealed class PhieuNhapHangDetailItem
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public int Index { get; set; }

    public string MaSanPham { get; set; } = "";

    public string TenSanPham { get; set; } = "";

    public string? DonViTinh { get; set; }

    public decimal SoLuong { get; set; }

    public decimal DonGiaNhap { get; set; }

    public decimal GiamGia { get; set; }

    public decimal ThanhTien { get; set; }

    public string? GhiChu { get; set; }

    public bool CoGiamGia => GiamGia != 0m;

    public string DonViTinhHienThi =>
        string.IsNullOrWhiteSpace(DonViTinh) ? "—" : DonViTinh!;

    public string SoLuongText => SoLuong.ToString("0.###", Vi);

    public string DonGiaNhapText => DonGiaNhap.ToString("N0", Vi);

    public string GiamGiaText => GiamGia.ToString("N0", Vi);

    public string ThanhTienText => ThanhTien.ToString("N0", Vi);
}
