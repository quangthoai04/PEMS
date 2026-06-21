-- Fix seed script for Visit Requests and Roles based on PROMPT_FIX_VISIT_ROLE_UI_FILTERS_AND_SEED_LOGIC.md

-- 1. Pending or Rejected requests should not have a host
UPDATE visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
SET vrc.current_host_user_id = NULL,
    vrc.host_assignment_source = CASE WHEN vrc.current_host_user_id IS NOT NULL THEN 'TRANSFERRED' ELSE vrc.host_assignment_source END,
    vrc.host_transferred_by = CASE WHEN vrc.current_host_user_id IS NOT NULL THEN 100005 ELSE NULL END,
    vrc.host_transferred_at = CASE WHEN vrc.current_host_user_id IS NOT NULL THEN NOW() ELSE NULL END
WHERE vr.status IN ('PENDING_APPROVAL', 'REJECTED') OR vrc.status = 'WAITING_REQUEST_APPROVAL';

-- 2. Multi-campus approved instances wait for host -> temp assign to Staff Leader
-- To avoid trigger host transfer issues, if it's already assigned, we must mark as TRANSFERRED 
-- to satisfy the database constraint since we aren't allowed to disable it.
UPDATE visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
SET vrc.host_assignment_source = CASE 
        WHEN vrc.current_host_user_id IS NOT NULL AND vrc.current_host_user_id != (SELECT user_id FROM users WHERE role_id = (SELECT role_id FROM roles WHERE role_code = 'STAFF') AND UPPER(sub_role) = 'LEADER' AND primary_campus_id = vrc.campus_id LIMIT 1) THEN 'TRANSFERRED'
        ELSE 'AUTO_STAFF_LEADER' END,
    vrc.host_transferred_by = CASE 
        WHEN vrc.current_host_user_id IS NOT NULL AND vrc.current_host_user_id != (SELECT user_id FROM users WHERE role_id = (SELECT role_id FROM roles WHERE role_code = 'STAFF') AND UPPER(sub_role) = 'LEADER' AND primary_campus_id = vrc.campus_id LIMIT 1) THEN (SELECT user_id FROM users WHERE role_id = (SELECT role_id FROM roles WHERE role_code = 'STAFF') AND UPPER(sub_role) = 'LEADER' AND primary_campus_id = vrc.campus_id LIMIT 1)
        ELSE NULL END,
    vrc.host_transferred_at = CASE 
        WHEN vrc.current_host_user_id IS NOT NULL AND vrc.current_host_user_id != (SELECT user_id FROM users WHERE role_id = (SELECT role_id FROM roles WHERE role_code = 'STAFF') AND UPPER(sub_role) = 'LEADER' AND primary_campus_id = vrc.campus_id LIMIT 1) THEN NOW()
        ELSE NULL END,
    vrc.current_host_user_id = (SELECT user_id FROM users WHERE role_id = (SELECT role_id FROM roles WHERE role_code = 'STAFF') AND UPPER(sub_role) = 'LEADER' AND primary_campus_id = vrc.campus_id LIMIT 1)
WHERE vr.visit_scope = 'MULTI_CAMPUS' 
  AND vr.status = 'APPROVED'
  AND vrc.status IN ('ASSIGNED', 'BEFORE_VISIT', 'DURING_VISIT', 'AFTER_VISIT', 'CLOSED', 'CANCELLED');

-- 3. Fix AUTO_STAFF_LEADER incorrectly used in SINGLE_CAMPUS
UPDATE visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
SET vrc.host_assignment_source = 'MANUAL_APPROVAL',
    vrc.host_assigned_by = (SELECT user_id FROM users WHERE role_id = (SELECT role_id FROM roles WHERE role_code = 'STAFF') AND UPPER(sub_role) = 'LEADER' AND primary_campus_id = vrc.campus_id LIMIT 1)
WHERE vr.visit_scope = 'SINGLE_CAMPUS' 
  AND vrc.host_assignment_source = 'AUTO_STAFF_LEADER';

-- 4. Fix Staff Leader seed as IC_HOST
DELETE vp FROM visit_participants vp
JOIN users u ON u.user_id = vp.user_id
WHERE u.role_id = (SELECT role_id FROM roles WHERE role_code = 'STAFF')
  AND UPPER(u.sub_role) = 'LEADER'
  AND vp.participant_role = 'IC_HOST';

-- 5. Fix Staff.hn being a host when they are supposed to be IC_SUPPORT
-- Find another user in the same campus to be the host, e.g., staff.host.seed.hn@fpt.edu.vn if it exists
UPDATE visit_request_campuses vrc
JOIN visit_participants vp ON vp.visit_instance_id = vrc.visit_instance_id
SET vrc.current_host_user_id = (SELECT user_id FROM users WHERE email = 'staff.host.seed.hn@fpt.edu.vn'),
    vrc.host_assignment_source = 'TRANSFERRED',
    vrc.host_transferred_by = (SELECT user_id FROM users WHERE role_id = (SELECT role_id FROM roles WHERE role_code = 'STAFF') AND UPPER(sub_role) = 'LEADER' AND primary_campus_id = vrc.campus_id LIMIT 1),
    vrc.host_transferred_at = NOW()
WHERE vrc.current_host_user_id = vp.user_id
  AND vp.participant_role = 'IC_SUPPORT'
  AND EXISTS (SELECT 1 FROM users WHERE email = 'staff.host.seed.hn@fpt.edu.vn');

-- If 'staff.host.seed.hn@fpt.edu.vn' doesn't exist, we can create a dummy host or use another staff.
-- We will just ensure vp.is_host = 0 for IC_SUPPORT.
UPDATE visit_participants vp
SET is_host = 0
WHERE participant_role IN ('IC_SUPPORT', 'DEPT_SUPPORT', 'STUDENT');
