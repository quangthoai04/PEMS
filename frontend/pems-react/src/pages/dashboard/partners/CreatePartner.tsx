/**
 * CreatePartner — tạo đối tác mới (docs/PARTNER_canh/01). Hồ sơ sinh ra ở trạng thái
 * PENDING_APPROVAL; owner_campus_id do backend tự gán từ campus của người tạo —
 * form KHÔNG gửi ownerCampusId.
 */
import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Loader2, AlertTriangle, Info } from 'lucide-react';
import { partnersApi } from '../../../features/partners/api/partnersApi';
import type {
  PartnerMatchResult,
  PartnerType,
} from '../../../features/partners/types/partners.types';
import { PARTNER_TYPE_LABELS } from '../../../features/partners/types/partners.types';
import { useDebounce } from '../../../shared/hooks/useDebounce';

export function CreatePartner() {
  const navigate = useNavigate();

  const [name, setName] = useState('');
  const [partnerCode, setPartnerCode] = useState('');
  const [shortName, setShortName] = useState('');
  const [country, setCountry] = useState('');
  const [city, setCity] = useState('');
  const [websiteUrl, setWebsiteUrl] = useState('');
  const [address, setAddress] = useState('');
  const [description, setDescription] = useState('');
  const [partnerType, setPartnerType] = useState<PartnerType>('UNIVERSITY');
  const [visibility, setVisibility] = useState<'PRIVATE' | 'INTERNAL'>('INTERNAL');

  const [withContact, setWithContact] = useState(false);
  const [contactName, setContactName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [contactTitle, setContactTitle] = useState('');

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [duplicateHint, setDuplicateHint] = useState<PartnerMatchResult | null>(null);

  const debouncedName = useDebounce(name, 500);

  // Cảnh báo sớm khi tên tổ chức khớp một partner đã tồn tại (match API).
  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (!debouncedName.trim() || debouncedName.trim().length < 3) {
        setDuplicateHint(null);
        return;
      }
      try {
        const match = await partnersApi.matchPartner(debouncedName.trim());
        if (!cancelled) setDuplicateHint(match.matchStatus === 'NONE' ? null : match);
      } catch {
        if (!cancelled) setDuplicateHint(null);
      }
    })();
    return () => { cancelled = true; };
  }, [debouncedName]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) { setError('Tên đối tác là bắt buộc.'); return; }
    if (withContact && !contactName.trim()) {
      setError('Họ tên người liên hệ là bắt buộc khi thêm người liên hệ ban đầu.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const result = await partnersApi.createPartner({
        partnerCode: partnerCode.trim() || null,
        name: name.trim(),
        shortName: shortName.trim() || null,
        country: country.trim() || null,
        city: city.trim() || null,
        websiteUrl: websiteUrl.trim() || null,
        address: address.trim() || null,
        description: description.trim() || null,
        partnerType,
        visibility,
        source: 'MANUAL',
        initialContact: withContact
          ? {
              fullName: contactName.trim(),
              email: contactEmail.trim() || null,
              phone: contactPhone.trim() || null,
              jobTitle: contactTitle.trim() || null,
            }
          : null,
      });
      navigate(`/dashboard/partners/${result.partnerId}`);
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Tạo đối tác thất bại. Vui lòng thử lại.');
    } finally {
      setSubmitting(false);
    }
  };

  const inputCls =
    'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white';
  const labelCls = 'block text-xs font-bold text-gray-500 uppercase mb-1';

  return (
    <div className="w-full pb-12 max-w-4xl">
      {/* Breadcrumb */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate('/dashboard/partners')} className="hover:text-[#004c91] cursor-pointer">
          Quản lý đối tác
        </button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Thêm mới đối tác</span>
      </div>

      <div className="border-b border-gray-100 pb-4 mb-6 flex items-center gap-3">
        <button
          onClick={() => navigate('/dashboard/partners')}
          className="p-2 rounded-lg text-gray-400 hover:bg-gray-100 hover:text-[#004c91] transition-colors cursor-pointer"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <h1 className="text-3xl font-bold text-[#004c91]">Thêm mới đối tác</h1>
      </div>

      <div className="mb-5 flex items-start gap-2 bg-blue-50 border border-blue-100 text-[#004c91] text-sm rounded-lg px-3 py-2.5">
        <Info className="w-4 h-4 mt-0.5 flex-shrink-0" />
        <span>
          Hồ sơ đối tác mới sẽ ở trạng thái <b>Chờ duyệt</b> và thuộc campus của bạn.
          Trưởng phòng IC cùng campus sẽ duyệt/từ chối hồ sơ.
        </span>
      </div>

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
            {duplicateHint && (
              <p className="mt-1.5 text-xs font-medium text-amber-600 flex items-center gap-1">
                <AlertTriangle className="w-3.5 h-3.5" />
                Có thể trùng với đối tác đã tồn tại: <b>{duplicateHint.partnerName}</b> ({duplicateHint.reason})
              </p>
            )}
          </div>
          <div>
            <label className={labelCls}>Mã đối tác</label>
            <input className={inputCls} value={partnerCode} onChange={(e) => setPartnerCode(e.target.value)} maxLength={50} placeholder="VD: DEAKIN" />
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
            <input className={inputCls} value={websiteUrl} onChange={(e) => setWebsiteUrl(e.target.value)} maxLength={500} placeholder="https://..." />
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
            <label className={labelCls}>Chế độ hiển thị</label>
            <select className={inputCls} value={visibility} onChange={(e) => setVisibility(e.target.value as 'PRIVATE' | 'INTERNAL')}>
              <option value="INTERNAL">Nội bộ (INTERNAL)</option>
              <option value="PRIVATE">Riêng tư (PRIVATE)</option>
            </select>
            <p className="mt-1 text-xs text-gray-400">
              PUBLIC chỉ khả dụng sau khi hồ sơ được duyệt.
            </p>
          </div>
        </div>

        {/* Initial contact */}
        <div className="border-t border-gray-100 pt-5">
          <label className="flex items-center gap-2 text-sm font-bold text-gray-700 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={withContact}
              onChange={(e) => setWithContact(e.target.checked)}
              className="rounded border-gray-300"
            />
            Thêm người liên hệ ban đầu
          </label>
          {withContact && (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-5 mt-4">
              <div>
                <label className={labelCls}>Họ tên *</label>
                <input className={inputCls} value={contactName} onChange={(e) => setContactName(e.target.value)} maxLength={150} />
              </div>
              <div>
                <label className={labelCls}>Chức danh</label>
                <input className={inputCls} value={contactTitle} onChange={(e) => setContactTitle(e.target.value)} maxLength={150} />
              </div>
              <div>
                <label className={labelCls}>Email</label>
                <input className={inputCls} type="email" value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} maxLength={150} />
              </div>
              <div>
                <label className={labelCls}>Số điện thoại</label>
                <input className={inputCls} value={contactPhone} onChange={(e) => setContactPhone(e.target.value)} maxLength={50} />
              </div>
            </div>
          )}
        </div>

        <div className="flex justify-end gap-3 pt-2">
          <button
            type="button"
            onClick={() => navigate('/dashboard/partners')}
            className="px-5 py-2.5 rounded-lg text-sm font-bold text-gray-500 hover:bg-gray-100 transition-colors cursor-pointer"
          >
            Huỷ
          </button>
          <button
            type="submit"
            disabled={submitting || !name.trim()}
            className="bg-[#f37021] hover:bg-[#d9621a] text-white px-6 py-2.5 rounded-lg text-sm font-bold transition-colors disabled:opacity-50 cursor-pointer flex items-center gap-2"
          >
            {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
            Tạo đối tác
          </button>
        </div>
      </form>
    </div>
  );
}
