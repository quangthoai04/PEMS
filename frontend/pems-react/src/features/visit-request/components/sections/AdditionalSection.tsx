import React from 'react';
import { type UseFormReturn } from 'react-hook-form';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { inputCls, textareaCls } from '../shared/FormField';
import { FormSection } from '../shared/FormSection';
import { useTranslation } from 'react-i18next';

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
  showErrors?: boolean;
}

export const AdditionalSection: React.FC<Props> = ({ form, showErrors }) => {
  const { t } = useTranslation(['visitRequest']);
  const { register, formState: { errors, touchedFields } } = form;

  // Same convention as the other sections: errors appear after the first submit
  // attempt (showErrors) or once the field has been touched.
  const showFieldError = (field: 'workingLanguage' | 'transportationNote' | 'mediaConsentStatus') =>
    !!errors[field] && (showErrors || !!touchedFields[field]);

  return (
    <FormSection
      id="section-additional"
      title={t('visitRequest:singleForm.sections.additional')}
    >
      <div className="grid grid-cols-1 gap-8 md:grid-cols-2">
        {/* Language */}
        <div data-field-error={showFieldError('workingLanguage') ? 'true' : undefined}>
          <label className="block text-sm font-bold text-slate-900 mb-2">
            {t('visitRequest:step3.language')}
          </label>
          <div className="mt-2 mb-3 flex items-center gap-8">
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('workingLanguage')}
                value="EN"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-sm font-bold text-gray-800 transition-colors group-hover:text-[#004c91]">{t('visitRequest:step3.en')}</span>
            </label>
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('workingLanguage')}
                value="VI"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-sm font-bold text-gray-800 transition-colors group-hover:text-[#004c91]">{t('visitRequest:step3.vi')}</span>
            </label>
          </div>
          {showFieldError('workingLanguage') && (
            <p className="text-xs text-red-600 font-medium mb-2">⚠ {errors.workingLanguage?.message}</p>
          )}
          <p className="mt-1 text-xs italic text-gray-500">
            {t('visitRequest:step3.langNote')}
          </p>
        </div>

        {/* Transportation — free text (campus-independent approval spec: no type enum anymore) */}
        <div data-field-error={showFieldError('transportationNote') ? 'true' : undefined}>
          <label className="block text-sm font-bold text-slate-900 mb-2">
            {t('visitRequest:step3.transport')}
          </label>
          <textarea
            {...register('transportationNote')}
            rows={4}
            placeholder={t('visitRequest:step3.transportPlaceholder')}
            className={textareaCls(showFieldError('transportationNote'))}
          />
          {showFieldError('transportationNote') && (
            <p className="mt-1 text-xs font-medium text-red-600">⚠ {errors.transportationNote?.message}</p>
          )}
          <p className="mt-1 text-xs italic text-gray-500">
            {t('visitRequest:step3.transportNote')}
          </p>
        </div>

        {/* Media Consent */}
        <div
          className="md:col-span-2 border-t border-slate-200 pt-6"
          data-field-error={showFieldError('mediaConsentStatus') ? 'true' : undefined}
        >
          <label className="block text-sm font-bold text-slate-900 mb-2">
            {t('visitRequest:step3.media')}
          </label>
          <div className="mt-2 mb-3 flex items-center gap-8">
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('mediaConsentStatus')}
                value="AGREED"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-sm font-bold text-gray-800 transition-colors group-hover:text-[#004c91]">{t('visitRequest:step3.agreed')}</span>
            </label>
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('mediaConsentStatus')}
                value="DECLINED"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-sm font-bold text-gray-800 transition-colors group-hover:text-[#004c91]">{t('visitRequest:step3.declined')}</span>
            </label>
          </div>
          {showFieldError('mediaConsentStatus') && (
            <p className="text-xs text-red-600 font-medium mb-2">⚠ {errors.mediaConsentStatus?.message}</p>
          )}

          <label className="mt-4 mb-2 block text-sm font-bold text-slate-900">
            {t('visitRequest:step3.mediaNoteTitle')}
          </label>
          <input
            {...register('mediaConsentNote')}
            placeholder={t('visitRequest:step3.mediaNotePlaceholder')}
            className={inputCls(false, !!(touchedFields.mediaConsentNote && form.getValues('mediaConsentNote')))}
          />
        </div>
      </div>

      {/* Notes */}
      <div className="mt-8">
        <label className="block text-sm font-bold text-slate-900 mb-2">{t('visitRequest:step3.notes')}</label>
        <textarea
          {...register('notes')}
          rows={4}
          placeholder={t('visitRequest:step3.notesPlaceholder')}
          className={textareaCls(false)}
        />
      </div>
    </FormSection>
  );
};
