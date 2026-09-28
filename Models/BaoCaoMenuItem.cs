namespace ShopHoa.Models;

/// <summary>
/// Một mục trong nhóm menu "Báo cáo".
/// Thêm báo cáo mới chỉ cần thêm một phần tử vào danh sách trong ViewModel,
/// không phải sửa lại giao diện.
/// </summary>
public sealed class BaoCaoMenuItem
{
    /// <summary>Khoá định danh, dùng để quyết định hiển thị nội dung báo cáo.</summary>
    public string Key { get; init; } = "";

    public string TieuDe { get; init; } = "";

    public string BieuTuong { get; init; } = "";
}
