/**
 * Trang CampusDetail (HO)
 * UC-84 View Campus Details + UC-85 Update Campus (master data).
 * Dữ liệu lấy từ API; edit chỉ sửa master data, không đổi status / trưởng phòng IC / IC department.
 */

import React, { useEffect, useMemo, useState } from 'react';
import {
  Building2, ChevronLeft, MapPin, Edit2, Save, X, Loader2, AlertTriangle,
} from 'lucide-react';
import { createPortal } from 'react-dom';
import { useParams, useNavigate } from 'react-router-dom';
import { useCampusDetail } from '../../../features/campus-management/hooks/useCampusManagement';
import { campusManagementApi } from '../../../features/campus-management/api/campusManagementApi';
import { getAuthErrorMessage } from '../../../features/authentication/api/authError';
import { CAMPUS_PROVINCES, campusReadinessReasons } from '../../../features/campus-management/constants';
import {
  CAMPUS_ADDRESS_MAX_LENGTH,
  CAMPUS_CODE_MAX_LENGTH,
  CAMPUS_EMAIL_MAX_LENGTH,
  CAMPUS_FIELD_VALIDATORS,
  CAMPUS_NAME_MAX_LENGTH,
  CAMPUS_PHONE_MAX_LENGTH,
  isCampusMasterFormDirty,
  normalizeCampusCity,
  normalizeCampusCode,
  normalizeCampusMasterForm,
  validateCampusMasterForm,
} from '../../../features/campus-management/validation/campusMasterValidation';
import type {
  CampusMasterFieldErrors,
  CampusMasterForm,
} from '../../../features/campus-management/validation/campusMasterValidation';

type Toast = { id: number; type: 'success' | 'error'; msg: string };
/** Same shape and same rules as the create modal — create and edit share one validator (§12.1). */
type EditForm = CampusMasterForm;

export function CampusDetail() {
  const { id } = useParams();
  const navigate = useNavigate();

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isHO = userRole === 'HO' || userRole === 'ADMIN';

  const { data: campus, loading, error, notFound, refetch } = useCampusDetail(id);

  const [isEditing, setIsEditing] = useState(false);
  const [form, setForm] = useState<EditForm>({ campusCode: '', name: '', city: '', address: '', phone: '', email: '' });
  const [errors, setErrors] = useState<CampusMasterFieldErrors>({});
  const [saving, setSaving] = useState(false);
  const [toasts, setToasts] = useState<Toast[]>([]);
  // §4.5/§12.5 — changing the campus code is never silent: it waits on an explicit confirmation.
  const [pendingCodeChange, setPendingCodeChange] = useState<{ oldCode: string; newCode: string } | null>(null);

  const pushToast = (type: Toast['type'], msg: string) => {
    const tid = Date.now() + Math.random();
    setToasts((prev) => [...prev, { id: tid, type, msg }]);
    setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== tid)), 4500);
  };

  useEffect(() => {
    window.scrollTo(0, 0);
  }, [id]);

  // Snapshot of the campus master data for the edit form / dirty check.
  const baseline = useMemo<EditForm>(() => ({
    campusCode: campus?.campusCode ?? '',
    name: campus?.name ?? '',
    city: campus?.city ?? '',
    address: campus?.address ?? '',
    phone: campus?.phone ?? '',
    email: campus?.email ?? '',
  }), [campus]);

  // §12.2 — dirty is decided on NORMALIZED values, so re-typing the same text with different
  // spacing/casing (or "+84 24…" for a stored "024…") is correctly seen as no change at all.
  const isDirty = useMemo(() => isCampusMasterFormDirty(form, baseline), [form, baseline]);

  /**
   * §6.3 — legacy-city tolerance, mirroring UpdateCampusCommandHandler: a campus stored before the
   * province whitelist existed must stay editable, so an unsupported city is only an error once the
   * HO actually changes it. Without this, one unmigrated row would freeze every other field too.
   */
  const cityIsUnchangedLegacyValue = (value: string) =>
    normalizeCampusCity(value).toLowerCase() === normalizeCampusCity(baseline.city).toLowerCase();

  const validateEditForm = (candidate: EditForm): CampusMasterFieldErrors => {
    const errs = validateCampusMasterForm(candidate);
    if (errs.city && cityIsUnchangedLegacyValue(candidate.city)) delete errs.city;
    return errs;
  };

  const formIsValid = useMemo(
    () => Object.keys(validateEditForm(form)).length === 0,
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [form, baseline.city],
  );

  const startEdit = () => {
    setForm(baseline);
    setErrors({});
    setIsEditing(true);
  };

  const cancelEdit = () => {
    // §12.4 — confirm before discarding unsaved changes; a clean form closes straight away.
    if (isDirty && !window.confirm('Bạn có thay đổi chưa lưu. Hủy và bỏ các thay đổi?')) return;
    setIsEditing(false);
    setErrors({});
  };

  // Typing clears the field's error; it is re-evaluated on blur (spec §11.3).
  const setField = (field: keyof EditForm, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    setErrors((prev) => {
      if (!prev[field]) return prev;
      const next = { ...prev };
      delete next[field];
      return next;
    });
  };

  const blurField = (field: keyof EditForm) => {
    const message = field === 'city' && cityIsUnchangedLegacyValue(form.city)
      ? null
      : CAMPUS_FIELD_VALIDATORS[field](form[field]);
    setErrors((prev) => {
      const next = { ...prev };
      if (message) next[field] = message;
      else delete next[field];
      return next;
    });
  };

  const submitUpdate = async () => {
    if (!campus || saving) return;
    setSaving(true);
    try {
      // Send the SAME normalized values the validation ran against (spec §3).
      await campusManagementApi.updateCampus({
        campusId: campus.campusId,
        ...normalizeCampusMasterForm(form),
      });
      pushToast('success', 'Đã lưu thay đổi campus.');
      setIsEditing(false);
      refetch();
    } catch (err) {
      // 409/422 → keep the form open + data (UC-85 §13), surface backend message.
      pushToast('error', getAuthErrorMessage(err, 'Không thể lưu thay đổi. Vui lòng thử lại.'));
    } finally {
      setSaving(false);
    }
  };

  const save = async () => {
    if (!campus || saving) return;
    // §12.2 — a normalized no-op never reaches the API, so no audit row and no updated_at churn.
    // The button is already disabled in this state; no error toast, there is nothing wrong.
    if (!isDirty) return;

    const errs = validateEditForm(form);
    if (Object.keys(errs).length > 0) {
      setErrors(errs);
      return;
    }

    // §12.5 — the code identifies the campus in reports/integrations: confirm before sending.
    const oldCode = normalizeCampusCode(baseline.campusCode);
    const newCode = normalizeCampusCode(form.campusCode);
    if (oldCode !== newCode) {
      setPendingCodeChange({ oldCode, newCode });
      return;
    }

    await submitUpdate();
  };

  const confirmCodeChange = async () => {
    setPendingCodeChange(null);
    await submitUpdate();
  };

  // Ensure city dropdown always contains the current value even if not in the preset list.
  const cityOptions = useMemo(() => {
    const set = new Set(CAMPUS_PROVINCES);
    if (form.city && !set.has(form.city)) return [form.city, ...CAMPUS_PROVINCES];
    return CAMPUS_PROVINCES;
  }, [form.city]);

  if (!isHO) {
    return (
      <div className="p-4 sm:p-6 md:p-8 h-full flex items-center justify-center">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-gray-900 mb-2">Không có quyền truy cập</h2>
          <p className="text-gray-500">Trang này chỉ dành cho tài khoản HO.</p>
        </div>
      </div>
    );
  }

  const Breadcrumb = (
    <>
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard')}>Dashboard</span>
        <span className="mx-2">/</span>
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard/campus')}>Quản lý campus</span>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-medium">Chi tiết campus</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6">
        <h1 className="text-3xl font-bold text-[#004c91]">Chi tiết Campus</h1>
      </div>
    </>
  );

  if (loading) {
    return (
      <div className="p-4 md:p-8 bg-gray-50/50 min-h-dvh">
        {Breadcrumb}
        <div className="flex items-center justify-center py-24 text-gray-500">
          <Loader2 className="w-6 h-6 animate-spin mr-2 text-[#004c91]" /> Đang tải chi tiết campus...
        </div>
      </div>
    );
  }

  if (notFound) {
    return (
      <div className="p-4 md:p-8 bg-gray-50/50 min-h-dvh">
        {Breadcrumb}
        <div className="flex flex-col items-center justify-center py-24 text-center gap-3">
          <AlertTriangle className="w-10 h-10 text-gray-400" />
          <p className="text-lg font-normal text-gray-900">Không tìm thấy campus.</p>
          <button
            onClick={() => navigate('/dashboard/campus')}
            className="mt-2 flex items-center gap-2 px-6 py-2.5 bg-white border border-gray-200 text-gray-700 font-bold rounded-xl hover:bg-gray-50 hover:text-[#004c91] transition-colors shadow-sm"
          >
            <ChevronLeft className="w-4 h-4" /> Quay lại danh sách
          </button>
        </div>
      </div>
    );
  }

  if (error || !campus) {
    return (
      <div className="p-4 md:p-8 bg-gray-50/50 min-h-dvh">
        {Breadcrumb}
        <div className="flex flex-col items-center justify-center py-24 text-center gap-3 text-red-600">
          <AlertTriangle className="w-8 h-8" />
          <p className="font-normal">{error ?? 'Không thể tải chi tiết campus.'}</p>
          <button onClick={() => refetch()} className="mt-2 px-4 py-1.5 text-sm font-bold text-[#004c91] border border-[#004c91]/30 rounded-lg hover:bg-[#e6eff7]">
            Thử lại
          </button>
        </div>
      </div>
    );
  }

  const dash = (v: string | null | undefined) => (v && v.trim() ? v : 'Chưa cập nhật');

  return (
    <div className="p-4 md:p-8 space-y-6 bg-gray-50/50 min-h-dvh">
      {Breadcrumb}

      <div className="bg-white rounded-3xl shadow-sm border border-gray-100 overflow-hidden w-full max-w-5xl">
        {/* Header */}
        <div className="bg-[#004c91] p-8 md:p-10 border-b border-[#003366] relative overflow-hidden">
          <div className="absolute top-0 right-0 p-8 opacity-10 pointer-events-none transform translate-x-4 -translate-y-4">
            <Building2 className="w-48 h-48 text-white" />
          </div>
          <div className="relative z-10 flex flex-col items-start gap-4">
            <div className="flex items-center justify-between w-full">
              <div className="flex items-center gap-3 flex-wrap">
                <span className={`inline-flex px-3 py-1 text-xs font-bold rounded-full ${
                  campus.status === 'ACTIVE' ? 'bg-[#eaffe4] text-[#0aa14f] border border-[#ceefda]' : 'bg-gray-100 text-gray-600 border border-gray-200'
                }`}>
                  {campus.status === 'ACTIVE' ? 'Hoạt động' : 'Ngừng hoạt động'}
                </span>
                {/* UC-86 §22.1 — operational readiness is shown separately from the status. */}
                {campus.status !== 'ACTIVE' ? (
                  <span className="inline-flex px-3 py-1 text-xs font-bold rounded-full bg-gray-100 text-gray-500 border border-gray-200">
                    Không nhận đăng ký
                  </span>
                ) : campus.readiness?.isAvailableForVisitRegistration ? (
                  <span className="inline-flex px-3 py-1 text-xs font-bold rounded-full bg-[#eaffe4] text-[#0aa14f] border border-[#ceefda]">
                    Sẵn sàng nhận đăng ký
                  </span>
                ) : (
                  <span className="inline-flex px-3 py-1 text-xs font-bold rounded-full bg-amber-50 text-amber-700 border border-amber-200">
                    Chưa sẵn sàng nhận đăng ký
                  </span>
                )}
                {campus.city && (
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-white rounded-full text-xs font-bold text-[#004c91] shadow-sm">
                    {campus.city}
                  </span>
                )}
                <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-white/15 rounded-full text-xs font-bold text-white uppercase tracking-wide">
                  {campus.campusCode}
                </span>
              </div>
              {!isEditing && (
                <button
                  onClick={startEdit}
                  className="p-2 text-white/90 hover:text-white bg-transparent border border-white/30 hover:bg-white/10 rounded-xl transition-all cursor-pointer flex items-center justify-center"
                  title="Chỉnh sửa"
                >
                  <Edit2 className="w-[20px] h-[20px]" />
                </button>
              )}
            </div>

            {isEditing ? (
              <div className="w-full">
                <input
                  value={form.name}
                  onChange={(e) => setField('name', e.target.value)}
                  onBlur={() => blurField('name')}
                  maxLength={CAMPUS_NAME_MAX_LENGTH}
                  aria-label="Tên campus"
                  aria-invalid={!!errors.name}
                  aria-describedby={errors.name ? 'edit-name-error' : undefined}
                  className={`w-full text-2xl md:text-3xl font-normal text-white bg-transparent border focus:bg-white/10 p-3 rounded-2xl outline-none transition-all placeholder:text-white/50 ${errors.name ? 'border-red-300' : 'border-white/30 focus:border-white'}`}
                  placeholder="Nhập tên campus..."
                />
                {errors.name && <p id="edit-name-error" className="text-xs text-red-200 font-normal mt-1">{errors.name}</p>}
              </div>
            ) : (
              <h2 className="text-2xl md:text-3xl font-bold text-white leading-snug mt-2">{campus.name}</h2>
            )}
          </div>
        </div>

        <div className="p-4 sm:p-6 md:p-10 space-y-8">
          {/* Thông tin cơ sở */}
          <section>
            <h3 className="text-lg font-bold text-[#004c91] flex items-center gap-2 border-b border-gray-100 pb-3 mb-4">
              <MapPin className="w-5 h-5" /> Thông tin cơ sở
            </h3>

            {isEditing ? (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                <Field label="Mã code" field="campusCode" required error={errors.campusCode}>
                  <input
                    id="edit-campusCode"
                    value={form.campusCode}
                    onChange={(e) => setField('campusCode', e.target.value)}
                    onBlur={() => blurField('campusCode')}
                    maxLength={CAMPUS_CODE_MAX_LENGTH}
                    autoCapitalize="characters"
                    className={inputCls(!!errors.campusCode)}
                    placeholder="VD: HN"
                    aria-invalid={!!errors.campusCode}
                    aria-describedby={errors.campusCode ? 'edit-campusCode-error' : undefined}
                  />
                </Field>
                {/* §6.1 — the field holds a province, not a free-form "vị trí". */}
                <Field label="Tỉnh/Thành phố" field="city" required error={errors.city}>
                  <select
                    id="edit-city"
                    value={form.city}
                    onChange={(e) => setField('city', e.target.value)}
                    onBlur={() => blurField('city')}
                    className={inputCls(!!errors.city)}
                    aria-invalid={!!errors.city}
                    aria-describedby={errors.city ? 'edit-city-error' : undefined}
                  >
                    {cityOptions.map((p) => <option key={p} value={p}>{p}</option>)}
                  </select>
                </Field>
                <Field label="Địa chỉ" field="address" required error={errors.address} full>
                  <input
                    id="edit-address"
                    value={form.address}
                    onChange={(e) => setField('address', e.target.value)}
                    onBlur={() => blurField('address')}
                    maxLength={CAMPUS_ADDRESS_MAX_LENGTH}
                    className={inputCls(!!errors.address)}
                    placeholder="Số nhà, đường, phường/xã..."
                    aria-invalid={!!errors.address}
                    aria-describedby={errors.address ? 'edit-address-error' : undefined}
                  />
                </Field>
                <Field label="Số điện thoại" field="phone" required error={errors.phone}>
                  <input
                    id="edit-phone"
                    value={form.phone}
                    onChange={(e) => setField('phone', e.target.value)}
                    onBlur={() => blurField('phone')}
                    maxLength={CAMPUS_PHONE_MAX_LENGTH}
                    inputMode="tel"
                    autoComplete="tel"
                    className={inputCls(!!errors.phone)}
                    placeholder="VD: 024 7300 5588"
                    aria-invalid={!!errors.phone}
                    aria-describedby={errors.phone ? 'edit-phone-error' : undefined}
                  />
                </Field>
                <Field label="Email" field="email" required error={errors.email}>
                  <input
                    id="edit-email"
                    type="email"
                    value={form.email}
                    onChange={(e) => setField('email', e.target.value)}
                    onBlur={() => blurField('email')}
                    maxLength={CAMPUS_EMAIL_MAX_LENGTH}
                    inputMode="email"
                    autoComplete="email"
                    className={inputCls(!!errors.email)}
                    placeholder="VD: hn@fpt.edu.vn"
                    aria-invalid={!!errors.email}
                    aria-describedby={errors.email ? 'edit-email-error' : undefined}
                  />
                </Field>
              </div>
            ) : (
              <div className="bg-[#e6eff7] rounded-2xl p-6 md:p-8 border border-blue-100/50 grid grid-cols-1 md:grid-cols-2 gap-6">
                <Info label="Mã campus" value={campus.campusCode} />
                <Info label="Tên campus" value={campus.name} />
                <Info label="Vị trí" value={dash(campus.city)} />
                <Info label="Địa chỉ" value={dash(campus.address)} />
                <Info label="Số điện thoại" value={dash(campus.phone)} />
                <Info label="Email" value={dash(campus.email)} />
                <Info label="Trưởng phòng IC" value={campus.icHeadName ?? 'Chưa phân công'} muted={!campus.icHeadName} />
                <Info label="Trạng thái" value={campus.status === 'ACTIVE' ? 'Hoạt động' : 'Ngừng hoạt động'} />
              </div>
            )}

            {/* UC-86 §22.1 — explain why an ACTIVE campus is not yet accepting registrations. */}
            {!isEditing && campus.status === 'ACTIVE' && campus.readiness
              && !campus.readiness.isAvailableForVisitRegistration && (
              <div className="mt-4 flex items-start gap-3 bg-amber-50 border border-amber-200 rounded-2xl p-4">
                <AlertTriangle className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" aria-hidden="true" />
                <div className="text-sm text-amber-800">
                  <p className="font-bold">Campus đang hoạt động nhưng chưa nhận đăng ký tham quan.</p>
                  <ul className="list-disc pl-5 mt-1 space-y-0.5">
                    {campusReadinessReasons(campus.readiness).map((reason) => (
                      <li key={reason}>{reason}</li>
                    ))}
                  </ul>
                </div>
              </div>
            )}
          </section>

          {/* Actions */}
          <div className="flex items-center justify-end gap-3 pt-6 border-t border-gray-100">
            {!isEditing ? (
              <button
                onClick={() => navigate('/dashboard/campus')}
                className="flex items-center gap-2 px-6 py-2.5 bg-white border border-gray-200 text-gray-700 font-bold rounded-xl hover:bg-gray-50 hover:text-[#004c91] transition-colors shadow-sm"
              >
                <ChevronLeft className="w-4 h-4" /> <span>Quay lại</span>
              </button>
            ) : (
              <>
                <button
                  onClick={cancelEdit}
                  disabled={saving}
                  className="flex items-center gap-2 px-6 py-2.5 bg-white border border-gray-200 text-gray-700 font-bold rounded-xl hover:bg-gray-50 transition-colors shadow-sm disabled:opacity-50"
                >
                  <X className="w-4 h-4" /> <span>Hủy</span>
                </button>
                {/* §12.3 — disabled unless something actually changed AND the form is valid. */}
                <button
                  onClick={save}
                  disabled={saving || !isDirty || !formIsValid}
                  title={!isDirty ? 'Chưa có thay đổi nào để lưu.' : undefined}
                  className="flex items-center gap-2 px-6 py-2.5 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003366] transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                  <span>Lưu thay đổi</span>
                </button>
              </>
            )}
          </div>
        </div>
      </div>

      {/* §4.5/§12.5 — the campus code is an identifier: changing it needs an explicit yes. */}
      {pendingCodeChange && createPortal(
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 backdrop-blur-sm p-4" role="dialog" aria-modal="true" aria-label="Xác nhận đổi mã campus">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between">
              <h3 className="text-lg font-bold text-[#004c91] flex items-center gap-2">
                <AlertTriangle className="w-5 h-5 text-[#f37021]" />
                Xác nhận đổi mã campus
              </h3>
              <button onClick={() => setPendingCodeChange(null)} aria-label="Đóng" className="p-1.5 text-gray-400 hover:text-gray-600 transition-colors bg-gray-50 hover:bg-gray-100 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 text-sm text-gray-700 leading-relaxed space-y-3">
              <p>
                Bạn đang thay đổi mã định danh campus từ{' '}
                <span className="font-bold text-gray-900">"{pendingCodeChange.oldCode}"</span> thành{' '}
                <span className="font-bold text-gray-900">"{pendingCodeChange.newCode}"</span>.
              </p>
              <p>Các báo cáo hoặc tích hợp đang sử dụng mã cũ có thể bị ảnh hưởng.</p>
              <p className="font-normal text-gray-900">Bạn có chắc muốn tiếp tục?</p>
            </div>

            <div className="p-5 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3">
              <button
                onClick={() => setPendingCodeChange(null)}
                className="px-5 py-2 bg-white border border-gray-200 text-gray-600 font-bold rounded-xl hover:bg-gray-50 transition-colors shadow-sm"
              >
                Hủy
              </button>
              <button
                onClick={confirmCodeChange}
                className="px-5 py-2 bg-[#f37021] text-white font-bold rounded-xl hover:bg-[#e85c0d] transition-colors shadow-sm"
              >
                Xác nhận
              </button>
            </div>
          </div>
        </div>,
        document.body,
      )}

      {/* Toasts */}
      <div className="fixed top-6 right-6 z-[110] flex flex-col gap-2">
        {toasts.map((t) => (
          <div key={t.id} className={`px-4 py-3 rounded-xl shadow-lg text-sm font-normal max-w-sm ${t.type === 'success' ? 'bg-[#0aa14f] text-white' : 'bg-red-600 text-white'}`}>
            {t.msg}
          </div>
        ))}
      </div>
    </div>
  );
}

const inputCls = (hasError: boolean) =>
  `w-full text-gray-900 text-[15px] p-3 bg-white border rounded-xl outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all font-normal ${hasError ? 'border-red-400' : 'border-[#004c91]/30'}`;

/**
 * One labelled field of the edit form. The error node carries the id referenced by the input's
 * aria-describedby, so a screen reader announces the message with the field (spec §11.3).
 */
function Field({ label, field, required, error, full, children }: {
  label: string;
  field: keyof CampusMasterForm;
  required?: boolean;
  error?: string;
  full?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className={full ? 'md:col-span-2' : ''}>
      <label htmlFor={`edit-${field}`} className="text-sm font-bold text-gray-700 block mb-2">
        {label}{required && <span className="text-red-500 ml-1">*</span>}
      </label>
      {children}
      {error && <p id={`edit-${field}-error`} className="text-xs text-red-500 font-normal mt-1">{error}</p>}
    </div>
  );
}

function Info({ label, value, muted }: { label: string; value: string; muted?: boolean }) {
  return (
    <div>
      <p className="text-sm text-gray-500 font-medium mb-1">{label}</p>
      <p className={`font-normal text-lg ${muted ? 'text-gray-400 italic' : 'text-gray-900'}`}>{value}</p>
    </div>
  );
}
