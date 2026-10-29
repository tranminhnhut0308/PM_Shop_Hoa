using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất danh sách phiếu nhập hàng ra file Excel với đầy đủ 2 worksheet:
/// - Worksheet 1: Tổng quan phiếu nhập
/// - Worksheet 2: Chi tiết từng mặt hàng nhập
/// Tuân thủ định dạng chuẩn, AutoFilter, Freeze Panes, không trùng lặp, cộng dồn chính xác.
/// </summary>
public static class BaoCaoPhieuNhapExcel
{
    public static XLWorkbook TaoWorkbook(
        IReadOnlyList<PhieuNhapHang> phieuNhaps,
        IReadOnlyList<PhieuNhapChiTietDong>? chiTiets,
        DateTime thoiDiemXuat,
        string? ghiChuLoc = null,
        string? tenCuaHang = null,
        string? nguoiXuat = null)
    {
        var wb = new XLWorkbook();

        GhiSheetTongQuan(wb, phieuNhaps, thoiDiemXuat, ghiChuLoc, tenCuaHang, nguoiXuat);
        GhiSheetChiTiet(wb, chiTiets ?? Array.Empty<PhieuNhapChiTietDong>(), thoiDiemXuat, ghiChuLoc, tenCuaHang, nguoiXuat);

        return wb;
    }

    /// <summary>Tương thích ngược với lời gọi cũ chỉ truyền danh sách phiếu nhập.</summary>
    public static XLWorkbook TaoWorkbook(
        IReadOnlyList<PhieuNhapHang> items,
        DateTime thoiDiemXuat)
    {
        return TaoWorkbook(items, null, thoiDiemXuat);
    }

    private static void GhiSheetTongQuan(
        XLWorkbook wb,
        IReadOnlyList<PhieuNhapHang> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng quan phiếu nhập");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO PHIẾU NHẬP HÀNG",
            string.IsNullOrWhiteSpace(ghiChuLoc) ? $"Số lượng: {items.Count:N0} phiếu nhập" : $"{ghiChuLoc} (Tổng số: {items.Count:N0} phiếu nhập)",
            thoiDiemXuat,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 14);

        string[] tieuDe =
        [
            "STT", "Mã phiếu nhập", "Thời gian nhập", "Mã NCC", "Tên nhà cung cấp", "Người tạo",
            "Phương thức", "Trạng thái", "Tiền hàng", "Chi phí khác / Giảm trừ", "Tổng tiền nhập",
            "Đã thanh toán", "Còn nợ", "Ghi chú"
        ];

        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongTienHang = 0, tongChiPhi = 0, tongTienNhap = 0, tongDaTra = 0, tongConNo = 0;

        var dong = dongHeader + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var p = items[i];

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), p.MaPhieuNhap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_NgayGio(ws.Cell(dong, 3), p.ThoiGianNhap, coGio: true);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), p.MaNhaCungCap ?? "", XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), p.TenNhaCungCap ?? "");
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 6), p.TenNhanVien ?? "");

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 7), p.PhuongThucThanhToanHienThi, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 8), p.TrangThaiHienThi, XLAlignmentHorizontalValues.Center);

            ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), p.TongTienHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), p.ChiPhiKhac);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), p.TongTienNhap);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 12), p.DaThanhToan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 13), p.ConNo);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 14), p.GhiChu ?? "");

            tongTienHang += p.TongTienHang;
            tongChiPhi += p.ChiPhiKhac;
            tongTienNhap += p.TongTienNhap;
            tongDaTra += p.DaThanhToan;
            tongConNo += p.ConNo;

            dong++;
        }

        // Dòng tổng cộng
        ws.Cell(dong, 1).Value = "";
        ws.Cell(dong, 6).Value = "TỔNG CỘNG";
        ws.Cell(dong, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 9), tongTienHang);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), tongChiPhi);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), tongTienNhap);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 12), tongDaTra);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 13), tongConNo);
        ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: items.Count > 0);
    }

    private static void GhiSheetChiTiet(
        XLWorkbook wb,
        IReadOnlyList<PhieuNhapChiTietDong> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Chi tiết mặt hàng");

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CHI TIẾT MẶT HÀNG NHẬP THEO PHIẾU",
            string.IsNullOrWhiteSpace(ghiChuLoc) ? $"Số lượng: {items.Count:N0} dòng hàng" : $"{ghiChuLoc} (Tổng số: {items.Count:N0} dòng hàng)",
            thoiDiemXuat,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: 12);

        string[] tieuDe =
        [
            "STT", "Mã phiếu nhập", "Ngày nhập", "Mã NCC", "Tên nhà cung cấp",
            "Mã hàng", "Tên hàng", "Đơn vị tính", "Số lượng nhập", "Đơn giá nhập", "Giảm giá", "Thành tiền", "Ghi chú"
        ];

        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongSoLuong = 0, tongGiamGia = 0, tongThanhTien = 0;

        var dong = dongHeader + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var r = items[i];

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.MaPhieuNhap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_NgayGio(ws.Cell(dong, 3), r.ThoiGianNhap, coGio: true);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), r.MaNhaCungCap ?? "", XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), r.TenNhaCungCap ?? "");

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 6), r.MaSanPham ?? "", XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 7), r.TenSanPham);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 8), r.DonViTinh ?? "", XLAlignmentHorizontalValues.Center);

            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 9), r.SoLuong);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 10), r.DonGiaNhap);
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
