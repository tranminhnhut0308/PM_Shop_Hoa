using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class HelpWindow : Window
{
    private readonly HelpViewModel _viewModel;

    public HelpWindow()
    {
        InitializeComponent();

        _viewModel = new HelpViewModel
        {
            DongAction = Close,
            MoCapNhatAction = MoCuaSoCapNhat,
            SaoChepAction = SaoChepAsync,
            MoLienKetAction = MoLienKetAsync,
        };

        DataContext = _viewModel;

        AddHandler(KeyDownEvent, OnHelpWindowKeyDown, RoutingStrategies.Tunnel);

        Opened += async (_, _) => await _viewModel.KhoiTaoAsync();
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e) => Close();

    private async void MoCuaSoCapNhat()
    {
        var updateWindow = new UpdateWindow();
        await updateWindow.ShowDialog(this);
    }

    private async Task SaoChepAsync(string noiDung)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard is null)
        {
            _viewModel.ThongBaoSaoChep = "Không truy cập được bộ nhớ tạm của hệ thống.";
            return;
        }

        await clipboard.SetTextAsync(noiDung);
    }

    private async Task MoLienKetAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _viewModel.LoiThongTin = "Đường dẫn không hợp lệ: " + url;
            return;
        }

        try
        {
            var launcher = TopLevel.GetTopLevel(this)?.Launcher;

            if (launcher is not null)
                await launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            _viewModel.LoiThongTin = "Không mở được đường dẫn: " + ex.Message;
        }
    }

    private void OnHelpWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _viewModel.SelectedTabIndex = 2;
            GuideSearchBox.Focus();
            e.Handled = true;
        }
    }
}
