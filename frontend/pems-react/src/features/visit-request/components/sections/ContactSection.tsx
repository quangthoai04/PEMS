import React, { useEffect, useRef, useState } from 'react';
import { Controller, type UseFormReturn, type UseFieldArrayReturn } from 'react-hook-form';
import { Plus, Trash2, Download, Upload, CheckCircle2, AlertCircle, X, FileSpreadsheet } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { CountrySelect } from '../shared/CountrySelect';
import { PhoneInput } from '../shared/PhoneInput';
import { inputCls } from '../shared/FormField';
import { OrganizationSelect } from '../shared/OrganizationSelect';
import { validateSupportTeamExcel, isAllowedExcelFile } from '../ExcelUpload/excelValidator';
import { downloadSupportTeamTemplate } from '../ExcelUpload/excelDownload';
import type { SupportTeamExcelValidationResult } from '../../types/visitRequest.types';

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
  supportTeamFields: UseFieldArrayReturn<VisitRequestSchema, 'supportTeam'>;
  onSyncSupportFromRegister: () => void;
  onClearSupportFirstRow: () => void;
  onSyncContactFromRegister: () => void;
  onClearContactPoint: () => void;
}

export const ContactSection: React.FC<Props> = ({
  form,
  supportTeamFields,
  onSyncSupportFromRegister,
  onClearSupportFirstRow,
  onSyncContactFromRegister,
  onClearContactPoint,
}) => {
  const { register, control, formState: { errors } } = form;
  const [isSupportSameAsRegister, setIsSupportSameAsRegister] = useState(false);
  const [isContactSameAsRegister, setIsContactSameAsRegister] = useState(false);

  const supportErrors = errors.supportTeam;
  const contactErrors = errors.contactPoint;

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
        valid: false, totalRows: 0, errorRows: 0,
        errors: [{ row: 0, column: '', message: 'Chỉ chấp nhận file .xlsx hoặc .xls' }],
        data: [],
      });
      setIsProcessing(false);
      e.target.value = '';
      return;
    }

    const result = await validateSupportTeamExcel(file);

    if (result.data.length > 0) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      result.data.forEach((member) => supportTeamFields.append(member as any));
      setAddedCount(result.data.length);
    }

    setUploadResult(result);
    setIsProcessing(false);
    e.target.value = '';
  };

  return (
    <div className="space-y-8 mt-8">
      {/* ── Support team ─────────────────────────────────────────────────────── */}
      <div>
        <div className="flex items-center justify-between mb-3 flex-wrap gap-2">
          <label className="block text-base font-bold text-gray-900">
            Danh sách team hỗ trợ khách <span className="text-red-500">*</span>
          </label>
          <label className="flex items-center gap-2.5 cursor-pointer text-sm text-[#004c91] font-bold select-none bg-blue-50/50 px-3 py-1.5 rounded-lg border border-blue-100 hover:bg-blue-50 transition-colors">
            <input
              type="checkbox"
              checked={isSupportSameAsRegister}
              onChange={(e) => handleSupportCheckbox(e.target.checked)}
              className="w-4 h-4 rounded text-[#004c91] focus:ring-[#004c91] border-blue-300 cursor-pointer"
            />
            Tôi là người hỗ trợ khách
          </label>
        </div>

        <div className="bg-white border border-gray-200 rounded-xl overflow-x-auto shadow-sm">
          <table className="w-full min-w-[750px] border-collapse text-sm">
            <thead className="bg-slate-50 border-b border-gray-200">
              <tr>
                <th className="p-3 text-center font-bold text-slate-700 w-12">STT</th>
                <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Họ và tên *</th>
                <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Chức vụ *</th>
                <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Đơn vị công tác *</th>
                <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Quốc tịch *</th>
                <th className="p-3 text-center w-12 border-l border-gray-200" />
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

                      <td className="p-0 border-l border-gray-100">
                        <input
                          {...register(`supportTeam.${i}.fullName`)}
                          placeholder="Nhập tên..."
                          className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300 text-sm"
                        />
                        {se?.fullName && <p className="px-3 pb-1 text-[10px] text-red-600">{se.fullName.message}</p>}
                      </td>

                      <td className="p-0 border-l border-gray-100">
                        <input
                          {...register(`supportTeam.${i}.jobTitle`)}
                          placeholder="Chức vụ..."
                          className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300 text-sm"
                        />
                        {se?.jobTitle && <p className="px-3 pb-1 text-[10px] text-red-600">{se.jobTitle.message}</p>}
                      </td>

                      <td className="p-0 border-l border-gray-100">
                        <input
                          {...register(`supportTeam.${i}.organization`)}
                          placeholder="Đơn vị..."
                          className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300 text-sm"
                        />
                        {se?.organization && <p className="px-3 pb-1 text-[10px] text-red-600">{se.organization.message}</p>}
                      </td>

                      <td className="p-1 border-l border-gray-100 min-w-[160px]">
                        <Controller
                          name={`supportTeam.${i}.nationality`}
                          control={control}
                          render={({ field }) => (
                            <CountrySelect
                              value={field.value}
                              onChange={field.onChange}
                              onBlur={field.onBlur}
                              hasError={!!se?.nationality}
                              placeholder="Quốc tịch..."
                            />
                          )}
                        />
                        {se?.nationality && <p className="px-1 pb-1 text-[10px] text-red-600">{se.nationality.message}</p>}
                      </td>

                      <td className="p-2 border-l border-gray-100 text-center">
                        <button
                          type="button"
                          disabled={supportTeamFields.fields.length === 1}
                          onClick={() => supportTeamFields.remove(i)}
                          className="p-2 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors disabled:opacity-30"
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

        <div className="flex flex-wrap items-center justify-between gap-3 mt-4">
          <button
            type="button"
            onClick={() =>
              supportTeamFields.append({ fullName: '', jobTitle: '', organization: '', nationality: '' })
            }
            className="inline-flex items-center gap-2 px-4 py-2 bg-[#f37021]/10 text-[#f37021] text-sm font-bold rounded-xl hover:bg-[#f37021]/20 transition-colors"
          >
            <Plus className="w-4 h-4" /> Thêm nhân sự
          </button>
          <div className="flex flex-wrap gap-2 sm:gap-3">
            <button
              type="button"
              onClick={downloadSupportTeamTemplate}
              className="inline-flex items-center gap-2 px-4 py-2 bg-white text-slate-700 text-sm font-bold rounded-xl hover:bg-slate-50 transition-colors border border-slate-200 shadow-sm"
            >
              <Download className="w-4 h-4" /> Tải mẫu
            </button>
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => fileInputRef.current?.click()}
              className="inline-flex items-center gap-2 px-4 py-2 bg-white text-[#004c91] text-sm font-bold rounded-xl hover:bg-blue-50 transition-colors border border-slate-200 shadow-sm disabled:opacity-60"
            >
              {isProcessing
                ? <span className="w-4 h-4 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
                : <Upload className="w-4 h-4" />}
              Up danh sách
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
                        ? `Đã thêm ${addedCount} nhân sự từ "${uploadFileName}". Tổng: ${supportTeamFields.fields.length} người.`
                        : `Không thêm được dữ liệu từ "${uploadFileName}"`}
                    </p>
                    {uploadResult.totalRows > 0 && (
                      <p className="text-[10px] text-gray-600 mt-0.5">
                        Tổng {uploadResult.totalRows} dòng · {uploadResult.errorRows} dòng lỗi
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
      </div>

      {/* ── Contact point ─────────────────────────────────────────────────────── */}
      <div>
        <div className="flex items-center justify-between mb-3 flex-wrap gap-2">
          <label className="block text-base font-bold text-gray-900">
            Thông tin đầu mối liên hệ <span className="text-red-500">*</span>
          </label>
          <label className="flex items-center gap-2.5 cursor-pointer text-sm text-[#004c91] font-bold select-none bg-blue-50/80 px-3 py-1.5 rounded-lg border border-blue-200 hover:bg-blue-100 transition-colors">
            <input
              type="checkbox"
              checked={isContactSameAsRegister}
              onChange={(e) => handleContactCheckbox(e.target.checked)}
              className="w-4 h-4 rounded text-[#004c91] focus:ring-[#004c91] border-blue-300 cursor-pointer"
            />
            Tôi cũng là đầu mối liên hệ
          </label>
        </div>

        <p className="text-xs text-slate-500 mb-2 -mt-1">
          Khi chọn “Tôi cũng là đầu mối liên hệ”, hệ thống sẽ tự điền Thông tin đầu mối liên hệ từ Thông tin người đăng ký form.
        </p>

        <div className="mb-3 rounded-lg bg-blue-50/60 border border-blue-100 px-3 py-2">
          <p className="text-xs text-[#004c91] leading-5">
            Thông tin đầu mối liên hệ sẽ được FPTU sử dụng để trao đổi về yêu cầu tham quan. Email đầu mối liên hệ
            cũng là email dùng để tạo tài khoản VISITOR và đăng nhập Google lần sau để theo dõi yêu cầu.
          </p>
        </div>

        <div className="bg-white border border-gray-200 rounded-xl overflow-x-auto shadow-sm">
          <table className="w-full min-w-[700px] border-collapse text-sm">
            <thead className="bg-[#004c91]/5 border-b border-gray-200">
              <tr>
                <th className="p-3 text-left font-bold text-[#004c91]">Họ và tên *</th>
                <th className="p-3 text-left font-bold text-[#004c91] border-l border-gray-200">Đơn vị công tác *</th>
                <th className="p-3 text-left font-bold text-[#004c91] border-l border-gray-200">Số điện thoại *</th>
                <th className="p-3 text-left font-bold text-[#004c91] border-l border-gray-200">Email *</th>
              </tr>
            </thead>
            <tbody>
              <tr className="hover:bg-orange-50/40 focus-within:bg-orange-50/30 transition-colors">
                <td className="p-0">
                  <input
                    {...register('contactPoint.fullName')}
                    placeholder="Nhập tên..."
                    className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300 text-sm"
                  />
                  {contactErrors?.fullName && (
                    <p className="px-3 pb-1 text-[10px] text-red-600">{contactErrors.fullName.message}</p>
                  )}
                </td>
                <td className="p-0 border-l border-gray-100">
                  <Controller
                    name="contactPoint.organization"
                    control={control}
                    render={({ field }) => (
                      <div className="p-1">
                        <OrganizationSelect
                          value={field.value}
                          onChange={field.onChange}
                          onBlur={field.onBlur}
                          hasError={!!contactErrors?.organization}
                          placeholder="Nhập đơn vị..."
                        />
                      </div>
                    )}
                  />
                  {contactErrors?.organization && (
                    <p className="px-3 pb-1 text-[10px] text-red-600">{contactErrors.organization.message}</p>
                  )}
                </td>
                <td className="p-1 border-l border-gray-100">
                  <Controller
                    name="contactPoint.phone"
                    control={control}
                    render={({ field }) => (
                      <PhoneInput
                        value={field.value}
                        onChange={field.onChange}
                        onBlur={field.onBlur}
                        hasError={!!contactErrors?.phone}
                      />
                    )}
                  />
                  {contactErrors?.phone && (
                    <p className="px-1 pb-1 text-[10px] text-red-600">{contactErrors.phone.message}</p>
                  )}
                </td>
                <td className="p-0 border-l border-gray-100">
                  <input
                    {...register('contactPoint.email')}
                    type="email"
                    placeholder="email@domain.com"
                    className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300 text-sm"
                  />
                  {contactErrors?.email && (
                    <p className="px-3 pb-1 text-[10px] text-red-600">{contactErrors.email.message}</p>
                  )}
                </td>
              </tr>
            </tbody>
          </table>
        </div>


      </div>
    </div>
  );
};
