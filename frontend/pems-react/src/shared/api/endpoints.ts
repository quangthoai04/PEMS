export const API_ENDPOINTS = {
  auth: {
    login: '/auth/login',
    google: '/auth/google',
    refresh: '/auth/refresh',
    logout: '/auth/logout',
    me: '/auth/me',
    permissions: '/auth/permissions',
    forgotPassword: '/auth/forgot-password',
    resetPassword: '/auth/reset-password',
    changePassword: '/auth/change-password',
  },
  profile: {
    // UC-14 view my profile, UC-15 update my profile (self-service; user resolved from token).
    me: '/profiles/viewprofile',
    update: '/profiles/updateprofile',
    // UC-15 avatar upload (multipart field "avatar"); user resolved from token.
    avatar: '/profiles/me/avatar',
  },
  campuses: {
    active: '/campuses/active',
    // UC-82 list (also serves UC-83 search/filter), UC-83 filter options, UC-86 status toggle.
    list: '/campuses/viewcampuslist',
    search: '/campuses/searchandfiltercampus',
    filterOptions: '/campuses/filter-options',
    manageStatus: '/campuses/managecampusstatus',
    // UC-86 §18 status-impact preview (blockers on disable / activation issues on enable).
    statusImpact: '/campuses/campusstatusimpact',
    // UC-86 §10 — anonymous campus options for the visit registration form (only campuses
    // ACTIVE + active IC department + valid Staff Leader; backend rechecks on submit).
    availableForRegistration: '/campuses/available-for-registration',
    // UC-81 create (+ auto IC dept), UC-84 details, UC-85 update master data.
    create: '/campuses/addnewcampus',
    details: '/campuses/viewcampusdetails',
    update: '/campuses/updatecampus',
  },
  accounts: {
    list: '/accounts/viewaccountlist',
    create: '/accounts/createaccount',
    manageStatus: '/accounts/manageaccountstatus',
    details: '/accounts/viewaccountdetails',
    search: '/accounts/searchandfilteraccounts',
    updateRole: '/accounts/updateaccountrole',
    // HO_BASIC_INFO — HO edits only full name + email of another HO / Staff Leader.
    updateBasicInfo: '/accounts/updatebasicaccountinfo',
    // Re-issues the confirmation link of a PENDING_EMAIL_CONFIRMATION account (rate-limited
    // server-side). Supersedes the previous token — it never creates a second account.
    resendEmailConfirmation: '/accounts/resend-email-confirmation',
    // Corrects the address of a PENDING_EMAIL_CONFIRMATION account (and its full name in the same
    // transaction). Distinct from updateBasicInfo above: this one issues a NEW confirmation token and
    // mails the activation link to the new address, which is the only way such an account can go live.
    editPendingEmail: '/accounts/edit-pending-email',
    statistics: '/accounts/statistics',
    campusDepartments: '/accounts/campus-departments',
    // UC-100-SL — role-assignment options for the "Chỉnh sửa vai trò" modal (campus scoped server-side).
    roleAssignmentOptions: '/accounts/role-assignment-options',
    staffLeaderAvailability: '/accounts/staff-leader-availability',
    hoCampusCheck: '/accounts/ho-campus-check',
    staffLeaderReplacementPreview: '/accounts/staff-leader-replacement-preview',
    replaceStaffLeader: '/accounts/replacestaffleader',
    // Staff Leader "Visitor liên quan" tab (read-only): list + detail of related Visitor accounts.
    relatedVisitors: '/accounts/related-visitors',
    relatedVisitorDetails: '/accounts/related-visitor-details',
    // Nationality filter options, built server-side from EVERY related Visitor of the campus.
    // Its own endpoint on purpose: deriving the options from the first page of the list above
    // would drop any nationality that only appears further down.
    relatedVisitorNationalities: '/accounts/staff-leader/related-visitors/nationalities',
  },
  // Department Leader personnel management (/dashboard/my-department). Every route is scoped to the
  // caller's OWN department server-side — deliberately no departmentId parameter anywhere, which is
  // what closes the IDOR the older /departments/*personnel* endpoints had.
  departmentLeader: {
    department: '/department-leader/department',
    personnel: '/department-leader/personnel',
    personnelDetail: (userId: string | number) => `/department-leader/personnel/${userId}`,
    personnelStatusImpact: (userId: string | number) =>
      `/department-leader/personnel/${userId}/status-impact`,
    personnelStatus: (userId: string | number) => `/department-leader/personnel/${userId}/status`,
    personnelResendConfirmation: (userId: string | number) =>
      `/department-leader/personnel/${userId}/resend-email-confirmation`,
    leaderCandidates: '/department-leader/leader-candidates',
    transferLeadership: '/department-leader/transfer-leadership',
  },
  departments: {
    // UC-104 list (also serves UC-103 search/filter), UC-101 create, status toggle,
    // UC-105 details, UC-102 update name. Campus scope is resolved server-side from the Staff Leader.
    list: '/departments/viewdepartmentlist',
    search: '/departments/searchandfilterdepartments',
    create: '/departments/addnewdepartment',
    manageStatus: '/departments/managedepartmentstatus',
    // UC-106 impact preview for the status confirmation modal (affected accounts/sessions/blockers).
    statusImpact: '/departments/departmentstatusimpact',
    details: '/departments/viewdepartmentdetails',
    update: '/departments/updatedepartment',
  },
  // VisitFPTU Gallery management (Staff Leader). Campus scope resolved server-side from the JWT.
  gallery: {
    // UC-GAL-01 list (also serves UC-GAL-02 search/filter), UC-GAL-03 detail.
    list: '/galleries/viewgalleryitemlist',
    search: '/galleries/searchgalleryitems',
    details: '/galleries/viewgalleryitemdetails',
    // Area/location reference data for filter dropdowns + upload picker.
    filterOptions: '/galleries/galleryfilteroptions',
    // UC-GAL-04 add (multipart), UC-GAL-07 edit (multipart), UC-GAL-05/06 enable/disable.
    create: '/galleries/addgalleryitem',
    update: '/galleries/updategalleryitem',
    changeStatus: '/galleries/changegalleryitemstatus',
    // UC-LOC-01..09 — area/location management ("Quản lý khu vực").
    locationList: '/galleries/viewgallerylocationlist',
    locationCreate: '/galleries/creategallerylocation',
    locationUpdate: '/galleries/updategallerylocation',
    locationChangeStatus: '/galleries/changegallerylocationstatus',
    // Authoritative single-location detail for the edit modal (bilingual names + covers).
    locationDetail: '/galleries/gallerylocationdetails',
    // "Dịch sang EN" preview — one Google call, nothing persisted.
    locationPreviewTranslation: '/galleries/previewlocationtranslation',
    // "Dịch sang EN" preview for the gallery item title — DB reuse when unchanged, nothing persisted.
    itemPreviewTranslation: '/galleries/previewitemtranslation',
  },
  files: {
    download: (id: string | number) => `/files/${id}/download`,
    content: (id: string | number) => `/files/${id}/content`,
  },
  partners: {
    list: '/partners',
    detail: (id: string | number) => `/partners/${id}`,
    create: '/partners',
    update: (id: string | number) => `/partners/${id}`,
    approve: (id: string | number) => `/partners/${id}/approve`,
    reject: (id: string | number) => `/partners/${id}/reject`,
    pendingApprovals: '/partners/pending-approvals',
    match: '/partners/match',
    contacts: (id: string | number) => `/partners/${id}/contacts`,
    contact: (id: string | number, contactId: string | number) => `/partners/${id}/contacts/${contactId}`,
    contactSetPrimary: (id: string | number, contactId: string | number) =>
      `/partners/${id}/contacts/${contactId}/set-primary`,
    aliases: (id: string | number) => `/partners/${id}/aliases`,
    alias: (id: string | number, aliasId: string | number) => `/partners/${id}/aliases/${aliasId}`,
    documents: (id: string | number) => `/partners/${id}/documents`,
  },
  visitPartnerLinks: {
    list: (visitInstanceId: string | number) => `/visit-instances/${visitInstanceId}/partner-links`,
    create: (visitInstanceId: string | number) => `/visit-instances/${visitInstanceId}/partner-links`,
    update: (visitInstanceId: string | number, linkId: string | number) =>
      `/visit-instances/${visitInstanceId}/partner-links/${linkId}`,
    rejectSuggestion: (visitInstanceId: string | number, linkId: string | number) =>
      `/visit-instances/${visitInstanceId}/partner-links/${linkId}/reject-suggestion`,
    createPartnerFromGuest: (visitInstanceId: string | number) =>
      `/visit-instances/${visitInstanceId}/partner-links/create-partner`,
  },
  apiIntegrations: {
    list: '/api-integrations',
    detail: (id: string | number) => `/api-integrations/${id}`,
    upsertGoogleDocumentAi: '/api-integrations/business-card-ocr/google-document-ai',
    upsertGoogleTranslation: '/api-integrations/news-translation/google-cloud-translation',
    upsertGoogleVisionFaceDetection: '/api-integrations/face-detection/google-cloud-vision',
    upsertResend: '/api-integrations/email-delivery/resend',
    update: (id: string | number) => `/api-integrations/${id}`,
    test: (id: string | number) => `/api-integrations/${id}/test`,
    enable: (id: string | number) => `/api-integrations/${id}/enable`,
    disable: (id: string | number) => `/api-integrations/${id}/disable`,
    logs: (id: string | number) => `/api-integrations/${id}/logs`,
    quota: (id: string | number) => `/api-integrations/${id}/quota`,
  },
  businessCardOcr: {
    scan: '/business-card-ocr/scan',
    job: (id: string | number) => `/business-card-ocr/jobs/${id}`,
    confirmContact: (id: string | number) => `/business-card-ocr/jobs/${id}/confirm-contact`,
    discard: (id: string | number) => `/business-card-ocr/jobs/${id}/discard`,
  },
  publicPartners: {
    search: '/public/partners/search',
    list: '/public/partners',
    detail: (idOrSlug: string | number) => `/public/partners/${idOrSlug}`,
    // Anonymous, partner-scoped logo/cover proxy — the public-page alternative to
    // /api/files/{id}/content (which requires a session).
    mediaContent: (fileId: string | number) => `/public/partners/media/${fileId}/content`,
    // Distinct countries among APPROVED + PUBLIC partners, for the country filter dropdown.
    countries: '/public/partners/countries',
    // Distinct partner_type values (with counts) among APPROVED + PUBLIC partners.
    types: '/public/partners/types',
  },
  publicFaqs: {
    list: '/public/faqs',
    // Every faq_type (all 7, including zero-count ones) with its PUBLISHED question count.
    typeCounts: '/public/faqs/type-counts',
  },
  // Site-wide keyword search across published news, approved+public partners, published faqs,
  // and active campuses — powers the Header's SearchPopup.
  publicSearch: {
    search: '/public/search',
  },
  // Public VisitFPTU Gallery (anonymous display layer over the Staff-Leader-managed gallery).
  publicVisitFptu: {
    campuses: '/public/visit-fptu/campuses',
    navigation: (campusCode: string) => `/public/visit-fptu/campuses/${campusCode}/navigation`,
    // Album grid of a location (each public item by its primary media).
    locationGalleryItems: (locationId: string | number) =>
      `/public/visit-fptu/locations/${locationId}/gallery-items`,
    // Location Showcase — the location's MEDIA items (column) + VISIT_DELEGATION items (row).
    locationShowcase: (locationId: string | number) =>
      `/public/visit-fptu/locations/${locationId}/showcase`,
    // Detail of one gallery item (all its media).
    galleryItemDetail: (galleryItemId: string | number) =>
      `/public/visit-fptu/gallery-items/${galleryItemId}`,
    // Anonymous, gallery-scoped media proxy. URLs are returned absolute (e.g. "/api/public/visit-fptu/media/123/content").
    mediaContent: (fileId: string | number) => `/public/visit-fptu/media/${fileId}/content`,
    // Bilingual narration audio behind the speaker icon — item + language (vi/en) scoped, no fileId.
    galleryItemAudio: (galleryItemId: string | number, languageCode: string) =>
      `/public/visit-fptu/gallery-items/${galleryItemId}/audio/${languageCode}`,
  },
  delegations: {
    list: '/delegations',
    managementList: '/delegations/viewguestdelegationlist',
    detail: (id: string | number) => `/delegations/${id}`,
    // Read-only "what the guest submitted" snapshot, shared by pre-approval review,
    // approved/waiting-host detail and rejected detail screens.
    submittedFormDetail: (visitRequestId: string | number) =>
      `/delegations/visit-requests/${visitRequestId}/submitted-form-detail`,
    // Campus-independent approval (SQL v10): the campus Staff Leader decides each campus
    // instance — approve (must pick the host in the same action) or reject with a reason.
    // HO no longer approves/rejects anything (monitor/read-only).
    hostCandidates: (visitInstanceId: string | number) => `/delegations/campuses/${visitInstanceId}/host-candidates`,
    approveCampusInstance: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/approve`,
    rejectCampusInstance: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/reject`,
    // Phase 2: permission flags for the visit-process detail page (source of truth for tab view/edit).
    processPermissions: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/process-permissions`,
    // Contribution Page (spec §5.3/§10.3): permission gate + read-only summary + workspace status.
    contribution: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/contribution`,
    summary: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/summary`,
    // VisitProcess "Trước tiếp khách": real setup detail + agenda upsert.
    processDetail: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/process-detail`,
    // "Báo cáo Lịch trình" PDF — same scope as processDetail (403 in the handler otherwise).
    scheduleReportPdf: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/schedule-report/pdf`,
    // "Gửi cập nhật chuẩn bị" — current host only; every route re-derives host + prep window
    // server-side, so a handover between opening the composer and sending is refused.
    setupProgressEmailPrepare: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/setup-progress-email/prepare`,
    setupProgressEmailRefresh: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/setup-progress-email/refresh`,
    // Deliberately NOT the generic /Emails/sendemail: that one asks only "are you signed in", which
    // stays true for a host who has since been replaced.
    setupProgressEmailSend: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/setup-progress-email/send`,
    saveAgenda: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/agenda`,
    // Valid "Người phụ trách" candidates (active host + ACCEPTED supporting participants of the instance).
    agendaResponsibleCandidates: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/agenda-responsible-candidates`,
    // ASSIGNED → BEFORE_VISIT. The current Host opens the preparation window; nothing else may.
    startPreparation: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/start-preparation`,
    // Operational reception stage transitions (Host only): Trước→Đang, Đang→Sau, Sau→Đóng đoàn.
    completeBeforeVisit: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/process/complete-before-visit`,
    completeDuringVisit: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/process/complete-during-visit`,
    completeAfterVisit: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/process/complete-after-visit`,
    // UC-136 Cancel Visit Request.
    cancel: (visitRequestId: string | number) => `/delegations/${visitRequestId}/cancel`,
    cancelCampus: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/cancel`,
    // UC-27 Confirm Participation: invitee's own invitations + accept/decline.
    myInvitations: '/delegations/my-invitations',
    invitationDetail: (participantId: string | number) => `/delegations/invitations/${participantId}`,
    respondInvitation: (participantId: string | number) => `/delegations/participants/${participantId}/respond`,
    // VisitProcess "Thành phần tham gia": participant list, invite candidate searches, invite/remove.
    instanceParticipants: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/participants`,
    participantCandidates: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/participant-candidates`,
    supportDepartments: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/support-departments`,
    departmentStaffCandidates: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/department-staff-candidates`,
    inviteParticipant: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/participants/invite`,
    removeParticipant: (visitInstanceId: string | number, participantId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/participants/${participantId}/remove`,
    // VisitProcess "Ghi chú chung" (preparation note) — Host only, prep window.
    preparationNote: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/preparation-note`,
    // VisitProcess "Cảnh báo & Thông báo" (scheduled reminder settings) — Host only, prep window.
    reminderSettings: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/reminder-settings`,
    cancelReminderSettings: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/reminder-settings/cancel`,
    // VisitProcess "Chuẩn bị chi tiết": Host logistics requests (list + create).
    instanceLogistics: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/logistics`,
    prepareVisitLogistics: '/delegations/preparevisitlogistics',
    cancelLogisticsItem: (visitInstanceId: string | number, logisticsItemId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/logistics/${logisticsItemId}/cancel`,
    // VisitProcess "Đang/Sau tiếp khách": Host signs the BORROWER side of a borrow/return handover.
    signLogisticsHandoverBorrower: (visitInstanceId: string | number, logisticsItemId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/logistics/${logisticsItemId}/handovers/sign-borrower`,
    // "Lưu vào hệ thống" — generates the handover PDF server-side and archives it as a document.
    saveLogisticsHandoverDocument: (
      visitInstanceId: string | number, logisticsItemId: string | number, handoverType: string,
    ) => `/delegations/visit-instances/${visitInstanceId}/logistics/${logisticsItemId}/handovers/${handoverType}/save-document`,
    // Host responds to a Department's change proposal (accept commits effective; reject keeps planned).
    confirmChangeProposal: '/delegations/confirmthechangeproposal',
    // "Xem mail đã gửi": sent-email history for one target (participant invitation / logistics request).
    participantSentEmails: (visitInstanceId: string | number, participantId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/participants/${participantId}/sent-emails`,
    logisticsSentEmails: (visitInstanceId: string | number, logisticsItemId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/logistics/${logisticsItemId}/sent-emails`,
  },
  // Email template preview (read-only render for the "Xem trước email" modal — never sends).
  emailTemplates: {
    preview: '/email-templates/preview',
    /**
     * "Xem trước kết quả" — turns the sender's edit into the exact message that will be delivered and
     * returns the token the send accepts as proof they approved it. Stores nothing.
     */
    finalPreview: '/email-templates/final-preview',
    // The five contact-* routes were removed with the feature: a contact preview, a candidate search,
    // and three settings routes. The sender is read from authentication, so there is nobody to search
    // for and nothing to configure.
  },
  // Phase 3: meeting minutes (biên bản) — 1 per visit_instance with an edit-lock workflow.
  meetingMinutes: {
    byInstance: (visitInstanceId: string | number) => `/meetingminutes/visit-instances/${visitInstanceId}`,
    createOrLock: (visitInstanceId: string | number) => `/meetingminutes/visit-instances/${visitInstanceId}/create-or-lock`,
    acquireLock: (minutesId: string | number) => `/meetingminutes/${minutesId}/acquire-lock`,
    save: (minutesId: string | number) => `/meetingminutes/${minutesId}`,
    releaseLock: (minutesId: string | number) => `/meetingminutes/${minutesId}/release-lock`,
    newParticipantCandidates: (minutesId: string | number) => `/meetingminutes/${minutesId}/new-participant-candidates`,
    userSearch: (visitInstanceId: string | number) => `/meetingminutes/visit-instances/${visitInstanceId}/user-search`,
    // "Quản lý việc sau tiếp khách" — action items assigned to the caller.
    myActionItems: '/meetingminutes/my-action-items',
    markActionItemDone: (actionItemId: string | number) => `/meetingminutes/action-items/${actionItemId}/mark-done`,
  },
  // Student visit photos (ảnh đoàn khách) — visit_photos/visit_photo_folders, độc lập Gallery.
  visitPhotos: {
    myFolders: '/visit-photos/folders',
    byInstance: (visitInstanceId: string | number) => `/visit-photos/instances/${visitInstanceId}`,
    upload: (visitInstanceId: string | number) => `/visit-photos/instances/${visitInstanceId}/upload`,
    remove: (visitPhotoId: string | number) => `/visit-photos/${visitPhotoId}/remove`,
    // Face detection + manual guest tagging (Sau tiếp khách).
    faceScans: (visitPhotoId: string | number) => `/visit-photos/${visitPhotoId}/face-scans`,
    faceScanDetail: (faceScanId: string | number) => `/visit-photos/face-scans/${faceScanId}`,
    taggableGuests: (visitInstanceId: string | number) => `/visit-photos/instances/${visitInstanceId}/taggable-guests`,
    confirmFaceTags: (faceScanId: string | number) => `/visit-photos/face-scans/${faceScanId}/confirm`,
  },
  // "Tài liệu" upload widget of a visit-in-progress (VisitDuringTab) — documents/documents rows,
  // routed into the delegation's Drive folder (Theo đoàn khách / Đối tác / Hậu cần subfolders).
  visitDocuments: {
    upload: (visitInstanceId: string | number) => `/visit-documents/instances/${visitInstanceId}/upload`,
  },
  // Phase 4: news (tin tức) attached to a visit_instance — many posts per instance.
  visitNews: {
    byInstance: (visitInstanceId: string | number) => `/news/visit-instances/${visitInstanceId}`,
    create: (visitInstanceId: string | number) => `/news/visit-instances/${visitInstanceId}`,
    update: (newsId: string | number) => `/news/visit-instance-news/${newsId}`,
    submitReview: (newsId: string | number) => `/news/visit-instance-news/${newsId}/submit-review`,
  },
  // Agenda template module (4 tables). Template CRUD + default mapping + per-instance setup/apply.
  agendaTemplates: {
    list: '/agenda-templates',
    create: '/agenda-templates',
    detail: (id: string | number) => `/agenda-templates/${id}`,
    update: (id: string | number) => `/agenda-templates/${id}`,
    delete: (id: string | number) => `/agenda-templates/${id}`,
    defaults: '/agenda-templates/defaults',
    setDefault: '/agenda-templates/defaults',
    resolveDefault: '/agenda-templates/default',
    forInstance: (visitInstanceId: string | number) => `/agenda-templates/for-instance/${visitInstanceId}`,
    apply: '/agenda-templates/apply',
  },
  visitRequests: {
    // Authenticated create (Visitor/IC Staff/Staff Leader) — không cần OTP, identity từ JWT.
    createAuthenticated: '/visit-requests',
    // Host candidates cho ASSIGN_HOST của Staff Leader ngay trong form tạo (own campus).
    createHostCandidates: '/visit-requests/host-candidates',
    initiate: '/visit-requests/initiate',
    verify: '/visit-requests/verify',
    resendOtp: '/visit-requests/resend-otp',
    // Human-verification (Turnstile) recovery sau khi nhập sai OTP quá số lần cho phép.
    otpRecover: '/visit-requests/otp/recover',
    // Visitor sửa đơn pending / gửi lại đơn bị từ chối (owner-only, không cần OTP).
    editDetail: (visitRequestId: string | number) => `/visit-requests/${visitRequestId}/edit-detail`,
    pendingEdit: (visitRequestId: string | number) => `/visit-requests/${visitRequestId}/pending-edit`,
    resubmit: (visitRequestId: string | number) => `/visit-requests/${visitRequestId}/resubmit`,
  },
  visitInvitations: {
    my: '/visit-invitations/my',
    detail: (participantId: string | number) => `/visit-invitations/${participantId}`,
    accept: (participantId: string | number) => `/visit-invitations/${participantId}/accept`,
    decline: (participantId: string | number) => `/visit-invitations/${participantId}/decline`,
    assignDepartmentStaff: (participantId: string | number) => `/visit-invitations/${participantId}/assign-department-staff`,
  },
  departmentReceptionTasks: {
    calendar: '/department/reception-tasks/calendar',
    assignmentsProgress: '/department/reception-tasks/assignments-progress',
    attentionItems: '/department/reception-tasks/attention-items',
    overdueResponses: '/department/reception-tasks/overdue-responses',
    invitationDetail: (participantId: string | number) => `/department/reception-tasks/invitations/${participantId}`,
    acceptInvitation: (participantId: string | number) => `/department/reception-tasks/invitations/${participantId}/accept`,
    declineInvitation: (participantId: string | number) => `/department/reception-tasks/invitations/${participantId}/decline`,
    assignInvitation: (participantId: string | number) => `/department/reception-tasks/invitations/${participantId}/assign`,
    requestDetail: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}`,
    acceptRequestSelf: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/accept-self`,
    confirmRequest: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/confirm`,
    rejectRequest: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/reject`,
    proposeChange: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/propose-change`,
    assignAssignee: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/assign`,
    assigneeCandidates: '/department/reception-tasks/assignee-candidates',
    personalEvents: '/department/reception-tasks/personal-events',
    acceptAssignment: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/accept-assignment`,
    declineAssignment: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/decline-assignment`,
    signHandover: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/handovers/sign`,
  },
  feedbacks: {
    visitSummary: '/feedbacks/visit-summary',
    raw: '/feedbacks',
    visitSummaryDetail: (visitRequestId: string | number) => `/feedbacks/visit-summary/${visitRequestId}`,
    visitInstanceSummaryDetail: (visitRequestId: string | number, visitInstanceId: string | number) => `/feedbacks/visit-summary/${visitRequestId}/instances/${visitInstanceId}`,
    detail: (feedbackId: string | number) => `/feedbacks/${feedbackId}`,
    // Feedback rule mới (v10): targets + batch submit + pending reminders.
    visitFeedbackTargets: (visitInstanceId: string | number) => `/feedbacks/visit-instances/${visitInstanceId}/targets`,
    submitVisitFeedback: (visitInstanceId: string | number) => `/feedbacks/visit-instances/${visitInstanceId}`,
    myPending: '/feedbacks/my-pending',
    myHostFeedback: (visitInstanceId: string | number) => `/feedbacks/my-host-feedback/${visitInstanceId}`,
    visitorFeedback: (visitInstanceId: string | number) => `/feedbacks/visitor-feedback/${visitInstanceId}`,
  },
  reports: {
    // Head Office report (HO-only): overview + export dùng đúng bộ filter đang áp dụng.
    hoOverview: '/reports/ho-overview',
    hoExport: '/reports/ho-overview/export',
  },
  dashboard: {
    // Dashboard bảng lịch cho STAFF+LEADER / STAFF+STAFF: viewMode=office|mine, from/to=YYYY-MM-DD.
    staffCalendar: '/dashboard/staff/calendar',
    staffCalendarDetail: (visitInstanceId: string | number) =>
      `/dashboard/staff/calendar/${visitInstanceId}/detail`,
  },
  // System Administration Console (ADMIN-only): dashboard thật, phiên đăng nhập,
  // login/security logs và audit trail. Backend: AdminController (/api/admin/*).
  admin: {
    dashboardSummary: '/admin/dashboard/summary',
    dashboardLoginActivity: '/admin/dashboard/login-activity',
    dashboardSecurity: '/admin/dashboard/security',
    dashboardIntegrations: '/admin/dashboard/integrations',
    dashboardRecentAudits: '/admin/dashboard/recent-audits',
    sessions: '/admin/sessions',
    revokeSession: (sessionId: string | number) => `/admin/sessions/${sessionId}/revoke`,
    revokeUserSessions: (userId: string | number) => `/admin/users/${userId}/revoke-sessions`,
    loginLogs: '/admin/login-logs',
    securityEvents: '/admin/security-events',
    auditLogs: '/admin/audit-logs',
    auditLogDetail: (auditLogId: string | number) => `/admin/audit-logs/${auditLogId}`,
  },
};
