using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một bản ghi lịch sử chỉnh sửa hóa đơn bán hàng.
/// </summary>
public sealed class LichSuSuaHoaDon
{
    public uint Id { get; set; }
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = string.Empty;
    public uint? NguoiSuaId { get; set; }
    public string TenNguoiSua { get; set; } = string.Empty;
    public DateTime ThoiGianSua { get; set; }
    public string LyDoSua { get; set; } = string.Empty;
    public decimal TongTienTruoc { get; set; }
    public decimal TongTienSau { get; set; }
    public decimal ChenhLechTien => TongTienSau - TongTienTruoc;
    public string GhiChuThayDoi { get; set; } = string.Empty;
    public string? DuLieuTruocJson { get; set; }
    public string? DuLieuSauJson { get; set; }

    public string ThoiGianSuaText => ThoiGianSua.ToString("dd/MM/yyyy HH:mm:ss");
    public string TongTienTruocText => $"{TongTienTruoc:N0} đ";
    public string TongTienSauText => $"{TongTienSau:N0} đ";
    public string ChenhLechText
    {
        get
        {
            if (ChenhLechTien > 0)
                return $"+{ChenhLechTien:N0} đ";
            if (ChenhLechTien < 0)
                return $"{ChenhLechTien:N0} đ";
            return "0 đ";
        }
    }
}

/// <summary>
/// Dữ liệu snapshot cho một item trong hóa đơn trước/sau khi sửa.
/// </summary>
public sealed class HoaDonItemSnapshot
{
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = string.Empty;
    public string TenSanPham { get; set; } = string.Empty;
    public string DonViTinh { get; set; } = string.Empty;
    public decimal SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal GiamGia { get; set; }
    public decimal ThanhTien { get; set; }
    public decimal GiaVon { get; set; }
}

/// <summary>
/// Dữ liệu yêu cầu chỉnh sửa hóa đơn gửi xuống DatabaseService.
/// </summary>
public sealed class HoaDonEditRequest
{
    public uint HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = string.Empty;
    public string LyDoSua { get; set; } = string.Empty;
    public decimal GiamGia { get; set; }
    public decimal ThuKhac { get; set; }
    public string? GhiChu { get; set; }
    public List<HoaDonItemEditModel> Items { get; set; } = [];
}

/// <summary>
/// Dòng mặt hàng trong form chỉnh sửa hóa đơn, hỗ trợ thông báo thay đổi (INotifyPropertyChanged).
/// </summary>
public sealed class HoaDonItemEditModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = string.Empty;
    public string TenSanPham { get; set; } = string.Empty;
    public string DonViTinh { get; set; } = "Bó";
    public decimal GiaVon { get; set; }

    private decimal _soLuong = 1;
    public decimal SoLuong
    {
        get => _soLuong;
        set
        {
            if (_soLuong != value)
            {
                _soLuong = value;
                OnPropertyChanged(nameof(SoLuong));
                OnPropertyChanged(nameof(ThanhTien));
            }
        }
    }

    private decimal _donGia;
    public decimal DonGia
    {
        get => _donGia;
        set
        {
            if (_donGia != value)
            {
                _donGia = value;
                OnPropertyChanged(nameof(DonGia));
                OnPropertyChanged(nameof(ThanhTien));
            }
        }
    }

    private decimal _giamGia;
    public decimal GiamGia
    {
        get => _giamGia;
        set
        {
            if (_giamGia != value)
            {
                _giamGia = value;
                OnPropertyChanged(nameof(GiamGia));
                OnPropertyChanged(nameof(ThanhTien));
            }
        }
    }

    public decimal ThanhTien => Math.Max(0, (SoLuong * DonGia) - GiamGia);

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
