using System;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ShopHoa.Controls;

/// <summary>
/// Control nhập ngày tháng năm phân đoạn 3 ô riêng biệt: Ngày / Tháng / Năm.
/// - Mỗi ô là một TextBox độc lập.
/// - Khi focus vào từng ô, tự động bôi đen toàn bộ giá trị cũ (Select All).
/// - Nhập số mới sẽ thay thế giá trị cũ.
/// - Nhập đủ 2 số ngày -> tự động chuyển sang ô tháng.
/// - Nhập đủ 2 số tháng -> tự động chuyển sang ô năm.
/// - Nhập đủ 4 số năm -> hoàn tất.
/// - Hỗ trợ phím Backspace, Delete, Left, Right, Tab, Enter.
/// - Kiểm tra tính hợp lệ của ngày, tháng, năm nhuận.
/// - Chặn ký tự tiếng Việt hoặc ký tự không phải số.
/// - Đồng bộ 2 chiều với Value (bảo toàn giờ và phút).
/// </summary>
public partial class DateInputBox : UserControl
{
    public static readonly StyledProperty<DateTime?> ValueProperty =
        AvaloniaProperty.Register<DateInputBox, DateTime?>(
            nameof(Value),
            defaultBindingMode: BindingMode.TwoWay);

    public DateTime? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public TextBox DayBox => TxtDay;
    public TextBox MonthBox => TxtMonth;
    public TextBox YearBox => TxtYear;

    public string Text => $"{TxtDay.Text}/{TxtMonth.Text}/{TxtYear.Text}";

    private bool _isInternalUpdating;
    private bool _isHandlingTextChange;

    public DateInputBox()
    {
        InitializeComponent();

        SetupBox(TxtDay, 2, () => OnDayChanged());
        SetupBox(TxtMonth, 2, () => OnMonthChanged());
        SetupBox(TxtYear, 4, () => OnYearChanged());

        this.AddHandler(LostFocusEvent, OnControlLostFocus, RoutingStrategies.Bubble);

        ValueProperty.Changed.AddClassHandler<DateInputBox>(
            static (control, args) => control.OnValueChanged(args));

        SyncFromValue(Value ?? DateTime.Now);
    }

    private void SetupBox(
        TextBox box,
        int maxLength,
        Action onChanged)
    {
        box.MaxLength = maxLength;

        box.GotFocus += (s, e) =>
        {
            Dispatcher.UIThread.Post(() => box.SelectAll(), DispatcherPriority.Input);
        };

        box.AddHandler(PointerReleasedEvent, (s, e) =>
        {
            Dispatcher.UIThread.Post(() => box.SelectAll(), DispatcherPriority.Input);
        }, RoutingStrategies.Bubble);

        box.AddHandler(TextInputEvent, OnBoxTextInput, RoutingStrategies.Tunnel);

        // Lắng nghe thay đổi Text qua PropertyChanged để bắt mọi thay đổi (cả người dùng gõ và code gán)
        box.PropertyChanged += (s, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                onChanged();
            }
        };

        if (box == TxtDay) box.KeyDown += OnDayKeyDown;
        else if (box == TxtMonth) box.KeyDown += OnMonthKeyDown;
        else if (box == TxtYear) box.KeyDown += OnYearKeyDown;
    }

    private void OnValueChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_isInternalUpdating)
            return;

        var newVal = args.GetNewValue<DateTime?>();
        if (newVal.HasValue)
        {
            SyncFromValue(newVal.Value);
        }
    }

    public void SyncFromValue(DateTime dt)
    {
        _isInternalUpdating = true;
        try
        {
            TxtDay.Text = dt.Day.ToString("D2");
            TxtMonth.Text = dt.Month.ToString("D2");
            TxtYear.Text = dt.Year.ToString("D4");
        }
        finally
        {
            _isInternalUpdating = false;
        }
    }

    private void OnBoxTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
            return;

        // Chặn tuyệt đối các ký tự không phải chữ số (hỗ trợ tránh lỗi gõ tiếng Việt Telex/VNI)
        foreach (char ch in e.Text)
        {
            if (!char.IsDigit(ch))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private string SanitizeDigitsOnly(TextBox box)
    {
        var raw = box.Text ?? string.Empty;
        var digits = Regex.Replace(raw, @"[^\d]", "");
        if (raw != digits)
        {
            box.Text = digits;
            box.CaretIndex = digits.Length;
        }
        return digits;
    }

    private void OnDayChanged()
    {
        if (_isInternalUpdating || _isHandlingTextChange)
            return;

        _isHandlingTextChange = true;
        try
        {
            var digits = SanitizeDigitsOnly(TxtDay);

            // Nếu người dùng gõ 1 chữ số mà số đó > 3 (ví dụ 4..9), tự động thành "04".."09" và nhảy sang Tháng
            if (digits.Length == 1 && digits[0] >= '4')
            {
                digits = "0" + digits;
                TxtDay.Text = digits;
                TxtDay.CaretIndex = digits.Length;
                CommitIfValid();
                AdvanceToMonth();
                return;
            }

            // Khi nhập đủ 2 số ngày -> tự động chuyển sang ô tháng
            if (digits.Length == 2)
            {
                if (int.TryParse(digits, out int d))
                {
                    if (d < 1) d = 1;
                    if (d > 31) d = 31;
                    digits = d.ToString("D2");
                    if (TxtDay.Text != digits)
                    {
                        TxtDay.Text = digits;
                        TxtDay.CaretIndex = digits.Length;
                    }
                }

                CommitIfValid();
                AdvanceToMonth();
            }
            else
            {
                CommitIfValid();
            }
        }
        finally
        {
            _isHandlingTextChange = false;
        }
    }

    private void OnMonthChanged()
    {
        if (_isInternalUpdating || _isHandlingTextChange)
            return;

        _isHandlingTextChange = true;
        try
        {
            var digits = SanitizeDigitsOnly(TxtMonth);

            // Nếu người dùng gõ 1 chữ số mà số đó > 1 (ví dụ 2..9), tự động thành "02".."09" và nhảy sang Năm
            if (digits.Length == 1 && digits[0] >= '2')
            {
                digits = "0" + digits;
                TxtMonth.Text = digits;
                TxtMonth.CaretIndex = digits.Length;
                CommitIfValid();
                AdvanceToYear();
                return;
            }

            // Khi nhập đủ 2 số tháng -> tự động chuyển sang ô năm
            if (digits.Length == 2)
            {
                if (int.TryParse(digits, out int m))
                {
                    if (m < 1) m = 1;
                    if (m > 12) m = 12;
                    digits = m.ToString("D2");
                    if (TxtMonth.Text != digits)
                    {
                        TxtMonth.Text = digits;
                        TxtMonth.CaretIndex = digits.Length;
                    }
                }

                CommitIfValid();
                AdvanceToYear();
            }
            else
            {
                CommitIfValid();
            }
        }
        finally
        {
            _isHandlingTextChange = false;
        }
    }

    private void OnYearChanged()
    {
        if (_isInternalUpdating || _isHandlingTextChange)
            return;

        _isHandlingTextChange = true;
        try
        {
            var digits = SanitizeDigitsOnly(TxtYear);

            // Nhập năm đủ 4 số -> hoàn tất
            if (digits.Length == 4)
            {
                if (int.TryParse(digits, out int y))
                {
                    y = Math.Clamp(y, 1900, 2100);
                    digits = y.ToString("D4");
                    if (TxtYear.Text != digits)
                    {
                        TxtYear.Text = digits;
                        TxtYear.CaretIndex = digits.Length;
                    }
                }

                CommitIfValid();
                try
                {
                    Dispatcher.UIThread.Post(() => TxtYear.SelectAll(), DispatcherPriority.Input);
                }
                catch { }
            }
            else
            {
                CommitIfValid();
            }
        }
        finally
        {
            _isHandlingTextChange = false;
        }
    }

    private void OnDayKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Right)
        {
            if (TxtDay.CaretIndex >= (TxtDay.Text?.Length ?? 0) || TxtDay.SelectionStart != TxtDay.SelectionEnd)
            {
                AdvanceToMonth();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            CommitIfValid();
            e.Handled = true;
        }
    }

    private void OnMonthKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back)
        {
            if (string.IsNullOrEmpty(TxtMonth.Text) || (TxtMonth.CaretIndex == 0 && TxtMonth.SelectionStart == TxtMonth.SelectionEnd))
            {
                AdvanceToDay();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Left)
        {
            if (TxtMonth.CaretIndex == 0 || TxtMonth.SelectionStart != TxtMonth.SelectionEnd)
            {
                AdvanceToDay();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Right)
        {
            if (TxtMonth.CaretIndex >= (TxtMonth.Text?.Length ?? 0) || TxtMonth.SelectionStart != TxtMonth.SelectionEnd)
            {
                AdvanceToYear();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            CommitIfValid();
            e.Handled = true;
        }
    }

    private void OnYearKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back)
        {
            if (string.IsNullOrEmpty(TxtYear.Text) || (TxtYear.CaretIndex == 0 && TxtYear.SelectionStart == TxtYear.SelectionEnd))
            {
                AdvanceToMonth();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Left)
        {
            if (TxtYear.CaretIndex == 0 || TxtYear.SelectionStart != TxtYear.SelectionEnd)
            {
                AdvanceToMonth();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            CommitIfValid();
            e.Handled = true;
        }
    }

    public void AdvanceToDay()
    {
        try
        {
            TxtDay.Focus();
            Dispatcher.UIThread.Post(() => TxtDay.SelectAll(), DispatcherPriority.Input);
        }
        catch { }
    }

    public void AdvanceToMonth()
    {
        try
        {
            TxtMonth.Focus();
            Dispatcher.UIThread.Post(() => TxtMonth.SelectAll(), DispatcherPriority.Input);
        }
        catch { }
    }

    public void AdvanceToYear()
    {
        try
        {
            TxtYear.Focus();
            Dispatcher.UIThread.Post(() => TxtYear.SelectAll(), DispatcherPriority.Input);
        }
        catch { }
    }

    private void OnControlLostFocus(object? sender, RoutedEventArgs e)
    {
        // Kiểm tra xem focus có chuyển sang control con bên trong DateInputBox không
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused == TxtDay || focused == TxtMonth || focused == TxtYear)
            return;

        // Khi thực sự rời khỏi DateInputBox: chuẩn hóa và khôi phục nếu chưa hoàn chỉnh
        NormalizeOnBlur();
    }

    public void NormalizeOnBlur()
    {
        bool dayParsed = int.TryParse(TxtDay.Text, out int day);
        bool monthParsed = int.TryParse(TxtMonth.Text, out int month);
        bool yearParsed = int.TryParse(TxtYear.Text, out int year);

        var fallback = Value ?? DateTime.Now;

        if (!yearParsed || year < 1000)
        {
            if (yearParsed && year > 0 && year < 100)
                year += 2000;
            else
                year = fallback.Year;
        }

        year = Math.Clamp(year, 1900, 2100);

        if (!monthParsed || month < 1 || month > 12)
            month = fallback.Month;

        int maxDays = DateTime.DaysInMonth(year, month);
        if (!dayParsed || day < 1)
            day = fallback.Day;

        if (day > maxDays)
            day = maxDays;

        _isInternalUpdating = true;
        try
        {
            TxtDay.Text = day.ToString("D2");
            TxtMonth.Text = month.ToString("D2");
            TxtYear.Text = year.ToString("D4");

            var timeOfDay = Value?.TimeOfDay ?? DateTime.Now.TimeOfDay;
            Value = new DateTime(year, month, day, timeOfDay.Hours, timeOfDay.Minutes, timeOfDay.Seconds);
        }
        finally
        {
            _isInternalUpdating = false;
        }
    }

    public bool CommitIfValid()
    {
        if (!int.TryParse(TxtDay.Text, out int day) ||
            !int.TryParse(TxtMonth.Text, out int month) ||
            !int.TryParse(TxtYear.Text, out int year))
        {
            return false;
        }

        if (year < 1000 || year > 2100 || month < 1 || month > 12)
            return false;

        int maxDays = DateTime.DaysInMonth(year, month);
        if (day < 1 || day > maxDays)
        {
            // Tự động điều chỉnh ngày về số ngày tối đa hợp lệ theo tháng và năm nhuận
            day = Math.Clamp(day, 1, maxDays);
            _isInternalUpdating = true;
            try
            {
                TxtDay.Text = day.ToString("D2");
            }
            finally
            {
                _isInternalUpdating = false;
            }
        }

        _isInternalUpdating = true;
        try
        {
            var timeOfDay = Value?.TimeOfDay ?? DateTime.Now.TimeOfDay;
            Value = new DateTime(year, month, day, timeOfDay.Hours, timeOfDay.Minutes, timeOfDay.Seconds);
        }
        finally
        {
            _isInternalUpdating = false;
        }

        return true;
    }

    /// <summary>
    /// Gán trực tiếp ngày tháng năm hợp lệ và cập nhật Value.
    /// </summary>
    public void SetDate(int day, int month, int year)
    {
        year = Math.Clamp(year, 1900, 2100);
        month = Math.Clamp(month, 1, 12);
        int maxDays = DateTime.DaysInMonth(year, month);
        day = Math.Clamp(day, 1, maxDays);

        _isInternalUpdating = true;
        try
        {
            TxtDay.Text = day.ToString("D2");
            TxtMonth.Text = month.ToString("D2");
            TxtYear.Text = year.ToString("D4");

            var timeOfDay = Value?.TimeOfDay ?? DateTime.Now.TimeOfDay;
            Value = new DateTime(year, month, day, timeOfDay.Hours, timeOfDay.Minutes, timeOfDay.Seconds);
        }
        finally
        {
            _isInternalUpdating = false;
        }
    }

    public void SetDay(string day)
    {
        TxtDay.Text = day;
    }

    public void SetMonth(string month)
    {
        TxtMonth.Text = month;
    }

    public void SetYear(string year)
    {
        TxtYear.Text = year;
    }
}
