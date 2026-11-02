using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Dòng hàng của giỏ hàng bán nháp (bản chụp thuần dữ liệu, lưu dạng JSON).
/// Mỗi giỏ hàng có danh sách dòng riêng, không dùng chung đối tượng giữa các giỏ hàng.
/// </summary>
public sealed class SaleCartDraftLine
{
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string? DonViTinh { get; set; }
    public uint? DonViTinhId { get; set; }
    public decimal GiaVon { get; set; }
    public decimal GiaBan { get; set; }
    public decimal DonGia { get; set; }
    public decimal TonKho { get; set; }
    public decimal SoLuong { get; set; } = 1;
    public decimal GiamGia { get; set; }
    public decimal ThanhTien => Math.Max(0m, SoLuong * DonGia - GiamGia);
}

/// <summary>
/// Toàn bộ trạng thái của một giỏ hàng bán nháp. Được serialize thành JSON và lưu
/// trong bảng don_ban_nhap để khôi phục khi chuyển trang / mở lại phần mềm.
/// </summary>
public sealed class SaleCartDraftState
{
    public string CartId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Số thứ tự hiển thị "Đơn N".</summary>
    public int SoThuTu { get; set; } = 1;

    public uint? KhachHangId { get; set; }
    public string? MaKhachHang { get; set; }
    public string TenKhachHang { get; set; } = "Khách lẻ";
    public string SoDienThoai { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public bool CanGiaoHang { get; set; }
    public bool InKemSdtDiaChi { get; set; }

    public string GhiChu { get; set; } = "";
    public string DiscountMode { get; set; } = "Số tiền";
    public decimal DiscountInput { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TienKhachDua { get; set; }
    public string PhuongThucThanhToan { get; set; } = "Tiền mặt";
    public decimal Subtotal { get; set; }
    public decimal TotalAmount { get; set; }

    public uint? HoaDonGocBiHuyId { get; set; }
    public string? MaHoaDonGocBiHuy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public DateTime NgayTao
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }

    public DateTime NgayCapNhat
    {
        get => UpdatedAt;
        set => UpdatedAt = value;
    }

    public List<SaleCartDraftLine> Lines { get; set; } = new();

    /// <summary>Giỏ hàng trống (chưa có hàng / chưa có thông tin riêng) thì không cần ghi xuống database.</summary>
    public bool IsEmpty =>
        Lines.Count == 0
        && (KhachHangId == null || KhachHangId == 0)
        && (string.IsNullOrWhiteSpace(TenKhachHang) || TenKhachHang == "Khách lẻ")
        && string.IsNullOrWhiteSpace(SoDienThoai)
        && string.IsNullOrWhiteSpace(DiaChi)
        && string.IsNullOrWhiteSpace(GhiChu)
        && DiscountInput == 0
        && TienKhachDua == 0
        && HoaDonGocBiHuyId == null;
}

/// <summary>Bản ghi giỏ hàng nháp đọc từ database.</summary>
public sealed class SaleCartDraftRecord
{
    public string CartId { get; set; } = "";
    public int SoThuTu { get; set; }
    public string DuLieu { get; set; } = "";
    public int PhienBan { get; set; }
}

/// <summary>Một thẻ giỏ hàng trên thanh quản lý nhiều đơn bán hàng.</summary>
public sealed partial class SaleCartTab : ObservableObject
{
    public SaleCartDraftState State { get; set; } = new();

    public string CartId => State.CartId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieuDe))]
    private string tenKhachHang = "Khách lẻ";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieuDe))]
    private int soThuTu = 1;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoTaNgan))]
    [NotifyPropertyChangedFor(nameof(CoSanPham))]
    private int soLuongSanPham;

    public bool CoSanPham => SoLuongSanPham > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoTaNgan))]
    private decimal tongTien;

    /// <summary>Ví dụ: "Đơn 1" hoặc "Đơn 1 – Chị Lan".</summary>
    public string TieuDe =>
        $"Đơn {SoThuTu}"
        + (string.IsNullOrWhiteSpace(TenKhachHang) || TenKhachHang == "Khách lẻ" ? "" : $" – {TenKhachHang}");

    /// <summary>Ví dụ: "3 SP • 150.000 đ" hoặc "0 SP • 0 đ".</summary>
    public string MoTaNgan =>
        $"{SoLuongSanPham} SP • {TongTien:N0} đ";

    public void CapNhatTuState()
    {
        SoThuTu = State.SoThuTu;
        TenKhachHang = string.IsNullOrWhiteSpace(State.TenKhachHang) ? "Khách lẻ" : State.TenKhachHang;
        var totalQty = (int)Math.Round(State.Lines.Sum(l => l.SoLuong));
        SoLuongSanPham = totalQty > 0 ? totalQty : State.Lines.Count;
        var subtotal = 0m;
        foreach (var line in State.Lines)
            subtotal += line.ThanhTien;

        var discount = State.DiscountMode == "Phần trăm"
            ? subtotal * Math.Clamp(State.DiscountInput, 0, 100) / 100
            : Math.Clamp(State.DiscountInput, 0, subtotal);

        TongTien = Math.Max(0m, subtotal - discount);
        State.Subtotal = subtotal;
        State.TotalAmount = TongTien;
    }
}
