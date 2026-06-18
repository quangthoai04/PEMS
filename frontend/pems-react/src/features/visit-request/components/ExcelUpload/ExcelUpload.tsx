import React, { useRef, useState } from 'react';
import { Upload, Download, CheckCircle2, AlertCircle, X, FileSpreadsheet } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { validateExcelFile, isAllowedExcelFile } from './excelValidator';
import type { ExcelValidationResult, VisitorEntry } from '../../types/visitRequest.types';

interface ExcelUploadProps {
  onValidData: (visitors: VisitorEntry[]) => void;
  templateUrl?: string;
}

export const ExcelUpload: React.FC<ExcelUploadProps> = ({ onValidData, templateUrl }) => {
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [result, setResult] = useState<ExcelValidationResult | null>(null);
  const [fileName, setFileName] = useState('');

  const processFile = async (file: File) => {
    if (!isAllowedExcelFile(file)) {
      setResult({
        valid: false,
        totalRows: 0,
        errorRows: 0,
        errors: [{ row: 0, column: '', message: 'Chỉ chấp nhận file .xlsx hoặc .xls' }],
        data: [],
      });
      return;
    }

    setIsProcessing(true);
    setFileName(file.name);
    try {
      const validation = await validateExcelFile(file);
      setResult(validation);
      if (validation.valid && validation.data.length > 0) {
        onValidData(validation.data);
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) processFile(file);
    e.target.value = '';
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files?.[0];
    if (file) processFile(file);
  };

  const reset = () => {
    setResult(null);
    setFileName('');
  };

  return (
    <div className="space-y-3">
      <div
        className={[
          'relative flex flex-col items-center justify-center gap-3 p-6 rounded-xl border-2 border-dashed cursor-pointer transition-all',
          isDragging
            ? 'border-[#f37021] bg-orange-50'
            : 'border-gray-200 hover:border-[#f37021] hover:bg-orange-50/30',
        ].join(' ')}
        onDragOver={(e) => { e.preventDefault(); setIsDragging(true); }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={handleDrop}
        onClick={() => inputRef.current?.click()}
      >
        <input
          ref={inputRef}
          type="file"
          accept=".xlsx,.xls"
          className="hidden"
          onChange={handleFileChange}
        />
        {isProcessing ? (
          <div className="flex items-center gap-2 text-[#004c91]">
            <div className="w-5 h-5 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
            <span className="text-sm font-semibold">Đang xử lý file...</span>
          </div>
        ) : (
          <>
            <div className="w-12 h-12 rounded-full bg-blue-50 flex items-center justify-center">
              <Upload className="w-6 h-6 text-[#004c91]" />
            </div>
            <div className="text-center">
              <p className="text-sm font-bold text-gray-700">
                Kéo thả hoặc <span className="text-[#f37021]">chọn file</span>
              </p>
              <p className="text-xs text-gray-500 mt-0.5">Hỗ trợ .xlsx, .xls</p>
            </div>
          </>
        )}
      </div>

      {templateUrl && (
        <a
          href={templateUrl}
          download
          onClick={(e) => e.stopPropagation()}
          className="inline-flex items-center gap-2 px-4 py-2 bg-white text-slate-700 text-sm font-bold rounded-xl hover:bg-slate-50 transition-colors border border-slate-200 shadow-sm"
        >
          <Download className="w-4 h-4" /> Tải file mẫu
        </a>
      )}

      <AnimatePresence>
        {result && (
          <motion.div
            initial={{ opacity: 0, y: -8 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -8 }}
            className={[
              'rounded-xl border p-4',
              result.valid ? 'bg-green-50 border-green-200' : 'bg-red-50 border-red-200',
            ].join(' ')}
          >
            <div className="flex items-start justify-between gap-2">
              <div className="flex items-center gap-2">
                {result.valid ? (
                  <CheckCircle2 className="w-5 h-5 text-green-600 shrink-0" />
                ) : (
                  <AlertCircle className="w-5 h-5 text-red-500 shrink-0" />
                )}
                <div>
                  <p className={`text-sm font-bold ${result.valid ? 'text-green-700' : 'text-red-700'}`}>
                    {result.valid
                      ? `Thành công: Đã tải ${result.data.length} dòng từ "${fileName}"`
                      : `Lỗi file: "${fileName}"`}
                  </p>
                  {result.totalRows > 0 && (
                    <p className="text-xs text-gray-600 mt-0.5">
                      Tổng: {result.totalRows} dòng · Lỗi: {result.errorRows} dòng
                    </p>
                  )}
                </div>
              </div>
              <button
                type="button"
                onClick={reset}
                className="p-1 text-gray-400 hover:text-gray-600 rounded transition-colors shrink-0"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {result.errors.length > 0 && (
              <div className="mt-3 space-y-1 max-h-40 overflow-y-auto">
                {result.errors.map((err, i) => (
                  <div key={i} className="flex items-start gap-2 text-xs text-red-700 bg-red-100/60 px-2.5 py-1.5 rounded-lg">
                    <FileSpreadsheet className="w-3.5 h-3.5 shrink-0 mt-0.5" />
                    <span>{err.message}</span>
                  </div>
                ))}
              </div>
            )}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
};
