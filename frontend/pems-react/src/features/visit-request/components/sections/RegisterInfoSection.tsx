import React from 'react';
import { Controller, type UseFormReturn } from 'react-hook-form';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { FormField, inputCls } from '../shared/FormField';
import { CountrySelect } from '../shared/CountrySelect';
import { PhoneInput } from '../shared/PhoneInput';
import { OrganizationSelect } from '../shared/OrganizationSelect';

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
}

export const RegisterInfoSection: React.FC<Props> = ({ form }) => {
  const { register, control, formState: { errors, touchedFields } } = form;
  const e = errors.registerInfo;
  const t = touchedFields.registerInfo;

  const isValid = (field: keyof NonNullable<typeof t>) =>
    t?.[field] && !e?.[field];

  return (
    <section>
      <SectionTitle index={1} title="THÔNG TIN NGƯỜI ĐĂNG KÝ" />
      <div className="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-6">

        <FormField
          label="Họ và tên"
          required
          error={e?.fullName?.message}
          isValid={isValid('fullName')}
        >
          <input
            {...register('registerInfo.fullName')}
            placeholder="Nguyễn Văn A"
            className={inputCls(!!e?.fullName, isValid('fullName'))}
          />
        </FormField>

        <FormField
          label="Quốc tịch"
          required
          error={e?.nationality?.message}
          isValid={isValid('nationality')}
        >
          <Controller
            name="registerInfo.nationality"
            control={control}
            render={({ field }) => (
              <CountrySelect
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                hasError={!!e?.nationality}
              />
            )}
          />
        </FormField>

        <FormField
          label="Đơn vị công tác"
          required
          error={e?.organization?.message}
          isValid={isValid('organization')}
        >
          <Controller
            name="registerInfo.organization"
            control={control}
            render={({ field }) => (
              <OrganizationSelect
                value={field.value}
                onChange={field.onChange}
                onBlur={field.onBlur}
                hasError={!!e?.organization}
              />
            )}
          />
        </FormField>

        <FormField
          label="Chức danh, phòng ban"
          required
          error={e?.jobTitle?.message}
          isValid={isValid('jobTitle')}
        >
          <input
            {...register('registerInfo.jobTitle')}
            placeholder="Giám đốc - Phòng Hợp tác Quốc tế"
            className={inputCls(!!e?.jobTitle, isValid('jobTitle'))}
          />
        </FormField>

        <FormField
          label="Số điện thoại"
          required
          error={e?.phone?.message}
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
                hasError={!!e?.phone}
              />
            )}
          />
        </FormField>

        <FormField
          label="Email"
          required
          error={e?.email?.message}
          isValid={isValid('email')}
        >
          <input
            {...register('registerInfo.email')}
            type="email"
            placeholder="example@domain.com"
            className={inputCls(!!e?.email, isValid('email'))}
          />
        </FormField>

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
