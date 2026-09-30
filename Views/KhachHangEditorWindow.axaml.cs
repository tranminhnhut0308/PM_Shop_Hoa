using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using System.Globalization;

namespace ShopHoa.Views;

/// <summary>
/// Form thêm / sửa khách hàng.
/// Chỉ dùng cho thêm mới và chỉnh sửa; xem chi tiết nằm ngay trong danh sách.
/// </summary>
public partial class KhachHangEditorWindow : Window
{
    public KhachHang Customer { get; }

    public bool IsEditMode { get; }

    public IReadOnlyList<NhomKhachHang> DanhSachNhom { get; }

    public NhomKhachHang? NhomKhachHangDaChon { get; set; }

    public string TieuDe =>
        IsEditMode
            ? "Sửa khách hàng"
            : "Thêm khách hàng";

    public KhachHangEditorWindow(
        KhachHang? customer,
        IReadOnlyList<NhomKhachHang>? nhomKhachHangOptions,
        bool isEditMode)
    {
        IsEditMode = isEditMode;
        DanhSachNhom = nhomKhachHangOptions ?? Array.Empty<NhomKhachHang>();

        // Tạo bản sao để khi bấm Hủy
        // dữ liệu trong danh sách không bị thay đổi.
        Customer = SaoChep(customer);

        NhomKhachHangDaChon =
            DanhSachNhom.FirstOrDefault(
                nhom => nhom.Id == Customer.NhomKhachHangId);

        // Gán dữ liệu trước khi dựng giao diện để các binding đọc đúng giá trị
        InitializeComponent();

        ChonGioiTinh(Customer.GioiTinh);

        // Ngày sinh nhập theo định dạng Việt Nam
        var ngaySinhBox = this.FindControl<TextBox>("NgaySinhBox");

        if (ngaySinhBox is not null && Customer.NgaySinh.HasValue)
            ngaySinhBox.Text =
                Customer.NgaySinh.Value.ToString("dd/MM/yyyy");

        DataContext = this;
    }

    private static KhachHang SaoChep(KhachHang? customer)
    {
        if (customer is null)
        {
            return new KhachHang
            {
                Id = 0,
                MaKhachHang = "",
                TenKhachHang = "",
                LoaiKhachHang = "CA_NHAN",
                TrangThai = true,
                NgayTao = DateTime.Now
            };
        }

        return new KhachHang
        {
            Id = customer.Id,
            MaKhachHang = customer.MaKhachHang,
            TenKhachHang = customer.TenKhachHang,
            LoaiKhachHang = customer.LoaiKhachHang,
            SoDienThoai = customer.SoDienThoai,
            NgaySinh = customer.NgaySinh,
            GioiTinh = customer.GioiTinh,
            Email = customer.Email,
            Facebook = customer.Facebook,
            DiaChi = customer.DiaChi,
            PhuongXa = customer.PhuongXa,
            QuanHuyen = customer.QuanHuyen,
            TinhThanh = customer.TinhThanh,
            NhomKhachHangId = customer.NhomKhachHangId,
            MaSoThue = customer.MaSoThue,
            TenNguoiMuaHoaDon = customer.TenNguoiMuaHoaDon,
            TenCongTy = customer.TenCongTy,
            DiaChiXuatHoaDon = customer.DiaChiXuatHoaDon,
            GhiChu = customer.GhiChu,
            TrangThai = customer.TrangThai,
            NgayTao = customer.NgayTao,
            NgayCapNhat = customer.NgayCapNhat
        };
    }

    /// <summary>
    /// Đồng bộ giới tính của khách hàng vào ComboBox.
    /// "Chưa có" = không nhập, "Nam" = Nam, "Nữ" = Nữ, "Khác" = Khác.
    /// </summary>
    private void ChonGioiTinh(string? gioiTinh)
    {
        var gioiTinhBox = this.FindControl<ComboBox>("GioiTinhBox");

        if (gioiTinhBox is null)
            return;

        var value = gioiTinh?.Trim();

        var index = value?.ToLowerInvariant() switch
        {
            "nam" => 1,
            "nữ" or "nu" or "nữ " => 2,
            "khác" or "khac" => 3,
            _ => 0
        };

        gioiTinhBox.SelectedIndex = index;
    }

    private string LayGioiTinh()
    {
        var gioiTinhBox = this.FindControl<ComboBox>("GioiTinhBox");

        var content = (gioiTinhBox?.SelectedItem as ComboBoxItem)?.Content?.ToString();

        return content == "Chưa có" || string.IsNullOrWhiteSpace(content)
            ? ""
            : content;
    }

    private void HienLoi(string message)
    {
        var loiMessage = this.FindControl<TextBlock>("LoiMessageTextBlock");

        if (loiMessage is not null)
            loiMessage.Text = message;
    }

    private void OnSaveClicked(
        object? sender,
        RoutedEventArgs e)
    {
        HienLoi("");

        var loiMessage = this.FindControl<TextBlock>("LoiMessageTextBlock");
        var tenBox = this.FindControl<TextBox>("TenKhachHangBox");
        var ngaySinhBox = this.FindControl<TextBox>("NgaySinhBox");

        // ==============================
        // CHUẨN HÓA DỮ LIỆU
        // ==============================

        Customer.MaKhachHang = Customer.MaKhachHang?.Trim() ?? "";
        Customer.TenKhachHang = Customer.TenKhachHang?.Trim() ?? "";
        Customer.SoDienThoai = Customer.SoDienThoai?.Trim();
        Customer.Email = Customer.Email?.Trim();
        Customer.Facebook = Customer.Facebook?.Trim();
        Customer.DiaChi = Customer.DiaChi?.Trim();
        Customer.PhuongXa = Customer.PhuongXa?.Trim();
        Customer.QuanHuyen = Customer.QuanHuyen?.Trim();
        Customer.TinhThanh = Customer.TinhThanh?.Trim();
        Customer.MaSoThue = Customer.MaSoThue?.Trim();
        Customer.TenNguoiMuaHoaDon = Customer.TenNguoiMuaHoaDon?.Trim();
        Customer.TenCongTy = Customer.TenCongTy?.Trim();
        Customer.DiaChiXuatHoaDon = Customer.DiaChiXuatHoaDon?.Trim();
        Customer.GhiChu = Customer.GhiChu?.Trim();
        Customer.GioiTinh = LayGioiTinh();
        Customer.NhomKhachHangId = NhomKhachHangDaChon?.Id;

        // ==============================
        // KIỂM TRA TÊN KHÁCH HÀNG
        // ==============================

        if (string.IsNullOrWhiteSpace(Customer.TenKhachHang))
        {
            HienLoi("Vui lòng nhập tên khách hàng.");

            tenBox?.Focus();

            return;
        }

        // ==============================
        // KIỂM TRA EMAIL
        // ==============================

        if (!string.IsNullOrWhiteSpace(Customer.Email) &&
            !Customer.Email.Contains("@"))
        {
            HienLoi("Email không hợp lệ.");

            return;
        }

        // ==============================
        // NGÀY SINH
        // ==============================

        Customer.NgaySinh = null;

        var ngaySinhText = ngaySinhBox?.Text?.Trim();

        if (!string.IsNullOrWhiteSpace(ngaySinhText))
        {
            if (!DateTime.TryParseExact(
                    ngaySinhText,
                    ["dd/MM/yyyy", "d/M/yyyy"],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var ngaySinh) &&
                !DateTime.TryParse(
                    ngaySinhText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out ngaySinh))
            {
                HienLoi("Ngày sinh không hợp lệ. Vui lòng nhập theo định dạng dd/MM/yyyy.");

                ngaySinhBox?.Focus();

                return;
            }

            if (ngaySinh > DateTime.Today)
            {
                HienLoi("Ngày sinh không thể lớn hơn ngày hôm nay.");

                ngaySinhBox?.Focus();

                return;
            }

            Customer.NgaySinh = ngaySinh;
        }

        // ==============================
        // LƯU
        // ==============================

        if (!IsEditMode)
            Customer.NgayTao = DateTime.Now;

        Customer.NgayCapNhat = DateTime.Now;

        if (loiMessage is not null)
            loiMessage.Text = "";

        Close(true);
    }

    private void OnCancelClicked(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }
}
