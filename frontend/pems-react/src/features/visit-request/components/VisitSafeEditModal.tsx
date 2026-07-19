import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { X } from 'lucide-react';
import {
  patchSafeDetails,
  type ResolvedVisitForm,
  type SafeEditPayload,
  type SafeEditResponse,
} from '../api/visitRequestV2Api';
import { errorCodeOf } from '../utils/visitV2Actions';

interface Props {
  form: ResolvedVisitForm;
  onClose: () => void;
  onSaved: () => void;
}

/**
 * Safe / privacy-urgent edit (plan §16.5). Apply-now corrections: registrant + primary-contact
 * presentation fields and per-instance transportation / note / media consent. Media DECLINED applies
 * immediately even <24h (backend authority). Optimistic concurrency via row versions → a stable 409
 * shows a steady message and a reload; the proposal is never presented as an approval-pending amendment.
 */
export default function VisitSafeEditModal({ form, onClose, onSaved }: Props) {
  const { t } = useTranslation(['visitRequestV2']);

  const [registrant, setRegistrant] = useState({
    fullName: form.registrant.fullName,
    organization: form.registrant.organization,
    jobTitle: form.registrant.jobTitle,
    phone: form.registrant.phone,
  });
  const [contact, setContact] = useState({
    fullName: form.primaryContact.fullName,
    organization: form.primaryContact.organization,
    phone: form.primaryContact.phone,
  });
  const [instances, setInstances] = useState(
    form.campusVisits.map(c => ({
      visitInstanceId: c.visitInstanceId,
      expectedRowVersion: c.rowVersion,
      campusName: c.campusName,
      transportationNote: c.transportationNote ?? '',
      noteToFptu: c.noteToFptu ?? '',
      mediaConsentStatus: c.mediaConsentStatus,
      mediaConsentNote: c.mediaConsentNote ?? '',
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
    const payload: SafeEditPayload = {
      expectedRequestRowVersion: form.rowVersion,
      registrant: {
        fullName: registrant.fullName.trim(),
        organization: registrant.organization.trim() || null,
        jobTitle: registrant.jobTitle.trim() || null,
        phone: registrant.phone.trim() || null,
      },
      contact: {
        fullName: contact.fullName.trim(),
        organization: contact.organization.trim() || null,
        phone: contact.phone.trim(),
      },
      instances: instances.map(i => ({
        visitInstanceId: i.visitInstanceId,
        expectedRowVersion: i.expectedRowVersion,
        transportationNote: i.transportationNote.trim() || null,
        noteToFptu: i.noteToFptu.trim() || null,
        mediaConsentStatus: i.mediaConsentStatus,
        mediaConsentNote: i.mediaConsentNote.trim() || null,
      })),
    };
    try {
      const res = await patchSafeDetails(form.visitRequestId, payload);
      setApplied(res);
      onSaved();
    } catch (err) {
      const code = errorCodeOf(err);
      if (err && (err as { response?: { status?: number } }).response?.status === 409) {
        setConflict(true);
        setError(t('visitRequestV2:safeEdit.conflict'));
      } else {
        setError(code ? t('visitRequestV2:safeEdit.errGeneric') : t('visitRequestV2:safeEdit.errGeneric'));
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
          <div className="space-y-2">
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
            <fieldset className="mb-3">
              <legend className="mb-1 text-sm font-bold text-slate-700">{t('visitRequestV2:summary.registrant')}</legend>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                <input className={field} value={registrant.fullName} onChange={e => setRegistrant({ ...registrant, fullName: e.target.value })} />
                <input className={field} value={registrant.phone} onChange={e => setRegistrant({ ...registrant, phone: e.target.value })} />
                <input className={field} value={registrant.organization} onChange={e => setRegistrant({ ...registrant, organization: e.target.value })} />
                <input className={field} value={registrant.jobTitle} onChange={e => setRegistrant({ ...registrant, jobTitle: e.target.value })} />
              </div>
            </fieldset>
            <fieldset className="mb-3">
              <legend className="mb-1 text-sm font-bold text-slate-700">{t('visitRequestV2:summary.primaryContact')}</legend>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                <input className={field} value={contact.fullName} onChange={e => setContact({ ...contact, fullName: e.target.value })} />
                <input className={field} value={contact.phone} onChange={e => setContact({ ...contact, phone: e.target.value })} />
                <input className={field} value={contact.organization} onChange={e => setContact({ ...contact, organization: e.target.value })} />
              </div>
              <p className="mt-1 text-[11px] text-slate-500">{t('visitRequestV2:safeEdit.emailImmutable')}</p>
            </fieldset>
            {instances.map(i => (
              <fieldset key={i.visitInstanceId} className="mb-3 rounded-xl border border-slate-200 p-3">
                <legend className="px-1 text-sm font-bold text-[#004c91]">{i.campusName}</legend>
                <label className="mt-1 block text-sm">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:summary.transportation')}</span>
                  <input className={field} value={i.transportationNote} onChange={e => setInstance(i.visitInstanceId, { transportationNote: e.target.value })} />
                </label>
                <label className="mt-2 block text-sm">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:summary.campusNote')}</span>
                  <input className={field} value={i.noteToFptu} onChange={e => setInstance(i.visitInstanceId, { noteToFptu: e.target.value })} />
                </label>
                <label className="mt-2 block text-sm">
                  <span className="mb-1 block text-xs font-semibold text-slate-600">{t('visitRequestV2:summary.mediaConsent')}</span>
                  <select className={field} value={i.mediaConsentStatus} onChange={e => setInstance(i.visitInstanceId, { mediaConsentStatus: e.target.value })}>
                    <option value="AGREED">{t('visitRequestV2:summary.mediaAgreed')}</option>
                    <option value="DECLINED">{t('visitRequestV2:summary.mediaDeclined')}</option>
                  </select>
                </label>
              </fieldset>
            ))}

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
              <button type="button" disabled={busy} onClick={() => void save()}
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
