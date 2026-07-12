import React from 'react';
import { Controller, type UseFormReturn } from 'react-hook-form';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { FormField, inputCls } from '../shared/FormField';
import { FormSection } from '../shared/FormSection';
import { CountrySelect } from '../shared/CountrySelect';
import { PhoneInput } from '../shared/PhoneInput';
import { PartnerOrgCombobox } from '../shared/PartnerOrgCombobox';
import { useTranslation } from 'react-i18next';

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
  showErrors?: boolean;
  /**
   * Authenticated mode: full name + email come from the signed-in account and are
   * rendered read-only (anti-impersonation — the backend overrides them anyway).
   */
  identityReadOnly?: boolean;
}

export const RegisterInfoSection: React.FC<Props> = ({ form, showErrors, identityReadOnly }) => {
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
    <FormSection
      id="section-registrant"
      title={t('visitRequest:singleForm.sections.registrant')}
    >
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
            readOnly={identityReadOnly}
            aria-readonly={identityReadOnly || undefined}
            className={`${inputCls(shouldShowError('fullName', e?.fullName), isValid('fullName'))} ${identityReadOnly ? 'cursor-not-allowed bg-slate-100 text-slate-500' : ''}`}
          />
          {identityReadOnly && (
            <p className="mt-1 text-[11px] font-medium text-slate-400">
              {t('visitRequest:step1.identityFromAccount')}
            </p>
          )}
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
            readOnly={identityReadOnly}
            aria-readonly={identityReadOnly || undefined}
            className={`${inputCls(shouldShowError('email', e?.email), isValid('email'))} ${identityReadOnly ? 'cursor-not-allowed bg-slate-100 text-slate-500' : ''}`}
          />
          {identityReadOnly && (
            <p className="mt-1 text-[11px] font-medium text-slate-400">
              {t('visitRequest:step1.identityFromAccount')}
            </p>
          )}
        </FormField>

      </div>
    </FormSection>
  );
};
