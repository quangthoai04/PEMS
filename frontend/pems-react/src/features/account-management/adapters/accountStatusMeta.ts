/**
 * Presentation metadata for an account status (UC-98 detail modal, spec §11.3/§11.7).
 *
 * The label/colour pair lives here — not inline in the page — so the detail badge and any future
 * consumer read the SAME mapping. The status itself is never edited from the detail modal; changing
 * it stays with the dedicated enable/disable + lock actions on the list.
 */

export type AccountStatusMeta = {
  label: string;
  className: string;
};

/** The four statuses the backend can return for an account (users.status). */
export const ACCOUNT_STATUS_META: Record<string, AccountStatusMeta> = {
  ACTIVE: {
    label: 'Hoạt động',
    className: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  },
  INACTIVE: {
    label: 'Vô hiệu hóa',
    className: 'bg-amber-50 text-amber-700 border-amber-200',
  },
  LOCKED: {
    label: 'Bị khóa',
    className: 'bg-red-50 text-red-700 border-red-200',
  },
  PENDING_EMAIL_CONFIRMATION: {
    label: 'Chờ xác nhận email',
    className: 'bg-sky-50 text-sky-700 border-sky-200',
  },
};

/** Neutral styling for a status this frontend does not know about yet. */
const UNKNOWN_STATUS_CLASS = 'bg-slate-50 text-slate-700 border-slate-200';

/**
 * Resolves the badge for a detail view.
 *
 * `rawStatus` is the UC-98 detail projection (`details.status`) and always wins: the row in the list
 * may be a stale snapshot from before the drawer was opened. The list value is only a fallback for
 * the moment before the detail request resolves.
 *
 * An unrecognised status is never dropped and never crashes — it is shown verbatim (uppercased) so
 * the operator sees what the server actually said; only a blank value degrades to "Không xác định".
 */
export function resolveAccountStatusMeta(
  rawStatus?: string | null,
  listStatus?: string | null,
): AccountStatusMeta {
  const normalized = String(rawStatus ?? listStatus ?? '').trim().toUpperCase();
  return (
    ACCOUNT_STATUS_META[normalized] ?? {
      label: normalized || 'Không xác định',
      className: UNKNOWN_STATUS_CLASS,
    }
  );
}
