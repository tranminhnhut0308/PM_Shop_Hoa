using System;
using System.Collections.Generic;

namespace ShopHoa.Models;

/// <summary>
/// Yêu cầu hủy phiếu nhập hàng từ giao diện người dùng.
/// </summary>
public sealed class HuyPhieuNhapYeuCau
{
    public uint PhieuNhapId { get; set; }
    public string MaPhieuNhap { get; set; } = string.Empty;
    public string LyDoHuy { get; set; } = string.Empty;

    /// <summary>
    /// Phương án xử lý khoản tiền đã thanh toán (nếu DaThanhToan > 0):
    /// HOAN_TIEN_MAT = Nhà cung cấp hoàn trả tiền mặt về quỹ tiệm (tạo phiếu thu tiền mặt),
    /// HOAN_CHUYEN_KHOAN = Nhà cung cấp hoàn trả chuyển khoản về tài khoản tiệm (tạo phiếu thu chuyển khoản),
    /// TREO_CONG_NO = Nhà cung cấp giữ tiền làm khoản trả trước / trừ nợ sau (không tạo phiếu thu, số dư công nợ NCC tự giảm).
    /// </summary>
    public string PhuongAnTien { get; set; } = "HOAN_TIEN_MAT";

    public decimal SoTienHoan { get; set; }
    public uint? NguoiHuyId { get; set; }
    public string TenNguoiHuy { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu khôi phục phiếu nhập hàng đã bị hủy.
/// </summary>
public sealed class KhoiPhucPhieuNhapYeuCau
{
    public uint PhieuNhapId { get; set; }
    public string MaPhieuNhap { get; set; } = string.Empty;
    public string LyDoKhoiPhuc { get; set; } = string.Empty;
    public uint? NguoiKhoiPhucId { get; set; }
    public string TenNguoiKhoiPhuc { get; set; } = string.Empty;
}

/// <summary>
/// Mặt hàng kiểm tra tồn kho khi chuẩn bị hủy phiếu nhập.
/// </summary>
public sealed class MatHangKiemTraHuy
{
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = string.Empty;
    public string TenSanPham { get; set; } = string.Empty;
    public string DonViTinh { get; set; } = string.Empty;
    public decimal SoLuongNhap { get; set; }
    public decimal TonKhoHienTai { get; set; }
    public decimal TonKhoSauKhiHuy => TonKhoHienTai - SoLuongNhap;
    public bool DuTonKho => TonKhoHienTai >= SoLuongNhap;
    public decimal SoLuongThieu => DuTonKho ? 0m : (SoLuongNhap - TonKhoHienTai);
}

/// <summary>
/// Kết quả thẩm định điều kiện hủy phiếu nhập trước khi cho phép xác nhận.
/// </summary>
public sealed class KiemTraHuyPhieuNhapResult
{
    public uint PhieuNhapId { get; set; }
    public string MaPhieuNhap { get; set; } = string.Empty;
    public string TenNhaCungCap { get; set; } = string.Empty;
    public decimal TongTienNhap { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public string PhuongThucThanhToan { get; set; } = "TIEN_MAT";

    public bool CoTheHuy { get; set; } = true;
    public string? LyDoKhongTheHuy { get; set; }

    public List<MatHangKiemTraHuy> MatHangs { get; set; } = [];

    /// <summary>Có ít nhất một mặt hàng bị thiếu tồn kho (đã bán hoặc xuất sau khi nhập).</summary>
    public bool CoMatHangThieuTonKho => MatHangs.Exists(m => !m.DuTonKho);

    public int SoMatHangThieuTonKho => MatHangs.Count(m => !m.DuTonKho);
}

/// <summary>
/// Bản ghi lưu vết kiểm toán bất biến khi Hủy hoặc Khôi phục phiếu nhập.
/// </summary>
public sealed class LichSuHuyPhieuNhap
{
    public ulong Id { get; set; }
    public uint PhieuNhapId { get; set; }
    public string MaPhieuNhap { get; set; } = string.Empty;
    public uint? NhaCungCapId { get; set; }
    public string TenNhaCungCap { get; set; } = string.Empty;
    public decimal TongTienNhap { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal ConNo { get; set; }
    public decimal SoTienHoan { get; set; }
    public string? MaPhieuThuHoan { get; set; }

    /// <summary>Hành động: HUY | KHOI_PHUC | MUA_LAI</summary>
    public string HanhDong { get; set; } = "HUY";

    public string LyDo { get; set; } = string.Empty;
    public uint? NguoiThucHienId { get; set; }
    public string TenNguoiThucHien { get; set; } = string.Empty;
    public DateTime ThoiGian { get; set; } = DateTime.Now;
    public string? DuLieuSnapshot { get; set; }

    public string ThoiGianText => ThoiGian.ToString("dd/MM/yyyy HH:mm:ss");
    public string HanhDongHienThi => HanhDong switch
    {
        "HUY" => "Hủy phiếu",
        "KHOI_PHUC" => "Khôi phục phiếu",
        "MUA_LAI" => "Mua lại / Tạo mới",
        _ => HanhDong
    };
}
