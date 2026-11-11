using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng bản in phiếu nhập hàng dạng HTML rồi mở bằng trình duyệt mặc định để
/// người dùng in lại (Ctrl+P) hoặc xuất PDF qua trình duyệt headless. Cách này
/// không phụ thuộc driver máy in và chạy được trên cả Windows lẫn Linux.
/// </summary>
public static class PhieuNhapPrintService
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Tên file PDF gợi ý theo mã phiếu nhập, ví dụ: PhieuNhap_PN261111_0001.pdf.</summary>
    public static string TaoTenFilePdf(string maPhieuNhap)
        => $"PhieuNhap_{MaAnToan(maPhieuNhap)}.pdf";

    /// <summary>Ghi phiếu nhập ra file HTML tạm và trả về đường dẫn file.</summary>
    public static string LuuFileTam(PhieuNhapHangDetail detail, ThongTinTiem? tiem = null)
    {
        tiem ??= new ThongTinTiem();
        var ten = MaAnToan(detail.MaPhieuNhap);
        var thuMuc = Path.Combine(Path.GetTempPath(), "ShopHoa", "PhieuNhap");
        Directory.CreateDirectory(thuMuc);

        var duongDan = Path.Combine(thuMuc, $"phieunhap-{ten}.html");

        File.WriteAllText(duongDan, TaoHtml(detail, tiem), new UTF8Encoding(false));

        return duongDan;
    }

    /// <summary>
    /// Xuất phiếu nhập ra PDF bằng trình duyệt headless (Edge/Chrome).
    /// </summary>
    public static async Task<(bool ThanhCong, string? DuongDan, string? Loi)> XuatPdfAsync(
        PhieuNhapHangDetail detail,
        ThongTinTiem? tiem,
        string duongDanPdf)
    {
        tiem ??= new ThongTinTiem();
        var duongDanHtml = LuuFileTam(detail, tiem);
        var filePdfTam = Path.Combine(
            Path.GetTempPath(),
            $"phieunhap-{MaAnToan(detail.MaPhieuNhap)}-{Guid.NewGuid():N}.pdf");

        try
        {
            var trinhDuyet = PhieuThuPrintService.TimTrinhDuyetCoSan();
            if (trinhDuyet is null)
            {
                return (false, null,
                    "Không tìm thấy Microsoft Edge hoặc Google Chrome trên máy. " +
                    "Hãy dùng nút \"In phiếu nhập\" rồi chọn \"Lưu dưới dạng PDF\" " +
                    "trong hộp thoại in của trình duyệt.");
            }

            var thanhCong = await InPdfBangTrinhDuyetAsync(trinhDuyet, duongDanHtml, filePdfTam);
            if (!thanhCong || !LaFilePdf(filePdfTam))
            {
                return (false, null,
                    "Trình duyệt không tạo được file PDF. Hãy dùng nút \"In phiếu nhập\" " +
                    "rồi chọn \"Lưu dưới dạng PDF\" trong hộp thoại in.");
            }

            var thuMucDich = Path.GetDirectoryName(duongDanPdf);
            if (!string.IsNullOrWhiteSpace(thuMucDich))
                Directory.CreateDirectory(thuMucDich);

            File.Move(filePdfTam, duongDanPdf, overwrite: true);
            return (true, duongDanPdf, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
        finally
        {
            TryDelete(filePdfTam);
        }
    }

    private static async Task<bool> InPdfBangTrinhDuyetAsync(
        string trinhDuyet,
        string duongDanHtml,
        string duongDanPdf)
    {
        var url = new Uri(duongDanHtml).AbsoluteUri;
        var thuMucProfile = Path.Combine(Path.GetTempPath(), "ShopHoa", "HeadlessProfile");

        foreach (var cheDoHeadless in new[] { "--headless=new", "--headless" })
        {
            var thanhCong = await ChayLenhInAsync(trinhDuyet, cheDoHeadless, url, duongDanPdf, thuMucProfile);
            if (thanhCong && LaFilePdf(duongDanPdf))
                return true;
        }

        return false;
    }

    private static async Task<bool> ChayLenhInAsync(
        string trinhDuyet,
        string cheDoHeadless,
        string url,
        string duongDanPdf,
        string thuMucProfile)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = trinhDuyet,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = string.Join(' ',
                    cheDoHeadless,
                    "--disable-gpu",
                    "--disable-extensions",
                    "--no-first-run",
                    "--no-default-browser-check",
                    $"--user-data-dir=\"{thuMucProfile}\"",
                    "--no-pdf-header-footer",
                    "--print-to-pdf-no-header",
                    $"--print-to-pdf=\"{duongDanPdf}\"",
                    $"\"{url}\"")
            };

            using var process = Process.Start(psi);
            if (process is null)
                return false;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0 && LaFilePdf(duongDanPdf);
        }
        catch
        {
            return false;
        }
    }

    private static bool LaFilePdf(string duongDan)
    {
        try
        {
            if (!File.Exists(duongDan))
                return false;

            var fi = new FileInfo(duongDan);
            if (fi.Length < 100)
                return false;

            using var fs = File.OpenRead(duongDan);
            Span<byte> header = stackalloc byte[4];
            var read = fs.Read(header);
            return read == 4 &&
                   header[0] == 0x25 && // %
                   header[1] == 0x50 && // P
                   header[2] == 0x44 && // D
                   header[3] == 0x46;   // F
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string duongDan)
    {
        try
        {
            if (File.Exists(duongDan))
                File.Delete(duongDan);
        }
        catch
        {
        }
    }

    /// <summary>Dựng nội dung HTML hoàn chỉnh của phiếu nhập.</summary>
    public static string TaoHtml(PhieuNhapHangDetail detail, ThongTinTiem? tiem = null)
    {
        tiem ??= new ThongTinTiem();
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>Phiếu nhập hàng - ").Append(Enc(detail.MaPhieuNhap)).Append("</title>");
        sb.Append("<style>");
        sb.Append("body{font-family:'Segoe UI',Arial,sans-serif;color:#263B35;margin:0;padding:24px;background:#fff;}");
        sb.Append(".wrap{max-width:560px;margin:0 auto;}");
        sb.Append(".center{text-align:center;}");
        sb.Append("h1{font-size:20px;margin:8px 0 2px;color:#163F38;}");
        sb.Append(".muted{color:#61736E;font-size:12px;}");
        sb.Append("table{width:100%;border-collapse:collapse;margin-top:12px;font-size:13px;}");
        sb.Append("th{text-align:left;border-bottom:1px solid #D8E5E0;padding:6px 4px;color:#163F38;}");
        sb.Append("td{padding:6px 4px;border-bottom:1px solid #EEF3F1;vertical-align:top;}");
        sb.Append(".r{text-align:right;}");
        sb.Append(".sum{margin-top:12px;font-size:13px;}");
        sb.Append(".sum .row{display:flex;justify-content:space-between;padding:3px 0;}");
        sb.Append(".total{font-size:16px;font-weight:bold;color:#087F6E;}");
        sb.Append(".debt{color:#D64545;font-weight:bold;}");
        sb.Append(".note{margin-top:14px;padding:8px;background:#FFF9ED;border:1px solid #F0E0BB;border-radius:6px;font-size:12px;}");
        sb.Append(".thanks{margin-top:18px;text-align:center;font-style:italic;color:#61736E;}");
        sb.Append(".hr{border:0;border-top:1px dashed #C9D8D3;margin:14px 0;}");
        sb.Append("@media print{body{padding:0;} .noprint{display:none;}}");
        sb.Append("</style></head><body><div class=\"wrap\">");

        // Đầu phiếu: thông tin tiệm.
        sb.Append("<div class=\"center\">");
        if (!string.IsNullOrWhiteSpace(tiem.TenTiem))
            sb.Append("<div style=\"font-size:18px;font-weight:bold;color:#163F38;\">")
              .Append(Enc(tiem.TenTiem)).Append("</div>");

        foreach (var dong in new[]
                 {
                     tiem.DiaChiHienThi,
                     tiem.DienThoaiZaloHienThi,
                     tiem.CoFacebook ? tiem.FacebookHienThi : "",
                     tiem.CoThongTinNganHang ? tiem.StkHienThi : "",
                     tiem.Email,
                     tiem.WebsiteHienThi,
                     tiem.MaSoThueHienThi
                 })
        {
            if (!string.IsNullOrWhiteSpace(dong))
                sb.Append("<div class=\"muted\">").Append(Enc(dong)).Append("</div>");
        }

        sb.Append("</div><hr class=\"hr\">");

        sb.Append("<h1 class=\"center\">PHIẾU NHẬP HÀNG</h1>");
        sb.Append("<div class=\"center muted\">Mã phiếu: <b>")
          .Append(Enc(detail.MaPhieuNhap))
          .Append("</b> &bull; Ngày nhập: ")
          .Append(Enc(detail.ThoiGianNhap.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)))
          .Append("</div>");

        sb.Append("<table>");
        DongThongTin(sb, "Mã phiếu nhập", detail.MaPhieuNhap);
        DongThongTin(sb, "Nhà cung cấp", detail.TenNhaCungCapHienThi);
        if (!string.IsNullOrWhiteSpace(detail.MaNhaCungCap))
            DongThongTin(sb, "Mã nhà cung cấp", detail.MaNhaCungCap!);
        DongThongTin(sb, "Nhân viên nhập", detail.TenNhanVienHienThi);
        if (detail.CoMaDatHang)
            DongThongTin(sb, "Mã đặt hàng nhập", detail.MaDatHangNhap!);
        DongThongTin(sb, "Phương thức thanh toán", detail.PhuongThucThanhToanHienThi);
        sb.Append("</table>");

        // Chi tiết hàng hóa.
        sb.Append("<table>");
        sb.Append("<tr><th>STT</th><th>Sản phẩm</th><th>ĐVT</th><th class=\"r\">SL</th><th class=\"r\">Đơn giá</th><th class=\"r\">Thành tiền</th></tr>");

        foreach (var item in detail.Items)
        {
            sb.Append("<tr>");
            sb.Append("<td class=\"r\">").Append(Enc(item.Index.ToString(CultureInfo.InvariantCulture))).Append("</td>");
            sb.Append("<td>").Append(Enc(item.TenSanPham)).Append("</td>");
            sb.Append("<td>").Append(Enc(item.DonViTinhHienThi)).Append("</td>");
            sb.Append("<td class=\"r\">").Append(Enc(item.SoLuongText)).Append("</td>");
            sb.Append("<td class=\"r\">").Append(item.DonGiaNhap.ToString("N0", Vi)).Append("</td>");
            sb.Append("<td class=\"r\">").Append(item.ThanhTien.ToString("N0", Vi)).Append("</td>");
            sb.Append("</tr>");
        }

        sb.Append("</table>");

        // Tổng kết.
        sb.Append("<div class=\"sum\">");
        DongTien(sb, "Tổng tiền hàng", detail.TongTienHang);
        if (detail.GiamGia != 0m)
            DongTien(sb, "Giảm giá", -detail.GiamGia);
        if (detail.ChiPhiKhac != 0m)
            DongTien(sb, "Chi phí khác", detail.ChiPhiKhac);
        DongTien(sb, "TỔNG NHẬP", detail.TongTienNhap, "total");
        if (detail.DaThanhToan > 0m)
            DongTien(sb, "Đã thanh toán", detail.DaThanhToan);
        DongTien(sb, "Công nợ", detail.ConNo, detail.ConNo > 0 ? "debt" : "");
        sb.Append("</div>");

        if (detail.CoGhiChu)
        {
            sb.Append("<div class=\"note\"><b>Ghi chú:</b> ")
              .Append(Enc(detail.GhiChu)).Append("</div>");
        }

        sb.Append("<div class=\"thanks\">").Append(Enc(tiem.LoiCamOnHienThi)).Append("</div>");

        sb.Append("<div class=\"noprint center\" style=\"margin-top:20px;\">");
        sb.Append("<button onclick=\"window.print()\" style=\"padding:10px 24px;border:0;border-radius:8px;background:#087F6E;color:#fff;font-size:14px;cursor:pointer;\">🖨 In phiếu nhập</button>");
        sb.Append("</div>");

        sb.Append("</div></body></html>");

        return sb.ToString();
    }

    private static void DongThongTin(StringBuilder sb, string nhan, string giaTri)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
            return;

        sb.Append("<tr><td style=\"border:0;padding-left:0;width:45%;\"><b>")
          .Append(Enc(nhan)).Append(":</b></td><td style=\"border:0;\">")
          .Append(Enc(giaTri)).Append("</td></tr>");
    }

    private static void DongTien(StringBuilder sb, string nhan, decimal giaTri, string lopCss = "")
    {
        var css = string.IsNullOrWhiteSpace(lopCss) ? "" : $" {lopCss}";
        var tien = giaTri.ToString("N0", Vi);

        sb.Append($"<div class=\"row{css}\"><span>{Enc(nhan)}</span><span>{tien} đ</span></div>");
    }

    private static string MaAnToan(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
            return "phieunhap";

        var sb = new StringBuilder(ma.Length);

        foreach (var c in ma)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                sb.Append(c);
        }

        return sb.Length == 0 ? "phieunhap" : sb.ToString();
    }

    private static string Enc(string? value)
        => string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");
}
