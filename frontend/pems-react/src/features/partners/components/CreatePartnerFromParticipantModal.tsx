/**
 * CreatePartnerFromParticipantModal — "Tạo hoặc liên kết đối tác" từ một dòng người
 * tham gia biên bản (docs/PARTNER_canh/01 §7 + §10.3).
 *
 * Nguyên tắc: KHÔNG bao giờ tạo đối tác trùng tên. Backend đối chiếu (alias / tên chuẩn
 * hoá / fuzzy / email-domain) và trả về danh sách đối tác ứng viên kèm điểm khớp; modal
 * hiển thị danh sách để người dùng chọn LIÊN KẾT thay vì tạo trùng. Nếu vẫn bấm "Vẫn tạo
 * mới" mà backend trả 409 thì modal đối chiếu lại và mời liên kết, không dead-end. Backend
 * tự set owner_campus_id theo campus người dùng.
 */
import React, { useEffect, useRef, useState } from 'react';
import { Link2, Loader2, X } from 'lucide-react';
import { partnersApi } from '../api/partnersApi';
import {
  PARTNER_TYPE_LABELS, PROFILE_STATUS_LABELS, VISIBILITY_LABELS,
  type PartnerType, type PartnerMatchResult, type PartnerMatchCandidate,
  type PartnerProfileStatus, type PartnerVisibility,
} from '../types/partners.types';

interface Prefill {
  /** Ưu tiên làm tên đối tác. */
  organization?: string | null;
  contactName?: string | null;
  contactEmail?: string | null;
  jobTitle?: string | null;
  /** Nguồn để ghi vào mô tả: "Tạo từ biên bản …". */
  sourceLabel?: string | null;
}

interface Props {
  open: boolean;
  onClose: () => void;
  visitInstanceId: number;
  guestMemberId?: number | null;
  minuteParticipantId?: number | null;
  prefill?: Prefill;
  /** Gọi sau khi tạo mới HOẶC liên kết thành công — cha đóng modal + refetch links. */
  onDone: () => void;
}

const PARTNER_TYPES = Object.keys(PARTNER_TYPE_LABELS) as PartnerType[];

const STATUS_CLS: Record<string, string> = {
  APPROVED: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  PENDING_APPROVAL: 'bg-amber-50 text-amber-700 border-amber-200',
  REJECTED: 'bg-red-50 text-red-700 border-red-200',
  DRAFT: 'bg-slate-100 text-slate-600 border-slate-200',
};

/** Phân loại điểm khớp: >=90 Khớp cao, 70–89 Có thể trùng, <70 Gợi ý tham khảo. */
function scoreTier(score: number): { label: string; cls: string } {
  if (score >= 90) return { label: 'Khớp cao', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' };
  if (score >= 70) return { label: 'Có thể trùng', cls: 'bg-amber-50 text-amber-700 border-amber-200' };
  return { label: 'Gợi ý tham khảo', cls: 'bg-slate-100 text-slate-600 border-slate-200' };
}

/** Suy ra danh sách ứng viên từ match (fallback về best-match nếu backend chưa trả candidates). */
function deriveCandidates(m: PartnerMatchResult | null): PartnerMatchCandidate[] {
  if (!m) return [];
  if (m.candidates && m.candidates.length > 0) return m.candidates;
  if (m.partnerId) {
    return [{
      partnerId: m.partnerId,
      name: m.partnerName ?? '',
      profileStatus: (m.profileStatus ?? 'PENDING_APPROVAL') as PartnerProfileStatus,
      visibility: 'INTERNAL' as PartnerVisibility,
      ownerCampusId: 0,
      ownerCampusName: null,
      country: null,
      city: null,
      matchScore: m.confidence ?? 0,
      matchReason: m.reason ?? null,
      canLink: true,
    }];
  }
  return [];
}

export function CreatePartnerFromParticipantModal({
  open, onClose, visitInstanceId, guestMemberId, minuteParticipantId, prefill, onDone,
}: Props) {
  const [name, setName] = useState('');
  const [partnerType, setPartnerType] = useState<PartnerType>('UNIVERSITY');
  const [country, setCountry] = useState('');
  const [city, setCity] = useState('');
  const [websiteUrl, setWebsiteUrl] = useState('');
  const [address, setAddress] = useState('');
  const [description, setDescription] = useState('');
  const [busy, setBusy] = useState(false);
  const [linkingId, setLinkingId] = useState<number | null>(null);
  const [checking, setChecking] = useState(false);
  const [match, setMatch] = useState<PartnerMatchResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const candidatesRef = useRef<HTMLDivElement | null>(null);

  const runMatch = async (org?: string | null, email?: string | null): Promise<PartnerMatchResult | null> => {
    const orgTrim = org?.trim();
    if (!orgTrim && !email?.trim()) { setMatch(null); return null; }
    setChecking(true);
    try {
      const res = await partnersApi.matchPartner(orgTrim || undefined, email?.trim() || undefined);
      const found = res && (res.matchStatus !== 'NONE' || (res.candidates?.length ?? 0) > 0) ? res : null;
      setMatch(found);
      return found;
    } catch {
      // Đối chiếu chỉ là gợi ý — lỗi ở đây không chặn luồng tạo.
      setMatch(null);
      return null;
    } finally {
      setChecking(false);
    }
  };

  // Prefill + đối chiếu sẵn mỗi lần mở modal cho một dòng khác nhau.
  useEffect(() => {
    if (!open) return;
    setName(prefill?.organization?.trim() || '');
    setPartnerType('UNIVERSITY');
    setCountry('');
    setCity('');
    setWebsiteUrl('');
    setAddress('');
    setDescription(prefill?.sourceLabel ? `Tạo từ ${prefill.sourceLabel}` : '');
    setError(null);
    setConflict(false);
    setBusy(false);
    setLinkingId(null);
    setMatch(null);
    void runMatch(prefill?.organization, prefill?.contactEmail);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, prefill?.organization, prefill?.contactEmail, prefill?.sourceLabel]);

  if (!open) return null;

  const trimmedName = name.trim();
  const noOrganization = !prefill?.organization?.trim();
  const anyBusy = busy || linkingId !== null || checking;
  const candidates = deriveCandidates(match);

  const linkExisting = async (partnerId: number) => {
    setLinkingId(partnerId);
    setError(null);
    try {
      await partnersApi.linkGuestToPartner(visitInstanceId, {
        guestMemberId: guestMemberId || null,
        minuteParticipantId: (minuteParticipantId ?? 0) > 0 ? minuteParticipantId : null,
        partnerId,
        matchSource: 'MANUAL',
        matchStatus: 'CONFIRMED',
      });
      onDone();
    } catch (e: any) {
      const status = e?.response?.status;
      setError(
        status === 403
          ? 'Bạn không có quyền liên kết với đối tác này hoặc đối tác nằm ngoài phạm vi cơ sở của bạn.'
          : (e?.response?.data?.message || 'Không thể liên kết đối tác. Vui lòng thử lại.'),
      );
      setLinkingId(null);
    }
  };

  const submit = async () => {
    if (!trimmedName) {
      setError('Vui lòng nhập tên đối tác.');
      return;
    }
    setBusy(true);
    setError(null);
    setConflict(false);
    try {
      await partnersApi.createPartnerFromGuest(visitInstanceId, {
        guestMemberId: guestMemberId || null,
        minuteParticipantId: (minuteParticipantId ?? 0) > 0 ? minuteParticipantId : null,
        partnerName: trimmedName,
        partnerType,
        country: country.trim() || null,
        city: city.trim() || null,
        websiteUrl: websiteUrl.trim() || null,
        address: address.trim() || null,
        description: description.trim() || null,
        contactEmail: prefill?.contactEmail?.trim() || null,
      });
      onDone();
    } catch (e: any) {
      const code = e?.response?.data?.errorCode;
      const status = e?.response?.status;
      // Trùng tên → KHÔNG tạo trùng: đối chiếu lại và mời liên kết với đối tác đã có.
      if (code === 'PARTNER_NAME_DUPLICATED' || status === 409) {
        setConflict(true);
        await runMatch(trimmedName, prefill?.contactEmail);
        setError('Tên đối tác đã tồn tại. Vui lòng liên kết với hồ sơ có sẵn ở trên hoặc đổi sang tên tổ chức khác.');
        setBusy(false);
        // Đưa người dùng lên khu vực gợi ý liên kết.
        setTimeout(() => candidatesRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 50);
        return;
      }
      setError(
        e?.response?.data?.message
          || 'Không tạo được đối tác. Vui lòng kiểm tra lại thông tin và thử lại.',
      );
      setBusy(false);
    }
  };

  const statusBadge = (status: string) => (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border whitespace-nowrap ${STATUS_CLS[status] ?? STATUS_CLS.DRAFT}`}>
      {PROFILE_STATUS_LABELS[status as PartnerProfileStatus] ?? status}
    </span>
  );

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 p-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-4xl max-h-[88vh] flex flex-col">
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 rounded-t-2xl">
          <h3 className="text-lg font-bold text-gray-800">Tạo hoặc liên kết đối tác</h3>
          <button onClick={onClose} disabled={anyBusy} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors disabled:opacity-40">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="px-6 py-5 space-y-4 overflow-y-auto">
          {prefill?.contactName && (
            <p className="text-xs text-gray-500 bg-gray-50 border border-gray-100 rounded-lg px-3 py-2">
              Người đại diện: <span className="font-semibold text-gray-700">{prefill.contactName}</span>
              {prefill.jobTitle ? ` — ${prefill.jobTitle}` : ''}
              {prefill.contactEmail ? ` · ${prefill.contactEmail}` : ''}
            </p>
          )}

          {checking && candidates.length === 0 && (
            <p className="text-xs text-gray-400 inline-flex items-center gap-1.5">
              <Loader2 className="w-3.5 h-3.5 animate-spin" /> Đang kiểm tra đối tác trùng lặp...
            </p>
          )}

          {/* Panel ứng viên — chọn để liên kết thay vì tạo trùng. */}
          {candidates.length > 0 && (
            <div
              ref={candidatesRef}
              className={`rounded-xl border p-4 ${conflict ? 'bg-amber-50 border-amber-300' : 'bg-slate-50 border-slate-200'}`}
            >
              <p className="text-sm font-bold text-gray-800">
                {candidates.length === 1
                  ? 'Đã tìm thấy 1 đối tác có thể liên quan'
                  : `Đã tìm thấy ${candidates.length} đối tác có thể liên quan`}
              </p>

              <div className="mt-3 space-y-2">
                {candidates.map((c) => {
                  const tier = scoreTier(c.matchScore);
                  const meta = [
                    c.ownerCampusName ? `Cơ sở: ${c.ownerCampusName}` : null,
                    c.visibility ? `Hiển thị: ${VISIBILITY_LABELS[c.visibility] ?? c.visibility}` : null,
                    c.country || null,
                  ].filter(Boolean).join(' · ');
                  return (
                    <div key={c.partnerId} className="flex items-center gap-3 rounded-lg border border-gray-200 bg-white px-3 py-2">
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-1.5">
                          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border whitespace-nowrap ${tier.cls}`}>
                            {tier.label} · {Math.round(c.matchScore)}%
                          </span>
                          {statusBadge(c.profileStatus)}
                        </div>
                        <div className="mt-1 truncate text-sm font-semibold text-slate-800" title={c.name}>
                          {c.name}
                        </div>
                        {meta && <div className="truncate text-xs text-gray-500" title={meta}>{meta}</div>}
                      </div>
                      <button
                        onClick={() => void linkExisting(c.partnerId)}
                        disabled={anyBusy || !c.canLink}
                        title={!c.canLink ? 'Đối tác nằm ngoài phạm vi cơ sở của bạn' : undefined}
                        className="shrink-0 inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-bold text-white bg-[#004c91] hover:bg-[#00386b] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                      >
                        {linkingId === c.partnerId ? <Loader2 className="w-4 h-4 animate-spin" /> : <Link2 className="w-4 h-4" />}
                        {linkingId === c.partnerId ? 'Đang liên kết...' : 'Liên kết'}
                      </button>
                    </div>
                  );
                })}
              </div>

              <p className="mt-3 text-[11px] leading-relaxed text-gray-500">
                Điểm khớp được hệ thống tính từ tên tổ chức, tên gọi khác (alias) và tên miền email (nếu có).
                Vui lòng kiểm tra trước khi liên kết.
              </p>
            </div>
          )}

          {noOrganization && candidates.length === 0 && (
            <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
              Dòng này chưa có đơn vị/tổ chức. Vui lòng nhập tên đối tác thật (tên tổ chức), không dùng tên cá nhân.
            </p>
          )}

          {/* Form tạo mới */}
          <div className="space-y-4 pt-1">
            {candidates.length > 0 && (
              <p className="text-xs font-semibold text-gray-500 border-t border-gray-100 pt-3">
                Hoặc tạo một đối tác mới (chỉ khi đây là tổ chức khác với các gợi ý ở trên):
              </p>
            )}

            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1">
                Tên đối tác <span className="text-red-500">*</span>
              </label>
              <input
                value={name}
                onChange={(e) => { setName(e.target.value); setConflict(false); }}
                onBlur={() => { if (name.trim()) void runMatch(name, prefill?.contactEmail); }}
                placeholder="VD: Đại học Quốc gia Singapore"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700"
              />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1">Loại đối tác</label>
                <select
                  value={partnerType}
                  onChange={(e) => setPartnerType(e.target.value as PartnerType)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] bg-white text-gray-700"
                >
                  {PARTNER_TYPES.map((t) => <option key={t} value={t}>{PARTNER_TYPE_LABELS[t]}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1">Quốc gia</label>
                <input
                  value={country}
                  onChange={(e) => setCountry(e.target.value)}
                  placeholder="VD: Singapore"
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] text-gray-700"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1">Thành phố</label>
                <input
                  value={city}
                  onChange={(e) => setCity(e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] text-gray-700"
                />
              </div>
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1">Website</label>
                <input
                  value={websiteUrl}
                  onChange={(e) => setWebsiteUrl(e.target.value)}
                  placeholder="https://..."
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] text-gray-700"
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1">Địa chỉ</label>
              <input
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] text-gray-700"
              />
            </div>

            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1">Mô tả</label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={2}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] text-gray-700"
              />
            </div>

            <p className="text-xs text-gray-400">
              Đối tác mới sẽ được tạo ở trạng thái <span className="font-semibold">Chờ duyệt</span> thuộc cơ sở của bạn và tự động liên kết với người tham gia này.
            </p>
          </div>

          {error && (
            <div className={`text-sm rounded-lg px-3 py-2.5 border ${conflict ? 'bg-amber-50 border-amber-200 text-amber-800' : 'bg-red-50 border-red-100 text-red-600'}`}>
              {error}
            </div>
          )}
        </div>

        <div className="flex flex-wrap items-center justify-end gap-2 px-6 py-4 border-t border-gray-100 rounded-b-2xl">
          {candidates.length > 0 && (
            <span className="mr-auto text-xs text-gray-400">
              Chỉ tạo mới nếu đây là một tổ chức khác với các gợi ý phía trên.
            </span>
          )}
          <button
            onClick={onClose}
            disabled={anyBusy}
            className="px-4 py-2 rounded-lg text-sm font-bold text-gray-500 hover:bg-gray-100 transition-colors disabled:opacity-40"
          >
            Huỷ
          </button>
          <button
            onClick={() => void submit()}
            disabled={!trimmedName || anyBusy}
            className="px-4 py-2 rounded-lg text-sm font-bold text-white bg-[#f37021] hover:bg-[#d9621a] transition-colors disabled:opacity-50 inline-flex items-center gap-1.5"
          >
            {busy && <Loader2 className="w-4 h-4 animate-spin" />}
            {busy ? 'Đang tạo...' : (candidates.length > 0 ? 'Vẫn tạo mới' : 'Tạo đối tác')}
          </button>
        </div>
      </div>
    </div>
  );
}
