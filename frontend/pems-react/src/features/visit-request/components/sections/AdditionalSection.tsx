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
            Ngôn ngữ làm việc <span className="text-red-500">*</span>
          </label>
          <div className="flex items-center gap-8 mt-2 mb-3">
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('workingLanguage')}
                value="EN"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">Tiếng Anh</span>
            </label>
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('workingLanguage')}
                value="VI"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">Tiếng Việt</span>
            </label>
          </div>
          {errors.workingLanguage && (
            <p className="text-xs text-red-600 font-medium mb-2">⚠ {errors.workingLanguage.message}</p>
          )}
          <p className="text-xs text-gray-500 italic mt-1">
            PEMS hiện chỉ hỗ trợ làm việc bằng Tiếng Việt hoặc Tiếng Anh. Nếu đoàn cần sử dụng ngôn ngữ khác, vui lòng tự chuẩn bị phiên dịch.
          </p>

        </div>

        {/* Transportation — free text (campus-independent approval spec: no type enum anymore) */}
        <div>
          <label className="block text-base font-bold text-gray-900 mb-2">
            Nhận diện phương tiện di chuyển tới FPTU
          </label>
          <textarea
            {...register('transportationNote')}
            rows={4}
            placeholder="Ví dụ: Xe 16 chỗ màu trắng, biển số 30A-xxxxx, dự kiến tới cổng lúc 8:30."
            className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none transition-all bg-white text-sm shadow-sm resize-none font-medium text-gray-900"
          />
          {errors.transportationNote && (
            <p className="text-xs text-red-600 font-medium mt-1">⚠ {errors.transportationNote.message}</p>
          )}
          <p className="text-xs text-gray-500 italic mt-1">
            Không bắt buộc — mô tả tự do giúp bảo vệ/lễ tân nhận diện đoàn khi tới cổng.
          </p>
        </div>

        {/* Media Consent */}
        <div className="md:col-span-2 border-t border-gray-200 pt-6 mt-2">
          <label className="block text-base font-bold text-gray-900 mb-2">
            Chấp thuận truyền thông/Chụp ảnh <span className="text-red-500">*</span>
          </label>
          <div className="flex items-center gap-8 mt-2 mb-3">
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('mediaConsentStatus')}
                value="AGREED"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">Đồng ý</span>
            </label>
            <label className="flex items-center gap-2.5 cursor-pointer group">
              <input
                type="radio"
                {...register('mediaConsentStatus')}
                value="DECLINED"
                className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer"
              />
              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">Từ chối</span>
            </label>

          </div>
          {errors.mediaConsentStatus && (
            <p className="text-xs text-red-600 font-medium mb-2">⚠ {errors.mediaConsentStatus.message}</p>
          )}

          <label className="block text-sm font-bold text-gray-900 mt-4 mb-2">
            Ghi chú truyền thông (Nếu có)
          </label>
          <input
            {...register('mediaConsentNote')}
            placeholder="VD: Vui lòng không chụp cận mặt học sinh..."
            className={inputCls(false, !!(touchedFields.mediaConsentNote && form.getValues('mediaConsentNote')))}
          />
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
