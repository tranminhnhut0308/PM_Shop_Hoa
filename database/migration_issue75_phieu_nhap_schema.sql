-- Repairs schema names introduced by the malformed Issue #61 migration.
-- Renaming columns preserves their existing data; no table or row is deleted.
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

DROP PROCEDURE IF EXISTS `migration_issue75_ensure_column`;
DELIMITER $$
CREATE PROCEDURE `migration_issue75_ensure_column`(
  IN p_table VARCHAR(64),
  IN p_column VARCHAR(64),
  IN p_legacy_column VARCHAR(64),
  IN p_definition VARCHAR(512)
)
BEGIN
  DECLARE v_table_exists INT DEFAULT 0;
  DECLARE v_column_exists INT DEFAULT 0;
  DECLARE v_legacy_exists INT DEFAULT 0;

  SELECT COUNT(*) INTO v_table_exists
  FROM INFORMATION_SCHEMA.TABLES
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table;

  IF v_table_exists > 0 THEN
    SELECT COUNT(*) INTO v_column_exists
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table
      AND COLUMN_NAME = p_column;

    IF p_legacy_column <> '' THEN
      SELECT COUNT(*) INTO v_legacy_exists
      FROM INFORMATION_SCHEMA.COLUMNS
      WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table
        AND COLUMN_NAME = p_legacy_column;
    END IF;

    IF v_column_exists = 0 THEN
      IF v_legacy_exists > 0 THEN
        SET @migration_sql = CONCAT(
          'ALTER TABLE `', p_table, '` CHANGE COLUMN `', p_legacy_column,
          '` `', p_column, '` ', p_definition
        );
      ELSE
        SET @migration_sql = CONCAT(
          'ALTER TABLE `', p_table, '` ADD COLUMN `', p_column, '` ', p_definition
        );
      END IF;

      PREPARE migration_statement FROM @migration_sql;
      EXECUTE migration_statement;
      DEALLOCATE PREPARE migration_statement;
    END IF;
  END IF;
END$$
DELIMITER ;

CALL `migration_issue75_ensure_column`('phieu_nhap_hang', 'so_hoa_don_dau_vao', '',
  'VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL');
CALL `migration_issue75_ensure_column`('phieu_nhap_hang', 'ma_dat_hang_nhap', '',
  'VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL');
CALL `migration_issue75_ensure_column`('phieu_nhap_hang', 'ngay_hoa_don_dau_vao', 'gay_hoa_don_dau_vao',
  'DATE NULL DEFAULT NULL');
CALL `migration_issue75_ensure_column`('phieu_nhap_hang', 'ten_nguoi_giao_hang', 'en_nguoi_giao_hang',
  'VARCHAR(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL');
CALL `migration_issue75_ensure_column`('phieu_nhap_hang', 'sdt_nguoi_giao_hang', '',
  'VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL');

CALL `migration_issue75_ensure_column`('dinh_kem_phieu_nhap', 'ten_file_goc', 'en_file_goc',
  CONCAT('VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ', CHAR(39), CHAR(39)));
CALL `migration_issue75_ensure_column`('dinh_kem_phieu_nhap', 'ten_file_luu', 'en_file_luu',
  CONCAT('VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ', CHAR(39), CHAR(39)));
CALL `migration_issue75_ensure_column`('dinh_kem_phieu_nhap', 'nguoi_tai_len_id', 'guoi_tai_len_id',
  'INT UNSIGNED NULL DEFAULT NULL');
CALL `migration_issue75_ensure_column`('dinh_kem_phieu_nhap', 'ten_nguoi_tai_len', 'en_nguoi_tai_len',
  CONCAT('VARCHAR(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ', CHAR(39), CHAR(39)));
CALL `migration_issue75_ensure_column`('dinh_kem_phieu_nhap', 'thoi_gian_tai_len', 'hoi_gian_tai_len',
  'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP');
CALL `migration_issue75_ensure_column`('dinh_kem_phieu_nhap', 'thoi_gian_xoa', 'hoi_gian_xoa',
  'DATETIME NULL DEFAULT NULL');
CALL `migration_issue75_ensure_column`('dinh_kem_phieu_nhap', 'nguoi_xoa_id', 'guoi_xoa_id',
  'INT UNSIGNED NULL DEFAULT NULL');
CALL `migration_issue75_ensure_column`('dinh_kem_phieu_nhap', 'ten_nguoi_xoa', 'en_nguoi_xoa',
  'VARCHAR(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL');

DROP PROCEDURE `migration_issue75_ensure_column`;
