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
  },
  accounts: {
    list: '/accounts/viewaccountlist',
    create: '/accounts/createaccount',
    manageStatus: '/accounts/manageaccountstatus',
    details: '/accounts/viewaccountdetails',
    search: '/accounts/searchandfilteraccounts',
    updateRole: '/accounts/updateaccountrole',
  },
  partners: {
    list: '/partners',
    detail: (id: string | number) => `/partners/${id}`,
    create: '/partners',
    update: (id: string | number) => `/partners/${id}`,
    search: '/partners/search',
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
};
