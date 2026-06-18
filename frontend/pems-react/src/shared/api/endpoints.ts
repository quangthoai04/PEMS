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
    detail: (id: string | number) => `/delegations/${id}`,
    processVisitRequest: (id: string | number) => `/visit-requests/${id}/process`,
  },
  visitRequests: {
    initiate: '/visit-requests/initiate',
    verify: '/visit-requests/verify',
    resendOtp: '/visit-requests/resend-otp',
  },
};
