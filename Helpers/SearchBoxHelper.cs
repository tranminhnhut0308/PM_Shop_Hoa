using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace ShopHoa.Helpers;

/// <summary>
/// Helper cung cấp tính năng nút xóa nhanh (Clear X) và tự động bôi đen (Select All)
/// khi Focus cho tất cả các thanh tìm kiếm trong toàn bộ ứng dụng (Issue #126).
/// </summary>
public static class SearchBoxHelper
{
    public static readonly AttachedProperty<bool> IsSearchBoxProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>(
            "IsSearchBox",
            typeof(SearchBoxHelper),
            defaultValue: false);

    private static readonly AttachedProperty<bool> IsAttachedProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>(
            "IsAttached",
            typeof(SearchBoxHelper),
            defaultValue: false);

    static SearchBoxHelper()
    {
        IsSearchBoxProperty.Changed.AddClassHandler<TextBox>(OnIsSearchBoxChanged);
    }

    public static bool GetIsSearchBox(TextBox textBox) => textBox.GetValue(IsSearchBoxProperty);

    public static void SetIsSearchBox(TextBox textBox, bool value) => textBox.SetValue(IsSearchBoxProperty, value);

    private static void OnIsSearchBoxChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Attach(textBox);
        }
    }

    private static void Attach(TextBox textBox)
    {
        if (textBox.GetValue(IsAttachedProperty))
            return;

        textBox.SetValue(IsAttachedProperty, true);

        // 1. Tạo nút Xóa nhanh (Clear X) đặt vào InnerRightContent
        var clearButton = new Button
        {
            Content = "✕",
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 0),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
            Focusable = false,
            IsVisible = !string.IsNullOrEmpty(textBox.Text)
        };

        ToolTip.SetTip(clearButton, "Xóa từ khóa tìm kiếm");

        clearButton.Click += (s, e) =>
        {
            textBox.Text = string.Empty;
            textBox.Focus();
        };

        // Đổi màu khi hover qua nút X
        clearButton.PointerEntered += (s, e) =>
        {
            clearButton.Foreground = new SolidColorBrush(Color.Parse("#087F6E"));
        };
        clearButton.PointerExited += (s, e) =>
        {
            clearButton.Foreground = new SolidColorBrush(Color.Parse("#94A3B8"));
        };

        // Gắn nút X vào InnerRightContent của TextBox
        textBox.InnerRightContent = clearButton;

        // Cập nhật ẩn/hiện nút X theo nội dung ô tìm kiếm
        textBox.TextChanged += (s, e) =>
        {
            clearButton.IsVisible = !string.IsNullOrEmpty(textBox.Text);
        };

        // 2. Tự động bôi đen khi Focus (Click hoặc Tab)
        var shouldSelectAllOnPointerUp = false;

        textBox.AddHandler(InputElement.PointerPressedEvent, (s, e) =>
        {
            if (!textBox.IsFocused)
            {
                shouldSelectAllOnPointerUp = true;
            }
        }, RoutingStrategies.Tunnel);

        textBox.GotFocus += (s, e) =>
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (textBox.IsFocused && !string.IsNullOrEmpty(textBox.Text))
                    {
                        textBox.SelectAll();
                    }
                }, DispatcherPriority.Input);
            }
        };

        textBox.AddHandler(InputElement.PointerReleasedEvent, (s, e) =>
        {
            if (shouldSelectAllOnPointerUp)
            {
                shouldSelectAllOnPointerUp = false;
                if (!string.IsNullOrEmpty(textBox.Text))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (textBox.IsFocused && !string.IsNullOrEmpty(textBox.Text))
                        {
                            textBox.SelectAll();
                        }
                    }, DispatcherPriority.Input);
                }
            }
        }, RoutingStrategies.Bubble);

        textBox.LostFocus += (s, e) =>
        {
            shouldSelectAllOnPointerUp = false;
        };
    }
}
