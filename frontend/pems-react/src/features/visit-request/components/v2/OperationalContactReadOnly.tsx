import { useTranslation } from 'react-i18next';

import { formatVietnamDateTime } from '../../../../shared/utils/vietnamTime';
import { PartnerSystemBadge } from '../../../../shared/components/PartnerSystemBadge';
import type { ResolvedOperationalContact } from '../../api/visitRequestV2Api';

interface Props {
  /** The contact of ONE campus. Never a sibling's, never a request-level stand-in. */
  contact: ResolvedOperationalContact;
  /** Scopes every test id so a two-campus screen has two addressable blocks, not one. */
  visitInstanceId: number;
  /** Show the confirmation SOURCE (self-match / email / transfer). Off for guest-facing screens. */
  showSource?: boolean;
  className?: string;
  /**
   * Rendered immediately after the title text, same line, tight gap (e.g. the profile-mismatch
   * indicator icon). Kept separate from `headerAction` so a caller can put something right next to
   * the words "Đầu mối đoàn khách phối hợp tại cơ sở" while a second, unrelated action (the identity-
   * change trigger) still lands on the far right of the row via `headerAction`.
   */
  titleTrailing?: React.ReactNode;
  /**
   * Rendered beside the title, on the same row (e.g. the "Thay đầu mối" / "Chuyển đầu mối" trigger).
   * The caller owns whatever it puts here — this component only reserves the row and wraps it so a
   * narrow viewport drops the action to its own line instead of overflowing.
   */
  headerAction?: React.ReactNode;
  /**
   * Rendered inside this SAME bordered card, below the read-only fields (e.g. `ContactIdentityActions`'
   * pending-invitation summary and resend/cancel actions). Kept as a slot rather than a second card so
   * the whole contact workflow — info, status, in-flight action — reads as one block instead of a card
   * with a loose panel floating below it.
   */
  children?: React.ReactNode;
}

/**
 * "Đầu mối đoàn khách phối hợp tại cơ sở" — read-only, per campus.
 *
 * Deliberately dumb: it fetches nothing, reads no role, consults no localStorage and derives no
 * permission. It renders the campus object it is handed. Everything it could have decided for itself
 * is a decision that belongs to the backend, and every past bug in this area came from a component
 * deciding one of them locally.
 *
 * It also renders while the invitation is still outstanding. Hiding the block until `fullName`
 * arrives was the old behaviour and it hid the two facts a reader actually needs then — the address
 * the invitation went to, and that it has not been answered.
 */
export function OperationalContactReadOnly({
  contact,
  visitInstanceId,
  showSource = false,
  className = '',
  titleTrailing,
  headerAction,
  children,
}: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const tid = (suffix: string) => `operational-contact-${visitInstanceId}-${suffix}`;

  const hasAnything =
    contact.fullName || contact.organization || contact.jobTitle || contact.phone || contact.email;

  return (
    <section
      className={`rounded-lg border border-slate-200 bg-white p-4 ${className}`}
      data-testid={tid('block')}
      data-visit-instance-id={visitInstanceId}
    >
      {/* flex-wrap + justify-between: title and action share the row on desktop; on a narrow
          viewport the action drops to its own line under the title instead of overflowing. */}
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-1.5">
          <h4 className="text-sm font-bold text-slate-800">
            {t('visitRequestV2:operationalContact.title')}
          </h4>
          {titleTrailing}
        </div>
        {headerAction}
      </div>

      {!hasAnything ? (
        <p className="text-sm text-slate-500" data-testid={tid('empty')}>
          {t('visitRequestV2:operationalContact.unavailable')}
        </p>
      ) : (
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
          <Field
            label={t('visitRequestV2:operationalContact.fullName')}
            value={contact.fullName}
            testId={tid('full-name')}
          />
          <div className="min-w-0">
            <dt className="text-xs font-medium text-slate-500">
              {t('visitRequestV2:operationalContact.organization')}
            </dt>
            <dd className="break-words text-sm text-slate-900" data-testid={tid('organization')}>
              {contact.organization && contact.organization.trim().length > 0 ? contact.organization : '—'}
              {contact.isOrganizationInSystem && (
                <PartnerSystemBadge
                  strength="light"
                  label={t('visitRequestV2:operationalContact.organizationInSystem')}
                  data-testid={tid('organization-partner-badge')}
                />
              )}
            </dd>
          </div>
          <Field
            label={t('visitRequestV2:operationalContact.jobTitle')}
            value={contact.jobTitle}
            testId={tid('job-title')}
          />
          <Field
            label={t('visitRequestV2:operationalContact.phone')}
            value={contact.phone}
            testId={tid('phone')}
          />
          <Field
            label={t('visitRequestV2:operationalContact.email')}
            value={contact.email}
            testId={tid('email')}
          />
          <Field
            label={t('visitRequestV2:operationalContact.confirmationStatus')}
            value={confirmationLabel(contact.confirmationStatus, t)}
            testId={tid('confirmation-status')}
          />
          {showSource && contact.confirmationSource ? (
            <Field
              label={t('visitRequestV2:operationalContact.confirmationSource')}
              value={sourceLabel(contact.confirmationSource, t)}
              testId={tid('confirmation-source')}
            />
          ) : null}
          {contact.confirmedAt ? (
            <Field
              label={t('visitRequestV2:operationalContact.confirmedAt')}
              value={formatVietnamDateTime(contact.confirmedAt)}
              testId={tid('confirmed-at')}
            />
          ) : null}
        </dl>
      )}

      {children}
    </section>
  );
}

/** Each field is its own label/value pair — never one joined string, which is unreadable and unsearchable. */
function Field({
  label,
  value,
  testId,
}: {
  label: string;
  value: string | null | undefined;
  testId: string;
}) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-medium text-slate-500">{label}</dt>
      <dd className="break-words text-sm text-slate-900" data-testid={testId}>
        {value && value.trim().length > 0 ? value : '—'}
      </dd>
    </div>
  );
}

/** Never render a raw enum: the reader is a person, and PENDING is not Vietnamese. */
function confirmationLabel(status: string, t: (k: string) => string): string {
  switch (status) {
    case 'CONFIRMED':
      return t('visitRequestV2:operationalContact.status.confirmed');
    case 'DECLINED':
      return t('visitRequestV2:operationalContact.status.declined');
    case 'EXPIRED':
      return t('visitRequestV2:operationalContact.status.expired');
    case 'TRANSFER_PENDING':
      return t('visitRequestV2:operationalContact.status.transferPending');
    default:
      return t('visitRequestV2:operationalContact.status.pending');
  }
}

function sourceLabel(source: string, t: (k: string) => string): string {
  switch (source) {
    case 'REGISTRANT_SELF_MATCH':
      return t('visitRequestV2:operationalContact.source.registrantSelfMatch');
    case 'TRANSFER':
      return t('visitRequestV2:operationalContact.source.transfer');
    default:
      return t('visitRequestV2:operationalContact.source.emailConfirmation');
  }
}

export default OperationalContactReadOnly;
