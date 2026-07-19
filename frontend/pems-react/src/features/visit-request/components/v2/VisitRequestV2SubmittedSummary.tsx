import { useTranslation } from 'react-i18next';
import type { V2CreateResponse } from '../../api/visitRequestV2Api';
import type { CampusVisitSchema, VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';
import { useRegistrationCampuses } from '../../hooks/useRegistrationCampuses';

interface Props {
  response: V2CreateResponse;
  /** The IMMUTABLE snapshot of exactly what was submitted — the source of truth for the summary,
   * so editing one campus can never retroactively change another campus's card. */
  values: VisitRequestV2Schema;
}

/** "YYYY-MM-DDTHH:mm" wall-clock → "DD/MM/YYYY HH:mm" without any timezone shift. */
function formatWallClock(value: string): string {
  const [datePart, timePart] = value.split('T');
  if (!datePart) return value;
  const [y, m, d] = datePart.split('-');
  if (!y || !m || !d) return value;
  return `${d}/${m}/${y}${timePart ? ` ${timePart.slice(0, 5)}` : ''}`;
}

function durationParts(start: string, end: string): { hours: number; minutes: number } | null {
  const s = new Date(start).getTime();
  const e = new Date(end).getTime();
  if (Number.isNaN(s) || Number.isNaN(e) || e <= s) return null;
  const totalMin = Math.round((e - s) / 60000);
  return { hours: Math.floor(totalMin / 60), minutes: totalMin % 60 };
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-bold uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className="mt-0.5 break-words text-sm text-slate-800">{children}</dd>
    </div>
  );
}

/**
 * Full post-submit summary (plan §8.3 / §4.9–4.10): request-level identity once, then ONE card per
 * campus rendered from the submitted snapshot — never "the first campus as representative". Reads the
 * immutable `values` for content and the `response` for the request code / per-campus instance status.
 */
export function VisitRequestV2SubmittedSummary({ response, values }: Props) {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest']);
  const { campuses } = useRegistrationCampuses();

  const campusName = (code: string): string =>
    campuses.find((c) => c.campusCode === code)?.campusName ?? code;
  const campusId = (code: string): number | undefined =>
    campuses.find((c) => c.campusCode === code)?.campusId;

  // Reliable per-campus instance link: campus code → campusId → the matching response instance
  // (never positional — the response order is not guaranteed to match the submitted order).
  const instanceFor = (code: string) => {
    const id = campusId(code);
    return id == null ? undefined : response.instances.find((i) => i.campusId === id);
  };

  const none = t('visitRequestV2:summary.none');
  const person = (p: { fullName: string; organization?: string; jobTitle?: string; nationality?: string }) =>
    [p.fullName, p.organization, p.jobTitle, p.nationality].filter((x) => x && x.trim()).join(' — ');

  return (
    <div className="space-y-5">
      {/* Request-level */}
      <section className="rounded-2xl border border-slate-200 bg-white p-5">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-base font-extrabold text-slate-900">{t('visitRequestV2:summary.heading')}</h2>
          <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-bold text-slate-700">
            {response.hasMixedCampusDetails
              ? t('visitRequestV2:summary.mixedBadge')
              : t('visitRequestV2:summary.uniformBadge')}
          </span>
        </div>
        <dl className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field label={t('visitRequestV2:summary.requestCode')}>
            <span className="font-bold">{response.requestCode}</span>
          </Field>
          <Field label={t('visitRequestV2:summary.aggregateStatus')}>{t('visitRequestV2:summary.submitted')}</Field>
          <Field label={t('visitRequestV2:summary.campusCount')}>{response.instances.length}</Field>
          <Field label={t('visitRequestV2:summary.partner')}>
            {values.partnerSelectionMode === 'EXISTING_PARTNER' && values.partnerId != null
              ? t('visitRequestV2:summary.partnerExisting', { id: values.partnerId })
              : t('visitRequestV2:summary.partnerNew')}
          </Field>
          <Field label={t('visitRequestV2:summary.registrant')}>
            {person(values.registerInfo)}
            <div className="text-xs text-slate-500">
              {values.registerInfo.phone} · {values.registerInfo.email}
            </div>
          </Field>
          <Field label={t('visitRequestV2:summary.primaryContact')}>
            {person(values.contactPoint)}
            <div className="text-xs text-slate-500">
              {values.contactPoint.phone} · {values.contactPoint.email} ·{' '}
              {response.primaryContactAccessStatus === 'ACTIVE'
                ? t('visitRequestV2:summary.contactActive')
                : t('visitRequestV2:summary.contactPending')}
            </div>
          </Field>
        </dl>
      </section>

      {/* One card per campus, from the submitted snapshot */}
      {values.campusVisits.map((cv: CampusVisitSchema, index: number) => {
        const instance = instanceFor(cv.campus);
        const dur = durationParts(cv.startDatetime, cv.endDatetime);
        const visitTypeLabel = cv.visitType === 'OTHER' && cv.visitTypeOther
          ? cv.visitTypeOther
          : t(`visitRequest:step2Info.visitTypes.${cv.visitType}`, cv.visitType);
        return (
          <section
            key={cv.clientKey}
            data-testid={`campus-summary-${index}`}
            className="rounded-2xl border border-slate-200 bg-white p-5"
          >
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h3 className="text-base font-extrabold text-[#004c91]">
                {t('visitRequestV2:summary.campusHeading', { index: index + 1, campus: campusName(cv.campus) })}
              </h3>
              {instance && (
                <span className="rounded-full bg-blue-50 px-2.5 py-0.5 text-xs font-bold text-[#004c91]">
                  {t('visitRequestV2:summary.instanceStatus')}:{' '}
                  {t(`visitRequestV2:status.${instance.status}`, instance.status)}
                </span>
              )}
            </div>

            <dl className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field label={t('visitRequestV2:summary.schedule')}>
                {formatWallClock(cv.startDatetime)} → {formatWallClock(cv.endDatetime)}
              </Field>
              <Field label={t('visitRequestV2:summary.duration')}>
                {dur ? t('visitRequestV2:summary.durationValue', dur) : none}
                <span className="ml-2 text-xs text-slate-500">{t('visitRequestV2:summary.timezoneValue')}</span>
              </Field>
              <Field label={t('visitRequestV2:summary.delegationName')}>{cv.delegationName || none}</Field>
              <Field label={t('visitRequestV2:summary.visitType')}>{visitTypeLabel}</Field>
              <Field label={t('visitRequestV2:summary.workingLanguage')}>
                {cv.workingLanguage === 'VI'
                  ? t('visitRequestV2:summary.languageVI')
                  : t('visitRequestV2:summary.languageEN')}
              </Field>
              <Field label={t('visitRequestV2:summary.mediaConsent')}>
                {cv.mediaConsentStatus === 'AGREED'
                  ? t('visitRequestV2:summary.mediaAgreed')
                  : t('visitRequestV2:summary.mediaDeclined')}
                {cv.mediaConsentNote ? ` — ${cv.mediaConsentNote}` : ''}
              </Field>
              <div className="sm:col-span-2">
                <Field label={t('visitRequestV2:summary.purpose')}>{cv.purpose || none}</Field>
              </div>
              {cv.workingContent ? (
                <div className="sm:col-span-2">
                  <Field label={t('visitRequestV2:summary.workingContent')}>{cv.workingContent}</Field>
                </div>
              ) : null}
              <div className="sm:col-span-2">
                <Field label={t('visitRequestV2:summary.visitors', { count: cv.visitors.length })}>
                  <ul className="list-disc space-y-0.5 pl-5">
                    {cv.visitors.map((v, i) => <li key={i}>{person(v)}</li>)}
                  </ul>
                </Field>
              </div>
              {cv.supportTeam.length > 0 && (
                <div className="sm:col-span-2">
                  <Field label={t('visitRequestV2:summary.supportTeam', { count: cv.supportTeam.length })}>
                    <ul className="list-disc space-y-0.5 pl-5">
                      {cv.supportTeam.map((v, i) => <li key={i}>{person(v)}</li>)}
                    </ul>
                  </Field>
                </div>
              )}
              <div className="sm:col-span-2">
                <Field label={t('visitRequestV2:summary.operationalContact')}>
                  {[cv.operationalContact.fullName, cv.operationalContact.organization]
                    .filter((x) => x && x.trim())
                    .join(' — ')}
                  <div className="text-xs text-slate-500">
                    {[cv.operationalContact.phone, cv.operationalContact.email].filter((x) => x && x.trim()).join(' · ') || none}
                  </div>
                </Field>
              </div>
              {cv.transportationNote ? (
                <Field label={t('visitRequestV2:summary.transportation')}>{cv.transportationNote}</Field>
              ) : null}
              {cv.notes ? (
                <Field label={t('visitRequestV2:summary.campusNote')}>{cv.notes}</Field>
              ) : null}
            </dl>
          </section>
        );
      })}
    </div>
  );
}
