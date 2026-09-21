<<<<<<< HEAD
=======
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
>>>>>>> 08456af (Them chuc nang nhap gia ban)
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class MainWindow : Window
{
    private bool _formattingDiscount;

    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainWindowViewModel
        {
            OwnerWindow = this
        };

        DataContext = viewModel;

        this.Opened += async (_, _) =>
        {
            await viewModel.InitializeAsync();
        };
    }

    private void OnSaleSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var text = (sender as TextBox)?.Text ?? string.Empty;
        viewModel.SaleSearchText = text;

        if (viewModel.TimKiemHoaBanCommand.CanExecute(text))
        {
            _ = viewModel.TimKiemHoaBanCommand.ExecuteAsync(text);
        }
    }

    private void OnSaleCustomerSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var text = (sender as TextBox)?.Text ?? string.Empty;
        viewModel.SaleCustomerName = text;

        if (text == "Khách lẻ")
        {
            viewModel.SelectedSaleCustomer = null;
            viewModel.SaleCustomerSuggestions.Clear();
            return;
        }

        if (viewModel.SelectedSaleCustomer?.TenKhachHang == text)
            return;

        viewModel.SelectedSaleCustomer = null;
        _ = viewModel.SearchSaleCustomersAsync(text);
    }

    private void OnSaleDiscountTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_formattingDiscount || sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return;

        var normalized = text
            .Replace(".", string.Empty)
            .Replace(",", string.Empty);

        if (!decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount))
            return;

        var formatted = amount.ToString(
            "N0",
            CultureInfo.GetCultureInfo("vi-VN"));

        if (formatted == text)
            return;

        _formattingDiscount = true;

        textBox.Text = formatted;
        textBox.CaretIndex = formatted.Length;

        _formattingDiscount = false;
    }

    private void OnCustomerSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var text = (sender as TextBox)?.Text ?? string.Empty;
        viewModel.CustomerSearch = text;

        if (viewModel.SearchCustomersCommand.CanExecute(text))
        {
            _ = viewModel.SearchCustomersCommand.ExecuteAsync(text);
        }
    }

    private void OnCustomerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.SelectedKhachHang =
            (sender as ListBox)?.SelectedItem as ShopHoa.Models.KhachHang;
    }
<<<<<<< HEAD
}
=======


    // ============================================================
    // XUẤT DANH SÁCH HÀNG HÓA
    // ============================================================

    private async void OnExportProductsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                await ShowMessageAsync(
                    "Thông báo",
                    "Không tìm thấy dữ liệu sản phẩm.");
                return;
            }

            if (viewModel.SanPhams == null || viewModel.SanPhams.Count == 0)
            {
                await ShowMessageAsync(
                    "Thông báo",
                    "Không có sản phẩm để xuất file.");
                return;
            }

            // ====================================================
            // CHỌN NƠI LƯU FILE
            // ====================================================

            var file = await StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Xuất danh sách hàng hóa",

                    SuggestedFileName =
                        $"DanhSachSanPham_{DateTime.Now:yyyyMMdd_HHmmss}.csv",

                    DefaultExtension = "csv",

                    FileTypeChoices =
                    [
                        new FilePickerFileType("Excel CSV")
                        {
                            Patterns = ["*.csv"]
                        }
                    ]
                });

            if (file == null)
                return;


            // ====================================================
            // TẠO FILE CSV
            // ====================================================

            var csv = new StringBuilder();

            // UTF-8 BOM để Excel hiển thị tiếng Việt đúng
            csv.Append('\uFEFF');


            // ====================================================
            // HEADER
            // ====================================================

            csv.AppendLine(
                "Mã hàng," +
                "Tên hàng," +
                "Loại hàng," +
                "Nhóm hàng," +
                "Giá bán," +
                "Giá vốn," +
                "Tồn kho," +
                "Khách đặt," +
                "ĐVT," +
                "Thời gian tạo," +
                "Dự kiến hết hàng");


            // ====================================================
            // DỮ LIỆU SẢN PHẨM
            // ====================================================

            foreach (var sanPham in viewModel.SanPhams)
            {
                var maSanPham =
                    EscapeCsv(sanPham.MaSanPham);

                var tenSanPham =
                    EscapeCsv(sanPham.TenSanPham);

                var loaiHang =
                    EscapeCsv(sanPham.LoaiHang);

                var nhomHang =
                    EscapeCsv(sanPham.NhomHangBaCap);

                var giaBan =
                    sanPham.GiaBan.ToString(
                        "N0",
                        CultureInfo.GetCultureInfo("vi-VN"));

                // Dùng Reflection để nếu model có GiaVon thì lấy,
                // nếu chưa có thì để trống, tránh lỗi build.
                var giaVonValue =
                    GetPropertyValue(sanPham, "GiaVon");

                var giaVon = FormatNumber(giaVonValue);

                var tonKho =
                    sanPham.TonKhoExcel.ToString(
                        "N0",
                        CultureInfo.GetCultureInfo("vi-VN"));

                var khachDat =
                    sanPham.KhachDat.ToString(
                        "N0",
                        CultureInfo.GetCultureInfo("vi-VN"));

                var donViTinh =
                    EscapeCsv(sanPham.DonViTinh);

                var ngayTao =
    EscapeCsv(sanPham.NgayTao);

                var duKienHetHang =
                    EscapeCsv(sanPham.DuKienHetHang);


                csv.AppendLine(
                    $"{maSanPham}," +
                    $"{tenSanPham}," +
                    $"{loaiHang}," +
                    $"{nhomHang}," +
                    $"{giaBan}," +
                    $"{giaVon}," +
                    $"{tonKho}," +
                    $"{khachDat}," +
                    $"{donViTinh}," +
                    $"{ngayTao}," +
                    $"{duKienHetHang}");
            }


            // ====================================================
            // GHI FILE
            // ====================================================

            await using var stream =
                await file.OpenWriteAsync();

            using var writer =
                new StreamWriter(
                    stream,
                    new UTF8Encoding(true));

            await writer.WriteAsync(csv.ToString());

            await writer.FlushAsync();


            // ====================================================
            // THÔNG BÁO THÀNH CÔNG
            // ====================================================

            await ShowMessageAsync(
                "Xuất file thành công",
                $"Đã xuất {viewModel.SanPhams.Count:N0} sản phẩm.\n\n" +
                $"File đã được lưu tại:\n{file.Name}");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                "Lỗi xuất file",
                $"Không thể xuất danh sách sản phẩm.\n\n" +
                $"Chi tiết lỗi:\n{ex.Message}");
        }
    }


    // ============================================================
    // ESCAPE CSV
    // ============================================================

    private static string EscapeCsv(object? value)
    {
        if (value == null)
            return "";

        var text = Convert.ToString(
            value,
            CultureInfo.InvariantCulture) ?? "";

        // CSV cần đặt trong dấu " nếu có:
        // dấu phẩy
        // xuống dòng
        // dấu "
        if (text.Contains(',') ||
            text.Contains('"') ||
            text.Contains('\n') ||
            text.Contains('\r'))
        {
            text = text.Replace(
                "\"",
                "\"\"");

            return $"\"{text}\"";
        }

        return text;
    }


    // ============================================================
    // LẤY PROPERTY BẰNG REFLECTION
    // ============================================================

    private static object? GetPropertyValue(
        object? obj,
        string propertyName)
    {
        if (obj == null)
            return null;

        var property =
            obj.GetType().GetProperty(propertyName);

        return property?.GetValue(obj);
    }


    // ============================================================
    // FORMAT SỐ
    // ============================================================

    private static string FormatNumber(object? value)
    {
        if (value == null)
            return "";

        if (value is decimal decimalValue)
        {
            return decimalValue.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        if (value is double doubleValue)
        {
            return doubleValue.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        if (value is float floatValue)
        {
            return floatValue.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        if (value is int intValue)
        {
            return intValue.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        if (value is long longValue)
        {
            return longValue.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        return EscapeCsv(value);
    }


    // ============================================================
    // MESSAGE BOX
    // ============================================================

    private async Task ShowMessageAsync(
        string title,
        string message)
    {
        var window = new Window
        {
            Title = title,
            Width = 500,
            Height = 240,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 100,
            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 15, 0, 0)
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(25)
        };

        panel.Children.Add(
            new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontSize = 15
            });

        panel.Children.Add(okButton);

        window.Content = panel;

        okButton.Click += (_, _) =>
        {
            window.Close();
        };

        await window.ShowDialog(this);
    }
}
>>>>>>> 08456af (Them chuc nang nhap gia ban)
