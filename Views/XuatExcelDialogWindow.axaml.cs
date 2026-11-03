using System.Threading.Tasks;
using Avalonia.Controls;
using ShopHoa.Models;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class XuatExcelDialogWindow : Window
{
    private readonly TaskCompletionSource<XuatExcelYeuCau?> _tcs = new();

    public XuatExcelDialogWindow()
    {
        InitializeComponent();
    }

    public static async Task<XuatExcelYeuCau?> ShowDialogAsync(
        Window owner,
        string tieuDeDialog,
        string tenPhanHe,
        System.DateTime? tuNgayHienTai,
        System.DateTime? denNgayHienTai,
        string moTaBoLocHienTai)
    {
        var vm = new XuatExcelDialogViewModel();
        vm.KhoiTao(tieuDeDialog, tenPhanHe, tuNgayHienTai, denNgayHienTai, moTaBoLocHienTai);

        var dialog = new XuatExcelDialogWindow
        {
            DataContext = vm
        };

        vm.CloseAction = result =>
        {
            dialog._tcs.TrySetResult(result);
            dialog.Close();
        };

        dialog.Closed += (_, _) =>
        {
            dialog._tcs.TrySetResult(null);
        };

        await dialog.ShowDialog(owner);
        return await dialog._tcs.Task;
    }
}
