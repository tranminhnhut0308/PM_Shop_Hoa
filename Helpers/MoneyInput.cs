using System.Globalization;

namespace ShopHoa.Helpers;

/// <summary>
/// Quy tắc đọc/ghi số tiền theo kiểu Việt Nam.
/// Tách riêng phần tính toán khỏi phần giao diện để dùng chung cho
/// ô nhập và cho việc hiển thị tổng tiền.
/// </summary>
public static class MoneyInput
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>
    /// Định dạng tiền Việt Nam có dấu phân cách hàng nghìn.
    /// Số nguyên thì không hiện phần thập phân (75.000 chứ không phải 75.000,00).
    /// </summary>
    public static string Format(decimal value)
    {
        return value == decimal.Truncate(value)
            ? value.ToString("N0", VietnameseCulture)
            : value.ToString("N2", VietnameseCulture);
    }

    /// <summary>Số trần không dấu phân cách, dùng khi người dùng đang gõ/sửa số.</summary>
    public static string ToRawText(decimal value)
    {
        return value == decimal.Truncate(value)
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Đọc số từ chuỗi người dùng gõ. Chấp nhận cả "3.842.857", "3842857", "1.500,5".
    /// Không parse được thì trả về 0 để người dùng vẫn nhập tiếp được.
    /// </summary>
    public static decimal Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;

        var normalized = text
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("₫", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (normalized.Length == 0)
            return 0m;

        if (normalized.Contains(','))
        {
            // "1.500,5" -> bỏ dấu phân cách nghìn, đổi dấu phẩy thành dấu thập phân
            normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
        }
        else
        {
            // Chỉ có dấu chấm -> coi là dấu phân cách hàng nghìn
            normalized = normalized.Replace(".", string.Empty);
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : 0m;
    }
}
