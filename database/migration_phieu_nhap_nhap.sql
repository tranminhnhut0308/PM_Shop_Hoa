-- Issue #56: lưu nháp nhiều phiếu nhập hàng.
-- Phiếu nháp KHÔNG cập nhật tồn kho, công nợ hay sổ quỹ; chỉ khi hoàn thành mới ghi vào
-- phieu_nhap_hang. Ứng dụng cũng tự tạo bảng này nếu chưa có (CREATE TABLE IF NOT EXISTS).
CREATE TABLE IF NOT EXISTS phieu_nhap_nhap (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    draft_id        CHAR(32)        NOT NULL COMMENT 'ID riêng của từng phiếu nháp',
    nguoi_dung_id   INT UNSIGNED    NOT NULL,
    chi_nhanh_id    INT UNSIGNED    NULL,
    so_thu_tu       INT             NOT NULL DEFAULT 1,
    nha_cung_cap_id INT UNSIGNED    NULL,
    so_dong         INT             NOT NULL DEFAULT 0,
    du_lieu         LONGTEXT        NOT NULL COMMENT 'JSON: NCC, dòng hàng, chi phí, thanh toán, ghi chú',
    phien_ban       INT UNSIGNED    NOT NULL DEFAULT 1,
    trang_thai      VARCHAR(20)     NOT NULL DEFAULT 'DANG_SOAN' COMMENT 'DANG_SOAN | DA_HOAN_TAT',
    phieu_nhap_id   INT UNSIGNED    NULL COMMENT 'Phiếu nhập chính thức sau khi hoàn tất',
    ngay_tao        DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ngay_cap_nhat   DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uk_pnn_draft (draft_id),
    KEY idx_pnn_user (nguoi_dung_id, trang_thai)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
