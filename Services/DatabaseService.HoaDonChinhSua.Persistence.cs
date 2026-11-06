using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    private static async Task ApplyHoaDonEditStockAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditState state)
    {
        foreach (var productId in state.OldQuantities.Keys.Union(state.NewQuantities.Keys).OrderBy(id => id))
        {
            await ApplyHoaDonEditStockChangeAsync(
                connection,
                transaction,
                state,
                productId,
                state.NewQuantities.GetValueOrDefault(productId) -
                state.OldQuantities.GetValueOrDefault(productId));
        }
    }

    private static async Task ApplyHoaDonEditStockChangeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditState state,
        uint productId,
        decimal delta)
    {
        if (delta == 0)
            return;

        var stock = state.Stock[productId];
        var stockAfter = stock.SoLuongTon - delta;
        if (stockAfter < 0)
        {
            var name = state.Products[productId].TenSanPham;
            throw new InvalidOperationException(
                $"Sản phẩm \"{name}\" không đủ tồn kho để cập nhật hóa đơn " +
                $"(tồn hiện tại {stock.SoLuongTon:N3}, cần thêm {Math.Max(0, delta):N3}).");
        }

        await connection.ExecuteAsync(
            """
            UPDATE ton_kho
            SET so_luong_ton = @TonSau, ngay_cap_nhat = @Now
            WHERE chi_nhanh_id = @ChiNhanhId AND san_pham_id = @SanPhamId;
            """,
            new
            {
                TonSau = stockAfter,
                state.Now,
                ChiNhanhId = state.BranchId,
                SanPhamId = productId
            },
            transaction);
        await WriteHoaDonEditStockHistoryAsync(
            connection, transaction, state, productId, delta, stock, stockAfter);
    }

    private static Task WriteHoaDonEditStockHistoryAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditState state,
        uint productId,
        decimal delta,
        TonKhoSuaHoaDonRow stock,
        decimal stockAfter)
        => connection.ExecuteAsync(
            """
            INSERT INTO lich_su_nhap_xuat_kho
            (
                chi_nhanh_id, san_pham_id, loai_giao_dich, loai_chung_tu,
                ma_chung_tu, so_luong, ton_truoc, ton_sau, gia_von,
                nguoi_thuc_hien_id, thoi_gian, ghi_chu
            )
            VALUES
            (
                @ChiNhanhId, @SanPhamId, @LoaiGiaoDich, 'DIEU_CHINH_HOA_DON',
                @MaChungTu, @SoLuong, @TonTruoc, @TonSau, @GiaVon,
                @NguoiThucHienId, @ThoiGian, @GhiChu
            );
            """,
            new
            {
                ChiNhanhId = state.BranchId,
                SanPhamId = productId,
                LoaiGiaoDich = delta > 0 ? "XUAT" : "NHAP",
                MaChungTu = state.Header.MaHoaDon,
                SoLuong = Math.Abs(delta),
                TonTruoc = stock.SoLuongTon,
                TonSau = stockAfter,
                GiaVon = stock.GiaVonBinhQuan > 0
                    ? stock.GiaVonBinhQuan
                    : state.Products[productId].GiaVon,
                NguoiThucHienId = state.UserId,
                ThoiGian = state.Now,
                GhiChu = $"Điều chỉnh tồn kho khi sửa hóa đơn {state.Header.MaHoaDon}. Lý do: {state.Request.LyDoSua}"
            },
            transaction);

    private static async Task ReplaceHoaDonEditItemsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditState state)
    {
        await connection.ExecuteAsync(
            "DELETE FROM chi_tiet_hoa_don_ban_hang WHERE hoa_don_id = @HoaDonId;",
            new { HoaDonId = state.Header.Id },
            transaction);

        const string sql = """
        INSERT INTO chi_tiet_hoa_don_ban_hang
        (
            hoa_don_id, san_pham_id, ma_san_pham, ten_san_pham, don_vi_tinh,
            so_luong, don_gia, giam_gia, thanh_tien, gia_von, ghi_chu
        )
        VALUES
        (
            @HoaDonId, @SanPhamId, @MaSanPham, @TenSanPham, @DonViTinh,
            @SoLuong, @DonGia, @GiamGia, @ThanhTien, @GiaVon, @GhiChu
        );
        """;

        foreach (var item in state.Request.Items)
        {
            var productId = item.SanPhamId;
            var oldItem = FindExistingInvoiceItem(state, productId);
            await connection.ExecuteAsync(
                sql,
                new
                {
                    HoaDonId = state.Header.Id,
                    SanPhamId = productId,
                    MaSanPham = oldItem?.MaSanPham ?? state.Products[productId].MaSanPham,
                    TenSanPham = oldItem?.TenSanPham ?? state.Products[productId].TenSanPham,
                    item.DonViTinh,
                    item.SoLuong,
                    item.DonGia,
                    item.GiamGia,
                    ThanhTien = Math.Round(
                        item.SoLuong * item.DonGia - item.GiamGia,
                        2,
                        MidpointRounding.AwayFromZero),
                    GiaVon = GetEditItemCost(state, productId),
                    item.GhiChu
                },
                transaction);
        }
    }

    private static async Task UpdateHoaDonEditFinancialsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditState state)
    {
        var currentDebt = await LoadCurrentCustomerDebtAsync(connection, transaction, state.Header.KhachHangId);
        await UpdateHoaDonEditHeaderAsync(connection, transaction, state);
        await WriteInvoiceDebtAdjustmentAsync(connection, transaction, state, currentDebt);
    }

    private static Task<decimal> LoadCurrentCustomerDebtAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint? customerId)
    {
        if (customerId is not uint id || id == 0)
            return Task.FromResult(0m);

        return connection.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
            FROM lich_su_cong_no_khach_hang
            WHERE khach_hang_id = @KhachHangId
            FOR UPDATE;
            """,
            new { KhachHangId = id },
            transaction);
    }

    private static Task<int> UpdateHoaDonEditHeaderAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditState state)
    {
        const string updateSql = """
        UPDATE hoa_don_ban_hang
        SET tong_tien_hang = @TongTienHang,
            giam_gia = @GiamGia,
            thu_khac = @ThuKhac,
            khach_can_tra = @KhachCanTra,
            khach_da_thanh_toan = @DaThanhToan,
            con_no = @ConNo,
            cong_no_da_ghi_nhan = @CongNoDaGhiNhan,
            trang_thai_thanh_toan = @TrangThaiThanhToan,
            ghi_chu = @GhiChu,
            ngay_cap_nhat = @Now
        WHERE id = @Id;
        """;
        return connection.ExecuteAsync(
            updateSql,
            new
            {
                TongTienHang = state.ItemsTotal,
                GiamGia = state.InvoiceDiscount,
                ThuKhac = state.Header.ThuKhac,
                KhachCanTra = state.AmountDue,
                DaThanhToan = state.Header.KhachDaThanhToan,
                ConNo = state.NewDebt,
                CongNoDaGhiNhan = state.Header.CongNoDaGhiNhan == 0 && state.NewDebt > 0
                    ? 1
                    : state.Header.CongNoDaGhiNhan,
                TrangThaiThanhToan = TrangThaiThanhToan(state),
                GhiChu = BlankToNull(state.Request.GhiChu),
                state.Now,
                Id = state.Header.Id
            },
            transaction);
    }

    private static string TrangThaiThanhToan(HoaDonEditState state)
        => state.NewDebt > 0
            ? state.Header.KhachDaThanhToan > 0 ? "THANH_TOAN_MOT_PHAN" : "CONG_NO"
            : "DA_THANH_TOAN";

    private static async Task WriteInvoiceDebtAdjustmentAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditState state,
        decimal currentDebt)
    {
        if (state.Header.KhachHangId is not uint customerId || customerId == 0)
            return;

        var delta = state.NewDebt - state.Header.ConNo;
        if (delta == 0)
            return;

        await connection.ExecuteAsync(
            """
            INSERT INTO lich_su_cong_no_khach_hang
            (
                khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu,
                ma_chung_tu, ghi_no, ghi_co, so_du, thoi_gian,
                nguoi_thuc_hien_id, ghi_chu
            )
            VALUES
            (
                @KhachHangId, @ChiNhanhId, 'DIEU_CHINH', 'HOA_DON',
                @MaChungTu, @GhiNo, @GhiCo, @SoDu, @ThoiGian,
                @NguoiThucHienId, @GhiChu
            );
            """,
            new
            {
                KhachHangId = customerId,
                ChiNhanhId = state.BranchId,
                MaChungTu = state.Header.MaHoaDon,
                GhiNo = Math.Max(0m, delta),
                GhiCo = Math.Max(0m, -delta),
                SoDu = currentDebt + delta,
                ThoiGian = state.Now,
                NguoiThucHienId = state.UserId,
                GhiChu = $"Điều chỉnh công nợ do sửa hóa đơn {state.Header.MaHoaDon} " +
                         $"({(delta > 0 ? "+" : "")}{delta:N0} đ). Lý do: {state.Request.LyDoSua}"
            },
            transaction);
    }

    private static async Task SaveHoaDonEditHistoryAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditState state)
    {
        var snapshotBefore = CreateHoaDonEditBeforeSnapshot(state);
        var snapshotAfter = CreateHoaDonEditAfterSnapshot(state);

        await connection.ExecuteAsync(
            """
            INSERT INTO lich_su_sua_hoa_don
            (
                hoa_don_id, ma_hoa_don, nguoi_sua_id, ten_nguoi_sua, thoi_gian_sua,
                ly_do_sua, tong_tien_truoc, tong_tien_sau, ghi_chu_thay_doi,
                du_lieu_truoc_json, du_lieu_sau_json
            )
            VALUES
            (
                @HoaDonId, @MaHoaDon, @NguoiSuaId, @TenNguoiSua, @ThoiGianSua,
                @LyDoSua, @TongTienTruoc, @TongTienSau, @GhiChuThayDoi,
                @DuLieuTruocJson, @DuLieuSauJson
            );
            """,
            new
            {
                HoaDonId = state.Header.Id,
                MaHoaDon = state.Header.MaHoaDon,
                NguoiSuaId = state.UserId,
                TenNguoiSua = Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Hệ thống",
                ThoiGianSua = state.Now,
                LyDoSua = state.Request.LyDoSua.Trim(),
                TongTienTruoc = state.Header.KhachCanTra,
                TongTienSau = state.AmountDue,
                GhiChuThayDoi = BuildHoaDonEditSummary(state),
                DuLieuTruocJson = JsonSerializer.Serialize(snapshotBefore),
                DuLieuSauJson = JsonSerializer.Serialize(snapshotAfter)
            },
            transaction);
    }

    private static List<HoaDonItemSnapshot> CreateHoaDonEditBeforeSnapshot(HoaDonEditState state)
        => state.OldItems.Select(item => new HoaDonItemSnapshot
        {
            SanPhamId = item.SanPhamId,
            MaSanPham = item.MaSanPham,
            TenSanPham = item.TenSanPham,
            DonViTinh = item.DonViTinh,
            SoLuong = item.SoLuong,
            DonGia = item.DonGia,
            GiamGia = item.GiamGia,
            ThanhTien = item.ThanhTien,
            GiaVon = item.GiaVon,
            GhiChu = item.GhiChu
        }).ToList();

    private static List<HoaDonItemSnapshot> CreateHoaDonEditAfterSnapshot(HoaDonEditState state)
        => state.Request.Items.Select(item => new HoaDonItemSnapshot
        {
            SanPhamId = item.SanPhamId,
            MaSanPham = FindExistingInvoiceItem(state, item.SanPhamId)?.MaSanPham
                ?? state.Products[item.SanPhamId].MaSanPham,
            TenSanPham = FindExistingInvoiceItem(state, item.SanPhamId)?.TenSanPham
                ?? state.Products[item.SanPhamId].TenSanPham,
            DonViTinh = item.DonViTinh,
            SoLuong = item.SoLuong,
            DonGia = item.DonGia,
            GiamGia = item.GiamGia,
            ThanhTien = Math.Round(item.SoLuong * item.DonGia - item.GiamGia, 2, MidpointRounding.AwayFromZero),
            GiaVon = GetEditItemCost(state, item.SanPhamId),
            GhiChu = item.GhiChu
        }).ToList();

    private static HoaDonItemDbRow? FindExistingInvoiceItem(HoaDonEditState state, uint productId)
        => state.OldItems.FirstOrDefault(item => item.SanPhamId == productId);

    private static decimal GetEditItemCost(HoaDonEditState state, uint productId)
    {
        var oldItem = FindExistingInvoiceItem(state, productId);
        if (oldItem is not null)
            return oldItem.GiaVon;

        var stockCost = state.Stock[productId].GiaVonBinhQuan;
        return stockCost > 0 ? stockCost : state.Products[productId].GiaVon;
    }

    private static string BuildHoaDonEditSummary(HoaDonEditState state)
    {
        var changes = new List<string>();
        var oldByProduct = state.OldItems.GroupBy(item => item.SanPhamId)
            .ToDictionary(group => group.Key, group => group.First());
        var newByProduct = state.Request.Items.ToDictionary(item => item.SanPhamId);

        foreach (var productId in state.OldQuantities.Keys.Union(state.NewQuantities.Keys).OrderBy(id => id))
        {
            oldByProduct.TryGetValue(productId, out var oldItem);
            newByProduct.TryGetValue(productId, out var newItem);
            if (oldItem is null && newItem is not null)
            {
                changes.Add($"+ Thêm '{state.Products[productId].TenSanPham}' SL {newItem.SoLuong:N3}, " +
                            $"giá {newItem.DonGia:N0} đ, giảm {newItem.GiamGia:N0} đ");
                continue;
            }
            if (oldItem is not null && newItem is null)
            {
                changes.Add($"- Bỏ mặt hàng '{oldItem.TenSanPham}'");
                continue;
            }
            if (oldItem is null || newItem is null)
                continue;

            var details = new List<string>();
            if (oldItem.SoLuong != newItem.SoLuong)
                details.Add($"SL {oldItem.SoLuong:N3} ➔ {newItem.SoLuong:N3}");
            if (oldItem.DonGia != newItem.DonGia)
                details.Add($"đơn giá {oldItem.DonGia:N0} ➔ {newItem.DonGia:N0} đ");
            if (oldItem.GiamGia != newItem.GiamGia)
                details.Add($"giảm giá dòng {oldItem.GiamGia:N0} ➔ {newItem.GiamGia:N0} đ");
            if (details.Count > 0)
                changes.Add($"• Sửa '{oldItem.TenSanPham}': {string.Join(", ", details)}");
        }

        if (state.Header.GiamGia != state.InvoiceDiscount)
            changes.Add($"• Giảm giá hóa đơn {state.Header.GiamGia:N0} ➔ {state.InvoiceDiscount:N0} đ");
        if (state.Header.KhachCanTra != state.AmountDue)
            changes.Add($"• Khách cần trả {state.Header.KhachCanTra:N0} ➔ {state.AmountDue:N0} đ");
        if (state.Header.ConNo != state.NewDebt)
            changes.Add($"• Công nợ {state.Header.ConNo:N0} ➔ {state.NewDebt:N0} đ");

        return string.Join(Environment.NewLine, changes);
    }

    private static async Task WriteHoaDonEditAuditAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        HoaDonEditState state)
    {
        var user = Session.CurrentUser;
        await connection.ExecuteAsync(
            """
            INSERT INTO nhat_ky_hoat_dong
            (
                nguoi_dung_id, ten_dang_nhap, ho_ten, vai_tro, hanh_dong,
                doi_tuong, doi_tuong_id, ma_chung_tu, noi_dung, gia_tri, thoi_gian
            )
            VALUES
            (
                @NguoiDungId, @TenDangNhap, @HoTen, @VaiTro, 'Sửa hóa đơn',
                'HOA_DON', @HoaDonId, @MaHoaDon, @NoiDung, @GiaTri, @ThoiGian
            );
            """,
            new
            {
                NguoiDungId = user?.Id,
                TenDangNhap = user?.TenDangNhap,
                HoTen = user?.HoTen,
                VaiTro = user?.VaiTro ?? user?.MaVaiTro ?? "",
                HoaDonId = state.Header.Id,
                MaHoaDon = state.Header.MaHoaDon,
                NoiDung = $"Chỉnh sửa hóa đơn {state.Header.MaHoaDon}: " +
                          $"{state.Header.KhachCanTra:N0} đ ➔ {state.AmountDue:N0} đ. " +
                          $"Lý do: {state.Request.LyDoSua}",
                GiaTri = state.AmountDue,
                ThoiGian = state.Now
            },
            transaction);
    }
}
