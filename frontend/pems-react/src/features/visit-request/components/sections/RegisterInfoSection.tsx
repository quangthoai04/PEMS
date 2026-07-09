import React from 'react';
import { Controller, type UseFormReturn } from 'react-hook-form';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { FormField, inputCls } from '../shared/FormField';
import { CountrySelect } from '../shared/CountrySelect';
import { PhoneInput } from '../shared/PhoneInput';
import { PartnerOrgCombobox } from '../shared/PartnerOrgCombobox';
import { useTranslation } from 'react-i18next';

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
  showErrors?: boolean;
}

export const RegisterInfoSection: React.FC<Props> = ({ form, showErrors }) => {
  const { t } = useTranslation(['visitRequest']);
  const { register, control, watch, setValue, formState: { errors, touchedFields, isSubmitted } } = form;
  const e = errors.registerInfo;
  const tf = touchedFields.registerInfo;

  const shouldShowError = (field: keyof NonNullable<typeof tf>, specificError?: any) => {
    return !!specificError && (tf?.[field] || showErrors || isSubmitted);
  };

  const isValid = (field: keyof NonNullable<typeof tf>) =>
    tf?.[field] && !e?.[field];

  return (
    <section>
      <SectionTitle index={1} title={t('visitRequest:step1.title')} />
      
      <div className="rounded-3xl border border-slate-200 border-l-4 border-l-[#F37021] bg-white/95 p-6 shadow-sm">
        <div className="mb-6 border-b border-slate-200 pb-4">
          <h3 className="text-lg font-extrabold text-slate-900">
            {t('visitRequest:step1.groupTitle')}
          </h3>
        </div>

        <div className="grid grid-cols-1 gap-x-10 gap-y-6 lg:grid-cols-2">

        <FormField
          label={t('visitRequest:step1.fullName')}
          required
          error={shouldShowError('fullName', e?.fullName) ? e?.fullName?.message : undefined}
          isValid={isValid('fullName')}
        >
          <input
            {...register('registerInfo.fullName')}
            placeholder={t('visitRequest:step1.fullNamePlaceholder')}
            className={inputCls(shouldShowError('fullName', e?.fullName), isValid('fullName'))}
          />
        </FormField>

        <FormField
          label={t('visitRequest:step1.nationality')}
          required
          error={shouldShowError('nationality', e?.nationality) ? e?.nationality?.message : undefined}
          isValid={isValid('nationality')}
          showValidIcon={false}
        >
          <Controller
            name="registerInfo.nationality"
            control={control}
            render={({ field }) => (
              <CountrySelect
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                hasError={shouldShowError('nationality', e?.nationality)}
              />
            )}
          />
        </FormField>

        <div className="lg:col-span-2">
          <FormField
            label={t('visitRequest:step1.organization')}
            required
            error={shouldShowError('organization', e?.organization) ? e?.organization?.message : undefined}
            isValid={isValid('organization')}
            subtitle={t('visitRequest:step1.organizationSubtitle')}
            showValidIcon={false}
          >
            <Controller
              name="registerInfo.organization"
              control={control}
              render={({ field }) => (
                <PartnerOrgCombobox
                  organization={field.value ?? ''}
                  partnerId={watch('partnerId') ?? null}
                  onChange={({ organization, partnerId, mode }) => {
                    field.onChange(organization);
                    setValue('partnerId', partnerId, { shouldValidate: true, shouldDirty: true });
                    setValue('partnerSelectionMode', mode, { shouldDirty: true });
                  }}
                  onBlur={field.onBlur}
                  hasError={shouldShowError('organization', e?.organization)}
                />
              )}
            />
          </FormField>
        </div>

        <FormField
          label={t('visitRequest:step1.jobTitle')}
          required
          error={shouldShowError('jobTitle', e?.jobTitle) ? e?.jobTitle?.message : undefined}
          isValid={isValid('jobTitle')}
        >
          <input
            {...register('registerInfo.jobTitle')}
            className={inputCls(shouldShowError('jobTitle', e?.jobTitle), isValid('jobTitle'))}
          />
        </FormField>

        <FormField
          label={t('visitRequest:step1.phone')}
          required
          error={shouldShowError('phone', e?.phone) ? e?.phone?.message : undefined}
          isValid={isValid('phone')}
        >
          <Controller
            name="registerInfo.phone"
            control={control}
            render={({ field }) => (
              <PhoneInput
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                hasError={shouldShowError('phone', e?.phone)}
              />
            )}
          />
        </FormField>

        <FormField
          label={t('visitRequest:step1.email')}
          required
          error={shouldShowError('email', e?.email) ? e?.email?.message : undefined}
          isValid={isValid('email')}
          subtitle={t('visitRequest:step1.emailSubtitle')}
        >
          <input
            {...register('registerInfo.email')}
            type="email"
            placeholder="example@domain.com"
            className={inputCls(shouldShowError('email', e?.email), isValid('email'))}
          />
        </FormField>

        </div>
      </div>
    </section>
  );
};

export const SectionTitle: React.FC<{ index: number; title: string }> = ({ index, title }) => (
  <h3 className="text-lg sm:text-xl font-black text-[#004c91] border-b-2 border-[#f37021]/30 pb-2 mb-6 flex items-center gap-2 w-max pr-6">
    <span className="flex items-center justify-center w-6 h-6 sm:w-7 sm:h-7 rounded-full bg-[#f37021] text-white text-sm">
      {index}
    </span>
    {title}
  </h3>
);
