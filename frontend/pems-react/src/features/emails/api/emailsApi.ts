import httpClient from '../../../shared/api/httpClient';
import type { EmailRecipientInput } from '../types/recipients';
import type { TemplateContract } from '../types/templateContract';

/**
 * Carries the idempotency key for one send attempt (G11-H). Same shape as the reports feature uses —
 * duplicated as a two-line helper rather than imported across features, which would couple the email
 * module to the reports module for one header.
 */
function idempotent(key: string) {
  return { headers: { 'Idempotency-Key': key } };
}

/**
 * Manual compose payload — the camelCase form of `SendEmailCommand`.
 * The three groups are three lists, as they are on the command; a single flattened list could not say
 * which addresses were copies.
 */
export interface SendEmailPayload {
  templateId?: number | null;
  subject: string;
  body: string;
  /** PLAIN_TEXT | HTML. The composer produces HTML. */
  bodyFormat?: 'PLAIN_TEXT' | 'HTML';
  to: EmailRecipientInput[];
  cc: EmailRecipientInput[];
  bcc: EmailRecipientInput[];
  /**
   * Files and inline images by `fileId`, uploaded before the send.
   *
   * The composer used to reach attachments onto a message by writing them to a draft row; this route
   * accepted none, so the "direct" send it offered would silently post the message without them.
   */
  attachments?: EmailComposeAttachmentInput[];
  /** What the message is about, for the history. Omitted means GENERAL. */
  relatedType?: string | null;
  relatedId?: number | null;
}

/** A file or inline image on a composed message — the camelCase form of `EmailComposeAttachmentInput`. */
export interface EmailComposeAttachmentInput {
  fileId: number;
  attachmentType?: 'ATTACHMENT' | 'INLINE_IMAGE';
  /** Required when attachmentType = INLINE_IMAGE (the cid the HTML body references). */
  contentId?: string | null;
  displayName?: string | null;
  displayOrder?: number;
}

/** The preview payload: the same message the send would take. */
export type PreviewEmailPayload = Omit<SendEmailPayload, 'templateId'>;

/**
 * What the backend says would go out.
 *
 * `body` is the SANITISED body — what the recipient will actually receive. The composer used to preview
 * its local state through the frontend sanitiser, whose allow-list is not the backend's, so a message
 * could preview cleanly and be delivered with parts of it removed.
 */
export interface PreviewEmailResult {
  subject: string;
  body: string;
  isHtml: boolean;
  to: string[];
  cc: string[];
  bcc: string[];
  /** Named attachments. Reaching this list means every one of them was readable. */
  attachments: string[];
}

/**
 * Reply payload — the camelCase form of `ReplytoEmailCommand`.
 *
 * There is no `to`: the reply goes to the original sender, resolved server-side from
 * `originalEmailId`. Letting the client name the TO would let a reply be redirected to somebody who was
 * never party to the thread.
 */
export interface ReplyEmailPayload {
  originalEmailId: number;
  body: string;
  cc?: EmailRecipientInput[];
  bcc?: EmailRecipientInput[];
}

export interface RecipientLimits {
  maxRecipients: number;
}

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
  /**
   * Reply to the original sender. The route REFUSES a request without `Idempotency-Key` (G11-H): a
   * browser that gives up on a slow reply and lets the user press Send again would otherwise post the
   * message twice, to real people, with no way for the server to tell the two apart.
   */
  replyEmail: (data: ReplyEmailPayload, idempotencyKey: string) => {
    return httpClient.post('/Emails/replytoemail', data, idempotent(idempotencyKey));
  },
  /**
   * Reply All: the original sender plus the original's VISIBLE recipients, minus the current user. The
   * recipient list is resolved by the server from the parent message — the client sends no addresses for
   * it, so it cannot smuggle in someone who was on BCC.
   */
  replyAllEmail: (data: ReplyEmailPayload, idempotencyKey: string) => {
    return httpClient.post('/Emails/replyalltoemail', data, idempotent(idempotencyKey));
  },
  getRecipientLimits: () => {
    return httpClient.get<RecipientLimits>('/Emails/recipient-limits');
  },
  markCompleted: (id: number) => {
    return httpClient.post(`/Emails/${id}/mark-completed`);
  },
  getUnprocessedCount: () => {
    return httpClient.get('/Emails/unprocessed-count');
  },
  /**
   * Manual send. Requires `Idempotency-Key`.
   *
   * This is now the compose screen's only send path. It used to be a route no screen called — the modal
   * saved a draft and sent that, protected by the DRAFT → SENT claim. With the draft gone, the
   * reservation behind this header IS the double-click protection, so the key is not optional decoration.
   */
  sendEmail: (data: SendEmailPayload, idempotencyKey: string) => {
    return httpClient.post('/Emails/sendemail', data, idempotent(idempotencyKey));
  },
  /**
   * What the message would look like going out, checked by the same code that would send it.
   *
   * No idempotency key: nothing is written and nothing reaches a provider, so a repeated preview is
   * simply a repeated question.
   */
  previewEmail: (data: PreviewEmailPayload) => {
    return httpClient.post<PreviewEmailResult>('/Emails/preview', data);
  },
  getEmailTemplateList: (params?: { keyword?: string; status?: string; purpose?: string; page?: number; pageSize?: number; mode?: string }) => {
    return httpClient.get('/email-templates', { params });
  },
  getEmailTemplateDetail: (id: number | string) => {
    return httpClient.get(`/email-templates/${id}`);
  },
  /**
   * What a template's variables actually are (G11-J). Fetched per template code before the editor
   * validates anything, and used by the compose screen to decide whether CC/BCC may be offered at all.
   */
  getEmailTemplateContract: (templateCode: string, language?: string) => {
    return httpClient.get<TemplateContract>(
      `/email-templates/contract/${encodeURIComponent(templateCode)}`,
      { params: language ? { language } : undefined },
    );
  },
  /**
   * The ONE save the template editor makes.
   *
   * The command no longer carries templateCode, purpose, campusId, bodyFormat, variablesText or status —
   * the catalog is fixed in code (G11-I) — and `expectedRevision` is the optimistic-concurrency token
   * read back from the detail response. The whole request is one transaction on the server, so a failure
   * The contact half is gone: sender information is ordinary variables, saved with the wording.
   */
  updateEmailTemplate: (id: number | string, data: UpdateEmailTemplatePayload) => {
    return httpClient.put<UpdateEmailTemplateResult>(`/email-templates/${id}`, data);
  },
  /**
   * Puts a template back to the content PEMS ships. HO only; the backend refuses anyone else, so
   * hiding the button is presentation, not protection.
   *
   * Carries the same `expectedRevision` as a save: restore is a full overwrite, and restoring over a
   * colleague's unseen edit is the same lost update as saving over it.
   */
  restoreEmailTemplateDefault: (id: number | string, expectedRevision: number) => {
    return httpClient.post<RestoreEmailTemplateResult>(
      `/email-templates/${id}/restore-default`, { expectedRevision });
  },
};

export interface UpdateEmailTemplatePayload {
  name: string;
  description?: string | null;
  subjectVi?: string | null;
  bodyVi?: string | null;
  subjectEn?: string | null;
  bodyEn?: string | null;
  expectedRevision?: number | null;
}

/**
 * What the API returns after a save — the full stored snapshot, not just the new revision.
 *
 * The editor re-baselines its dirty check from this, so it has to be what the DATABASE now holds rather
 * than what the client sent: headings are trimmed and stripped of markup on the way in, an empty
 * description is stored as NULL, and a heading equal to the shipped wording is stored as "inherit". Each
 * of those would otherwise leave the screen reporting an unsaved change the instant a save succeeded.
 */
export interface UpdateEmailTemplateResult {
  emailTemplateId: number;
  templateCode?: string | null;
  success: boolean;
  message?: string | null;
  revision: number;
  updatedAt?: string | null;
  name?: string | null;
  description?: string | null;
  subjectVi?: string | null;
  bodyVi?: string | null;
  subjectEn?: string | null;
  bodyEn?: string | null;
}

// Restore returns the same shape as a save. It used to add contactSettingsRestored, because restore
// replaced two things and the screen had to say which; there is one thing again.
export type RestoreEmailTemplateResult = UpdateEmailTemplateResult;
