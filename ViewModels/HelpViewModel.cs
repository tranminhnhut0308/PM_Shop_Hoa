using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// ViewModel cho cửa sổ "SHOPHOA – HỆ THỐNG VÀ TRỢ GIÚP".
/// Nội dung tĩnh lấy từ <see cref="HelpContent"/>; thông tin liên hệ lấy từ
/// <see cref="StoreInfoService"/> để không hard-code tên tiệm / số điện thoại.
/// </summary>
public sealed partial class HelpViewModel : ObservableObject
{
    private readonly StoreInfoService _storeInfoService =
        new(() => new DatabaseService().TaoKetNoiAsync());

    public string MoTaNgan => HelpContent.MoTaNgan;

    public IReadOnlyList<HelpFeature> TinhNang => HelpContent.TinhNang;
    public IReadOnlyList<HelpShortcutGroup> NhomPhimTat => HelpContent.NhomPhimTat;
    public IReadOnlyList<string> DiemMoi => HelpContent.DiemMoi;
    public IReadOnlyList<HelpUpdateNote> LuuYCapNhat => HelpContent.LuuYCapNhat;

    public ObservableCollection<HelpGuide> DanhSachHuongDan { get; } = [];

    public string PhienBanHienThi { get; } =
        $"Phiên bản {UpdateService.Instance.CurrentVersionText}";

    public string HeDieuHanh { get; } = RuntimeInformation.OSDescription;

    public string ThongTinKyThuat =>
        $"{PhienBanHienThi} · {HeDieuHanh} · .NET {Environment.Version}";

    public string TrangCapNhatUrl => UpdateService.Instance.UpdateUrl;

    public Action? DongAction { get; set; }
    public Action? MoCapNhatAction { get; set; }
    public Func<string, Task>? MoLienKetAction { get; set; }
    public Func<string, Task>? SaoChepAction { get; set; }

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private string guideSearchText = "";

    [ObservableProperty]
    private HelpGuide? selectedGuide;

    [ObservableProperty]
    private bool dangTaiThongTin;

    [ObservableProperty]
    private string thongBaoSaoChep = "";

    [ObservableProperty]
    private string loiThongTin = "";

    [ObservableProperty]
    private string tenTiem = "";

    [ObservableProperty]
    private string diaChi = "";

    [ObservableProperty]
    private string soDienThoai = "";

    [ObservableProperty]
    private string email = "";

    [ObservableProperty]
    private string website = "";

    [ObservableProperty]
    private string maSoThue = "";

    public string TenTiemHienThi =>
        string.IsNullOrWhiteSpace(TenTiem) ? "Chưa cấu hình tên tiệm" : TenTiem;

    public bool CoDiaChi => !string.IsNullOrWhiteSpace(DiaChi);
    public bool CoDienThoai => !string.IsNullOrWhiteSpace(SoDienThoai);
    public bool CoEmail => !string.IsNullOrWhiteSpace(Email);
    public bool CoWebsite => !string.IsNullOrWhiteSpace(Website);
    public bool CoMaSoThue => !string.IsNullOrWhiteSpace(MaSoThue);

    public bool CoThongTinLienHe =>
        CoDiaChi || CoDienThoai || CoEmail || CoWebsite || CoMaSoThue;

    public bool KhongCoThongTinLienHe => !CoThongTinLienHe;
    public bool KhongCoHuongDan => DanhSachHuongDan.Count == 0;

    public HelpViewModel()
    {
        foreach (var guide in HelpContent.HuongDan)
            DanhSachHuongDan.Add(guide);

        SelectedGuide = DanhSachHuongDan.FirstOrDefault();
    }

    public async Task KhoiTaoAsync()
    {
        DangTaiThongTin = true;
        LoiThongTin = "";

        try
        {
            var info = await _storeInfoService.LayThongTinAsync();

            TenTiem = info.TenTiem;
            DiaChi = info.DiaChi;
            SoDienThoai = info.SoDienThoai;
            Email = info.Email;
            Website = info.Website;
            MaSoThue = info.MaSoThue;
        }
        catch (Exception ex)
        {
            LoiThongTin = "Không tải được thông tin liên hệ: " + ex.Message;
            SafeLog.WriteError("Lỗi tải thông tin hỗ trợ", ex);
        }
        finally
        {
            DangTaiThongTin = false;
            ThongBaoCoThongTinLienHe();
        }
    }

    partial void OnGuideSearchTextChanged(string value) => ApDungLocHuongDan();

    partial void OnTenTiemChanged(string value)
    {
        OnPropertyChanged(nameof(TenTiemHienThi));
        ThongBaoCoThongTinLienHe();
    }

    partial void OnDiaChiChanged(string value) => ThongBaoCoThongTinLienHe();
    partial void OnSoDienThoaiChanged(string value) => ThongBaoCoThongTinLienHe();
    partial void OnEmailChanged(string value) => ThongBaoCoThongTinLienHe();
    partial void OnWebsiteChanged(string value) => ThongBaoCoThongTinLienHe();
    partial void OnMaSoThueChanged(string value) => ThongBaoCoThongTinLienHe();

    private void ThongBaoCoThongTinLienHe()
    {
        OnPropertyChanged(nameof(CoDiaChi));
        OnPropertyChanged(nameof(CoDienThoai));
        OnPropertyChanged(nameof(CoEmail));
        OnPropertyChanged(nameof(CoWebsite));
        OnPropertyChanged(nameof(CoMaSoThue));
        OnPropertyChanged(nameof(CoThongTinLienHe));
        OnPropertyChanged(nameof(KhongCoThongTinLienHe));
    }

    private void ApDungLocHuongDan()
    {
        var dangChon = SelectedGuide?.Khoa;

        DanhSachHuongDan.Clear();

        foreach (var guide in HelpContent.HuongDan)
        {
            if (HelpContent.KhopTuKhoa(guide, GuideSearchText))
                DanhSachHuongDan.Add(guide);
        }

        SelectedGuide = DanhSachHuongDan.FirstOrDefault(g => g.Khoa == dangChon)
            ?? DanhSachHuongDan.FirstOrDefault();

        OnPropertyChanged(nameof(KhongCoHuongDan));
    }

    [RelayCommand]
    private void Dong() => DongAction?.Invoke();

    [RelayCommand]
    private void MoCapNhat() => MoCapNhatAction?.Invoke();

    [RelayCommand]
    private void MoTabGioiThieu() => SelectedTabIndex = 0;

    [RelayCommand]
    private void MoTabPhimTat() => SelectedTabIndex = 1;

    [RelayCommand]
    private void MoTabHuongDan() => SelectedTabIndex = 2;

    [RelayCommand]
    private void MoTabCapNhat() => SelectedTabIndex = 3;

    [RelayCommand]
    private void MoTabHoTro() => SelectedTabIndex = 4;

    [RelayCommand]
    private async Task SaoChepPhienBanAsync()
        => await SaoChepAsync(PhienBanHienThi, "Đã sao chép thông tin phiên bản.");

    [RelayCommand]
    private async Task SaoChepThongTinKyThuatAsync()
        => await SaoChepAsync(ThongTinKyThuat, "Đã sao chép thông tin kỹ thuật.");

    [RelayCommand]
    private async Task SaoChepDiaChiAsync()
        => await SaoChepAsync(DiaChi, "Đã sao chép địa chỉ.");

    [RelayCommand]
    private async Task SaoChepDienThoaiAsync()
        => await SaoChepAsync(SoDienThoai, "Đã sao chép số điện thoại.");

    [RelayCommand]
    private async Task SaoChepEmailAsync()
        => await SaoChepAsync(Email, "Đã sao chép email.");

    [RelayCommand]
    private async Task SaoChepWebsiteAsync()
        => await SaoChepAsync(Website, "Đã sao chép website.");

    [RelayCommand]
    private async Task SaoChepMaSoThueAsync()
        => await SaoChepAsync(MaSoThue, "Đã sao chép mã số thuế.");

    [RelayCommand]
    private async Task MoWebsiteAsync()
    {
        if (!CoWebsite || MoLienKetAction is null)
            return;

        var url = Website.Trim();

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        await MoLienKetAction(url);
    }

    [RelayCommand]
    private async Task MoTrangCapNhatAsync()
    {
        if (MoLienKetAction is null || string.IsNullOrWhiteSpace(TrangCapNhatUrl))
            return;

        await MoLienKetAction(TrangCapNhatUrl);
    }

    private async Task SaoChepAsync(string? noiDung, string thongBao)
    {
        if (string.IsNullOrWhiteSpace(noiDung) || SaoChepAction is null)
            return;

        await SaoChepAction(noiDung.Trim());
        ThongBaoSaoChep = thongBao;
    }
}
