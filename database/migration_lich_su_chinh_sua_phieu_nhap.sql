-- ============================================================
-- LỊCH SỬ CHỈNH SỬA PHIẾU NHẬP HÀNG
-- ============================================================
--
-- Mục đích:
--   Lưu lại toàn bộ lịch sử thay đổi khi người dùng chỉnh sửa một phiếu nhập hàng đã tạo:
--   người thực hiện, thời gian sửa, lý do sửa, tiền hàng/chi phí/giảm giá/công nợ trước - sau,
--   nhà cung cấp trước - sau, ảnh chụp dữ liệu JSON và mô tả chi tiết độ chênh lệch hàng hóa.
--
-- Cách dùng:
--   Ứng dụng tự động tạo bảng này khi khởi động hoặc khi nạp chi tiết phiếu nhập
--   (DatabaseService.EnsureLichSuChinhSuaPhieuNhapTableAsync).
--   File script này dùng để kiểm tra hoặc chạy tay trên database; an toàn khi chạy nhiều lần.
--

CREATE TABLE IF NOT EXISTS `lich_su_chinh_sua_phieu_nhap` (
  `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT,
  `phieu_nhap_id` int(10) UNSIGNED NOT NULL,
  `ma_phieu_nhap` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ten_nguoi_thuc_hien` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `ly_do` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `tong_tien_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
  `tong_tien_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
  `chi_phi_khac_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
  `chi_phi_khac_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
  `giam_gia_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
  `giam_gia_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
  `da_thanh_toan_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
  `da_thanh_toan_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
  `con_no_truoc` decimal(15,2) NOT NULL DEFAULT '0.00',
  `con_no_sau` decimal(15,2) NOT NULL DEFAULT '0.00',
  `nha_cung_cap_id_truoc` int(10) UNSIGNED DEFAULT NULL,
  `nha_cung_cap_id_sau` int(10) UNSIGNED DEFAULT NULL,
  `ten_nha_cung_cap_truoc` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_nha_cung_cap_sau` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thoi_gian_nhap_truoc` datetime DEFAULT NULL,
  `thoi_gian_nhap_sau` datetime DEFAULT NULL,
  `du_lieu_truoc_json` longtext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `du_lieu_sau_json` longtext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `chi_tiet_thay_doi` text COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_lscpnh_phieu_nhap` (`phieu_nhap_id`),
  KEY `idx_lscpnh_ma_phieu` (`ma_phieu_nhap`),
  KEY `idx_lscpnh_thoi_gian` (`thoi_gian`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
