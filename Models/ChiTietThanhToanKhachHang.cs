using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShopHoa.Models;

/// <summary>
/// Một chứng từ (hóa đơn bán hàng / phiếu nhập / mã tham chiếu khác) liên quan
/// đến phiếu thu thanh toán công nợ khách hàng đang xem.
/// </summary>
public sealed class ChungTuLienQuanItem
{
    public string MaChungTu { get; set; } = "";
    public bool LaHoaDon { get; set; }
    public bool LaPhieuNhap { get; set; }
    public decimal SoTienPhanBo { get; set; }

    public bool CoTheMo => LaHoaDon || LaPhieuNhap;

    public string LoaiHienThi => LaHoaDon
        ? "Hóa đơn"
        : LaPhieuNhap
            ? "Phiếu nhập"
            : "Tham chiếu";

    public string SoTienPhanBoHienThi =>
        SoTienPhanBo > 0 ? $"{SoTienPhanBo:N0} đ" : "—";
}

/// <summary>
/// Dữ liệu đầy đủ của một giao dịch thanh toán công nợ khách hàng dùng cho cửa
/// sổ chi tiết, biên bản in / PDF / Excel / sao chép nội dung (Issue #102).
/// <para>
/// Toàn bộ số tiền lấy nguyên vẹn từ bản ghi <c>lich_su_thanh_toan_cong_no_khach_hang</c>
/// do database trả về — model không tự tính lại công nợ để tránh lệch sổ cái.
/// </para>
/// </summary>
public sealed class ChiTietThanhToanKhachHang
{
    public ThanhToanCongNoKhachHang GiaoDich { get; set; } = new();
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public IReadOnlyList<ChungTuLienQuanItem> ChungTuLienQuans { get; set; } = [];
    public ThongTinTiem? ThongTinTiem { get; set; }

    // =========================================================
    // THÔNG TIN GIAO DỊCH
    // =========================================================

    public string MaPhieuThu => GiaoDich.MaGiaoDich;
    public string LoaiGiaoDich => "Thu tiền khách hàng";
    public string TrangThaiHienThi => GiaoDich.TrangThaiHienThi;
    public bool DaHuy => GiaoDich.DaHuy;
    public DateTime ThoiGian => GiaoDich.ThoiGian;
    public string ThoiGianText => GiaoDich.ThoiGianText;
    public string PhuongThucHienThi => GiaoDich.PhuongThucHienThi;
    public string NguoiThucHien => GiaoDich.NguoiThucHien;
    public string GhiChuHienThi => GiaoDich.GhiChuHienThi;
    public string MaThamChieuHienThi => GiaoDich.MaThamChieuHienThi;

    // =========================================================
    // CHI TIẾT CÔNG NỢ (số liệu gốc từ database, không tự tính lại)
    // =========================================================

    public decimal CongNoTruoc => GiaoDich.CongNoTruoc;
    public decimal SoTien => GiaoDich.SoTien;
    public decimal CongNoSau => GiaoDich.CongNoSau;

    public string CongNoTruocText => $"{CongNoTruoc:N0} đ";
    public string SoTienText => $"{SoTien:N0} đ";
    public string CongNoSauText => $"{CongNoSau:N0} đ";

    public bool DaTraHet => CongNoSau <= 0;

    /// <summary>Chuỗi mã các chứng từ liên quan, phân cách bởi dấu phẩy.</summary>
    public string MaChungTuLienQuanText =>
        ChungTuLienQuans.Count == 0
            ? "—"
            : string.Join(", ", ChungTuLienQuans.Select(x => x.MaChungTu));

    /// <summary>Tổng tiền đã phân bổ vào các hóa đơn của phiếu thu này.</summary>
    public decimal TongTienPhanBo =>
        ChungTuLienQuans.Where(x => x.SoTienPhanBo > 0).Sum(x => x.SoTienPhanBo);

    /// <summary>Số tiền thanh toán chưa gắn với chứng từ nào.</summary>
    public decimal TienChuaPhanBo => Math.Max(0m, SoTien - TongTienPhanBo);

    public string TenCuaHang => ThongTinTiem?.TenTiem?.Trim() ?? "";

    // =========================================================
    // TẠO DỮ LIỆU CHI TIẾT
    // =========================================================

    /// <summary>
    /// Gộp danh sách phân bổ hóa đơn và mã tham chiếu thành danh sách chứng từ
    /// liên quan đầy đủ, không trùng lặp. Giao dịch phân bổ cho nhiều hóa đơn
    /// thì hiển thị đủ tất cả, không chỉ một mã.
    /// </summary>
    public static ChiTietThanhToanKhachHang Tao(
        ThanhToanCongNoKhachHang record,
        string maKhachHang,
        string tenKhachHang,
        IReadOnlyList<PhanBoThanhToanKhachHang> allocations,
        string? maThamChieu = null)
    {
        var result = new ChiTietThanhToanKhachHang
        {
            GiaoDich = record,
            MaKhachHang = string.IsNullOrWhiteSpace(maKhachHang) ? record.MaKhachHang : maKhachHang.Trim(),
            TenKhachHang = tenKhachHang
        };

        var danhSach = new List<ChungTuLienQuanItem>();

        foreach (var allocation in allocations)
        {
            if (string.IsNullOrWhiteSpace(allocation.MaHoaDon))
                continue;

            if (danhSach.Any(x => x.MaChungTu.Equals(allocation.MaHoaDon.Trim(), StringComparison.OrdinalIgnoreCase)))
                continue;

            danhSach.Add(new ChungTuLienQuanItem
            {
                MaChungTu = allocation.MaHoaDon.Trim(),
                LaHoaDon = true,
                SoTienPhanBo = allocation.SoTien
            });
        }

        foreach (var ma in TachMaThamChieu(maThamChieu))
        {
            if (danhSach.Any(x => x.MaChungTu.Equals(ma, StringComparison.OrdinalIgnoreCase)))
                continue;

            danhSach.Add(new ChungTuLienQuanItem
            {
                MaChungTu = ma,
                LaHoaDon = LaMaHoaDon(ma),
                LaPhieuNhap = LaMaPhieuNhap(ma)
            });
        }

        result.ChungTuLienQuans = danhSach;
        return result;
    }

    /// <summary>
    /// Tách mã tham chiếu: cho phép một chuỗi chứa nhiều mã cách nhau bởi
    /// dấu phẩy / chấm phẩy / khoảng trắng.
    /// </summary>
    public static IReadOnlyList<string> TachMaThamChieu(string? maThamChieu)
    {
        if (string.IsNullOrWhiteSpace(maThamChieu))
            return Array.Empty<string>();

        return maThamChieu
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0)
            .ToList();
    }

    /// <summary>Nhận diện mã hóa đơn bán hàng theo tiền tố HD (HD261111-001...).</summary>
    public static bool LaMaHoaDon(string ma) =>
        ma.StartsWith("HD", StringComparison.OrdinalIgnoreCase);

    /// <summary>Nhận diện mã phiếu nhập hàng theo tiền tố PN (PN000001...).</summary>
    public static bool LaMaPhieuNhap(string ma) =>
        ma.StartsWith("PN", StringComparison.OrdinalIgnoreCase);

    // =========================================================
    // SAO CHÉP NỘI DỤNG GỬI KHÁCH HÀNG (Zalo / Messenger / SMS)
    // =========================================================

    /// <summary>
    /// Dựng nội dung văn bản dễ đọc để nhân viên gửi cho khách hàng qua Zalo /
    /// Messenger / tin nhắn. Chỉ chứa thông tin nghiệp vụ cần thiết, không có
    /// dữ liệu kỹ thuật.
    /// </summary>
    public string TaoNoiDungSaoChep()
    {
        var sb = new StringBuilder();

        sb.AppendLine("BIÊN NHẬN THANH TOÁN CÔNG NỢ");
        if (!string.IsNullOrWhiteSpace(TenCuaHang))
            sb.AppendLine(TenCuaHang);
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Mã phiếu thu: {MaPhieuThu}");
        sb.AppendLine($"Thời gian: {ThoiGianText}");
        sb.AppendLine($"Khách hàng: {MaKhachHang} - {TenKhachHang}");
        sb.AppendLine($"Phương thức thanh toán: {PhuongThucHienThi}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Công nợ trước thanh toán: {CongNoTruoc:N0} đ");
        sb.AppendLine($"Số tiền đã thanh toán: {SoTien:N0} đ");
        sb.AppendLine($"Công nợ còn lại: {CongNoSau:N0} đ");

        if (ChungTuLienQuans.Count > 0)
            sb.AppendLine($"Chứng từ liên quan: {MaChungTuLienQuanText}");

        if (DaTraHet && !DaHuy)
            sb.AppendLine("Trạng thái: Đã thanh toán hết công nợ.");
        else if (DaHuy)
            sb.AppendLine($"Trạng thái: Phiếu đã hủy ({GiaoDich.ThoiGianHuyText}).");

        if (!string.IsNullOrWhiteSpace(GiaoDich.GhiChu))
            sb.AppendLine($"Ghi chú: {GiaoDich.GhiChu.Trim()}");

        sb.AppendLine($"Người thực hiện: {NguoiThucHien}");

        var loiCamOn = ThongTinTiem?.LoiCamOnHienThi;
        if (!string.IsNullOrWhiteSpace(loiCamOn))
            sb.AppendLine(loiCamOn);

        return sb.ToString().TrimEnd();
    }
}
