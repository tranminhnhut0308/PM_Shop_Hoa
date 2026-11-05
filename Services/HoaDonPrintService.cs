using System.Globalization;
using System.IO;
using System.Text;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng bản in hóa đơn bán hàng dạng HTML rồi mở bằng trình duyệt mặc định để
/// người dùng in lại (Ctrl+P). Cách này không phụ thuộc driver máy in và chạy
/// được trên cả Windows lẫn Linux, phù hợp để "xuất lại" hóa đơn cũ.
/// <para>
/// Mẫu in chỉ lấy dữ liệu từ hóa đơn và từ <see cref="ThongTinTiem"/> do người
/// dùng cấu hình. Không có tên tiệm / địa chỉ / số tài khoản nào bị viết cứng
/// trong file này.
/// </para>
/// </summary>
public static class HoaDonPrintService
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Ghi hóa đơn ra file HTML tạm và trả về đường dẫn file.</summary>
    public static string LuuFileTam(HoaDonBanHangReceipt receipt)
    {
        var ten = MaAnToan(receipt.MaHoaDon);
        var thuMuc = Path.Combine(Path.GetTempPath(), "ShopHoa", "HoaDon");
        Directory.CreateDirectory(thuMuc);

        var duongDan = Path.Combine(thuMuc, $"hoadon-{ten}.html");

        File.WriteAllText(duongDan, TaoHtml(receipt), new UTF8Encoding(false));

        return duongDan;
    }

    private static void DongKenhLienHe(
        StringBuilder sb,
        IEnumerable<KenhLienHe> channels,
        string position)
    {
        foreach (var channel in channels
                     .Where(x => x.HienThi && x.ViTriHienThi == position
                         && !string.IsNullOrWhiteSpace(x.Ten)
                         && !string.IsNullOrWhiteSpace(x.NoiDung))
                     .OrderBy(x => x.ThuTu))
        {
            sb.Append("<div class=\"store-line\"><b>").Append(Enc(channel.Ten.Trim()))
              .Append(":</b> ").Append(Enc(channel.NoiDung.Trim())).Append("</div>");
        }
    }

    /// <summary>Dựng nội dung HTML hoàn chỉnh của hóa đơn.</summary>
    public static string TaoHtml(HoaDonBanHangReceipt receipt)
    {
        var tiem = receipt.ThongTinTiem ?? new ThongTinTiem();
        var kho = tiem.KhoGiayHienThi;
        var lopKho = kho == ThongTinTiem.KhoGiay58
            ? "k58"
            : kho == ThongTinTiem.KhoGiayA4
                ? "ka4"
                : "k80";

        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>").Append(Enc($"Hóa đơn {receipt.MaHoaDon}")).Append("</title>");
        sb.Append("<style>");
        sb.Append("@page{size:").Append(CssPageSize(kho)).Append(";margin:0;}");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("html,body{margin:0;padding:0;background:#fff;}");
        sb.Append("body{font-family:'Segoe UI','Helvetica Neue',Arial,'Noto Sans',sans-serif;");
        sb.Append("color:#222;font-size:13px;line-height:1.35;");
        sb.Append("-webkit-print-color-adjust:exact;print-color-adjust:exact;}");
        sb.Append(".wrap{width:76mm;max-width:100%;margin:0 auto;padding:4mm 3mm;}");
        sb.Append(".wrap.k58{width:56mm;font-size:11px;padding:3mm 2mm;line-height:1.3;}");
        sb.Append(".wrap.ka4{width:190mm;font-size:13.5px;padding:8mm 6mm;}");
        sb.Append(".center{text-align:center;}.right{text-align:right;}");
        sb.Append(".store{text-align:center;}");
        sb.Append(".logo{display:block;max-height:18mm;max-width:60%;margin:0 auto 2mm;object-fit:contain;}");
        sb.Append(".store-name{font-size:17px;font-weight:700;letter-spacing:.2px;color:#111;text-transform:uppercase;}");
        sb.Append(".k58 .store-name{font-size:14px;}.ka4 .store-name{font-size:20px;}");
        sb.Append(".store-line{font-size:11.5px;color:#333;margin-top:1px;word-wrap:break-word;overflow-wrap:anywhere;}");
        sb.Append(".k58 .store-line{font-size:10px;}");
        sb.Append(".title{text-align:center;font-size:16px;font-weight:700;letter-spacing:1px;margin:2mm 0 1mm;}");
        sb.Append(".k58 .title{font-size:13px;}.ka4 .title{font-size:19px;}");
        sb.Append(".subtitle{text-align:center;font-size:11.5px;color:#555;}");
        sb.Append(".sep{border:0;border-top:1px dashed #444;margin:2.8mm 0;}");
        sb.Append("table{border-collapse:collapse;width:100%;}");
        sb.Append("table.info td{padding:1px 0;vertical-align:top;font-size:12px;}");
        sb.Append(".k58 table.info td{font-size:10.5px;}");
        sb.Append("table.info td.k{width:32%;color:#444;white-space:nowrap;padding-right:2mm;}");
        sb.Append("table.info td.v{font-weight:600;color:#111;word-wrap:break-word;}");
        sb.Append("table.items{table-layout:fixed;font-size:11px;}");
        sb.Append(".k58 table.items{font-size:10px;}");
        sb.Append(".ka4 table.items{font-size:12.5px;}");
        sb.Append("table.items th{font-size:11px;font-weight:700;text-align:left;overflow-wrap:anywhere;");
        sb.Append("border-bottom:1px solid #555;padding:1.4mm .8mm;color:#111;}");
        sb.Append(".k58 table.items th{font-size:9.5px;padding:1mm .4mm;}");
        sb.Append("table.items td{border-bottom:1px solid #e3e3e3;padding:1.4mm .8mm;");
        sb.Append("vertical-align:top;word-wrap:break-word;overflow-wrap:anywhere;}");
        sb.Append(".k58 table.items td{padding:1mm .4mm;}");
        sb.Append("table.items .r{text-align:right;white-space:normal;overflow-wrap:anywhere;word-break:break-word;}");
        sb.Append("table.items .c{text-align:center;}");
        sb.Append(".item-sub{font-size:9.5px;color:#555;margin-top:.4mm;word-wrap:break-word;}");
        sb.Append(".k58 .item-sub{font-size:9px;}");
        sb.Append(".sum{margin-top:1mm;font-size:12.5px;}");
        sb.Append(".k58 .sum{font-size:11px;}.ka4 .sum{font-size:14px;}");
        sb.Append(".sum .row{display:flex;justify-content:space-between;gap:3mm;padding:.7mm 0;}");
        sb.Append(".sum .row .val{white-space:nowrap;font-weight:600;}");
        sb.Append(".sum .row.total{font-size:15px;font-weight:700;color:#0a6b5c;");
        sb.Append("border-top:1px solid #999;margin-top:1mm;padding-top:1.4mm;}");
        sb.Append(".k58 .sum .row.total{font-size:13px;}");
        sb.Append(".sum .row.debt{color:#c0392b;font-weight:700;}");
        sb.Append(".note{margin-top:2mm;padding:1.5mm 2mm;border:1px solid #d8d8d8;");
        sb.Append("border-radius:1.5mm;font-size:11.5px;word-wrap:break-word;}");
        sb.Append(".cancelled{margin:2mm 0;padding:2mm;border:1.5px solid #d64545;");
        sb.Append("border-radius:1.5mm;color:#a32020;font-size:11.5px;}");
        sb.Append(".qr-wrap{margin-top:1mm;}");
        sb.Append(".qr{display:inline-block;width:").Append(tiem.KichThuocQrHienThi).Append("mm;");
        sb.Append("height:").Append(tiem.KichThuocQrHienThi).Append("mm;object-fit:contain;");
        sb.Append("image-rendering:pixelated;}");
        sb.Append(".qr-caption{font-size:10.5px;color:#444;margin-top:.8mm;}");
        sb.Append(".thanks{margin-top:2mm;text-align:center;font-style:italic;color:#333;");
        sb.Append("font-size:12px;word-wrap:break-word;}");
        sb.Append(".footer{margin-top:1mm;text-align:center;font-size:10px;color:#666;}");
        sb.Append(".noprint{text-align:center;margin-top:6mm;}");
        sb.Append(".noprint button{padding:9px 22px;border:0;border-radius:6px;");
        sb.Append("background:#087F6E;color:#fff;font-size:14px;cursor:pointer;}");
        sb.Append("@media print{body{padding:0;}.wrap{padding:3mm 2mm;}.noprint{display:none;}}");
        sb.Append("</style></head><body>");
        sb.Append("<div class=\"wrap ").Append(lopKho).Append("\">");

        // =========================================================
        // NHÓM 1: THÔNG TIN CỬA HÀNG
        // =========================================================
        sb.Append("<div class=\"store\">");

        var logo = tiem.HienThiLogo ? AnhDataUri(tiem.DuongDanLogo) : "";

        if (logo.Length > 0)
            sb.Append("<img class=\"logo\" src=\"").Append(logo).Append("\" alt=\"Logo\">");

        if (!string.IsNullOrWhiteSpace(tiem.TenTiem))
            sb.Append("<div class=\"store-name\">").Append(Enc(tiem.TenTiem.Trim())).Append("</div>");

        DongThongTin(sb, tiem.DiaChiHienThi);
        DongThongTin(sb, tiem.SoDienThoaiHienThi);
        DongKenhLienHe(sb, tiem.KenhLienHes, KenhLienHe.ViTriDau);
        DongThongTin(sb, tiem.MaSoThueHienThi);

        sb.Append("</div>");

        // =========================================================
        // NHÓM 2: THÔNG TIN HÓA ĐƠN
        // =========================================================
        sb.Append("<hr class=\"sep\">");
        sb.Append("<div class=\"title\">HÓA ĐƠN BÁN HÀNG</div>");

        if (receipt.LaHoaDonDaHuy)
        {
            sb.Append("<div class=\"cancelled\">");
            sb.Append("<div><b>HÓA ĐƠN ĐÃ HỦY</b></div>");
            sb.Append("<div><b>Lý do:</b> ").Append(Enc(receipt.LyDoHuy ?? "Không rõ")).Append("</div>");
            sb.Append("<div><b>Người hủy:</b> ").Append(Enc(receipt.TenNguoiHuyHienThi))
              .Append(" — ").Append(Enc(receipt.ThoiGianHuyText)).Append("</div>");
            if (receipt.CoHoaDonThayThe)
                sb.Append("<div><b>Hóa đơn thay thế:</b> ").Append(Enc(receipt.MaHoaDonThayThe)).Append("</div>");
            sb.Append("</div>");
        }

        sb.Append("<table class=\"info\">");
        DongInfo(sb, "Mã hóa đơn", receipt.MaHoaDon);
        DongInfo(sb, "Ngày giờ bán",
            receipt.ThoiGianBan.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(receipt.TenNhanVien))
            DongInfo(sb, "Nhân viên", receipt.TenNhanVien);

        DongInfo(sb, "Khách hàng",
            string.IsNullOrWhiteSpace(receipt.TenKhachHang) ? "Khách lẻ" : receipt.TenKhachHang);

        DongInfo(sb, "SĐT khách", receipt.SoDienThoaiHienThi);

        if (receipt.HienThiDiaChiKhachHang)
            DongInfo(sb, "Địa chỉ", receipt.DiaChiHienThi);

        if (receipt.CoHoaDonGoc)
            DongInfo(sb, "Thay thế HĐ", receipt.MaHoaDonGoc ?? "");

        sb.Append("</table>");

        // =========================================================
        // NHÓM 3: DANH SÁCH SẢN PHẨM
        // =========================================================
        sb.Append("<hr class=\"sep\">");

        var stt = 0;

        if (kho == ThongTinTiem.KhoGiay58)
        {
            // Khổ 58mm quá hẹp cho 6 cột: mỗi sản phẩm in 2 dòng để tên không bị
            // bóp méo và các con số không tràn ra ngoài mép giấy.
            sb.Append("<table class=\"items\">");
            sb.Append("<colgroup><col style=\"width:8%\"><col style=\"width:62%\">");
            sb.Append("<col style=\"width:30%\"></colgroup>");
            sb.Append("<thead><tr>");
            sb.Append("<th class=\"c\">#</th>");
            sb.Append("<th>Tên sản phẩm</th>");
            sb.Append("<th class=\"r\">Thành tiền (đ)</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var item in receipt.Items)
            {
                stt++;
                sb.Append("<tr>");
                sb.Append("<td class=\"c\">").Append(stt).Append("</td>");
                sb.Append("<td>").Append(Enc(item.TenSanPham));
                if (!string.IsNullOrWhiteSpace(item.GhiChu))
                    sb.Append("<div class=\"item-sub\">").Append(Enc(item.GhiChu)).Append("</div>");
                sb.Append("<div class=\"item-sub\">SL: ").Append(SoLuong(item.SoLuong));

                if (!string.IsNullOrWhiteSpace(item.DonViTinh))
                    sb.Append(' ').Append(Enc(item.DonViTinh.Trim()));

                sb.Append(" × ").Append(Tien(item.DonGia)).Append("</div></td>");
                sb.Append("<td class=\"r\">").Append(TienSo(item.ThanhTien)).Append("</td>");
                sb.Append("</tr>");
            }
        }
        else
        {
            sb.Append("<table class=\"items\">");
            sb.Append("<colgroup>");
            sb.Append("<col style=\"width:7%\"><col style=\"width:35%\"><col style=\"width:10%\">");
            sb.Append("<col style=\"width:8%\"><col style=\"width:18%\"><col style=\"width:22%\">");
            sb.Append("</colgroup>");
            sb.Append("<thead><tr>");
            sb.Append("<th class=\"c\">STT</th>");
            sb.Append("<th>Tên sản phẩm</th>");
            sb.Append("<th class=\"c\">ĐVT</th>");
            sb.Append("<th class=\"r\">SL</th>");
            sb.Append("<th class=\"r\">Đơn giá (đ)</th>");
            sb.Append("<th class=\"r\">Thành tiền (đ)</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var item in receipt.Items)
            {
                stt++;
                sb.Append("<tr>");
                sb.Append("<td class=\"c\">").Append(stt).Append("</td>");
                sb.Append("<td>").Append(Enc(item.TenSanPham));
                if (!string.IsNullOrWhiteSpace(item.GhiChu))
                    sb.Append("<div class=\"item-sub\">").Append(Enc(item.GhiChu)).Append("</div>");
                sb.Append("</td>");
                sb.Append("<td class=\"c\">").Append(Enc(item.DonViTinh)).Append("</td>");
                sb.Append("<td class=\"c\">").Append(SoLuong(item.SoLuong)).Append("</td>");
                sb.Append("<td class=\"r\">").Append(TienSo(item.DonGia)).Append("</td>");
                sb.Append("<td class=\"r\">").Append(TienSo(item.ThanhTien)).Append("</td>");
                sb.Append("</tr>");
            }
        }

        sb.Append("</tbody></table>");

        // =========================================================
        // NHÓM 4: TỔNG KẾT THANH TOÁN
        // =========================================================
        sb.Append("<hr class=\"sep\">");
        sb.Append("<div class=\"sum\">");
        DongTien(sb, "Tổng tiền hàng", receipt.TongTienHang);

        if (receipt.GiamGia > 0)
            DongTien(sb, "Giảm giá", -receipt.GiamGia);

        DongTien(sb, "Tổng thanh toán", receipt.KhachCanTra, "total");

        if (receipt.TienKhachDua > 0)
            DongTien(sb, "Tiền khách đưa", receipt.TienKhachDua);

        if (receipt.KhachDaThanhToan > 0 && receipt.KhachDaThanhToan != receipt.TienKhachDua)
            DongTien(sb, "Khách đã trả", receipt.KhachDaThanhToan);

        if (receipt.TienThoi > 0)
            DongTien(sb, "Tiền thừa", receipt.TienThoi);

        if (receipt.ConNo > 0)
            DongTien(sb, "Công nợ còn lại", receipt.ConNo, "debt");

        sb.Append("</div>");

        if (!string.IsNullOrWhiteSpace(receipt.GhiChu))
        {
            sb.Append("<hr class=\"sep\">");
            sb.Append("<div class=\"note\"><b>Ghi chú:</b> ")
              .Append(Enc(receipt.GhiChu)).Append("</div>");
        }

        DongKenhLienHe(sb, tiem.KenhLienHes, KenhLienHe.ViTriCuoi);

        if (tiem.CoThongTinNganHang)
        {
            sb.Append("<div class=\"footer\"><b>Thông tin ngân hàng</b><br>")
              .Append(Enc(tiem.StkHienThi)).Append("</div>");
        }

        // =========================================================
        // NHÓM 5: QR + LỜI CẢM ƠN
        // =========================================================
        sb.Append("<hr class=\"sep\">");

        var qr = tiem.HienThiQr ? AnhDataUri(tiem.DuongDanQr) : "";

        if (qr.Length > 0)
        {
            var canh = tiem.ViTriQrHienThi switch
            {
                ThongTinTiem.ViTriTrai => "left",
                ThongTinTiem.ViTriPhai => "right",
                _ => "center"
            };

            sb.Append("<div class=\"qr-wrap\" style=\"text-align:").Append(canh).Append(";\">");
            sb.Append("<img class=\"qr\" src=\"").Append(qr).Append("\" alt=\"QR Code\">");
            sb.Append("<div class=\"qr-caption\">Quét mã để thanh toán / liên hệ</div>");
            sb.Append("</div>");
        }

        sb.Append("<div class=\"thanks\">").Append(Enc(tiem.LoiCamOnHienThi)).Append("</div>");

        sb.Append("<div class=\"noprint\">");
        sb.Append("<button onclick=\"window.print()\">🖨 In hóa đơn</button>");
        sb.Append("</div>");

        sb.Append("</div></body></html>");

        return sb.ToString();
    }

    private static string CssPageSize(string kho) => kho switch
    {
        ThongTinTiem.KhoGiay58 => "58mm auto",
        ThongTinTiem.KhoGiayA4 => "A4",
        _ => "80mm auto"
    };

    private static void DongThongTin(StringBuilder sb, string? dong)
    {
        if (!string.IsNullOrWhiteSpace(dong))
            sb.Append("<div class=\"store-line\">").Append(Enc(dong.Trim())).Append("</div>");
    }

    private static void DongInfo(StringBuilder sb, string nhan, string? giaTri)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
            return;

        sb.Append("<tr><td class=\"k\">").Append(Enc(nhan)).Append(":</td>");
        sb.Append("<td class=\"v\">").Append(Enc(giaTri.Trim())).Append("</td></tr>");
    }

    private static void DongTien(StringBuilder sb, string nhan, decimal giaTri, string lopCss = "")
    {
        var css = string.IsNullOrWhiteSpace(lopCss) ? "" : $" {lopCss}";

        sb.Append($"<div class=\"row{css}\"><span>{Enc(nhan)}</span>")
          .Append($"<span class=\"val\">{Tien(giaTri)}</span></div>");
    }

    private static string Tien(decimal giaTri) => giaTri.ToString("N0", Vi) + " đ";

    /// <summary>Số tiền không kèm "đ" cho cột bảng (đơn vị đã ghi ở tiêu đề cột).</summary>
    private static string TienSo(decimal giaTri) => giaTri.ToString("N0", Vi);

    private static string SoLuong(decimal soLuong) =>
        soLuong == decimal.Truncate(soLuong)
            ? soLuong.ToString("N0", Vi)
            : soLuong.ToString("N2", Vi);

    /// <summary>
    /// Đọc file ảnh và nhúng thẳng vào HTML dạng data URI. Nhúng thẳng giúp bản
    /// in không phụ thuộc đường dẫn file và QR luôn rõ nét, không bị vỡ khi mở
    /// từ thư mục tạm.
    /// </summary>
    private static string AnhDataUri(string? duongDan)
    {
        if (string.IsNullOrWhiteSpace(duongDan) || !File.Exists(duongDan))
            return "";

        try
        {
            var duoi = Path.GetExtension(duongDan).ToLowerInvariant();

            var mime = duoi switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "image/png"
            };

            var bytes = File.ReadAllBytes(duongDan);

            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }
        catch
        {
            return "";
        }
    }

    private static string MaAnToan(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
            return "hoadon";

        var sb = new StringBuilder(ma.Length);

        foreach (var c in ma)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                sb.Append(c);
        }

        return sb.Length == 0 ? "hoadon" : sb.ToString();
    }

    private static string Enc(string? value)
        => string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");
}
