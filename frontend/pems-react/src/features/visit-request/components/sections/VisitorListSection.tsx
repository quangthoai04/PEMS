import React, { useRef, useState } from 'react';
import { Controller, type UseFormReturn, type UseFieldArrayReturn } from 'react-hook-form';
import { Plus, Trash2, Download, Upload, CheckCircle2, AlertCircle, X, FileSpreadsheet } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { CountrySelect } from '../shared/CountrySelect';
import { OrganizationCombobox } from '../shared/OrganizationCombobox';
import { FormSection } from '../shared/FormSection';
import { inputCls } from '../shared/FormField';
import { validateVisitorExcel, isAllowedExcelFile, type ExcelTranslator } from '../ExcelUpload/excelValidator';
import { downloadVisitorTemplate } from '../ExcelUpload/excelDownload';
import type { ExcelValidationResult, VisitorEntry } from '../../types/visitRequest.types';
import { useTranslation } from 'react-i18next';

function hasRealError(error: unknown): boolean {
  if (!error) return false;
  if (Array.isArray(error)) return error.some(hasRealError);
  if (typeof error === 'object') {
    return Object.values(error as Record<string, unknown>).some(hasRealError);
  }
  return true;
}

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
  visitorFields: UseFieldArrayReturn<VisitRequestSchema, 'visitors'>;
  showErrors?: boolean;
}

export const VisitorListSection: React.FC<Props> = ({ form, visitorFields, showErrors }) => {
  const { t } = useTranslation(['visitRequest']);
  // Excel helpers live outside React, so the translator is handed to them explicitly.
  const excelT: ExcelTranslator = (key, options) => t(key, options as never) as unknown as string;
  const { control, formState: { errors } } = form;
  const visitorErrors = errors.visitors;

  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [uploadResult, setUploadResult] = useState<ExcelValidationResult | null>(null);
  const [uploadFileName, setUploadFileName] = useState('');
  const [addedCount, setAddedCount] = useState(0);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploadFileName(file.name);
    setIsProcessing(true);
    setUploadResult(null);
    setAddedCount(0);

    if (!isAllowedExcelFile(file)) {
      setUploadResult({
        valid: false, totalRows: 0, errorRows: 0, skippedDuplicates: 0,
        errors: [{ row: 0, column: '', message: t('visitRequest:step2Visitors.uploadError') }],
        data: [],
      });
      setIsProcessing(false);
      e.target.value = '';
      return;
    }

    const existingData = visitorFields.fields.map(f => ({
      fullName: f.fullName,
      jobTitle: f.jobTitle,
      organization: f.organization,
      nationality: f.nationality
    }));
    const result = await validateVisitorExcel(file, existingData as VisitorEntry[], excelT);

    if (result.data.length > 0) {
      // Email checking removed
      const toAdd = result.data;

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      toAdd.forEach((v) => visitorFields.append(v as any));
      setAddedCount(toAdd.length);

      setUploadResult({
        ...result,
        errors: [...result.errors],
        errorRows: result.errorRows,
        valid: result.errors.length === 0,
      });
    } else {
      setUploadResult(result);
    }

    setIsProcessing(false);
    e.target.value = '';
  };

  const rootErrorMessage = (visitorErrors as any)?.root?.message || (typeof visitorErrors === 'object' && !Array.isArray(visitorErrors) ? (visitorErrors as any).message : null);
  const hasRootError = !!rootErrorMessage;
  const hasAnyError = showErrors && (hasRootError || hasRealError(visitorErrors));

  return (
    <FormSection
      id="section-visitors"
      title={t('visitRequest:singleForm.sections.visitors')}
      required
    >
      {hasAnyError && (
        <div className="error-scroll-target mb-4 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 p-3">
          <AlertCircle className="w-5 h-5 text-red-500 shrink-0" />
          <div>
            <p className="text-sm font-bold text-red-700">{t('visitRequest:step2Visitors.errorBoxTitle')}</p>
            {rootErrorMessage && <p className="text-xs text-red-600 mt-0.5">{rootErrorMessage}</p>}
          </div>
        </div>
      )}

      {/* Desktop: single table frame */}
      <div className="hidden overflow-x-auto rounded-xl border border-slate-200 lg:block">
        <table className="w-full min-w-[680px] border-collapse text-sm">
          <thead className="border-b border-gray-200 bg-slate-50">
            <tr>
              <th className="w-12 p-3 text-center font-bold text-slate-700">{t('visitRequest:step2Visitors.stt')}</th>
              <th className="border-l border-gray-200 p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Visitors.fullName')}</th>
              <th className="border-l border-gray-200 p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Visitors.jobTitle')}</th>
              <th className="border-l border-gray-200 p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Visitors.organization')}</th>
              <th className="border-l border-gray-200 p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Visitors.nationality')}</th>
              <th className="w-12 border-l border-gray-200 p-3 text-center" />
            </tr>
          </thead>
          <tbody>
            <AnimatePresence>
              {visitorFields.fields.map((field, i) => {
                const fe = visitorErrors?.[i];
                const rowHasError = !!(fe?.fullName || fe?.nationality || fe?.organization || fe?.jobTitle);
                return (
                  <motion.tr
                    key={field.id}
                    initial={{ opacity: 0, y: -6 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, height: 0 }}
                    className={[
                      'border-b border-gray-100 last:border-b-0 transition-colors',
                      rowHasError ? 'bg-red-50/40' : 'hover:bg-orange-50/40 focus-within:bg-orange-50/30',
                    ].join(' ')}
                  >
                    <td className="p-3 text-center font-bold text-slate-400 text-sm">{i + 1}</td>

                    {/* Controlled inputs: the same field renders in BOTH the desktop table and
                        the mobile list, and RHF's uncontrolled register() breaks with duplicate
                        refs (it reads the last-registered — hidden — input). */}
                    <td className="p-0 border-l border-gray-100">
                      <Controller
                        name={`visitors.${i}.fullName`}
                        control={control}
                        render={({ field }) => (
                          <input
                            {...field}
                            placeholder={t('visitRequest:step2Visitors.placeholderName')}
                            className={cellInputCls(!!fe?.fullName)}
                          />
                        )}
                      />
                      {fe?.fullName && <CellError msg={fe.fullName.message} />}
                    </td>

                    <td className="p-0 border-l border-gray-100">
                      <Controller
                        name={`visitors.${i}.jobTitle`}
                        control={control}
                        render={({ field }) => (
                          <input
                            {...field}
                            placeholder={t('visitRequest:step2Visitors.placeholderJob')}
                            className={cellInputCls(!!fe?.jobTitle)}
                          />
                        )}
                      />
                      {fe?.jobTitle && <CellError msg={fe.jobTitle.message} />}
                    </td>

                    <td className="p-0 border-l border-gray-100 min-w-[200px]">
                      <Controller
                        name={`visitors.${i}.organization`}
                        control={control}
                        render={({ field }) => (
                          <OrganizationCombobox
                            value={field.value}
                            onChange={field.onChange}
                            onBlur={field.onBlur}
                            hasError={!!fe?.organization}
                            placeholder={t('visitRequest:step2Visitors.placeholderOrg')}
                            isCell={true}
                          />
                        )}
                      />
                      {fe?.organization && <CellError msg={fe.organization.message} />}
                    </td>

                    <td className="p-0 border-l border-gray-100 min-w-[160px]">
                      <Controller
                        name={`visitors.${i}.nationality`}
                        control={control}
                        render={({ field }) => (
                          <CountrySelect
                            value={field.value}
                            onChange={field.onChange}
                            onBlur={field.onBlur}
                            hasError={!!fe?.nationality}
                            placeholder={t('visitRequest:step2Visitors.placeholderNat')}
                            isCell={true}
                          />
                        )}
                      />
                      {fe?.nationality && <CellError msg={fe.nationality.message} />}
                    </td>

                    <td className="p-2 border-l border-gray-100 text-center">
                      <button
                        type="button"
                        onClick={() => visitorFields.remove(i)}
                        aria-label={t('visitRequest:shared.delete')}
                        className="p-2 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </td>
                  </motion.tr>
                );
              })}
            </AnimatePresence>
          </tbody>
        </table>
      </div>

      {/* Mobile/tablet: stacked rows separated by dividers — no wide table, no cards */}
      <div className="lg:hidden">
        {visitorFields.fields.map((field, i) => {
          const fe = visitorErrors?.[i];
          return (
            <div key={field.id} className="space-y-3 border-b border-slate-200 py-4 first:pt-0 last:border-b-0">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                  {t('visitRequest:step2Visitors.stt')} {i + 1}
                </span>
                <button
                  type="button"
                  onClick={() => visitorFields.remove(i)}
                  aria-label={t('visitRequest:shared.delete')}
                  className="rounded-lg p-2 text-gray-400 transition-colors hover:bg-red-50 hover:text-red-500"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>

              <MobileField label={t('visitRequest:step2Visitors.fullName')} error={fe?.fullName?.message}>
                <Controller
                  name={`visitors.${i}.fullName`}
                  control={control}
                  render={({ field }) => (
                    <input
                      {...field}
                      placeholder={t('visitRequest:step2Visitors.placeholderName')}
                      className={inputCls(!!fe?.fullName, false, false)}
                    />
                  )}
                />
              </MobileField>

              <MobileField label={t('visitRequest:step2Visitors.jobTitle')} error={fe?.jobTitle?.message}>
                <Controller
                  name={`visitors.${i}.jobTitle`}
                  control={control}
                  render={({ field }) => (
                    <input
                      {...field}
                      placeholder={t('visitRequest:step2Visitors.placeholderJob')}
                      className={inputCls(!!fe?.jobTitle, false, false)}
                    />
                  )}
                />
              </MobileField>

              <MobileField label={t('visitRequest:step2Visitors.organization')} error={fe?.organization?.message}>
                <Controller
                  name={`visitors.${i}.organization`}
                  control={control}
                  render={({ field }) => (
                    <OrganizationCombobox
                      value={field.value}
                      onChange={field.onChange}
                      onBlur={field.onBlur}
                      hasError={!!fe?.organization}
                      placeholder={t('visitRequest:step2Visitors.placeholderOrg')}
                    />
                  )}
                />
              </MobileField>

              <MobileField label={t('visitRequest:step2Visitors.nationality')} error={fe?.nationality?.message}>
                <Controller
                  name={`visitors.${i}.nationality`}
                  control={control}
                  render={({ field }) => (
                    <CountrySelect
                      value={field.value}
                      onChange={field.onChange}
                      onBlur={field.onBlur}
                      hasError={!!fe?.nationality}
                      placeholder={t('visitRequest:step2Visitors.placeholderNat')}
                    />
                  )}
                />
              </MobileField>
            </div>
          );
        })}
      </div>

      {/* Flat toolbar */}
      <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
        <button
          type="button"
          onClick={() =>
            visitorFields.append({ fullName: '', jobTitle: '', organization: '', nationality: '' })
          }
          className="inline-flex items-center gap-2 rounded-xl bg-[#f37021]/10 px-4 py-2 text-sm font-bold text-[#f37021] transition-colors hover:bg-[#f37021]/20"
        >
          <Plus className="w-4 h-4" /> {t('visitRequest:step2Visitors.addVisitor')}
        </button>

        <div className="flex flex-wrap gap-2 sm:gap-3">
          <button
            type="button"
            onClick={() => downloadVisitorTemplate(excelT)}
            className="inline-flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-bold text-slate-700 transition-colors hover:bg-slate-50"
          >
            <Download className="w-4 h-4" /> {t('visitRequest:step2Visitors.downloadTemplate')}
          </button>

          <button
            type="button"
            disabled={isProcessing}
            onClick={() => fileInputRef.current?.click()}
            className="inline-flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-bold text-[#004c91] transition-colors hover:bg-blue-50 disabled:opacity-60"
          >
            {isProcessing
              ? <span className="w-4 h-4 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
              : <Upload className="w-4 h-4" />}
            {t('visitRequest:step2Visitors.uploadList')}
          </button>
          <input ref={fileInputRef} type="file" accept=".xlsx,.xls" className="hidden" onChange={handleFileChange} />
        </div>
      </div>

      <AnimatePresence>
        {uploadResult && (
          <motion.div
            initial={{ opacity: 0, y: -6 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0 }}
            className={[
              'mt-3 rounded-xl border p-3',
              addedCount > 0 ? 'bg-green-50 border-green-200' : 'bg-red-50 border-red-200',
            ].join(' ')}
          >
            <div className="flex items-start justify-between gap-2">
              <div className="flex items-center gap-2">
                {addedCount > 0
                  ? <CheckCircle2 className="w-4 h-4 text-green-600 shrink-0" />
                  : <AlertCircle className="w-4 h-4 text-red-500 shrink-0" />}
                <div>
                  <p className={`text-xs font-bold ${addedCount > 0 ? 'text-green-700' : 'text-red-700'}`}>
                    {addedCount > 0
                      ? t('visitRequest:step2Visitors.addedMsg', { added: addedCount, fileName: uploadFileName, total: visitorFields.fields.length })
                      : t('visitRequest:step2Visitors.failedMsg', { fileName: uploadFileName })}
                  </p>
                  {uploadResult.totalRows > 0 && (
                    <p className="text-[10px] text-gray-600 mt-0.5">
                      {uploadResult.skippedDuplicates > 0
                        ? t('visitRequest:step2Visitors.statsMsgDup', { totalRows: uploadResult.totalRows, errorRows: uploadResult.errorRows, skipped: uploadResult.skippedDuplicates })
                        : t('visitRequest:step2Visitors.statsMsg', { totalRows: uploadResult.totalRows, errorRows: uploadResult.errorRows })}
                    </p>
                  )}
                </div>
              </div>
              <button type="button" onClick={() => setUploadResult(null)} className="text-gray-400 hover:text-gray-600">
                <X className="w-3.5 h-3.5" />
              </button>
            </div>
            {uploadResult.errors.length > 0 && (
              <div className="mt-2 space-y-1 max-h-32 overflow-y-auto">
                {uploadResult.errors.map((err, i) => (
                  <div key={i} className="flex items-start gap-1.5 text-[11px] text-red-700 bg-red-100/60 px-2 py-1 rounded">
                    <FileSpreadsheet className="w-3 h-3 shrink-0 mt-0.5" />
                    {err.message}
                  </div>
                ))}
              </div>
            )}
          </motion.div>
        )}
      </AnimatePresence>
    </FormSection>
  );
};

/** Mobile stacked field: label + control + inline error, no card wrapper. */
export const MobileField: React.FC<{ label: string; error?: string; children: React.ReactNode }> = ({ label, error, children }) => (
  <div data-field-error={error ? 'true' : undefined}>
    <label className="mb-1 block text-xs font-bold text-slate-600">{label}</label>
    {children}
    {error && <p className="mt-1 text-xs font-medium text-red-600">{error}</p>}
  </div>
);

const cellInputCls = (hasError: boolean) =>
  [
    'w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-400 text-sm border focus:ring-1 focus:outline-none transition-colors',
    hasError ? 'text-red-700 border-red-300 bg-red-50/20 focus:border-red-400 focus:ring-red-300' : 'text-gray-900 border-transparent focus:border-blue-200 focus:ring-blue-200',
  ].join(' ');

const CellError: React.FC<{ msg?: string }> = ({ msg }) =>
  msg ? <p className="px-3 pb-2 pt-0.5 text-[10px] text-red-600 font-medium bg-red-50/20">{msg}</p> : null;
