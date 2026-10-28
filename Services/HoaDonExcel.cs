using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng file Excel cho danh sách hóa đơn: liệt kê đầy đủ các hóa đơn
/// khớp bộ lọc/tìm kiếm hiện tại với tiêu đề, định dạng số, ngày giờ và dòng tổng cộng.
/// </summary>
public static class HoaDonExcel
{
    private const string MauTieuDe = "#164B40";
    private const string DinhDangTien = "#,##0";
    private const string DinhDangNgayGio = "dd/MM/yyyy HH:mm";

    public static XLWorkbook TaoWorkbook(IReadOnlyList<HoaDon> items, DateTime thoiDiemXuat, string? ghiChuLoc = null)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Hóa đơn");

        ws.Cell(1, 1).Value = "Danh sách hóa đơn bán hàng";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        var subTitle = $"Xuất lúc {thoiDiemXuat:dd/MM/yyyy HH:mm} — {items.Count:N0} hóa đơn";
        if (!string.IsNullOrWhiteSpace(ghiChuLoc))
        {
            subTitle += $" ({ghiChuLoc})";
        }
        ws.Cell(2, 1).Value = subTitle;

        string[] tieuDe =
        [
            "STT", "Mã hóa đơn", "Thời gian", "Mã khách hàng", "Tên khách hàng",
            "Tổng tiền hàng", "Giảm giá", "Khách cần trả", "Đã thanh toán", "Còn nợ",
            "Ghi chú"
        ];

        const int dongTieuDe = 4;
        for (var c = 0; c < tieuDe.Length; c++)
        {
            var o = ws.Cell(dongTieuDe, c + 1);
            o.Value = tieuDe[c];
            o.Style.Font.Bold = true;
            o.Style.Font.FontColor = XLColor.White;
            o.Style.Fill.BackgroundColor = XLColor.FromHtml(MauTieuDe);
            o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            o.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        ws.Row(dongTieuDe).Height = 24;

        decimal tongTienHang = 0, tongGiamGia = 0, tongCanTra = 0, tongDaThanhToan = 0, tongConNo = 0;

        var dong = dongTieuDe + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var hd = items[i];

            ws.Cell(dong, 1).Value = i + 1;
            ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(dong, 2).Value = hd.MaHoaDon;
            ws.Cell(dong, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var oThoiGian = ws.Cell(dong, 3);
            oThoiGian.Value = hd.ThoiGianBan;
            oThoiGian.Style.NumberFormat.Format = DinhDangNgayGio;
            oThoiGian.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(dong, 4).Value = hd.MaKhachHang ?? "";
            ws.Cell(dong, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(dong, 5).Value = string.IsNullOrWhiteSpace(hd.TenKhachHang) ? "Khách lẻ" : hd.TenKhachHang;

            ws.Cell(dong, 6).Value = hd.TongTienHang;
            ws.Cell(dong, 7).Value = hd.GiamGia;
            ws.Cell(dong, 8).Value = hd.KhachCanTra;
            ws.Cell(dong, 9).Value = hd.KhachDaThanhToan;
            ws.Cell(dong, 10).Value = hd.ConNo;

            for (var c = 6; c <= 10; c++)
            {
                ws.Cell(dong, c).Style.NumberFormat.Format = DinhDangTien;
                ws.Cell(dong, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            ws.Cell(dong, 11).Value = hd.GhiChu ?? "";

            tongTienHang += hd.TongTienHang;
            tongGiamGia += hd.GiamGia;
            tongCanTra += hd.KhachCanTra;
            tongDaThanhToan += hd.KhachDaThanhToan;
            tongConNo += hd.ConNo;

            dong++;
        }

        if (items.Count > 0)
        {
            ws.Cell(dong, 5).Value = "Tổng cộng";
            ws.Cell(dong, 5).Style.Font.Bold = true;
            ws.Cell(dong, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(dong, 6).Value = tongTienHang;
            ws.Cell(dong, 7).Value = tongGiamGia;
            ws.Cell(dong, 8).Value = tongCanTra;
            ws.Cell(dong, 9).Value = tongDaThanhToan;
            ws.Cell(dong, 10).Value = tongConNo;

            for (var c = 6; c <= 10; c++)
            {
                ws.Cell(dong, c).Style.Font.Bold = true;
                ws.Cell(dong, c).Style.NumberFormat.Format = DinhDangTien;
                ws.Cell(dong, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            ws.Row(dong).Height = 22;
        }

        ws.SheetView.FreezeRows(dongTieuDe);

        ws.Column(1).Width = 6;   // STT
        ws.Column(2).Width = 18;  // Mã HĐ
        ws.Column(3).Width = 18;  // Thời gian
        ws.Column(4).Width = 15;  // Mã KH
        ws.Column(5).Width = 26;  // Tên KH
        ws.Column(6).Width = 16;  // Tiền hàng
        ws.Column(7).Width = 14;  // Giảm giá
        ws.Column(8).Width = 16;  // Cần trả
        ws.Column(9).Width = 16;  // Đã trả
        ws.Column(10).Width = 16; // Còn nợ
        ws.Column(11).Width = 30; // Ghi chú

        return wb;
    }
}
