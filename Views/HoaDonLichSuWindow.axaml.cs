using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class HoaDonLichSuWindow : Window
{
    private readonly string _maHoaDon;
    private readonly DatabaseService _db = new();
    private List<LichSuChinhSuaHoaDon> _lichSuList = [];

    public HoaDonLichSuWindow() : this("")
    {
    }

    public HoaDonLichSuWindow(string maHoaDon)
    {
        InitializeComponent();
        _maHoaDon = maHoaDon;
        TxtMaHoaDon.Text = $"#{maHoaDon}";
        Loaded += async (_, _) => await TaiDuLieuAsync();
    }

    private async Task TaiDuLieuAsync()
    {
        try
        {
            _lichSuList = await _db.GetLichSuChinhSuaHoaDonAsync(_maHoaDon);
            TxtTongSoLan.Text = $"Tổng số lần sửa: {_lichSuList.Count}";
            ListLichSu.ItemsSource = _lichSuList;

            if (_lichSuList.Count > 0)
            {
                ListLichSu.SelectedIndex = 0;
            }
            else
            {
                PanelChiTiet.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi tai lich su chinh sua hoa don", ex);
        }
    }

    private void ListLichSu_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ListLichSu.SelectedItem is not LichSuChinhSuaHoaDon item)
        {
            PanelChiTiet.IsVisible = false;
            return;
        }

        PanelChiTiet.IsVisible = true;
        HienThiChiTiet(item);
    }

    private void HienThiChiTiet(LichSuChinhSuaHoaDon item)
    {
        TxtChiTietNguoiSua.Text = item.TenNguoiThucHien;
        TxtChiTietThoiGian.Text = item.ThoiGianHienThi;
        TxtChiTietLyDo.Text = item.LyDo;

        // Tài chính
        TxtTongTienTruoc.Text = $"{item.TongTienTruoc:N0} đ";
        TxtTongTienSau.Text = $"{item.TongTienSau:N0} đ";
        decimal diffTongTien = item.TongTienSau - item.TongTienTruoc;
        TxtTongTienDiff.Text = (diffTongTien > 0 ? "+" : "") + $"{diffTongTien:N0} đ";

        TxtGiamGiaTruoc.Text = $"{item.GiamGiaTruoc:N0} đ";
        TxtGiamGiaSau.Text = $"{item.GiamGiaSau:N0} đ";
        decimal diffGiamGia = item.GiamGiaSau - item.GiamGiaTruoc;
        TxtGiamGiaDiff.Text = (diffGiamGia > 0 ? "+" : "") + $"{diffGiamGia:N0} đ";

        TxtCanTraTruoc.Text = $"{item.KhachCanTraTruoc:N0} đ";
        TxtCanTraSau.Text = $"{item.KhachCanTraSau:N0} đ";
        decimal diffCanTra = item.KhachCanTraSau - item.KhachCanTraTruoc;
        TxtCanTraDiff.Text = (diffCanTra > 0 ? "+" : "") + $"{diffCanTra:N0} đ";

        TxtConNoTruoc.Text = $"{item.ConNoTruoc:N0} đ";
        TxtConNoSau.Text = $"{item.ConNoSau:N0} đ";
        decimal diffConNo = item.ConNoSau - item.ConNoTruoc;
        TxtConNoDiff.Text = (diffConNo > 0 ? "+" : "") + $"{diffConNo:N0} đ";

        TxtChiTietThayDoiText.Text = string.IsNullOrWhiteSpace(item.ChiTietThayDoi) ? "Không có chi tiết bổ sung." : item.ChiTietThayDoi;

        // So sánh mặt hàng
        HienThiSoSanhSanPham(item);
    }

    private void HienThiSoSanhSanPham(LichSuChinhSuaHoaDon item)
    {
        try
        {
            var oldSnapshot = !string.IsNullOrWhiteSpace(item.DuLieuTruocJson)
                ? JsonSerializer.Deserialize<HoaDonSnapshot>(item.DuLieuTruocJson)
                : null;

            var newSnapshot = !string.IsNullOrWhiteSpace(item.DuLieuSauJson)
                ? JsonSerializer.Deserialize<HoaDonSnapshot>(item.DuLieuSauJson)
                : null;

            var oldItems = oldSnapshot?.Items ?? [];
            var newItems = newSnapshot?.Items ?? [];

            var allProducts = oldItems.Select(x => (x.SanPhamId, x.TenSanPham))
                .Union(newItems.Select(x => (x.SanPhamId, x.TenSanPham)))
                .Distinct()
                .ToList();

            var rows = new List<ItemDiffRow>();

            foreach (var (pid, tenSp) in allProducts)
            {
                var o = oldItems.FirstOrDefault(x => x.SanPhamId == pid);
                var n = newItems.FirstOrDefault(x => x.SanPhamId == pid);

                rows.Add(new ItemDiffRow
                {
                    TenSanPham = !string.IsNullOrWhiteSpace(tenSp) ? tenSp : (n?.TenSanPham ?? o?.TenSanPham ?? "Sản phẩm"),
                    SoLuongTruoc = o?.SoLuong ?? 0,
                    SoLuongSau = n?.SoLuong ?? 0,
                    DonGia = n?.DonGia ?? o?.DonGia ?? 0,
                    ThanhTienTruoc = o?.ThanhTien ?? 0,
                    ThanhTienSau = n?.ThanhTien ?? 0
                });
            }

            GridItemDiff.ItemsSource = rows;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi phan tich snapshot json so sanh mat hang", ex);
            GridItemDiff.ItemsSource = null;
        }
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    public sealed class ItemDiffRow
    {
        public string TenSanPham { get; set; } = "";
        public decimal SoLuongTruoc { get; set; }
        public decimal SoLuongSau { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTienTruoc { get; set; }
        public decimal ThanhTienSau { get; set; }

        public string SoLuongTruocHienThi => SoLuongTruoc > 0 ? $"{SoLuongTruoc:N0}" : "-";
        public string SoLuongSauHienThi => SoLuongSau > 0 ? $"{SoLuongSau:N0}" : "-";
        public string DonGiaHienThi => $"{DonGia:N0} đ";
        public string ThanhTienTruocHienThi => ThanhTienTruoc > 0 ? $"{ThanhTienTruoc:N0} đ" : "-";
        public string ThanhTienSauHienThi => ThanhTienSau > 0 ? $"{ThanhTienSau:N0} đ" : "-";
    }
}
