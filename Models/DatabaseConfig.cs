namespace ShopHoa.Models;

public sealed class DatabaseConfig
{
    public string Server { get; set; } = "baokhoagold.ddns.net";

    public int Port { get; set; } = 3306;

    public string Database { get; set; } = "hoa_shop";

    public string User { get; set; } = "admin_Nhut";

    public string Password { get; set; } = "";

    public string SslMode { get; set; } = "None";
}
