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
    private readonly DatabaseService _db = new();

    public LoginViewModel()
    {
        _ = KiemTraKetNoiAsync();
    }

    [ObservableProperty]
    private string tenDangNhap = "admin";

    [ObservableProperty]
    private string matKhau = "123456";

    [ObservableProperty]
    private bool dangXuLy;

    [ObservableProperty]
    private string thongBao = "";

    [ObservableProperty]
    private bool ghiNhoDangNhap;

    [ObservableProperty]
    private string trangThaiKetNoi = "Đang kiểm tra kết nối...";

    // =========================================================
    // KIỂM TRA DATABASE
    // =========================================================

    private async Task KiemTraKetNoiAsync()
    {
        try
        {
            await _db.WarmUpAsync();

            TrangThaiKetNoi = "Đã kết nối MariaDB";
        }
        catch (Exception ex)
        {
            TrangThaiKetNoi = "Chưa kết nối MariaDB";

            Console.WriteLine("========== DATABASE CONNECTION ERROR ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("================================================");
        }
    }

    // =========================================================
    // ĐĂNG NHẬP
    // =========================================================

    [RelayCommand]
    private async Task DangNhapAsync(Window? loginWindow)
    {
        if (DangXuLy)
            return;

        if (string.IsNullOrWhiteSpace(TenDangNhap))
        {
            ThongBao = "Vui lòng nhập tên đăng nhập.";
            return;
        }

        if (string.IsNullOrWhiteSpace(MatKhau))
        {
            ThongBao = "Vui lòng nhập mật khẩu.";
            return;
        }

        DangXuLy = true;
        ThongBao = "";
        TrangThaiKetNoi = "Đang kết nối MariaDB...";

        try
        {
            // =====================================================
            // ĐĂNG NHẬP TRỰC TIẾP TỪ DATABASE
            // =====================================================

            var user = await _db.DangNhapAsync(
                TenDangNhap.Trim(),
                MatKhau);

            if (user is null)
            {
                ThongBao = "Tên đăng nhập hoặc mật khẩu không đúng.";
                TrangThaiKetNoi = "Kết nối database thành công.";
                return;
            }

            // =====================================================
            // LƯU USER HIỆN TẠI
            // =====================================================

            Session.CurrentUser = user;

            // Cập nhật thời gian đăng nhập
            _ = _db.CapNhatLanDangNhapCuoiAsync(user.Id);

            TrangThaiKetNoi = "Đăng nhập thành công.";
            ThongBao = "";

            // =====================================================
            // MỞ MAIN WINDOW
            // =====================================================

            try
            {
                var mainWindow = new MainWindow();

                mainWindow.Show();

                loginWindow?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("========== MAIN WINDOW ERROR ==========");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("=======================================");

                ThongBao =
                    "Đăng nhập thành công nhưng không thể mở giao diện chính.\n" +
                    ex.Message;

                TrangThaiKetNoi =
                    "Lỗi khi mở MainWindow.";

                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("========== DATABASE LOGIN ERROR ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("==========================================");

            ThongBao =
                "Không thể kết nối database.\n" +
                ex.Message;

            TrangThaiKetNoi =
                "Lỗi kết nối MariaDB.";
        }
        finally
        {
            DangXuLy = false;
        }
    }

    // =========================================================
    // THOÁT
    // =========================================================

    [RelayCommand]
    private void Thoat(Window? loginWindow)
    {
        loginWindow?.Close();
    }

    // =========================================================
    // QUÊN MẬT KHẨU
    // =========================================================

    [RelayCommand]
    private void QuenMatKhau()
    {
        ThongBao =
            "Vui lòng liên hệ quản trị viên để đặt lại mật khẩu.";
    }

    // =========================================================
    // TÀI KHOẢN KHÁC
    // =========================================================

    [RelayCommand]
    private void TaiKhoanKhac()
    {
        TenDangNhap = "";
        MatKhau = "";
        ThongBao = "";

        TrangThaiKetNoi =
            "Đang chờ đăng nhập...";
    }
}