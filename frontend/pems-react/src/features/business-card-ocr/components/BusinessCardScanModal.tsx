/**
 * BusinessCardScanModal — 4-step business-card OCR flow (02 prompt §11):
 *   1 Upload → 2 Processing (Google Document AI qua backend) → 3 Review/sửa + chọn đối tác → 4 Kết quả.
 * Contact chỉ được tạo khi user bấm "Lưu người liên hệ" (confirm) — không bao giờ tự động.
 */
import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  X, UploadCloud, Loader2, CheckCircle2, AlertTriangle, ScanLine, Search,
} from 'lucide-react';
import { businessCardOcrApi } from '../api/businessCardOcrApi';
import type {
  BusinessCardOcrJob,
  ScanBusinessCardContext,
} from '../types/businessCardOcr.types';
import { partnersApi } from '../../partners/api/partnersApi';
import type { PartnerListItem } from '../../partners/types/partners.types';
import { PROFILE_STATUS_LABELS } from '../../partners/types/partners.types';

type Step = 'UPLOAD' | 'PROCESSING' | 'REVIEW' | 'RESULT';

interface Props {
  open: boolean;
  onClose: () => void;
  /** Preselected context: partner detail (partnerId) or meeting row (visitInstanceId + guest). */
  context?: ScanBusinessCardContext;
  /** Called after a contact was confirmed so the caller can refresh its lists/badges. */
  onConfirmed?: (result: { partnerId: number; contactId: number }) => void;
}

const ACCEPTED = ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'];

export function BusinessCardScanModal({ open, onClose, context, onConfirmed }: Props) {
  const [step, setStep] = useState<Step>('UPLOAD');
  const [file, setFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [job, setJob] = useState<BusinessCardOcrJob | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Review form state
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [jobTitle, setJobTitle] = useState('');
  const [departmentName, setDepartmentName] = useState('');
  const [organization, setOrganization] = useState('');
  const [isPrimary, setIsPrimary] = useState(false);

  // Partner selector
  const [partnerId, setPartnerId] = useState<number | null>(context?.partnerId ?? null);
  const [partnerName, setPartnerName] = useState<string>('');
  const [partnerSearch, setPartnerSearch] = useState('');
  const [partnerOptions, setPartnerOptions] = useState<PartnerListItem[]>([]);
  const [searching, setSearching] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [confirmedContactId, setConfirmedContactId] = useState<number | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  const reset = () => {
    setStep('UPLOAD');
    setFile(null);
    setPreviewUrl(null);
    setJob(null);
    setError(null);
    setFullName(''); setEmail(''); setPhone(''); setJobTitle('');
    setDepartmentName(''); setOrganization('');
    setIsPrimary(false);
    setPartnerId(context?.partnerId ?? null);
    setPartnerName('');
    setPartnerSearch('');
    setPartnerOptions([]);
    setConfirmedContactId(null);
  };

  useEffect(() => {
    if (open) reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => {
    if (!file) { setPreviewUrl(null); return; }
    if (file.type.startsWith('image/')) {
      const url = URL.createObjectURL(file);
      setPreviewUrl(url);
      return () => URL.revokeObjectURL(url);
    }
    setPreviewUrl(null);
  }, [file]);

  // Debounced partner search for the selector.
  useEffect(() => {
    if (step !== 'REVIEW') return;
    const handle = setTimeout(async () => {
      if (!partnerSearch.trim()) { setPartnerOptions([]); return; }
      try {
        setSearching(true);
        const res = await partnersApi.getPartners({ search: partnerSearch.trim(), page: 1, pageSize: 8 });
        setPartnerOptions(res.items);
      } catch {
        setPartnerOptions([]);
      } finally {
        setSearching(false);
      }
    }, 350);
    return () => clearTimeout(handle);
  }, [partnerSearch, step]);

  const pickFile = (candidate: File | undefined | null) => {
    setError(null);
    if (!candidate) return;
    if (!ACCEPTED.includes(candidate.type)) {
      setError('Chỉ hỗ trợ JPEG/PNG/WebP/PDF.');
      return;
    }
    if (candidate.size > 10 * 1024 * 1024) {
      setError('Tệp vượt quá 10 MB.');
      return;
    }
    setFile(candidate);
  };

  const startScan = async () => {
    if (!file) return;
    setStep('PROCESSING');
    setError(null);
    try {
      const result = await businessCardOcrApi.scan(file, context);
      setJob(result);
      if (result.status === 'FAILED') {
        setError(result.errorMessage || 'Quét danh thiếp thất bại.');
        setStep('UPLOAD');
        return;
      }
      // Prefill the review form from the OCR draft.
      setFullName(result.parsed?.fullName ?? '');
      setEmail(result.parsed?.email ?? '');
      setPhone(result.parsed?.phone ?? '');
      setJobTitle(result.parsed?.jobTitle ?? '');
      setDepartmentName(result.parsed?.departmentName ?? '');
      setOrganization(result.parsed?.organization ?? '');
      if (!context?.partnerId && result.matchedPartner) {
        setPartnerId(result.matchedPartner.partnerId);
        setPartnerName(result.matchedPartner.partnerName);
      }
      setStep('REVIEW');
    } catch (e: any) {
      const message = e?.response?.data?.message || 'Quét danh thiếp thất bại. Vui lòng thử lại.';
      setError(message);
      setStep('UPLOAD');
    }
  };

  const confirm = async () => {
    if (!job || !partnerId || !fullName.trim()) return;
    setConfirming(true);
    setError(null);
    try {
      const result = await businessCardOcrApi.confirmContact(job.ocrJobId, {
        partnerId,
        fullName: fullName.trim(),
        email: email.trim() || null,
        phone: phone.trim() || null,
        jobTitle: jobTitle.trim() || null,
        departmentName: departmentName.trim() || null,
        isPrimary,
        visitInstanceId: context?.visitInstanceId ?? null,
        guestMemberId: context?.guestMemberId ?? null,
        minuteParticipantId: context?.minuteParticipantId ?? null,
      });
      setConfirmedContactId(result.contactId);
      setStep('RESULT');
      onConfirmed?.({ partnerId: result.partnerId, contactId: result.contactId });
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Lưu người liên hệ thất bại.');
    } finally {
      setConfirming(false);
    }
  };

  const discardAndClose = async () => {
    if (job && (step === 'REVIEW')) {
      try { await businessCardOcrApi.discard(job.ocrJobId); } catch { /* best effort */ }
    }
    onClose();
  };

  const inputCls =
    'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white';

  const confidenceBadge = useMemo(() => {
    const value = job?.confidenceScore ?? null;
    if (value == null) return null;
    const color = value >= 90 ? 'bg-green-50 text-green-600'
      : value >= 70 ? 'bg-yellow-50 text-yellow-600'
        : 'bg-red-50 text-red-500';
    return (
      <span className={`px-2.5 py-1 rounded-md text-xs font-bold ${color}`}>
        Độ tin cậy: {value.toFixed(0)}%
      </span>
    );
  }, [job?.confidenceScore]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-3xl max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <div className="flex items-center gap-2">
            <ScanLine className="w-5 h-5 text-[#004c91]" />
            <h3 className="text-lg font-bold text-[#004c91]">Quét danh thiếp</h3>
            <span className="text-xs text-gray-400 font-medium ml-2">
              Provider: Google Document AI
            </span>
          </div>
          <button
            onClick={step === 'PROCESSING' ? undefined : discardAndClose}
            disabled={step === 'PROCESSING'}
            className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 hover:text-gray-600 disabled:opacity-40 cursor-pointer"
            title={step === 'PROCESSING' ? 'Đang xử lý — vui lòng chờ' : 'Đóng'}
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6">
          {error && (
            <div className="mb-4 flex items-start gap-2 bg-red-50 border border-red-100 text-red-600 text-sm rounded-lg px-3 py-2.5">
              <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {/* Step 1 — Upload */}
          {step === 'UPLOAD' && (
            <div>
              <div
                onClick={() => fileInputRef.current?.click()}
                onDragOver={(e) => e.preventDefault()}
                onDrop={(e) => { e.preventDefault(); pickFile(e.dataTransfer.files?.[0]); }}
                className="border-2 border-dashed border-gray-300 hover:border-[#004c91] rounded-2xl p-10 text-center cursor-pointer transition-colors"
              >
                <UploadCloud className="w-10 h-10 text-gray-300 mx-auto mb-3" />
                <p className="text-sm font-medium text-gray-600">
                  Kéo thả hoặc bấm để chọn ảnh danh thiếp
                </p>
                <p className="text-xs text-gray-400 mt-1">JPEG, PNG, WebP, PDF — tối đa 10 MB</p>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept={ACCEPTED.join(',')}
                  className="hidden"
                  onChange={(e) => pickFile(e.target.files?.[0])}
                />
              </div>

              {file && (
                <div className="mt-4 flex items-center gap-4 bg-slate-50 rounded-xl p-3 border border-gray-100">
                  {previewUrl ? (
                    <img src={previewUrl} alt="preview" className="w-24 h-16 object-cover rounded-lg border border-gray-200" />
                  ) : (
                    <div className="w-24 h-16 flex items-center justify-center bg-white rounded-lg border border-gray-200 text-xs text-gray-400">
                      PDF
                    </div>
                  )}
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-bold text-gray-700 truncate">{file.name}</p>
                    <p className="text-xs text-gray-400">{(file.size / 1024).toFixed(0)} KB</p>
                  </div>
                  <button
                    onClick={startScan}
                    className="bg-[#f37021] hover:bg-[#d9621a] text-white px-5 py-2 rounded-lg text-sm font-bold transition-colors cursor-pointer"
                  >
                    Quét danh thiếp
                  </button>
                </div>
              )}
            </div>
          )}

          {/* Step 2 — Processing */}
          {step === 'PROCESSING' && (
            <div className="py-16 text-center">
              <Loader2 className="w-10 h-10 text-[#004c91] mx-auto animate-spin mb-4" />
              <p className="text-sm font-medium text-gray-600">
                Đang xử lý bằng Google Document AI...
              </p>
              <p className="text-xs text-gray-400 mt-1">Vui lòng không đóng cửa sổ này.</p>
            </div>
          )}

          {/* Step 3 — Review */}
          {step === 'REVIEW' && job && (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {/* Left: preview */}
              <div>
                <div className="rounded-xl border border-gray-200 bg-slate-50 p-3 flex items-center justify-center min-h-[180px]">
                  {previewUrl ? (
                    <img src={previewUrl} alt="card" className="max-h-64 rounded-lg object-contain" />
                  ) : (
                    <span className="text-sm text-gray-400">Không có bản xem trước</span>
                  )}
                </div>
                <div className="mt-3 flex items-center gap-2">{confidenceBadge}</div>
                {job.matchedPartner && !context?.partnerId && (
                  <div className="mt-3 bg-blue-50 border border-blue-100 rounded-lg px-3 py-2.5 text-sm">
                    <p className="font-bold text-[#004c91]">
                      Gợi ý đối tác: {job.matchedPartner.partnerName}
                    </p>
                    <p className="text-xs text-gray-500 mt-0.5">
                      {PROFILE_STATUS_LABELS[job.matchedPartner.profileStatus as keyof typeof PROFILE_STATUS_LABELS] ?? job.matchedPartner.profileStatus}
                      {' · '}{job.matchedPartner.reason}
                    </p>
                  </div>
                )}
              </div>

              {/* Right: editable fields */}
              <div className="space-y-3">
                <div>
                  <label className="text-xs font-bold text-gray-500 uppercase">Họ tên *</label>
                  <input className={inputCls} value={fullName} onChange={(e) => setFullName(e.target.value)} />
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs font-bold text-gray-500 uppercase">Email</label>
                    <input className={inputCls} value={email} onChange={(e) => setEmail(e.target.value)} />
                  </div>
                  <div>
                    <label className="text-xs font-bold text-gray-500 uppercase">Số điện thoại</label>
                    <input className={inputCls} value={phone} onChange={(e) => setPhone(e.target.value)} />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs font-bold text-gray-500 uppercase">Chức danh</label>
                    <input className={inputCls} value={jobTitle} onChange={(e) => setJobTitle(e.target.value)} />
                  </div>
                  <div>
                    <label className="text-xs font-bold text-gray-500 uppercase">Phòng ban</label>
                    <input className={inputCls} value={departmentName} onChange={(e) => setDepartmentName(e.target.value)} />
                  </div>
                </div>
                <div>
                  <label className="text-xs font-bold text-gray-500 uppercase">Tổ chức (từ OCR)</label>
                  <input className={inputCls} value={organization} onChange={(e) => setOrganization(e.target.value)} />
                </div>

                {/* Partner selector */}
                <div>
                  <label className="text-xs font-bold text-gray-500 uppercase">Đối tác *</label>
                  {partnerId ? (
                    <div className="flex items-center justify-between bg-green-50 border border-green-100 rounded-lg px-3 py-2 mt-1">
                      <span className="text-sm font-bold text-green-700">
                        {partnerName || job.matchedPartner?.partnerName || `#${partnerId}`}
                      </span>
                      {!context?.partnerId && (
                        <button
                          onClick={() => { setPartnerId(null); setPartnerName(''); }}
                          className="text-xs text-gray-500 hover:text-red-500 font-medium cursor-pointer"
                        >
                          Chọn lại
                        </button>
                      )}
                    </div>
                  ) : (
                    <div className="relative mt-1">
                      <Search className="w-4 h-4 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2" />
                      <input
                        className={`${inputCls} pl-9`}
                        placeholder="Tìm đối tác theo tên/mã..."
                        value={partnerSearch}
                        onChange={(e) => setPartnerSearch(e.target.value)}
                      />
                      {(partnerOptions.length > 0 || searching) && (
                        <div className="absolute z-10 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
                          {searching && (
                            <div className="px-3 py-2 text-xs text-gray-400">Đang tìm...</div>
                          )}
                          {partnerOptions.map((p) => (
                            <button
                              key={p.partnerId}
                              onClick={() => {
                                setPartnerId(p.partnerId);
                                setPartnerName(p.name);
                                setPartnerOptions([]);
                                setPartnerSearch('');
                              }}
                              className="w-full text-left px-3 py-2 hover:bg-slate-50 cursor-pointer"
                            >
                              <span className="text-sm font-medium text-gray-700">{p.name}</span>
                              <span className="ml-2 text-xs text-gray-400">
                                {PROFILE_STATUS_LABELS[p.profileStatus]}
                              </span>
                            </button>
                          ))}
                        </div>
                      )}
                    </div>
                  )}
                </div>

                <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={isPrimary}
                    onChange={(e) => setIsPrimary(e.target.checked)}
                    className="rounded border-gray-300"
                  />
                  Đặt làm người liên hệ chính
                </label>

                <div className="flex items-center gap-2 pt-2">
                  <button
                    onClick={confirm}
                    disabled={!partnerId || !fullName.trim() || confirming}
                    className="flex-1 bg-[#004c91] hover:bg-[#003a70] text-white px-4 py-2.5 rounded-lg text-sm font-bold transition-colors disabled:opacity-50 cursor-pointer"
                  >
                    {confirming ? 'Đang lưu...' : 'Lưu người liên hệ'}
                  </button>
                  <button
                    onClick={discardAndClose}
                    disabled={confirming}
                    className="px-4 py-2.5 rounded-lg text-sm font-bold text-gray-500 hover:bg-gray-100 transition-colors cursor-pointer"
                  >
                    Huỷ bỏ
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* Step 4 — Result */}
          {step === 'RESULT' && (
            <div className="py-12 text-center">
              <CheckCircle2 className="w-12 h-12 text-green-500 mx-auto mb-4" />
              <p className="text-lg font-bold text-gray-700">Đã lưu người liên hệ thành công</p>
              <p className="text-sm text-gray-500 mt-1">
                {fullName} đã được thêm vào đối tác {partnerName || `#${partnerId}`}
                {confirmedContactId ? ` (mã liên hệ #${confirmedContactId})` : ''}.
              </p>
              <div className="mt-6 flex items-center justify-center gap-3">
                {partnerId && (
                  <a
                    href={`/dashboard/partners/${partnerId}`}
                    className="bg-[#004c91] hover:bg-[#003a70] text-white px-5 py-2 rounded-lg text-sm font-bold transition-colors"
                  >
                    Xem đối tác
                  </a>
                )}
                <button
                  onClick={onClose}
                  className="px-5 py-2 rounded-lg text-sm font-bold text-gray-500 hover:bg-gray-100 transition-colors cursor-pointer"
                >
                  Đóng
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
