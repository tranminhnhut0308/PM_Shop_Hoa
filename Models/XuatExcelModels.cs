using System;
using System.Collections.Generic;

namespace ShopHoa.Models;

/// <summary>
/// Loại báo cáo xuất Excel theo Issue #63.
/// </summary>
public enum LoaiBaoCaoXuat
{
    /// <summary>Báo cáo tổng quan: Mỗi hóa đơn/phiếu nhập 1 dòng.</summary>
    TongQuan = 1,

    /// <summary>Báo cáo chi tiết: Mỗi mặt hàng trong chứng từ 1 dòng.</summary>
    ChiTiet = 2,

    /// <summary>Xuất đầy đủ: 3 sheet gồm Tổng quan + Chi tiết + Tổng hợp theo sản phẩm.</summary>
    DayDu = 3
}

/// <summary>
/// Phạm vi lọc dữ liệu khi xuất Excel theo Issue #63.
/// </summary>
public enum PhamViLocXuat
{
    /// <summary>Xuất theo các tiêu chí bộ lọc đang chọn trên màn hình.</summary>
    TheoBoLocHienTai = 1,

    /// <summary>Xuất toàn bộ dữ liệu trong khoảng thời gian đã chọn.</summary>
    ToanBoKhoangThoiGian = 2
}

/// <summary>
/// Thông tin yêu cầu xuất Excel trả về từ dialog lựa chọn.
/// </summary>
public sealed class XuatExcelYeuCau
{
    public LoaiBaoCaoXuat LoaiBaoCao { get; set; } = LoaiBaoCaoXuat.DayDu;
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public PhamViLocXuat PhamViLoc { get; set; } = PhamViLocXuat.TheoBoLocHienTai;
    public string MoTaKhoangThoiGian { get; set; } = "";
    public string MoTaBoLoc { get; set; } = "";
}

// =========================================================================
// DTOs XUẤT EXCEL TRANG HÓA ĐƠN
// =========================================================================

public sealed class HoaDonTongQuanXuatDong
{
    public int Stt { get; set; }
    public uint Id { get; set; }
    public string MaHoaDon { get; set; } = "";
    public DateTime NgayLapHoaDon { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public int TongSoMatHang { get; set; }
    public decimal TongSoLuongSanPham { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal TongThanhToan { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public string PhuongThucThanhToan { get; set; } = "";
    public string TrangThaiThanhToan { get; set; } = "";
    public string TrangThaiHoaDon { get; set; } = "";
    public string NguoiTao { get; set; } = "";
    public string GhiChu { get; set; } = "";
    public bool LaHoaDonDaHuy { get; set; }
}

public sealed class HoaDonChiTietXuatDong
{
    public int Stt { get; set; }
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = "";
    public DateTime NgayLapHoaDon { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string MaHang { get; set; } = "";
    public string TenHang { get; set; } = "";
    public string NhomHang { get; set; } = "";
    public string DonViTinh { get; set; } = "";
    public decimal SoLuong { get; set; }
    public decimal DonGiaBan { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThanhTien { get; set; }
    public string GhiChu { get; set; } = "";
    public bool LaHoaDonDaHuy { get; set; }
}

public sealed class HoaDonTongHopSanPhamDong
{
    public int Stt { get; set; }
    public string MaHang { get; set; } = "";
    public string TenHang { get; set; } = "";
    public string NhomHang { get; set; } = "";
    public string DonViTinh { get; set; } = "";
    public decimal TongSoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal DoanhThuSauGiamGia { get; set; }
}

// =========================================================================
// DTOs XUẤT EXCEL TRANG PHIẾU NHẬP HÀNG
// =========================================================================

public sealed class PhieuNhapTongQuanXuatDong
{
    public int Stt { get; set; }
    public uint Id { get; set; }
    public string MaPhieuNhap { get; set; } = "";
    public DateTime NgayNhapHang { get; set; }
    public DateTime? NgayHoaDonDauVao { get; set; }
    public string SoHoaDonDauVao { get; set; } = "";
    public string MaNhaCungCap { get; set; } = "";
    public string TenNhaCungCap { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public int TongSoMatHang { get; set; }
    public decimal TongSoLuongNhap { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ChiPhiKhac { get; set; }
    public decimal TongThanhToan { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public string PhuongThucThanhToan { get; set; } = "";
    public string TrangThaiThanhToan { get; set; } = "";
    public string TrangThaiPhieuNhap { get; set; } = "";
    public string NguoiTao { get; set; } = "";
    public string GhiChu { get; set; } = "";
    public bool LaPhieuDaHuy { get; set; }
}

public sealed class PhieuNhapChiTietXuatDong
{
    public int Stt { get; set; }
    public uint PhieuNhapId { get; set; }
    public string MaPhieuNhap { get; set; } = "";
    public DateTime NgayNhapHang { get; set; }
    public string MaNhaCungCap { get; set; } = "";
    public string TenNhaCungCap { get; set; } = "";
    public string MaHang { get; set; } = "";
    public string TenHang { get; set; } = "";
    public string NhomHang { get; set; } = "";
    public string DonViTinhNhap { get; set; } = "";
    public decimal HeSoQuyDoi { get; set; } = 1;
    public decimal SoLuongNhap { get; set; }
    public decimal SoLuongQuyDoi { get; set; }
    public decimal DonGiaNhap { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThanhTien { get; set; }
    public bool LaPhieuDaHuy { get; set; }
}

public sealed class PhieuNhapTongHopSanPhamDong
{
    public int Stt { get; set; }
    public string MaHang { get; set; } = "";
    public string TenHang { get; set; } = "";
    public string NhomHang { get; set; } = "";
    public string DonViTinh { get; set; } = "";
    public decimal TongSoLuongNhap { get; set; }
    public decimal TongTienNhap { get; set; }
}
