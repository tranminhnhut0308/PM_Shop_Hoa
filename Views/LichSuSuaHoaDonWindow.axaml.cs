using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class LichSuSuaHoaDonWindow : Window
{
    public LichSuSuaHoaDonWindow()
    {
        InitializeComponent();
    }

    public LichSuSuaHoaDonWindow(string maHoaDon, IReadOnlyList<LichSuSuaHoaDon> lichSu) : this()
    {
        TxtTitle.Text = $"Lịch sử chỉnh sửa - {maHoaDon}";
        TxtCount.Text = $"{lichSu.Count} lần chỉnh sửa";

        if (lichSu.Count == 0)
        {
            TxtEmpty.IsVisible = true;
            HistoryItemsList.IsVisible = false;
        }
        else
        {
            TxtEmpty.IsVisible = false;
            HistoryItemsList.IsVisible = true;
            HistoryItemsList.ItemsSource = lichSu;
        }
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
