using Avalonia.Controls;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;
using System.Globalization;

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

        var normalized = text.Replace(".", string.Empty).Replace(",", string.Empty);
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return;

        var formatted = amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
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
            _ = viewModel.SearchCustomersCommand.ExecuteAsync(text);
    }

    private void OnCustomerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.SelectedKhachHang = (sender as ListBox)?.SelectedItem as ShopHoa.Models.KhachHang;
    }
}
