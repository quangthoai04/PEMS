import { useCallback, useEffect, useState } from 'react';
import { AlertCircle, Loader2, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  cancelContactTransfer,
  getActiveContactTransfer,
  initiateContactTransfer,
  replacePendingContact,
  resendContactClaim,
  resendContactTransfer,
  type ContactTransferState,
} from '../api/visitRequestV2Api';
import { getApiErrorMessage, showErrorToast, showSuccessToast } from '../../../shared/utils/toast';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';

interface Props {
  visitRequestId: number;
  /** ACTIVE | PENDING_CONFIRMATION — drives which workflow (claim vs transfer) is offered. */
  primaryContactAccessStatus: string;
  /** Masked contact email for display (never the full address of someone else). */
  contactEmailMasked?: string | null;
  /** Whether the CURRENT user may manage the invitation (registrant or ACTIVE contact). */
  canManage: boolean;
  onChanged?: () => void;
}

interface ContactFormState {
  fullName: string;
  organization: string;
  phone: string;
  email: string;
  reason: string;
}

const emptyForm: ContactFormState = { fullName: '', organization: '', phone: '', email: '', reason: '' };

/**
 * The contact-role workflow, rendered INSIDE the contact section rather than as its own card.
 *
 * While the contact is PENDING_CONFIRMATION: resend the INITIAL_CLAIM (72h) or replace the invited
 * email (typo fix). Once ACTIVE: propose a 24h TRANSFER — the current owner keeps every right until
 * the invited person explicitly accepts; the panel shows/cancels/resends the pending transfer.
 *
 * Feedback follows the house rule: the outcome of a mutation is a toast (the panel may be scrolled
 * out of view by the time the server answers), while a failure to LOAD the transfer state stays
 * inline with a retry — it used to be swallowed into "no pending transfer", which silently hid a
 * transfer that really was in flight and invited the user to start a second one.
 */
export default function ContactIdentityActions({
  visitRequestId,
  primaryContactAccessStatus,
  contactEmailMasked,
  canManage,
  onChanged,
}: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const isPending = primaryContactAccessStatus === 'PENDING_CONFIRMATION';
  const [transfer, setTransfer] = useState<ContactTransferState | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [showForm, setShowForm] = useState<'replace' | 'transfer' | null>(null);
  const [form, setForm] = useState<ContactFormState>(emptyForm);

  const refreshTransfer = useCallback(async () => {
    if (isPending || !canManage) return;
    setLoading(true);
    setLoadError(false);
    try {
      setTransfer(await getActiveContactTransfer(visitRequestId));
    } catch {
      // NOT "no transfer" — we simply do not know, and saying "none" would be a guess that leads the
      // user into starting a duplicate transfer.
      setTransfer(null);
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  }, [visitRequestId, isPending, canManage]);

  useEffect(() => {
    void refreshTransfer();
  }, [refreshTransfer]);

  if (!canManage) return null;

  const run = async (fn: () => Promise<{ message: string }>) => {
    setBusy(true);
    try {
      const result = await fn();
      showSuccessToast(result.message);
      setShowForm(null);
      setForm(emptyForm);
      await refreshTransfer();
      onChanged?.();
    } catch (err: unknown) {
      showErrorToast(getApiErrorMessage(err, t('visitRequestV2:contact.actionFailed')));
    } finally {
      setBusy(false);
    }
  };

  const fieldLabel = (field: keyof ContactFormState) =>
    field === 'fullName' ? t('visitRequestV2:person.fullName')
      : field === 'organization' ? t('visitRequestV2:person.organization')
        : field === 'phone' ? t('visitRequestV2:card.phone')
          : t('visitRequestV2:card.email');

  const contactForm = (mode: 'replace' | 'transfer') => (
    <form
      className="mt-3 space-y-2"
      onSubmit={e => {
        e.preventDefault();
        void run(() =>
          mode === 'replace'
            ? replacePendingContact(visitRequestId, {
                fullName: form.fullName,
                organization: form.organization,
                phone: form.phone,
                email: form.email,
              })
            : initiateContactTransfer(visitRequestId, {
                fullName: form.fullName,
                organization: form.organization,
                phone: form.phone,
                email: form.email,
                reason: form.reason || undefined,
              }),
        );
      }}
    >
      {(['fullName', 'organization', 'phone', 'email'] as const).map(field => (
        <div key={field}>
          <label htmlFor={`ci-${field}`} className="block text-xs font-semibold text-slate-500">
            {fieldLabel(field)}
            {field !== 'organization' && <span className="text-red-500"> *</span>}
          </label>
          <input
            id={`ci-${field}`}
            type={field === 'email' ? 'email' : 'text'}
            required={field !== 'organization'}
            maxLength={field === 'organization' ? 200 : 150}
            className="mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-sm"
            value={form[field]}
            onChange={e => setForm(f => ({ ...f, [field]: e.target.value }))}
          />
        </div>
      ))}
      {mode === 'transfer' && (
        <div>
          <label htmlFor="ci-reason" className="block text-xs font-semibold text-slate-500">
            {t('visitRequestV2:contact.transferReason')}
          </label>
          <input
            id="ci-reason"
            maxLength={500}
            className="mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-sm"
            value={form.reason}
            onChange={e => setForm(f => ({ ...f, reason: e.target.value }))}
          />
        </div>
      )}
      {mode === 'transfer' && (
        <p className="text-xs text-amber-700" role="note">
          {t('visitRequestV2:contact.transferKeepsRights')}
        </p>
      )}
      <div className="flex gap-2 pt-1">
        <button
          type="submit"
          disabled={busy}
          data-testid="contact-form-submit"
          className="rounded-lg bg-[#f37021] px-3 py-1.5 text-sm font-bold text-white hover:bg-[#e0631a] disabled:opacity-50"
        >
          {mode === 'replace' ? t('visitRequestV2:contact.replaceSubmit') : t('visitRequestV2:contact.transferSubmit')}
        </button>
        <button
          type="button"
          className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold"
          onClick={() => setShowForm(null)}
        >
          {t('visitRequestV2:common.cancel')}
        </button>
      </div>
    </form>
  );

  return (
    <div data-testid="contact-identity-actions" className="mt-4 border-t border-slate-200 pt-4">
      <p className="text-xs font-bold uppercase tracking-wide text-[#004c91]">
        {t('visitRequestV2:contact.manageTitle')}
      </p>

      {loading && (
        <p role="status" className="mt-2 flex items-center gap-2 text-sm text-slate-500">
          <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> {t('visitRequestV2:contact.loadingTransfer')}
        </p>
      )}

      {loadError && (
        <div role="alert" className="mt-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          <div className="flex items-start gap-2">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
            <p>{t('visitRequestV2:contact.transferLoadFailed')}</p>
          </div>
          <button
            type="button"
            data-testid="contact-transfer-retry"
            onClick={() => void refreshTransfer()}
            className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-sm font-bold text-amber-800 hover:bg-amber-100"
          >
            <RefreshCw className="h-4 w-4" aria-hidden /> {t('visitRequestV2:detail.retry')}
          </button>
        </div>
      )}

      {isPending ? (
        <>
          <p className="mt-2 text-sm text-amber-700">
            {t('visitRequestV2:contact.pendingNotice', { email: contactEmailMasked ?? '' })}
          </p>
          {showForm === 'replace' ? (
            contactForm('replace')
          ) : (
            <div className="mt-3 flex flex-wrap gap-2">
              <button
                type="button"
                disabled={busy}
                data-testid="contact-resend-claim"
                className="rounded-lg bg-[#f37021] px-3 py-1.5 text-sm font-bold text-white hover:bg-[#e0631a] disabled:opacity-50"
                onClick={() => void run(() => resendContactClaim(visitRequestId))}
              >
                {t('visitRequestV2:contact.resendInvitation')}
              </button>
              <button
                type="button"
                data-testid="contact-replace-open"
                className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                onClick={() => setShowForm('replace')}
              >
                {t('visitRequestV2:contact.replaceOpen')}
              </button>
            </div>
          )}
        </>
      ) : transfer?.hasPendingTransfer ? (
        <>
          <p className="mt-2 text-sm text-amber-700">
            {t('visitRequestV2:contact.transferPending', {
              email: transfer.newEmailMasked ?? '',
              expiresAt: transfer.expiresAt ? formatVietnamDateTime(transfer.expiresAt) : '',
            })}
          </p>
          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              disabled={busy}
              data-testid="contact-resend-transfer"
              className="rounded-lg bg-[#f37021] px-3 py-1.5 text-sm font-bold text-white hover:bg-[#e0631a] disabled:opacity-50"
              onClick={() => void run(() => resendContactTransfer(visitRequestId))}
            >
              {t('visitRequestV2:contact.resendInvitation')}
            </button>
            <button
              type="button"
              disabled={busy}
              data-testid="contact-cancel-transfer"
              className="rounded-lg border border-red-300 px-3 py-1.5 text-sm font-semibold text-red-600 hover:bg-red-50"
              onClick={() => void run(() => cancelContactTransfer(visitRequestId))}
            >
              {t('visitRequestV2:contact.cancelTransfer')}
            </button>
          </div>
        </>
      ) : (
        <>
          <p className="mt-2 text-sm text-slate-600">{t('visitRequestV2:contact.activeNotice')}</p>
          {showForm === 'transfer' ? (
            contactForm('transfer')
          ) : (
            <button
              type="button"
              data-testid="contact-transfer-open"
              className="mt-3 rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold text-slate-700 hover:bg-slate-50"
              onClick={() => setShowForm('transfer')}
            >
              {t('visitRequestV2:contact.transferOpen')}
            </button>
          )}
        </>
      )}
    </div>
  );
}
