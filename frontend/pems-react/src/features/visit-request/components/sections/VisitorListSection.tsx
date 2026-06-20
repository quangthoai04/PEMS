import React, { useRef, useState } from 'react';
import { Controller, type UseFormReturn, type UseFieldArrayReturn } from 'react-hook-form';
import { Plus, Trash2, Download, Upload, CheckCircle2, AlertCircle, X, FileSpreadsheet } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { CountrySelect } from '../shared/CountrySelect';
import { validateVisitorExcel, isAllowedExcelFile } from '../ExcelUpload/excelValidator';
import { downloadVisitorTemplate } from '../ExcelUpload/excelDownload';
import type { ExcelValidationResult, ExcelValidationError } from '../../types/visitRequest.types';

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
  visitorFields: UseFieldArrayReturn<VisitRequestSchema, 'visitors'>;
}

export const VisitorListSection: React.FC<Props> = ({ form, visitorFields }) => {
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
        valid: false, totalRows: 0, errorRows: 0,
        errors: [{ row: 0, column: '', message: 'Chỉ chấp nhận file .xlsx hoặc .xls' }],
        data: [],
      });
      setIsProcessing(false);
      e.target.value = '';
      return;
    }

    const result = await validateVisitorExcel(file);

    if (result.data.length > 0) {
      // Cross-check emails against existing form entries
      const existingEmails = new Set(
        form.getValues('visitors')
          .map((v) => v.email.toLowerCase().trim())
          .filter(Boolean)
      );

      const crossDupErrors: ExcelValidationError[] = [];
      const toAdd = result.data.filter((visitor) => {
        const key = visitor.email.toLowerCase().trim();
        if (existingEmails.has(key)) {
          crossDupErrors.push({
            row: 0,
            column: 'Email',
            message: `Email "${visitor.email}" đã có trong danh sách hiện tại — bỏ qua.`,
          });
          return false;
        }
        existingEmails.add(key);
        return true;
      });

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      toAdd.forEach((v) => visitorFields.append(v as any));
      setAddedCount(toAdd.length);

      setUploadResult({
        ...result,
        errors: [...result.errors, ...crossDupErrors],
        errorRows: result.errorRows + crossDupErrors.length,
        valid: result.errors.length === 0 && crossDupErrors.length === 0,
      });
    } else {
      setUploadResult(result);
    }

    setIsProcessing(false);
    e.target.value = '';
  };

  return (
    <div className="bg-blue-50/20 rounded-2xl border-l-4 border-l-[#004c91] border border-gray-100 p-5 sm:p-7 shadow-sm mt-8">
      <h4 className="text-[#004c91] font-bold text-base mb-5 border-b border-blue-100 pb-2 uppercase tracking-wide">
        II. Thành phần tham dự & Liên hệ
      </h4>

      {/* Visitor list */}
      <div className="mb-8">
        <label className="block text-base font-bold text-gray-900 mb-3">
          Danh sách khách <span className="text-red-500">*</span>
        </label>

        <div className="bg-white border border-gray-200 rounded-xl overflow-x-auto shadow-sm">
          <table className="w-full min-w-[680px] border-collapse text-sm">
            <thead className="bg-slate-50 border-b border-gray-200">
              <tr>
                <th className="p-3 text-center font-bold text-slate-700 w-12">STT</th>
                <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Họ và tên *</th>
                <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Email *</th>
                <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Quốc tịch *</th>
                <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Chức vụ</th>
                <th className="p-3 text-center w-12 border-l border-gray-200" />
              </tr>
            </thead>
            <tbody>
              <AnimatePresence>
                {visitorFields.fields.map((field, i) => {
                  const fe = visitorErrors?.[i];
                  const rowHasError = !!(fe?.fullName || fe?.email || fe?.nationality);
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
                          {...register(`visitors.${i}.email`)}
                          type="email"
                          placeholder="email@domain.com"
                          className={cellInputCls(!!fe?.email)}
                        />
                        {fe?.email && <CellError msg={fe.email.message} />}
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

                      <td className="p-0 border-l border-gray-100">
                        <input
                          {...register(`visitors.${i}.jobTitle`)}
                          placeholder="Chức vụ..."
                          className={cellInputCls(false)}
                        />
                      </td>

                      <td className="p-2 border-l border-gray-100 text-center">
                        <button
                          type="button"
                          disabled={visitorFields.fields.length === 1}
                          onClick={() => visitorFields.remove(i)}
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

        {typeof visitorErrors === 'object' && !Array.isArray(visitorErrors) && 'message' in (visitorErrors as any) && (
          <p className="mt-2 text-xs text-red-600 font-medium">⚠ {(visitorErrors as any).message}</p>
        )}

        <div className="flex flex-wrap items-center justify-between gap-3 mt-4">
          <button
            type="button"
            onClick={() =>
              visitorFields.append({ fullName: '', jobTitle: '', organization: '', nationality: '', email: '' })
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
                        Tổng {uploadResult.totalRows} dòng · {uploadResult.errorRows} dòng lỗi/bỏ qua
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
    'w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300 text-sm',
    hasError ? 'text-red-700' : 'text-gray-900',
  ].join(' ');

const CellError: React.FC<{ msg?: string }> = ({ msg }) =>
  msg ? <p className="px-3 pb-1 text-[10px] text-red-600 font-medium">{msg}</p> : null;
