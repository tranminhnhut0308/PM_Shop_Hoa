using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

public sealed partial class KenhLienHe : ObservableObject
{
    public ulong Id { get; set; }

    [ObservableProperty]
    private string ten = "";

    [ObservableProperty]
    private string noiDung = "";

    [ObservableProperty]
    private bool hienThi = true;

    [ObservableProperty]
    private string viTriHienThi = ViTriCuoi;

    [ObservableProperty]
    private int thuTu;

    public const string ViTriDau = "DAU";
    public const string ViTriCuoi = "CUOI";

    public string ViTriText => ViTriHienThi == ViTriDau ? "Đầu hóa đơn" : "Cuối hóa đơn";

    partial void OnViTriHienThiChanged(string value)
        => OnPropertyChanged(nameof(ViTriText));
}
