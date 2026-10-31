using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Dòng hàng của phiếu nhập nháp (bản chụp thuần dữ liệu, lưu dạng JSON).
/// Mỗi phiếu có danh sách dòng riêng, không dùng chung đối tượng với phiếu khác.
/// </summary>
public sealed class PhieuNhapDraftLine
{
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string? DonViTinh { get; set; }
    public uint? DonViTinhId { get; set; }
    public decimal GiaVon { get; set; }
    public decimal GiaBan { get; set; }
    public decimal TonKho { get; set; }

    public decimal SoLuong { get; set; }
    public decimal DonGiaNhap { get; set; }
    public decimal GiamGia { get; set; }

    /// <summary>ĐVT người dùng chọn tại dòng (cho hàng chưa có ĐVT).</summary>
    public uint? DonViTinhChonId { get; set; }

    public bool ChoPhepChonDonViTinh { get; set; }
}

/// <summary>
/// Toàn bộ trạng thái của một phiếu nhập nháp. Được serialize thành JSON và lưu
/// trong bảng phieu_nhap_nhap để khôi phục khi chuyển trang / mở lại phần mềm.
/// </summary>
public sealed class PhieuNhapDraftState
{
    public string DraftId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Số thứ tự hiển thị "Phiếu nhập N".</summary>
    public int SoThuTu { get; set; } = 1;

    public uint NhaCungCapId { get; set; }
    public string? MaNhaCungCap { get; set; }
    public string? TenNhaCungCap { get; set; }

    public DateTime? NgayNhap { get; set; } = DateTime.Now;
    public string SoHoaDonDauVao { get; set; } = "";
    public string GhiChu { get; set; } = "";
    public decimal ChiPhiKhac { get; set; }
    public decimal DaThanhToan { get; set; }
    public string PhuongThuc { get; set; } = "Tiền mặt";

    public List<PhieuNhapDraftLine> Lines { get; set; } = new();

    /// <summary>Phiếu trống (chưa có hàng/NCC/thông tin) thì không cần ghi xuống database.</summary>
    public bool IsEmpty =>
        Lines.Count == 0
        && NhaCungCapId == 0
        && string.IsNullOrWhiteSpace(GhiChu)
        && string.IsNullOrWhiteSpace(SoHoaDonDauVao)
        && ChiPhiKhac == 0
        && DaThanhToan == 0;
}

/// <summary>Bản ghi nháp đọc từ database.</summary>
public sealed class PhieuNhapDraftRecord
{
    public string DraftId { get; set; } = "";
    public int SoThuTu { get; set; }
    public string DuLieu { get; set; } = "";
    public int PhienBan { get; set; }
}

/// <summary>Một thẻ phiếu nhập trên thanh quản lý nhiều phiếu.</summary>
public sealed partial class PhieuNhapTab : ObservableObject
{
    /// <summary>Trạng thái đã chụp gần nhất (dùng khi phiếu không phải phiếu đang mở).</summary>
    public PhieuNhapDraftState State { get; set; } = new();

    public string DraftId => State.DraftId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieuDe))]
    private string? tenNhaCungCap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieuDe))]
    private int soThuTu = 1;

    [ObservableProperty]
    private bool isActive;

    /// <summary>Ví dụ: "Phiếu nhập 2 – Nhà cung cấp B" hoặc "Phiếu nhập 3 – Chưa chọn NCC".</summary>
    public string TieuDe =>
        $"Phiếu nhập {SoThuTu} – "
        + (string.IsNullOrWhiteSpace(TenNhaCungCap) ? "Chưa chọn NCC" : TenNhaCungCap);

    public void CapNhatTuState()
    {
        SoThuTu = State.SoThuTu;
        TenNhaCungCap = State.TenNhaCungCap;
    }
}
