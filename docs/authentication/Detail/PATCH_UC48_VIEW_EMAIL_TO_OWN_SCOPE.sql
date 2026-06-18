-- PEMS RBAC patch: change UC-48 View Email from R to O (Own scope)
-- Reason: email visibility must be scoped to sender/recipient/participant or linked delegation access, not global read.
-- Safe to run after pems_full.sql seed. Re-run is idempotent.

UPDATE role_permissions rp
JOIN permissions p ON p.permission_id = rp.permission_id
SET rp.permission_level = 'O'
WHERE p.permission_code = 'UC-48.VIEW_EMAIL'
  AND rp.permission_level = 'R';

-- Verify:
SELECT r.role_code, rp.sub_role, p.permission_code, rp.permission_level
FROM role_permissions rp
JOIN roles r ON r.role_id = rp.role_id
JOIN permissions p ON p.permission_id = rp.permission_id
WHERE p.permission_code = 'UC-48.VIEW_EMAIL'
ORDER BY r.role_code, rp.sub_role;
