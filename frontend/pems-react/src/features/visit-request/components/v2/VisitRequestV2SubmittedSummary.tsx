import { useTranslation } from 'react-i18next';
import type { V2CreateResponse } from '../../api/visitRequestV2Api';
import type { CampusVisitSchema, VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';
import { useRegistrationCampuses } from '../../hooks/useRegistrationCampuses';
import { VisitStatusBadge } from './shared/VisitStatusBadge';

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

  /** A blank cell reads as "we lost this"; say plainly that it was not provided. */
  const cell = (value?: string | null) =>
    value && value.trim() ? value : <span className="text-slate-400">{t('visitRequestV2:summary.notProvided')}</span>;

  /**
   * The people on the visit, as a real table.
   *
   * They used to be a bulleted list with the four fields glued together by em dashes — "Nguyễn Văn A
   * — FPT — Trưởng phòng — Việt Nam". Nobody can scan that for a nationality, a missing field just
   * collapses the dashes so you cannot tell WHICH one is missing, and a receipt someone prints and
   * carries to reception needs a row number to point at. Columns and an STT fix all three.
   *
   * Desktop gets the table; narrow screens get stacked cards that keep the same STT, because a
   * five-column table on a phone is unreadable in a different way.
   */
  const PeopleTable = ({
    people, label, testId,
  }: {
    people: Array<{ fullName: string; organization?: string; jobTitle?: string; nationality?: string }>;
    label: string;
    testId: string;
  }) => {
    if (people.length === 0) {
      return (
        <div className="sm:col-span-2">
          <dt className="text-xs font-bold uppercase tracking-wide text-slate-500">{label}</dt>
          <dd data-testid={`${testId}-empty`} className="mt-1 text-sm italic text-slate-400">
            {t('visitRequestV2:summary.noMembers')}
          </dd>
        </div>
      );
    }
    const headers = [
      t('visitRequestV2:summary.colIndex'),
      t('visitRequestV2:summary.colFullName'),
      t('visitRequestV2:summary.colJobTitle'),
      t('visitRequestV2:summary.colOrganization'),
      t('visitRequestV2:summary.colNationality'),
    ];
    return (
      <div className="sm:col-span-2">
        <p className="mb-1.5 text-xs font-bold uppercase tracking-wide text-slate-500">{label}</p>

        {/* Wide content scrolls inside its own box so the receipt never scrolls sideways. */}
        <div className="hidden overflow-x-auto rounded-xl border border-slate-200 sm:block">
          <table data-testid={testId} className="w-full min-w-[520px] border-collapse text-sm">
            <thead>
              <tr className="bg-slate-50 text-left">
                {headers.map((h, i) => (
                  <th
                    key={h}
                    scope="col"
                    className={`border-b border-slate-200 px-3 py-2 text-xs font-bold uppercase tracking-wide text-slate-600 ${
                      i === 0 ? 'w-12 text-right' : ''
                    }`}
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {people.map((p, i) => (
                <tr key={i} className="odd:bg-white even:bg-slate-50/60">
                  {/* STT restarts at 1 in EVERY table — guests and support are counted separately. */}
                  <td className="border-b border-slate-100 px-3 py-2 text-right font-semibold text-slate-500">
                    {i + 1}
                  </td>
                  <td className="border-b border-slate-100 px-3 py-2 font-medium text-slate-900">{cell(p.fullName)}</td>
                  <td className="border-b border-slate-100 px-3 py-2 text-slate-700">{cell(p.jobTitle)}</td>
                  <td className="border-b border-slate-100 px-3 py-2 text-slate-700">{cell(p.organization)}</td>
                  <td className="border-b border-slate-100 px-3 py-2 text-slate-700">{cell(p.nationality)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <ul data-testid={`${testId}-mobile`} className="space-y-2 sm:hidden">
          {people.map((p, i) => (
            <li key={i} className="rounded-xl border border-slate-200 p-3">
              <p className="flex items-baseline gap-2">
                <span className="text-xs font-bold text-slate-500">{i + 1}.</span>
                <span className="font-semibold text-slate-900">{cell(p.fullName)}</span>
              </p>
              <dl className="mt-1 space-y-0.5 pl-6 text-xs text-slate-600">
                <div className="flex gap-1.5">
                  <dt className="font-semibold">{t('visitRequestV2:summary.colJobTitle')}:</dt>
                  <dd>{cell(p.jobTitle)}</dd>
                </div>
                <div className="flex gap-1.5">
                  <dt className="font-semibold">{t('visitRequestV2:summary.colOrganization')}:</dt>
                  <dd>{cell(p.organization)}</dd>
                </div>
                <div className="flex gap-1.5">
                  <dt className="font-semibold">{t('visitRequestV2:summary.colNationality')}:</dt>
                  <dd>{cell(p.nationality)}</dd>
                </div>
              </dl>
            </li>
          ))}
        </ul>
      </div>
    );
  };

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
          {/* The organization NAME the user picked, with "already in our system" as the note under
              it — never the partner's primary key. `partnerId` is still what the payload carries and
              what the backend links against; it is simply not a fact this reader can do anything
              with, and printing "(ID 109)" on a receipt exposes an internal identifier for nothing.
              The name comes from the submitted snapshot: selecting a partner in the combobox is what
              wrote it into `registerInfo.organization`. */}
          <Field label={t('visitRequestV2:summary.partner')}>
            {values.partnerSelectionMode === 'EXISTING_PARTNER' && values.partnerId != null ? (
              <span data-testid="v2-summary-partner-existing">
                {values.registerInfo.organization?.trim() && (
                  <span className="font-semibold">{values.registerInfo.organization.trim()}<br /></span>
                )}
                <span className="text-xs text-slate-500">{t('visitRequestV2:summary.partnerExisting')}</span>
              </span>
            ) : t('visitRequestV2:summary.partnerNew')}
          </Field>
          <Field label={t('visitRequestV2:summary.registrant')}>
            {person(values.registerInfo)}
            <div className="text-xs text-slate-500">
              {values.registerInfo.phone} · {values.registerInfo.email}
            </div>
          </Field>
          {/* No request-level contact any more: each campus names its own, and the only
              request-level fact is how many of them still have to answer. */}
          <Field label={t('visitRequestV2:summary.contactConfirmation')}>
            {response.pendingContactConfirmations > 0
              ? t('visitRequestV2:summary.contactPendingCount', { count: response.pendingContactConfirmations })
              : t('visitRequestV2:summary.contactAllConfirmed')}
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
                <span className="inline-flex items-center gap-1.5 text-xs font-bold text-slate-600">
                  {t('visitRequestV2:summary.instanceStatus')}:
                  <VisitStatusBadge kind="instance" status={instance.status} />
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
              </Field>
              <div className="sm:col-span-2">
                <Field label={t('visitRequestV2:summary.purpose')}>{cv.purpose || none}</Field>
              </div>
              {/* Kept in place when blank, like purpose above: this is the guest's receipt for what
                  they just submitted, and a field that silently drops out looks like it was lost. */}
              <div className="sm:col-span-2">
                <Field label={t('visitRequestV2:summary.workingContent')}>{cv.workingContent || none}</Field>
              </div>
              {/* Guests and support get SEPARATE tables, each numbered from 1 — they are different
                  groups arriving under different rules, and one running count would imply otherwise. */}
              <PeopleTable
                people={cv.visitors}
                label={t('visitRequestV2:summary.visitors', { count: cv.visitors.length })}
                testId={`campus-${index}-visitors-table`}
              />
              <PeopleTable
                people={cv.supportTeam}
                label={t('visitRequestV2:summary.supportTeam', { count: cv.supportTeam.length })}
                testId={`campus-${index}-support-table`}
              />
              <div className="sm:col-span-2">
                <Field label={t('visitRequestV2:summary.operationalContact')}>
                  {/* Job title is part of what the form asks for, and it is the line that tells the
                      campus whether the person on the other end can settle a schedule or has to go
                      and ask. It was collected and then dropped from this post-submit summary. */}
                  {[cv.operationalContact.fullName, cv.operationalContact.jobTitle, cv.operationalContact.organization]
                    .filter((x) => x && x.trim())
                    .join(' — ')}
                  <div className="text-xs text-slate-500">
                    {[cv.operationalContact.phone, cv.operationalContact.email].filter((x) => x && x.trim()).join(' · ') || none}
                  </div>
                </Field>
              </div>
              <Field label={t('visitRequestV2:summary.transportation')}>{cv.transportationNote || none}</Field>
              <Field label={t('visitRequestV2:summary.campusNote')}>{cv.notes || none}</Field>
            </dl>
          </section>
        );
      })}
    </div>
  );
}
