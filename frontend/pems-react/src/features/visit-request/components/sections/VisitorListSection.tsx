import React, { useRef, useState } from 'react';
import { Controller, type UseFormReturn, type UseFieldArrayReturn } from 'react-hook-form';
import { Plus, Trash2, Download, Upload, CheckCircle2, AlertCircle, X, FileSpreadsheet } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { CountrySelect } from '../shared/CountrySelect';
import { OrganizationCombobox } from '../shared/OrganizationCombobox';
import { validateVisitorExcel, isAllowedExcelFile } from '../ExcelUpload/excelValidator';
import { downloadVisitorTemplate } from '../ExcelUpload/excelDownload';
import type { ExcelValidationResult, ExcelValidationError, VisitorEntry } from '../../types/visitRequest.types';

function hasRealError(error: unknown): boolean {
  if (!error) return false;
  if (Array.isArray(error)) return error.some(hasRealError);
  if (typeof error === 'object') {
    return Object.values(error as Record<string, unknown>).some(hasRealError);
  }
  return true;
}

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
  visitorFields: UseFieldArrayReturn<VisitRequestSchema, 'visitors'>;
  showErrors?: boolean;
}

export const VisitorListSection: React.FC<Props> = ({ form, visitorFields, showErrors }) => {
  const { register, control, formState: { errors } } = form;
  const visitorErrors = errors.visitors;

  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [uploadResult, setUploadResult] = useState<ExcelValidationResult | null>(null);
  const [uploadFileName, setUploadFileName] = useState('');
  const [addedCount, setAddedCount] = useState(0);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploadFileName(file.name);
    setIsProcessing(true);
    setUploadResult(null);
    setAddedCount(0);

    if (!isAllowedExcelFile(file)) {
      setUploadResult({
        valid: false, totalRows: 0, errorRows: 0, skippedDuplicates: 0,
        errors: [{ row: 0, column: '', message: 'Chỉ chấp nhận file .xlsx hoặc .xls' }],
        data: [],
      });
      setIsProcessing(false);
      e.target.value = '';
      return;
    }

    const existingData = visitorFields.fields.map(f => ({
      fullName: f.fullName,
      jobTitle: f.jobTitle,
      organization: f.organization,
      nationality: f.nationality
    }));
    const result = await validateVisitorExcel(file, existingData as VisitorEntry[]);

    if (result.data.length > 0) {
      // Email checking removed
      const toAdd = result.data;

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      toAdd.forEach((v) => visitorFields.append(v as any));
      setAddedCount(toAdd.length);

      setUploadResult({
        ...result,
        errors: [...result.errors],
        errorRows: result.errorRows,
        valid: result.errors.length === 0,
      });
    } else {
      setUploadResult(result);
    }

    setIsProcessing(false);
    e.target.value = '';
  };

  const rootErrorMessage = (visitorErrors as any)?.root?.message || (typeof visitorErrors === 'object' && !Array.isArray(visitorErrors) ? (visitorErrors as any).message : null);
  const hasRootError = !!rootErrorMessage;
  const hasAnyError = showErrors && (hasRootError || hasRealError(visitorErrors));

  return (
    <div className={`rounded-2xl border bg-white shadow-sm mt-8 transition-colors ${hasAnyError ? 'border-red-300 shadow-red-500/10' : 'border-slate-200'}`}>
      <div className={`border-b px-6 py-4 flex items-center justify-between ${hasAnyError ? 'border-red-200 bg-red-50/50 rounded-t-2xl' : 'border-slate-200'}`}>
        <h4 className="text-[#004c91] font-bold text-lg">Danh sách khách <span className="text-red-500">*</span></h4>
      </div>

      {hasAnyError && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-3 mx-6 mt-4 flex items-start gap-2 error-scroll-target">
          <AlertCircle className="w-5 h-5 text-red-500 shrink-0" />
          <div>
            <p className="text-sm font-bold text-red-700">Vui lòng kiểm tra lại thông tin trong phần này.</p>
            {rootErrorMessage && <p className="text-xs text-red-600 mt-0.5">{rootErrorMessage}</p>}
          </div>
        </div>
      )}

      <div className="p-6">
        <div className="bg-white border border-gray-200 rounded-xl overflow-x-auto shadow-sm">
          <table className="w-full min-w-[680px] border-collapse text-sm">
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
                {visitorFields.fields.map((field, i) => {
                  const fe = visitorErrors?.[i];
                  const rowHasError = !!(fe?.fullName || fe?.nationality || fe?.organization || fe?.jobTitle);
                  return (
                    <motion.tr
                      key={field.id}
                      initial={{ opacity: 0, y: -6 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, height: 0 }}
                      className={[
                        'border-b border-gray-100 last:border-b-0 transition-colors',
                        rowHasError ? 'bg-red-50/40' : 'hover:bg-orange-50/40 focus-within:bg-orange-50/30',
                      ].join(' ')}
                    >
                      <td className="p-3 text-center font-bold text-slate-400 text-sm">{i + 1}</td>

                      <td className="p-0 border-l border-gray-100">
                        <input
                          {...register(`visitors.${i}.fullName`)}
                          placeholder="Nhập tên..."
                          className={cellInputCls(!!fe?.fullName)}
                        />
                        {fe?.fullName && <CellError msg={fe.fullName.message} />}
                      </td>

                      <td className="p-0 border-l border-gray-100">
                        <input
                          {...register(`visitors.${i}.jobTitle`)}
                          placeholder="Chức vụ..."
                          className={cellInputCls(!!fe?.jobTitle)}
                        />
                        {fe?.jobTitle && <CellError msg={fe.jobTitle.message} />}
                      </td>

                      <td className="p-1 border-l border-gray-100 min-w-[200px]">
                        <Controller
                          name={`visitors.${i}.organization`}
                          control={control}
                          render={({ field }) => (
                            <OrganizationCombobox
                              value={field.value}
                              onChange={field.onChange}
                              onBlur={field.onBlur}
                              hasError={!!fe?.organization}
                              placeholder="Đơn vị công tác..."
                            />
                          )}
                        />
                        {fe?.organization && <CellError msg={fe.organization.message} />}
                      </td>

                      <td className="p-1 border-l border-gray-100 min-w-[160px]">
                        <Controller
                          name={`visitors.${i}.nationality`}
                          control={control}
                          render={({ field }) => (
                            <CountrySelect
                              value={field.value}
                              onChange={field.onChange}
                              onBlur={field.onBlur}
                              hasError={!!fe?.nationality}
                              placeholder="Quốc tịch..."
                            />
                          )}
                        />
                        {fe?.nationality && <CellError msg={fe.nationality.message} />}
                      </td>

                      <td className="p-2 border-l border-gray-100 text-center">
                        <button
                          type="button"
                          onClick={() => visitorFields.remove(i)}
                          className="p-2 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors"
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
              visitorFields.append({ fullName: '', jobTitle: '', organization: '', nationality: '' })
            }
            className="inline-flex items-center gap-2 px-4 py-2 bg-[#f37021]/10 text-[#f37021] text-sm font-bold rounded-xl hover:bg-[#f37021]/20 transition-colors"
          >
            <Plus className="w-4 h-4" /> Thêm khách
          </button>

          <div className="flex flex-wrap gap-2 sm:gap-3">
            <button
              type="button"
              onClick={downloadVisitorTemplate}
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
                        ? `Đã thêm ${addedCount} khách từ "${uploadFileName}". Tổng: ${visitorFields.fields.length} người.`
                        : `Không thêm được dữ liệu từ "${uploadFileName}"`}
                    </p>
                    {uploadResult.totalRows > 0 && (
                      <p className="text-[10px] text-gray-600 mt-0.5">
                        Tổng {uploadResult.totalRows} dòng · {uploadResult.errorRows} dòng lỗi/bỏ qua{uploadResult.skippedDuplicates > 0 ? ` · ${uploadResult.skippedDuplicates} dòng trùng đã bỏ qua` : ''}.
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
    </div>
  );
};

const cellInputCls = (hasError: boolean) =>
  [
    'w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300 text-sm border focus:ring-1 focus:outline-none transition-colors',
    hasError ? 'text-red-700 border-red-300 bg-red-50/20 focus:border-red-400 focus:ring-red-300' : 'text-gray-900 border-transparent focus:border-blue-200 focus:ring-blue-200',
  ].join(' ');

const CellError: React.FC<{ msg?: string }> = ({ msg }) =>
  msg ? <p className="px-3 pb-1 text-[10px] text-red-600 font-medium">{msg}</p> : null;
