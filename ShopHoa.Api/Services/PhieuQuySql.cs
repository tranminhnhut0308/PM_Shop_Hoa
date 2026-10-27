namespace ShopHoa.Api.Services;

/// <summary>
/// Biểu thức SQL xác định phiếu thu/chi trong bảng <c>phieu_chi</c>, dùng chung
/// cho các báo cáo của API. Bảng phải được alias là <c>pc</c>. Phiếu cũ chưa có
/// <c>loai_phieu</c> thì lùi về quy tắc PT là thu, còn lại là chi.
/// </summary>
internal static class PhieuQuySql
{
    public const string LoaiBieuThuc =
        "CASE WHEN UPPER(TRIM(COALESCE(pc.loai_phieu, ''))) IN ('THU', 'CHI') "
        + "THEN UPPER(TRIM(pc.loai_phieu)) "
        + "WHEN TRIM(pc.ma_phieu_chi) LIKE 'PT%' THEN 'THU' ELSE 'CHI' END";

    public const string LaThu = LoaiBieuThuc + " = 'THU'";

    public const string LaChi = LoaiBieuThuc + " = 'CHI'";
}
