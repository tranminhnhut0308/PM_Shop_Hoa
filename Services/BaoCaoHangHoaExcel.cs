using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất báo cáo hàng hóa ra file Excel quản trị chuyên nghiệp:
/// - Sheet 1: Tổng hợp (Bảng KPI chỉ số tổng quan, Top 10 sản phẩm theo doanh thu có màu nhấn Top 3, Cơ cấu doanh thu theo nhóm hàng kèm thanh tỷ trọng trực quan)
/// - Sheet 2: Chi tiết theo mã hàng với Freeze Panes, AutoFilter, dòng tổng cộng, chuẩn in ấn A4.
/// </summary>
public static class BaoCaoHangHoaExcel
{
    public static XLWorkbook TaoWorkbook(
        BaoCaoHangHoaDuLieu duLieu,
        string? tenCuaHang = null,
        string? nguoiXuat = null)
    {
        var wb = new XLWorkbook();

        // ---------- Sheet 1: Tổng hợp & Phân tích ----------
        GhiSheetTongHop(wb, duLieu, tenCuaHang, nguoiXuat);

        // ---------- Sheet 2: Chi tiết toàn bộ mặt hàng ----------
        GhiSheetChiTiet(wb, duLieu, tenCuaHang, nguoiXuat);

        return wb;
    }

    private static void GhiSheetTongHop(
        XLWorkbook wb,
        BaoCaoHangHoaDuLieu duLieu,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng hợp hàng hóa");
        var th = duLieu.TongHop;

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO HÀNG HÓA TỔNG HỢP & PHÂN TÍCH",
            $"Khoảng thời gian: {duLieu.TuNgay:dd/MM/yyyy} - {duLieu.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 7);

        var dong = dongHeader;

        // ============================================================
        // PHẦN I: BẢNG KPI CHỈ SỐ TỔNG QUAN
        // ============================================================
        var oNhom1 = ws.Cell(dong, 1);
        oNhom1.Value = "I. CHỈ SỐ TỔNG QUAN QUẢN TRỊ HÀNG HÓA";
        oNhom1.Style.Font.Bold = true;
        oNhom1.Style.Font.FontSize = 12;
        oNhom1.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeKpi = ["STT", "Chỉ số quản trị", "Giá trị", "Đơn vị tính", "Đánh giá / Trạng thái"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeKpi);
        dong++;

        var danhSachKpi = new (string Ten, object GiaTri, string Kieu, string DonVi, string DanhGia, string? MauChu)[]
        {
            ("Tổng số mặt hàng phát sinh bán", th.SoMaHang, "int", "Mã hàng", "Có phát sinh giao dịch bán trong kỳ", null),
            ("Tổng số lượng bán", th.SoLuongBan, "soluong", "Sản phẩm", "Tổng khối lượng/số lượng sản phẩm bán ra", null),
            ("Doanh thu sau chiết khấu", th.DoanhThu, "tien", "VNĐ", "Doanh thu bán thực tế đã trừ giảm giá", ExcelHelper.MauTichCuc),
            ("Giá vốn hàng bán", th.GiaVon, "tien", "VNĐ", "Giá vốn snapshot tại thời điểm xuất bán", null),
            ("Lợi nhuận gộp", th.LoiNhuan, "tien", "VNĐ", "Doanh thu sau CK − Giá vốn hàng bán", th.LoiNhuan >= 0 ? ExcelHelper.MauTichCuc : ExcelHelper.MauNguyHiem),
            ("Tỷ suất lợi nhuận gộp", th.TyLeLoiNhuan, "phantram", "%", "Lợi nhuận gộp / Doanh thu sau CK", th.TyLeLoiNhuan >= 0 ? ExcelHelper.MauTichCuc : ExcelHelper.MauNguyHiem),
            ("Số mặt hàng có phát sinh giao dịch / danh mục", th.SoMatHangDangKinhDoanh > 0 ? th.SoMatHangDangKinhDoanh : th.TongSoMatHang, "int", "Mã hàng", "Số mã hàng đang hoạt động trong hệ thống", null),
            ("Số mặt hàng tồn kho bằng 0", th.SoMatHangHetHang, "int", "Mã hàng", th.SoMatHangHetHang > 0 ? "Cần lập kế hoạch nhập bổ sung" : "Mức tồn kho an toàn", th.SoMatHangHetHang > 0 ? ExcelHelper.MauNguyHiem : ExcelHelper.MauTichCuc)
        };

        for (int i = 0; i < danhSachKpi.Length; i++)
        {
            var item = danhSachKpi[i];
            var r = ws.Row(dong);
            r.Height = 21;

            if (i % 2 == 1)
            {
                ws.Range(dong, 1, dong, tieuDeKpi.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ExcelHelper.MauNenThe);
            }

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), item.Ten);
            ws.Cell(dong, 2).Style.Font.Bold = item.Ten.Contains("Doanh thu") || item.Ten.Contains("Lợi nhuận");

            var cellVal = ws.Cell(dong, 3);
            switch (item.Kieu)
            {
                case "tien":
                    ExcelHelper.GhiO_Tien(cellVal, Convert.ToDecimal(item.GiaTri), hienThiVnd: true);
                    break;
                case "soluong":
                    ExcelHelper.GhiO_SoLuong(cellVal, Convert.ToDecimal(item.GiaTri));
                    break;
                case "phantram":
                    cellVal.Value = Convert.ToDouble(item.GiaTri) / 100.0;
                    cellVal.Style.NumberFormat.Format = ExcelHelper.DinhDangPhanTram;
                    cellVal.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    cellVal.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cellVal.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);
                    break;
                default:
                    ExcelHelper.GhiO_Chu(cellVal, Convert.ToInt32(item.GiaTri).ToString("N0"), XLAlignmentHorizontalValues.Right);
                    break;
            }

            if (item.MauChu is not null)
            {
                cellVal.Style.Font.FontColor = XLColor.FromHtml(item.MauChu);
                cellVal.Style.Font.Bold = true;
            }

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), item.DonVi, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), item.DanhGia);
            if (item.Ten.Contains("tồn kho bằng 0") && th.SoMatHangHetHang > 0)
            {
                ws.Cell(dong, 5).Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauNguyHiem);
                ws.Cell(dong, 5).Style.Font.Bold = true;
            }

            dong++;
        }

        dong += 2;

        // ============================================================
        // PHẦN II: TOP 10 SẢN PHẨM THEO DOANH THU
        // ============================================================
        var oNhom2 = ws.Cell(dong, 1);
        oNhom2.Value = "II. TOP 10 SẢN PHẨM THEO DOANH THU";
        oNhom2.Style.Font.Bold = true;
        oNhom2.Style.Font.FontSize = 12;
        oNhom2.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeTop = ["STT", "Mã hàng", "Tên hàng", "Số lượng bán", "Doanh thu (VNĐ)"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeTop);
        int dongTopBatDau = dong + 1;
        dong++;

        decimal topTongSl = 0, topTongDt = 0;
        for (int r = 0; r < duLieu.TopDoanhThu.Count; r++)
        {
            var sp = duLieu.TopDoanhThu[r];
            ws.Row(dong).Height = 20;

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), sp.MaHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), sp.TenHang);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 4), sp.SoLuongBan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), sp.DoanhThu, hienThiVnd: true);

            // Màu nhấn cho Top 3 sản phẩm
            if (r < 3)
            {
                ExcelHelper.DinhDangDongTop(ws, dong, 1, tieuDeTop.Length, r + 1);
            }

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
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), topTongDt, hienThiVnd: true);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDeTop.Length);
            dong++;
        }

        dong += 2;

        // ============================================================
        // PHẦN III: DOANH THU THEO NHÓM HÀNG
        // ============================================================
        var oNhom3 = ws.Cell(dong, 1);
        oNhom3.Value = "III. DOANH THU THEO NHÓM HÀNG";
        oNhom3.Style.Font.Bold = true;
        oNhom3.Style.Font.FontSize = 12;
        oNhom3.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        string[] tieuDeNhom = ["STT", "Nhóm hàng", "Số mã hàng", "Số lượng bán", "Doanh thu (VNĐ)", "Tỷ trọng (%)", "Cơ cấu tỷ trọng"];
        ExcelHelper.GhiTieuDeCot(ws, dong, tieuDeNhom);
        dong++;

        decimal nhomTongDt = 0, nhomTongSl = 0;
        int nhomTongMa = 0;
        for (int n = 0; n < duLieu.NhomHang.Count; n++)
        {
            var item = duLieu.NhomHang[n];
            ws.Row(dong).Height = 20;

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), n + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), item.TenNhom);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), item.SoMaHang.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 4), item.SoLuongBan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), item.DoanhThu, hienThiVnd: true);

            var cellPhanTram = ws.Cell(dong, 6);
            cellPhanTram.Value = item.TyLePhanTram / 100.0;
            cellPhanTram.Style.NumberFormat.Format = ExcelHelper.DinhDangPhanTram;
            cellPhanTram.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            cellPhanTram.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cellPhanTram.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);

            var cellBar = ws.Cell(dong, 7);
            cellBar.Value = ExcelHelper.TaoThanhTrucQuan(item.TyLePhanTram, 12);
            cellBar.Style.Font.FontName = "Consolas";
            cellBar.Style.Font.FontSize = 9.5;
            cellBar.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
            cellBar.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            cellBar.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cellBar.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);

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
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), nhomTongMa.ToString("N0"), XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 4), nhomTongSl);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), nhomTongDt, hienThiVnd: true);

            var cellPhanTramTong = ws.Cell(dong, 6);
            cellPhanTramTong.Value = 1.0;
            cellPhanTramTong.Style.NumberFormat.Format = ExcelHelper.DinhDangPhanTram;
            cellPhanTramTong.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var cellBarTong = ws.Cell(dong, 7);
            cellBarTong.Value = ExcelHelper.TaoThanhTrucQuan(100.0, 12);
            cellBarTong.Style.Font.FontName = "Consolas";
            cellBarTong.Style.Font.FontSize = 9.5;
            cellBarTong.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);

            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDeNhom.Length);
        }

        // Tự động điều chỉnh độ rộng cột
        for (int c = 1; c <= tieuDeNhom.Length; c++)
        {
            ws.Column(c).AdjustToContents();
            if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
            if (ws.Column(c).Width > 45) ws.Column(c).Width = 45;
        }

        ws.ShowGridLines = true;
        ExcelHelper.ThietLapTrangIn(ws, XLPageOrientation.Landscape);
    }

    private static void GhiSheetChiTiet(
        XLWorkbook wb,
        BaoCaoHangHoaDuLieu duLieu,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ct = wb.Worksheets.Add("Chi tiết toàn bộ mã hàng");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ct,
            "BÁO CÁO CHI TIẾT HÀNG HÓA",
            $"Khoảng thời gian: {duLieu.TuNgay:dd/MM/yyyy} - {duLieu.DenNgay:dd/MM/yyyy}  (Tổng số: {duLieu.ChiTiet.Count:N0} mã hàng)",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 8);

        string[] cot = ["STT", "Mã hàng", "Tên hàng", "Nhóm hàng", "Số lượng bán", "Doanh thu (VNĐ)", "Giá vốn (VNĐ)", "Lợi nhuận (VNĐ)"];
        ExcelHelper.GhiTieuDeCot(ct, dongHeader, cot);

        int dong = dongHeader + 1;
        for (int r = 0; r < duLieu.ChiTiet.Count; r++)
        {
            var row = duLieu.ChiTiet[r];
            ct.Row(dong).Height = 20;

            if (r % 2 == 1)
            {
                ct.Range(dong, 1, dong, cot.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ExcelHelper.MauNenThe);
            }

            ExcelHelper.GhiO_Chu(ct.Cell(dong, 1), r + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ct.Cell(dong, 2), row.MaHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ct.Cell(dong, 3), row.TenHang);
            ExcelHelper.GhiO_Chu(ct.Cell(dong, 4), row.NhomHang);
            ExcelHelper.GhiO_SoLuong(ct.Cell(dong, 5), row.SoLuongBan);
            ExcelHelper.GhiO_Tien(ct.Cell(dong, 6), row.DoanhThu, hienThiVnd: true);
            ExcelHelper.GhiO_Tien(ct.Cell(dong, 7), row.GiaVon, hienThiVnd: true);

            var cellLn = ct.Cell(dong, 8);
            ExcelHelper.GhiO_Tien(cellLn, row.LoiNhuan, hienThiVnd: true);
            if (row.LoiNhuan < 0)
            {
                cellLn.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauNguyHiem);
                cellLn.Style.Font.Bold = true;
            }
            else
            {
                cellLn.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauTichCuc);
            }

            dong++;
        }

        // Dòng tổng cộng khớp KPI
        var th = duLieu.TongHop;
        ct.Cell(dong, 1).Value = "";
        ct.Cell(dong, 4).Value = "TỔNG CỘNG";
        ct.Cell(dong, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ExcelHelper.GhiO_SoLuong(ct.Cell(dong, 5), th.SoLuongBan);
        ExcelHelper.GhiO_Tien(ct.Cell(dong, 6), th.DoanhThu, hienThiVnd: true);
        ExcelHelper.GhiO_Tien(ct.Cell(dong, 7), th.GiaVon, hienThiVnd: true);

        var cellLnTong = ct.Cell(dong, 8);
        ExcelHelper.GhiO_Tien(cellLnTong, th.LoiNhuan, hienThiVnd: true);
        cellLnTong.Style.Font.FontColor = th.LoiNhuan >= 0 ? XLColor.FromHtml(ExcelHelper.MauTichCuc) : XLColor.FromHtml(ExcelHelper.MauNguyHiem);

        ExcelHelper.DinhDangDongTongCong(ct, dong, 1, cot.Length);

        ExcelHelper.HoanThienTrang(ct, dongHeader, dong, cot.Length, batAutoFilter: duLieu.ChiTiet.Count > 0, minWidth: 11, maxWidth: 45);
        ExcelHelper.ThietLapTrangIn(ct, XLPageOrientation.Landscape);
    }
}
