using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShopHoa.Views;

public partial class NhaCungCapEditorWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public NhaCungCap Supplier { get; }

    public bool IsEditMode { get; }

    public string TieuDe =>
        IsEditMode
            ? "Sửa nhà cung cấp"
            : "Thêm nhà cung cấp";

    private string _loiMessage = "";

    public string LoiMessage
    {
        get => _loiMessage;
        private set
        {
            if (_loiMessage == value)
                return;

            _loiMessage = value;
            OnPropertyChanged();
        }
    }

    public NhaCungCapEditorWindow(NhaCungCap? supplier)
    {
        InitializeComponent();

        IsEditMode = supplier is not null;

        if (supplier is null)
        {
            Supplier = new NhaCungCap
            {
                MaNhaCungCap = "",
                TenNhaCungCap = "",
                TrangThai = true,
                NgayTao = DateTime.Now
            };
        }
        else
        {
            // Tạo bản sao để khi bấm Hủy
            // dữ liệu trong danh sách không bị thay đổi.
            Supplier = new NhaCungCap
            {
                Id = supplier.Id,
                MaNhaCungCap = supplier.MaNhaCungCap,
                TenNhaCungCap = supplier.TenNhaCungCap,
                SoDienThoai = supplier.SoDienThoai,
                Email = supplier.Email,
                DiaChi = supplier.DiaChi,
                PhuongXa = supplier.PhuongXa,
                QuanHuyen = supplier.QuanHuyen,
                TinhThanh = supplier.TinhThanh,
                MaSoThue = supplier.MaSoThue,
                NguoiLienHe = supplier.NguoiLienHe,
                GhiChu = supplier.GhiChu,
                TrangThai = supplier.TrangThai,
                ConNo = supplier.ConNo,
                NgayTao = supplier.NgayTao
            };
        }

        DataContext = this;
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }

    private void OnSaveClicked(
        object? sender,
        RoutedEventArgs e)
    {
        LoiMessage = "";

        // ==============================
        // CHUẨN HÓA DỮ LIỆU
        // ==============================

        Supplier.MaNhaCungCap =
            Supplier.MaNhaCungCap?.Trim() ?? "";

        Supplier.TenNhaCungCap =
            Supplier.TenNhaCungCap?.Trim() ?? "";

        Supplier.SoDienThoai =
            Supplier.SoDienThoai?.Trim();

        Supplier.Email =
            Supplier.Email?.Trim();

        Supplier.DiaChi =
            Supplier.DiaChi?.Trim();

        Supplier.PhuongXa =
            Supplier.PhuongXa?.Trim();

        Supplier.QuanHuyen =
            Supplier.QuanHuyen?.Trim();

        Supplier.TinhThanh =
            Supplier.TinhThanh?.Trim();

        Supplier.MaSoThue =
            Supplier.MaSoThue?.Trim();

        Supplier.NguoiLienHe =
            Supplier.NguoiLienHe?.Trim();

        Supplier.GhiChu =
            Supplier.GhiChu?.Trim();

        // ==============================
        // KIỂM TRA MÃ NHÀ CUNG CẤP
        // ==============================

        if (string.IsNullOrWhiteSpace(
                Supplier.MaNhaCungCap))
        {
            LoiMessage =
                "Vui lòng nhập mã nhà cung cấp.";

            return;
        }

        // ==============================
        // KIỂM TRA TÊN NHÀ CUNG CẤP
        // ==============================

        if (string.IsNullOrWhiteSpace(
                Supplier.TenNhaCungCap))
        {
            LoiMessage =
                "Vui lòng nhập tên nhà cung cấp.";

            return;
        }

        // ==============================
        // KIỂM TRA EMAIL
        // ==============================

        if (!string.IsNullOrWhiteSpace(
                Supplier.Email))
        {
            if (!Supplier.Email.Contains("@"))
            {
                LoiMessage =
                    "Email không hợp lệ.";

                return;
            }
        }

        // ==============================
        // NGÀY TẠO
        // ==============================

        if (!IsEditMode)
        {
            Supplier.NgayTao = DateTime.Now;
        }

        // ==============================
        // LƯU
        // ==============================

        Close(true);
    }

    private void OnCancelClicked(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }

    private async void OnDeleteClicked(
        object? sender,
        RoutedEventArgs e)
    {
        var result =
            await ShowDeleteConfirmationAsync();

        if (!result)
            return;

        // Hiện tại MainWindowViewModel
        // sẽ cần xử lý kết quả xóa riêng.
        //
        // Tạm thời đóng cửa sổ.
        Close(false);
    }

    private async Task<bool> ShowDeleteConfirmationAsync()
    {
        var confirmWindow = new Window
        {
            Title = "Xác nhận xóa",
            Width = 420,
            Height = 220,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner
        };

        var yesButton = new Button
        {
            Content = "Xóa",
            Width = 100,
            Background =
                Avalonia.Media.Brushes.IndianRed,
            Foreground =
                Avalonia.Media.Brushes.White
        };

        var noButton = new Button
        {
            Content = "Hủy",
            Width = 100
        };

        var result = false;

        yesButton.Click += (_, _) =>
        {
            result = true;
            confirmWindow.Close();
        };

        noButton.Click += (_, _) =>
        {
            result = false;
            confirmWindow.Close();
        };

        confirmWindow.Content =
            new StackPanel
            {
                Margin =
                    new Avalonia.Thickness(25),

                Spacing = 18,

                Children =
                {
                    new TextBlock
                    {
                        Text =
                            $"Bạn có chắc muốn xóa nhà cung cấp " +
                            $"\"{Supplier.TenNhaCungCap}\" không?",

                        TextWrapping =
                            Avalonia.Media.TextWrapping.Wrap,

                        FontSize = 15
                    },

                    new StackPanel
                    {
                        Orientation =
                            Avalonia.Layout.Orientation.Horizontal,

                        HorizontalAlignment =
                            Avalonia.Layout.HorizontalAlignment.Right,

                        Spacing = 10,

                        Children =
                        {
                            noButton,
                            yesButton
                        }
                    }
                }
            };

        await confirmWindow.ShowDialog(this);

        return result;
    }
}