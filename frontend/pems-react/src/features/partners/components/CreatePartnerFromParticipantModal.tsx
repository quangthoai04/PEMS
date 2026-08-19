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
import { ChevronUp, ExternalLink, Info, Link2, Loader2, X } from 'lucide-react';
import { partnersApi } from '../api/partnersApi';
import {
  getApiErrorMessage,
  showLoadingToast,
  updateToastSuccess,
  updateToastMessageError,
  dismissToast,
} from '../../../shared/utils/toast';
import {
  PARTNER_LINK_BLOCKED_LABELS, PARTNER_TYPE_LABELS, PROFILE_STATUS_LABELS, VISIBILITY_LABELS,
  type PartnerDetail, type PartnerType, type PartnerMatchResult, type PartnerMatchCandidate,
  type PartnerProfileStatus, type PartnerVisibility,
} from '../types/partners.types';
import { formatVietnamDate } from '../../../shared/utils/vietnamTime';
import { CountrySelect } from '../../visit-request/components/shared/CountrySelect';
import { fieldErrorsOf, firstFieldError } from '../../visit-request/utils/visitV2Actions';
import { focusFirstInvalidField } from '../../visit-request/utils/formErrorNavigation';
import { CitySelect } from './CitySelect';

type CreatePartnerFieldKey = 'name' | 'websiteUrl';
type CreatePartnerFieldErrors = Partial<Record<CreatePartnerFieldKey, string>>;
/** Tên property C# đúng như `CreatePartnerCommandValidator` trả về trong `errors` dict. */
const CREATE_PARTNER_FIELD_BACKEND_MAP: Record<CreatePartnerFieldKey, string> = {
  name: 'Name', websiteUrl: 'WebsiteUrl',
};

/** Mirror đúng rule backend (`Uri.TryCreate`, thêm https:// nếu chưa có scheme) — chỉ để UX. */
function isValidWebsiteUrl(url: string): boolean {
  const trimmed = url.trim();
  if (!trimmed) return true;
  try {
    // eslint-disable-next-line no-new
    new URL(trimmed.includes('://') ? trimmed : `https://${trimmed}`);
    return true;
  } catch {
    return false;
  }
}

interface Prefill {
  /** Ưu tiên làm tên đối tác. */
  organization?: string | null;
  contactName?: string | null;
  contactEmail?: string | null;
  jobTitle?: string | null;
  /** Quốc tịch của khách (nếu có) — giá trị khởi tạo cho Quốc gia, vẫn sửa được. */
  nationality?: string | null;
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
    // Legacy shape: only the best match came back, with no per-candidate link policy. Assume the
    // SAFE answer rather than the convenient one — a rejected or draft profile is never linkable, so
    // defaulting this to `true` would put the button back exactly where PART-04 removed it. The
    // backend re-checks on click regardless.
    const status = (m.profileStatus ?? 'PENDING_APPROVAL') as PartnerProfileStatus;
    const linkable = status === 'APPROVED' || status === 'PENDING_APPROVAL';
    return [{
      partnerId: m.partnerId,
      name: m.partnerName ?? '',
      profileStatus: status,
      visibility: 'INTERNAL' as PartnerVisibility,
      ownerCampusId: 0,
      ownerCampusName: null,
      country: null,
      city: null,
      matchScore: m.confidence ?? 0,
      matchReason: m.reason ?? null,
      canLink: linkable,
      blockedReason: linkable ? null : (status === 'REJECTED' ? 'PARTNER_REJECTED' : 'PARTNER_DRAFT'),
      recommendedAction: linkable ? 'LINK' : (status === 'REJECTED' ? 'RESUBMIT' : 'NONE'),
    }];
  }
  return [];
}

/** Chưa cập nhật cho field trống — không để dấu "-" hay ô rỗng làm UI xấu. */
function orNotUpdated(v?: string | null): string {
  return v && v.trim() ? v.trim() : 'Chưa cập nhật';
}

function fmtDate(iso?: string | null): string {
  return formatVietnamDate(iso, { fallback: 'Chưa cập nhật' });
}

function withProtocol(url: string): string {
  return /^https?:\/\//i.test(url) ? url : `https://${url}`;
}

function DetailRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-[11px] font-medium text-gray-400">{label}</dt>
      <dd className="text-xs text-gray-700 break-words">{children}</dd>
    </div>
  );
}

/**
 * Inline panel mở dưới một candidate (Hướng A — không lồng modal trong modal).
 * Xem kỹ trước khi liên kết. Detail lấy từ internal API GET /api/partners/{id}
 * (đã enforce quyền xem theo campus); trong lúc chờ, hiển thị tạm field từ candidate.
 * Bấm "Chi tiết" KHÔNG tạo/liên kết gì — chỉ liên kết khi bấm nút bên dưới.
 */
function CandidateDetailPanel({
  candidate, detail, loading, error, linking, busy, onLink,
}: {
  candidate: PartnerMatchCandidate;
  detail: PartnerDetail | null;
  loading: boolean;
  error: string | null;
  linking: boolean;
  busy: boolean;
  onLink: () => void;
}) {
  const tier = scoreTier(candidate.matchScore);
  const code = detail?.partnerCode && detail.partnerCode.trim() ? detail.partnerCode.trim() : 'Chưa có';
  // Trước khi detail tải xong, fallback về dữ liệu đã có trong candidate.
  const shortName = detail?.shortName ?? candidate.shortName;
  const country = detail?.country ?? candidate.country;
  const city = detail?.city ?? candidate.city;
  const status = detail?.profileStatus ?? candidate.profileStatus;
  const visibility = detail?.visibility ?? candidate.visibility;
  const campus = detail?.ownerCampusName ?? candidate.ownerCampusName;
  const website = detail?.websiteUrl?.trim();
  const profileHref = `/dashboard/partners/${candidate.partnerId}`;

  return (
    <div className="border-t border-gray-100 bg-slate-50/70 px-4 py-3.5 space-y-3">
      {loading && (
        <p className="text-xs text-gray-400 inline-flex items-center gap-1.5">
          <Loader2 className="w-3.5 h-3.5 animate-spin" /> Đang tải chi tiết đối tác...
        </p>
      )}

      {!loading && error && (
        <div className="text-xs rounded-lg px-3 py-2 border bg-red-50 border-red-100 text-red-600">
          {error}
        </div>
      )}

      {!loading && !error && (
        <div>
          <p className="text-[11px] font-bold uppercase tracking-wide text-gray-400 mb-1.5">Thông tin đối tác</p>
          <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-2">
            <DetailRow label="Mã đối tác">{code}</DetailRow>
            <DetailRow label="Tên viết tắt">{orNotUpdated(shortName)}</DetailRow>
            <DetailRow label="Trạng thái">
              {PROFILE_STATUS_LABELS[status as PartnerProfileStatus] ?? status}
            </DetailRow>
            <DetailRow label="Hiển thị">
              {VISIBILITY_LABELS[visibility as PartnerVisibility] ?? visibility ?? 'Chưa cập nhật'}
            </DetailRow>
            <DetailRow label="Cơ sở sở hữu">{orNotUpdated(campus)}</DetailRow>
            <DetailRow label="Loại đối tác">
              {detail ? (PARTNER_TYPE_LABELS[detail.partnerType] ?? detail.partnerType) : 'Chưa cập nhật'}
            </DetailRow>
            <DetailRow label="Quốc gia">{orNotUpdated(country)}</DetailRow>
            <DetailRow label="Thành phố">{orNotUpdated(city)}</DetailRow>
            <DetailRow label="Website">
              {website ? (
                <a
                  href={withProtocol(website)}
                  target="_blank"
                  rel="noreferrer"
                  className="text-[#004c91] hover:underline inline-flex items-center gap-1 break-all"
                >
                  {website} <ExternalLink className="w-3 h-3 shrink-0" />
                </a>
              ) : 'Chưa cập nhật'}
            </DetailRow>
            <DetailRow label="Ngày tạo">{fmtDate(detail?.createdAt)}</DetailRow>
            {detail?.creatorName && <DetailRow label="Người tạo">{detail.creatorName}</DetailRow>}
            <div className="sm:col-span-2">
              <DetailRow label="Địa chỉ">{orNotUpdated(detail?.address)}</DetailRow>
            </div>
            <div className="sm:col-span-2">
              <DetailRow label="Mô tả">{orNotUpdated(detail?.description)}</DetailRow>
            </div>
          </dl>
        </div>
      )}

      {/* Vì sao được gợi ý — luôn có từ candidate, không phụ thuộc detail. */}
      <div className="rounded-lg border border-slate-200 bg-white px-3 py-2">
        <p className="text-[11px] font-bold uppercase tracking-wide text-gray-400 mb-1">Vì sao được gợi ý</p>
        <div className="flex flex-wrap items-center gap-2 text-xs text-gray-600">
          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border ${tier.cls}`}>
            {tier.label}
          </span>
          {candidate.matchReason && <span>Lý do: {candidate.matchReason}</span>}
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2 pt-0.5">
        {/* Opening the full profile is a READ, so it is offered whether or not linking is allowed —
            "xem lý do" is exactly what a blocked candidate needs (PART-04). */}
        <a
          href={profileHref}
          target="_blank"
          rel="noreferrer"
          className="mr-auto text-xs font-semibold text-slate-500 hover:text-[#004c91] inline-flex items-center gap-1"
        >
          {candidate.canLink ? 'Mở hồ sơ đầy đủ' : 'Xem lý do / hồ sơ'} <ExternalLink className="w-3 h-3" />
        </a>
        {candidate.canLink ? (
          <button
            onClick={onLink}
            disabled={busy}
            className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-bold text-white bg-[#004c91] hover:bg-[#00386b] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {linking ? <Loader2 className="w-4 h-4 animate-spin" /> : <Link2 className="w-4 h-4" />}
            {linking ? 'Đang liên kết...' : 'Liên kết đối tác này'}
          </button>
        ) : (
          <span className="text-[11px] font-normal text-amber-700 max-w-[22rem] text-right">
            {candidate.blockedReason
              ? PARTNER_LINK_BLOCKED_LABELS[candidate.blockedReason]
              : 'Bạn không có quyền liên kết đối tác này.'}
          </span>
        )}
      </div>
    </div>
  );
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
  const [fieldErrors, setFieldErrors] = useState<CreatePartnerFieldErrors>({});
  // Xem chi tiết candidate (inline expand). Chỉ mở 1 panel/lần cho gọn.
  const [detailOpenId, setDetailOpenId] = useState<number | null>(null);
  const [detailCache, setDetailCache] = useState<Record<number, PartnerDetail>>({});
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const candidatesRef = useRef<HTMLDivElement | null>(null);

  const runMatch = async (org?: string | null, email?: string | null): Promise<PartnerMatchResult | null> => {
    // Danh sách candidate sắp đổi → đóng panel chi tiết đang mở để tránh lệch dữ liệu.
    setDetailOpenId(null);
    setDetailError(null);
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
    setCountry(prefill?.nationality?.trim() || '');
    setCity('');
    setWebsiteUrl('');
    setAddress('');
    setDescription(prefill?.sourceLabel ? `Tạo từ ${prefill.sourceLabel}` : '');
    setError(null);
    setConflict(false);
    setFieldErrors({});
    setBusy(false);
    setLinkingId(null);
    setMatch(null);
    setDetailOpenId(null);
    setDetailCache({});
    setDetailError(null);
    setDetailLoading(false);
    void runMatch(prefill?.organization, prefill?.contactEmail);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, prefill?.organization, prefill?.contactEmail, prefill?.nationality, prefill?.sourceLabel]);

  if (!open) return null;

  const trimmedName = name.trim();
  const noOrganization = !prefill?.organization?.trim();
  const anyBusy = busy || linkingId !== null || checking;
  const candidates = deriveCandidates(match);

  // Mở/đóng panel chi tiết của một candidate. Lazy-load detail (cache theo partnerId),
  // KHÔNG tạo/liên kết gì. Lỗi quyền/không tìm thấy được xử lý cục bộ trong panel.
  const toggleDetail = async (partnerId: number) => {
    if (detailOpenId === partnerId) { setDetailOpenId(null); return; }
    setDetailOpenId(partnerId);
    setDetailError(null);
    if (detailCache[partnerId]) { setDetailLoading(false); return; }
    setDetailLoading(true);
    try {
      const d = await partnersApi.getPartnerDetail(partnerId);
      setDetailCache((prev) => ({ ...prev, [partnerId]: d }));
    } catch (e: any) {
      const status = e?.response?.status;
      setDetailError(
        status === 403
          ? 'Bạn không có quyền xem chi tiết đối tác này.'
          : status === 404
            ? 'Không tìm thấy đối tác.'
            : 'Không thể tải chi tiết đối tác. Vui lòng thử lại.',
      );
    } finally {
      setDetailLoading(false);
    }
  };

  const linkExisting = async (partnerId: number) => {
    setLinkingId(partnerId);
    setError(null);
    const toastId = showLoadingToast('Đang liên kết đối tác...', 'partner-guest-link');
    try {
      await partnersApi.linkGuestToPartner(visitInstanceId, {
        guestMemberId: guestMemberId || null,
        minuteParticipantId: (minuteParticipantId ?? 0) > 0 ? minuteParticipantId : null,
        partnerId,
        matchSource: 'MANUAL',
        matchStatus: 'CONFIRMED',
      });
      updateToastSuccess(toastId, 'Đã liên kết đối tác.');
      onDone();
    } catch (e: any) {
      const status = e?.response?.status;
      const message = status === 403
        ? 'Bạn không có quyền liên kết với đối tác này hoặc đối tác nằm ngoài phạm vi cơ sở của bạn.'
        : getApiErrorMessage(e, 'Không thể liên kết đối tác. Vui lòng thử lại.');
      setError(message);
      updateToastMessageError(toastId, message);
      setLinkingId(null);
    }
  };

  /** Mirror phía client của rule backend (`CreatePartnerCommandValidator`) — chỉ để UX. */
  const validateCreateForm = (): CreatePartnerFieldErrors => {
    const errors: CreatePartnerFieldErrors = {};
    if (!trimmedName) errors.name = 'Vui lòng nhập tên đối tác.';
    if (websiteUrl.trim() && !isValidWebsiteUrl(websiteUrl)) errors.websiteUrl = 'Website không hợp lệ.';
    return errors;
  };

  /** Xoá lỗi của MỘT field ngay khi nó hợp lệ trở lại — không đợi submit lại. */
  const clearFieldError = (key: CreatePartnerFieldKey, value: string) => {
    if (!fieldErrors[key]) return;
    if (key === 'name' && !value.trim()) return;
    if (key === 'websiteUrl' && value.trim() && !isValidWebsiteUrl(value)) return;
    setFieldErrors((prev) => ({ ...prev, [key]: undefined }));
  };

  const submit = async () => {
    const clientErrors = validateCreateForm();
    if (Object.keys(clientErrors).length > 0) {
      setFieldErrors(clientErrors);
      window.setTimeout(() => focusFirstInvalidField(), 60);
      return;
    }
    setFieldErrors({});
    setBusy(true);
    setError(null);
    setConflict(false);
    const toastId = showLoadingToast('Đang tạo hồ sơ đối tác...', 'partner-guest-create');
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
      updateToastSuccess(toastId, 'Đã tạo hồ sơ đối tác thành công.');
      onDone();
    } catch (e: any) {
      const code = e?.response?.data?.errorCode;
      const status = e?.response?.status;
      // Trùng tên → KHÔNG tạo trùng: đối chiếu lại và mời liên kết với đối tác đã có.
      // Đây là luồng hướng dẫn (không phải lỗi cứng) nên đóng toast loading và để UI gợi ý
      // liên kết inline dẫn dắt, tránh toast đỏ gây hiểu nhầm.
      if (code === 'PARTNER_NAME_DUPLICATED' || status === 409) {
        setConflict(true);
        dismissToast(toastId);
        await runMatch(trimmedName, prefill?.contactEmail);
        setError('Tên đối tác đã tồn tại. Vui lòng liên kết với hồ sơ có sẵn ở trên hoặc đổi sang tên tổ chức khác.');
        setBusy(false);
        // Đưa người dùng lên khu vực gợi ý liên kết.
        setTimeout(() => candidatesRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 50);
        return;
      }
      // Lỗi field ổn định (FluentValidation) đi kèm form — chỉ dùng toast cho lỗi chung/mạng/conflict.
      const backendFields = fieldErrorsOf(e);
      const mapped: CreatePartnerFieldErrors = {};
      if (backendFields) {
        (Object.keys(CREATE_PARTNER_FIELD_BACKEND_MAP) as CreatePartnerFieldKey[]).forEach((key) => {
          const msg = firstFieldError(backendFields, CREATE_PARTNER_FIELD_BACKEND_MAP[key]);
          if (msg) mapped[key] = msg;
        });
      }
      if (Object.keys(mapped).length > 0) {
        setFieldErrors(mapped);
        dismissToast(toastId);
        setBusy(false);
        window.setTimeout(() => focusFirstInvalidField(), 60);
        return;
      }
      const message = getApiErrorMessage(
        e,
        'Không tạo được đối tác. Vui lòng kiểm tra lại thông tin và thử lại.',
      );
      setError(message);
      updateToastMessageError(toastId, message);
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
      {/* Overlay is a flat p-4 (2rem vertical gutter). */}
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-4xl max-h-[calc(100dvh-2rem)] flex flex-col">
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 rounded-t-2xl">
          <h3 className="text-lg font-bold text-gray-800">Tạo hoặc liên kết đối tác</h3>
          <button onClick={onClose} disabled={anyBusy} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors disabled:opacity-40">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="px-6 py-5 space-y-4 overflow-y-auto">
          {prefill?.contactName && (
            <p className="text-xs text-gray-500 bg-gray-50 border border-gray-100 rounded-lg px-3 py-2">
              Người đại diện: <span className="font-normal text-gray-700">{prefill.contactName}</span>
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
                  const expanded = detailOpenId === c.partnerId;
                  return (
                    <div key={c.partnerId} className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                      <div className="flex items-center gap-3 px-3 py-2">
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-1.5">
                            <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border whitespace-nowrap ${tier.cls}`}>
                              {tier.label}
                            </span>
                            {statusBadge(c.profileStatus)}
                          </div>
                          <div className="mt-1 truncate text-sm font-semibold text-slate-800" title={c.name}>
                            {c.name}
                          </div>
                          {meta && <div className="truncate text-xs text-gray-500" title={meta}>{meta}</div>}
                          {c.matchReason && (
                            <div className="truncate text-[11px] text-gray-400" title={c.matchReason}>
                              Lý do: {c.matchReason}
                            </div>
                          )}
                          {/* Why this candidate is here but cannot be linked. Shown on the ROW, where
                              the decision is made — not hidden behind "Chi tiết". */}
                          {!c.canLink && c.blockedReason && (
                            <div className="mt-1 text-[11px] font-normal text-amber-700">
                              {PARTNER_LINK_BLOCKED_LABELS[c.blockedReason]}
                              {c.reviewNote?.trim() && (
                                <span className="block font-normal text-amber-800">
                                  Lý do từ chối: {c.reviewNote.trim()}
                                </span>
                              )}
                            </div>
                          )}
                        </div>
                        <div className="flex shrink-0 items-center gap-2">
                          <button
                            onClick={() => void toggleDetail(c.partnerId)}
                            disabled={busy || linkingId !== null}
                            aria-expanded={expanded}
                            className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-xs font-semibold text-slate-600 border border-slate-200 hover:border-[#004c91] hover:text-[#004c91] transition-colors disabled:opacity-40"
                          >
                            {expanded ? <ChevronUp className="w-3.5 h-3.5" /> : <Info className="w-3.5 h-3.5" />}
                            Chi tiết
                          </button>
                          {/* A blocked candidate gets NO link button at all — not a disabled one.
                              A greyed-out "Liên kết" still reads as "the right action, temporarily
                              unavailable", when the right action for a rejected profile is to fix and
                              resubmit it (PART-04). */}
                          {c.canLink ? (
                            <button
                              onClick={() => void linkExisting(c.partnerId)}
                              disabled={anyBusy}
                              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-bold text-white bg-[#004c91] hover:bg-[#00386b] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                            >
                              {linkingId === c.partnerId ? <Loader2 className="w-4 h-4 animate-spin" /> : <Link2 className="w-4 h-4" />}
                              {linkingId === c.partnerId ? 'Đang liên kết...' : 'Liên kết'}
                            </button>
                          ) : c.recommendedAction === 'RESUBMIT' ? (
                            <a
                              href={`/dashboard/partners/${c.partnerId}`}
                              target="_blank"
                              rel="noreferrer"
                              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-bold text-amber-800 border border-amber-300 bg-amber-50 hover:bg-amber-100 transition-colors"
                            >
                              Chỉnh sửa và gửi duyệt lại <ExternalLink className="w-3.5 h-3.5" />
                            </a>
                          ) : null}
                        </div>
                      </div>
                      {expanded && (
                        <CandidateDetailPanel
                          candidate={c}
                          detail={detailCache[c.partnerId] ?? null}
                          loading={detailLoading}
                          error={detailError}
                          linking={linkingId === c.partnerId}
                          busy={anyBusy}
                          onLink={() => void linkExisting(c.partnerId)}
                        />
                      )}
                    </div>
                  );
                })}
              </div>

              <p className="mt-3 text-[11px] leading-relaxed text-gray-500">
                Điểm khớp được hệ thống tính từ tên tổ chức, tên gọi khác (alias), tên gần giống và tên miền email (nếu có).
                Đây chỉ là gợi ý — vui lòng kiểm tra trước khi liên kết.
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
              <p className="text-xs font-normal text-gray-500 border-t border-gray-100 pt-3">
                Hoặc tạo một đối tác mới (chỉ khi đây là tổ chức khác với các gợi ý ở trên):
              </p>
            )}

            <div data-field-error={fieldErrors.name ? 'true' : undefined}>
              <label htmlFor="cpfp-name" className="block text-sm font-semibold text-gray-700 mb-1">
                Tên đối tác <span className="text-red-500">*</span>
              </label>
              <input
                id="cpfp-name"
                data-testid="create-partner-field-name"
                value={name}
                onChange={(e) => { setName(e.target.value); setConflict(false); clearFieldError('name', e.target.value); }}
                onBlur={() => { if (name.trim()) void runMatch(name, prefill?.contactEmail); }}
                placeholder="VD: Đại học Quốc gia Singapore"
                maxLength={200}
                aria-invalid={fieldErrors.name ? true : undefined}
                aria-describedby={fieldErrors.name ? 'cpfp-name-error' : undefined}
                className={`w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-1 text-gray-700 ${
                  fieldErrors.name
                    ? 'border-red-400 focus:border-red-500 focus:ring-red-300'
                    : 'border-gray-300 focus:border-[#004c91] focus:ring-[#004c91]'
                }`}
              />
              {fieldErrors.name && (
                <p id="cpfp-name-error" role="alert" className="mt-1 text-xs font-normal text-red-600">{fieldErrors.name}</p>
              )}
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
                <CountrySelect
                  storeLang="vi"
                  value={country}
                  onChange={(next) => { if (country.trim() && next.trim().toLowerCase() !== country.trim().toLowerCase()) setCity(''); setCountry(next); }}
                  placeholder="Chọn hoặc nhập quốc gia..."
                />
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1">Thành phố</label>
                <CitySelect
                  country={country}
                  value={city}
                  onChange={setCity}
                  placeholder="Chọn hoặc nhập thành phố..."
                />
              </div>
              <div data-field-error={fieldErrors.websiteUrl ? 'true' : undefined}>
                <label htmlFor="cpfp-website" className="block text-sm font-semibold text-gray-700 mb-1">Website</label>
                <input
                  id="cpfp-website"
                  data-testid="create-partner-field-websiteUrl"
                  value={websiteUrl}
                  onChange={(e) => { setWebsiteUrl(e.target.value); clearFieldError('websiteUrl', e.target.value); }}
                  placeholder="https://..."
                  maxLength={500}
                  aria-invalid={fieldErrors.websiteUrl ? true : undefined}
                  aria-describedby={fieldErrors.websiteUrl ? 'cpfp-website-error' : undefined}
                  className={`w-full border rounded-lg px-3 py-2 text-sm focus:outline-none text-gray-700 ${
                    fieldErrors.websiteUrl ? 'border-red-400 focus:border-red-500' : 'border-gray-300 focus:border-[#004c91]'
                  }`}
                />
                {fieldErrors.websiteUrl && (
                  <p id="cpfp-website-error" role="alert" className="mt-1 text-xs font-normal text-red-600">{fieldErrors.websiteUrl}</p>
                )}
              </div>
            </div>

            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1">Địa chỉ</label>
              <input
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                maxLength={500}
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
            disabled={anyBusy}
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
