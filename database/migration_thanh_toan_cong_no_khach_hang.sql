CREATE TABLE IF NOT EXISTS `lich_su_thanh_toan_cong_no_khach_hang` (
  `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT,
  `khach_hang_id` int(10) UNSIGNED NOT NULL,
  `ma_giao_dich` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `so_tien` decimal(15,2) NOT NULL,
  `cong_no_truoc` decimal(15,2) NOT NULL,
  `cong_no_sau` decimal(15,2) NOT NULL,
  `thoi_gian` datetime NOT NULL,
  `phuong_thuc` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TIEN_MAT',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `trang_thai` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOAN_THANH',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_lsthukh_ma_giao_dich` (`ma_giao_dich`),
  KEY `idx_lsthukh_khach_thoi_gian` (`khach_hang_id`, `thoi_gian`),
  KEY `idx_lsthukh_nguoi_thuc_hien` (`nguoi_thuc_hien_id`),
  CONSTRAINT `fk_lsthukh_khach_hang`
    FOREIGN KEY (`khach_hang_id`) REFERENCES `khach_hang` (`id`)
    ON UPDATE CASCADE,
  CONSTRAINT `fk_lsthukh_nguoi_thuc_hien`
    FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`)
    ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
