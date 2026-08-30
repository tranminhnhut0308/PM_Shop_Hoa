using ShopHoa.Models;

namespace ShopHoa.Services;

public static class Session
{
    public static NguoiDung? CurrentUser { get; set; }
}
