using System;
using System.Collections.Generic;

namespace ShopHoa.Models;

public enum TrangThaiGiaoHangKhiHuy
{
    /// <summary>Hóa đơn chưa xuất/giao hàng cho khách -> hoàn tác số lượng đã trừ kho.</summary>
    ChuaGiaoHang = 0,

    /// <summary>Hàng đã giao nhưng khách đã trả lại về kho -> cộng lại tồn kho.</summary>
    DaGiaoDaNhanLaiHang = 1,

    /// <summary>Hàng đã giao và khách vẫn đang giữ (chưa trả hàng) -> KHÔNG cộng lại kho.</summary>
    DaGiaoKhachVanGiuHang = 2
}

public enum PhuongAnXuLyTienKhiHuy
{
    /// <summary>Hóa đơn chưa thu tiền (KhachDaThanhToan = 0).</summary>
    KhongCo = 0,

    /// <summary>Hoàn tiền trực tiếp cho khách -> tạo phiếu chi (PC) trong sổ quỹ.</summary>
    HoanTienChoKhach = 1,

    /// <summary>Chuyển số tiền khách đã trả thành khoản trả trước / cấn trừ công nợ khách hàng.</summary>
    ChuyenKhoanKhachTraTruoc = 2
}

public enum CheDoHuyHoaDon
{
    /// <summary>Chỉ hủy hóa đơn, không mở đơn nháp mới.</summary>
    ChiHuy = 0,

    /// <summary>Hủy hóa đơn cũ và sao chép thông tin sang đơn nháp tại trang Bán hàng.</summary>
    HuyVaTaoHoaDonTuongTu = 1
}

public sealed class ChiTietSanPhamBanSaoHoaDon
{
    public uint Id { get; set; }
    public uint? SanPhamId { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string DonViTinh { get; set; } = "Bó";
    public decimal SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThanhTien { get; set; }
    public decimal GiaVon { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class PhieuQuyLienQuanHoaDonRow
{
    public uint Id { get; set; }
    public string MaPhieuChi { get; set; } = "";
    public string LoaiPhieu { get; set; } = "";
    public decimal SoTien { get; set; }
    public string PhuongThuc { get; set; } = "";
    public string NoiDung { get; set; } = "";
    public string? GhiChu { get; set; }
    public DateTime ThoiGianChi { get; set; }
    public string TrangThai { get; set; } = "";
}

public sealed class ThongTinXacNhanHuyHoaDon
{
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = "";
    public uint ChiNhanhId { get; set; } = 1;
    public uint? KhachHangId { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "Khách lẻ";
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public bool InKemThongTin { get; set; }
    public DateTime ThoiGianBan { get; set; }

    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThuKhac { get; set; }
    public decimal KhachCanTra { get; set; }
    public decimal KhachDaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public bool CongNoDaGhiNhan { get; set; } = true;

    public string TrangThaiThanhToan { get; set; } = "";
    public string TrangThaiHoaDon { get; set; } = "";
    public string? GhiChu { get; set; }

    public bool DaGiaoHang { get; set; }
    public string TrangThaiGiaoHangMoTa { get; set; } = "Đã xuất kho khi bán hàng";

    public List<ChiTietSanPhamBanSaoHoaDon> Items { get; set; } = [];
    public List<PhieuQuyLienQuanHoaDonRow> PhieuThus { get; set; } = [];

    public bool LaKhachCoHoSo => KhachHangId is > 0;
    public bool CoTienDaThanhToan => KhachDaThanhToan > 0;
    public bool CoCongNoConLai => ConNo > 0;

    public string TrangThaiThanhToanGiaoHangHienThi
    {
        get
        {
            var ttThanhToan = ConNo <= 0
                ? "Đã thanh toán đủ"
                : (KhachDaThanhToan > 0 ? $"Thanh toán một phần (Đã thu {KhachDaThanhToan:N0} đ)" : "Chưa thanh toán (Công nợ 100%)");
            return $"{ttThanhToan} • {TrangThaiGiaoHangMoTa}";
        }
    }
}

public sealed class HuyHoaDonRequest
{
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = "";
    public string LyDoHuy { get; set; } = "";

    public TrangThaiGiaoHangKhiHuy TrangThaiGiaoHang { get; set; } = TrangThaiGiaoHangKhiHuy.ChuaGiaoHang;

    /// <summary>
    /// Khi hàng đã giao, nhân viên phải xác nhận đã nhận lại hàng trả về kho trước khi cộng lại tồn kho.
    /// </summary>
    public bool XacNhanDaNhanLaiHangTraVe { get; set; }

    public PhuongAnXuLyTienKhiHuy PhuongAnTien { get; set; } = PhuongAnXuLyTienKhiHuy.HoanTienChoKhach;
    public string PhuongThucHoanTien { get; set; } = "TIEN_MAT";

    public CheDoHuyHoaDon CheDoHuy { get; set; } = CheDoHuyHoaDon.ChiHuy;
}

public sealed class HuyHoaDonResult
{
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = "";
    public CheDoHuyHoaDon CheDoHuy { get; set; }
    public bool DaHoanKho { get; set; }
    public decimal TongSoLuongHoanKho { get; set; }
    public decimal CongNoDaHoanTac { get; set; }
    public decimal SoTienHoanKhach { get; set; }
    public decimal SoTienChuyenTraTruoc { get; set; }
    public string? MaPhieuChiHoan { get; set; }
    public ThongTinXacNhanHuyHoaDon DuLieuSaoChep { get; set; } = new();
    public HoaDonBanHangReceipt? ReceiptDaHuy { get; set; }
}
