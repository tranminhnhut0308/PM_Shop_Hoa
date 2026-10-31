namespace ShopHoa.Services;

/// <summary>
/// Nội dung tĩnh cho cửa sổ "SHOPHOA – HỆ THỐNG VÀ TRỢ GIÚP".
/// <para>
/// Toàn bộ nội dung trợ giúp nằm ở đây để không phải sửa file giao diện lớn.
/// Chỉ công bố những phím tắt đã được triển khai thật trong MainWindow.
/// </para>
/// </summary>
public static class HelpContent
{
    public const string TenUngDung = "SHOPHOA";
    public const string KhauHieu = "HỆ THỐNG VÀ TRỢ GIÚP";
    public const string MoTaNgan =
        "Phần mềm quản lý shop hoa: bán hàng, hàng hóa, khách hàng, nhà cung cấp, "
        + "nhập hàng, hóa đơn, sổ quỹ và báo cáo trong một màn hình thống nhất.";

    public static IReadOnlyList<HelpFeature> TinhNang { get; } =
    [
        new("🧾", "Bán hàng nhanh",
            "Tìm sản phẩm, chọn khách hàng, chỉnh số lượng và thanh toán ngay trên một màn hình."),
        new("🌼", "Quản lý hàng hóa",
            "Theo dõi mã hàng, nhóm hàng, đơn vị tính, giá bán, giá nhập và tồn kho."),
        new("📥", "Nhập hàng nhiều phiếu",
            "Mở nhiều phiếu nhập nháp cùng lúc; dữ liệu được tự động lưu để làm tiếp sau."),
        new("👥", "Khách hàng & công nợ",
            "Quản lý thông tin khách hàng, lịch sử mua hàng và công nợ cần thu."),
        new("🏪", "Nhà cung cấp",
            "Theo dõi nhà cung cấp, công nợ cần trả và lịch sử nhập hàng."),
        new("📊", "Sổ quỹ & báo cáo",
            "Ghi nhận thu chi, xem báo cáo bán hàng, hàng hóa, khách hàng và cuối ngày."),
    ];

    public static IReadOnlyList<HelpShortcutGroup> NhomPhimTat { get; } =
    [
        new("Chung", "⌨", "Dùng được ở hầu hết các màn hình chính.",
        [
            new("F1", "Mở cửa sổ Trợ giúp", "Toàn ứng dụng"),
            new("Ctrl + F", "Đưa con trỏ vào ô tìm kiếm của màn hình hiện tại", "Theo màn hình"),
            new("F5", "Làm mới dữ liệu đang xem", "Theo màn hình"),
        ]),
        new("Bán hàng", "🧾", "Áp dụng khi màn hình Bán hàng đang mở.",
        [
            new("Ctrl + F", "Tìm hàng hóa", "Màn hình Bán hàng"),
            new("Ctrl + K", "Chuyển đến ô tìm khách hàng", "Màn hình Bán hàng"),
            new("Enter", "Thêm sản phẩm đầu tiên trong kết quả tìm hàng", "Ô tìm hàng hóa"),
            new("Enter", "Chọn khách hàng đầu tiên trong gợi ý", "Ô tìm khách hàng"),
            new("Enter", "Xác nhận số lượng của dòng trong giỏ", "Ô số lượng"),
            new("Ctrl + Enter", "Mở bước thanh toán khi giỏ đã có hàng", "Màn hình Bán hàng"),
            new("Esc", "Đóng màn hình Bán hàng (hỏi lại nếu giỏ còn hàng)", "Màn hình Bán hàng"),
        ]),
        new("Nhập hàng", "📥", "Áp dụng khi đang lập phiếu nhập.",
        [
            new("Ctrl + N", "Tạo phiếu nhập mới", "Màn hình Nhập hàng"),
            new("Ctrl + S", "Lưu nháp phiếu nhập hiện tại", "Form lập phiếu nhập"),
            new("Ctrl + F", "Tìm hàng hóa để thêm vào phiếu", "Form lập phiếu nhập"),
            new("Ctrl + K", "Chuyển đến ô tìm nhà cung cấp", "Form lập phiếu nhập"),
            new("Enter", "Thêm hàng hóa đầu tiên trong kết quả tìm", "Ô tìm hàng hóa"),
            new("Enter", "Chọn nhà cung cấp đầu tiên trong gợi ý", "Ô tìm nhà cung cấp"),
        ]),
        new("Danh mục", "🗂", "Áp dụng ở Hàng hóa, Khách hàng, Nhà cung cấp và Sổ quỹ.",
        [
            new("Ctrl + F", "Tìm kiếm trong danh sách đang mở", "Màn hình danh mục"),
            new("Ctrl + N", "Tạo mới theo màn hình đang mở", "Hàng hóa, Khách hàng, Nhà cung cấp"),
            new("Ctrl + N", "Mở form lập phiếu thu mới", "Sổ quỹ"),
            new("F5", "Làm mới danh sách đang xem", "Màn hình danh mục"),
        ]),
        new("Cửa sổ Trợ giúp", "❓", "Dùng ngay trong cửa sổ này.",
        [
            new("Esc", "Đóng cửa sổ Trợ giúp", "Cửa sổ Trợ giúp"),
            new("Ctrl + F", "Tìm nhanh trong danh sách hướng dẫn", "Tab Hướng dẫn sử dụng"),
        ]),
    ];

    public static IReadOnlyList<HelpGuide> HuongDan { get; } =
    [
        new(
            "ban-hang",
            "🧾",
            "Bán hàng và thanh toán",
            "Quy trình cơ bản để lập một hóa đơn bán hàng.",
            ["bán hàng", "hóa đơn", "thanh toán", "giỏ hàng", "khách lẻ"],
            [
                new(1, "Mở màn hình Bán hàng",
                    "Chọn Bán hàng trên thanh menu, hoặc bấm nút Bán hàng ở khu vực làm việc."),
                new(2, "Thêm hàng vào giỏ",
                    "Gõ tên hoặc mã hàng vào ô tìm kiếm bên trái. Bấm vào sản phẩm để thêm vào giỏ. "
                    + "Có thể nhấn Enter để thêm sản phẩm đầu tiên trong kết quả."),
                new(3, "Chọn khách hàng",
                    "Gõ tên hoặc số điện thoại vào ô Khách hàng. Bấm Khách lẻ nếu không cần lưu thông tin khách."),
                new(4, "Kiểm tra giỏ hàng",
                    "Chỉnh số lượng, giá bán hoặc giảm giá cho từng dòng. Kiểm tra tổng tiền trước khi thanh toán."),
                new(5, "Thanh toán",
                    "Bấm Thanh toán hoặc nhấn Ctrl + Enter. Nhập tiền khách đưa nếu cần và xác nhận để lưu hóa đơn."),
            ],
            "Sau khi thanh toán, tồn kho và sổ quỹ được cập nhật theo hóa đơn. Nếu giỏ còn hàng, "
            + "nhấn Esc sẽ hỏi lại trước khi đóng màn hình Bán hàng."),
        new(
            "nhap-hang",
            "📥",
            "Lập phiếu nhập hàng",
            "Tạo, lưu nháp và hoàn tất phiếu nhập từ nhà cung cấp.",
            ["nhập hàng", "phiếu nhập", "nhà cung cấp", "lưu nháp", "tồn kho"],
            [
                new(1, "Mở form lập phiếu",
                    "Vào Nhập hàng và chọn Hàng hóa mới, hoặc nhấn Ctrl + N khi đang ở màn hình Nhập hàng."),
                new(2, "Chọn nhà cung cấp",
                    "Gõ tên nhà cung cấp vào ô tìm kiếm và chọn từ gợi ý. Có thể thêm nhanh nhà cung cấp mới."),
                new(3, "Thêm hàng hóa",
                    "Tìm hàng hóa, chọn đơn vị tính và nhập số lượng, giá nhập cho từng dòng."),
                new(4, "Lưu nháp",
                    "Nhấn Ctrl + S hoặc bấm Lưu nháp để cất phiếu đang làm. Phiếu nháp không thay đổi tồn kho, "
                    + "công nợ hay sổ quỹ."),
                new(5, "Hoàn thành phiếu",
                    "Kiểm tra lại chi phí, đã thanh toán và ghi chú, sau đó bấm Hoàn thành để ghi nhận phiếu nhập."),
            ],
            "Có thể mở nhiều phiếu nhập nháp cùng lúc. Phần mềm tự lưu định kỳ khi ngừng thao tác, "
            + "nhưng vẫn nên bấm Ctrl + S trước khi đóng ứng dụng."),
        new(
            "hang-hoa",
            "🌼",
            "Quản lý hàng hóa",
            "Thêm, sửa, tìm kiếm và theo dõi tồn kho hàng hóa.",
            ["hàng hóa", "sản phẩm", "tồn kho", "giá bán", "nhóm hàng", "đơn vị tính"],
            [
                new(1, "Tìm hàng hóa",
                    "Vào Hàng hóa, gõ mã hoặc tên hàng vào ô tìm kiếm. Nhấn Ctrl + F để đưa con trỏ vào ô này."),
                new(2, "Thêm hàng mới",
                    "Bấm Thêm hàng hóa hoặc nhấn Ctrl + N. Nhập mã, tên, nhóm hàng, đơn vị tính và giá bán."),
                new(3, "Chỉnh sửa",
                    "Bấm vào dòng hàng hóa để xem chi tiết hoặc dùng nút sửa. Kiểm tra kỹ giá bán trước khi lưu."),
                new(4, "Nhập hoặc xuất Excel",
                    "Dùng nút Nhập Excel / Xuất Excel khi cần cập nhật danh mục hàng loạt."),
            ],
            "Tồn kho thay đổi khi hoàn tất phiếu nhập hoặc hóa đơn bán hàng. "
            + "Không sửa trực tiếp tồn kho nếu chưa kiểm tra nghiệp vụ."),
        new(
            "khach-hang",
            "👥",
            "Khách hàng và công nợ",
            "Quản lý thông tin khách hàng, lịch sử mua hàng và số tiền cần thu.",
            ["khách hàng", "công nợ", "thu nợ", "số điện thoại", "lịch sử mua"],
            [
                new(1, "Tìm khách hàng",
                    "Vào Khách hàng và nhập mã, tên hoặc số điện thoại. Nhấn Ctrl + F để focus ô tìm kiếm."),
                new(2, "Thêm khách hàng",
                    "Bấm Thêm khách hàng hoặc nhấn Ctrl + N. Nhập tên và số điện thoại chính xác."),
                new(3, "Xem công nợ",
                    "Bấm vào khách hàng để xem chi tiết, lịch sử mua và công nợ hiện tại."),
                new(4, "Thu nợ",
                    "Từ chi tiết khách hàng, chọn thu nợ và nhập số tiền khách thanh toán."),
            ],
            "Nên kiểm tra lại số điện thoại trước khi lưu để tránh trùng khách hàng."),
        new(
            "hoa-don",
            "📄",
            "Tra cứu và xử lý hóa đơn",
            "Tìm hóa đơn, xem chi tiết, in lại hoặc xử lý điều chỉnh.",
            ["hóa đơn", "in hóa đơn", "tra cứu", "điều chỉnh", "trả hàng"],
            [
                new(1, "Tìm hóa đơn",
                    "Vào Hóa đơn và nhập mã hóa đơn hoặc tên khách hàng. Có thể lọc nhanh theo ngày."),
                new(2, "Xem chi tiết",
                    "Bấm vào hóa đơn để xem hàng hóa, thanh toán và thông tin khách hàng."),
                new(3, "In hoặc xuất hóa đơn",
                    "Dùng nút in trong chi tiết hóa đơn để in lại hoặc xuất file."),
                new(4, "Điều chỉnh khi cần",
                    "Chỉ thực hiện điều chỉnh/trả hàng khi đã kiểm tra đúng hóa đơn và nhập lý do rõ ràng."),
            ],
            "Mọi thao tác điều chỉnh đều có xác nhận. Nên ghi lý do cụ thể để tra cứu lại sau này."),
        new(
            "nha-cung-cap",
            "🏪",
            "Nhà cung cấp",
            "Quản lý danh bạ nhà cung cấp và công nợ cần trả.",
            ["nhà cung cấp", "công nợ", "trả nợ", "nhập hàng", "danh bạ"],
            [
                new(1, "Tìm nhà cung cấp",
                    "Vào Nhà cung cấp, nhập mã, tên, số điện thoại hoặc email. Nhấn Ctrl + F để focus ô tìm kiếm."),
                new(2, "Thêm nhà cung cấp",
                    "Bấm Thêm nhà cung cấp hoặc nhấn Ctrl + N."),
                new(3, "Xem công nợ",
                    "Bấm vào nhà cung cấp để xem chi tiết công nợ và lịch sử nhập hàng."),
                new(4, "Thanh toán công nợ",
                    "Từ chi tiết nhà cung cấp, chọn thanh toán công nợ và nhập số tiền đã trả."),
            ],
            "Khi lập phiếu nhập, nên chọn đúng nhà cung cấp để công nợ được ghi nhận chính xác."),
        new(
            "so-quy",
            "💰",
            "Sổ quỹ thu chi",
            "Ghi nhận phiếu thu, phiếu chi và đối chiếu số dư quỹ.",
            ["sổ quỹ", "thu chi", "phiếu thu", "phiếu chi", "tồn quỹ"],
            [
                new(1, "Mở Sổ quỹ",
                    "Chọn Sổ quỹ trên thanh menu để xem danh sách phiếu và số dư."),
                new(2, "Tạo phiếu thu",
                    "Bấm lập phiếu thu hoặc nhấn Ctrl + N. Chọn quỹ, người nộp, số tiền và nội dung."),
                new(3, "Tạo phiếu chi",
                    "Chọn lập phiếu chi, nhập người nhận, số tiền và lý do chi."),
                new(4, "Đối chiếu",
                    "Dùng ô tìm kiếm và bộ lọc thời gian để đối chiếu phiếu với thực tế."),
            ],
            "Phiếu thu/chi ảnh hưởng trực tiếp đến số dư quỹ. Kiểm tra số tiền và nội dung trước khi lưu."),
        new(
            "bao-cao",
            "📊",
            "Xem báo cáo",
            "Theo dõi kết quả bán hàng, hàng hóa, khách hàng, nhà cung cấp và cuối ngày.",
            ["báo cáo", "doanh thu", "lợi nhuận", "cuối ngày", "biểu đồ", "excel"],
            [
                new(1, "Chọn loại báo cáo",
                    "Vào Báo cáo và chọn mục phù hợp ở danh sách bên trái."),
                new(2, "Chọn khoảng thời gian",
                    "Dùng các nút Hôm nay, Tuần này, Tháng này hoặc chọn khoảng ngày tùy chỉnh."),
                new(3, "Xem và lọc dữ liệu",
                    "Nhấn F5 để làm mới báo cáo đang xem. Một số báo cáo có ô tìm kiếm riêng."),
                new(4, "Xuất Excel",
                    "Dùng nút Xuất Excel khi cần lưu lại hoặc gửi báo cáo cho người khác."),
            ],
            "Số liệu báo cáo phụ thuộc vào hóa đơn, phiếu nhập và phiếu thu/chi đã hoàn tất."),
    ];

    public static IReadOnlyList<string> DiemMoi { get; } =
    [
        "Cửa sổ Trợ giúp tập trung 5 nhóm nội dung: giới thiệu, phím tắt, hướng dẫn, cập nhật và hỗ trợ.",
        "Bộ phím tắt thao tác nhanh cho bán hàng, nhập hàng và các danh mục chính.",
        "Nhiều phiếu nhập nháp cùng lúc, tự động lưu và khôi phục khi mở lại phần mềm.",
        "Tra cứu nhanh hơn nhờ phím tắt đưa con trỏ thẳng vào ô tìm kiếm.",
        "Thông tin phiên bản và kênh hỗ trợ được gom về một nơi để dễ báo lỗi.",
    ];

    public static IReadOnlyList<HelpUpdateNote> LuuYCapNhat { get; } =
    [
        new("💾", "Dữ liệu được giữ nguyên",
            "Cập nhật phần mềm không xóa dữ liệu trên máy chủ. Tuy vậy, nên hoàn tất hoặc lưu nháp các phiếu đang làm."),
        new("🌐", "Cần kết nối Internet",
            "Quá trình kiểm tra và tải bản cập nhật cần mạng ổn định."),
        new("🔁", "Khởi động lại sau khi cập nhật",
            "Khi tải xong, bấm Khởi động lại ngay trong cửa sổ cập nhật để áp dụng phiên bản mới."),
    ];

    public static string TimKiemHuongDan(HelpGuide guide)
    {
        var parts = new List<string>
        {
            guide.TieuDe,
            guide.MoTa,
            guide.GhiChu,
        };

        parts.AddRange(guide.TuKhoa);
        parts.AddRange(guide.CacBuoc.Select(b => b.TieuDe + " " + b.NoiDung));

        return string.Join(" ", parts);
    }

    public static bool KhopTuKhoa(HelpGuide guide, string tuKhoa)
    {
        if (string.IsNullOrWhiteSpace(tuKhoa))
            return true;

        var noiDung = TimKiemHuongDan(guide);

        foreach (var tu in tuKhoa.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (noiDung.Contains(tu, StringComparison.CurrentCultureIgnoreCase))
                return true;
        }

        return false;
    }
}

public sealed record HelpFeature(string BieuTuong, string TieuDe, string MoTa);

public sealed record HelpShortcut(string Phim, string HanhDong, string PhamVi);

public sealed record HelpShortcutGroup(
    string TieuDe,
    string BieuTuong,
    string MoTa,
    IReadOnlyList<HelpShortcut> PhimTat);

public sealed record HelpGuideStep(int ThuTu, string TieuDe, string NoiDung);

public sealed record HelpGuide(
    string Khoa,
    string BieuTuong,
    string TieuDe,
    string MoTa,
    IReadOnlyList<string> TuKhoa,
    IReadOnlyList<HelpGuideStep> CacBuoc,
    string GhiChu);

public sealed record HelpUpdateNote(string BieuTuong, string TieuDe, string MoTa);
