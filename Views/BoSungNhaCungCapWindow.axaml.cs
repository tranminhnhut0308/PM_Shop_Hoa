using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class BoSungNhaCungCapWindow : Window
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly uint _phieuNhapId;
    private readonly string _maPhieuNhap = "";
    private readonly DatabaseService? _db;

    private List<NhaCungCap> _allSuppliers = [];
    private NhaCungCap? _selectedSupplier;

    public BoSungNhaCungCapWindow()
    {
        InitializeComponent();
    }

    public BoSungNhaCungCapWindow(
        uint phieuNhapId,
        string maPhieuNhap,
        DateTime thoiGianNhap,
        decimal tongTienNhap,
        decimal daThanhToan,
        decimal conNo,
        DatabaseService db) : this()
    {
        _phieuNhapId = phieuNhapId;
        _maPhieuNhap = maPhieuNhap;
        _db = db;

        TxtHeaderTitle.Text = $"Bổ sung nhà cung cấp — {maPhieuNhap}";
        TxtMaPhieu.Text = maPhieuNhap;
        TxtThoiGian.Text = thoiGianNhap.ToString("dd/MM/yyyy HH:mm");
        TxtTongTien.Text = tongTienNhap.ToString("N0", Vi) + " đ";
        TxtThanhToanVaConNo.Text = $"{daThanhToan:N0} đ / Còn {conNo:N0} đ";

        TxtSearch.TextChanged += OnSearchTextChanged;
        TxtSearch.KeyDown += OnSearchKeyDown;
        LstSuggestions.SelectionChanged += OnSuggestionsSelectionChanged;
        BtnDoiNcc.Click += OnDoiNccClick;
        BtnHuy.Click += (_, _) => Close(false);
        BtnXacNhan.Click += OnXacNhanClick;

        Loaded += async (_, _) => await TaiDanhSachNhaCungCapAsync();
    }

    private async Task TaiDanhSachNhaCungCapAsync()
    {
        if (_db is null)
            return;

        try
        {
            var list = await _db.GetNhaCungCapAsync();
            _allSuppliers = list.Where(n => n.TrangThai).ToList();
            CapNhatGoiY(TxtSearch.Text);
            TxtSearch.Focus();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách nhà cung cấp trong BoSungNhaCungCapWindow", ex);
            TxtError.Text = "Không thể tải danh sách nhà cung cấp: " + ex.Message;
            TxtError.IsVisible = true;
        }
    }

    private void CapNhatGoiY(string? query)
    {
        var q = (query ?? "").Trim();
        IEnumerable<NhaCungCap> matches;
        if (string.IsNullOrWhiteSpace(q))
        {
            matches = _allSuppliers.Take(30);
        }
        else
        {
            matches = _allSuppliers.Where(n =>
                (!string.IsNullOrWhiteSpace(n.TenNhaCungCap) && n.TenNhaCungCap.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(n.MaNhaCungCap) && n.MaNhaCungCap.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(n.SoDienThoai) && n.SoDienThoai.Contains(q, StringComparison.OrdinalIgnoreCase))
            ).Take(30);
        }

        var resultList = matches.ToList();
        LstSuggestions.ItemsSource = resultList;
        if (resultList.Count > 0)
        {
            LstSuggestions.SelectedIndex = 0;
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_selectedSupplier is not null)
            return;

        CapNhatGoiY(TxtSearch.Text);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (LstSuggestions.ItemCount > 0)
            {
                var next = LstSuggestions.SelectedIndex + 1;
                if (next < LstSuggestions.ItemCount)
                    LstSuggestions.SelectedIndex = next;
                LstSuggestions.ScrollIntoView(LstSuggestions.SelectedIndex);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (LstSuggestions.ItemCount > 0)
            {
                var prev = LstSuggestions.SelectedIndex - 1;
                if (prev >= 0)
                    LstSuggestions.SelectedIndex = prev;
                LstSuggestions.ScrollIntoView(LstSuggestions.SelectedIndex);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (LstSuggestions.SelectedItem is NhaCungCap ncc)
            {
                ChonNhaCungCap(ncc);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(false);
            e.Handled = true;
        }
    }

    private void OnSuggestionsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LstSuggestions.SelectedItem is NhaCungCap ncc && _selectedSupplier is null)
        {
            // Only auto-select on explicit click or enter, not navigation
        }
    }

    private void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Visual visual && visual.DataContext is NhaCungCap ncc)
        {
            ChonNhaCungCap(ncc);
        }
    }

    private void ChonNhaCungCap(NhaCungCap ncc)
    {
        _selectedSupplier = ncc;
        TxtSelectedTenNcc.Text = ncc.TenNhaCungCap;
        TxtSelectedMaNcc.Text = ncc.MaNhaCungCap;
        TxtSelectedSdt.Text = string.IsNullOrWhiteSpace(ncc.SoDienThoai) ? "—" : ncc.SoDienThoai;
        TxtSelectedDiaChi.Text = string.IsNullOrWhiteSpace(ncc.DiaChi) ? "—" : ncc.DiaChi;

        BorderSuggestions.IsVisible = false;
        TxtSearch.IsVisible = false;
        BorderSelectedSupplier.IsVisible = true;
        BtnXacNhan.IsEnabled = true;
        TxtError.IsVisible = false;
    }

    private void OnDoiNccClick(object? sender, RoutedEventArgs e)
    {
        _selectedSupplier = null;
        BorderSelectedSupplier.IsVisible = false;
        TxtSearch.IsVisible = true;
        BorderSuggestions.IsVisible = true;
        BtnXacNhan.IsEnabled = false;
        CapNhatGoiY(TxtSearch.Text);
        TxtSearch.Focus();
    }

    private async void OnXacNhanClick(object? sender, RoutedEventArgs e)
    {
        if (_db is null || _selectedSupplier is null)
            return;

        BtnXacNhan.IsEnabled = false;
        BtnHuy.IsEnabled = false;
        BtnXacNhan.Content = "Đang xử lý...";
        TxtError.IsVisible = false;

        try
        {
            await _db.BoSungNhaCungCapChoPhieuNhapAsync(
                _phieuNhapId,
                _selectedSupplier.Id,
                lyDo: TxtLyDo.Text);

            Close(true);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError($"Lỗi bổ sung NCC cho phiếu {_maPhieuNhap}", ex);
            TxtError.Text = "Lỗi bổ sung nhà cung cấp: " + ex.Message;
            TxtError.IsVisible = true;
            BtnXacNhan.IsEnabled = true;
            BtnHuy.IsEnabled = true;
            BtnXacNhan.Content = "✔ Xác nhận bổ sung";
        }
    }
}
