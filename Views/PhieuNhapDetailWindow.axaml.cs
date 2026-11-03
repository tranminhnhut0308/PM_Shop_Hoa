using System;
using System.IO;
using Avalonia.Platform.Storage;
using ShopHoa.Helpers;
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class PhieuNhapDetailWindow : Window
{
    /// <summary>
    /// Hàm thanh toán công nợ cho phiếu nhập đang xem. Trả về chi tiết phiếu đã
    /// đọc lại sau khi thanh toán để cửa sổ cập nhật số nợ/trạng thái nút. Null khi
    /// người dùng hủy hoặc gặp lỗi.
    /// </summary>
    private readonly Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? _thanhToan;

    /// <summary>
    /// Hàm chỉnh sửa phiếu nhập đang xem. Trả về chi tiết phiếu đã đọc lại sau khi
    /// lưu thay đổi để cửa sổ cập nhật lại thông tin và lịch sử thay đổi. Null khi
    /// người dùng hủy hoặc gặp lỗi.
    /// </summary>
    private readonly Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? _chinhSua;

    /// <summary>
    /// Hàm hủy phiếu nhập hàng. Mở hộp thoại xác nhận hủy, thực hiện transaction và
    /// trả về detail đã cập nhật trạng thái ĐÃ HỦY. Null khi người dùng hủy.
    /// </summary>
    private readonly Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? _huyPhieu;

    /// <summary>
    /// Hàm khôi phục phiếu nhập đã hủy. Kiểm tra điều kiện, cộng lại kho & công nợ,
    /// trả về detail đã cập nhật trạng thái ĐÃ NHẬP HÀNG. Null khi người dùng hủy.
    /// </summary>
    private readonly Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? _khoiPhuc;

    /// <summary>
    /// Hàm mua lại / tạo phiếu mới sao chép hàng hóa từ phiếu cũ.
    /// </summary>
    private readonly Func<PhieuNhapHangDetail, Window, Task>? _muaLai;

    public PhieuNhapDetailWindow()
        : this(new PhieuNhapHangDetail())
    {
    }

    public PhieuNhapDetailWindow(PhieuNhapHangDetail detail)
        : this(detail, null, null, null, null, null)
    {
    }

    public PhieuNhapDetailWindow(
        PhieuNhapHangDetail detail,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? thanhToan)
        : this(detail, thanhToan, null, null, null, null)
    {
    }

    public PhieuNhapDetailWindow(
        PhieuNhapHangDetail detail,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? thanhToan,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? chinhSua)
        : this(detail, thanhToan, chinhSua, null, null, null)
    {
    }

    public PhieuNhapDetailWindow(
        PhieuNhapHangDetail detail,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? thanhToan,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? chinhSua,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? huyPhieu,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? khoiPhuc,
        Func<PhieuNhapHangDetail, Window, Task>? muaLai)
    {
        InitializeComponent();
        _thanhToan = thanhToan;
        _chinhSua = chinhSua;
        _huyPhieu = huyPhieu;
        _khoiPhuc = khoiPhuc;
        _muaLai = muaLai;
        DataContext = detail;
        CapNhatGiaoDien(detail);
    }

    private void CapNhatGiaoDien(PhieuNhapHangDetail detail)
    {
        // Chỉ hiện nút thanh toán khi phiếu chưa bị hủy và còn nợ
        ThanhToanButton.IsVisible = detail.CoTheThanhToan && _thanhToan is not null;

        // Chỉ hiện nút chỉnh sửa nếu chưa bị hủy và có hàm xử lý chỉnh sửa
        SuaPhieuButton.IsVisible = !detail.IsDaHuy && _chinhSua is not null;

        // Chỉ hiện nút hủy phiếu nếu phiếu chưa hủy và không phải lưu tạm
        HuyPhieuButton.IsVisible = detail.CoTheHuy && _huyPhieu is not null;

        // Chỉ hiện nút khôi phục nếu phiếu đã bị hủy
        KhoiPhucPhieuButton.IsVisible = detail.CoTheKhoiPhuc && _khoiPhuc is not null;

        // Nút mua lại luôn sẵn sàng nếu có hàm mua lại
        MuaLaiButton.IsVisible = _muaLai is not null;
    }

    private async void SuaPhieuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PhieuNhapHangDetail detail)
            return;

        if (_chinhSua is null)
            return;

        SuaPhieuButton.IsEnabled = false;

        try
        {
            var capNhat = await _chinhSua(detail, this);

            if (capNhat is not null)
            {
                DataContext = null;
                DataContext = capNhat;
                CapNhatGiaoDien(capNhat);
            }
        }
        finally
        {
            SuaPhieuButton.IsEnabled = true;
        }
    }

    private async void ThanhToanButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PhieuNhapHangDetail detail || detail.ConNo <= 0m)
            return;

        if (_thanhToan is null)
            return;

        ThanhToanButton.IsEnabled = false;

        try
        {
            var capNhat = await _thanhToan(detail, this);

            if (capNhat is not null)
            {
                DataContext = null;
                DataContext = capNhat;
                CapNhatGiaoDien(capNhat);
            }
        }
        finally
        {
            ThanhToanButton.IsEnabled = true;
        }
    }

    private async void HuyPhieuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PhieuNhapHangDetail detail)
            return;

        if (_huyPhieu is null)
            return;

        HuyPhieuButton.IsEnabled = false;

        try
        {
            var capNhat = await _huyPhieu(detail, this);

            if (capNhat is not null)
            {
                DataContext = null;
                DataContext = capNhat;
                CapNhatGiaoDien(capNhat);
            }
        }
        finally
        {
            HuyPhieuButton.IsEnabled = true;
        }
    }

    private async void KhoiPhucPhieuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PhieuNhapHangDetail detail)
            return;

        if (_khoiPhuc is null)
            return;

        KhoiPhucPhieuButton.IsEnabled = false;

        try
        {
            var capNhat = await _khoiPhuc(detail, this);

            if (capNhat is not null)
            {
                DataContext = null;
                DataContext = capNhat;
                CapNhatGiaoDien(capNhat);
            }
        }
        finally
        {
            KhoiPhucPhieuButton.IsEnabled = true;
        }
    }

    private async void MuaLaiButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PhieuNhapHangDetail detail)
            return;

        if (_muaLai is null)
            return;

        MuaLaiButton.IsEnabled = false;

        try
        {
            await _muaLai(detail, this);
            Close();
        }
        finally
        {
            MuaLaiButton.IsEnabled = true;
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void MoChungTuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DinhKemPhieuNhap dk)
            return;

        var path = dk.DuongDanTuyetDoi;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            await NotificationWindow.ShowWarningAsync("Tập tin chứng từ không tồn tại trên hệ thống lưu trữ.", "Không tìm thấy file", this);
            return;
        }

        try
        {
            XuatFileHelper.MoFile(path);
        }
        catch (Exception ex)
        {
            await NotificationWindow.ShowErrorAsync("Không thể mở tài liệu: " + ex.Message, "Lỗi mở file", this);
        }
    }

    private async void TaiChungTuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DinhKemPhieuNhap dk)
            return;

        var sourcePath = dk.DuongDanTuyetDoi;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            await NotificationWindow.ShowWarningAsync("Tập tin chứng từ không tồn tại trên hệ thống lưu trữ.", "Không tìm thấy file", this);
            return;
        }

        try
        {
            var saveFile = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Tải chứng từ về máy tính",
                SuggestedFileName = dk.TenFileGoc
            });

            if (saveFile is null)
                return;

            var destPath = saveFile.Path.LocalPath;
            if (!string.IsNullOrWhiteSpace(destPath))
            {
                File.Copy(sourcePath, destPath, overwrite: true);
                await NotificationWindow.ShowSuccessAsync($"Đã tải file về thành công:\n{destPath}", "Tải về hoàn tất", this);
            }
        }
        catch (Exception ex)
        {
            await NotificationWindow.ShowErrorAsync("Không thể tải file: " + ex.Message, "Lỗi tải về", this);
        }
    }
}