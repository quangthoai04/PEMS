/**
 * DB-backed editable email drafts (autosave / compose), mirroring SQL v10 email_drafts +
 * email_draft_recipients + email_draft_attachments. Drafts are server-persisted and owner-scoped —
 * NOT stored in localStorage (business email data must live in the DB).
 *
 * Endpoints (api/Emails):
 *   POST   /Emails/drafts                 create
 *   GET    /Emails/drafts/{id}            load (re-hydrate after reload)
 *   PUT    /Emails/drafts/{id}            update / autosave
 *   PATCH  /Emails/drafts/{id}/discard    soft-discard (never hard-deleted)
 *   POST   /Emails/drafts/{id}/send       send → produces a sent_email, marks draft SENT
 */
import httpClient from '../../../shared/api/httpClient';

export type EmailBodyFormat = 'PLAIN_TEXT' | 'HTML';
export type EmailAttachmentType = 'ATTACHMENT' | 'INLINE_IMAGE';
export type EmailDraftStatus = 'DRAFT' | 'SENT' | 'DISCARDED';

export interface EmailDraftRecipientInput {
  email: string;
  name?: string | null;
  recipientType?: 'TO' | 'CC' | 'BCC';
  displayOrder?: number;
}

export interface EmailDraftAttachmentInput {
  fileId: number;
  attachmentType?: EmailAttachmentType;
  /** Required when attachmentType = INLINE_IMAGE (the cid the HTML body references). */
  contentId?: string | null;
  displayName?: string | null;
  displayOrder?: number;
}

export interface EmailDraftRecipientDto {
  emailDraftRecipientId: number;
  recipientEmail: string;
  recipientName?: string | null;
  recipientType: string;
  displayOrder: number;
}

export interface EmailDraftAttachmentDto {
  emailDraftAttachmentId: number;
  fileId: number;
  attachmentType: EmailAttachmentType;
  contentId?: string | null;
  displayName?: string | null;
  displayOrder: number;
  originalFilename?: string | null;
  mimeType?: string | null;
  fileSize?: number | null;
  webViewUrl?: string | null;
  downloadUrl?: string | null;
  thumbnailUrl?: string | null;
}

export interface EmailDraftDto {
  emailDraftId: number;
  emailTemplateId?: number | null;
  relatedType?: string | null;
  relatedId?: number | null;
  subject?: string | null;
  bodyContent?: string | null;
  bodyFormat: EmailBodyFormat;
  status: EmailDraftStatus;
  sentEmailId?: number | null;
  createdAt?: string | null;
  updatedAt?: string | null;
  recipients: EmailDraftRecipientDto[];
  attachments: EmailDraftAttachmentDto[];
}

export interface CreateEmailDraftPayload {
  emailTemplateId?: number | null;
  relatedType?: string | null;
  relatedId?: number | null;
  subject?: string | null;
  bodyContent?: string | null;
  bodyFormat?: EmailBodyFormat;
  recipients?: EmailDraftRecipientInput[];
  attachments?: EmailDraftAttachmentInput[];
}

export type UpdateEmailDraftPayload = CreateEmailDraftPayload;

export interface SendEmailDraftResult {
  emailDraftId: number;
  sentEmailId: number;
  status: 'SENT' | 'PARTIAL_FAILED' | 'FAILED';
  success: boolean;
  draftStatus: EmailDraftStatus;
  message: string;
}

export interface DiscardEmailDraftResult {
  emailDraftId: number;
  status: EmailDraftStatus;
}

/**
 * A row in the "Nháp" list. Deliberately without a body or recipient addresses — the list only needs
 * enough to recognise and reopen a draft, and the full content comes from `getDraft`.
 */
export interface EmailDraftSummaryDto {
  emailDraftId: number;
  subject?: string | null;
  updatedAt: string;
  recipientCount: number;
  attachmentCount: number;
}

export interface ListEmailDraftsResult {
  items: EmailDraftSummaryDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export const emailDraftsApi = {
  listDrafts: async (params: { page?: number; pageSize?: number } = {}): Promise<ListEmailDraftsResult> => {
    const { data } = await httpClient.get('/Emails/drafts', { params });
    return data;
  },
  createDraft: async (payload: CreateEmailDraftPayload): Promise<EmailDraftDto> => {
    const { data } = await httpClient.post('/Emails/drafts', payload);
    return data;
  },
  getDraft: async (draftId: number | string): Promise<EmailDraftDto> => {
    const { data } = await httpClient.get(`/Emails/drafts/${draftId}`);
    return data;
  },
  updateDraft: async (draftId: number | string, payload: UpdateEmailDraftPayload): Promise<EmailDraftDto> => {
    const { data } = await httpClient.put(`/Emails/drafts/${draftId}`, payload);
    return data;
  },
  discardDraft: async (draftId: number | string): Promise<DiscardEmailDraftResult> => {
    const { data } = await httpClient.patch(`/Emails/drafts/${draftId}/discard`);
    return data;
  },
  sendDraft: async (draftId: number | string): Promise<SendEmailDraftResult> => {
    const { data } = await httpClient.post(`/Emails/drafts/${draftId}/send`);
    return data;
  },
};
