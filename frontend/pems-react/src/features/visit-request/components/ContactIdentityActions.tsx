import { useCallback, useEffect, useMemo, useState } from 'react';
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
import { hasAction, VisitV2Action } from '../utils/visitV2Actions';
import { getApiErrorMessage, showErrorToast, showSuccessToast } from '../../../shared/utils/toast';
import { isSameEmailIdentity } from '../../../shared/utils/emailIdentity';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { AutoGrowTextarea } from './shared/AutoGrowTextarea';

interface Props {
  visitRequestId: number;
  /** ACTIVE | PENDING_CONFIRMATION — describes the contact's state to the reader. */
  primaryContactAccessStatus: string;
  /** The contact's email: shown in the state line, and compared to block a same-address transfer. */
  contactEmail?: string | null;
  /**
   * Request-level `viewer.allowedActions` from the backend. Each button is rendered ONLY when its own
   * code is present — never from role, relation or status. The five codes mirror the guards in the
   * claim/transfer handlers (actor, lifecycle window, resend cap, one-pending-change), so the panel no
   * longer offers a resend past its cap or a transfer the backend would refuse.
   */
  allowedActions: string[] | undefined;
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

/** Mirrors the FluentValidation limits on both commands — the backend stays the authority. */
const MAX = { fullName: 150, organization: 200, phone: 50, email: 150, reason: 500 } as const;

const labelCls = 'block text-xs font-semibold text-slate-500';
const fieldCls =
  'mt-1 h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none ' +
  'transition-colors focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91]';

/**
 * The contact-role workflow, rendered INSIDE the contact section rather than as its own card.
 *
 * While the contact is PENDING_CONFIRMATION: resend the INITIAL_CLAIM (72h) or replace the invited
 * email (typo fix). Once ACTIVE: propose a 24h TRANSFER — the current owner keeps every right until
 * the invited person explicitly accepts; the panel shows/cancels/resends the pending transfer.
 *
 * The form stays closed until asked for and then opens inline, two columns on desktop and one on
 * mobile: five stacked full-width rows turned a small handover into a page of its own.
 *
 * Feedback follows the house rule: the outcome of a mutation is a toast (the panel may be scrolled
 * out of view by the time the server answers), while a failure to LOAD the transfer state stays
 * inline with a retry — it used to be swallowed into "no pending transfer", which silently hid a
 * transfer that really was in flight and invited the user to start a second one.
 */
export default function ContactIdentityActions({
  visitRequestId,
  primaryContactAccessStatus,
  contactEmail,
  allowedActions,
  onChanged,
}: Props) {
  const { t } = useTranslation(['visitRequestV2', 'validation']);
  const isPending = primaryContactAccessStatus === 'PENDING_CONFIRMATION';

  const can = useMemo(() => ({
    resendClaim: hasAction(allowedActions, VisitV2Action.ResendContactClaim),
    replaceContact: hasAction(allowedActions, VisitV2Action.ReplacePendingContact),
    initiateTransfer: hasAction(allowedActions, VisitV2Action.InitiateContactTransfer),
    resendTransfer: hasAction(allowedActions, VisitV2Action.ResendContactTransfer),
    cancelTransfer: hasAction(allowedActions, VisitV2Action.CancelContactTransfer),
  }), [allowedActions]);
  const hasAnyAction = Object.values(can).some(Boolean);

  const [transfer, setTransfer] = useState<ContactTransferState | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [showForm, setShowForm] = useState<'replace' | 'transfer' | null>(null);
  const [form, setForm] = useState<ContactFormState>(emptyForm);
  const [emailError, setEmailError] = useState<string | null>(null);

  // Only worth asking when a transfer action is on the table at all.
  const transferRelevant = can.resendTransfer || can.cancelTransfer || can.initiateTransfer;
  const refreshTransfer = useCallback(async () => {
    if (!transferRelevant) return;
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
  }, [visitRequestId, transferRelevant]);

  useEffect(() => {
    void refreshTransfer();
  }, [refreshTransfer]);

  if (!hasAnyAction) return null;

  const closeForm = () => {
    // Only this form's own scratch data — the visit-request draft is a different thing entirely.
    setShowForm(null);
    setForm(emptyForm);
    setEmailError(null);
  };

  const run = async (fn: () => Promise<{ message: string }>) => {
    if (busy) return; // a second click while the first is in flight would send the invitation twice
    setBusy(true);
    try {
      const result = await fn();
      showSuccessToast(result.message);
      closeForm();
      await refreshTransfer();
      onChanged?.();
    } catch (err: unknown) {
      showErrorToast(getApiErrorMessage(err, t('visitRequestV2:contact.actionFailed')));
    } finally {
      setBusy(false);
    }
  };

  const submitForm = (mode: 'replace' | 'transfer') => {
    // Transferring to the address that already holds the role is refused by the backend
    // (EMAIL_UNCHANGED). Saying so here costs one comparison and saves a round trip.
    if (mode === 'transfer' && isSameEmailIdentity(form.email, contactEmail)) {
      setEmailError(t('visitRequestV2:contact.transferEmailUnchanged'));
      return;
    }
    setEmailError(null);
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
  };

  const textField = (
    field: 'fullName' | 'organization' | 'phone' | 'email',
    label: string,
    required: boolean,
  ) => (
    <div>
      <label htmlFor={`ci-${field}`} className={labelCls}>
        {label}
        {required && <span className="text-red-500"> *</span>}
      </label>
      <input
        id={`ci-${field}`}
        type={field === 'email' ? 'email' : field === 'phone' ? 'tel' : 'text'}
        inputMode={field === 'phone' ? 'tel' : undefined}
        placeholder={field === 'phone' ? '0912345678' : undefined}
        required={required}
        maxLength={MAX[field]}
        className={fieldCls}
        value={form[field]}
        onChange={e => {
          const { value } = e.target;
          setForm(f => ({ ...f, [field]: value }));
          if (field === 'email') setEmailError(null);
        }}
        aria-invalid={field === 'email' && emailError ? true : undefined}
        aria-describedby={
          field === 'email' && emailError ? 'ci-email-error'
            : field === 'phone' ? 'ci-phone-hint'
              : undefined
        }
      />
      {/* The accepted shapes, stated up front. This form is server-validated, so the alternative is
          a round trip that comes back saying only "không hợp lệ" (plan §18). */}
      {field === 'phone' && (
        <p id="ci-phone-hint" className="mt-1 text-xs font-medium text-slate-500">
          {t('validation:phoneHint')}
        </p>
      )}
      {field === 'email' && emailError && (
        <p id="ci-email-error" role="alert" className="mt-1 text-xs font-semibold text-red-600">
          {emailError}
        </p>
      )}
    </div>
  );

  const contactForm = (mode: 'replace' | 'transfer') => (
    <form
      className="mt-3 max-w-4xl"
      data-testid={`contact-form-${mode}`}
      onSubmit={e => {
        e.preventDefault();
        submitForm(mode);
      }}
    >
      {/* Two columns from the tablet breakpoint up, one below it — no horizontal scroll on mobile. */}
      <div className="grid grid-cols-1 gap-x-6 gap-y-4 md:grid-cols-2" data-testid="contact-form-grid">
        {textField('fullName', t('visitRequestV2:person.fullName'), true)}
        {textField('organization', t('visitRequestV2:person.organization'), false)}
        {textField('phone', t('visitRequestV2:card.phone'), true)}
        {textField('email', t('visitRequestV2:card.email'), true)}

        {mode === 'transfer' && (
          <div className="md:col-span-2" data-testid="contact-form-reason">
            <label htmlFor="ci-reason" className={labelCls}>
              {t('visitRequestV2:contact.transferReason')}
            </label>
            <div className="mt-1">
              <AutoGrowTextarea
                id="ci-reason"
                minRows={2}
                maxLength={MAX.reason}
                value={form.reason}
                onChange={value => setForm(f => ({ ...f, reason: value }))}
              />
            </div>
          </div>
        )}
      </div>

      {mode === 'transfer' && (
        <p className="mt-3 text-xs text-amber-700" role="note">
          {t('visitRequestV2:contact.transferKeepsRights')}
        </p>
      )}

      <div className="mt-4 flex flex-col gap-2 sm:flex-row sm:justify-end">
        <button
          type="button"
          disabled={busy}
          data-testid="contact-form-cancel"
          className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          onClick={closeForm}
        >
          {t('visitRequestV2:common.cancel')}
        </button>
        <button
          type="submit"
          disabled={busy}
          data-testid="contact-form-submit"
          className="inline-flex items-center justify-center gap-2 rounded-lg bg-[#f37021] px-4 py-2 text-sm font-bold text-white hover:bg-[#e0631a] disabled:opacity-50"
        >
          {busy && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />}
          {busy
            ? t('visitRequestV2:contact.sending')
            : mode === 'replace'
              ? t('visitRequestV2:contact.replaceSubmit')
              : t('visitRequestV2:contact.transferSubmit')}
        </button>
      </div>
    </form>
  );

  const pendingTransfer = transfer?.hasPendingTransfer === true;

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

      {isPending && (
        <p className="mt-2 text-sm text-amber-700">
          {t('visitRequestV2:contact.pendingNotice', { email: contactEmail ?? '' })}
        </p>
      )}

      {pendingTransfer && (
        <p className="mt-2 text-sm text-amber-700">
          {t('visitRequestV2:contact.transferPending', {
            email: transfer?.newEmailMasked ?? '',
            expiresAt: transfer?.expiresAt ? formatVietnamDateTime(transfer.expiresAt) : '',
          })}
        </p>
      )}

      {!isPending && !pendingTransfer && !loadError && (
        <p className="mt-2 text-sm text-slate-600">{t('visitRequestV2:contact.activeNotice')}</p>
      )}

      {showForm ? (
        contactForm(showForm)
      ) : (
        <div className="mt-3 flex flex-wrap gap-2">
          {can.resendClaim && (
            <button
              type="button"
              disabled={busy}
              data-testid="contact-resend-claim"
              className="rounded-lg bg-[#f37021] px-3 py-1.5 text-sm font-bold text-white hover:bg-[#e0631a] disabled:opacity-50"
              onClick={() => void run(() => resendContactClaim(visitRequestId))}
            >
              {t('visitRequestV2:contact.resendInvitation')}
            </button>
          )}
          {can.replaceContact && (
            <button
              type="button"
              data-testid="contact-replace-open"
              className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold text-slate-700 hover:bg-slate-50"
              onClick={() => setShowForm('replace')}
            >
              {t('visitRequestV2:contact.replaceOpen')}
            </button>
          )}
          {can.resendTransfer && (
            <button
              type="button"
              disabled={busy}
              data-testid="contact-resend-transfer"
              className="rounded-lg bg-[#f37021] px-3 py-1.5 text-sm font-bold text-white hover:bg-[#e0631a] disabled:opacity-50"
              onClick={() => void run(() => resendContactTransfer(visitRequestId))}
            >
              {t('visitRequestV2:contact.resendInvitation')}
            </button>
          )}
          {can.cancelTransfer && (
            <button
              type="button"
              disabled={busy}
              data-testid="contact-cancel-transfer"
              className="rounded-lg border border-red-300 px-3 py-1.5 text-sm font-semibold text-red-600 hover:bg-red-50 disabled:opacity-50"
              onClick={() => void run(() => cancelContactTransfer(visitRequestId))}
            >
              {t('visitRequestV2:contact.cancelTransfer')}
            </button>
          )}
          {can.initiateTransfer && (
            <button
              type="button"
              data-testid="contact-transfer-open"
              className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold text-slate-700 hover:bg-slate-50"
              onClick={() => setShowForm('transfer')}
            >
              {t('visitRequestV2:contact.transferOpen')}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
