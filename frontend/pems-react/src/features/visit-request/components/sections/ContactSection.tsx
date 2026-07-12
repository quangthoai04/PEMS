import React, { useEffect, useRef, useState } from 'react';
import { Controller, type UseFormReturn, type UseFieldArrayReturn } from 'react-hook-form';
import { Plus, Trash2, Download, Upload, CheckCircle2, AlertCircle, X, FileSpreadsheet } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { CountrySelect } from '../shared/CountrySelect';
import { OrganizationCombobox } from '../shared/OrganizationCombobox';
import { PhoneInput } from '../shared/PhoneInput';
import { FormField, inputCls } from '../shared/FormField';
import { FormSection } from '../shared/FormSection';
import { MobileField } from './VisitorListSection';
import { validateSupportTeamExcel, isAllowedExcelFile, type ExcelTranslator } from '../ExcelUpload/excelValidator';
import { downloadSupportTeamTemplate } from '../ExcelUpload/excelDownload';
import type { SupportTeamExcelValidationResult, SupportTeamEntry } from '../../types/visitRequest.types';
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
  supportTeamFields: UseFieldArrayReturn<VisitRequestSchema, 'supportTeam'>;
  onSyncSupportFromRegister: () => void;
  onClearSupportFirstRow: () => void;
  onSyncContactFromRegister: () => void;
  onClearContactPoint: () => void;
  showErrors?: boolean;
  /**
   * Internal actors (IC Staff / Staff Leader) can NEVER be their own contact owner:
   * hides the "Tôi cũng là đầu mối liên hệ" checkbox and shows the different-person hint.
   */
  allowContactSelf?: boolean;
}

export const ContactSection: React.FC<Props> = ({
  form,
  supportTeamFields,
  onSyncSupportFromRegister,
  onClearSupportFirstRow,
  onSyncContactFromRegister,
  onClearContactPoint,
  showErrors,
  allowContactSelf = true,
}) => {
  const { t } = useTranslation(['visitRequest']);
  // Excel helpers live outside React, so the translator is handed to them explicitly.
  const excelT: ExcelTranslator = (key, options) => t(key, options as never) as unknown as string;
  const { register, control, formState: { errors, touchedFields } } = form;
  const [isSupportSameAsRegister, setIsSupportSameAsRegister] = useState(false);
  const [isContactSameAsRegister, setIsContactSameAsRegister] = useState(false);

  const supportErrors = errors.supportTeam;
  const contactErrors = errors.contactPoint;
  const contactTouched = touchedFields.contactPoint;

  // Support team Excel upload state
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [uploadResult, setUploadResult] = useState<SupportTeamExcelValidationResult | null>(null);
  const [uploadFileName, setUploadFileName] = useState('');
  const [addedCount, setAddedCount] = useState(0);

  // Keep the contact point in sync with the registrant while "Tôi cũng là đầu mối liên hệ"
  // is ticked, so editing the registrant afterwards still updates the contact — and therefore
  // the VISITOR account, which is always created/linked from the contact email.
  const registrant = form.watch('registerInfo');
  useEffect(() => {
    if (isContactSameAsRegister) onSyncContactFromRegister();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    isContactSameAsRegister,
    registrant.fullName,
    registrant.organization,
    registrant.phone,
    registrant.email,
  ]);

  const handleSupportCheckbox = (checked: boolean) => {
    setIsSupportSameAsRegister(checked);
    if (checked) onSyncSupportFromRegister();
    else onClearSupportFirstRow();
  };

  const handleContactCheckbox = (checked: boolean) => {
    setIsContactSameAsRegister(checked);
    if (checked) onSyncContactFromRegister();
    else onClearContactPoint();
  };

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
        errors: [{ row: 0, column: '', message: t('visitRequest:step2Contact.uploadError') }],
        data: [],
      });
      setIsProcessing(false);
      e.target.value = '';
      return;
    }

    const existingData = supportTeamFields.fields.map(f => ({
      fullName: f.fullName,
      jobTitle: f.jobTitle,
      organization: f.organization,
      nationality: f.nationality
    }));
    const result = await validateSupportTeamExcel(file, existingData as SupportTeamEntry[], excelT);

    if (result.data.length > 0) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      result.data.forEach((member) => supportTeamFields.append(member as any));
      setAddedCount(result.data.length);
    }

    setUploadResult(result);
    setIsProcessing(false);
    e.target.value = '';
  };

  const supportRootErrorMessage = (supportErrors as any)?.root?.message || (typeof supportErrors === 'object' && !Array.isArray(supportErrors) ? (supportErrors as any).message : null);
  const hasSupportRootError = !!supportRootErrorMessage;
  const hasAnySupportError = showErrors && (hasSupportRootError || hasRealError(supportErrors));

  const hasAnyContactError = showErrors && !!contactErrors;

  // Server errors (contact email business conflict) must surface immediately,
  // regardless of touched state or whether a full submit was attempted.
  const showContactError = (field: keyof NonNullable<typeof contactErrors>) => {
    const err = contactErrors?.[field] as { message?: string; type?: string } | undefined;
    if (!err) return undefined;
    if (showErrors || err.type === 'server' || contactTouched?.[field as keyof NonNullable<typeof contactTouched>]) {
      return err.message;
    }
    return undefined;
  };

  return (
    <>
      {/* ── Support team ─────────────────────────────────────────────────────── */}
      <FormSection
        id="section-support"
        title={t('visitRequest:singleForm.sections.support')}
        required
        headerRight={
          <label className="flex cursor-pointer select-none items-center gap-2.5 text-sm font-bold text-[#004c91]">
            <input
              type="checkbox"
              checked={isSupportSameAsRegister}
              onChange={(e) => handleSupportCheckbox(e.target.checked)}
              className="w-4 h-4 rounded text-[#004c91] focus:ring-[#004c91] border-blue-300 cursor-pointer"
            />
            {t('visitRequest:step2Contact.iamSupport')}
          </label>
        }
      >
        {hasAnySupportError && (
          <div className="error-scroll-target mb-4 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 p-3">
            <AlertCircle className="w-5 h-5 text-red-500 shrink-0" />
            <div>
              <p className="text-sm font-bold text-red-700">{t('visitRequest:step2Contact.errorBoxTitle')}</p>
              {supportRootErrorMessage && <p className="text-xs text-red-600 mt-0.5">{supportRootErrorMessage}</p>}
            </div>
          </div>
        )}

        {/* Desktop: single table frame */}
        <div className="hidden overflow-x-auto rounded-xl border border-slate-200 lg:block">
          <table className="w-full min-w-[750px] border-collapse text-sm">
            <thead className="border-b border-gray-200 bg-slate-50">
              <tr>
                <th className="w-12 p-3 text-center font-bold text-slate-700">{t('visitRequest:step2Contact.stt')}</th>
                <th className="border-l border-gray-200 p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Contact.fullName')}</th>
                <th className="border-l border-gray-200 p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Contact.jobTitle')}</th>
                <th className="border-l border-gray-200 p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Contact.organization')}</th>
                <th className="border-l border-gray-200 p-3 text-left font-bold text-slate-700">{t('visitRequest:step2Contact.nationality')}</th>
                <th className="w-12 border-l border-gray-200 p-3 text-center" />
              </tr>
            </thead>
            <tbody>
              <AnimatePresence>
                {supportTeamFields.fields.map((field, i) => {
                  const se = supportErrors?.[i];
                  return (
                    <motion.tr
                      key={field.id}
                      initial={{ opacity: 0, y: -6 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, height: 0 }}
                      className="border-b border-gray-100 last:border-b-0 hover:bg-orange-50/40 focus-within:bg-orange-50/30 transition-colors"
                    >
                      <td className="p-3 text-center font-bold text-slate-400">{i + 1}</td>

                      {/* Controlled inputs: rendered in both desktop table and mobile list,
                          duplicate uncontrolled register() refs would break value tracking. */}
                      <td className="p-0 border-l border-gray-100">
                        <Controller
                          name={`supportTeam.${i}.fullName`}
                          control={control}
                          render={({ field }) => (
                            <input
                              {...field}
                              placeholder={t('visitRequest:step2Contact.placeholderName')}
                              className={cellInputCls(!!se?.fullName)}
                            />
                          )}
                        />
                        {se?.fullName && <CellError msg={se.fullName.message} />}
                      </td>

                      <td className="p-0 border-l border-gray-100">
                        <Controller
                          name={`supportTeam.${i}.jobTitle`}
                          control={control}
                          render={({ field }) => (
                            <input
                              {...field}
                              placeholder={t('visitRequest:step2Contact.placeholderJob')}
                              className={cellInputCls(!!se?.jobTitle)}
                            />
                          )}
                        />
                        {se?.jobTitle && <CellError msg={se.jobTitle.message} />}
                      </td>

                      <td className="p-0 border-l border-gray-100 min-w-[200px]">
                        <Controller
                          name={`supportTeam.${i}.organization`}
                          control={control}
                          render={({ field }) => (
                            <OrganizationCombobox
                              value={field.value}
                              onChange={field.onChange}
                              onBlur={field.onBlur}
                              hasError={!!se?.organization}
                              placeholder={t('visitRequest:step2Contact.placeholderOrg')}
                              isCell={true}
                            />
                          )}
                        />
                        {se?.organization && <CellError msg={se.organization.message} />}
                      </td>

                      <td className="p-0 border-l border-gray-100 min-w-[160px]">
                        <Controller
                          name={`supportTeam.${i}.nationality`}
                          control={control}
                          render={({ field }) => (
                            <CountrySelect
                              value={field.value}
                              onChange={field.onChange}
                              onBlur={field.onBlur}
                              hasError={!!se?.nationality}
                              placeholder={t('visitRequest:step2Contact.placeholderNat')}
                              isCell={true}
                            />
                          )}
                        />
                        {se?.nationality && <CellError msg={se.nationality.message} />}
                      </td>

                      <td className="p-2 border-l border-gray-100 text-center">
                        <button
                          type="button"
                          onClick={() => supportTeamFields.remove(i)}
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

        {/* Mobile/tablet: stacked rows separated by dividers */}
        <div className="lg:hidden">
          {supportTeamFields.fields.map((field, i) => {
            const se = supportErrors?.[i];
            return (
              <div key={field.id} className="space-y-3 border-b border-slate-200 py-4 first:pt-0 last:border-b-0">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                    {t('visitRequest:step2Contact.stt')} {i + 1}
                  </span>
                  <button
                    type="button"
                    onClick={() => supportTeamFields.remove(i)}
                    aria-label={t('visitRequest:shared.delete')}
                    className="rounded-lg p-2 text-gray-400 transition-colors hover:bg-red-50 hover:text-red-500"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>

                <MobileField label={t('visitRequest:step2Contact.fullName')} error={se?.fullName?.message}>
                  <Controller
                    name={`supportTeam.${i}.fullName`}
                    control={control}
                    render={({ field }) => (
                      <input
                        {...field}
                        placeholder={t('visitRequest:step2Contact.placeholderName')}
                        className={inputCls(!!se?.fullName, false, false)}
                      />
                    )}
                  />
                </MobileField>

                <MobileField label={t('visitRequest:step2Contact.jobTitle')} error={se?.jobTitle?.message}>
                  <Controller
                    name={`supportTeam.${i}.jobTitle`}
                    control={control}
                    render={({ field }) => (
                      <input
                        {...field}
                        placeholder={t('visitRequest:step2Contact.placeholderJob')}
                        className={inputCls(!!se?.jobTitle, false, false)}
                      />
                    )}
                  />
                </MobileField>

                <MobileField label={t('visitRequest:step2Contact.organization')} error={se?.organization?.message}>
                  <Controller
                    name={`supportTeam.${i}.organization`}
                    control={control}
                    render={({ field }) => (
                      <OrganizationCombobox
                        value={field.value}
                        onChange={field.onChange}
                        onBlur={field.onBlur}
                        hasError={!!se?.organization}
                        placeholder={t('visitRequest:step2Contact.placeholderOrg')}
                      />
                    )}
                  />
                </MobileField>

                <MobileField label={t('visitRequest:step2Contact.nationality')} error={se?.nationality?.message}>
                  <Controller
                    name={`supportTeam.${i}.nationality`}
                    control={control}
                    render={({ field }) => (
                      <CountrySelect
                        value={field.value}
                        onChange={field.onChange}
                        onBlur={field.onBlur}
                        hasError={!!se?.nationality}
                        placeholder={t('visitRequest:step2Contact.placeholderNat')}
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
              supportTeamFields.append({ fullName: '', jobTitle: '', organization: '', nationality: '' })
            }
            className="inline-flex items-center gap-2 rounded-xl bg-[#f37021]/10 px-4 py-2 text-sm font-bold text-[#f37021] transition-colors hover:bg-[#f37021]/20"
          >
            <Plus className="w-4 h-4" /> {t('visitRequest:step2Contact.addSupport')}
          </button>
          <div className="flex flex-wrap gap-2 sm:gap-3">
            <button
              type="button"
              onClick={() => downloadSupportTeamTemplate(excelT)}
              className="inline-flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-bold text-slate-700 transition-colors hover:bg-slate-50"
            >
              <Download className="w-4 h-4" /> {t('visitRequest:step2Contact.downloadTemplate')}
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
              {t('visitRequest:step2Contact.uploadList')}
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
                        ? t('visitRequest:step2Contact.addedMsg', { added: addedCount, fileName: uploadFileName, total: supportTeamFields.fields.length })
                        : t('visitRequest:step2Contact.failedMsg', { fileName: uploadFileName })}
                    </p>
                    {uploadResult.totalRows > 0 && (
                      <p className="text-[10px] text-gray-600 mt-0.5">
                        {uploadResult.skippedDuplicates > 0
                          ? t('visitRequest:step2Contact.statsMsgDup', { totalRows: uploadResult.totalRows, errorRows: uploadResult.errorRows, skipped: uploadResult.skippedDuplicates })
                          : t('visitRequest:step2Contact.statsMsg', { totalRows: uploadResult.totalRows, errorRows: uploadResult.errorRows })}
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

      {/* ── Contact point ─────────────────────────────────────────────────────── */}
      <FormSection
        id="section-contact"
        title={t('visitRequest:singleForm.sections.contact')}
        required
        description={t('visitRequest:step2Contact.contactDesc2')}
        headerRight={
          allowContactSelf ? (
            <label className="flex cursor-pointer select-none items-center gap-2.5 text-sm font-bold text-[#004c91]">
              <input
                type="checkbox"
                checked={isContactSameAsRegister}
                onChange={(e) => handleContactCheckbox(e.target.checked)}
                className="w-4 h-4 rounded text-[#004c91] focus:ring-[#004c91] border-blue-300 cursor-pointer"
              />
              {t('visitRequest:step2Contact.iamContact')}
            </label>
          ) : (
            <span className="text-xs font-semibold text-slate-500">
              {t('visitRequest:step2Contact.internalMustPickOther')}
            </span>
          )
        }
      >
        {hasAnyContactError && (
          <div className="error-scroll-target mb-4 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 p-3">
            <AlertCircle className="w-5 h-5 text-red-500 shrink-0" />
            <p className="text-sm font-bold text-red-700">{t('visitRequest:step2Contact.errorBoxTitle')}</p>
          </div>
        )}

        <p className="mb-4 text-xs text-slate-500">
          {t('visitRequest:step2Contact.contactDesc1')}
        </p>

        {/* Flat field grid — no table-in-card */}
        <div className="grid grid-cols-1 gap-x-8 gap-y-5 md:grid-cols-2">
          <FormField
            label={t('visitRequest:step2Contact.contactFullName').replace(' *', '')}
            required
            error={showContactError('fullName')}
          >
            <input
              {...register('contactPoint.fullName')}
              placeholder={t('visitRequest:step2Contact.placeholderName')}
              className={inputCls(!!showContactError('fullName'), false, false)}
            />
          </FormField>

          <FormField
            label={t('visitRequest:step2Contact.contactOrg').replace(' *', '')}
            required
            error={showContactError('organization')}
            showValidIcon={false}
          >
            <Controller
              name="contactPoint.organization"
              control={control}
              render={({ field }) => (
                <OrganizationCombobox
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  hasError={!!showContactError('organization')}
                  placeholder={t('visitRequest:step2Contact.placeholderOrg')}
                />
              )}
            />
          </FormField>

          <FormField
            label={t('visitRequest:step2Contact.contactPhone').replace(' *', '')}
            required
            error={showContactError('phone')}
            showValidIcon={false}
          >
            <Controller
              name="contactPoint.phone"
              control={control}
              render={({ field }) => (
                <PhoneInput
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  hasError={!!showContactError('phone')}
                />
              )}
            />
          </FormField>

          <FormField
            label={t('visitRequest:step2Contact.contactEmail').replace(' *', '')}
            required
            error={showContactError('email')}
          >
            <input
              {...register('contactPoint.email')}
              type="email"
              placeholder={t('visitRequest:step2Contact.placeholderEmail')}
              className={inputCls(!!showContactError('email'), false, false)}
            />
          </FormField>
        </div>
      </FormSection>
    </>
  );
};

const cellInputCls = (hasError: boolean) =>
  [
    'w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-400 text-sm border focus:ring-1 focus:outline-none transition-colors',
    hasError ? 'text-red-700 border-red-300 bg-red-50/20 focus:border-red-400 focus:ring-red-300' : 'text-gray-900 border-transparent focus:border-blue-200 focus:ring-blue-200',
  ].join(' ');

const CellError: React.FC<{ msg?: string }> = ({ msg }) =>
  msg ? <p className="px-3 pb-2 pt-0.5 text-[10px] text-red-600 font-medium bg-red-50/20">{msg}</p> : null;
