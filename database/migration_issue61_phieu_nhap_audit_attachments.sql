-- ============================================================
<<<<<<< HEAD
-- MIGRATION ISSUE #61: Supplier delivery, invoice and attachments
-- ============================================================

=======
-- MIGRATION ISSUE #61: THONG TIN GIAO NHAN, HOA DON & CHUNG TU PHIEU NHAP HANG
-- ============================================================

-- 1. Bổ sung các cột vào bảng phieu_nhap_hang nếu chưa tồn tại.
--    Mỗi cột được kiểm tra độc lập để database cũ chỉ thiếu một vài cột
--    (ví dụ ma_dat_hang_nhap) vẫn được bổ sung đầy đủ.
>>>>>>> 1c5e0674376daca902ac17d7858913d067ad7aed
SET @dbname = DATABASE();

SET @preparedStatement = (SELECT IF(
  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'phieu_nhap_hang'
<<<<<<< HEAD
     AND COLUMN_NAME = 'ten_nguoi_giao_hang') > 0,
  'SELECT 1',
  'ALTER TABLE `phieu_nhap_hang` ADD COLUMN `ten_nguoi_giao_hang` VARCHAR(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL;'
=======
     AND COLUMN_NAME = 'so_hoa_don_dau_vao') > 0,
  'SELECT 1',
  'ALTER TABLE phieu_nhap_hang ADD COLUMN so_hoa_don_dau_vao VARCHAR(50) NULL DEFAULT NULL AFTER ghi_chu;'
>>>>>>> 1c5e0674376daca902ac17d7858913d067ad7aed
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

SET @preparedStatement = (SELECT IF(
  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'phieu_nhap_hang'
<<<<<<< HEAD
     AND COLUMN_NAME = 'sdt_nguoi_giao_hang') > 0,
  'SELECT 1',
  'ALTER TABLE `phieu_nhap_hang` ADD COLUMN `sdt_nguoi_giao_hang` VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL;'
=======
     AND COLUMN_NAME = 'ma_dat_hang_nhap') > 0,
  'SELECT 1',
  'ALTER TABLE phieu_nhap_hang ADD COLUMN ma_dat_hang_nhap VARCHAR(50) NULL DEFAULT NULL AFTER so_hoa_don_dau_vao;'
>>>>>>> 1c5e0674376daca902ac17d7858913d067ad7aed
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

SET @preparedStatement = (SELECT IF(
  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'phieu_nhap_hang'
     AND COLUMN_NAME = 'ngay_hoa_don_dau_vao') > 0,
  'SELECT 1',
<<<<<<< HEAD
  'ALTER TABLE `phieu_nhap_hang` ADD COLUMN `ngay_hoa_don_dau_vao` DATE NULL DEFAULT NULL;'
=======
  'ALTER TABLE phieu_nhap_hang ADD COLUMN ngay_hoa_don_dau_vao DATE NULL DEFAULT NULL AFTER ma_dat_hang_nhap;'
>>>>>>> 1c5e0674376daca902ac17d7858913d067ad7aed
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

<<<<<<< HEAD
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
=======
SET @preparedStatement = (SELECT IF(
  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'phieu_nhap_hang'
     AND COLUMN_NAME = 'ten_nguoi_giao_hang') > 0,
  'SELECT 1',
  'ALTER TABLE phieu_nhap_hang ADD COLUMN ten_nguoi_giao_hang VARCHAR(150) NULL DEFAULT NULL AFTER ngay_hoa_don_dau_vao;'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

SET @preparedStatement = (SELECT IF(
  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'phieu_nhap_hang'
     AND COLUMN_NAME = 'sdt_nguoi_giao_hang') > 0,
  'SELECT 1',
  'ALTER TABLE phieu_nhap_hang ADD COLUMN sdt_nguoi_giao_hang VARCHAR(50) NULL DEFAULT NULL AFTER ten_nguoi_giao_hang;'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

-- 2. Bổ sung cột đơn vị tính đọc từ Excel cho chi tiết phiếu nhập.
SET @preparedStatement = (SELECT IF(
  (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'chi_tiet_phieu_nhap_hang'
     AND COLUMN_NAME = 'don_vi_tinh_excel') > 0,
  'SELECT 1',
  'ALTER TABLE chi_tiet_phieu_nhap_hang ADD COLUMN don_vi_tinh_excel VARCHAR(50) NULL DEFAULT NULL AFTER ten_san_pham;'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

-- 3. Bảng đính kèm chứng từ (attachments) cho phiếu nhập hàng
CREATE TABLE IF NOT EXISTS dinh_kem_phieu_nhap (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  phieu_nhap_id INT UNSIGNED NOT NULL,
  ma_phieu_nhap VARCHAR(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  ten_file_goc VARCHAR(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  ten_file_luu VARCHAR(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  duong_dan_tuong_doi VARCHAR(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  dinh_dang VARCHAR(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  dung_luong BIGINT UNSIGNED NOT NULL DEFAULT 0,
  loai_chung_tu VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOA_DON_GIAY',
  ghi_chu TEXT COLLATE utf8mb4_unicode_ci NULL,
  nguoi_tai_len_id INT UNSIGNED NULL DEFAULT NULL,
  ten_nguoi_tai_len VARCHAR(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  thoi_gian_tai_len DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  da_xoa TINYINT(1) NOT NULL DEFAULT 0,
  thoi_gian_xoa DATETIME NULL DEFAULT NULL,
  nguoi_xoa_id INT UNSIGNED NULL DEFAULT NULL,
  ten_nguoi_xoa VARCHAR(150) COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  ly_do_xoa TEXT COLLATE utf8mb4_unicode_ci NULL,
  PRIMARY KEY (id),
  KEY idx_dkpn_phieu_nhap (phieu_nhap_id),
  KEY idx_dkpn_ma_phieu (ma_phieu_nhap),
  KEY idx_dkpn_thoi_gian (thoi_gian_tai_len)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
>>>>>>> 1c5e0674376daca902ac17d7858913d067ad7aed
