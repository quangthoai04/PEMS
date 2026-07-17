import React, { useRef, useState } from 'react';
import { useFieldArray, type UseFormReturn } from 'react-hook-form';
import { AlertCircle, ChevronDown, Copy, FileSpreadsheet, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';
import type { RegistrationCampusOption } from '../../api/visitRequestApi';
import { FormField, inputCls } from '../shared/FormField';
import {
  isAllowedExcelFile,
  validateSupportTeamExcel,
  validateVisitorExcel,
  type ExcelTranslator,
} from '../ExcelUpload/excelValidator';
import { downloadVisitorTemplate, downloadSupportTeamTemplate } from '../ExcelUpload/excelDownload';

const MAX_EXCEL_FILE_BYTES = 5 * 1024 * 1024; // 5MB per-campus import cap

const VISIT_TYPES = ['CAMPUS_TOUR', 'MEETING', 'WORKSHOP', 'SIGNING_CEREMONY', 'EXCHANGE', 'OTHER'] as const;

interface Props {
  form: UseFormReturn<VisitRequestV2Schema>;
  index: number;
  /** Stable identity of this card — the React key; NEVER the array index. */
  clientKey: string;
  open: boolean;
  onToggle: () => void;
  campuses: RegistrationCampusOption[];
  campusesLoading: boolean;
  /** Labels of the OTHER cards offered as one-time copy sources (empty → no copy UI). */
  copySources: Array<{ index: number; label: string }>;
  onCopyFrom: (sourceIndex: number) => void;
  onApplyToAll: () => void;
  onRemove: () => void;
  canRemove: boolean;
  showErrors?: boolean;
}

/** Counts leaf errors under one campus card so the collapsed header can show a badge. */
function countErrors(node: unknown): number {
  if (!node || typeof node !== 'object') return 0;
  if ('message' in (node as Record<string, unknown>) && typeof (node as { message?: unknown }).message === 'string') {
    return 1;
  }
  return Object.values(node as Record<string, unknown>).reduce<number>((acc, v) => acc + countErrors(v), 0);
}

/**
 * ONE campus visit card (plan §9.1): a complete, independent snapshot — schedule, content,
 * people, operational contact and requirements. Collapsing only HIDES the body (CSS), the
 * fields stay mounted so React Hook Form never unregisters and no typed data is lost.
 */
export const CampusVisitCard: React.FC<Props> = ({
  form,
  index,
  clientKey,
  open,
  onToggle,
  campuses,
  campusesLoading,
  copySources,
  onCopyFrom,
  onApplyToAll,
  onRemove,
  canRemove,
  showErrors,
}) => {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest']);
  const { register, control, watch, formState: { errors } } = form;
  const base = `campusVisits.${index}` as const;
  const cardErrors = errors.campusVisits?.[index];
  const errorCount = countErrors(cardErrors);

  const visitorFields = useFieldArray({ control, name: `campusVisits.${index}.visitors` });
  const supportFields = useFieldArray({ control, name: `campusVisits.${index}.supportTeam` });

  const [excelMessage, setExcelMessage] = useState<string | null>(null);
  const visitorFileRef = useRef<HTMLInputElement>(null);
  const supportFileRef = useRef<HTMLInputElement>(null);

  const campusCode = watch(`${base}.campus`);
  const visitType = watch(`${base}.visitType`);
  const mediaConsent = watch(`${base}.mediaConsentStatus`);
  const campusName = campuses.find(c => c.campusCode === campusCode)?.campusName;
  const headerLabel = campusName ?? t('visitRequestV2:card.unselectedCampus');

  const excelT: ExcelTranslator = (key, options) => t(key, options);

  const importExcel = async (kind: 'visitors' | 'supportTeam', file: File) => {
    setExcelMessage(null);
    if (!isAllowedExcelFile(file)) {
      setExcelMessage(t('visitRequestV2:excel.invalidType'));
      return;
    }
    if (file.size > MAX_EXCEL_FILE_BYTES) {
      setExcelMessage(t('visitRequestV2:excel.tooLarge', { maxMb: 5 }));
      return;
    }
    // Import applies to THIS campus card only — never to a global member list.
    if (kind === 'visitors') {
      const result = await validateVisitorExcel(file, [], excelT);
      if (!result.valid) {
        setExcelMessage(result.errors[0]?.message ?? t('visitRequestV2:excel.parseFailed'));
        return;
      }
      visitorFields.replace(result.data.map(r => ({
        fullName: r.fullName, jobTitle: r.jobTitle, organization: r.organization, nationality: r.nationality,
      })));
      setExcelMessage(t('visitRequestV2:excel.importedVisitors', { count: result.data.length }));
    } else {
      const result = await validateSupportTeamExcel(file, [], excelT);
      if (!result.valid) {
        setExcelMessage(result.errors[0]?.message ?? t('visitRequestV2:excel.parseFailed'));
        return;
      }
      supportFields.replace(result.data.map(r => ({
        fullName: r.fullName, jobTitle: r.jobTitle, organization: r.organization, nationality: r.nationality,
      })));
      setExcelMessage(t('visitRequestV2:excel.importedSupport', { count: result.data.length }));
    }
  };

  const fieldError = (path: string): string | undefined => {
    const segs = path.split('.');
    let node: unknown = cardErrors;
    for (const s of segs) {
      if (!node || typeof node !== 'object') return undefined;
      node = (node as Record<string, unknown>)[s];
    }
    const msg = (node as { message?: unknown } | undefined)?.message;
    return typeof msg === 'string' ? msg : undefined;
  };

  const bodyId = `campus-card-body-${clientKey}`;

  const personRow = (
    kind: 'visitors' | 'supportTeam',
    rowIndex: number,
    onRemoveRow: () => void,
    removable: boolean,
  ) => (
    <div className="grid grid-cols-1 gap-2 sm:grid-cols-[1fr_1fr_1fr_1fr_auto] items-start">
      {(['fullName', 'jobTitle', 'organization', 'nationality'] as const).map(f => (
        <div key={f}>
          <input
            {...register(`${base}.${kind}.${rowIndex}.${f}`)}
            placeholder={t(`visitRequestV2:person.${f}`)}
            aria-label={t(`visitRequestV2:person.${f}`)}
            className={inputCls(!!fieldError(`${kind}.${rowIndex}.${f}`), false, false)}
          />
          {fieldError(`${kind}.${rowIndex}.${f}`) && (
            <p className="mt-1 text-xs font-semibold text-red-600">{fieldError(`${kind}.${rowIndex}.${f}`)}</p>
          )}
        </div>
      ))}
      <button
        type="button"
        aria-label={t('visitRequestV2:card.removeRow')}
        disabled={!removable}
        className="mt-2 rounded-lg p-2 text-slate-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-30"
        onClick={onRemoveRow}
      >
        <Trash2 className="h-4 w-4" />
      </button>
    </div>
  );

  return (
    <div className="rounded-2xl border border-slate-200 bg-white shadow-sm">
      {/* Header — always visible; collapsing hides ONLY the body below */}
      <div className="flex items-center gap-2 p-4">
        <button
          type="button"
          aria-expanded={open}
          aria-controls={bodyId}
          className="flex min-w-0 flex-1 items-center gap-3 text-left"
          onClick={onToggle}
        >
          <ChevronDown className={`h-5 w-5 shrink-0 text-slate-400 transition-transform ${open ? 'rotate-180' : ''}`} />
          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[#004c91]/10 text-sm font-bold text-[#004c91]">
            {index + 1}
          </span>
          <span className="truncate text-base font-bold text-slate-900">{headerLabel}</span>
          {errorCount > 0 && (
            <span
              role="status"
              className="ml-1 inline-flex items-center gap-1 rounded-full bg-red-100 px-2 py-0.5 text-xs font-bold text-red-700"
            >
              <AlertCircle className="h-3.5 w-3.5" />
              {t('visitRequestV2:card.errorBadge', { count: errorCount })}
            </span>
          )}
        </button>
        {canRemove && (
          <button
            type="button"
            aria-label={t('visitRequestV2:card.removeCampus')}
            className="rounded-lg p-2 text-slate-400 hover:bg-red-50 hover:text-red-600"
            onClick={onRemove}
          >
            <Trash2 className="h-4 w-4" />
          </button>
        )}
      </div>

      <div id={bodyId} className={open ? 'space-y-6 border-t border-slate-100 p-4 sm:p-6' : 'hidden'}>
        {/* One-time copy tools — copying is a deep clone; later edits never touch the source */}
        {copySources.length > 0 && (
          <div className="flex flex-wrap items-center gap-2 rounded-xl bg-slate-50 p-3">
            <Copy className="h-4 w-4 text-slate-400" />
            <label htmlFor={`copy-src-${clientKey}`} className="text-sm font-semibold text-slate-600">
              {t('visitRequestV2:card.copyFromLabel')}
            </label>
            <select
              id={`copy-src-${clientKey}`}
              className="h-9 rounded-lg border border-slate-300 bg-white px-2 text-sm"
              defaultValue=""
              onChange={e => {
                const src = Number(e.target.value);
                if (!Number.isNaN(src) && e.target.value !== '') onCopyFrom(src);
                e.target.value = '';
              }}
            >
              <option value="" disabled>{t('visitRequestV2:card.copyFromPlaceholder')}</option>
              {copySources.map(s => (
                <option key={s.index} value={s.index}>{s.label}</option>
              ))}
            </select>
            <button
              type="button"
              className="ml-auto rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold text-slate-700 hover:bg-white"
              onClick={onApplyToAll}
            >
              {t('visitRequestV2:card.applyToAll')}
            </button>
          </div>
        )}

        {/* Schedule */}
        <div className="grid grid-cols-1 gap-x-6 gap-y-5 lg:grid-cols-3">
          <FormField label={t('visitRequestV2:card.campus')} required error={showErrors ? fieldError('campus') : fieldError('campus')} showValidIcon={false}>
            <select
              {...register(`${base}.campus`)}
              className={inputCls(!!fieldError('campus'), !!campusCode, false)}
              disabled={campusesLoading}
            >
              <option value="">{t('visitRequestV2:card.campusPlaceholder')}</option>
              {campuses.map(c => (
                <option key={c.campusCode} value={c.campusCode}>{c.campusName}</option>
              ))}
            </select>
          </FormField>
          <FormField label={t('visitRequestV2:card.startAt')} required error={fieldError('startDatetime')} showValidIcon={false}>
            <input type="datetime-local" {...register(`${base}.startDatetime`)} className={inputCls(!!fieldError('startDatetime'), false, false)} />
          </FormField>
          <FormField
            label={t('visitRequestV2:card.endAt')}
            required
            error={fieldError('endDatetime')}
            subtitle={t('visitRequestV2:card.minDurationHint')}
            showValidIcon={false}
          >
            <input type="datetime-local" {...register(`${base}.endDatetime`)} className={inputCls(!!fieldError('endDatetime'), false, false)} />
          </FormField>
        </div>

        {/* Content */}
        <div className="grid grid-cols-1 gap-x-6 gap-y-5 lg:grid-cols-2">
          <FormField label={t('visitRequestV2:card.delegationName')} required error={fieldError('delegationName')} showValidIcon={false}>
            <input {...register(`${base}.delegationName`)} className={inputCls(!!fieldError('delegationName'), false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:card.visitType')} required error={fieldError('visitType')} showValidIcon={false}>
            <select {...register(`${base}.visitType`)} className={inputCls(!!fieldError('visitType'), false, false)}>
              {VISIT_TYPES.map(vt => (
                <option key={vt} value={vt}>
                  {t(`visitRequest:step2Info.visitTypes.${vt}`, vt)}
                </option>
              ))}
            </select>
          </FormField>
          {visitType === 'OTHER' && (
            <FormField label={t('visitRequestV2:card.visitTypeOther')} required error={fieldError('visitTypeOther')} showValidIcon={false}>
              <input {...register(`${base}.visitTypeOther`)} className={inputCls(!!fieldError('visitTypeOther'), false, false)} />
            </FormField>
          )}
          <FormField label={t('visitRequestV2:card.purpose')} required error={fieldError('purpose')} className="lg:col-span-2" showValidIcon={false}>
            <textarea rows={2} {...register(`${base}.purpose`)} className={`${inputCls(!!fieldError('purpose'), false, false)} h-auto py-2`} />
          </FormField>
          <FormField label={t('visitRequestV2:card.workingContent')} error={fieldError('workingContent')} className="lg:col-span-2" showValidIcon={false}>
            <textarea rows={3} {...register(`${base}.workingContent`)} className={`${inputCls(!!fieldError('workingContent'), false, false)} h-auto py-2`} />
          </FormField>
        </div>

        {/* Visitors */}
        <fieldset>
          <legend className="mb-2 flex w-full flex-wrap items-center gap-2 text-sm font-extrabold text-slate-900">
            {t('visitRequestV2:card.visitors')} <span className="text-red-500">*</span>
            <span className="text-xs font-medium text-slate-400">
              {t('visitRequestV2:card.memberCount', { count: visitorFields.fields.length, max: 200 })}
            </span>
            <span className="ml-auto flex items-center gap-2">
              <button
                type="button"
                className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50"
                onClick={() => downloadVisitorTemplate(excelT)}
              >
                <FileSpreadsheet className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.template')}
              </button>
              <button
                type="button"
                className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50"
                onClick={() => visitorFileRef.current?.click()}
              >
                <FileSpreadsheet className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.importForCampus')}
              </button>
            </span>
          </legend>
          <input
            ref={visitorFileRef}
            type="file"
            accept=".xlsx,.xls"
            className="hidden"
            aria-hidden
            onChange={e => {
              const file = e.target.files?.[0];
              if (file) void importExcel('visitors', file);
              e.target.value = '';
            }}
          />
          {fieldError('visitors') && (
            <p className="mb-2 text-xs font-semibold text-red-600">{fieldError('visitors')}</p>
          )}
          <div className="space-y-2">
            {visitorFields.fields.map((f, i) => (
              <React.Fragment key={f.id}>
                {personRow('visitors', i, () => visitorFields.remove(i), visitorFields.fields.length > 1)}
              </React.Fragment>
            ))}
          </div>
          <button
            type="button"
            className="mt-2 inline-flex items-center gap-1 rounded-lg border border-dashed border-slate-300 px-3 py-1.5 text-sm font-semibold text-[#004c91] hover:bg-slate-50 disabled:opacity-40"
            disabled={visitorFields.fields.length >= 200}
            onClick={() => visitorFields.append({ fullName: '', jobTitle: '', organization: '', nationality: '' })}
          >
            <Plus className="h-4 w-4" /> {t('visitRequestV2:card.addVisitor')}
          </button>
        </fieldset>

        {/* Support team */}
        <fieldset>
          <legend className="mb-2 flex w-full flex-wrap items-center gap-2 text-sm font-extrabold text-slate-900">
            {t('visitRequestV2:card.supportTeam')}
            <span className="text-xs font-medium text-slate-400">
              {t('visitRequestV2:card.memberCount', { count: supportFields.fields.length, max: 200 })}
            </span>
            <span className="ml-auto flex items-center gap-2">
              <button
                type="button"
                className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50"
                onClick={() => downloadSupportTeamTemplate(excelT)}
              >
                <FileSpreadsheet className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.template')}
              </button>
              <button
                type="button"
                className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50"
                onClick={() => supportFileRef.current?.click()}
              >
                <FileSpreadsheet className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.importForCampus')}
              </button>
            </span>
          </legend>
          <input
            ref={supportFileRef}
            type="file"
            accept=".xlsx,.xls"
            className="hidden"
            aria-hidden
            onChange={e => {
              const file = e.target.files?.[0];
              if (file) void importExcel('supportTeam', file);
              e.target.value = '';
            }}
          />
          <div className="space-y-2">
            {supportFields.fields.map((f, i) => (
              <React.Fragment key={f.id}>
                {personRow('supportTeam', i, () => supportFields.remove(i), true)}
              </React.Fragment>
            ))}
          </div>
          <button
            type="button"
            className="mt-2 inline-flex items-center gap-1 rounded-lg border border-dashed border-slate-300 px-3 py-1.5 text-sm font-semibold text-[#004c91] hover:bg-slate-50 disabled:opacity-40"
            disabled={supportFields.fields.length >= 200}
            onClick={() => supportFields.append({ fullName: '', jobTitle: '', organization: '', nationality: '' })}
          >
            <Plus className="h-4 w-4" /> {t('visitRequestV2:card.addSupport')}
          </button>
        </fieldset>

        {excelMessage && (
          <p role="status" className="text-sm font-semibold text-slate-700">{excelMessage}</p>
        )}

        {/* Operational contact (per-campus working contact — a snapshot, never a login) */}
        <fieldset>
          <legend className="mb-2 text-sm font-extrabold text-slate-900">
            {t('visitRequestV2:card.operationalContact')}
            <span className="ml-2 text-xs font-medium text-slate-400">{t('visitRequestV2:card.operationalContactHint')}</span>
          </legend>
          <div className="grid grid-cols-1 gap-x-6 gap-y-5 lg:grid-cols-2">
            <FormField label={t('visitRequestV2:person.fullName')} required error={fieldError('operationalContact.fullName')} showValidIcon={false}>
              <input {...register(`${base}.operationalContact.fullName`)} className={inputCls(!!fieldError('operationalContact.fullName'), false, false)} />
            </FormField>
            <FormField label={t('visitRequestV2:person.organization')} error={fieldError('operationalContact.organization')} showValidIcon={false}>
              <input {...register(`${base}.operationalContact.organization`)} className={inputCls(!!fieldError('operationalContact.organization'), false, false)} />
            </FormField>
            <FormField label={t('visitRequestV2:card.phone')} required error={fieldError('operationalContact.phone')} showValidIcon={false}>
              <input {...register(`${base}.operationalContact.phone`)} placeholder="+84…" className={inputCls(!!fieldError('operationalContact.phone'), false, false)} />
            </FormField>
            <FormField label={t('visitRequestV2:card.email')} error={fieldError('operationalContact.email')} showValidIcon={false}>
              <input type="email" {...register(`${base}.operationalContact.email`)} className={inputCls(!!fieldError('operationalContact.email'), false, false)} />
            </FormField>
          </div>
        </fieldset>

        {/* Additional requirements */}
        <fieldset>
          <legend className="mb-2 text-sm font-extrabold text-slate-900">{t('visitRequestV2:card.additional')}</legend>
          <div className="grid grid-cols-1 gap-x-6 gap-y-5 lg:grid-cols-2">
            <FormField label={t('visitRequestV2:card.workingLanguage')} required error={fieldError('workingLanguage')} showValidIcon={false}>
              <select {...register(`${base}.workingLanguage`)} className={inputCls(false, false, false)}>
                <option value="VI">{t('visitRequestV2:card.languageVi')}</option>
                <option value="EN">{t('visitRequestV2:card.languageEn')}</option>
              </select>
            </FormField>
            <FormField label={t('visitRequestV2:card.mediaConsent')} required error={fieldError('mediaConsentStatus')} showValidIcon={false}>
              <select {...register(`${base}.mediaConsentStatus`)} className={inputCls(false, false, false)}>
                <option value="DECLINED">{t('visitRequestV2:card.mediaDeclined')}</option>
                <option value="AGREED">{t('visitRequestV2:card.mediaAgreed')}</option>
              </select>
            </FormField>
            {mediaConsent === 'AGREED' && (
              <FormField label={t('visitRequestV2:card.mediaNote')} error={fieldError('mediaConsentNote')} className="lg:col-span-2" showValidIcon={false}>
                <input {...register(`${base}.mediaConsentNote`)} className={inputCls(!!fieldError('mediaConsentNote'), false, false)} />
              </FormField>
            )}
            <FormField label={t('visitRequestV2:card.transportationNote')} error={fieldError('transportationNote')} showValidIcon={false}>
              <input {...register(`${base}.transportationNote`)} className={inputCls(!!fieldError('transportationNote'), false, false)} />
            </FormField>
            <FormField label={t('visitRequestV2:card.notes')} error={fieldError('notes')} showValidIcon={false}>
              <input {...register(`${base}.notes`)} className={inputCls(!!fieldError('notes'), false, false)} />
            </FormField>
          </div>
        </fieldset>
      </div>
    </div>
  );
};
