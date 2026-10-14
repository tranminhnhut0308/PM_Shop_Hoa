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
    // Đánh dấu form đã bị đóng (Hủy hoặc nút X) để không Close lần nữa sau khi await.
    private bool _daDongForm;

    public KhachHang Customer { get; }

    public bool IsEditMode { get; }

    public IReadOnlyList<NhomKhachHang> DanhSachNhom { get; }

    public NhomKhachHang? NhomKhachHangDaChon { get; set; }

    public string TieuDe =>
        IsEditMode
            ? "Sửa khách hàng"
            : "Thêm khách hàng";

    /// <summary>
    /// Kiểm tra trùng số điện thoại do nơi gọi cung cấp
    /// (form không truy cập database trực tiếp).
    /// Tham số: số điện thoại, id khách hàng hiện tại (0 = thêm mới).
    /// Trả về true nếu số đã tồn tại.
    /// </summary>
    public Func<string?, uint, Task<bool>>? TrungSoDienThoaiAsync { get; }

    public KhachHangEditorWindow(
        KhachHang? customer,
        IReadOnlyList<NhomKhachHang>? nhomKhachHangOptions,
        bool isEditMode,
        Func<string?, uint, Task<bool>>? trungSoDienThoaiAsync = null)
    {
        IsEditMode = isEditMode;
        TrungSoDienThoaiAsync = trungSoDienThoaiAsync;
        DanhSachNhom = nhomKhachHangOptions ?? Array.Empty<NhomKhachHang>();

        // Tạo bản sao để khi bấm Hủy
        // dữ liệu trong danh sách không bị thay đổi.
        Customer = SaoChep(customer);

        NhomKhachHangDaChon =
            DanhSachNhom.FirstOrDefault(
                nhom => nhom.Id == Customer.NhomKhachHangId);

        // Gán dữ liệu trước khi dựng giao diện để các binding đọc đúng giá trị
        InitializeComponent();

        Closed += (_, _) => _daDongForm = true;

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

    private async void OnSaveClicked(
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

        var so1 = this.FindControl<TextBox>("SoDienThoai1Box")?.Text?.Trim().Replace(" ", "");
        var so2 = this.FindControl<TextBox>("SoDienThoai2Box")?.Text?.Trim().Replace(" ", "");
        var so3 = this.FindControl<TextBox>("SoDienThoai3Box")?.Text?.Trim().Replace(" ", "");

        var danhSach = new List<string>();
        if (!string.IsNullOrWhiteSpace(so1)) danhSach.Add(so1);
        if (!string.IsNullOrWhiteSpace(so2)) danhSach.Add(so2);
        if (!string.IsNullOrWhiteSpace(so3)) danhSach.Add(so3);
        Customer.SoDienThoai = danhSach.Count > 0 ? string.Join(" - ", danhSach) : null;
        Customer.Email = Customer.Email?.Trim();
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
        // KIỂM TRA TRÙNG SỐ ĐIỆN THOẠI
        // ==============================

        if (TrungSoDienThoaiAsync is not null &&
            !string.IsNullOrWhiteSpace(Customer.SoDienThoai))
        {
            var nutLuu = this.FindControl<Button>("NutLuuButton");

            if (nutLuu is not null)
                nutLuu.IsEnabled = false;

            try
            {
                if (await TrungSoDienThoaiAsync(Customer.SoDienThoai, Customer.Id))
                {
                    HienLoi($"Số điện thoại {Customer.SoDienThoai} đã tồn tại. Vui lòng dùng số khác.");

                    this.FindControl<TextBox>("SoDienThoaiBox")?.Focus();

                    return;
                }
            }
            catch (Exception ex)
            {
                HienLoi("Không thể kiểm tra số điện thoại: " + ex.Message);

                return;
            }
            finally
            {
                if (nutLuu is not null)
                    nutLuu.IsEnabled = true;
            }
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

        // Trong lúc chờ kiểm tra số điện thoại, người dùng có thể đã đóng form.
        if (_daDongForm)
            return;

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

    private int _soLuongSoDienThoai = 1;

    private void OnThemSoDienThoai(object? sender, RoutedEventArgs e)
    {
        if (_soLuongSoDienThoai >= 3) return;

        _soLuongSoDienThoai++;

        if (_soLuongSoDienThoai == 2)
        {
            this.FindControl<TextBox>("SoDienThoai2Box")!.IsVisible = true;
        }
        else if (_soLuongSoDienThoai == 3)
        {
            this.FindControl<TextBox>("SoDienThoai3Box")!.IsVisible = true;
            this.FindControl<Button>("BtnThemSo")!.IsEnabled = false; // hết chỗ thêm
        }
    }

    private void OnEmailLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox textBox && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            var text = textBox.Text.Trim();
            // Nếu người dùng nhập tên mà chưa có đuôi @, tự động cộng thêm @gmail.com
            if (!text.Contains("@"))
            {
                textBox.Text = text + "@gmail.com";
            }
        }
    }


}
