-- ============================================================
-- LỊCH SỬ CHỈNH SỬA HÓA ĐƠN BÁN HÀNG
-- ============================================================
--
-- Mục đích
--   Lưu lại toàn bộ thay đổi khi người dùng sửa một hóa đơn bán hàng đã tạo:
--   người thực hiện, thời gian, lý do, tổng tiền/công nợ trước - sau,
--   ảnh chụp (JSON) dữ liệu trước/sau và mô tả chi tiết các thay đổi.
--
-- Cách dùng
--   Ứng dụng tự tạo bảng này khi khởi động hoặc khi mở chức năng sửa hóa đơn
--   (DatabaseService.EnsureLichSuChinhSuaHoaDonTableAsync).
--   File script này dùng để chạy tay trên database hiện có nếu cần; an toàn khi chạy lại nhiều lần.
--

CREATE TABLE IF NOT EXISTS `lich_su_chinh_sua_hoa_don` (
  `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT,
  `hoa_don_id` int(10) UNSIGNED NOT NULL,
  `ma_hoa_don` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ten_nguoi_thuc_hien` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `ly_do` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `tong_tien_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
  `tong_tien_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
  `giam_gia_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
  `giam_gia_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
  `khach_can_tra_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
  `khach_can_tra_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
  `khach_da_tra` decimal(15,2) NOT NULL DEFAULT '0.00',
  `con_no_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
  `con_no_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
  `khach_hang_id_truoc` int(10) UNSIGNED DEFAULT NULL,
  `khach_hang_id_sau` int(10) UNSIGNED DEFAULT NULL,
  `ten_khach_hang_truoc` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_khach_hang_sau` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thoi_gian_ban_truoc` datetime DEFAULT NULL,
  `thoi_gian_ban_sau` datetime DEFAULT NULL,
  `du_lieu_truoc_json` longtext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `du_lieu_sau_json` longtext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `chi_tiet_thay_doi` text COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_lsshd_hoa_don` (`hoa_don_id`),
  KEY `idx_lsshd_ma_hoa_don` (`ma_hoa_don`),
  KEY `idx_lsshd_thoi_gian` (`thoi_gian`),
  KEY `idx_lsshd_nguoi_thuc_hien` (`nguoi_thuc_hien_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
