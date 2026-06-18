-- =====================================================================
-- PEMS — Permission catalogue seed (Authentication / Profile / RBAC)
-- Run AFTER database/scripts/pems_full.sql. Idempotent (INSERT IGNORE on
-- the unique permission_code). This is a documentation / re-seeding-parity
-- SUBSET of the full catalogue in pems_full.sql — codes, names and groups
-- MUST match pems_full.sql exactly (single source of truth). Canonical code
-- format: UC-NN.NAME (2 digits for 1-99, 3 digits for 100+; never 3-digit-pad
-- a 2-digit number like UC-098).
-- =====================================================================
USE pems_db;

INSERT IGNORE INTO permissions (permission_id, permission_code, name, permission_group, is_system) VALUES
  -- Authentication
  (UUID(), 'UC-10.LOGIN_VIA_SSO',         'UC-10 - Login via SSO',         'Authentication', 1),
  (UUID(), 'UC-11.LOGIN_VIA_CREDENTIALS', 'UC-11 - Login via Credentials', 'Authentication', 1),
  (UUID(), 'UC-12.LOGOUT',                'UC-12 - Logout',                'Authentication', 1),
  (UUID(), 'UC-13.FORGOT_PASSWORD',       'UC-13 - Forgot Password',       'Authentication', 1),
  -- Profile Management
  (UUID(), 'UC-14.VIEW_PROFILE',          'UC-14 - View Profile',          'Profile Management', 1),
  (UUID(), 'UC-15.UPDATE_PROFILE',        'UC-15 - Update Profile',        'Profile Management', 1),
  (UUID(), 'UC-16.CHANGE_PASSWORD',       'UC-16 - Change Password',       'Profile Management', 1),
  -- Account Management
  (UUID(), 'UC-95.VIEW_ACCOUNT_LIST',          'UC-95 - View Account List',         'Account Management', 1),
  (UUID(), 'UC-96.CREATE_ACCOUNT',             'UC-96 - Create Account',            'Account Management', 1),
  (UUID(), 'UC-97.MANAGE_ACCOUNT_STATUS',      'UC-97 - Manage Account Status',     'Account Management', 1),
  (UUID(), 'UC-98.VIEW_ACCOUNT_DETAILS',       'UC-98 - View Account Details',      'Account Management', 1),
  (UUID(), 'UC-99.SEARCH_AND_FILTER_ACCOUNTS', 'UC-99 - Search and Filter Accounts','Account Management', 1),
  (UUID(), 'UC-100.UPDATE_ACCOUNT_ROLE',       'UC-100 - Update Account Role',      'Account Management', 1),
  -- Role & Permission Management
  (UUID(), 'UC-117.VIEW_ROLE_LIST',            'UC-117 - View Role List',             'Role & Permission Management', 1),
  (UUID(), 'UC-118.CREATE_NEW_ROLE',           'UC-118 - Create New Role',            'Role & Permission Management', 1),
  (UUID(), 'UC-119.CONFIGURE_ROLE_PERMISSIONS','UC-119 - Configure Role Permissions', 'Role & Permission Management', 1),
  (UUID(), 'UC-120.UPDATE_ROLE_DETAILS',       'UC-120 - Update Role Details',        'Role & Permission Management', 1),
  (UUID(), 'UC-121.DISABLE_DELETE_ROLE',       'UC-121 - Disable/Delete Role',        'Role & Permission Management', 1);
