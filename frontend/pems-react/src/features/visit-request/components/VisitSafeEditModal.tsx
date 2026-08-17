import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { X } from 'lucide-react';
import {
  patchSafeDetails,
  type ResolvedVisitForm,
  type SafeEditResponse,
} from '../api/visitRequestV2Api';
import { buildChangedOnlyPayload } from '../utils/safeEditDiff';
import { hasAction, VisitV2Action } from '../utils/visitV2Actions';
import { showErrorToast, showSuccessToast } from '../../../shared/utils/toast';
import { PartnerOrgCombobox } from './shared/PartnerOrgCombobox';
import { CountrySelect } from './shared/CountrySelect';
import { PhoneField } from './shared/PhoneField';
import { AutoGrowTextarea } from './shared/AutoGrowTextarea';

const NOTES_MAX_LENGTH = 2000;

interface Props {
  form: ResolvedVisitForm;
  onClose: () => void;
  onSaved: () => void;
}

/**
 * Safe / privacy-urgent edit (plan §16.5). Apply-now corrections: the registrant snapshot, plus each
 * presentation fields and per-instance transportation / note / media consent. Media DECLINED applies
 * immediately even <24h (backend authority). Optimistic concurrency via row versions → a stable 409
 * shows a steady message and a reload; the proposal is never presented as an approval-pending amendment.
 */
export default function VisitSafeEditModal({ form, onClose, onSaved }: Props) {
  const { t } = useTranslation(['visitRequestV2']);

  const [registrant, setRegistrant] = useState({
    fullName: form.registrant.fullName,
    nationality: form.registrant.nationality,
    organization: form.registrant.organization,
    jobTitle: form.registrant.jobTitle,
    phone: form.registrant.phone,
    partnerId: form.partnerId,
  });
  // ONLY the campuses the backend says are editable right now. A campus that is under way, finished,
  // or inside its window used to appear here as a normal editable block — the user would type into it
  // and have the entire edit refused on save, including the campuses that were perfectly fine.
  const editableCampuses = form.campusVisits.filter(c =>
    hasAction(c.allowedActions, VisitV2Action.SubmitSafeEdit));
  const lockedCampuses = form.campusVisits.filter(c =>
    !hasAction(c.allowedActions, VisitV2Action.SubmitSafeEdit));

  // The registrant/contact block is SHARED by every campus, so it has its own all-or-nothing verdict.
  // On a mixed request one campus can be editable while this is not: the campus fields below still
  // work, and these are shown read-only rather than accepting typing the backend would reject.
  const canEditShared = hasAction(form.viewer.allowedActions, VisitV2Action.SubmitSafeEdit);

  const [instances, setInstances] = useState(
    editableCampuses.map(c => ({
      visitInstanceId: c.visitInstanceId,
      expectedRowVersion: c.rowVersion,
      campusName: c.campusName,
      transportationNote: c.transportationNote ?? '',
      mediaConsentStatus: c.mediaConsentStatus,
      notes: c.notes ?? '',
    })),
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [applied, setApplied] = useState<SafeEditResponse | null>(null);

  const setInstance = (id: number, patch: Partial<(typeof instances)[number]>) =>
    setInstances(prev => prev.map(i => (i.visitInstanceId === id ? { ...i, ...patch } : i)));

  const save = async () => {
    setBusy(true);
    setError(null);
    setConflict(false);
    const payload = buildChangedOnlyPayload(form, registrant, instances);
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
      // A version conflict is actionable INSIDE the modal (reload and retry), so it stays inline.
      // Anything else is a plain failure the user cannot fix here: surface it and keep the modal open.
      if (err && (err as { response?: { status?: number } }).response?.status === 409) {
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
      <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white dark:bg-slate-900 p-5 shadow-xl">
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
              <legend className="mb-1 text-sm font-bold text-slate-700">{t('visitRequestV2:summary.registrant')}</legend>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                <label className="block text-sm" data-testid="safe-edit-registrant-fullName">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:registrant.fullName')}</span>
                  <input className={field} value={registrant.fullName} onChange={e => setRegistrant({ ...registrant, fullName: e.target.value })} />
                </label>
                <label className="block text-sm" data-testid="safe-edit-registrant-nationality">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:registrant.nationality')}</span>
                  <CountrySelect
                    value={registrant.nationality}
                    ariaLabel={t('visitRequestV2:registrant.nationality')}
                    onChange={value => setRegistrant({ ...registrant, nationality: value })}
                  />
                </label>
                <label className="block text-sm sm:col-span-2" data-testid="safe-edit-registrant-organization">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:registrant.organization')}</span>
                  <PartnerOrgCombobox
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
                    field={{
                      value: registrant.phone,
                      onChange: e => setRegistrant({ ...registrant, phone: e.target.value }),
                    }}
                  />
                </label>
              </div>
            </fieldset>
            {!canEditShared && (
              <p
                data-testid="safe-edit-shared-locked"
                className="mb-3 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600"
              >
                {t('visitRequestV2:safeEdit.sharedLocked')}
              </p>
            )}
            {instances.map(i => (
              <fieldset key={i.visitInstanceId} className="mb-3 rounded-xl border border-slate-200 p-3">
                <legend className="px-1 text-sm font-bold text-[#004c91]">{i.campusName}</legend>
                <label className="mt-1 block text-sm">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:summary.transportation')}</span>
                  <input data-testid={`safe-edit-transportation-${i.visitInstanceId}`} className={field} value={i.transportationNote} onChange={e => setInstance(i.visitInstanceId, { transportationNote: e.target.value })} />
                </label>
                {/* The contact profile has exactly one door now — "Manage the contact role" on the
                    campus's own detail screen (plan PEMS_CONTACT_ONE_DOOR). Quick Edit no longer offers
                    a second one: correcting a typo here used to be indistinguishable, at the database
                    level, from the deeper edits that workflow gates behind invitation/confirmation. */}
                <p
                  data-testid={`safe-edit-contact-managed-elsewhere-${i.visitInstanceId}`}
                  className="mt-3 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600"
                >
                  {t('visitRequestV2:safeEdit.contactManagedElsewhere')}
                </p>
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
