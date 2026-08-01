-- Adds the VISIT_AGENDA_PROPOSAL system email template: Host sends the drafted agenda to the
-- campus's operational contact so both sides can discuss/confirm it. Reply-To/Cc are the Host,
-- resolved server-side (SendVisitAgendaEmailCommand) — not part of this template's content.
INSERT INTO email_templates
  (template_code, name, purpose, campus_id, description,
   status, subject_vi, body_vi, subject_en, body_en, body_format, variables_text,
   created_at, created_by, updated_at, updated_by)
VALUES
  ('VISIT_AGENDA_PROPOSAL', 'Đề xuất lịch trình đón tiếp', 'VISIT_REQUEST', NULL,
   'Gửi lịch trình dự kiến tới đầu mối liên hệ tại cơ sở để hai bên trao đổi và chốt lịch. Reply-To và Cc là Host phụ trách tiếp đón.',
   'ACTIVE',
   '[PEMS] Đề xuất lịch trình đón tiếp — {{delegationName}}',
   '<p>Xin chào <strong>{{contactFullName}}</strong>,</p><p>PEMS - FPT University xin gửi đến bạn lịch trình dự kiến cho chuyến thăm của đoàn <strong>{{delegationName}}</strong> tại <strong>{{campusName}}</strong>, dự kiến diễn ra trong khoảng thời gian <strong>{{plannedTime}}</strong>.</p>{{agendaBlock}}<p>Nếu bạn muốn trao đổi, điều chỉnh hoặc bổ sung nội dung nào trong lịch trình trên, vui lòng phản hồi trực tiếp email này, hoặc liên hệ người phụ trách tiếp đón đoàn: <strong>{{hostName}}</strong> ({{hostEmail}}).</p><p style="color:#6b7280;font-size:12px">Trân trọng,<br/>PEMS - FPT University</p>',
   '[PEMS] Proposed visit agenda — {{delegationName}}',
   '<p>Hello <strong>{{contactFullName}}</strong>,</p><p>PEMS - FPT University is sharing the proposed agenda for the visit of <strong>{{delegationName}}</strong> at <strong>{{campusName}}</strong>, planned for <strong>{{plannedTime}}</strong>.</p>{{agendaBlock}}<p>If you would like to discuss, change or add anything to the agenda above, please reply directly to this email, or contact the person hosting your delegation: <strong>{{hostName}}</strong> ({{hostEmail}}).</p><p style="color:#6b7280;font-size:12px">Best regards,<br/>PEMS - FPT University</p>',
   'HTML', 'contactFullName,delegationName,campusName,plannedTime,hostName,hostEmail',
   CURRENT_TIMESTAMP, NULL, NULL, NULL);
