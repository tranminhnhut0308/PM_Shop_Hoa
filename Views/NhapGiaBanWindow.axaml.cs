using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ShopHoa.Helpers;

namespace ShopHoa.Views;

public partial class NhapGiaBanWindow : Window
{
    private readonly TextBox _giaBanInput;
    private bool _daDong;

    public decimal? GiaBan { get; private set; }

    public NhapGiaBanWindow()
    {
        InitializeComponent();

        _giaBanInput = NameScopeExtensions.Find<TextBox>(GiaBanTextBox, "Input")
            ?? throw new InvalidOperationException("Không tìm thấy ô nhập giá bán.");

        AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Dispatcher.UIThread.Post(() => GiaBanTextBox.FocusInput(), DispatcherPriority.Input);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var focusOnInput = ReferenceEquals(focused, _giaBanInput);

        if (e.Key == Key.Escape)
        {
            HuyClicked(sender, e);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && (focusOnInput || focused is null))
        {
            LuuClicked(sender, e);
            e.Handled = true;
        }
    }

    private void LuuClicked(object? sender, RoutedEventArgs e)
    {
        if (_daDong)
            return;

        GiaBanTextBox.CommitInput();

        var giaBan = GiaBanTextBox.Value;

        if (giaBan <= 0 || giaBan > MoneyInput.GiaTienToiDa)
        {
            GiaBanTextBox.FocusInput();
            return;
        }

        GiaBan = giaBan;
        _daDong = true;
        Close(GiaBan);
    }

    private void HuyClicked(object? sender, RoutedEventArgs e)
    {
        if (_daDong)
            return;

        GiaBan = null;
        _daDong = true;
        Close(null);
    }
}
