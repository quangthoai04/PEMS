/**
 * PEMS SQL v10 domain constants shared across features.
 * Source of truth: database/scripts/pems_full_..._v10_clean_logistics_handover_fields.sql
 */

// ── FAQ (faqs.faq_type) — Vietnamese only, no language_code in v10 ──────────
export const FAQ_TYPES = {
  ACCOUNT_ACCESS: 'ACCOUNT_ACCESS',
  VISIT_REQUEST: 'VISIT_REQUEST',
  DELEGATION_MANAGEMENT: 'DELEGATION_MANAGEMENT',
  LOGISTICS_RESOURCE: 'LOGISTICS_RESOURCE',
  DOCUMENT_MEDIA: 'DOCUMENT_MEDIA',
  NOTIFICATION_EMAIL: 'NOTIFICATION_EMAIL',
  OTHER: 'OTHER',
} as const;
export type FaqType = (typeof FAQ_TYPES)[keyof typeof FAQ_TYPES];

export const FAQ_TYPE_LABELS: Record<FaqType, string> = {
  ACCOUNT_ACCESS: 'Tài khoản & đăng nhập',
  VISIT_REQUEST: 'Đăng ký tham quan',
  DELEGATION_MANAGEMENT: 'Quản lý đoàn',
  LOGISTICS_RESOURCE: 'Hậu cần & mượn tài nguyên',
  DOCUMENT_MEDIA: 'Tài liệu & hình ảnh',
  NOTIFICATION_EMAIL: 'Thông báo & email',
  OTHER: 'Khác',
};

export const FAQ_STATUSES = {
  PUBLISHED: 'PUBLISHED',
  HIDDEN: 'HIDDEN',
} as const;

// ── Logistics handover (visit_logistics_item_handovers) ─────────────────────
export const LOGISTICS_HANDOVER_TYPES = {
  BORROW: 'BORROW',
  RETURN: 'RETURN',
} as const;

export const HANDOVER_ITEM_CONDITIONS = {
  GOOD: 'GOOD',
  DAMAGED: 'DAMAGED',
  MISSING: 'MISSING',
  OTHER: 'OTHER',
} as const;

export const HANDOVER_SIGNER_SIDES = {
  BORROWER: 'BORROWER',
  PROVIDER: 'PROVIDER',
} as const;

// ── Email action tokens (email_action_tokens) ───────────────────────────────
export const EMAIL_ACTION_CONTEXTS = {
  PARTICIPATION_RESPONSE: 'PARTICIPATION_RESPONSE',
  LOGISTICS_ASSIGNEE_RESPONSE: 'LOGISTICS_ASSIGNEE_RESPONSE',
  LOGISTICS_NEGOTIATION: 'LOGISTICS_NEGOTIATION',
  LOGISTICS_PROPOSAL_RESPONSE: 'LOGISTICS_PROPOSAL_RESPONSE',
  LOGISTICS_HANDOVER_SIGNATURE: 'LOGISTICS_HANDOVER_SIGNATURE',
} as const;

export const EMAIL_INTENDED_ACTIONS = {
  ACCEPT: 'ACCEPT',
  DECLINE: 'DECLINE',
  NEGOTIATE: 'NEGOTIATE',
  APPROVE_PROPOSAL: 'APPROVE_PROPOSAL',
  REJECT_PROPOSAL: 'REJECT_PROPOSAL',
  CONFIRM_BORROW: 'CONFIRM_BORROW',
  CONFIRM_RETURN: 'CONFIRM_RETURN',
} as const;

export const EMAIL_ACTION_RESULT_STATUSES = {
  PENDING: 'PENDING',
  SUCCESS: 'SUCCESS',
  ALREADY_RESPONDED: 'ALREADY_RESPONDED',
  EXPIRED: 'EXPIRED',
  INVALID: 'INVALID',
  FAILED: 'FAILED',
} as const;
