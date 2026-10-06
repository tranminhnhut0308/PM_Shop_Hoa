using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Danh mục nhóm quyền + thứ tự hiển thị + key "xem" của từng module.
/// Nguồn quyền duy nhất cho UI phân quyền, menu và các lớp kiểm tra quyền.
/// </summary>
public static class PermissionCatalog
{
    public sealed record GroupDefinition(string Nhom, string TenNhom, string[] Keys);

    public static readonly IReadOnlyList<GroupDefinition> Groups = new[]
    {
        new GroupDefinition("Dashboard", "Tổng quan", new[]
        {
            PermissionKeys.DashboardView
        }),
        new GroupDefinition("Products", "Hàng hóa", new[]
        {
            PermissionKeys.ProductsView,
            PermissionKeys.ProductsDetail,
            PermissionKeys.ProductsCreate,
            PermissionKeys.ProductsEdit,
            PermissionKeys.ProductsUpdateCost,
            PermissionKeys.ProductsUpdatePrice,
            PermissionKeys.ProductsAdjustStock,
            PermissionKeys.ProductsDelete,
            PermissionKeys.ProductsImport,
            PermissionKeys.ProductsExport
        }),
        new GroupDefinition("Sales", "Bán hàng", new[]
        {
            PermissionKeys.SalesView,
            PermissionKeys.SalesCreate,
            PermissionKeys.SalesEdit,
            PermissionKeys.SalesPayment,
            PermissionKeys.SalesHistory,
            PermissionKeys.SalesDelete
        }),
        new GroupDefinition("Invoices", "Hóa đơn", new[]
        {
            PermissionKeys.InvoicesView,
            PermissionKeys.InvoicesCreate,
            PermissionKeys.InvoicesEdit,
            PermissionKeys.InvoicesCancel,
            PermissionKeys.InvoicesDelete,
            PermissionKeys.InvoicesPrint,
            PermissionKeys.InvoicesImport,
            PermissionKeys.InvoicesExport
        }),
        new GroupDefinition("Customers", "Khách hàng", new[]
        {
            PermissionKeys.CustomersView,
            PermissionKeys.CustomersDetail,
            PermissionKeys.CustomersCreate,
            PermissionKeys.CustomersEdit,
            PermissionKeys.CustomersDelete,
            PermissionKeys.CustomersImport,
            PermissionKeys.CustomersExport,
            PermissionKeys.CustomersDebtView,
            PermissionKeys.CustomersDebtPay
        }),
        new GroupDefinition("Suppliers", "Nhà cung cấp", new[]
        {
            PermissionKeys.SuppliersView,
            PermissionKeys.SuppliersDetail,
            PermissionKeys.SuppliersCreate,
            PermissionKeys.SuppliersEdit,
            PermissionKeys.SuppliersDelete,
            PermissionKeys.SuppliersImport,
            PermissionKeys.SuppliersExport,
            PermissionKeys.SuppliersDebtView,
            PermissionKeys.SuppliersDebtPay
        }),
        new GroupDefinition("Purchases", "Phiếu nhập", new[]
        {
            PermissionKeys.PurchasesView,
            PermissionKeys.PurchasesDetail,
            PermissionKeys.PurchasesCreate,
            PermissionKeys.PurchasesEdit,
            PermissionKeys.PurchasesDelete,
            PermissionKeys.PurchasesImport,
            PermissionKeys.PurchasesExport,
            PermissionKeys.PurchasesPrint
        }),
        new GroupDefinition("CashBook", "Sổ quỹ", new[]
        {
            PermissionKeys.CashBookView,
            PermissionKeys.CashBookInit,
            PermissionKeys.CashBookOpeningBalance,
            PermissionKeys.CashBookCreateReceipt,
            PermissionKeys.CashBookCreatePayment,
            PermissionKeys.CashBookEdit,
            PermissionKeys.CashBookDelete,
            PermissionKeys.CashBookCancel,
            PermissionKeys.CashBookDetail,
            PermissionKeys.CashBookImport,
            PermissionKeys.CashBookExport
        }),
        new GroupDefinition("Reports", "Báo cáo", new[]
        {
            PermissionKeys.ReportsView,
            PermissionKeys.ReportsSales,
            PermissionKeys.ReportsPurchases,
            PermissionKeys.ReportsStock,
            PermissionKeys.ReportsCustomerDebt,
            PermissionKeys.ReportsSupplierDebt,
            PermissionKeys.ReportsCashFlow,
            PermissionKeys.ReportsPrint,
            PermissionKeys.ReportsExport
        }),
        new GroupDefinition("Users", "Tài khoản", new[]
        {
            PermissionKeys.UsersView,
            PermissionKeys.UsersCreate,
            PermissionKeys.UsersEdit,
            PermissionKeys.UsersDelete,
            PermissionKeys.UsersLock,
            PermissionKeys.UsersUnlock,
            PermissionKeys.UsersResetPassword,
            PermissionKeys.UsersChangePassword,
            PermissionKeys.UsersAssignPermission
        }),
        new GroupDefinition("Settings", "Cài đặt", new[]
        {
            PermissionKeys.SettingsView,
            PermissionKeys.SettingsEdit
        })
    };

    /// <summary>Tất cả key chi tiết, không trùng lặp.</summary>
    public static readonly IReadOnlySet<string> AllKeys = Groups
        .SelectMany(g => g.Keys)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> _tenNhom =
        Groups.ToDictionary(g => g.Nhom, g => g.TenNhom, StringComparer.Ordinal);

    private static readonly Dictionary<string, string> _tenQuyen = BuildTenQuyen();

    private static Dictionary<string, string> BuildTenQuyen()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in Groups)
        {
            foreach (var key in group.Keys)
            {
                map[key] = Describe(key);
            }
        }

        return map;
    }

    /// <summary>Key quyền "xem" dùng để quyết định có hiện module trong menu hay không.</summary>
    public static readonly IReadOnlyDictionary<string, string> ModuleViewKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Dashboard"] = PermissionKeys.DashboardView,
            ["Products"] = PermissionKeys.ProductsView,
            ["Sales"] = PermissionKeys.SalesView,
            ["Invoices"] = PermissionKeys.InvoicesView,
            ["Customers"] = PermissionKeys.CustomersView,
            ["Suppliers"] = PermissionKeys.SuppliersView,
            ["Purchases"] = PermissionKeys.PurchasesView,
            ["CashBook"] = PermissionKeys.CashBookView,
            ["Reports"] = PermissionKeys.ReportsView,
            ["Users"] = PermissionKeys.UsersView,
            ["Settings"] = PermissionKeys.SettingsView
        };

    public static string GetTenNhom(string? nhom)
    {
        if (string.IsNullOrWhiteSpace(nhom))
        {
            return "Khác";
        }

        return _tenNhom.TryGetValue(nhom, out var ten) ? ten : nhom;
    }

    public static string GetTenQuyen(string? maQuyen)
    {
        if (string.IsNullOrWhiteSpace(maQuyen))
        {
            return string.Empty;
        }

        return _tenQuyen.TryGetValue(maQuyen, out var ten) ? ten : maQuyen;
    }

    public static int GetThuTuNhom(string? nhom)
    {
        for (var i = 0; i < Groups.Count; i++)
        {
            if (string.Equals(Groups[i].Nhom, nhom, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static readonly Dictionary<string, string> _tenAction = new(StringComparer.Ordinal)
    {
        ["View"] = "Xem danh sách",
        ["Detail"] = "Xem chi tiết",
        ["Create"] = "Thêm mới",
        ["Edit"] = "Sửa",
        ["UpdateCost"] = "Cập nhật giá vốn",
        ["UpdatePrice"] = "Cập nhật giá bán",
        ["AdjustStock"] = "Điều chỉnh tồn kho",
        ["Delete"] = "Xóa",
        ["Import"] = "Import",
        ["Export"] = "Export",
        ["Print"] = "In",
        ["Cancel"] = "Hủy",
        ["Payment"] = "Thanh toán",
        ["History"] = "Xem lịch sử",
        ["Init"] = "Khởi tạo",
        ["OpeningBalance"] = "Nhập số dư đầu kỳ",
        ["DebtView"] = "Xem công nợ",
        ["DebtPay"] = "Thu/Trả nợ",
        ["Lock"] = "Khóa tài khoản",
        ["Unlock"] = "Mở khóa tài khoản",
        ["ResetPassword"] = "Đặt lại mật khẩu",
        ["ChangePassword"] = "Tự đổi mật khẩu",
        ["AssignPermission"] = "Phân quyền"
    };

    private static string Describe(string key)
    {
        var dot = key.IndexOf('.');
        if (dot <= 0 || dot >= key.Length - 1)
        {
            return key;
        }

        var module = key[..dot];
        var action = key[(dot + 1)..];

        var tenNhom = GetTenNhom(module);
        var tenAction = _tenAction.TryGetValue(action, out var label) ? label : action;

        return $"[{tenNhom}] {tenAction}";
    }
}
