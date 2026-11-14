using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ShopHoa.Helpers;

namespace ShopHoa.Controls;

/// <summary>
/// Control nhập ngày giờ phân đoạn (Segmented DateTime Editor):
/// Định dạng: dd/MM/yyyy HH:mm
/// - Hỗ trợ chỉnh sửa riêng từng thành phần (Ngày, Tháng, Năm, Giờ, Phút).
/// - Dùng phím mũi tên Trái / Phải để chuyển đổi giữa các thành phần, không nhân đôi số.
/// - Nhấp chuột vào bất kỳ thành phần nào để chọn trực tiếp thành phần đó.
/// - Không tự động ghi đè hoặc làm nhảy con trỏ khi đang chỉnh sửa.
/// - Kiểm tra tính hợp lệ của ngày, tháng, năm nhuận tự động.
/// </summary>
public class DateTimeBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    public static readonly StyledProperty<DateTime?> ValueProperty =
        AvaloniaProperty.Register<DateTimeBox, DateTime?>(
            nameof(Value),
            defaultBindingMode: BindingMode.TwoWay);

    public DateTime? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private int _day = DateTime.Now.Day;
    private int _month = DateTime.Now.Month;
    private int _year = DateTime.Now.Year;
    private int _hour = DateTime.Now.Hour;
    private int _minute = DateTime.Now.Minute;

    private int _activeSegment = 0; // 0: dd, 1: MM, 2: yyyy, 3: HH, 4: mm
    private string _segmentBuffer = "";
    private bool _isInternalUpdating;

    public DateTimeBox()
    {
        IsReadOnly = false;
        SyncFromValue(Value ?? DateTime.Now);

        ValueProperty.Changed.AddClassHandler<DateTimeBox>(
            static (control, args) => control.OnValueChanged(args));
    }

    private void OnValueChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_isInternalUpdating)
            return;

        var newVal = args.GetNewValue<DateTime?>();
        if (newVal.HasValue)
        {
            SyncFromValue(newVal.Value);
            UpdateDisplayText(keepSelection: true);
        }
    }

    public void SyncFromValue(DateTime dt)
    {
        _year = dt.Year;
        _month = dt.Month;
        _day = dt.Day;
        _hour = dt.Hour;
        _minute = dt.Minute;
        ValidateDayInMonth();
        UpdateDisplayText(keepSelection: false);
    }

    private void ValidateDayInMonth()
    {
        _year = Math.Clamp(_year, 1900, 2100);
        _month = Math.Clamp(_month, 1, 12);
        int maxDays = DateTime.DaysInMonth(_year, _month);
        if (_day > maxDays)
            _day = maxDays;
        if (_day < 1)
            _day = 1;
        _hour = Math.Clamp(_hour, 0, 23);
        _minute = Math.Clamp(_minute, 0, 59);
    }

    private void UpdateDisplayText(bool keepSelection)
    {
        _isInternalUpdating = true;
        try
        {
            ValidateDayInMonth();
            Text = $"{_day:D2}/{_month:D2}/{_year:D4} {_hour:D2}:{_minute:D2}";
            if (keepSelection)
            {
                ApplySegmentSelection(_activeSegment);
            }
        }
        finally
        {
            _isInternalUpdating = false;
        }
    }

    public void SelectSegment(int segmentIndex)
    {
        _activeSegment = Math.Clamp(segmentIndex, 0, 4);
        _segmentBuffer = "";
        ApplySegmentSelection(_activeSegment);
    }

    private void ApplySegmentSelection(int segmentIndex)
    {
        int start = 0;
        int length = 2;
        switch (segmentIndex)
        {
            case 0: start = 0; length = 2; break;   // dd
            case 1: start = 3; length = 2; break;   // MM
            case 2: start = 6; length = 4; break;   // yyyy
            case 3: start = 11; length = 2; break;  // HH
            case 4: start = 14; length = 2; break;  // mm
        }

        Dispatcher.UIThread.Post(() =>
        {
            SelectionStart = start;
            SelectionEnd = start + length;
            CaretIndex = start + length;
        }, DispatcherPriority.Input);
    }

    public int GetSegmentFromIndex(int index)
    {
        if (index <= 2) return 0;       // dd
        if (index <= 5) return 1;       // MM
        if (index <= 10) return 2;      // yyyy
        if (index <= 13) return 3;      // HH
        return 4;                       // mm
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pos = CaretIndex;
        var seg = GetSegmentFromIndex(pos);
        SelectSegment(seg);
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        SelectSegment(_activeSegment);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!string.IsNullOrEmpty(_segmentBuffer))
        {
            if (_activeSegment == 2 && _segmentBuffer.Length > 0 && int.TryParse(_segmentBuffer, out int y))
            {
                if (y >= 1000) _year = Math.Clamp(y, 1900, 2100);
            }
            _segmentBuffer = "";
        }
        CommitAndRefresh();
    }

    public void StepSegment(int delta)
    {
        switch (_activeSegment)
        {
            case 0: // Day
                int maxDays = DateTime.DaysInMonth(_year, _month);
                _day += delta;
                if (_day > maxDays) _day = 1;
                if (_day < 1) _day = maxDays;
                break;
            case 1: // Month
                _month += delta;
                if (_month > 12) _month = 1;
                if (_month < 1) _month = 12;
                ValidateDayInMonth();
                break;
            case 2: // Year
                _year += delta;
                _year = Math.Clamp(_year, 1900, 2100);
                ValidateDayInMonth();
                break;
            case 3: // Hour
                _hour += delta;
                if (_hour > 23) _hour = 0;
                if (_hour < 0) _hour = 23;
                break;
            case 4: // Minute
                _minute += delta;
                if (_minute > 59) _minute = 0;
                if (_minute < 0) _minute = 59;
                break;
        }

        CommitAndRefresh();
        ApplySegmentSelection(_activeSegment);
    }

    public void ProcessDigit(char digit)
    {
        if (digit < '0' || digit > '9')
            return;

        switch (_activeSegment)
        {
            case 0: // Day (dd)
                _segmentBuffer += digit;
                if (_segmentBuffer.Length == 1)
                {
                    int d = digit - '0';
                    if (d > 3)
                    {
                        _day = d;
                        _segmentBuffer = "";
                        CommitAndRefresh();
                        SelectSegment(1); // Auto-advance to Month
                    }
                    else
                    {
                        _day = Math.Max(1, d);
                        CommitAndRefresh();
                        SelectionStart = 0; SelectionEnd = 2; CaretIndex = 1;
                    }
                }
                else if (_segmentBuffer.Length >= 2)
                {
                    if (int.TryParse(_segmentBuffer, out int val))
                    {
                        int maxDays = DateTime.DaysInMonth(_year, _month);
                        if (val < 1) val = 1;
                        if (val > maxDays) val = maxDays;
                        _day = val;
                    }
                    _segmentBuffer = "";
                    CommitAndRefresh();
                    SelectSegment(1); // Auto-advance to Month
                }
                break;

            case 1: // Month (MM)
                _segmentBuffer += digit;
                if (_segmentBuffer.Length == 1)
                {
                    int m = digit - '0';
                    if (m > 1)
                    {
                        _month = m;
                        _segmentBuffer = "";
                        ValidateDayInMonth();
                        CommitAndRefresh();
                        SelectSegment(2); // Auto-advance to Year
                    }
                    else
                    {
                        _month = Math.Max(1, m);
                        ValidateDayInMonth();
                        CommitAndRefresh();
                        SelectionStart = 3; SelectionEnd = 5; CaretIndex = 4;
                    }
                }
                else if (_segmentBuffer.Length >= 2)
                {
                    if (int.TryParse(_segmentBuffer, out int val))
                    {
                        val = Math.Clamp(val, 1, 12);
                        _month = val;
                        ValidateDayInMonth();
                    }
                    _segmentBuffer = "";
                    CommitAndRefresh();
                    SelectSegment(2); // Auto-advance to Year
                }
                break;

            case 2: // Year (yyyy)
                _segmentBuffer += digit;
                if (_segmentBuffer.Length < 4)
                {
                    SelectionStart = 6;
                    SelectionEnd = 10;
                    CaretIndex = 6 + _segmentBuffer.Length;
                }
                else if (_segmentBuffer.Length >= 4)
                {
                    if (int.TryParse(_segmentBuffer, out int val))
                    {
                        val = Math.Clamp(val, 1900, 2100);
                        _year = val;
                        ValidateDayInMonth();
                    }
                    _segmentBuffer = "";
                    CommitAndRefresh();
                    SelectSegment(3); // Auto-advance to Hour
                }
                break;

            case 3: // Hour (HH)
                _segmentBuffer += digit;
                if (_segmentBuffer.Length == 1)
                {
                    int h = digit - '0';
                    if (h > 2)
                    {
                        _hour = h;
                        _segmentBuffer = "";
                        CommitAndRefresh();
                        SelectSegment(4); // Auto-advance to Minute
                    }
                    else
                    {
                        _hour = h;
                        CommitAndRefresh();
                        SelectionStart = 11; SelectionEnd = 13; CaretIndex = 12;
                    }
                }
                else if (_segmentBuffer.Length >= 2)
                {
                    if (int.TryParse(_segmentBuffer, out int val))
                    {
                        val = Math.Clamp(val, 0, 23);
                        _hour = val;
                    }
                    _segmentBuffer = "";
                    CommitAndRefresh();
                    SelectSegment(4); // Auto-advance to Minute
                }
                break;

            case 4: // Minute (mm)
                _segmentBuffer += digit;
                if (_segmentBuffer.Length == 1)
                {
                    int min = digit - '0';
                    if (min > 5)
                    {
                        _minute = min;
                        _segmentBuffer = "";
                        CommitAndRefresh();
                        SelectSegment(4);
                    }
                    else
                    {
                        _minute = min;
                        CommitAndRefresh();
                        SelectionStart = 14; SelectionEnd = 16; CaretIndex = 15;
                    }
                }
                else if (_segmentBuffer.Length >= 2)
                {
                    if (int.TryParse(_segmentBuffer, out int val))
                    {
                        val = Math.Clamp(val, 0, 59);
                        _minute = val;
                    }
                    _segmentBuffer = "";
                    CommitAndRefresh();
                    SelectSegment(4);
                }
                break;
        }
    }

    private void CommitAndRefresh()
    {
        ValidateDayInMonth();
        _isInternalUpdating = true;
        try
        {
            Text = $"{_day:D2}/{_month:D2}/{_year:D4} {_hour:D2}:{_minute:D2}";
            var newDt = new DateTime(_year, _month, _day, _hour, _minute, 0);
            if (Value != newDt)
            {
                Value = newDt;
            }
        }
        finally
        {
            _isInternalUpdating = false;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right:
                if (_activeSegment < 4)
                {
                    SelectSegment(_activeSegment + 1);
                }
                e.Handled = true;
                return;

            case Key.Left:
                if (_activeSegment > 0)
                {
                    SelectSegment(_activeSegment - 1);
                }
                e.Handled = true;
                return;

            case Key.Tab:
                if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
                {
                    if (_activeSegment > 0)
                    {
                        SelectSegment(_activeSegment - 1);
                        e.Handled = true;
                        return;
                    }
                }
                else
                {
                    if (_activeSegment < 4)
                    {
                        SelectSegment(_activeSegment + 1);
                        e.Handled = true;
                        return;
                    }
                }
                // Nếu ở đầu hoặc cuối thì cho Tab chuyển sang control kế tiếp
                break;

            case Key.Up:
                StepSegment(1);
                e.Handled = true;
                return;

            case Key.Down:
                StepSegment(-1);
                e.Handled = true;
                return;

            case Key.Back:
            case Key.Delete:
                _segmentBuffer = "";
                SelectSegment(_activeSegment);
                e.Handled = true;
                return;

            case Key.Oem2: // '/'
            case Key.Divide:
            case Key.OemMinus:
            case Key.Subtract:
            case Key.OemPeriod:
            case Key.Decimal:
                if (_activeSegment < 2)
                {
                    SelectSegment(_activeSegment + 1);
                    e.Handled = true;
                    return;
                }
                break;

            case Key.Space:
                if (_activeSegment == 2)
                {
                    SelectSegment(3);
                    e.Handled = true;
                    return;
                }
                break;

            case Key.Oem1: // ':'
                if (_activeSegment == 3)
                {
                    SelectSegment(4);
                    e.Handled = true;
                    return;
                }
                break;

            case Key.Enter:
                CommitAndRefresh();
                UpdateDisplayText(keepSelection: true);
                break;

            case Key.Escape:
                _segmentBuffer = "";
                SelectSegment(_activeSegment);
                e.Handled = true;
                return;
        }

        // Nhập chữ số
        char digit = '\0';
        if (e.Key >= Key.D0 && e.Key <= Key.D9)
            digit = (char)('0' + (e.Key - Key.D0));
        else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            digit = (char)('0' + (e.Key - Key.NumPad0));

        if (digit != '\0')
        {
            ProcessDigit(digit);
            e.Handled = true;
            return;
        }

        // Cho phép phím tắt hệ thống (Ctrl+C, Ctrl+V, ...)
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            base.OnKeyDown(e);
            return;
        }

        // Chặn các phím chữ cái hoặc ký tự không hợp lệ khác
        if (e.Key is >= Key.A and <= Key.Z)
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
            return;

        // Dán chuỗi ngày giờ hoàn chỉnh
        if (e.Text.Length > 1 && NgayNhapInput.TryParse(e.Text, Value, out var parsed))
        {
            SyncFromValue(parsed);
            CommitAndRefresh();
            SelectSegment(_activeSegment);
            e.Handled = true;
            return;
        }

        if (e.Text.Length == 1 && char.IsDigit(e.Text[0]))
        {
            ProcessDigit(e.Text[0]);
            e.Handled = true;
            return;
        }

        e.Handled = true;
    }
}
