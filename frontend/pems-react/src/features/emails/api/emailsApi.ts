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
   * Direct manual send — no draft. Requires `Idempotency-Key` for the same reason reply does.
   *
   * No screen currently calls this: the compose modal saves a draft and sends that, which is protected by
   * the DRAFT→SENT claim instead. It is kept and protected because it is a live API route, and an
   * unprotected send route is unprotected whether or not the current UI happens to use it.
   */
  sendEmail: (data: SendEmailPayload, idempotencyKey: string) => {
    return httpClient.post('/Emails/sendemail', data, idempotent(idempotencyKey));
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
   * Content only. The command no longer carries templateCode, purpose, campusId, bodyFormat,
   * variablesText or status — the catalog is fixed in code (G11-I) — and `expectedRevision` is the
   * optimistic-concurrency token read back from the detail response.
   */
  updateEmailTemplate: (id: number | string, data: UpdateEmailTemplatePayload) => {
    return httpClient.put(`/email-templates/${id}`, data);
  },
  /**
   * Puts a template's content back to the wording PEMS ships (G11-I). HO only; the backend refuses
   * anyone else, so hiding the button is presentation, not protection.
   *
   * Carries the same `expectedRevision` as a save: restore is a full content overwrite, and restoring
   * over a colleague's unseen edit is the same lost update as saving over it.
   */
  restoreEmailTemplateDefault: (id: number | string, expectedRevision: number) => {
    return httpClient.post(`/email-templates/${id}/restore-default`, { expectedRevision });
  },
  /**
   * The reply-contact settings for one template: whether the block appears, where the contact comes
   * from, and which of its fields are shown.
   *
   * Readable by every composing role — the compose and preview screens need to know whether a block
   * will appear at all. Saving is HO only; the backend enforces that.
   */
  getEmailContactSettings: (templateCode: string) => {
    return httpClient.get<EmailContactSettings>(
      `/email-templates/${encodeURIComponent(templateCode)}/contact-settings`,
    );
  },
  restoreEmailContactSettingsDefault: (templateCode: string) => {
    return httpClient.post<EmailContactSettings>(
      `/email-templates/${encodeURIComponent(templateCode)}/contact-settings/restore-default`,
      {}
    );
  },
  updateEmailContactSettings: (templateCode: string, data: EmailContactSettingsPayload) => {
    return httpClient.put<EmailContactSettings>(
      `/email-templates/${encodeURIComponent(templateCode)}/contact-settings`,
      data,
    );
  },
  /**
   * Renders the contact block as an UNSAVED policy would produce it, with sample data. A POST because
   * the draft policy travels in the body; nothing is stored.
   */
  previewEmailContactBlock: (
    templateCode: string,
    data: EmailContactSettingsPayload & { language?: string },
  ) => {
    return httpClient.post<EmailContactBlockPreview>(
      `/email-templates/${encodeURIComponent(templateCode)}/contact-settings/preview`,
      data,
    );
  },
};

export interface EmailContactBlockPreview {
  /** Rendered block, or '' when this policy renders nothing. Empty is a valid answer, not a failure. */
  html: string;
  rendersBlock: boolean;
}

/**
 * How a template's reply-contact block behaves.
 *
 * Note what is NOT here: no name, no address, no telephone number, no user id. An operator chooses
 * WHICH fields the block shows; the values are read from users/campuses/departments when the mail is
 * sent, so a template can never present a hand-typed mailbox as somebody's.
 */
export interface EmailContactSettings {
  templateCode: string;
  requirement: 'NONE' | 'OPTIONAL' | 'REQUIRED';
  contactSource:
    | 'HOST' | 'SENDER' | 'HOST_THEN_SENDER'
    | 'CAMPUS_DEFAULT' | 'DEPARTMENT_DEFAULT' | 'SUPPORT_CONTACT';
  showEmail: boolean;
  showPhone: boolean;
  showDepartment: boolean;
  showCampus: boolean;
  showSender: boolean;
  headingVi: string;
  headingEn: string;
  replyToSource: 'NONE' | 'CONTACT' | 'SENDER';
  /** The placeholder a REQUIRED policy needs in the body — supplied so no screen hard-codes it. */
  blockPlaceholder: string;
  bodyCarriesBlockVi: boolean;
  bodyCarriesBlockEn: boolean;
  /**
   * Whether a TEMPLATE-scope row exists at all. This is a plain fact, NOT "these are the defaults":
   * the seed writes a row for every catalogued template, so it is true almost everywhere and says
   * nothing about whether any given value was inherited. Read the per-field `*Source` labels for that.
   */
  hasOwnPolicyRow: boolean;
  requirementSource: EmailContactPolicyLevel;
  contactSourceSource: EmailContactPolicyLevel;
  showEmailSource: EmailContactPolicyLevel;
  showPhoneSource: EmailContactPolicyLevel;
  showDepartmentSource: EmailContactPolicyLevel;
  showCampusSource: EmailContactPolicyLevel;
  showSenderSource: EmailContactPolicyLevel;
  replyToSourceSource: EmailContactPolicyLevel;
  /** The two headings share one label: they are stored on one row and edited as a pair. */
  headingSource: EmailContactPolicyLevel;
  availableRequirements: string[];
  availableSources: string[];
  availableReplyToSources: string[];
}

/**
 * Which cascade level supplied one field's effective value. Per field, because the cascade is applied
 * per field — a campus row that only hides telephone numbers changes one label and leaves the rest.
 */
export type EmailContactPolicyLevel =
  | 'TEMPLATE' | 'CAMPUS' | 'DEPARTMENT' | 'SYSTEM' | 'SHIPPED_DEFAULT';

export type EmailContactSettingsPayload = Pick<
  EmailContactSettings,
  'requirement' | 'contactSource' | 'showEmail' | 'showPhone'
  | 'showDepartment' | 'showCampus' | 'showSender' | 'headingVi' | 'headingEn' | 'replyToSource'
>;

/** Content fields an operator may change, plus the concurrency token they loaded with. */
export interface UpdateEmailTemplatePayload {
  name: string;
  description?: string | null;
  subjectVi?: string | null;
  bodyVi?: string | null;
  subjectEn?: string | null;
  bodyEn?: string | null;
  expectedRevision?: number | null;
}
