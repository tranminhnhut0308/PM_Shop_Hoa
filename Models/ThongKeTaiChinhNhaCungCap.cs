namespace ShopHoa.Models;

/// <summary>
/// Tổng hợp tài chính của một nhà cung cấp, dùng cho vùng chi tiết và tab Công nợ.
/// Toàn bộ số liệu lấy từ database, không có giá trị mặc định giả.
/// </summary>
public sealed class ThongKeTaiChinhNhaCungCap
{
    /// <summary>Số phiếu nhập hàng đã ghi nhận cho nhà cung cấp.</summary>
    public int SoPhieuNhap { get; set; }

    /// <summary>Tổng tiền hàng + chi phí khác của các phiếu nhập.</summary>
    public decimal TongTienNhap { get; set; }

    /// <summary>Tổng tiền đã thanh toán trên các phiếu nhập.</summary>
    public decimal TongDaThanhToan { get; set; }

    /// <summary>Tổng nợ còn lại trên các phiếu nhập.</summary>
    public decimal TongConNoPhieuNhap { get; set; }

    /// <summary>Số lần ghi vào sổ cái công nợ của nhà cung cấp.</summary>
    public int SoLanGhiCongNo { get; set; }

    /// <summary>Công nợ hiện tại theo sổ cái công nợ (ghi nợ - ghi có).</summary>
    public decimal TongCongNoLichSu { get; set; }

    /// <summary>Thời điểm ghi công nợ gần nhất, null nếu chưa có phát sinh.</summary>
    public DateTime? LanGhiCongNoGanNhat { get; set; }

    /// <summary>Nhà cung cấp có số tiền lấy từ file danh sách không.</summary>
    public bool CoDuLieuKiotViet { get; set; }

    /// <summary>Nợ cần trả hiện tại ghi trong file danh sách (null nếu file không có).</summary>
    public decimal? NoCanTraHienTaiExcel { get; set; }

    /// <summary>Tổng mua ghi trong file danh sách (null nếu file không có).</summary>
    public decimal? TongMuaExcel { get; set; }

    /// <summary>Tổng mua trừ trả hàng ghi trong file danh sách (null nếu file không có).</summary>
    public decimal? TongMuaTruTraHangExcel { get; set; }

    /// <summary>Hệ thống đã có dữ liệu tài chính cho nhà cung cấp này chưa.</summary>
    public bool CoDuLieu =>
        CoDuLieuKiotViet
        || SoPhieuNhap > 0
        || SoLanGhiCongNo > 0
        || TongTienNhap > 0
        || TongDaThanhToan > 0
        || TongCongNoHienTai > 0;

    /// <summary>
    /// Công nợ hiện tại: khi có dữ liệu từ file danh sách thì lấy đúng số trong
    /// file (giống cột "Còn nợ" của bảng), chưa có thì ưu tiên sổ cái công nợ,
    /// cuối cùng mới lấy tổng nợ còn lại trên các phiếu nhập hàng.
    /// </summary>
    public decimal TongCongNoHienTai =>
        CoDuLieuKiotViet && NoCanTraHienTaiExcel.HasValue
            ? NoCanTraHienTaiExcel.Value
            : SoLanGhiCongNo > 0 ? TongCongNoLichSu : TongConNoPhieuNhap;

    /// <summary>Tổng tiền đã mua: ưu tiên số trong file danh sách.</summary>
    public decimal TongMua =>
        CoDuLieuKiotViet && TongMuaExcel.HasValue
            ? TongMuaExcel.Value
            : TongTienNhap;

    /// <summary>Tổng tiền đã mua trừ trả hàng: ưu tiên số trong file danh sách.</summary>
    public decimal TongMuaTruTraHang =>
        CoDuLieuKiotViet && TongMuaTruTraHangExcel.HasValue
            ? TongMuaTruTraHangExcel.Value
            : TongMua;
}
