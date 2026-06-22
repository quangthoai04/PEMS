# PEMS SQL v8.4 Schema Impact Map

## 1. Tables Added
- `minute_participants`
- `feedback_rating_items`
- `sent_email_recipients`
- `calendar_event_attendees`
- `calendar_event_reminders`
- `api_configuration_headers`
- `agenda_template_items`
- `audit_log_changes`

## 2. Tables Removed or Not Used Anymore
- N/A (None removed in this patch, though temporary visit request tables were removed in previous patches)

## 3. Existing Tables with New Columns
- `files`: `external_file_id`, `web_view_url`, `download_url`, `thumbnail_url`, `file_purpose`
- `partners`: `logo_file_id`, `cover_file_id`, `address`, `public_slug`, `profile_status`, `review_note`, `reviewed_by`, `reviewed_at`, `visibility`
- `partner_contacts`: `source_type`, `scanned_card_file_id`, `ocr_confidence`
- `visit_requests`: `created_source`, `visit_type`, `visit_type_other`, `contact_person_full_name`, `contact_person_organization`, `contact_person_phone`, `contact_person_email`, `transportation_type`, `transportation_detail`, `media_consent_status`, `media_consent_note`
- `visit_guest_members`: `member_type`, `display_order`
- `visit_logistics_items`: `handover_confirmed_by`, `handover_confirmed_at`, `handover_note`, `service_report_signed_by`, `service_report_signed_at`, `service_report_file_id`
- `faqs`: `faq_type`, `language_code`
- `galleries`: `area_name`, `specific_location_name`, `location_description`, `hero_file_id`, `virtual_tour_url`
- `gallery_images`: `media_type`, `thumbnail_file_id`
- `email_templates`: `campus_id`, `description`, `subject_vi`, `body_vi`, `subject_en`, `body_en`, `variables_text`
- `sent_emails`: `provider_thread_id`, `provider_message_id`, `retry_count`, `last_attempt_at`, `delivered_at`
- `calendar_events`: `is_all_day`, `recurrence_rule`
- `api_configurations`: `api_key_encrypted`, `bearer_token_encrypted`, `basic_username`, `basic_password_encrypted`, `oauth_client_id`, `oauth_client_secret_encrypted`, `oauth_token_url`, `oauth_scope`, `body_template_text`, `rate_limit_per_minute`, `monthly_quota`, `retry_enabled`, `max_retries`, `cache_ttl_seconds`, `last_test_status`, `last_tested_at`, `last_test_message`

## 4. Existing Tables with Changed Enum Values
- `visit_request_campuses`: modified `cancellation_source` to include `INTERNAL_DECISION`

## 5. JSON Columns Removed
- `visit_requests`: `support_team_json`, `contact_person_json`
- `minutes`: `participants_json`
- `faqs`: `category` (not JSON but dropped)
- `galleries`: `location_name` (not JSON but dropped)
- `email_templates`: `translations_json`, `variables_json`
- `sent_emails`: `recipients_json`, `metadata_json`
- `calendar_events`: `attendees_json`, `reminders_json`
- `api_configurations`: `credentials_json`, `headers_json`, `body_template_json`, `settings_json`
- `agenda_templates`: `items_json`
- `audit_logs`: `old_values_json`, `new_values_json`

## 6. Backend Domain Entities Affected
- PEMS.Domain.Entities.Documents.UploadedFile
- PEMS.Domain.Entities.Partners.Partner
- PEMS.Domain.Entities.Partners.PartnerContact
- PEMS.Domain.Entities.Delegations.VisitRequest
- PEMS.Domain.Entities.Delegations.VisitGuestMember
- PEMS.Domain.Entities.Delegations.VisitLogisticsItem
- PEMS.Domain.Entities.Minutes.Minute (and add MinuteParticipant)
- PEMS.Domain.Entities.Feedbacks.Feedback (and add FeedbackRatingItem)
- PEMS.Domain.Entities.Faqs.Faq
- PEMS.Domain.Entities.Galleries.Gallery
- PEMS.Domain.Entities.Galleries.GalleryImage
- PEMS.Domain.Entities.Emails.EmailTemplate
- PEMS.Domain.Entities.Emails.SentEmail (and add SentEmailRecipient)
- PEMS.Domain.Entities.Calendar.CalendarEvent (and add CalendarEventAttendee, CalendarEventReminder)
- PEMS.Domain.Entities.ApiIntegrations.ApiConfiguration (and add ApiConfigurationHeader)
- PEMS.Domain.Entities.AgendaTemplates.AgendaTemplate (and add AgendaTemplateItem)
- PEMS.Domain.Entities.Users.AuditLog (and add AuditLogChange)

## 7. EF Core DbContext/Configuration Affected
- `ApplicationDbContext.cs` needs DbSets for the 8 new tables.
- JSON mapping needs to be removed.
- Configurations need mapping for new child collections and columns.

## 8. Application Commands/Queries Affected
- Handlers dealing with: Visit Request Submit, Delegation List/Detail/Process, Email (Templates, Send), Gallery (Locations, Images), FAQ (Category mapping), Calendar (Attendees, Reminders), API Config (Credentials masking, Test result saving), Agenda Templates (Items array), Audit.

## 9. Frontend Types/Adapters/Pages Affected
- Types and validation schemas for all modified entities (VisitRequest form, Contact Person, Transportation, Media Consent).
- FAQ list and form (faq_type).
- Gallery form (area_name, specific_location_name, media_type).
- Email templates and Send form.
- API Config form.

## 10. Build/Test Risks
- High risk of compilation errors due to widespread DTO and Entity changes.
- Ensure backend compiles strictly after step 8.
