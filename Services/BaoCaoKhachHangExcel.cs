using System;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất báo cáo khách hàng ra file Excel chuyên nghiệp:
/// - Sheet 1: Tổng hợp (Chỉ số tổng quan phát sinh trong kỳ vs số dư công nợ hiện tại)
/// - Sheet 2: Top khách hàng theo tiêu chí quan tâm
/// - Sheet 3: Danh sách chi tiết khách hàng và chỉ số mua hàng, thanh toán, công nợ.
/// </summary>
public static class BaoCaoKhachHangExcel
{
    public static XLWorkbook TaoWorkbook(
        BaoCaoKhachHangDuLieu duLieu,
        string? tenCuaHang = null,
        string? nguoiXuat = null)
    {
        var wb = new XLWorkbook();

        GhiSheetTongHop(wb, duLieu, tenCuaHang, nguoiXuat);
        GhiSheetTop(wb, duLieu, tenCuaHang, nguoiXuat);
        GhiSheetChiTiet(wb, duLieu, tenCuaHang, nguoiXuat);

        return wb;
    }

    private static void GhiSheetTongHop(
        XLWorkbook wb,
        BaoCaoKhachHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng hợp");
        var t = d.TongHop;

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO KHÁCH HÀNG TỔNG HỢP",
            $"Từ ngày {t.TuNgay:dd/MM/yyyy} đến ngày {t.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 3);

        var dong = dongHeader;
        var oNhom = ws.Cell(dong, 1);
        oNhom.Value = "CHỈ SỐ BÁN HÀNG & CÔNG NỢ KHÁCH HÀNG";
        oNhom.Style.Font.Bold = true;
        oNhom.Style.Font.FontSize = 12;
        oNhom.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        (string Label, object Value, bool LaTien)[] kpis =
        [
            ("Tổng số khách hàng phát sinh giao dịch", t.TongKhachHang, false),
            ("Số hóa đơn / đơn hàng trong kỳ", t.SoDonHang, false),
            ("Tổng tiền hàng trong kỳ", t.TongTienHang, true),
            ("Tổng giảm giá trong kỳ", t.GiamGia, true),
            ("Doanh số mua hàng (Tiền hàng − Giảm giá)", t.GiaTriMuaHang, true),
            ("Tổng tiền khách đã thanh toán trong kỳ", t.DaThanhToan, true),
            ("Tổng công nợ phát sinh theo kỳ", t.CongNo, true)
        ];

        foreach (var (lbl, val, laTien) in kpis)
        {
            var cellLbl = ws.Cell(dong, 1);
            cellLbl.Value = lbl;
            cellLbl.Style.Font.Bold = lbl.Contains("Doanh số") || lbl.Contains("Tổng số");
            cellLbl.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cellLbl.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);

            var cellVal = ws.Cell(dong, 2);
            if (laTien)
                ExcelHelper.GhiO_Tien(cellVal, Convert.ToDecimal(val));
            else
                ExcelHelper.GhiO_Chu(cellVal, val, XLAlignmentHorizontalValues.Right);

            cellVal.Style.Font.Bold = cellLbl.Style.Font.Bold;
            dong++;
        }

        ws.Column(1).Width = 52;
        ws.Column(2).Width = 22;
        ws.ShowGridLines = true;
    }

    private static void GhiSheetTop(
        XLWorkbook wb,
        BaoCaoKhachHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Top khách hàng");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "TOP 10 KHÁCH HÀNG TIÊU BIỂU",
            $"Từ ngày {d.TongHop.TuNgay:dd/MM/yyyy} đến ngày {d.TongHop.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 5);

        string[] tieuDe = ["Thứ hạng", "Mã khách hàng", "Tên khách hàng", "Số điện thoại", "Giá trị"];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        var dong = dongHeader + 1;
        decimal tongGiaTri = 0;
        foreach (var r in d.Top)
        {
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r.Stt, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.MaKhachHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), r.TenKhachHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), r.SoDienThoai, XLAlignmentHorizontalValues.Center);

            if (r.LaTien)
                ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), r.GiaTri);
            else
                ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), r.GiaTri, XLAlignmentHorizontalValues.Right);

            tongGiaTri += r.GiaTri;
            dong++;
        }

        if (d.Top.Count > 0)
        {
            ws.Cell(dong, 1).Value = "TỔNG TOP 10";
            ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), tongGiaTri);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.Top.Count > 0);
    }

    private static void GhiSheetChiTiet(
        XLWorkbook wb,
        BaoCaoKhachHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Chi tiết khách hàng");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CHI TIẾT DOANH SỐ & CÔNG NỢ KHÁCH HÀNG",
            $"Từ ngày {d.TongHop.TuNgay:dd/MM/yyyy} đến ngày {d.TongHop.DenNgay:dd/MM/yyyy} (Tổng số: {d.DongTrang.Count:N0} khách hàng)",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 10);

        string[] tieuDe =
        [
            "STT", "Mã khách hàng", "Tên khách hàng", "Số điện thoại", "Số đơn hàng",
            "Tổng tiền hàng", "Giảm giá", "Doanh số mua hàng", "Đã thanh toán", "Công nợ"
        ];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongTien = 0, tongGiam = 0, tongDoanhSo = 0, tongDaThu = 0, tongCongNo = 0;
        int tongSoDon = 0;

        var dong = dongHeader + 1;
        foreach (var r in d.DongTrang)
        {
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r.Stt, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.MaKhachHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), r.TenKhachHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), r.SoDienThoai, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), r.SoDonHang, XLAlignmentHorizontalValues.Right);

            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), r.TongTienHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), r.GiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), r.GiaTriMuaHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), r.DaThanhToan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), r.CongNo);

            tongSoDon += r.SoDonHang;
            tongTien += r.TongTienHang;
            tongGiam += r.GiamGia;
            tongDoanhSo += r.GiaTriMuaHang;
            tongDaThu += r.DaThanhToan;
            tongCongNo += r.CongNo;

            dong++;
        }

        // Dòng tổng cộng
        if (d.DongTrang.Count > 0)
        {
            ws.Cell(dong, 1).Value = "TỔNG CỘNG";
            ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), tongSoDon, XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), tongTien);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), tongGiam);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), tongDoanhSo);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), tongDaThu);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), tongCongNo);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.DongTrang.Count > 0);
    }
}
