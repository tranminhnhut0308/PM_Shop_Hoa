using System.Globalization;
using System.Text;
using ClosedXML.Excel;
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
                ton_kho_excel,
                ton_toi_thieu,
                ton_toi_da,
                khach_dat,
                du_kien_het_hang,
                loai_hang_excel,
                nhom_hang_3_cap_excel,
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
                @TonKhoExcel,
                @TonToiThieu,
                @TonToiDa,
                @KhachDat,
                @DuKienHetHang,
                @LoaiHang,
                @NhomHangBaCap,
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
                    ton_kho_excel = @TonKhoExcel,
                    ton_toi_thieu = @TonToiThieu,
                    ton_toi_da = @TonToiDa,
                    khach_dat = @KhachDat,
                    du_kien_het_hang = @DuKienHetHang,
                    loai_hang_excel = @LoaiHang,
                    nhom_hang_3_cap_excel = @NhomHangBaCap,
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

    public async Task<int> ImportSanPhamTuFileAsync(string filePath)
        => await ImportSanPhamTuFileAsync(filePath, null);

    public async Task<int> ImportSanPhamTuFileAsync(string filePath, IReadOnlyCollection<SanPham>? productsOverride)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Đường dẫn file không hợp lệ.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file danh sách hàng hóa.", filePath);

        var products = productsOverride ?? ParseProductsFromFile(filePath);

        if (products.Count == 0)
            return 0;

        const string sql = """
            INSERT INTO san_pham
            (
                ma_san_pham,
                ma_vach,
                ten_san_pham,
                gia_ban,
                gia_von,
                ton_toi_thieu,
                ton_toi_da,
                khach_dat,
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
                @GiaBan,
                @GiaVon,
                @TonToiThieu,
                @TonToiDa,
                @KhachDat,
                @ViTriKho,
                @MoTa,
                @GhiChu,
                @DuocBanTrucTiep,
                @DangKinhDoanh
            );
            """;

        await using var connection = await OpenAsync();

        var importedCount = 0;
        foreach (var product in products)
        {
            await connection.ExecuteAsync(sql, product);
            importedCount++;
        }

        return importedCount;
    }

    public List<SanPham> ParseProductsFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Đường dẫn file không hợp lệ.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file danh sách hàng hóa.", filePath);

        var rows = ReadProductRowsFromFile(filePath);
        return rows
            .Select(MapRowToProduct)
            .Where(product => !string.IsNullOrWhiteSpace(product.TenSanPham))
            .ToList();
    }

    private static List<Dictionary<string, string>> ReadProductRowsFromFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).Trim().ToLowerInvariant();

        if (extension == ".csv")
            return ReadCsvProductRows(filePath);

        if (extension is ".xlsx" or ".xls")
            return ReadExcelProductRows(filePath);

        throw new NotSupportedException("File không được hỗ trợ. Vui lòng chọn file Excel (.xlsx, .xls) hoặc CSV.");
    }

    private static List<Dictionary<string, string>> ReadExcelProductRows(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault() ??
            throw new InvalidOperationException("File Excel không chứa sheet dữ liệu hợp lệ.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2)
            return [];

        var headers = new List<string>();
        var firstRow = worksheet.Row(1);
        foreach (var cell in firstRow.Cells())
            headers.Add(cell.GetString().Trim());

        var result = new List<Dictionary<string, string>>();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasAnyValue = false;

            for (var index = 0; index < headers.Count; index++)
            {
                var header = headers[index];
                var value = row.Cell(index + 1).GetString().Trim();

                if (!string.IsNullOrWhiteSpace(header) || !string.IsNullOrWhiteSpace(value))
                    hasAnyValue = true;

                if (!string.IsNullOrWhiteSpace(header))
                    values[header] = value;
            }

            if (hasAnyValue)
                result.Add(values);
        }

        return result;
    }

    private static List<Dictionary<string, string>> ReadCsvProductRows(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2)
            return [];

        var headers = ParseCsvLine(lines[0]);
        var result = new List<Dictionary<string, string>>();

        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cells = ParseCsvLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasAnyValue = false;

            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                var value = i < cells.Count ? cells[i].Trim() : string.Empty;

                if (!string.IsNullOrWhiteSpace(header) || !string.IsNullOrWhiteSpace(value))
                    hasAnyValue = true;

                if (!string.IsNullOrWhiteSpace(header))
                    row[header] = value;
            }

            if (hasAnyValue)
                result.Add(row);
        }

        return result;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (character == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private static SanPham MapRowToProduct(Dictionary<string, string> rawRow)
    {
        var normalizedRow = rawRow
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => NormalizeHeader(entry.Key),
                entry => entry.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var maSanPham = GetFirstString(normalizedRow, "mahang", "masanpham", "mach", "macode") ?? GenerateProductCode();
        var tenSanPham = GetFirstString(normalizedRow, "tenhang", "tensanpham", "ten", "name", "sanpham", "san_pham")
            ?? GetAnyNonEmptyValue(normalizedRow, "mahang", "masanpham", "mavach", "giaban", "giavon");

        var product = new SanPham
        {
            MaSanPham = maSanPham,
            MaVach = GetFirstString(normalizedRow, "mavach", "barcode", "ma_vach"),
            TenSanPham = tenSanPham ?? maSanPham,
            GiaBan = ParseDecimal(GetFirstString(normalizedRow, "giaban", "giabanhang", "price", "dongia"), 0m),
            GiaVon = ParseDecimal(GetFirstString(normalizedRow, "giavon", "giavonhang", "cost"), 0m),
            TonKhoExcel = ParseDecimal(GetFirstString(normalizedRow, "tonkho", "tonkhoexcel", "soluongton"), 0m),
            TonToiThieu = ParseDecimal(GetFirstString(normalizedRow, "tontoithieu", "tonthieunhat", "tonthieu"), 0m),
            TonToiDa = ParseDecimal(GetFirstString(normalizedRow, "tontoida", "tonlonnhat", "tonmax"), 0m),
            KhachDat = ParseDecimal(GetFirstString(normalizedRow, "khdat", "khachdat", "soluongdat"), 0m),
            DuKienHetHang = GetFirstString(normalizedRow, "dukienhethang", "du_kien_het_hang", "het_hang"),
            DonViTinh = GetFirstString(normalizedRow, "dvt", "donvitinh", "don_vi_tinh"),
            LoaiHang = GetFirstString(normalizedRow, "loaihang", "loaihangexcel", "loai"),
            NhomHangBaCap = GetFirstString(normalizedRow, "nhomhang3cap", "nhomhang3capexcel", "nhomhang"),
            ViTriKho = GetFirstString(normalizedRow, "vitri", "vitrikho", "vitri_kho"),
            DangKinhDoanh = ParseBoolean(GetFirstString(normalizedRow, "dangkinhdoanh", "trangthai", "status"), true),
            DuocBanTrucTiep = ParseBoolean(GetFirstString(normalizedRow, "duocbanchirectiep", "duocbantructiep", "bantructiep"), true),
            MoTa = GetFirstString(normalizedRow, "mota", "mo_ta"),
            GhiChu = GetFirstString(normalizedRow, "ghichu", "ghi_chu")
        };

        return product;
    }

    private static string? GetFirstString(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(NormalizeHeader(key), out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static string? GetAnyNonEmptyValue(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            var normalizedKey = NormalizeHeader(key);
            if (row.TryGetValue(normalizedKey, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        foreach (var value in row.Values)
        {
            var trimmed = value.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                return trimmed;
        }

        return null;
    }

    private static decimal ParseDecimal(string? value, decimal defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var normalized = value.Replace(".", "")
            .Replace(",", ".")
            .Replace(" ", string.Empty);

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return defaultValue;
    }

    private static bool ParseBoolean(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var normalized = value.Trim();
        if (bool.TryParse(normalized, out var result))
            return result;

        return normalized is "1" or "yes" or "y" or "co" or "đúng" ? true : defaultValue;
    }

    private static string NormalizeHeader(string value)
    {
        var normalized = value
            .Replace("\uFEFF", string.Empty)
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder
            .ToString()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(".", string.Empty)
            .Replace("/", string.Empty);
    }

    private static string GenerateProductCode()
    {
        return $"SP-{DateTime.Now:yyyyMMdd-HHmmssfff}";
    }

    private static string GenerateCustomerCode()
    {
        return $"KH{DateTime.Now:yyyyMMddHHmmssfff}";
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

    public async Task<int> ImportKhachHangTuFileAsync(string filePath)
        => await ImportKhachHangTuFileAsync(filePath, null);

    public async Task<int> ImportKhachHangTuFileAsync(string filePath, IReadOnlyCollection<KhachHang>? customersOverride)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Đường dẫn file không hợp lệ.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file danh sách khách hàng.", filePath);

        var customers = customersOverride ?? ParseKhachHangFromFile(filePath);

        if (customers.Count == 0)
            return 0;

        const string sql = """
            INSERT INTO khach_hang
            (
                ma_khach_hang,
                ten_khach_hang,
                loai_khach_hang,
                so_dien_thoai,
                ngay_sinh,
                gioi_tinh,
                email,
                facebook,
                dia_chi,
                phuong_xa,
                quan_huyen,
                tinh_thanh,
                nhom_khach_hang_id,
                ma_so_thue,
                ten_nguoi_mua_hoa_don,
                ten_cong_ty,
                dia_chi_xuat_hoa_don,
                ghi_chu,
                trang_thai,
                ngay_tao,
                ngay_cap_nhat
            )
            VALUES
            (
                @MaKhachHang,
                @TenKhachHang,
                @LoaiKhachHang,
                @SoDienThoai,
                @NgaySinh,
                @GioiTinh,
                @Email,
                @Facebook,
                @DiaChi,
                @PhuongXa,
                @QuanHuyen,
                @TinhThanh,
                NULL,
                @MaSoThue,
                @TenNguoiMuaHoaDon,
                @TenCongTy,
                @DiaChiXuatHoaDon,
                @GhiChu,
                @TrangThai,
                NOW(),
                NOW()
            );
            """;

        await using var connection = await OpenAsync();

        var importedCount = 0;
        foreach (var customer in customers)
        {
            if (string.IsNullOrWhiteSpace(customer.TenKhachHang))
                continue;

            var normalizedCustomer = customer;
            if (string.IsNullOrWhiteSpace(normalizedCustomer.MaKhachHang))
                normalizedCustomer.MaKhachHang = GenerateCustomerCode();

            normalizedCustomer.LoaiKhachHang = string.IsNullOrWhiteSpace(normalizedCustomer.LoaiKhachHang)
                ? "CA_NHAN"
                : normalizedCustomer.LoaiKhachHang;

            normalizedCustomer.TrangThai = true;

            try
            {
                await connection.ExecuteAsync(sql, normalizedCustomer);
                importedCount++;
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                // Ignore a duplicate customer code and continue importing the other rows.
            }
        }

        return importedCount;
    }

    public List<KhachHang> ParseKhachHangFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Đường dẫn file không hợp lệ.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file danh sách khách hàng.", filePath);

        var rows = ReadCustomerRowsFromFile(filePath);
        return rows
            .Select(MapRowToKhachHang)
            .Where(customer => !string.IsNullOrWhiteSpace(customer.TenKhachHang) || !string.IsNullOrWhiteSpace(customer.MaKhachHang))
            .ToList();
    }

    private static List<Dictionary<string, string>> ReadCustomerRowsFromFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).Trim().ToLowerInvariant();

        if (extension == ".csv")
            return ReadCsvRows(filePath);

        if (extension is ".xlsx" or ".xls")
            return ReadExcelRows(filePath);

        throw new NotSupportedException("File không được hỗ trợ. Vui lòng chọn file Excel (.xlsx, .xls) hoặc CSV.");
    }

    private static List<Dictionary<string, string>> ReadExcelRows(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault() ??
            throw new InvalidOperationException("File Excel không chứa sheet dữ liệu hợp lệ.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow == 0)
            return [];

        var headerRowNumber = FindCustomerHeaderRow(worksheet, lastRow);
        if (headerRowNumber == 0)
            throw new InvalidOperationException("Không tìm thấy dòng tiêu đề khách hàng trong file.");

        var firstHeaderColumn = worksheet.Row(headerRowNumber).FirstCellUsed()?.Address.ColumnNumber ?? 1;
        var lastHeaderColumn = worksheet.Row(headerRowNumber).LastCellUsed()?.Address.ColumnNumber ?? 0;
        if (lastHeaderColumn < firstHeaderColumn)
            return [];

        var headers = new List<string>();
        for (var column = firstHeaderColumn; column <= lastHeaderColumn; column++)
            headers.Add(worksheet.Cell(headerRowNumber, column).GetString().Trim());

        var result = new List<Dictionary<string, string>>();

        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasAnyValue = false;

            for (var index = 0; index < headers.Count; index++)
            {
                var header = headers[index];
                var value = row.Cell(firstHeaderColumn + index).GetFormattedString().Trim();

                if (!string.IsNullOrWhiteSpace(header) || !string.IsNullOrWhiteSpace(value))
                    hasAnyValue = true;

                if (!string.IsNullOrWhiteSpace(header))
                    values[header] = value;
            }

            if (hasAnyValue)
                result.Add(values);
        }

        return result;
    }

    private static int FindCustomerHeaderRow(IXLWorksheet worksheet, int lastRow)
    {
        for (var rowNumber = 1; rowNumber <= Math.Min(lastRow, 30); rowNumber++)
        {
            var values = worksheet.Row(rowNumber).CellsUsed()
                .Select(cell => NormalizeHeaderKey(cell.GetString()))
                .ToList();

            var hasCode = values.Any(value => value is "ma_khach_hang" or "ma_kh");
            var hasName = values.Any(value => value is "ten_khach_hang" or "ten_kh" or "ten");

            if (hasCode || hasName)
                return rowNumber;
        }

        return 0;
    }

    private static List<Dictionary<string, string>> ReadCsvRows(string filePath)
    {
        var rows = new List<Dictionary<string, string>>();
        var lines = File.ReadAllLines(filePath);

        if (lines.Length < 2)
            return rows;

        var headers = lines[0].Split(',');
        for (var i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            if (values.Length == 0)
                continue;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < headers.Length; j++)
            {
                if (j < values.Length)
                    row[headers[j].Trim()] = values[j].Trim();
            }

            if (row.Count > 0)
                rows.Add(row);
        }

        return rows;
    }

    private static string NormalizeHeaderKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character) || character == '_')
                builder.Append(char.ToLowerInvariant(character));
            else if (char.IsWhiteSpace(character) || character == '-' || character == '/' || character == '.')
                builder.Append('_');
        }

        return builder.ToString().Replace("__", "_").Trim('_');
    }

    private static string? GetCellValue(Dictionary<string, string> row, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var normalized = NormalizeHeaderKey(candidate);
            foreach (var key in row.Keys)
            {
                if (NormalizeHeaderKey(key) == normalized)
                    return row[key].Trim();
            }
        }

        return null;
    }

    private static KhachHang MapRowToKhachHang(Dictionary<string, string> row)
    {
        var customer = new KhachHang
        {
            MaKhachHang = GetCellValue(row, "ma_khach_hang", "mã khách hàng", "ma khach hang", "customer code", "customer_code") ?? string.Empty,
            TenKhachHang = GetCellValue(row, "ten_khach_hang", "tên khách hàng", "ten khach hang", "customer name", "name") ?? string.Empty,
            LoaiKhachHang = GetCellValue(row, "loai_khach_hang", "loại khách hàng", "loai khach hang", "customer type") ?? "CA_NHAN",
            SoDienThoai = GetCellValue(row, "so_dien_thoai", "số điện thoại", "dien thoai", "phone", "sdt"),
            Email = GetCellValue(row, "email"),
            Facebook = GetCellValue(row, "facebook"),
            DiaChi = GetCellValue(row, "dia_chi", "địa chỉ", "dia chi", "address"),
            PhuongXa = GetCellValue(row, "phuong_xa", "phường/xã", "phuong xa", "ward"),
            QuanHuyen = GetCellValue(row, "quan_huyen", "quận/huyện", "quan huyen", "district"),
            TinhThanh = GetCellValue(row, "tinh_thanh", "tỉnh/thành", "tinh thanh", "province"),
            NhomKhachHang = GetCellValue(row, "nhom_khach_hang", "nhóm khách hàng", "nhom khach hang", "group"),
            MaSoThue = GetCellValue(row, "ma_so_thue", "mã số thuế", "ma so thue", "tax code"),
            TenNguoiMuaHoaDon = GetCellValue(row, "ten_nguoi_mua_hoa_don", "tên người mua hóa đơn", "nguoi mua hoa don"),
            TenCongTy = GetCellValue(row, "ten_cong_ty", "tên công ty", "cong ty"),
            DiaChiXuatHoaDon = GetCellValue(row, "dia_chi_xuat_hoa_don", "địa chỉ xuất hóa đơn", "dia chi xuat hoa don"),
            GhiChu = GetCellValue(row, "ghi_chu", "ghi chú", "note")
        };

        if (!string.IsNullOrWhiteSpace(GetCellValue(row, "ngay_sinh", "ngày sinh", "birthday", "dob")))
        {
            var value = GetCellValue(row, "ngay_sinh", "ngày sinh", "birthday", "dob");
            if (DateTime.TryParse(value, out var parsedDate))
                customer.NgaySinh = parsedDate;
        }

        if (!string.IsNullOrWhiteSpace(GetCellValue(row, "gioi_tinh", "giới tính", "gender", "sex")))
            customer.GioiTinh = GetCellValue(row, "gioi_tinh", "giới tính", "gender", "sex");

        if (string.IsNullOrWhiteSpace(customer.MaKhachHang))
            customer.MaKhachHang = GenerateCustomerCode();

        if (string.IsNullOrWhiteSpace(customer.LoaiKhachHang))
            customer.LoaiKhachHang = "CA_NHAN";

        return customer;
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

    public async Task<HoaDonBanHangReceipt> TaoHoaDonBanHangAsync(
        IReadOnlyCollection<SaleCartItem> items,
        KhachHang? customer,
        decimal discount)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Giỏ hàng đang trống.");

        var now = DateTime.Now;
        var invoiceCode = $"HD{now:yyyyMMddHHmmssfff}";
        var total = items.Sum(item => item.ThanhTien);
        var safeDiscount = Math.Clamp(discount, 0, total);
        var amountDue = total - safeDiscount;
        var branchId = Session.CurrentUser?.ChiNhanhId ?? 1;
        var userId = Session.CurrentUser?.Id;

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string invoiceSql = """
                INSERT INTO hoa_don_ban_hang
                (
                    ma_hoa_don,
                    chi_nhanh_id,
                    khach_hang_id,
                    nguoi_ban_id,
                    thoi_gian_ban,
                    tong_tien_hang,
                    giam_gia,
                    khach_can_tra,
                    khach_da_thanh_toan,
                    con_no,
                    trang_thai_thanh_toan,
                    trang_thai_hoa_don,
                    ngay_tao,
                    ngay_cap_nhat
                )
                VALUES
                (
                    @MaHoaDon,
                    @ChiNhanhId,
                    @KhachHangId,
                    @NguoiBanId,
                    @ThoiGianBan,
                    @TongTienHang,
                    @GiamGia,
                    @KhachCanTra,
                    @KhachDaThanhToan,
                    @ConNo,
                    'DA_THANH_TOAN',
                    'HOAN_THANH',
                    @ThoiGianBan,
                    @ThoiGianBan
                );
                SELECT LAST_INSERT_ID();
                """;

            var invoiceId = await connection.ExecuteScalarAsync<uint>(
                invoiceSql,
                new
                {
                    MaHoaDon = invoiceCode,
                    ChiNhanhId = branchId,
                    KhachHangId = customer?.Id,
                    NguoiBanId = userId,
                    ThoiGianBan = now,
                    TongTienHang = total,
                    GiamGia = safeDiscount,
                    KhachCanTra = amountDue,
                    KhachDaThanhToan = amountDue,
                    ConNo = 0m
                },
                transaction);

            const string detailSql = """
                INSERT INTO chi_tiet_hoa_don_ban_hang
                (
                    hoa_don_id,
                    san_pham_id,
                    ma_san_pham,
                    ten_san_pham,
                    don_vi_tinh,
                    so_luong,
                    don_gia,
                    thanh_tien,
                    gia_von
                )
                VALUES
                (
                    @HoaDonId,
                    @SanPhamId,
                    @MaSanPham,
                    @TenSanPham,
                    @DonViTinh,
                    @SoLuong,
                    @DonGia,
                    @ThanhTien,
                    @GiaVon
                );
                """;

            foreach (var item in items)
            {
                await connection.ExecuteAsync(
                    detailSql,
                    new
                    {
                        HoaDonId = invoiceId,
                        SanPhamId = item.SanPham.Id,
                        MaSanPham = item.SanPham.MaSanPham,
                        TenSanPham = item.SanPham.TenSanPham,
                        DonViTinh = item.SanPham.DonViTinh,
                        item.SoLuong,
                        DonGia = item.SanPham.GiaBan,
                        item.ThanhTien,
                        GiaVon = item.SanPham.GiaVon
                    },
                    transaction);
            }

            await transaction.CommitAsync();

            return new HoaDonBanHangReceipt
            {
                MaHoaDon = invoiceCode,
                ThoiGianBan = now,
                TenKhachHang = customer?.TenKhachHang ?? "Khách lẻ",
                TenNhanVien = Session.CurrentUser?.HoTen ?? "",
                TongTienHang = total,
                GiamGia = safeDiscount,
                KhachCanTra = amountDue,
                KhachDaThanhToan = amountDue,
                ConNo = 0,
                Items = items.Select(item => new HoaDonBanHangReceiptItem
                {
                    TenSanPham = item.SanPham.TenSanPham,
                    SoLuong = item.SoLuong,
                    DonGia = item.SanPham.GiaBan,
                    ThanhTien = item.ThanhTien
                }).ToList()
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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
