namespace ShopHoa.Services;

/// <summary>
/// Biểu thức SQL xác định một phiếu trong bảng <c>phieu_chi</c> là thu hay chi.
/// <para>
/// Cột <c>loai_phieu</c> lưu loại của phiếu nhập từ file (mã có thể là TT, CTM...
/// chứ không chỉ PT/PC). Phiếu cũ chưa có giá trị trong cột này thì lùi về quy tắc
/// cũ: mã bắt đầu bằng "PT" là thu, còn lại là chi.
/// </para>
/// <para>
/// Dùng chung một biểu thức cho bộ lọc, tổng hợp và báo cáo để không nơi nào
/// tính thu/chi lệch nhau. Mọi truy vấn dùng biểu thức này phải alias bảng
/// <c>phieu_chi</c> là <c>pc</c>.
/// </para>
/// </summary>
internal static class PhieuQuySql
{
    public const string LoaiBieuThuc =
        "CASE WHEN UPPER(TRIM(COALESCE(pc.loai_phieu, ''))) IN ('THU', 'CHI') "
        + "THEN UPPER(TRIM(pc.loai_phieu)) "
        + "WHEN TRIM(pc.ma_phieu_chi) LIKE 'PT%' THEN 'THU' ELSE 'CHI' END";

    public const string LaThu = LoaiBieuThuc + " = 'THU'";

    public const string LaChi = LoaiBieuThuc + " = 'CHI'";

    public const string CotLoai = LoaiBieuThuc + " AS LoaiPhieu";
}
