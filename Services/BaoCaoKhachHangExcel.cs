using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất báo cáo phân tích khách hàng ra file Excel quản trị chuyên nghiệp:
/// - Sheet 1: Báo cáo phân tích quản trị (Phần I - KPI tổng quan, Phần II - Top 10 khách hàng theo doanh thu,
///            Phần III - Phân tích công nợ khách hàng, Phần IV - Phân tích khách hàng theo nhóm)
/// - Sheet 2: Danh sách chi tiết toàn bộ khách hàng kèm AutoFilter, Freeze Panes, dòng tổng cộng.
/// </summary>
public static class BaoCaoKhachHangExcel
{
    public static XLWorkbook TaoWorkbook(
        BaoCaoKhachHangDuLieu duLieu,
        string? tenCuaHang = null,
        string? nguoiXuat = null)
    {
        var wb = new XLWorkbook();

        // Sheet 1: Tổng hợp & Phân tích chuyên sâu
        GhiSheetPhanTichQuanTri(wb, duLieu, tenCuaHang, nguoiXuat);

        // Sheet 2: Danh sách chi tiết khách hàng
        GhiSheetChiTietKhachHang(wb, duLieu, tenCuaHang, nguoiXuat);

        return wb;
    }

    private static void GhiSheetPhanTichQuanTri(
        XLWorkbook wb,
        BaoCaoKhachHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Phân tích khách hàng");
        var t = d.TongHop;

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO PHÂN TÍCH KHÁCH HÀNG & CÔNG NỢ",
            $"Khoảng thời gian: {t.TuNgay:dd/MM/yyyy} - {t.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 10);

        var dong = dongHeader;

        // ============================================================
        // PHẦN I: CHỈ SỐ TỔNG QUAN
        // ============================================================
        var oNhom1 = ws.Cell(dong, 1);
        oNhom1.Value = "I. CHỈ SỐ TỔNG QUAN KHÁCH HÀNG & BÁN HÀNG";
        oNhom1.Style.Font.Bold = true;
        oNhom1.Style.Font.FontSize = 12;
        oNhom1.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeKpi = ["STT", "Chỉ số quản trị", "Giá trị", "Đơn vị tính", "Đánh giá / Ý nghĩa"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeKpi);
        dong++;

        var kpiList = new (string Ten, object GiaTri, string Kieu, string DonVi, string YNghia, string? MauChu)[]
        {
            ("Tổng số khách hàng phát sinh giao dịch", t.TongKhachHang, "int", "Khách hàng", "Số khách hàng có hóa đơn trong kỳ", null),
            ("Số hóa đơn bán hàng", t.SoDonHang, "int", "Hóa đơn", "Tổng số đơn hàng hoàn thành", null),
            ("Tổng tiền hàng", t.TongTienHang, "tien", "VNĐ", "Tổng giá trị hàng trước chiết khấu", null),
            ("Tổng tiền giảm giá", t.GiamGia, "tien", "VNĐ", "Chiết khấu/khuyến mãi áp dụng cho khách", null),
            ("Doanh số mua hàng sau giảm giá", t.GiaTriMuaHang, "tien", "VNĐ", "Doanh thu thực tế mang lại từ khách hàng", ExcelHelper.MauTichCuc),
            ("Tổng tiền khách đã thanh toán", t.DaThanhToan, "tien", "VNĐ", "Số tiền thực thu bằng tiền mặt/chuyển khoản", ExcelHelper.MauTichCuc),
            ("Tổng công nợ phát sinh", t.CongNo, "tien", "VNĐ", t.CongNo > 0 ? "Công nợ mới ghi nhận phát sinh trong kỳ" : "Không phát sinh nợ mới", t.CongNo > 0 ? ExcelHelper.MauNguyHiem : ExcelHelper.MauTichCuc),
            ("Số khách hàng còn nợ", t.SoKhachConNo, "int", "Khách hàng", t.SoKhachConNo > 0 ? "Khách hàng có số nợ chưa thanh toán hết" : "Tất cả khách hàng đã thanh toán", t.SoKhachConNo > 0 ? ExcelHelper.MauCanhBao : ExcelHelper.MauTichCuc),
            ("Số khách hàng đã thanh toán đầy đủ", t.SoKhachDaThanhToanHet, "int", "Khách hàng", "Khách hàng không còn nợ trên các đơn kỳ này", ExcelHelper.MauTichCuc),
            ("Giá trị mua hàng trung bình / khách hàng", t.GiaTriMuaTrungBinh, "tien", "VNĐ/khách", "Doanh số bình quân trên mỗi khách giao dịch", null)
        };

        for (int i = 0; i < kpiList.Length; i++)
        {
            var item = kpiList[i];
            ws.Row(dong).Height = 21;
            if (i % 2 == 1)
                ws.Range(dong, 1, dong, tieuDeKpi.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ExcelHelper.MauNenThe);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), item.Ten);
            ws.Cell(dong, 2).Style.Font.Bold = item.Ten.Contains("Doanh số") || item.Ten.Contains("công nợ");

            var cellVal = ws.Cell(dong, 3);
            if (item.Kieu == "tien")
                ExcelHelper.GhiO_Tien(cellVal, Convert.ToDecimal(item.GiaTri), hienThiVnd: true);
            else
                ExcelHelper.GhiO_Chu(cellVal, Convert.ToInt32(item.GiaTri).ToString("N0"), XLAlignmentHorizontalValues.Right);

            if (item.MauChu is not null)
            {
                cellVal.Style.Font.FontColor = XLColor.FromHtml(item.MauChu);
                cellVal.Style.Font.Bold = true;
            }

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), item.DonVi, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), item.YNghia);
            dong++;
        }

        dong += 2;

        // ============================================================
        // PHẦN II: TOP 10 KHÁCH HÀNG THEO DOANH THU
        // ============================================================
        var oNhom2 = ws.Cell(dong, 1);
        oNhom2.Value = "II. TOP 10 KHÁCH HÀNG THEO DOANH THU";
        oNhom2.Style.Font.Bold = true;
        oNhom2.Style.Font.FontSize = 12;
        oNhom2.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeTop = ["STT", "Mã KH", "Tên khách hàng", "Số điện thoại", "Số hóa đơn", "Tổng SL mua", "Tổng doanh thu (VNĐ)", "Đã thanh toán (VNĐ)", "Còn nợ (VNĐ)", "Trực quan"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeTop);
        dong++;

        var danhSachTop10 = d.Top10DoanhThu.Count > 0 ? d.Top10DoanhThu : d.Top;
        decimal maxDoanhThuTop = danhSachTop10.Count > 0 ? danhSachTop10.Max(x => x.GiaTriMuaHang) : 0m;
        decimal topTongDt = 0, topTongDaTra = 0, topTongNo = 0, topTongSl = 0;
        int topTongHd = 0;

        for (int r = 0; r < danhSachTop10.Count; r++)
        {
            var kh = danhSachTop10[r];
            ws.Row(dong).Height = 20;

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), kh.MaKhachHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), kh.TenKhachHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), kh.SoDienThoai, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), kh.SoDonHang.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 6), kh.TongSoLuongMua);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), kh.GiaTriMuaHang, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), kh.DaThanhToan, hienThiVnd: true);

            var cellNo = ws.Cell(dong, 9);
            ExcelHelper.GhiO_Tien(cellNo, kh.CongNo, hienThiVnd: true);
            if (kh.CongNo > 0)
            {
                cellNo.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauNguyHiem);
                cellNo.Style.Font.Bold = true;
            }

            var cellBar = ws.Cell(dong, 10);
            double tyLe = maxDoanhThuTop > 0 ? (double)(kh.GiaTriMuaHang / maxDoanhThuTop * 100m) : 0;
            cellBar.Value = ExcelHelper.TaoThanhTrucQuan(tyLe, 10);
            cellBar.Style.Font.FontName = "Consolas";
            cellBar.Style.Font.FontSize = 9.5;
            cellBar.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);

            if (r < 3)
            {
                ExcelHelper.DinhDangDongTop(ws, dong, 1, tieuDeTop.Length, r + 1);
            }

            topTongHd += kh.SoDonHang;
            topTongSl += kh.TongSoLuongMua;
            topTongDt += kh.GiaTriMuaHang;
            topTongDaTra += kh.DaThanhToan;
            topTongNo += kh.CongNo;
            dong++;
        }

        if (danhSachTop10.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 3).Value = "TỔNG TOP 10";
            ws.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), topTongHd.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 6), topTongSl);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), topTongDt, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), topTongDaTra, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), topTongNo, hienThiVnd: true);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDeTop.Length);
            dong++;
        }

        dong += 2;

        // ============================================================
        // PHẦN III: PHÂN TÍCH CÔNG NỢ KHÁCH HÀNG
        // ============================================================
        var oNhom3 = ws.Cell(dong, 1);
        oNhom3.Value = "III. PHÂN TÍCH CÔNG NỢ KHÁCH HÀNG";
        oNhom3.Style.Font.Bold = true;
        oNhom3.Style.Font.FontSize = 12;
        oNhom3.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        // 3.1 Bảng chỉ số công nợ
        string[] tieuDeCnTongHop = ["Chỉ số công nợ", "Số lượng / Giá trị", "Đơn vị tính", "Tỷ trọng / Đánh giá"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeCnTongHop);
        dong++;

        var chiSoCongNo = new (string Ten, object GiaTri, string Kieu, string DonVi, string DanhGia, string? Mau)[]
        {
            ("Khách hàng còn nợ trong kỳ", t.SoKhachConNo, "int", "Khách hàng", t.TongKhachHang > 0 ? $"{(double)t.SoKhachConNo / t.TongKhachHang * 100:0.0}% tổng khách hàng" : "0%", t.SoKhachConNo > 0 ? ExcelHelper.MauCanhBao : ExcelHelper.MauTichCuc),
            ("Khách hàng đã thanh toán đầy đủ", t.SoKhachDaThanhToanHet, "int", "Khách hàng", t.TongKhachHang > 0 ? $"{(double)t.SoKhachDaThanhToanHet / t.TongKhachHang * 100:0.0}% tổng khách hàng" : "100%", ExcelHelper.MauTichCuc),
            ("Tổng công nợ phát sinh trong kỳ", t.CongNo, "tien", "VNĐ", t.GiaTriMuaHang > 0 ? $"Tỷ lệ nợ/doanh số: {(double)(t.CongNo / t.GiaTriMuaHang * 100m):0.0}%" : "0%", t.CongNo > 0 ? ExcelHelper.MauNguyHiem : ExcelHelper.MauTichCuc),
            ("Tổng tiền đã thu trong kỳ", t.DaThanhToan, "tien", "VNĐ", t.GiaTriMuaHang > 0 ? $"Tỷ lệ thu hồi: {(double)(t.DaThanhToan / t.GiaTriMuaHang * 100m):0.0}%" : "100%", ExcelHelper.MauTichCuc),
            ("Số khách hàng có phát sinh thanh toán", t.SoKhachCoThanhToan, "int", "Khách hàng", "Khách hàng có trả tiền mua hàng trong kỳ", null)
        };

        foreach (var cn in chiSoCongNo)
        {
            ws.Row(dong).Height = 20;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), cn.Ten);
            ws.Cell(dong, 1).Style.Font.Bold = cn.Ten.Contains("Tổng");

            var cellVal = ws.Cell(dong, 2);
            if (cn.Kieu == "tien")
                ExcelHelper.GhiO_Tien(cellVal, Convert.ToDecimal(cn.GiaTri), hienThiVnd: true);
            else
                ExcelHelper.GhiO_Chu(cellVal, Convert.ToInt32(cn.GiaTri).ToString("N0"), XLAlignmentHorizontalValues.Right);

            if (cn.Mau is not null)
            {
                cellVal.Style.Font.FontColor = XLColor.FromHtml(cn.Mau);
                cellVal.Style.Font.Bold = true;
            }

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), cn.DonVi, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), cn.DanhGia);
            dong++;
        }

        dong++;

        // 3.2 Bảng chi tiết công nợ khách hàng
        var oNhom3b = ws.Cell(dong, 1);
        oNhom3b.Value = "BẢNG CHI TIẾT CÔNG NỢ KHÁCH HÀNG (CÁC KHÁCH HÀNG CÒN NỢ HOẶC PHÁT SINH GIAO DỊCH)";
        oNhom3b.Style.Font.Bold = true;
        oNhom3b.Style.Font.FontSize = 10.5;
        oNhom3b.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeCnChiTiet = ["STT", "Mã KH", "Tên khách hàng", "Số điện thoại", "Tổng tiền mua hàng (VNĐ)", "Đã thanh toán (VNĐ)", "Còn nợ (VNĐ)", "So sánh Thu / Nợ"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeCnChiTiet);
        dong++;

        var danhSachCn = d.DanhSachCongNo.Count > 0 ? d.DanhSachCongNo : d.DongTrang.Where(x => x.CongNo > 0).ToList();
        decimal cnTongMua = 0, cnTongDaThu = 0, cnTongNo = 0;

        for (int i = 0; i < danhSachCn.Count; i++)
        {
            var item = danhSachCn[i];
            ws.Row(dong).Height = 20;

            if (i % 2 == 1)
                ws.Range(dong, 1, dong, tieuDeCnChiTiet.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ExcelHelper.MauNenThe);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), item.MaKhachHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), item.TenKhachHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), item.SoDienThoai, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), item.GiaTriMuaHang, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), item.DaThanhToan, hienThiVnd: true);

            var cellNo = ws.Cell(dong, 7);
            ExcelHelper.GhiO_Tien(cellNo, item.CongNo, hienThiVnd: true);
            if (item.CongNo > 0)
            {
                cellNo.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauNguyHiem);
                cellNo.Style.Font.Bold = true;
            }

            var cellBar = ws.Cell(dong, 8);
            double tyLeThu = item.GiaTriMuaHang > 0 ? (double)(item.DaThanhToan / item.GiaTriMuaHang * 100m) : 100.0;
            cellBar.Value = $"Đã thu: {tyLeThu:0}% {ExcelHelper.TaoThanhTrucQuan(tyLeThu, 8)}";
            cellBar.Style.Font.FontName = "Consolas";
            cellBar.Style.Font.FontSize = 9.5;
            cellBar.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);

            cnTongMua += item.GiaTriMuaHang;
            cnTongDaThu += item.DaThanhToan;
            cnTongNo += item.CongNo;
            dong++;
        }

        if (danhSachCn.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 3).Value = "TỔNG CỘNG";
            ws.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), cnTongMua, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), cnTongDaThu, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), cnTongNo, hienThiVnd: true);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDeCnChiTiet.Length);
            dong++;
        }

        dong += 2;

        // ============================================================
        // PHẦN IV: PHÂN TÍCH KHÁCH HÀNG THEO NHÓM
        // ============================================================
        var oNhom4 = ws.Cell(dong, 1);
        oNhom4.Value = "IV. PHÂN TÍCH KHÁCH HÀNG THEO NHÓM";
        oNhom4.Style.Font.Bold = true;
        oNhom4.Style.Font.FontSize = 12;
        oNhom4.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeNhom = ["STT", "Nhóm khách hàng", "Số lượng khách hàng", "Doanh thu (VNĐ)", "Tỷ trọng doanh thu (%)", "Biểu đồ cơ cấu"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeNhom);
        dong++;

        int nhomTongKhach = 0;
        decimal nhomTongDt = 0;
        for (int i = 0; i < d.NhomKhachHang.Count; i++)
        {
            var nhom = d.NhomKhachHang[i];
            ws.Row(dong).Height = 20;

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), nhom.TenNhom);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), nhom.SoKhachHang.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), nhom.DoanhThu, hienThiVnd: true);

            var cellPt = ws.Cell(dong, 5);
            cellPt.Value = nhom.TyLePhanTram / 100.0;
            cellPt.Style.NumberFormat.Format = ExcelHelper.DinhDangPhanTram;
            cellPt.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            cellPt.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cellPt.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);

            var cellBar = ws.Cell(dong, 6);
            cellBar.Value = ExcelHelper.TaoThanhTrucQuan(nhom.TyLePhanTram, 12);
            cellBar.Style.Font.FontName = "Consolas";
            cellBar.Style.Font.FontSize = 9.5;
            cellBar.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
            cellBar.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cellBar.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);

            nhomTongKhach += nhom.SoKhachHang;
            nhomTongDt += nhom.DoanhThu;
            dong++;
        }

        if (d.NhomKhachHang.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 2).Value = "TỔNG CỘNG";
            ws.Cell(dong, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), nhomTongKhach.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), nhomTongDt, hienThiVnd: true);

            var cellPtTong = ws.Cell(dong, 5);
            cellPtTong.Value = 1.0;
            cellPtTong.Style.NumberFormat.Format = ExcelHelper.DinhDangPhanTram;
            cellPtTong.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var cellBarTong = ws.Cell(dong, 6);
            cellBarTong.Value = ExcelHelper.TaoThanhTrucQuan(100.0, 12);
            cellBarTong.Style.Font.FontName = "Consolas";
            cellBarTong.Style.Font.FontSize = 9.5;
            cellBarTong.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);

            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDeNhom.Length);
        }

        // Tự co độ rộng cột
        for (int c = 1; c <= 10; c++)
        {
            ws.Column(c).AdjustToContents();
            if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
            if (ws.Column(c).Width > 45) ws.Column(c).Width = 45;
        }

        ws.ShowGridLines = true;
        ExcelHelper.ThietLapTrangIn(ws, XLPageOrientation.Landscape);
    }

    private static void GhiSheetChiTietKhachHang(
        XLWorkbook wb,
        BaoCaoKhachHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Chi tiết toàn bộ khách hàng");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CHI TIẾT DOANH SỐ & CÔNG NỢ KHÁCH HÀNG",
            $"Khoảng thời gian: {d.TongHop.TuNgay:dd/MM/yyyy} - {d.TongHop.DenNgay:dd/MM/yyyy}  (Tổng số: {d.DongTrang.Count:N0} khách hàng)",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 10);

        string[] tieuDe =
        [
            "STT", "Mã KH", "Tên khách hàng", "Số điện thoại", "Số hóa đơn",
            "Tổng tiền hàng (VNĐ)", "Giảm giá (VNĐ)", "Doanh số mua hàng (VNĐ)", "Đã thanh toán (VNĐ)", "Còn nợ (VNĐ)"
        ];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongTien = 0, tongGiam = 0, tongDoanhSo = 0, tongDaThu = 0, tongCongNo = 0;
        int tongSoDon = 0;

        var dong = dongHeader + 1;
        for (int r = 0; r < d.DongTrang.Count; r++)
        {
            var row = d.DongTrang[r];
            ws.Row(dong).Height = 20;

            if (r % 2 == 1)
                ws.Range(dong, 1, dong, tieuDe.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ExcelHelper.MauNenThe);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), row.MaKhachHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), row.TenKhachHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), row.SoDienThoai, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), row.SoDonHang.ToString("N0"), XLAlignmentHorizontalValues.Right);

            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), row.TongTienHang, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), row.GiamGia, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), row.GiaTriMuaHang, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), row.DaThanhToan, hienThiVnd: true);

            var cellNo = ws.Cell(dong, 10);
            ExcelHelper.GhiO_Tien(cellNo, row.CongNo, hienThiVnd: true);
            if (row.CongNo > 0)
            {
                cellNo.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauNguyHiem);
                cellNo.Style.Font.Bold = true;
            }

            tongSoDon += row.SoDonHang;
            tongTien += row.TongTienHang;
            tongGiam += row.GiamGia;
            tongDoanhSo += row.GiaTriMuaHang;
            tongDaThu += row.DaThanhToan;
            tongCongNo += row.CongNo;

            dong++;
        }

        // Dòng tổng cộng khớp KPI
        if (d.DongTrang.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 3).Value = "TỔNG CỘNG";
            ws.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), tongSoDon.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), tongTien, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), tongGiam, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), tongDoanhSo, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), tongDaThu, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), tongCongNo, hienThiVnd: true);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.DongTrang.Count > 0, minWidth: 11, maxWidth: 45);
        ExcelHelper.ThietLapTrangIn(ws, XLPageOrientation.Landscape);
    }
}
