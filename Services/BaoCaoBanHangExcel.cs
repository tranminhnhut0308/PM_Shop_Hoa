using System;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng file Excel cho báo cáo bán hàng chuẩn hoá:
/// Tổng hợp, Doanh thu theo ngày, Chi tiết hóa đơn, Top sản phẩm, Top khách hàng,
/// Doanh thu theo nhóm hàng, Phương thức thanh toán.
/// </summary>
public static class BaoCaoBanHangExcel
{
    public static XLWorkbook TaoWorkbook(
        BaoCaoBanHangDuLieu duLieu,
        string? tenCuaHang = null,
        string? nguoiXuat = null)
    {
        var wb = new XLWorkbook();

        GhiSheetTongHop(wb, duLieu, tenCuaHang, nguoiXuat);
        GhiSheetTheoNgay(wb, duLieu, tenCuaHang, nguoiXuat);
        GhiSheetChiTietHoaDon(wb, duLieu, tenCuaHang, nguoiXuat);
        GhiSheetTopSanPham(wb, duLieu, tenCuaHang, nguoiXuat);
        GhiSheetTopKhachHang(wb, duLieu, tenCuaHang, nguoiXuat);
        GhiSheetNhomHang(wb, duLieu, tenCuaHang, nguoiXuat);
        GhiSheetPhuongThuc(wb, duLieu, tenCuaHang, nguoiXuat);

        return wb;
    }

    // ============================================================
    // 1. TỔNG HỢP
    // ============================================================

    private static void GhiSheetTongHop(
        XLWorkbook wb,
        BaoCaoBanHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng hợp");
        var t = d.TongHop;

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO BÁN HÀNG TỔNG HỢP",
            $"Từ ngày {d.TuNgay:dd/MM/yyyy} đến ngày {d.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 3);

        var dong = dongHeader;
        var oNhom = ws.Cell(dong, 1);
        oNhom.Value = "CHỈ SỐ BÁN HÀNG CHÍNH";
        oNhom.Style.Font.Bold = true;
        oNhom.Style.Font.FontSize = 12;
        oNhom.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
        dong++;

        (string Label, object Value, bool LaTien)[] kpis =
        [
            ("Số hóa đơn bán hàng", t.SoHoaDon, false),
            ("Tổng tiền hàng", t.TongTienHang, true),
            ("Tổng giảm giá", t.GiamGia, true),
            ("Thu khác", t.ThuKhac, true),
            ("Doanh thu bán hàng (Tiền hàng − Giảm giá + Thu khác)", t.DoanhThu, true),
            ("Tổng tiền đã thu", t.DaThu, true),
            ("Tổng công nợ phát sinh còn lại", t.CongNo, true),
            ("Giá trị đơn hàng trung bình", t.GiaTriDonTrungBinh, true)
        ];

        foreach (var (lbl, val, laTien) in kpis)
        {
            var cellLbl = ws.Cell(dong, 1);
            cellLbl.Value = lbl;
            cellLbl.Style.Font.Bold = lbl.Contains("Doanh thu") || lbl.Contains("Số hóa đơn");
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

        ws.Column(1).Width = 50;
        ws.Column(2).Width = 22;
        ws.ShowGridLines = true;
    }

    // ============================================================
    // 2. DOANH THU THEO NGÀY
    // ============================================================

    private static void GhiSheetTheoNgay(
        XLWorkbook wb,
        BaoCaoBanHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Theo ngày");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO DOANH THU BÁN HÀNG THEO NGÀY",
            $"Từ ngày {d.TuNgay:dd/MM/yyyy} đến ngày {d.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 8);

        string[] tieuDe = ["Ngày", "Số hóa đơn", "Tiền hàng", "Giảm giá", "Thu khác", "Doanh thu", "Đã thu", "Công nợ"];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        var dong = dongHeader + 1;
        foreach (var r in d.BangDuLieu)
        {
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r.ThoiGian.ToString("dd/MM/yyyy"), XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.SoHoaDon, XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 3), r.TongTienHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), r.GiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), r.ThuKhac);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), r.DoanhThu);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), r.DaThu);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), r.CongNo);
            dong++;
        }

        // Dòng tổng cộng
        ws.Cell(dong, 1).Value = "TỔNG CỘNG";
        ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), d.TongHop.SoHoaDon, XLAlignmentHorizontalValues.Right);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 3), d.TongHop.TongTienHang);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), d.TongHop.GiamGia);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), d.TongHop.ThuKhac);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), d.TongHop.DoanhThu);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), d.TongHop.DaThu);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), d.TongHop.CongNo);
        ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.BangDuLieu.Count > 0);
    }

    // ============================================================
    // 3. CHI TIẾT HÓA ĐƠN
    // ============================================================

    private static void GhiSheetChiTietHoaDon(
        XLWorkbook wb,
        BaoCaoBanHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Chi tiết hóa đơn");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CHI TIẾT HÓA ĐƠN BÁN HÀNG",
            $"Từ ngày {d.TuNgay:dd/MM/yyyy} đến ngày {d.DenNgay:dd/MM/yyyy} (Tổng số: {d.ChiTietHoaDon.Count:N0} hóa đơn)",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 10);

        string[] tieuDe =
        [
            "Mã hóa đơn", "Thời gian", "Khách hàng",
            "Tiền hàng", "Giảm giá", "Thu khác", "Tổng thanh toán", "Đã thu", "Còn nợ", "Trạng thái"
        ];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongTien = 0, tongGiam = 0, tongThuK = 0, tongDoanhThu = 0, tongDaThu = 0, tongConNo = 0;
        var dong = dongHeader + 1;
        foreach (var r in d.ChiTietHoaDon)
        {
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r.MaHoaDon, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_NgayGio(ws.Cell(dong, 2), r.ThoiGianBan, coGio: true);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), string.IsNullOrWhiteSpace(r.KhachHang) ? "Khách lẻ" : r.KhachHang);

            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), r.TongTienHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), r.GiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), r.ThuKhac);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), r.DoanhThu);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), r.DaThu);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), r.CongNo);

            var tt = !string.IsNullOrWhiteSpace(r.TrangThai) ? r.TrangThai : (r.CongNo <= 0 ? "Đã thanh toán" : (r.DaThu > 0 ? "Thanh toán một phần" : "Chưa thanh toán"));
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 10), tt, XLAlignmentHorizontalValues.Center);

            tongTien += r.TongTienHang;
            tongGiam += r.GiamGia;
            tongThuK += r.ThuKhac;
            tongDoanhThu += r.DoanhThu;
            tongDaThu += r.DaThu;
            tongConNo += r.CongNo;

            dong++;
        }

        // Dòng tổng cộng
        if (d.ChiTietHoaDon.Count > 0)
        {
            ws.Cell(dong, 1).Value = "TỔNG CỘNG";
            ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), tongTien);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), tongGiam);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), tongThuK);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), tongDoanhThu);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), tongDaThu);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), tongConNo);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.ChiTietHoaDon.Count > 0);
    }

    // ============================================================
    // 4. TOP SẢN PHẨM
    // ============================================================

    private static void GhiSheetTopSanPham(
        XLWorkbook wb,
        BaoCaoBanHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Top sản phẩm");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "TOP 10 SẢN PHẨM BÁN CHẠY NHẤT",
            $"Từ ngày {d.TuNgay:dd/MM/yyyy} đến ngày {d.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 4);

        string[] tieuDe = ["Thứ hạng", "Tên sản phẩm", "Số lượng bán", "Doanh thu"];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        var dong = dongHeader + 1;
        decimal tongSl = 0, tongDt = 0;
        foreach (var r in d.TopSanPham)
        {
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r.Stt, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.SanPham);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 3), r.SoLuongBan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), r.DoanhThu);

            tongSl += r.SoLuongBan;
            tongDt += r.DoanhThu;
            dong++;
        }

        if (d.TopSanPham.Count > 0)
        {
            ws.Cell(dong, 1).Value = "TỔNG TOP 10";
            ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 3), tongSl);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), tongDt);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.TopSanPham.Count > 0);
    }

    // ============================================================
    // 5. TOP KHÁCH HÀNG
    // ============================================================

    private static void GhiSheetTopKhachHang(
        XLWorkbook wb,
        BaoCaoBanHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Top khách hàng");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "TOP 10 KHÁCH HÀNG CÓ DOANH SỐ CAO NHẤT",
            $"Từ ngày {d.TuNgay:dd/MM/yyyy} đến ngày {d.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 6);

        string[] tieuDe = ["Thứ hạng", "Tên khách hàng", "Số hóa đơn", "Tổng mua", "Đã thanh toán", "Công nợ"];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        var dong = dongHeader + 1;
        int tongHd = 0;
        decimal tongDs = 0, tongDaThu = 0, tongCongNo = 0;
        foreach (var r in d.TopKhachHang)
        {
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r.Stt, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.KhachHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), r.SoHoaDon, XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), r.TongMua);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), r.DaThanhToan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), r.CongNo);

            tongHd += r.SoHoaDon;
            tongDs += r.TongMua;
            tongDaThu += r.DaThanhToan;
            tongCongNo += r.CongNo;
            dong++;
        }

        if (d.TopKhachHang.Count > 0)
        {
            ws.Cell(dong, 1).Value = "TỔNG TOP 10";
            ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), tongHd, XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), tongDs);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 5), tongDaThu);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), tongCongNo);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.TopKhachHang.Count > 0);
    }

    // ============================================================
    // 6. NHÓM HÀNG
    // ============================================================

    private static void GhiSheetNhomHang(
        XLWorkbook wb,
        BaoCaoBanHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Theo nhóm hàng");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CƠ CẤU DOANH THU THEO NHÓM HÀNG",
            $"Từ ngày {d.TuNgay:dd/MM/yyyy} đến ngày {d.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 5);

        string[] tieuDe = ["STT", "Nhóm hàng", "Số lượng bán", "Doanh thu", "Tỷ trọng (%)"];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        var dong = dongHeader + 1;
        decimal tongSl = 0, tongDt = 0;
        foreach (var r in d.NhomHang)
        {
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r.Stt, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.NhomHang);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 3), r.SoLuongBan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), r.DoanhThu);
            ExcelHelper.GhiO_PhanTram(ws.Cell(dong, 5), (double)r.TyLePhanTram);

            tongSl += r.SoLuongBan;
            tongDt += r.DoanhThu;
            dong++;
        }

        if (d.NhomHang.Count > 0)
        {
            ws.Cell(dong, 1).Value = "TỔNG CỘNG";
            ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 3), tongSl);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 4), tongDt);
            ExcelHelper.GhiO_PhanTram(ws.Cell(dong, 5), 100.0);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.NhomHang.Count > 0);
    }

    // ============================================================
    // 7. PHƯƠNG THỨC THANH TOÁN
    // ============================================================

    private static void GhiSheetPhuongThuc(
        XLWorkbook wb,
        BaoCaoBanHangDuLieu d,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Phương thức thanh toán");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CƠ CẤU PHƯƠNG THỨC THANH TOÁN",
            $"Từ ngày {d.TuNgay:dd/MM/yyyy} đến ngày {d.DenNgay:dd/MM/yyyy}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 4);

        string[] tieuDe = ["Phương thức", "Số giao dịch", "Số tiền thanh toán", "Tỷ trọng (%)"];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        var dong = dongHeader + 1;
        int tongGiaoDich = 0;
        decimal tongTien = 0;
        foreach (var r in d.PhuongThucThanhToan)
        {
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), r.PhuongThucHienThi);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.SoGiaoDich, XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 3), r.SoTien);
            ExcelHelper.GhiO_PhanTram(ws.Cell(dong, 4), (double)r.TyLePhanTram);

            tongGiaoDich += r.SoGiaoDich;
            tongTien += r.SoTien;
            dong++;
        }

        if (d.PhuongThucThanhToan.Count > 0)
        {
            ws.Cell(dong, 1).Value = "TỔNG CỘNG";
            ws.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), tongGiaoDich, XLAlignmentHorizontalValues.Right);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 3), tongTien);
            ExcelHelper.GhiO_PhanTram(ws.Cell(dong, 4), 100.0);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: d.PhuongThucThanhToan.Count > 0);
    }
}
