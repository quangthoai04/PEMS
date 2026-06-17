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
    submitVisitRequest: '/visit-requests',
    processVisitRequest: (id: string | number) => `/visit-requests/${id}/process`,
  },
};
