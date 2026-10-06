using ClosedXML.Excel;
using ShopHoa.Models;

namespace ShopHoa.Services;

public static class BaoCaoBanHangExcel
{
    public static XLWorkbook TaoWorkbook(BaoCaoBanHangDuLieu duLieu)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Tổng hợp");
        ws.Cell(1,1).Value = "Báo cáo bán hàng";
        ws.Cell(2,1).Value = $"Từ {duLieu.TuNgay:dd/MM/yyyy} đến {duLieu.DenNgay:dd/MM/yyyy}";
        ws.Cell(4,1).Value = "Số hóa đơn"; ws.Cell(4,2).Value = duLieu.TongHop.SoHoaDon;
        ws.Cell(5,1).Value = "Tổng tiền hàng"; ws.Cell(5,2).Value = duLieu.TongHop.TongTienHang;
        ws.Cell(6,1).Value = "Giảm giá"; ws.Cell(6,2).Value = duLieu.TongHop.GiamGia;
        ws.Cell(7,1).Value = "Doanh thu"; ws.Cell(7,2).Value = duLieu.TongHop.DoanhThu;
        ws.Cell(8,1).Value = "Đã thu"; ws.Cell(8,2).Value = duLieu.TongHop.DaThu;
        ws.Cell(9,1).Value = "Công nợ"; ws.Cell(9,2).Value = duLieu.TongHop.CongNo;
        ws.Cell(10,1).Value = "Giá trị đơn trung bình"; ws.Cell(10,2).Value = duLieu.TongHop.GiaTriDonTrungBinh;
        ws.Column(2).Style.NumberFormat.Format = "#,##0";
        return wb;
    }
}
