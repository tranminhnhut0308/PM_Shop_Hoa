-- ============================================================
-- MIGRATION ISSUE #61: THONG TIN GIAO NHAN, HOA DON & CHUNG TU PHIEU NHAP HANG
-- ============================================================

-- 1. Bổ sung các cột vào bảng phieu_nhap_hang nếu chưa tồn tại
SET @dbname = DATABASE();

SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = @dbname
      AND TABLE_NAME = 'phieu_nhap_hang'
      AND COLUMN_NAME = 'ten_nguoi_giao_hang'
  ) > 0,
  'SELECT 1',
  'ALTER TABLE phieu_nhap_hang ADD COLUMN 	en_nguoi_giao_hang VARCHAR(150) NULL DEFAULT NULL AFTER ghi_chu;'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = @dbname
      AND TABLE_NAME = 'phieu_nhap_hang'
      AND COLUMN_NAME = 'sdt_nguoi_giao_hang'
  ) > 0,
  'SELECT 1',
  'ALTER TABLE phieu_nhap_hang ADD COLUMN sdt_nguoi_giao_hang VARCHAR(50) NULL DEFAULT NULL AFTER 	en_nguoi_giao_hang;'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = @dbname
      AND TABLE_NAME = 'phieu_nhap_hang'
      AND COLUMN_NAME = 'ngay_hoa_don_dau_vao'
  ) > 0,
  'SELECT 1',
  'ALTER TABLE phieu_nhap_hang ADD COLUMN 
gay_hoa_don_dau_vao DATE NULL DEFAULT NULL AFTER so_hoa_don_dau_vao;'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

-- 2. Bảng đính kèm chứng từ (attachments) cho phiếu nhập hàng
CREATE TABLE IF NOT EXISTS dinh_kem_phieu_nhap (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  phieu_nhap_id INT UNSIGNED NOT NULL,
  ma_phieu_nhap VARCHAR(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  	en_file_goc VARCHAR(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  	en_file_luu VARCHAR(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  duong_dan_tuong_doi VARCHAR(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  dinh_dang VARCHAR(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  dung_luong BIGINT UNSIGNED NOT NULL DEFAULT 0,
  loai_chung_tu VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOA_DON_GIAY',
  ghi_chu TEXT COLLATE utf8mb4_unicode_ci NULL,
  
guoi_tai_len_id INT UNSIGNED NULL DEFAULT NULL,
  	en_nguoi_tai_len VARCHAR(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  	hoi_gian_tai_len DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  da_xoa TINYINT(1) NOT NULL DEFAULT 0,
  	hoi_gian_xoa DATETIME NULL DEFAULT NULL,
  
guoi_xoa_id INT UNSIGNED NULL DEFAULT NULL,
  	en_nguoi_xoa VARCHAR(150) COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  ly_do_xoa TEXT COLLATE utf8mb4_unicode_ci NULL,
  PRIMARY KEY (id),
  KEY idx_dkpn_phieu_nhap (phieu_nhap_id),
  KEY idx_dkpn_ma_phieu (ma_phieu_nhap),
  KEY idx_dkpn_thoi_gian (	hoi_gian_tai_len)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;