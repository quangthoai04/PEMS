import React, { useRef, useState } from 'react';
import { Controller, useFieldArray, type UseFormReturn } from 'react-hook-form';
import { AlertCircle, ChevronDown, Copy, FileSpreadsheet, Plus, Replace, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';
import { V2_MAX_MEMBERS_PER_CAMPUS, V2_MIN_ADVANCE_HOURS_CREATE } from '../../schema/visitRequestV2.schema';
import type { RegistrationCampusOption } from '../../api/visitRequestApi';
import { FormField, inputCls } from '../shared/FormField';
import { AutoGrowTextarea } from '../shared/AutoGrowTextarea';
import { AutoGrowTextField } from '../shared/AutoGrowTextField';
import { PhoneField } from '../shared/PhoneField';
import { VisitDateTimeRangePicker } from '../shared/VisitDateTimeRangePicker';
import { CountrySelect } from '../shared/CountrySelect';
import { OrganizationCombobox } from '../shared/OrganizationCombobox';
import { showSuccessToast } from '../../../../shared/utils/toast';
import { countFieldErrors } from '../../utils/formErrorNavigation';
import { validatePersonExcel, canApplyImport, type ExcelTranslator, type PersonRow } from '../ExcelUpload/excelValidator';
import { ExcelImportPanel, type ExcelImportState } from '../ExcelUpload/ExcelImportPanel';
import { downloadVisitorTemplate, downloadSupportTeamTemplate } from '../ExcelUpload/excelDownload';
import { CampusProcessingV2Panel } from './CampusProcessingV2Panel';
import type { CreatorRole } from '../../schema/visitRequestV2.schema';
import type { CampusProcessingChoice } from '../../api/visitRequestApi';
import { HelpTooltip } from '../shared/HelpTooltip';

const VISIT_TYPES = ['CAMPUS_TOUR', 'MEETING', 'WORKSHOP', 'SIGNING_CEREMONY', 'EXCHANGE', 'OTHER'] as const;

/**
 * What a quick-fill copies into the per-campus operational contact. This is a ONE-TIME copy, never
 * a link: the registrant and the campus contact are frequently the same person on the day the form
 * is filled and different people a week later, so keeping them in sync would silently undo an
 * edit somebody made deliberately (plan §12).
 */
const QUICK_FILL_FIELDS = ['fullName', 'organization', 'phone', 'email'] as const;
type QuickFillSource = 'registrant' | 'contact';

/**
 * Per-field ceilings, mirroring `buildCampusVisitSchema` and the FluentValidation rules that
 * back it. Declared here only so the counter and the schema cannot drift apart on screen.
 */
const MAX = {
  delegationName: 200,
  visitTypeOther: 200,
  purpose: 2000,
  workingContent: 4000,
  transportationNote: 2000,
  mediaConsentNote: 2000,
  notes: 2000,
  personName: 150,
  personJobTitle: 150,
  personOrganization: 200,
  contactName: 150,
  contactOrganization: 200,
} as const;

interface Props {
  form: UseFormReturn<VisitRequestV2Schema>;
  index: number;
  /** Stable identity of this card — the React key; NEVER the array index. */
  clientKey: string;
  open: boolean;
  onToggle: () => void;
  campuses: RegistrationCampusOption[];
  campusesLoading: boolean;
  /** Campus CODEs already chosen on ANY card — filtered out here so a campus cannot be picked twice. */
  takenCampusCodes?: string[];
  /** Labels of the OTHER cards offered as one-time copy sources (empty → no copy UI). */
  copySources: Array<{ index: number; label: string }>;
  onCopyFrom: (sourceIndex: number) => void;
  onApplyToAll: () => void;
  onRemove: () => void;
  canRemove: boolean;
  showErrors?: boolean;
  /** 72h for a new submit, 24h for visitor edit/resubmit — same source as the Zod schema. */
  minAdvanceHours?: number;
  /**
   * Authenticated create only: who will process THIS campus. Omitted entirely for the public
   * form, which never renders internal processing controls and never sends a processing intent.
   */
  processing?: {
    role: CreatorRole;
    ownCampusCode?: string | null;
    /** All choices keyed by campus CODE; this card reads its own by the campus it currently watches. */
    values: Record<string, CampusProcessingChoice>;
    onChange: (next: CampusProcessingChoice) => void;
  };
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
  takenCampusCodes = [],
  copySources,
  onCopyFrom,
  onApplyToAll,
  onRemove,
  canRemove,
  showErrors,
  minAdvanceHours = V2_MIN_ADVANCE_HOURS_CREATE,
  processing,
}) => {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest']);
  const { register, control, watch, formState: { errors } } = form;
  const base = `campusVisits.${index}` as const;
  const cardErrors = errors.campusVisits?.[index];
  const errorCount = countFieldErrors(cardErrors);

  const visitorFields = useFieldArray({ control, name: `campusVisits.${index}.visitors` });
  const supportFields = useFieldArray({ control, name: `campusVisits.${index}.supportTeam` });

  // ONE state per section: guests and support staff are imported separately, and a shared
  // message could not say which list it described (nor survive a second import).
  const [excelState, setExcelState] = useState<Record<'visitors' | 'supportTeam', ExcelImportState>>({
    visitors: {}, supportTeam: {},
  });
  const [pendingReplace, setPendingReplace] = useState<
    { kind: 'visitors' | 'supportTeam'; rows: PersonRow[]; fileName: string } | null
  >(null);
  const visitorFileRef = useRef<HTMLInputElement>(null);
  const supportFileRef = useRef<HTMLInputElement>(null);

  const campusCode = watch(`${base}.campus`);
  const visitType = watch(`${base}.visitType`);
  const mediaConsent = watch(`${base}.mediaConsentStatus`);
  const campusName = campuses.find(c => c.campusCode === campusCode)?.campusName;
  const headerLabel = campusName ?? t('visitRequestV2:card.unselectedCampus');

  const excelT: ExcelTranslator = (key, options) => t(key, options);

  const setSection = (kind: 'visitors' | 'supportTeam', next: ExcelImportState) =>
    setExcelState(prev => ({ ...prev, [kind]: next }));

  /** The rows CURRENTLY in a section — the baseline for duplicate detection and the 200 cap. */
  const currentRows = (kind: 'visitors' | 'supportTeam'): PersonRow[] =>
    ((watch(`${base}.${kind}`) ?? []) as PersonRow[]).map(r => ({
      fullName: r?.fullName ?? '', jobTitle: r?.jobTitle ?? '',
      organization: r?.organization ?? '', nationality: r?.nationality ?? '',
    }));

  /**
   * A row the user has not filled in at all. An untouched blank row is scaffolding, not data:
   * it must not consume one of the 200 slots, and appending after it would leave a gap.
   */
  const isBlank = (r: PersonRow) =>
    !r.fullName.trim() && !r.jobTitle.trim() && !r.organization.trim() && !r.nationality.trim();

  /**
   * Imports into THIS campus card only, and only ever ADDS (plan §7): replacing was destroying
   * rows typed by hand with no warning. Replacing is still possible — as its own action, below,
   * behind a confirmation.
   */
  const importExcel = async (kind: 'visitors' | 'supportTeam', file: File) => {
    setSection(kind, { loadingFileName: file.name });
    const existing = currentRows(kind).filter(r => !isBlank(r));

    // validatePersonExcel never throws: an unreadable workbook comes back as a report with a
    // fatalMessage, so the panel can explain it instead of the promise rejecting silently.
    const report = await validatePersonExcel(file, kind, existing, excelT);
    setSection(kind, { report });
    if (!canApplyImport(report)) return;

    const rows = report.data.map(r => ({
      fullName: r.fullName, jobTitle: r.jobTitle, organization: r.organization, nationality: r.nationality,
    }));
    const fields = kind === 'visitors' ? visitorFields : supportFields;
    const blanks = currentRows(kind).map(isBlank);
    // Drop the untouched placeholder rows so the imported list starts where the user's does.
    for (let i = blanks.length - 1; i >= 0; i--) if (blanks[i]) fields.remove(i);
    fields.append(rows);
  };

  /** Whether the last import for this section still holds rows a replace could use. */
  const canReplaceFrom = (kind: 'visitors' | 'supportTeam') => {
    const report = excelState[kind].report;
    return !!report && canApplyImport(report);
  };

  const requestReplace = (kind: 'visitors' | 'supportTeam') => {
    const report = excelState[kind].report;
    if (!report || !canApplyImport(report)) return;
    setPendingReplace({ kind, rows: report.data, fileName: report.fileName });
  };

  const confirmReplace = () => {
    if (!pendingReplace) return;
    const fields = pendingReplace.kind === 'visitors' ? visitorFields : supportFields;
    fields.replace(pendingReplace.rows);
    setPendingReplace(null);
  };

  // ── Operational-contact quick fill (plan §11–§13) ──────────────────────────────────────────
  // Watched, not read on click, so the buttons enable themselves as soon as the source block is
  // usable rather than staying grey until something else re-renders the card.
  const watchedRegistrant = watch('registerInfo');
  const watchedPrimaryContact = watch('contactPoint');
  const [pendingQuickFill, setPendingQuickFill] = useState<QuickFillSource | null>(null);
  const [quickFilledFrom, setQuickFilledFrom] = useState<QuickFillSource | null>(null);

  const quickFillValues = (kind: QuickFillSource) => {
    const src = kind === 'registrant' ? watchedRegistrant : watchedPrimaryContact;
    return {
      fullName: src?.fullName ?? '',
      organization: src?.organization ?? '',
      phone: src?.phone ?? '',
      email: src?.email ?? '',
    };
  };

  /** A source with no name and no email cannot fill anything useful, so its button stays off. */
  const canQuickFillFrom = (kind: QuickFillSource) => {
    const v = quickFillValues(kind);
    return v.fullName.trim().length > 0 && v.email.trim().length > 0;
  };

  const operationalContactHasData = () => {
    const oc = form.getValues(`${base}.operationalContact`);
    return QUICK_FILL_FIELDS.some(f => (oc?.[f] ?? '').trim().length > 0);
  };

  const applyQuickFill = (kind: QuickFillSource) => {
    const v = quickFillValues(kind);
    // Per-field setValue on THIS card only — a whole-object set on a shared path would have leaked
    // into the other campus cards, which are independent snapshots by design.
    for (const f of QUICK_FILL_FIELDS) {
      form.setValue(`${base}.operationalContact.${f}`, v[f], { shouldDirty: true, shouldValidate: true });
    }
    setQuickFilledFrom(kind);
    setPendingQuickFill(null);
    showSuccessToast(t(kind === 'registrant'
      ? 'visitRequestV2:card.quickFillCopiedRegistrant'
      : 'visitRequestV2:card.quickFillCopiedContact'));
  };

  /** Never overwrites silently: anything already typed here is confirmed away first (plan §13). */
  const requestQuickFill = (kind: QuickFillSource) => {
    if (operationalContactHasData()) setPendingQuickFill(kind);
    else applyQuickFill(kind);
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

  const cellError = (msg?: string) =>
    msg ? <p className="px-2 pb-1 text-xs font-semibold text-red-600">{msg}</p> : null;

  /**
   * Name / job title are plain text; organization and nationality reuse the SAME searchable
   * comboboxes as v1 (free text still allowed when nothing matches). Every field goes through
   * Controller because each row is rendered twice — desktop table and mobile card — and two
   * uncontrolled `register()` refs for one field make React Hook Form track only the last one.
   */
  const personFieldCell = (
    kind: 'visitors' | 'supportTeam',
    rowIndex: number,
    field: 'fullName' | 'jobTitle' | 'organization' | 'nationality',
    isCell: boolean,
  ) => {
    const name = `${base}.${kind}.${rowIndex}.${field}` as const;
    const hasError = !!fieldError(`${kind}.${rowIndex}.${field}`);
    const placeholder = t(`visitRequestV2:person.${field}`);

    if (field === 'organization' || field === 'nationality') {
      return (
        <Controller
          name={name}
          control={control}
          render={({ field: f }) => (field === 'nationality' ? (
            <CountrySelect
              value={f.value ?? ''}
              onChange={f.onChange}
              onBlur={f.onBlur}
              hasError={hasError}
              placeholder={placeholder}
              isCell={isCell}
            />
          ) : (
            <OrganizationCombobox
              value={f.value ?? ''}
              onChange={f.onChange}
              onBlur={f.onBlur}
              hasError={hasError}
              placeholder={placeholder}
              isCell={isCell}
            />
          ))}
        />
      );
    }

    // Name and job title WRAP rather than scroll sideways: a long name in a table cell used to
    // be readable only by dragging the caret through it (plan §21.3).
    const max = field === 'fullName' ? MAX.personName : MAX.personJobTitle;
    return (
      <Controller
        name={name}
        control={control}
        render={({ field: f }) => (
          <AutoGrowTextField
            value={f.value ?? ''}
            onChange={f.onChange}
            onBlur={f.onBlur}
            placeholder={placeholder}
            ariaLabel={placeholder}
            hasError={hasError}
            maxLength={max}
            isCell={isCell}
            testId={`${kind}-${rowIndex}-${field}`}
          />
        )}
      />
    );
  };

  const PERSON_COLUMNS = ['fullName', 'jobTitle', 'organization', 'nationality'] as const;

  /**
   * Desktop renders a table whose leading column is a RE-DERIVED ordinal (`i + 1`), so removing a
   * row renumbers the rest automatically — the number is never stored. Below `lg` the same rows
   * become stacked cards, since a 6-column table cannot stay readable on a phone.
   */
  const personTable = (
    kind: 'visitors' | 'supportTeam',
    rows: { id: string }[],
    onRemoveRow: (i: number) => void,
    canRemoveRow: (i: number) => boolean,
  ) => (
    <>
      <div className="hidden overflow-x-auto rounded-xl border border-slate-200 lg:block">
        <table data-testid={`v2-${kind}-table`} className="w-full min-w-[760px] border-collapse text-sm">
          <thead className="border-b border-slate-200 bg-slate-50">
            <tr>
              <th scope="col" className="w-12 p-3 text-center font-bold text-slate-700">
                {t('visitRequestV2:person.stt')}
              </th>
              {PERSON_COLUMNS.map(c => (
                <th key={c} scope="col" className="border-l border-slate-200 p-3 text-left font-bold text-slate-700">
                  {t(`visitRequestV2:person.${c}`)}
                </th>
              ))}
              <th scope="col" className="w-14 border-l border-slate-200 p-3 text-center font-bold text-slate-700">
                {t('visitRequestV2:person.actions')}
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((f, i) => (
              <tr key={f.id} className="border-b border-slate-100 last:border-b-0 hover:bg-orange-50/30">
                <td className="p-3 text-center font-bold text-slate-400">{i + 1}</td>
                {PERSON_COLUMNS.map(c => (
                  <td key={c} className="border-l border-slate-100 p-0 align-top">
                    {personFieldCell(kind, i, c, true)}
                    {cellError(fieldError(`${kind}.${i}.${c}`))}
                  </td>
                ))}
                <td className="border-l border-slate-100 p-2 text-center align-top">
                  <button
                    type="button"
                    aria-label={t('visitRequestV2:card.removeRow')}
                    disabled={!canRemoveRow(i)}
                    className="rounded-lg p-2 text-slate-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-30"
                    onClick={() => onRemoveRow(i)}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="space-y-3 lg:hidden">
        {rows.map((f, i) => (
          <div key={f.id} className="rounded-xl border border-slate-200 p-3">
            <div className="mb-2 flex items-center justify-between">
              <span className="text-xs font-bold text-slate-400">
                {t('visitRequestV2:person.stt')} {i + 1}
              </span>
              <button
                type="button"
                aria-label={t('visitRequestV2:card.removeRow')}
                disabled={!canRemoveRow(i)}
                className="rounded-lg p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-30"
                onClick={() => onRemoveRow(i)}
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
            <div className="space-y-2">
              {PERSON_COLUMNS.map(c => (
                <div key={c}>
                  {personFieldCell(kind, i, c, false)}
                  {cellError(fieldError(`${kind}.${i}.${c}`))}
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
    </>
  );

  return (
    <div data-testid={`campus-edit-card-${campusCode || 'new'}`} className="rounded-2xl border border-slate-200 bg-white shadow-sm">
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

        {/* Campus */}
        <div className="grid grid-cols-1 gap-x-6 gap-y-5 lg:grid-cols-3">
          <FormField label={t('visitRequestV2:card.campus')} required error={fieldError('campus')} showValidIcon={false}>
            <select
              {...register(`${base}.campus`)}
              className={inputCls(!!fieldError('campus'), !!campusCode, false)}
              disabled={campusesLoading}
            >
              <option value="">{t('visitRequestV2:card.campusPlaceholder')}</option>
              {campuses
                // Hide campuses already taken by another card; this card keeps its own selection.
                .filter(c => c.campusCode === campusCode
                  || !takenCampusCodes.includes(c.campusCode.toUpperCase()))
                .map(c => (
                  <option key={c.campusCode} value={c.campusCode}>{c.campusName}</option>
                ))}
            </select>
          </FormField>
        </div>

        {/* Schedule — one date + a start and end time, with the end offered by duration. Both
            halves still write the SAME two wall-clock fields the API has always taken. */}
        <Controller
          name={`${base}.startDatetime`}
          control={control}
          render={({ field: startField }) => (
            <Controller
              name={`${base}.endDatetime`}
              control={control}
              render={({ field: endField }) => (
                <VisitDateTimeRangePicker
                  idPrefix={`campus-${index}`}
                  minAdvanceHours={minAdvanceHours}
                  startValue={startField.value ?? ''}
                  endValue={endField.value ?? ''}
                  onChange={({ start, end }) => { startField.onChange(start); endField.onChange(end); }}
                  startError={fieldError('startDatetime')}
                  endError={fieldError('endDatetime')}
                />
              )}
            />
          )}
        />

        {/* Content */}
        <div className="grid grid-cols-1 gap-x-6 gap-y-5 lg:grid-cols-2">
          <FormField label={t('visitRequestV2:card.delegationName')} required error={fieldError('delegationName')} showValidIcon={false}>
            <Controller
              name={`${base}.delegationName`}
              control={control}
              render={({ field }) => (
                <AutoGrowTextField
                  testId="campus-delegation-input"
                  value={field.value ?? ''}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  hasError={!!fieldError('delegationName')}
                  maxLength={MAX.delegationName}
                  ariaLabel={t('visitRequestV2:card.delegationName')}
                />
              )}
            />
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
              <Controller
                name={`${base}.visitTypeOther`}
                control={control}
                render={({ field }) => (
                  <AutoGrowTextField
                    value={field.value ?? ''}
                    onChange={field.onChange}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('visitTypeOther')}
                    maxLength={MAX.visitTypeOther}
                    ariaLabel={t('visitRequestV2:card.visitTypeOther')}
                  />
                )}
              />
            </FormField>
          )}
        </div>

        {/* Purpose and working content sit side by side on desktop and stack when narrow — they are
            read together, and both grow with their content rather than scrolling internally. */}
        <div className="grid grid-cols-1 gap-x-6 gap-y-5 lg:grid-cols-2">
          <FormField label={t('visitRequestV2:card.purpose')} required error={fieldError('purpose')} showValidIcon={false}>
            <Controller
              name={`${base}.purpose`}
              control={control}
              render={({ field }) => (
                <AutoGrowTextarea
                  value={field.value ?? ''}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  hasError={!!fieldError('purpose')}
                  maxLength={MAX.purpose}
                  minRows={3}
                />
              )}
            />
          </FormField>
          <FormField label={t('visitRequestV2:card.workingContent')} required error={fieldError('workingContent')} showValidIcon={false}>
            <Controller
              name={`${base}.workingContent`}
              control={control}
              render={({ field }) => (
                <AutoGrowTextarea
                  value={field.value ?? ''}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  hasError={!!fieldError('workingContent')}
                  maxLength={MAX.workingContent}
                  minRows={3}
                />
              )}
            />
          </FormField>
        </div>

        {/* Visitors */}
        <fieldset>
          <legend className="mb-2 flex w-full flex-wrap items-center gap-2 text-sm font-extrabold text-slate-900">
            {t('visitRequestV2:card.visitors')} <span className="text-red-500">*</span>
            <span className="text-xs font-medium text-slate-400">
              {t('visitRequestV2:card.memberCount', { count: visitorFields.fields.length, max: V2_MAX_MEMBERS_PER_CAMPUS })}
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
                data-testid="v2-visitors-import"
                disabled={!!excelState.visitors.loadingFileName}
                className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50 disabled:opacity-50"
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
          <ExcelImportPanel
            testId="v2-excel-visitors"
            state={excelState.visitors}
            campusLabel={headerLabel}
            onChooseAnother={() => visitorFileRef.current?.click()}
            onDismiss={() => setSection('visitors', {})}
          />
          {canReplaceFrom('visitors') && (
            <button
              type="button"
              data-testid="v2-visitors-replace"
              className="mt-2 inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50"
              onClick={() => requestReplace('visitors')}
            >
              <Replace className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.replaceAll')}
            </button>
          )}
          {personTable(
            'visitors',
            visitorFields.fields,
            i => visitorFields.remove(i),
            () => true,
          )}
          <button
            type="button"
            className="mt-2 inline-flex items-center gap-1 rounded-lg border border-dashed border-slate-300 px-3 py-1.5 text-sm font-semibold text-[#004c91] hover:bg-slate-50 disabled:opacity-40"
            disabled={visitorFields.fields.length >= V2_MAX_MEMBERS_PER_CAMPUS}
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
              {t('visitRequestV2:card.memberCount', { count: supportFields.fields.length, max: V2_MAX_MEMBERS_PER_CAMPUS })}
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
                data-testid="v2-support-import"
                disabled={!!excelState.supportTeam.loadingFileName}
                className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50 disabled:opacity-50"
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
          <ExcelImportPanel
            testId="v2-excel-support"
            state={excelState.supportTeam}
            campusLabel={headerLabel}
            onChooseAnother={() => supportFileRef.current?.click()}
            onDismiss={() => setSection('supportTeam', {})}
          />
          {canReplaceFrom('supportTeam') && (
            <button
              type="button"
              data-testid="v2-support-replace"
              className="mt-2 inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50"
              onClick={() => requestReplace('supportTeam')}
            >
              <Replace className="h-3.5 w-3.5" /> {t('visitRequestV2:excel.replaceAll')}
            </button>
          )}
          {personTable(
            'supportTeam',
            supportFields.fields,
            i => supportFields.remove(i),
            () => true,
          )}
          <button
            type="button"
            className="mt-2 inline-flex items-center gap-1 rounded-lg border border-dashed border-slate-300 px-3 py-1.5 text-sm font-semibold text-[#004c91] hover:bg-slate-50 disabled:opacity-40"
            disabled={supportFields.fields.length >= V2_MAX_MEMBERS_PER_CAMPUS}
            onClick={() => supportFields.append({ fullName: '', jobTitle: '', organization: '', nationality: '' })}
          >
            <Plus className="h-4 w-4" /> {t('visitRequestV2:card.addSupport')}
          </button>
        </fieldset>

        {/* Replacing a list is destructive and is never what "import" alone does — it asks first. */}
        {pendingReplace && (
          <div role="alertdialog" data-testid="v2-replace-confirm" className="rounded-xl border border-amber-300 bg-amber-50 p-3">
            <p className="text-sm font-bold text-amber-900">
              {t('visitRequestV2:excel.replaceConfirmTitle')}
            </p>
            <p className="mt-1 text-sm text-amber-800">
              {t('visitRequestV2:excel.replaceConfirmBody', {
                fileName: pendingReplace.fileName,
                count: pendingReplace.rows.length,
                current: currentRows(pendingReplace.kind).filter(r => !isBlank(r)).length,
              })}
            </p>
            <div className="mt-2 flex flex-wrap gap-2">
              <button
                type="button"
                data-testid="v2-replace-confirm-yes"
                className="rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-amber-700"
                onClick={confirmReplace}
              >
                {t('visitRequestV2:excel.replaceConfirmYes')}
              </button>
              <button
                type="button"
                className="rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-bold text-amber-800 hover:bg-amber-100"
                onClick={() => setPendingReplace(null)}
              >
                {t('visitRequestV2:excel.replaceConfirmNo')}
              </button>
            </div>
          </div>
        )}

        {/* Operational contact (per-campus working contact — a snapshot, never a login) */}
        <fieldset>
          <legend className="mb-2 flex w-full flex-wrap items-center gap-2 text-sm font-extrabold text-slate-900">
            <span className="flex items-center">
              {t('visitRequestV2:card.operationalContact')}
              <HelpTooltip content={t('visitRequestV2:card.operationalContactHint')} className="ml-1.5" />
            </span>
            {/* Both sources are offered because both happen: sometimes the registrant coordinates the
                campus themselves, sometimes the request's primary contact does, and they are often
                two different people. One button could only ever serve half the cases. */}
            <span className="flex flex-wrap items-center gap-2 sm:ml-auto">
              <button
                type="button"
                data-testid={`campus-opcontact-use-registrant-${index}`}
                disabled={!canQuickFillFrom('registrant')}
                onClick={() => requestQuickFill('registrant')}
                className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
              >
                <Copy className="h-3.5 w-3.5" /> {t('visitRequestV2:card.quickFillRegistrant')}
              </button>
              <button
                type="button"
                data-testid={`campus-opcontact-use-contact-${index}`}
                disabled={!canQuickFillFrom('contact')}
                onClick={() => requestQuickFill('contact')}
                className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
              >
                <Copy className="h-3.5 w-3.5" /> {t('visitRequestV2:card.quickFillPrimaryContact')}
              </button>
            </span>
          </legend>

          {quickFilledFrom && (
            <p data-testid={`campus-opcontact-copied-${index}`} className="mb-2 text-xs font-medium text-slate-500">
              {t(quickFilledFrom === 'registrant'
                ? 'visitRequestV2:card.quickFillCopiedRegistrant'
                : 'visitRequestV2:card.quickFillCopiedContact')}{' '}
              {t('visitRequestV2:card.quickFillIndependent')}
            </p>
          )}

          {/* Replacing what is already there is confirmed, never silent (plan §13). */}
          {pendingQuickFill && (
            <div
              role="alertdialog"
              data-testid={`campus-opcontact-replace-confirm-${index}`}
              className="mb-3 rounded-xl border border-amber-300 bg-amber-50 p-3"
            >
              <p className="text-sm font-bold text-amber-900">
                {t('visitRequestV2:card.quickFillReplaceTitle')}
              </p>
              <div className="mt-2 flex flex-wrap gap-2">
                <button
                  type="button"
                  data-testid={`campus-opcontact-replace-yes-${index}`}
                  className="rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-amber-700"
                  onClick={() => applyQuickFill(pendingQuickFill)}
                >
                  {t('visitRequestV2:card.quickFillReplaceYes')}
                </button>
                <button
                  type="button"
                  className="rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-bold text-amber-800 hover:bg-amber-100"
                  onClick={() => setPendingQuickFill(null)}
                >
                  {t('visitRequestV2:common.cancel')}
                </button>
              </div>
            </div>
          )}

          <div className="grid grid-cols-1 gap-x-6 gap-y-5 lg:grid-cols-2">
            <FormField label={t('visitRequestV2:person.fullName')} required error={fieldError('operationalContact.fullName')} showValidIcon={false}>
              <Controller
                name={`${base}.operationalContact.fullName`}
                control={control}
                render={({ field }) => (
                  <AutoGrowTextField
                    value={field.value ?? ''}
                    onChange={field.onChange}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('operationalContact.fullName')}
                    maxLength={MAX.contactName}
                    ariaLabel={t('visitRequestV2:person.fullName')}
                    testId="campus-opcontact-name"
                  />
                )}
              />
            </FormField>
            {/* The same searchable organization control the guest rows use. The stored value is
                still plain text — this contact is a snapshot and the schema has no relation to a
                partner record, so picking a known organization links nothing and the request's own
                partner selection is untouched. */}
            <FormField label={t('visitRequestV2:person.organization')} required error={fieldError('operationalContact.organization')} showValidIcon={false}>
              <Controller
                name={`${base}.operationalContact.organization`}
                control={control}
                render={({ field }) => (
                  <OrganizationCombobox
                    value={field.value ?? ''}
                    onChange={field.onChange}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('operationalContact.organization')}
                    placeholder={t('visitRequestV2:person.organization')}
                    ariaLabel={t('visitRequestV2:person.organization')}
                    testId="campus-opcontact-org"
                  />
                )}
              />
            </FormField>
            <FormField label={t('visitRequestV2:card.phone')} required error={fieldError('operationalContact.phone')} showValidIcon={false}>
              <PhoneField
                field={register(`${base}.operationalContact.phone`)}
                hasError={!!fieldError('operationalContact.phone')}
                error={fieldError('operationalContact.phone')}
                testId={`campus-opcontact-phone-${index}`}
              />
            </FormField>
            <FormField label={t('visitRequestV2:card.email')} required error={fieldError('operationalContact.email')} showValidIcon={false}>
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
                <Controller
                  name={`${base}.mediaConsentNote`}
                  control={control}
                  render={({ field }) => (
                    <AutoGrowTextarea
                      value={field.value ?? ''}
                      onChange={field.onChange}
                      onBlur={field.onBlur}
                      hasError={!!fieldError('mediaConsentNote')}
                      maxLength={MAX.mediaConsentNote}
                      minRows={2}
                    />
                  )}
                />
              </FormField>
            )}
            <FormField label={t('visitRequestV2:card.transportationNote')} error={fieldError('transportationNote')} showValidIcon={false}>
              <Controller
                name={`${base}.transportationNote`}
                control={control}
                render={({ field }) => (
                  <AutoGrowTextarea
                    value={field.value ?? ''}
                    onChange={field.onChange}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('transportationNote')}
                    maxLength={MAX.transportationNote}
                    minRows={2}
                  />
                )}
              />
            </FormField>
            <FormField label={t('visitRequestV2:card.notes')} error={fieldError('notes')} showValidIcon={false}>
              <Controller
                name={`${base}.notes`}
                control={control}
                render={({ field }) => (
                  <AutoGrowTextarea
                    value={field.value ?? ''}
                    onChange={field.onChange}
                    onBlur={field.onBlur}
                    hasError={!!fieldError('notes')}
                    maxLength={MAX.notes}
                    minRows={2}
                  />
                )}
              />
            </FormField>
          </div>

          {/* Who processes THIS campus — authenticated create only; absent for public submit. */}
          {processing && (
            <CampusProcessingV2Panel
              campusCode={campusCode}
              role={processing.role}
              ownCampusCode={processing.ownCampusCode}
              startDatetime={watch(`${base}.startDatetime`)}
              endDatetime={watch(`${base}.endDatetime`)}
              value={processing.values[(campusCode || '').toUpperCase()]}
              onChange={processing.onChange}
            />
          )}
        </fieldset>
      </div>
    </div>
  );
};
