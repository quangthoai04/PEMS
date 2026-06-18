-- ============================================================
-- UC-17 Schema Fix: visit_requests table drift
-- Run against: pems_db
-- ============================================================

USE pems_db;

-- 1. Add missing registrant_nationality column
--    (entity has this column, original DDL did not include it)
ALTER TABLE `visit_requests`
  ADD COLUMN `registrant_nationality` VARCHAR(100) NULL
  AFTER `registrant_full_name`;

-- 2. Expand status ENUM to include all values used by the application
--    Original: PENDING_APPROVAL, REJECTED, APPROVED, CANCELLED
--    Added:    PENDING_HO_APPROVAL, PENDING_STAFF_LEAD_APPROVAL, IN_PROGRESS, COMPLETED
ALTER TABLE `visit_requests`
  MODIFY COLUMN `status` ENUM(
    'PENDING_APPROVAL',
    'PENDING_HO_APPROVAL',
    'PENDING_STAFF_LEAD_APPROVAL',
    'APPROVED',
    'REJECTED',
    'CANCELLED',
    'IN_PROGRESS',
    'COMPLETED'
  ) NOT NULL DEFAULT 'PENDING_STAFF_LEAD_APPROVAL';
