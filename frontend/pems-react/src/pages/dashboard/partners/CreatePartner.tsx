/**
 * CreatePartner — tạo đối tác mới (docs/PARTNER_canh/01). Hồ sơ sinh ra ở trạng thái
 * PENDING_APPROVAL; owner_campus_id do backend tự gán từ campus của người tạo —
 * form KHÔNG gửi ownerCampusId.
 */
import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Loader2, AlertTriangle, Info, Languages, RotateCw, Upload, Image as ImageIcon } from 'lucide-react';
import toast from 'react-hot-toast';
import { partnersApi } from '../../../features/partners/api/partnersApi';
import type {
  PartnerMatchResult,
  PartnerType,
} from '../../../features/partners/types/partners.types';
import { PARTNER_TYPE_LABELS } from '../../../features/partners/types/partners.types';
import { CountrySelect } from '../../../features/visit-request/components/shared/CountrySelect';
import { CitySelect } from '../../../features/partners/components/CitySelect';
import { useDebounce } from '../../../shared/hooks/useDebounce';
import { usePartnerBilingualTranslate } from './usePartnerBilingualTranslate';
import { BilingualColumns, LanguageColumnLabel } from '../news/components/BilingualColumns';
import { SmartImage } from '../news/components/SmartImage';
import { validateFile } from '../../../shared/utils/fileValidation';
import {
  getApiErrorMessage,
  showLoadingToast,
  updateToastSuccess,
  updateToastMessageError,
} from '../../../shared/utils/toast';

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

  // Upload of the actual file only happens after createPartner() returns a partnerId (the
  // logo/cover Drive folder is keyed by the partner's own record — see uploadLogo/uploadCover).
  // Until then we just hold the picked File + a local blob preview.
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [coverFile, setCoverFile] = useState<File | null>(null);
  const [logoPreview, setLogoPreview] = useState<string | null>(null);
  const [coverPreview, setCoverPreview] = useState<string | null>(null);

  function handleImageSelect(kind: 'logo' | 'cover', file: File | undefined) {
    if (!file) return;
    const check = validateFile(file, kind === 'logo' ? 'PARTNER_LOGO' : 'PARTNER_COVER');
    if (!check.ok) {
      toast.error(check.message || 'Tệp không hợp lệ.');
      return;
    }
    if (kind === 'logo') {
      setLogoFile(file);
      setLogoPreview(URL.createObjectURL(file));
    } else {
      setCoverFile(file);
      setCoverPreview(URL.createObjectURL(file));
    }
  }

  const [withContact, setWithContact] = useState(false);
  const [contactName, setContactName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [contactTitle, setContactTitle] = useState('');

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [duplicateHint, setDuplicateHint] = useState<PartnerMatchResult | null>(null);

  // Bilingual editor — off by default (VI-only form); the admin opts in via the EN toggle, which
  // then translates the current name/shortName/description/address exactly once.
  const [bilingual, setBilingual] = useState(false);
  const [englishName, setEnglishName] = useState('');
  const [englishShortName, setEnglishShortName] = useState('');
  const [englishDescription, setEnglishDescription] = useState('');
  const [englishAddress, setEnglishAddress] = useState('');
  const [englishNameTouched, setEnglishNameTouched] = useState(false);
  const [englishShortNameTouched, setEnglishShortNameTouched] = useState(false);
  const [englishDescriptionTouched, setEnglishDescriptionTouched] = useState(false);
  const [englishAddressTouched, setEnglishAddressTouched] = useState(false);

  const { translating: translatingPartner, retranslateNow: retranslatePartner } = usePartnerBilingualTranslate({
    enabled: bilingual,
    name,
    shortName,
    country,
    city,
    description,
    address,
    onTranslated: (result) => {
      if (!englishNameTouched) setEnglishName(result.name);
      if (!englishShortNameTouched) setEnglishShortName(result.shortName);
      if (!englishDescriptionTouched) setEnglishDescription(result.description);
      if (!englishAddressTouched) setEnglishAddress(result.address);
    },
  });

  async function handleRetranslatePartner() {
    setEnglishNameTouched(false);
    setEnglishShortNameTouched(false);
    setEnglishDescriptionTouched(false);
    setEnglishAddressTouched(false);
    const ok = await retranslatePartner();
    if (ok) toast.success('Đã dịch lại sang tiếng Anh.');
    else toast.error('Không thể dịch tự động. Vui lòng thử lại.');
  }

  const debouncedName = useDebounce(name, 500);

  // Đổi sang quốc gia khác thì xoá thành phố cũ (gợi ý không còn khớp);
  // giữ nguyên nếu trước đó chưa chọn quốc gia để không mất dữ liệu đã gõ.
  const handleCountryChange = (next: string) => {
    if (country.trim() && next.trim().toLowerCase() !== country.trim().toLowerCase()) {
      setCity('');
    }
    setCountry(next);
  };

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
    const toastId = showLoadingToast('Đang tạo hồ sơ đối tác...', 'partner-create');
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
        // Omitted entirely when the EN panel was never opened — the backend then auto-translates
        // once and stores the result, same rule as FAQ/News.
        ...(bilingual
          ? {
              englishName: englishName.trim() || null,
              englishShortName: englishShortName.trim() || null,
              englishDescription: englishDescription.trim() || null,
              englishAddress: englishAddress.trim() || null,
            }
          : {}),
      });

      // Logo/cover can only be uploaded once the partner (and its Drive folder) exists, so this
      // runs as a second step right after create. A failure here must not look like the whole
      // create failed — the partner record was already saved — so it gets its own toast and the
      // flow still navigates to the new partner's detail page.
      if (logoFile || coverFile) {
        try {
          const [logoUpload, coverUpload] = await Promise.all([
            logoFile ? partnersApi.uploadLogo(result.partnerId, logoFile) : Promise.resolve(null),
            coverFile ? partnersApi.uploadCover(result.partnerId, coverFile) : Promise.resolve(null),
          ]);
          await partnersApi.updatePartner(result.partnerId, {
            partnerCode: partnerCode.trim() || null,
            name: name.trim(),
            shortName: shortName.trim() || null,
            country: country.trim() || null,
            city: city.trim() || null,
            websiteUrl: websiteUrl.trim() || null,
            address: address.trim() || null,
            description: description.trim() || null,
            partnerType,
            cooperationStatus: 'POTENTIAL',
            visibility,
            logoFileId: logoUpload?.fileId ?? null,
            coverFileId: coverUpload?.fileId ?? null,
            ...(bilingual
              ? {
                  englishName: englishName.trim() || null,
                  englishShortName: englishShortName.trim() || null,
                  englishDescription: englishDescription.trim() || null,
                  englishAddress: englishAddress.trim() || null,
                }
              : {}),
          });
        } catch {
          toast.error('Đã tạo đối tác nhưng tải logo/ảnh bìa thất bại. Vào Chỉnh sửa để tải lại.');
        }
      }

      updateToastSuccess(toastId, 'Đã tạo hồ sơ đối tác thành công.');
      navigate(`/dashboard/partners/${result.partnerId}`);
    } catch (err: any) {
      const message = getApiErrorMessage(err, 'Không thể tạo hồ sơ đối tác. Vui lòng kiểm tra lại thông tin.');
      setError(message);
      updateToastMessageError(toastId, message);
    } finally {
      setSubmitting(false);
    }
  };

  const inputCls =
    'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white';
  const labelCls = 'block text-xs font-bold text-gray-500 uppercase mb-1';

  return (
    <div className="w-full pb-12 max-w-4xl mx-auto">
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

      <div className="border-b border-gray-100 pb-4 mb-6 flex items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate('/dashboard/partners')}
            className="p-2 rounded-lg text-gray-400 hover:bg-gray-100 hover:text-[#004c91] transition-colors cursor-pointer"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h1 className="text-3xl font-bold text-[#004c91]">Thêm mới đối tác</h1>
        </div>
        <div className="flex items-center gap-2">
          {bilingual && (
            <button
              type="button"
              onClick={handleRetranslatePartner}
              disabled={translatingPartner}
              title="Dịch lại sang tiếng Anh"
              className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-bold text-gray-500 hover:bg-gray-100 disabled:opacity-50"
            >
              <RotateCw className={`w-3.5 h-3.5 ${translatingPartner ? 'animate-spin' : ''}`} />
              {translatingPartner ? 'Đang dịch...' : 'Dịch lại'}
            </button>
          )}
          <button
            type="button"
            onClick={() => setBilingual(v => !v)}
            title={bilingual ? 'Tắt soạn song ngữ' : 'Bật soạn song ngữ Việt – Anh'}
            className={`p-1.5 rounded-lg transition-colors ${bilingual ? 'bg-[#004c91]/10 text-[#004c91]' : 'text-gray-400 hover:bg-gray-100'}`}
          >
            <Languages className="w-4 h-4" />
          </button>
        </div>
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
          <div className="md:col-span-2 grid grid-cols-1 sm:grid-cols-2 gap-5">
            <div>
              <label className={labelCls}>Logo</label>
              <div className="flex items-center gap-4">
                <div className="w-20 h-20 rounded-lg border border-gray-200 bg-gray-50 flex items-center justify-center overflow-hidden flex-shrink-0">
                  {logoPreview
                    ? <SmartImage src={logoPreview} alt="Logo đối tác" className="w-full h-full object-cover" />
                    : <ImageIcon className="w-6 h-6 text-gray-300" />}
                </div>
                <label className={`inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-gray-300 text-xs font-bold text-gray-600 hover:bg-gray-50 ${submitting ? 'opacity-50' : 'cursor-pointer'}`}>
                  <Upload className="w-3.5 h-3.5" />
                  {logoPreview ? 'Đổi ảnh' : 'Chọn ảnh'}
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    className="hidden"
                    disabled={submitting}
                    onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ''; handleImageSelect('logo', f); }}
                  />
                </label>
              </div>
            </div>
            <div>
              <label className={labelCls}>Ảnh bìa</label>
              <div className="flex items-center gap-4">
                <div className="w-32 h-20 rounded-lg border border-gray-200 bg-gray-50 flex items-center justify-center overflow-hidden flex-shrink-0">
                  {coverPreview
                    ? <SmartImage src={coverPreview} alt="Ảnh bìa đối tác" className="w-full h-full object-cover" />
                    : <ImageIcon className="w-6 h-6 text-gray-300" />}
                </div>
                <label className={`inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-gray-300 text-xs font-bold text-gray-600 hover:bg-gray-50 ${submitting ? 'opacity-50' : 'cursor-pointer'}`}>
                  <Upload className="w-3.5 h-3.5" />
                  {coverPreview ? 'Đổi ảnh' : 'Chọn ảnh'}
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    className="hidden"
                    disabled={submitting}
                    onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ''; handleImageSelect('cover', f); }}
                  />
                </label>
              </div>
            </div>
          </div>
          <div className="md:col-span-2">
            <label className={labelCls}>
              Tên đối tác *{bilingual && <LanguageColumnLabel>VI</LanguageColumnLabel>}
            </label>
            <BilingualColumns
              showEnglish={bilingual}
              left={
                <>
                  <input className={inputCls} value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
                  {duplicateHint && (
                    <p className="mt-1.5 text-xs font-normal text-amber-600 flex items-center gap-1">
                      <AlertTriangle className="w-3.5 h-3.5" />
                      Có thể trùng với đối tác đã tồn tại: <b>{duplicateHint.partnerName}</b> ({duplicateHint.reason})
                    </p>
                  )}
                </>
              }
              right={
                <div>
                  <label className={labelCls}>Name <LanguageColumnLabel>EN</LanguageColumnLabel></label>
                  <input
                    className={inputCls}
                    value={englishName}
                    onChange={(e) => { setEnglishName(e.target.value); setEnglishNameTouched(true); }}
                    maxLength={200}
                    placeholder="Partner name (English)..."
                  />
                </div>
              }
            />
          </div>
          <div>
            <label className={labelCls}>Mã đối tác</label>
            <input className={inputCls} value={partnerCode} onChange={(e) => setPartnerCode(e.target.value)} maxLength={50} placeholder="VD: DEAKIN" />
          </div>
          <div className={bilingual ? 'md:col-span-2' : undefined}>
            <label className={labelCls}>
              Tên viết tắt{bilingual && <LanguageColumnLabel>VI</LanguageColumnLabel>}
            </label>
            <BilingualColumns
              showEnglish={bilingual}
              left={<input className={inputCls} value={shortName} onChange={(e) => setShortName(e.target.value)} maxLength={100} />}
              right={
                <div>
                  <label className={labelCls}>Short name <LanguageColumnLabel>EN</LanguageColumnLabel></label>
                  <input
                    className={inputCls}
                    value={englishShortName}
                    onChange={(e) => { setEnglishShortName(e.target.value); setEnglishShortNameTouched(true); }}
                    maxLength={100}
                    placeholder="Short name (English)..."
                  />
                </div>
              }
            />
          </div>
          <div>
            <label className={labelCls}>Quốc gia</label>
            <CountrySelect
              storeLang="vi"
              value={country}
              onChange={handleCountryChange}
              placeholder="Chọn hoặc nhập quốc gia..."
            />
          </div>
          <div>
            <label className={labelCls}>Thành phố</label>
            <CitySelect
              country={country}
              value={city}
              onChange={setCity}
              placeholder="Chọn hoặc nhập thành phố..."
            />
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
            <label className={labelCls}>
              Địa chỉ{bilingual && <LanguageColumnLabel>VI</LanguageColumnLabel>}
            </label>
            <BilingualColumns
              showEnglish={bilingual}
              left={<input className={inputCls} value={address} onChange={(e) => setAddress(e.target.value)} maxLength={500} />}
              right={
                <div>
                  <label className={labelCls}>Address <LanguageColumnLabel>EN</LanguageColumnLabel></label>
                  <input
                    className={inputCls}
                    value={englishAddress}
                    onChange={(e) => { setEnglishAddress(e.target.value); setEnglishAddressTouched(true); }}
                    maxLength={500}
                    placeholder="Address (English)..."
                  />
                </div>
              }
            />
          </div>
          <div className="md:col-span-2">
            <label className={labelCls}>
              Mô tả{bilingual && <LanguageColumnLabel>VI</LanguageColumnLabel>}
            </label>
            <BilingualColumns
              showEnglish={bilingual}
              left={<textarea className={inputCls} rows={4} value={description} onChange={(e) => setDescription(e.target.value)} />}
              right={
                <div>
                  <label className={labelCls}>Description <LanguageColumnLabel>EN</LanguageColumnLabel></label>
                  <textarea
                    className={inputCls}
                    rows={4}
                    value={englishDescription}
                    onChange={(e) => { setEnglishDescription(e.target.value); setEnglishDescriptionTouched(true); }}
                    placeholder="Description (English)..."
                  />
                </div>
              }
            />
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
