-- =====================================================================
-- PEMS — Role ⇄ Permission matrix seed
-- Run AFTER permissions.sql. Idempotent (INSERT IGNORE on the (role_id,
-- permission_id) PK). Mirrors PermissionMatrixSeed.cs in the backend.
--
-- Levels: F=Full, E=Execute/Edit, R=Read, O=Own.
-- =====================================================================
USE pems_db;

-- 1) Every role gets "Own" (O) on authentication + profile use cases.
INSERT IGNORE INTO role_permissions (role_id, permission_id, permission_level, granted_at)
SELECT r.role_id, p.permission_id, 'O', NOW()
FROM roles r
JOIN permissions p
  ON p.permission_code IN (
      'UC-010.LOGIN_SSO','UC-011.LOGIN_CREDENTIALS','UC-012.LOGOUT','UC-013.FORGOT_PASSWORD',
      'UC-014.VIEW_PROFILE','UC-015.UPDATE_PROFILE','UC-016.CHANGE_PASSWORD')
WHERE r.role_code IN ('ADMIN','HO','STAFF','DEPT','STUDENT','VISITOR')
  AND r.deleted_at IS NULL;

-- 2) ADMIN — full (F) account & role management.
INSERT IGNORE INTO role_permissions (role_id, permission_id, permission_level, granted_at)
SELECT r.role_id, p.permission_id, 'F', NOW()
FROM roles r
JOIN permissions p
  ON p.permission_code IN (
      'UC-095.VIEW_ACCOUNT_LIST','UC-096.CREATE_ACCOUNT','UC-097.MANAGE_ACCOUNT_STATUS',
      'UC-098.VIEW_ACCOUNT_DETAILS','UC-099.SEARCH_FILTER_ACCOUNTS','UC-100.UPDATE_ACCOUNT_ROLE',
      'UC-117.VIEW_ROLE_LIST','UC-118.CREATE_ROLE','UC-119.CONFIGURE_ROLE_PERMISSIONS',
      'UC-120.UPDATE_ROLE_DETAILS','UC-121.DISABLE_DELETE_ROLE')
WHERE r.role_code = 'ADMIN'
  AND r.deleted_at IS NULL;

-- 3) HO + STAFF — read-only (R) account visibility (data scope enforced in backend).
INSERT IGNORE INTO role_permissions (role_id, permission_id, permission_level, granted_at)
SELECT r.role_id, p.permission_id, 'R', NOW()
FROM roles r
JOIN permissions p
  ON p.permission_code IN (
      'UC-095.VIEW_ACCOUNT_LIST','UC-098.VIEW_ACCOUNT_DETAILS','UC-099.SEARCH_FILTER_ACCOUNTS')
WHERE r.role_code IN ('HO','STAFF')
  AND r.deleted_at IS NULL;
