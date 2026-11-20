using System.Globalization;

namespace ShopHoa.Helpers;

/// <summary>
/// Quy tắc định dạng SỐ LƯỢNG hàng hóa (tồn kho, khách đặt...).
/// Khác với tiền: không gắn ký hiệu tiền tệ và không giữ các số 0 thập phân
/// vô nghĩa do cột DECIMAL(15,3) sinh ra (100.000 phải hiện là 100, không phải
/// "100.000" khiến người dùng tưởng là một trăm nghìn).
/// </summary>
public static class SoLuongFormat
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>
    /// Chuỗi dùng cho ô nhập liệu: không có dấu phân cách hàng nghìn (để người
    /// dùng sửa số bình thường và không bị nhầm dấu chấm), bỏ số 0 thập phân vô
    /// nghĩa nhưng vẫn giữ phần thập phân thật.
    /// Ví dụ: 100.000 -> "100"; 15.000 -> "15"; 100.500 -> "100.5".
    /// </summary>
    public static string Raw(decimal value)
    {
        return value == decimal.Truncate(value)
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
    }

    /// <summary>
    /// Chuỗi hiển thị theo văn hóa Việt Nam: có dấu phân cách hàng nghìn, giữ
    /// phần thập phân thật và bỏ số 0 thập phân vô nghĩa.
    /// Ví dụ: 100.000 -> "100"; 1500 -> "1.500"; 100.500 -> "100,5".
    /// </summary>
    public static string HienThi(decimal value)
    {
        return value == decimal.Truncate(value)
            ? value.ToString("N0", VietnameseCulture)
            : value.ToString("#,0.###", VietnameseCulture);
    }

    /// <summary>
    /// Đọc số lượng an toàn từ chuỗi người dùng nhập (hỗ trợ cả số nguyên, dấu phẩy và dấu chấm thập phân,
    /// ví dụ: "5", "10", "1.5", "1,5", "100.5", "1.000").
    /// </summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var raw = text.Trim();

        // Nếu có cả . và , (ví dụ "1.000,5") -> . là phân cách nghìn, , là thập phân
        if (raw.Contains('.') && raw.Contains(','))
        {
            raw = raw.Replace(".", string.Empty).Replace(',', '.');
            return decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        // Nếu chỉ có dấu phẩy -> coi là dấu thập phân (kiểu VN: 1,5)
        if (raw.Contains(','))
        {
            raw = raw.Replace(',', '.');
            return decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        // Nếu chỉ có dấu chấm:
        // Với số lượng hàng hóa hoa (bó, cành, giỏ, kg), số lượng thường là số thực (ví dụ 1.5, 0.5, 2.5)
        // hoặc số nguyên có dấu chấm phân cách nghìn (ví dụ 1.000).
        if (raw.Contains('.'))
        {
            var parts = raw.Split('.');
            if (parts.Length == 2 && parts[1].Length == 3 && int.TryParse(parts[0], out var p0) && p0 >= 1)
            {
                if (decimal.TryParse(raw.Replace(".", string.Empty), NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                    return true;
            }

            if (decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                return true;
        }

        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
               || decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value);
    }
}
