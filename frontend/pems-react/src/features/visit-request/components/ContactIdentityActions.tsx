import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertCircle, Loader2, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  cancelOperationalContactChange,
  getOperationalContactState,
  reinviteOperationalContactConfirmation,
  resendOperationalContactConfirmation,
  saveOperationalContact,
  type OperationalContactState,
  type ResolvedOperationalContact,
} from '../api/visitRequestV2Api';
import { errorCodeOf, fieldErrorsOf, firstFieldError, hasAction, VisitV2Action } from '../utils/visitV2Actions';
import ContactProfileSyncPrompt from './ContactProfileSyncPrompt';
import { showErrorToast, showMessageErrorToast, showSuccessToast } from '../../../shared/utils/toast';
import { isSameEmailIdentity } from '../../../shared/utils/emailIdentity';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { AutoGrowTextarea } from './shared/AutoGrowTextarea';
import { OrganizationCombobox } from './shared/OrganizationCombobox';
import { PhoneField } from './shared/PhoneField';
import { focusFirstInvalidField } from '../utils/formErrorNavigation';

interface Props {
  visitRequestId: number;
  /**
   * The campus this panel acts on. Every action below names it: a contact belongs to ONE campus, and
   * a request-level version of this panel is exactly how one person used to acquire authority over
   * campuses they were never invited to.
   */
  visitInstanceId: number;
  /** True once an account actually holds this campus — decides confirmation vs transfer actions. */
  contactConfirmed: boolean;
  /**
   * THIS campus's current contact snapshot. The form opens on it (plan §4), so a user correcting a
   * phone number is not asked to retype the other four fields — and retyping is how an address gets
   * "changed" by a typo and turns an ordinary correction into a confirmation email.
   */
  contact: ResolvedOperationalContact;
  /**
   * The campus instance's rowVersion as last read. Sent with a metadata-only save so a modal left open
   * while somebody else edited cannot overwrite the newer values.
   */
  rowVersion?: number;
  /**
   * The backend's verdict for THIS campus (`campusVisit.allowedActions`). Each control is rendered ONLY
   * when its own code is present — never from role, relation or status. The codes mirror the guards in
   * the contact handlers, so the panel cannot offer a resend past its cap, or any contact change on a
   * campus whose visit has already started.
   */
  allowedActions: string[] | undefined;
  onChanged?: () => void;
}

interface ContactFormState {
  fullName: string;
  organization: string;
  jobTitle: string;
  phone: string;
  email: string;
  reason: string;
}

/** Mirrors the FluentValidation limits on the commands — the backend stays the authority. */
const MAX = { fullName: 150, organization: 200, jobTitle: 150, phone: 50, email: 150, reason: 500 } as const;

/**
 * Per-field errors for the FOUR fields that are not the address (plan PEMS_VALIDATION_UX §2). Email
 * keeps its own dedicated `emailError` state below — it already carries identity-specific business
 * refusals (account inactive, cannot be used for a visitor account) that this generic map has no
 * concept of, and merging the two would either lose that nuance or duplicate it.
 */
type ContactFieldErrors = Partial<Record<'fullName' | 'organization' | 'jobTitle' | 'phone' | 'reason', string>>;

/**
 * Mã lỗi ổn định của backend → câu i18n cụ thể.
 *
 * Chỉ những trường hợp NGHIỆP VỤ đã biết mới nằm ở đây; lỗi lạ vẫn đi qua đường generic để không
 * che mất sự cố thật. Khoá theo mã, KHÔNG parse message — message có thể đổi, mã thì không.
 */
const CONTACT_ERROR_KEYS: Record<string, string> = {
  OPERATIONAL_CONTACT_CHANGE_CONFLICT: 'visitRequestV2:contact.errorChangeConflict',
  OPERATIONAL_CONTACT_ALREADY_CONFIRMED: 'visitRequestV2:contact.errorAlreadyConfirmed',
  OPERATIONAL_CONTACT_CONFIRMATION_NOT_FOUND: 'visitRequestV2:contact.errorInvitationNotFound',
  OPERATIONAL_CONTACT_CONFIRMATION_EXPIRED: 'visitRequestV2:contact.errorInvitationExpired',
  OPERATIONAL_CONTACT_CONFIRMATION_SUPERSEDED: 'visitRequestV2:contact.errorInvitationSuperseded',
  OPERATIONAL_CONTACT_CONFIRMATION_RATE_LIMITED: 'visitRequestV2:contact.errorRateLimited',
  OPERATIONAL_CONTACT_PROFILE_NO_CHANGES: 'visitRequestV2:contact.errorNoChanges',
  VISIT_INSTANCE_VERSION_CONFLICT: 'visitRequestV2:contact.errorVersionConflict',
};

/**
 * Lỗi thuộc về ĐỊA CHỈ vừa nhập, không phải về thao tác nói chung — nên hiện dưới ô Email chứ
 * không phải bằng toast.
 *
 * Cả ba đều bị từ chối TRƯỚC khi ghi bất cứ thứ gì (OperationalContactEligibility): cơ sở vẫn
 * nguyên đầu mối cũ, lời mời cũ, trạng thái cũ. Việc duy nhất cần làm là sửa lại email — mà một
 * toast tắt sau 3 giây thì bỏ người dùng lại trước đúng cái ô đó, không còn dấu vết gì.
 *
 * `OPERATIONAL_CONTACT_ACCOUNT_INACTIVE` cũng nằm ở đây (không còn trong bảng toast phía trên):
 * trong panel này nó chỉ đến từ nhánh chuyển giao kiểm tra tài khoản của email MỚI. Bản thể còn
 * lại của mã đó — người bấm link xác nhận có tài khoản đã khoá — xảy ra ở trang xác nhận công
 * khai, không phải màn này.
 */
const CONTACT_EMAIL_ERROR_KEYS: Record<string, string> = {
  // Dùng chung câu chữ với form tạo đơn public, để một người nhập nhầm email nội bộ ở hai chỗ
  // khác nhau nhận đúng một lời giải thích.
  CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT:
    'errors:api.CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT',
  VISITOR_ACCOUNT_INACTIVE: 'errors:api.VISITOR_ACCOUNT_INACTIVE',
  OPERATIONAL_CONTACT_ACCOUNT_INACTIVE: 'visitRequestV2:contact.errorAccountInactive',
};

const labelCls = 'block text-xs font-semibold text-slate-500';
const fieldCls = (hasError?: boolean) =>
  'mt-1 h-10 w-full rounded-lg border bg-white px-3 text-sm outline-none transition-colors ' +
  (hasError
    ? 'border-red-500 focus:border-red-500 focus:ring-1 focus:ring-red-300'
    : 'border-slate-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91]');

/**
 * The operational-contact management workflow for ONE campus, rendered INSIDE that campus's contact
 * section on the DETAIL screen. This is the only place a contact is managed: the request-edit form
 * shows the contact read-only and offers nothing, because editing a visit request and deciding who
 * runs a campus are two different acts with two different consequences.
 *
 * One form, five fields, one save. What the save MEANS is the server's to decide from the address:
 *
 * - same address → the details are corrected. Nothing is emailed, nothing is invited, nobody's
 *   authority moves, and an approved campus starting tomorrow is still allowed it.
 * - different address → somebody has to accept. Before the campus is decided that is a replace; after,
 *   a transfer in which the current contact keeps every right until the new person says yes.
 *
 * The panel does not classify the edit itself — it cannot know the stored address for certain, and a
 * wrong guess either emails a stranger about a typo or hands over a campus silently. It only warns,
 * before the user commits, which of the two they appear to be doing.
 */
export default function ContactIdentityActions({
  visitRequestId,
  visitInstanceId,
  contactConfirmed,
  contact,
  rowVersion,
  allowedActions,
  onChanged,
}: Props) {
  const { t } = useTranslation(['visitRequestV2', 'validation', 'errors']);
  const isPending = !contactConfirmed;
  const contactEmail = contact.email || null;

  const can = useMemo(() => ({
    edit: hasAction(allowedActions, VisitV2Action.UpdateContactProfile)
      || hasAction(allowedActions, VisitV2Action.ReplaceOperationalContact)
      || hasAction(allowedActions, VisitV2Action.InitiateContactTransfer),
    /** Whether changing the ADDRESS is on the table, as opposed to correcting the details only. */
    changeIdentity: hasAction(allowedActions, VisitV2Action.ReplaceOperationalContact)
      || hasAction(allowedActions, VisitV2Action.InitiateContactTransfer),
    /** After a decision, a new address is a handover rather than a correction — worth saying so. */
    transferOnly: !hasAction(allowedActions, VisitV2Action.ReplaceOperationalContact)
      && hasAction(allowedActions, VisitV2Action.InitiateContactTransfer),
    resend: hasAction(allowedActions, VisitV2Action.ResendContactConfirmation),
    /** Không còn lời mời nào sống — phải mở lời mời MỚI, không phải "gửi lại". */
    reinvite: hasAction(allowedActions, VisitV2Action.ReinviteContactConfirmation),
    cancelChange: hasAction(allowedActions, VisitV2Action.CancelContactChange),
  }), [allowedActions]);
  const hasAnyAction = can.edit || can.resend || can.reinvite || can.cancelChange;

  const [state, setState] = useState<OperationalContactState | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState<ContactFormState | null>(null);
  const [emailError, setEmailError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<ContactFieldErrors>({});
  // Hủy lời mời có HẬU QUẢ khác nhau tùy loại lời mời, và không hoàn tác được — nên hỏi trước,
  // với đúng câu mô tả hậu quả của loại đang chờ (approval-gate: không auto-pick im lặng).
  const [confirmCancel, setConfirmCancel] = useState(false);

  const refreshState = useCallback(async () => {
    if (!hasAnyAction) return;
    setLoading(true);
    setLoadError(false);
    try {
      setState(await getOperationalContactState(visitRequestId, visitInstanceId));
    } catch {
      // NOT "no pending change" — we simply do not know, and saying "none" would be a guess that leads
      // the user into starting a duplicate invitation.
      setState(null);
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  }, [visitRequestId, visitInstanceId, hasAnyAction]);

  useEffect(() => {
    void refreshState();
  }, [refreshState]);

  if (!hasAnyAction) return null;

  const openForm = () => {
    // Opens on what is stored (plan §4). Retyping four correct fields to fix a fifth is how a correct
    // address acquires a typo — and a typo in the address is not a correction, it is a handover.
    setForm({
      fullName: contact.fullName ?? '',
      organization: contact.organization ?? '',
      jobTitle: contact.jobTitle ?? '',
      phone: contact.phone ?? '',
      email: contact.email ?? '',
      reason: '',
    });
    setEmailError(null);
    setFieldErrors({});
    setShowForm(true);
  };

  const closeForm = () => {
    // Only this form's own scratch data — the visit-request draft is a different thing entirely.
    setShowForm(false);
    setForm(null);
    setEmailError(null);
    setFieldErrors({});
  };

  const run = async (fn: () => Promise<{ message: string }>) => {
    if (busy) return; // a second click while the first is in flight would send the invitation twice
    setBusy(true);
    try {
      const result = await fn();
      showSuccessToast(result.message);
      closeForm();
      await refreshState();
      onChanged?.();
    } catch (err: unknown) {
      // A stable per-field `errors` dict (FluentValidation, plan PEMS_VALIDATION_UX §2.3) beats any
      // toast: it names exactly which control is wrong. Tried FIRST and only while the form is open —
      // there is nowhere to attach a field error once it has closed.
      if (showForm) {
        const backendFields = fieldErrorsOf(err);
        if (backendFields) {
          const mapped: ContactFieldErrors = {};
          (['fullName', 'organization', 'jobTitle', 'phone', 'reason'] as const).forEach(key => {
            const msg = firstFieldError(backendFields, key);
            if (msg) mapped[key] = msg;
          });
          const emailMsg = firstFieldError(backendFields, 'email');
          if (Object.keys(mapped).length > 0 || emailMsg) {
            setFieldErrors(mapped);
            if (emailMsg) setEmailError(emailMsg);
            window.setTimeout(() => focusFirstInvalidField(), 60);
            return;
          }
          // A validation error whose fields we cannot map (e.g. `Reason` alone with a message this
          // component has no slot for) still falls through to the generic branches below rather than
          // being silently swallowed.
        }
      }

      // Các xung đột nghiệp vụ ĐÃ BIẾT phải nói đúng chuyện, không rơi về "Đã xảy ra lỗi. Vui lòng
      // thử lại." — câu đó vừa vô nghĩa (thử lại sẽ hỏng y hệt) vừa che mất việc thao tác có thể đã
      // thành công một phần. Map theo MÃ ổn định của backend, không parse message.
      //
      // Cả ba nhánh dưới đây đều dùng showMessageErrorToast / showErrorToast(err) cho ĐÚNG kiểu:
      // showErrorToast nhận LỖI rồi tự trích message. Đưa cho nó một chuỗi đã dựng sẵn thì chuỗi
      // ấy không có `response`/`message` nào để trích, và mọi lỗi của panel — kể cả những mã có
      // câu chữ riêng ngay trên kia — cùng rơi về "Đã xảy ra lỗi. Vui lòng thử lại."
      const code = errorCodeOf(err);

      const emailKey = code ? CONTACT_EMAIL_ERROR_KEYS[code] : undefined;
      if (emailKey) {
        // Form đang mở → gắn vào ô Email để sửa tại chỗ. Đóng rồi (bấm "Mời lại" chẳng hạn) thì
        // không có ô nào để gắn, lúc đó vẫn phải nói bằng toast. Không refresh: lời từ chối này
        // xảy ra trước mọi thay đổi, màn hình đang hiển thị đúng thực tế.
        if (showForm) setEmailError(t(emailKey));
        else showMessageErrorToast(t(emailKey));
        return;
      }

      const known = code ? CONTACT_ERROR_KEYS[code] : undefined;
      if (known) {
        showMessageErrorToast(t(known));
        // Trạng thái phía server đã khác với những gì màn hình đang hiển thị (đã có lời mời chờ, đã
        // có người xác nhận, lời mời đã bị thay...) — tải lại để người dùng thấy đúng thực tế thay
        // vì bấm lại vào một nút không còn hợp lệ.
        await refreshState();
        onChanged?.();
      } else {
        showErrorToast(err, t('visitRequestV2:contact.actionFailed'));
      }
    } finally {
      setBusy(false);
    }
  };

  /** True when the typed address is a DIFFERENT identity from the stored one (case/space-insensitive). */
  const identityChanging = form != null && !isSameEmailIdentity(form.email, contactEmail);

  /**
   * Whether the form still says exactly what it opened on.
   *
   * The server already refuses a no-op save with PROFILE_NO_CHANGES, and it should — but a user who
   * opened the panel, read it and pressed Lưu was getting that refusal as a red toast for doing
   * nothing wrong. Compared on the same terms the server uses: trimmed, and the address as an
   * identity rather than as a string, so a stray space is not "a change".
   */
  const unchanged =
    form != null
    && form.fullName.trim() === (contact.fullName ?? '').trim()
    && form.organization.trim() === (contact.organization ?? '').trim()
    && form.jobTitle.trim() === (contact.jobTitle ?? '').trim()
    && form.phone.trim() === (contact.phone ?? '').trim()
    && isSameEmailIdentity(form.email, contactEmail);

  /**
   * Client mirror of the backend's required/format rules (plan PEMS_VALIDATION_UX §2.2) — a UX aid
   * only, never a second source of truth: the server re-validates every field regardless, and a value
   * that slips past this still fails there. Phone stays optional here because it is optional on the
   * command (`MaximumLength` only, no `NotEmpty`); Organization is required, matching Create/Pending
   * Edit/Resubmit and the command's own `NotEmpty` rule.
   */
  const validateContactFields = (f: ContactFormState): { errors: ContactFieldErrors; email: string | null } => {
    const errors: ContactFieldErrors = {};
    if (!f.fullName.trim())
      errors.fullName = t('validation:requiredField', { field: t('visitRequestV2:person.fullName') });
    if (!f.organization.trim())
      errors.organization = t('validation:requiredField', { field: t('visitRequestV2:person.organization') });
    if (!f.jobTitle.trim())
      errors.jobTitle = t('validation:requiredField', { field: t('visitRequestV2:person.jobTitle') });
    let email: string | null = null;
    if (!f.email.trim()) {
      email = t('validation:requiredField', { field: t('visitRequestV2:card.email') });
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email.trim())) {
      email = t('validation:emailInvalidField', { field: t('visitRequestV2:card.email') });
    }
    return { errors, email };
  };

  const submitForm = () => {
    if (!form) return;
    // Nothing moved → close, and send nothing. A request whose only possible answer is "you changed
    // nothing" is a request not worth making.
    if (unchanged) {
      closeForm();
      return;
    }
    const { errors: nextFieldErrors, email: nextEmailError } = validateContactFields(form);
    if (Object.keys(nextFieldErrors).length > 0 || nextEmailError) {
      setFieldErrors(nextFieldErrors);
      setEmailError(nextEmailError);
      window.setTimeout(() => focusFirstInvalidField(), 60);
      return;
    }
    if (identityChanging && !can.changeIdentity) {
      setEmailError(t('visitRequestV2:contact.identityChangeNotAllowed'));
      return;
    }
    setEmailError(null);
    setFieldErrors({});
    void run(() =>
      saveOperationalContact(visitRequestId, visitInstanceId, {
        fullName: form.fullName,
        organization: form.organization,
        jobTitle: form.jobTitle,
        phone: form.phone,
        email: form.email,
        reason: form.reason || undefined,
        // Only meaningful on the metadata branch; the server ignores it on the other two.
        expectedRowVersion: rowVersion,
      }),
    );
  };

  const textField = (
    field: 'fullName' | 'organization' | 'jobTitle' | 'phone' | 'email',
    label: string,
    required: boolean,
  ) => {
    // Email keeps its own dedicated state (identity-specific business refusals); the other four share
    // the generic `fieldErrors` map. Same rendering contract either way.
    const genericError = field === 'email' ? undefined : fieldErrors[field];
    const hasError = field === 'email' ? !!emailError : !!genericError;
    const errorId = field === 'email' ? 'ci-email-error' : `ci-${field}-error`;
    // A change of value for a non-email field: shared by the plain `<input>` path below AND the two
    // control-specific branches (Organization/Phone), so all three clear their own error the same way.
    const commit = (value: string) => {
      setForm(f => (f ? { ...f, [field]: value } : f));
      if (genericError && value.trim()) {
        setFieldErrors(prev => ({ ...prev, [field]: undefined }));
      }
    };
    // Phone's own hint is PhoneField's built-in, focus-conditional one (below) — nothing here needs to
    // point at it. Email keeps its static hint id, exactly as before.
    const ariaDescribedBy = hasError ? errorId : field === 'email' ? 'ci-email-hint' : undefined;
    return (
    <div data-field-error={hasError ? 'true' : undefined}>
      <label htmlFor={`ci-${field}`} className={labelCls}>
        {label}
        {required && <span className="text-red-500"> *</span>}
      </label>
      {field === 'organization' ? (
        // Same shared control as Create/Manage's Operational Contact organization field
        // (CampusVisitCard.tsx) — async search + free-solo text, REQUEST_FORM policy (the component's
        // default). No `partnerId`: the Operational Contact snapshot has no partner-link column, so — like
        // that same call site — the id half of the callback is simply not wired up.
        <OrganizationCombobox
          inputId={`ci-${field}`}
          testId={`contact-field-${field}`}
          ariaLabel={label}
          placeholder={label}
          value={form?.organization ?? ''}
          hasError={hasError}
          onChange={value => commit(value)}
        />
      ) : field === 'phone' ? (
        // Same shared control as every other editable phone field in Visit V2 (Create/Edit registrant,
        // per-campus operational contact) — same hint copy, same format rule.
        <PhoneField
          testId={`contact-field-${field}`}
          hasError={hasError}
          error={genericError}
          field={{
            id: `ci-${field}`,
            value: form?.phone ?? '',
            maxLength: MAX.phone,
            onChange: e => commit(e.target.value),
            'aria-invalid': hasError ? true : undefined,
            'aria-describedby': ariaDescribedBy,
          }}
        />
      ) : (
      <input
        id={`ci-${field}`}
        data-testid={`contact-field-${field}`}
        // `type="text"` even for email: `type="email"` runs the browser's OWN native format
        // check on submit — for exactly the same reason `required` was dropped above, a malformed
        // address would be blocked by an unstyled native tooltip before this component's own message
        // ever rendered. `inputMode` still gives mobile keyboards the right layout.
        type="text"
        inputMode={field === 'email' ? 'email' : undefined}
        // Deliberately NOT the native `required` attribute: the browser's own constraint validation
        // would intercept Submit before `validateContactFields`/the field-error state ever run,
        // surfacing an unstyled, non-localized tooltip instead of the inline message below. The `*`
        // marker still says the field is required; enforcement is entirely this component's own.
        maxLength={MAX[field]}
        className={fieldCls(hasError)}
        value={form?.[field] ?? ''}
        onChange={e => {
          const { value } = e.target;
          commit(value);
          if (field === 'email') setEmailError(null);
        }}
        aria-invalid={hasError ? true : undefined}
        aria-describedby={ariaDescribedBy}
      />
      )}
      {genericError && (
        <p id={errorId} role="alert" className="mt-1 text-xs font-semibold text-red-600">
          {genericError}
        </p>
      )}
      {/* The accepted shapes, stated up front (plan §18) — PhoneField's own hint, shown while
          focused and error-free, same copy every other Phone field in Visit V2 uses. */}
      {/* Says what the address DOES before it is changed, not after. Once the save has gone through,
          an invitation is already out and "did you mean to do that?" is too late to be useful. */}
      {field === 'email' && !emailError && (
        <p id="ci-email-hint" className="mt-1 text-xs font-medium text-slate-500">
          {t('visitRequestV2:contact.emailIdentityHint')}
        </p>
      )}
      {field === 'email' && emailError && (
        <p id="ci-email-error" role="alert" className="mt-1 text-xs font-semibold text-red-600">
          {emailError}
        </p>
      )}
    </div>
    );
  };

  const contactForm = (
    <form
      className="mt-3 max-w-4xl"
      data-testid="contact-form"
      onSubmit={e => {
        e.preventDefault();
        submitForm();
      }}
    >
      {/* Two columns from the tablet breakpoint up, one below it — no horizontal scroll on mobile. */}
      <div className="grid grid-cols-1 gap-x-6 gap-y-4 md:grid-cols-2" data-testid="contact-form-grid">
        {textField('fullName', t('visitRequestV2:person.fullName'), true)}
        {textField('organization', t('visitRequestV2:person.organization'), true)}
        {textField('jobTitle', t('visitRequestV2:person.jobTitle'), true)}
        {textField('phone', t('visitRequestV2:card.phone'), false)}
        {textField('email', t('visitRequestV2:card.email'), true)}
      </div>

      {/* The reason travels with a handover, so it is asked for only when the address has actually
          changed on a decided campus — which is the only branch that stores it. */}
      {identityChanging && can.transferOnly && (
        <div className="mt-4 max-w-2xl" data-testid="contact-form-reason">
          <label htmlFor="ci-reason" className={labelCls}>
            {t('visitRequestV2:contact.transferReason')}
          </label>
          <div className="mt-1">
            <AutoGrowTextarea
              id="ci-reason"
              minRows={2}
              maxLength={MAX.reason}
              value={form?.reason ?? ''}
              onChange={value => setForm(f => (f ? { ...f, reason: value } : f))}
            />
          </div>
        </div>
      )}

      {identityChanging && (
        <p
          className="mt-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs font-semibold text-amber-800"
          role="note"
          data-testid="contact-identity-warning"
        >
          {can.transferOnly
            ? t('visitRequestV2:contact.transferKeepsRights')
            : t('visitRequestV2:contact.replaceReopensGate')}
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
          // Disabled while nothing has moved: the alternative is a button that looks live, sends a
          // request and comes back with an error for a form the user did not touch.
          disabled={busy || unchanged}
          title={unchanged ? t('visitRequestV2:contact.noChangesHint') : undefined}
          data-testid="contact-form-submit"
          className="inline-flex items-center justify-center gap-2 rounded-lg bg-[#f37021] px-4 py-2 text-sm font-bold text-white hover:bg-[#e0631a] disabled:opacity-50"
        >
          {busy && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />}
          {busy
            ? t('visitRequestV2:contact.sending')
            : identityChanging
              ? t('visitRequestV2:contact.saveIdentityChange')
              : t('visitRequestV2:contact.saveProfile')}
        </button>
      </div>
    </form>
  );

  const pendingTransfer = state?.pendingChangeKind === 'TRANSFER';
  const pendingLive = state?.pendingChangeStatus === 'PENDING';

  /**
   * WHICH invitation is in flight, so the cancel button says what it cancels.
   *
   * A campus whose contact has never confirmed is waiting on an INITIAL_CONFIRMATION; only a campus
   * that already has a confirmed contact can be mid-TRANSFER. Calling both "hủy lời mời chuyển giao"
   * told the first group they were handing over a role nobody holds yet. If the state could not be
   * read, the contact's own confirmation status is the honest fallback — the same fact the server
   * derives the kind from.
   */
  const pendingKind = state?.pendingChangeKind ?? (contactConfirmed ? 'TRANSFER' : 'INITIAL_CONFIRMATION');
  const cancelLabel = pendingKind === 'TRANSFER'
    ? t('visitRequestV2:contact.cancelTransfer')
    : t('visitRequestV2:contact.cancelConfirmation');

  /**
   * Không có ai giữ cơ sở VÀ không có lời mời nào đang chờ.
   *
   * Đây là trạng thái sau khi hủy lời mời xác nhận, và nó KHÁC "đang chờ xác nhận": không ai sẽ trả
   * lời, vì không có email nào đang bay. Trước đây cả hai đều hiện cùng một câu "chưa xác nhận lời
   * mời (hiệu lực 72 giờ)", nên người đăng ký ngồi đợi một email không tồn tại. Backend nay phân
   * biệt qua `NO_ACTIVE_INVITATION`; ở đây suy từ state đã tải (chính xác hơn vì có cả trạng thái
   * hết hạn chưa quét), và chỉ khi state đọc được — không biết thì không khẳng định.
   */
  const noActiveInvitation =
    !contactConfirmed && !loadError && state != null && !pendingLive;

  return (
    <div
      data-testid={`contact-identity-actions-${visitInstanceId}`}
      className="mt-4 border-t border-slate-200 pt-4"
    >
      <p className="text-xs font-bold uppercase tracking-wide text-[#004c91]">
        {t('visitRequestV2:contact.manageTitle')}
      </p>

      {/* Offered only to the contact themselves — the server withholds `profileDifference` from
          everybody else, so this cannot appear on a registrant's screen. */}
      {state?.profileDifference && (
        <div className="mt-3">
          <ContactProfileSyncPrompt
            difference={state.profileDifference}
            onSynced={() => void refreshState()}
          />
        </div>
      )}

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
            onClick={() => void refreshState()}
            className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-sm font-bold text-amber-800 hover:bg-amber-100"
          >
            <RefreshCw className="h-4 w-4" aria-hidden /> {t('visitRequestV2:detail.retry')}
          </button>
        </div>
      )}

      {/* "Đang chờ xác nhận" chỉ đúng khi thực sự có lời mời đang bay. Nếu không còn lời mời nào
          (vừa hủy / đã từ chối / hết hạn) thì nói thẳng là chưa có lời mời hiệu lực và cơ sở vẫn
          đang chặn cổng xác nhận — người đăng ký cần HÀNH ĐỘNG, không phải chờ. */}
      {isPending && (
        noActiveInvitation ? (
          <p className="mt-2 text-sm text-amber-700" data-testid="contact-no-active-invitation">
            {t('visitRequestV2:contact.noActiveInvitationNotice')}
          </p>
        ) : (
          <p className="mt-2 text-sm text-amber-700">
            {t('visitRequestV2:contact.pendingNotice', { email: contactEmail ?? '' })}
          </p>
        )
      )}

      {/* A pending TRANSFER names an address that does NOT yet hold the campus. Saying so explicitly
          is what keeps the block above from reading as "this is the contact now" (plan §14). */}
      {pendingTransfer && pendingLive && (
        <p className="mt-2 text-sm text-amber-700" data-testid="contact-transfer-pending">
          {t('visitRequestV2:contact.transferPending', {
            email: state?.pendingEmailMasked ?? '',
            expiresAt: state?.expiresAt ? formatVietnamDateTime(state.expiresAt) : '',
          })}
        </p>
      )}

      {!isPending && !pendingTransfer && !loadError && (
        <p className="mt-2 text-sm text-slate-600">{t('visitRequestV2:contact.activeNotice')}</p>
      )}

      {showForm ? (
        contactForm
      ) : (
        <div className="mt-3 flex flex-wrap gap-2">
          {can.edit && (
            <button
              type="button"
              data-testid="contact-edit-open"
              className="rounded-lg border border-[#004c91] px-3 py-1.5 text-sm font-bold text-[#004c91] hover:bg-[#004c91]/5"
              onClick={openForm}
            >
              {t('visitRequestV2:contact.editOpen')}
            </button>
          )}
          {can.resend && (
            <button
              type="button"
              disabled={busy}
              data-testid="contact-resend-claim"
              className="rounded-lg bg-[#f37021] px-3 py-1.5 text-sm font-bold text-white hover:bg-[#e0631a] disabled:opacity-50"
              onClick={() => void run(() => resendOperationalContactConfirmation(visitRequestId, visitInstanceId))}
            >
              {t('visitRequestV2:contact.resendInvitation')}
            </button>
          )}
          {can.reinvite && (
            <button
              type="button"
              disabled={busy}
              data-testid="contact-reinvite"
              className="rounded-lg bg-[#f37021] px-3 py-1.5 text-sm font-bold text-white hover:bg-[#e0631a] disabled:opacity-50"
              onClick={() => void run(() => reinviteOperationalContactConfirmation(visitRequestId, visitInstanceId))}
            >
              {t('visitRequestV2:contact.reinvite')}
            </button>
          )}
          {can.cancelChange && (
            <button
              type="button"
              disabled={busy}
              data-testid="contact-cancel-transfer"
              className="rounded-lg border border-red-300 px-3 py-1.5 text-sm font-semibold text-red-600 hover:bg-red-50 disabled:opacity-50"
              onClick={() => setConfirmCancel(true)}
            >
              {cancelLabel}
            </button>
          )}
        </div>
      )}

      {/* Hủy lời mời không hoàn tác được và hậu quả khác hẳn nhau giữa hai loại — nên hỏi trước,
          bằng đúng câu mô tả hậu quả của loại đang chờ. */}
      {confirmCancel && (
        <div className="fixed inset-0 z-[120] flex items-center justify-center bg-slate-900/40 p-4 backdrop-blur-sm">
          <div
            role="dialog"
            aria-modal="true"
            data-testid="contact-cancel-confirm"
            className="w-full max-w-md rounded-2xl bg-white p-5 shadow-2xl"
          >
            <h4 className="text-base font-bold text-slate-800">{cancelLabel}</h4>
            <p className="mt-2 whitespace-pre-line text-sm text-slate-600">
              {pendingKind === 'TRANSFER'
                ? t('visitRequestV2:contact.cancelTransferConsequence')
                : t('visitRequestV2:contact.cancelConfirmationConsequence')}
            </p>
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                disabled={busy}
                className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold text-slate-600 hover:bg-slate-50 disabled:opacity-50"
                onClick={() => setConfirmCancel(false)}
              >
                {t('visitRequestV2:common.cancel')}
              </button>
              <button
                type="button"
                disabled={busy}
                data-testid="contact-cancel-confirm-submit"
                className="rounded-lg bg-red-600 px-3 py-1.5 text-sm font-bold text-white hover:bg-red-700 disabled:opacity-50"
                onClick={() => {
                  setConfirmCancel(false);
                  void run(() => cancelOperationalContactChange(visitRequestId, visitInstanceId));
                }}
              >
                {t('visitRequestV2:contact.cancelConfirmAction')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
