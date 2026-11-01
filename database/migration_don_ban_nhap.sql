-- Issue #50: Quản lý nhiều giỏ hàng và lưu trạng thái đơn hàng nháp.
-- Đơn nháp KHÔNG cập nhật tồn kho, công nợ hay sổ quỹ; chỉ khi thanh toán thành công mới ghi vào
-- hoa_don_ban_hang. Ứng dụng cũng tự tạo bảng này nếu chưa có (CREATE TABLE IF NOT EXISTS).
CREATE TABLE IF NOT EXISTS don_ban_nhap (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    cart_id         CHAR(32)        NOT NULL COMMENT 'ID riêng của từng giỏ hàng nháp',
    nguoi_dung_id   INT UNSIGNED    NOT NULL,
    chi_nhanh_id    INT UNSIGNED    NULL,
    so_thu_tu       INT             NOT NULL DEFAULT 1,
    khach_hang_id   INT UNSIGNED    NULL,
    so_dong         INT             NOT NULL DEFAULT 0,
    tong_tien       DECIMAL(15,2)   NOT NULL DEFAULT 0,
    du_lieu         LONGTEXT        NOT NULL COMMENT 'JSON: khách hàng, dòng hàng, giảm giá, thanh toán, ghi chú',
    phien_ban       INT UNSIGNED    NOT NULL DEFAULT 1,
    trang_thai      VARCHAR(20)     NOT NULL DEFAULT 'DANG_SOAN' COMMENT 'DANG_SOAN | DA_HOAN_TAT',
    hoa_don_id      INT UNSIGNED    NULL COMMENT 'Hóa đơn chính thức sau khi thanh toán',
    ngay_tao        DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ngay_cap_nhat   DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uk_dbn_cart (cart_id),
    KEY idx_dbn_user (nguoi_dung_id, trang_thai)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
