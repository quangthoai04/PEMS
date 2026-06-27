import httpClient from '../../../shared/api/httpClient';

export const emailsApi = {
  getEmailList: (params: {
    mailBox: string;
    keyword?: string;
    status?: string;
    relatedType?: string;
    startDate?: string;
    endDate?: string;
    page: number;
    pageSize: number;
  }) => {
    return httpClient.get('/Emails/viewemaillist', { params });
  },
  getEmailDetail: (id: number, sourceType: string) => {
    return httpClient.get('/Emails/viewemail', { params: { id, sourceType } });
  },
  replyEmail: (data: { originalEmailId: number; body: string; cc?: any[]; bcc?: any[] }) => {
    return httpClient.post('/Emails/replytoemail', data);
  },
  markCompleted: (id: number) => {
    return httpClient.post(`/Emails/${id}/mark-completed`);
  },
  getUnprocessedCount: () => {
    return httpClient.get('/Emails/unprocessed-count');
  },
  sendEmail: (data: any) => {
    return httpClient.post('/Emails/sendemail', data);
  },
  getEmailTemplateList: (params?: { keyword?: string; status?: string; page?: number; pageSize?: number; mode?: string }) => {
    return httpClient.get('/email-templates', { params });
  },
  getEmailTemplateDetail: (id: number | string) => {
    return httpClient.get(`/email-templates/${id}`);
  },
  createEmailTemplate: (data: any) => {
    return httpClient.post('/email-templates', data);
  },
  updateEmailTemplate: (id: number | string, data: any) => {
    return httpClient.put(`/email-templates/${id}`, data);
  },
  toggleEmailTemplateStatus: (id: number | string, status: string) => {
    return httpClient.patch(`/email-templates/${id}/status`, { status });
  }
};
