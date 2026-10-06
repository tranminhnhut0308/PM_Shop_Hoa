using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;
using ClosedXML.Excel;

namespace ShopHoa.ViewModels;

public enum BaoCaoBanHangKieuHienThi { BieuDo, BaoCao }
public enum BaoCaoBanHangMocQuanTam { ThoiGian, SanPham, KhachHang, PhuongThucThanhToan }

public partial class BaoCaoBanHangViewModel : ObservableObject
{
    private readonly BaoCaoBanHangService _service;

    public BaoCaoBanHangViewModel(DatabaseService db)
    {
        _service = new BaoCaoBanHangService(db.TaoKetNoiAsync);
        KieuHienThi = BaoCaoBanHangKieuHienThi.BieuDo;
        MocQuanTam = BaoCaoBanHangMocQuanTam.ThoiGian;
        TuNgay = DateTime.Today.AddDays(-6);
        DenNgay = DateTime.Today;
        KhoangThoiGian = "7 ngày";
        TuyChinh = false;
        _ = TaiDuLieuAsync();
    }

    [ObservableProperty] private BaoCaoBanHangKieuHienThi kieuHienThi;
    [ObservableProperty] private BaoCaoBanHangMocQuanTam mocQuanTam;
    [ObservableProperty] private bool dangTai;
    [ObservableProperty] private string thongBaoLoi = string.Empty;
    [ObservableProperty] private DateTime tuNgay;
    [ObservableProperty] private DateTime denNgay;
    [ObservableProperty] private bool tuyChinh;
    [ObservableProperty] private string khoangThoiGian = "Hôm nay";
    [ObservableProperty] private BaoCaoBanHangDuLieu duLieu = new();
    [ObservableProperty] private DateTime? ngayChiTiet;

    public ObservableCollection<BaoCaoBanHangDiemThoiGian> BieuDoItems => new(DuLieu.BieuDo);
    public ObservableCollection<BaoCaoBanHangBangRow> BangItems => new(DuLieu.BangDuLieu);
    public ObservableCollection<BaoCaoBanHangTopSanPham> TopSanPhamItems => new(DuLieu.TopSanPham);
    public ObservableCollection<BaoCaoBanHangTopKhachHang> TopKhachHangItems => new(DuLieu.TopKhachHang);
    public ObservableCollection<BaoCaoBanHangPhuongThucRow> PhuongThucItems => new(DuLieu.PhuongThucThanhToan);
    public ObservableCollection<BaoCaoBanHangChiTietHoaDon> ChiTietHoaDonList => new(DuLieu.ChiTietHoaDon);

    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        await TaiDuLieuAsync();
    }

    [RelayCommand]
    private async Task TaiDuLieuAsync()
    {
        try
        {
            DangTai = true;
            ThongBaoLoi = string.Empty;
            if (DenNgay < TuNgay) DenNgay = TuNgay;
            DuLieu = await _service.TaiDuLieuAsync(TuNgay, DenNgay, 1, TuyChinh, default);
            OnPropertyChanged(nameof(BieuDoItems));
            OnPropertyChanged(nameof(BangItems));
            OnPropertyChanged(nameof(TopSanPhamItems));
            OnPropertyChanged(nameof(TopKhachHangItems));
            OnPropertyChanged(nameof(PhuongThucItems));
            OnPropertyChanged(nameof(ChiTietHoaDonList));
            OnPropertyChanged(nameof(TongHop));
        }
        catch (Exception ex) { ThongBaoLoi = ex.Message; }
        finally { DangTai = false; }
    }

    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        try
        {
            var cuaSo = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt ? lt.MainWindow : null;
            if (cuaSo == null) return;
            using (var wb = BaoCaoBanHangExcel.TaoWorkbook(DuLieu))
            {
                var file = await cuaSo.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Xuất Excel báo cáo bán hàng",
                    FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } } },
                    SuggestedFileName = DuLieu.TongHop.TenFileXuat()
                });
                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    wb.SaveAs(stream);
                }
            }
        }
        catch (Exception ex) { ThongBaoLoi = ex.Message; }
    }

    [RelayCommand]
    private async Task XemChiTietNgayAsync(DateTime ngay)
    {
        NgayChiTiet = ngay;
        DuLieu.ChiTietHoaDon = await _service.TaiChiTietHoaDonAsync(ngay, default);
        OnPropertyChanged(nameof(ChiTietHoaDonList));
    }

    public BaoCaoBanHangTongHop TongHop => DuLieu.TongHop;
}
