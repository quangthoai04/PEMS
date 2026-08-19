/**
 * i18n gate — Phase D3/D4/D5 (Guest/Visitor hardcode + raw-enum + backend-message-leak scan).
 *
 * Scope is INTENTIONALLY limited to the curated file list below — every page/component confirmed
 * Guest- or VISITOR-reachable across the full i18n implementation (see
 * `docs/CanhIter3FixBug/PEMS_GUEST_VISITOR_FULL_I18N_IMPLEMENTATION_PLAN.md` and memory
 * `visitor-i18n-coverage-gap`). Scanning the whole repo would bury real findings under hundreds of
 * legitimate Staff/HO/Department-only hardcoded-Vietnamese lines, which is explicitly out of scope.
 *
 * Files with heavy Staff/Host-only content are deliberately EXCLUDED from this automated scan — a
 * naive whole-file Vietnamese-literal check on them would be dominated by false positives from
 * legitimate out-of-scope code:
 *   - `VisitProcess.tsx`, `VisitRequestManagement.tsx` — mostly Staff/Host tooling with a few
 *     genuinely Visitor-reachable sections; those sections were hand-audited instead (see memory).
 *   - `VisitContributionPage.tsx`, `VisitProcessSummaryPage.tsx` — the route guard technically
 *     allows BUSINESS_ROLES, but access is actually decided by the backend to Host / an
 *     ACCEPTED-ASSIGNED internal participant / a Department user with a logistics relation (see
 *     each file's own top-of-file doc comment) — structurally never VISITOR. Only their entry
 *     shell (loading/access-denied/breadcrumb) is Visitor-reachable and was translated; the deep
 *     report content past that shell is legitimately Vietnamese-only and out of scope, so scanning
 *     these two files whole would misreport it as a gap.
 *
 * This is a heuristic regression gate, not a full static analyzer: it flags any line containing a
 * Vietnamese-diacritic character that is not a comment and not inside a translation call, then
 * requires every such line to be explicitly allow-listed with a reason. A NEW unexplained
 * hardcoded-Vietnamese line added later fails the test until triaged (fixed or allow-listed).
 */
import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const SRC_ROOT = path.resolve(__dirname, '../../');

/** Guest/Visitor-reachable files, relative to src/. Exported so
 * `scopedFileListFreshness.test.ts` (Phase 20 gate) can verify this list hasn't drifted from
 * what App.tsx's route graph actually reaches — see that file for how "drift" is detected. */
export const SCOPED_FILES = [
  'pages/account/ConfirmEmailPage.tsx',
  'pages/identity/VisitContactInvitationPage.tsx',
  'features/visit-request/components/OtpVerificationModal.tsx',
  'features/visit-request/components/v2/CampusVisitCard.tsx',
  'components/layout/DashboardLayout.tsx',
  'components/dashboard/Sidebar.tsx',
  'components/layout/Header.tsx',
  'pages/dashboard/profile/Profile.tsx',
  'pages/dashboard/visit/VisitorVisitDetailPage.tsx',
  'features/feedbacks/components/VisitFeedbackModal.tsx',
  'pages/dashboard/visit/VisitFeedbackPage.tsx',
  'features/feedbacks/components/FeedbackGroupSection.tsx',
  'features/feedbacks/components/FeedbackTargetRow.tsx',
  'features/feedbacks/components/CompactStarRating.tsx',
  'features/feedbacks/hooks/useVisitFeedback.ts',
  'pages/dashboard/visit/VisitRequestDetail.tsx',
  'pages/dashboard/visit/CreateVisitRequestEntry.tsx',
  'pages/dashboard/visit/MyContactInvitationsPage.tsx',
  'pages/dashboard/visit/VisitRequestV2DetailPage.tsx',
  'features/visit-request/components/v2/VisitRequestV2DetailView.tsx',
  'pages/dashboard/visit/EditVisitRequestV2Page.tsx',
  'pages/dashboard/visit/EditPendingCampusV2Page.tsx',
  'pages/PartnersPage.tsx',
  'pages/PartnerDetailPage.tsx',
  'pages/CampusDetailVisitPage.tsx',
  'pages/NewsPage.tsx',
  'pages/NewsDetailPage.tsx',
  'features/notifications/components/NotificationBellButton.tsx',
  'features/notifications/components/NotificationDetailModal.tsx',
  'features/notifications/components/NotificationFilterBar.tsx',
  'features/notifications/context/NotificationsContext.tsx',
  'features/notifications/utils/resolveNotificationPresentation.ts',
  'pages/notifications/NotificationsPage.tsx',

  // Discovered by scopedFileListFreshness.test.ts (Phase 20 gate): the rest of the closure
  // reachable from Guest routes + Visitor-allowed dashboard routes. Home/marketing sections, auth
  // pages, legal pages, and the full visit-request-v2 form/shared-component tree were confirmed
  // already fully i18n'd (no un-wrapped Vietnamese found) before being added here.
  'components/home/AboutFptuSection.tsx',
  'components/home/CampusShowcaseSection.tsx',
  'components/home/FaqPreviewSection.tsx',
  'components/home/FinalCtaSection.tsx',
  'components/home/GalleryPreviewSection.tsx',
  'components/home/HeroSection.tsx',
  'components/home/LazyGlobeShowcase.tsx',
  'components/home/NewsSection.tsx',
  'components/home/PartnersSection.tsx',
  'components/home/VisitProcessSection.tsx',
  'components/home/internal/GuideStepsSection.tsx',
  'components/home/internal/InternalFinalCta.tsx',
  'components/home/internal/QuickAccessSection.tsx',
  'components/home/internal/WelcomeHero.tsx',
  'components/layout/LegalPageLayout.tsx',
  'features/authentication/api/authenticationApi.ts',
  'features/authentication/types/authentication.types.ts',
  'features/delegations/api/delegationsApi.ts',
  'features/department-reception-tasks/api/departmentReceptionTasksApi.ts',
  'features/emails/utils/emailEditorCapabilities.ts',
  'features/emails/utils/emailEditorFormats.ts',
  'features/emails/utils/emailEditorTable.ts',
  'features/emails/utils/emailEditorTemplateBlocks.ts',
  'features/emails/utils/emailEditorVariableChips.ts',
  'features/emails/utils/emailHtmlCanonicalizer.ts',
  'features/emails/utils/emailScopeKey.ts',
  'features/emails/utils/inlineImages.ts',
  'features/emails/utils/systemActionNode.ts',
  'features/feedbacks/api/visitFeedbackApi.ts',
  'features/feedbacks/types/visitFeedback.types.ts',
  'features/notifications/api/notificationsApi.ts',
  'features/notifications/constants/notificationFilters.ts',
  'features/notifications/types/notification.types.ts',
  'features/notifications/utils/calendarChangeNotifs.ts',
  'features/profile/hooks/useProfile.ts',
  'features/public-content/api/publicContentApi.ts',
  'features/public-content/types/publicContent.types.ts',
  'features/public-faq/api/publicFaqApi.ts',
  'features/public-faq/types/publicFaq.types.ts',
  'features/public-partners/api/publicPartnersApi.ts',
  'features/public-partners/hooks/usePublicPartnerImage.ts',
  'features/public-partners/types/publicPartners.types.ts',
  'features/public-partners/utils/countryFlag.ts',
  'features/visit-fptu/publicVisitFptu.types.ts',
  'features/visit-fptu/publicVisitFptuApi.ts',
  'features/visit-request/api/featureApi.ts',
  'features/visit-request/api/visitRequestApi.ts',
  'features/visit-request/api/visitRequestV2Api.ts',
  'features/visit-request/components/ContactIdentityActions.tsx',
  'features/visit-request/components/ContactProfileSyncPrompt.tsx',
  'features/visit-request/components/ExcelUpload/ExcelImportPanel.tsx',
  'features/visit-request/components/ExcelUpload/excelDownload.ts',
  'features/visit-request/components/InstanceResubmitPanel.tsx',
  'features/visit-request/components/TurnstileWidget.tsx',
  'features/visit-request/components/VisitAmendmentSubmitModal.tsx',
  'features/visit-request/components/VisitHistoryDetailDrawer.tsx',
  'features/visit-request/components/VisitHistoryTimeline.tsx',
  'features/visit-request/components/VisitHostTransferModal.tsx',
  'features/visit-request/components/VisitSafeEditModal.tsx',
  'features/visit-request/components/shared/AutoGrowTextField.tsx',
  'features/visit-request/components/shared/AutoGrowTextarea.tsx',
  'features/visit-request/components/shared/FormField.tsx',
  'features/visit-request/components/shared/FormSection.tsx',
  'features/visit-request/components/shared/OrganizationCombobox.tsx',
  'features/visit-request/components/shared/PartnerOrgCombobox.tsx',
  'features/visit-request/components/shared/PhoneField.tsx',
  'features/visit-request/components/shared/TimeSelect.tsx',
  'features/visit-request/components/shared/VisitDateTimeRangePicker.tsx',
  'features/visit-request/components/shared/characterCount.ts',
  'features/visit-request/components/shared/visitDateTime.ts',
  'features/visit-request/components/v2/AssignHostPicker.tsx',
  'features/visit-request/components/v2/CampusHostSelectionV2Panel.tsx',
  'features/visit-request/components/v2/CampusVisitDetailCard.tsx',
  'features/visit-request/components/v2/ContactLinkPromptDialog.tsx',
  'features/visit-request/components/v2/OperationalContactReadOnly.tsx',
  'features/visit-request/components/v2/ReceptionHostReadOnly.tsx',
  'features/visit-request/components/v2/VisitCreateUncertainPanel.tsx',
  'features/visit-request/components/v2/VisitRequestFormV2.tsx',
  'features/visit-request/components/v2/VisitRequestV2Modal.tsx',
  'features/visit-request/components/v2/VisitRequestV2SubmittedSummary.tsx',
  'features/visit-request/components/v2/VisitRequestV2SuccessPanel.tsx',
  'features/visit-request/components/v2/shared/PersonListTable.tsx',
  'features/visit-request/components/v2/shared/ReadOnlyInfoGrid.tsx',
  'features/visit-request/components/v2/shared/VisitActionButton.tsx',
  'features/visit-request/components/v2/shared/VisitOutcomeSummary.tsx',
  'features/visit-request/components/v2/shared/VisitSectionCard.tsx',
  'features/visit-request/components/v2/shared/VisitStatusBadge.tsx',
  'features/visit-request/components/v2/shared/campusRevisionState.ts',
  'features/visit-request/components/v2/shared/visitStatus.ts',
  'features/visit-request/hooks/useContactLinkPrompt.ts',
  'features/visit-request/hooks/useRegistrationCampuses.ts',
  'features/visit-request/hooks/useVisitRequestFormV2.ts',
  'features/visit-request/schema/visitRequestV2.schema.ts',
  'features/visit-request/types/visitRequest.types.ts',
  'features/visit-request/utils/formErrorNavigation.ts',
  'features/visit-request/utils/memberDuplicates.ts',
  'features/visit-request/utils/safeEditDiff.ts',
  'features/visit-request/utils/visitRequestV2DraftStorage.ts',
  'features/visit-request/utils/visitRequestV2Form.ts',
  'features/visit-request/utils/visitV2Actions.ts',
  'pages/FAQPage.tsx',
  'pages/ForbiddenPage.tsx',
  'pages/HomePage.tsx',
  'pages/InternalHomePage.tsx',
  'pages/InvalidAccountPage.tsx',
  'pages/PublicHomePage.tsx',
  'pages/VisitFPTUPage.tsx',
  'pages/auth/ChangePasswordPage.tsx',
  'pages/auth/ForgotPasswordPage.tsx',
  'pages/auth/ResetPasswordPage.tsx',
  'pages/legal/PrivacyPolicyPage.tsx',
  'pages/legal/TermsOfServicePage.tsx',
  'pages/visit/VisitRequestV2Page.tsx',
  'services/visit-expense.service.ts',
  'shared/api/authInterceptor.ts',
  'shared/api/endpoints.ts',
  'shared/api/filesApi.ts',
  'shared/api/normalizeApiError.ts',
  'shared/auth/AuthContext.tsx',
  'shared/auth/authStorage.ts',
  'shared/auth/dashboardRoute.ts',
  'shared/auth/permissionChecker.ts',
  'shared/auth/resolveEffectiveRole.ts',
  'shared/auth/resolveHomeRoleBucket.ts',
  'shared/components/files/FileAttachmentItem.tsx',
  'shared/components/files/FilePreviewModal.tsx',
  'shared/components/files/filePreviewKind.ts',
  'shared/components/state/EmptyState.tsx',
  'shared/components/state/ErrorState.tsx',
  'shared/components/state/LoadingState.tsx',
  'shared/components/state/StaleDataBanner.tsx',
  'shared/components/state/index.ts',
  'shared/constants/countryCoordinatesFull.ts',
  'shared/features/VisitEntrySurfaces.tsx',
  'shared/features/perCampusV2Capability.tsx',
  'shared/features/perCampusV2Entry.ts',
  'shared/hooks/useAuth.ts',
  'shared/hooks/useAuthenticatedImage.ts',
  'shared/hooks/useCountryTranslation.ts',
  'shared/i18n/config.ts',
  'shared/i18n/localizedDbText.ts',
  'shared/security/sanitizeHtml.ts',
  'shared/utils/emailIdentity.ts',
  'shared/utils/fileDownload.ts',
  'shared/utils/fileUtils.ts',
  'shared/utils/formRevalidate.ts',
  'shared/utils/galleryShare.ts',
  'shared/utils/nameInitials.ts',
  'shared/utils/passwordPolicy.ts',
  'shared/utils/phoneNumber.ts',
  'shared/utils/resolveFileUrl.ts',
  'shared/utils/youtube.ts',
  'components/home/GlobeShowcase.tsx',
  'features/authentication/api/authError.ts',
  'features/profile/api/profileApi.ts',
  'features/profile/types/profile.types.ts',
  'features/public-partners/utils/countryMatch.ts',
  'features/visit-request/components/ExcelUpload/excelValidator.ts',
  'features/visit-request/components/VisitAmendmentPanel.tsx',
  'features/visit-request/components/shared/CountrySelect.tsx',
  'features/visit-request/components/shared/HelpTooltip.tsx',
  'shared/api/httpClient.ts',
  'shared/constants/countryCoordinates.ts',
  'shared/features/useVisitEntryCta.tsx',
  'shared/utils/countryNames.ts',
  'shared/utils/toast.ts',
  'shared/utils/vietnamTime.ts',
];

/**
 * Files that a static, route-guard-only reachability scan (see
 * `scopedFileListFreshness.test.ts`, Phase 20) WOULD flag as Visitor-reachable — the React Router
 * guard (`allowedRoles`/`BUSINESS_ROLES`/`ALL_ROLES`) technically admits VISITOR — but that are
 * proven, with concrete code evidence, to never actually render for a Visitor. Exported so the
 * freshness gate can treat "in SCOPED_FILES OR in here" as fully triaged; anything reachable by
 * the route graph that is in NEITHER list is a real gap the freshness gate must fail on.
 */
export const ACKNOWLEDGED_ROUTE_GUARD_EXCLUSIONS: Record<string, string> = {
  'pages/dashboard/visit/VisitProcess.tsx':
    'Mostly Staff/Host reception tooling; the few genuinely Visitor-reachable sections were hand-audited (see memory visitor-i18n-coverage-gap) instead of scanning the whole 2000+-line file.',
  'pages/dashboard/visit/VisitRequestManagement.tsx':
    'Mostly Staff/Host list/management tooling with its own extensive pre-existing i18n architecture (the `tt()` VISITOR-only-bilingual pattern originates here); hand-audited, not whole-file-scanned.',
  'pages/dashboard/visit/VisitContributionPage.tsx':
    'Own doc comment: "Access is decided by the backend ... Host, an ACCEPTED/ASSIGNED participant, or a Department user with a real logistics relation" — structurally never VISITOR despite the shared VISIT_PROCESS route-guard key. Only the entry shell (in SCOPED_FILES-covered files) is Visitor-reachable.',
  'pages/dashboard/visit/VisitProcessSummaryPage.tsx':
    'Same backend-decided access model as VisitContributionPage.tsx (see its own doc comment) — never VISITOR in practice.',
  'pages/dashboard/visit/HoVisitProcessDetail.tsx':
    'Own doc comment: "Theo dõi quá trình vận hành tiếp đón và điểm danh của người quản trị HO" (tracks reception/attendance FOR THE HO ADMINISTRATOR) — HO-only by design despite sharing the VISIT_PROCESS route-guard key.',
  'pages/dashboard/visit/VisitParticipantInvitationDetail.tsx':
    'Confirmed N/A for Visitor: every role check in the file is STAFF/DEPARTMENT/STAFF_LEADER; "participant" here means internal reception-team staff, not the guest being received (see memory visitor-i18n-coverage-gap).',
  'pages/dashboard/home/DashboardHome.tsx':
    'App.tsx renders this ONLY for roles other than VISITOR/STUDENT at the /dashboard index route — `effectiveRole === \'VISITOR\' ? <Navigate to="/dashboard/visit" replace /> : <DashboardHome />` — Visitor is redirected before this component ever mounts.',
  'pages/dashboard/visit/DeptLeadVisitTasksPage.tsx':
    'App.tsx VISIT_LIST route body: `effectiveRole === \'DEPARTMENT_LEAD\' ? <DeptLeadVisitTasksPage /> : <VisitRequestManagement />` — only Department Lead ever gets this branch; Visitor always renders VisitRequestManagement (already in SCOPED_FILES). Its own subtree (SharedDashboardView, TaskHandoverModal, LogisticsExpensePanel, ConfirmModal, UnitPriceInput) is Dept-Lead-only by the same evidence.',
  'shared/auth/dashboardRouteAccess.ts':
    'Pure routing-policy config, not a rendering component. Its `sidebarLabel` strings are the ones Sidebar.tsx documents as "i18n-free — Sidebar hiện dùng chuỗi tiếng Việt cố định" for every routeKey EXCEPT VISIT_LIST, which Sidebar.tsx special-cases with its own tt() call (`item.key === \'VISIT_LIST\' ? tt(...) : item.sidebarLabel` — Sidebar.tsx:233) precisely because VISIT_LIST is the only sidebar entry Visitor ever sees (every other allowedRoles/showInSidebar routeKey here is Staff/HO/Dept/Admin-only).',
  'features/feedbacks/components/HostFeedbackModal.tsx':
    'Structurally reachable (imported by NotificationsPage.tsx/NotificationBellButton.tsx, both SCOPED_FILES) but only OPENS on notificationType actionType===OPEN_HOST_FEEDBACK_MODAL — confirmed this session via the full notification-producer inventory (CompleteVisitStageCommandHandler host-feedback-invite, SubmitVisitFeedbackCommandHandler G5) that this action type is only ever sent to a Host/internal recipient, never Visitor. The modal renders but a Visitor can never trigger it open.',
  'features/feedbacks/components/VisitorFeedbackDetailModal.tsx':
    'Same reachable-but-never-opens-for-Visitor evidence as HostFeedbackModal.tsx — actionType===OPEN_VISITOR_FEEDBACK_MODAL is sent by SubmitVisitFeedbackCommandHandler G4 to the Host/Coordinator (to view what the Visitor submitted), never to the Visitor themselves.',
  'features/feedbacks/utils/visitInstanceStatusLabel.ts':
    'Shared helper used only by HostFeedbackModal.tsx and VisitorFeedbackDetailModal.tsx (both excluded above for the same reason) to label the visit instance status inside those never-opens-for-Visitor modals.',
  'features/delegations/types/delegations.types.ts':
    'Large shared types+constants module (REQUEST_STATUS_LABELS/INSTANCE_STATUS_LABELS/etc.) consumed broadly across Staff/HO/Dept screens. Its VI-only label constants are a legitimate default for every consumer NOT explicitly gated for Visitor+EN (matching VisitRequestManagement.tsx\'s established dual-path pattern) — genuinely Visitor-reachable consumers (VisitRequestDetail.tsx, VisitRequestManagement.tsx) were individually hand-audited, but this shared definitions file itself was not exhaustively re-checked against every present and future consumer; flagged here rather than silently assumed safe.',
};

const VIETNAMESE_DIACRITIC = /[À-ỹ]/; // covers Latin Extended-A/B + Vietnamese combining ranges used by all VI text in this repo

/** file -> set of 1-indexed line numbers known to hold legitimate Vietnamese content (proper
 * nouns, business codes, or comments already excluded by the comment check) with the reason why
 * they are not a translation gap. Add an entry here ONLY after confirming the line is genuinely
 * out of scope — never to silence a real miss. */
const ALLOWLIST: Record<string, Record<number, string>> = {
  'components/home/GlobeShowcase.tsx': {
    94: 'console.warn(...) — developer diagnostic, never rendered to any user.',
  },
  'features/authentication/api/authError.ts': {
    74: "const VIETNAMESE_CHARS = /[àáâ...]/i — a REGEX PATTERN used to detect Vietnamese text (to suppress raw backend prose in EN mode), not UI text itself.",
  },
  'shared/utils/toast.ts': {
    74: 'Same VIETNAMESE_CHARS regex-pattern definition as authError.ts — not UI text.',
  },
  'features/public-partners/utils/countryMatch.ts': {
    11: 'Diacritic-stripping regex/comment for search matching, not UI text.',
    12: "`.replace(/[đĐ]/g, 'd')` — normalizing Vietnamese đ/Đ for search matching, not UI text.",
  },
  'features/visit-request/components/shared/CountrySelect.tsx': {
    47: "`.replace(/đ/g, 'd')` — diacritic-stripping for search filtering (see stripDiacritics above), not UI text. The component itself is fully bilingual (t() for placeholder/create-label/no-options, dynamic VI/EN country name source).",
    48: "`.replace(/Đ/g, 'D')` — same diacritic-stripping, not UI text.",
  },
  'features/visit-request/components/ExcelUpload/excelValidator.ts': {
    40: "COLUMN_ALIASES.fullName — deliberately bilingual column-header MATCHING data (own comment: \"a file downloaded in one language must still parse when uploaded in the other\"), not rendered UI text.",
    41: 'COLUMN_ALIASES.jobTitle — same bilingual header-matching data.',
    42: 'COLUMN_ALIASES.organization — same bilingual header-matching data.',
    43: 'COLUMN_ALIASES.nationality — same bilingual header-matching data.',
  },
  'shared/api/httpClient.ts': {
    89: 'FORCED_LOGOUT_REASON_KEY fallback message (BR-AUTH-CAMPUS-08). PublicHomePage.tsx now consumes and renders this value one-shot -- this line is the ONLY hardcoded-VI source for it (the interceptor prefers the backend-supplied errorBody.message when present). Still not i18n-translated: pending a proper follow-up to move this fallback into validation/errors locale JSON.',
  },
  'shared/constants/countryCoordinates.ts': {
    16: 'Map-pin lookup table keyed by lowercase Vietnamese country name (e.g. việt nam/hàn quốc) for matching against localized country name strings — a data KEY, never rendered as UI text itself.',
    17: 'Same lookup-table data key.', 18: 'Same lookup-table data key.', 19: 'Same lookup-table data key.',
    20: 'Same lookup-table data key.', 21: 'Same lookup-table data key.', 23: 'Same lookup-table data key.',
    28: 'Same lookup-table data key.', 30: 'Same lookup-table data key.', 31: 'Same lookup-table data key.',
    33: 'Same lookup-table data key.', 34: 'Same lookup-table data key.', 41: 'Same lookup-table data key.',
    42: 'Same lookup-table data key.', 43: 'Same lookup-table data key.', 44: 'Same lookup-table data key.',
    45: 'Same lookup-table data key.', 46: 'Same lookup-table data key.', 47: 'Same lookup-table data key.',
    48: 'Same lookup-table data key.', 49: 'Same lookup-table data key.', 50: 'Same lookup-table data key.',
    51: 'Same lookup-table data key.', 52: 'Same lookup-table data key.', 54: 'Same lookup-table data key.',
    55: 'Same lookup-table data key.', 58: 'Same lookup-table data key.', 59: 'Same lookup-table data key.',
    60: 'Same lookup-table data key.', 63: 'Same lookup-table data key.', 65: 'Same lookup-table data key.',
    72: 'Same lookup-table data key.', 73: 'Same lookup-table data key.',
  },
  'shared/utils/countryNames.ts': {
    20: 'VI<->code country-name data table (feeds getViCountryNames()/getEnCountryNames(), consumed bilingually by CountrySelect.tsx/useCountryTranslation.ts, both confirmed already fully bilingual) — the VI half of legitimate bilingual data, not untranslated UI text.',
    21: 'Same bilingual data table.', 23: 'Same bilingual data table.', 24: 'Same bilingual data table.',
    29: 'Same bilingual data table.', 30: 'Same bilingual data table.', 34: 'Same bilingual data table.',
    36: 'Same bilingual data table.', 37: 'Same bilingual data table.', 38: 'Same bilingual data table.',
    40: 'Same bilingual data table.',
  },
  'shared/utils/vietnamTime.ts': {
    // formatVietnamRelative() — the file's OWN comment block (right above these lines) documents
    // this as the deliberately VI-only legacy function, still used by Staff/HO/Department screens
    // that never switch language; formatLocalizedRelativeTime below it is the Guest/Visitor path.
    226: 'formatVietnamRelative() — deliberately VI-only legacy function, see in-file comment above formatLocalizedRelativeTime.',
    227: 'formatVietnamRelative() — same deliberate VI-only legacy function.',
    229: 'formatVietnamRelative() — same deliberate VI-only legacy function.',
    231: 'formatVietnamRelative() — same deliberate VI-only legacy function.',
  },
  'features/profile/types/profile.types.ts': {
    31: "displayPosition: 'Trưởng phòng' | 'Nhân viên' | null — a TypeScript compile-time type union (Dept sub-role labels), not a runtime string ever rendered; the field itself is virtually always null for a VISITOR account (Visitor has no department/sub-role) and Profile.tsx renders it with a '—' fallback either way.",
  },
  'components/layout/Header.tsx': {
    // Language-switcher item labels: by UX convention these always show a language's own native
    // name ("Tiếng Việt"/"English"), never translated into whichever language is currently active
    // — same as every other language picker (a French picker still shows "Deutsch", not "German").
    // Line shifted from 178 to 182 by the responsive-fix pass (docs/CanhIter3FixBug/GopYCQuyen/
    // PEMS_System_Wide_Responsive_UI_Audit_and_Fix_Plan.md): getLinkClass() dropped its unused
    // `widthClass` parameter and its 7 call sites shrank from two arguments to one.
    182: "language-switcher item label — always shown in the language's own name, by convention",
  },
  'pages/CampusDetailVisitPage.tsx': {
    // CAMPUS_FALLBACK.description (hn/hcm/dn/ct/qn) is only ever read as a `t(..., {defaultValue})`
    // fallback at the one call site (`campusDescriptions.${routeId}`) — confirmed the i18n key
    // exists for every route id in both locales, so this Vietnamese text is dead/unreachable, not a
    // translation gap. Do not delete without re-checking the call site first.
    67: 'CAMPUS_FALLBACK.hn.description — unreachable defaultValue, see comment above',
    72: 'CAMPUS_FALLBACK.hcm.description — unreachable defaultValue, see comment above',
    76: 'CAMPUS_FALLBACK.dn.description — unreachable defaultValue, see comment above',
    80: 'CAMPUS_FALLBACK.ct.description — unreachable defaultValue, see comment above',
    84: 'CAMPUS_FALLBACK.qn.description — unreachable defaultValue, see comment above',
  },
};

/**
 * Marks each line as comment-or-not, tracking `/* ... *\/` and `{/* ... *\/}` block-comment state
 * across lines (a single-line startsWith check misses multi-line JSX comment bodies, whose
 * continuation lines are free prose with no `*`/`//` prefix). Approximate on purpose — this is a
 * lint-style heuristic, not a real parser — but good enough that no genuine JSX text line is ever
 * misclassified as a comment (the failure mode this errs toward is "flag too much", never "miss a
 * real hardcoded string by mistaking it for a comment").
 */
function classifyCommentLines(lines: string[]): boolean[] {
  const isComment: boolean[] = new Array(lines.length).fill(false);
  let inBlock = false;
  lines.forEach((rawLine, idx) => {
    const line = rawLine.trim();
    if (inBlock) {
      isComment[idx] = true;
      if (line.includes('*/')) inBlock = false;
      return;
    }
    if (line.startsWith('//')) {
      isComment[idx] = true;
      return;
    }
    const blockStart = line.indexOf('/*');
    if (blockStart !== -1) {
      isComment[idx] = true;
      // Only stays open past this line if there is no matching closer AFTER the opener.
      if (line.indexOf('*/', blockStart + 2) === -1) inBlock = true;
      return;
    }
  });
  return isComment;
}

/** A line is considered "translation-covered" if the Vietnamese text sits inside a `t(`/`tt(`
 * call (the i18next key path itself, e.g. `t('feedback:overallGroup.title')`, or a `defaultValue`
 * fallback) rather than a bare literal handed straight to JSX/props. */
function looksTranslationCovered(line: string): boolean {
  return /\b(t|tt|i18n\.t)\s*\(/.test(line);
}

/** Strips a trailing `// vietnamese note` end-of-line comment (this codebase's dominant comment
 * style) so a Vietnamese explanatory note after real code doesn't get flagged as hardcoded UI
 * text. Deliberately simple — does not attempt to understand string literals — so it only strips
 * on a `<code> // <comment>` shape (whitespace before `//`, not `://` as in a URL). */
function stripTrailingLineComment(line: string): string {
  const match = line.match(/\s\/\/(?!\/)/);
  if (!match || match.index === undefined) return line;
  if (line[match.index - 1] === ':') return line; // guards against `http://`-style false strips
  return line.slice(0, match.index);
}

describe('Guest/Visitor hardcoded-text scan (Phase D3/D4 gate)', () => {
  for (const relPath of SCOPED_FILES) {
    const absPath = path.join(SRC_ROOT, relPath);

    it(`${relPath} has no un-allow-listed hardcoded Vietnamese line`, () => {
      if (!fs.existsSync(absPath)) {
        throw new Error(`Scoped file no longer exists at src/${relPath} — update SCOPED_FILES.`);
      }
      const lines = fs.readFileSync(absPath, 'utf-8').split('\n');
      const isComment = classifyCommentLines(lines);
      const allowed = ALLOWLIST[relPath] ?? {};
      const offenders: string[] = [];

      lines.forEach((line, idx) => {
        const lineNo = idx + 1;
        if (isComment[idx]) return;
        const codePortion = stripTrailingLineComment(line);
        if (!VIETNAMESE_DIACRITIC.test(codePortion)) return;
        if (looksTranslationCovered(codePortion)) return;
        if (allowed[lineNo]) return;
        offenders.push(`  L${lineNo}: ${line.trim().slice(0, 140)}`);
      });

      expect(offenders, `Unexplained hardcoded Vietnamese in src/${relPath}:\n${offenders.join('\n')}`).toEqual([]);
    });
  }
});

describe('Guest/Visitor raw-enum scan (Phase D4 gate)', () => {
  // Fields whose raw backend value (roleCode, status code, etc.) must never reach JSX text
  // directly — it must always be routed through an i18n label map first.
  const RAW_ENUM_PATTERN = /\{(?:item\.|user\.|data\.)?(roleCode|status|partnerType|participantStatus|visitType|workingLanguage|mediaConsent)\}/;

  /** A controlled-input binding (`value={status}`, `onChange={...}`) reads/writes form STATE —
   * it never renders the enum as visible text, so it isn't a translation gap. Also skips a
   * template-literal i18n KEY BUILDER (e.g. `` `ns:success.status.${status}` `` or `` `http.${status}` ``,
   * no `t(`/`tr(` on the same line so `looksTranslationCovered` alone can't see it — recognized
   * here by the `word.path.${` shape any real i18n key path has, namespace-colon optional). */
  function looksLikeFormBindingOrKeyBuilder(line: string): boolean {
    if (/\bvalue=\{/.test(line)) return true;
    if (/[`'"][\w.]*(?::[\w.]*)?\.\$\{/.test(line)) return true;
    return false;
  }

  for (const relPath of SCOPED_FILES) {
    const absPath = path.join(SRC_ROOT, relPath);

    it(`${relPath} never interpolates a raw enum/status/role directly into JSX text`, () => {
      if (!fs.existsSync(absPath)) return;
      const lines = fs.readFileSync(absPath, 'utf-8').split('\n');
      const isComment = classifyCommentLines(lines);
      const offenders: string[] = [];
      lines.forEach((line, idx) => {
        if (isComment[idx]) return;
        if (looksTranslationCovered(line)) return; // e.g. t(`ns.key.${status}`) — status is part of the i18n KEY, not raw output
        if (looksLikeFormBindingOrKeyBuilder(line)) return;
        if (RAW_ENUM_PATTERN.test(line)) offenders.push(`  L${idx + 1}: ${line.trim().slice(0, 140)}`);
      });
      expect(offenders, `Raw enum/status/role interpolated directly in src/${relPath}:\n${offenders.join('\n')}`).toEqual([]);
    });
  }
});

describe('Guest/Visitor backend-message-leak scan (Phase D5 gate)', () => {
  // A RAW system message reaching the UI: reading `.message` straight off an API response/error
  // object and rendering it, instead of going through getApiErrorMessage/translateErrorCode or a
  // fixed localized string. `feedbackApiError`/`getApiErrorMessage` wrap this safely, so a call
  // site that already routes through them is fine even though the literal text `.message` appears
  // near it — the checks below specifically target the UNWRAPPED direct-read patterns.
  const RAW_MESSAGE_PATTERNS = [
    /toast\.(success|error)\(\s*(response|result|data|res)\??\.(data\??\.)?message\s*\)/,
    /(setError|setMessage)\(\s*(response|result|data|res)\??\.(data\??\.)?message\s*\)/,
  ];

  for (const relPath of SCOPED_FILES) {
    const absPath = path.join(SRC_ROOT, relPath);

    it(`${relPath} never renders a raw unwrapped backend message`, () => {
      if (!fs.existsSync(absPath)) return;
      const lines = fs.readFileSync(absPath, 'utf-8').split('\n');
      const isComment = classifyCommentLines(lines);
      const offenders: string[] = [];
      lines.forEach((line, idx) => {
        if (isComment[idx]) return;
        if (RAW_MESSAGE_PATTERNS.some((re) => re.test(line))) {
          offenders.push(`  L${idx + 1}: ${line.trim().slice(0, 140)}`);
        }
      });
      expect(offenders, `Raw unwrapped backend message in src/${relPath}:\n${offenders.join('\n')}`).toEqual([]);
    });
  }
});
