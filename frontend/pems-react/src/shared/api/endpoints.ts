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
    // UC-18 HO approve / reject a MULTI_CAMPUS request (whole request).
    hoApprove: (visitRequestId: string | number) => `/delegations/${visitRequestId}/ho-approve`,
    hoReject: (visitRequestId: string | number) => `/delegations/${visitRequestId}/ho-reject`,
    // UC-22 Staff Leader: reject own-campus single request, list host candidates, assign/transfer host.
    campusReject: (visitRequestId: string | number) => `/delegations/${visitRequestId}/campus-reject`,
    hostCandidates: (visitInstanceId: string | number) => `/delegations/campuses/${visitInstanceId}/host-candidates`,
    assignHost: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/assign-host`,
    // UC-136 Cancel Visit Request.
    cancel: (visitRequestId: string | number) => `/delegations/${visitRequestId}/cancel`,
    cancelCampus: (visitRequestId: string | number, visitInstanceId: string | number) =>
      `/delegations/${visitRequestId}/campuses/${visitInstanceId}/cancel`,
    // UC-27 Confirm Participation: invitee's own invitations + accept/decline.
    myInvitations: '/delegations/my-invitations',
    invitationDetail: (participantId: string | number) => `/delegations/invitations/${participantId}`,
    respondInvitation: (participantId: string | number) => `/delegations/participants/${participantId}/respond`,
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
  departmentReceptionTasks: {
    calendar: '/department/reception-tasks/calendar',
    invitationDetail: (participantId: string | number) => `/department/reception-tasks/invitations/${participantId}`,
    acceptInvitation: (participantId: string | number) => `/department/reception-tasks/invitations/${participantId}/accept`,
    declineInvitation: (participantId: string | number) => `/department/reception-tasks/invitations/${participantId}/decline`,
    requestDetail: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}`,
    confirmRequest: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/confirm`,
    rejectRequest: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/reject`,
    proposeChange: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/propose-change`,
    assignAssignee: (logisticsItemId: string | number) => `/department/reception-tasks/requests/${logisticsItemId}/assign`,
    assigneeCandidates: '/department/reception-tasks/assignee-candidates',
    personalEvents: '/department/reception-tasks/personal-events',
  },
};
