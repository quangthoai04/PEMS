-- =============================================================================
-- PEMS Seed — Public News + Visitor Feedback for CLOSED visit instances
-- Target visitor: visitor@example.com
--
-- Scope:
--   - No schema changes
--   - No migration
--   - No fake FK ids
--   - Uses current DB data only
--
-- You can run:
--   A) only News block
--   B) only Feedback block
--   C) both blocks
-- =============================================================================

START TRANSACTION;

-- =============================================================================
-- A. Seed PUBLISHED public news for CLOSED visit instances of visitor@example.com
--    Only inserts when that CLOSED visit instance has no PUBLISHED news yet.
-- =============================================================================

INSERT INTO news (
    campus_id,
    visit_instance_id,
    author_user_id,
    cover_file_id,
    status,
    submitted_at,
    reviewed_by,
    reviewed_at,
    review_note,
    published_at,
    is_featured,
    row_version,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    vrc.campus_id,
    vrc.visit_instance_id,
    vrc.current_host_user_id,
    NULL AS cover_file_id,
    'PUBLISHED' AS status,
    COALESCE(vrc.closed_at, CURRENT_TIMESTAMP) - INTERVAL 2 HOUR AS submitted_at,
    COALESCE(vrc.host_assigned_by, vrc.coordinator_user_id, vrc.current_host_user_id) AS reviewed_by,
    COALESCE(vrc.closed_at, CURRENT_TIMESTAMP) - INTERVAL 1 HOUR AS reviewed_at,
    'Seed public news for visitor closed delegation demo.' AS review_note,
    COALESCE(vrc.closed_at, CURRENT_TIMESTAMP) - INTERVAL 30 MINUTE AS published_at,
    FALSE AS is_featured,
    1 AS row_version,
    COALESCE(vrc.closed_at, CURRENT_TIMESTAMP) - INTERVAL 2 HOUR AS created_at,
    vrc.current_host_user_id AS created_by,
    COALESCE(vrc.closed_at, CURRENT_TIMESTAMP) - INTERVAL 30 MINUTE AS updated_at,
    COALESCE(vrc.host_assigned_by, vrc.coordinator_user_id, vrc.current_host_user_id) AS updated_by
FROM visit_request_campuses vrc
JOIN visit_requests vr
    ON vr.visit_request_id = vrc.visit_request_id
JOIN users visitor
    ON visitor.user_id = vr.visitor_user_id
WHERE visitor.email = 'visitor@example.com'
  AND vrc.status = 'CLOSED'
  AND vrc.current_host_user_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM news n0
      WHERE n0.visit_instance_id = vrc.visit_instance_id
        AND n0.status = 'PUBLISHED'
  );

-- Vietnamese translation for the seeded news.
INSERT INTO news_translations (
    news_id,
    language_code,
    title,
    slug,
    summary,
    seo_title,
    seo_description,
    created_at,
    updated_at
)
SELECT
    n.news_id,
    'vi' AS language_code,
    CONCAT('Bản tin chuyến thăm: ', vr.delegation_name) AS title,
    CONCAT('visitor-closed-visit-', vrc.visit_instance_id) AS slug,
    CONCAT(
        'Tổng kết chuyến thăm của ', vr.delegation_name,
        ' tại ', c.name,
        ', ghi nhận các hoạt động nổi bật và định hướng hợp tác sau buổi làm việc.'
    ) AS summary,
    CONCAT('Bản tin chuyến thăm ', vr.delegation_name) AS seo_title,
    CONCAT(
        'Bản tin public về chuyến thăm của ', vr.delegation_name,
        ' tại ', c.name, '.'
    ) AS seo_description,
    COALESCE(n.created_at, CURRENT_TIMESTAMP) AS created_at,
    COALESCE(n.updated_at, CURRENT_TIMESTAMP) AS updated_at
FROM news n
JOIN visit_request_campuses vrc
    ON vrc.visit_instance_id = n.visit_instance_id
JOIN visit_requests vr
    ON vr.visit_request_id = vrc.visit_request_id
JOIN users visitor
    ON visitor.user_id = vr.visitor_user_id
JOIN campuses c
    ON c.campus_id = vrc.campus_id
WHERE visitor.email = 'visitor@example.com'
  AND vrc.status = 'CLOSED'
  AND n.status = 'PUBLISHED'
  AND n.review_note = 'Seed public news for visitor closed delegation demo.'
  AND NOT EXISTS (
      SELECT 1
      FROM news_translations nt0
      WHERE nt0.news_id = n.news_id
        AND nt0.language_code = 'vi'
  );

-- Section 1: overview.
INSERT INTO news_content_sections (
    news_translation_id,
    section_order,
    section_title,
    section_body_html,
    section_body_text,
    created_at,
    updated_at
)
SELECT
    nt.news_translation_id,
    1 AS section_order,
    'Tổng quan chuyến thăm' AS section_title,
    CONCAT(
        '<p>Đoàn <strong>', vr.delegation_name,
        '</strong> đã hoàn tất chuyến thăm tại <strong>', c.name,
        '</strong>. Chương trình giúp hai bên trao đổi về môi trường học tập, hoạt động hợp tác quốc tế và các định hướng phối hợp trong thời gian tới.</p>'
    ) AS section_body_html,
    CONCAT(
        'Đoàn ', vr.delegation_name,
        ' đã hoàn tất chuyến thăm tại ', c.name,
        '. Chương trình giúp hai bên trao đổi về môi trường học tập, hoạt động hợp tác quốc tế và các định hướng phối hợp trong thời gian tới.'
    ) AS section_body_text,
    COALESCE(n.created_at, CURRENT_TIMESTAMP) AS created_at,
    COALESCE(n.updated_at, CURRENT_TIMESTAMP) AS updated_at
FROM news_translations nt
JOIN news n
    ON n.news_id = nt.news_id
JOIN visit_request_campuses vrc
    ON vrc.visit_instance_id = n.visit_instance_id
JOIN visit_requests vr
    ON vr.visit_request_id = vrc.visit_request_id
JOIN users visitor
    ON visitor.user_id = vr.visitor_user_id
JOIN campuses c
    ON c.campus_id = vrc.campus_id
WHERE visitor.email = 'visitor@example.com'
  AND nt.language_code = 'vi'
  AND nt.slug = CONCAT('visitor-closed-visit-', vrc.visit_instance_id)
  AND NOT EXISTS (
      SELECT 1
      FROM news_content_sections s0
      WHERE s0.news_translation_id = nt.news_translation_id
        AND s0.section_order = 1
  );

-- Section 2: highlights.
INSERT INTO news_content_sections (
    news_translation_id,
    section_order,
    section_title,
    section_body_html,
    section_body_text,
    created_at,
    updated_at
)
SELECT
    nt.news_translation_id,
    2 AS section_order,
    'Hoạt động nổi bật' AS section_title,
    CONCAT(
        '<p>Trong khuôn khổ chương trình, đoàn đã tham quan các khu vực trọng điểm, gặp gỡ đại diện nhà trường và trao đổi về các chủ đề phù hợp với mục tiêu chuyến thăm. Các nội dung follow-up sẽ tiếp tục được hai bên thống nhất sau buổi làm việc.</p>'
    ) AS section_body_html,
    'Trong khuôn khổ chương trình, đoàn đã tham quan các khu vực trọng điểm, gặp gỡ đại diện nhà trường và trao đổi về các chủ đề phù hợp với mục tiêu chuyến thăm. Các nội dung follow-up sẽ tiếp tục được hai bên thống nhất sau buổi làm việc.' AS section_body_text,
    COALESCE(n.created_at, CURRENT_TIMESTAMP) AS created_at,
    COALESCE(n.updated_at, CURRENT_TIMESTAMP) AS updated_at
FROM news_translations nt
JOIN news n
    ON n.news_id = nt.news_id
JOIN visit_request_campuses vrc
    ON vrc.visit_instance_id = n.visit_instance_id
JOIN visit_requests vr
    ON vr.visit_request_id = vrc.visit_request_id
JOIN users visitor
    ON visitor.user_id = vr.visitor_user_id
WHERE visitor.email = 'visitor@example.com'
  AND nt.language_code = 'vi'
  AND nt.slug = CONCAT('visitor-closed-visit-', vrc.visit_instance_id)
  AND NOT EXISTS (
      SELECT 1
      FROM news_content_sections s0
      WHERE s0.news_translation_id = nt.news_translation_id
        AND s0.section_order = 2
  );

-- =============================================================================
-- B. Seed Visitor feedback for CLOSED visit instances of visitor@example.com
--    Only inserts when the Visitor has not already submitted feedback for that host
--    on that visit instance.
-- =============================================================================

INSERT INTO feedbacks (
    visit_request_id,
    visit_instance_id,
    submitted_by_user_id,
    submitter_role,
    submitter_context,
    submitter_name_snapshot,
    target_user_id,
    target_role,
    target_context,
    target_name_snapshot,
    rating,
    comment,
    submitted_at
)
SELECT
    vr.visit_request_id,
    vrc.visit_instance_id,
    visitor.user_id AS submitted_by_user_id,
    'VISITOR' AS submitter_role,
    'Đại diện đoàn khách' AS submitter_context,
    visitor.full_name AS submitter_name_snapshot,
    host.user_id AS target_user_id,
    'HOST' AS target_role,
    'Host chính chuyến thăm' AS target_context,
    host.full_name AS target_name_snapshot,
    5 AS rating,
    CONCAT(
        'Cảm ơn ', host.full_name,
        ' đã hỗ trợ đoàn trong suốt chuyến thăm. Lịch trình được chuẩn bị rõ ràng, thông tin trao đổi hữu ích và quá trình tiếp đón diễn ra chuyên nghiệp.'
    ) AS comment,
    COALESCE(vrc.closed_at, CURRENT_TIMESTAMP) + INTERVAL 30 MINUTE AS submitted_at
FROM visit_request_campuses vrc
JOIN visit_requests vr
    ON vr.visit_request_id = vrc.visit_request_id
JOIN users visitor
    ON visitor.user_id = vr.visitor_user_id
JOIN users host
    ON host.user_id = vrc.current_host_user_id
WHERE visitor.email = 'visitor@example.com'
  AND vrc.status = 'CLOSED'
  AND NOT EXISTS (
      SELECT 1
      FROM feedbacks f0
      WHERE f0.visit_instance_id = vrc.visit_instance_id
        AND f0.submitted_by_user_id = visitor.user_id
        AND f0.submitter_role = 'VISITOR'
        AND f0.target_user_id = host.user_id
        AND f0.target_role = 'HOST'
  );

-- Criteria breakdown for the seeded Visitor feedback.
INSERT INTO feedback_rating_items (
    feedback_id,
    criterion_code,
    criterion_label,
    rating,
    display_order,
    created_at
)
SELECT
    f.feedback_id,
    criteria.criterion_code,
    criteria.criterion_label,
    criteria.rating,
    criteria.display_order,
    f.submitted_at AS created_at
FROM feedbacks f
JOIN visit_request_campuses vrc
    ON vrc.visit_instance_id = f.visit_instance_id
JOIN visit_requests vr
    ON vr.visit_request_id = vrc.visit_request_id
JOIN users visitor
    ON visitor.user_id = vr.visitor_user_id
JOIN (
    SELECT 'HOST_PREPARATION' AS criterion_code, 'Chuẩn bị và điều phối' AS criterion_label, 5 AS rating, 1 AS display_order
    UNION ALL
    SELECT 'COMMUNICATION', 'Trao đổi và hỗ trợ thông tin', 5, 2
    UNION ALL
    SELECT 'AGENDA_QUALITY', 'Chất lượng lịch trình', 5, 3
    UNION ALL
    SELECT 'OVERALL_EXPERIENCE', 'Trải nghiệm tổng thể', 5, 4
) criteria
WHERE visitor.email = 'visitor@example.com'
  AND vrc.status = 'CLOSED'
  AND f.submitted_by_user_id = visitor.user_id
  AND f.submitter_role = 'VISITOR'
  AND f.target_role = 'HOST'
  AND NOT EXISTS (
      SELECT 1
      FROM feedback_rating_items fri0
      WHERE fri0.feedback_id = f.feedback_id
        AND fri0.criterion_code = criteria.criterion_code
  );

COMMIT;

-- =============================================================================
-- Verify
-- =============================================================================

SELECT
    vrc.visit_instance_id,
    vr.request_code,
    vr.delegation_name,
    vrc.status AS campus_status,
    COUNT(DISTINCT n.news_id) AS published_news_count,
    COUNT(DISTINCT f.feedback_id) AS visitor_feedback_count
FROM visit_request_campuses vrc
JOIN visit_requests vr
    ON vr.visit_request_id = vrc.visit_request_id
JOIN users visitor
    ON visitor.user_id = vr.visitor_user_id
LEFT JOIN news n
    ON n.visit_instance_id = vrc.visit_instance_id
   AND n.status = 'PUBLISHED'
LEFT JOIN feedbacks f
    ON f.visit_instance_id = vrc.visit_instance_id
   AND f.submitted_by_user_id = visitor.user_id
   AND f.submitter_role = 'VISITOR'
WHERE visitor.email = 'visitor@example.com'
  AND vrc.status = 'CLOSED'
GROUP BY
    vrc.visit_instance_id,
    vr.request_code,
    vr.delegation_name,
    vrc.status
ORDER BY vrc.visit_instance_id;
