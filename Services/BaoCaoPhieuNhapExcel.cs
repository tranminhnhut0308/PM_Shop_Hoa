using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất danh sách phiếu nhập hàng ra file Excel theo Issue #63:
/// - Hỗ trợ 3 chế độ: Tổng quan, Chi tiết, và Xuất đầy đủ (3 Sheet: Tổng quan + Chi tiết + Tổng hợp nhập hàng)
/// - Bảo toàn dữ liệu lịch sử snapshot đơn vị tính và hệ số quy đổi tại thời điểm nhập
/// - Xử lý phiếu đã hủy: hiển thị rõ trạng thái, không cộng dồn vào dòng tổng cộng giao dịch hợp lệ
/// - Chuẩn hóa định dạng ClosedXML, AutoFilter, Freeze Panes, tiền VNĐ, số lượng
/// </summary>
public static class BaoCaoPhieuNhapExcel
{
    public static XLWorkbook TaoWorkbookMoi(
        IReadOnlyList<PhieuNhapTongQuanXuatDong> tongQuans,
        IReadOnlyList<PhieuNhapChiTietXuatDong> chiTiets,
        IReadOnlyList<PhieuNhapTongHopSanPhamDong> tongHops,
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
        IReadOnlyList<PhieuNhapHang> items,
        DateTime thoiDiemXuat)
    {
        var list = new List<PhieuNhapTongQuanXuatDong>();
        for (var i = 0; i < items.Count; i++)
        {
            var p = items[i];
            list.Add(new PhieuNhapTongQuanXuatDong
            {
                Stt = i + 1,
                Id = p.Id,
                MaPhieuNhap = p.MaPhieuNhap,
                NgayNhapHang = p.ThoiGianNhap,
                NgayHoaDonDauVao = p.NgayHoaDonDauVao,
                SoHoaDonDauVao = p.SoHoaDonDauVao ?? "",
                MaNhaCungCap = p.MaNhaCungCap ?? "",
                TenNhaCungCap = p.TenNhaCungCap ?? "",
                SoDienThoai = p.SdtNguoiGiaoHang ?? "",
                TongSoMatHang = 1,
                TongSoLuongNhap = 1,
                TongTienHang = p.TongTienHang,
                GiamGia = p.GiamGia,
                ChiPhiKhac = p.ChiPhiKhac,
                TongThanhToan = p.TongTienNhap,
                DaThanhToan = p.DaThanhToan,
                ConNo = p.ConNo,
                PhuongThucThanhToan = p.PhuongThucThanhToanHienThi,
                TrangThaiThanhToan = p.TrangThaiHienThi,
                TrangThaiPhieuNhap = p.TrangThaiHienThi,
                NguoiTao = p.TenNhanVien ?? "",
                GhiChu = p.GhiChu ?? "",
                LaPhieuDaHuy = string.Equals(p.TrangThai, "HUY", StringComparison.OrdinalIgnoreCase) || string.Equals(p.TrangThai, "DA_HUY", StringComparison.OrdinalIgnoreCase)
            });
        }
        return TaoWorkbookMoi(list, Array.Empty<PhieuNhapChiTietXuatDong>(), Array.Empty<PhieuNhapTongHopSanPhamDong>(), LoaiBaoCaoXuat.TongQuan, thoiDiemXuat);
    }

    private static void GhiSheetTongQuan(
        XLWorkbook wb,
        IReadOnlyList<PhieuNhapTongQuanXuatDong> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng quan phiếu nhập");

        string[] tieuDe =
        [
            "STT", "Mã phiếu nhập", "Ngày nhập hàng", "Ngày HĐ đầu vào", "Số HĐ đầu vào",
            "Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Số mặt hàng", "Tổng số lượng nhập",
            "Tổng tiền hàng", "Giảm giá", "Chi phí khác", "Tổng thanh toán", "Đã thanh toán", "Còn nợ",
            "Phương thức TT", "Trạng thái TT", "Trạng thái phiếu", "Người tạo", "Ghi chú"
        ];

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO TỔNG QUAN PHIẾU NHẬP HÀNG",
            string.IsNullOrWhiteSpace(ghiChuLoc) ? $"Số lượng: {items.Count:N0} phiếu nhập" : $"{ghiChuLoc} (Tổng số: {items.Count:N0} phiếu nhập)",
            thoiDiemXuat,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: tieuDe.Length);

        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongSL = 0, tongTienHang = 0, tongGiamGia = 0, tongChiPhi = 0, tongThanhToan = 0, tongDaTra = 0, tongConNo = 0;

        var dong = dongHeader + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var p = items[i];

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), p.MaPhieuNhap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_NgayGio(ws.Cell(dong, 3), p.NgayNhapHang, coGio: true);
            if (p.NgayHoaDonDauVao.HasValue)
                ExcelHelper.GhiO_NgayGio(ws.Cell(dong, 4), p.NgayHoaDonDauVao.Value, coGio: false);
            else
                ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), "—", XLAlignmentHorizontalValues.Center);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), p.SoHoaDonDauVao, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 6), p.MaNhaCungCap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 7), p.TenNhaCungCap);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 8), p.SoDienThoai, XLAlignmentHorizontalValues.Center);

            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 9), p.TongSoMatHang);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 10), p.TongSoLuongNhap);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), p.TongTienHang);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 12), p.GiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 13), p.ChiPhiKhac);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 14), p.TongThanhToan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 15), p.DaThanhToan);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 16), p.ConNo);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 17), p.PhuongThucThanhToan, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 18), p.TrangThaiThanhToan, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 19), p.TrangThaiPhieuNhap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 20), p.NguoiTao);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 21), p.GhiChu);

            if (p.LaPhieuDaHuy)
            {
                ws.Range(dong, 1, dong, tieuDe.Length).Style.Font.FontColor = XLColor.FromHtml("#9CA3AF");
                ws.Range(dong, 1, dong, tieuDe.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
            }
            else
            {
                tongSL += p.TongSoLuongNhap;
                tongTienHang += p.TongTienHang;
                tongGiamGia += p.GiamGia;
                tongChiPhi += p.ChiPhiKhac;
                tongThanhToan += p.TongThanhToan;
                tongDaTra += p.DaThanhToan;
                tongConNo += p.ConNo;
            }

            dong++;
        }

        // Dòng tổng cộng (chỉ cộng phiếu hợp lệ chưa hủy)
        ws.Cell(dong, 1).Value = "";
        ws.Cell(dong, 7).Value = "TỔNG CỘNG (HỢP LỆ)";
        ws.Cell(dong, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 10), tongSL);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 11), tongTienHang);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 12), tongGiamGia);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 13), tongChiPhi);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 14), tongThanhToan);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 15), tongDaTra);
        ExcelHelper.GhiO_Tien(ws.Cell(dong, 16), tongConNo);
        ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);

        ExcelHelper.HoanThienTrang(ws, dongHeader, dong, tieuDe.Length, batAutoFilter: items.Count > 0);
    }

    private static void GhiSheetChiTiet(
        XLWorkbook wb,
        IReadOnlyList<PhieuNhapChiTietXuatDong> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Chi tiết mặt hàng");

        string[] tieuDe =
        [
            "STT", "Mã phiếu nhập", "Ngày nhập hàng", "Mã NCC", "Tên nhà cung cấp",
            "Mã hàng", "Tên hàng", "Nhóm hàng", "ĐVT nhập", "Hệ số quy đổi",
            "Số lượng nhập", "Số lượng quy đổi", "Đơn giá nhập", "Giảm giá", "Thành tiền"
        ];

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "CHI TIẾT MẶT HÀNG NHẬP THEO PHIẾU",
            string.IsNullOrWhiteSpace(ghiChuLoc) ? $"Số lượng: {items.Count:N0} dòng hàng" : $"{ghiChuLoc} (Tổng số: {items.Count:N0} dòng hàng)",
            thoiDiemXuat,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: tieuDe.Length);

        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongSLNhap = 0, tongSLQuyDoi = 0, tongGiamGia = 0, tongThanhTien = 0;

        var dong = dongHeader + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var r = items[i];

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.MaPhieuNhap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_NgayGio(ws.Cell(dong, 3), r.NgayNhapHang, coGio: true);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), r.MaNhaCungCap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), r.TenNhaCungCap);

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 6), r.MaHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 7), r.TenHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 8), r.NhomHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 9), r.DonViTinhNhap, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 10), r.HeSoQuyDoi);

            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 11), r.SoLuongNhap);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 12), r.SoLuongQuyDoi);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 13), r.DonGiaNhap);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 14), r.GiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 15), r.ThanhTien);

            if (r.LaPhieuDaHuy)
            {
                ws.Range(dong, 1, dong, tieuDe.Length).Style.Font.FontColor = XLColor.FromHtml("#9CA3AF");
                ws.Range(dong, 1, dong, tieuDe.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
            }
            else
            {
                tongSLNhap += r.SoLuongNhap;
                tongSLQuyDoi += r.SoLuongQuyDoi;
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
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 11), tongSLNhap);
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 12), tongSLQuyDoi);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 14), tongGiamGia);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 15), tongThanhTien);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, Math.Max(dong, dongHeader + 1), tieuDe.Length, batAutoFilter: items.Count > 0);
    }

    private static void GhiSheetTongHop(
        XLWorkbook wb,
        IReadOnlyList<PhieuNhapTongHopSanPhamDong> items,
        DateTime thoiDiemXuat,
        string? ghiChuLoc,
        string? tenCuaHang,
        string? nguoiXuat)
    {
        var ws = wb.Worksheets.Add("Tổng hợp theo sản phẩm");

        string[] tieuDe =
        [
            "STT", "Mã hàng", "Tên hàng hóa", "Nhóm hàng", "Đơn vị tính",
            "Tổng số lượng nhập", "Tổng tiền nhập"
        ];

        var dongHeader = ExcelHelper.GhiHeaderBaoCao(
            ws,
            "BÁO CÁO TỔNG HỢP NHẬP HÀNG THEO SẢN PHẨM",
            string.IsNullOrWhiteSpace(ghiChuLoc) ? $"Số lượng: {items.Count:N0} mặt hàng" : $"{ghiChuLoc} (Tổng số: {items.Count:N0} mặt hàng)",
            thoiDiemXuat,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: tieuDe.Length);

        ExcelHelper.GhiTieuDeCot(ws, dongHeader, tieuDe);

        decimal tongSL = 0, tongTien = 0;

        var dong = dongHeader + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var r = items[i];

            ExcelHelper.GhiO_Chu(ws.Cell(dong, 1), i + 1, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 2), r.MaHang, XLAlignmentHorizontalValues.Center);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 3), r.TenHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 4), r.NhomHang);
            ExcelHelper.GhiO_Chu(ws.Cell(dong, 5), r.DonViTinh, XLAlignmentHorizontalValues.Center);

            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 6), r.TongSoLuongNhap);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), r.TongTienNhap);

            tongSL += r.TongSoLuongNhap;
            tongTien += r.TongTienNhap;

            dong++;
        }

        // Dòng tổng cộng
        if (items.Count > 0)
        {
            ws.Cell(dong, 1).Value = "";
            ws.Cell(dong, 3).Value = "TỔNG CỘNG";
            ws.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ExcelHelper.GhiO_SoLuong(ws.Cell(dong, 6), tongSL);
            ExcelHelper.GhiO_Tien(ws.Cell(dong, 7), tongTien);
            ExcelHelper.DinhDangDongTongCong(ws, dong, 1, tieuDe.Length);
        }

        ExcelHelper.HoanThienTrang(ws, dongHeader, Math.Max(dong, dongHeader + 1), tieuDe.Length, batAutoFilter: items.Count > 0);
    }
}
