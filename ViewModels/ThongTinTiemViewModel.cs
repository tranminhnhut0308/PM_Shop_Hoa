using System.Diagnostics;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>Một lựa chọn trong các ô cấu hình mẫu in.</summary>
public sealed record LuaChonHoaDon(string GiaTri, string Nhan)
{
    public override string ToString() => Nhan;
}

/// <summary>
/// ViewModel cho màn hình "Cài đặt hóa đơn in" (trước đây là "Thông tin tiệm").
/// <para>
/// Màn này chỉ làm một việc: đọc và ghi bản ghi cấu hình duy nhất qua
/// <see cref="StoreInfoService"/>. Không sao chép dữ liệu ra biến tĩnh, không tự
/// gọi database riêng — nhờ vậy hóa đơn và mọi mẫu in sau đó đọc đúng thông tin
/// vừa lưu, không cần làm gì thêm.
/// </para>
/// </summary>
public sealed partial class ThongTinTiemViewModel : ObservableObject
{
    private readonly StoreInfoService _service;

    public ThongTinTiemViewModel(StoreInfoService service)
    {
        _service = service;
    }

    /// <summary>Lấy cửa sổ chính để mở hộp thoại chọn ảnh (gán từ MainWindowViewModel).</summary>
    public Func<Window?> LayCuaSoChinh { get; set; } = static () => null;

    /// <summary>Cập nhật dòng trạng thái chung của cửa sổ chính.</summary>
    public Action<string>? CapNhatTrangThai { get; set; }

    /// <summary>Hiện hộp thoại thông báo (tiêu đề, nội dung) cho người dùng.</summary>
    public Func<string, string, Task>? HienThiHopNhat { get; set; }

    /// <summary>
    /// Người dùng có quyền sửa cấu hình không (gán từ MainWindowViewModel).
    /// Khi false thì các ô nhập và nút lưu bị khoá, nhưng vẫn xem được.
    /// </summary>
    public Func<bool>? CoTheChinhSua { get; set; }

    private bool DuocPhepChinhSua => CoTheChinhSua?.Invoke() ?? true;

    // ============================================================
    // LỰA CHỌN MẪU IN
    // ============================================================

    public IReadOnlyList<LuaChonHoaDon> KhoGiayOptions { get; } =
    [
        new(ThongTinTiem.KhoGiay58, "58mm (máy in nhiệt)"),
        new(ThongTinTiem.KhoGiay80, "80mm (máy in nhiệt)"),
        new(ThongTinTiem.KhoGiayA4, "A4")
    ];

    public IReadOnlyList<LuaChonHoaDon> ViTriQrOptions { get; } =
    [
        new(ThongTinTiem.ViTriTrai, "Trái"),
        new(ThongTinTiem.ViTriGiua, "Giữa"),
        new(ThongTinTiem.ViTriPhai, "Phải")
    ];

    public IReadOnlyList<LuaChonHoaDon> KichThuocQrOptions { get; } =
    [
        new("24", "Nhỏ (24mm)"),
        new("32", "Vừa (32mm)"),
        new("40", "Lớn (40mm)"),
        new("50", "Rất lớn (50mm)")
    ];

    // ============================================================
    // TRẠNG THÁI MÀN HÌNH
    // ============================================================

    /// <summary>Đang tải thông tin đã lưu từ database.</summary>
    [ObservableProperty]
    private bool dangTai = true;

    /// <summary>Đang ghi xuống database, khoá nút "Lưu" để tránh bấm hai lần.</summary>
    [ObservableProperty]
    private bool dangLuu;

    /// <summary>Thông báo lỗi hiển thị ngay trên màn hình (rỗng = không lỗi).</summary>
    [ObservableProperty]
    private string thongBaoLoi = "";

    /// <summary>Logo đã nạp để xem trước.</summary>
    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? logoXemTruoc;

    /// <summary>QR đã nạp để xem trước.</summary>
    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? qrXemTruoc;

    /// <summary>Đường dẫn file logo sẽ lưu cùng bản ghi cấu hình.</summary>
    [ObservableProperty]
    private string duongDanLogo = "";

    /// <summary>Đường dẫn file QR sẽ lưu cùng bản ghi cấu hình.</summary>
    [ObservableProperty]
    private string duongDanQr = "";

    /// <summary>
    /// Người dùng hiện tại có quyền sửa không. Cập nhật mỗi lần mở màn nên
    /// khoá/ mở khoá các ô nhập và nút lưu theo quyền.
    /// </summary>
    [ObservableProperty]
    private bool canChinhSua = true;

    /// <summary>Có logo để hiển thị hay không (dùng cho IsVisible trong XAML).</summary>
    public bool CoLogoXemTruoc => LogoXemTruoc is not null;

    /// <summary>Có QR để hiển thị hay không.</summary>
    public bool CoQrXemTruoc => QrXemTruoc is not null;

    /// <summary>Tên file logo để người dùng biết đang dùng ảnh nào.</summary>
    public string TenLogoHienThi =>
        string.IsNullOrWhiteSpace(DuongDanLogo) ? "Chưa chọn logo" : Path.GetFileName(DuongDanLogo);

    /// <summary>Tên file QR để người dùng biết đang dùng ảnh nào.</summary>
    public string TenQrHienThi =>
        string.IsNullOrWhiteSpace(DuongDanQr) ? "Chưa chọn QR" : Path.GetFileName(DuongDanQr);

    // ============================================================
    // CÁC TRƯỜNG NHẬP
    // ============================================================

    [ObservableProperty] private string tenTiem = "";
    [ObservableProperty] private string diaChi = "";
    [ObservableProperty] private string soDienThoai = "";
    [ObservableProperty] private string zalo = "";
    [ObservableProperty] private string facebook = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string maSoThue = "";
    [ObservableProperty] private string website = "";
    [ObservableProperty] private string tenNganHang = "";
    [ObservableProperty] private string soTaiKhoan = "";
    [ObservableProperty] private string chuTaiKhoan = "";
    [ObservableProperty] private string loiCamOn = "";

    [ObservableProperty] private bool hienThiLogo = true;
    [ObservableProperty] private bool hienThiQr = true;
    [ObservableProperty] private bool hienThiFacebook = true;
    [ObservableProperty] private bool hienThiNganHang = true;
    [ObservableProperty] private bool hienThiDiaChiKhachHang = true;

    [ObservableProperty] private LuaChonHoaDon? khoGiayChon;
    [ObservableProperty] private LuaChonHoaDon? viTriQrChon;
    [ObservableProperty] private LuaChonHoaDon? kichThuocQrChon;

    // ============================================================
    // TẢI / LƯU
    // ============================================================

    /// <summary>
    /// Nạp cấu hình đang lưu. Gọi mỗi lần người dùng mở lại màn hình nên thấy
    /// đúng dữ liệu mới nhất, kể cả sau khi đã lưu.
    /// </summary>
    [RelayCommand]
    private async Task TaiLaiAsync()
    {
        DangTai = true;
        ThongBaoLoi = "";

        try
        {
            var info = await _service.LayThongTinKemLogoAsync();
            ApDungLenManHinh(info);

            CanChinhSua = DuocPhepChinhSua;

            CapNhatTrangThai?.Invoke(
                string.IsNullOrWhiteSpace(info.TenTiem)
                    ? "Chưa có thông tin cửa hàng, hãy nhập và bấm Lưu cấu hình."
                    : $"Đã tải cấu hình hóa đơn in: {info.TenTiem}");
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không tải được cấu hình hóa đơn in: " + ex.Message;
            SafeLog.WriteError("Loi tai thong tin tiem", ex);
        }
        finally
        {
            DangTai = false;
        }
    }

    private void ApDungLenManHinh(ThongTinTiem info)
    {
        TenTiem = info.TenTiem;
        DiaChi = info.DiaChi;
        SoDienThoai = info.SoDienThoai;
        Zalo = info.Zalo;
        Facebook = info.Facebook;
        Email = info.Email;
        MaSoThue = info.MaSoThue;
        Website = info.Website;
        TenNganHang = info.TenNganHang;
        SoTaiKhoan = info.SoTaiKhoan;
        ChuTaiKhoan = info.ChuTaiKhoan;
        LoiCamOn = info.LoiCamOn;

        HienThiLogo = info.HienThiLogo;
        HienThiQr = info.HienThiQr;
        HienThiFacebook = info.HienThiFacebook;
        HienThiNganHang = info.HienThiNganHang;
        HienThiDiaChiKhachHang = info.HienThiDiaChiKhachHang;

        KhoGiayChon = ChonTheoGiaTri(KhoGiayOptions, info.KhoGiayHienThi);
        ViTriQrChon = ChonTheoGiaTri(ViTriQrOptions, info.ViTriQrHienThi);
        KichThuocQrChon = ChonTheoGiaTri(KichThuocQrOptions, info.KichThuocQrHienThi.ToString());

        DuongDanLogo = info.DuongDanLogo;
        LogoXemTruoc = info.LogoBitmap;
        DuongDanQr = info.DuongDanQr;
        QrXemTruoc = info.QrBitmap;
    }

    private static LuaChonHoaDon? ChonTheoGiaTri(
        IReadOnlyList<LuaChonHoaDon> ds, string giaTri)
        => ds.FirstOrDefault(x => string.Equals(x.GiaTri, giaTri, StringComparison.OrdinalIgnoreCase))
           ?? ds.FirstOrDefault();

    /// <summary>
    /// Ghi cấu hình xuống database. Không có bản ghi thì INSERT bản ghi đầu
    /// tiên, đã có thì UPDATE đúng bản ghi đó.
    /// </summary>
    [RelayCommand]
    private async Task LuuAsync()
    {
        if (DangLuu)
            return;

        if (!DuocPhepChinhSua)
        {
            ThongBaoLoi = "Bạn không có quyền sửa cấu hình hóa đơn in.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TenTiem))
        {
            ThongBaoLoi = "Tên cửa hàng không được để trống.";
            await HienThiHopNhatThongBaoAsync(
                "Thiếu tên cửa hàng",
                "Vui lòng nhập tên cửa hàng trước khi lưu.");
            return;
        }

        DangLuu = true;
        ThongBaoLoi = "";

        try
        {
            // Ảnh được chép vào thư mục dữ liệu ứng dụng trước khi ghi, để sau này
            // người dùng di chuyển/xoá file gốc thì hóa đơn vẫn có ảnh.
            var duongDanLogoDaLuu = string.IsNullOrWhiteSpace(DuongDanLogo)
                ? ""
                : await _service.LuuLogoAsync(DuongDanLogo);

            var duongDanQrDaLuu = string.IsNullOrWhiteSpace(DuongDanQr)
                ? ""
                : await _service.LuuQrAsync(DuongDanQr);

            var info = new ThongTinTiem
            {
                TenTiem = TenTiem,
                DiaChi = DiaChi,
                SoDienThoai = SoDienThoai,
                Zalo = Zalo,
                Facebook = Facebook,
                Email = Email,
                MaSoThue = MaSoThue,
                Website = Website,
                TenNganHang = TenNganHang,
                SoTaiKhoan = SoTaiKhoan,
                ChuTaiKhoan = ChuTaiKhoan,
                LoiCamOn = LoiCamOn,
                DuongDanLogo = duongDanLogoDaLuu,
                DuongDanQr = duongDanQrDaLuu,
                HienThiLogo = HienThiLogo,
                HienThiQr = HienThiQr,
                HienThiFacebook = HienThiFacebook,
                HienThiNganHang = HienThiNganHang,
                HienThiDiaChiKhachHang = HienThiDiaChiKhachHang,
                KhoGiayIn = KhoGiayChon?.GiaTri ?? ThongTinTiem.KhoGiay80,
                ViTriQr = ViTriQrChon?.GiaTri ?? ThongTinTiem.ViTriGiua,
                KichThuocQr = int.TryParse(KichThuocQrChon?.GiaTri, out var kt) ? kt : 32
            };

            await _service.LuuThongTinAsync(info);

            // Đọc lại để chắc chắn màn hình và hóa đơn cùng thấy một dữ liệu,
            // đồng thời nạp lại ảnh vừa lưu.
            var daLuu = await _service.LayThongTinKemLogoAsync();
            ApDungLenManHinh(daLuu);

            CanChinhSua = DuocPhepChinhSua;

            const string noiDung = "Đã lưu cấu hình hóa đơn in";

            CapNhatTrangThai?.Invoke(noiDung);

            await HienThiHopNhatThongBaoAsync(
                "Lưu thành công",
                noiDung + ".\n\nCác hóa đơn in từ lần này trở đi sẽ dùng thông tin, "
                + "logo, QR và khổ giấy vừa cấu hình. Hóa đơn cũ không bị thay đổi.");
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không lưu được cấu hình hóa đơn in: " + ex.Message;
            CapNhatTrangThai?.Invoke("❌ " + ThongBaoLoi);
            SafeLog.WriteError("Loi luu thong tin tiem", ex);

            await HienThiHopNhatThongBaoAsync(
                "Không lưu được cấu hình",
                ex.Message);
        }
        finally
        {
            DangLuu = false;
        }
    }

    /// <summary>
    /// Đưa các ô nhập về giá trị mặc định ban đầu. Chưa ghi xuống database —
    /// người dùng xem lại rồi bấm "Lưu cấu hình" mới áp dụng.
    /// </summary>
    [RelayCommand]
    private void KhoiPhucMacDinh()
    {
        if (!DuocPhepChinhSua)
            return;

        var macDinh = ThongTinTiem.TaoMacDinh();

        ApDungLenManHinh(macDinh);

        DuongDanLogo = "";
        LogoXemTruoc = null;
        DuongDanQr = "";
        QrXemTruoc = null;

        ThongBaoLoi = "";
        CapNhatTrangThai?.Invoke(
            "Đã khôi phục giá trị mặc định. Bấm “Lưu cấu hình” để áp dụng.");
    }

    /// <summary>
    /// Chọn logo từ máy tính và xem trước ngay. File được chép vào thư mục dữ
    /// liệu ứng dụng ở bước Lưu, nên việc chọn trước không ghi gì xuống database.
    /// </summary>
    [RelayCommand]
    private Task ChonLogoAsync() => ChonAnhAsync(
        "Chọn logo cho cửa hàng",
        duongDan =>
        {
            LogoXemTruoc = new Avalonia.Media.Imaging.Bitmap(duongDan);
            DuongDanLogo = duongDan;
            CapNhatTrangThai?.Invoke(
                $"Đã chọn logo {Path.GetFileName(duongDan)}. Bấm Lưu cấu hình để áp dụng.");
        },
        "logo");

    /// <summary>Chọn ảnh QR do người dùng cung cấp (PNG/JPG/JPEG).</summary>
    [RelayCommand]
    private Task ChonQrAsync() => ChonAnhAsync(
        "Chọn ảnh QR Code",
        duongDan =>
        {
            QrXemTruoc = new Avalonia.Media.Imaging.Bitmap(duongDan);
            DuongDanQr = duongDan;
            CapNhatTrangThai?.Invoke(
                $"Đã chọn QR {Path.GetFileName(duongDan)}. Bấm Lưu cấu hình để áp dụng.");
        },
        "QR");

    private async Task ChonAnhAsync(
        string tieuDe,
        Action<string> apDung,
        string loai)
    {
        if (!DuocPhepChinhSua)
            return;

        var owner = LayCuaSoChinh();

        if (owner is null)
            return;

        var files = await owner.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = tieuDe,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Ảnh PNG/JPG")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]
                    }
                ]
            });

        var file = files.FirstOrDefault();

        if (file is null)
        {
            CapNhatTrangThai?.Invoke($"Đã hủy chọn {loai}.");
            return;
        }

        var duongDan = file.Path.LocalPath;

        if (string.IsNullOrWhiteSpace(duongDan) || !File.Exists(duongDan))
        {
            ThongBaoLoi = $"Không đọc được file {loai} đã chọn.";
            return;
        }

        try
        {
            apDung(duongDan);
            ThongBaoLoi = "";
        }
        catch (Exception ex)
        {
            ThongBaoLoi = $"File {loai} không hợp lệ: " + ex.Message;
            SafeLog.WriteError($"Loi xem truoc {loai} tiem", ex);
        }
    }

    /// <summary>Bỏ logo khỏi bản ghi (hóa đơn sẽ in không có logo).</summary>
    [RelayCommand]
    private void BoLogo()
    {
        LogoXemTruoc = null;
        DuongDanLogo = "";
        CapNhatTrangThai?.Invoke("Đã bỏ logo. Bấm Lưu cấu hình để áp dụng.");
    }

    /// <summary>Bỏ QR khỏi bản ghi (hóa đơn sẽ in không có QR).</summary>
    [RelayCommand]
    private void BoQr()
    {
        QrXemTruoc = null;
        DuongDanQr = "";
        CapNhatTrangThai?.Invoke("Đã bỏ QR. Bấm Lưu cấu hình để áp dụng.");
    }

    /// <summary>
    /// Dựng hóa đơn mẫu từ các giá trị đang nhập (kể cả chưa lưu) và mở bằng
    /// trình duyệt để người dùng xem trước đúng bản in thực tế.
    /// </summary>
    [RelayCommand]
    private async Task XemTruocAsync()
    {
        try
        {
            var info = TaoThongTinTuManHinh();
            _service.NapAnh(info);

            var receipt = new HoaDonBanHangReceipt
            {
                MaHoaDon = "HD0001",
                ThoiGianBan = DateTime.Now,
                TenKhachHang = "Nguyễn Thị Minh Anh",
                SoDienThoai = "0909 123 456",
                DiaChi = "45 đường 30/4, Ninh Kiều, Cần Thơ",
                TenNhanVien = "Nhân viên bán hàng",
                ThongTinTiem = info,
                Items =
                [
                    new HoaDonBanHangReceiptItem
                    {
                        TenSanPham = "Bó hoa hồng đỏ cao cấp gói giấy Hàn Quốc kèm thiệp chúc mừng",
                        DonViTinh = "Bó",
                        SoLuong = 2,
                        DonGia = 450000,
                        ThanhTien = 900000
                    },
                    new HoaDonBanHangReceiptItem
                    {
                        TenSanPham = "Giỏ trái cây nhập khẩu",
                        DonViTinh = "Giỏ",
                        SoLuong = 1,
                        DonGia = 650000,
                        ThanhTien = 650000
                    }
                ],
                TongTienHang = 1550000,
                GiamGia = 50000,
                KhachCanTra = 1500000,
                TienKhachDua = 1000000,
                KhachDaThanhToan = 1000000,
                ConNo = 500000,
                GhiChu = "Giao trước 17h, gọi khách trước khi giao."
            };

            var html = HoaDonPrintService.TaoHtml(receipt);
            var thuMuc = Path.Combine(Path.GetTempPath(), "ShopHoa", "HoaDon");
            Directory.CreateDirectory(thuMuc);
            var duongDan = Path.Combine(thuMuc, "xem-truoc-hoa-don.html");

            File.WriteAllText(duongDan, html, new System.Text.UTF8Encoding(false));

            Process.Start(new ProcessStartInfo
            {
                FileName = duongDan,
                UseShellExecute = true
            });

            CapNhatTrangThai?.Invoke("Đã mở bản xem trước hóa đơn trong trình duyệt.");
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không mở được bản xem trước: " + ex.Message;
            SafeLog.WriteError("Loi xem truoc hoa don", ex);

            await HienThiHopNhatThongBaoAsync("Không xem trước được", ex.Message);
        }
    }

    private ThongTinTiem TaoThongTinTuManHinh() => new()
    {
        TenTiem = TenTiem,
        DiaChi = DiaChi,
        SoDienThoai = SoDienThoai,
        Zalo = Zalo,
        Facebook = Facebook,
        Email = Email,
        MaSoThue = MaSoThue,
        Website = Website,
        TenNganHang = TenNganHang,
        SoTaiKhoan = SoTaiKhoan,
        ChuTaiKhoan = ChuTaiKhoan,
        LoiCamOn = LoiCamOn,
        DuongDanLogo = DuongDanLogo,
        DuongDanQr = DuongDanQr,
        HienThiLogo = HienThiLogo,
        HienThiQr = HienThiQr,
        HienThiFacebook = HienThiFacebook,
        HienThiNganHang = HienThiNganHang,
        HienThiDiaChiKhachHang = HienThiDiaChiKhachHang,
        KhoGiayIn = KhoGiayChon?.GiaTri ?? ThongTinTiem.KhoGiay80,
        ViTriQr = ViTriQrChon?.GiaTri ?? ThongTinTiem.ViTriGiua,
        KichThuocQr = int.TryParse(KichThuocQrChon?.GiaTri, out var kt) ? kt : 32
    };

    private async Task HienThiHopNhatThongBaoAsync(string tieuDe, string noiDung)
    {
        if (HienThiHopNhat is null)
            return;

        await HienThiHopNhat(tieuDe, noiDung);
    }

    partial void OnLogoXemTruocChanged(Avalonia.Media.Imaging.Bitmap? value)
        => OnPropertyChanged(nameof(CoLogoXemTruoc));

    partial void OnQrXemTruocChanged(Avalonia.Media.Imaging.Bitmap? value)
        => OnPropertyChanged(nameof(CoQrXemTruoc));

    partial void OnDuongDanLogoChanged(string value)
        => OnPropertyChanged(nameof(TenLogoHienThi));

    partial void OnDuongDanQrChanged(string value)
        => OnPropertyChanged(nameof(TenQrHienThi));
}
