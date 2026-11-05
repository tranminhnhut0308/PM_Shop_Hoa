using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Thông tin cửa hàng dùng chung cho mọi mẫu chứng từ (hóa đơn bán hàng,
/// phiếu nhập, báo cáo...).
/// <para>
/// Bảng <c>thong_tin_tiem</c> chỉ chứa MỘT bản ghi (id = 1). Người dùng sửa
/// thì UPDATE đúng bản ghi đó, không tạo bản ghi mới, nhờ vậy hóa đơn luôn đọc
/// được thông tin mới nhất mà không phải dò tìm "bản ghi tiệm nào là mới nhất".
/// </para>
/// <para>
/// Mọi giá trị mặc định (tên tiệm, địa chỉ, số điện thoại, ngân hàng...) chỉ
/// nằm ở <see cref="TaoMacDinh"/> và được ghi vào database đúng một lần khi
/// khởi tạo. Sau đó người dùng tự sửa trong màn "Cài đặt hóa đơn in"; mẫu in
/// không chứa thông tin cửa hàng cố định.
/// </para>
/// </summary>
public sealed partial class ThongTinTiem : ObservableObject
{
    /// <summary>Khoá của bản ghi thông tin tiệm. Luôn = 1 trong thiết kế này.</summary>
    public const uint IdMacDinh = 1;

    public const string KhoGiay58 = "58mm";
    public const string KhoGiay80 = "80mm";
    public const string KhoGiayA4 = "A4";

    public const string ViTriTrai = "left";
    public const string ViTriGiua = "center";
    public const string ViTriPhai = "right";

    // =========================================================
    // THÔNG TIN CỬA HÀNG
    // =========================================================

    public string TenTiem { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public string Zalo { get; set; } = "";
    public string Facebook { get; set; } = "";
    public string Email { get; set; } = "";
    public string MaSoThue { get; set; } = "";
    public string Website { get; set; } = "";
    public List<KenhLienHe> KenhLienHes { get; set; } = [];

    // =========================================================
    // THÔNG TIN NGÂN HÀNG
    // =========================================================

    public string TenNganHang { get; set; } = "";
    public string SoTaiKhoan { get; set; } = "";
    public string ChuTaiKhoan { get; set; } = "";

    // =========================================================
    // HÌNH ẢNH
    // =========================================================

    public string DuongDanLogo { get; set; } = "";
    public string DuongDanQr { get; set; } = "";

    /// <summary>
    /// Logo đã nạp để hiển thị (không lưu xuống database). Null khi người dùng
    /// chưa chọn logo hoặc file đã bị mất.
    /// </summary>
    [ObservableProperty]
    private Bitmap? logoBitmap;

    /// <summary>Ảnh QR thanh toán/liên hệ do người dùng tải lên (không lưu xuống database).</summary>
    [ObservableProperty]
    private Bitmap? qrBitmap;

    // =========================================================
    // TÙY CHỈNH MẪU IN
    // =========================================================

    public bool HienThiLogo { get; set; } = true;
    public bool HienThiQr { get; set; } = true;
    public bool HienThiFacebook { get; set; } = true;
    public bool HienThiNganHang { get; set; } = true;
    public bool HienThiDiaChiKhachHang { get; set; } = true;

    /// <summary>Khổ giấy in: 58mm, 80mm hoặc A4.</summary>
    public string KhoGiayIn { get; set; } = KhoGiay80;

    /// <summary>Kích thước QR khi in, tính bằng mm.</summary>
    public int KichThuocQr { get; set; } = 32;

    /// <summary>Vị trí QR trong phần cuối hóa đơn: left / center / right.</summary>
    public string ViTriQr { get; set; } = ViTriGiua;

    public string LoiCamOn { get; set; } = "";

    // =========================================================
    // GIÁ TRỊ MẶC ĐỊNH BAN ĐẦU
    // =========================================================

    /// <summary>
    /// Bộ thông tin mặc định của cửa hàng. Chỉ dùng để gieo dữ liệu lần đầu;
    /// sau đó người dùng sửa trực tiếp trong màn "Cài đặt hóa đơn in".
    /// </summary>
    public static ThongTinTiem TaoMacDinh() => new()
    {
        TenTiem = "Vựa Hoa Nhà Huy",
        DiaChi = "101 đường Trần Bình Trọng, NK, TPCT",
        SoDienThoai = "0357 886 765",
        Zalo = "0376 879 469",
        Facebook = "Vựa Hoa Nhà Huy Cần Thơ",
        TenNganHang = "Techcombank",
        SoTaiKhoan = "9376 8794 68",
        ChuTaiKhoan = "Trương Đan Huy",
        LoiCamOn = "Cảm ơn quý khách! Hẹn gặp lại."
    };

    // =========================================================
    // CHUỖI HIỂN THỊ
    // =========================================================

    /// <summary>File logo có dùng được để hiển thị không.</summary>
    public bool CoLogo => LogoBitmap is not null;

    /// <summary>File QR có dùng được để hiển thị không.</summary>
    public bool CoQr => QrBitmap is not null;

    /// <summary>Logo được bật và có ảnh — dùng cho bản xem trước trên màn hình.</summary>
    public bool CoLogoInHoaDon => HienThiLogo && CoLogo;

    /// <summary>QR được bật và có ảnh — dùng cho bản xem trước trên màn hình.</summary>
    public bool CoQrInHoaDon => HienThiQr && CoQr;

    /// <summary>Lời cảm ơn cuối hóa đơn. Người dùng xoá hết thì lấy mặc định thay vì để hóa đơn trống mất dòng kết.</summary>
    public string LoiCamOnHienThi =>
        string.IsNullOrWhiteSpace(LoiCamOn) ? "Cảm ơn quý khách!" : LoiCamOn.Trim();

    /// <summary>Địa chỉ kèm tiền tố "ĐC:" đúng như cách in ra hóa đơn.</summary>
    public string DiaChiHienThi
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DiaChi))
                return "";
            var dc = DiaChi.Trim();
            return dc.StartsWith("ĐC:", StringComparison.OrdinalIgnoreCase) ? dc : $"ĐC: {dc}";
        }
    }

    /// <summary>Số điện thoại kèm tiền tố "SĐT:" đúng như cách in ra hóa đơn.</summary>
    public string SoDienThoaiHienThi =>
        string.IsNullOrWhiteSpace(SoDienThoai) ? "" : $"SĐT: {SoDienThoai.Trim()}";

    /// <summary>Dòng điện thoại + Zalo gộp lại cho gọn trên hóa đơn.</summary>
    public string DienThoaiZaloHienThi
    {
        get
        {
            var sdt = (SoDienThoai ?? "").Trim();
            var zalo = (Zalo ?? "").Trim();

            if (sdt.Length == 0 && zalo.Length == 0)
                return "";

            if (sdt.Length > 0 && zalo.Length > 0)
                return $"Sđt/zalo: {sdt} - {zalo}";

            return sdt.Length > 0 ? $"Sđt: {sdt}" : $"Zalo: {zalo}";
        }
    }

    public string FacebookHienThi
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Facebook))
                return "";
            var fb = Facebook.Trim();
            if (fb.StartsWith("Face:", StringComparison.OrdinalIgnoreCase) ||
                fb.StartsWith("Facebook:", StringComparison.OrdinalIgnoreCase))
                return fb;
            return $"Face: {fb}";
        }
    }

    public bool CoFacebook =>
        HienThiFacebook && !string.IsNullOrWhiteSpace(Facebook);

    public string NganHangChiTiet
    {
        get
        {
            var phan = new List<string>(3);

            if (!string.IsNullOrWhiteSpace(SoTaiKhoan))
                phan.Add(SoTaiKhoan.Trim());

            if (!string.IsNullOrWhiteSpace(TenNganHang))
                phan.Add(TenNganHang.Trim());

            if (!string.IsNullOrWhiteSpace(ChuTaiKhoan))
                phan.Add(ChuTaiKhoan.Trim());

            return phan.Count == 0 ? "" : string.Join(" - ", phan);
        }
    }

    public string StkHienThi
    {
        get
        {
            var chiTiet = NganHangChiTiet;
            if (string.IsNullOrWhiteSpace(chiTiet))
                return "";
            return chiTiet.StartsWith("Stk:", StringComparison.OrdinalIgnoreCase)
                ? chiTiet
                : $"Stk: {chiTiet}";
        }
    }

    public string NganHangHienThi => StkHienThi;

    public bool CoThongTinNganHang =>
        HienThiNganHang && !string.IsNullOrWhiteSpace(NganHangChiTiet);

    /// <summary>Mã số thuế kèm nhãn, rỗng nếu người dùng chưa nhập.</summary>
    public string MaSoThueHienThi =>
        string.IsNullOrWhiteSpace(MaSoThue) ? "" : $"MST: {MaSoThue.Trim()}";

    /// <summary>Website kèm nhãn, rỗng nếu người dùng chưa nhập.</summary>
    public string WebsiteHienThi =>
        string.IsNullOrWhiteSpace(Website) ? "" : Website.Trim();

    /// <summary>Có ít nhất một dòng thông tin liên hệ để in ra hóa đơn không.</summary>
    public bool CoThongTinLienHe =>
        !string.IsNullOrWhiteSpace(DiaChi)
        || !string.IsNullOrWhiteSpace(SoDienThoai)
        || KenhLienHes.Any(x => x.HienThi && !string.IsNullOrWhiteSpace(x.NoiDung))
        || !string.IsNullOrWhiteSpace(MaSoThue);

    /// <summary>Khổ giấy đã chuẩn hoá, dùng cho CSS khi in.</summary>
    public string KhoGiayHienThi => KhoGiayIn switch
    {
        KhoGiay58 => KhoGiay58,
        KhoGiayA4 => KhoGiayA4,
        _ => KhoGiay80
    };

    /// <summary>Kích thước QR đã kẹp trong khoảng hợp lý (mm).</summary>
    public int KichThuocQrHienThi => Math.Clamp(KichThuocQr <= 0 ? 32 : KichThuocQr, 16, 80);

    /// <summary>Vị trí QR đã chuẩn hoá thành class CSS.</summary>
    public string ViTriQrHienThi => ViTriQr switch
    {
        ViTriTrai => ViTriTrai,
        ViTriPhai => ViTriPhai,
        _ => ViTriGiua
    };

    partial void OnLogoBitmapChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(CoLogo));
        OnPropertyChanged(nameof(CoLogoInHoaDon));
    }

    partial void OnQrBitmapChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(CoQr));
        OnPropertyChanged(nameof(CoQrInHoaDon));
    }
}
