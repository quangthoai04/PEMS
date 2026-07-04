/**
 * PartnerEdit — cập nhật hồ sơ đối tác (PUT /api/partners/{id}).
 * Cập nhật một hồ sơ REJECTED sẽ tự nộp lại (backend chuyển về PENDING_APPROVAL).
 */
import React, { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Loader2, AlertTriangle, Info } from 'lucide-react';
import { partnersApi } from '../../../features/partners/api/partnersApi';
import type { PartnerType } from '../../../features/partners/types/partners.types';
import { PARTNER_TYPE_LABELS } from '../../../features/partners/types/partners.types';
import {
  getApiErrorMessage,
  showLoadingToast,
  updateToastSuccess,
  updateToastMessageError,
} from '../../../shared/utils/toast';

const inputCls =
  'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white';
const labelCls = 'block text-xs font-bold text-gray-500 uppercase mb-1';

export function PartnerEdit() {
  const navigate = useNavigate();
  const { id } = useParams();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [wasRejected, setWasRejected] = useState(false);
  const [profileStatus, setProfileStatus] = useState('');

  const [name, setName] = useState('');
  const [partnerCode, setPartnerCode] = useState('');
  const [shortName, setShortName] = useState('');
  const [country, setCountry] = useState('');
  const [city, setCity] = useState('');
  const [websiteUrl, setWebsiteUrl] = useState('');
  const [address, setAddress] = useState('');
  const [description, setDescription] = useState('');
  const [partnerType, setPartnerType] = useState<PartnerType>('UNIVERSITY');
  const [cooperationStatus, setCooperationStatus] = useState('POTENTIAL');
  const [visibility, setVisibility] = useState('INTERNAL');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!id) return;
    (async () => {
      try {
        const partner = await partnersApi.getPartnerDetail(id);
        if (!partner.allowedActions.includes('EDIT')) {
          setError('Bạn không có quyền chỉnh sửa đối tác này.');
          return;
        }
        setName(partner.name);
        setPartnerCode(partner.partnerCode ?? '');
        setShortName(partner.shortName ?? '');
        setCountry(partner.country ?? '');
        setCity(partner.city ?? '');
        setWebsiteUrl(partner.websiteUrl ?? '');
        setAddress(partner.address ?? '');
        setDescription(partner.description ?? '');
        setPartnerType(partner.partnerType);
        setCooperationStatus(partner.cooperationStatus);
        setVisibility(partner.visibility);
        setWasRejected(partner.profileStatus === 'REJECTED');
        setProfileStatus(partner.profileStatus);
      } catch (e: any) {
        setError(e?.response?.data?.message || 'Không tải được hồ sơ đối tác.');
      } finally {
        setLoading(false);
      }
    })();
  }, [id]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!id || !name.trim()) return;
    setSubmitting(true);
    setError(null);
    const toastId = showLoadingToast(
      wasRejected ? 'Đang cập nhật và gửi lại hồ sơ đối tác...' : 'Đang cập nhật hồ sơ đối tác...',
      'partner-update',
    );
    try {
      await partnersApi.updatePartner(id, {
        partnerCode: partnerCode.trim() || null,
        name: name.trim(),
        shortName: shortName.trim() || null,
        country: country.trim() || null,
        city: city.trim() || null,
        websiteUrl: websiteUrl.trim() || null,
        address: address.trim() || null,
        description: description.trim() || null,
        partnerType,
        cooperationStatus,
        visibility: visibility as 'PRIVATE' | 'INTERNAL' | 'PUBLIC',
      });
      updateToastSuccess(
        toastId,
        wasRejected ? 'Đã cập nhật và gửi lại hồ sơ đối tác để duyệt.' : 'Đã cập nhật hồ sơ đối tác.',
      );
      navigate(`/dashboard/partners/${id}`);
    } catch (err: any) {
      const message = getApiErrorMessage(err, 'Không thể cập nhật hồ sơ đối tác.');
      setError(message);
      updateToastMessageError(toastId, message);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="w-full py-24 text-center text-gray-400">
        <Loader2 className="w-8 h-8 animate-spin inline-block mr-2" /> Đang tải...
      </div>
    );
  }

  return (
    <div className="w-full pb-12 max-w-4xl">
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard/partners')} className="hover:text-[#004c91] cursor-pointer">
          Quản lý đối tác
        </button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Chỉnh sửa đối tác</span>
      </div>

      <div className="border-b border-gray-100 pb-4 mb-6 flex items-center gap-3">
        <button onClick={() => navigate(`/dashboard/partners/${id}`)}
          className="p-2 rounded-lg text-gray-400 hover:bg-gray-100 hover:text-[#004c91] transition-colors cursor-pointer">
          <ArrowLeft className="w-5 h-5" />
        </button>
        <h1 className="text-3xl font-bold text-[#004c91]">Chỉnh sửa đối tác</h1>
      </div>

      {wasRejected && (
        <div className="mb-5 flex items-start gap-2 bg-blue-50 border border-blue-100 text-[#004c91] text-sm rounded-lg px-3 py-2.5">
          <Info className="w-4 h-4 mt-0.5 flex-shrink-0" />
          <span>Hồ sơ này đang <b>bị từ chối</b>. Lưu thay đổi sẽ nộp lại hồ sơ (chuyển về Chờ duyệt).</span>
        </div>
      )}
      {error && (
        <div className="mb-5 flex items-start gap-2 bg-red-50 border border-red-100 text-red-600 text-sm rounded-lg px-3 py-2.5">
          <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
          <span>{error}</span>
        </div>
      )}

      <form onSubmit={submit} className="bg-white rounded-2xl shadow-sm border border-gray-200 p-6 space-y-5">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          <div className="md:col-span-2">
            <label className={labelCls}>Tên đối tác *</label>
            <input className={inputCls} value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
          </div>
          <div>
            <label className={labelCls}>Mã đối tác</label>
            <input className={inputCls} value={partnerCode} onChange={(e) => setPartnerCode(e.target.value)} maxLength={50} />
          </div>
          <div>
            <label className={labelCls}>Tên viết tắt</label>
            <input className={inputCls} value={shortName} onChange={(e) => setShortName(e.target.value)} maxLength={100} />
          </div>
          <div>
            <label className={labelCls}>Quốc gia</label>
            <input className={inputCls} value={country} onChange={(e) => setCountry(e.target.value)} maxLength={100} />
          </div>
          <div>
            <label className={labelCls}>Thành phố</label>
            <input className={inputCls} value={city} onChange={(e) => setCity(e.target.value)} maxLength={100} />
          </div>
          <div>
            <label className={labelCls}>Website</label>
            <input className={inputCls} value={websiteUrl} onChange={(e) => setWebsiteUrl(e.target.value)} maxLength={500} />
          </div>
          <div>
            <label className={labelCls}>Loại đối tác</label>
            <select className={inputCls} value={partnerType} onChange={(e) => setPartnerType(e.target.value as PartnerType)}>
              {Object.entries(PARTNER_TYPE_LABELS).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </div>
          <div className="md:col-span-2">
            <label className={labelCls}>Địa chỉ</label>
            <input className={inputCls} value={address} onChange={(e) => setAddress(e.target.value)} maxLength={500} />
          </div>
          <div className="md:col-span-2">
            <label className={labelCls}>Mô tả</label>
            <textarea className={inputCls} rows={4} value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>
          <div>
            <label className={labelCls}>Trạng thái hợp tác</label>
            <select className={inputCls} value={cooperationStatus} onChange={(e) => setCooperationStatus(e.target.value)}>
              <option value="POTENTIAL">Tiềm năng</option>
              <option value="ACTIVE">Đang hợp tác</option>
              <option value="INACTIVE">Ngưng hợp tác</option>
              <option value="BLACKLISTED">Danh sách đen</option>
            </select>
          </div>
          <div>
            <label className={labelCls}>Chế độ hiển thị</label>
            <select className={inputCls} value={visibility} onChange={(e) => setVisibility(e.target.value)}>
              <option value="PRIVATE">Riêng tư (PRIVATE)</option>
              <option value="INTERNAL">Nội bộ (INTERNAL)</option>
              {/* PUBLIC hợp lệ chỉ khi hồ sơ đã APPROVED và không phải resubmission */}
              {profileStatus === 'APPROVED' && !wasRejected && (
                <option value="PUBLIC">Công khai (PUBLIC)</option>
              )}
            </select>
          </div>
        </div>

        <div className="flex justify-end gap-3 pt-2">
          <button type="button" onClick={() => navigate(`/dashboard/partners/${id}`)}
            className="px-5 py-2.5 rounded-lg text-sm font-bold text-gray-500 hover:bg-gray-100 transition-colors cursor-pointer">
            Huỷ
          </button>
          <button type="submit" disabled={submitting || !name.trim()}
            className="bg-[#004c91] hover:bg-[#003a70] text-white px-6 py-2.5 rounded-lg text-sm font-bold transition-colors disabled:opacity-50 cursor-pointer flex items-center gap-2">
            {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
            Lưu thay đổi
          </button>
        </div>
      </form>
    </div>
  );
}
