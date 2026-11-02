using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ShopHoa.Helpers;

/// <summary>
/// Helper chuẩn hóa từ khóa và chấm điểm độ tương đồng / thứ bậc ưu tiên tìm kiếm sản phẩm.
/// Áp dụng thống nhất cho màn hình bán hàng, phiếu nhập và danh mục hàng hóa.
/// </summary>
public static class ProductSearchHelper
{
    private static readonly (string CoDau, string KhongDau)[] BangChuanHoaTiengViet =
    [
        ("đ", "d"), ("Đ", "d"),
        ("á", "a"), ("à", "a"), ("ả", "a"), ("ã", "a"), ("ạ", "a"),
        ("â", "a"), ("ấ", "a"), ("ầ", "a"), ("ẩ", "a"), ("ẫ", "a"), ("ậ", "a"),
        ("ă", "a"), ("ắ", "a"), ("ằ", "a"), ("ẳ", "a"), ("ẵ", "a"), ("ặ", "a"),
        ("é", "e"), ("è", "e"), ("ẻ", "e"), ("ẽ", "e"), ("ẹ", "e"),
        ("ê", "e"), ("ế", "e"), ("ề", "e"), ("ể", "e"), ("ễ", "e"), ("ệ", "e"),
        ("í", "i"), ("ì", "i"), ("ỉ", "i"), ("ĩ", "i"), ("ị", "i"),
        ("ó", "o"), ("ò", "o"), ("ỏ", "o"), ("õ", "o"), ("ọ", "o"),
        ("ô", "o"), ("ố", "o"), ("ồ", "o"), ("ổ", "o"), ("ỗ", "o"), ("ộ", "o"),
        ("ơ", "o"), ("ớ", "o"), ("ờ", "o"), ("ở", "o"), ("ỡ", "o"), ("ợ", "o"),
        ("ú", "u"), ("ù", "u"), ("ủ", "u"), ("ũ", "u"), ("ụ", "u"),
        ("ư", "u"), ("ứ", "u"), ("ừ", "u"), ("ử", "u"), ("ữ", "u"), ("ự", "u"),
        ("ý", "y"), ("ỳ", "y"), ("ỷ", "y"), ("ỹ", "y"), ("ỵ", "y")
    ];

    /// <summary>
    /// Chuẩn hóa chuỗi văn bản:
    /// - Trim khoảng trắng đầu cuối
    /// - Gộp nhiều khoảng trắng liền nhau thành 1
    /// - Chuẩn hóa Unicode (FormD tách dấu, loại bỏ NonSpacingMark)
    /// - Chuyển chữ thường, thay đ/Đ thành d
    /// </summary>
    public static string ChuanHoa(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Chuẩn hóa FormC trước để gom các ký tự tổ hợp rời rạc thành dạng dựng sẵn chuẩn
        var normalized = input.Trim().Normalize(NormalizationForm.FormC);

        // Sau đó chuyển sang FormD để tách dấu phụ
        var formD = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);

        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(c))
            {
                builder.Append(' ');
            }
            else if (c is 'đ' or 'Đ')
            {
                builder.Append('d');
            }
            else
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        // Gộp khoảng trắng thừa
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Tách từ khóa thành các token từ đơn, chỉ giữ ký tự chữ và số.
    /// </summary>
    public static string[] TachTuKhoa(string? keyword)
    {
        var chuanHoa = ChuanHoa(keyword);
        if (chuanHoa.Length == 0)
            return [];

        var tokens = new List<string>();
        var sb = new StringBuilder();

        foreach (var c in chuanHoa)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
                sb.Clear();
            }
        }

        if (sb.Length > 0)
            tokens.Add(sb.ToString());

        return [.. tokens.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Biểu thức SQL thay thế dấu tiếng Việt trên cột database để so sánh đồng nhất với C#.
    /// </summary>
    public static string BieuThucChuanHoaSql(string cot)
    {
        var bieuThuc = $"LOWER({cot})";
        foreach (var (coDau, khongDau) in BangChuanHoaTiengViet)
        {
            bieuThuc = $"REPLACE({bieuThuc}, '{coDau}', '{khongDau}')";
        }
        return $"COALESCE({bieuThuc}, '')";
    }

    /// <summary>
    /// Kiểm tra xem sản phẩm có khớp với từ khóa tìm kiếm hay không.
    /// Nếu từ khóa rỗng -> luôn true.
    /// Nếu có từ khóa -> tất cả token từ khóa phải có mặt trong tên, mã hàng hoặc mã vạch (AND tokens).
    /// </summary>
    public static bool KhopTimKiem(
        string? rawKeyword,
        string? maHang,
        string? maVach,
        string? tenHang)
    {
        if (string.IsNullOrWhiteSpace(rawKeyword))
            return true;

        var tokens = TachTuKhoa(rawKeyword);
        if (tokens.Length == 0)
            return true;

        var normMa = ChuanHoa(maHang);
        var normVach = ChuanHoa(maVach);
        var normTen = ChuanHoa(tenHang);

        foreach (var token in tokens)
        {
            if (!normTen.Contains(token, StringComparison.OrdinalIgnoreCase) &&
                !normMa.Contains(token, StringComparison.OrdinalIgnoreCase) &&
                !normVach.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Chấm điểm độ ưu tiên sắp xếp cho một sản phẩm trong bộ nhớ C#.
    /// Điểm càng nhỏ thì độ ưu tiên càng cao (0 là cao nhất).
    /// Thứ bậc:
    /// 1. Khớp chính xác toàn bộ tên sản phẩm (Điểm 10), khớp mã hàng (Điểm 12), khớp mã vạch (Điểm 15)
    /// 2. Khớp chính xác một phần tên sản phẩm:
    ///    - Tên sản phẩm bắt đầu bằng cụm từ khóa (Điểm 30)
    ///    - Cụm từ khóa xuất hiện nguyên vẹn trong tên (ranh giới từ: Điểm 40, chuỗi con: Điểm 50)
    ///    - Mã hàng hoặc mã vạch bắt đầu bằng từ khóa (Điểm 45)
    /// 3. Khớp đầy đủ các từ khóa nhưng khác thứ tự (Điểm 60)
    /// 4. Khớp một phần từ khóa (Điểm 80)
    /// </summary>
    public static int TinhDiemUuTien(
        string? rawKeyword,
        string? maHang,
        string? maVach,
        string? tenHang)
    {
        var normQuery = ChuanHoa(rawKeyword);
        if (string.IsNullOrEmpty(normQuery))
            return 999;

        var normMa = ChuanHoa(maHang);
        var normVach = ChuanHoa(maVach);
        var normTen = ChuanHoa(tenHang);

        // 1. Khớp chính xác toàn bộ tên sản phẩm hoặc mã
        if (normTen == normQuery)
            return 10;
        if (normMa == normQuery)
            return 12;
        if (!string.IsNullOrEmpty(normVach) && normVach == normQuery)
            return 15;

        // 2. Khớp chính xác một phần tên sản phẩm (bắt đầu bằng cụm từ khóa)
        if (normTen.StartsWith(normQuery, StringComparison.OrdinalIgnoreCase))
            return 30;

        // Mã hàng hoặc mã vạch bắt đầu bằng từ khóa
        if (normMa.StartsWith(normQuery, StringComparison.OrdinalIgnoreCase))
            return 45;
        if (!string.IsNullOrEmpty(normVach) && normVach.StartsWith(normQuery, StringComparison.OrdinalIgnoreCase))
            return 48;

        // Cụm từ khóa xuất hiện nguyên vẹn trong tên sản phẩm
        var idx = normTen.IndexOf(normQuery, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            // Kiểm tra ranh giới từ (ký tự trước và sau là khoảng trắng hoặc đầu/cuối chuỗi)
            var isWordBoundaryStart = idx == 0 || char.IsWhiteSpace(normTen[idx - 1]);
            var endIdx = idx + normQuery.Length;
            var isWordBoundaryEnd = endIdx == normTen.Length || char.IsWhiteSpace(normTen[endIdx]);

            if (isWordBoundaryStart && isWordBoundaryEnd)
                return 40; // Khớp nguyên từ hoàn chỉnh (ví dụ từ "pháp" trong "Hồng Đỏ Pháp")
            if (isWordBoundaryStart)
                return 42; // Bắt đầu từ một từ
            return 50;     // Chuỗi con
        }

        // 3. Khớp tất cả các từ trong từ khóa nhưng khác thứ tự (AND tokens)
        var tokens = TachTuKhoa(rawKeyword);
        if (tokens.Length > 1)
        {
            var allMatch = true;
            foreach (var token in tokens)
            {
                if (!normTen.Contains(token, StringComparison.OrdinalIgnoreCase) &&
                    !normMa.Contains(token, StringComparison.OrdinalIgnoreCase) &&
                    !normVach.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
                return 60;
        }

        // 4. Khớp một phần từ khóa
        if (tokens.Length > 0)
        {
            foreach (var token in tokens)
            {
                if (normTen.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                    normMa.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                    normVach.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return 80;
                }
            }
        }

        return 100;
    }
}
