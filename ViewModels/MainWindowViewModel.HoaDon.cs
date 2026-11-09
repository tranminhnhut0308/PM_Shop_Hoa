using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly HoaDonImportService _hoaDonImport = new();
    private bool _suppressInvoiceDateReload;
    private readonly HashSet<uint> _customerDebtPaymentsInProgress = [];

    /// <summary>
    /// Thu công nợ theo khách hàng:
    /// hỏi số tiền + phương thức rồi tự động trừ dần vào các hóa đơn còn nợ (cũ
    /// nhất trước). Mỗi hóa đơn được cập nhật và mỗi lần thu đều tạo phiếu thu
    /// trong sổ quỹ.
    /// </summary>
    [RelayCommand]
    private async Task ThuNoKhachHangAsync(KhachHangRowViewModel? row)
    {
        if (row is null || OwnerWindow is null)
            return;

        if (!_customerDebtPaymentsInProgress.Add(row.CustomerId))
        {
            StatusMessage = $"Đang xử lý thanh toán công nợ của {row.TenKhachHang}.";
            return;
        }

        try
        {
            await ThuNoKhachHangCoreAsync(row);
        }
        finally
        {
            _customerDebtPaymentsInProgress.Remove(row.CustomerId);
        }
    }

    private async Task ThuNoKhachHangCoreAsync(KhachHangRowViewModel row)
    {
        var owner = OwnerWindow;
        if (owner is null)
            return;

        if (!await KiemTraQuyenAsync(
                PermissionKeys.CustomersDebtPay,
                "Bạn không có quyền thu công nợ khách hàng."))
        {
            return;
        }

        var khachHangId = row.CustomerId;

        decimal conNo;
        decimal soDuTraTruoc = 0m;

        try
        {
            var thongKe = await _db.GetThongKeMuaHangCuaKhachHangAsync(khachHangId);
            conNo = thongKe.TongConNoHienTai;
            soDuTraTruoc = thongKe.SoDuTraTruoc;
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Không đọc được công nợ", ex.Message);
            return;
        }

        var nguoiThuOptions = await _db.GetSoQuyNguoiTaoOptionsAsync();
        var dialog = new ThuNoKhachHangWindow(
            row.MaKhachHang,
            row.TenKhachHang,
            conNo,
            soDuTraTruoc,
            nguoiThuOptions,
            Session.CurrentUser?.Id);
        var ketQua = await dialog.ShowDialog<ThuNoKhachHangYeuCau?>(owner);

        if (ketQua is null)
        {
            StatusMessage = "Đã hủy thu công nợ.";
            return;
        }

        var conNoDuKien = Math.Max(0m, conNo - ketQua.SoTien);
        var tienDuDuKien = Math.Max(0m, ketQua.SoTien - Math.Max(0m, conNo));
        var soDuSauDuKien = soDuTraTruoc + tienDuDuKien;
        var thongBaoDu = tienDuDuKien > 0
            ? $"\nTiền thanh toán dư: {tienDuDuKien:N0} đ\nSố dư trả trước mới: {soDuSauDuKien:N0} đ"
            : "";

        var xacNhan = await ShowConfirmationAsync(
            "Xác nhận thanh toán công nợ",
            $"Khách hàng: {row.MaKhachHang} - {row.TenKhachHang}\n"
            + $"Số tiền thanh toán: {ketQua.SoTien:N0} đ\n"
            + $"Công nợ còn lại dự kiến: {conNoDuKien:N0} đ"
            + thongBaoDu + "\n\n"
            + "Xác nhận ghi nhận giao dịch này?",
            "Xác nhận thanh toán",
            "Hủy");

        if (!xacNhan)
        {
            StatusMessage = "Đã hủy thanh toán công nợ.";
            return;
        }

        try
        {
            var ketQuaThu = await _db.ThuNoKhachHangAsync(
                khachHangId: khachHangId,
                soTien: ketQua.SoTien,
                phuongThucThanhToan: ketQua.PhuongThuc,
                maThamChieu: ketQua.MaThamChieu,
                ghiChu: ketQua.GhiChu,
                thoiGianThanhToan: ketQua.ThoiGian,
                nguoiThucHienId: ketQua.NguoiThucHienId);

            // Nạp lại danh sách để số nợ/tổng bán của dòng khớp database.
            await LoadCustomersAsync();

            // Mở lại vùng chi tiết ở tab Công nợ để người dùng thấy số nợ mới.
            var refreshedRow = CustomerRows.FirstOrDefault(r => r.CustomerId == khachHangId);

            if (refreshedRow is not null)
            {
                await MoChiTietKhachHangAsync(refreshedRow);

                if (refreshedRow.Detail is not null)
                    refreshedRow.Detail.SelectedTab = "congno";
            }

            await Task.WhenAll(LoadInvoicesAsync(), TaiLaiSoQuyAsync());
            _ = LamMoiTongQuanNeuDangMoAsync();

            StatusMessage =
                $"Đã thanh toán {ketQuaThu.TongDaThu:N0} đ cho {ketQuaThu.TenKhachHang}. "
                + $"Còn nợ {ketQuaThu.ConNoConLai:N0} đ.";
            await ShowMessageAsync(
                "Thanh toán thành công",
                $"Đã thanh toán {ketQuaThu.TongDaThu:N0} đ cho "
                + $"{ketQuaThu.MaKhachHang} - {ketQuaThu.TenKhachHang}.\n"
                + $"Công nợ trước: {ketQuaThu.CongNoTruoc:N0} đ.\n"
                + $"Công nợ còn lại: {ketQuaThu.ConNoConLai:N0} đ.\n"
                + $"Đã phân bổ vào {ketQuaThu.SoHoaDonDuocPhanBo:N0} hóa đơn.\n"
                + $"Mã giao dịch / phiếu thu: {ketQuaThu.MaGiaoDich} (ghi sổ quỹ một lần).");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Không thu được công nợ", ex.Message);
            SafeLog.WriteError("Loi thu no khach hang", ex);
        }
    }

    [RelayCommand]
    private async Task XemLichSuThanhToanKhachHangAsync(KhachHangRowViewModel? row)
    {
        if (row is null || OwnerWindow is null)
            return;

        try
        {
            var window = new LichSuThanhToanKhachHangWindow(
                _db,
                row.CustomerId,
                row.MaKhachHang,
                row.TenKhachHang);
            await window.ShowDialog(OwnerWindow);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Không mở được lịch sử thanh toán", ex.Message);
            SafeLog.WriteError("Loi mo lich su thanh toan cong no khach hang", ex);
        }
    }

    /// <summary>
    /// Nhập hóa đơn từ file Excel/CSV có sẵn của người dùng: đọc dòng tiêu đề để
    /// tự đối chiếu cột, chỉ mở màn hình ánh xạ khi còn cột chưa khớp, cho xem
    /// trước rồi mới ghi xuống database.
    /// </summary>
    [RelayCommand]
    private async Task NhapHoaDonTuFileAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.InvoicesImport, "Bạn không có quyền import hóa đơn."))
        {
            return;
        }
        if (OwnerWindow is null || IsImporting)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn file hóa đơn cần nhập",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Excel / CSV")
                    {
                        Patterns = ["*.xlsx", "*.xlsm", "*.csv"]
                    }
                ]
            });

        var file = files.FirstOrDefault();

        if (file is null)
        {
            StatusMessage = "Bạn chưa chọn file hóa đơn.";
            return;
        }

        try
        {
            IsImporting = true;
            ImportHasProgress = false;
            ImportProgress = 0;

            CapNhatTrangThaiNhap($"Đang đọc dòng tiêu đề của \"{file.Name}\"...");

            var thongTin = await _hoaDonImport.DocTieuDeAsync(file.Path.LocalPath);

            if (thongTin.Mapping.Sheets.Count == 0)
            {
                IsImporting = false;

                await ShowMessageAsync(
                    "File không đọc được nội dung",
                    $"File \"{thongTin.TenFile}\" không có sheet nào chứa dòng tiêu đề. "
                    + "Hãy kiểm tra lại file rồi nhập lại.");

                return;
            }

            var mapping = thongTin.Mapping;

            // File đã tự khớp đủ cột thì nhảy thẳng sang xem trước, không bắt
            // người dùng xác nhận lại những gì phần mềm đã hiểu đúng. Còn lại thì
            // mở màn hình ánh xạ để người dùng tự gán cột, kể cả sheet mà phần
            // mềm chưa nhận ra cột mã hóa đơn.
            if (!thongTin.DaKhopDayDu)
            {
                IsImporting = false;

                mapping = await HoaDonImportMappingWindow.ShowAsync(OwnerWindow, thongTin)
                          ?? throw new OperationCanceledException();

                var thieu = mapping.CoTruongChuaGan().Distinct().ToList();

                if (thieu.Count > 0)
                {
                    await ShowMessageAsync(
                        "Thiếu cột bắt buộc",
                        "Các trường sau chưa được gán cột: "
                        + string.Join(", ", thieu.Select(TenTruongHoaDon.Lay))
                        + ".\n\nNhững hóa đơn bị ảnh hưởng sẽ được báo ở bước xem trước, "
                        + "bạn vẫn có thể tiếp tục nếu muốn nhập phần còn lại.");
                }
            }

            IsImporting = true;
            CapNhatTrangThaiNhap($"Đang đọc dữ liệu từ \"{mapping.TenFile}\"...");

            var progress = new Progress<HoaDonImportProgress>(CapNhatTrangThaiNhap);

            var preview = await _hoaDonImport.DocFileAsync(
                file.Path.LocalPath,
                mapping,
                progress);

            if (preview.TongSoHoaDon == 0)
            {
                IsImporting = false;

                await ShowMessageAsync(
                    "File không có dữ liệu",
                    $"File \"{preview.TenFile}\" không tìm thấy dòng nào có mã hóa đơn "
                    + "theo cách ánh xạ đã chọn.");

                return;
            }

            // Tắt overlay để cửa sổ xem trước hiện được trên UI thread.
            IsImporting = false;

            var quyetDinh = await HoaDonImportPreviewWindow.ShowAsync(OwnerWindow, preview);

            if (quyetDinh is null)
            {
                StatusMessage = "Đã hủy nhập hóa đơn. Không có dữ liệu nào được thay đổi.";
                return;
            }

            if (preview.SoHoaDonHopLe == 0)
            {
                await ShowMessageAsync(
                    "Không có hóa đơn nào nhập được",
                    "Mọi hóa đơn trong file đều bị loại. "
                    + (preview.DanhSachLoi.FirstOrDefault()?.LyDo ?? "Hãy xem lại danh sách lỗi."));

                return;
            }

            var tuyChonImport = await HoiTuyChonImportHoaDonAsync();
            if (!tuyChonImport.HasValue)
            {
                StatusMessage = "Đã hủy import hóa đơn. Không có dữ liệu nào được thay đổi.";
                return;
            }

            IsImporting = true;
            ImportHasProgress = false;
            ImportProgress = 0;

            CapNhatTrangThaiNhap("Đang ghi hóa đơn vào database...");

            var ketQua = await _hoaDonImport.ImportAsync(
                preview,
                quyetDinh.XuLyTrung,
                quyetDinh.TaoKhachHangMoi,
                tuyChonImport.Value.UpdateCustomerDebt,
                tuyChonImport.Value.UpdateSalesTotal,
                quyetDinh.TaoSanPhamMoi,
                progress);

            // Danh sách hóa đơn trên màn hình đã cũ, nạp lại để thấy dữ liệu mới.
            IsImporting = false;
            _invoicesLoaded = false;

            InvoiceSearch = "";
            _suppressInvoiceDateReload = true;
            try
            {
                InvoiceFilterDate = null;
            }
            finally
            {
                _suppressInvoiceDateReload = false;
            }

            await LoadInvoicesAsync();

            await HoaDonImportResultWindow.ShowAsync(OwnerWindow, ketQua);

            StatusMessage = TaoThongBaoKetQua(ketQua);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy nhập hóa đơn. Không có dữ liệu nào được thay đổi.";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ Không thể nhập hóa đơn từ file: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
            ImportHasProgress = false;
            ImportProgress = 0;
        }
    }

    private async Task<(bool UpdateCustomerDebt, bool UpdateSalesTotal)?> HoiTuyChonImportHoaDonAsync()
    {
        if (OwnerWindow is null)
            return null;

        var dialog = new Window
        {
            Title = "Xác nhận import hóa đơn",
            Width = 700,
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            MinHeight = 500,
            CanResize = false,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brushes.White
        };

        var content = new Avalonia.Controls.StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 14
        };

        content.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = "Xác nhận cập nhật dữ liệu khi import",
            FontSize = 20,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brush.Parse("#173F38"),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        content.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = "Hai tùy chọn dưới đây độc lập. Chọn riêng cách xử lý công nợ và doanh số cho các hóa đơn trong file.",
            FontSize = 14,
            Foreground = Avalonia.Media.Brush.Parse("#263B35"),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        content.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = "1. Cập nhật công nợ khách hàng",
            FontSize = 15,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = Avalonia.Media.Brush.Parse("#173F38")
        });

        var updateDebt = new Avalonia.Controls.RadioButton
        {
            Content = "Có cập nhật công nợ khách hàng",
            GroupName = "CustomerDebt",
            IsChecked = true
        };
        var skipDebt = new Avalonia.Controls.RadioButton
        {
            Content = "Không cập nhật công nợ khách hàng",
            GroupName = "CustomerDebt"
        };
        content.Children.Add(updateDebt);
        content.Children.Add(skipDebt);
        content.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = "Khi không cập nhật, hóa đơn vẫn được lưu nhưng không tạo hoặc thay đổi giao dịch công nợ hiện có.",
            FontSize = 12,
            Foreground = Avalonia.Media.Brush.Parse("#52645F"),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        content.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = "2. Cập nhật Tổng bán",
            FontSize = 15,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = Avalonia.Media.Brush.Parse("#173F38"),
            Margin = new Avalonia.Thickness(0, 6, 0, 0)
        });

        var updateSales = new Avalonia.Controls.RadioButton
        {
            Content = "Có cập nhật Tổng bán",
            GroupName = "SalesTotal",
            IsChecked = true
        };
        var skipSales = new Avalonia.Controls.RadioButton
        {
            Content = "Không cập nhật Tổng bán",
            GroupName = "SalesTotal"
        };
        content.Children.Add(updateSales);
        content.Children.Add(skipSales);
        content.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = "Bạn có muốn tính doanh số từ các hóa đơn được import vào Tổng bán và các báo cáo doanh thu không?\n\n"
                   + "Nếu hóa đơn lịch sử đã được tính vào doanh số trước đó, hãy chọn Không cập nhật Tổng bán để tránh cộng trùng.",
            FontSize = 12,
            Foreground = Avalonia.Media.Brush.Parse("#52645F"),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var buttons = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var continueButton = new Avalonia.Controls.Button
        {
            Content = "Tiếp tục import",
            Padding = new Avalonia.Thickness(16, 10),
            Background = Avalonia.Media.Brush.Parse("#087F6E"),
            Foreground = Avalonia.Media.Brushes.White,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };
        continueButton.Click += (_, _) => dialog.Close((
            updateDebt.IsChecked == true,
            updateSales.IsChecked == true));

        var cancelButton = new Avalonia.Controls.Button
        {
            Content = "Hủy",
            Padding = new Avalonia.Thickness(16, 10),
            Background = Avalonia.Media.Brush.Parse("#FFFFFF"),
            Foreground = Avalonia.Media.Brush.Parse("#263B35"),
            BorderBrush = Avalonia.Media.Brush.Parse("#C9DCD6"),
            BorderThickness = new Avalonia.Thickness(1)
        };
        cancelButton.Click += (_, _) => dialog.Close(null);

        buttons.Children.Add(continueButton);
        buttons.Children.Add(cancelButton);
        content.Children.Add(buttons);
        dialog.Content = content;

        return await dialog.ShowDialog<(bool UpdateCustomerDebt, bool UpdateSalesTotal)?>(OwnerWindow);
    }

    /// <summary>
    /// Cập nhật nội dung overlay đang chạy. Khi biết tổng số đơn vị công việc
    /// thì hiện thanh tiến độ xác định, không thì dùng thanh chạy vô hạn.
    /// </summary>
    private void CapNhatTrangThaiNhap(HoaDonImportProgress tienDo)
    {
        if (tienDo.TongSo > 0)
        {
            ImportHasProgress = true;

            var daXuLy = Math.Clamp(tienDo.DaXuLy, 0, tienDo.TongSo);
            ImportProgress = daXuLy * 100d / tienDo.TongSo;
        }

        CapNhatTrangThaiNhap(
            tienDo.TongSo > 0
                ? $"{tienDo.GiaiDoan} {tienDo.DaXuLy:N0}/{tienDo.TongSo:N0}"
                : $"{tienDo.GiaiDoan} {tienDo.DaXuLy:N0}");
    }

    private void CapNhatTrangThaiNhap(string trangThai)
    {
        ImportStatus = trangThai;
        StatusMessage = trangThai;
    }

    private static string TaoThongBaoKetQua(HoaDonImportResult ketQua)
    {
        if (ketQua.DaRollback)
            return "❌ Lỗi nghiêm trọng, đã hủy toàn bộ thay đổi. Database giữ nguyên như cũ.";

        if (ketQua.TongSoGhiThanhCong == 0)
            return $"⚠️ Không có hóa đơn nào được ghi. {ketQua.ThongBaoTatCa}";

        return $"✅ Đã nhập {ketQua.SoThemMoi:N0}, cập nhật {ketQua.SoCapNhat:N0} hóa đơn "
            + $"từ file \"{ketQua.TenFile}\". {ketQua.ThongBaoTatCa}";
    }

    /// <summary>
    /// Xuất danh sách hóa đơn theo Issue #63:
    /// - Hiển thị cửa sổ lựa chọn (Tổng quan / Chi tiết / Đầy đủ 3 sheet)
    /// - Tùy chọn khoảng thời gian và phạm vi bộ lọc
    /// - Định dạng chuyên nghiệp qua ReportExportService và ClosedXML
    /// </summary>
    [RelayCommand]
    private async Task XuatFileHoaDonAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.InvoicesExport, "Bạn không có quyền export hóa đơn."))
        {
            return;
        }

        var cuaSo = OwnerWindow ?? ReportExportService.LayCuaSoChinh();
        if (cuaSo is null)
            return;

        // Tổng hợp mô tả bộ lọc hiện tại trên giao diện
        var moTaLoc = new List<string>();
        if (!string.IsNullOrWhiteSpace(InvoiceSearch))
            moTaLoc.Add($"Từ khóa: \"{InvoiceSearch.Trim()}\"");
        if (SelectedInvoiceCustomer?.Id != null)
            moTaLoc.Add($"Khách hàng: {SelectedInvoiceCustomer.DisplayText}");
        if (!string.IsNullOrWhiteSpace(SelectedInvoicePaymentStatus) && SelectedInvoicePaymentStatus != "Tất cả")
            moTaLoc.Add($"Thanh toán: {SelectedInvoicePaymentStatus}");
        if (!string.IsNullOrWhiteSpace(SelectedInvoiceStatus) && SelectedInvoiceStatus != "Tất cả")
            moTaLoc.Add($"Trạng thái: {SelectedInvoiceStatus}");

        var moTaBoLocHienTai = moTaLoc.Count > 0 ? string.Join(", ", moTaLoc) : "Không áp dụng lọc";

        // Mở cửa sổ lựa chọn xuất Excel
        var yeuCau = await XuatExcelDialogWindow.ShowDialogAsync(
            cuaSo,
            "Tùy chọn xuất Excel - Hóa đơn",
            "Hóa đơn bán hàng",
            InvoiceFilterStartDate?.Date,
            InvoiceFilterEndDate?.Date,
            moTaBoLocHienTai);

        if (yeuCau is null)
        {
            StatusMessage = "Bạn đã hủy xuất file hóa đơn.";
            return;
        }

        try
        {
            StatusMessage = "Đang tải dữ liệu hóa đơn để xuất Excel...";

            var (tongQuans, chiTiets, tongHops) = await _db.GetHoaDonXuatExcelDataAsync(
                yeuCau,
                searchKeyword: InvoiceSearch,
                khachHangId: SelectedInvoiceCustomer?.Id,
                trangThaiThanhToan: SelectedInvoicePaymentStatus,
                trangThaiHoaDon: SelectedInvoiceStatus,
                nhanVienId: SelectedInvoiceEmployee?.Id,
                khachLe: SelectedInvoiceCustomer?.IsWalkIn == true);

            var tongSoBanGhi = yeuCau.LoaiBaoCao switch
            {
                LoaiBaoCaoXuat.TongQuan => tongQuans.Count,
                LoaiBaoCaoXuat.ChiTiet => chiTiets.Count,
                _ => tongQuans.Count
            };

            if (tongSoBanGhi == 0)
            {
                await ShowMessageAsync(
                    "Xuất file",
                    "Không tìm thấy dữ liệu hóa đơn nào khớp với khoảng thời gian và phạm vi đã chọn.");
                StatusMessage = "Không có dữ liệu hóa đơn để xuất.";
                return;
            }

            var tenLoai = yeuCau.LoaiBaoCao switch
            {
                LoaiBaoCaoXuat.TongQuan => "TongQuan",
                LoaiBaoCaoXuat.ChiTiet => "ChiTiet",
                _ => "DayDu"
            };

            var tenTieuDe = yeuCau.LoaiBaoCao switch
            {
                LoaiBaoCaoXuat.TongQuan => "Báo cáo tổng quan hóa đơn",
                LoaiBaoCaoXuat.ChiTiet => "Báo cáo chi tiết mặt hàng bán",
                _ => "Báo cáo đầy đủ hóa đơn bán hàng"
            };

            var tenFileGoiY = $"HoaDon_{tenLoai}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var ghiChuLoc = $"{yeuCau.MoTaKhoangThoiGian} | {yeuCau.MoTaBoLoc}";

            await ReportExportService.ExportReportAsync(
                cuaSo,
                tenTieuDe,
                tenFileGoiY,
                () => HoaDonExcel.TaoWorkbookMoi(
                    tongQuans,
                    chiTiets,
                    tongHops,
                    yeuCau.LoaiBaoCao,
                    DateTime.Now,
                    ghiChuLoc,
                    "VỰA HOA NHÀ HUY",
                    Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Hệ thống"),
                recordCount: tongSoBanGhi,
                statusCallback: msg => StatusMessage = msg,
                reportPeriod: yeuCau.MoTaKhoangThoiGian);
        }
        catch (Exception ex)
        {
            StatusMessage = "Xuất file thất bại: " + ex.Message;
            await ShowMessageAsync(
                "Xuất file thất bại",
                $"Không thể xuất danh sách hóa đơn.\n\nChi tiết lỗi:\n{ex.Message}");
        }
    }
}
