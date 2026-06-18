import React from 'react';
import { type UseFormReturn } from 'react-hook-form';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { inputCls } from '../shared/FormField';

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
}

export const AdditionalSection: React.FC<Props> = ({ form }) => {
  const { register, formState: { errors, touchedFields } } = form;

  return (
    <div className="bg-slate-50/50 rounded-2xl border-l-4 border-l-[#f37021] border border-gray-100 p-5 sm:p-7 shadow-sm mt-8">
      <h4 className="text-gray-800 font-bold text-base mb-5 border-b border-gray-200 pb-2 uppercase tracking-wide">
        III. Yêu cầu bổ sung
      </h4>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
        {/* Language */}
        <div>
          <label className="block text-base font-bold text-gray-900 mb-2">
            Ngôn ngữ sử dụng <span className="text-red-500">*</span>
          </label>
          <div className="flex items-center gap-8 mt-2 mb-3">
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('language')}
                value="english"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">Tiếng Anh</span>
            </label>
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('language')}
                value="vietnamese"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">Tiếng Việt</span>
            </label>
          </div>
          {errors.language && (
            <p className="text-xs text-red-600 font-medium mb-2">⚠ {errors.language.message}</p>
          )}
          <div className="bg-slate-50 border border-slate-100 p-3 rounded-xl">
            <p className="text-xs text-slate-500 italic leading-relaxed">
              <span className="font-bold text-slate-600 not-italic mr-1">Note:</span>
              Hiện tại FPTU chỉ có thể hỗ trợ bằng Tiếng Anh và Tiếng Việt. Với ngôn ngữ khác, đầu mối gửi request cần chủ động bố trí phiên dịch viên.
            </p>
          </div>
        </div>

        {/* Vehicle */}
        <div>
          <label className="block text-base font-bold text-gray-900 mb-2">
            Nhận diện phương tiện di chuyển tới FPTU
          </label>
          <input
            {...register('vehicle')}
            placeholder="VD: Xe khách 45 chỗ, biển số 29A-XXXXX..."
            className={inputCls(false, !!(touchedFields.vehicle && form.getValues('vehicle')))}
          />
          <div className="text-xs text-slate-500 bg-slate-50 p-3 rounded-xl border border-slate-100 mt-2">
            <ul className="list-none space-y-2 italic">
              <li className="flex gap-2 items-start">
                <span className="text-[#004c91] font-bold not-italic shrink-0">∗</span>
                Các phương tiện cá nhân không được di chuyển trong khuôn viên trường nếu chưa được cho phép.
              </li>
              <li className="flex gap-2 items-start">
                <span className="text-[#004c91] font-bold not-italic shrink-0">∗</span>
                Với đoàn có số lượng từ 6 người trở lên, đầu mối chủ động yêu cầu xe điện từ FSO qua FPTU.
              </li>
            </ul>
          </div>
        </div>
      </div>

      {/* Notes */}
      <div className="mt-8">
        <label className="block text-base font-bold text-gray-900 mb-2">Ghi chú cho FPTU</label>
        <textarea
          {...register('notes')}
          rows={4}
          placeholder="Nhập bất kỳ ghi chú thiết yếu nào..."
          className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none transition-all bg-white text-sm shadow-sm resize-none font-medium text-gray-900"
        />
      </div>
    </div>
  );
};
