using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class ChinhSoLuongNhanhWindow : Window
{
    public KetQuaChinhSoLuong? KetQua { get; private set; }
    private bool _updatingSoLuong;

    public ChinhSoLuongNhanhWindow()
    {
        InitializeComponent();

        SoLuongTextBox.GotFocus += (s, e) => Dispatcher.UIThread.Post(() => {
            if (!string.IsNullOrEmpty(SoLuongTextBox.Text)) SoLuongTextBox.SelectAll();
        }, DispatcherPriority.Input);
        SoLuongTextBox.AddHandler(InputElement.PointerReleasedEvent, (s, e) => {
            if (e.InitialPressMouseButton == MouseButton.Left)
                Dispatcher.UIThread.Post(() => {
                    if (!string.IsNullOrEmpty(SoLuongTextBox.Text)) SoLuongTextBox.SelectAll();
                }, DispatcherPriority.Input);
        }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        SoLuongTextBox.TextChanged += SoLuongTextBox_TextChanged;
    }

    private void SoLuongTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingSoLuong) return;
        var src = SoLuongTextBox.Text ?? "";
        if (MoneyInput.TryFormatEditingText(src, SoLuongTextBox.CaretIndex, 999999, out var formatted, out var caret))
        {
            _updatingSoLuong = true;
            try
            {
                if (SoLuongTextBox.Text != formatted)
                    SoLuongTextBox.Text = formatted;
                SoLuongTextBox.CaretIndex = caret;
            }
            finally
            {
                _updatingSoLuong = false;
            }
        }
    }

    public ChinhSoLuongNhanhWindow(SanPham product) : this()
    {
        TenSanPhamText.Text = product.TenSanPham;
        MaSanPhamText.Text = string.IsNullOrWhiteSpace(product.MaSanPham) ? "" : $"Mã hàng: {product.MaSanPham}";

        // Gợi ý mặc định 10 đơn vị tồn kho
        SoLuongTextBox.Text = "10";
        SoLuongTextBox.Focus();
        SoLuongTextBox.SelectAll();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Enter)
        {
            LuuClicked();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HuyClicked();
            e.Handled = true;
        }
    }

    private void QuickSetClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string valStr)
        {
            SoLuongTextBox.Text = valStr;
            LoiTextBlock.IsVisible = false;
            SoLuongTextBox.Focus();
            SoLuongTextBox.SelectAll();
        }
    }

    private void LuuClicked(object? sender = null, RoutedEventArgs? e = null)
    {
        var text = SoLuongTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            LoiTextBlock.Text = "Vui lòng nhập số lượng tồn kho.";
            LoiTextBlock.IsVisible = true;
            SoLuongTextBox.Focus();
            return;
        }

        // Bỏ dấu chấm phân cách hàng nghìn nếu có
        text = text.Replace(".", "").Replace(",", ".");

        if (!decimal.TryParse(
                text,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var soLuong)
            || soLuong <= 0)
        {
            LoiTextBlock.Text = "Số lượng phải là số lớn hơn 0.";
            LoiTextBlock.IsVisible = true;
            SoLuongTextBox.Focus();
            return;
        }

        KetQua = new KetQuaChinhSoLuong
        {
            SoLuongTonMoi = soLuong,
            ThemVaoGioHang = ThemVaoGioHangCheckBox.IsChecked ?? true
        };

        Close(KetQua);
    }

    private void HuyClicked(object? sender = null, RoutedEventArgs? e = null)
    {
        KetQua = null;
        Close(null);
    }
}

public sealed class KetQuaChinhSoLuong
{
    public decimal SoLuongTonMoi { get; set; }
    public bool ThemVaoGioHang { get; set; } = true;
}
