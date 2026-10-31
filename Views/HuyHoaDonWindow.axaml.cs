using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class HuyHoaDonWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly string _maHoaDon;
    private ThongTinXacNhanHuyHoaDon? _thongTin;
    private bool _dangXuLy;

    public HuyHoaDonWindow() : this("")
    {
    }

    public HuyHoaDonWindow(string maHoaDon)
    {
        InitializeComponent();
        _maHoaDon = (maHoaDon ?? "").Trim();
        TxtMaHoaDonBadge.Text = string.IsNullOrWhiteSpace(_maHoaDon) ? "#HD" : $"#{_maHoaDon}";

        Loaded += async (_, _) => await TaiThongTinHoaDonAsync();
    }

    private async Task TaiThongTinHoaDonAsync()
    {
        try
        {
            _thongTin = await _db.GetThongTinXacNhanHuyHoaDonAsync(_maHoaDon);
            HienThiThongTin(_thongTin);
        }
        catch (Exception ex)
        {
            HienLoi(ex.Message);
            BtnChiHuy.IsEnabled = false;
            BtnHuyVaTaoLai.IsEnabled = false;
        }
    }

    private void HienThiThongTin(ThongTinXacNhanHuyHoaDon info)
    {
        TxtMaHoaDonBadge.Text = $"#{info.MaHoaDon}";
        TxtMaHoaDon.Text = $"{info.MaHoaDon} (Bán lúc {info.ThoiGianBan:dd/MM/yyyy HH:mm})";
        TxtTenKhachHang.Text = string.IsNullOrWhiteSpace(info.MaKhachHang)
            ? info.TenKhachHang
            : $"{info.TenKhachHang} ({info.MaKhachHang})";
        TxtTrangThaiHienTai.Text = info.TrangThaiThanhToanGiaoHangHienThi;

        TxtTongTienHoaDon.Text = $"{info.KhachCanTra:N0} đ";
        TxtSoTienDaThanhToan.Text = $"{info.KhachDaThanhToan:N0} đ";
        TxtCongNoConLai.Text = $"{info.ConNo:N0} đ";

        if (info.PhieuThus.Count > 0)
        {
            PanelPhieuThuLienQuan.IsVisible = true;
            TxtDanhSachPhieuThu.Text = string.Join(
                Environment.NewLine,
                info.PhieuThus.Select(p => $"• {p.MaPhieuChi}: {p.SoTien:N0} đ ({p.PhuongThuc}) — {p.ThoiGianChi:dd/MM/yyyy HH:mm}"));
        }
        else
        {
            PanelPhieuThuLienQuan.IsVisible = false;
        }

        if (info.LaHoaDonTongHopIfAny())
        {
            RbChuaGiaoHang.Content = "Hóa đơn tổng hợp (Không có chi tiết hàng hóa xuất kho)";
            RbDaGiaoDaNhanLai.IsEnabled = false;
            RbDaGiaoKhachVanGiu.IsEnabled = false;
        }

        // Cấu hình phần công nợ & tiền đã thu
        if (info.ConNo > 0 && info.KhachDaThanhToan <= 0)
        {
            TxtThongBaoCongNo.Text =
                $"• Hóa đơn chưa thanh toán: Hệ thống sẽ hoàn tác phần công nợ phát sinh ({info.ConNo:N0} đ) của khách hàng {info.TenKhachHang}.";
            PanelXuLyTienDaThu.IsVisible = false;
        }
        else if (info.ConNo > 0 && info.KhachDaThanhToan > 0)
        {
            TxtThongBaoCongNo.Text =
                $"• Hóa đơn thanh toán một phần: Hệ thống sẽ hoàn tác công nợ còn lại ({info.ConNo:N0} đ) và xử lý số tiền đã thu ({info.KhachDaThanhToan:N0} đ) theo lựa chọn bên dưới:";
            PanelXuLyTienDaThu.IsVisible = true;
        }
        else if (info.KhachDaThanhToan > 0)
        {
            TxtThongBaoCongNo.Text =
                $"• Hóa đơn đã thanh toán đủ ({info.KhachDaThanhToan:N0} đ): Vui lòng chọn phương án xử lý khoản tiền đã thu của khách:";
            PanelXuLyTienDaThu.IsVisible = true;
        }
        else
        {
            TxtThongBaoCongNo.Text = "• Hóa đơn không phát sinh tiền đã thu và không có công nợ.";
            PanelXuLyTienDaThu.IsVisible = false;
        }

        RbHoanTienChoKhach.Content =
            $"Hoàn tiền lại cho khách ({info.KhachDaThanhToan:N0} đ) — Tạo phiếu chi trong Sổ quỹ";
        RbChuyenKhachTraTruoc.Content =
            $"Chuyển {info.KhachDaThanhToan:N0} đ thành khoản khách trả trước / cấn trừ công nợ";

        if (!info.LaKhachCoHoSo)
        {
            RbChuyenKhachTraTruoc.IsEnabled = false;
            RbHoanTienChoKhach.IsChecked = true;
            TxtGhiChuKhachLeKhongChuyenTraTruoc.IsVisible = true;
        }
        else
        {
            RbChuyenKhachTraTruoc.IsEnabled = true;
            TxtGhiChuKhachLeKhongChuyenTraTruoc.IsVisible = false;
        }
    }

    private void OnTrangThaiGiaoHangChanged(object? sender, RoutedEventArgs e)
    {
        if (PanelXacNhanNhanLaiHang is not null && RbDaGiaoDaNhanLai is not null)
        {
            PanelXacNhanNhanLaiHang.IsVisible = RbDaGiaoDaNhanLai.IsChecked == true;
        }
    }

    private void OnPhuongAnTienChanged(object? sender, RoutedEventArgs e)
    {
        if (PanelPhuongThucHoanTien is not null && RbHoanTienChoKhach is not null)
        {
            PanelPhuongThucHoanTien.IsVisible = RbHoanTienChoKhach.IsChecked == true;
        }
    }

    private void BtnDong_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_dangXuLy)
            return;

        Close(null);
    }

    private async void BtnChiHuy_OnClick(object? sender, RoutedEventArgs e)
    {
        await ThucHienHuyAsync(CheDoHuyHoaDon.ChiHuy);
    }

    private async void BtnHuyVaTaoLai_OnClick(object? sender, RoutedEventArgs e)
    {
        await ThucHienHuyAsync(CheDoHuyHoaDon.HuyVaTaoHoaDonTuongTu);
    }

    private async Task ThucHienHuyAsync(CheDoHuyHoaDon cheDo)
    {
        if (_dangXuLy || _thongTin is null)
            return;

        BannerLoi.IsVisible = false;

        var lyDo = (TxtLyDoHuy.Text ?? "").Trim();
        if (lyDo.Length == 0)
        {
            HienLoi("Vui lòng nhập lý do hủy hóa đơn (bắt buộc).");
            TxtLyDoHuy.Focus();
            return;
        }

        var trangThaiGiaoHang = RbDaGiaoDaNhanLai.IsChecked == true
            ? TrangThaiGiaoHangKhiHuy.DaGiaoDaNhanLaiHang
            : RbDaGiaoKhachVanGiu.IsChecked == true
                ? TrangThaiGiaoHangKhiHuy.DaGiaoKhachVanGiuHang
                : TrangThaiGiaoHangKhiHuy.ChuaGiaoHang;

        if (trangThaiGiaoHang == TrangThaiGiaoHangKhiHuy.DaGiaoDaNhanLaiHang
            && ChkXacNhanDaNhanLaiHang.IsChecked != true)
        {
            HienLoi("Hàng đã giao cho khách: Bạn phải tích xác nhận 'Đã nhận lại đầy đủ hàng hóa khách trả về kho' trước khi cộng lại tồn kho.");
            return;
        }

        var phuongAnTien = !_thongTin.CoTienDaThanhToan
            ? PhuongAnXuLyTienKhiHuy.KhongCo
            : (RbChuyenKhachTraTruoc.IsChecked == true
                ? PhuongAnXuLyTienKhiHuy.ChuyenKhoanKhachTraTruoc
                : PhuongAnXuLyTienKhiHuy.HoanTienChoKhach);

        var phuongThucHoan = "TIEN_MAT";
        if (CboPhuongThucHoanTien.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
        {
            phuongThucHoan = tag;
        }

        var tieuDeXacNhan = cheDo == CheDoHuyHoaDon.HuyVaTaoHoaDonTuongTu
            ? "Xác nhận hủy và tạo hóa đơn tương tự"
            : "Xác nhận hủy hóa đơn";

        var noiDungXacNhan = cheDo == CheDoHuyHoaDon.HuyVaTaoHoaDonTuongTu
            ? $"Bạn có chắc chắn muốn HỦY hóa đơn {_thongTin.MaHoaDon} và sao chép thông tin sang đơn nháp tại màn Bán hàng?"
            : $"Bạn có chắc chắn muốn HỦY hóa đơn {_thongTin.MaHoaDon}? Thao tác này không thể hoàn tác.";

        var confirm = new ConfirmationWindow(
            tieuDeXacNhan,
            noiDungXacNhan,
            "Xác nhận hủy",
            "Quay lại");

        if (!await confirm.ShowDialog<bool>(this))
            return;

        _dangXuLy = true;
        BtnChiHuy.IsEnabled = false;
        BtnHuyVaTaoLai.IsEnabled = false;
        BtnDong.IsEnabled = false;

        try
        {
            var result = await _db.HuyHoaDonBanHangAsync(new HuyHoaDonRequest
            {
                HoaDonId = _thongTin.HoaDonId,
                MaHoaDon = _thongTin.MaHoaDon,
                LyDoHuy = lyDo,
                TrangThaiGiaoHang = trangThaiGiaoHang,
                XacNhanDaNhanLaiHangTraVe = ChkXacNhanDaNhanLaiHang.IsChecked == true,
                PhuongAnTien = phuongAnTien,
                PhuongThucHoanTien = phuongThucHoan,
                CheDoHuy = cheDo
            });

            Close(result);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi thực hiện hủy hóa đơn trên HuyHoaDonWindow", ex);
            HienLoi(ex.Message);
            _dangXuLy = false;
            BtnChiHuy.IsEnabled = true;
            BtnHuyVaTaoLai.IsEnabled = true;
            BtnDong.IsEnabled = true;
        }
    }

    private void HienLoi(string message)
    {
        TxtLoi.Text = message;
        BannerLoi.IsVisible = true;
    }
}

internal static class ThongTinXacNhanHuyHoaDonExtensions
{
    public static bool LaHoaDonTongHopIfAny(this ThongTinXacNhanHuyHoaDon info)
        => string.Equals(info.TrangThaiHoaDon, "TONG_HOP", StringComparison.OrdinalIgnoreCase);
}
