using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ShopHoa.Services;

namespace ShopHoa.Helpers;

/// <summary>
/// Tiện ích định dạng và tạo trang Excel chuẩn hoá cho toàn bộ hệ thống ShopHoa:
/// Tiêu đề, Header, Khung viền, Căn lề, Định dạng tiền tệ/số lượng/ngày/phần trăm,
/// Cố định dòng tiêu đề, Tự động bật bộ lọc (AutoFilter), Tự động chỉnh độ rộng cột.
/// </summary>
public static class ExcelHelper
{
    public const string MauChuDao = "#164B40";      // Màu xanh chủ đạo của ShopHoa
    public const string MauNenNhe = "#EAF6F3";      // Nền nhấn nhẹ
    public const string MauVien = "#D0DFDA";        // Màu viền ô tinh tế
    public const string MauTongCong = "#DDF0EB";     // Màu nền dòng tổng cộng

    public const string DinhDangTien = "#,##0";
    public const string DinhDangTienVND = "#,##0\" ₫\"";
    public const string DinhDangSoLuong = "#,##0.###";
    public const string DinhDangPhanTram = "0.00%";
    public const string DinhDangPhanTramSo = "0.00";
    public const string DinhDangNgayGio = "dd/MM/yyyy HH:mm";
    public const string DinhDangNgay = "dd/MM/yyyy";

    /// <summary>
    /// Ghi phần đầu báo cáo (Banner thông tin tiệm, tên báo cáo, khoảng thời gian, ngày giờ xuất, người xuất).
    /// </summary>
    public static int GhiHeaderBaoCao(
        IXLWorksheet sheet,
        string tenBaoCao,
        string? khoangThoiGian = null,
        DateTime? thoiDiemXuat = null,
        string? tenCuaHang = null,
        string? nguoiXuat = null,
        int tongSoCot = 8)
    {
        var dong = 1;

        // 1. Tên cửa hàng nếu có
        var storeName = !string.IsNullOrWhiteSpace(tenCuaHang) ? tenCuaHang : "CỬA HÀNG SHOP HOA";
        var oStore = sheet.Cell(dong, 1);
        oStore.Value = storeName.ToUpper();
        oStore.Style.Font.Bold = true;
        oStore.Style.Font.FontSize = 10;
        oStore.Style.Font.FontColor = XLColor.FromHtml(MauChuDao);
        dong++;

        // 2. Tiêu đề báo cáo
        var oTitle = sheet.Cell(dong, 1);
        oTitle.Value = tenBaoCao;
        oTitle.Style.Font.Bold = true;
        oTitle.Style.Font.FontSize = 16;
        oTitle.Style.Font.FontColor = XLColor.FromHtml(MauChuDao);
        dong++;

        // 3. Khoảng thời gian
        if (!string.IsNullOrWhiteSpace(khoangThoiGian))
        {
            var oTime = sheet.Cell(dong, 1);
            oTime.Value = khoangThoiGian;
            oTime.Style.Font.Italic = true;
            oTime.Style.Font.FontSize = 11;
            oTime.Style.Font.FontColor = XLColor.FromHtml("#4A5B56");
            dong++;
        }

        // 4. Metadata: Ngày giờ xuất & Người xuất
        var thoiGianStr = (thoiDiemXuat ?? DateTime.Now).ToString(DinhDangNgayGio);
        var meta = $"Thời gian xuất: {thoiGianStr}";
        var user = !string.IsNullOrWhiteSpace(nguoiXuat) ? nguoiXuat : Session.CurrentUser?.HoTen;
        if (!string.IsNullOrWhiteSpace(user))
        {
            meta += $"  |  Người xuất: {user}";
        }
        var oMeta = sheet.Cell(dong, 1);
        oMeta.Value = meta;
        oMeta.Style.Font.FontSize = 9.5;
        oMeta.Style.Font.FontColor = XLColor.FromHtml("#6E827C");
        dong++;

        // Dòng trống cách biệt
        dong++;
        return dong;
    }

    /// <summary>
    /// Ghi hàng tiêu đề cột của bảng chi tiết.
    /// </summary>
    public static void GhiTieuDeCot(IXLWorksheet sheet, int dong, params string[] tieuDe)
    {
        for (var c = 0; c < tieuDe.Length; c++)
        {
            var o = sheet.Cell(dong, c + 1);
            o.Value = tieuDe[c];
            o.Style.Font.Bold = true;
            o.Style.Font.FontSize = 11;
            o.Style.Font.FontColor = XLColor.White;
            o.Style.Fill.BackgroundColor = XLColor.FromHtml(MauChuDao);
            o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            o.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            o.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            o.Style.Border.OutsideBorderColor = XLColor.FromHtml(MauVien);
        }

        sheet.Row(dong).Height = 26;
        sheet.SheetView.FreezeRows(dong);
    }

    /// <summary>
    /// Ghi một ô số tiền với định dạng tiền chuẩn và căn lề phải.
    /// </summary>
    public static void GhiO_Tien(IXLCell o, decimal giaTri, bool hienThiVnd = false)
    {
        o.Value = giaTri;
        o.Style.NumberFormat.Format = hienThiVnd ? DinhDangTienVND : DinhDangTien;
        o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        o.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        o.Style.Border.OutsideBorderColor = XLColor.FromHtml(MauVien);
    }

    /// <summary>
    /// Ghi một ô số lượng với định dạng số lượng chuẩn và căn lề phải.
    /// </summary>
    public static void GhiO_SoLuong(IXLCell o, decimal giaTri)
    {
        o.Value = giaTri;
        o.Style.NumberFormat.Format = DinhDangSoLuong;
        o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        o.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        o.Style.Border.OutsideBorderColor = XLColor.FromHtml(MauVien);
    }

    /// <summary>
    /// Ghi một ô tỷ lệ phần trăm (0..100) hoặc số thập phân thuần.
    /// </summary>
    public static void GhiO_PhanTram(IXLCell o, double giaTri)
    {
        o.Value = giaTri;
        o.Style.NumberFormat.Format = DinhDangPhanTramSo;
        o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        o.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        o.Style.Border.OutsideBorderColor = XLColor.FromHtml(MauVien);
    }

    /// <summary>
    /// Ghi một ô ngày giờ căn giữa.
    /// </summary>
    public static void GhiO_NgayGio(IXLCell o, DateTime giaTri, bool coGio = true)
    {
        o.Value = giaTri;
        o.Style.NumberFormat.Format = coGio ? DinhDangNgayGio : DinhDangNgay;
        o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        o.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        o.Style.Border.OutsideBorderColor = XLColor.FromHtml(MauVien);
    }

    /// <summary>
    /// Ghi một ô văn bản thông thường (mặc định căn trái, có viền mỏng).
    /// </summary>
    public static void GhiO_Chu(IXLCell o, object? giaTri, XLAlignmentHorizontalValues canLe = XLAlignmentHorizontalValues.Left)
    {
        if (giaTri is null)
            o.Value = "";
        else
            o.Value = XLCellValue.FromObject(giaTri);

        o.Style.Alignment.Horizontal = canLe;
        o.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        o.Style.Border.OutsideBorderColor = XLColor.FromHtml(MauVien);
    }

    /// <summary>
    /// Định dạng dòng Tổng cộng ở cuối bảng: Font đậm, nền nhấn, viền trên đôi hoặc viền nét.
    /// </summary>
    public static void DinhDangDongTongCong(IXLWorksheet sheet, int dong, int cotBatDau, int cotKetThuc)
    {
        var range = sheet.Range(dong, cotBatDau, dong, cotKetThuc);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(MauTongCong);
        range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Double;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Row(dong).Height = 22;
    }

    /// <summary>
    /// Tinh chỉnh độ rộng các cột và bật bộ lọc AutoFilter nếu có dữ liệu.
    /// </summary>
    public static void HoanThienTrang(
        IXLWorksheet sheet,
        int dongTieuDe,
        int dongCuoi,
        int soCot,
        bool batAutoFilter = true,
        double minWidth = 10,
        double maxWidth = 45)
    {
        if (batAutoFilter && dongCuoi > dongTieuDe)
        {
            sheet.Range(dongTieuDe, 1, dongCuoi, soCot).SetAutoFilter();
        }

        // Tự động điều chỉnh độ rộng cột
        for (int c = 1; c <= soCot; c++)
        {
            var col = sheet.Column(c);
            col.AdjustToContents(dongTieuDe, Math.Max(dongCuoi, dongTieuDe + 1));
            if (col.Width < minWidth) col.Width = minWidth;
            if (col.Width > maxWidth) col.Width = maxWidth;
        }

        sheet.ShowGridLines = true;
    }
}
