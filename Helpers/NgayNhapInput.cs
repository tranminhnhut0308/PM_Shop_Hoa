using System;
using System.Globalization;

namespace ShopHoa.Helpers;

/// <summary>
/// Quy tắc đọc/ghi ngày giờ nhập hàng.
/// Tách riêng phần phân tích chuỗi khỏi giao diện để ô nhập giữ nguyên
/// nội dung người dùng đang gõ (không tự định dạng lại giữa chừng).
/// </summary>
public static class NgayNhapInput
{
    public const string DinhDangHienThi = "dd/MM/yyyy HH:mm";

    private static readonly string[] DinhDang =
    [
        "dd/MM/yyyy HH:mm",
        "dd/MM/yyyy H:mm",
        "dd/MM/yyyy H:m",
        "d/M/yyyy HH:mm",
        "d/M/yyyy H:mm",
        "d/M/yyyy H:m",
        "dd/MM/yyyy HH:mm:ss",
        "dd/MM/yyyy H:m:s",
        "d/M/yyyy H:mm:ss",
        "d/M/yyyy H:m:s",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd-MM-yyyy HH:mm",
        "dd-MM-yyyy",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd"
    ];

    /// <summary>Hiển thị ngày giờ theo định dạng Việt Nam dd/MM/yyyy HH:mm.</summary>
    public static string Format(DateTime? value)
    {
        return value?.ToString(DinhDangHienThi, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Đọc chuỗi người dùng gõ. Khi chỉ nhập ngày thì giữ nguyên phần giờ hiện có
    /// thay vì tự đổi về 00:00.
    /// </summary>
    public static bool TryParse(string? text, DateTime? giaTriHienTai, out DateTime value)
    {
        value = default;

        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
            return false;

        if (!DateTime.TryParseExact(
                raw,
                DinhDang,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var ngayGio))
        {
            return false;
        }

        if (!raw.Contains(':') && giaTriHienTai.HasValue)
            ngayGio = ngayGio.Date + giaTriHienTai.Value.TimeOfDay;

        value = ngayGio;
        return true;
    }
}
