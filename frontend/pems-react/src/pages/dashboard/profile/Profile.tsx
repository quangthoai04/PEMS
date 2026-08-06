/**
 * Trang Hồ sơ cá nhân (self-service profile).
 * UC-14 View Profile + UC-15 Update Profile (text fields). Avatar upload chưa triển khai.
 * Dữ liệu lấy từ backend qua GET /profiles/viewprofile; không hardcode cơ sở theo role.
 */

import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'motion/react';
import {
  Edit2, Phone, School, ShieldCheck, User, Save, X, Mail, Briefcase, Globe, IdCard, Building2, CheckCircle2, AlertCircle, Camera, Loader2,
} from 'lucide-react';
import avatarImg from '../../../assets/Avatar/AvatarDefault.png';
import { useProfile } from '../../../features/profile/hooks/useProfile';
import { profileApi, validateAvatarFile } from '../../../features/profile/api/profileApi';
import type { GenderValue, UpdateProfileRequest, ViewProfileResponse } from '../../../features/profile/types/profile.types';
import { CountrySelect } from '../../../features/visit-request/components/shared/CountrySelect';
import { getViCountryNames } from '../../../shared/utils/countryNames';
import { getAuthErrorMessage } from '../../../features/authentication/api/authError';
import { useAuthenticatedImage } from '../../../shared/hooks/useAuthenticatedImage';
import { useAuth } from '../../../shared/hooks/useAuth';

/** Chuyển mã alpha-2 (lưu trong DB) hoặc tên tiếng Anh về tên tiếng Việt để hiển thị. */
function resolveNationalityLabel(value: string | null | undefined): string {
  if (!value) return 'Chưa cập nhật';
  const viNames = getViCountryNames();
  // Nếu value là mã alpha-2 (2 ký tự viết hoa)
  if (/^[A-Z]{2}$/.test(value) && viNames[value]) return viNames[value];
  // Nếu value là tên tiếng Anh, tra ngược về alpha-2 rồi lấy tên vi
  const found = Object.entries(viNames).find(
    ([, vi]) => vi.toLowerCase() === value.toLowerCase()
  );
  if (found) return found[1];
  return value;
}

const GENDER_LABELS: Record<GenderValue, string> = { MALE: 'Nam', FEMALE: 'Nữ', OTHER: 'Khác' };

/** Validate a phone number (optional field). Returns an error message, or null when valid.
 *  International-friendly: optional leading +, separators - . ( ) and spaces, 8–15 digits. */
function validatePhone(raw: string): string | null {
  const phone = raw.trim();
  if (!phone) return null; // optional — empty clears it
  if (!/^\+?[0-9\s.\-()]+$/.test(phone)) {
    return 'Số điện thoại chỉ được chứa chữ số và các ký tự + - . ( ).';
  }
  const digits = phone.replace(/\D/g, '');
  if (digits.length < 8 || digits.length > 15) {
    return 'Số điện thoại phải có từ 8 đến 15 chữ số.';
  }
  return null;
}

interface EditForm {
  fullName: string;
  gender: GenderValue | '';
  phone: string;
  nationality: string;
}

function toForm(p: ViewProfileResponse): EditForm {
  return {
    fullName: p.fullName ?? '',
    gender: p.gender ?? '',
    phone: p.phone ?? '',
    nationality: p.nationality ?? '',
  };
}

/** Mirror name/phone into the legacy currentUser + pems_user so Header/Sidebar stay in sync.
 *  Explicitly sets phone to null (not undefined) so clearing the field is persisted. */
function syncLocalUser(updated: ViewProfileResponse) {
  try {
    const cuRaw = localStorage.getItem('currentUser');
    if (cuRaw) {
      const cu = JSON.parse(cuRaw);
      cu.name = updated.fullName;
      localStorage.setItem('currentUser', JSON.stringify(cu));
    }
    const puRaw = localStorage.getItem('pems_user');
    if (puRaw) {
      const pu = JSON.parse(puRaw);
      pu.fullName = updated.fullName;
      // Use null explicitly so cleared phone is not retained from old localStorage value
      pu.phone = updated.phone ?? null;
      localStorage.setItem('pems_user', JSON.stringify(pu));
    }
  } catch {
    /* ignore malformed local state */
  }
}

export function Profile() {
  const navigate = useNavigate();
  const { profile, loading, error, update, applyAvatar } = useProfile();
  const { updateUser } = useAuth();

  const [isEditing, setIsEditing] = useState(false);
  const [form, setForm] = useState<EditForm>({ fullName: '', gender: '', phone: '', nationality: '' });
  const [fieldErrors, setFieldErrors] = useState<{ fullName?: string; phone?: string }>({});
  const [saving, setSaving] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  // Avatar upload state. `avatarPreview` is a local object URL shown immediately (pending or saved);
  // `pendingFile` is set only while a chosen file is not yet uploaded.
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [pendingFile, setPendingFile] = useState<File | null>(null);
  const [avatarPreview, setAvatarPreview] = useState<string | null>(null);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);

  // The stored avatar fetched as an authenticated blob (null while loading / none / error).
  const fetchedAvatar = useAuthenticatedImage(profile?.avatarUrl ?? null);
  const displayedAvatar = avatarPreview ?? fetchedAvatar ?? avatarImg;

  useEffect(() => {
    if (toast) {
      const t = setTimeout(() => setToast(null), 3500);
      return () => clearTimeout(t);
    }
  }, [toast]);

  // Revoke the local preview object URL on unmount to avoid leaking blobs.
  useEffect(() => () => {
    if (avatarPreview) URL.revokeObjectURL(avatarPreview);
  }, [avatarPreview]);

  const handlePickAvatar = () => fileInputRef.current?.click();

  const handleAvatarSelected = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = ''; // allow re-selecting the same file
    if (!file) return;

    const err = validateAvatarFile(file);
    if (err) {
      setToast({ type: 'error', message: err });
      return;
    }
    if (avatarPreview) URL.revokeObjectURL(avatarPreview);
    setPendingFile(file);
    setAvatarPreview(URL.createObjectURL(file));
  };

  const cancelAvatar = () => {
    if (avatarPreview) URL.revokeObjectURL(avatarPreview);
    setAvatarPreview(null);
    setPendingFile(null);
  };

  const handleSaveAvatar = async () => {
    if (!pendingFile) return;
    setUploadingAvatar(true);
    try {
      const result = await profileApi.uploadAvatar(pendingFile);
      applyAvatar(result.avatarUrl);
      // Update the shared AuthContext user so the dashboard Sidebar + home Header re-render with the
      // new avatar immediately (it also persists into pems_user + the legacy currentUser mirror).
      updateUser({ avatarUrl: result.avatarUrl });
      // Keep the preview shown (it equals what we just uploaded) so the avatar doesn't flash while
      // the authenticated blob for the new URL is (re)fetched. Only clear the pending state.
      setPendingFile(null);
      setToast({ type: 'success', message: 'Cập nhật ảnh đại diện thành công.' });
    } catch (err) {
      setToast({ type: 'error', message: getAuthErrorMessage(err, 'Không thể cập nhật ảnh đại diện. Vui lòng thử lại.') });
    } finally {
      setUploadingAvatar(false);
    }
  };

  const isVisitor = profile?.roleCode === 'VISITOR';
  const isStudent = profile?.roleCode === 'STUDENT';
  const isStaffOrDept = profile?.roleCode === 'STAFF' || profile?.roleCode === 'DEPARTMENT';

  const startEdit = () => {
    if (!profile) return;
    setForm(toForm(profile));
    setFieldErrors({});
    setIsEditing(true);
  };

  const cancelEdit = () => {
    setIsEditing(false);
    setFieldErrors({});
  };

  const validate = (): boolean => {
    const errs: { fullName?: string; phone?: string } = {};
    if (!form.fullName.trim()) errs.fullName = 'Họ và tên không được để trống.';
    else if (form.fullName.trim().length > 150) errs.fullName = 'Họ và tên quá dài (tối đa 150 ký tự).';
    const phoneErr = validatePhone(form.phone);
    if (phoneErr) errs.phone = phoneErr;
    setFieldErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleSave = async () => {
    if (!profile || !validate()) return;
    setSaving(true);
    const payload: UpdateProfileRequest = {
      fullName: form.fullName.trim(),
      gender: form.gender ? form.gender : null,
      // Backend uses nullable-patch pattern: `null` is ignored, `""` clears the field.
      phone: form.phone.trim() || '',
      nationality: form.nationality ? form.nationality : null,
    };

    try {
      const updated = await update(payload);
      syncLocalUser(updated);
      setIsEditing(false);
      setToast({ type: 'success', message: 'Cập nhật hồ sơ thành công.' });
    } catch (err) {
      // Keep edit mode + entered data on failure.
      setToast({ type: 'error', message: getAuthErrorMessage(err, 'Không thể cập nhật hồ sơ. Vui lòng thử lại.') });
    } finally {
      setSaving(false);
    }
  };

  const roleBadge = 'bg-[#fff0e6] text-[#f37021] border-[#fcd5ba]';

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3 }}
      className="p-4 sm:p-6 md:p-8 pb-12 max-w-4xl mx-auto"
    >
      {/* Toast */}
      {toast && (
        <div className="fixed top-6 right-6 z-50">
          <div
            className={`flex items-center gap-2 rounded-xl border px-4 py-3 shadow-md text-sm font-medium ${
              toast.type === 'success'
                ? 'bg-green-50 border-green-200 text-green-700'
                : 'bg-red-50 border-red-200 text-red-700'
            }`}
          >
            {toast.type === 'success' ? <CheckCircle2 className="w-5 h-5" /> : <AlertCircle className="w-5 h-5" />}
            {toast.message}
          </div>
        </div>
      )}

      <div className="mb-6 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Hồ sơ cá nhân</span>
      </div>

      <div className="mb-8">
        <h1 className="text-3xl font-bold text-[#004c91]">Hồ sơ cá nhân</h1>
      </div>

      {loading ? (
        <ProfileSkeleton />
      ) : error || !profile ? (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-8 text-center">
          <AlertCircle className="mx-auto mb-3 h-8 w-8 text-red-500" />
          <p className="font-medium text-red-700">{getAuthErrorMessage(error, 'Không thể tải hồ sơ cá nhân.')}</p>
        </div>
      ) : (
        <div className="relative overflow-hidden rounded-3xl border border-gray-100 bg-white shadow-sm">
          {/* Banner */}
          <div className="relative h-40 bg-gradient-to-r from-[#004c91] to-[#0066c0]">
            <div className="absolute inset-0 opacity-10 bg-[radial-gradient(circle_at_center,_white_1px,_transparent_1px)] bg-[length:20px_20px]" />

            {profile.status === 'ACTIVE' && !isEditing && (
              <button
                onClick={startEdit}
                className="absolute top-6 right-6 flex items-center gap-2 rounded-xl border border-white/40 bg-white/20 px-4 py-2 text-sm font-semibold text-white shadow-sm backdrop-blur-sm transition-all hover:bg-white/30"
              >
                <Edit2 className="h-4 w-4" />
                Chỉnh sửa hồ sơ
              </button>
            )}
            {isEditing && (
              <div className="absolute top-6 right-6 flex items-center gap-2">
                <button
                  onClick={cancelEdit}
                  disabled={saving}
                  className="flex items-center gap-1.5 rounded-xl border border-white/40 bg-white/20 px-4 py-2 text-sm font-semibold text-white shadow-sm backdrop-blur-sm transition-all hover:bg-white/30 disabled:opacity-60"
                >
                  <X className="h-4 w-4" />
                  Hủy
                </button>
                <button
                  onClick={handleSave}
                  disabled={saving}
                  className="flex items-center gap-1.5 rounded-xl border border-white bg-white px-4 py-2 text-sm font-bold text-[#004c91] shadow-sm transition-all hover:bg-gray-50 disabled:opacity-60"
                >
                  <Save className="h-4 w-4" />
                  {saving ? 'Đang lưu...' : 'Lưu thay đổi'}
                </button>
              </div>
            )}
          </div>

          <div className="px-8 pb-10">
            <div className="flex flex-col items-start gap-8 md:flex-row">
              {/* Avatar (UC-15 upload lên Google Drive) */}
              <div className="relative -mt-16 flex flex-shrink-0 flex-col items-center">
                <div className="relative h-36 w-36">
                  <div className="flex h-36 w-36 items-center justify-center overflow-hidden rounded-full bg-gray-100 shadow-md">
                    <img src={displayedAvatar} alt="Avatar" className="h-full w-full object-cover" />
                  </div>

                  {/* Loading overlay while uploading */}
                  {uploadingAvatar && (
                    <div className="absolute inset-0 flex items-center justify-center rounded-full bg-black/40">
                      <Loader2 className="h-7 w-7 animate-spin text-white" />
                    </div>
                  )}

                  {/* "Đổi ảnh" button — only for ACTIVE accounts */}
                  {profile.status === 'ACTIVE' && !uploadingAvatar && (
                    <button
                      type="button"
                      onClick={handlePickAvatar}
                      title="Đổi ảnh đại diện"
                      className="absolute bottom-1 right-1 flex h-10 w-10 items-center justify-center rounded-full border-2 border-white bg-[#004c91] text-white shadow-md transition-all hover:bg-[#0066c0]"
                    >
                      <Camera className="h-5 w-5" />
                    </button>
                  )}

                  <input
                    ref={fileInputRef}
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    className="hidden"
                    onChange={handleAvatarSelected}
                  />
                </div>

                {/* Pending save/cancel controls (only when a new file is chosen but not yet uploaded) */}
                {pendingFile && (
                  <div className="mt-3 flex items-center gap-2">
                    <button
                      type="button"
                      onClick={handleSaveAvatar}
                      disabled={uploadingAvatar}
                      className="flex items-center gap-1.5 rounded-lg bg-[#004c91] px-3 py-1.5 text-xs font-bold text-white shadow-sm transition-all hover:bg-[#0066c0] disabled:opacity-60"
                    >
                      <Save className="h-3.5 w-3.5" />
                      {uploadingAvatar ? 'Đang lưu...' : 'Lưu ảnh'}
                    </button>
                    <button
                      type="button"
                      onClick={cancelAvatar}
                      disabled={uploadingAvatar}
                      className="flex items-center gap-1.5 rounded-lg border border-gray-300 bg-white px-3 py-1.5 text-xs font-semibold text-gray-600 shadow-sm transition-all hover:bg-gray-50 disabled:opacity-60"
                    >
                      <X className="h-3.5 w-3.5" />
                      Hủy
                    </button>
                  </div>
                )}

                <div className="mt-4 flex flex-col items-center">
                  <div className={`flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-bold uppercase tracking-wide shadow-sm ${roleBadge}`}>
                    <ShieldCheck className="h-3.5 w-3.5" />
                    {profile.displayRole}
                  </div>
                </div>
              </div>

              {/* Info */}
              <div className="w-full flex-1 pt-4">
                {isEditing ? (
                  <div className="mb-3">
                    <input
                      type="text"
                      value={form.fullName}
                      onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                      className="w-full rounded-xl border-2 border-[#b6d4f0] bg-[#f0f7fc] px-4 py-2 text-3xl font-bold tracking-tight text-gray-900 shadow-sm transition-all focus:border-[#004c91] focus:bg-white focus:outline-none focus:ring-4 focus:ring-[#004c91]/20"
                    />
                    {fieldErrors.fullName && <p className="mt-1 text-sm text-red-600">{fieldErrors.fullName}</p>}
                  </div>
                ) : (
                  <div className="mb-3 border-b border-gray-300 pb-3">
                    <h2 className="text-3xl font-bold tracking-tight text-gray-900">{profile.fullName}</h2>
                  </div>
                )}

                {/* Cơ sở — không hiển thị cho VISITOR; không hardcode theo role */}
                {!isVisitor && (
                  <div className="mt-2 flex items-center gap-2 font-semibold text-[#f37021]">
                    <School className="h-5 w-5" />
                    <span>Cơ sở: {profile.displayCampusName ?? 'Chưa cấu hình'}</span>
                  </div>
                )}

                <div className="mt-8 grid grid-cols-1 gap-6 md:grid-cols-2">
                  {/* Giới tính */}
                  <InfoCard icon={<User className="h-5 w-5" />} label="Giới tính">
                    {isEditing ? (
                      <select
                        value={form.gender}
                        onChange={(e) => setForm({ ...form, gender: e.target.value as GenderValue | '' })}
                        className="w-full rounded-lg border border-[#b6d4f0] bg-white px-3 py-1.5 font-medium text-gray-900 focus:border-[#004c91] focus:outline-none focus:ring-1 focus:ring-[#004c91]"
                      >
                        <option value="" disabled hidden>Chọn giới tính</option>
                        <option value="MALE">Nam</option>
                        <option value="FEMALE">Nữ</option>
                        <option value="OTHER">Khác</option>
                      </select>
                    ) : (
                      <ValueText>{profile.gender ? GENDER_LABELS[profile.gender] : 'Chưa cập nhật'}</ValueText>
                    )}
                  </InfoCard>

                  {/* Số điện thoại */}
                  <InfoCard icon={<Phone className="h-5 w-5" />} label="Số điện thoại">
                    {isEditing ? (
                      <>
                        <input
                          type="text"
                          value={form.phone}
                          onChange={(e) => setForm({ ...form, phone: e.target.value })}
                          placeholder="VD: 0912345678"
                          className="w-full rounded-lg border border-[#b6d4f0] bg-white px-3 py-1.5 font-medium text-gray-900 focus:border-[#004c91] focus:outline-none focus:ring-1 focus:ring-[#004c91]"
                        />
                        {fieldErrors.phone && <p className="mt-1 text-sm text-red-600">{fieldErrors.phone}</p>}
                      </>
                    ) : (
                      <ValueText>{profile.phone || 'Chưa cập nhật'}</ValueText>
                    )}
                  </InfoCard>

                  {/* Email — readonly */}
                  <InfoCard icon={<Mail className="h-5 w-5" />} label="Email" full>
                    <ValueText>{profile.email}</ValueText>
                  </InfoCard>

                  {/* MSSV — STUDENT */}
                  {isStudent && (
                    <InfoCard icon={<IdCard className="h-5 w-5" />} label="MSSV" full>
                      <ValueText className="uppercase">{profile.studentCode || '—'}</ValueText>
                    </InfoCard>
                  )}

                  {/* Phòng ban + Chức vụ — STAFF / DEPARTMENT */}
                  {isStaffOrDept && (
                    <>
                      <InfoCard icon={<Building2 className="h-5 w-5" />} label="Phòng ban">
                        <ValueText>{profile.displayDepartmentName || '—'}</ValueText>
                      </InfoCard>
                      <InfoCard icon={<Briefcase className="h-5 w-5" />} label="Chức vụ">
                        <ValueText>{profile.displayPosition || '—'}</ValueText>
                      </InfoCard>
                    </>
                  )}

                  {/* Quốc tịch — chỉ VISITOR */}
                  {isVisitor && (
                    <InfoCard icon={<Globe className="h-5 w-5" />} label="Quốc tịch" full>
                      {isEditing ? (
                        <CountrySelect
                          value={form.nationality ?? ''}
                          onChange={(v) => setForm({ ...form, nationality: v })}
                          placeholder="Tìm hoặc chọn quốc tịch..."
                          storeLang="en"
                        />
                      ) : (
                        <ValueText>{resolveNationalityLabel(profile.nationality)}</ValueText>
                      )}
                    </InfoCard>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </motion.div>
  );
}

function InfoCard({
  icon, label, children, full,
}: { icon: React.ReactNode; label: string; children: React.ReactNode; full?: boolean }) {
  return (
    <div className={`flex items-center gap-4 rounded-2xl border border-[#d2e5f5] bg-[#f0f7fc] p-4 transition-all hover:shadow-sm ${full ? 'md:col-span-2' : ''}`}>
      <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full border border-[#eef5fa] bg-white text-[#004c91] shadow-sm">
        {icon}
      </div>
      <div className="min-w-0 flex-1">
        <p className="mb-0.5 text-xs font-semibold uppercase tracking-wider text-gray-500">{label}</p>
        {children}
      </div>
    </div>
  );
}

function ValueText({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <p className={`font-medium text-gray-900 ${className}`}>{children}</p>;
}

function ProfileSkeleton() {
  return (
    <div className="overflow-hidden rounded-3xl border border-gray-100 bg-white shadow-sm">
      <div className="h-40 animate-pulse bg-gradient-to-r from-slate-200 to-slate-100" />
      <div className="px-8 pb-10">
        <div className="-mt-16 h-36 w-36 animate-pulse rounded-full bg-slate-200" />
        <div className="mt-6 h-8 w-64 animate-pulse rounded bg-slate-200" />
        <div className="mt-8 grid grid-cols-1 gap-6 md:grid-cols-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-20 animate-pulse rounded-2xl bg-slate-100" />
          ))}
        </div>
      </div>
    </div>
  );
}
