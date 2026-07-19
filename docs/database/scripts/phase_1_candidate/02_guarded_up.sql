-- Guarded UP Script for Phase I
-- MUST SET @ENABLE_PHASE_1_DROP = 1 to run
DELIMITER //

CREATE PROCEDURE `ExecutePhase1Drop`()
BEGIN
    DECLARE db_name VARCHAR(255);
    SELECT DATABASE() INTO db_name;
    
    IF db_name NOT IN ('pems_i_fresh', 'pems_i_upgrade', 'pems_i_refusal', 'pems_i_rollback') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Refused: Not an allowed disposable database.';
    END IF;

    IF @ENABLE_PHASE_1_DROP != 1 OR @ENABLE_PHASE_1_DROP IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Refused: Explicit opt-in required (@ENABLE_PHASE_1_DROP = 1)';
    END IF;

    -- Proceed to drop columns
    ALTER TABLE `visit_requests`
    DROP COLUMN `delegation_name`,
    DROP COLUMN `visit_type`,
    DROP COLUMN `visit_type_other`,
    DROP COLUMN `purpose`,
    DROP COLUMN `working_content`,
    DROP COLUMN `working_language`,
    DROP COLUMN `transportation_note`,
    DROP COLUMN `media_consent_status`,
    DROP COLUMN `media_consent_note`,
    DROP COLUMN `note_to_fptu`;
    
    SELECT 'Phase I Drop Completed' as result;
END//

DELIMITER ;

CALL ExecutePhase1Drop();
DROP PROCEDURE ExecutePhase1Drop;
