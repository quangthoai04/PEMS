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
  },
  campuses: {
    active: '/campuses/active',
    // UC-82 list (also serves UC-83 search/filter), UC-83 filter options, UC-86 status toggle.
    list: '/campuses/viewcampuslist',
    search: '/campuses/searchandfiltercampus',
    filterOptions: '/campuses/filter-options',
    manageStatus: '/campuses/managecampusstatus',
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
    statistics: '/accounts/statistics',
    campusDepartments: '/accounts/campus-departments',
    staffLeaderAvailability: '/accounts/staff-leader-availability',
    hoCampusCheck: '/accounts/ho-campus-check',
    staffLeaderReplacementPreview: '/accounts/staff-leader-replacement-preview',
    replaceStaffLeader: '/accounts/replacestaffleader',
    // Staff Leader "Visitor liên quan" tab (read-only): list + detail of related Visitor accounts.
    relatedVisitors: '/accounts/related-visitors',
    relatedVisitorDetails: '/accounts/related-visitor-details',
  },
  partners: {
    list: '/partners',
    detail: (id: string | number) => `/partners/${id}`,
    create: '/partners',
    update: (id: string | number) => `/partners/${id}`,
  },
  publicPartners: {
    search: '/public/partners/search',
  },
  delegations: {
    list: '/delegations',
    managementList: '/delegations/viewguestdelegationlist',
    detail: (id: string | number) => `/delegations/${id}`,
    // Read-only "what the guest submitted" snapshot, shared by pre-approval review,
    // approved/waiting-host detail and rejected detail screens.
    submittedFormDetail: (visitRequestId: string | number) =>
      `/delegations/visit-requests/${visitRequestId}/submitted-form-detail`,
    // UC-18 HO approve / reject a MULTI_CAMPUS request (whole request).
    hoApprove: (visitRequestId: string | number) => `/delegations/${visitRequestId}/ho-approve`,
    hoReject: (visitRequestId: string | number) => `/delegations/${visitRequestId}/ho-reject`,
    // UC-22 Staff Leader: reject own-campus single request, list host candidates, assign/transfer host.
    campusReject: (visitRequestId: string | number) => `/delegations/${visitRequestId}/campus-reject`,
    hostCandidates: (visitInstanceId: string | number) => `/delegations/campuses/${visitInstanceId}/host-candidates`,
    assignHost: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/assign-host`,
    // Phase 2: permission flags for the visit-process detail page (source of truth for tab view/edit).
    processPermissions: (visitInstanceId: string | number) =>
      `/delegations/visit-instances/${visitInstanceId}/process-permissions`,
    // VisitProcess "Trước tiếp khách": real setup detail + agenda upsert.
    processDetail: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/process-detail`,
    saveAgenda: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/agenda`,
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
  },
  // Phase 3: meeting minutes (biên bản) — 1 per visit_instance with an edit-lock workflow.
  meetingMinutes: {
    byInstance: (visitInstanceId: string | number) => `/meetingminutes/visit-instances/${visitInstanceId}`,
    createOrLock: (visitInstanceId: string | number) => `/meetingminutes/visit-instances/${visitInstanceId}/create-or-lock`,
    acquireLock: (minutesId: string | number) => `/meetingminutes/${minutesId}/acquire-lock`,
    save: (minutesId: string | number) => `/meetingminutes/${minutesId}`,
    releaseLock: (minutesId: string | number) => `/meetingminutes/${minutesId}/release-lock`,
  },
  // Phase 4: news (tin tức) attached to a visit_instance — many posts per instance.
  visitNews: {
    byInstance: (visitInstanceId: string | number) => `/news/visit-instances/${visitInstanceId}`,
    create: (visitInstanceId: string | number) => `/news/visit-instances/${visitInstanceId}`,
    update: (newsId: string | number) => `/news/visit-instance-news/${newsId}`,
    submitReview: (newsId: string | number) => `/news/visit-instance-news/${newsId}/submit-review`,
  },
  visitRequests: {
    initiate: '/visit-requests/initiate',
    verify: '/visit-requests/verify',
    resendOtp: '/visit-requests/resend-otp',
  },
  visitInvitations: {
    my: '/visit-invitations/my',
    detail: (participantId: string | number) => `/visit-invitations/${participantId}`,
    accept: (participantId: string | number) => `/visit-invitations/${participantId}/accept`,
    decline: (participantId: string | number) => `/visit-invitations/${participantId}/decline`,
    assignDepartmentStaff: (participantId: string | number) => `/visit-invitations/${participantId}/assign-department-staff`,
  },
};
