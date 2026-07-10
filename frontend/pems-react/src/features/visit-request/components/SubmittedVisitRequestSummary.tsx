import React from 'react';
import { CheckCircle2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { VisitRequestSchema } from '../schema/visitRequest.schema';
import type { VerifyResponse } from '../api/visitRequestApi';

/** Immutable snapshot of a successfully submitted request (kept in React memory only). */
export interface SubmittedVisitRequest {
  response: VerifyResponse;
  values: VisitRequestSchema;
}

interface Props {
  submission: SubmittedVisitRequest;
  /** Focus target after OTP success (heading gets tabIndex={-1}). */
  headingRef?: React.Ref<HTMLHeadingElement>;
}

/**
 * PEMS datetimes are local wall-clock strings ("YYYY-MM-DDTHH:mm", VN time).
 * Format them without timezone conversion: build the exact wall-clock instant in UTC
 * and render it in UTC, so a viewer in any timezone sees the entered VN time.
 */
function formatWallClock(value: string, locale: string): string {
  const m = value?.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/);
  if (!m) return value || '';
  const [, y, mo, d, h, mi] = m;
  const utc = new Date(Date.UTC(+y, +mo - 1, +d, +h, +mi));
  return new Intl.DateTimeFormat(locale, {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC',
  }).format(utc);
}

/**
 * Status comes from the backend but the label is frontend i18n — the raw backend
 * `message` is Vietnamese and must never leak into the English UI.
 */
const getSubmittedStatusPresentation = (status: string) => {
  switch (status) {
    case 'PENDING_APPROVAL':
      return { labelKey: 'visitRequest:result.status.pendingApproval', kind: 'pending' as const };
    default:
      return { labelKey: 'visitRequest:result.status.received', kind: 'neutral' as const };
  }
};

const STATUS_BADGE_CLS: Record<'pending' | 'neutral', string> = {
  pending:
    'inline-flex min-w-[96px] items-center justify-center whitespace-nowrap rounded-full border border-yellow-200 bg-yellow-50 px-2.5 py-1 text-xs font-semibold text-yellow-700',
  neutral:
    'inline-flex min-w-[96px] items-center justify-center whitespace-nowrap rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-600',
};

export const SubmittedVisitRequestSummary: React.FC<Props> = ({ submission, headingRef }) => {
  const { t, i18n } = useTranslation(['visitRequest']);
  const { response, values } = submission;
  const locale = i18n.language?.startsWith('vi') ? 'vi-VN' : 'en-GB';

  const empty = t('visitRequest:result.emptyValue');
  const show = (value?: string | null) => (value && value.trim() ? value : empty);

  const status = getSubmittedStatusPresentation(response.status);

  const campusLabel = (code: string) =>
    t(`visitRequest:step2Info.campusOptions.${code}`, code);
  const visitTypeLabel =
    values.visitType === 'OTHER'
      ? t('visitRequest:step2Info.typeOther')
      : t(`visitRequest:step2Info.visitTypes.${values.visitType}`, values.visitType);
  const visitModeLabel =
    values.visitMode === 'multiple'
      ? t('visitRequest:step2Info.multiCampus')
      : t('visitRequest:step2Info.singleCampus');
  const workingLanguageLabel =
    values.workingLanguage === 'EN' ? t('visitRequest:step3.en') : t('visitRequest:step3.vi');
  const mediaConsentLabel =
    values.mediaConsentStatus === 'AGREED'
      ? t('visitRequest:step3.agreed')
      : t('visitRequest:step3.declined');

  const personColumns = [
    t('visitRequest:excel.template.index'),
    t('visitRequest:excel.template.fullName'),
    t('visitRequest:excel.template.jobTitle'),
    t('visitRequest:excel.template.organization'),
    t('visitRequest:excel.template.nationality'),
  ];

  return (
    <div>
      {/* ── Result banner ─────────────────────────────────────────────────── */}
      <div role="status" className="flex flex-col items-center border-b border-slate-200 pb-7 text-center">
        <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-green-100">
          <CheckCircle2 className="h-8 w-8 text-green-600" />
        </div>
        <h2
          ref={headingRef}
          tabIndex={-1}
          className="text-xl font-extrabold text-slate-900 outline-none sm:text-2xl"
        >
          {t('visitRequest:result.title')}
        </h2>
        <p className="mt-1 text-sm text-slate-500">{t('visitRequest:result.description')}</p>

        <div className="mt-4 flex flex-wrap items-center justify-center gap-x-6 gap-y-2">
          <span className="flex items-center gap-2 text-sm text-slate-600">
            <span className="font-semibold">{t('visitRequest:result.statusLabel')}:</span>
            <span className={STATUS_BADGE_CLS[status.kind]}>{t(status.labelKey)}</span>
          </span>
          <span className="flex items-center gap-2 text-sm text-slate-600">
            <span className="font-semibold">{t('visitRequest:result.requestCode')}:</span>
            <span className="text-base font-extrabold tracking-wider text-[#004c91]">
              {response.requestCode}
            </span>
          </span>
        </div>

        <p className="mt-3 text-xs text-slate-500">{t('visitRequest:result.emailHint')}</p>
      </div>

      <h3 className="mt-7 text-lg font-extrabold text-[#004c91]">
        {t('visitRequest:result.reviewTitle')}
      </h3>

      {/* ── A. Registrant ─────────────────────────────────────────────────── */}
      <ReviewSection title={t('visitRequest:singleForm.sections.registrant')}>
        <dl className="grid grid-cols-1 gap-x-10 gap-y-4 sm:grid-cols-2">
          <ReviewField label={t('visitRequest:step1.fullName')} value={show(values.registerInfo.fullName)} />
          <ReviewField label={t('visitRequest:step1.nationality')} value={show(values.registerInfo.nationality)} />
          <ReviewField label={t('visitRequest:step1.organization')} value={show(values.registerInfo.organization)} />
          <ReviewField label={t('visitRequest:step1.jobTitle')} value={show(values.registerInfo.jobTitle)} />
          <ReviewField label={t('visitRequest:step1.phone')} value={show(values.registerInfo.phone)} />
          <ReviewField label={t('visitRequest:step1.email')} value={show(values.registerInfo.email)} />
        </dl>
      </ReviewSection>

      {/* ── B. Visit ──────────────────────────────────────────────────────── */}
      <ReviewSection title={t('visitRequest:singleForm.sections.visit')}>
        <dl className="grid grid-cols-1 gap-x-10 gap-y-4 sm:grid-cols-2">
          <ReviewField label={t('visitRequest:step2Info.delegationName')} value={show(values.delegationName)} />
          <ReviewField label={t('visitRequest:step2Info.visitMode')} value={visitModeLabel} />
          <ReviewField label={t('visitRequest:step2Info.visitType')} value={visitTypeLabel} />
          {values.visitType === 'OTHER' && (
            <ReviewField label={t('visitRequest:step2Info.visitTypeOther')} value={show(values.visitTypeOther)} />
          )}
          <div className="sm:col-span-2">
            <ReviewField label={t('visitRequest:step2Info.purpose')} value={show(values.purpose)} />
          </div>
          <div className="sm:col-span-2">
            <ReviewField label={t('visitRequest:step2Info.workingContent')} value={show(values.workingContent)} />
          </div>
        </dl>
      </ReviewSection>

      {/* ── C. Schedule ───────────────────────────────────────────────────── */}
      <ReviewSection title={t('visitRequest:singleForm.sections.schedule')}>
        {/* Desktop table */}
        <div className="hidden overflow-x-auto rounded-xl border border-slate-200 sm:block">
          <table className="w-full border-collapse text-sm">
            <thead className="border-b border-slate-200 bg-slate-50">
              <tr>
                <th className="p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Info.campusLabel')}</th>
                <th className="p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Info.startTime')}</th>
                <th className="p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Info.endTime')}</th>
                <th className="p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Info.timezone')}</th>
              </tr>
            </thead>
            <tbody>
              {values.visits.map((visit, i) => (
                <tr key={i} className="border-b border-slate-100 last:border-b-0">
                  <td className="p-3 font-semibold text-slate-800">{campusLabel(visit.campus)}</td>
                  <td className="p-3 text-slate-700">{formatWallClock(visit.startDatetime, locale)}</td>
                  <td className="p-3 text-slate-700">{formatWallClock(visit.endDatetime, locale)}</td>
                  <td className="p-3 text-slate-700">VN (GMT+7)</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {/* Mobile list */}
        <div className="sm:hidden">
          {values.visits.map((visit, i) => (
            <dl key={i} className="grid grid-cols-1 gap-y-3 border-b border-slate-200 py-4 first:pt-0 last:border-b-0">
              <ReviewField label={t('visitRequest:step2Info.campusLabel')} value={campusLabel(visit.campus)} />
              <ReviewField label={t('visitRequest:step2Info.startTime')} value={formatWallClock(visit.startDatetime, locale)} />
              <ReviewField label={t('visitRequest:step2Info.endTime')} value={formatWallClock(visit.endDatetime, locale)} />
              <ReviewField label={t('visitRequest:step2Info.timezone')} value="VN (GMT+7)" />
            </dl>
          ))}
        </div>
      </ReviewSection>

      {/* ── D. Visitors ───────────────────────────────────────────────────── */}
      <ReviewSection title={t('visitRequest:singleForm.sections.visitors')}>
        <PersonTable columns={personColumns} people={values.visitors} empty={empty} />
      </ReviewSection>

      {/* ── E. Support team ───────────────────────────────────────────────── */}
      <ReviewSection title={t('visitRequest:singleForm.sections.support')}>
        <PersonTable columns={personColumns} people={values.supportTeam} empty={empty} />
      </ReviewSection>

      {/* ── F. Contact point ──────────────────────────────────────────────── */}
      <ReviewSection title={t('visitRequest:singleForm.sections.contact')}>
        <dl className="grid grid-cols-1 gap-x-10 gap-y-4 sm:grid-cols-2">
          <ReviewField label={t('visitRequest:excel.template.fullName')} value={show(values.contactPoint.fullName)} />
          <ReviewField label={t('visitRequest:excel.template.organization')} value={show(values.contactPoint.organization)} />
          <ReviewField label={t('visitRequest:step1.phone')} value={show(values.contactPoint.phone)} />
          <ReviewField label={t('visitRequest:step1.email')} value={show(values.contactPoint.email)} />
        </dl>
      </ReviewSection>

      {/* ── G. Additional ─────────────────────────────────────────────────── */}
      <ReviewSection title={t('visitRequest:singleForm.sections.additional')}>
        <dl className="grid grid-cols-1 gap-x-10 gap-y-4 sm:grid-cols-2">
          <ReviewField label={t('visitRequest:result.fields.workingLanguage')} value={workingLanguageLabel} />
          <ReviewField label={t('visitRequest:result.fields.transportation')} value={show(values.transportationNote)} />
          <ReviewField label={t('visitRequest:result.fields.mediaConsent')} value={mediaConsentLabel} />
          <ReviewField label={t('visitRequest:result.fields.mediaConsentNote')} value={show(values.mediaConsentNote)} />
          <div className="sm:col-span-2">
            <ReviewField label={t('visitRequest:result.fields.notes')} value={show(values.notes)} />
          </div>
        </dl>
      </ReviewSection>
    </div>
  );
};

/** Flat review group: heading + divider, no card. */
const ReviewSection: React.FC<{ title: string; children: React.ReactNode }> = ({ title, children }) => (
  <section className="border-b border-slate-200 py-6 last:border-b-0 last:pb-0">
    <h4 className="mb-4 text-base font-extrabold text-slate-900">{title}</h4>
    {children}
  </section>
);

function ReviewField({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className="mt-1 break-words text-sm font-semibold text-slate-800">{value || '—'}</dd>
    </div>
  );
}

interface PersonRow {
  fullName: string;
  jobTitle: string;
  organization: string;
  nationality: string;
}

/** Read-only people list: flat table on desktop, divided rows on mobile. */
const PersonTable: React.FC<{ columns: string[]; people: PersonRow[]; empty: string }> = ({ columns, people, empty }) => {
  const [sttCol, nameCol, jobCol, orgCol, natCol] = columns;
  const show = (v?: string) => (v && v.trim() ? v : empty);
  return (
    <>
      <div className="hidden overflow-x-auto rounded-xl border border-slate-200 sm:block">
        <table className="w-full border-collapse text-sm">
          <thead className="border-b border-slate-200 bg-slate-50">
            <tr>
              <th className="w-12 p-3 text-center font-bold text-slate-700">{sttCol}</th>
              <th className="p-3 text-left font-bold text-slate-700">{nameCol}</th>
              <th className="p-3 text-left font-bold text-slate-700">{jobCol}</th>
              <th className="p-3 text-left font-bold text-slate-700">{orgCol}</th>
              <th className="p-3 text-left font-bold text-slate-700">{natCol}</th>
            </tr>
          </thead>
          <tbody>
            {people.map((p, i) => (
              <tr key={i} className="border-b border-slate-100 last:border-b-0">
                <td className="p-3 text-center font-bold text-slate-400">{i + 1}</td>
                <td className="p-3 font-semibold text-slate-800">{show(p.fullName)}</td>
                <td className="p-3 text-slate-700">{show(p.jobTitle)}</td>
                <td className="p-3 text-slate-700">{show(p.organization)}</td>
                <td className="p-3 text-slate-700">{show(p.nationality)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="sm:hidden">
        {people.map((p, i) => (
          <dl key={i} className="grid grid-cols-1 gap-y-3 border-b border-slate-200 py-4 first:pt-0 last:border-b-0">
            <ReviewField label={sttCol} value={String(i + 1)} />
            <ReviewField label={nameCol} value={show(p.fullName)} />
            <ReviewField label={jobCol} value={show(p.jobTitle)} />
            <ReviewField label={orgCol} value={show(p.organization)} />
            <ReviewField label={natCol} value={show(p.nationality)} />
          </dl>
        ))}
      </div>
    </>
  );
};
