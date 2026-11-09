using System;

namespace ShopHoa.Models;

/// <summary>
/// Dòng lịch sử biến động số dư trả trước (thanh toán dư, cấn trừ hóa đơn/phiếu nhập, hoàn trả khi hủy).
/// ISSUE #97: Quản lý số dư còn lại trong công nợ.
/// </summary>
public sealed class LichSuTraTruocItem
{
    public ulong Id { get; set; }
    public string MaGiaoDich { get; set; } = string.Empty;
    public string LoaiGiaoDich { get; set; } = string.Empty;
    public string? LoaiChungTu { get; set; }
    public string? MaChungTu { get; set; }
    public decimal SoTien { get; set; }
    public decimal SoDuTruoc { get; set; }
    public decimal SoDuSau { get; set; }
    public string? PhuongThuc { get; set; }
    public DateTime ThoiGian { get; set; }
    public string? NguoiThucHien { get; set; }
    public string? GhiChu { get; set; }

    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    public string LoaiGiaoDichHienThi => LoaiGiaoDich switch
    {
        "THANH_TOAN_DU" => "Thanh toán dư",
        "CAN_TRU_HOA_DON" => "Cấn trừ hóa đơn",
        "CAN_TRU_PHIEU_NHAP" => "Cấn trừ phiếu nhập",
        "HOAN_TRA" => "Hoàn trả do hủy",
        _ => LoaiGiaoDich
    };

    public string PhuongThucHienThi => PhuongThuc switch
    {
        "TIEN_MAT" => "Tiền mặt",
        "CHUYEN_KHOAN" => "Chuyển khoản",
        "THE" => "Thẻ",
        "VI_DIEN_TU" => "Ví điện tử",
        "CAN_TRU" => "Cấn trừ",
        _ => PhuongThuc ?? "—"
    };
}
