using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class HuyPhieuNhapWindow : Window
{
    private readonly PhieuNhapHangDetail _detail;
    private readonly DatabaseService _db;
    private readonly uint? _nguoiThaoTacId;
    private readonly string? _tenNguoiThaoTac;
    private bool _tonKhoHopLe = true;

    public bool DaHuyThanhCong { get; private set; }

    public HuyPhieuNhapWindow()
        : this(new PhieuNhapHangDetail(), new DatabaseService(), null, null)
    {
    }

    public HuyPhieuNhapWindow(
        PhieuNhapHangDetail detail,
        DatabaseService db,
        uint? nguoiThaoTacId,
        string? tenNguoiThaoTac)
    {
        InitializeComponent();
        _detail = detail;
        _db = db;
        _nguoiThaoTacId = nguoiThaoTacId;
        _tenNguoiThaoTac = tenNguoiThaoTac;

        KhoiTaoGiaoDien();
        Opened += async (_, _) => await KiemTraDieuKienTonKhoAsync();
    }

    private void KhoiTaoGiaoDien()
    {
        TxtNhaCungCap.Text = $"{_detail.TenNhaCungCapHienThi} ({_detail.MaNhaCungCapHienThi})";
        TxtTongTienNhap.Text = _detail.TongTienNhapText;
        TxtDaThanhToan.Text = _detail.DaThanhToanText;
        TxtConNo.Text = _detail.ConNoText;

        if (_detail.DaThanhToan > 0)
        {
            PanelXuLyTienDaChi.IsVisible = true;
            TxtThongBaoCongNo.Text = $"Phiếu đã thanh toán {_detail.DaThanhToanText}, còn nợ {_detail.ConNoText}. Bạn có thể hoàn tiền cho nhà cung cấp hoặc giữ khoản này để cấn trừ công nợ sau.";
        }
        else
        {
            PanelXuLyTienDaChi.IsVisible = false;
            TxtThongBaoCongNo.Text = $"Phiếu chưa thanh toán tiền, hệ thống sẽ tự động hoàn tác toàn bộ công nợ {_detail.ConNoText} phát sinh từ phiếu này khỏi nhà cung cấp.";
        }
    }

    private async Task KiemTraDieuKienTonKhoAsync()
    {
        try
        {
            var ketQua = await _db.KiemTraDieuKienHuyPhieuNhapAsync(_detail.PhieuNhapId);
            _tonKhoHopLe = ketQua.HopLe;

            if (ketQua.HopLe)
            {
                PanelTonKhoHopLe.IsVisible = true;
                PanelTonKhoLoi.IsVisible = false;
                BtnXacNhanHuy.IsEnabled = true;
            }
            else
            {
                PanelTonKhoHopLe.IsVisible = false;
                PanelTonKhoLoi.IsVisible = true;
                BtnXacNhanHuy.IsEnabled = false;

                if (ketQua.MatHangLoi.Count > 0)
                {
                    TxtChiTietTonKhoLoi.Text = string.Join("\n", ketQua.MatHangLoi.Select(m =>
                        $"• {m.TenSanPham} ({m.MaSanPham}): Cần hoàn tác {m.SoLuongCanHoanTac:N0} {m.DonViTinh}, tồn kho hiện tại chỉ còn {m.TonKhoHienTai:N0} {m.DonViTinh} (thiếu {m.ThieuHut:N0})"));
                }
                else
                {
                    TxtChiTietTonKhoLoi.Text = ketQua.CanhBao ?? "Tồn kho không đủ để hoàn tác.";
                }
            }
        }
        catch (Exception ex)
        {
            PanelTonKhoHopLe.IsVisible = false;
            PanelTonKhoLoi.IsVisible = true;
            TxtChiTietTonKhoLoi.Text = $"Lỗi kiểm tra tồn kho: {ex.Message}";
            BtnXacNhanHuy.IsEnabled = false;
        }
    }

    private void HienThiLoi(string thongBao)
    {
        TxtLoi.Text = thongBao;
        BannerLoi.IsVisible = true;
    }

    private void AnLoi()
    {
        BannerLoi.IsVisible = false;
        TxtLoi.Text = "";
    }

    private async void BtnXacNhanHuy_OnClick(object? sender, RoutedEventArgs e)
    {
        AnLoi();

        var lyDo = TxtLyDoHuy.Text?.Trim();
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            HienThiLoi("Vui lòng nhập rõ lý do hủy phiếu nhập hàng (bắt buộc).");
            TxtLyDoHuy.Focus();
            return;
        }

        if (!_tonKhoHopLe)
        {
            HienThiLoi("Không thể hủy phiếu vì tồn kho không đủ điều kiện để hoàn tác.");
            return;
        }

        BtnXacNhanHuy.IsEnabled = false;
        BtnQuayLai.IsEnabled = false;

        try
        {
            var hoanTienChoNhaCungCap = _detail.DaThanhToan > 0 && (RbHoanTienChoNhaCungCap.IsChecked == true);
            var phuongThuc = (CboPhuongThucHoanTien.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TIEN_MAT";

            var yeuCau = new HuyPhieuNhapYeuCau
            {
                PhieuNhapId = _detail.PhieuNhapId,
                LyDoHuy = lyDo,
                HoanTienChoNhaCungCap = hoanTienChoNhaCungCap,
                PhuongThucHoanTien = phuongThuc,
                NguoiThaoTacId = _nguoiThaoTacId,
                TenNguoiThaoTac = _tenNguoiThaoTac
            };

            await _db.HuyPhieuNhapHangAsync(yeuCau);
            DaHuyThanhCong = true;
            Close(true);
        }
        catch (Exception ex)
        {
            HienThiLoi($"Không thể hủy phiếu nhập: {ex.Message}");
            BtnXacNhanHuy.IsEnabled = true;
            BtnQuayLai.IsEnabled = true;
        }
    }

    private void BtnQuayLai_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
