-- =====================================================================
-- PEMS — Permission catalogue seed (Authentication / Profile / RBAC)
-- Run AFTER database/scripts/pems_full.sql. Idempotent (INSERT IGNORE on
-- the unique permission_code). Mirrors PermissionSeed.cs in the backend.
-- =====================================================================
USE pems_db;

INSERT IGNORE INTO permissions (permission_id, permission_code, name, permission_group, is_system) VALUES
  -- Authentication
  (UUID(), 'UC-010.LOGIN_SSO',            'Login via SSO',            'Authentication', 1),
  (UUID(), 'UC-011.LOGIN_CREDENTIALS',    'Login via Credentials',    'Authentication', 1),
  (UUID(), 'UC-012.LOGOUT',               'Logout',                   'Authentication', 1),
  (UUID(), 'UC-013.FORGOT_PASSWORD',      'Forgot Password',          'Authentication', 1),
  -- Profile
  (UUID(), 'UC-014.VIEW_PROFILE',         'View Profile',             'Profile', 1),
  (UUID(), 'UC-015.UPDATE_PROFILE',       'Update Profile',           'Profile', 1),
  (UUID(), 'UC-016.CHANGE_PASSWORD',      'Change Password',          'Profile', 1),
  -- Account Management
  (UUID(), 'UC-095.VIEW_ACCOUNT_LIST',      'View Account List',        'Account Management', 1),
  (UUID(), 'UC-096.CREATE_ACCOUNT',         'Create Account',           'Account Management', 1),
  (UUID(), 'UC-097.MANAGE_ACCOUNT_STATUS',  'Manage Account Status',    'Account Management', 1),
  (UUID(), 'UC-098.VIEW_ACCOUNT_DETAILS',   'View Account Details',     'Account Management', 1),
  (UUID(), 'UC-099.SEARCH_FILTER_ACCOUNTS', 'Search / Filter Accounts', 'Account Management', 1),
  (UUID(), 'UC-100.UPDATE_ACCOUNT_ROLE',    'Update Account Role',      'Account Management', 1),
  -- Role Management
  (UUID(), 'UC-117.VIEW_ROLE_LIST',            'View Role List',             'Role Management', 1),
  (UUID(), 'UC-118.CREATE_ROLE',               'Create Role',                'Role Management', 1),
  (UUID(), 'UC-119.CONFIGURE_ROLE_PERMISSIONS','Configure Role Permissions', 'Role Management', 1),
  (UUID(), 'UC-120.UPDATE_ROLE_DETAILS',       'Update Role Details',        'Role Management', 1),
  (UUID(), 'UC-121.DISABLE_DELETE_ROLE',       'Disable / Delete Role',      'Role Management', 1);
