-- =====================================================================
-- PEMS — Campuses seed
-- The 5 campuses (HN, HCM, DN, CT, QN)
-- =====================================================================
USE pems_db;

START TRANSACTION;

INSERT INTO campuses (campus_id, campus_code, name, city, status) VALUES
  (UUID(), 'HN',  'FPT University Hà Nội',         'Hà Nội',          'ACTIVE'),
  (UUID(), 'HCM', 'FPT University TP. Hồ Chí Minh','TP. Hồ Chí Minh', 'ACTIVE'),
  (UUID(), 'DN',  'FPT University Đà Nẵng',        'Đà Nẵng',         'ACTIVE'),
  (UUID(), 'CT',  'FPT University Cần Thơ',        'Cần Thơ',         'ACTIVE'),
  (UUID(), 'QN',  'FPT University Quy Nhơn',       'Quy Nhơn',        'ACTIVE')
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  city = VALUES(city),
  status = VALUES(status);

COMMIT;
