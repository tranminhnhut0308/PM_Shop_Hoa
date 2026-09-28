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
    private bool? _hasPhieuNhapExtraColumns;

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

    public async Task<IReadOnlyList<SanPham>> GetSanPhamAsync(string? search = null)
    {
        await using var connection = await OpenAsync();

        var keyword = (search ?? string.Empty).Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(keyword);
        var limit = hasSearch ? 200 : 1000;

        // Tách 2 trường hợp để MySQL tối ưu tốt hơn:
        // - Không tìm kiếm: dùng ORDER BY + LIMIT 30, không có điều kiện OR @Search = ''.
        // - Có tìm kiếm: tìm trực tiếp trong DB và chỉ trả tối đa 200 dòng.
        string sql = hasSearch
            ? """
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
                  sp.dang_kinh_doanh AS DuocBanTrucTiep,
                  sp.trang_thai AS DangKinhDoanh,
                  sp.mo_ta AS MoTa,
                  sp.ghi_chu AS GhiChu,
                  sp.hinh_anh AS HinhAnh
              FROM san_pham sp
              LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
              LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
              LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
              WHERE (
                    sp.ma_san_pham LIKE CONCAT(@Search, '%')
                    OR sp.ma_vach LIKE CONCAT(@Search, '%')
                    OR sp.ten_san_pham LIKE CONCAT('%', @Search, '%')
                )
              ORDER BY
                  CASE
                      WHEN sp.ma_san_pham = @Search THEN 0
                      WHEN sp.ma_vach = @Search THEN 1
                      ELSE 2
                  END,
                  sp.ten_san_pham
              LIMIT @Limit;
              """
            : """
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
                  sp.dang_kinh_doanh AS DuocBanTrucTiep,
                  sp.trang_thai AS DangKinhDoanh,
                  sp.mo_ta AS MoTa,
                  sp.ghi_chu AS GhiChu,
                  sp.hinh_anh AS HinhAnh
              FROM san_pham sp
              LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
              LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
              LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
              ORDER BY sp.ten_san_pham, sp.id
              LIMIT @Limit;
              """;

        var parameters = new
        {
            Search = keyword,
            Limit = limit
        };

        var products = (
            await connection.QueryAsync<SanPham>(sql, parameters)
        ).AsList();

        // Chỉ dùng bảng import cũ khi bảng san_pham không có dữ liệu.
        if (products.Count > 0)
            return products;

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
                  OR `Mã hàng` LIKE CONCAT(@Search, '%')
                  OR `Mã vạch` LIKE CONCAT(@Search, '%')
                  OR `Tên hàng` LIKE CONCAT('%', @Search, '%')
              )
            ORDER BY `Tên hàng`
            LIMIT @Limit;
            """;

        try
        {
            return (
                await connection.QueryAsync<SanPham>(
                    importSql,
                    parameters
                )
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
    public async Task CapNhatGiaBanSanPhamAsync(
        uint productId,
        decimal giaBan)
    {
        if (giaBan <= 0)
            throw new ArgumentException(
                "Giá bán phải lớn hơn 0.",
                nameof(giaBan));

        await using var connection = await OpenAsync();

        const string sql = """
        UPDATE san_pham
        SET gia_ban = @GiaBan,
            ngay_cap_nhat = NOW()
        WHERE id = @ProductId;
        """;

        var affectedRows = await connection.ExecuteAsync(
            sql,
            new
            {
                ProductId = productId,
                GiaBan = giaBan
            });

        if (affectedRows == 0)
            throw new InvalidOperationException(
                "Không tìm thấy sản phẩm để cập nhật giá bán.");
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

    /// <summary>
    /// Sinh mã khách hàng tuần tự theo dạng KH0000001.
    /// Chỉ xét các mã KH + số để không bị ảnh hưởng bởi mã cũ khác định dạng.
    /// </summary>
    public async Task<string> TaoMaKhachHangMoiAsync()
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(ma_khach_hang, 3) AS UNSIGNED)),
            0
        ) + 1
        FROM khach_hang
        WHERE ma_khach_hang REGEXP '^KH[0-9]{1,7}$';
        """;

        var nextNumber = await connection.ExecuteScalarAsync<long>(sql);

        if (nextNumber > 9_999_999)
            throw new InvalidOperationException("Đã vượt giới hạn mã khách hàng tự động KH9999999.");

        return $"KH{nextNumber:D7}";
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
            kh.loai_khach_hang AS LoaiKhachHang,
            kh.so_dien_thoai AS SoDienThoai,
            kh.ngay_sinh AS NgaySinh,
            kh.gioi_tinh AS GioiTinh,
            kh.email AS Email,
            kh.facebook AS Facebook,
            kh.dia_chi AS DiaChi,
            kh.phuong_xa AS PhuongXa,
            kh.quan_huyen AS QuanHuyen,
            kh.tinh_thanh AS TinhThanh,
            nkh.ten_nhom AS NhomKhachHang
            ,kh.ma_so_thue AS MaSoThue
            ,kh.ten_nguoi_mua_hoa_don AS TenNguoiMuaHoaDon
            ,kh.ten_cong_ty AS TenCongTy
            ,kh.dia_chi_xuat_hoa_don AS DiaChiXuatHoaDon
            ,kh.ghi_chu AS GhiChu
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

    public async Task XoaKhachHangAsync(uint customerId)
    {
        await using var connection = await OpenAsync();

        // Giữ lại lịch sử hóa đơn/công nợ; danh sách khách hàng chỉ hiển thị bản ghi còn hoạt động.
        var affectedRows = await connection.ExecuteAsync(
            "UPDATE khach_hang SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id AND trang_thai = 1;",
            new { Id = customerId });

        if (affectedRows == 0)
            throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã bị xóa.");
    }
    public async Task<uint> ThemKhachHangAsync(KhachHang customer)
    {
        await using var connection = await OpenAsync();

        if (string.IsNullOrWhiteSpace(customer.MaKhachHang))
            throw new ArgumentException("Mã khách hàng không được để trống.");

        if (string.IsNullOrWhiteSpace(customer.TenKhachHang))
            throw new ArgumentException("Tên khách hàng không được để trống.");

        // Kiểm tra trùng mã khách hàng
        const string checkCodeSql = """
        SELECT COUNT(*)
        FROM khach_hang
        WHERE ma_khach_hang = @MaKhachHang
          AND trang_thai = 1;
        """;

        var exists = await connection.ExecuteScalarAsync<int>(
            checkCodeSql,
            new
            {
                customer.MaKhachHang
            });

        if (exists > 0)
            throw new InvalidOperationException(
                $"Mã khách hàng {customer.MaKhachHang} đã tồn tại.");

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
            1,
            NOW(),
            NOW()
        );

        SELECT LAST_INSERT_ID();
        """;

        var id = await connection.ExecuteScalarAsync<uint>(
            sql,
            new
            {
                customer.MaKhachHang,
                customer.TenKhachHang,
                customer.LoaiKhachHang,
                customer.SoDienThoai,
                customer.NgaySinh,
                customer.GioiTinh,
                customer.Email,
                customer.Facebook,
                customer.DiaChi,
                customer.PhuongXa,
                customer.QuanHuyen,
                customer.TinhThanh,
                customer.MaSoThue,
                customer.TenNguoiMuaHoaDon,
                customer.TenCongTy,
                customer.DiaChiXuatHoaDon,
                customer.GhiChu
            });

        return id;
    }
    public async Task CapNhatKhachHangAsync(KhachHang customer)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        UPDATE khach_hang
        SET ma_khach_hang = @MaKhachHang,
            ten_khach_hang = @TenKhachHang,
            loai_khach_hang = @LoaiKhachHang,
            so_dien_thoai = @SoDienThoai,
            ngay_sinh = @NgaySinh,
            gioi_tinh = @GioiTinh,
            email = @Email,
            facebook = @Facebook,
            dia_chi = @DiaChi,
            phuong_xa = @PhuongXa,
            quan_huyen = @QuanHuyen,
            tinh_thanh = @TinhThanh,
            ma_so_thue = @MaSoThue,
            ten_nguoi_mua_hoa_don = @TenNguoiMuaHoaDon,
            ten_cong_ty = @TenCongTy,
            dia_chi_xuat_hoa_don = @DiaChiXuatHoaDon,
            ghi_chu = @GhiChu,
            ngay_cap_nhat = NOW()
        WHERE id = @Id AND trang_thai = 1;
        """;

        var affectedRows = await connection.ExecuteAsync(sql, customer);
        if (affectedRows == 0)
            throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã bị xóa.");
    }

    public async Task<int> ImportKhachHangTuFileAsync(string filePath)
        => await ImportKhachHangTuFileAsync(filePath, null, false);

    public async Task<int> ImportKhachHangTuFileAsync(
        string filePath,
        IReadOnlyCollection<KhachHang>? customersOverride,
        bool replaceExisting = false)
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

            const string findSql = """
                SELECT id
                FROM khach_hang
                WHERE trang_thai = 1
                  AND (
                        ma_khach_hang = @MaKhachHang
                        OR (
                            NULLIF(TRIM(@SoDienThoai), '') IS NOT NULL
                            AND so_dien_thoai = @SoDienThoai
                        )
                  )
                LIMIT 1;
                """;

            var existingId = await connection.ExecuteScalarAsync<uint?>(
                findSql,
                new
                {
                    normalizedCustomer.MaKhachHang,
                    normalizedCustomer.SoDienThoai
                });

            if (existingId.HasValue)
            {
                if (!replaceExisting)
                    continue;

                const string updateSql = """
                    UPDATE khach_hang
                    SET ma_khach_hang = @MaKhachHang,
                        ten_khach_hang = @TenKhachHang,
                        loai_khach_hang = @LoaiKhachHang,
                        so_dien_thoai = @SoDienThoai,
                        ngay_sinh = @NgaySinh,
                        gioi_tinh = @GioiTinh,
                        email = @Email,
                        facebook = @Facebook,
                        dia_chi = @DiaChi,
                        phuong_xa = @PhuongXa,
                        quan_huyen = @QuanHuyen,
                        tinh_thanh = @TinhThanh,
                        ma_so_thue = @MaSoThue,
                        ten_nguoi_mua_hoa_don = @TenNguoiMuaHoaDon,
                        ten_cong_ty = @TenCongTy,
                        dia_chi_xuat_hoa_don = @DiaChiXuatHoaDon,
                        ghi_chu = @GhiChu,
                        trang_thai = 1,
                        ngay_cap_nhat = NOW()
                    WHERE id = @Id;
                    """;

                await connection.ExecuteAsync(
                    updateSql,
                    new
                    {
                        Id = existingId.Value,
                        normalizedCustomer.MaKhachHang,
                        normalizedCustomer.TenKhachHang,
                        normalizedCustomer.LoaiKhachHang,
                        normalizedCustomer.SoDienThoai,
                        normalizedCustomer.NgaySinh,
                        normalizedCustomer.GioiTinh,
                        normalizedCustomer.Email,
                        normalizedCustomer.Facebook,
                        normalizedCustomer.DiaChi,
                        normalizedCustomer.PhuongXa,
                        normalizedCustomer.QuanHuyen,
                        normalizedCustomer.TinhThanh,
                        normalizedCustomer.MaSoThue,
                        normalizedCustomer.TenNguoiMuaHoaDon,
                        normalizedCustomer.TenCongTy,
                        normalizedCustomer.DiaChiXuatHoaDon,
                        normalizedCustomer.GhiChu
                    });

                importedCount++;
                continue;
            }

            await connection.ExecuteAsync(sql, normalizedCustomer);
            importedCount++;
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

    COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
    COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,

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
    OR kh.ma_khach_hang LIKE CONCAT('%', @Search, '%')
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
    public async Task<string> TaoMaHoaDonMoiAsync()
    {
        await using var connection = await OpenAsync();
        var prefix = $"HD{DateTime.Now:yyMMdd}"; // VD: HD260927

        var sql = @"
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(ma_hoa_don, 9) AS UNSIGNED)), 
            0
        ) + 1
        FROM hoa_don_ban_hang
        WHERE ma_hoa_don LIKE CONCAT(@Prefix, '-%');";

        var nextNum = await connection.ExecuteScalarAsync<long>(sql, new { Prefix = prefix });
        return $"{prefix}-{nextNum:D3}"; // Kết quả: HD260927-001, HD260927-002...
    }
    public async Task<HoaDonBanHangReceipt> TaoHoaDonBanHangAsync(
    IReadOnlyCollection<SaleCartItem> items,
    KhachHang? customer,
    decimal discount,
    decimal tienKhachDua)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Giỏ hàng đang trống.");

        var now = DateTime.Now;
        var invoiceCode = await TaoMaHoaDonMoiAsync();

        // Tổng tiền hàng
        var total = items.Sum(item => item.ThanhTien);

        // Giảm giá hợp lệ
        var safeDiscount = Math.Clamp(discount, 0, total);

        // Khách cần thanh toán
        var amountDue = total - safeDiscount;

        // Tiền khách đưa không được âm
        var cashReceived = Math.Max(0, tienKhachDua);

        // Số tiền thực tế được tính là đã thanh toán
        // Nếu khách đưa dư thì chỉ ghi nhận bằng số tiền cần trả.
        var paidAmount = Math.Min(cashReceived, amountDue);

        // Công nợ phát sinh
        var debtAmount = Math.Max(0, amountDue - paidAmount);

        // Tiền thối
        var changeAmount = Math.Max(0, cashReceived - amountDue);

        // Trạng thái thanh toán
        string paymentStatus;

        if (debtAmount > 0)
        {
            paymentStatus = "CONG_NO";
        }
        else
        {
            paymentStatus = "DA_THANH_TOAN";
        }

        var branchId = Session.CurrentUser?.ChiNhanhId ?? 1;
        var userId = Session.CurrentUser?.Id;

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // ============================================================
            // 1. TẠO HÓA ĐƠN
            // ============================================================

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
                @TrangThaiThanhToan,
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

                    // Khách cần trả
                    KhachCanTra = amountDue,

                    // Khách thực tế đã thanh toán
                    KhachDaThanhToan = paidAmount,

                    // Công nợ
                    ConNo = debtAmount,

                    TrangThaiThanhToan = paymentStatus
                },
                transaction);

            // ============================================================
            // 2. CHI TIẾT HÓA ĐƠN
            // ============================================================

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

            // ============================================================
            // 3. NẾU CÓ CÔNG NỢ -> LƯU VÀO LỊCH SỬ CÔNG NỢ KHÁCH HÀNG
            // ============================================================

            if (customer is not null && customer.Id > 0 && debtAmount > 0)
            {
                // Lấy công nợ hiện tại của khách
                const string currentDebtSql = """
                SELECT COALESCE(
                    SUM(ghi_no - ghi_co),
                    0
                )
                FROM lich_su_cong_no_khach_hang
                WHERE khach_hang_id = @KhachHangId;
                """;

                var currentDebt = await connection.ExecuteScalarAsync<decimal>(
                    currentDebtSql,
                    new
                    {
                        KhachHangId = customer.Id
                    },
                    transaction);

                // Công nợ mới sau khi phát sinh hóa đơn
                var newDebtBalance = currentDebt + debtAmount;

                // Ghi lịch sử công nợ
                const string debtSql = """
                INSERT INTO lich_su_cong_no_khach_hang
                (
                    khach_hang_id,
                    chi_nhanh_id,
                    loai_giao_dich,
                    loai_chung_tu,
                    ma_chung_tu,
                    ghi_no,
                    ghi_co,
                    so_du,
                    thoi_gian,
                    nguoi_thuc_hien_id,
                    ghi_chu
                )
                VALUES
                (
                    @KhachHangId,
                    @ChiNhanhId,
                    'BAN_HANG',
                    'HOA_DON',
                    @MaChungTu,
                    @GhiNo,
                    0,
                    @SoDu,
                    @ThoiGian,
                    @NguoiThucHienId,
                    @GhiChu
                );
                """;

                await connection.ExecuteAsync(
                    debtSql,
                    new
                    {
                        KhachHangId = customer.Id,
                        ChiNhanhId = branchId,

                        MaChungTu = invoiceCode,

                        // Khoản nợ phát sinh
                        GhiNo = debtAmount,

                        // Số dư công nợ mới
                        SoDu = newDebtBalance,

                        ThoiGian = now,

                        NguoiThucHienId = userId,

                        GhiChu =
                            $"Công nợ phát sinh từ hóa đơn {invoiceCode}"
                    },
                    transaction);
            }

            // ============================================================
            // 4. COMMIT
            // ============================================================

            await transaction.CommitAsync();

            // ============================================================
            // 5. TRẢ DỮ LIỆU CHO HÓA ĐƠN HIỂN THỊ
            // ============================================================

            return new HoaDonBanHangReceipt
            {
                MaHoaDon = invoiceCode,
                ThoiGianBan = now,

                TenKhachHang = customer?.TenKhachHang ?? "Khách lẻ",
                TenNhanVien = Session.CurrentUser?.HoTen ?? "",

                TongTienHang = total,
                GiamGia = safeDiscount,

                // Tổng khách cần trả
                KhachCanTra = amountDue,

                // Số tiền thực tế đã thanh toán
                KhachDaThanhToan = paidAmount,

                // Tiền khách đưa
                TienKhachDua = cashReceived,

                // Tiền thối
                TienThoi = changeAmount,

                // Công nợ
                ConNo = debtAmount,

                // Trạng thái
                TrangThaiThanhToan = debtAmount > 0
        ? "Thanh toán thiếu - Công nợ"
        : changeAmount > 0
            ? "Đã thanh toán đủ - Tiền thối"
            : "Đã thanh toán đủ",

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
            ncc.email AS Email,
            ncc.dia_chi AS DiaChi,
            ncc.phuong_xa AS PhuongXa,
            ncc.quan_huyen AS QuanHuyen,
            ncc.tinh_thanh AS TinhThanh,
            ncc.ma_so_thue AS MaSoThue,
            ncc.nguoi_lien_he AS NguoiLienHe,
            ncc.ghi_chu AS GhiChu,
            ncc.trang_thai AS TrangThai,
            ncc.ngay_tao AS NgayTao,
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
                OR ncc.email LIKE CONCAT('%', @Search, '%')
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

    /// <summary>
    /// Sinh mã nhà cung cấp tuần tự theo dạng NCC0000001.
    /// Chỉ dùng các mã NCC + số tối đa 7 chữ số để không bị ảnh hưởng
    /// bởi các mã cũ dạng timestamp.
    /// </summary>
    public async Task<string> TaoMaNhaCungCapMoiAsync()
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(ma_nha_cung_cap, 4) AS UNSIGNED)),
            0
        ) + 1
        FROM nha_cung_cap
        WHERE ma_nha_cung_cap REGEXP '^NCC[0-9]{1,7}$';
        """;

        var nextNumber = await connection.ExecuteScalarAsync<long>(sql);

        if (nextNumber > 9_999_999)
            throw new InvalidOperationException("Đã vượt giới hạn mã nhà cung cấp tự động NCC9999999.");

        return $"NCC{nextNumber:D7}";
    }

    public async Task<uint> ThemNhaCungCapAsync(NhaCungCap supplier)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        INSERT INTO nha_cung_cap
        (
            ma_nha_cung_cap,
            ten_nha_cung_cap,
            so_dien_thoai,
            email,
            dia_chi,
            phuong_xa,
            quan_huyen,
            tinh_thanh,
            ma_so_thue,
            nguoi_lien_he,
            ghi_chu,
            chi_nhanh_id,
            trang_thai,
            ngay_tao,
            ngay_cap_nhat
        )
        VALUES
        (
            @MaNhaCungCap,
            @TenNhaCungCap,
            @SoDienThoai,
            @Email,
            @DiaChi,
            @PhuongXa,
            @QuanHuyen,
            @TinhThanh,
            @MaSoThue,
            @NguoiLienHe,
            @GhiChu,
            @ChiNhanhId,
            1,
            NOW(),
            NOW()
        );
        SELECT LAST_INSERT_ID();
        """;

        return await connection.ExecuteScalarAsync<uint>(
            sql,
            new
            {
                supplier.MaNhaCungCap,
                supplier.TenNhaCungCap,
                supplier.SoDienThoai,
                supplier.Email,
                supplier.DiaChi,
                supplier.PhuongXa,
                supplier.QuanHuyen,
                supplier.TinhThanh,
                supplier.MaSoThue,
                supplier.NguoiLienHe,
                supplier.GhiChu,
                ChiNhanhId = Session.CurrentUser?.ChiNhanhId
            });
    }

    public async Task CapNhatNhaCungCapAsync(NhaCungCap supplier)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        UPDATE nha_cung_cap
        SET ten_nha_cung_cap = @TenNhaCungCap,
            so_dien_thoai = @SoDienThoai,
            email = @Email,
            dia_chi = @DiaChi,
            phuong_xa = @PhuongXa,
            quan_huyen = @QuanHuyen,
            tinh_thanh = @TinhThanh,
            ma_so_thue = @MaSoThue,
            nguoi_lien_he = @NguoiLienHe,
            ghi_chu = @GhiChu,
            ngay_cap_nhat = NOW()
        WHERE id = @Id AND trang_thai = 1;
        """;

        var affectedRows = await connection.ExecuteAsync(sql, supplier);
        if (affectedRows == 0)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại hoặc đã bị xóa.");
    }

    public async Task XoaNhaCungCapAsync(uint supplierId)
    {
        await using var connection = await OpenAsync();

        var affectedRows = await connection.ExecuteAsync(
            "UPDATE nha_cung_cap SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id AND trang_thai = 1;",
            new { Id = supplierId });

        if (affectedRows == 0)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại hoặc đã bị xóa.");
    }

    public async Task<int> ImportNhaCungCapTuFileAsync(string filePath)
        => await ImportNhaCungCapTuFileAsync(filePath, null);

    public async Task<int> ImportNhaCungCapTuFileAsync(string filePath, IReadOnlyCollection<NhaCungCap>? suppliersOverride)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Đường dẫn file không hợp lệ.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file danh sách nhà cung cấp.", filePath);

        var suppliers = suppliersOverride ?? ParseNhaCungCapFromFile(filePath);
        if (suppliers.Count == 0)
            return 0;

        var importedCount = 0;
        foreach (var supplier in suppliers)
        {
            if (string.IsNullOrWhiteSpace(supplier.TenNhaCungCap))
                continue;

            if (string.IsNullOrWhiteSpace(supplier.MaNhaCungCap))
                supplier.MaNhaCungCap = GenerateSupplierCode();

            try
            {
                await ThemNhaCungCapAsync(supplier);
                importedCount++;
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                // Bỏ qua mã nhà cung cấp trùng và tiếp tục các dòng còn lại.
            }
        }

        return importedCount;
    }
    public List<NhaCungCap> ParseNhaCungCapFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(
                "Đường dẫn file không hợp lệ.",
                nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                "Không tìm thấy file danh sách nhà cung cấp.",
                filePath);

        var rows = ReadSupplierRowsFromFile(filePath);

        return rows
            .Select(MapRowToNhaCungCap)
            .Where(supplier =>
                !string.IsNullOrWhiteSpace(supplier.TenNhaCungCap) ||
                !string.IsNullOrWhiteSpace(supplier.MaNhaCungCap))
            .ToList();
    }
    private static List<Dictionary<string, string>> ReadSupplierRowsFromFile(
    string filePath)
    {
        var extension = Path.GetExtension(filePath)
            .Trim()
            .ToLowerInvariant();

        if (extension == ".csv")
            return ReadCsvRows(filePath);

        if (extension == ".xlsx")
            return ReadSupplierExcelRows(filePath);

        throw new NotSupportedException(
            "File không được hỗ trợ. Vui lòng chọn file Excel (.xlsx) hoặc CSV.");
    }
    private static List<Dictionary<string, string>> ReadSupplierExcelRows(
    string filePath)
    {
        using var workbook = new XLWorkbook(filePath);

        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
            throw new InvalidOperationException(
                "File Excel không chứa sheet dữ liệu hợp lệ.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

        if (lastRow == 0)
            return [];

        // Tìm dòng tiêu đề nhà cung cấp
        var headerRowNumber = FindSupplierHeaderRow(
            worksheet,
            lastRow);

        if (headerRowNumber == 0)
        {
            throw new InvalidOperationException(
                "Không tìm thấy dòng tiêu đề nhà cung cấp trong file.");
        }

        var firstHeaderColumn =
            worksheet.Row(headerRowNumber)
                .FirstCellUsed()
                ?.Address.ColumnNumber ?? 1;

        var lastHeaderColumn =
            worksheet.Row(headerRowNumber)
                .LastCellUsed()
                ?.Address.ColumnNumber ?? 0;

        if (lastHeaderColumn < firstHeaderColumn)
            return [];

        var headers = new List<string>();

        for (var column = firstHeaderColumn;
             column <= lastHeaderColumn;
             column++)
        {
            var header = worksheet
                .Cell(headerRowNumber, column)
                .GetString()
                .Trim();

            headers.Add(header);
        }

        var result =
            new List<Dictionary<string, string>>();

        for (var rowNumber = headerRowNumber + 1;
             rowNumber <= lastRow;
             rowNumber++)
        {
            var row =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            var hasAnyValue = false;

            for (var index = 0;
                 index < headers.Count;
                 index++)
            {
                var header = headers[index];

                if (string.IsNullOrWhiteSpace(header))
                    continue;

                var columnNumber =
                    firstHeaderColumn + index;

                var value = worksheet
                    .Cell(rowNumber, columnNumber)
                    .GetFormattedString()
                    .Trim();

                if (!string.IsNullOrWhiteSpace(value))
                    hasAnyValue = true;

                row[header] = value;
            }

            if (hasAnyValue)
                result.Add(row);
        }

        return result;
    }
    private static int FindSupplierHeaderRow(
    IXLWorksheet worksheet,
    int lastRow)
    {
        for (var rowNumber = 1;
             rowNumber <= lastRow;
             rowNumber++)
        {
            var values = worksheet
                .Row(rowNumber)
                .CellsUsed()
                .Select(cell =>
                    NormalizeHeader(cell.GetString()))
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (values.Count == 0)
                continue;

            var hasSupplierCode =
                values.Any(value =>
                    value is
                        "manhacungcap" or
                        "mancc" or
                        "macungcap" or
                        "suppliercode");

            var hasSupplierName =
                values.Any(value =>
                    value is
                        "tennhacungcap" or
                        "tenncc" or
                        "suppliername");

            var hasPhone =
                values.Any(value =>
                    value is
                        "sodienthoai" or
                        "dienthoai" or
                        "sdt" or
                        "phone");

            if (hasSupplierCode ||
                hasSupplierName ||
                (hasPhone && values.Count >= 2))
            {
                return rowNumber;
            }
        }

        return 0;
    }
    private static NhaCungCap MapRowToNhaCungCap(Dictionary<string, string> row)
    {
        var supplier = new NhaCungCap
        {
            MaNhaCungCap = GetCellValue(row, "ma_nha_cung_cap", "mã nhà cung cấp", "ma ncc", "supplier code") ?? string.Empty,
            TenNhaCungCap = GetCellValue(row, "ten_nha_cung_cap", "tên nhà cung cấp", "ten ncc", "supplier name", "name") ?? string.Empty,
            SoDienThoai = GetCellValue(row, "so_dien_thoai", "số điện thoại", "dien thoai", "phone", "sdt"),
            Email = GetCellValue(row, "email"),
            DiaChi = GetCellValue(row, "dia_chi", "địa chỉ", "dia chi", "address"),
            PhuongXa = GetCellValue(row, "phuong_xa", "phường/xã", "phuong xa", "ward"),
            QuanHuyen = GetCellValue(row, "quan_huyen", "quận/huyện", "quan huyen", "district"),
            TinhThanh = GetCellValue(row, "tinh_thanh", "tỉnh/thành", "tinh thanh", "province"),
            MaSoThue = GetCellValue(row, "ma_so_thue", "mã số thuế", "ma so thue", "tax code"),
            NguoiLienHe = GetCellValue(row, "nguoi_lien_he", "người liên hệ", "lien he", "contact"),
            GhiChu = GetCellValue(row, "ghi_chu", "ghi chú", "note"),
            TrangThai = true
        };

        if (string.IsNullOrWhiteSpace(supplier.MaNhaCungCap))
            supplier.MaNhaCungCap = GenerateSupplierCode();

        return supplier;
    }
    private static string GenerateSupplierCode()
    {
        return $"NCC{DateTime.Now:yyyyMMddHHmmssfff}";
    }
    // ==========================================
    // QUẢN LÝ NHÓM SẢN PHẨM & ĐƠN VỊ TÍNH
    // ==========================================

    public async Task<uint> ThemNhomSanPhamAsync(string tenNhom)
    {
        await using var connection = await OpenAsync();
        const string sql = """
            INSERT INTO nhom_san_pham (ten_nhom, trang_thai)
            VALUES (@TenNhom, 1);
            SELECT LAST_INSERT_ID();
            """;
        return await connection.ExecuteScalarAsync<uint>(sql, new { TenNhom = tenNhom });
    }

    public async Task XoaNhomSanPhamAsync(uint id)
    {
        await using var connection = await OpenAsync();
        const string sql = "UPDATE nhom_san_pham SET trang_thai = 0 WHERE id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<uint> ThemDonViTinhAsync(string tenDonVi)
    {
        await using var connection = await OpenAsync();
        const string sql = """
            INSERT INTO don_vi_tinh (ten_don_vi, trang_thai)
            VALUES (@TenDonVi, 1);
            SELECT LAST_INSERT_ID();
            """;
        return await connection.ExecuteScalarAsync<uint>(sql, new { TenDonVi = tenDonVi });
    }

    public async Task XoaDonViTinhAsync(uint id)
    {
        await using var connection = await OpenAsync();
        const string sql = "UPDATE don_vi_tinh SET trang_thai = 0 WHERE id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    // ==========================================
    // PHIẾU NHẬP HÀNG
    // ==========================================

    /// <summary>
    /// Sinh mã phiếu nhập tự động theo dạng PN260927-001,
    /// cùng cơ chế sinh mã tuần tự như hóa đơn bán hàng.
    /// </summary>
    public async Task<string> TaoMaPhieuNhapMoiAsync()
    {
        await using var connection = await OpenAsync();
        var prefix = $"PN{DateTime.Now:yyMMdd}";

        const string sql = """
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(ma_phieu_nhap, 9) AS UNSIGNED)),
            0
        ) + 1
        FROM phieu_nhap_hang
        WHERE ma_phieu_nhap LIKE CONCAT(@Prefix, '-%');
        """;

        var nextNum = await connection.ExecuteScalarAsync<long>(sql, new { Prefix = prefix });
        return $"{prefix}-{nextNum:D3}";
    }

    public async Task<IReadOnlyList<PhieuNhapHang>> GetPhieuNhapHangAsync(
        string? search = null)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            p.id AS Id,
            p.ma_phieu_nhap AS MaPhieuNhap,
            p.nha_cung_cap_id AS NhaCungCapId,
            ncc.ma_nha_cung_cap AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            p.nguoi_nhap_id AS NguoiNhapId,
            nd.ho_ten AS TenNhanVien,
            p.thoi_gian_nhap AS ThoiGianNhap,
            p.tong_tien_hang AS TongTienHang,
            p.chi_phi_khac AS ChiPhiKhac,
            p.tong_tien_nhap AS TongTienNhap,
            p.da_thanh_toan AS DaThanhToan,
            p.con_no AS ConNo,
            p.trang_thai AS TrangThai,
            p.ghi_chu AS GhiChu
        FROM phieu_nhap_hang p
        LEFT JOIN nha_cung_cap ncc
            ON ncc.id = p.nha_cung_cap_id
        LEFT JOIN nguoi_dung nd
            ON nd.id = p.nguoi_nhap_id
        WHERE (
                @Search = ''
                OR p.ma_phieu_nhap LIKE CONCAT('%', @Search, '%')
                OR ncc.ten_nha_cung_cap LIKE CONCAT('%', @Search, '%')
              )
        ORDER BY p.thoi_gian_nhap DESC, p.id DESC;
        """;

        return (
            await connection.QueryAsync<PhieuNhapHang>(
                sql,
                new
                {
                    Search = search ?? ""
                })
        ).AsList();
    }

    /// <summary>
    /// Lấy danh sách phiếu nhập theo trang cho báo cáo.
    /// Chỉ đọc dữ liệu của trang hiện tại (LIMIT/OFFSET) kèm tổng số bản ghi,
    /// không tải toàn bộ bảng lên giao diện.
    /// </summary>
    public async Task<(IReadOnlyList<PhieuNhapHang> Items, int TongSoBanGhi)> GetPhieuNhapHangPagedAsync(
        string? search,
        int page,
        int pageSize)
    {
        if (page < 1)
            page = 1;

        if (pageSize < 1)
            pageSize = 15;

        var offset = (page - 1) * pageSize;
        var keyword = (search ?? "").Trim();

        const string countSql = """
        SELECT COUNT(*)
        FROM phieu_nhap_hang p
        LEFT JOIN nha_cung_cap ncc
            ON ncc.id = p.nha_cung_cap_id
        WHERE @Search = ''
           OR p.ma_phieu_nhap LIKE CONCAT('%', @Search, '%');
        """;

        const string pageSql = """
        SELECT
            p.id AS Id,
            p.ma_phieu_nhap AS MaPhieuNhap,
            p.nha_cung_cap_id AS NhaCungCapId,
            ncc.ma_nha_cung_cap AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            p.nguoi_nhap_id AS NguoiNhapId,
            nd.ho_ten AS TenNhanVien,
            p.thoi_gian_nhap AS ThoiGianNhap,
            p.tong_tien_hang AS TongTienHang,
            p.chi_phi_khac AS ChiPhiKhac,
            p.tong_tien_nhap AS TongTienNhap,
            p.da_thanh_toan AS DaThanhToan,
            p.con_no AS ConNo,
            p.trang_thai AS TrangThai,
            p.ghi_chu AS GhiChu
        FROM phieu_nhap_hang p
        LEFT JOIN nha_cung_cap ncc
            ON ncc.id = p.nha_cung_cap_id
        LEFT JOIN nguoi_dung nd
            ON nd.id = p.nguoi_nhap_id
        WHERE @Search = ''
           OR p.ma_phieu_nhap LIKE CONCAT('%', @Search, '%')
        ORDER BY p.thoi_gian_nhap DESC, p.id DESC
        LIMIT @PageSize OFFSET @Offset;
        """;

        await using var connection = await OpenAsync();

        var parameters = new
        {
            Search = keyword,
            PageSize = pageSize,
            Offset = offset
        };

        var tongSoBanGhi = Convert.ToInt32(
            await connection.ExecuteScalarAsync<long>(countSql, parameters));

        var items = (
            await connection.QueryAsync<PhieuNhapHang>(pageSql, parameters)
        ).AsList();

        return (items, tongSoBanGhi);
    }

    /// <summary>
    /// Hoàn thành phiếu nhập: lưu header, toàn bộ dòng chi tiết, cộng tồn kho,
    /// ghi lịch sử nhập xuất kho và cập nhật lại giá vốn bình quân — tất cả trong
    /// một transaction, hoặc rollback trọn vẹn nếu có bước nào lỗi.
    /// </summary>
    public async Task<uint> LuuPhieuNhapHangAsync(
        PhieuNhapHang phieu,
        IReadOnlyList<ChiTietPhieuNhapHang> items)
    {
        if (phieu is null)
            throw new ArgumentNullException(nameof(phieu));

        if (items is null)
            throw new ArgumentNullException(nameof(items));

        var dongHopLe = items
            .Where(item => item.SanPham.Id != 0 && item.SoLuong > 0)
            .ToList();

        if (dongHopLe.Count == 0)
            throw new InvalidOperationException("Phiếu nhập chưa có hàng hóa nào hợp lệ.");

        var branchId = phieu.ChiNhanhId != 0
            ? phieu.ChiNhanhId
            : Session.CurrentUser?.ChiNhanhId ?? 1;

        phieu.ChiNhanhId = branchId;
        phieu.NguoiNhapId ??= Session.CurrentUser?.Id;

        phieu.TongTienHang = dongHopLe.Sum(item => item.ThanhTien);
        phieu.ChiPhiKhac = phieu.ChiPhiKhac < 0 ? 0 : phieu.ChiPhiKhac;
        phieu.TongTienNhap = phieu.TongTienHang + phieu.ChiPhiKhac;
        phieu.DaThanhToan = Math.Clamp(phieu.DaThanhToan, 0, phieu.TongTienNhap);
        phieu.ConNo = phieu.TongTienNhap - phieu.DaThanhToan;
        phieu.TrangThai = "DA_NHAP_HANG";

        var hasExtraColumns = await HasPhieuNhapExtraColumnsAsync();

        var extraColumns = hasExtraColumns
            ? ",\n                    so_hoa_don_dau_vao,\n                    ma_dat_hang_nhap"
            : string.Empty;

        var extraValues = hasExtraColumns
            ? ",\n                    @SoHoaDonDauVao,\n                    @MaDatHangNhap"
            : string.Empty;

        var extraAssignments = hasExtraColumns
            ? ",\n                    so_hoa_don_dau_vao = @SoHoaDonDauVao,\n                    ma_dat_hang_nhap   = @MaDatHangNhap"
            : string.Empty;

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // ============================================================
            // 0. CHỐN GHI TRÙNG KHI BẤM "HOÀN THÀNH" NHIỀU LẦN
            //    Đọc trạng thái hiện tại trong transaction để tồn kho
            //    không bị cộng hai lần cho cùng một phiếu.
            // ============================================================

            var trangThaiHienTai = phieu.Id == 0
                ? null
                : await connection.ExecuteScalarAsync<string?>(
                    """
                    SELECT trang_thai
                    FROM phieu_nhap_hang
                    WHERE id = @Id
                    FOR UPDATE;
                    """,
                    new { phieu.Id },
                    transaction);

            // Phiếu đã hoàn thành thì tồn kho đã được cộng một lần,
            // không được cộng thêm lần nữa.
            if (trangThaiHienTai == "DA_NHAP_HANG")
                throw new InvalidOperationException(
                    "Phiếu nhập này đã hoàn thành trước đó. Tồn kho đã được cập nhật nên không thể hoàn thành lại.");

            // ============================================================
            // 1. HEADER
            // ============================================================

            if (phieu.Id == 0)
            {
                var insertSql = $"""
                INSERT INTO phieu_nhap_hang
                (
                    ma_phieu_nhap,
                    chi_nhanh_id,
                    nha_cung_cap_id,
                    nguoi_nhap_id,
                    thoi_gian_nhap,
                    tong_tien_hang,
                    giam_gia,
                    chi_phi_khac,
                    tong_tien_nhap,
                    da_thanh_toan,
                    con_no,
                    trang_thai,
                    ghi_chu{extraColumns},
                    ngay_tao,
                    ngay_cap_nhat
                )
                VALUES
                (
                    @MaPhieuNhap,
                    @ChiNhanhId,
                    @NhaCungCapId,
                    @NguoiNhapId,
                    @ThoiGianNhap,
                    @TongTienHang,
                    0,
                    @ChiPhiKhac,
                    @TongTienNhap,
                    @DaThanhToan,
                    @ConNo,
                    @TrangThai,
                    @GhiChu{extraValues},
                    NOW(),
                    NOW()
                );
                SELECT LAST_INSERT_ID();
                """;

                phieu.Id = await connection.ExecuteScalarAsync<uint>(
                    insertSql,
                    BuildPhieuNhapParameters(phieu),
                    transaction);
            }
            else
            {
                var updateSql = $"""
                UPDATE phieu_nhap_hang
                SET ma_phieu_nhap      = @MaPhieuNhap,
                    nha_cung_cap_id    = @NhaCungCapId,
                    nguoi_nhap_id      = @NguoiNhapId,
                    thoi_gian_nhap     = @ThoiGianNhap,
                    tong_tien_hang     = @TongTienHang,
                    giam_gia           = 0,
                    chi_phi_khac       = @ChiPhiKhac,
                    tong_tien_nhap     = @TongTienNhap,
                    da_thanh_toan      = @DaThanhToan,
                    con_no             = @ConNo,
                    trang_thai         = @TrangThai,
                    ghi_chu            = @GhiChu{extraAssignments},
                    ngay_cap_nhat      = NOW()
                WHERE id = @Id;
                """;

                await connection.ExecuteAsync(
                    updateSql,
                    BuildPhieuNhapParameters(phieu),
                    transaction);
            }

            // ============================================================
            // 2. CHI TIẾT PHIẾU NHẬP
            // ============================================================

            await connection.ExecuteAsync(
                "DELETE FROM chi_tiet_phieu_nhap_hang WHERE phieu_nhap_id = @PhieuNhapId;",
                new { PhieuNhapId = phieu.Id },
                transaction);

            const string insertDetailSql = """
            INSERT INTO chi_tiet_phieu_nhap_hang
            (
                phieu_nhap_id,
                san_pham_id,
                ma_san_pham,
                ten_san_pham,
                so_luong,
                don_gia_nhap,
                giam_gia,
                thanh_tien,
                ghi_chu
            )
            VALUES
            (
                @PhieuNhapId,
                @SanPhamId,
                @MaSanPham,
                @TenSanPham,
                @SoLuong,
                @DonGiaNhap,
                @GiamGia,
                @ThanhTien,
                NULL
            );
            """;

            foreach (var item in dongHopLe)
            {
                await connection.ExecuteAsync(
                    insertDetailSql,
                    new
                    {
                        PhieuNhapId = phieu.Id,
                        SanPhamId = item.SanPham.Id,
                        MaSanPham = item.SanPham.MaSanPham,
                        TenSanPham = item.SanPham.TenSanPham,
                        item.SoLuong,
                        item.DonGiaNhap,
                        item.GiamGia,
                        item.ThanhTien
                    },
                    transaction);
            }

            // ============================================================
            // 3. CẬP NHẬT TỒN KHO + LỊCH SỬ NHẬP XUẤT KHO
            // ============================================================

            await CapNhatTonKhoPhieuNhapAsync(
                connection,
                transaction,
                phieu,
                dongHopLe);

            await transaction.CommitAsync();

            return phieu.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task CapNhatTonKhoPhieuNhapAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PhieuNhapHang phieu,
        IReadOnlyList<ChiTietPhieuNhapHang> items)
    {
        const string selectTonSql = """
        SELECT so_luong_ton, gia_von_binh_quan
        FROM ton_kho
        WHERE chi_nhanh_id = @ChiNhanhId
          AND san_pham_id = @SanPhamId
        FOR UPDATE;
        """;

        const string upsertTonSql = """
        INSERT INTO ton_kho
        (
            chi_nhanh_id,
            san_pham_id,
            so_luong_ton,
            gia_von_binh_quan,
            ngay_cap_nhat
        )
        VALUES
        (
            @ChiNhanhId,
            @SanPhamId,
            @SoLuongTon,
            @GiaVonBinhQuan,
            NOW()
        )
        ON DUPLICATE KEY UPDATE
            so_luong_ton      = @SoLuongTon,
            gia_von_binh_quan = @GiaVonBinhQuan,
            ngay_cap_nhat     = NOW();
        """;

        const string updateGiaVonSql = """
        UPDATE san_pham
        SET gia_von      = @GiaVon,
            ngay_cap_nhat = NOW()
        WHERE id = @SanPhamId;
        """;

        const string insertLichSuSql = """
        INSERT INTO lich_su_nhap_xuat_kho
        (
            chi_nhanh_id,
            san_pham_id,
            loai_giao_dich,
            loai_chung_tu,
            ma_chung_tu,
            so_luong,
            ton_truoc,
            ton_sau,
            gia_von,
            nguoi_thuc_hien_id,
            thoi_gian,
            ghi_chu
        )
        VALUES
        (
            @ChiNhanhId,
            @SanPhamId,
            'NHAP',
            'PHIEU_NHAP_HANG',
            @MaChungTu,
            @SoLuong,
            @TonTruoc,
            @TonSau,
            @GiaVon,
            @NguoiThucHienId,
            NOW(),
            NULL
        );
        """;

        foreach (var item in items)
        {
            var tonHienTai = await connection.QueryAsync(
                selectTonSql,
                new
                {
                    phieu.ChiNhanhId,
                    SanPhamId = item.SanPham.Id
                },
                transaction);

            decimal tonTruoc = 0;
            decimal giaVonCu = 0;

            if (tonHienTai.FirstOrDefault() is IDictionary<string, object> row)
            {
                tonTruoc = ToDecimal(row.TryGetValue("so_luong_ton", out var soLuong) ? soLuong : null);
                giaVonCu = ToDecimal(row.TryGetValue("gia_von_binh_quan", out var giaVon) ? giaVon : null);
            }

            var tonSau = tonTruoc + item.SoLuong;

            // Giá vốn bình quân = (tồn cũ * giá cũ + lượng nhập * giá nhập) / tồn sau
            var giaVonBinhQuan = tonSau > 0
                ? (tonTruoc * giaVonCu + item.SoLuong * item.DonGiaNhap) / tonSau
                : item.DonGiaNhap;

            await connection.ExecuteAsync(
                upsertTonSql,
                new
                {
                    phieu.ChiNhanhId,
                    SanPhamId = item.SanPham.Id,
                    SoLuongTon = tonSau,
                    GiaVonBinhQuan = Math.Round(giaVonBinhQuan, 2)
                },
                transaction);

            await connection.ExecuteAsync(
                updateGiaVonSql,
                new
                {
                    GiaVon = item.DonGiaNhap,
                    SanPhamId = item.SanPham.Id
                },
                transaction);

            await connection.ExecuteAsync(
                insertLichSuSql,
                new
                {
                    phieu.ChiNhanhId,
                    SanPhamId = item.SanPham.Id,
                    MaChungTu = phieu.MaPhieuNhap,
                    item.SoLuong,
                    TonTruoc = tonTruoc,
                    TonSau = tonSau,
                    GiaVon = item.DonGiaNhap,
                    phieu.NguoiNhapId
                },
                transaction);
        }
    }

    private object BuildPhieuNhapParameters(PhieuNhapHang phieu) => new
    {
        phieu.Id,
        phieu.MaPhieuNhap,
        phieu.ChiNhanhId,
        phieu.NhaCungCapId,
        phieu.NguoiNhapId,
        phieu.ThoiGianNhap,
        phieu.TongTienHang,
        phieu.ChiPhiKhac,
        phieu.TongTienNhap,
        phieu.DaThanhToan,
        phieu.ConNo,
        phieu.TrangThai,
        GhiChu = string.IsNullOrWhiteSpace(phieu.GhiChu) ? null : phieu.GhiChu.Trim(),
        SoHoaDonDauVao = string.IsNullOrWhiteSpace(phieu.SoHoaDonDauVao) ? null : phieu.SoHoaDonDauVao.Trim(),
        MaDatHangNhap = string.IsNullOrWhiteSpace(phieu.MaDatHangNhap) ? null : phieu.MaDatHangNhap.Trim()
    };

    /// <summary>
    /// Hai cột so_hoa_don_dau_vao / ma_dat_hang_nhap là tuỳ chọn.
    /// Kiểm tra information_schema một lần để database cũ vẫn chạy được.
    /// </summary>
    private async Task<bool> HasPhieuNhapExtraColumnsAsync()
    {
        if (_hasPhieuNhapExtraColumns.HasValue)
            return _hasPhieuNhapExtraColumns.Value;

        try
        {
            await using var connection = await OpenAsync();

            const string sql = """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'phieu_nhap_hang'
              AND COLUMN_NAME IN ('so_hoa_don_dau_vao', 'ma_dat_hang_nhap');
            """;

            var count = await connection.ExecuteScalarAsync<long>(sql);
            _hasPhieuNhapExtraColumns = count >= 2;
        }
        catch
        {
            _hasPhieuNhapExtraColumns = false;
        }

        return _hasPhieuNhapExtraColumns.Value;
    }

    private static decimal ToDecimal(object? value)
    {
        if (value is null || value is DBNull)
            return 0;

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }
}

