import React from 'react';
import { Controller, type UseFormReturn } from 'react-hook-form';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { FormField, inputCls } from '../shared/FormField';
import { CountrySelect } from '../shared/CountrySelect';
import { PhoneInput } from '../shared/PhoneInput';
import { OrganizationSelect } from '../shared/OrganizationSelect';
import { partnersData } from '../../../../pages/PartnersPage';

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
                hasError={!!e?.nationality}
              />
            )}
          />
        </FormField>

        <FormField
          label="Đối tác/Tổ chức đã có trong hệ thống"
          error={e?.partnerId?.message as string | undefined}
          isValid={t?.partnerId && !e?.partnerId}
          subtitle="Nếu đơn vị của bạn đã là đối tác của FPTU, vui lòng chọn tại đây."
        >
          <div className="relative">
            <select
              {...register('partnerId', { setValueAs: v => v === "" ? null : Number(v) })}
              className={inputCls(!!e?.partnerId, t?.partnerId && !e?.partnerId)}
            >
              <option value="">-- Tổ chức mới / Chưa có trong hệ thống --</option>
              {partnersData.map(partner => (
                <option key={partner.id} value={partner.id}>
                  {partner.name}
                </option>
              ))}
            </select>
          </div>
        </FormField>

        <FormField
          label="Đơn vị công tác"
          required
          error={e?.organization?.message}
          isValid={isValid('organization')}
          showValidIcon={false}
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
          subtitle="Email này chỉ dùng để nhận mã OTP xác thực việc gửi form. Tài khoản theo dõi yêu cầu sẽ được tạo theo email ở phần Thông tin đầu mối liên hệ."
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
