using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Kiểm tra quyền người dùng dựa trên vai trò lấy từ database.
/// <para>
/// Mã vai trò không gắn cứng vào tên hiển thị: những hằng số bên dưới là mã
/// <c>vai_tro.ma_vai_tro</c> đang có trong database. Muốn mở rộng/thu hẹp quyền
/// thì khai báo tại đây một lần, mọi màn hình dùng chung (UI + ViewModel).
/// </para>
/// </summary>
public static class Quyen
{
    /// <summary>vai_tro.ma_vai_tro = 'QUAN_TRI' — toàn quyền.</summary>
    public const string QuanTri = "QUAN_TRI";

    /// <summary>vai_tro.ma_vai_tro = 'QUAN_LY' — quản lý bán hàng, kho và báo cáo.</summary>
    public const string QuanLy = "QUAN_LY";

    /// <summary>vai_tro.ma_vai_tro = 'KHO' — nhân viên kho, quản lý hàng hóa.</summary>
    public const string Kho = "KHO";

    /// <summary>vai_tro.ma_vai_tro = 'THU_NGAN' — chỉ bán hàng và thanh toán.</summary>
    public const string ThuNgan = "THU_NGAN";

    /// <summary>vai_tro.ma_vai_tro = 'BAO_CAO' — chỉ xem báo cáo.</summary>
    public const string BaoCao = "BAO_CAO";

    /// <summary>
    /// Vai trò được sửa hàng hóa (bao gồm cả giá bán).
    /// Không bao gồm thu ngân và xem báo cáo vì nghiệp vụ của hai vai trò này
    /// không sửa được danh mục hàng hóa.
    /// </summary>
    private static readonly string[] VaiTroSuaHangHoa =
    [
        QuanTri,
        QuanLy,
        Kho
    ];

    /// <summary>Người dùng có quyền sửa hàng hóa / chỉnh giá bán hay không.</summary>
    public static bool DuocSuaHangHoa(NguoiDung? nguoiDung)
        => HasAnyRole(nguoiDung, VaiTroSuaHangHoa);

    /// <summary>
    /// Người dùng có ít nhất một trong các mã vai trò truyền vào hay không.
    /// So sánh không phân biệt hoa thường và bỏ qua khoảng trắng thừa.
    /// </summary>
    public static bool HasAnyRole(NguoiDung? nguoiDung, params string[] maVaiTro)
    {
        if (nguoiDung is null || maVaiTro.Length == 0)
            return false;

        foreach (var ma in nguoiDung.DanhSachMaVaiTro())
        {
            foreach (var canCo in maVaiTro)
            {
                if (string.Equals(ma, canCo, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}