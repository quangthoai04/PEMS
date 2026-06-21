-- ==============================================================================
-- SQL Patch: Fix Visit Management Host & Status Alignment
-- 
-- Description:
-- 1. Ensures Staff Leader is never an official Host (removes them from IC_HOST).
-- 2. Fixes invalid AUTO_STAFF_LEADER rows that are in operational states (DURING_VISIT, etc.).
-- 3. Seeds VISITOR and HOST cancellation labels.
-- 
-- Rollback:
-- None required for seed data, re-run complete DB dump if needed.
-- 
-- Execution: Run manually on the database after reviewing.
-- ==============================================================================

-- 1. Find a valid normal Staff user to transfer invalid Staff Leader hosts to.
SET @ValidStaffId = (
    SELECT MIN(u.user_id) 
    FROM users u 
    JOIN roles r ON r.role_id = u.role_id 
    WHERE r.role_code = 'STAFF' AND u.sub_role = 'Staff' AND u.is_active = 1
);

SET @StaffLeaderId = (
    SELECT MIN(u.user_id) 
    FROM users u 
    JOIN roles r ON r.role_id = u.role_id 
    WHERE r.role_code = 'STAFF' AND u.sub_role = 'Leader' AND u.is_active = 1
);

-- 2. Fix SINGLE_CAMPUS where Staff Leader is wrongly assigned as official host
UPDATE visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
JOIN users u ON u.user_id = vrc.current_host_user_id
JOIN roles r ON r.role_id = u.role_id
SET 
    vrc.current_host_user_id = @ValidStaffId,
    vrc.host_assignment_source = 'MANUAL_APPROVAL',
    vrc.host_assigned_by = @StaffLeaderId
WHERE vr.visit_scope = 'SINGLE_CAMPUS'
  AND r.role_code = 'STAFF' AND u.sub_role = 'Leader';

-- 3. Fix MULTI_CAMPUS where AUTO_STAFF_LEADER reached operational status
-- They must be transferred to a normal staff.
UPDATE visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
SET 
    vrc.current_host_user_id = @ValidStaffId,
    vrc.host_assignment_source = 'TRANSFERRED',
    vrc.host_transferred_by = @StaffLeaderId,
    vrc.host_transferred_at = NOW()
WHERE vr.visit_scope = 'MULTI_CAMPUS'
  AND vrc.host_assignment_source = 'AUTO_STAFF_LEADER'
  AND vrc.status IN ('DURING_VISIT', 'AFTER_VISIT', 'CLOSED');

-- 4. Delete IC_HOST participant records where user is Staff Leader
DELETE vp 
FROM visit_participants vp
JOIN users u ON u.user_id = vp.user_id
JOIN roles r ON r.role_id = u.role_id
WHERE vp.participant_role = 'IC_HOST'
  AND r.role_code = 'STAFF' AND u.sub_role = 'Leader';

-- 5. Seed Cancellation Labels
-- Case 1: Visitor Cancellation
UPDATE visit_requests
SET 
    cancellation_actor_type = 'VISITOR',
    cancellation_source = 'SELF_SERVICE'
WHERE status = 'CANCELLED' 
LIMIT 1;

-- Case 2: Host Cancellation
UPDATE visit_request_campuses
SET 
    cancellation_actor_type = 'HOST',
    cancellation_source = 'HOST_ASSISTED'
WHERE status = 'CANCELLED'
LIMIT 1;

-- ==============================================================================
-- Diagnostics Check
-- ==============================================================================

-- Staff Leader official host invalid (Expected: 0)
SELECT COUNT(*) AS invalid_count
FROM visit_request_campuses vrc
JOIN users u ON u.user_id = vrc.current_host_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'STAFF'
  AND u.sub_role = 'Leader'
  AND (
    vrc.host_assignment_source <> 'AUTO_STAFF_LEADER'
    OR vrc.status IN ('DURING_VISIT', 'AFTER_VISIT', 'CLOSED')
  );

-- Pending host assignment invalid status (Expected: 0)
SELECT COUNT(*) AS invalid_count
FROM visit_request_campuses vrc
WHERE vrc.host_assignment_source = 'AUTO_STAFF_LEADER'
  AND vrc.status NOT IN ('ASSIGNED', 'BEFORE_VISIT', 'CANCELLED');

-- Staff Leader IC_HOST participant invalid (Expected: 0)
SELECT COUNT(*) AS invalid_count
FROM visit_participants vp
JOIN users u ON u.user_id = vp.user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'STAFF'
  AND u.sub_role = 'Leader'
  AND vp.participant_role = 'IC_HOST';
