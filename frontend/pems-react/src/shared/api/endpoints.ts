export const API_ENDPOINTS = {
  auth: {
    login: '/auth/login',
    sso: '/auth/sso',
    logout: '/auth/logout',
    forgotPassword: '/auth/forgot-password',
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
