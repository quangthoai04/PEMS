import React from 'react';
import { Controller, type UseFormReturn } from 'react-hook-form';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { FormField, inputCls } from '../shared/FormField';
import { CountrySelect } from '../shared/CountrySelect';
import { PhoneInput } from '../shared/PhoneInput';
import { PartnerAsyncSelect } from '../shared/PartnerAsyncSelect';

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
  showErrors?: boolean;
}

export const RegisterInfoSection: React.FC<Props> = ({ form, showErrors }) => {
  const { register, control, watch, setValue, formState: { errors, touchedFields, isSubmitted } } = form;
  const e = errors.registerInfo;
  const t = touchedFields.registerInfo;
  const selectedPartnerId = watch('partnerId');

  const shouldShowError = (field: keyof NonNullable<typeof t>, specificError?: any) => {
    return !!specificError && (t?.[field] || showErrors || isSubmitted);
  };

  const isValid = (field: keyof NonNullable<typeof t>) =>
    t?.[field] && !e?.[field];

  return (
    <section>
      <SectionTitle index={1} title="THÔNG TIN NGƯỜI ĐĂNG KÝ" />
      
      <div className="rounded-3xl border border-slate-200 border-l-4 border-l-[#F37021] bg-white/95 p-6 shadow-sm">
        <div className="mb-6 border-b border-slate-200 pb-4">
          <h3 className="text-lg font-extrabold text-slate-900">
            I. THÔNG TIN NGƯỜI ĐĂNG KÝ
          </h3>
        </div>

        <div className="grid grid-cols-1 gap-x-10 gap-y-6 lg:grid-cols-2">

        <FormField
          label="Họ và tên"
          required
          error={shouldShowError('fullName', e?.fullName) ? e?.fullName?.message : undefined}
          isValid={isValid('fullName')}
        >
          <input
            {...register('registerInfo.fullName')}
            placeholder="Nguyễn Văn A"
            className={inputCls(shouldShowError('fullName', e?.fullName), isValid('fullName'))}
          />
        </FormField>

        <FormField
          label="Quốc tịch"
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
            label="Đối tác/Tổ chức đã có trong hệ thống"
            error={(form.formState.touchedFields.partnerId || showErrors || isSubmitted) ? form.formState.errors.partnerId?.message as string | undefined : undefined}
            isValid={form.formState.touchedFields.partnerId && !form.formState.errors.partnerId}
            subtitle="Nếu đơn vị của bạn đã là đối tác của FPTU, vui lòng chọn tại đây."
            showValidIcon={false}
          >
            <Controller
              name="partnerId"
              control={control}
              render={({ field }) => (
                <PartnerAsyncSelect
                  value={field.value ?? null}
                  partnerName={watch('registerInfo.organization')}
                  onChange={(val, name) => {
                    field.onChange(val);
                    if (val !== null) {
                      setValue('registerInfo.organization', name, { shouldValidate: true, shouldDirty: true });
                    } else {
                      setValue('registerInfo.organization', '', { shouldValidate: true, shouldDirty: true });
                    }
                  }}
                  onBlur={field.onBlur}
                  hasError={!!form.formState.errors.partnerId && (!!form.formState.touchedFields.partnerId || !!showErrors || isSubmitted)}
                />
              )}
            />
          </FormField>
        </div>

        {selectedPartnerId === null && (
          <div className="lg:col-span-2">
            <FormField
              label="Đơn vị công tác"
              required
              error={shouldShowError('organization', e?.organization) ? e?.organization?.message : undefined}
              isValid={isValid('organization')}
            >
              <input
                {...register('registerInfo.organization')}
                placeholder="Nhập tên đơn vị công tác của bạn..."
                className={inputCls(shouldShowError('organization', e?.organization), isValid('organization'))}
              />
            </FormField>
          </div>
        )}

        <FormField
          label="Chức danh, phòng ban"
          required
          error={shouldShowError('jobTitle', e?.jobTitle) ? e?.jobTitle?.message : undefined}
          isValid={isValid('jobTitle')}
        >
          <input
            {...register('registerInfo.jobTitle')}
            placeholder="Giám đốc - Phòng Hợp tác Quốc tế"
            className={inputCls(shouldShowError('jobTitle', e?.jobTitle), isValid('jobTitle'))}
          />
        </FormField>

        <FormField
          label="Số điện thoại"
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
          label="Email"
          required
          error={shouldShowError('email', e?.email) ? e?.email?.message : undefined}
          isValid={isValid('email')}
          subtitle="Email này chỉ dùng để nhận mã OTP xác thực việc gửi form. Tài khoản theo dõi yêu cầu sẽ được tạo theo email ở phần Thông tin đầu mối liên hệ."
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
