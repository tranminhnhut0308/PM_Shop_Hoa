using System;
using ShopHoa.Helpers;

namespace ShopHoa.Tests;

/// <summary>
/// Kiểm thử ô "Ngày nhập" của phiếu nhập hàng:
/// - Gõ ngày giờ từng phần bằng bàn phím (kể cả di chuyển bằng phím mũi tên).
/// - Bôi đen từng phần ngày/tháng/năm bằng chuột rồi gõ đè.
/// - Không bị nhân đôi ký tự và giữ đúng nội dung người dùng đã gõ.
/// </summary>
internal static class NgayNhapInputVerifier
{
    public static int Run()
    {
        if (!VerifyKeyboardEditing()
            || !VerifyMouseSelectionEditing()
            || !VerifyDateOnlyKeepsTime()
            || !VerifySupportedFormats())
        {
            return 1;
        }

        Console.WriteLine("Các ca nhập Ngày nhập bằng bàn phím/chuột, giữ con trỏ và định dạng ngày giờ đều đạt.");
        return 0;
    }

    private static bool VerifyKeyboardEditing()
    {
        var field = new DateFieldSimulator("10/11/2026 08:11");

        // Xóa phần ngày rồi gõ ngày mới.
        field.SelectRange(0, 2);
        field.Type("12");

        // Phím mũi tên sang phải để vào phần tháng (không được nhân đôi số cuối).
        field.MoveRight();
        field.SelectRange(3, 2);
        field.Type("05");

        field.MoveRight();
        field.SelectRange(6, 4);
        field.Type("2027");

        if (!TryParse(field.Text, out var value)
            || field.Text != "12/05/2027 08:11"
            || value != new DateTime(2027, 5, 12, 8, 11, 0))
        {
            return Fail($"Nhập bằng bàn phím sai: '{field.Text}'.");
        }

        return true;
    }

    private static bool VerifyMouseSelectionEditing()
    {
        var field = new DateFieldSimulator("10/11/2026 08:11");

        // Dùng chuột bôi đen phần tháng rồi gõ đè.
        field.SelectRange(3, 2);
        field.Type("12");

        // Bôi đen phần giờ rồi gõ đè.
        field.SelectRange(11, 2);
        field.Type("23");

        if (!TryParse(field.Text, out var value)
            || field.Text != "10/12/2026 23:11"
            || value != new DateTime(2026, 12, 10, 23, 11, 0))
        {
            return Fail($"Nhập bằng chuột sai: '{field.Text}'.");
        }

        return true;
    }

    private static bool VerifyDateOnlyKeepsTime()
    {
        var current = new DateTime(2026, 11, 10, 8, 11, 0);

        if (!NgayNhapInput.TryParse("12/05/2027", current, out var value)
            || value != new DateTime(2027, 5, 12, 8, 11, 0))
        {
            return Fail("Chỉ nhập ngày phải giữ nguyên phần giờ hiện có.");
        }

        return true;
    }

    private static bool VerifySupportedFormats()
    {
        var cases = new (string Text, DateTime Expected)[]
        {
            ("10/11/2026 08:11", new DateTime(2026, 11, 10, 8, 11, 0)),
            ("1/2/2026 8:5", new DateTime(2026, 2, 1, 8, 5, 0)),
            ("10-11-2026", new DateTime(2026, 11, 10, 0, 0, 0)),
            ("2026-11-10", new DateTime(2026, 11, 10, 0, 0, 0))
        };

        foreach (var testCase in cases)
        {
            if (!NgayNhapInput.TryParse(testCase.Text, null, out var value)
                || value != testCase.Expected)
            {
                return Fail($"Không đọc được định dạng '{testCase.Text}'.");
            }
        }

        if (NgayNhapInput.TryParse("31/02/2026", null, out _))
            return Fail("Ngày không tồn tại phải bị từ chối.");

        return true;
    }

    private static bool TryParse(string text, out DateTime value)
        => NgayNhapInput.TryParse(text, new DateTime(2026, 11, 10, 8, 11, 0), out value);

    private static bool Fail(string message)
    {
        Console.WriteLine(message);
        return false;
    }

    /// <summary>
    /// Mô phỏng đúng cách TextBox chèn/xóa ký tự tại vị trí con trỏ,
    /// không tự định dạng lại chuỗi trong lúc gõ.
    /// </summary>
    private sealed class DateFieldSimulator
    {
        public DateFieldSimulator(string text)
        {
            Text = text;
            Caret = text.Length;
        }

        public string Text { get; private set; }
        public int Caret { get; private set; }
        private int SelectionStart { get; set; } = -1;
        private int SelectionEnd { get; set; } = -1;

        public void SelectRange(int start, int length)
        {
            SelectionStart = Math.Clamp(start, 0, Text.Length);
            SelectionEnd = Math.Clamp(start + length, SelectionStart, Text.Length);
            Caret = SelectionEnd;
        }

        public void MoveRight()
        {
            if (SelectionStart >= 0)
            {
                Caret = SelectionEnd;
                ClearSelection();
            }

            if (Caret < Text.Length)
                Caret++;
        }

        public void Type(string input)
        {
            if (SelectionStart >= 0)
            {
                var start = Math.Min(SelectionStart, SelectionEnd);
                var length = Math.Abs(SelectionEnd - SelectionStart);
                Text = Text.Remove(start, length);
                Caret = start;
                ClearSelection();
            }

            Caret = Math.Clamp(Caret, 0, Text.Length);
            Text = Text.Insert(Caret, input);
            Caret += input.Length;
        }

        private void ClearSelection()
        {
            SelectionStart = -1;
            SelectionEnd = -1;
        }
    }
}
