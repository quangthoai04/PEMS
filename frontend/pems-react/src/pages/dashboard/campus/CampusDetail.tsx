/**
 * Trang CampusDetail (HO)
 * UC-84 View Campus Details + UC-85 Update Campus (master data).
 * Dữ liệu lấy từ API; edit chỉ sửa master data, không đổi status / trưởng phòng IC / IC department.
 */

import React, { useEffect, useMemo, useState } from 'react';
import {
  Building2, ChevronLeft, MapPin, Edit2, Save, X, Loader2, AlertTriangle,
} from 'lucide-react';
import { useParams, useNavigate } from 'react-router-dom';
import { useCampusDetail } from '../../../features/campus-management/hooks/useCampusManagement';
import { campusManagementApi } from '../../../features/campus-management/api/campusManagementApi';
import { getAuthErrorMessage } from '../../../features/authentication/api/authError';
import { CAMPUS_PROVINCES } from '../../../features/campus-management/constants';

type Toast = { id: number; type: 'success' | 'error'; msg: string };
type EditForm = { campusCode: string; name: string; city: string; address: string; phone: string; email: string };

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
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [toasts, setToasts] = useState<Toast[]>([]);

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

  const isDirty = useMemo(
    () => (Object.keys(baseline) as (keyof EditForm)[]).some((k) => (form[k] ?? '').trim() !== (baseline[k] ?? '').trim()),
    [form, baseline],
  );

  const startEdit = () => {
    setForm(baseline);
    setErrors({});
    setIsEditing(true);
  };

  const cancelEdit = () => {
    // BR §12 — confirm before discarding unsaved changes.
    if (isDirty && !window.confirm('Bạn có thay đổi chưa lưu. Hủy và bỏ các thay đổi?')) return;
    setIsEditing(false);
    setErrors({});
  };

  const setField = (field: keyof EditForm, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    setErrors((prev) => {
      if (!prev[field]) return prev;
      const next = { ...prev };
      delete next[field];
      return next;
    });
  };

  const validate = (): boolean => {
    const errs: Record<string, string> = {};
    if (!form.campusCode.trim()) errs.campusCode = 'Vui lòng nhập mã campus.';
    if (!form.name.trim()) errs.name = 'Vui lòng nhập tên campus.';
    if (!form.city.trim()) errs.city = 'Vui lòng chọn thành phố.';
    if (!form.address.trim()) errs.address = 'Vui lòng nhập địa chỉ.';
    if (!form.phone.trim()) errs.phone = 'Vui lòng nhập số điện thoại.';
    else if ((form.phone.match(/\d/g) ?? []).length < 8) errs.phone = 'Số điện thoại không đúng định dạng.';
    if (!form.email.trim()) errs.email = 'Vui lòng nhập email.';
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) errs.email = 'Email không đúng định dạng.';
    setErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const save = async () => {
    if (!campus) return;
    // AF-01 — nothing changed: skip the API.
    if (!isDirty) {
      pushToast('error', 'Không có thay đổi nào để lưu.');
      return;
    }
    if (!validate()) return;
    setSaving(true);
    try {
      await campusManagementApi.updateCampus({
        campusId: campus.campusId,
        campusCode: form.campusCode.trim(),
        name: form.name.trim(),
        city: form.city.trim(),
        address: form.address.trim(),
        phone: form.phone.trim(),
        email: form.email.trim(),
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
      <div className="p-4 md:p-8 bg-gray-50/50 min-h-screen">
        {Breadcrumb}
        <div className="flex items-center justify-center py-24 text-gray-500">
          <Loader2 className="w-6 h-6 animate-spin mr-2 text-[#004c91]" /> Đang tải chi tiết campus...
        </div>
      </div>
    );
  }

  if (notFound) {
    return (
      <div className="p-4 md:p-8 bg-gray-50/50 min-h-screen">
        {Breadcrumb}
        <div className="flex flex-col items-center justify-center py-24 text-center gap-3">
          <AlertTriangle className="w-10 h-10 text-gray-400" />
          <p className="text-lg font-bold text-gray-900">Không tìm thấy campus.</p>
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
      <div className="p-4 md:p-8 bg-gray-50/50 min-h-screen">
        {Breadcrumb}
        <div className="flex flex-col items-center justify-center py-24 text-center gap-3 text-red-600">
          <AlertTriangle className="w-8 h-8" />
          <p className="font-medium">{error ?? 'Không thể tải chi tiết campus.'}</p>
          <button onClick={() => refetch()} className="mt-2 px-4 py-1.5 text-sm font-bold text-[#004c91] border border-[#004c91]/30 rounded-lg hover:bg-[#e6eff7]">
            Thử lại
          </button>
        </div>
      </div>
    );
  }

  const dash = (v: string | null | undefined) => (v && v.trim() ? v : 'Chưa cập nhật');

  return (
    <div className="p-4 md:p-8 space-y-6 bg-gray-50/50 min-h-screen">
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
                  className="w-full text-2xl md:text-3xl font-bold text-white bg-transparent border border-white/30 focus:border-white focus:bg-white/10 p-3 rounded-2xl outline-none transition-all placeholder:text-white/50"
                  placeholder="Nhập tên campus..."
                />
                {errors.name && <p className="text-xs text-red-200 font-medium mt-1">{errors.name}</p>}
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
                <Field label="Mã code" required error={errors.campusCode}>
                  <input
                    value={form.campusCode}
                    onChange={(e) => setField('campusCode', e.target.value)}
                    className={inputCls(!!errors.campusCode)}
                    placeholder="VD: HN"
                  />
                </Field>
                <Field label="Vị trí" required error={errors.city}>
                  <select value={form.city} onChange={(e) => setField('city', e.target.value)} className={inputCls(!!errors.city)}>
                    {cityOptions.map((p) => <option key={p} value={p}>{p}</option>)}
                  </select>
                </Field>
                <Field label="Địa chỉ" required error={errors.address} full>
                  <input
                    value={form.address}
                    onChange={(e) => setField('address', e.target.value)}
                    className={inputCls(!!errors.address)}
                    placeholder="Số nhà, đường, phường/xã..."
                  />
                </Field>
                <Field label="Số điện thoại" required error={errors.phone}>
                  <input value={form.phone} onChange={(e) => setField('phone', e.target.value)} className={inputCls(!!errors.phone)} placeholder="VD: 024 7300 5588" />
                </Field>
                <Field label="Email" required error={errors.email}>
                  <input value={form.email} onChange={(e) => setField('email', e.target.value)} className={inputCls(!!errors.email)} placeholder="VD: hn@fpt.edu.vn" />
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
                <button
                  onClick={save}
                  disabled={saving}
                  className="flex items-center gap-2 px-6 py-2.5 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003366] transition-colors shadow-sm disabled:opacity-50"
                >
                  {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                  <span>Lưu thay đổi</span>
                </button>
              </>
            )}
          </div>
        </div>
      </div>

      {/* Toasts */}
      <div className="fixed top-6 right-6 z-[110] flex flex-col gap-2">
        {toasts.map((t) => (
          <div key={t.id} className={`px-4 py-3 rounded-xl shadow-lg text-sm font-medium max-w-sm ${t.type === 'success' ? 'bg-[#0aa14f] text-white' : 'bg-red-600 text-white'}`}>
            {t.msg}
          </div>
        ))}
      </div>
    </div>
  );
}

const inputCls = (hasError: boolean) =>
  `w-full text-gray-900 text-[15px] p-3 bg-white border rounded-xl outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all font-medium ${hasError ? 'border-red-400' : 'border-[#004c91]/30'}`;

function Field({ label, required, error, full, children }: { label: string; required?: boolean; error?: string; full?: boolean; children: React.ReactNode }) {
  return (
    <div className={full ? 'md:col-span-2' : ''}>
      <label className="text-sm font-bold text-gray-700 block mb-2">{label}{required && <span className="text-red-500 ml-1">*</span>}</label>
      {children}
      {error && <p className="text-xs text-red-500 font-medium mt-1">{error}</p>}
    </div>
  );
}

function Info({ label, value, muted }: { label: string; value: string; muted?: boolean }) {
  return (
    <div>
      <p className="text-sm text-gray-500 font-medium mb-1">{label}</p>
      <p className={`font-bold text-lg ${muted ? 'text-gray-400 italic' : 'text-gray-900'}`}>{value}</p>
    </div>
  );
}
