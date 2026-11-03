using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một bản ghi lịch sử chỉnh sửa hóa đơn bán hàng.
/// </summary>
public sealed class LichSuSuaHoaDon
{
    public uint Id { get; set; }
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = string.Empty;
    public uint? NguoiSuaId { get; set; }
    public string TenNguoiSua { get; set; } = string.Empty;
    public DateTime ThoiGianSua { get; set; }
    public string LyDoSua { get; set; } = string.Empty;
    public decimal TongTienTruoc { get; set; }
    public decimal TongTienSau { get; set; }
    public decimal ChenhLechTien => TongTienSau - TongTienTruoc;
    public string GhiChuThayDoi { get; set; } = string.Empty;
    public string? DuLieuTruocJson { get; set; }
    public string? DuLieuSauJson { get; set; }

    public string ThoiGianSuaText => ThoiGianSua.ToString("dd/MM/yyyy HH:mm:ss");
    public string TongTienTruocText => $"{TongTienTruoc:N0} đ";
    public string TongTienSauText => $"{TongTienSau:N0} đ";
    public string ChenhLechText
    {
        get
        {
            if (ChenhLechTien > 0)
                return $"+{ChenhLechTien:N0} đ";
            if (ChenhLechTien < 0)
                return $"{ChenhLechTien:N0} đ";
            return "0 đ";
        }
    }
}
