using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Phiên làm việc của người dùng đang chạy ứng dụng.
/// Chỉ giữ trong RAM, đóng ứng dụng là mất.
/// </summary>
public static class Session
{
    public static NguoiDung? CurrentUser { get; private set; }

    public static bool IsLoggedIn => CurrentUser is not null;

    public static void SetCurrentUser(NguoiDung user)
    {
        CurrentUser = user;
    }

    /// <summary>
    /// Kết thúc phiên hiện tại.
    /// Không đụng tới database nên tài khoản vẫn tồn tại và đăng nhập lại được.
    /// </summary>
    public static void Logout()
    {
        CurrentUser = null;
    }
}
