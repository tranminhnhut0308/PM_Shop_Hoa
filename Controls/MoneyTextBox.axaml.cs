using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ShopHoa.Helpers;

namespace ShopHoa.Controls;

/// <summary>
/// Ô nhập số tiền:
/// - Chưa có focus: hiển thị định dạng Việt Nam có dấu phân cách hàng nghìn (3.842.857),
///   số nguyên thì không hiện ".00".
/// - Khi nhập vẫn giữ dấu phân cách và cập nhật số tiền, đồng thời giữ vị trí con trỏ.
/// - Nội dung trống được giữ trống khi người dùng đang sửa.
/// - Khi click vào ô hoặc nhận focus: tự động bôi đen toàn bộ (Select All) để nhập số mới nhanh chóng.
/// </summary>
public partial class MoneyTextBox : UserControl
{
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<MoneyTextBox, decimal>(nameof(Value));

    public static readonly StyledProperty<decimal> MaximumProperty =
        AvaloniaProperty.Register<MoneyTextBox, decimal>(
            nameof(Maximum),
            MoneyInput.GiaTienToiDa);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<MoneyTextBox, string?>(nameof(Watermark), "0");

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<MoneyTextBox, bool>(nameof(IsReadOnly));

    public decimal Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public decimal Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public event EventHandler? ValueChanged;

    private bool _updatingValue;
    private bool _hasFocus;
    private bool _userClearedValue;
    private string _lastAcceptedText = "0";

    public MoneyTextBox()
    {
        InitializeComponent();

        ValueProperty.Changed.AddClassHandler<MoneyTextBox>(
            static (control, args) =>
            {
                control.SyncFromValue();
                control.ValueChanged?.Invoke(control, EventArgs.Empty);
            });
        IsReadOnlyProperty.Changed.AddClassHandler<MoneyTextBox>(
            static (control, args) =>
            {
                if (control.Input is not null)
                    control.Input.IsReadOnly = control.IsReadOnly;
            });

        AddHandler(InputElement.PointerPressedEvent, (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && Input is not null && !Input.IsFocused)
            {
                Input.Focus();
            }
        }, RoutingStrategies.Tunnel);

        Input.GotFocus += OnInputGotFocus;
        Input.LostFocus += OnInputLostFocus;
        Input.TextChanged += OnInputTextChanged;
        Input.KeyDown += OnInputKeyDown;
        Input.IsReadOnly = IsReadOnly;

        SyncFromValue();
    }

    public void FocusInput()
    {
        Input.Focus();
        SelectAllWhenHasContent();
    }

    public void CommitInput()
    {
        WindowsImeTextInputFix.CommitComposition(Input);
        CommitFromInputText();
    }

    private void OnInputGotFocus(object? sender, RoutedEventArgs e)
    {
        _hasFocus = true;
        SelectAllWhenHasContent();
    }

    private void SelectAllWhenHasContent()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Input is not null && !string.IsNullOrEmpty(Input.Text))
            {
                Input.SelectAll();
            }
        }, DispatcherPriority.Input);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;

            CommitInput();

            TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        }
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        _hasFocus = false;

        WindowsImeTextInputFix.CommitComposition(Input);

        CommitFromInputText();
    }

    private void CommitFromInputText()
    {
        var raw = (Input.Text ?? string.Empty).Trim();
        if (MoneyInput.TryParse(raw, false, out var parsed, out _))
        {
            Value = parsed;
        }

        var formatted = MoneyInput.Format(Value);
        _updatingValue = true;
        try
        {
            if (Input.Text != formatted)
                Input.Text = formatted;
            _lastAcceptedText = formatted;
            _userClearedValue = false;
        }
        finally
        {
            _updatingValue = false;
        }
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingValue)
            return;

        var source = Input.Text ?? string.Empty;
        var sourceCaret = Input.CaretIndex;
        var transient = MoneyInput.IsTransientComposition(source, sourceCaret);
        if (!MoneyInput.TryFormatEditingText(
                source,
                sourceCaret,
                Maximum,
                out var formatted,
                out var caret))
        {
            RestoreLastAcceptedText();
            return;
        }

        _updatingValue = true;
        try
        {
            if (Input.Text != formatted)
                Input.Text = formatted;
            Input.CaretIndex = caret;
            _userClearedValue = formatted.Length == 0;

            if (formatted.Length == 0)
            {
                Value = 0m;
            }
            else if (formatted != "," &&
                     MoneyInput.TryParse(formatted, false, out var value, out _))
            {
                Value = value;
            }

            if (!transient)
                _lastAcceptedText = formatted;
        }
        finally
        {
            _updatingValue = false;
        }
    }

    /// <summary>Đồng bộ hiển thị khi giá trị thay đổi từ ViewModel (nạp phiếu cũ, tính lại tổng...).</summary>
    private void SyncFromValue()
    {
        if (_hasFocus)
            return;

        var formatted = MoneyInput.Format(Value);

        _updatingValue = true;

        try
        {
            if (Input is not null && Input.Text != formatted)
                Input.Text = formatted;
            _lastAcceptedText = formatted;
            _userClearedValue = false;
        }
        finally
        {
            _updatingValue = false;
        }
    }

    private void RestoreLastAcceptedText()
    {
        _updatingValue = true;
        try
        {
            Input.Text = _lastAcceptedText;
            Input.CaretIndex = _lastAcceptedText.Length;
        }
        finally
        {
            _updatingValue = false;
        }
    }

}
