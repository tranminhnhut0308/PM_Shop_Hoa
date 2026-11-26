DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_migration_issue136`$$

CREATE PROCEDURE `sp_migration_issue136`()
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'san_pham'
          AND COLUMN_NAME = 'ma_dvt_co_ban'
    ) THEN
        ALTER TABLE `san_pham`
            ADD COLUMN `ma_dvt_co_ban` VARCHAR(50) NULL DEFAULT NULL AFTER `don_vi_tinh_id`;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'san_pham'
          AND COLUMN_NAME = 'quy_doi'
    ) THEN
        ALTER TABLE `san_pham`
            ADD COLUMN `quy_doi` DECIMAL(15,4) NULL DEFAULT NULL AFTER `ma_dvt_co_ban`;
    END IF;
END$$

DELIMITER ;

CALL `sp_migration_issue136`();

DROP PROCEDURE IF EXISTS `sp_migration_issue136`;
