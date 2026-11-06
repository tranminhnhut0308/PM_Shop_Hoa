using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất báo cáo phân tích nhà cung cấp ra file Excel quản trị chuyên nghiệp:
/// - Sheet 1: Báo cáo phân tích quản trị (Phần I - KPI tổng quan nhập hàng & công nợ, Phần II - Top 10 NCC theo giá trị nhập,
///            Phần III - Phân tích công nợ NCC chi tiết & tổng hợp, Phần IV - Phân tích tỷ trọng và so sánh nhập - thanh toán)
/// - Sheet 2: Danh sách chi tiết toàn bộ nhà cung cấp kèm AutoFilter, Freeze Panes, dòng tổng cộng.
/// </summary>
public static class BaoCaoNhaCungCapExcel
{
    public static XLWorkbook TaoWorkbook(
        BaoCaoNhaCungCapDuLieu duLieu,
        string? tenCuaHang = null,
        string? nguoiXuat = null)
    {
        var wb = new XLWorkbook();

        // Sheet 1: Tổng hợp & Phân tích chuyên sâu
        GhiSheetPhanTichQuanTri(wb, duLieu, tenCuaHang, nguoiXuat);

        // Sheet 2: Danh sách chi tiết toàn bộ nhà cung cấp
        GhiSheetChiTietNhaCungCap(wb, duLieu, tenCuaHang, nguoiXuat);

        return wb;
    }

    private static void GhiSheetPhanTichQuanTri(
        XLWorkbook wb,
        BaoCaoNhaCungCapDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Phân tích nhà cung cấp");
        var t = d.TongHop;

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO PHÂN TÍCH NHÀ CUNG CẤP & NHẬP HÀNG",
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
        oNhom1.Value = "I. CHỈ SỐ TỔNG QUAN NHẬP HÀNG & CÔNG NỢ NHÀ CUNG CẤP";
        oNhom1.Style.Font.Bold = true;
        oNhom1.Style.Font.FontSize = 12;
        oNhom1.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeKpi = ["STT", "Chỉ số quản trị", "Giá trị", "Đơn vị tính", "Đánh giá / Ý nghĩa"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeKpi);
        dong++;

        var kpiList = new (string Ten, object GiaTri, string Kieu, string DonVi, string YNghia, string? MauChu)[]
        {
            ("Tổng tiền nhập hàng", t.TongTienNhap, "tien", "VNĐ", "Tổng giá trị các phiếu nhập hợp lệ", ExcelHelper.MauChuDao),
            ("Số phiếu nhập hàng", t.SoPhieuNhap, "int", "Phiếu nhập", "Không tính phiếu đã hủy", null),
            ("Số nhà cung cấp", t.TongNhaCungCap, "int", "Nhà cung cấp", "Có phát sinh nhập hàng trong kỳ", null),
            ("Số hàng hóa đã nhập", t.TongSoLuongNhap, "decimal", "Sản phẩm", "Tổng số lượng hàng trên phiếu nhập", null),
            ("Chi nhà cung cấp theo sổ quỹ", t.TienChiSoQuy, "tien", "VNĐ", "Tổng phiếu chi nhà cung cấp đã hoàn thành", ExcelHelper.MauTichCuc),
            ("Đã thanh toán cho nhà cung cấp", t.DaThanhToan, "tien", "VNĐ", "Tổng thanh toán ghi nhận trong sổ công nợ", ExcelHelper.MauTichCuc),
            ("Tổng công nợ nhà cung cấp", t.CongNo, "tien", "VNĐ", "Số dư công nợ hiện tại của nhà cung cấp có phát sinh", ExcelHelper.MauCanhBao)
        };

        for (int i = 0; i < kpiList.Length; i++)
        {
            var item = kpiList[i];
            ws.Row(dong).Height = 21;
            if (i % 2 == 1)
                ws.Range(dong, 1, dong, tieuDeKpi.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ExcelHelper.MauNenThe);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), item.Ten);
            ws.Cell(dong, 2).Style.Font.Bold = item.Ten.Contains("Tổng giá trị") || item.Ten.Contains("công nợ");

            var cellVal = ws.Cell(dong, 3);
            if (item.Kieu == "tien")
                ExcelHelper.GhiO_Tien(cellVal, Convert.ToDecimal(item.GiaTri), hienThiVnd: true);
            else if (item.Kieu == "decimal")
                ExcelHelper.GhiO_SoLuong(cellVal, Convert.ToDecimal(item.GiaTri));
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
        // PHẦN II: TOP 10 NHÀ CUNG CẤP THEO GIÁ TRỊ NHẬP
        // ============================================================
        var oNhom2 = ws.Cell(dong, 1);
        oNhom2.Value = "II. TOP 10 NHÀ CUNG CẤP THEO GIÁ TRỊ NHẬP HÀNG";
        oNhom2.Style.Font.Bold = true;
        oNhom2.Style.Font.FontSize = 12;
        oNhom2.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeTop = ["STT", "Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Số phiếu nhập", "Tổng SL hàng nhập", "Tổng tiền nhập (VNĐ)", "Đã thanh toán (VNĐ)", "Còn nợ (VNĐ)", "Trực quan"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeTop);
        dong++;

        decimal maxNhapTop = d.Top.Count > 0 ? d.Top.Max(x => x.TongTienNhap) : 0m;
        decimal topTongTienNhap = 0, topTongDaTra = 0, topTongNo = 0, topTongSl = 0;
        int topTongPhieu = 0;

        for (int r = 0; r < d.Top.Count; r++)
        {
            var ncc = d.Top[r];
            ws.Row(dong).Height = 20;

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), ncc.MaNhaCungCap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), ncc.TenNhaCungCap);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), ncc.SoDienThoai, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), ncc.SoPhieuNhap.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 6), ncc.TongSoLuongNhap);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), ncc.TongTienNhap, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), ncc.DaThanhToan, hienThiVnd: true);

            var cellNo = ws.Cell(dong, 9);
            ExcelHelper.GhiO_Tien(cellNo, ncc.CongNo, hienThiVnd: true);
            if (ncc.CongNo > 0)
            {
                cellNo.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauNguyHiem);
                cellNo.Style.Font.Bold = true;
            }

            var cellBar = ws.Cell(dong, 10);
            double tyLe = maxNhapTop > 0 ? (double)(ncc.TongTienNhap / maxNhapTop * 100m) : 0;
            cellBar.Value = ExcelHelper.TaoThanhTrucQuan(tyLe, 10);
            cellBar.Style.Font.FontName = "Consolas";
            cellBar.Style.Font.FontSize = 9.5;
            cellBar.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);

            if (r < 3)
            {
                ExcelHelper.DinhDangDongTop(ws, dong, 1, tieuDeTop.Length, r + 1);
            }

            topTongPhieu += ncc.SoPhieuNhap;
            topTongSl += ncc.TongSoLuongNhap;
            topTongTienNhap += ncc.TongTienNhap;
            topTongDaTra += ncc.DaThanhToan;
            topTongNo += ncc.CongNo;
            dong++;
        }

        if (d.Top.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 3).Value = "TỔNG TOP 10";
            ws.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), topTongPhieu.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 6), topTongSl);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), topTongTienNhap, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), topTongDaTra, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), topTongNo, hienThiVnd: true);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDeTop.Length);
            dong++;
        }

        dong += 2;

        // ============================================================
        // PHẦN III: PHÂN TÍCH CÔNG NỢ NHÀ CUNG CẤP
        // ============================================================
        var oNhom3 = ws.Cell(dong, 1);
        oNhom3.Value = "III. PHÂN TÍCH CÔNG NỢ NHÀ CUNG CẤP";
        oNhom3.Style.Font.Bold = true;
        oNhom3.Style.Font.FontSize = 12;
        oNhom3.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        // 3.1 Bảng tổng hợp công nợ
        string[] tieuDeCnTongHop = ["Chỉ số công nợ", "Số lượng / Giá trị", "Đơn vị tính", "Tỷ trọng / Đánh giá"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeCnTongHop);
        dong++;

        var chiSoCongNo = new (string Ten, object GiaTri, string Kieu, string DonVi, string DanhGia, string? Mau)[]
        {
            ("Tổng tiền nhập hàng", t.TongTienNhap, "tien", "VNĐ", "Giá trị các phiếu nhập hợp lệ trong kỳ", ExcelHelper.MauChuDao),
            ("Đã thanh toán theo sổ công nợ", t.DaThanhToan, "tien", "VNĐ", t.TongTienNhap > 0 ? $"Thanh toán / nhập hàng: {(double)(t.DaThanhToan / t.TongTienNhap * 100m):0.0}%" : "0%", ExcelHelper.MauTichCuc),
            ("Chi nhà cung cấp theo sổ quỹ", t.TienChiSoQuy, "tien", "VNĐ", "Phiếu chi đã hoàn thành trong kỳ", ExcelHelper.MauTichCuc),
            ("Tổng công nợ hiện tại", t.CongNo, "tien", "VNĐ", "Số dư sổ công nợ nhà cung cấp", t.CongNo > 0 ? ExcelHelper.MauCanhBao : ExcelHelper.MauTichCuc)
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

        // 3.2 Bảng chi tiết công nợ nhà cung cấp
        var oNhom3b = ws.Cell(dong, 1);
        oNhom3b.Value = "BẢNG CHI TIẾT CÔNG NỢ NHÀ CUNG CẤP";
        oNhom3b.Style.Font.Bold = true;
        oNhom3b.Style.Font.FontSize = 10.5;
        oNhom3b.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeCnChiTiet = ["STT", "Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Tổng tiền nhập hàng (VNĐ)", "Đã thanh toán (VNĐ)", "Còn nợ (VNĐ)", "So sánh Thanh toán / Nợ"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeCnChiTiet);
        dong++;

        var danhSachCnNcc = d.DongTrang.ToList();
        decimal cnTongNhap = 0, cnTongDaChi = 0, cnTongNo = 0;

        for (int i = 0; i < danhSachCnNcc.Count; i++)
        {
            var item = danhSachCnNcc[i];
            ws.Row(dong).Height = 20;

            if (i % 2 == 1)
                ws.Range(dong, 1, dong, tieuDeCnChiTiet.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ExcelHelper.MauNenThe);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), item.MaNhaCungCap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), item.TenNhaCungCap);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), item.SoDienThoai, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), item.TongTienNhap, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), item.DaThanhToan, hienThiVnd: true);

            var cellNo = ws.Cell(dong, 7);
            ExcelHelper.GhiO_Tien(cellNo, item.CongNo, hienThiVnd: true);
            if (item.CongNo > 0)
            {
                cellNo.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauNguyHiem);
                cellNo.Style.Font.Bold = true;
            }

            var cellBar = ws.Cell(dong, 8);
            double tyLeThanhToan = item.TongTienNhap > 0 ? (double)(item.DaThanhToan / item.TongTienNhap * 100m) : 0.0;
            cellBar.Value = $"Đã thanh toán: {tyLeThanhToan:0}% {ExcelHelper.TaoThanhTrucQuan(tyLeThanhToan, 8)}";
            cellBar.Style.Font.FontName = "Consolas";
            cellBar.Style.Font.FontSize = 9.5;
            cellBar.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);

            cnTongNhap += item.TongTienNhap;
            cnTongDaChi += item.DaThanhToan;
            cnTongNo += item.CongNo;
            dong++;
        }

        if (danhSachCnNcc.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 3).Value = "TỔNG CỘNG";
            ws.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), cnTongNhap, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), cnTongDaChi, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), cnTongNo, hienThiVnd: true);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDeCnChiTiet.Length);
            dong++;
        }

        dong += 2;

        // ============================================================
        // PHẦN IV: PHÂN TÍCH TỶ TRỌNG NHẬP HÀNG THEO NHÀ CUNG CẤP
        // ============================================================
        var oNhom4 = ws.Cell(dong, 1);
        oNhom4.Value = "IV. PHÂN TÍCH TỶ TRỌNG NHẬP HÀNG THEO NHÀ CUNG CẤP & SO SÁNH THANH TOÁN";
        oNhom4.Style.Font.Bold = true;
        oNhom4.Style.Font.FontSize = 12;
        oNhom4.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeTyTrong = ["STT", "Mã NCC", "Tên nhà cung cấp", "Số phiếu nhập", "Tổng tiền nhập (VNĐ)", "Tỷ trọng nhập (%)", "Biểu đồ tỷ trọng", "Tỷ lệ thanh toán"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeTyTrong);
        dong++;

        decimal tongGiaTriKy = t.TongTienNhap;
        for (int i = 0; i < d.DongTrang.Count; i++)
        {
            var item = d.DongTrang[i];
            ws.Row(dong).Height = 20;

            double tyLePhanTram = tongGiaTriKy > 0 ? (double)(item.TongTienNhap / tongGiaTriKy * 100m) : 0.0;
            double tyLeThanhToan = item.TongTienNhap > 0 ? (double)(item.DaThanhToan / item.TongTienNhap * 100m) : 0.0;

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), item.MaNhaCungCap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), item.TenNhaCungCap);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), item.SoPhieuNhap.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), item.TongTienNhap, hienThiVnd: true);

            var cellPt = ws.Cell(dong, 6);
            cellPt.Value = tyLePhanTram / 100.0;
            cellPt.Style.NumberFormat.Format = ExcelHelper.DinhDangPhanTram;
            cellPt.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            cellPt.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cellPt.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);

            var cellBar = ws.Cell(dong, 7);
            cellBar.Value = ExcelHelper.TaoThanhTrucQuan(tyLePhanTram, 12);
            cellBar.Style.Font.FontName = "Consolas";
            cellBar.Style.Font.FontSize = 9.5;
            cellBar.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
            cellBar.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cellBar.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);

            var cellSoSanh = ws.Cell(dong, 8);
            cellSoSanh.Value = $"Đã thanh toán {tyLeThanhToan:0}%";
            cellSoSanh.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cellSoSanh.Style.Font.Bold = true;
            cellSoSanh.Style.Font.FontColor = tyLeThanhToan >= 99.9 ? XLColor.FromHtml(ExcelHelper.MauTichCuc) : XLColor.FromHtml(ExcelHelper.MauCanhBao);
            cellSoSanh.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cellSoSanh.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);

            dong++;
        }

        if (d.DongTrang.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 3).Value = "TỔNG CỘNG";
            ws.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), d.DongTrang.Sum(x => x.SoPhieuNhap).ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), tongGiaTriKy, hienThiVnd: true);

            var cellPtTong = ws.Cell(dong, 6);
            cellPtTong.Value = 1.0;
            cellPtTong.Style.NumberFormat.Format = ExcelHelper.DinhDangPhanTram;
            cellPtTong.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var cellBarTong = ws.Cell(dong, 7);
            cellBarTong.Value = ExcelHelper.TaoThanhTrucQuan(100.0, 12);
            cellBarTong.Style.Font.FontName = "Consolas";
            cellBarTong.Style.Font.FontSize = 9.5;
            cellBarTong.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);

            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDeTyTrong.Length);
        }

        // Tự động điều chỉnh độ rộng cột
        for (int c = 1; c <= 10; c++)
        {
            ws.Column(c).AdjustToContents();
            if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
            if (ws.Column(c).Width > 45) ws.Column(c).Width = 45;
        }

        ws.ShowGridLines = true;
        ExcelHelper.ThietLapTrangIn(ws, XLPageOrientation.Landscape);
    }

    private static void GhiSheetChiTietNhaCungCap(
        XLWorkbook wb,
        BaoCaoNhaCungCapDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Chi tiết toàn bộ nhà cung cấp");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CHI TIẾT NHẬP HÀNG & CÔNG NỢ NHÀ CUNG CẤP",
            $"Khoảng thời gian: {d.TongHop.TuNgay:dd/MM/yyyy} - {d.TongHop.DenNgay:dd/MM/yyyy}  (Tổng số: {d.DongTrang.Count:N0} nhà cung cấp)",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 9);

        string[] tieuDe =
        [
            "STT", "Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Số phiếu nhập", "Tổng SL nhập",
            "Tổng tiền nhập (VNĐ)", "Đã thanh toán (VNĐ)", "Còn nợ (VNĐ)"
        ];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongTien = 0, tongDaThanhToan = 0, tongCongNo = 0, tongSl = 0;
        int tongPhieu = 0;

        var dong = dongHeader + 1;
        for (int r = 0; r < d.DongTrang.Count; r++)
        {
            var row = d.DongTrang[r];
            ws.Row(dong).Height = 20;

            if (r % 2 == 1)
                ws.Range(dong, 1, dong, tieuDe.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ExcelHelper.MauNenThe);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), row.MaNhaCungCap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), row.TenNhaCungCap);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), row.SoDienThoai, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), row.SoPhieuNhap.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 6), row.TongSoLuongNhap);

            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), row.TongTienNhap, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), row.DaThanhToan, hienThiVnd: true);

            var cellNo = ws.Cell(dong, 9);
            ExcelHelper.GhiO_Tien(cellNo, row.CongNo, hienThiVnd: true);
            if (row.CongNo > 0)
            {
                cellNo.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauNguyHiem);
                cellNo.Style.Font.Bold = true;
            }

            tongPhieu += row.SoPhieuNhap;
            tongSl += row.TongSoLuongNhap;
            tongTien += row.TongTienNhap;
            tongDaThanhToan += row.DaThanhToan;
            tongCongNo += row.CongNo;

            dong++;
        }

        // Dòng tổng cộng khớp KPI
        if (d.DongTrang.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 3).Value = "TỔNG CỘNG";
            ws.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), tongPhieu.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 6), tongSl);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), tongTien, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), tongDaThanhToan, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), tongCongNo, hienThiVnd: true);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.DongTrang.Count > 0, minWidth: 11, maxWidth: 45);
        ExcelHelper.ThietLapTrangIn(ws, XLPageOrientation.Landscape);
    }
}
