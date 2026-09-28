using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ShopHoa.Converters;

/// <summary>
/// Đổi màu ngôi sao "Yêu thích": vàng khi đã đánh dấu, xám khi chưa.
/// </summary>
public sealed class YeuThichConverter : IValueConverter
{
    public static YeuThichConverter Instance { get; } = new();

    private static readonly IBrush DaYeuThich = new SolidColorBrush(Color.Parse("#F5B301"));
    private static readonly IBrush ChuaYeuThich = new SolidColorBrush(Color.Parse("#D5DEDB"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? DaYeuThich : ChuaYeuThich;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
