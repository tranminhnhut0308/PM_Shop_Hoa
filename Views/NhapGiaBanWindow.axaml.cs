using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;

namespace ShopHoa.Views;

public partial class NhapGiaBanWindow : Window
{
    public decimal? GiaBan { get; private set; }

    public NhapGiaBanWindow()
    {
        InitializeComponent();

        GiaBanTextBox.FocusInput();
    }

    private void LuuClicked(object? sender, RoutedEventArgs e)
    {
        var giaBan = GiaBanTextBox.Value;
        if (giaBan <= 0 || giaBan > MoneyInput.GiaTienToiDa)
        {
            GiaBanTextBox.FocusInput();
            return;
        }

        GiaBan = giaBan;

        Close(GiaBan);
    }

    private void HuyClicked(object? sender, RoutedEventArgs e)
    {
        GiaBan = null;

        Close(null);
    }
}