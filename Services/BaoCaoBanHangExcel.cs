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

        // Hàng tiêu đề bảng 3 cột chuẩn theo Issue #47
        string[] tieuDeBang = ["Chỉ tiêu", "Giá trị", "Ghi chú"];
        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDeBang);

        var dong = dongHeader + 1;

        void GhiDongPhanNhom(string tenNhom)
        {
            var range = ws.Range(dong, 1, dong, 3);
            range.Merge();
            range.Value = tenNhom;
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 11;
            range.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
            range.Style.Fill.BackgroundColor = XLColor.FromHtml(ExcelHelper.MauNenNhe);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(dong).Height = 22;
            dong++;
        }

        void GhiChiTieu(string chiTieu, object giaTri, bool laTien, string ghiChu = "", bool inDam = false)
        {
            var c1 = ws.Cell(dong, 1);
            c1.Value = chiTieu;
            c1.Style.Font.Bold = inDam;
            c1.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            c1.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);

            var c2 = ws.Cell(dong, 2);
            if (laTien)
                ExcelHelper.GhiO_Tien(c2, Convert.ToDecimal(giaTri));
            else if (giaTri is int or long)
            {
                c2.Value = Convert.ToInt64(giaTri);
                c2.Style.NumberFormat.Format = ExcelHelper.DinhDangTien;
                c2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                c2.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                c2.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);
            }
            else if (giaTri is decimal or double or float)
            {
                c2.Value = Convert.ToDecimal(giaTri);
                c2.Style.NumberFormat.Format = ExcelHelper.DinhDangSoLuong;
                c2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                c2.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                c2.Style.Border.OutsideBorderColor = XLColor.FromHtml(ExcelHelper.MauVien);
            }
            else
            {
                ExcelHelper.GhiO_Chu(c2, giaTri, XLAlignmentHorizontalValues.Right);
            }
            c2.Style.Font.Bold = inDam;

            var c3 = ws.Cell(dong, 3);
            ExcelHelper.GhiO_Chu(c3, ghiChu, XLAlignmentHorizontalValues.Left);
            c3.Style.Font.Italic = true;
            c3.Style.Font.FontColor = XLColor.FromHtml("#6E827C");

            dong++;
        }

        // NHÓM 1: CHỈ SỐ BÁN HÀNG CHÍNH
        GhiDongPhanNhom("I. CHỈ SỐ BÁN HÀNG CHÍNH");
        GhiChiTieu("Số hóa đơn bán hàng", t.SoHoaDon, false, "Hóa đơn hoàn thành", true);
        GhiChiTieu("Tổng tiền hàng", t.TongTienHang, true, "Tổng giá trị niêm yết");
        GhiChiTieu("Tổng giảm giá", t.GiamGia, true, "Chiết khấu/khuyến mãi");
        GhiChiTieu("Thu khác", t.ThuKhac, true, "Phụ thu tính vào doanh thu");
        GhiChiTieu("Doanh thu bán hàng", t.DoanhThu, true, "Tiền hàng − Giảm giá + Thu khác", true);
        GhiChiTieu("Tổng tiền đã thu", t.DaThu, true, "Khách đã thanh toán");
        GhiChiTieu("Tổng công nợ phát sinh", t.CongNo, true, "Còn nợ trên hóa đơn");
        GhiChiTieu("Giá trị đơn hàng trung bình", t.GiaTriDonTrungBinh, true, "Doanh thu / Số hóa đơn", true);

        // NHÓM 2: THỐNG KÊ THANH TOÁN
        GhiDongPhanNhom("II. THỐNG KÊ THANH TOÁN");
        GhiChiTieu("Tiền mặt đã thu", t.TienMat, true, "Thu trực tiếp tiền mặt");
        GhiChiTieu("Chuyển khoản đã thu", t.ChuyenKhoan, true, "Ngân hàng, QR");
        GhiChiTieu("Phương thức khác", t.TienKhac, true, "Ví, thẻ, v.v.");
        GhiChiTieu("Tổng tiền thanh toán", t.TongThanhToan, true, "Tổng các phương thức", true);
        GhiChiTieu("Số hóa đơn đã thanh toán đủ", t.SoHoaDonDaThanhToan, false, "Đã trả 100%");
        GhiChiTieu("Số hóa đơn thanh toán một phần", t.SoHoaDonThanhToanMotPhan, false, "Còn nợ một phần");
        GhiChiTieu("Số hóa đơn chưa thanh toán", t.SoHoaDonChuaThanhToan, false, "Chưa trả tiền");

        // NHÓM 3: THỐNG KÊ CÔNG NỢ & KHÁCH HÀNG
        GhiDongPhanNhom("III. THỐNG KÊ CÔNG NỢ & KHÁCH HÀNG");
        GhiChiTieu("Công nợ phát sinh trong kỳ", t.CongNo, true, "Công nợ hóa đơn mới");
        GhiChiTieu("Số khách hàng phát sinh mua hàng", t.SoKhachHang, false, "Khách hàng có hồ sơ");
        GhiChiTieu("Số khách hàng đang có công nợ", t.SoKhachHangCoCongNo, false, "Khách còn nợ trong kỳ");
        GhiChiTieu("Khách hàng có doanh số cao nhất", string.IsNullOrWhiteSpace(t.KhachHangDoanhThuCaoNhat) ? "Không có" : t.KhachHangDoanhThuCaoNhat, false, "");
        GhiChiTieu("Khách hàng có công nợ cao nhất", string.IsNullOrWhiteSpace(t.KhachHangCongNoCaoNhat) ? "Không có" : t.KhachHangCongNoCaoNhat, false, "");

        // NHÓM 4: THỐNG KÊ HÀNG HÓA BÁN RA
        GhiDongPhanNhom("IV. THỐNG KÊ HÀNG HÓA BÁN RA");
        GhiChiTieu("Tổng số lượng sản phẩm đã bán", t.TongSanPhamBanRa, false, "Số lượng thực tế");
        GhiChiTieu("Sản phẩm có doanh thu cao nhất", string.IsNullOrWhiteSpace(t.SanPhamDoanhThuCaoNhat) ? "Không có" : t.SanPhamDoanhThuCaoNhat, false, "");
        GhiChiTieu("Sản phẩm có số lượng bán nhiều nhất", string.IsNullOrWhiteSpace(t.SanPhamSoLuongNhieuNhat) ? "Không có" : t.SanPhamSoLuongNhieuNhat, false, "");
        GhiChiTieu("Nhóm hàng có doanh thu cao nhất", string.IsNullOrWhiteSpace(t.NhomHangDoanhThuCaoNhat) ? "Không có" : t.NhomHangDoanhThuCaoNhat, false, "");

        ws.Column(1).Width = 42;
        ws.Column(2).Width = 24;
        ws.Column(3).Width = 36;
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
