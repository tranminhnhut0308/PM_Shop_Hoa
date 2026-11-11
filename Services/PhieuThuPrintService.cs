using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng mẫu in PHIẾU THU / BIÊN NHẬN THANH TOÁN CÔNG NỢ KHÁCH HÀNG dạng HTML rồi
/// mở bằng trình duyệt mặc định để người dùng in (Ctrl+P) — tái sử dụng đúng cơ
/// chế in của ShopHoa (xem <see cref="HoaDonPrintService"/>,
/// <see cref="PhieuNhapPrintService"/>). Hỗ trợ khổ giấy 58mm / 80mm (K80) / A4
/// theo cấu hình "Cài đặt hóa đơn in".
/// <para>
/// Xuất PDF tái dùng cùng bản HTML: gọi trình duyệt Chromium (Edge/Chrome) ở
/// chế độ headless với <c>--print-to-pdf</c> nên tiếng Việt hiển thị đầy đủ bằng
/// font hệ thống, không cần thư viện PDF ngoài.
/// </para>
/// <para>
/// Mẫu in chỉ đọc dữ liệu đã lưu của giao dịch (sổ quỹ / lịch sử thanh toán),
/// không ghi lại hay thay đổi bất kỳ số tiền nào.
/// </para>
/// </summary>
public static class PhieuThuPrintService
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Ghi phiếu thu ra file HTML tạm và trả về đường dẫn file.</summary>
    public static string LuuFileTam(ChiTietThanhToanKhachHang chiTiet)
    {
        var ten = MaAnToan(chiTiet.MaPhieuThu);
        var thuMuc = Path.Combine(Path.GetTempPath(), "ShopHoa", "PhieuThu");
        Directory.CreateDirectory(thuMuc);

        var duongDan = Path.Combine(thuMuc, $"phieuthu-{ten}.html");
        File.WriteAllText(duongDan, TaoHtml(chiTiet), new UTF8Encoding(false));
        return duongDan;
    }

    /// <summary>
    /// Xuất biên nhận dạng PDF bằng trình duyệt headless. Trả về true và đường
    /// dẫn khi thành công; false kèm thông báo lỗi khi không tìm thấy trình
    /// duyệt hỗ trợ.
    /// </summary>
    public static async Task<(bool ThanhCong, string? DuongDan, string? Loi)> XuatPdfAsync(
        ChiTietThanhToanKhachHang chiTiet,
        string duongDanPdf)
    {
        var duongDanHtml = LuuFileTam(chiTiet);
        var filePdfTam = Path.Combine(
            Path.GetTempPath(),
            $"phieuthu-{MaAnToan(chiTiet.MaPhieuThu)}-{Guid.NewGuid():N}.pdf");

        try
        {
            var trinhDuyet = TimTrinhDuyetCoSan();
            if (trinhDuyet is null)
            {
                return (false, null,
                    "Không tìm thấy Microsoft Edge hoặc Google Chrome trên máy. " +
                    "Hãy dùng nút \"In phiếu thu\" rồi chọn \"Lưu dưới dạng PDF\" " +
                    "trong hộp thoại in của trình duyệt.");
            }

            var thanhCong = await InPdfBangTrinhDuyetAsync(trinhDuyet, duongDanHtml, filePdfTam);
            if (!thanhCong || !LaFilePdf(filePdfTam))
            {
                return (false, null,
                    "Trình duyệt không tạo được file PDF. Hãy dùng nút \"In phiếu thu\" " +
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

    /// <summary>Dựng nội dung HTML hoàn chỉnh của biên nhận.</summary>
    public static string TaoHtml(ChiTietThanhToanKhachHang chiTiet)
    {
        var tiem = chiTiet.ThongTinTiem ?? new ThongTinTiem();
        var kho = tiem.KhoGiayHienThi;
        var lopKho = kho == ThongTinTiem.KhoGiay58
            ? "k58"
            : kho == ThongTinTiem.KhoGiayA4
                ? "ka4"
                : "k80";

        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>").Append(Enc($"Phiếu thu {chiTiet.MaPhieuThu}")).Append("</title>");
        sb.Append("<style>");
        sb.Append("@page{size:").Append(CssPageSize(kho)).Append(";margin:0;}");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("html,body{margin:0;padding:0;background:#fff;}");
        sb.Append("body{font-family:'Segoe UI','Helvetica Neue',Arial,'Noto Sans',sans-serif;");
        sb.Append("color:#222;font-size:13px;line-height:1.35;");
        sb.Append("-webkit-print-color-adjust:exact;print-color-adjust:exact;}");
        sb.Append(".wrap{width:80mm;max-width:100%;margin:0 auto;padding:5mm 4mm;}");
        sb.Append(".wrap.k58{width:56mm;font-size:11px;padding:3mm 2mm;line-height:1.3;}");
        sb.Append(".wrap.ka4{width:186mm;font-size:13.5px;padding:12mm 10mm;}");
        sb.Append(".center{text-align:center;}.right{text-align:right;}");
        sb.Append(".store{text-align:center;}");
        sb.Append(".logo{display:block;max-height:18mm;max-width:60%;margin:0 auto 2mm;object-fit:contain;}");
        sb.Append(".store-name{font-size:16px;font-weight:700;letter-spacing:.2px;color:#111;text-transform:uppercase;}");
        sb.Append(".k58 .store-name{font-size:13.5px;}.ka4 .store-name{font-size:19px;}");
        sb.Append(".store-line{font-size:11.5px;color:#333;margin-top:1px;word-wrap:break-word;overflow-wrap:anywhere;}");
        sb.Append(".k58 .store-line{font-size:10px;}");
        sb.Append(".title{text-align:center;font-size:16px;font-weight:700;letter-spacing:1px;margin:2.5mm 0 .5mm;}");
        sb.Append(".k58 .title{font-size:13px;}.ka4 .title{font-size:19px;}");
        sb.Append(".subtitle{text-align:center;font-size:11.5px;color:#555;}");
        sb.Append(".k58 .subtitle{font-size:10px;}");
        sb.Append(".sep{border:0;border-top:1px dashed #444;margin:2.8mm 0;}");
        sb.Append("table{border-collapse:collapse;width:100%;}");
        sb.Append("table.info td{padding:1px 0;vertical-align:top;font-size:12px;}");
        sb.Append(".k58 table.info td{font-size:10.5px;}");
        sb.Append(".ka4 table.info td{font-size:13px;}");
        sb.Append("table.info td.k{width:34%;color:#444;white-space:nowrap;padding-right:2mm;}");
        sb.Append("table.info td.v{font-weight:600;color:#111;word-wrap:break-word;}");
        sb.Append(".sum{margin-top:1mm;font-size:12.5px;}");
        sb.Append(".k58 .sum{font-size:11px;}.ka4 .sum{font-size:14px;}");
        sb.Append(".sum .row{display:flex;justify-content:space-between;gap:3mm;padding:.7mm 0;}");
        sb.Append(".sum .row .val{white-space:nowrap;font-weight:600;}");
        sb.Append(".sum .row.pay{font-size:15.5px;font-weight:700;color:#0a6b5c;");
        sb.Append("border-top:1px solid #999;margin-top:1mm;padding-top:1.4mm;}");
        sb.Append(".k58 .sum .row.pay{font-size:13px;}.ka4 .sum .row.pay{font-size:17px;}");
        sb.Append(".sum .row.debt{color:#c0392b;font-weight:700;}");
        sb.Append(".sum .row.paidoff{color:#0a6b5c;font-weight:700;}");
        sb.Append("table.items{table-layout:fixed;font-size:11px;margin-top:1mm;}");
        sb.Append(".k58 table.items{font-size:10px;}.ka4 table.items{font-size:12.5px;}");
        sb.Append("table.items th{font-weight:700;text-align:left;border-bottom:1px solid #555;");
        sb.Append("padding:1.2mm .8mm;color:#111;}");
        sb.Append("table.items td{border-bottom:1px solid #e3e3e3;padding:1.2mm .8mm;");
        sb.Append("vertical-align:top;word-wrap:break-word;overflow-wrap:anywhere;}");
        sb.Append("table.items .r{text-align:right;}");
        sb.Append(".note{margin-top:2mm;padding:1.5mm 2mm;border:1px solid #d8d8d8;");
        sb.Append("border-radius:1.5mm;font-size:11.5px;word-wrap:break-word;}");
        sb.Append(".k58 .note{font-size:10px;}");
        sb.Append(".cancelled{margin:2mm 0;padding:2mm;border:1.5px solid #d64545;");
        sb.Append("border-radius:1.5mm;color:#a32020;font-size:11.5px;}");
        sb.Append(".sign{margin-top:6mm;width:100%;}");
        sb.Append(".sign td{width:50%;text-align:center;vertical-align:top;font-size:11.5px;padding:0 3mm;}");
        sb.Append(".k58 .sign td{font-size:10px;}");
        sb.Append(".sign .role{font-weight:700;}");
        sb.Append(".sign .hint{color:#555;font-style:italic;font-size:10.5px;margin-top:.5mm;}");
        sb.Append(".sign .space{height:18mm;}");
        sb.Append(".ka4 .sign .space{height:24mm;}");
        sb.Append(".thanks{margin-top:3mm;text-align:center;font-style:italic;color:#333;font-size:12px;}");
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
        DongThongTin(sb, tiem.CoFacebook ? tiem.FacebookHienThi : "");

        sb.Append("</div>");

        // =========================================================
        // NHÓM 2: TIÊU ĐỀ PHIẾU THU
        // =========================================================
        sb.Append("<hr class=\"sep\">");
        sb.Append("<div class=\"title\">PHIẾU THU</div>");
        sb.Append("<div class=\"subtitle\">BIÊN NHẬN THANH TOÁN CÔNG NỢ KHÁCH HÀNG</div>");

        if (chiTiet.DaHuy)
        {
            sb.Append("<div class=\"cancelled\">");
            sb.Append("<div><b>PHIẾU THU ĐÃ HỦY — số tiền này đã được hoàn tác công nợ.</b></div>");
            sb.Append("<div><b>Lý do:</b> ").Append(Enc(string.IsNullOrWhiteSpace(chiTiet.GiaoDich.LyDoHuy) ? "Không rõ" : chiTiet.GiaoDich.LyDoHuy)).Append("</div>");
            sb.Append("<div><b>Người hủy:</b> ").Append(Enc(string.IsNullOrWhiteSpace(chiTiet.GiaoDich.NguoiHuy) ? "—" : chiTiet.GiaoDich.NguoiHuy))
              .Append(" — ").Append(Enc(chiTiet.GiaoDich.ThoiGianHuyText)).Append("</div>");
            sb.Append("</div>");
        }

        // =========================================================
        // NHÓM 3: THÔNG TIN GIAO DỊCH
        // =========================================================
        sb.Append("<table class=\"info\">");
        DongInfo(sb, "Mã phiếu thu", chiTiet.MaPhieuThu);
        DongInfo(sb, "Ngày giờ thanh toán", chiTiet.ThoiGianText);
        DongInfo(sb, "Mã khách hàng", chiTiet.MaKhachHang);
        DongInfo(sb, "Tên khách hàng", chiTiet.TenKhachHang);
        DongInfo(sb, "Phương thức thanh toán", chiTiet.PhuongThucHienThi);
        if (chiTiet.ChungTuLienQuans.Count > 0)
            DongInfo(sb, "Chứng từ liên quan", chiTiet.MaChungTuLienQuanText);
        else
            DongInfo(sb, "Mã tham chiếu", chiTiet.MaThamChieuHienThi);
        DongInfo(sb, "Người lập phiếu", chiTiet.NguoiThucHien);
        sb.Append("</table>");

        // =========================================================
        // NHÓM 4: PHÂN BỔ VÀO CHỨNG TỪ (nếu có)
        // =========================================================
        var coPhanBo = chiTiet.ChungTuLienQuans.Count > 0;
        if (coPhanBo)
        {
            sb.Append("<hr class=\"sep\">");
            sb.Append("<table class=\"items\">");
            sb.Append("<thead><tr>");
            sb.Append("<th style=\"width:10%;\" class=\"right\">STT</th>");
            sb.Append("<th style=\"width:52%;\">Chứng từ liên quan</th>");
            sb.Append("<th style=\"width:38%;\" class=\"right\">Số tiền phân bổ</th>");
            sb.Append("</tr></thead><tbody>");

            for (var index = 0; index < chiTiet.ChungTuLienQuans.Count; index++)
            {
                var chungTu = chiTiet.ChungTuLienQuans[index];
                sb.Append("<tr>");
                sb.Append("<td class=\"right\">").Append(index + 1).Append("</td>");
                sb.Append("<td>")
                  .Append(Enc(chungTu.MaChungTu))
                  .Append(" <span style=\"color:#666;\">(")
                  .Append(Enc(chungTu.LoaiHienThi))
                  .Append(")</span></td>");
                sb.Append("<td class=\"right\">").Append(Enc(chungTu.SoTienPhanBoHienThi)).Append("</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
        }

        // =========================================================
        // NHÓM 5: TỔNG KẾT CÔNG NỢ
        // =========================================================
        sb.Append("<hr class=\"sep\">");
        sb.Append("<div class=\"sum\">");
        DongTien(sb, "Công nợ trước thanh toán", chiTiet.CongNoTruoc);

        sb.Append("<div class=\"row pay\"><span>SỐ TIỀN THỰC NHẬN</span>")
          .Append("<span class=\"val\">").Append(Tien(chiTiet.SoTien)).Append("</span></div>");

        DongTien(sb, "Công nợ còn lại", chiTiet.CongNoSau, chiTiet.DaTraHet ? "paidoff" : "debt");

        sb.Append("</div>");

        if (!string.IsNullOrWhiteSpace(chiTiet.GiaoDich.GhiChu))
        {
            sb.Append("<div class=\"note\"><b>Ghi chú:</b> ")
              .Append(Enc(chiTiet.GiaoDich.GhiChu.Trim())).Append("</div>");
        }

        // =========================================================
        // NHÓM 6: CHỮ KÝ
        // =========================================================
        sb.Append("<table class=\"sign\">");
        sb.Append("<tr>");
        sb.Append("<td>");
        sb.Append("<div class=\"role\">Người nộp tiền</div>");
        sb.Append("<div class=\"hint\">(Ký, ghi rõ họ tên)</div>");
        sb.Append("<div class=\"space\"></div>");
        sb.Append("</td>");
        sb.Append("<td>");
        sb.Append("<div class=\"role\">Người thu tiền</div>");
        sb.Append("<div class=\"hint\">(Ký, ghi rõ họ tên)</div>");
        sb.Append("<div class=\"space\"></div>");
        sb.Append("</td>");
        sb.Append("</tr>");
        sb.Append("</table>");

        sb.Append("<div class=\"thanks\">").Append(Enc(tiem.LoiCamOnHienThi)).Append("</div>");

        sb.Append("<div class=\"noprint\">");
        sb.Append("<button onclick=\"window.print()\">🖨 In phiếu thu</button>");
        sb.Append("</div>");

        sb.Append("</div></body></html>");

        return sb.ToString();
    }

    // =========================================================
    // HỖ TRỢ XUẤT PDF BẰNG TRÌNH DUYỆT HEADLESS
    // =========================================================

    /// <summary>Danh sách vị trí thường thấy của Edge / Chrome trên Windows.</summary>
    private static readonly string[] DuongDanTrinhDuyet =
    [
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
    ];

    /// <summary>Tìm trình duyệt Chromium đã cài sẵn trên máy.</summary>
    public static string? TimTrinhDuyetCoSan()
    {
        foreach (var duongDan in DuongDanTrinhDuyet)
        {
            if (File.Exists(duongDan))
                return duongDan;
        }

        return null;
    }

    private static async Task<bool> InPdfBangTrinhDuyetAsync(
        string trinhDuyet,
        string duongDanHtml,
        string duongDanPdf)
    {
        var url = new Uri(duongDanHtml).AbsoluteUri;
        var thuMucProfile = Path.Combine(Path.GetTempPath(), "ShopHoa", "HeadlessProfile");

        // Thử headless mới trước; trình duyệt quá cũ không hiểu --headless=new
        // thì rơi về headless cổ điển.
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
                    $"--virtual-time-budget=15000",
                    $"\"{url}\"")
            };

            using var process = Process.Start(psi);
            if (process is null)
                return false;

            // Chờ tối đa 60 giây để không treo ứng dụng nếu trình duyệt bị kẹt.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); } catch { }
                return false;
            }

            return process.ExitCode == 0 && LaFilePdf(duongDanPdf);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Kiểm tra file tồn tại, có dung lượng và có chữ ký %PDF.</summary>
    internal static bool LaFilePdf(string duongDan)
    {
        try
        {
            if (!File.Exists(duongDan) || new FileInfo(duongDan).Length < 100)
                return false;

            using var stream = new FileStream(duongDan, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var boDem = new byte[4];
            return stream.Read(boDem, 0, boDem.Length) == 4
                && boDem[0] == (byte)'%' && boDem[1] == (byte)'P'
                && boDem[2] == (byte)'D' && boDem[3] == (byte)'F';
        }
        catch
        {
            return false;
        }
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

    private static string MaAnToan(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
            return "phieuthu";

        var sb = new StringBuilder(ma.Length);
        foreach (var c in ma)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                sb.Append(c);
        }

        return sb.Length == 0 ? "phieuthu" : sb.ToString();
    }

    /// <summary>
    /// Tên file PDF gợi ý theo mã phiếu thu, ví dụ: PhieuThu_PT0026.pdf.
    /// </summary>
    public static string TaoTenFilePdf(string maPhieuThu)
        => $"PhieuThu_{MaAnToan(maPhieuThu)}.pdf";

    /// <summary>
    /// Đọc file ảnh và nhúng thẳng vào HTML dạng data URI để bản in không phụ
    /// thuộc đường dẫn file (tái dùng cách của HoaDonPrintService).
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

    private static string Enc(string? value)
        => string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");

    private static void TryDelete(string duongDan)
    {
        try
        {
            if (File.Exists(duongDan))
                File.Delete(duongDan);
        }
        catch
        {
            // Không để lỗi dọn file tạm che mất kết quả xuất PDF chính.
        }
    }
}
