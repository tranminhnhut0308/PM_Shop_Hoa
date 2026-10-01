using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ShopHoa.ViewModels;
namespace ShopHoa.Views;

public partial class MainWindow : Window
{
    private bool _formattingDiscount;
    private bool _formattingCash;

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
    private async void OpenSettingsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var settingsDialog = new CategorySettingsWindow();
        await settingsDialog.ShowDialog(this);

        // Tải lại danh sách nhóm hàng & ĐVT trong ViewModel nếu cần
        // viewModel.ReloadProductOptions();
    }

    private async void LogoutButton_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        await viewModel.DangXuatCommand.ExecuteAsync(null);
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

    private void OnSaleCustomerSearchTextChanged(
    object? sender,
    TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? string.Empty;

        viewModel.SaleCustomerName = text;

        if (text.Equals(
                "Khách lẻ",
                StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedSaleCustomer = null;
            viewModel.SaleCustomerSuggestions.Clear();
            return;
        }

        if (viewModel.SelectedSaleCustomer is not null &&
            !viewModel.SelectedSaleCustomer.TenKhachHang.Equals(
                text,
                StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedSaleCustomer = null;
        }

        // Tìm realtime
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

    private void OnImportProductSearchTextChanged(
    object? sender,
    TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        viewModel.ImportProductSearch = textBox.Text ?? string.Empty;

        if (viewModel.TimKiemSanPhamNhapCommand.CanExecute(null))
        {
            _ = viewModel.TimKiemSanPhamNhapCommand.ExecuteAsync(textBox.Text);
        }
    }

    private void OnImportSupplierSearchTextChanged(
    object? sender,
    TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        viewModel.ImportSupplierSearch = textBox.Text ?? string.Empty;

        if (viewModel.TimKiemNhaCungCapNhapCommand.CanExecute(null))
        {
            _ = viewModel.TimKiemNhaCungCapNhapCommand.ExecuteAsync(textBox.Text);
        }
    }

    private void OnPhieuNhapNgayNhapBoxPointerPressed(
    object? sender,
        PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        // Ô ngày nhập chỉ để hiển thị, bấm vào để mở lịch chọn ngày
        viewModel.MoLichPhieuNhapCommand.Execute(null);
    }

    private void OnBaoCaoPhieuNhapSearchTextChanged(
    object? sender,
        TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        viewModel.BaoCaoPhieuNhapSearch = textBox.Text ?? string.Empty;

        viewModel.TimKiemBaoCaoPhieuNhapTuDong();
    }

    private void OnBaoCaoPhieuNhapChonTatCaChanged(
    object? sender,
    RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not CheckBox checkBox)
            return;

        viewModel.ChonTatCaBaoCaoPhieuNhapCommand.Execute(checkBox.IsChecked);
    }

    private void OnBaoCaoPhieuNhapRowChecked(
    object? sender,
    RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.CapNhatTrangThaiChonTatCaBaoCaoPhieuNhap();
    }

    private void OnBaoCaoPhieuNhapPageSizeChanged(
    object? sender,
    SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not ComboBox comboBox)
            return;

        if (!int.TryParse(comboBox.SelectedItem?.ToString(), out var pageSize) || pageSize <= 0)
            return;

        viewModel.BaoCaoPhieuNhapPageSize = pageSize;
        viewModel.TimKiemBaoCaoPhieuNhapTuDong();
    }

    private void OnCustomerSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var text = (sender as TextBox)?.Text ?? string.Empty;
        viewModel.CustomerSearch = text;

        // Tìm kiếm realtime: ViewModel tự debounce 350ms
        // để không gọi database liên tục khi người dùng gõ.
        viewModel.TimKiemKhachHangTuDong();
    }

    private void OnCustomerPageSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not ComboBox comboBox)
            return;

        if (!int.TryParse(comboBox.SelectedItem?.ToString(), out var pageSize) || pageSize <= 0)
            return;

        // Bỏ qua khi giá trị không đổi (kể cả lúc giao diện vừa khởi tạo)
        if (viewModel.CustomerPageSize == pageSize)
            return;

        // ViewModel tự quay về trang 1 khi đổi số bản ghi mỗi trang
        viewModel.CustomerPageSize = pageSize;

        _ = viewModel.TaiLaiDanhSachKhachHangAsync();
    }

    /// <summary>
    /// Click vào dòng khách hàng: mở / đóng / chuyển vùng chi tiết ngay dưới dòng đó.
    /// Bấm bên trong vùng chi tiết hoặc lên nút bấm thì không đóng/mở lại dòng.
    /// </summary>
    private void OnCustomerRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        var row = TimDongKhachHang(e.Source as StyledElement, out var benTrongChiTiet);

        if (row is null || benTrongChiTiet)
            return;

        viewModel.MoRongDongKhachHangCommand.Execute(row);
    }

    /// <summary>
    /// Click vào bảng thống kê (tổng chi tiêu / công nợ): mở đúng tab tương ứng
    /// của vùng chi tiết đang mở.
    /// </summary>
    private void OnCustomerSummaryPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var tab = (sender as Control)?.Tag?.ToString();

        if (viewModel.ExpandedCustomer is null)
            return;

        // Mở đúng tab của vùng chi tiết đang mở
        viewModel.SelectedCustomer?.Detail?.ChonTabCommand.Execute(
            tab == "congno" ? "congno" : "lichsu");
    }

    /// <summary>
    /// Đi từ phần tử được bấm lên trên để tìm dòng khách hàng chứa nó.
    /// Báo thêm nếu phần tử nằm bên trong vùng chi tiết đã mở.
    /// </summary>
    private static KhachHangRowViewModel? TimDongKhachHang(
        StyledElement? element,
        out bool benTrongChiTiet)
    {
        benTrongChiTiet = false;

        while (element is not null)
        {
            if (element.Name == "DetailPanel")
                benTrongChiTiet = true;

            // Không coi thao tác bấm nút bên trong dòng là mở/đóng dòng
            if (element is Button or ComboBox or TextBox or Avalonia.Controls.Primitives.ToggleButton)
                benTrongChiTiet = true;

            if (element.DataContext is KhachHangRowViewModel row)
                return row;

            element = element.Parent as StyledElement;
        }

        return null;
    }

    // ============================================================
    // NHÀ CUNG CẤP
    // ============================================================

    /// <summary>
    /// Gõ từ khóa tìm nhà cung cấp: ViewModel tự debounce 350ms
    /// để không gọi database liên tục.
    /// </summary>
    private void OnSupplierSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.SupplierSearch = (sender as TextBox)?.Text ?? string.Empty;

        viewModel.TimKiemNhaCungCapTuDong();
    }

    private void OnSupplierPageSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not ComboBox comboBox)
            return;

        if (!int.TryParse(comboBox.SelectedItem?.ToString(), out var pageSize) || pageSize <= 0)
            return;

        // Bỏ qua khi giá trị không đổi (kể cả lúc giao diện vừa khởi tạo)
        if (viewModel.SupplierPageSize == pageSize)
            return;

        viewModel.SupplierPageSize = pageSize;
        viewModel.SupplierPage = 1;

        _ = viewModel.LoadSupplierPageAsync();
    }

    /// <summary>
    /// Click vào dòng nhà cung cấp: mở / đóng / chuyển vùng chi tiết
    /// ngay dưới dòng đó. Bấm bên trong vùng chi tiết hoặc lên nút bấm
    /// thì không đóng/mở lại dòng.
    /// </summary>
    private void OnSupplierRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        var row = TimDongNhaCungCap(e.Source as StyledElement, out var benTrongChiTiet);

        if (row is null || benTrongChiTiet)
            return;

        viewModel.ToggleSupplierDetailCommand.Execute(row);
    }

    /// <summary>
    /// Click vào ô thống kê nhanh: mở đúng tab tương ứng của vùng chi tiết
    /// đang mở.
    /// </summary>
    private void OnSupplierSummaryPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var tab = (sender as Control)?.Tag?.ToString();

        if (viewModel.ExpandedSupplierRow?.Detail is null)
            return;

        viewModel.ExpandedSupplierRow.Detail.ChonTabCommand.Execute(
            tab == "congno" ? "congno" : "lichsu");
    }

    /// <summary>
    /// Đi từ phần tử được bấm lên trên để tìm dòng nhà cung cấp chứa nó.
    /// Báo thêm nếu phần tử nằm bên trong vùng chi tiết đã mở.
    /// </summary>
    private static NhaCungCapRowViewModel? TimDongNhaCungCap(
        StyledElement? element,
        out bool benTrongChiTiet)
    {
        benTrongChiTiet = false;

        while (element is not null)
        {
            if (element.Name == "DetailPanel")
                benTrongChiTiet = true;

            // Không coi thao tác bấm nút/ô nhập bên trong dòng là mở/đóng dòng
            if (element is Button or ComboBox or TextBox or Avalonia.Controls.Primitives.ToggleButton)
                benTrongChiTiet = true;

            if (element.DataContext is NhaCungCapRowViewModel row)
                return row;

            element = element.Parent as StyledElement;
        }

        return null;
    }

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

    private void OnTienKhachDuaTextChangedUI(
    object? sender,
    TextChangedEventArgs e)
    {
        if (_formattingCash || sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? string.Empty;

        // Cho phép xóa trắng
        if (string.IsNullOrWhiteSpace(text))
        {
            if (DataContext is MainWindowViewModel vm)
                vm.TienKhachDua = 0;

            return;
        }

        // Bỏ dấu phân cách hàng nghìn
        var normalized = text
            .Replace(".", "")
            .Replace(",", "");

        // Chỉ nhận số
        if (!decimal.TryParse(
                normalized,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return;
        }

        // Cập nhật số thật
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.TienKhachDua = amount;
        }

        // Format giống ô Giảm giá
        var formatted = amount.ToString(
            "N0",
            CultureInfo.GetCultureInfo("vi-VN"));

        if (formatted == text)
            return;

        _formattingCash = true;

        textBox.Text = formatted;

        // Đưa con trỏ về cuối
        textBox.CaretIndex = formatted.Length;

        _formattingCash = false;
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
