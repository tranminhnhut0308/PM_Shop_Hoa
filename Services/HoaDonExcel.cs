using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất danh sách hóa đơn bán hàng ra file Excel theo Issue #63:
/// - Hỗ trợ 3 chế độ: Tổng quan, Chi tiết, và Xuất đầy đủ (3 Sheet: Tổng quan + Chi tiết + Tổng hợp doanh thu)
/// - Bảo toàn dữ liệu lịch sử và snapshot mặt hàng
/// - Xử lý hóa đơn đã hủy: hiển thị rõ trạng thái, không cộng dồn vào dòng tổng cộng giao dịch hợp lệ
/// - Chuẩn hóa định dạng ClosedXML, AutoFilter, Freeze Panes, tiền VNĐ, số lượng
/// </summary>
public static class HoaDonExcel
{
    public static XLWorkbook TaoWorkbookMoi(
        IReadOnlyList<HoaDonTongQuanXuatDong> tongQuans,
        IReadOnlyList<HoaDonChiTietXuatDong> chiTiets,
        IReadOnlyList<HoaDonTongHopSanPhamDong> tongHops,
        LoaiBaoCaoXuat loaiBaoCao,
        DateTime thoiDiemXuat,
        string? ghiChuLoc = null,
        string? tenCuaHang = null,
        string? nguoiXuat = null)
    {
        var wb = new XLWorkbook();

        if (loaiBaoCao == LoaiBaoCaoXuat.TongQuan || loaiBaoCao == LoaiBaoCaoXuat.DayDu)
        {
            GhiSheetTongQuan(wb, tongQuans, thoiDiemXuat, ghiChuLoc, tenCuaHang, nguoiXuat);
        }

        if (loaiBaoCao == LoaiBaoCaoXuat.ChiTiet || loaiBaoCao == LoaiBaoCaoXuat.DayDu)
        {
            GhiSheetChiTiet(wb, chiTiets, thoiDiemXuat, ghiChuLoc, tenCuaHang, nguoiXuat);
        }

        if (loaiBaoCao == LoaiBaoCaoXuat.DayDu)
        {
            GhiSheetTongHop(wb, tongHops, thoiDiemXuat, ghiChuLoc, tenCuaHang, nguoiXuat);
        }

        return wb;
    }

    /// <summary>Tương thích ngược với các lời gọi cũ.</summary>
    public static XLWorkbook TaoWorkbook(
        IReadOnlyList<HoaDon> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc = null)
    {
        var list = new List<HoaDonTongQuanXuatDong>();
        for (var i = 0; i < items.Count; i++)
        {
            var h = items[i];
            list.Add(new HoaDonTongQuanXuatDong
            {
                Stt = i + 1,
                Id = h.Id,
                MaHoaDon = h.MaHoaDon,
                NgayLapHoaDon = h.ThoiGianBan,
                MaKhachHang = h.MaKhachHang ?? "",
                TenKhachHang = string.IsNullOrWhiteSpace(h.TenKhachHang) ? "Khách lẻ" : h.TenKhachHang,
                SoDienThoai = "",
                TongSoMatHang = 1,
                TongSoLuongSanPham = 1,
                TongTienHang = h.TongTienHang,
                GiamGia = h.GiamGia,
                TongThanhToan = h.KhachCanTra,
                DaThanhToan = h.KhachDaThanhToan,
                ConNo = h.ConNo,
                PhuongThucThanhToan = "",
                TrangThaiThanhToan = h.TrangThaiHienThi,
                TrangThaiHoaDon = h.LaHoaDonDaHuy ? "Đã hủy" : "Hoàn thành",
                NguoiTao = "",
                GhiChu = h.GhiChu ?? "",
                LaHoaDonDaHuy = h.LaHoaDonDaHuy
            });
        }
        return TaoWorkbookMoi(list, Array.Empty<HoaDonChiTietXuatDong>(), Array.Empty<HoaDonTongHopSanPhamDong>(), LoaiBaoCaoXuat.TongQuan, thoiDiemXuat, ghiChuLoc);
    }

    private static void GhiSheetTongQuan(
        XLWorkbook wb,
        IReadOnlyList<HoaDonTongQuanXuatDong> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng quan hóa đơn");

        string[] tieuDe =
        [
            "STT", "Mã hóa đơn", "Ngày lập HĐ", "Mã khách hàng", "Tên khách hàng", "Số điện thoại",
            "Số mặt hàng", "Tổng số lượng", "Tổng tiền hàng", "Giảm giá", "Tổng thanh toán",
            "Đã thanh toán", "Còn nợ", "Phương thức TT", "Trạng thái TT", "Trạng thái HĐ",
            "Người tạo", "Ghi chú"
        ];

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO TỔNG QUAN HÓA ĐƠN BÁN HÀNG",
            string.IsNullOrWhiteSpace(ghiChuLoc) ? $"Số lượng: {items.Count:N0} hóa đơn" : $"{ghiChuLoc} (Tổng số: {items.Count:N0} hóa đơn)",
            thoiDiemXuat,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: tieuDe.Length);

        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongSL = 0, tongTienHang = 0, tongGiamGia = 0, tongThanhToan = 0, tongDaTra = 0, tongConNo = 0;

        var dong = dongHeader + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var hd = items[i];

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), hd.MaHoaDon, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_NgayGio(ws.Cell(dong, 3), hd.NgayLapHoaDon, coGio: true);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), hd.MaKhachHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), hd.TenKhachHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 6), hd.SoDienThoai, XLAlignmentHorizontalValues.Center);

            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 7), hd.TongSoMatHang);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 8), hd.TongSoLuongSanPham);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), hd.TongTienHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), hd.GiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), hd.TongThanhToan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 12), hd.DaThanhToan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 13), hd.ConNo);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 14), hd.PhuongThucThanhToan, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 15), hd.TrangThaiThanhToan, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 16), hd.TrangThaiHoaDon, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 17), hd.NguoiTao);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 18), hd.GhiChu);

            if (hd.LaHoaDonDaHuy)
            {
                // Tô màu cảnh báo hóa đơn đã hủy
                ws.Range(dong, 1, dong, tieuDe.Length).Style.Font.FontColor = XLColor.FromHtml("#9CA3AF");
                ws.Range(dong, 1, dong, tieuDe.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
            }
            else
            {
                tongSL += hd.TongSoLuongSanPham;
                tongTienHang += hd.TongTienHang;
                tongGiamGia += hd.GiamGia;
                tongThanhToan += hd.TongThanhToan;
                tongDaTra += hd.DaThanhToan;
                tongConNo += hd.ConNo;
            }

            dong++;
        }

        // Dòng tổng cộng (chỉ cộng các hóa đơn hợp lệ chưa hủy)
        ws.Cell(dong, 1).Value = "";
        ws.Cell(dong, 5).Value = "TỔNG CỘNG (HỢP LỆ)";
        ws.Cell(dong, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 8), tongSL);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), tongTienHang);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), tongGiamGia);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), tongThanhToan);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 12), tongDaTra);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 13), tongConNo);
        ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: items.Count > 0);
    }

    private static void GhiSheetChiTiet(
        XLWorkbook wb,
        IReadOnlyList<HoaDonChiTietXuatDong> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Chi tiết mặt hàng");

        string[] tieuDe =
        [
            "STT", "Mã hóa đơn", "Ngày lập HĐ", "Mã khách hàng", "Tên khách hàng",
            "Mã hàng", "Tên hàng", "Nhóm hàng", "Đơn vị tính", "Số lượng", "Đơn giá bán",
            "Giảm giá", "Thành tiền", "Ghi chú sản phẩm"
        ];

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CHI TIẾT MẶT HÀNG BÁN THEO HÓA ĐƠN",
            string.IsNullOrWhiteSpace(ghiChuLoc) ? $"Số lượng: {items.Count:N0} dòng hàng" : $"{ghiChuLoc} (Tổng số: {items.Count:N0} dòng hàng)",
            thoiDiemXuat,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: tieuDe.Length);

        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongSL = 0, tongGiamGia = 0, tongThanhTien = 0;

        var dong = dongHeader + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var r = items[i];

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.MaHoaDon, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_NgayGio(ws.Cell(dong, 3), r.NgayLapHoaDon, coGio: true);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), r.MaKhachHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), r.TenKhachHang);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 6), r.MaHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 7), r.TenHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 8), r.NhomHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 9), r.DonViTinh, XLAlignmentHorizontalValues.Center);

            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 10), r.SoLuong);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), r.DonGiaBan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 12), r.GiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 13), r.ThanhTien);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 14), r.GhiChu);

            if (r.LaHoaDonDaHuy)
            {
                ws.Range(dong, 1, dong, tieuDe.Length).Style.Font.FontColor = XLColor.FromHtml("#9CA3AF");
                ws.Range(dong, 1, dong, tieuDe.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
            }
            else
            {
                tongSL += r.SoLuong;
                tongGiamGia += r.GiamGia;
                tongThanhTien += r.ThanhTien;
            }

            dong++;
        }

        // Dòng tổng cộng
        if (items.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 7).Value = "TỔNG CỘNG (HỢP LỆ)";
            ws.Cell(dong, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 10), tongSL);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 12), tongGiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 13), tongThanhTien);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, Math.Max(dong, dongHeader + 1), tieuDe.Length, batAutoFilter: items.Count > 0);
    }

    private static void GhiSheetTongHop(
        XLWorkbook wb,
        IReadOnlyList<HoaDonTongHopSanPhamDong> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng hợp theo sản phẩm");

        string[] tieuDe =
        [
            "STT", "Mã hàng", "Tên hàng hóa", "Nhóm hàng", "Đơn vị tính",
            "Tổng số lượng bán", "Doanh thu", "Doanh thu sau giảm giá"
        ];

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO TỔNG HỢP DOANH THU THEO SẢN PHẨM",
            string.IsNullOrWhiteSpace(ghiChuLoc) ? $"Số lượng: {items.Count:N0} mặt hàng" : $"{ghiChuLoc} (Tổng số: {items.Count:N0} mặt hàng)",
            thoiDiemXuat,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: tieuDe.Length);

        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongSL = 0, tongDoanhThu = 0, tongDoanhThuSauGiam = 0;

        var dong = dongHeader + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var r = items[i];

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.MaHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), r.TenHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), r.NhomHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), r.DonViTinh, XLAlignmentHorizontalValues.Center);

            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 6), r.TongSoLuongBan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), r.DoanhThu);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), r.DoanhThuSauGiamGia);

            tongSL += r.TongSoLuongBan;
            tongDoanhThu += r.DoanhThu;
            tongDoanhThuSauGiam += r.DoanhThuSauGiamGia;

            dong++;
        }

        // Dòng tổng cộng
        if (items.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 3).Value = "TỔNG CỘNG";
            ws.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 6), tongSL);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), tongDoanhThu);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), tongDoanhThuSauGiam);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, Math.Max(dong, dongHeader + 1), tieuDe.Length, batAutoFilter: items.Count > 0);
    }
}
