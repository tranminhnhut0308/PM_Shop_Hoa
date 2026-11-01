using System;

namespace ShopHoa.Helpers;

/// <summary>
/// Quy chuẩn sinh tên file tự động cho các báo cáo trong hệ thống ShopHoa.
/// Tránh trùng tên khi xuất nhiều lần liên tiếp: {LoaiBaoCao}_YYYYMMDD_HHmmss.xlsx
/// </summary>
public static class ReportFileNamingHelper
{
    /// <summary>
    /// Tạo tên file gợi ý theo loại báo cáo và thời gian hiện tại.
    /// </summary>
    public static string TaoTenFile(string loaiBaoCao)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{loaiBaoCao}_{timestamp}.xlsx";
    }

    public static string TaoTenFileBanHang() => TaoTenFile("BaoCaoBanHang");
    public static string TaoTenFileCuoiNgay() => TaoTenFile("BaoCaoCuoiNgay");
    public static string TaoTenFileHangHoa() => TaoTenFile("BaoCaoHangHoa");
    public static string TaoTenFileKhachHang() => TaoTenFile("BaoCaoKhachHang");
    public static string TaoTenFileNhaCungCap() => TaoTenFile("BaoCaoNhaCungCap");
}
