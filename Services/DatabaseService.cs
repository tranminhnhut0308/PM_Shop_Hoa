using System.Text.Json;
using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var db = doc.RootElement.GetProperty("Database");

        var server = db.GetProperty("Server").GetString() ?? "127.0.0.1";
        var port = db.GetProperty("Port").GetInt32();
        var database = db.GetProperty("Database").GetString() ?? "hoa_shop";
        var user = db.GetProperty("User").GetString() ?? "root";
        var password = db.GetProperty("Password").GetString() ?? "";
        var ssl = db.GetProperty("SslMode").GetString() ?? "None";

        _connectionString =
            $"Server={server};Port={port};Database={database};User ID={user};Password={password};SslMode={ssl};Allow User Variables=True;";
    }

    public async Task<MySqlConnection> OpenAsync()
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<bool> TestConnectionAsync()
    {
        await using var connection = await OpenAsync();
        await connection.ExecuteScalarAsync<int>("SELECT 1;");
        return true;
    }

    public async Task<IReadOnlyList<SanPham>> GetSanPhamAsync(string? search = null)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            sp.id AS Id,
            sp.ma_san_pham AS MaSanPham,
            sp.ma_vach AS MaVach,
            sp.ten_san_pham AS TenSanPham,
            sp.gia_ban AS GiaBan,
            sp.gia_von AS GiaVon,
            COALESCE(tk.so_luong_ton, 0) AS TonKho,
            sp.ton_toi_thieu AS TonToiThieu,
            sp.ton_toi_da AS TonToiDa,
            dvt.ten_don_vi AS DonViTinh,
            nsp.ten_nhom AS NhomSanPham
        FROM san_pham sp
        LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
        LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
        LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
        WHERE sp.trang_thai = 1
          AND (@Search = '' OR sp.ma_san_pham LIKE CONCAT('%', @Search, '%')
               OR sp.ma_vach LIKE CONCAT('%', @Search, '%')
               OR sp.ten_san_pham LIKE CONCAT('%', @Search, '%'))
        ORDER BY sp.ten_san_pham;
        """;

        return (await connection.QueryAsync<SanPham>(sql, new { Search = search ?? "" })).AsList();
    }

    public async Task<IReadOnlyList<KhachHang>> GetKhachHangAsync(string? search = null)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            kh.id AS Id,
            kh.ma_khach_hang AS MaKhachHang,
            kh.ten_khach_hang AS TenKhachHang,
            kh.so_dien_thoai AS SoDienThoai,
            kh.email AS Email,
            kh.dia_chi AS DiaChi,
            nkh.ten_nhom AS NhomKhachHang
        FROM khach_hang kh
        LEFT JOIN nhom_khach_hang nkh ON nkh.id = kh.nhom_khach_hang_id
        WHERE kh.trang_thai = 1
          AND (@Search = '' OR kh.ma_khach_hang LIKE CONCAT('%', @Search, '%')
               OR kh.ten_khach_hang LIKE CONCAT('%', @Search, '%')
               OR kh.so_dien_thoai LIKE CONCAT('%', @Search, '%'))
        ORDER BY kh.ten_khach_hang;
        """;

        return (await connection.QueryAsync<KhachHang>(sql, new { Search = search ?? "" })).AsList();
    }

    public async Task<IReadOnlyList<HoaDon>> GetHoaDonAsync(string? search = null)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            hd.id AS Id,
            hd.ma_hoa_don AS MaHoaDon,
            hd.thoi_gian_ban AS ThoiGianBan,
            kh.ten_khach_hang AS TenKhachHang,
            hd.tong_tien_hang AS TongTienHang,
            hd.giam_gia AS GiamGia,
            hd.thu_khac AS ThuKhac,
            hd.khach_can_tra AS KhachCanTra,
            hd.khach_da_thanh_toan AS KhachDaThanhToan,
            hd.con_no AS ConNo,
            hd.trang_thai_thanh_toan AS TrangThaiThanhToan
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        WHERE (@Search = '' OR hd.ma_hoa_don LIKE CONCAT('%', @Search, '%')
               OR kh.ten_khach_hang LIKE CONCAT('%', @Search, '%'))
        ORDER BY hd.thoi_gian_ban DESC
        LIMIT 500;
        """;

        return (await connection.QueryAsync<HoaDon>(sql, new { Search = search ?? "" })).AsList();
    }

    public async Task<IReadOnlyList<NhaCungCap>> GetNhaCungCapAsync(string? search = null)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            ncc.id AS Id,
            ncc.ma_nha_cung_cap AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            ncc.so_dien_thoai AS SoDienThoai,
            ncc.dia_chi AS DiaChi,
            COALESCE(v.cong_no, 0) AS ConNo
        FROM nha_cung_cap ncc
        LEFT JOIN vw_cong_no_nha_cung_cap v ON v.nha_cung_cap_id = ncc.id
        WHERE ncc.trang_thai = 1
          AND (@Search = '' OR ncc.ma_nha_cung_cap LIKE CONCAT('%', @Search, '%')
               OR ncc.ten_nha_cung_cap LIKE CONCAT('%', @Search, '%')
               OR ncc.so_dien_thoai LIKE CONCAT('%', @Search, '%'))
        ORDER BY ncc.ten_nha_cung_cap;
        """;

        return (await connection.QueryAsync<NhaCungCap>(sql, new { Search = search ?? "" })).AsList();
    }
}
