using System;
using System.IO;
using System.Security.Cryptography;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một tập tin chứng từ đính kèm (hóa đơn giấy, biên bản giao nhận, ảnh lỗi/hỏng,...)
/// Ánh xạ với bảng dinh_kem_phieu_nhap trong CSDL.
/// </summary>
public sealed class DinhKemPhieuNhap
{
    public ulong Id { get; set; }
    public uint PhieuNhapId { get; set; }
    public string MaPhieuNhap { get; set; } = "";
    public string TenFileGoc { get; set; } = "";
    public string TenFileLuu { get; set; } = "";
    public string DuongDanTuongDoi { get; set; } = "";
    public string DinhDang { get; set; } = "";
    public ulong DungLuong { get; set; }
    public string LoaiChungTu { get; set; } = "HOA_DON_GIAY";
    public string? GhiChu { get; set; }
    public uint? NguoiTaiLenId { get; set; }
    public string TenNguoiTaiLen { get; set; } = "";
    public DateTime ThoiGianTaiLen { get; set; } = DateTime.Now;
    public bool DaXoa { get; set; }
    public DateTime? ThoiGianXoa { get; set; }
    public uint? NguoiXoaId { get; set; }
    public string? TenNguoiXoa { get; set; }
    public string? LyDoXoa { get; set; }

    /// <summary>Đường dẫn vật lý tuyệt đối trên máy tính.</summary>
    public string? DuongDanTuyetDoi { get; set; }

    /// <summary>Có phải file ảnh hay không (để hiển thị preview/thumbnail).</summary>
    public bool IsImage =>
        string.Equals(DinhDang, "JPG", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(DinhDang, "JPEG", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(DinhDang, "PNG", StringComparison.OrdinalIgnoreCase);

    /// <summary>Có phải file tài liệu PDF hay không.</summary>
    public bool IsPdf =>
        string.Equals(DinhDang, "PDF", StringComparison.OrdinalIgnoreCase);

    /// <summary>Dung lượng hiển thị thân thiện (KB, MB).</summary>
    public string DungLuongHienThi
    {
        get
        {
            if (DungLuong < 1024)
                return $"{DungLuong} B";
            if (DungLuong < 1024 * 1024)
                return $"{(DungLuong / 1024.0):F1} KB";
            return $"{(DungLuong / (1024.0 * 1024.0)):F2} MB";
        }
    }

    /// <summary>Thời gian tải lên định dạng thân thiện.</summary>
    public string ThoiGianTaiLenHienThi =>
        ThoiGianTaiLen.ToString("dd/MM/yyyy HH:mm");
}

/// <summary>
/// Dùng để quản lý file chứng từ tạm trên UI trong lúc lập hoặc sửa phiếu nhập hàng.
/// </summary>
public sealed class ChungTuItemModel
{
    public string TempId { get; set; } = Guid.NewGuid().ToString("N");
    public ulong? ExistingId { get; set; } // Nếu là file đã lưu từ DB
    public string TenFileGoc { get; set; } = "";
    public string? DuongDanNguon { get; set; } // File gốc trên máy hoặc file copy trong AppData
    public string? DuongDanTam { get; set; }   // File copy trong thư mục chứng từ tạm
    public string DinhDang { get; set; } = "";
    public long DungLuong { get; set; }
    public string LoaiChungTu { get; set; } = "HOA_DON_GIAY";
    public string? GhiChu { get; set; }
    public string NguoiTaiLen { get; set; } = "";
    public DateTime ThoiGianTaiLen { get; set; } = DateTime.Now;

    public bool IsExisting => ExistingId.HasValue && ExistingId.Value > 0;
    public bool IsImage =>
        string.Equals(DinhDang, "JPG", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(DinhDang, "JPEG", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(DinhDang, "PNG", StringComparison.OrdinalIgnoreCase);
    public bool IsPdf =>
        string.Equals(DinhDang, "PDF", StringComparison.OrdinalIgnoreCase);

    public string DungLuongHienThi
    {
        get
        {
            if (DungLuong < 1024)
                return $"{DungLuong} B";
            if (DungLuong < 1024 * 1024)
                return $"{(DungLuong / 1024.0):F1} KB";
            return $"{(DungLuong / (1024.0 * 1024.0)):F2} MB";
        }
    }

    public string ThoiGianTaiLenHienThi =>
        ThoiGianTaiLen.ToString("dd/MM/yyyy HH:mm");

    public string IconHienThi => IsPdf ? "📄" : (IsImage ? "🖼" : "📎");
}
