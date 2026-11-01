using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class HuyPhieuQuyWindow : Window
{
    private readonly SoQuyChiTiet _chiTiet;

    public HuyPhieuQuyWindow()
    {
        InitializeComponent();
        _chiTiet = new SoQuyChiTiet();
    }

    public HuyPhieuQuyWindow(SoQuyChiTiet chiTiet) : this()
    {
        _chiTiet = chiTiet ?? throw new ArgumentNullException(nameof(chiTiet));
        NapDuLieu();
    }

    private void NapDuLieu()
    {
        Title = _chiTiet.LaThu ? "Xác nhận hủy phiếu thu" : "Xác nhận hủy phiếu chi";
        TxtTieuDeHeader.Text = _chiTiet.LaThu ? "Xác nhận hủy phiếu thu" : "Xác nhận hủy phiếu chi";
        TxtMaPhieuBadge.Text = $"#{_chiTiet.MaPhieu}";

        TxtMaPhieu.Text = _chiTiet.MaPhieu;
        TxtSoTien.Text = _chiTiet.SoTienHienThi;
        LblDoiTac.Text = _chiTiet.NhanNguoiNopNhan.ToUpperInvariant();
        TxtDoiTac.Text = string.IsNullOrWhiteSpace(_chiTiet.NguoiNopNhan) ? "Không có" : _chiTiet.NguoiNopNhan;
        TxtPhuongThuc.Text = _chiTiet.PhuongThucHienThi;

        if (_chiTiet.CoChungTuLienQuan)
        {
            var ds = _chiTiet.DanhSachChungTuLienQuan;
            if (ds.Count == 1)
            {
                var doc = ds[0];
                TxtChungTuLienQuan.Text = $"{doc.LoaiChungTu} {doc.MaChungTu} - Đối tác: {doc.DoiTac} ({doc.SoTienLienQuan:N0} đ)";
            }
            else
            {
                var summary = string.Join(", ", ds.Select(d => $"{d.MaChungTu} ({d.SoTienLienQuan:N0}đ)"));
                TxtChungTuLienQuan.Text = $"{ds.Count} chứng từ: {summary}";
            }

            TxtCanhBaoChungTu.Text = _chiTiet.LaThu
                ? $"• Hoàn tác thanh toán của {ds.Count} hóa đơn bán hàng liên quan."
                : $"• Hoàn tác thanh toán của {ds.Count} phiếu nhập hàng liên quan.";

            TxtCanhBaoCongNo.Text = _chiTiet.LaThu
                ? $"• Khôi phục tăng công nợ phải thu của khách hàng ({_chiTiet.SoTien:N0} đ)."
                : $"• Khôi phục tăng nợ phải trả cho nhà cung cấp ({_chiTiet.SoTien:N0} đ).";
        }
        else
        {
            TxtChungTuLienQuan.Text = "Phiếu thu/chi độc lập (không liên kết hóa đơn hay phiếu nhập).";
            TxtCanhBaoChungTu.Text = "• Phiếu độc lập: không tác động đến hóa đơn bán hàng hay phiếu nhập.";
            TxtCanhBaoCongNo.Text = "• Không ảnh hưởng công nợ hóa đơn/phiếu nhập cụ thể.";
        }
    }

    private void OnBoQuaClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnXacNhanHuyClick(object? sender, RoutedEventArgs e)
    {
        var lyDo = TxtLyDoHuy.Text?.Trim();

        if (string.IsNullOrWhiteSpace(lyDo) || lyDo.Length < 5)
        {
            TxtLoiValidate.Text = "Vui lòng nhập lý do hủy chi tiết (tối thiểu 5 ký tự).";
            TxtLoiValidate.IsVisible = true;
            TxtLyDoHuy.Focus();
            return;
        }

        TxtLoiValidate.IsVisible = false;
        Close(lyDo);
    }
}
