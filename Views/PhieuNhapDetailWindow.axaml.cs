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

    public PhieuNhapDetailWindow()
        : this(new PhieuNhapHangDetail())
    {
    }

    public PhieuNhapDetailWindow(PhieuNhapHangDetail detail)
        : this(detail, null, null)
    {
    }

    public PhieuNhapDetailWindow(
        PhieuNhapHangDetail detail,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? thanhToan)
        : this(detail, thanhToan, null)
    {
    }

    public PhieuNhapDetailWindow(
        PhieuNhapHangDetail detail,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? thanhToan,
        Func<PhieuNhapHangDetail, Window, Task<PhieuNhapHangDetail?>>? chinhSua)
    {
        InitializeComponent();
        _thanhToan = thanhToan;
        _chinhSua = chinhSua;
        DataContext = detail;
        CapNhatGiaoDien(detail);
    }

    private void CapNhatGiaoDien(PhieuNhapHangDetail detail)
    {
        // Chỉ hiện nút thanh toán khi phiếu đã hoàn thành và còn công nợ phải trả.
        ThanhToanButton.IsVisible = detail.CoTheThanhToan;

        // Chỉ hiện nút chỉnh sửa nếu có hàm xử lý chỉnh sửa được truyền vào
        SuaPhieuButton.IsVisible = _chinhSua is not null;
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

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
