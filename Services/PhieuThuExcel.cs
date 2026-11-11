using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Xuất chi tiết một giao dịch thanh toán công nợ khách hàng ra Excel (Issue #102).
/// Dùng chung bộ định dạng chuẩn hoá <see cref="ExcelHelper"/> của ShopHoa nên
/// màu sắc, viền, định dạng tiền tệ / ngày giờ đồng bộ với mọi báo cáo khác.
/// </summary>
public static class PhieuThuExcel
{
    /// <summary>Các cột của bảng chi tiết giao dịch.</summary>
    public static string[] TieuDeCot() =>
    [
        "Mã phiếu thu", "Ngày giờ", "Mã khách hàng", "Tên khách hàng",
        "Phương thức thanh toán", "Công nợ trước", "Số tiền thanh toán",
        "Công nợ sau", "Mã chứng từ liên quan", "Người thực hiện", "Ghi chú"
    ];

    /// <summary>
    /// Dựng workbook Excel cho một giao dịch thanh toán công nợ khách hàng.
    /// Số liệu lấy nguyên vẹn từ bản ghi database, không tính lại công nợ.
    /// </summary>
    public static XLWorkbook TaoWorkbook(
        ChiTietThanhToanKhachHang chiTiet,
        string? tenCuaHang = null,
        string? nguoiXuat = null)
    {
        var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("Phiếu thu");

        var dong = ExcelHelper.GhiHeaderBaoCao(
            sheet,
            "CHI TIẾT THANH TOÁN CÔNG NỢ KHÁCH HÀNG",
            $"Mã phiếu thu: {chiTiet.MaPhieuThu} — {chiTiet.ThoiGianText}",
            DateTime.Now,
            tenCuaHang,
            nguoiXuat,
            tongSoCot: TieuDeCot().Length);

        var tieuDe = TieuDeCot();
        ExcelHelper.GhiTieuDeCot(sheet, dong, tieuDe);
        dong++;

        var row = dong;
        ExcelHelper.GhiO_Chu(sheet.Cell(row, 1), chiTiet.MaPhieuThu);
        ExcelHelper.GhiO_NgayGio(sheet.Cell(row, 2), chiTiet.ThoiGian);
        ExcelHelper.GhiO_Chu(sheet.Cell(row, 3), chiTiet.MaKhachHang);
        ExcelHelper.GhiO_Chu(sheet.Cell(row, 4), chiTiet.TenKhachHang);
        ExcelHelper.GhiO_Chu(sheet.Cell(row, 5), chiTiet.PhuongThucHienThi);
        ExcelHelper.GhiO_Tien(sheet.Cell(row, 6), chiTiet.CongNoTruoc);
        ExcelHelper.GhiO_Tien(sheet.Cell(row, 7), chiTiet.SoTien);
        ExcelHelper.GhiO_Tien(sheet.Cell(row, 8), chiTiet.CongNoSau);
        ExcelHelper.GhiO_Chu(sheet.Cell(row, 9), chiTiet.MaChungTuLienQuanText);
        ExcelHelper.GhiO_Chu(sheet.Cell(row, 10), chiTiet.NguoiThucHien);
        ExcelHelper.GhiO_Chu(sheet.Cell(row, 11), chiTiet.GiaoDich.GhiChu ?? "");

        // Nhấn màu tiền thanh toán (xanh) và công nợ còn lại (đỏ khi còn nợ).
        sheet.Cell(row, 7).Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauTichCuc);
        sheet.Cell(row, 7).Style.Font.Bold = true;
        if (!chiTiet.DaTraHet)
        {
            sheet.Cell(row, 8).Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauNguyHiem);
            sheet.Cell(row, 8).Style.Font.Bold = true;
        }

        sheet.Row(row).Height = 22;

        // Phân bổ vào từng chứng từ liên quan (nếu giao dịch gắn nhiều hóa đơn).
        if (chiTiet.ChungTuLienQuans.Count > 0)
        {
            row += 2;
            var oTieuDePhanBo = sheet.Cell(row, 1);
            oTieuDePhanBo.Value = "PHÂN BỔ THEO CHỨNG TỪ LIÊN QUAN";
            oTieuDePhanBo.Style.Font.Bold = true;
            oTieuDePhanBo.Style.Font.FontColor = XLColor.FromHtml(ExcelHelper.MauChuDao);
            row++;

            ExcelHelper.GhiTieuDeCot(sheet, row, "STT", "Loại chứng từ", "Mã chứng từ", "Số tiền phân bổ");
            row++;

            for (var index = 0; index < chiTiet.ChungTuLienQuans.Count; index++)
            {
                var chungTu = chiTiet.ChungTuLienQuans[index];
                ExcelHelper.GhiO_Chu(sheet.Cell(row, 1), index + 1);
                ExcelHelper.GhiO_Chu(sheet.Cell(row, 2), chungTu.LoaiHienThi);
                ExcelHelper.GhiO_Chu(sheet.Cell(row, 3), chungTu.MaChungTu);
                ExcelHelper.GhiO_Tien(sheet.Cell(row, 4), chungTu.SoTienPhanBo);
                row++;
            }

            ExcelHelper.GhiO_Chu(sheet.Cell(row, 2), "Tổng đã phân bổ");
            sheet.Cell(row, 2).Style.Font.Bold = true;
            ExcelHelper.GhiO_Tien(sheet.Cell(row, 4), chiTiet.TongTienPhanBo);
            ExcelHelper.DinhDangDongTongCong(sheet, row, 1, 4);
        }

        ExcelHelper.HoanThienTrang(sheet, dong, row, tieuDe.Length, batAutoFilter: false);
        ExcelHelper.ThietLapTrangIn(sheet);

        return wb;
    }
}
