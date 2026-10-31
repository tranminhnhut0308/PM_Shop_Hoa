-- ============================================================================
-- MIGRATION: Quy trình Hủy hóa đơn và Tạo lại hóa đơn mới (Issues #34, #46)
-- Thay thế chức năng chỉnh sửa trực tiếp hóa đơn cũ bằng quy trình hủy hóa đơn
-- có kiểm soát tồn kho, công nợ khách hàng, sổ quỹ và truy vết hóa đơn thay thế.
-- ============================================================================

SET NAMES utf8mb4;

-- 1. Bổ sung các cột thông tin hủy và liên kết hóa đơn gốc/thay thế vào bảng hoa_don_ban_hang
ALTER TABLE hoa_don_ban_hang
    ADD COLUMN IF NOT EXISTS ly_do_huy TEXT NULL,
    ADD COLUMN IF NOT EXISTS nguoi_huy_id INT UNSIGNED NULL,
    ADD COLUMN IF NOT EXISTS ten_nguoi_huy VARCHAR(150) NULL,
    ADD COLUMN IF NOT EXISTS thoi_gian_huy DATETIME NULL,
    ADD COLUMN IF NOT EXISTS trang_thai_giao_hang_khi_huy VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS da_hoan_kho TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS phuong_an_tien_khi_huy VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS so_tien_hoan DECIMAL(15, 2) NOT NULL DEFAULT 0.00,
    ADD COLUMN IF NOT EXISTS ma_phieu_chi_hoan VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS hoa_don_goc_id INT UNSIGNED NULL,
    ADD COLUMN IF NOT EXISTS ma_hoa_don_goc VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS hoa_don_thay_the_id INT UNSIGNED NULL,
    ADD COLUMN IF NOT EXISTS ma_hoa_don_thay_the VARCHAR(50) NULL;

-- 2. Tạo bảng lịch sử hủy hóa đơn để lưu vết kiểm toán bất biến
CREATE TABLE IF NOT EXISTS lich_su_huy_hoa_don (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    hoa_don_id INT UNSIGNED NOT NULL,
    ma_hoa_don VARCHAR(50) NOT NULL,
    khach_hang_id INT UNSIGNED NULL,
    ten_khach_hang VARCHAR(255) NULL,
    tong_tien_hoa_don DECIMAL(15, 2) NOT NULL DEFAULT 0.00,
    so_tien_da_thanh_toan DECIMAL(15, 2) NOT NULL DEFAULT 0.00,
    cong_no_khi_huy DECIMAL(15, 2) NOT NULL DEFAULT 0.00,
    trang_thai_giao_hang VARCHAR(50) NOT NULL DEFAULT 'CHUA_GIAO',
    da_hoan_kho TINYINT(1) NOT NULL DEFAULT 0,
    phuong_an_tien VARCHAR(50) NOT NULL DEFAULT 'KHONG_CO',
    so_tien_hoan DECIMAL(15, 2) NOT NULL DEFAULT 0.00,
    phuong_thuc_hoan VARCHAR(50) NULL,
    ma_phieu_chi_hoan VARCHAR(50) NULL,
    che_do_huy VARCHAR(50) NOT NULL DEFAULT 'CHI_HUY',
    hoa_don_thay_the_id INT UNSIGNED NULL,
    ma_hoa_don_thay_the VARCHAR(50) NULL,
    ly_do_huy TEXT NOT NULL,
    nguoi_huy_id INT UNSIGNED NULL,
    ten_nguoi_huy VARCHAR(150) NULL,
    thoi_gian_huy DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    du_lieu_snapshot LONGTEXT NULL,
    INDEX idx_lshhd_hoa_don_id (hoa_don_id),
    INDEX idx_lshhd_ma_hoa_don (ma_hoa_don),
    INDEX idx_lshhd_thoi_gian_huy (thoi_gian_huy)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
