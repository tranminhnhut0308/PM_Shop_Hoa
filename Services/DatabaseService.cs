using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed class DatabaseService
{
    private readonly DatabaseConfigService _configService;

    public DatabaseService()
    {
        _configService = new DatabaseConfigService();
    }

    private string GetConnectionString()
    {
        var config = _configService.Load();

        return
            $"Server={config.Server};" +
            $"Port={config.Port};" +
            $"Database={config.Database};" +
            $"User ID={config.User};" +
            $"Password={config.Password};" +
            $"SslMode={config.SslMode};" +
            $"Allow User Variables=True;";
    }

    public async Task<MySqlConnection> OpenAsync()
    {
        var connection = new MySqlConnection(GetConnectionString());

        await connection.OpenAsync();

        return connection;
    }

    public async Task<bool> TestConnectionAsync()
{
    try
    {
        await using var connection = await OpenAsync();

        await connection.ExecuteScalarAsync<int>("SELECT 1;");

        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine("========== DATABASE ERROR ==========");
        Console.WriteLine(ex.ToString());
        Console.WriteLine("====================================");

        throw;
    }
}

    // Mở sẵn một kết nối để MySqlConnector đưa vào connection pool.
    // Lần đăng nhập sau đó có thể dùng lại kết nối này thay vì bắt tay lại với server.
    public async Task WarmUpAsync()
    {
        await using var connection = await OpenAsync();
    }

    public async Task<NguoiDung?> DangNhapAsync(
        string tenDangNhap,
        string matKhau)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            nd.id AS Id,
            nd.ma_nguoi_dung AS MaNguoiDung,
            nd.ten_dang_nhap AS TenDangNhap,
            nd.mat_khau AS MatKhau,
            nd.ho_ten AS HoTen,
            nd.so_dien_thoai AS SoDienThoai,
            nd.email AS Email,
            nd.chi_nhanh_id AS ChiNhanhId,
            cn.ten_chi_nhanh AS TenChiNhanh,
            GROUP_CONCAT(
                vt.ten_vai_tro
                ORDER BY vt.id
                SEPARATOR ', '
            ) AS VaiTro

        FROM nguoi_dung nd

        LEFT JOIN chi_nhanh cn
            ON cn.id = nd.chi_nhanh_id

        LEFT JOIN nguoi_dung_vai_tro ndvt
            ON ndvt.nguoi_dung_id = nd.id

        LEFT JOIN vai_tro vt
            ON vt.id = ndvt.vai_tro_id

        WHERE nd.ten_dang_nhap = @TenDangNhap
          AND nd.trang_thai = 1

        GROUP BY
            nd.id,
            nd.ma_nguoi_dung,
            nd.ten_dang_nhap,
            nd.mat_khau,
            nd.ho_ten,
            nd.so_dien_thoai,
            nd.email,
            nd.chi_nhanh_id,
            cn.ten_chi_nhanh

        LIMIT 1;
        """;

        var user =
            await connection.QuerySingleOrDefaultAsync<NguoiDung>(
                sql,
                new
                {
                    TenDangNhap = tenDangNhap
                });

        if (user is null)
        {
            return null;
        }

        bool validPassword;

        if (user.MatKhau.StartsWith("$2a$") ||
            user.MatKhau.StartsWith("$2b$") ||
            user.MatKhau.StartsWith("$2y$"))
        {
            validPassword =
                BCrypt.Net.BCrypt.Verify(
                    matKhau,
                    user.MatKhau);
        }
        else
        {
            validPassword =
                string.Equals(
                    matKhau,
                    user.MatKhau,
                    StringComparison.Ordinal);
        }

        if (!validPassword)
        {
            return null;
        }

        return user;
    }

    // Đây là thông tin nhật ký, không cần chặn thao tác đăng nhập của người dùng.
    public async Task CapNhatLanDangNhapCuoiAsync(uint userId)
    {
        try
        {
            await using var connection = await OpenAsync();

            await connection.ExecuteAsync(
            """
            UPDATE nguoi_dung
            SET
                lan_dang_nhap_cuoi = NOW(),
                ngay_cap_nhat = NOW()
            WHERE id = @Id;
            """,
            new
            {
                Id = userId
            });
        }
        catch (Exception ex)
        {
            // Không để lỗi ghi nhật ký làm ảnh hưởng đến phiên đăng nhập hợp lệ.
            Console.WriteLine($"Không thể cập nhật lần đăng nhập cuối: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<SanPham>> GetSanPhamAsync(
        string? search = null)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            sp.id AS Id,
            sp.ma_san_pham AS MaSanPham,
            sp.ma_vach AS MaVach,
            COALESCE(NULLIF(TRIM(sp.ten_san_pham), ''), sp.ma_san_pham) AS TenSanPham,
            sp.nhom_san_pham_id AS NhomSanPhamId,
            sp.don_vi_tinh_id AS DonViTinhId,
            sp.thuong_hieu_id AS ThuongHieuId,
            sp.gia_ban AS GiaBan,
            sp.gia_von AS GiaVon,
            COALESCE(tk.so_luong_ton, 0) AS TonKho,
            COALESCE(sp.ton_kho_excel, COALESCE(tk.so_luong_ton, 0)) AS TonKhoExcel,
            sp.ton_toi_thieu AS TonToiThieu,
            sp.ton_toi_da AS TonToiDa,
            COALESCE(sp.khach_dat, 0) AS KhachDat,
            sp.ngay_tao AS NgayTao,
            sp.du_kien_het_hang AS DuKienHetHang,
            dvt.ten_don_vi AS DonViTinh,
            nsp.ten_nhom AS NhomSanPham,
            sp.loai_hang_excel AS LoaiHang,
            sp.nhom_hang_3_cap_excel AS NhomHangBaCap,
            sp.vi_tri_kho AS ViTriKho,
            sp.dang_kinh_doanh AS DangKinhDoanh,
            sp.co_ban_truc_tiep AS DuocBanTrucTiep,
            sp.trang_thai AS DangKinhDoanh,
            sp.mo_ta AS MoTa,
            sp.ghi_chu AS GhiChu,
            sp.hinh_anh AS HinhAnh
        FROM san_pham sp
        LEFT JOIN ton_kho tk
            ON tk.san_pham_id = sp.id
        LEFT JOIN don_vi_tinh dvt
            ON dvt.id = sp.don_vi_tinh_id
        LEFT JOIN nhom_san_pham nsp
            ON nsp.id = sp.nhom_san_pham_id
                WHERE (
                @Search = ''
                OR sp.ma_san_pham LIKE CONCAT('%', @Search, '%')
                OR sp.ma_vach LIKE CONCAT('%', @Search, '%')
                OR sp.ten_san_pham LIKE CONCAT('%', @Search, '%')
              )
        ORDER BY sp.ten_san_pham;
        """;

        var parameters = new
        {
            Search = search ?? ""
        };

        var products = (
            await connection.QueryAsync<SanPham>(sql, parameters)
        ).AsList();

        if (products.Count > 0)
        {
            return products;
        }

        const string importSql = """
        SELECT
            `Mã hàng` AS MaSanPham,
            `Mã vạch` AS MaVach,
            COALESCE(`Tên hàng`, `Mã hàng`) AS TenSanPham,
            COALESCE(`Giá bán`, 0) AS GiaBan,
            COALESCE(`Giá vốn`, 0) AS GiaVon,
            COALESCE(`Tồn kho`, 0) AS TonKho,
            COALESCE(`Tồn kho`, 0) AS TonKhoExcel,
            COALESCE(`Tồn nhỏ nhất`, 0) AS TonToiThieu,
            COALESCE(`Tồn lớn nhất`, 0) AS TonToiDa,
            COALESCE(`KH đặt`, 0) AS KhachDat,
            `Thời gian tạo` AS NgayTao,
            `Dự kiến hết hàng` AS DuKienHetHang,
            `ĐVT` AS DonViTinh,
            `Nhóm hàng(3 Cấp)` AS NhomSanPham,
            `Loại hàng` AS LoaiHang,
            `Nhóm hàng(3 Cấp)` AS NhomHangBaCap,
            `Vị trí` AS ViTriKho,
            COALESCE(`Đang kinh doanh`, 1) AS DangKinhDoanh,
            COALESCE(`Được bán trực tiếp`, 1) AS DuocBanTrucTiep
        FROM import_san_pham_excel
        WHERE COALESCE(`Đang kinh doanh`, 1) = 1
          AND (
                @Search = ''
                OR `Mã hàng` LIKE CONCAT('%', @Search, '%')
                OR `Mã vạch` LIKE CONCAT('%', @Search, '%')
                OR `Tên hàng` LIKE CONCAT('%', @Search, '%')
              )
        ORDER BY `Tên hàng`;
        """;

        try
        {
            return (
                await connection.QueryAsync<SanPham>(importSql, parameters)
            ).AsList();
        }
        catch (MySqlException ex) when (ex.Number == 1146)
        {
            return products;
        }
    }

    public async Task<IReadOnlyList<ProductOption>> GetNhomSanPhamAsync()
    {
        await using var connection = await OpenAsync();
        return (await connection.QueryAsync<ProductOption>(
            "SELECT id AS Id, ten_nhom AS Name FROM nhom_san_pham WHERE trang_thai = 1 ORDER BY ten_nhom;"
        )).AsList();
    }

    public async Task<IReadOnlyList<ProductOption>> GetDonViTinhAsync()
    {
        await using var connection = await OpenAsync();
        return (await connection.QueryAsync<ProductOption>(
            "SELECT id AS Id, ten_don_vi AS Name FROM don_vi_tinh WHERE trang_thai = 1 ORDER BY ten_don_vi;"
        )).AsList();
    }

    public async Task CapNhatHinhAnhSanPhamAsync(uint productId, string imagePath)
    {
        await using var connection = await OpenAsync();

        await connection.ExecuteAsync(
            """
            UPDATE san_pham
            SET hinh_anh = @ImagePath,
                ngay_cap_nhat = NOW()
            WHERE id = @ProductId;
            """,
            new
            {
                ProductId = productId,
                ImagePath = imagePath
            });
    }

        public async Task<uint> ThemSanPhamAsync(SanPham product)
        {
            await using var connection = await OpenAsync();

            const string sql = """
            INSERT INTO san_pham
            (
                ma_san_pham,
                ma_vach,
                ten_san_pham,
                nhom_san_pham_id,
                don_vi_tinh_id,
                thuong_hieu_id,
                gia_ban,
                gia_von,
                ton_toi_thieu,
                ton_toi_da,
                vi_tri_kho,
                mo_ta,
                ghi_chu,
                co_ban_truc_tiep,
                trang_thai
            )
            VALUES
            (
                @MaSanPham,
                @MaVach,
                @TenSanPham,
                @NhomSanPhamId,
                @DonViTinhId,
                @ThuongHieuId,
                @GiaBan,
                @GiaVon,
                @TonToiThieu,
                @TonToiDa,
                @ViTriKho,
                @MoTa,
                @GhiChu,
                @DuocBanTrucTiep,
                @DangKinhDoanh
            );
            SELECT LAST_INSERT_ID();
            """;

            return await connection.ExecuteScalarAsync<uint>(sql, product);
        }

        public async Task CapNhatSanPhamAsync(SanPham product)
        {
            await using var connection = await OpenAsync();

            await connection.ExecuteAsync(
                """
                UPDATE san_pham
                SET ma_san_pham = @MaSanPham,
                    ma_vach = @MaVach,
                    ten_san_pham = @TenSanPham,
                    nhom_san_pham_id = @NhomSanPhamId,
                    don_vi_tinh_id = @DonViTinhId,
                    thuong_hieu_id = @ThuongHieuId,
                    gia_ban = @GiaBan,
                    gia_von = @GiaVon,
                    ton_toi_thieu = @TonToiThieu,
                    ton_toi_da = @TonToiDa,
                    vi_tri_kho = @ViTriKho,
                    mo_ta = @MoTa,
                    ghi_chu = @GhiChu,
                    co_ban_truc_tiep = @DuocBanTrucTiep,
                    trang_thai = @DangKinhDoanh,
                    ngay_cap_nhat = NOW()
                WHERE id = @Id;
                """,
                product);
        }

    public async Task XoaSanPhamAsync(uint productId)
    {
        await using var connection = await OpenAsync();

        await connection.ExecuteAsync(
            "DELETE FROM san_pham WHERE id = @Id;",
            new { Id = productId });
    }

    public async Task<IReadOnlyList<KhachHang>> GetKhachHangAsync(
        string? search = null)
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
        LEFT JOIN nhom_khach_hang nkh
            ON nkh.id = kh.nhom_khach_hang_id
        WHERE kh.trang_thai = 1
          AND (
                @Search = ''
                OR kh.ma_khach_hang LIKE CONCAT('%', @Search, '%')
                OR kh.ten_khach_hang LIKE CONCAT('%', @Search, '%')
                OR kh.so_dien_thoai LIKE CONCAT('%', @Search, '%')
              )
        ORDER BY kh.ten_khach_hang;
        """;

        return (
            await connection.QueryAsync<KhachHang>(
                sql,
                new
                {
                    Search = search ?? ""
                })
        ).AsList();
    }

    public async Task<IReadOnlyList<HoaDon>> GetHoaDonAsync(
        string? search = null)
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
        LEFT JOIN khach_hang kh
            ON kh.id = hd.khach_hang_id
        WHERE (
            @Search = ''
            OR hd.ma_hoa_don LIKE CONCAT('%', @Search, '%')
            OR kh.ten_khach_hang LIKE CONCAT('%', @Search, '%')
        )
        ORDER BY hd.thoi_gian_ban DESC
        LIMIT 500;
        """;

        return (
            await connection.QueryAsync<HoaDon>(
                sql,
                new
                {
                    Search = search ?? ""
                })
        ).AsList();
    }

    public async Task<IReadOnlyList<NhaCungCap>> GetNhaCungCapAsync(
        string? search = null)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            ncc.id AS Id,
            ncc.ma_nha_cung_cap AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            ncc.so_dien_thoai AS SoDienThoai,
            ncc.dia_chi AS DiaChi,
            COALESCE(v.con_no, 0) AS ConNo
        FROM nha_cung_cap ncc
        LEFT JOIN vw_cong_no_nha_cung_cap v
            ON v.id = ncc.id
        WHERE ncc.trang_thai = 1
          AND (
                @Search = ''
                OR ncc.ma_nha_cung_cap LIKE CONCAT('%', @Search, '%')
                OR ncc.ten_nha_cung_cap LIKE CONCAT('%', @Search, '%')
                OR ncc.so_dien_thoai LIKE CONCAT('%', @Search, '%')
              )
        ORDER BY ncc.ten_nha_cung_cap;
        """;

        return (
            await connection.QueryAsync<NhaCungCap>(
                sql,
                new
                {
                    Search = search ?? ""
                })
        ).AsList();
    }
}
