import React, { useEffect, useState } from 'react';
import { X, RefreshCw, UserCog, AlertTriangle, ArrowRight } from 'lucide-react';
import { accountManagementApi } from '../api/accountManagementApi';
import { getAccountErrorMessage } from '../api/accountError';
import type {
  ReplaceStaffLeaderMode,
  ReplaceStaffLeaderResponse,
  StaffLeaderReplacementPreview,
} from '../types/accountManagement.types';
import {
  ACCOUNT_EMAIL_MAX_LENGTH,
  ACCOUNT_FULL_NAME_MAX_LENGTH,
  REPLACEMENT_REASON_MAX_LENGTH,
  normalizeAccountEmail,
  normalizeFullName,
  normalizeReplacementReason,
  validateAccountEmail,
  validateFullName,
  validateReplacementReason,
} from '../validation/accountIdentityValidation';

interface Props {
  campusId: string | number;
  /** Optional campus name for the header before the preview loads. */
  campusName?: string | null;
  onClose: () => void;
  /** Called after a successful replace so the parent can toast + refresh. */
  onReplaced: (result: ReplaceStaffLeaderResponse) => void;
}

const STATUS_LABEL: Record<string, string> = { ACTIVE: 'Đang hoạt động', INACTIVE: 'Vô hiệu hóa', LOCKED: 'Bị khóa' };

// Warning shown for the current leader's status (spec §16.3).
const OLD_LEADER_WARNING: Record<string, string> = {
  ACTIVE: 'Người hiện tại sẽ được chuyển về Staff thường và bị đăng xuất khỏi các phiên hiện tại.',
  INACTIVE: 'Staff Leader hiện tại đang bị vô hiệu hóa. Sau khi thay thế, tài khoản này vẫn giữ trạng thái vô hiệu hóa và không còn quyền Staff Leader.',
  LOCKED: 'Staff Leader hiện tại đang bị khóa. Việc thay thế sẽ giữ nguyên trạng thái khóa và ghi nhận security event.',
};

/** Field-level errors; the create-new inputs are only validated in mode CREATE_NEW_USER. */
type ReplaceFieldErrors = { fullName?: string; email?: string; reason?: string; candidate?: string };

// Red border for a field that failed validation (identity spec §7.4).
const fieldClass = (hasError: boolean) =>
  `w-full px-4 py-2.5 rounded-xl border outline-none text-sm bg-white ${
    hasError
      ? 'border-red-400 focus:border-red-500 focus:ring-1 focus:ring-red-400'
      : 'border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91]'
  }`;

/**
 * Replace Staff Leader (Trưởng phòng IC) of a campus — HO only. Two modes: promote an existing IC
 * Staff, or create a new leader. Shows the current leader + a status-specific warning, validates
 * input, then asks for a final confirmation before calling the API. See REPLACE_STAFF_LEADER spec.
 */
export function ReplaceStaffLeaderModal({ campusId, campusName, onClose, onReplaced }: Props) {
  const [preview, setPreview] = useState<StaffLeaderReplacementPreview | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [mode, setMode] = useState<ReplaceStaffLeaderMode>('EXISTING_USER');
  const [candidateId, setCandidateId] = useState('');
  const [newUser, setNewUser] = useState({ fullName: '', email: '' });
  const [reason, setReason] = useState('');

  const [showConfirm, setShowConfirm] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<ReplaceFieldErrors>({});

  useEffect(() => {
    let active = true;
    setLoading(true);
    setLoadError(null);
    accountManagementApi
      .getStaffLeaderReplacementPreview(campusId)
      .then((res) => { if (active) setPreview(res); })
      .catch((err) => { if (active) setLoadError(getAccountErrorMessage(err, 'Không thể tải dữ liệu thay thế Staff Leader.')); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [campusId]);

  // No eligible candidates → default to the create-new mode so the form is usable.
  useEffect(() => {
    if (preview && preview.canReplace && preview.eligibleCandidates.length === 0) setMode('CREATE_NEW_USER');
  }, [preview]);

  const current = preview?.currentLeader ?? null;
  const candidates = preview?.eligibleCandidates ?? [];
  const selectedCandidate = candidates.find((c) => String(c.userId) === candidateId) ?? null;

  // New-leader identity for the confirm dialog.
  const newLeaderName = mode === 'EXISTING_USER' ? selectedCandidate?.fullName : normalizeFullName(newUser.fullName);
  const newLeaderEmail = mode === 'EXISTING_USER' ? selectedCandidate?.email : normalizeAccountEmail(newUser.email);

  /**
   * Reason is always required. The create-new name/email are validated ONLY in CREATE_NEW_USER —
   * in EXISTING_USER those inputs are hidden and must never block the flow (spec §6.3.1 / AC-07).
   */
  const validate = (): ReplaceFieldErrors => {
    const errors: ReplaceFieldErrors = {};
    const reasonError = validateReplacementReason(reason);
    if (reasonError) errors.reason = reasonError;

    if (mode === 'EXISTING_USER') {
      if (!candidateId) errors.candidate = 'Vui lòng chọn nhân sự thay thế.';
    } else {
      const fullNameError = validateFullName(newUser.fullName);
      if (fullNameError) errors.fullName = fullNameError;
      const emailError = validateAccountEmail(newUser.email);
      if (emailError) errors.email = emailError;
    }
    return errors;
  };

  const openConfirm = () => {
    const errors = validate();
    setFieldErrors(errors);
    if (Object.values(errors).some(Boolean)) { setSubmitError(null); return; }
    setSubmitError(null);
    setShowConfirm(true);
  };

  const submit = async () => {
    if (submitting) return; // double-click guard
    setSubmitting(true);
    setSubmitError(null);
    try {
      // Normalized values are what the backend stores and compares against.
      const result = await accountManagementApi.replaceStaffLeader({
        campusId,
        mode,
        reason: normalizeReplacementReason(reason),
        ...(mode === 'EXISTING_USER'
          ? { newLeaderUserId: candidateId }
          : { fullName: normalizeFullName(newUser.fullName), email: normalizeAccountEmail(newUser.email) }),
      });
      onReplaced(result);
    } catch (err) {
      setShowConfirm(false);
      setSubmitError(getAccountErrorMessage(err, 'Không thể thay thế Staff Leader. Vui lòng thử lại.'));
    } finally {
      setSubmitting(false);
    }
  };

  const headerCampus = preview?.campusName ?? campusName ?? '';

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-[70] flex items-center justify-center p-4 animate-in fade-in duration-200">
      <div className="bg-white rounded-2xl w-full max-w-2xl shadow-xl overflow-hidden animate-in zoom-in-95 duration-300 flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between bg-[#004c91] shrink-0">
          <h2 className="text-xl font-black text-white flex items-center gap-2">
            <UserCog className="w-5 h-5" /> Thay thế Trưởng phòng IC
          </h2>
          <button onClick={onClose} className="w-8 h-8 rounded-full hover:bg-white/20 flex items-center justify-center text-white transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 overflow-y-auto space-y-5">
          {loading ? (
            <div className="flex items-center gap-2 text-sm font-normal text-gray-500 py-8 justify-center">
              <RefreshCw className="w-5 h-5 animate-spin" /> Đang tải dữ liệu...
            </div>
          ) : loadError ? (
            <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-normal text-red-700">{loadError}</div>
          ) : preview && !preview.canReplace ? (
            <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 flex items-start gap-2">
              <AlertTriangle className="w-5 h-5 shrink-0 mt-0.5 text-amber-600" />
              <p className="font-normal leading-snug">{preview.message}</p>
            </div>
          ) : preview ? (
            <>
              {/* 1. Campus + IC department */}
              <div className="rounded-xl border border-gray-200 bg-gray-50 px-4 py-3 text-sm">
                <p className="font-bold text-[#004c91]">{headerCampus}</p>
                <p className="text-gray-500 mt-0.5">Phòng ban: {preview.icDepartmentName ?? 'Phòng Hợp tác Quốc tế'}</p>
              </div>

              {/* 2. Current leader + status warning */}
              {current && (
                <div>
                  <h4 className="text-xs font-black uppercase tracking-wider text-gray-500 mb-2">Trưởng phòng IC hiện tại</h4>
                  <div className="rounded-xl border border-gray-200 px-4 py-3 text-sm flex items-center justify-between gap-3">
                    <div>
                      <p className="font-bold text-gray-900">{current.fullName}</p>
                      <p className="text-gray-500">{current.email}</p>
                    </div>
                    <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-[11px] font-bold uppercase tracking-wide border ${
                      current.status === 'ACTIVE' ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                        : current.status === 'INACTIVE' ? 'bg-amber-50 text-amber-700 border-amber-200'
                        : 'bg-red-50 text-red-700 border-red-200'
                    }`}>
                      {STATUS_LABEL[current.status] ?? current.status}
                    </span>
                  </div>
                  {OLD_LEADER_WARNING[current.status] && (
                    <p className="mt-2 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2 flex items-start gap-1.5">
                      <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
                      {OLD_LEADER_WARNING[current.status]}
                    </p>
                  )}
                </div>
              )}

              {/* 3. New leader */}
              <div>
                <h4 className="text-xs font-black uppercase tracking-wider text-gray-500 mb-2">Trưởng phòng IC mới</h4>

                {/* Mode toggle */}
                <div className="flex gap-2 mb-3">
                  <button
                    type="button"
                    onClick={() => setMode('EXISTING_USER')}
                    className={`flex-1 px-3 py-2 rounded-xl text-sm font-bold border transition-colors outline-none ${
                      mode === 'EXISTING_USER' ? 'bg-[#004c91] text-white border-[#004c91]' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'
                    }`}
                  >
                    Chọn nhân sự IC có sẵn
                  </button>
                  <button
                    type="button"
                    onClick={() => setMode('CREATE_NEW_USER')}
                    className={`flex-1 px-3 py-2 rounded-xl text-sm font-bold border transition-colors outline-none ${
                      mode === 'CREATE_NEW_USER' ? 'bg-[#004c91] text-white border-[#004c91]' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'
                    }`}
                  >
                    Tạo tài khoản mới
                  </button>
                </div>

                {mode === 'EXISTING_USER' ? (
                  <div>
                    <select
                      value={candidateId}
                      aria-invalid={!!fieldErrors.candidate}
                      aria-describedby={fieldErrors.candidate ? 'replace-candidate-error' : undefined}
                      onChange={(e) => { setCandidateId(e.target.value); setFieldErrors((prev) => ({ ...prev, candidate: undefined })); }}
                      className={`w-full px-4 py-2.5 rounded-xl border outline-none text-sm bg-gray-50 hover:bg-gray-100 cursor-pointer ${
                        fieldErrors.candidate
                          ? 'border-red-400 focus:border-red-500 focus:ring-1 focus:ring-red-400'
                          : 'border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91]'
                      }`}
                    >
                      <option value="">-- Chọn nhân sự IC Staff --</option>
                      {candidates.map((c) => (
                        <option key={c.userId} value={c.userId}>{c.fullName} — {c.email}</option>
                      ))}
                    </select>
                    {fieldErrors.candidate && (
                      <p id="replace-candidate-error" className="mt-1.5 text-sm text-red-600 font-normal">{fieldErrors.candidate}</p>
                    )}
                    {candidates.length === 0 && (
                      <p className="mt-1.5 text-xs text-gray-500">Phòng IC của cơ sở chưa có nhân sự IC Staff phù hợp. Bạn có thể tạo tài khoản mới.</p>
                    )}
                  </div>
                ) : (
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <div>
                      <label htmlFor="replace-full-name" className="block text-sm font-bold text-gray-700 mb-1.5">Họ và tên <span className="text-red-500">*</span></label>
                      <input
                        id="replace-full-name"
                        type="text"
                        value={newUser.fullName}
                        maxLength={ACCOUNT_FULL_NAME_MAX_LENGTH}
                        autoComplete="name"
                        aria-invalid={!!fieldErrors.fullName}
                        aria-describedby={fieldErrors.fullName ? 'replace-full-name-error' : undefined}
                        onChange={(e) => { setNewUser({ ...newUser, fullName: e.target.value }); setFieldErrors((prev) => ({ ...prev, fullName: undefined })); }}
                        onBlur={(e) => setFieldErrors((prev) => ({ ...prev, fullName: validateFullName(e.target.value) ?? undefined }))}
                        placeholder="Trần Văn B"
                        className={fieldClass(!!fieldErrors.fullName)}
                      />
                      {fieldErrors.fullName && (
                        <p id="replace-full-name-error" className="mt-1.5 text-sm text-red-600 font-normal">{fieldErrors.fullName}</p>
                      )}
                    </div>
                    <div>
                      <label htmlFor="replace-email" className="block text-sm font-bold text-gray-700 mb-1.5">Email <span className="text-red-500">*</span></label>
                      <input
                        id="replace-email"
                        type="email"
                        value={newUser.email}
                        maxLength={ACCOUNT_EMAIL_MAX_LENGTH}
                        autoComplete="email"
                        inputMode="email"
                        aria-invalid={!!fieldErrors.email}
                        aria-describedby={fieldErrors.email ? 'replace-email-error' : undefined}
                        onChange={(e) => { setNewUser({ ...newUser, email: e.target.value }); setFieldErrors((prev) => ({ ...prev, email: undefined })); }}
                        onBlur={(e) => setFieldErrors((prev) => ({ ...prev, email: validateAccountEmail(e.target.value) ?? undefined }))}
                        placeholder="example@gmail.com"
                        className={fieldClass(!!fieldErrors.email)}
                      />
                      {fieldErrors.email ? (
                        <p id="replace-email-error" className="mt-1.5 text-sm text-red-600 font-normal">{fieldErrors.email}</p>
                      ) : (
                        <p className="mt-1.5 text-xs text-gray-500">Chỉ chấp nhận @gmail.com và @fpt.edu.vn.</p>
                      )}
                    </div>
                  </div>
                )}
              </div>

              {/* 4. Reason (required) */}
              <div>
                <label htmlFor="replace-reason" className="block text-sm font-bold text-gray-700 mb-2">Lý do thay thế <span className="text-red-500">*</span></label>
                <textarea
                  id="replace-reason"
                  value={reason}
                  maxLength={REPLACEMENT_REASON_MAX_LENGTH}
                  aria-invalid={!!fieldErrors.reason}
                  aria-describedby={fieldErrors.reason ? 'replace-reason-error' : undefined}
                  onChange={(e) => { setReason(e.target.value); setFieldErrors((prev) => ({ ...prev, reason: undefined })); }}
                  onBlur={(e) => setFieldErrors((prev) => ({ ...prev, reason: validateReplacementReason(e.target.value) ?? undefined }))}
                  rows={2}
                  placeholder="Ví dụ: Thay đổi nhân sự phụ trách Phòng Hợp tác Quốc tế."
                  className={`${fieldClass(!!fieldErrors.reason)} resize-none`}
                />
                {fieldErrors.reason ? (
                  <p id="replace-reason-error" className="mt-1.5 text-sm text-red-600 font-normal">{fieldErrors.reason}</p>
                ) : (
                  <p className="mt-1.5 text-xs text-gray-500">Tối thiểu 10 ký tự, tối đa 500 ký tự.</p>
                )}
              </div>

              {submitError && (
                <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-normal text-red-700">{submitError}</div>
              )}
            </>
          ) : null}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 rounded-b-2xl flex items-center justify-end gap-3 shrink-0">
          <button
            onClick={onClose}
            className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors outline-none"
          >
            Hủy bỏ
          </button>
          <button
            onClick={openConfirm}
            disabled={loading || submitting || !preview?.canReplace}
            className="px-5 py-2.5 rounded-xl font-bold text-white bg-[#f37021] hover:bg-[#e85c0d] shadow-sm transition-all outline-none disabled:opacity-60 disabled:cursor-not-allowed"
          >
            Tiếp tục
          </button>
        </div>
      </div>

      {/* Confirm dialog */}
      {showConfirm && current && (
        <div className="fixed inset-0 bg-black/40 z-[80] flex items-center justify-center p-4 animate-in fade-in duration-150">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200">
            <div className="px-6 py-4 border-b border-gray-100">
              <h3 className="text-lg font-black text-gray-800">Xác nhận thay thế Staff Leader</h3>
            </div>
            <div className="p-6 text-sm text-gray-700 leading-relaxed space-y-3">
              <p>Bạn có chắc muốn thay thế Staff Leader của <strong className="text-[#004c91]">{headerCampus}</strong>?</p>
              <div className="rounded-xl border border-gray-200 bg-gray-50 px-4 py-3 space-y-2">
                <div>
                  <p className="text-[11px] font-bold uppercase tracking-wider text-gray-400">Cơ sở / Phòng ban</p>
                  <p className="font-normal text-gray-800">
                    {headerCampus} — {preview?.icDepartmentName ?? 'Phòng Hợp tác Quốc tế'}
                  </p>
                </div>
                <div>
                  <p className="text-[11px] font-bold uppercase tracking-wider text-gray-400">Người hiện tại</p>
                  <p className="font-normal text-gray-800">{current.fullName} — {current.email}</p>
                </div>
                <div className="flex justify-center text-gray-400"><ArrowRight className="w-4 h-4 rotate-90" /></div>
                <div>
                  <p className="text-[11px] font-bold uppercase tracking-wider text-gray-400">
                    Người thay thế ({mode === 'EXISTING_USER' ? 'nhân sự IC có sẵn' : 'tài khoản mới'})
                  </p>
                  <p className="font-bold text-[#0aa14f]">
                    {newLeaderName || '(tài khoản mới)'}{newLeaderEmail ? ` — ${newLeaderEmail}` : ''}
                  </p>
                </div>
                <div>
                  <p className="text-[11px] font-bold uppercase tracking-wider text-gray-400">Lý do thay thế</p>
                  <p className="font-normal text-gray-700 break-words">{normalizeReplacementReason(reason)}</p>
                </div>
              </div>
              <p className="text-xs text-amber-700">
                Sau khi xác nhận, người hiện tại sẽ được chuyển về vai trò Staff (STAFF/STAFF) và các phiên đăng nhập liên quan sẽ bị thu hồi.
              </p>
              {submitError && (
                <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-normal text-red-700">{submitError}</div>
              )}
            </div>
            <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3">
              <button
                onClick={() => setShowConfirm(false)}
                disabled={submitting}
                className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors outline-none disabled:opacity-60"
              >
                Quay lại
              </button>
              <button
                onClick={submit}
                disabled={submitting}
                className="px-5 py-2.5 rounded-xl font-bold text-white bg-[#f37021] hover:bg-[#e85c0d] shadow-sm transition-all outline-none disabled:opacity-60 disabled:cursor-not-allowed"
              >
                {submitting ? 'Đang xử lý...' : 'Xác nhận thay thế'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
