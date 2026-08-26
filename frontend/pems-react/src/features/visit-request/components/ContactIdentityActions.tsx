import { forwardRef, useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react';
import { AlertCircle, ChevronDown, ChevronUp, Loader2, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  cancelOperationalContactChange,
  getOperationalContactState,
  initiateOperationalContactTransfer,
  reinviteOperationalContactConfirmation,
  replaceOperationalContact,
  resendOperationalContactConfirmation,
  type OperationalContactProfileDifference,
  type OperationalContactState,
  type ResolvedOperationalContact,
} from '../api/visitRequestV2Api';
import { errorCodeOf, fieldErrorsOf, firstFieldError, hasAction, VisitV2Action } from '../utils/visitV2Actions';
import { showErrorToast, showMessageErrorToast, showSuccessToast } from '../../../shared/utils/toast';
import { isSameEmailIdentity, isValidEmailSyntax } from '../../../shared/utils/emailIdentity';
import { isValidPhone } from '../../../shared/utils/phoneNumber';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
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
   * THIS campus's current contact snapshot — used only to know the CURRENT email (to block a same-
   * address Transfer, plan CanhIter3FixBug §17.2) and to know whether it changed after a save. The
   * Transfer form itself never prefills from this (§17.1); metadata correction lives in Sửa nhanh now.
   */
  contact: ResolvedOperationalContact;
  /**
   * The backend's verdict for THIS campus (`campusVisit.allowedActions`). Each control is rendered ONLY
   * when its own code is present — never from role, relation or status. The codes mirror the guards in
   * the contact handlers, so the panel cannot offer a resend past its cap, or any contact change on a
   * campus whose visit has already started.
   */
  allowedActions: string[] | undefined;
  onChanged?: () => void;
  /**
   * True when the primary "Chuyển đầu mối" trigger is rendered elsewhere (the header row beside the
   * section title, via `ContactChangeTriggerButton`) instead of inline in this panel's own body.
   * The panel still owns opening the form — the external trigger calls it through the ref handle.
   */
  hidePrimaryTrigger?: boolean;
  /** Notifies a caller that renders the trigger externally when the form opens/closes, so it can hide
   *  its own button while the form (with its own Submit/Cancel) is already in view. */
  onFormOpenChange?: (open: boolean) => void;
  /**
   * Notifies a caller that renders the profile-mismatch icon externally (in the contact card's title
   * row, next to "Đầu mối đoàn khách phối hợp tại cơ sở") of the current offer, or `null` once there
   * is nothing left to reconcile. Fires on every state load/refresh, mirroring `onFormOpenChange`.
   */
  onProfileDifferenceChange?: (difference: OperationalContactProfileDifference | null) => void;
}

/** Imperative handle for a caller that renders the primary trigger outside this panel's own body. */
export interface ContactIdentityActionsHandle {
  openForm: () => void;
  /** Lets the externally-rendered profile-sync popover re-read state after it applies a change. */
  refreshState: () => Promise<void>;
}

interface ContactFormState {
  fullName: string;
  organization: string;
  jobTitle: string;
  phone: string;
  email: string;
}

/** Mirrors the FluentValidation limits on the commands — the backend stays the authority. */
const MAX = { fullName: 150, organization: 200, jobTitle: 150, phone: 50, email: 150 } as const;

/**
 * Per-field errors for the FOUR fields that are not the address (plan PEMS_VALIDATION_UX §2). Email
 * keeps its own dedicated `emailError` state below — it already carries identity-specific business
 * refusals (account inactive, cannot be used for a visitor account) that this generic map has no
 * concept of, and merging the two would either lose that nuance or duplicate it.
 */
type ContactFieldErrors = Partial<Record<'fullName' | 'organization' | 'jobTitle' | 'phone', string>>;

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
 * "Thay đầu mối" / "Chuyển đầu mối" — the operational-contact IDENTITY-change workflow for ONE campus,
 * rendered inside that campus's contact card on the Detail screen (plan CanhIter3FixBug). Same-person
 * metadata and
 * relation correction no longer live here — they moved into Sửa nhanh's contact block, which shares no
 * component with this one. This panel exists ONLY for the case where the campus's contact becomes a
 * DIFFERENT person: the form always opens BLANK (§17.1), and email is a required field naming the new
 * address.
 *
 * What the save means, decided from whether anyone currently holds the campus:
 *
 * - no confirmed holder yet → Replace (immediate, or an invitation if the new address is external).
 * - a confirmed holder exists → Transfer (an invitation; the current holder keeps every right until the
 *   new person accepts — decline/cancel/expiry leave them unchanged).
 *
 * A typed address equal to the campus's CURRENT one is refused (both client-side and by the backend)
 * with a message pointing at Sửa nhanh instead — this panel's whole purpose is a genuine identity change.
 */
const ContactIdentityActions = forwardRef<ContactIdentityActionsHandle, Props>(function ContactIdentityActions(
  {
    visitRequestId,
    visitInstanceId,
    contactConfirmed,
    contact,
    allowedActions,
    onChanged,
    hidePrimaryTrigger = false,
    onFormOpenChange,
    onProfileDifferenceChange,
  }: Props,
  ref,
) {
  const { t } = useTranslation(['visitRequestV2', 'validation', 'errors']);
  const isPending = !contactConfirmed;
  const contactEmail = contact.email || null;

  const can = useMemo(() => {
    /**
     * The two identity-change codes are never both granted at once — `VisitFormReadService` gates
     * REPLACE on nobody holding the campus yet and TRANSFER on somebody already holding it, so they
     * are structurally exclusive. Kept as two separate booleans (not one merged "changeIdentity") so
     * the trigger button can pick the label that names the actual workflow — "Thay đầu mối" replaces
     * a person nobody has confirmed yet, "Chuyển đầu mối" hands the role off from whoever holds it —
     * rather than one generic word for two different consequences.
     */
    const canReplace = hasAction(allowedActions, VisitV2Action.ReplaceOperationalContact);
    const canTransfer = hasAction(allowedActions, VisitV2Action.InitiateContactTransfer);
    return {
      canReplace,
      canTransfer,
      /** Either identity-change action is on the table — used only where it truly does not matter
       *  which (e.g. "is there any such action at all"). Anything that renders a LABEL must branch on
       *  `canReplace`/`canTransfer` individually instead. */
      changeIdentity: canReplace || canTransfer,
      resend: hasAction(allowedActions, VisitV2Action.ResendContactConfirmation),
      /** Không còn lời mời nào sống — phải mở lời mời MỚI, không phải "gửi lại". */
      reinvite: hasAction(allowedActions, VisitV2Action.ReinviteContactConfirmation),
      cancelChange: hasAction(allowedActions, VisitV2Action.CancelContactChange),
    };
  }, [allowedActions]);
  const hasAnyAction = can.changeIdentity || can.resend || can.reinvite || can.cancelChange;
  /** Which trigger the inline/header button renders — `null` when neither is granted. Exclusive by
   *  construction (see `can` above), so this is never an arbitrary tie-break. */
  const triggerKind: 'replace' | 'transfer' | null =
    can.canReplace ? 'replace' : can.canTransfer ? 'transfer' : null;

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
  // "Xem chi tiết" cho người đang được mời trong một transfer đang chờ — mặc định đóng, KHÔNG persist,
  // vì đây là chi tiết phụ (summary đã đủ để quyết định resend/cancel).
  const [showPendingDetails, setShowPendingDetails] = useState(false);

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

  const openForm = () => {
    // Genuinely blank (plan CanhIter3FixBug §17.1) — this is now the identity-change-only door, so
    // prefilling from the current contact would invite the exact typo-becomes-a-handover mistake the
    // split exists to prevent. Same-person correction lives in Sửa nhanh now.
    setForm({ fullName: '', organization: '', jobTitle: '', phone: '', email: '' });
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

  // Lets a caller that renders the trigger externally (header row) open this panel's form, or ask it
  // to re-read state after an external action (the profile-sync popover's own apply), without this
  // panel handing its state up — the ref is the only thing that crosses that boundary.
  useImperativeHandle(ref, () => ({ openForm, refreshState }), [openForm, refreshState]);

  useEffect(() => {
    onFormOpenChange?.(showForm);
  }, [showForm, onFormOpenChange]);

  useEffect(() => {
    onProfileDifferenceChange?.(state?.profileDifference ?? null);
  }, [state?.profileDifference, onProfileDifferenceChange]);

  const pendingTransfer = state?.pendingChangeKind === 'TRANSFER';
  const pendingLive = state?.pendingChangeStatus === 'PENDING';

  /**
   * Identifies WHICH invitation is live, so "Xem chi tiết" resets to collapsed only when it should.
   *
   * Kind + masked address is enough to tell one invitation from another without a dedicated id: a
   * RESEND keeps the same kind and address (only `tokenVersion`/`expiresAt` move, plan §19 — the panel
   * must stay expanded through that), while a cancel-then-new-transfer, an accept, or simply no pending
   * left all change this key (the last two to `null`), which is exactly when a stale "expanded" from a
   * different invitation must not carry over.
   */
  const pendingIdentityKey = pendingTransfer && pendingLive
    ? `${state?.pendingChangeKind ?? ''}:${state?.pendingEmailMasked ?? ''}`
    : null;
  const pendingIdentityKeyRef = useRef(pendingIdentityKey);
  useEffect(() => {
    if (pendingIdentityKeyRef.current !== pendingIdentityKey) {
      pendingIdentityKeyRef.current = pendingIdentityKey;
      setShowPendingDetails(false);
    }
  }, [pendingIdentityKey]);

  if (!hasAnyAction) return null;

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
          (['fullName', 'organization', 'jobTitle', 'phone'] as const).forEach(key => {
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
          // A validation error whose fields we cannot map to a control on this form still falls through
          // to the generic branches below rather than being silently swallowed.
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
   * that slips past this still fails there. Phone stays OPTIONAL here (blank submits) — that has not
   * changed — but a NON-BLANK value must now be shaped like a phone number, matching the command's
   * `MustBeAPhoneNumber` rule; Organization is required, matching Create/Pending Edit/Resubmit and the
   * command's own `NotEmpty` rule.
   */
  const validateContactFields = (f: ContactFormState): { errors: ContactFieldErrors; email: string | null } => {
    const errors: ContactFieldErrors = {};
    if (!f.fullName.trim())
      errors.fullName = t('validation:requiredField', { field: t('visitRequestV2:person.fullName') });
    if (!f.organization.trim())
      errors.organization = t('validation:requiredField', { field: t('visitRequestV2:person.organization') });
    if (!f.jobTitle.trim())
      errors.jobTitle = t('validation:requiredField', { field: t('visitRequestV2:person.jobTitle') });
    if (f.phone.trim() && !isValidPhone(f.phone))
      errors.phone = t('validation:phoneInvalidField', { field: t('visitRequestV2:card.phone') });
    let email: string | null = null;
    if (!f.email.trim()) {
      email = t('validation:requiredField', { field: t('visitRequestV2:card.email') });
    } else if (!isValidEmailSyntax(f.email)) {
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
    // Client-side head-start (plan CanhIter3FixBug §17.2/§33) — the backend is the real authority and
    // rejects a same-address Replace/Transfer outright regardless of this check. Never falls through
    // to a profile-update route: this panel no longer has one.
    if (!identityChanging) {
      setEmailError(t('visitRequestV2:contact.transferEmailSameAsCurrent'));
      return;
    }
    setEmailError(null);
    setFieldErrors({});
    // Explicit dispatch by current holder state (plan §17.3/§33) — never the ambiguous
    // saveOperationalContact router, whose same-address branch would silently treat this as a profile
    // correction instead of the identity change this form always means.
    void run(() =>
      contactConfirmed
        ? initiateOperationalContactTransfer(visitRequestId, visitInstanceId, {
            fullName: form.fullName,
            organization: form.organization,
            jobTitle: form.jobTitle,
            phone: form.phone,
            email: form.email,
          })
        : replaceOperationalContact(visitRequestId, visitInstanceId, {
            fullName: form.fullName,
            organization: form.organization,
            jobTitle: form.jobTitle,
            phone: form.phone,
            email: form.email,
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
    // point at it. Email has no static hint of its own either — just the error message below when the
    // typed address is rejected.
    const ariaDescribedBy = hasError ? errorId : undefined;
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
        <p id={errorId} role="alert" className="mt-1 text-xs font-normal text-red-600">
          {genericError}
        </p>
      )}
      {/* The accepted shapes, stated up front (plan §18) — PhoneField's own hint, shown while
          focused and error-free, same copy every other Phone field in Visit V2 uses. */}
      {field === 'email' && emailError && (
        <p id="ci-email-error" role="alert" className="mt-1 text-xs font-normal text-red-600">
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

      {/* Only the Replace case still gets an inline warning — it names a consequence (re-opening the
          confirmation gate for the whole request) that the Transfer case does not have: the current
          contact there keeps every right until the new person accepts, so there is nothing to warn
          about. */}
      {identityChanging && !can.canTransfer && (
        <p
          className="mt-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs font-normal text-amber-800"
          role="note"
          data-testid="contact-identity-warning"
        >
          {t('visitRequestV2:contact.replaceReopensGate')}
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

  // The inline trigger only exists when nobody else is rendering it (default / standalone use); once
  // a caller takes it over via `hidePrimaryTrigger`, this panel contributes nothing to that row.
  const showInlineTrigger = triggerKind !== null && !hidePrimaryTrigger;
  const showSecondaryActions = can.resend || can.reinvite || can.cancelChange;
  // Nothing left to say once the header owns the trigger and the contact is simply confirmed with no
  // invitation in flight — rendering the border/padding wrapper then would be a divider around an
  // empty box. The profile-mismatch offer no longer counts here: it now renders as an icon in the
  // contact card's title row (via `onProfileDifferenceChange`), not inside this panel's own body.
  const showBody =
    loading || loadError || isPending
    || (pendingTransfer && pendingLive) || showForm || showInlineTrigger || showSecondaryActions;
  if (!showBody) return null;

  return (
    <div
      data-testid={`contact-identity-actions-${visitInstanceId}`}
      className="mt-4 border-t border-slate-200 pt-4"
    >
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

      {showForm ? (
        contactForm
      ) : (
        <div className="mt-3 flex flex-wrap gap-2">
          {showInlineTrigger && triggerKind && (
            <ContactChangeTriggerButton kind={triggerKind} onClick={openForm} />
          )}
          {/* A light disclosure, not another CTA (plan: giữ card gọn — summary/resend/cancel đã đủ để
              quyết định, chi tiết người được mời chỉ cần khi thật sự muốn xem). */}
          {pendingTransfer && pendingLive && (
            <button
              type="button"
              aria-expanded={showPendingDetails}
              aria-controls={`pending-contact-detail-${visitInstanceId}`}
              data-testid="contact-pending-details-toggle"
              className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold text-[#004c91] hover:bg-slate-50"
              onClick={() => setShowPendingDetails(v => !v)}
            >
              {showPendingDetails
                ? t('visitRequestV2:contact.pendingDetailsCollapse')
                : t('visitRequestV2:contact.pendingDetailsExpand')}
              {showPendingDetails
                ? <ChevronUp className="h-4 w-4" aria-hidden />
                : <ChevronDown className="h-4 w-4" aria-hidden />}
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

      {/* Collapsed by default (plan: card gọn) — the summary above already names the address and the
          expiry; this is only the rest of what the invitation snapshot says about that person, for
          whoever wants it. Never the current contact: this person holds nothing until they accept. */}
      {pendingTransfer && pendingLive && showPendingDetails && (
        <div
          id={`pending-contact-detail-${visitInstanceId}`}
          data-testid="contact-pending-details"
          className="mt-3 rounded-lg border border-slate-200 bg-slate-50 p-3"
        >
          <p className="mb-2 text-xs font-semibold text-slate-500">
            {t('visitRequestV2:contact.pendingContactHeading')}
          </p>
          {state?.pendingContact ? (
            <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
              <PendingContactField
                label={t('visitRequestV2:person.fullName')}
                value={state.pendingContact.fullName}
                testId={`contact-pending-full-name-${visitInstanceId}`}
              />
              <PendingContactField
                label={t('visitRequestV2:person.organization')}
                value={state.pendingContact.organization}
                testId={`contact-pending-organization-${visitInstanceId}`}
              />
              <PendingContactField
                label={t('visitRequestV2:person.jobTitle')}
                value={state.pendingContact.jobTitle}
                testId={`contact-pending-job-title-${visitInstanceId}`}
              />
              <PendingContactField
                label={t('visitRequestV2:card.phone')}
                value={state.pendingContact.phone}
                testId={`contact-pending-phone-${visitInstanceId}`}
              />
              <PendingContactField
                label={t('visitRequestV2:card.email')}
                value={state.pendingContact.emailMasked}
                testId={`contact-pending-email-${visitInstanceId}`}
              />
            </dl>
          ) : (
            <p className="text-sm text-slate-500" data-testid="contact-pending-details-unavailable">
              {t('visitRequestV2:contact.pendingContactUnavailable')}
            </p>
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
});

export default ContactIdentityActions;

/**
 * The single primary identity-change trigger — rendered either inline by `ContactIdentityActions`
 * itself (default) or, when `hidePrimaryTrigger` is set, by a caller that places it in the section
 * header next to the title and drives it through the ref handle. Exported so both call sites share
 * the exact same markup/test id instead of two copies drifting apart.
 *
 * `kind` picks the label that names what actually happens — never a generic "change contact" word for
 * two different consequences: REPLACE swaps a person nobody has confirmed yet (no authority moves),
 * TRANSFER hands the role off from whoever currently holds it (an invitation, current holder keeps
 * every right until accepted). The caller decides `kind` from the same `allowedActions` codes this
 * component would use itself — see `ContactIdentityActions`'s own `can.canReplace`/`can.canTransfer`.
 */
export function ContactChangeTriggerButton({
  kind,
  onClick,
}: {
  kind: 'replace' | 'transfer';
  onClick: () => void;
}) {
  const { t } = useTranslation(['visitRequestV2']);
  return (
    <button
      type="button"
      data-testid="contact-edit-open"
      className="rounded-lg border border-[#004c91] px-3 py-1.5 text-sm font-bold text-[#004c91] hover:bg-[#004c91]/5"
      onClick={onClick}
    >
      {kind === 'replace'
        ? t('visitRequestV2:contact.replaceContactAction')
        : t('visitRequestV2:contact.transferContactAction')}
    </button>
  );
}

/** One label/value pair inside the pending-contact detail panel — same visual language as the read-only
 *  contact card (`OperationalContactReadOnly`'s own `Field`), kept local since this is the only other
 *  place that needs it. */
function PendingContactField({
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
