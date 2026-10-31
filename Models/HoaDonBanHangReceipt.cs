namespace ShopHoa.Models;

public sealed class HoaDonBanHangReceipt
{
    /// <summary>Khoá chính của hóa đơn trong database (0 nghĩa là chưa lưu).</summary>
    public uint HoaDonId { get; set; }

    public string MaHoaDon { get; set; } = "";
    public bool LaHoaDonTongHop { get; set; }
    public string TrangThaiHoaDon { get; set; } = "HOAN_THANH";

    public bool LaHoaDonDaHuy =>
        string.Equals(TrangThaiHoaDon, "DA_HUY", StringComparison.OrdinalIgnoreCase)
        || string.Equals(TrangThaiHoaDon, "HUY", StringComparison.OrdinalIgnoreCase);

    public DateTime ThoiGianBan { get; set; }
    public string TenKhachHang { get; set; } = "Khách lẻ";
    public string TenNhanVien { get; set; } = "";
    public List<HoaDonBanHangReceiptItem> Items { get; set; } = [];
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal KhachCanTra { get; set; }
    public decimal KhachDaThanhToan { get; set; }
    public decimal TienKhachDua { get; set; }

    public decimal TienThoi { get; set; }
    public decimal ConNo { get; set; }
    public string TrangThaiThanhToan { get; set; } = "";

    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public bool InKemThongTin { get; set; }

    public bool HienThiThongTinLienHe => InKemThongTin && (!string.IsNullOrWhiteSpace(SoDienThoai) || !string.IsNullOrWhiteSpace(DiaChi));
    public string SoDienThoaiHienThi => string.IsNullOrWhiteSpace(SoDienThoai) ? "Chưa có" : SoDienThoai;
    public string DiaChiHienThi => string.IsNullOrWhiteSpace(DiaChi) ? "Chưa có" : DiaChi;

    /// <summary>Ghi chú riêng của hóa đơn (yêu cầu riêng của khách...).</summary>
    public string? GhiChu { get; set; }

    public bool CoGhiChu => !string.IsNullOrWhiteSpace(GhiChu);

    /// <summary>
    /// Mã phiếu Thu trong sổ quỹ đã sinh kèm hóa đơn này (ví dụ "PT0004").
    /// Rỗng khi khách chưa trả đồng nào nên không có dòng tiền vào quỹ.
    /// </summary>
    public string MaPhieuThu { get; set; } = "";

    /// <summary>Tiền thối đã lưu xuống database (cột thu_khac).</summary>
    public decimal ThuKhac { get; set; }

    /// <summary>
    /// Các cảnh báo phát sinh khi lưu đơn (bán vượt tồn kho, sản phẩm chưa có
    /// số tồn...). Hóa đơn vẫn được lưu nhưng người dùng phải được báo rõ.
    /// </summary>
    public List<string> CanhBao { get; set; } = [];

    public bool CoCanhBao => CanhBao.Count > 0;

    /// <summary>
    /// Thông tin tiệm in lên đầu hóa đơn, lấy từ
    /// <see cref="Services.StoreInfoService"/>. Luôn có giá trị (không null) để
    /// hóa đơn không bao giờ trắng phần đầu, kể cả khi đọc lỗi.
    /// </summary>
    public ThongTinTiem ThongTinTiem { get; set; } = new();

    // ============================================================
    // THÔNG TIN HỦY HÓA ĐƠN & LIÊN KẾT HÓA ĐƠN GỐC / THAY THẾ
    // ============================================================

    public string? LyDoHuy { get; set; }
    public uint? NguoiHuyId { get; set; }
    public string? TenNguoiHuy { get; set; }
    public DateTime? ThoiGianHuy { get; set; }
    public string? TrangThaiGiaoHangKhiHuy { get; set; }
    public bool DaHoanKhoKhiHuy { get; set; }
    public string? PhuongAnTienKhiHuy { get; set; }
    public decimal SoTienHoanKhiHuy { get; set; }
    public string? MaPhieuChiHoan { get; set; }
    public uint? HoaDonGocId { get; set; }
    public string? MaHoaDonGoc { get; set; }
    public uint? HoaDonThayTheId { get; set; }
    public string? MaHoaDonThayThe { get; set; }

    public bool CoHoaDonGoc => !string.IsNullOrWhiteSpace(MaHoaDonGoc);
    public bool CoHoaDonThayThe => !string.IsNullOrWhiteSpace(MaHoaDonThayThe);
    public bool CoPhieuChiHoan => !string.IsNullOrWhiteSpace(MaPhieuChiHoan);

    public string TenNguoiHuyHienThi =>
        string.IsNullOrWhiteSpace(TenNguoiHuy) ? "Hệ thống" : TenNguoiHuy;

    public string ThoiGianHuyText =>
        ThoiGianHuy.HasValue ? ThoiGianHuy.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa ghi nhận";

    public string TrangThaiGiaoHangKhiHuyHienThi => TrangThaiGiaoHangKhiHuy switch
    {
        "CHUA_GIAO" => "Chưa xuất hàng (Đã hoàn lại tồn kho)",
        "DA_GIAO_DA_NHAN_LAI" => "Hàng đã giao - Đã nhận lại hàng trả về (Đã cộng lại kho)",
        "DA_GIAO_KHACH_GIU" => "Hàng đã giao - Khách vẫn giữ hàng (Không cộng lại kho)",
        _ => DaHoanKhoKhiHuy ? "Đã hoàn lại tồn kho" : "Không hoàn lại tồn kho"
    };

    public string PhuongAnTienKhiHuyHienThi => PhuongAnTienKhiHuy switch
    {
        "HOAN_TIEN" => !string.IsNullOrWhiteSpace(MaPhieuChiHoan)
            ? $"Hoàn tiền cho khách ({SoTienHoanKhiHuy:N0} đ — Phiếu chi {MaPhieuChiHoan})"
            : $"Hoàn tiền cho khách ({SoTienHoanKhiHuy:N0} đ)",
        "CHUYEN_TRA_TRUOC" => $"Chuyển {KhachDaThanhToan:N0} đ thành khoản khách trả trước / cấn trừ công nợ",
        _ => "Không phát sinh hoàn tiền (Hóa đơn chưa thu tiền)"
    };

    /// <summary>Danh sách các giao dịch sổ quỹ / công nợ / kho liên quan đến hóa đơn.</summary>
    public List<GiaoDichLienQuanHoaDonItem> GiaoDichLienQuans { get; set; } = [];

    public bool CoGiaoDichLienQuan => GiaoDichLienQuans.Count > 0;
}

public sealed class HoaDonBanHangReceiptItem
{
    public string TenSanPham { get; set; } = "";
    public string DonViTinh { get; set; } = "Bó";
    public decimal SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }
}

public sealed class GiaoDichLienQuanHoaDonItem
{
    public string LoaiGiaoDich { get; set; } = "";
    public string MaGiaoDich { get; set; } = "";
    public decimal SoTien { get; set; }
    public string PhuongThuc { get; set; } = "";
    public DateTime ThoiGian { get; set; }
    public string NoiDung { get; set; } = "";

    public string ThoiGianText => ThoiGian.ToString("dd/MM/yyyy HH:mm");
    public string SoTienText => SoTien > 0 ? $"{SoTien:N0} đ" : "";
    public string HienThiTomTat =>
        string.IsNullOrWhiteSpace(SoTienText)
            ? $"[{LoaiGiaoDich}] {MaGiaoDich} ({ThoiGianText}): {NoiDung}"
            : $"[{LoaiGiaoDich}] {MaGiaoDich} — {SoTienText} ({ThoiGianText}): {NoiDung}";
}
