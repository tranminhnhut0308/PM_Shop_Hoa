using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Một dòng giao dịch hiển thị trong bảng Sổ quỹ.
/// Model này chỉ phục vụ giao diện (UI), chưa gắn với nghiệp vụ lưu phiếu.
/// </summary>
public partial class SoQuyGiaoDich : ObservableObject
{
    public string MaPhieu { get; set; } = "";

    public DateTime ThoiGian { get; set; } = DateTime.Now;

    /// <summary>true = phiếu thu, false = phiếu chi.</summary>
    public bool LaThu { get; set; }

    public string NguoiNopNhan { get; set; } = "";

    public decimal GiaTri { get; set; }

    /// <summary>Tên loại thu/chi lấy từ bảng nhom_chi_phi (rỗng nếu phiếu chưa gán loại).</summary>
    public string TenLoai { get; set; } = "";

    /// <summary>Mã phương thức quỹ (tiền mặt, chuyển khoản...).</summary>
    public string PhuongThuc { get; set; } = "";

    /// <summary>Mã trạng thái chứng từ trong database (HOAN_THANH / HUY).</summary>
    public string TrangThai { get; set; } = "";

    /// <summary>Ô chọn dòng.</summary>
    [ObservableProperty] private bool daChon;

    /// <summary>Sao yêu thích.</summary>
    [ObservableProperty] private bool yeuThich;

    public string LoaiHienThi => LaThu ? "Thu" : "Chi";

    public string MauLoai => LaThu ? "#1877F2" : "#D92D20";

    public string MauNenLoai => LaThu ? "#EAF1FF" : "#FEF3F2";

    /// <summary>Chỉ phiếu đã hủy mới bị loại khỏi số tiền của quỹ.</summary>
    public bool DaHuy => TrangThai.Equals("HUY", StringComparison.OrdinalIgnoreCase);

    /// <summary>Nhãn trạng thái hiển thị trên dòng phiếu.</summary>
    public string TrangThaiHienThi => TrangThai switch
    {
        "HOAN_THANH" => "Đã thanh toán",
        "HUY" => "Đã hủy",
        _ => string.IsNullOrWhiteSpace(TrangThai) ? "Chưa có" : TrangThai
    };

    /// <summary>Màu chữ của nhãn trạng thái: hủy thì nổi bật hơn.</summary>
    public string MauTrangThai => DaHuy ? "#D92D20" : "#667085";

    /// <summary>Nền nhãn trạng thái.</summary>
    public string MauNenTrangThai => DaHuy ? "#FEF3F2" : "#F2F4F7";

    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    partial void OnDaChonChanged(bool value) => OnPropertyChanged(nameof(DaChonHienThi));

    partial void OnYeuThichChanged(bool value) => OnPropertyChanged(nameof(BieuTuongYeuThich));

    public bool DaChonHienThi => DaChon;

    public string BieuTuongYeuThich => YeuThich ? "★" : "☆";
}
