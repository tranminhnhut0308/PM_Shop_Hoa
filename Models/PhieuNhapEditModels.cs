using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Một dòng hàng hóa đang được chỉnh sửa trong phiếu nhập.
/// </summary>
public sealed partial class PhieuNhapItemEditModel : ObservableObject
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public uint Id { get; set; } // id trong chi_tiet_phieu_nhap_hang nếu là dòng cũ, 0 nếu thêm mới
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public uint? DonViTinhId { get; set; }

    [ObservableProperty]
    private string _donViTinh = "";

    [ObservableProperty]
    private decimal _soLuong = 1;

    [ObservableProperty]
    private decimal _donGiaNhap;

    [ObservableProperty]
    private decimal _giamGia;

    [ObservableProperty]
    private string? _ghiChu;

    public decimal ThanhTien => Math.Max(0m, (SoLuong * DonGiaNhap) - GiamGia);

    // Lưu lại giá trị gốc lúc mở để tính delta tồn kho và so sánh thay đổi
    public decimal SoLuongBanDau { get; set; }
    public decimal DonGiaNhapBanDau { get; set; }
    public decimal GiamGiaBanDau { get; set; }

    public string SoLuongText => SoLuong.ToString("0.###", Vi);
    public string DonGiaNhapText => DonGiaNhap.ToString("N0", Vi) + " đ";
    public string ThanhTienText => ThanhTien.ToString("N0", Vi) + " đ";

    partial void OnSoLuongChanged(decimal value)
    {
        if (value < 0)
            SoLuong = 0;
        OnPropertyChanged(nameof(ThanhTien));
        OnPropertyChanged(nameof(ThanhTienText));
        OnPropertyChanged(nameof(SoLuongText));
    }

    partial void OnDonGiaNhapChanged(decimal value)
    {
        if (value < 0)
            DonGiaNhap = 0;
        OnPropertyChanged(nameof(ThanhTien));
        OnPropertyChanged(nameof(ThanhTienText));
        OnPropertyChanged(nameof(DonGiaNhapText));
    }

    partial void OnGiamGiaChanged(decimal value)
    {
        if (value < 0)
            GiamGia = 0;
        OnPropertyChanged(nameof(ThanhTien));
        OnPropertyChanged(nameof(ThanhTienText));
    }
}

/// <summary>
/// Dữ liệu yêu cầu chỉnh sửa phiếu nhập hàng.
/// </summary>
public sealed class PhieuNhapEditRequest
{
    public uint PhieuNhapId { get; set; }
    public string MaPhieuNhap { get; set; } = "";
    public uint ChiNhanhId { get; set; }
    public DateTime ThoiGianNhap { get; set; } = DateTime.Now;
    public uint? NhaCungCapId { get; set; }
    public string? MaNhaCungCap { get; set; }
    public string? TenNhaCungCap { get; set; }
    public string? SoHoaDonDauVao { get; set; }
    public string? MaDatHangNhap { get; set; }
    public string? GhiChu { get; set; }
    public decimal ChiPhiKhac { get; set; }
    public decimal GiamGia { get; set; }
    public decimal DaThanhToan { get; set; }
    public string PhuongThucThanhToan { get; set; } = "TIEN_MAT";
    public string LyDoChinhSua { get; set; } = "";
    public List<PhieuNhapItemEditModel> Items { get; set; } = [];

    public decimal TongTienHang => Items.Sum(i => i.ThanhTien);
    public decimal TongTienNhap => Math.Max(0m, TongTienHang - GiamGia + ChiPhiKhac);
    public decimal ConNo => Math.Max(0m, TongTienNhap - DaThanhToan);
}

/// <summary>
/// Bản ghi kiểm toán lịch sử thay đổi phiếu nhập (bảng lich_su_chinh_sua_phieu_nhap).
/// </summary>
public sealed class LichSuChinhSuaPhieuNhap
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public ulong Id { get; set; }
    public uint PhieuNhapId { get; set; }
    public string MaPhieuNhap { get; set; } = "";
    public DateTime ThoiGian { get; set; }
    public uint? NguoiThucHienId { get; set; }
    public string TenNguoiThucHien { get; set; } = "";
    public string LyDo { get; set; } = "";
    public decimal TongTienTruoc { get; set; }
    public decimal TongTienSau { get; set; }
    public decimal ChiPhiKhacTruoc { get; set; }
    public decimal ChiPhiKhacSau { get; set; }
    public decimal GiamGiaTruoc { get; set; }
    public decimal GiamGiaSau { get; set; }
    public decimal DaThanhToanTruoc { get; set; }
    public decimal DaThanhToanSau { get; set; }
    public decimal ConNoTruoc { get; set; }
    public decimal ConNoSau { get; set; }
    public uint? NhaCungCapIdTruoc { get; set; }
    public uint? NhaCungCapIdSau { get; set; }
    public string? TenNhaCungCapTruoc { get; set; }
    public string? TenNhaCungCapSau { get; set; }
    public DateTime? ThoiGianNhapTruoc { get; set; }
    public DateTime? ThoiGianNhapSau { get; set; }
    public string? DuLieuTruocJson { get; set; }
    public string? DuLieuSauJson { get; set; }
    public string? ChiTietThayDoi { get; set; }

    // Helpers cho Binding UI
    public string ThoiGianText => ThoiGian.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    public string TongTienTruocText => TongTienTruoc.ToString("N0", Vi) + " đ";
    public string TongTienSauText => TongTienSau.ToString("N0", Vi) + " đ";
    public string ConNoTruocText => ConNoTruoc.ToString("N0", Vi) + " đ";
    public string ConNoSauText => ConNoSau.ToString("N0", Vi) + " đ";
    public decimal ChenhLechTien => TongTienSau - TongTienTruoc;
    public string ChenhLechTienText => (ChenhLechTien >= 0 ? "+" : "") + ChenhLechTien.ToString("N0", Vi) + " đ";
    public bool HasChiTietThayDoi => !string.IsNullOrWhiteSpace(ChiTietThayDoi);
}

/// <summary>
/// Lựa chọn nhà cung cấp dùng cho AutoCompleteBox / ComboBox.
/// </summary>
public sealed class SupplierOption
{
    public uint Id { get; set; }
    public string MaNhaCungCap { get; set; } = "";
    public string TenNhaCungCap { get; set; } = "";
    public string? SoDienThoai { get; set; }

    public string DisplayText =>
        string.IsNullOrWhiteSpace(SoDienThoai)
            ? $"{TenNhaCungCap} ({MaNhaCungCap})"
            : $"{TenNhaCungCap} ({MaNhaCungCap}) - {SoDienThoai}";
}
