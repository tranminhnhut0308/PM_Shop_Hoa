using System;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất báo cáo nhà cung cấp ra file Excel chuyên nghiệp:
/// - Sheet 1: Tổng hợp (Chỉ số phát sinh trong kỳ & Số dư công nợ hiện tại, Top NCC theo giá trị nhập)
/// - Sheet 2: Danh sách chi tiết nhà cung cấp (Số phiếu nhập, Tổng nhập, Tiền trả/giảm trừ, Đã thanh toán, Công nợ hiện tại).
/// </summary>
public static class BaoCaoNhaCungCapExcel
{
    public static XLWorkbook TaoWorkbook(
        BaoCaoNhaCungCapDuLieu duLieu,
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
        BaoCaoNhaCungCapDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng hợp");
        var t = d.TongHop;

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO NHÀ CUNG CẤP TỔNG HỢP",
            $"Từ ngày {t.TuNgay:dd/MM/yyyy} đến ngày {t.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 3);

        var dong = dongHeader;
        var oNhom = ws.Cell(dong, 1);
        oNhom.Value = "CHỈ SỐ NHẬP HÀNG & CÔNG NỢ NHÀ CUNG CẤP";
        oNhom.Style.Font.Bold = true;
        oNhom.Style.Font.FontSize = 12;
        oNhom.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        (string Label, object Value, bool LaTien)[] kpis =
        [
            ("Tổng số nhà cung cấp phát sinh giao dịch", t.TongNhaCungCap, false),
            ("Số phiếu nhập hàng trong kỳ", t.SoPhieuNhap, false),
            ("Tổng tiền nhập hàng trong kỳ", t.TongTienNhap, true),
            ("Tiền trả hàng hoặc giảm trừ", t.TienTraHang, true),
            ("Giá trị nhập hàng sau giảm trừ", t.GiaTriNhapHang, true),
            ("Tổng tiền đã thanh toán cho NCC", t.DaThanhToan, true),
            ("Công nợ nhà cung cấp hiện tại (lũy kế)", t.CongNo, true)
        ];

        foreach (var (lbl, val, laTien) in kpis)
        {
            var cellLbl = ws.Cell(dong, 1);
            cellLbl.Value = lbl;
            cellLbl.Style.Font.Bold = lbl.Contains("Giá trị nhập") || lbl.Contains("Tổng số");
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

        // Chú thích rõ ràng về cơ chế số liệu
        dong++;
        var oNote = ws.Cell(dong, 1);
        oNote.Value = "(*) Lưu ý: Số phiếu nhập và Tổng tiền nhập phản ánh giao dịch phát sinh trong khoảng thời gian báo cáo. "
                    + "Cột Công nợ phản ánh số dư công nợ lũy kế hiện tại của nhà cung cấp.";
        oNote.Style.Font.Italic = true;
        oNote.Style.Font.FontSize = 9.5;
        oNote.Style.Font.FontColor = XLColor.FromHtml("#6E827C");

        ws.Column(1).Width = 52;
        ws.Column(2).Width = 22;
        ws.ShowGridLines = true;
    }

    private static void GhiSheetTop(
        XLWorkbook wb,
        BaoCaoNhaCungCapDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Top nhà cung cấp");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "TOP 10 NHÀ CUNG CẤP CÓ GIÁ TRỊ NHẬP LỚN NHẤT",
            $"Từ ngày {d.TongHop.TuNgay:dd/MM/yyyy} đến ngày {d.TongHop.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 5);

        string[] tieuDe = ["Thứ hạng", "Mã nhà cung cấp", "Tên nhà cung cấp", "Giá trị nhập hàng", "Công nợ hiện tại"];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        var dong = dongHeader + 1;
        decimal tongNhap = 0, tongCn = 0;
        foreach (var r in d.Top)
        {
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r.Stt, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.MaNhaCungCap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), r.TenNhaCungCap);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), r.GiaTriNhapHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), r.CongNo);

            tongNhap += r.GiaTriNhapHang;
            tongCn += r.CongNo;
            dong++;
        }

        if (d.Top.Count > 0)
        {
            ws.Cell(dong, 1).Value = "TỔNG TOP 10";
            ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), tongNhap);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), tongCn);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.Top.Count > 0);
    }

    private static void GhiSheetChiTiet(
        XLWorkbook wb,
        BaoCaoNhaCungCapDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Chi tiết nhà cung cấp");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CHI TIẾT NHẬP HÀNG & CÔNG NỢ NHÀ CUNG CẤP",
            $"Từ ngày {d.TongHop.TuNgay:dd/MM/yyyy} đến ngày {d.TongHop.DenNgay:dd/MM/yyyy} (Tổng số: {d.DongTrang.Count:N0} nhà cung cấp)",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 10);

        string[] tieuDe =
        [
            "STT", "Mã nhà cung cấp", "Tên nhà cung cấp", "Số điện thoại", "Số phiếu nhập",
            "Tổng tiền nhập", "Tiền trả hàng", "Giá trị nhập hàng", "Đã thanh toán", "Công nợ hiện tại"
        ];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongTien = 0, tongTra = 0, tongNhap = 0, tongDaTra = 0, tongCongNo = 0;
        int tongPhieu = 0;

        var dong = dongHeader + 1;
        foreach (var r in d.DongTrang)
        {
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r.Stt, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.MaNhaCungCap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), r.TenNhaCungCap);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), r.SoDienThoai, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), r.SoPhieuNhap, XLAlignmentHorizontalValues.Right);

            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), r.TongTienNhap);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), r.TienTraHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), r.GiaTriNhapHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), r.DaThanhToan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), r.CongNo);

            tongPhieu += r.SoPhieuNhap;
            tongTien += r.TongTienNhap;
            tongTra += r.TienTraHang;
            tongNhap += r.GiaTriNhapHang;
            tongDaTra += r.DaThanhToan;
            tongCongNo += r.CongNo;

            dong++;
        }

        // Dòng tổng cộng
        if (d.DongTrang.Count > 0)
        {
            ws.Cell(dong, 1).Value = "TỔNG CỘNG";
            ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), tongPhieu, XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), tongTien);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), tongTra);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), tongNhap);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), tongDaTra);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), tongCongNo);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.DongTrang.Count > 0);
    }
}
