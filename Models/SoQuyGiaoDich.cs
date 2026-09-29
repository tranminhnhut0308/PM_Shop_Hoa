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

    /// <summary>Ô chọn dòng.</summary>
    [ObservableProperty] private bool daChon;

    /// <summary>Sao yêu thích.</summary>
    [ObservableProperty] private bool yeuThich;

    public string LoaiHienThi => LaThu ? "Thu" : "Chi";

    public string MauLoai => LaThu ? "#1877F2" : "#D92D20";

    public string MauNenLoai => LaThu ? "#EAF1FF" : "#FEF3F2";

    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    partial void OnDaChonChanged(bool value) => OnPropertyChanged(nameof(DaChonHienThi));

    partial void OnYeuThichChanged(bool value) => OnPropertyChanged(nameof(BieuTuongYeuThich));

    public bool DaChonHienThi => DaChon;

    public string BieuTuongYeuThich => YeuThich ? "★" : "☆";
}
