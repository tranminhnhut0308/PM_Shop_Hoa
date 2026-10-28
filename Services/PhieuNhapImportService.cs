using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed class PhieuNhapImportService
{
    private static readonly IReadOnlyDictionary<string, string> TenCotThayThe =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["manhaphang"] = "maphieunhap",
            ["chiphinhaptrancc"] = "chiphinaptranncc",
            ["chiphipnhaptncc"] = "chiphinaptranncc",
            ["chiphinaptncc"] = "chiphinaptranncc",
            ["chiphinaptranhacungcap"] = "chiphinaptranncc",
            ["chiphiphitranncc"] = "chiphinaptranncc",
            ["chiphiphatncc"] = "chiphinaptranncc",
            ["chiphiphatnhacungcap"] = "chiphinaptranncc",
            ["cantranhancc"] = "cantranncc",
            ["cantrancc"] = "cantranncc",
            ["cantranhacungcap"] = "cantranncc",
            ["cantrannhacungcap"] = "cantranncc",
            ["tiencantra"] = "cantranncc",
            ["sotiencantra"] = "cantranncc",
            ["tongtiencantra"] = "cantranncc",
            ["tongtienphaitra"] = "cantranncc",
            ["phaithanhtoan"] = "cantranncc",
            ["tiendatrancc"] = "tiendatranncc",
            ["tiendatran"] = "tiendatranncc",
            ["tiendathanhtoan"] = "tiendatranncc",
            ["sotiendathanhtoan"] = "tiendatranncc",
            ["dathanhtoanncc"] = "tiendatranncc",
            ["dathanhtoan"] = "tiendatranncc",
            ["dathanhtoannhacungcap"] = "tiendatranncc",
            ["thanhtoanncc"] = "tiendatranncc",
            ["tienthanhtoan"] = "tiendatranncc",
            ["sotienthanhtoan"] = "tiendatranncc"
        };

    private static readonly string[] CotBatBuoc =
    [
        "maphieunhap", "thoigian", "manhacungcap", "tongtienhang",
        "giamgiaphieunhap", "chiphinaptranncc", "cantranncc", "tiendatranncc",
        "chiphinhapkhac", "mahang", "soluong", "thanhtien"
    ];

    public Task<PhieuNhapImportPreview> DocFileAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file phiếu nhập.", filePath);

        return Task.Run(() => DocFile(filePath, ct), ct);
    }

    private static PhieuNhapImportPreview DocFile(string filePath, CancellationToken ct)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("File Excel không có worksheet.");
        var headerRow = worksheet.FirstRowUsed()
            ?? throw new InvalidDataException("Worksheet không có dữ liệu.");

        var headers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cell in headerRow.CellsUsed())
        {
            var rawHeader = KhoaCot(cell.GetString());
            var key = TenCotThayThe.TryGetValue(rawHeader, out var canonical)
                ? canonical
                : rawHeader;
            headers.TryAdd(key, cell.Address.ColumnNumber);
        }
        var missing = CotBatBuoc.Where(column => !headers.ContainsKey(column)).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException(
                "Không nhận diện được mẫu file phiếu nhập. Thiếu cột: " + string.Join(", ", missing)
                + ". Các cột đã đọc được: "
                + string.Join(", ", headerRow.CellsUsed().Select(cell => cell.GetString().Trim()).Where(name => name.Length > 0)));
        if (!headers.ContainsKey("gianhap") && !headers.ContainsKey("dongia"))
            throw new InvalidDataException("Không nhận diện được mẫu file phiếu nhập. Thiếu cột Giá nhập hoặc Đơn giá.");

        var rows = new Dictionary<string, PhieuNhapImportReceipt>(StringComparer.OrdinalIgnoreCase);
        var preview = new PhieuNhapImportPreview { TenFile = Path.GetFileName(filePath) };
        var issues = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var currentInvoiceCode = "";

        foreach (var row in worksheet.RowsUsed().Where(row => row.RowNumber() > headerRow.RowNumber()))
        {
            ct.ThrowIfCancellationRequested();
            var code = Text(row, headers, "maphieunhap");
            var productCode = Text(row, headers, "mahang");
            if (string.IsNullOrWhiteSpace(code))
            {
                if (string.IsNullOrWhiteSpace(productCode))
                {
                    currentInvoiceCode = "";
                    continue;
                }

                if (currentInvoiceCode.Length == 0)
                {
                    AddIssue(issues, $"Dòng {row.RowNumber()}", "Dòng chi tiết thiếu Mã nhập hàng.");
                    continue;
                }

                code = currentInvoiceCode;
            }
            else
            {
                currentInvoiceCode = code;
            }

            try
            {
                if (!rows.TryGetValue(code, out var receipt))
                {
                    receipt = new PhieuNhapImportReceipt
                    {
                        MaPhieuNhap = code,
                        TenChiNhanh = Text(row, headers, "chinhanh"),
                        ThoiGianNhap = Date(row, headers, "thoigian"),
                        MaNhaCungCap = Text(row, headers, "manhacungcap"),
                        TongTienHang = Money(row, headers, "tongtienhang"),
                        GiamGia = Money(row, headers, "giamgiaphieunhap"),
                        ChiPhiNhapTraNcc = Money(row, headers, "chiphinaptranncc"),
                        ChiPhiNhapKhac = Money(row, headers, "chiphinhapkhac"),
                        CanTraNcc = Money(row, headers, "cantranncc"),
                        DaTraNcc = Money(row, headers, "tiendatranncc"),
                        TongSoLuong = Money(row, headers, "tongsoluong"),
                        TongSoMatHang = Money(row, headers, "tongsomathang"),
                        GhiChu = Text(row, headers, "ghichu"),
                        SoHoaDonDauVao = Text(row, headers, "sohoadondauvao")
                    };
                    rows.Add(code, receipt);
                }
                else
                {
                    CheckRepeatedHeader(row, headers, receipt, issues, code);
                }

                var quantity = Money(row, headers, "soluong");
                var unitPrice = HasValue(row, headers, "gianhap")
                    ? Money(row, headers, "gianhap")
                    : Money(row, headers, "dongia");
                var lineDiscount = Money(row, headers, "giamgia");
                var lineTotal = Money(row, headers, "thanhtien");

                if (string.IsNullOrWhiteSpace(productCode) && quantity == 0 && lineTotal == 0)
                    continue;

                if (string.IsNullOrWhiteSpace(productCode))
                {
                    AddIssue(issues, code, $"Dòng {row.RowNumber()}: thiếu Mã hàng.");
                    continue;
                }

                if (quantity <= 0 || unitPrice < 0 || lineDiscount < 0 || lineTotal < 0)
                {
                    AddIssue(issues, code, $"Dòng {row.RowNumber()}: số lượng/đơn giá/giảm giá/thành tiền không hợp lệ.");
                    continue;
                }

                var expectedLineTotal = Math.Max(0, quantity * unitPrice - lineDiscount);
                var roundedLineTotal = decimal.Round(
                    expectedLineTotal,
                    0,
                    MidpointRounding.AwayFromZero);
                if (Math.Abs(expectedLineTotal - lineTotal) > 0.01m
                    && Math.Abs(roundedLineTotal - lineTotal) > 0.01m)
                {
                    AddIssue(issues, code,
                        $"Dòng {row.RowNumber()}: Thành tiền {lineTotal:N2} không khớp số lượng × giá nhập − giảm giá ({expectedLineTotal:N2}).");
                    continue;
                }

                receipt.Items.Add(new PhieuNhapImportLine
                {
                    SoDongExcel = row.RowNumber(),
                    MaSanPham = productCode,
                    SoLuong = quantity,
                    DonGiaNhap = unitPrice,
                    GiamGia = lineDiscount,
                    ThanhTien = lineTotal,
                    GhiChu = Text(row, headers, "ghichuhanghoa")
                });
            }
            catch (InvalidDataException ex)
            {
                AddIssue(issues, code, $"Dòng {row.RowNumber()}: {ex.Message}");
            }
        }

        foreach (var receipt in rows.Values)
        {
            var expectedTotal = receipt.Items.Sum(item => item.ThanhTien);
            if (Math.Abs(expectedTotal - receipt.TongTienHang) > 0.01m)
                AddIssue(issues, receipt.MaPhieuNhap,
                    $"Tổng thành tiền chi tiết {expectedTotal:N2} không khớp Tổng tiền hàng {receipt.TongTienHang:N2}.");

            var expectedPayable = receipt.TongTienHang - receipt.GiamGia
                + receipt.ChiPhiNhapTraNcc + receipt.ChiPhiNhapKhac;
            if (Math.Abs(expectedPayable - receipt.CanTraNcc) > 0.01m)
                AddIssue(issues, receipt.MaPhieuNhap,
                    $"Cần trả NCC {receipt.CanTraNcc:N2} không khớp tổng hàng − giảm giá + chi phí ({expectedPayable:N2}).");
            if (receipt.TongSoLuong > 0 &&
                Math.Abs(receipt.Items.Sum(item => item.SoLuong) - receipt.TongSoLuong) > 0.01m)
                AddIssue(issues, receipt.MaPhieuNhap, "Tổng số lượng chi tiết không khớp cột Tổng số lượng.");
            if (receipt.TongSoMatHang > 0 &&
                receipt.Items.Select(item => item.MaSanPham).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != receipt.TongSoMatHang)
                AddIssue(issues, receipt.MaPhieuNhap, "Số mã hàng chi tiết không khớp cột Tổng số mặt hàng.");

            if (receipt.DaTraNcc < 0 || receipt.DaTraNcc > receipt.CanTraNcc)
                AddIssue(issues, receipt.MaPhieuNhap, "Tiền đã trả NCC nằm ngoài khoảng từ 0 đến Cần trả NCC.");
            if (receipt.Items.Count == 0)
                AddIssue(issues, receipt.MaPhieuNhap, "Phiếu không có dòng hàng hóa hợp lệ.");
            if (string.IsNullOrWhiteSpace(receipt.MaNhaCungCap))
                AddIssue(issues, receipt.MaPhieuNhap, "Thiếu Mã nhà cung cấp.");
            if (receipt.ThoiGianNhap == default)
                AddIssue(issues, receipt.MaPhieuNhap, "Thiếu hoặc sai định dạng Thời gian.");
        }

        preview.Loi = issues
            .SelectMany(pair => pair.Value.Select(reason => new PhieuNhapImportIssue(pair.Key, reason)))
            .ToList();
        preview.PhieuNhap = rows.Values
            .Where(receipt => !issues.ContainsKey(receipt.MaPhieuNhap))
            .ToList();
        return preview;
    }

    private static void CheckRepeatedHeader(
        IXLRow row,
        IReadOnlyDictionary<string, int> headers,
        PhieuNhapImportReceipt receipt,
        IDictionary<string, List<string>> issues,
        string code)
    {
        CheckText("manhacungcap", receipt.MaNhaCungCap, Text(row, headers, "manhacungcap"));
        CheckText("chinhanh", receipt.TenChiNhanh, Text(row, headers, "chinhanh"));
        CheckMoney("tongtienhang", receipt.TongTienHang, Money(row, headers, "tongtienhang"), row, headers);
        CheckMoney("giamgiaphieunhap", receipt.GiamGia, Money(row, headers, "giamgiaphieunhap"), row, headers);
        CheckMoney("chiphinaptranncc", receipt.ChiPhiNhapTraNcc, Money(row, headers, "chiphinaptranncc"), row, headers);
        CheckMoney("chiphinhapkhac", receipt.ChiPhiNhapKhac, Money(row, headers, "chiphinhapkhac"), row, headers);
        CheckMoney("cantranncc", receipt.CanTraNcc, Money(row, headers, "cantranncc"), row, headers);
        CheckMoney("tiendatranncc", receipt.DaTraNcc, Money(row, headers, "tiendatranncc"), row, headers);
        CheckMoney("tongsoluong", receipt.TongSoLuong, Money(row, headers, "tongsoluong"), row, headers);
        CheckMoney("tongsomathang", receipt.TongSoMatHang, Money(row, headers, "tongsomathang"), row, headers);
        if (HasValue(row, headers, "thoigian"))
            CheckDate("thoigian", receipt.ThoiGianNhap, Date(row, headers, "thoigian"));

        void CheckText(string field, string expected, string actual)
        {
            if (actual.Length > 0 && expected.Length > 0 &&
                !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                AddIssue(issues, code, $"Các dòng của phiếu có dữ liệu cột {field} không nhất quán.");
        }

        void CheckMoney(string field, decimal expected, decimal actual, IXLRow currentRow, IReadOnlyDictionary<string, int> currentHeaders)
        {
            if (HasValue(currentRow, currentHeaders, field) && Math.Abs(expected - actual) > 0.01m)
                AddIssue(issues, code, $"Các dòng của phiếu có dữ liệu cột {field} không nhất quán.");
        }

        void CheckDate(string field, DateTime expected, DateTime actual)
        {
            if (expected != actual)
                AddIssue(issues, code, $"Các dòng của phiếu có dữ liệu cột {field} không nhất quán.");
        }
    }

    private static string Text(IXLRow row, IReadOnlyDictionary<string, int> headers, string key)
        => headers.TryGetValue(key, out var column) ? row.Cell(column).GetFormattedString().Trim() : "";

    private static bool HasValue(IXLRow row, IReadOnlyDictionary<string, int> headers, string key)
        => headers.TryGetValue(key, out var column) && !row.Cell(column).IsEmpty();

    private static decimal Money(IXLRow row, IReadOnlyDictionary<string, int> headers, string key)
    {
        if (!headers.TryGetValue(key, out var column))
            return 0m;
        var cell = row.Cell(column);
        if (cell.IsEmpty())
            return 0m;
        if (cell.TryGetValue<decimal>(out var value))
            return value;

        var text = cell.GetFormattedString().Trim().Replace("đ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("₫", "", StringComparison.Ordinal).Replace(" ", "");
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out value))
            return value;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return value;
        throw new InvalidDataException($"Giá trị tiền không hợp lệ ở ô {cell.Address}: \"{text}\".");
    }

    private static DateTime Date(IXLRow row, IReadOnlyDictionary<string, int> headers, string key)
    {
        if (!headers.TryGetValue(key, out var column))
            return default;
        var cell = row.Cell(column);
        if (cell.TryGetValue<DateTime>(out var value))
            return value;
        var text = cell.GetFormattedString().Trim();
        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.AssumeLocal, out value))
            return value;
        return default;
    }

    private static string KhoaCot(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (character is 'đ' or 'Đ')
            {
                builder.Append('d');
                continue;
            }
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static void AddIssue(IDictionary<string, List<string>> issues, string code, string reason)
    {
        if (!issues.TryGetValue(code, out var reasons))
            issues.Add(code, reasons = []);
        if (!reasons.Contains(reason, StringComparer.Ordinal))
            reasons.Add(reason);
    }
}
