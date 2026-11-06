using System.Globalization;
using ShopHoa.Helpers;

namespace ShopHoa.Tests;

internal static class MoneyInputVerifier
{
    public static int Run()
    {
        if (!VerifyCommonAmounts()
            || !VerifyExcelPaste()
            || !VerifyCaret()
            || !VerifyDecimalInput()
            || !VerifyEmptyAndInvalidInput())
        {
            return 1;
        }

        Console.WriteLine("Các ca định dạng tiền, dán số, số thập phân, xóa trắng và vị trí con trỏ đều đạt.");
        return 0;
    }

    private static bool VerifyCommonAmounts()
    {
        var cases = new (decimal Value, string Expected)[]
        {
            (0m, "0"),
            (500m, "500"),
            (50000m, "50.000"),
            (234567m, "234.567"),
            (1234900m, "1.234.900"),
            (1000000000m, "1.000.000.000")
        };

        foreach (var testCase in cases)
        {
            var raw = testCase.Value.ToString(CultureInfo.InvariantCulture);
            if (MoneyInput.Format(testCase.Value) != testCase.Expected
                || !MoneyInput.TryFormatEditingText(
                    raw, raw.Length, MoneyInput.GiaTienToiDa, out var formatted, out var caret)
                || formatted != testCase.Expected
                || caret != formatted.Length
                || !MoneyInput.TryParse(formatted, true, out var parsed, out _)
                || parsed != testCase.Value)
            {
                return Fail($"Sai định dạng tiền cho {testCase.Value}.");
            }
        }

        return true;
    }

    private static bool VerifyExcelPaste()
    {
        if (!MoneyInput.TryFormatEditingText(
                " 1.234.900 ₫ ", 13, MoneyInput.GiaTienToiDa, out var pasted, out _)
            || pasted != "1.234.900"
            || !MoneyInput.TryParse(pasted, true, out var value, out _)
            || value != 1234900m)
        {
            return Fail("Sai xử lý dán số tiền đã định dạng từ bảng tính.");
        }

        return true;
    }

    private static bool VerifyCaret()
    {
        if (!MoneyInput.TryFormatEditingText(
                "1234900", 3, MoneyInput.GiaTienToiDa, out var edited, out var caret)
            || edited != "1.234.900"
            || caret != 4)
        {
            return Fail("Sai vị trí con trỏ khi sửa ở giữa chuỗi.");
        }

        return true;
    }

    private static bool VerifyDecimalInput()
    {
        if (!MoneyInput.TryFormatEditingText(
                "1.234,5", 7, MoneyInput.GiaTienToiDa, out var formatted, out _)
            || formatted != "1.234,5"
            || !MoneyInput.TryParse(formatted, true, out var value, out _)
            || value != 1234.5m)
        {
            return Fail("Sai xử lý giá trị thập phân theo định dạng Việt Nam.");
        }

        return true;
    }

    private static bool VerifyEmptyAndInvalidInput()
    {
        if (!MoneyInput.TryFormatEditingText(
                "", 0, MoneyInput.GiaTienToiDa, out var empty, out _)
            || empty.Length != 0
            || MoneyInput.TryFormatEditingText(
                "-50000", 6, MoneyInput.GiaTienToiDa, out _, out _)
            || MoneyInput.TryFormatEditingText(
                "50a000", 6, MoneyInput.GiaTienToiDa, out _, out _))
        {
            return Fail("Sai xử lý nội dung trống hoặc ký tự không hợp lệ.");
        }

        return true;
    }

    private static bool Fail(string message)
    {
        Console.Error.WriteLine(message);
        return false;
    }
}
