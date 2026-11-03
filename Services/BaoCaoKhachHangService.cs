using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ màn "Báo cáo khách hàng": tổng hợp số đơn, tổng tiền hàng, giảm giá,
/// giá trị mua, đã thanh toán và công nợ theo từng khách hàng trong khoảng
/// thời gian đang chọn.
/// <para>
/// Mọi con số tính trong SQL (GROUP BY / SUM / ORDER BY / LIMIT) theo đúng
/// chuẩn báo cáo quản trị: bộ lọc thời gian dùng thoi_gian_ban >= Tu AND < Den+1
/// với hóa đơn trang_thai_hoa_don = 'HOAN_THANH'. Bảng danh sách phân trang bằng
/// LIMIT/OFFSET.
/// </para>
/// </summary>
public sealed class BaoCaoKhachHangService
{
    /// <summary>Khoá sắp xếp hợp lệ → biểu thức ORDER BY (chỉ nhận khoá do chương trình định nghĩa).</summary>
    private static readonly IReadOnlyDictionary<string, string> CotSapXep =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MaKhachHang"] = "kh.ma_khach_hang",
            ["TenKhachHang"] = "kh.ten_khach_hang",
            ["SoDienThoai"] = "kh.so_dien_thoai",
            ["SoDonHang"] = "SoDonHang",
            ["TongTienHang"] = "TongTienHang",
            ["GiamGia"] = "GiamGia",
            ["GiaTriMuaHang"] = "GiaTriMuaHang",
            ["DaThanhToan"] = "DaThanhToan",
            ["CongNo"] = "CongNo"
        };

    public const string KhoaMacDinh = "GiaTriMuaHang";

    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public BaoCaoKhachHangService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    public static (DateTime TuNgay, DateTime DenNgay) TinhKhoang(DateTime tuNgay, DateTime denNgay)
    {
        var tu = tuNgay.Date;
        var den = denNgay.Date;
        return (tu, den.AddDays(1));
    }

    /// <summary>
    /// Tải dữ liệu toàn diện cho màn hình và xuất báo cáo Excel quản trị khách hàng.
    /// </summary>
    public async Task<BaoCaoKhachHangDuLieu> TaiDuLieuAsync(
        DateTime tuNgay,
        DateTime denNgay,
        string? timKiem,
        string sapXepKho,
        bool sapXepTang,
        string metricKho,
        int trang,
        int soDongTrang,
        CancellationToken token = default)
    {
        var (tu, denKeTiep) = TinhKhoang(tuNgay, denNgay);

        await using var conn = await _taoKetNoi();

        var dk = new DieuKien
        {
            TuNgay = tu,
            DenKeTiep = denKeTiep,
            TimKiem = string.IsNullOrWhiteSpace(timKiem) ? null : "%" + timKiem.Trim() + "%"
        };

        var tongHop = await TaiTongHopAsync(conn, dk, token);
        tongHop.TuNgay = tu;
        tongHop.DenNgay = denKeTiep.AddDays(-1);

        int soDong = soDongTrang < 1 ? 15 : soDongTrang;
        int trangHieu = trang < 1 ? 1 : trang;
        int offset = (trangHieu - 1) * soDong;

        var dongTrang = await TaiTrangAsync(conn, dk, sapXepKho, sapXepTang, soDong, offset, token);
        for (int i = 0; i < dongTrang.Count; i++)
            dongTrang[i].Stt = offset + i + 1;

        var top = await TaiTopAsync(conn, dk, metricKho, token);
        for (int i = 0; i < top.Count; i++)
            top[i].Stt = i + 1;
        TinhThanhBar(top, metricKho);

        // Top 10 khách hàng theo doanh thu kèm tổng SL mua (dùng cho báo cáo quản trị Excel)
        var top10DoanhThu = await TaiTop10DoanhThuAsync(conn, dk, token);
        for (int i = 0; i < top10DoanhThu.Count; i++)
            top10DoanhThu[i].Stt = i + 1;

        // Phân tích khách hàng theo nhóm
        var nhomList = await TaiNhomKhachHangAsync(conn, dk, tongHop.GiaTriMuaHang, token);

        // Danh sách khách hàng phục vụ phân tích công nợ
        var danhSachCongNo = await TaiDanhSachCongNoAsync(conn, dk, token);
        for (int i = 0; i < danhSachCongNo.Count; i++)
            danhSachCongNo[i].Stt = i + 1;

        return new BaoCaoKhachHangDuLieu
        {
            TongHop = tongHop,
            Top = top,
            Top10DoanhThu = top10DoanhThu,
            NhomKhachHang = nhomList,
            DanhSachCongNo = danhSachCongNo,
            DongTrang = dongTrang
        };
    }

    // ============================================================
    // TRUY VẤN CON
    // ============================================================

    private sealed class DieuKien
    {
        public DateTime TuNgay { get; init; }
        public DateTime DenKeTiep { get; init; }
        public string? TimKiem { get; init; }

        public string Where =>
            @"hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep"
            + (TimKiem is null
                ? ""
                : @"
  AND (kh.ma_khach_hang LIKE @TimKiem OR kh.ten_khach_hang LIKE @TimKiem OR kh.so_dien_thoai LIKE @TimKiem)");

        public object Params => new { TuNgay, DenKeTiep, TimKiem };

        public const string From = @"
FROM hoa_don_ban_hang hd
INNER JOIN khach_hang kh ON kh.id = hd.khach_hang_id";
    }

    private const string TongHopCot = @"
  COUNT(*) AS SoDonHang,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.tong_tien_hang ELSE 0 END), 0) AS TongTienHang,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.giam_gia ELSE 0 END), 0) AS GiamGia,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.tong_tien_hang - hd.giam_gia ELSE 0 END), 0) AS GiaTriMuaHang,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.khach_da_thanh_toan ELSE 0 END), 0) AS DaThanhToan,
  COALESCE(SUM(CASE WHEN hd.cong_no_da_ghi_nhan = 1 THEN hd.con_no ELSE 0 END), 0) AS CongNo";

    private const string NhomKhach = @"kh.id, kh.ma_khach_hang, kh.ten_khach_hang, kh.so_dien_thoai";

    private async Task<BaoCaoKhachHangTongHopKy> TaiTongHopAsync(
        MySqlConnection conn, DieuKien dk, CancellationToken token)
    {
        string sql = @"
SELECT
  COUNT(DISTINCT kh.id) AS TongKhachHang,
  COUNT(*) AS SoDonHang,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.tong_tien_hang ELSE 0 END), 0) AS TongTienHang,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.giam_gia ELSE 0 END), 0) AS GiamGia,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.tong_tien_hang - hd.giam_gia ELSE 0 END), 0) AS GiaTriMuaHang,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.khach_da_thanh_toan ELSE 0 END), 0) AS DaThanhToan,
  COALESCE(SUM(CASE WHEN hd.cong_no_da_ghi_nhan = 1 THEN hd.con_no ELSE 0 END), 0) AS CongNo,
  COUNT(DISTINCT CASE WHEN hd.cong_no_da_ghi_nhan = 1 AND hd.con_no > 0 THEN kh.id END) AS SoKhachConNo,
  COUNT(DISTINCT CASE WHEN hd.doanh_so_da_ghi_nhan = 1 AND hd.khach_da_thanh_toan > 0 THEN kh.id END) AS SoKhachCoThanhToan
" + DieuKien.From + @"
WHERE " + dk.Where + @";
";

        return await conn.QueryFirstOrDefaultAsync<BaoCaoKhachHangTongHopKy>(
                   new CommandDefinition(sql, dk.Params, cancellationToken: token))
               ?? new BaoCaoKhachHangTongHopKy();
    }

    private async Task<List<BaoCaoKhachHangDong>> TaiTrangAsync(
        MySqlConnection conn, DieuKien dk, string sapXepKho, bool sapXepTang,
        int soDong, int offset, CancellationToken token)
    {
        string orderBy = LayOrderBy(sapXepKho, sapXepTang);

        string sql = @"
SELECT
  kh.id AS Id,
  COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
  COALESCE(kh.ten_khach_hang, '') AS TenKhachHang,
  COALESCE(kh.so_dien_thoai, '') AS SoDienThoai,
  " + TongHopCot + DieuKien.From + @"
WHERE " + dk.Where + @"
GROUP BY " + NhomKhach + @"
ORDER BY " + orderBy + @", kh.id
LIMIT @SoDong OFFSET @Offset;
";

        var thamSo = new
        {
            TuNgay = dk.TuNgay,
            DenKeTiep = dk.DenKeTiep,
            TimKiem = dk.TimKiem,
            SoDong = soDong,
            Offset = offset
        };

        var rs = await conn.QueryAsync<BaoCaoKhachHangDong>(
            new CommandDefinition(sql, thamSo, cancellationToken: token));
        return rs.AsList();
    }

    private async Task<List<BaoCaoKhachHangTop>> TaiTopAsync(
        MySqlConnection conn, DieuKien dk, string metricKho, CancellationToken token)
    {
        string orderBy = LayOrderBy(metricKho, sapXepTang: false);

        string sql = @"
SELECT
  kh.id AS Id,
  COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
  COALESCE(kh.ten_khach_hang, '') AS TenKhachHang,
  COALESCE(kh.so_dien_thoai, '') AS SoDienThoai,
  " + TongHopCot + DieuKien.From + @"
WHERE " + dk.Where + @"
GROUP BY " + NhomKhach + @"
ORDER BY " + orderBy + @", SoDonHang DESC, kh.id
LIMIT 10;
";

        var rs = await conn.QueryAsync<BaoCaoKhachHangTop>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    /// <summary>
    /// Tải Top 10 khách hàng theo doanh thu kèm tổng số lượng sản phẩm mua thực tế (không cộng trùng).
    /// </summary>
    private async Task<List<BaoCaoKhachHangTop>> TaiTop10DoanhThuAsync(
        MySqlConnection conn, DieuKien dk, CancellationToken token)
    {
        string sql = @"
SELECT
  kh.id AS Id,
  COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
  COALESCE(kh.ten_khach_hang, '') AS TenKhachHang,
  COALESCE(kh.so_dien_thoai, '') AS SoDienThoai,
  COUNT(hd.id) AS SoDonHang,
  COALESCE(SUM(ct.tong_sl), 0) AS TongSoLuongMua,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.tong_tien_hang - hd.giam_gia ELSE 0 END), 0) AS GiaTriMuaHang,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.khach_da_thanh_toan ELSE 0 END), 0) AS DaThanhToan,
  COALESCE(SUM(CASE WHEN hd.cong_no_da_ghi_nhan = 1 THEN hd.con_no ELSE 0 END), 0) AS CongNo
FROM hoa_don_ban_hang hd
INNER JOIN khach_hang kh ON kh.id = hd.khach_hang_id
LEFT JOIN (
  SELECT hoa_don_id, SUM(so_luong) AS tong_sl
  FROM chi_tiet_hoa_don_ban_hang
  GROUP BY hoa_don_id
) ct ON ct.hoa_don_id = hd.id
WHERE " + dk.Where + @"
GROUP BY kh.id, kh.ma_khach_hang, kh.ten_khach_hang, kh.so_dien_thoai
ORDER BY GiaTriMuaHang DESC, SoDonHang DESC, kh.id
LIMIT 10;
";

        var rs = await conn.QueryAsync<BaoCaoKhachHangTop>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        var list = rs.AsList();
        foreach (var x in list)
        {
            x.GiaTri = x.GiaTriMuaHang;
            x.LaTien = true;
        }
        return list;
    }

    /// <summary>
    /// Phân tích doanh thu và cơ cấu khách hàng theo nhóm.
    /// </summary>
    private async Task<List<BaoCaoKhachHangNhom>> TaiNhomKhachHangAsync(
        MySqlConnection conn, DieuKien dk, decimal tongDoanhThuKy, CancellationToken token)
    {
        string sql = @"
SELECT
  COALESCE(nkh.ten_nhom, 'Chưa phân nhóm') AS TenNhom,
  COUNT(DISTINCT kh.id) AS SoKhachHang,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN hd.tong_tien_hang - hd.giam_gia ELSE 0 END), 0) AS DoanhThu
FROM hoa_don_ban_hang hd
INNER JOIN khach_hang kh ON kh.id = hd.khach_hang_id
LEFT JOIN nhom_khach_hang nkh ON nkh.id = kh.nhom_khach_hang_id
WHERE " + dk.Where + @"
GROUP BY COALESCE(nkh.ten_nhom, 'Chưa phân nhóm')
ORDER BY DoanhThu DESC;
";

        var rs = await conn.QueryAsync<BaoCaoKhachHangNhom>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        var list = rs.AsList();
        for (int i = 0; i < list.Count; i++)
        {
            list[i].Stt = i + 1;
            list[i].TyLePhanTram = tongDoanhThuKy > 0
                ? (double)(list[i].DoanhThu / tongDoanhThuKy * 100m)
                : 0.0;
        }
        return list;
    }

    /// <summary>
    /// Lấy danh sách khách hàng có công nợ phát sinh trong kỳ để phân tích công nợ chi tiết.
    /// </summary>
    private async Task<List<BaoCaoKhachHangDong>> TaiDanhSachCongNoAsync(
        MySqlConnection conn, DieuKien dk, CancellationToken token)
    {
        string sql = @"
SELECT
  kh.id AS Id,
  COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
  COALESCE(kh.ten_khach_hang, '') AS TenKhachHang,
  COALESCE(kh.so_dien_thoai, '') AS SoDienThoai,
  " + TongHopCot + DieuKien.From + @"
WHERE " + dk.Where + @"
GROUP BY " + NhomKhach + @"
HAVING CongNo > 0 OR DaThanhToan > 0
ORDER BY CongNo DESC, GiaTriMuaHang DESC
LIMIT 100;
";

        var rs = await conn.QueryAsync<BaoCaoKhachHangDong>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    /// <summary>Chuyển khoá sắp xếp của UI sang biểu thức ORDER BY (chỉ nhận khoá whitelist).</summary>
    private static string LayOrderBy(string? kho, bool sapXepTang)
    {
        var cot = kho is not null && CotSapXep.TryGetValue(kho, out var expr)
            ? expr
            : CotSapXep[KhoaMacDinh];

        return $"{cot} {(sapXepTang ? "ASC" : "DESC")}";
    }

    // ============================================================
    // TÍNH THANH BIỂU ĐỒ
    // ============================================================

    private static void TinhThanhBar(List<BaoCaoKhachHangTop> list, string metricKho)
    {
        string kho = (metricKho ?? KhoaMacDinh).Trim().ToUpperInvariant();
        bool laTien = kho != "SODONHANG";

        foreach (var x in list)
            x.LaTien = laTien;

        if (list.Count == 0)
            return;

        decimal max = list.Max(x => GiaTriCua(x, kho));
        foreach (var x in list)
        {
            x.GiaTri = GiaTriCua(x, kho);
            x.TyLeBar = max <= 0 ? 0 : (double)(x.GiaTri / max * 100m);
        }
    }

    private static decimal GiaTriCua(BaoCaoKhachHangTop x, string kho) => kho switch
    {
        "SODONHANG" => x.SoDonHang,
        "CONGNO" => x.CongNo,
        "TONGTIENHANG" => x.TongTienHang,
        _ => x.GiaTriMuaHang
    };
}
