using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Nguồn dữ liệu duy nhất cho thông tin tiệm và cấu hình mẫu hóa đơn in.
/// <para>
/// Nguyên tắc: toàn bộ màn hình và mọi mẫu hóa đơn đều đọc qua service này.
/// Không màn nào tự gọi database riêng cho thông tin tiệm, không màn nào giữ
/// bản sao riêng rồi tự cập nhật, và không chỗ nào hard-code tên tiệm /
/// địa chỉ / số điện thoại. Sửa ở một chỗ là mọi hóa đơn sinh ra sau đó tự
/// lấy thông tin mới.
/// </para>
/// <para>
/// Bảng <c>thong_tin_tiem</c> chỉ có một bản ghi (id = 1). Lần lưu luôn UPDATE
/// đúng bản ghi đó nên không tích tụ bản ghi theo số lần bấm "Lưu".
/// </para>
/// </summary>
public sealed class StoreInfoService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public StoreInfoService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    // =========================================================
    // BẢNG LƯU TRỮ
    // =========================================================

    /// <summary>
    /// Các cột được bổ sung theo thời gian. Database cũ chưa có thì tự thêm,
    /// không bắt người dùng chạy script tay.
    /// </summary>
    private static readonly (string Ten, string DinhNghia)[] CotBoSung =
    [
        ("zalo", "varchar(30) NOT NULL DEFAULT ''"),
        ("facebook", "varchar(200) NOT NULL DEFAULT ''"),
        ("ten_ngan_hang", "varchar(120) NOT NULL DEFAULT ''"),
        ("so_tai_khoan", "varchar(60) NOT NULL DEFAULT ''"),
        ("chu_tai_khoan", "varchar(120) NOT NULL DEFAULT ''"),
        ("duong_dan_qr", "varchar(500) NOT NULL DEFAULT ''"),
        ("hien_thi_logo", "tinyint(1) NOT NULL DEFAULT 1"),
        ("hien_thi_qr", "tinyint(1) NOT NULL DEFAULT 1"),
        ("hien_thi_facebook", "tinyint(1) NOT NULL DEFAULT 1"),
        ("hien_thi_ngan_hang", "tinyint(1) NOT NULL DEFAULT 1"),
        ("hien_thi_dia_chi_khach_hang", "tinyint(1) NOT NULL DEFAULT 1"),
        ("kho_giay_in", "varchar(10) NOT NULL DEFAULT '80mm'"),
        ("kich_thuoc_qr", "int NOT NULL DEFAULT 32"),
        ("vi_tri_qr", "varchar(10) NOT NULL DEFAULT 'center'"),
        ("da_ap_dung_mac_dinh", "tinyint(1) NOT NULL DEFAULT 0"),
        ("da_chuyen_doi_kenh_lien_he", "tinyint(1) NOT NULL DEFAULT 0")
    ];

    /// <summary>
    /// Bảo đảm bảng <c>thong_tin_tiem</c> tồn tại, có đủ cột mới và có đúng một
    /// bản ghi. Database cũ chưa có bảng/cột thì tự tạo, không bắt người dùng
    /// chạy script tay. Lỗi tạo bảng (không đủ quyền DDL, mất mạng) không làm
    /// sập ứng dụng: service trả về thông tin mặc định và ghi log.
    /// </summary>
    private async Task<bool> DamBaoBangAsync()
    {
        try
        {
            await using var connection = await _taoKetNoi();

            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS thong_tin_tiem (
                    id             int(10) UNSIGNED NOT NULL,
                    ten_tiem       varchar(200) NOT NULL DEFAULT '',
                    dia_chi        varchar(300) NOT NULL DEFAULT '',
                    so_dien_thoai  varchar(30)  NOT NULL DEFAULT '',
                    zalo           varchar(30)  NOT NULL DEFAULT '',
                    facebook       varchar(200) NOT NULL DEFAULT '',
                    email          varchar(120) NOT NULL DEFAULT '',
                    ma_so_thue     varchar(30)  NOT NULL DEFAULT '',
                    website        varchar(200) NOT NULL DEFAULT '',
                    ten_ngan_hang  varchar(120) NOT NULL DEFAULT '',
                    so_tai_khoan   varchar(60)  NOT NULL DEFAULT '',
                    chu_tai_khoan  varchar(120) NOT NULL DEFAULT '',
                    duong_dan_logo varchar(500) NOT NULL DEFAULT '',
                    duong_dan_qr   varchar(500) NOT NULL DEFAULT '',
                    loi_cam_on     varchar(500) NOT NULL DEFAULT '',
                    hien_thi_logo  tinyint(1)   NOT NULL DEFAULT 1,
                    hien_thi_qr    tinyint(1)   NOT NULL DEFAULT 1,
                    hien_thi_facebook tinyint(1) NOT NULL DEFAULT 1,
                    hien_thi_ngan_hang tinyint(1) NOT NULL DEFAULT 1,
                    hien_thi_dia_chi_khach_hang tinyint(1) NOT NULL DEFAULT 1,
                    kho_giay_in    varchar(10)  NOT NULL DEFAULT '80mm',
                    kich_thuoc_qr  int          NOT NULL DEFAULT 32,
                    vi_tri_qr      varchar(10)  NOT NULL DEFAULT 'center',
                    da_ap_dung_mac_dinh tinyint(1) NOT NULL DEFAULT 0,
                    da_chuyen_doi_kenh_lien_he tinyint(1) NOT NULL DEFAULT 0,
                    ngay_cap_nhat  datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (id),
                    CONSTRAINT ck_thong_tin_tiem_id CHECK (id = 1)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                """);

            await DamBaoDuCotAsync(connection);

            var macDinh = ThongTinTiem.TaoMacDinh();

            // INSERT IGNORE để bản ghi duy nhất luôn tồn tại. Bản ghi mới được
            // gieo sẵn giá trị mặc định và đánh dấu đã áp dụng mặc định.
            await connection.ExecuteAsync(
                """
                INSERT IGNORE INTO thong_tin_tiem
                    (id, ten_tiem, dia_chi, so_dien_thoai, zalo, facebook,
                     ten_ngan_hang, so_tai_khoan, chu_tai_khoan, loi_cam_on,
                     da_ap_dung_mac_dinh)
                VALUES
                    (1, @TenTiem, @DiaChi, @SoDienThoai, @Zalo, @Facebook,
                     @TenNganHang, @SoTaiKhoan, @ChuTaiKhoan, @LoiCamOn, 1);
                """,
                new
                {
                    macDinh.TenTiem,
                    macDinh.DiaChi,
                    macDinh.SoDienThoai,
                    macDinh.Zalo,
                    macDinh.Facebook,
                    macDinh.TenNganHang,
                    macDinh.SoTaiKhoan,
                    macDinh.ChuTaiKhoan,
                    macDinh.LoiCamOn
                });

            // Nâng cấp từ phiên bản cũ: bản ghi đã có nhưng các cột mới còn rỗng.
            // Chỉ điền đúng một lần (cờ da_ap_dung_mac_dinh), không ghi đè dữ
            // liệu người dùng đã sửa và không tự điền lại sau khi họ xoá trắng.
            await connection.ExecuteAsync(
                """
                UPDATE thong_tin_tiem SET
                    ten_tiem      = IF(TRIM(ten_tiem) = '', @TenTiem, ten_tiem),
                    dia_chi       = IF(TRIM(dia_chi) = '', @DiaChi, dia_chi),
                    so_dien_thoai = IF(TRIM(so_dien_thoai) = '', @SoDienThoai, so_dien_thoai),
                    zalo          = IF(TRIM(zalo) = '', @Zalo, zalo),
                    facebook      = IF(TRIM(facebook) = '', @Facebook, facebook),
                    ten_ngan_hang = IF(TRIM(ten_ngan_hang) = '', @TenNganHang, ten_ngan_hang),
                    so_tai_khoan  = IF(TRIM(so_tai_khoan) = '', @SoTaiKhoan, so_tai_khoan),
                    chu_tai_khoan = IF(TRIM(chu_tai_khoan) = '', @ChuTaiKhoan, chu_tai_khoan),
                    loi_cam_on    = IF(TRIM(loi_cam_on) = '', @LoiCamOn, loi_cam_on),
                    da_ap_dung_mac_dinh = 1
                WHERE id = 1 AND da_ap_dung_mac_dinh = 0;
                """,
                new
                {
                    macDinh.TenTiem,
                    macDinh.DiaChi,
                    macDinh.SoDienThoai,
                    macDinh.Zalo,
                    macDinh.Facebook,
                    macDinh.TenNganHang,
                    macDinh.SoTaiKhoan,
                    macDinh.ChuTaiKhoan,
                    macDinh.LoiCamOn
                });

            await DamBaoBangKenhLienHeAsync(connection);
            return true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi tao bang thong_tin_tiem", ex);
            return false;
        }
    }

    /// <summary>
    /// MySQL không hỗ trợ ADD COLUMN IF NOT EXISTS trên mọi phiên bản, nên kiểm
    /// tra information_schema rồi mới ALTER. Lỗi từng cột chỉ ghi log để không
    /// chặn các cột còn lại.
    /// </summary>
    private static async Task DamBaoDuCotAsync(MySqlConnection connection)
    {
        var hienCo = (await connection.QueryAsync<string>(
            """
            SELECT LOWER(COLUMN_NAME)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'thong_tin_tiem';
            """)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (ten, dinhNghia) in CotBoSung)
        {
            if (hienCo.Contains(ten))
                continue;

            try
            {
                await connection.ExecuteAsync(
                    $"ALTER TABLE thong_tin_tiem ADD COLUMN `{ten}` {dinhNghia};");
            }
            catch (Exception ex)
            {
                SafeLog.WriteError($"Loi them cot thong_tin_tiem.{ten}", ex);
            }
        }
    }

    // =========================================================
    // ĐỌC
    // =========================================================

    /// <summary>
    /// Đọc thông tin tiệm đang lưu. Không bao giờ trả null: khi bảng chưa có
    /// hoặc đọc lỗi thì trả về bản ghi mặc định để giao diện vẫn hiển thị
    /// được (không trắng màn, không crash).
    /// </summary>
    public async Task<ThongTinTiem> LayThongTinAsync()
    {
        await DamBaoBangAsync();

        try
        {
            await using var connection = await _taoKetNoi();

            const string sql = """
            SELECT
                id,
                ten_tiem        AS TenTiem,
                dia_chi         AS DiaChi,
                so_dien_thoai   AS SoDienThoai,
                zalo            AS Zalo,
                facebook        AS Facebook,
                email           AS Email,
                ma_so_thue      AS MaSoThue,
                website         AS Website,
                ten_ngan_hang   AS TenNganHang,
                so_tai_khoan    AS SoTaiKhoan,
                chu_tai_khoan   AS ChuTaiKhoan,
                duong_dan_logo  AS DuongDanLogo,
                duong_dan_qr    AS DuongDanQr,
                loi_cam_on      AS LoiCamOn,
                hien_thi_logo   AS HienThiLogo,
                hien_thi_qr     AS HienThiQr,
                hien_thi_facebook AS HienThiFacebook,
                hien_thi_ngan_hang AS HienThiNganHang,
                hien_thi_dia_chi_khach_hang AS HienThiDiaChiKhachHang,
                kho_giay_in     AS KhoGiayIn,
                kich_thuoc_qr   AS KichThuocQr,
                vi_tri_qr       AS ViTriQr
            FROM thong_tin_tiem
            WHERE id = 1
            LIMIT 1;
            """;

            var info = await connection.QueryFirstOrDefaultAsync<ThongTinTiem>(sql);

            if (info is null)
                return ThongTinTiem.TaoMacDinh();

            info.KenhLienHes = (await connection.QueryAsync<KenhLienHe>(
                """
                SELECT id AS Id, ten_kenh AS Ten, noi_dung AS NoiDung,
                       hien_thi AS HienThi, vi_tri_hien_thi AS ViTriHienThi,
                       thu_tu AS ThuTu
                FROM thong_tin_tiem_kenh_lien_he
                WHERE thong_tin_tiem_id = @Id
                ORDER BY thu_tu, id;
                """,
                new { Id = ThongTinTiem.IdMacDinh })).ToList();

            return info;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi doc thong tin tiem", ex);
            return ThongTinTiem.TaoMacDinh();
        }
    }

    /// <summary>
    /// Thông tin tiệm đã nạp sẵn logo + QR, dùng cho các mẫu in nhanh (hóa đơn).
    /// </summary>
    public async Task<ThongTinTiem> LayThongTinKemLogoAsync()
    {
        var info = await LayThongTinAsync();
        NapAnh(info);
        return info;
    }

    // =========================================================
    // GHI
    // =========================================================

    /// <summary>
    /// Lưu thông tin tiệm. Bản ghi đã có thì UPDATE, chưa có thì INSERT — luôn
    /// một bản ghi duy nhất, không sinh dòng mới mỗi lần bấm "Lưu".
    /// </summary>
    public async Task LuuThongTinAsync(ThongTinTiem info)
    {
        await DamBaoBangAsync();

        await using var connection = await _taoKetNoi();
        await using var transaction = await connection.BeginTransactionAsync();

        // ON DUPLICATE KEY UPDATE xử lý cả hai nhánh trong một câu lệnh: bản ghi
        // có sẵn thì cập nhật tại chỗ, chưa có thì chèn, đều không tạo dòng thứ hai.
        // Các cột liên hệ cũ được giữ nguyên để không làm thay đổi những chứng từ
        // khác đang dùng chúng; hóa đơn bán hàng chỉ đọc danh sách kênh mới.
        const string sql = """
        INSERT INTO thong_tin_tiem
            (id, ten_tiem, dia_chi, so_dien_thoai, zalo, facebook, email, ma_so_thue,
             website, ten_ngan_hang, so_tai_khoan, chu_tai_khoan, duong_dan_logo,
             duong_dan_qr, loi_cam_on, hien_thi_logo, hien_thi_qr, hien_thi_facebook,
             hien_thi_ngan_hang, hien_thi_dia_chi_khach_hang, kho_giay_in,
             kich_thuoc_qr, vi_tri_qr, da_ap_dung_mac_dinh)
        VALUES
            (@Id, @TenTiem, @DiaChi, @SoDienThoai, @Zalo, @Facebook, @Email, @MaSoThue,
             @Website, @TenNganHang, @SoTaiKhoan, @ChuTaiKhoan, @DuongDanLogo,
             @DuongDanQr, @LoiCamOn, @HienThiLogo, @HienThiQr, @HienThiFacebook,
             @HienThiNganHang, @HienThiDiaChiKhachHang, @KhoGiayIn,
             @KichThuocQr, @ViTriQr, 1)
        ON DUPLICATE KEY UPDATE
            ten_tiem        = VALUES(ten_tiem),
            dia_chi         = VALUES(dia_chi),
            so_dien_thoai   = VALUES(so_dien_thoai),
            ma_so_thue      = VALUES(ma_so_thue),
            ten_ngan_hang   = VALUES(ten_ngan_hang),
            so_tai_khoan    = VALUES(so_tai_khoan),
            chu_tai_khoan   = VALUES(chu_tai_khoan),
            duong_dan_logo  = VALUES(duong_dan_logo),
            duong_dan_qr    = VALUES(duong_dan_qr),
            loi_cam_on      = VALUES(loi_cam_on),
            hien_thi_logo   = VALUES(hien_thi_logo),
            hien_thi_qr     = VALUES(hien_thi_qr),
            hien_thi_ngan_hang = VALUES(hien_thi_ngan_hang),
            hien_thi_dia_chi_khach_hang = VALUES(hien_thi_dia_chi_khach_hang),
            kho_giay_in     = VALUES(kho_giay_in),
            kich_thuoc_qr   = VALUES(kich_thuoc_qr),
            vi_tri_qr       = VALUES(vi_tri_qr),
            da_ap_dung_mac_dinh = 1,
            ngay_cap_nhat   = CURRENT_TIMESTAMP;
        """;

        await connection.ExecuteAsync(sql, new
        {
            Id = ThongTinTiem.IdMacDinh,
            TenTiem = (info.TenTiem ?? "").Trim(),
            DiaChi = (info.DiaChi ?? "").Trim(),
            SoDienThoai = (info.SoDienThoai ?? "").Trim(),
            Zalo = "",
            Facebook = "",
            Email = "",
            MaSoThue = (info.MaSoThue ?? "").Trim(),
            Website = "",
            TenNganHang = (info.TenNganHang ?? "").Trim(),
            SoTaiKhoan = (info.SoTaiKhoan ?? "").Trim(),
            ChuTaiKhoan = (info.ChuTaiKhoan ?? "").Trim(),
            DuongDanLogo = (info.DuongDanLogo ?? "").Trim(),
            DuongDanQr = (info.DuongDanQr ?? "").Trim(),
            LoiCamOn = (info.LoiCamOn ?? "").Trim(),
            HienThiLogo = info.HienThiLogo ? 1 : 0,
            HienThiQr = info.HienThiQr ? 1 : 0,
            HienThiFacebook = 0,
            HienThiNganHang = info.HienThiNganHang ? 1 : 0,
            HienThiDiaChiKhachHang = info.HienThiDiaChiKhachHang ? 1 : 0,
            KhoGiayIn = info.KhoGiayHienThi,
            KichThuocQr = info.KichThuocQrHienThi,
            ViTriQr = info.ViTriQrHienThi
        }, transaction);

        await LuuKenhLienHeAsync(connection, transaction, info.KenhLienHes);
        await transaction.CommitAsync();
    }

    private static async Task DamBaoBangKenhLienHeAsync(MySqlConnection connection)
    {
        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS thong_tin_tiem_kenh_lien_he (
                id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                thong_tin_tiem_id INT(10) UNSIGNED NOT NULL DEFAULT 1,
                ten_kenh VARCHAR(100) NOT NULL,
                noi_dung VARCHAR(500) NOT NULL,
                hien_thi TINYINT(1) NOT NULL DEFAULT 1,
                vi_tri_hien_thi VARCHAR(10) NOT NULL DEFAULT 'CUOI',
                thu_tu INT NOT NULL DEFAULT 0,
                PRIMARY KEY (id),
                KEY ix_tiem_kenh_thu_tu (thong_tin_tiem_id, thu_tu, id),
                CONSTRAINT fk_tiem_kenh_thong_tin
                    FOREIGN KEY (thong_tin_tiem_id) REFERENCES thong_tin_tiem(id)
                    ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);

        await using var transaction = await connection.BeginTransactionAsync();
        var legacy = await connection.QuerySingleAsync<LegacyContact>(
            """
            SELECT facebook AS Facebook, zalo AS Zalo, email AS Email,
                   website AS Website, hien_thi_facebook AS HienThiFacebook,
                   da_chuyen_doi_kenh_lien_he AS DaChuyenDoi
            FROM thong_tin_tiem
            WHERE id = 1
            FOR UPDATE;
            """,
            transaction: transaction);

        if (legacy.DaChuyenDoi)
        {
            await transaction.CommitAsync();
            return;
        }

        var oldChannels = new[]
        {
            new LegacyChannel("Facebook", legacy.Facebook, legacy.HienThiFacebook, KenhLienHe.ViTriCuoi),
            new LegacyChannel("Zalo", legacy.Zalo, true, KenhLienHe.ViTriCuoi),
            new LegacyChannel("Email", legacy.Email, true, KenhLienHe.ViTriDau),
            new LegacyChannel("Website", legacy.Website, true, KenhLienHe.ViTriDau)
        };

        foreach (var channel in oldChannels.Where(x => !string.IsNullOrWhiteSpace(x.NoiDung)))
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO thong_tin_tiem_kenh_lien_he
                    (thong_tin_tiem_id, ten_kenh, noi_dung, hien_thi, vi_tri_hien_thi, thu_tu)
                VALUES
                    (1, @Ten, @NoiDung, @HienThi, @ViTri, @ThuTu);
                """,
                new
                {
                    channel.Ten,
                    NoiDung = channel.NoiDung.Trim(),
                    HienThi = channel.HienThi ? 1 : 0,
                    ViTri = channel.ViTri,
                    ThuTu = channel.ViTri == KenhLienHe.ViTriDau ? 0 : 1
                },
                transaction);
        }

        await connection.ExecuteAsync(
            "UPDATE thong_tin_tiem SET da_chuyen_doi_kenh_lien_he = 1 WHERE id = 1;",
            transaction: transaction);
        await transaction.CommitAsync();
    }

    private static async Task LuuKenhLienHeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IReadOnlyCollection<KenhLienHe> channels)
    {
        var ids = new List<ulong>();

        foreach (var (channel, index) in channels.Select((item, index) => (item, index)))
        {
            var name = (channel.Ten ?? "").Trim();
            var content = (channel.NoiDung ?? "").Trim();
            var position = channel.ViTriHienThi == KenhLienHe.ViTriDau
                ? KenhLienHe.ViTriDau
                : KenhLienHe.ViTriCuoi;

            if (name.Length == 0 || content.Length == 0)
                throw new InvalidOperationException("Tên kênh và nội dung liên hệ không được để trống.");
            if (name.Length > 100 || content.Length > 500)
                throw new InvalidOperationException("Tên kênh tối đa 100 ký tự và nội dung tối đa 500 ký tự.");

            if (channel.Id == 0)
            {
                await connection.ExecuteAsync(
                    """
                    INSERT INTO thong_tin_tiem_kenh_lien_he
                        (thong_tin_tiem_id, ten_kenh, noi_dung, hien_thi, vi_tri_hien_thi, thu_tu)
                    VALUES
                        (1, @Ten, @NoiDung, @HienThi, @ViTri, @ThuTu);
                    """,
                    new
                    {
                        Ten = name,
                        NoiDung = content,
                        HienThi = channel.HienThi ? 1 : 0,
                        ViTri = position,
                        ThuTu = index
                    },
                    transaction);
                channel.Id = await connection.ExecuteScalarAsync<ulong>(
                    "SELECT LAST_INSERT_ID();", transaction: transaction);
            }
            else
            {
                var affected = await connection.ExecuteAsync(
                    """
                    UPDATE thong_tin_tiem_kenh_lien_he
                    SET ten_kenh = @Ten, noi_dung = @NoiDung, hien_thi = @HienThi,
                        vi_tri_hien_thi = @ViTri, thu_tu = @ThuTu
                    WHERE id = @Id AND thong_tin_tiem_id = 1;
                    """,
                    new
                    {
                        channel.Id,
                        Ten = name,
                        NoiDung = content,
                        HienThi = channel.HienThi ? 1 : 0,
                        ViTri = position,
                        ThuTu = index
                    },
                    transaction);

                if (affected == 0 && await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM thong_tin_tiem_kenh_lien_he WHERE id = @Id AND thong_tin_tiem_id = 1;",
                        new { channel.Id }, transaction) == 0)
                    throw new InvalidOperationException("Kênh liên hệ không thuộc cửa hàng hiện tại.");
            }

            ids.Add(channel.Id);
        }

        if (ids.Count == 0)
        {
            await connection.ExecuteAsync(
                "DELETE FROM thong_tin_tiem_kenh_lien_he WHERE thong_tin_tiem_id = 1;",
                transaction: transaction);
        }
        else
        {
            await connection.ExecuteAsync(
                "DELETE FROM thong_tin_tiem_kenh_lien_he WHERE thong_tin_tiem_id = 1 AND id NOT IN @Ids;",
                new { Ids = ids },
                transaction);
        }
    }

    private sealed class LegacyContact
    {
        public string Facebook { get; set; } = "";
        public string Zalo { get; set; } = "";
        public string Email { get; set; } = "";
        public string Website { get; set; } = "";
        public bool HienThiFacebook { get; set; }
        public bool DaChuyenDoi { get; set; }
    }

    private sealed record LegacyChannel(string Ten, string NoiDung, bool HienThi, string ViTri);

    // =========================================================
    // HÌNH ẢNH (LOGO / QR)
    // =========================================================

    /// <summary>
    /// Nạp logo và QR từ đường dẫn đã lưu vào đối tượng. File không tồn tại hoặc
    /// không đọc được thì để trống — hóa đơn vẫn in bình thường, chỉ không có ảnh.
    /// </summary>
    public void NapAnh(ThongTinTiem info)
    {
        info.LogoBitmap = DocAnh(info.DuongDanLogo);
        info.QrBitmap = DocAnh(info.DuongDanQr);
    }

    private static Avalonia.Media.Imaging.Bitmap? DocAnh(string? duongDan)
    {
        if (string.IsNullOrWhiteSpace(duongDan))
            return null;

        try
        {
            return File.Exists(duongDan)
                ? new Avalonia.Media.Imaging.Bitmap(duongDan)
                : null;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi nap anh tiem", ex);
            return null;
        }
    }

    /// <summary>Giữ lại tên cũ cho các chỗ đang gọi.</summary>
    public void NapLogo(ThongTinTiem info) => NapAnh(info);

    /// <summary>
    /// Chép file logo người dùng chọn vào thư mục dữ liệu ứng dụng rồi trả về
    /// đường dẫn đó.
    /// </summary>
    public Task<string> LuuLogoAsync(string duongDanFileGoc)
        => LuuAnhAsync(duongDanFileGoc, "logo", "logo_tiem");

    /// <summary>
    /// Chép file QR người dùng chọn vào thư mục dữ liệu ứng dụng rồi trả về
    /// đường dẫn đó.
    /// </summary>
    public Task<string> LuuQrAsync(string duongDanFileGoc)
        => LuuAnhAsync(duongDanFileGoc, "qr", "qr_tiem");

    /// <summary>
    /// Chép ảnh vào thư mục dữ liệu ứng dụng. Lưu bản sao thay vì giữ đường dẫn
    /// tới file gốc: người dùng có thể di chuyển/xoá file sau khi chọn, hoặc
    /// chạy phần mềm trên máy khác — bản sao vẫn còn nên hóa đơn không mất ảnh.
    /// </summary>
    private static async Task<string> LuuAnhAsync(
        string duongDanFileGoc,
        string thuMucCon,
        string tenCoDinh)
    {
        if (string.IsNullOrWhiteSpace(duongDanFileGoc) || !File.Exists(duongDanFileGoc))
            throw new FileNotFoundException("File ảnh không tồn tại.", duongDanFileGoc);

        var thuMuc = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShopHoa",
            thuMucCon);

        Directory.CreateDirectory(thuMuc);

        // Giữ đuôi file gốc (png/jpg/...) để phần mềm đoán đúng kiểu ảnh.
        var duoi = Path.GetExtension(duongDanFileGoc);

        if (string.IsNullOrWhiteSpace(duoi) || duoi.Length > 10)
            duoi = ".png";

        // Tên cố định: ứng dụng chỉ dùng một logo / một QR, không tích tụ file
        // rác mỗi lần người dùng chọn ảnh khác.
        var dichDen = Path.Combine(thuMuc, tenCoDinh + duoi.ToLowerInvariant());

        if (string.Equals(
                Path.GetFullPath(duongDanFileGoc),
                Path.GetFullPath(dichDen),
                StringComparison.OrdinalIgnoreCase))
        {
            return dichDen;
        }

        await using (var input = File.OpenRead(duongDanFileGoc))
        await using (var output = File.Create(dichDen))
        {
            await input.CopyToAsync(output);
        }

        // Xoá bản cũ khác đuôi để lần nạp sau không trúng nhầm ảnh cũ.
        foreach (var file in Directory.EnumerateFiles(thuMuc, tenCoDinh + ".*"))
        {
            if (!string.Equals(file, dichDen, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(file); } catch { /* không quan trọng */ }
            }
        }

        return dichDen;
    }

    /// <summary>Xoá file ảnh đã lưu (nếu nằm trong thư mục dữ liệu ứng dụng).</summary>
    public void XoaAnh(string? duongDan)
    {
        if (string.IsNullOrWhiteSpace(duongDan))
            return;

        try
        {
            var goc = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShopHoa");

            if (Path.GetFullPath(duongDan).StartsWith(
                    Path.GetFullPath(goc),
                    StringComparison.OrdinalIgnoreCase)
                && File.Exists(duongDan))
            {
                File.Delete(duongDan);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi xoa anh tiem", ex);
        }
    }
}
