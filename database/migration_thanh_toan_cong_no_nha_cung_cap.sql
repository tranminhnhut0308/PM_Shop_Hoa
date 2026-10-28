CREATE TABLE IF NOT EXISTS `lich_su_thanh_toan_cong_no_nha_cung_cap` (
  `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT,
  `nha_cung_cap_id` int(10) UNSIGNED NOT NULL,
  `ma_nha_cung_cap` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_nha_cung_cap` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `don_vi_nhan` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ma_giao_dich` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `so_tien` decimal(15,2) NOT NULL,
  `cong_no_truoc` decimal(15,2) NOT NULL,
  `cong_no_sau` decimal(15,2) NOT NULL,
  `thoi_gian` datetime NOT NULL,
  `phuong_thuc` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TIEN_MAT',
  `ma_tham_chieu` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `trang_thai` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOAN_THANH',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_lstncc_ma_giao_dich` (`ma_giao_dich`),
  KEY `idx_lstncc_ncc_thoi_gian` (`nha_cung_cap_id`, `thoi_gian`),
  KEY `idx_lstncc_nguoi_thuc_hien` (`nguoi_thuc_hien_id`),
  CONSTRAINT `fk_lstncc_thanh_toan_ncc`
    FOREIGN KEY (`nha_cung_cap_id`) REFERENCES `nha_cung_cap` (`id`)
    ON UPDATE CASCADE,
  CONSTRAINT `fk_lstncc_thanh_toan_nguoi`
    FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`)
    ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
