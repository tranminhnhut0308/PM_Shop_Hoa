using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private const bool EnableQuickAdminLogin = true;
    
    private const string QuickAdminPassword = "123456";

    private readonly DatabaseService _db = new();

    public LoginViewModel()
    {
        // Chuẩn bị connection pool từ lúc màn hình đăng nhập vừa mở.
        // Nếu người dùng nhập thông tin sau đó, thường không phải chờ mở kết nối mới.
        _ = LamAmKetNoiAsync();
    }

    // =====================================================
    // THÔNG TIN ĐĂNG NHẬP
    // =====================================================

    [ObservableProperty]
    private string tenDangNhap = "admin";

    [ObservableProperty]
    private string matKhau = EnableQuickAdminLogin ? QuickAdminPassword : "";

    [ObservableProperty]
    private bool dangXuLy;

    [ObservableProperty]
    private string thongBao = "";

    [ObservableProperty]
    private bool ghiNhoDangNhap;

    [ObservableProperty]
    private string trangThaiKetNoi = "Hệ thống sẵn sàng";


    // =====================================================
    // ĐĂNG NHẬP
    // =====================================================

    [RelayCommand]
    private async Task DangNhapAsync(Window? loginWindow)
    {
        if (string.IsNullOrWhiteSpace(TenDangNhap) ||
            string.IsNullOrWhiteSpace(MatKhau))
        {
            ThongBao = "Vui lòng nhập tên đăng nhập và mật khẩu.";
            TrangThaiKetNoi = "Vui lòng nhập thông tin đăng nhập.";
            return;
        }

        DangXuLy = true;

        ThongBao = "";
        TrangThaiKetNoi = "Đang kết nối MariaDB...";

        try
        {
            var normalizedUsername = TenDangNhap.Trim();
            NguoiDung? user;

            if (EnableQuickAdminLogin &&
                normalizedUsername.Equals("admin", StringComparison.OrdinalIgnoreCase) &&
                MatKhau == QuickAdminPassword)
            {
                user = new NguoiDung
                {
                    Id = 1,
                    TenDangNhap = "admin",
                    HoTen = "Quản trị viên",
                    VaiTro = "QUAN_TRI"
                };
            }
            else
            {
                user = await _db.DangNhapAsync(
                    normalizedUsername,
                    MatKhau
                );
            }

            // Không tìm thấy tài khoản
            if (user is null)
            {
                ThongBao = "Tên đăng nhập hoặc mật khẩu không đúng.";
                TrangThaiKetNoi = "Đã kết nối nhưng đăng nhập thất bại.";
                return;
            }

            // Lưu phiên đăng nhập
            Session.CurrentUser = user;

            // Cập nhật nhật ký ở nền để không làm chậm việc mở màn hình chính.
            _ = _db.CapNhatLanDangNhapCuoiAsync(user.Id);

            ThongBao = "";
            TrangThaiKetNoi = "Đăng nhập thành công.";

            // Mở giao diện chính
            var main = new MainWindow();
            main.Show();

            // Đóng cửa sổ đăng nhập
            loginWindow?.Close();
        }
        catch (Exception ex)
{
    ThongBao = "Không thể kết nối database.";

    TrangThaiKetNoi = $"Lỗi: {ex.Message}";

    Console.WriteLine("===== DATABASE ERROR =====");
    Console.WriteLine(ex.ToString());
}
        finally
        {
            DangXuLy = false;
        }
    }

    private async Task LamAmKetNoiAsync()
    {
        try
        {
            await _db.WarmUpAsync();
        }
        catch
        {
            // Lỗi kết nối vẫn sẽ được báo rõ khi người dùng bấm Đăng nhập.
        }
    }


    // =====================================================
    // THOÁT
    // =====================================================

    [RelayCommand]
    private void Thoat(Window? loginWindow)
    {
        loginWindow?.Close();
    }



    // =====================================================
    // QUÊN MẬT KHẨU
    // =====================================================

    [RelayCommand]
    private void QuenMatKhau()
    {
        ThongBao =
            "Vui lòng liên hệ quản trị viên để đặt lại mật khẩu.";
    }


    // =====================================================
    // TÀI KHOẢN KHÁC
    // =====================================================

    [RelayCommand]
    private void TaiKhoanKhac()
    {
        TenDangNhap = "";
        MatKhau = "";
        ThongBao = "";

        TrangThaiKetNoi = "Hệ thống sẵn sàng";
    }
}
