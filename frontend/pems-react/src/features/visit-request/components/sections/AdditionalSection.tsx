import React from 'react';
import { type UseFormReturn } from 'react-hook-form';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { inputCls } from '../shared/FormField';
import { useTranslation } from 'react-i18next';

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
}

export const AdditionalSection: React.FC<Props> = ({ form }) => {
  const { t } = useTranslation(['visitRequest']);
  const { register, formState: { errors, touchedFields } } = form;

  return (
    <div className="bg-slate-50/50 rounded-2xl border-l-4 border-l-[#f37021] border border-gray-100 p-5 sm:p-7 shadow-sm mt-8">
      <h4 className="text-gray-800 font-bold text-base mb-5 border-b border-gray-200 pb-2 uppercase tracking-wide">
        {t('visitRequest:step3.title')}
      </h4>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
        {/* Language */}
        <div>
          <label className="block text-base font-bold text-gray-900 mb-2">
            {t('visitRequest:step3.language')}
          </label>
          <div className="flex items-center gap-8 mt-2 mb-3">
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('workingLanguage')}
                value="EN"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">{t('visitRequest:step3.en')}</span>
            </label>
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('workingLanguage')}
                value="VI"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">{t('visitRequest:step3.vi')}</span>
            </label>
          </div>
          {errors.workingLanguage && (
            <p className="text-xs text-red-600 font-medium mb-2">⚠ {errors.workingLanguage.message}</p>
          )}
          <p className="text-xs text-gray-500 italic mt-1">
            {t('visitRequest:step3.langNote')}
          </p>

        </div>

        {/* Transportation — free text (campus-independent approval spec: no type enum anymore) */}
        <div>
          <label className="block text-base font-bold text-gray-900 mb-2">
            {t('visitRequest:step3.transport')}
          </label>
          <textarea
            {...register('transportationNote')}
            rows={4}
            placeholder={t('visitRequest:step3.transportPlaceholder')}
            className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none transition-all bg-white text-sm shadow-sm resize-none font-medium text-gray-900"
          />
          {errors.transportationNote && (
            <p className="text-xs text-red-600 font-medium mt-1">⚠ {errors.transportationNote.message}</p>
          )}
          <p className="text-xs text-gray-500 italic mt-1">
            {t('visitRequest:step3.transportNote')}
          </p>
        </div>

        {/* Media Consent */}
        <div className="md:col-span-2 border-t border-gray-200 pt-6 mt-2">
          <label className="block text-base font-bold text-gray-900 mb-2">
            {t('visitRequest:step3.media')}
          </label>
          <div className="flex items-center gap-8 mt-2 mb-3">
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('mediaConsentStatus')}
                value="AGREED"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">{t('visitRequest:step3.agreed')}</span>
            </label>
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('mediaConsentStatus')}
                value="DECLINED"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">{t('visitRequest:step3.declined')}</span>
            </label>

          </div>
          {errors.mediaConsentStatus && (
            <p className="text-xs text-red-600 font-medium mb-2">⚠ {errors.mediaConsentStatus.message}</p>
          )}

          <label className="block text-sm font-bold text-gray-900 mt-4 mb-2">
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
        <label className="block text-base font-bold text-gray-900 mb-2">{t('visitRequest:step3.notes')}</label>
        <textarea
          {...register('notes')}
          rows={4}
          placeholder={t('visitRequest:step3.notesPlaceholder')}
          className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none transition-all bg-white text-sm shadow-sm resize-none font-medium text-gray-900"
        />
      </div>
    </div>
  );
};
