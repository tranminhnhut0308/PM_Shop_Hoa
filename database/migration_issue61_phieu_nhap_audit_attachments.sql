-- ============================================================
-- MIGRATION ISSUE #61: Supplier delivery, invoice and attachments
-- ============================================================

SET @dbname = DATABASE();

SET @preparedStatement = (SELECT IF(
  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'phieu_nhap_hang'
     AND COLUMN_NAME = 'ten_nguoi_giao_hang') > 0,
  'SELECT 1',
  'ALTER TABLE `phieu_nhap_hang` ADD COLUMN `ten_nguoi_giao_hang` VARCHAR(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL;'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

SET @preparedStatement = (SELECT IF(
  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'phieu_nhap_hang'
     AND COLUMN_NAME = 'sdt_nguoi_giao_hang') > 0,
  'SELECT 1',
  'ALTER TABLE `phieu_nhap_hang` ADD COLUMN `sdt_nguoi_giao_hang` VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL;'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

SET @preparedStatement = (SELECT IF(
  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'phieu_nhap_hang'
     AND COLUMN_NAME = 'ngay_hoa_don_dau_vao') > 0,
  'SELECT 1',
  'ALTER TABLE `phieu_nhap_hang` ADD COLUMN `ngay_hoa_don_dau_vao` DATE NULL DEFAULT NULL;'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

CREATE TABLE IF NOT EXISTS `dinh_kem_phieu_nhap` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `phieu_nhap_id` INT UNSIGNED NOT NULL,
  `ma_phieu_nhap` VARCHAR(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_file_goc` VARCHAR(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_file_luu` VARCHAR(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `duong_dan_tuong_doi` VARCHAR(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `dinh_dang` VARCHAR(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `dung_luong` BIGINT UNSIGNED NOT NULL DEFAULT 0,
  `loai_chung_tu` VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOA_DON_GIAY',
  `ghi_chu` TEXT COLLATE utf8mb4_unicode_ci NULL,
  `nguoi_tai_len_id` INT UNSIGNED NULL DEFAULT NULL,
  `ten_nguoi_tai_len` VARCHAR(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `thoi_gian_tai_len` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `da_xoa` TINYINT(1) NOT NULL DEFAULT 0,
  `thoi_gian_xoa` DATETIME NULL DEFAULT NULL,
  `nguoi_xoa_id` INT UNSIGNED NULL DEFAULT NULL,
  `ten_nguoi_xoa` VARCHAR(150) COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `ly_do_xoa` TEXT COLLATE utf8mb4_unicode_ci NULL,
  PRIMARY KEY (`id`),
  KEY `idx_dkpn_phieu_nhap` (`phieu_nhap_id`),
  KEY `idx_dkpn_ma_phieu` (`ma_phieu_nhap`),
  KEY `idx_dkpn_thoi_gian` (`thoi_gian_tai_len`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;