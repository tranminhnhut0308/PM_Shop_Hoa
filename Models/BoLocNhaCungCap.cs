namespace ShopHoa.Models;

/// <summary>
/// Bộ lọc danh sách nhà cung cấp.
///
/// Mọi điều kiện ở đây đều được đưa thẳng xuống SQL (WHERE / COUNT / SUM)
/// chứ không lọc trên UI, nên danh sách vẫn nhanh khi bảng nha_cung_cap có
/// hàng nghìn nhà cung cấp.
/// </summary>
public sealed class BoLocNhaCungCap
{
    /// <summary>Xem cả nhà cung cấp có nợ và không có nợ.</summary>
    public const int CongNoTatCa = 0;

    /// <summary>Chỉ xem nhà cung cấp đang còn nợ.</summary>
    public const int CongNoConNo = 1;

    /// <summary>Chỉ xem nhà cung cấp đã không còn nợ.</summary>
    public const int CongNoKhongConNo = 2;

    /// <summary>Xem tất cả trạng thái.</summary>
    public const int TrangThaiTatCa = -1;

    /// <summary>Chỉ xem nhà cung cấp đang hoạt động.</summary>
    public const int TrangThaiDangHoatDong = 1;

    /// <summary>Chỉ xem nhà cung cấp đã ngừng hoạt động.</summary>
    public const int TrangThaiNgungHoatDong = 0;

    /// <summary>Trạng thái hoạt động: 1 = Đang hoạt động, 0 = Ngừng hoạt động, -1 = Tất cả.</summary>
    public int TrangThaiMode { get; set; } = TrangThaiDangHoatDong;

    /// <summary>Chuỗi tìm kiếm: mã NCC, tên, số điện thoại hoặc email.</summary>
    public string TimKiem { get; set; } = "";

    /// <summary>Lọc theo mã nhà cung cấp.</summary>
    public string MaNhaCungCap { get; set; } = "";

    /// <summary>Lọc theo tên nhà cung cấp.</summary>
    public string TenNhaCungCap { get; set; } = "";

    /// <summary>Lọc theo số điện thoại.</summary>
    public string SoDienThoai { get; set; } = "";

    /// <summary>Lọc theo email.</summary>
    public string Email { get; set; } = "";

    /// <summary>Lọc theo địa chỉ (khớp cả địa chỉ, phường/xã, quận/huyện, tỉnh/thành).</summary>
    public string DiaChi { get; set; } = "";

    /// <summary>
    /// Một trong <see cref="CongNoTatCa"/>, <see cref="CongNoConNo"/>,
    /// <see cref="CongNoKhongConNo"/>.
    /// </summary>
    public int CongNoMode { get; set; } = CongNoTatCa;

    /// <summary>Công nợ nhỏ nhất. Null = không giới hạn.</summary>
    public decimal? ConNoTu { get; set; }

    /// <summary>Công nợ lớn nhất. Null = không giới hạn.</summary>
    public decimal? ConNoDen { get; set; }

    /// <summary>Có bộ lọc nào khác ngoài ô tìm kiếm hay không (dùng cho nút Xóa bộ lọc).</summary>
    public bool CoLoc =>
        !string.IsNullOrWhiteSpace(MaNhaCungCap)
        || !string.IsNullOrWhiteSpace(TenNhaCungCap)
        || !string.IsNullOrWhiteSpace(SoDienThoai)
        || !string.IsNullOrWhiteSpace(Email)
        || !string.IsNullOrWhiteSpace(DiaChi)
        || CongNoMode != CongNoTatCa
        || TrangThaiMode != TrangThaiDangHoatDong
        || ConNoTu.HasValue
        || ConNoDen.HasValue;

    /// <summary>Mô tả ngắn bộ lọc đang có, hiển thị trên nút "Bộ lọc".</summary>
    public string MoTa
    {
        get
        {
            var dieuKien = new List<string>();

            if (!string.IsNullOrWhiteSpace(MaNhaCungCap))
                dieuKien.Add("mã");

            if (!string.IsNullOrWhiteSpace(TenNhaCungCap))
                dieuKien.Add("tên");

            if (!string.IsNullOrWhiteSpace(SoDienThoai))
                dieuKien.Add("điện thoại");

            if (!string.IsNullOrWhiteSpace(Email))
                dieuKien.Add("email");

            if (!string.IsNullOrWhiteSpace(DiaChi))
                dieuKien.Add("địa chỉ");

            if (CongNoMode == CongNoConNo)
                dieuKien.Add("còn nợ");
            else if (CongNoMode == CongNoKhongConNo)
                dieuKien.Add("không còn nợ");

            if (TrangThaiMode == TrangThaiNgungHoatDong)
                dieuKien.Add("ngừng hoạt động");
            else if (TrangThaiMode == TrangThaiTatCa)
                dieuKien.Add("tất cả trạng thái");

            if (ConNoTu.HasValue || ConNoDen.HasValue)
                dieuKien.Add("khoảng công nợ");

            return dieuKien.Count == 0
                ? "Bộ lọc"
                : $"Bộ lọc ({dieuKien.Count})";
        }
    }
}
