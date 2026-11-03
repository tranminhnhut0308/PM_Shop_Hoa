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
/// Dịch vụ báo cáo hàng hóa nâng cấp (Issue #48 - Phần II):
/// Tổng hợp chỉ số kinh doanh & tồn kho, Top 10 sản phẩm theo doanh thu/số lượng/bán chậm,
/// Doanh thu theo nhóm hàng, Thống kê tồn kho chi tiết và danh sách sản phẩm không bán.
/// </summary>
public sealed class BaoCaoHangHoaService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public BaoCaoHangHoaService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    public static (DateTime TuNgay, DateTime DenNgay) TinhKhoang(DateTime tuNgay, DateTime denNgay)
    {
        var tu = tuNgay.Date;
        var den = denNgay.Date;
        return (tu, den.AddDays(1));
    }

    public async Task<BaoCaoHangHoaDuLieu> TaiDuLieuAsync(
        DateTime tuNgay,
        DateTime denNgay,
        bool gopHangHoa,
        string? loaiHang,
        int nhomId,
        string? timKiem,
        CancellationToken token = default)
    {
        var (tu, denKeTiep) = TinhKhoang(tuNgay, denNgay);
        await using var conn = await _taoKetNoi();

        var dieuKien = new DieuKien
        {
            TuNgay = tu,
            DenKeTiep = denKeTiep,
            LoaiHang = string.IsNullOrWhiteSpace(loaiHang) ? null : loaiHang.Trim(),
            NhomId = nhomId,
            TimKiem = string.IsNullOrWhiteSpace(timKiem) ? null : "%" + timKiem.Trim() + "%"
        };

        string nhomGroup = gopHangHoa
            ? "TRIM(ct.ten_san_pham)"
            : "ct.san_pham_id, ct.ma_san_pham, ct.ten_san_pham";
        string chonTen = gopHangHoa ? "TRIM(ct.ten_san_pham) AS TenHang" : "ct.ten_san_pham AS TenHang";
        string chonMa = gopHangHoa ? "COALESCE(MIN(ct.ma_san_pham), '') AS MaHang" : "ct.ma_san_pham AS MaHang";
        string demMa = gopHangHoa
            ? "COUNT(DISTINCT TRIM(ct.ten_san_pham))"
            : "COUNT(DISTINCT ct.san_pham_id, ct.ma_san_pham)";

        // 1. Chỉ số tổng quan & Tồn kho
        var tongHop = await TaiTongHopAsync(conn, dieuKien, nhomGroup, token);
        tongHop.TuNgay = tu;
        tongHop.DenNgay = denKeTiep.AddDays(-1);

        // 2. Top 10 sản phẩm theo doanh thu & số lượng
        var topDoanhThu = await TaiTopAsync(conn, dieuKien, nhomGroup, chonTen, chonMa,
            "DoanhThu DESC, SoLuongBan DESC", token);
        var topSoLuong = await TaiTopAsync(conn, dieuKien, nhomGroup, chonTen, chonMa,
            "SoLuongBan DESC, DoanhThu DESC", token);

        // 3. Top 10 bán chậm (có bán ít nhất trong kỳ)
        var topBanCham = await TaiTopAsync(conn, dieuKien, nhomGroup, chonTen, chonMa,
            "SoLuongBan ASC, DoanhThu ASC", token);

        // 4. Doanh thu theo nhóm hàng
        var nhomHang = await TaiNhomHangAsync(conn, dieuKien, demMa, token);

        // 5. Bảng chi tiết bán hàng
        var chiTiet = await TaiChiTietAsync(conn, dieuKien, nhomGroup, chonTen, chonMa, token);

        // 6. Thống kê tồn kho chi tiết (Nhóm IV)
        var tonKhoList = await TaiTonKhoListAsync(conn, dieuKien, token);

        // 7. Sản phẩm không phát sinh bán trong kỳ (Nhóm V)
        var hangKhongBan = await TaiHangKhongBanAsync(conn, dieuKien, token);

        TinhThanhBar(topDoanhThu, x => x.DoanhThu);
        TinhThanhBar(topSoLuong, x => x.SoLuongBan);
        TinhThanhBar(topBanCham, x => x.SoLuongBan);
        TinhThanhNhom(nhomHang);

        for (int i = 0; i < topDoanhThu.Count; i++) topDoanhThu[i].Stt = i + 1;
        for (int i = 0; i < topSoLuong.Count; i++) topSoLuong[i].Stt = i + 1;
        for (int i = 0; i < topBanCham.Count; i++) topBanCham[i].Stt = i + 1;
        for (int i = 0; i < nhomHang.Count; i++) nhomHang[i].Stt = i + 1;
        for (int i = 0; i < tonKhoList.Count; i++) tonKhoList[i].Stt = i + 1;
        for (int i = 0; i < hangKhongBan.Count; i++) hangKhongBan[i].Stt = i + 1;
        for (int i = 0; i < chiTiet.Count; i++) chiTiet[i].Stt = i + 1;

        return new BaoCaoHangHoaDuLieu
        {
            TuNgay = tu,
            DenNgay = denKeTiep.AddDays(-1),
            TongHop = tongHop,
            TopDoanhThu = topDoanhThu,
            TopSoLuong = topSoLuong,
            TopBanCham = topBanCham,
            NhomHang = nhomHang,
            TonKhoList = tonKhoList,
            HangKhongBan = hangKhongBan,
            ChiTiet = chiTiet
        };
    }

    public async Task<List<string>> TaiLoaiHangAsync(CancellationToken token = default)
    {
        await using var conn = await _taoKetNoi();
        var rs = await conn.QueryAsync<string>(new CommandDefinition(
            @"SELECT DISTINCT TRIM(loai_hang_excel) FROM san_pham
WHERE loai_hang_excel IS NOT NULL AND TRIM(loai_hang_excel) <> ''
ORDER BY 1;",
            cancellationToken: token));
        return rs.AsList();
    }

    public async Task<List<BaoCaoHangHoaNhomOption>> TaiNhomHangAsync(CancellationToken token = default)
    {
        await using var conn = await _taoKetNoi();
        var rs = await conn.QueryAsync<(uint Id, string TenNhom)>(new CommandDefinition(
            @"SELECT id, ten_nhom FROM nhom_san_pham WHERE trang_thai = 1 ORDER BY id;",
            cancellationToken: token));

        var list = new List<BaoCaoHangHoaNhomOption>
        {
            new() { Id = -1, TenNhom = "", TenHienThi = "Tất cả nhóm hàng" }
        };
        foreach (var (id, ten) in rs)
            list.Add(new BaoCaoHangHoaNhomOption { Id = (int)id, TenNhom = ten, TenHienThi = ten });
        list.Add(new BaoCaoHangHoaNhomOption { Id = 0, TenNhom = "", TenHienThi = "Chưa phân nhóm" });
        return list;
    }

    private sealed class DieuKien
    {
        public DateTime TuNgay { get; init; }
        public DateTime DenKeTiep { get; init; }
        public string? LoaiHang { get; init; }
        public int NhomId { get; init; }
        public string? TimKiem { get; init; }

        public string Where =>
            @"hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep"
            + (LoaiHang is null ? "" : "\n  AND TRIM(sp.loai_hang_excel) = @LoaiHang")
            + (NhomId > 0 ? "\n  AND sp.nhom_san_pham_id = @NhomId"
               : NhomId == 0 ? "\n  AND sp.nhom_san_pham_id IS NULL" : "")
            + (TimKiem is null ? "" : "\n  AND (ct.ten_san_pham LIKE @TimKiem OR ct.ma_san_pham LIKE @TimKiem)");

        public object Params => new
        {
            TuNgay,
            DenKeTiep,
            LoaiHang,
            NhomId,
            TimKiem
        };

        public const string From = @"
FROM chi_tiet_hoa_don_ban_hang ct
INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id";
    }

    private async Task<BaoCaoHangHoaTongHop> TaiTongHopAsync(
        MySqlConnection conn, DieuKien dk, string nhomGroup, CancellationToken token)
    {
        // 1. Thống kê bán hàng trong kỳ
        string sqlBanHang = @"
SELECT
  COUNT(*) AS SoMaHang,
  COALESCE(SUM(q.SoLuongBan), 0) AS SoLuongBan,
  COALESCE(SUM(q.DoanhThu), 0) AS DoanhThu,
  COALESCE(SUM(q.GiaVon), 0) AS GiaVon
FROM (
  SELECT
    COALESCE(SUM(ct.so_luong), 0) AS SoLuongBan,
    COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN ct.thanh_tien ELSE 0 END), 0) AS DoanhThu,
    COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN ct.gia_von * ct.so_luong ELSE 0 END), 0) AS GiaVon
  " + DieuKien.From + @"
  WHERE " + dk.Where + @"
  GROUP BY " + nhomGroup + @"
) q;
";
        var r = await conn.QueryFirstOrDefaultAsync<BaoCaoHangHoaTongHop>(
            new CommandDefinition(sqlBanHang, dk.Params, cancellationToken: token)) ?? new BaoCaoHangHoaTongHop();

        // 2. Thống kê tổng giảm giá trong kỳ
        string sqlGiamGia = @"
SELECT COALESCE(SUM(giam_gia), 0)
FROM hoa_don_ban_hang hd
WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep;
";
        r.TongGiamGia = await conn.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sqlGiamGia, dk.Params, cancellationToken: token));

        // 3. Thống kê mặt hàng & Tồn kho hiện tại trong danh mục san_pham
        string sqlDanhMuc = @"
SELECT
  COUNT(sp.id) AS TongSoMatHang,
  COALESCE(SUM(CASE WHEN sp.trang_thai = 1 THEN 1 ELSE 0 END), 0) AS SoMatHangDangKinhDoanh,
  COALESCE(SUM(CASE WHEN sp.trang_thai = 0 THEN 1 ELSE 0 END), 0) AS SoMatHangNgungKinhDoanh,
  COALESCE(SUM(COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0)), 0) AS TongSoLuongTon,
  COALESCE(SUM(COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) * sp.gia_von), 0) AS GiaTriTonKhoHienTai,
  COALESCE(SUM(CASE WHEN COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) <= 0 THEN 1 ELSE 0 END), 0) AS SoMatHangHetHang,
  COALESCE(SUM(CASE WHEN COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) > 0 AND COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) <= sp.ton_toi_thieu AND sp.ton_toi_thieu > 0 THEN 1 ELSE 0 END), 0) AS SoMatHangSapHetHang,
  COALESCE(SUM(CASE WHEN COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) >= sp.ton_toi_da AND sp.ton_toi_da > 0 THEN 1 ELSE 0 END), 0) AS SoMatHangTonKhoNhieu
FROM san_pham sp
LEFT JOIN (
  SELECT san_pham_id, SUM(so_luong_ton) AS so_luong_ton
  FROM ton_kho
  GROUP BY san_pham_id
) tk ON tk.san_pham_id = sp.id
WHERE 1 = 1"
+ (dk.LoaiHang is null ? "" : "\n  AND TRIM(sp.loai_hang_excel) = @LoaiHang")
+ (dk.NhomId > 0 ? "\n  AND sp.nhom_san_pham_id = @NhomId"
   : dk.NhomId == 0 ? "\n  AND sp.nhom_san_pham_id IS NULL" : "")
+ (dk.TimKiem is null ? "" : "\n  AND (sp.ten_san_pham LIKE @TimKiem OR sp.ma_san_pham LIKE @TimKiem);");

        var dm = await conn.QueryFirstOrDefaultAsync(new CommandDefinition(sqlDanhMuc, dk.Params, cancellationToken: token));
        if (dm is not null)
        {
            r.TongSoMatHang = (int)(dm.TongSoMatHang ?? 0);
            r.SoMatHangDangKinhDoanh = (int)(dm.SoMatHangDangKinhDoanh ?? 0);
            r.SoMatHangNgungKinhDoanh = (int)(dm.SoMatHangNgungKinhDoanh ?? 0);
            r.TongSoLuongTon = (decimal)(dm.TongSoLuongTon ?? 0m);
            r.GiaTriTonKhoHienTai = (decimal)(dm.GiaTriTonKhoHienTai ?? 0m);
            r.SoMatHangHetHang = (int)(dm.SoMatHangHetHang ?? 0);
            r.SoMatHangSapHetHang = (int)(dm.SoMatHangSapHetHang ?? 0);
            r.SoMatHangTonKhoNhieu = (int)(dm.SoMatHangTonKhoNhieu ?? 0);
        }

        return r;
    }

    private async Task<List<BaoCaoHangHoaTop>> TaiTopAsync(
        MySqlConnection conn, DieuKien dk, string nhomGroup, string chonTen, string chonMa,
        string orderBy, CancellationToken token)
    {
        string sql = @"
SELECT
  " + chonMa + @",
  " + chonTen + @",
  COALESCE(MIN(dvt.ten_don_vi), '') AS DonViTinh,
  COALESCE(SUM(ct.so_luong), 0) AS SoLuongBan,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN ct.thanh_tien ELSE 0 END), 0) AS DoanhThu,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN ct.gia_von * ct.so_luong ELSE 0 END), 0) AS GiaVon
" + DieuKien.From + @"
LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
WHERE " + dk.Where + @"
GROUP BY " + nhomGroup + @"
ORDER BY " + orderBy + @"
LIMIT 10;
";
        var rs = await conn.QueryAsync<BaoCaoHangHoaTop>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    private async Task<List<BaoCaoHangHoaNhom>> TaiNhomHangAsync(
        MySqlConnection conn, DieuKien dk, string demMa, CancellationToken token)
    {
        string sql = @"
SELECT
  COALESCE(NULLIF(TRIM(n.ten_nhom), ''), 'Chưa phân nhóm') AS TenNhom,
  " + demMa + @" AS SoMaHang,
  COALESCE(SUM(ct.so_luong), 0) AS SoLuongBan,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN ct.thanh_tien ELSE 0 END), 0) AS DoanhThu
" + DieuKien.From + @"
LEFT JOIN nhom_san_pham n ON n.id = sp.nhom_san_pham_id
WHERE " + dk.Where + @"
GROUP BY n.id, COALESCE(NULLIF(TRIM(n.ten_nhom), ''), 'Chưa phân nhóm')
ORDER BY DoanhThu DESC, SoLuongBan DESC;
";
        var rs = await conn.QueryAsync<BaoCaoHangHoaNhom>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    private async Task<List<BaoCaoHangHoaChiTiet>> TaiChiTietAsync(
        MySqlConnection conn, DieuKien dk, string nhomGroup, string chonTen, string chonMa,
        CancellationToken token)
    {
        string sql = @"
SELECT
  " + chonMa + @",
  " + chonTen + @",
  COALESCE(NULLIF(TRIM(n.ten_nhom), ''), 'Chưa phân nhóm') AS NhomHang,
  COALESCE(SUM(ct.so_luong), 0) AS SoLuongBan,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN ct.thanh_tien ELSE 0 END), 0) AS DoanhThu,
  COALESCE(SUM(CASE WHEN hd.doanh_so_da_ghi_nhan = 1 THEN ct.gia_von * ct.so_luong ELSE 0 END), 0) AS GiaVon
" + DieuKien.From + @"
LEFT JOIN nhom_san_pham n ON n.id = sp.nhom_san_pham_id
WHERE " + dk.Where + @"
GROUP BY " + nhomGroup + @"
ORDER BY DoanhThu DESC, SoLuongBan DESC;
";
        var rs = await conn.QueryAsync<BaoCaoHangHoaChiTiet>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    private async Task<List<BaoCaoHangHoaTonKho>> TaiTonKhoListAsync(
        MySqlConnection conn, DieuKien dk, CancellationToken token)
    {
        string sql = @"
SELECT
  sp.ma_san_pham AS MaHang,
  sp.ten_san_pham AS TenHang,
  COALESCE(dvt.ten_don_vi, '') AS DonViTinh,
  COALESCE(nsp.ten_nhom, 'Chưa phân nhóm') AS NhomHang,
  COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) AS TonKho,
  sp.ton_toi_thieu AS TonToiThieu,
  sp.ton_toi_da AS TonToiDa,
  sp.gia_von AS GiaVon,
  (COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) * sp.gia_von) AS GiaTriTon
FROM san_pham sp
LEFT JOIN (
  SELECT san_pham_id, SUM(so_luong_ton) AS so_luong_ton
  FROM ton_kho
  GROUP BY san_pham_id
) tk ON tk.san_pham_id = sp.id
LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
WHERE 1 = 1"
+ (dk.LoaiHang is null ? "" : "\n  AND TRIM(sp.loai_hang_excel) = @LoaiHang")
+ (dk.NhomId > 0 ? "\n  AND sp.nhom_san_pham_id = @NhomId"
   : dk.NhomId == 0 ? "\n  AND sp.nhom_san_pham_id IS NULL" : "")
+ (dk.TimKiem is null ? "" : "\n  AND (sp.ten_san_pham LIKE @TimKiem OR sp.ma_san_pham LIKE @TimKiem)")
+ @"
ORDER BY TonKho ASC, GiaTriTon DESC;
";
        var rs = await conn.QueryAsync<BaoCaoHangHoaTonKho>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    private async Task<List<BaoCaoHangHoaTonKho>> TaiHangKhongBanAsync(
        MySqlConnection conn, DieuKien dk, CancellationToken token)
    {
        string sql = @"
SELECT
  sp.ma_san_pham AS MaHang,
  sp.ten_san_pham AS TenHang,
  COALESCE(dvt.ten_don_vi, '') AS DonViTinh,
  COALESCE(nsp.ten_nhom, 'Chưa phân nhóm') AS NhomHang,
  COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) AS TonKho,
  sp.ton_toi_thieu AS TonToiThieu,
  sp.ton_toi_da AS TonToiDa,
  sp.gia_von AS GiaVon,
  (COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) * sp.gia_von) AS GiaTriTon
FROM san_pham sp
LEFT JOIN (
  SELECT san_pham_id, SUM(so_luong_ton) AS so_luong_ton
  FROM ton_kho
  GROUP BY san_pham_id
) tk ON tk.san_pham_id = sp.id
LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
WHERE sp.trang_thai = 1
  AND sp.id NOT IN (
    SELECT DISTINCT ct.san_pham_id
    FROM chi_tiet_hoa_don_ban_hang ct
    INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
    WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
      AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep
      AND ct.san_pham_id IS NOT NULL
  )"
+ (dk.LoaiHang is null ? "" : "\n  AND TRIM(sp.loai_hang_excel) = @LoaiHang")
+ (dk.NhomId > 0 ? "\n  AND sp.nhom_san_pham_id = @NhomId"
   : dk.NhomId == 0 ? "\n  AND sp.nhom_san_pham_id IS NULL" : "")
+ (dk.TimKiem is null ? "" : "\n  AND (sp.ten_san_pham LIKE @TimKiem OR sp.ma_san_pham LIKE @TimKiem)")
+ @"
ORDER BY TonKho DESC, GiaTriTon DESC
LIMIT 50;
";
        var rs = await conn.QueryAsync<BaoCaoHangHoaTonKho>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    private static void TinhThanhBar(List<BaoCaoHangHoaTop> list, Func<BaoCaoHangHoaTop, decimal> giaTri)
    {
        if (list.Count == 0)
            return;

        var max = list.Max(giaTri);
        foreach (var x in list)
            x.TyLeBar = max <= 0 ? 0 : (double)(giaTri(x) / max * 100m);
    }

    private static void TinhThanhNhom(List<BaoCaoHangHoaNhom> list)
    {
        if (list.Count == 0)
            return;

        var max = list.Max(x => x.DoanhThu);
        var tong = list.Sum(x => x.DoanhThu);
        foreach (var x in list)
        {
            x.TyLeBar = max <= 0 ? 0 : (double)(x.DoanhThu / max * 100m);
            x.TyLePhanTram = tong <= 0 ? 0 : (double)(x.DoanhThu / tong * 100m);
        }
    }
}
