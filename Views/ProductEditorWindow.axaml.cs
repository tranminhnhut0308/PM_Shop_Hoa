using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class ProductEditorWindow : Window
{
    private readonly SanPham _product;
    private readonly bool _isNew;
    private readonly bool _duocSuaGiaBan;
    private bool _dangDinhDangTien;

    public SanPham Product => _product;

    public ProductEditorWindow() : this(null)
    {
    }

    public ProductEditorWindow(
        SanPham? product = null,
        IReadOnlyList<ProductOption>? groups = null,
        IReadOnlyList<ProductOption>? units = null,
        bool duocSuaGiaBan = true)
    {
        InitializeComponent();

        _duocSuaGiaBan = duocSuaGiaBan;
        _isNew = product is null;
        _product = product ?? new SanPham
        {
            MaSanPham = GenerateProductCode(),
            DuocBanTrucTiep = true
        };

        NhomSanPhamComboBox.ItemsSource = groups ?? [];
        DonViTinhComboBox.ItemsSource = units ?? [];

        // Không có quyền sửa hàng hóa thì không cho gõ giá bán,
        // nhưng vẫn xem và sửa các thông tin khác của sản phẩm.
        GiaBanTextBox.IsEnabled = _duocSuaGiaBan;
        GiaBanTextBox.IsReadOnly = !_duocSuaGiaBan;
        ToolTip.SetTip(
            GiaBanTextBox,
            _duocSuaGiaBan
                ? "Nhập số tiền, ví dụ 50.000 hoặc 1500000"
                : "Bạn không có quyền chỉnh giá bán");
        GiaBanTextBox.GotFocus += GiaBanTextBox_OnGotFocus;
        GiaBanTextBox.LostFocus += GiaBanTextBox_OnLostFocus;
        GiaBanTextBox.TextChanged += GiaBanTextBox_OnTextChanged;

        Title = _isNew ? "Thêm sản phẩm" : "Sửa sản phẩm";
        LoadProduct();
    }

    private void LoadProduct()
    {
        TieuDeTextBlock.Text = _isNew
            ? "Thông tin sản phẩm"
            : $"Sửa sản phẩm {_product.MaSanPham}";

        MoTaTextBlock.Text = _isNew
            ? "Nhập thông tin để tạo sản phẩm mới trong kho hàng."
            : "Chỉnh sửa thông tin của sản phẩm đang chọn.";

        MaSanPhamTextBlock.Text = _product.MaSanPham;
        TenSanPhamTextBox.Text = _product.TenSanPham;
        NhomSanPhamComboBox.SelectedItem = FindOption(NhomSanPhamComboBox.ItemsSource, _product.NhomSanPhamId);
        DonViTinhComboBox.SelectedItem = FindOption(DonViTinhComboBox.ItemsSource, _product.DonViTinhId);

        // Giá bán hiển thị kiểu Việt Nam (50.000), các ô số khác giữ nguyên dạng gốc
        GiaBanTextBox.Text = MoneyInput.Format(_product.GiaBan);
        GiaVonTextBox.Text =  MoneyInput.Format(_product.GiaVon);
        TonKhoTextBox.Text = _product.TonKhoExcel.ToString(CultureInfo.InvariantCulture);
        KhachDatTextBox.Text = _product.KhachDat.ToString(CultureInfo.InvariantCulture);
        ViTriKhoTextBox.Text = _product.ViTriKho ?? "";
        LoaiHangTextBox.Text = _product.LoaiHang ?? "";
        NhomHangBaCapTextBox.Text = _product.NhomHangBaCap ?? "";
        DuKienHetHangTextBox.Text = _product.DuKienHetHang ?? "";
        DuocBanTrucTiepCheckBox.IsChecked = _product.DuocBanTrucTiep;
        DangKinhDoanhCheckBox.IsChecked = _product.DangKinhDoanh;
    }

    /// <summary>Bắt đầu gõ thì bỏ dấu phân cách để sửa số bình thường.</summary>
    private void GiaBanTextBox_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (!MoneyInput.TryParse(GiaBanTextBox.Text, false, out var value, out _))
            return;

        var raw = MoneyInput.ToRawText(value);

        if (GiaBanTextBox.Text != raw)
            GiaBanTextBox.Text = raw;

        GiaBanTextBox.SelectAll();
    }

    /// <summary>Rời ô thì format lại kiểu Việt Nam, kể cả khi người dùng gõ sai.</summary>
    private void GiaBanTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!MoneyInput.TryParse(GiaBanTextBox.Text, false, out var value, out _))
            value = 0m;

        _dangDinhDangTien = true;
        GiaBanTextBox.Text = MoneyInput.Format(value);
        _dangDinhDangTien = false;
    }

    /// <summary>
    /// Gõ tới đâu thì thêm dấu phân cách hàng nghìn tới đó (gõ 1500000 ra 1.500.000).
    /// Chỉ lấy chữ số để khỏi hiện ký hiệu tiền lẫn trong ô nhập.
    /// </summary>
    private void GiaBanTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_dangDinhDangTien)
            return;

        var digits = new string(
            (GiaBanTextBox.Text ?? "")
                .Where(char.IsDigit)
                .ToArray());

        // Để trống thì chờ người dùng gõ tiếp, không tự nhảy về "0"
        if (digits.Length == 0)
            return;

        if (!decimal.TryParse(
                digits,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return;
        }

        var formatted = MoneyInput.Format(value);

        if (formatted == GiaBanTextBox.Text)
            return;

        _dangDinhDangTien = true;

        GiaBanTextBox.Text = formatted;
        GiaBanTextBox.CaretIndex = formatted.Length;

        _dangDinhDangTien = false;
    }

    private void SaveButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ErrorTextBlock.Text = "";
        LoiGiaBanTextBlock.Text = "";

        if (string.IsNullOrWhiteSpace(TenSanPhamTextBox.Text))
        {
            ErrorTextBlock.Text = "Mã và tên sản phẩm là bắt buộc.";
            return;
        }

        // Validate giá bán trước: không cho lưu nếu nhập chữ, số âm,
        // để trống hoặc vượt quá giới hạn DECIMAL(15,2).
        var giaBan = _product.GiaBan;

        if (_duocSuaGiaBan &&
            !MoneyInput.TryParse(GiaBanTextBox.Text, true, out giaBan, out var loiGiaBan))
        {
            LoiGiaBanTextBlock.Text = loiGiaBan;
            ErrorTextBlock.Text = "Giá bán chưa hợp lệ, vui lòng kiểm tra lại.";
            GiaBanTextBox.Focus();
            return;
        }

        if (!TryReadDecimal(GiaVonTextBox.Text, out var giaVon))
        {
            ErrorTextBlock.Text = "Giá vốn phải là số hợp lệ.";
            return;
        }

        if (!TryReadDecimal(TonKhoTextBox.Text, out var tonKho) ||
            !TryReadDecimal(KhachDatTextBox.Text, out var khachDat))
        {
            ErrorTextBlock.Text = "Tồn kho và KH đặt phải là số hợp lệ.";
            return;
        }

        // Mọi thông tin đã hợp lệ mới ghi vào đối tượng sản phẩm.
        _product.TenSanPham = TenSanPhamTextBox.Text.Trim();
        _product.NhomSanPhamId = (NhomSanPhamComboBox.SelectedItem as ProductOption)?.Id;
        _product.DonViTinhId = (DonViTinhComboBox.SelectedItem as ProductOption)?.Id;
        _product.GiaBan = giaBan;
        _product.GiaVon = giaVon;
        _product.TonKhoExcel = tonKho;
        _product.KhachDat = khachDat;
        _product.ViTriKho = NullIfEmpty(ViTriKhoTextBox.Text);
        _product.LoaiHang = NullIfEmpty(LoaiHangTextBox.Text);
        _product.NhomHangBaCap = NullIfEmpty(NhomHangBaCapTextBox.Text);
        _product.DuKienHetHang = NullIfEmpty(DuKienHetHangTextBox.Text);
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

    private static string GenerateProductCode() =>
        $"SP-{DateTime.Now:yyyyMMdd-HHmmssfff}";

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}