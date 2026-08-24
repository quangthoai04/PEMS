import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { X, Lock } from 'lucide-react';
import {
  patchSafeDetails,
  type ResolvedVisitForm,
  type SafeEditResponse,
} from '../api/visitRequestV2Api';
import { buildChangedOnlyPayload } from '../utils/safeEditDiff';
import { hasAction, VisitV2Action, errorCodeOf, capabilityFor } from '../utils/visitV2Actions';
import { showErrorToast, showSuccessToast } from '../../../shared/utils/toast';
import { isValidPhone } from '../../../shared/utils/phoneNumber';
import { personIdentityKey } from '../../../shared/utils/personIdentity';
import { PartnerOrgCombobox } from './shared/PartnerOrgCombobox';
import { CountrySelect } from './shared/CountrySelect';
import { PhoneField } from './shared/PhoneField';
import { AutoGrowTextarea } from './shared/AutoGrowTextarea';
import { OrganizationCombobox } from './shared/OrganizationCombobox';
import { HelpTooltip } from './shared/HelpTooltip';

const NOTES_MAX_LENGTH = 2000;
const RELATION_MISMATCH_CODE = 'OPERATIONAL_CONTACT_RELATION_PROFILE_MISMATCH';

const MAX = { fullName: 150, organization: 200, jobTitle: 150, phone: 50 } as const;

interface Props {
  form: ResolvedVisitForm;
  onClose: () => void;
  onSaved: () => void;
}

/**
 * Safe / privacy-urgent edit (plan §16.5) plus, per campus, the same-person operational-contact
 * correction (plan CanhIter3FixBug §4/§5/§6) — metadata + relation to a delegation member, email
 * locked. Apply-now, never an amendment. Optimistic concurrency via row versions → a stable 409 shows a
 * steady message and a reload.
 */
export default function VisitSafeEditModal({ form, onClose, onSaved }: Props) {
  const { t } = useTranslation(['visitRequestV2', 'validation']);

  const [registrant, setRegistrant] = useState({
    fullName: form.registrant.fullName,
    nationality: form.registrant.nationality,
    organization: form.registrant.organization,
    jobTitle: form.registrant.jobTitle,
    phone: form.registrant.phone,
    partnerId: form.partnerId,
  });
  // A campus is INCLUDED here when it offers EITHER generic Safe fields OR contact editing — the two
  // capabilities are independent (plan CanhIter3FixBug, decision M): a campus still WAITING_REQUEST_
  // APPROVAL has UpdateContactProfile but not SubmitSafeEdit, and must still reach its contact block.
  const editableCampuses = form.campusVisits.filter(
    c => hasAction(c.allowedActions, VisitV2Action.SubmitSafeEdit)
      || hasAction(c.allowedActions, VisitV2Action.UpdateContactProfile),
  );
  const lockedCampuses = form.campusVisits.filter(
    c => !hasAction(c.allowedActions, VisitV2Action.SubmitSafeEdit)
      && !hasAction(c.allowedActions, VisitV2Action.UpdateContactProfile),
  );

  // The registrant/contact block is SHARED by every campus, so it has its own all-or-nothing verdict.
  // On a mixed request one campus can be editable while this is not: the campus fields below still
  // work, and these are shown read-only rather than accepting typing the backend would reject.
  const canEditShared = hasAction(form.viewer.allowedActions, VisitV2Action.SubmitSafeEdit);
  // Same backend verdict as `canEditShared` — the refused/allowed entry itself, so the tooltip and the
  // locked notice can show the EXACT reason and lead-time the policy computed, instead of a frontend
  // guess at why. Undefined for a non-registrant viewer (the backend only emits this capability for the
  // registrant), in which case the copy below falls back to the general explanation alone.
  const registrantCapability = capabilityFor(form.viewer.capabilities, VisitV2Action.SubmitSafeEdit);

  const [instances, setInstances] = useState(
    editableCampuses.map(c => ({
      visitInstanceId: c.visitInstanceId,
      expectedRowVersion: c.rowVersion,
      campusName: c.campusName,
      transportationNote: c.transportationNote ?? '',
      mediaConsentStatus: c.mediaConsentStatus,
      notes: c.notes ?? '',
      contactFullName: c.operationalContact.fullName,
      contactOrganization: c.operationalContact.organization,
      contactJobTitle: c.operationalContact.jobTitle,
      contactPhone: c.operationalContact.phone ?? '',
      contactGuestMemberId: c.operationalContact.guestMemberId ?? null,
      canGenericSafe: hasAction(c.allowedActions, VisitV2Action.SubmitSafeEdit),
      canEditContact: hasAction(c.allowedActions, VisitV2Action.UpdateContactProfile),
      contactEmail: c.operationalContact.email,
      members: [...c.visitors, ...c.supportMembers],
    })),
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [applied, setApplied] = useState<SafeEditResponse | null>(null);
  // PhoneField is a visual/input component, not a validation authority — the check has to live here.
  const [phoneError, setPhoneError] = useState<string | null>(null);
  const [contactErrors, setContactErrors] = useState<Record<number, string | undefined>>({});
  // Same "optional but must be shaped like one" rule as the registrant phone above, applied per campus's
  // operational-contact phone — a gap the registrant check alone did not cover (GitHub CanhIter3FixBug
  // phone-required repro): mirrors backend MustBeAPhoneNumber, which likewise passes blank and only
  // rejects a non-blank malformed value.
  const [contactPhoneErrors, setContactPhoneErrors] = useState<Record<number, string | undefined>>({});

  const setInstance = (id: number, patch: Partial<(typeof instances)[number]>) =>
    setInstances(prev => prev.map(i => (i.visitInstanceId === id ? { ...i, ...patch } : i)));

  const setContactError = (id: number, message: string | undefined) =>
    setContactErrors(prev => ({ ...prev, [id]: message }));

  // ── Effective-relation mismatch check (plan CanhIter3FixBug, decision N) — runs whenever a member is
  //    currently selected, regardless of whether the relation dropdown itself was just touched, so a
  //    metadata-only edit that would desync an existing link is caught client-side too. Backend is the
  //    real authority; this is a UX head-start (mapped from RelationProfileMismatch on submit as well). ──
  const relationMismatches = useMemo(() => {
    const result: Record<number, boolean> = {};
    for (const i of instances) {
      if (i.contactGuestMemberId == null) { result[i.visitInstanceId] = false; continue; }
      const member = i.members.find(m => m.guestMemberId === i.contactGuestMemberId);
      if (!member) { result[i.visitInstanceId] = false; continue; }
      const contactKey = personIdentityKey(i.contactFullName, i.contactJobTitle, i.contactOrganization);
      const memberKey = personIdentityKey(member.fullName, member.jobTitle, member.organization);
      result[i.visitInstanceId] = contactKey !== memberKey;
    }
    return result;
  }, [instances]);

  const save = async () => {
    setBusy(true);
    setError(null);
    setConflict(false);
    // Phone is optional, but a non-blank value must be shaped like one — mirrors the backend's
    // MustBeAPhoneNumber rule so a malformed number is caught here instead of round-tripping to the API.
    if (canEditShared && registrant.phone.trim() && !isValidPhone(registrant.phone)) {
      setPhoneError(t('validation:phoneInvalidField', { field: t('visitRequestV2:card.phone') }));
      setBusy(false);
      return;
    }
    setPhoneError(null);

    // Same rule, per campus's operational-contact phone: optional, but a non-blank value must be shaped
    // like one. Blank/untouched phones never fail this — only a genuinely malformed non-blank value does.
    const invalidContactPhones = instances.filter(
      i => i.canEditContact && i.contactPhone.trim() && !isValidPhone(i.contactPhone));
    if (invalidContactPhones.length > 0) {
      const next: Record<number, string | undefined> = {};
      for (const i of invalidContactPhones)
        next[i.visitInstanceId] = t('validation:phoneInvalidField', { field: t('visitRequestV2:card.phone') });
      setContactPhoneErrors(next);
      setBusy(false);
      return;
    }
    setContactPhoneErrors({});

    const mismatched = instances.filter(i => i.canEditContact && relationMismatches[i.visitInstanceId]);
    if (mismatched.length > 0) {
      for (const i of mismatched) setContactError(i.visitInstanceId, t('visitRequestV2:safeEdit.contactMismatchError'));
      setBusy(false);
      return;
    }
    setContactErrors({});

    const payload = buildChangedOnlyPayload(form, registrant, instances, canEditShared);
    if (payload === null) {
      setError(t('visitRequestV2:safeEdit.noChanges'));
      setBusy(false);
      return;
    }
    try {
      const res = await patchSafeDetails(form.visitRequestId, payload);
      setApplied(res);
      // The parent closes the modal on this callback, so the in-modal success panel is never seen —
      // the confirmation has to survive the modal, which is what the global toast is for.
      showSuccessToast(t('visitRequestV2:safeEdit.appliedCount', { count: res.appliedChanges.length }));
      onSaved();
    } catch (err) {
      const code = errorCodeOf(err);
      // Backend RelationProfileMismatch maps to the SAME inline error the client-side check shows
      // (plan CanhIter3FixBug, decision P) — never a generic toast — keeping the modal open so the
      // user can fix the mismatch right there. Frontend pre-check is UX only; backend is authority.
      if (code === RELATION_MISMATCH_CODE) {
        const target = instances.find(i => i.canEditContact) ?? instances[0];
        if (target) setContactError(target.visitInstanceId, t('visitRequestV2:safeEdit.contactMismatchError'));
      } else if (err && (err as { response?: { status?: number } }).response?.status === 409) {
        // A version conflict is actionable INSIDE the modal (reload and retry), so it stays inline.
        setConflict(true);
        setError(t('visitRequestV2:safeEdit.conflict'));
      } else {
        setError(t('visitRequestV2:safeEdit.errGeneric'));
        showErrorToast(err, t('visitRequestV2:safeEdit.errGeneric'));
      }
    } finally {
      setBusy(false);
    }
  };

  const field = 'w-full rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 p-2 text-sm';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" role="dialog" aria-modal="true"
      aria-label={t('visitRequestV2:safeEdit.title')}>
      <div className="max-h-[calc(100dvh-2rem)] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white dark:bg-slate-900 p-5 shadow-xl">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-base font-extrabold text-[#004c91]">{t('visitRequestV2:safeEdit.title')}</h2>
          <button type="button" onClick={onClose} className="rounded p-1 text-slate-500 hover:bg-slate-100" aria-label={t('visitRequestV2:common.cancel')}>
            <X className="h-5 w-5" />
          </button>
        </div>
        <p className="mb-4 rounded-lg bg-blue-50 px-3 py-2 text-xs text-blue-800">{t('visitRequestV2:safeEdit.applyNowNote')}</p>

        {applied ? (
          <div className="space-y-2" data-testid="safe-edit-applied">
            <p className="rounded-lg bg-green-50 px-3 py-2 text-sm text-green-800" role="status">
              {t('visitRequestV2:safeEdit.appliedCount', { count: applied.appliedChanges.length })}
            </p>
            <div className="flex justify-end">
              <button type="button" onClick={onClose} className="rounded-lg bg-[#004c91] px-4 py-2 text-sm font-bold text-white">
                {t('visitRequestV2:common.cancel')}
              </button>
            </div>
          </div>
        ) : (
          <>
            {/* Shared across every campus, so it travels with the request-level verdict. Disabled
                rather than hidden: the user still needs to SEE the details they cannot change, and
                a section that vanishes reads as data loss. */}
            <fieldset className="mb-3" disabled={!canEditShared} data-testid="safe-edit-shared-fields">
              {/* The tooltip trigger is wrapped in its own <span>, not a direct child of <legend> —
                  <legend> establishes an unusual containing block in some browsers, which clipped an
                  absolutely-positioned descendant placed directly inside it (mirrors the working
                  pattern CampusVisitCard.tsx already uses for its own HelpTooltip-in-a-legend). */}
              <legend className="mb-1 text-sm font-bold text-slate-700">
                <span className="flex items-center">
                  {t('visitRequestV2:summary.registrant')}
                  <HelpTooltip
                    testId="safe-edit-registrant-tooltip"
                    label={t('visitRequestV2:summary.registrant')}
                    // This trigger sits right under the modal's fixed header, with no room to open
                    // upward — the default 'top' placement clipped the bubble's first line against
                    // the top of the viewport.
                    placement="bottom"
                    content={
                      <>
                        {t('visitRequestV2:safeEdit.registrantInfoHelp')}
                        {registrantCapability?.requiredLeadHours != null && (
                          <>
                            {' '}
                            {t('visitRequestV2:safeEdit.registrantInfoHelpLeadHours', {
                              hours: registrantCapability.requiredLeadHours,
                            })}
                          </>
                        )}
                      </>
                    }
                  />
                </span>
              </legend>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                <label className="block text-sm" data-testid="safe-edit-registrant-fullName">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:registrant.fullName')}</span>
                  <input className={field} value={registrant.fullName} onChange={e => setRegistrant({ ...registrant, fullName: e.target.value })} />
                </label>
                <label className="block text-sm" data-testid="safe-edit-registrant-nationality">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:registrant.nationality')}</span>
                  <CountrySelect
                    strict
                    disabled={!canEditShared}
                    value={registrant.nationality}
                    ariaLabel={t('visitRequestV2:registrant.nationality')}
                    onChange={value => setRegistrant({ ...registrant, nationality: value })}
                  />
                </label>
                <label className="block text-sm sm:col-span-2" data-testid="safe-edit-registrant-organization">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:registrant.organization')}</span>
                  <PartnerOrgCombobox
                    disabled={!canEditShared}
                    organization={registrant.organization}
                    partnerId={registrant.partnerId}
                    onChange={next => setRegistrant({ ...registrant, organization: next.organization, partnerId: next.partnerId })}
                  />
                </label>
                <label className="block text-sm" data-testid="safe-edit-registrant-jobTitle">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:registrant.jobTitle')}</span>
                  <input className={field} value={registrant.jobTitle} onChange={e => setRegistrant({ ...registrant, jobTitle: e.target.value })} />
                </label>
                {/* Same shared control as every other editable phone field in Visit V2 — same format
                    hint, same "tel" type/inputMode, instead of a bare text input reimplementing (and
                    silently drifting from) that behavior. */}
                <label className="block text-sm" data-testid="safe-edit-registrant-phone">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:card.phone')}</span>
                  <PhoneField
                    className={field}
                    testId="safe-edit-registrant-phone-input"
                    disabled={!canEditShared}
                    hasError={!!phoneError}
                    error={phoneError ?? undefined}
                    field={{
                      value: registrant.phone,
                      onChange: e => {
                        setRegistrant({ ...registrant, phone: e.target.value });
                        if (phoneError) setPhoneError(null);
                      },
                    }}
                  />
                  {phoneError && (
                    <p role="alert" className="mt-1 text-xs font-normal text-red-600">{phoneError}</p>
                  )}
                </label>
              </div>
            </fieldset>
            {!canEditShared && (
              <p
                data-testid="safe-edit-shared-locked"
                className="mb-3 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600"
              >
                {t('visitRequestV2:safeEdit.sharedLocked')}
                {/* The exact backend reason — which campus, or the cutoff sentence — appended verbatim
                    when the read model provided one, rather than the frontend reconstructing why from
                    campus statuses it would have to duplicate the policy to interpret correctly. */}
                {registrantCapability?.disabledReason && (
                  <span data-testid="safe-edit-shared-locked-reason"> {registrantCapability.disabledReason}</span>
                )}
              </p>
            )}
            {instances.map(i => (
              <fieldset key={i.visitInstanceId} className="mb-3 rounded-xl border border-slate-200 p-3">
                <legend className="px-1 text-sm font-bold text-[#004c91]">{i.campusName}</legend>

                <fieldset disabled={!i.canGenericSafe}>
                  <label className="mt-1 block text-sm">
                    <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:summary.transportation')}</span>
                    <input data-testid={`safe-edit-transportation-${i.visitInstanceId}`} className={field} value={i.transportationNote} onChange={e => setInstance(i.visitInstanceId, { transportationNote: e.target.value })} />
                  </label>
                  <label className="mt-2 block text-sm">
                    <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:summary.mediaConsent')}</span>
                    <select data-testid={`safe-edit-media-${i.visitInstanceId}`} className={field} value={i.mediaConsentStatus} onChange={e => setInstance(i.visitInstanceId, { mediaConsentStatus: e.target.value })}>
                      <option value="AGREED">{t('visitRequestV2:summary.mediaAgreed')}</option>
                      <option value="DECLINED">{t('visitRequestV2:summary.mediaDeclined')}</option>
                    </select>
                  </label>
                  <label className="mt-2 block text-sm">
                    <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:summary.campusNote')}</span>
                    <AutoGrowTextarea
                      data-testid={`safe-edit-notes-${i.visitInstanceId}`}
                      value={i.notes}
                      onChange={value => setInstance(i.visitInstanceId, { notes: value })}
                      maxLength={NOTES_MAX_LENGTH}
                      minRows={2}
                    />
                  </label>
                </fieldset>

                {/* Same-person operational-contact correction (plan CanhIter3FixBug §4) — independent of
                    the generic Safe fields above: a campus can have one capability without the other. */}
                <fieldset className="mt-4 border-t border-slate-200 pt-3" disabled={!i.canEditContact} data-testid={`safe-edit-contact-${i.visitInstanceId}`}>
                  <p className="mb-2 flex items-center text-xs font-bold uppercase tracking-wide text-[#004c91]">
                    {t('visitRequestV2:safeEdit.contactTitle')}
                    <HelpTooltip
                      testId={`safe-edit-contact-title-tooltip-${i.visitInstanceId}`}
                      label={t('visitRequestV2:safeEdit.contactTitle')}
                      content={t('visitRequestV2:safeEdit.contactTitleHint')}
                    />
                  </p>
                  <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                    <label className="block text-sm">
                      <span className="mb-1 block text-xs font-semibold text-slate-600">
                        {t('visitRequestV2:person.fullName')} <span className="text-red-500">*</span>
                      </span>
                      <input
                        data-testid={`safe-edit-contact-fullName-${i.visitInstanceId}`}
                        className={field} maxLength={MAX.fullName}
                        value={i.contactFullName}
                        onChange={e => setInstance(i.visitInstanceId, { contactFullName: e.target.value })}
                      />
                    </label>
                    <label className="block text-sm">
                      <span className="mb-1 block text-xs font-semibold text-slate-600">
                        {t('visitRequestV2:person.organization')} <span className="text-red-500">*</span>
                      </span>
                      <OrganizationCombobox
                        inputId={`safe-edit-contact-org-${i.visitInstanceId}`}
                        testId={`safe-edit-contact-organization-${i.visitInstanceId}`}
                        ariaLabel={t('visitRequestV2:person.organization')}
                        value={i.contactOrganization}
                        onChange={value => setInstance(i.visitInstanceId, { contactOrganization: value })}
                      />
                    </label>
                    <label className="block text-sm">
                      <span className="mb-1 block text-xs font-semibold text-slate-600">
                        {t('visitRequestV2:person.jobTitle')} <span className="text-red-500">*</span>
                      </span>
                      <input
                        data-testid={`safe-edit-contact-jobTitle-${i.visitInstanceId}`}
                        className={field} maxLength={MAX.jobTitle}
                        value={i.contactJobTitle}
                        onChange={e => setInstance(i.visitInstanceId, { contactJobTitle: e.target.value })}
                      />
                    </label>
                    <label className="block text-sm">
                      <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:card.phone')}</span>
                      <PhoneField
                        className={field}
                        testId={`safe-edit-contact-phone-${i.visitInstanceId}`}
                        hasError={!!contactPhoneErrors[i.visitInstanceId]}
                        error={contactPhoneErrors[i.visitInstanceId]}
                        field={{
                          value: i.contactPhone,
                          maxLength: MAX.phone,
                          onChange: e => {
                            setInstance(i.visitInstanceId, { contactPhone: e.target.value });
                            if (contactPhoneErrors[i.visitInstanceId])
                              setContactPhoneErrors(prev => ({ ...prev, [i.visitInstanceId]: undefined }));
                          },
                        }}
                      />
                      {contactPhoneErrors[i.visitInstanceId] && (
                        <p
                          role="alert"
                          data-testid={`safe-edit-contact-phone-error-${i.visitInstanceId}`}
                          className="mt-1 text-xs font-normal text-red-600"
                        >
                          {contactPhoneErrors[i.visitInstanceId]}
                        </p>
                      )}
                    </label>
                    <label className="block text-sm sm:col-span-2">
                      <span className="mb-1 flex items-center text-xs font-semibold text-slate-600">
                        {t('visitRequestV2:card.email')}
                        <Lock className="ml-1 h-3 w-3 text-slate-400" aria-hidden />
                      </span>
                      <p
                        data-testid={`safe-edit-contact-email-${i.visitInstanceId}`}
                        className="mt-1 h-10 flex items-center rounded-lg border border-slate-200 bg-slate-50 px-3 text-sm text-slate-500"
                      >
                        {i.contactEmail}
                      </p>
                      <span className="mt-1 block text-xs text-slate-500">{t('visitRequestV2:safeEdit.contactEmailLockedHint')}</span>
                    </label>
                  </div>

                  <div className="mt-3">
                    <label className="mb-1 flex items-center text-sm font-semibold text-slate-700">
                      {t('visitRequestV2:amend.contactPickLabel')}
                      <HelpTooltip
                        testId={`safe-edit-contact-relation-tooltip-${i.visitInstanceId}`}
                        label={t('visitRequestV2:amend.contactPickLabel')}
                        content={t('visitRequestV2:safeEdit.contactRelationTooltip')}
                      />
                    </label>
                    <select
                      data-testid={`safe-edit-contact-relation-${i.visitInstanceId}`}
                      className={field}
                      value={i.contactGuestMemberId ?? ''}
                      onChange={e => {
                        setInstance(i.visitInstanceId, { contactGuestMemberId: e.target.value === '' ? null : Number(e.target.value) });
                        setContactError(i.visitInstanceId, undefined);
                      }}
                    >
                      <option value="">{t('visitRequestV2:card.contactPickNone')}</option>
                      {i.members.map(m => (
                        <option key={m.guestMemberId} value={m.guestMemberId}>{m.fullName}</option>
                      ))}
                    </select>
                    {(contactErrors[i.visitInstanceId] || relationMismatches[i.visitInstanceId]) && (
                      <p
                        role="alert"
                        data-testid={`safe-edit-contact-mismatch-${i.visitInstanceId}`}
                        className="mt-2 text-xs font-normal text-red-600"
                      >
                        {contactErrors[i.visitInstanceId] ?? t('visitRequestV2:safeEdit.contactMismatchError')}
                      </p>
                    )}
                  </div>
                </fieldset>
              </fieldset>
            ))}

            {/* Named, not omitted: on a multi-campus request a user who cannot find a campus assumes
                the page is broken. Saying which campus and why is shorter than the support thread. */}
            {lockedCampuses.length > 0 && (
              <p
                data-testid="safe-edit-locked-campuses"
                className="mb-3 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600"
              >
                {t('visitRequestV2:safeEdit.lockedCampuses', {
                  campuses: lockedCampuses.map(c => c.campusName).join(', '),
                })}
              </p>
            )}

            {error && (
              <div className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">
                <p>{error}</p>
                {conflict && (
                  <button type="button" onClick={onSaved} className="mt-1 font-bold underline">
                    {t('visitRequestV2:safeEdit.reload')}
                  </button>
                )}
              </div>
            )}

            <div className="mt-4 flex justify-end gap-2">
              <button type="button" onClick={onClose} className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700">
                {t('visitRequestV2:common.cancel')}
              </button>
              <button type="button" data-testid="safe-edit-submit" disabled={busy} onClick={() => void save()}
                className="rounded-lg bg-[#004c91] px-4 py-2 text-sm font-bold text-white hover:bg-[#003a6f] disabled:opacity-50">
                {t('visitRequestV2:safeEdit.save')}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
