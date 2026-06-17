-- =====================================================================
-- PEMS — Campuses seed
-- The 5 campuses (HN, HCM, DN, CT, QN) and their departments are already
-- created by database/scripts/pems_full.sql. This file documents that and is
-- a no-op when re-run (INSERT IGNORE on campus_code).
-- =====================================================================
USE pems_db;

INSERT IGNORE INTO campuses (campus_id, campus_code, name, city, status) VALUES
  (UUID(), 'HN',  'FPT University Hà Nội',         'Hà Nội',          'ACTIVE'),
  (UUID(), 'HCM', 'FPT University TP. Hồ Chí Minh','TP. Hồ Chí Minh', 'ACTIVE'),
  (UUID(), 'DN',  'FPT University Đà Nẵng',        'Đà Nẵng',         'ACTIVE'),
  (UUID(), 'CT',  'FPT University Cần Thơ',        'Cần Thơ',         'ACTIVE'),
  (UUID(), 'QN',  'FPT University Quy Nhơn',       'Quy Nhơn',        'ACTIVE');
