using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

public partial class XuatExcelDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string tieuDe = "Tùy chọn xuất file Excel";

    [ObservableProperty]
    private string phanHe = "Hóa đơn bán hàng";

    // 1. LOẠI BÁO CÁO
    [ObservableProperty]
    private bool isBaoCaoTongQuan;

    [ObservableProperty]
    private bool isBaoCaoChiTiet;

    [ObservableProperty]
    private bool isBaoCaoDayDu = true;

    // 2. KHOẢNG THỜI GIAN
    [ObservableProperty]
    private DateTimeOffset? tuNgay;

    [ObservableProperty]
    private DateTimeOffset? denNgay;

    // 3. PHẠM VI BỘ LỌC
    [ObservableProperty]
    private bool isTheoBoLocHienTai = true;

    [ObservableProperty]
    private bool isToanBoKhoangThoiGian;

    [ObservableProperty]
    private string thongTinBoLocHienTai = "Toàn bộ dữ liệu";

    [ObservableProperty]
    private string thongTinTomTat = "";

    public Action<XuatExcelYeuCau?>? CloseAction { get; set; }

    public XuatExcelDialogViewModel()
    {
        // Mặc định tháng này
        ChonThangNay();
        CapNhatTomTat();
    }

    public void KhoiTao(
        string tieuDeDialog,
        string tenPhanHe,
        DateTime? tuNgayHienTai,
        DateTime? denNgayHienTai,
        string moTaBoLocHienTai)
    {
        TieuDe = tieuDeDialog;
        PhanHe = tenPhanHe;
        ThongTinBoLocHienTai = string.IsNullOrWhiteSpace(moTaBoLocHienTai) ? "Không áp dụng lọc" : moTaBoLocHienTai;

        if (tuNgayHienTai.HasValue)
            TuNgay = new DateTimeOffset(tuNgayHienTai.Value.Date);
        else
            TuNgay = new DateTimeOffset(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));

        if (denNgayHienTai.HasValue)
            DenNgay = new DateTimeOffset(denNgayHienTai.Value.Date);
        else
            DenNgay = new DateTimeOffset(DateTime.Today);

        CapNhatTomTat();
    }

    partial void OnIsBaoCaoTongQuanChanged(bool value) { if (value) CapNhatTomTat(); }
    partial void OnIsBaoCaoChiTietChanged(bool value) { if (value) CapNhatTomTat(); }
    partial void OnIsBaoCaoDayDuChanged(bool value) { if (value) CapNhatTomTat(); }
    partial void OnTuNgayChanged(DateTimeOffset? value) => CapNhatTomTat();
    partial void OnDenNgayChanged(DateTimeOffset? value) => CapNhatTomTat();
    partial void OnIsTheoBoLocHienTaiChanged(bool value) { if (value) CapNhatTomTat(); }
    partial void OnIsToanBoKhoangThoiGianChanged(bool value) { if (value) CapNhatTomTat(); }

    [RelayCommand]
    public void ChonHomNay()
    {
        TuNgay = new DateTimeOffset(DateTime.Today);
        DenNgay = new DateTimeOffset(DateTime.Today);
    }

    [RelayCommand]
    public void ChonHomQua()
    {
        var homQua = DateTime.Today.AddDays(-1);
        TuNgay = new DateTimeOffset(homQua);
        DenNgay = new DateTimeOffset(homQua);
    }

    [RelayCommand]
    public void Chon7NgayQua()
    {
        TuNgay = new DateTimeOffset(DateTime.Today.AddDays(-6));
        DenNgay = new DateTimeOffset(DateTime.Today);
    }

    [RelayCommand]
    public void ChonThangNay()
    {
        var dauThang = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        TuNgay = new DateTimeOffset(dauThang);
        DenNgay = new DateTimeOffset(DateTime.Today);
    }

    [RelayCommand]
    public void ChonThangTruoc()
    {
        var dauThangNay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var cuoiThangTruoc = dauThangNay.AddDays(-1);
        var dauThangTruoc = new DateTime(cuoiThangTruoc.Year, cuoiThangTruoc.Month, 1);

        TuNgay = new DateTimeOffset(dauThangTruoc);
        DenNgay = new DateTimeOffset(cuoiThangTruoc);
    }

    [RelayCommand]
    public void ChonNamNay()
    {
        var dauNam = new DateTime(DateTime.Today.Year, 1, 1);
        TuNgay = new DateTimeOffset(dauNam);
        DenNgay = new DateTimeOffset(DateTime.Today);
    }

    private void CapNhatTomTat()
    {
        var loaiStr = IsBaoCaoTongQuan
            ? "Báo cáo tổng quan (1 Sheet)"
            : (IsBaoCaoChiTiet ? "Báo cáo chi tiết mặt hàng (1 Sheet)" : "Xuất đầy đủ (3 Sheet: Tổng quan, Chi tiết, Tổng hợp)");

        var tu = TuNgay?.DateTime ?? DateTime.Today;
        var den = DenNgay?.DateTime ?? DateTime.Today;
        var tgStr = tu.Date == den.Date
            ? $"Ngày {tu:dd/MM/yyyy}"
            : $"Từ {tu:dd/MM/yyyy} đến {den:dd/MM/yyyy}";

        var phamViStr = IsTheoBoLocHienTai
            ? $"Theo bộ lọc đang áp dụng ({ThongTinBoLocHienTai})"
            : "Toàn bộ dữ liệu trong khoảng thời gian";

        ThongTinTomTat = $"• Loại báo cáo: {loaiStr}\n• Thời gian: {tgStr}\n• Phạm vi dữ liệu: {phamViStr}";
    }

    [RelayCommand]
    public void XacNhan()
    {
        var tu = TuNgay?.DateTime.Date ?? DateTime.Today;
        var den = DenNgay?.DateTime.Date ?? DateTime.Today;

        if (tu > den)
        {
            var temp = tu;
            tu = den;
            den = temp;
        }

        var loai = IsBaoCaoTongQuan
            ? LoaiBaoCaoXuat.TongQuan
            : (IsBaoCaoChiTiet ? LoaiBaoCaoXuat.ChiTiet : LoaiBaoCaoXuat.DayDu);

        var phamVi = IsToanBoKhoangThoiGian
            ? PhamViLocXuat.ToanBoKhoangThoiGian
            : PhamViLocXuat.TheoBoLocHienTai;

        var tgStr = tu.Date == den.Date
            ? $"Ngày {tu:dd/MM/yyyy}"
            : $"Từ {tu:dd/MM/yyyy} đến {den:dd/MM/yyyy}";

        var yeuCau = new XuatExcelYeuCau
        {
            LoaiBaoCao = loai,
            TuNgay = tu,
            DenNgay = den,
            PhamViLoc = phamVi,
            MoTaKhoangThoiGian = tgStr,
            MoTaBoLoc = phamVi == PhamViLocXuat.TheoBoLocHienTai ? ThongTinBoLocHienTai : "Toàn bộ khoảng thời gian"
        };

        CloseAction?.Invoke(yeuCau);
    }

    [RelayCommand]
    public void Huy()
    {
        CloseAction?.Invoke(null);
    }
}
