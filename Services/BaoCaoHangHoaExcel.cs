using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất báo cáo hàng hóa ra file .xlsx chuyên nghiệp:
/// - Sheet 1: Tổng hợp (Chỉ số tổng quan, Top 10 sản phẩm theo doanh thu đầy đủ SL/Doanh thu/Giá vốn/Lợi nhuận, Cơ cấu nhóm hàng)
/// - Sheet 2: Bảng chi tiết toàn bộ mã hàng với AutoFilter, Freeze Panes, định dạng chuẩn.
/// </summary>
public static class BaoCaoHangHoaExcel
{
    public static XLWorkbook TaoWorkbook(
        BaoCaoHangHoaDuLieu duLieu,
        string? tenCuaHang = null,
        string? nguoiXuat = null)
    {
        var wb = new XLWorkbook();

        // ---------- Sheet 1: Tổng hợp ----------
        GhiSheetTongHop(wb, duLieu, tenCuaHang, nguoiXuat);

        // ---------- Sheet 2: Chi tiết ----------
        GhiSheetChiTiet(wb, duLieu, tenCuaHang, nguoiXuat);

        return wb;
    }

    private static void GhiSheetTongHop(
        XLWorkbook wb,
        BaoCaoHangHoaDuLieu duLieu,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng hợp");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO HÀNG HÓA TỔNG HỢP",
            $"Từ ngày {duLieu.TuNgay:dd/MM/yyyy} đến ngày {duLieu.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 7);

        var th = duLieu.TongHop;

        // BẢNG 1: CHỈ SỐ TỔNG HỢP
        var dong = dongHeader;
        var oNhom1 = ws.Cell(dong, 1);
        oNhom1.Value = "I. CHỈ SỐ TỔNG QUAN";
        oNhom1.Style.Font.Bold = true;
        oNhom1.Style.Font.FontSize = 12;
        oNhom1.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] kpiLabels =
        [
            "Số mã hàng phát sinh bán",
            "Tổng số lượng bán",
            "Doanh thu (sau chiết khấu)",
            "Giá vốn (snapshot khi bán)",
            "Lợi nhuận gộp",
            "Tỷ lệ lợi nhuận / Doanh thu (%)"
        ];

        object[] kpiValues =
        [
            th.SoMaHang,
            th.SoLuongBan,
            th.DoanhThu,
            th.GiaVon,
            th.LoiNhuan,
            th.TyLeLoiNhuan
        ];

        for (int k = 0; k < kpiLabels.Length; k++)
        {
            var cellLabel = ws.Cell(dong, 1);
            cellLabel.Value = kpiLabels[k];
            cellLabel.Style.Font.Bold = k >= 2;
            cellLabel.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cellLabel.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);

            var cellVal = ws.Cell(dong, 2);
            if (k == 0)
                ExcelHelper.GhiO_Chu(cellVal, kpiValues[k], XLAlignmentHorizontalValues.Right);
            else if (k == 1)
                ExcelHelper.GhiO_SoLuong(cellVal, (decimal)kpiValues[k]);
            else if (k == 5)
                ExcelHelper.GhiO_PhanTram(cellVal, (double)kpiValues[k]);
            else
                ExcelHelper.GhiO_Tien(cellVal, (decimal)kpiValues[k]);

            cellVal.Style.Font.Bold = k >= 2;
            dong++;
        }

        dong += 2;

        // BẢNG 2: TOP 10 SẢN PHẨM THEO DOANH THU
        var oNhom2 = ws.Cell(dong, 1);
        oNhom2.Value = "II. TOP 10 SẢN PHẨM THEO DOANH THU";
        oNhom2.Style.Font.Bold = true;
        oNhom2.Style.Font.FontSize = 12;
        oNhom2.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeTop = ["STT", "Mã hàng", "Tên hàng", "SL bán", "Doanh thu"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeTop);
        dong++;

        decimal topTongSl = 0, topTongDt = 0;
        for (int r = 0; r < duLieu.TopDoanhThu.Count; r++)
        {
            var sp = duLieu.TopDoanhThu[r];
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), sp.MaHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), sp.TenHang);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 4), sp.SoLuongBan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), sp.DoanhThu);

            topTongSl += sp.SoLuongBan;
            topTongDt += sp.DoanhThu;
            dong++;
        }

        if (duLieu.TopDoanhThu.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 3).Value = "TỔNG TOP 10";
            ws.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 4), topTongSl);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), topTongDt);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDeTop.Length);
            dong++;
        }

        dong += 2;

        // BẢNG 3: DOANH THU THEO NHÓM HÀNG
        var oNhom3 = ws.Cell(dong, 1);
        oNhom3.Value = "III. DOANH THU THEO NHÓM HÀNG";
        oNhom3.Style.Font.Bold = true;
        oNhom3.Style.Font.FontSize = 12;
        oNhom3.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeNhom = ["STT", "Nhóm hàng", "Số mã hàng", "Số lượng bán", "Doanh thu", "Tỷ trọng (%)"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeNhom);
        dong++;

        decimal nhomTongDt = 0, nhomTongSl = 0;
        int nhomTongMa = 0;
        for (int n = 0; n < duLieu.NhomHang.Count; n++)
        {
            var item = duLieu.NhomHang[n];
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), n + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), item.TenNhom);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), item.SoMaHang, XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 4), item.SoLuongBan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), item.DoanhThu);
            ExcelHelper.GhiO_PhanTram(ws.Cell(dong, 6), item.TyLePhanTram);

            nhomTongMa += item.SoMaHang;
            nhomTongSl += item.SoLuongBan;
            nhomTongDt += item.DoanhThu;
            dong++;
        }

        if (duLieu.NhomHang.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 2).Value = "TỔNG CỘNG";
            ws.Cell(dong, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), nhomTongMa, XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 4), nhomTongSl);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), nhomTongDt);
            ExcelHelper.GhiO_PhanTram(ws.Cell(dong, 6), 100.0);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDeNhom.Length);
        }

        for (int c = 1; c <= 6; c++)
        {
            ws.Column(c).AdjustToContents();
            if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
            if (ws.Column(c).Width > 45) ws.Column(c).Width = 45;
        }
        ws.ShowGridLines = true;
    }

    private static void GhiSheetChiTiet(
        XLWorkbook wb,
        BaoCaoHangHoaDuLieu duLieu,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ct = wb.Worksheets.Add("Chi tiết theo mã hàng");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ct,
            "BÁO CÁO CHI TIẾT HÀNG HÓA",
            $"Từ ngày {duLieu.TuNgay:dd/MM/yyyy} đến ngày {duLieu.DenNgay:dd/MM/yyyy} (Tổng số: {duLieu.ChiTiet.Count:N0} mã hàng)",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 8);

        string[] cot = ["STT", "Mã hàng", "Tên hàng", "Nhóm hàng", "SL bán", "Doanh thu", "Giá vốn", "Lợi nhuận"];
        ExcelHelper.GhiTieuDeCot(ct, dongHeader, cot);

        int dong = dongHeader + 1;
        for (int r = 0; r < duLieu.ChiTiet.Count; r++)
        {
            var row = duLieu.ChiTiet[r];
            ExcelHelper.GhiO_Chu(ct.Cell(dong, 1), r + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ct.Cell(dong, 2), row.MaHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ct.Cell(dong, 3), row.TenHang);
            ExcelHelper.GhiO_Chu(ct.Cell(dong, 4), row.NhomHang);
            ExcelHelper.GhiO_SoLuong(ct.Cell(dong, 5), row.SoLuongBan);
            ExcelHelper.GhiO_Tien(ct.Cell(dong, 6), row.DoanhThu);
            ExcelHelper.GhiO_Tien(ct.Cell(dong, 7), row.GiaVon);
            ExcelHelper.GhiO_Tien(ct.Cell(dong, 8), row.LoiNhuan);

            dong++;
        }

        // Dòng tổng cộng khớp KPI
        var th = duLieu.TongHop;
        ct.Cell(dong, 1).Value = "";
        ct.Cell(dong, 4).Value = "TỔNG CỘNG";
        ct.Cell(dong, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ExcelHelper.GhiO_SoLuong(ct.Cell(dong, 5), th.SoLuongBan);
        ExcelHelper.GhiO_Tien(ct.Cell(dong, 6), th.DoanhThu);
        ExcelHelper.GhiO_Tien(ct.Cell(dong, 7), th.GiaVon);
        ExcelHelper.GhiO_Tien(ct.Cell(dong, 8), th.LoiNhuan);
        ExcelHelper.DinhDangDongTongCong(ct, dong, 1, cot.Length);

        ExcelHelper.HoanThienTrang(ct, dongHeader, dong, cot.Length, batAutoFilter: duLieu.ChiTiet.Count > 0);
    }
}
