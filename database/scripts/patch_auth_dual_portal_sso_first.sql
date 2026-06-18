-- =====================================================================
-- PATCH: Auth Dual-Portal / SSO-first
-- =====================================================================
-- Purpose:
--   Allow the application to record visitor accounts that were created
--   automatically on first external (Google SSO / FEID) login at the
--   Visitor portal, by extending the users.created_via ENUM.
--
-- Safe to run multiple times (idempotent): MODIFY COLUMN simply restates
-- the column definition. It does NOT drop or alter existing data.
--
-- Run against the PEMS database, e.g.:
--   mysql -u root -p pems_db < database/scripts/patch_auth_dual_portal_sso_first.sql
-- =====================================================================

ALTER TABLE users
  MODIFY COLUMN created_via
    ENUM('MANUAL_CREATED','VISITOR_FORM','SSO_AUTO_PROVISION')
    NOT NULL DEFAULT 'MANUAL_CREATED'
    COMMENT 'MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor, SSO_AUTO_PROVISION=tạo tự động khi đăng nhập SSO ở cổng Visitor';
