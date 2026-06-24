-- Patch: enforce exactly ONE meeting-minutes record per campus instance (UC biên bản, Phase 3).
-- The `minutes` table originally had only a non-unique KEY idx_minutes_visit_status
-- (visit_instance_id, status); this adds the missing 1:1 guarantee at the DB level.
--
-- Run once against the dev database. If duplicate rows already exist they must be merged/removed
-- first (the SELECT below lists offenders), otherwise the ALTER fails.

-- 1) Find any instances that already have more than one minutes row (should be empty):
SELECT visit_instance_id, COUNT(*) AS n
FROM minutes
GROUP BY visit_instance_id
HAVING n > 1;

-- 2) Add the unique key (run after confirming step 1 returns no rows):
ALTER TABLE minutes
  ADD UNIQUE KEY uq_minutes_visit_instance (visit_instance_id);
