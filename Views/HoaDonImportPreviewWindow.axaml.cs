using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using ShopHoa.Models;

namespace ShopHoa.Views;

/// <summary>Lựa chọn của người dùng trong cửa sổ xem trước hóa đơn.</summary>
public sealed record QuyetDinhNhapHoaDon(
    XuLyHoaDonTrung XuLyTrung,
    bool TaoKhachHangMoi,
    bool TaoSanPhamMoi);

/// <summary>
/// Cửa sổ xem trước trước khi nhập hóa đơn: tóm tắt số hóa đơn, cảnh báo, lỗi,
/// danh sách hóa đơn đọc được và lựa chọn cách xử lý hóa đơn trùng mã.
/// Chỉ đọc dữ liệu, chưa ghi gì xuống database.
/// </summary>
public partial class HoaDonImportPreviewWindow : Window
{
    /// <summary>Số hóa đơn tối đa hiển thị, để file vài nghìn dòng không làm nghẽn giao diện.</summary>
    private const int SoHoaDonToiDaHienThi = 1000;

    private readonly HoaDonImportPreview _preview;

    private HoaDonImportPreviewWindow(HoaDonImportPreview preview)
    {
        InitializeComponent();

        _preview = preview;

        Title = "Xem trước trước khi nhập hóa đơn";
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

        TieudeFileTextBlock.Text =
            $"File: {preview.TenFile} — đọc được {preview.TongSoHoaDon:N0} hóa đơn, "
            + $"{preview.TongSoDongChiTiet:N0} dòng chi tiết.";

        TomTatTextBlock.Text = TaoTomTat(preview);

        CanhBaoItems.ItemsSource = preview.CanhBao;
        CanhBaoBorder.IsVisible = preview.CanhBao.Count > 0;

        LoiItems.ItemsSource = preview.DanhSachLoi;
        LoiBorder.IsVisible = preview.DanhSachLoi.Count > 0;
        LoiTieudeTextBlock.Text =
            $"Danh sách lỗi ({preview.DanhSachLoi.Count:N0} dòng), sửa file gốc rồi nhập lại:";

        var soHienThi = Math.Min(preview.DanhSachHoaDon.Count, SoHoaDonToiDaHienThi);
        HoaDonItems.ItemsSource = preview.DanhSachHoaDon.Take(soHienThi).ToList();

        if (soHienThi < preview.DanhSachHoaDon.Count)
        {
            TieudeFileTextBlock.Text +=
                $" Danh sách bên dưới chỉ hiện {soHienThi:N0} hóa đơn đầu, "
                + "thanh tiến độ và kết quả vẫn tính trên toàn bộ file.";
        }

        // Không có hóa đơn nào nhập được thì chỉ còn lựa chọn hủy.
        var coTheNhap = preview.SoHoaDonHopLe > 0 && preview.SoHoaDonHuy < preview.TongSoHoaDon;

        TaoKhachHangMoiCheckBox.IsEnabled = preview.CoTheTaoKhachHangMoi;
        TaoKhachHangMoiCheckBox.IsChecked = false;

        CapNhatGhiChuTaoKhachHang();

        SanPhamChuaCoItems.ItemsSource = preview.SanPhamChuaCo;
        SanPhamChuaCoBorder.IsVisible = preview.SanPhamChuaCo.Count > 0;
        SanPhamChuaCoTieudeTextBlock.Text =
            $"Hàng hóa chưa có trong danh mục ({preview.SanPhamChuaCo.Count:N0} mã)";

        TaoSanPhamMoiCheckBox.IsEnabled = preview.CoTheTaoSanPhamMoi;
        TaoSanPhamMoiCheckBox.IsChecked = preview.CoTheTaoSanPhamMoi;

        CapNhatGhiChuTaoSanPham();

        CapNhatButton.IsEnabled = coTheNhap;
        BoQuaButton.IsEnabled = coTheNhap;
    }

    /// <summary>
    /// Mở cửa sổ xem trước. Trả về lựa chọn của người dùng, null nghĩa là hủy
    /// và không ghi gì xuống database.
    /// </summary>
    public static Task<QuyetDinhNhapHoaDon?> ShowAsync(Window owner, HoaDonImportPreview preview)
    {
        var dialog = new HoaDonImportPreviewWindow(preview);
        return dialog.ShowDialog<QuyetDinhNhapHoaDon?>(owner);
    }

    private static string TaoTomTat(HoaDonImportPreview preview)
    {
        var dong = new List<string>
        {
            $"Hóa đơn đọc được và nhập được: {preview.SoHoaDonHopLe:N0} "
                + $"(mới {preview.SoHoaDonMoi:N0}, trùng mã {preview.SoHoaDonTrung:N0})"
        };

        if (preview.SoHoaDonTongHop > 0)
        {
            dong.Add(
                $"Chế độ tổng hợp: {preview.SoHoaDonTongHop:N0} hóa đơn không có chi tiết hàng hóa. "
                + "Chúng được đánh dấu riêng, không tạo dòng hàng, không trừ tồn kho "
                + "và không tạo phiếu thu/giao dịch quỹ.");
        }

        if (preview.SoHoaDonHuy > 0)
            dong.Add($"Bị hủy ngay trong file nên không nhập: {preview.SoHoaDonHuy:N0}");

        if (preview.SoKhachHangKhongTonTai > 0)
        {
            dong.Add(
                $"Không tìm thấy khách hàng: {preview.SoKhachHangKhongTonTai:N0}"
                + (preview.SoKhachHangCoTheTaoMoi > 0
                    ? $", trong đó {preview.SoKhachHangCoTheTaoMoi:N0} mã có thể tạo mới."
                    : ", hóa đơn này sẽ bị loại."));
        }

        if (preview.SoSanPhamKhongTonTai > 0)
        {
            dong.Add(
                $"Không tìm thấy hàng hóa: {preview.SanPhamChuaCo.Count:N0} mã, "
                + $"liên quan {preview.SoSanPhamKhongTonTai:N0} hóa đơn. "
                + "Chọn tạo sản phẩm mới bên dưới để nhập đầy đủ các dòng hàng.");
        }

        if (preview.SoLoi > 0)
            dong.Add($"Dòng lỗi: {preview.SoLoi:N0}");

        return string.Join("\n", dong);
    }

    private void CapNhatGhiChuTaoKhachHang()
    {
        if (!_preview.CoTheTaoKhachHangMoi)
        {
            TaoKhachHangMoiGhiChuTextBlock.Text =
                "File không có khách hàng mới nào đủ mã và tên để tạo.";

            return;
        }

        var soThieu = _preview.SoKhachHangKhongTonTai - _preview.SoKhachHangCoTheTaoMoi;

        TaoKhachHangMoiGhiChuTextBlock.Text = soThieu > 0
            ? $"Tạo được {_preview.SoKhachHangCoTheTaoMoi:N0} mã khách hàng mới. "
                + $"{soThieu:N0} hóa đơn thiếu mã hoặc tên khách hàng nên vẫn bị loại."
            : $"Sẽ tạo mới tối đa {_preview.SoKhachHangCoTheTaoMoi:N0} mã khách hàng, "
                + "mỗi mã chỉ tạo một lần trong lượt nhập này.";
    }

    private void CapNhatGhiChuTaoSanPham()
    {
        if (!_preview.CoTheTaoSanPhamMoi)
        {
            TaoSanPhamMoiGhiChuTextBlock.Text =
                "Không có mã hàng hóa nào cần tạo mới.";
            return;
        }

        var coTen = _preview.SanPhamChuaCo.Count(item => !string.IsNullOrWhiteSpace(item.TenSanPham));
        var coDvt = _preview.SanPhamChuaCo.Count(item => !string.IsNullOrWhiteSpace(item.DonViTinh));
        var thieuTen = _preview.SanPhamChuaCo.Count - coTen;

        TaoSanPhamMoiGhiChuTextBlock.Text =
            $"Sẽ tạo mới {_preview.SanPhamChuaCo.Count:N0} sản phẩm, mỗi mã chỉ tạo một lần. "
            + $"Có tên hàng: {coTen:N0}; có đơn vị tính: {coDvt:N0}."
            + (thieuTen > 0
                ? $" {thieuTen:N0} mã thiếu tên sẽ dùng chính mã hàng làm tên và có thể sửa lại sau."
                : "")
            + " Không tạo tồn kho, không thay đổi công nợ hay tổng bán ngoài các dòng hàng của hóa đơn.";
    }

    private void HuyButton_OnClick(object? sender, RoutedEventArgs e)
        => Close();

    private void BoQuaButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(new QuyetDinhNhapHoaDon(
            XuLyHoaDonTrung.BoQua,
            TaoKhachHangMoiCheckBox.IsChecked == true,
            TaoSanPhamMoiCheckBox.IsChecked == true));

    private void CapNhatButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(new QuyetDinhNhapHoaDon(
            XuLyHoaDonTrung.CapNhat,
            TaoKhachHangMoiCheckBox.IsChecked == true,
            TaoSanPhamMoiCheckBox.IsChecked == true));
}
