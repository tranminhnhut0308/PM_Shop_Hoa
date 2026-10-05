using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Các truy vấn thống kê cho màn hình "Tổng quan".
/// <para>
/// Nguyên tắc: mọi phép tính (COUNT / SUM / GROUP BY / ORDER BY / LIMIT) đều để
/// database thực hiện. Không SELECT toàn bộ bảng về RAM rồi tính ở client, và
/// không hard-code số liệu.
/// </para>
/// <para>
/// Quy tắc doanh thu (đồng nhất với màn Hóa đơn):
/// chỉ tính hóa đơn <c>trang_thai_hoa_don = 'HOAN_THANH'</c>, doanh thu là
/// <c>khach_can_tra</c> (không phải "tiền khách đưa"), tiền thu là
/// <c>khach_da_thanh_toan</c> (không phải <c>tien_khach_dua</c>), nợ là <c>con_no</c>.
/// Nhờ vậy hóa đơn 500.000 với tiền thừa 100.000 vẫn ra doanh thu 500.000.
/// </para>
/// </summary>
public sealed class DashboardService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public DashboardService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    /// <summary>
    /// Khoảng thời gian được tính ở C# rồi truyền mốc <c>TuNgay</c>/<c>DenNgay</c>
    /// (khoảng nửa vờng, luôn kín) xuống SQL. Nhờ vậy chỉ có đúng một dạng điều kiện
    /// thời gian duy nhất cho mọi truy vấn, dễ kiểm chứng và không phụ thuộc
    /// múi giờ của database.
    /// </summary>
    public static (DateTime TuNgay, DateTime DenNgay) TinhKhoang(
        string khoangThoiGian,
        DateTime? tuNgayTuChon,
        DateTime? denNgayTuChon,
        DateTime homNay)
    {
        var dauNgay = homNay.Date;

        return khoangThoiGian switch
        {
            TongQuanKhoangThoiGian.BayNgay => (dauNgay.AddDays(-6), dauNgay.AddDays(1).AddTicks(-1)),

            TongQuanKhoangThoiGian.ThangNay => (
                new DateTime(homNay.Year, homNay.Month, 1),
                dauNgay.AddDays(1).AddTicks(-1)),

            TongQuanKhoangThoiGian.ThangTruoc => (
                new DateTime(homNay.Year, homNay.Month, 1).AddMonths(-1),
                new DateTime(homNay.Year, homNay.Month, 1).AddTicks(-1)),

            TongQuanKhoangThoiGian.NamNay => (
                new DateTime(homNay.Year, 1, 1),
                dauNgay.AddDays(1).AddTicks(-1)),

            TongQuanKhoangThoiGian.TuDien => (
                (tuNgayTuChon ?? dauNgay).Date,
                (denNgayTuChon ?? dauNgay).Date.AddDays(1).AddTicks(-1)),

            // Mặc định = Hôm nay
            _ => (dauNgay, dauNgay.AddDays(1).AddTicks(-1))
        };
    }

    /// <summary>
    /// Biểu thức SQL dùng để cắt bucket theo ngày/giờ/thứ. Chỉ dùng hàm có sẵn
    /// của MariaDB để không phụ thuộc tuỳ chọn đặc biệt.
    /// </summary>
    private static string BieuThucBucket(string chieu)
    {
        return chieu switch
        {
            TongQuanChieuDoanhThu.TheoGio =>
                "DATE_FORMAT(hd.thoi_gian_ban, '%Y-%m-%d %H:00:00')",

            // Theo thứ: gom theo thứ trong tuần (0 = Thứ 2 ... 6 = Chủ nhật).
            TongQuanChieuDoanhThu.TheoThu =>
                "WEEKDAY(hd.thoi_gian_ban)",

            _ => "DATE_FORMAT(hd.thoi_gian_ban, '%Y-%m-%d')"
        };
    }

    private static string TenCotTongTien() => "TongDoanhThu";

    /// <summary>
    /// Tải toàn bộ số liệu Dashboard. Các nhóm truy vấn độc lập được chạy song
    /// song để giảm thời gian chờ, mỗi nhóm chỉ lấy đúng số dòng cần dùng.
    /// </summary>
    public async Task<TongQuanDuLieu> TaiDuLieuAsync(
        string khoangThoiGian,
        string chieuDoanhThu,
        string xepHangTopHang,
        DateTime? tuNgayTuChon,
        DateTime? denNgayTuChon,
        DateTime homNay,
        CancellationToken token = default)
    {
        var (tuNgay, denNgay) = TinhKhoang(khoangThoiGian, tuNgayTuChon, denNgayTuChon, homNay);

        var ketQua = new TongQuanDuLieu
        {
            ThoiGianHienThi = $"{TongQuanDuLieu.NgayHienThi(tuNgay)} – {TongQuanDuLieu.NgayHienThi(denNgay)}"
        };

        // 5 nhóm độc lập: chạy song song, mỗi nhóm là 1-2 câu SQL có LIMIT.
        var taskBanHangHomNay = TaiSoLieuBanHangHomNayAsync(homNay, token);
        var taskBieuDo = TaiBieuDoDoanhThuAsync(chieuDoanhThu, tuNgay, denNgay, token);
        var taskHoatDong = TaiHoatDongGanDayAsync(token);
        var taskTopHang = TaiTopHangBanChayAsync(xepHangTopHang, tuNgay, denNgay, token);
        var taskTopKhach = TaiTopKhachHangAsync(tuNgay, denNgay, token);

        await Task.WhenAll(taskBanHangHomNay, taskBieuDo, taskHoatDong, taskTopHang, taskTopKhach);

        var banHang = await taskBanHangHomNay;
        var (tongDoanhThu, tongSoHoaDon, bieuDo) = await taskBieuDo;
        var hoatDong = await taskHoatDong;
        var topHang = await taskTopHang;
        var topKhach = await taskTopKhach;

        ketQua.DoanhThuHomNay = banHang.DoanhThu;
        ketQua.SoHoaDonHomNay = banHang.SoHoaDon;
        ketQua.DaThuHomNay = banHang.DaThu;
        ketQua.ConNoHomNay = banHang.ConNo;
        ketQua.SoDonHangHomNay = banHang.SoHoaDon;

        ketQua.TongDoanhThuKyChon = tongDoanhThu;
        ketQua.TongSoHoaDonKyChon = tongSoHoaDon;
        ketQua.BieuDoDoanhThu = bieuDo;
        ketQua.HoatDongGanDay = hoatDong;
        ketQua.TopHangBanChay = topHang;
        ketQua.TopKhachHang = topKhach;

        // Thống kê kho không phụ thuộc bộ lọc thời gian nên tải sau, giữ cho
        // 5 task đầu chạy cùng lúc.
        ketQua.ThongKeKho = await TaiThongKeKhoAsync(token);

        return ketQua;
    }

    /// <summary>5 thẻ "Kết quả bán hàng hôm nay" — 1 câu COUNT/SUM duy nhất.</summary>
    private async Task<(decimal DoanhThu, int SoHoaDon, decimal DaThu, decimal ConNo)> TaiSoLieuBanHangHomNayAsync(
        DateTime homNay,
        CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        var dauNgay = homNay.Date;
        var cuoiNgay = dauNgay.AddDays(1);

        const string sql = """
        SELECT
            COALESCE(SUM(hd.khach_can_tra), 0) AS DoanhThu,
            COUNT(*) AS SoHoaDon,
            COALESCE(SUM(hd.khach_da_thanh_toan), 0) AS DaThu,
            COALESCE(SUM(hd.con_no), 0) AS ConNo
        FROM hoa_don_ban_hang hd
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @DauNgay
          AND hd.thoi_gian_ban < @CuoiNgay;
        """;

        var row = await connection.QuerySingleAsync<(decimal DoanhThu, int SoHoaDon, decimal DaThu, decimal ConNo)>(
            new CommandDefinition(
                sql,
                new
                {
                    DauNgay = dauNgay,
                    CuoiNgay = cuoiNgay
                },
                cancellationToken: token));

        return row;
    }

    /// <summary>
    /// Biểu đồ doanh thu: GROUP BY theo bucket trong DB, rồi chuẩn hoá chiều cao
    /// cột ngay ở database nên client không phải tính lại.
    /// </summary>
    private async Task<(decimal TongDoanhThu, int TongSoHoaDon, List<TongQuanDiemDoanhThu> Diem)> TaiBieuDoDoanhThuAsync(
        string chieu,
        DateTime tuNgay,
        DateTime denNgay,
        CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        var bucket = BieuThucBucket(chieu);

        // Ngày: giới hạn 366 điểm để biểu đồ không kẹt khi chọn "Năm nay".
        // Giờ: 24 điểm/ngày nhưng chỉ lấy các giờ có phát sinh.
        var gioiHanDiem = chieu switch
        {
            TongQuanChieuDoanhThu.TheoGio => 24 * 31,
            TongQuanChieuDoanhThu.TheoThu => 7,
            _ => 366
        };

        var sql = $"""
        SELECT
            {bucket} AS Bucket,
            COALESCE(SUM(hd.khach_can_tra), 0) AS {TenCotTongTien()},
            COUNT(*) AS SoHoaDon,
            MAX(hd.thoi_gian_ban) AS ThoiGianLonNhat
        FROM hoa_don_ban_hang hd
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban <= @DenNgay
        GROUP BY Bucket
        ORDER BY Bucket
        LIMIT {gioiHanDiem};
        """;

        var hangDuLieu = (
            await connection.QueryAsync<(
                string Bucket,
                decimal TongDoanhThu,
                int SoHoaDon,
                DateTime ThoiGianLonNhat)>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TuNgay = tuNgay,
                        DenNgay = denNgay
                    },
                    cancellationToken: token))
        ).AsList();

        var tongDoanhThu = hangDuLieu.Sum(r => r.TongDoanhThu);
        var tongSoHoaDon = hangDuLieu.Sum(r => r.SoHoaDon);

        var maxDoanhThu = hangDuLieu.Count == 0 ? 0m : hangDuLieu.Max(r => r.TongDoanhThu);
        var tyLeBeRong = hangDuLieu.Count == 0 ? 0d : 100d / hangDuLieu.Count;

        var diem = new List<TongQuanDiemDoanhThu>(hangDuLieu.Count);

        foreach (var r in hangDuLieu)
        {
            // "Theo thứ" gom cả tuần nên Bucket là số 0..6, không phải chuỗi ngày.
            // Với ngày/giờ thì Bucket là chuỗi đã format sẵn từ DATE_FORMAT.
            var theoThu = chieu == TongQuanChieuDoanhThu.TheoThu;

            var mocThoiGian = !theoThu && DateTime.TryParse(r.Bucket, out var parsed)
                ? parsed
                : r.ThoiGianLonNhat;

            var tenThu = theoThu && int.TryParse(r.Bucket, out var thu)
                ? TenThuTrongTuan((DayOfWeek)((thu + 1) % 7))
                : TenThuTrongTuan(mocThoiGian.DayOfWeek);

            diem.Add(new TongQuanDiemDoanhThu
            {
                Nhan = chieu switch
                {
                    TongQuanChieuDoanhThu.TheoThu => tenThu,
                    TongQuanChieuDoanhThu.TheoGio => mocThoiGian.ToString("HH:00"),
                    _ => mocThoiGian.ToString("dd/MM"),
                },
                NhanDayDu = theoThu
                    ? $"{tenThu} trong khoảng đang chọn"
                    : chieu == TongQuanChieuDoanhThu.TheoGio
                        ? $"Giờ {mocThoiGian:HH:mm} ngày {mocThoiGian:dd/MM/yyyy}"
                        : $"Ngày {mocThoiGian:dd/MM/yyyy}",
                DoanhThu = r.TongDoanhThu,
                SoHoaDon = r.SoHoaDon,
                TyLeChieuCao = maxDoanhThu <= 0 ? 0 : (double)(r.TongDoanhThu / maxDoanhThu * 100m),
                TyLeBeRong = tyLeBeRong
            });
        }

        return (tongDoanhThu, tongSoHoaDon, diem);
    }

    private static string TenThuTrongTuan(DayOfWeek thu) => thu switch
    {
        DayOfWeek.Monday => "Thứ 2",
        DayOfWeek.Tuesday => "Thứ 3",
        DayOfWeek.Wednesday => "Thứ 4",
        DayOfWeek.Thursday => "Thứ 5",
        DayOfWeek.Friday => "Thứ 6",
        DayOfWeek.Saturday => "Thứ 7",
        _ => "Chủ nhật"
    };

    /// <summary>
    /// "Hoạt động gần đây" gộp từ các bảng đã có sẵn (hóa đơn, phiếu nhập, sổ quỹ)
    /// bằng UNION ALL rồi LIMIT — không tạo dữ liệu giả, không bảng lịch sử mới.
    /// </summary>
    private async Task<List<TongQuanHoatDong>> TaiHoatDongGanDayAsync(CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT * FROM (
            SELECT
                hd.thoi_gian_ban AS ThoiGian,
                hd.ma_hoa_don AS MaChungTu,
                'BAN_HANG' AS Loai,
                CASE
                    WHEN hd.trang_thai_hoa_don = 'HUY' THEN 'Hóa đơn đã hủy'
                    WHEN COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), '') = '' THEN 'Bán hóa đơn khách lẻ'
                    ELSE CONCAT('Bán hàng cho ', kh.ten_khach_hang)
                END AS TieuDe,
                COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS MoTa,
                CASE WHEN hd.trang_thai_hoa_don = 'HUY' THEN 0 ELSE hd.khach_can_tra END AS GiaTri
            FROM hoa_don_ban_hang hd
            LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id

            UNION ALL

            SELECT
                pn.thoi_gian_nhap AS ThoiGian,
                pn.ma_phieu_nhap AS MaChungTu,
                'NHAP_HANG' AS Loai,
                'Nhập hàng từ nhà cung cấp' AS TieuDe,
                COALESCE(NULLIF(TRIM(ncc.ten_nha_cung_cap), ''), 'Chưa chọn nhà cung cấp') AS MoTa,
                pn.tong_tien_nhap AS GiaTri
            FROM phieu_nhap_hang pn
            LEFT JOIN nha_cung_cap ncc ON ncc.id = pn.nha_cung_cap_id
            WHERE pn.trang_thai <> 'HUY'

            UNION ALL

            SELECT
                pc.thoi_gian_chi AS ThoiGian,
                pc.ma_phieu_chi AS MaChungTu,
                'SO_QUY' AS Loai,
                CASE
                    WHEN pc.trang_thai = 'HUY' THEN 'Hóa đơn đã hủy'
                    WHEN pc.hoa_don_ban_hang_id IS NOT NULL THEN 'Thu tiền bán hàng'
                    ELSE 'Ghi nhận thu chi sổ quỹ'
                END AS TieuDe,
                COALESCE(
                    NULLIF(TRIM(pc.noi_dung), ''),
                    CASE
                        WHEN pc.hoa_don_ban_hang_id IS NOT NULL THEN 'Hóa đơn đã thanh toán'
                        ELSE 'Thu chi thường'
                    END
                ) AS MoTa,
                CASE WHEN pc.trang_thai = 'HUY' THEN 0
                     WHEN pc.so_tien >= 0 THEN pc.so_tien
                     ELSE -pc.so_tien END AS GiaTri
            FROM phieu_chi pc
        ) AS hop_dong
        ORDER BY ThoiGian DESC, MaChungTu DESC
        LIMIT 40;
        """;

        return (
            await connection.QueryAsync<TongQuanHoatDong>(
                new CommandDefinition(sql, cancellationToken: token))
        ).AsList();
    }

    /// <summary>
    /// Top 10 hàng bán chạy — GROUP BY ở tầng database, chỉ trả về đúng 10 dòng.
    /// </summary>
    private async Task<List<TongQuanHangTop>> TaiTopHangBanChayAsync(
        string xepHang,
        DateTime tuNgay,
        DateTime denNgay,
        CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        var theoDoanhThu = xepHang == TongQuanXepHang.TheoDoanhThu;

        // Lưu ý: cotSapXep đã là biểu thức SUM(...) hoàn chỉnh nên không bọc SUM
        // thêm lần nữa, nếu không MariaDB báo "Invalid use of group function".
        var cotSapXep = theoDoanhThu ? "SUM(ct.thanh_tien)" : "SUM(ct.so_luong)";

        var sql = $"""
        SELECT
            ct.san_pham_id AS Id,
            COALESCE(NULLIF(TRIM(ct.ten_san_pham), ''), 'Sản phẩm đã xoá') AS Ten,
            COALESCE(NULLIF(TRIM(ct.ma_san_pham), ''), '-') AS TenPhu,
            COALESCE(SUM(ct.so_luong), 0) AS SoLuong,
            COALESCE(SUM(ct.thanh_tien), 0) AS DoanhThu,
            COALESCE({cotSapXep}, 0) AS GiaTriXepHang
        FROM chi_tiet_hoa_don_ban_hang ct
        INNER JOIN hoa_don_ban_hang hd
            ON hd.id = ct.hoa_don_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban <= @DenNgay
        GROUP BY ct.san_pham_id, Ten, TenPhu
        ORDER BY {cotSapXep} DESC, ct.ten_san_pham ASC
        LIMIT 10;
        """;

        var hangDuLieu = (
            await connection.QueryAsync<(uint Id, string Ten, string TenPhu, decimal SoLuong, decimal DoanhThu, decimal GiaTriXepHang)>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TuNgay = tuNgay,
                        DenNgay = denNgay
                    },
                    cancellationToken: token))
        ).AsList();

        var maxGiaTri = hangDuLieu.Count == 0 ? 0m : hangDuLieu.Max(r => r.GiaTriXepHang);

        var kq = new List<TongQuanHangTop>(hangDuLieu.Count);

        for (var i = 0; i < hangDuLieu.Count; i++)
        {
            var r = hangDuLieu[i];

            kq.Add(new TongQuanHangTop
            {
                ThuTu = i + 1,
                Ten = r.Ten,
                TenPhu = r.TenPhu,
                SoLuong = r.SoLuong,
                DoanhThu = r.DoanhThu,
                TyLePhanTram = maxGiaTri <= 0 ? 0 : (double)(r.GiaTriXepHang / maxGiaTri * 100m)
            });
        }

        return kq;
    }

    /// <summary>
    /// Top 10 khách hàng. Hóa đơn không gắn khách hàng ("khách lẻ") gộp chung
    /// thành một nhóm đúng như tên hiển thị sẵn có của phần mềm, không tạo
    /// khách hàng ảo trong database.
    /// </summary>
    private async Task<List<TongQuanHangTop>> TaiTopKhachHangAsync(
        DateTime tuNgay,
        DateTime denNgay,
        CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT
            COALESCE(kh.id, 0) AS Id,
            COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS Ten,
            COALESCE(kh.ma_khach_hang, 'Khách lẻ') AS TenPhu,
            COUNT(*) AS SoHoaDon,
            COALESCE(SUM(hd.khach_can_tra), 0) AS DoanhThu
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban <= @DenNgay
        GROUP BY kh.id, Ten, TenPhu
        ORDER BY DoanhThu DESC, Ten ASC
        LIMIT 10;
        """;

        var hangDuLieu = (
            await connection.QueryAsync<(uint Id, string Ten, string TenPhu, int SoHoaDon, decimal DoanhThu)>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TuNgay = tuNgay,
                        DenNgay = denNgay
                    },
                    cancellationToken: token))
        ).AsList();

        var maxDoanhThu = hangDuLieu.Count == 0 ? 0m : hangDuLieu.Max(r => r.DoanhThu);

        var kq = new List<TongQuanHangTop>(hangDuLieu.Count);

        for (var i = 0; i < hangDuLieu.Count; i++)
        {
            var r = hangDuLieu[i];

            kq.Add(new TongQuanHangTop
            {
                ThuTu = i + 1,
                Ten = r.Ten,
                TenPhu = $"{r.SoHoaDon:N0} hóa đơn",
                SoLuong = r.SoHoaDon,
                DoanhThu = r.DoanhThu,
                TyLePhanTram = maxDoanhThu <= 0 ? 0 : (double)(r.DoanhThu / maxDoanhThu * 100m)
            });
        }

        return kq;
    }

    /// <summary>
    /// Thống kê kho. "Sắp hết"/"hết hàng" lấy đúng quy tắc đang dùng ở tab Hàng hóa:
    /// tồn &lt;= 0 là hết hàng, tồn &lt;= ngưỡng tồn tối thiểu là sắp hết.
    /// Giá trị tồn kho lấy từ <c>ton_kho.so_luong_ton * gia_von_binh_quan</c>.
    /// </summary>
    private async Task<TongQuanThongKeKho> TaiThongKeKhoAsync(CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT
            (SELECT COUNT(*) FROM san_pham WHERE trang_thai = 1) AS TongSoHangHoa,
            (SELECT COUNT(*)
             FROM ton_kho tk
             INNER JOIN san_pham sp ON sp.id = tk.san_pham_id
             WHERE tk.so_luong_ton > 0
               AND tk.so_luong_ton <= sp.ton_toi_thieu) AS HangSapHet,
            (SELECT COUNT(*)
             FROM ton_kho tk
             INNER JOIN san_pham sp ON sp.id = tk.san_pham_id
             WHERE tk.so_luong_ton <= 0) AS HangHetHang,
            (SELECT COUNT(*) FROM khach_hang WHERE trang_thai = 1) AS TongSoKhachHang,
            (SELECT COUNT(*) FROM nha_cung_cap WHERE trang_thai = 1) AS TongSoNhaCungCap,
            (SELECT COALESCE(SUM(tk.so_luong_ton * tk.gia_von_binh_quan), 0)
             FROM ton_kho tk) AS GiaTriTonKho;
        """;

        return await connection.QuerySingleAsync<TongQuanThongKeKho>(
            new CommandDefinition(sql, cancellationToken: token));
    }
}