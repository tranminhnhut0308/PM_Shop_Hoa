using System.Globalization;
using Avalonia.Controls;
using ShopHoa.Models;
using System.Collections.Generic;

namespace ShopHoa.Views;

public partial class ProductEditorWindow : Window
{
    private readonly SanPham _product;
    private readonly bool _isNew;

    public SanPham Product => _product;

    public ProductEditorWindow() : this(null)
    {
    }

    public ProductEditorWindow(
        SanPham? product = null,
        IReadOnlyList<ProductOption>? groups = null,
        IReadOnlyList<ProductOption>? units = null)
    {
        InitializeComponent();
        _isNew = product is null;
        _product = product ?? new SanPham
        {
            DuocBanTrucTiep = true
        };

        NhomSanPhamComboBox.ItemsSource = groups ?? [];
        DonViTinhComboBox.ItemsSource = units ?? [];

        Title = _isNew ? "Thêm sản phẩm" : "Sửa sản phẩm";
        LoadProduct();
    }

    private void LoadProduct()
    {
        MaSanPhamTextBox.Text = _product.MaSanPham;
        TenSanPhamTextBox.Text = _product.TenSanPham;
        MaVachTextBox.Text = _product.MaVach ?? "";
        NhomSanPhamComboBox.SelectedItem = FindOption(NhomSanPhamComboBox.ItemsSource, _product.NhomSanPhamId);
        DonViTinhComboBox.SelectedItem = FindOption(DonViTinhComboBox.ItemsSource, _product.DonViTinhId);
        GiaBanTextBox.Text = _product.GiaBan.ToString(CultureInfo.InvariantCulture);
        GiaVonTextBox.Text = _product.GiaVon.ToString(CultureInfo.InvariantCulture);
        TonToiThieuTextBox.Text = _product.TonToiThieu.ToString(CultureInfo.InvariantCulture);
        TonToiDaTextBox.Text = _product.TonToiDa.ToString(CultureInfo.InvariantCulture);
        ViTriKhoTextBox.Text = _product.ViTriKho ?? "";
        MoTaTextBox.Text = _product.MoTa ?? "";
        GhiChuTextBox.Text = _product.GhiChu ?? "";
        DuocBanTrucTiepCheckBox.IsChecked = _product.DuocBanTrucTiep;
        DangKinhDoanhCheckBox.IsChecked = _product.DangKinhDoanh;
    }

    private void SaveButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MaSanPhamTextBox.Text) ||
            string.IsNullOrWhiteSpace(TenSanPhamTextBox.Text))
        {
            ErrorTextBlock.Text = "Mã và tên sản phẩm là bắt buộc.";
            return;
        }

        if (!TryReadDecimal(GiaBanTextBox.Text, out var giaBan) ||
            !TryReadDecimal(GiaVonTextBox.Text, out var giaVon) ||
            !TryReadDecimal(TonToiThieuTextBox.Text, out var tonToiThieu) ||
            !TryReadDecimal(TonToiDaTextBox.Text, out var tonToiDa))
        {
            ErrorTextBlock.Text = "Giá và số lượng phải là số hợp lệ.";
            return;
        }

        _product.MaSanPham = MaSanPhamTextBox.Text.Trim();
        _product.TenSanPham = TenSanPhamTextBox.Text.Trim();
        _product.MaVach = NullIfEmpty(MaVachTextBox.Text);
        _product.NhomSanPhamId = (NhomSanPhamComboBox.SelectedItem as ProductOption)?.Id;
        _product.DonViTinhId = (DonViTinhComboBox.SelectedItem as ProductOption)?.Id;
        _product.GiaBan = giaBan;
        _product.GiaVon = giaVon;
        _product.TonToiThieu = tonToiThieu;
        _product.TonToiDa = tonToiDa;
        _product.ViTriKho = NullIfEmpty(ViTriKhoTextBox.Text);
        _product.MoTa = NullIfEmpty(MoTaTextBox.Text);
        _product.GhiChu = NullIfEmpty(GhiChuTextBox.Text);
        _product.DuocBanTrucTiep = DuocBanTrucTiepCheckBox.IsChecked == true;
        _product.DangKinhDoanh = DangKinhDoanhCheckBox.IsChecked == true;
        Close(true);
    }

    private static ProductOption? FindOption(object? source, uint? id)
    {
        if (id is null || source is not IEnumerable<ProductOption> options)
            return null;

        return options.FirstOrDefault(option => option.Id == id.Value);
    }

    private static bool TryReadDecimal(string? value, out decimal result)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result) ||
               decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
