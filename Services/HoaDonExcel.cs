using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất danh sách hóa đơn ra file Excel với đầy đủ 2 worksheet:
/// - Worksheet 1: Tổng quan hóa đơn
/// - Worksheet 2: Chi tiết mặt hàng từng hóa đơn
/// Tuân thủ định dạng chuẩn, AutoFilter, Freeze Panes, không tràn số, không trùng lặp.
/// </summary>
public static class HoaDonExcel
{
    public static XLWorkbook TaoWorkbook(
        IReadOnlyList<HoaDon> hoaDons,
        IReadOnlyList<HoaDonChiTietDong>? chiTiets,
        DateTime thoiDiemXuat,
        string? ghiChuLoc = null,
        string? tenCuaHang = null,
        string? nguoiXuat = null)
    {
        var wb = new XLWorkbook();

        GhiSheetTongQuan(wb, hoaDons, thoiDiemXuat, ghiChuLoc, tenCuaHang, nguoiXuat);
        GhiSheetChiTiet(wb, chiTiets ?? Array.Empty<HoaDonChiTietDong>(), thoiDiemXuat, ghiChuLoc, tenCuaHang, nguoiXuat);

        return wb;
    }

    /// <summary>Tương thích ngược với lời gọi cũ chỉ truyền danh sách hóa đơn.</summary>
    public static XLWorkbook TaoWorkbook(
        IReadOnlyList<HoaDon> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc = null)
    {
        return TaoWorkbook(items, null, thoiDiemXuat, ghiChuLoc);
    }

    private static void GhiSheetTongQuan(
        XLWorkbook wb,
        IReadOnlyList<HoaDon> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng quan hóa đơn");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "DANH SÁCH HÓA ĐƠN BÁN HÀNG",
            string.IsNullOrWhiteSpace(ghiChuLoc) ? $"Số lượng: {items.Count:N0} hóa đơn" : $"{ghiChuLoc} (Tổng số: {items.Count:N0} hóa đơn)",
            thoiDiemXuat,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 12);

        string[] tieuDe =
        [
            "STT", "Mã hóa đơn", "Thời gian", "Mã khách hàng", "Tên khách hàng",
            "Tổng tiền hàng", "Giảm giá", "Thu khác", "Tổng thanh toán", "Đã thanh toán", "Còn nợ",
            "Trạng thái", "Ghi chú"
        ];

        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongTienHang = 0, tongGiamGia = 0, tongThuKhac = 0, tongCanTra = 0, tongDaThanhToan = 0, tongConNo = 0;

        var dong = dongHeader + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var hd = items[i];

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), hd.MaHoaDon, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_NgayGio(ws.Cell(dong, 3), hd.ThoiGianBan, coGio: true);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), hd.MaKhachHang ?? "", XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), string.IsNullOrWhiteSpace(hd.TenKhachHang) ? "Khách lẻ" : hd.TenKhachHang);

            ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), hd.TongTienHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), hd.GiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), hd.ThuKhac);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), hd.KhachCanTra);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), hd.KhachDaThanhToan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), hd.ConNo);

            var trangThai = hd.LaHoaDonDaHuy
                ? "Đã hủy"
                : (hd.LaHoaDonTongHop ? "Tổng hợp" : (hd.ConNo <= 0 ? "Hoàn thành" : (hd.KhachDaThanhToan > 0 ? "Thanh toán một phần" : "Chưa thanh toán")));
            var ghiChuXuat = hd.LaHoaDonDaHuy && !string.IsNullOrWhiteSpace(hd.ThongTinHuyTomTat)
                ? (string.IsNullOrWhiteSpace(hd.GhiChu) ? hd.ThongTinHuyTomTat : $"{hd.GhiChu} | {hd.ThongTinHuyTomTat}")
                : (hd.GhiChu ?? "");
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 12), trangThai, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 13), ghiChuXuat);

            if (!hd.LaHoaDonDaHuy)
            {
                tongTienHang += hd.TongTienHang;
                tongGiamGia += hd.GiamGia;
                tongThuKhac += hd.ThuKhac;
                tongCanTra += hd.KhachCanTra;
                tongDaThanhToan += hd.KhachDaThanhToan;
                tongConNo += hd.ConNo;
            }

            dong++;
        }

        // Dòng tổng cộng
        ws.Cell(dong, 1).Value = "";
        ws.Cell(dong, 5).Value = "TỔNG CỘNG";
        ws.Cell(dong, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 6), tongTienHang);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), tongGiamGia);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 8), tongThuKhac);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), tongCanTra);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), tongDaThanhToan);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), tongConNo);
        ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: items.Count > 0);
    }

    private static void GhiSheetChiTiet(
        XLWorkbook wb,
        IReadOnlyList<HoaDonChiTietDong> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Chi tiết mặt hàng");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CHI TIẾT MẶT HÀNG BÁN THEO HÓA ĐƠN",
            string.IsNullOrWhiteSpace(ghiChuLoc) ? $"Số lượng: {items.Count:N0} dòng hàng" : $"{ghiChuLoc} (Tổng số: {items.Count:N0} dòng hàng)",
            thoiDiemXuat,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 12);

        string[] tieuDe =
        [
            "STT", "Mã hóa đơn", "Ngày lập", "Mã khách hàng", "Tên khách hàng",
            "Mã hàng", "Tên hàng", "Đơn vị tính", "Số lượng", "Đơn giá", "Giảm giá", "Thành tiền", "Ghi chú"
        ];

        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongSoLuong = 0, tongGiamGia = 0, tongThanhTien = 0;

        var dong = dongHeader + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var r = items[i];

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.MaHoaDon, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_NgayGio(ws.Cell(dong, 3), r.ThoiGianBan, coGio: true);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), r.MaKhachHang ?? "", XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), string.IsNullOrWhiteSpace(r.TenKhachHang) ? "Khách lẻ" : r.TenKhachHang);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 6), r.MaSanPham ?? "", XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 7), r.TenSanPham);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 8), r.DonViTinh ?? "", XLAlignmentHorizontalValues.Center);

            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 9), r.SoLuong);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), r.DonGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), r.GiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 12), r.ThanhTien);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 13), r.GhiChu ?? "");

            tongSoLuong += r.SoLuong;
            tongGiamGia += r.GiamGia;
            tongThanhTien += r.ThanhTien;

            dong++;
        }

        // Dòng tổng cộng
        if (items.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 7).Value = "TỔNG CỘNG";
            ws.Cell(dong, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 9), tongSoLuong);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), tongGiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 12), tongThanhTien);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, Math.Max(dong, dongHeader + 1), tieuDe.Length, batAutoFilter: items.Count > 0);
    }
}
