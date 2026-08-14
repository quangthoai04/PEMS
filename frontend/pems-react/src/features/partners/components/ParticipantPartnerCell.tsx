/**
 * ParticipantPartnerCell — cột "Đối tác" trong bảng người tham gia biên bản
 * (docs/PARTNER_canh/01 §10.3). UI tách rõ TRẠNG THÁI (badge nhỏ) và TÊN đối tác
 * (1 dòng, truncate + tooltip) để ô bảng gọn, không bị cao bất thường:
 *  - INTERNAL           → badge "Nội bộ".
 *  - link CONFIRMED     → badge trạng thái hồ sơ (Đã liên kết / Chờ duyệt / Từ chối) + tên + Xem hồ sơ.
 *  - link SUGGESTED     → badge "Gợi ý" + tên + Liên kết / Bỏ qua.
 *  - chưa link          → badge "Chưa liên kết" + Tạo / liên kết.
 *
 * Quét danh thiếp KHÔNG nằm ở đây: màn "Đang tiếp khách" (VisitDuringTab) đã có nguyên mục Scan Card
 * Visit làm đủ luồng chụp/OCR/sửa thông tin/khớp đối tác, nên nút trong từng dòng bảng chỉ là lối vào
 * thứ hai cho cùng một việc.
 */
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Link2, UserPlus, X } from 'lucide-react';
import { partnersApi } from '../api/partnersApi';
import type { VisitGuestPartnerLink } from '../types/partners.types';
import { CreatePartnerFromParticipantModal } from './CreatePartnerFromParticipantModal';
import {
  showLoadingToast,
  updateToastSuccess,
  updateToastError,
} from '../../../shared/utils/toast';

interface Props {
  visitInstanceId: number;
  participantKind: string; // INTERNAL | GUEST | MANUAL
  minuteParticipantId?: number | null;
  guestMemberId?: number | null;
  /** Link đã tải sẵn từ cha (GET /visit-instances/{id}/partner-links). */
  link?: VisitGuestPartnerLink | null;
  /** Cho phép thao tác (Host/participant có quyền với visit). */
  canManage?: boolean;
  /** Prefill cho modal tạo đối tác (lấy từ snapshot dòng người tham gia). */
  prefillOrganization?: string | null;
  prefillContactName?: string | null;
  prefillContactEmail?: string | null;
  prefillJobTitle?: string | null;
  /** Quốc tịch của khách (nếu dòng này gắn với một guest) — dùng làm giá trị mặc định cho
   *  Quốc gia trong modal tạo đối tác. */
  prefillNationality?: string | null;
  /** Nhãn nguồn để ghi vào mô tả đối tác, vd "biên bản cuộc họp #12". */
  sourceLabel?: string | null;
  onChanged?: () => void;
}

/**
 * Badge cho TRẠNG THÁI HỒ SƠ ĐỐI TÁC — chỉ trả lời "hồ sơ tổ chức đã được duyệt chưa".
 *
 * Nhãn đều mở đầu bằng "Hồ sơ" để không lẫn với trạng thái QUAN HỆ (đã liên kết / gợi ý / chưa
 * liên kết). Trước đây APPROVED hiển thị "Đã liên kết" và REJECTED hiển thị "Từ chối" — hai câu trả
 * lời cho hai câu hỏi khác nhau nằm chung một chỗ, nên "Từ chối" đọc được thành "người dùng đã bỏ
 * qua gợi ý" lẫn "Staff Leader đã từ chối hồ sơ" (PART-05).
 */
function partnerStatusMeta(status?: string | null): { label: string; cls: string } {
  switch (status) {
    case 'APPROVED':
      return { label: 'Hồ sơ đã duyệt', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' };
    case 'PENDING_APPROVAL':
      return { label: 'Hồ sơ chờ duyệt', cls: 'bg-amber-50 text-amber-700 border-amber-200' };
    case 'REJECTED':
      return { label: 'Hồ sơ bị từ chối', cls: 'bg-red-50 text-red-700 border-red-200' };
    case 'DRAFT':
      return { label: 'Hồ sơ nháp', cls: 'bg-slate-100 text-slate-600 border-slate-200' };
    default:
      // Trạng thái lạ/null → không crash, và cũng không khẳng định điều mình không biết.
      return { label: 'Hồ sơ', cls: 'bg-slate-100 text-slate-600 border-slate-200' };
  }
}

const BADGE_BASE = 'inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border whitespace-nowrap';
const NAME_CLS = 'max-w-[180px] truncate text-[13px] font-semibold text-slate-800';

export function ParticipantPartnerCell({
  visitInstanceId, participantKind, minuteParticipantId, guestMemberId,
  link, canManage = true,
  prefillOrganization, prefillContactName, prefillContactEmail, prefillJobTitle, prefillNationality, sourceLabel,
  onChanged,
}: Props) {
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);

  if (participantKind === 'INTERNAL') {
    return (
      <span className={`${BADGE_BASE} bg-blue-50 text-blue-700 border-blue-200`}>
        Nội bộ
      </span>
    );
  }

  const hasTarget = (minuteParticipantId ?? 0) > 0 || (guestMemberId ?? 0) > 0;
  const activeLink = link && link.matchStatus !== 'REJECTED' ? link : null;

  const confirmSuggestion = async () => {
    if (!activeLink) return;
    setBusy(true);
    const toastId = showLoadingToast('Đang liên kết đối tác...', 'partner-suggestion-confirm');
    try {
      await partnersApi.linkGuestToPartner(visitInstanceId, {
        guestMemberId: activeLink.guestMemberId,
        minuteParticipantId: activeLink.minuteParticipantId,
        partnerId: activeLink.partnerId,
        partnerContactId: activeLink.partnerContactId,
        matchSource: activeLink.matchSource,
        matchStatus: 'CONFIRMED',
      });
      updateToastSuccess(toastId, 'Đã liên kết đối tác.');
      onChanged?.();
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể liên kết đối tác.');
    } finally { setBusy(false); }
  };

  const dismissSuggestion = async () => {
    if (!activeLink) return;
    setBusy(true);
    const toastId = showLoadingToast('Đang bỏ qua gợi ý liên kết...', 'partner-suggestion-dismiss');
    try {
      await partnersApi.rejectLinkSuggestion(visitInstanceId, activeLink.linkId);
      updateToastSuccess(toastId, 'Đã bỏ qua gợi ý liên kết.');
      onChanged?.();
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể bỏ qua gợi ý liên kết.');
    } finally { setBusy(false); }
  };

  // Đã liên kết (CONFIRMED) — quan hệ đã xong, việc còn lại là THÔNG TIN LIÊN HỆ.
  //
  // Hai badge tách bạch: "Đã liên kết" nói về QUAN HỆ (thành viên này thuộc đối tác nào), badge
  // trạng thái hồ sơ nói về HỒ SƠ ĐỐI TÁC (đã duyệt chưa). Trước đây hai thứ dùng chung một badge
  // nên "Chờ duyệt" đọc thành "chưa liên kết xong", còn "Từ chối" thì không rõ là từ chối liên kết
  // hay từ chối hồ sơ (PART-05).
  //
  // Và KHÔNG còn "Tạo / liên kết" ở đây: đối tác đã xác định rồi, mời tạo tiếp chỉ dẫn người dùng
  // đi tạo hồ sơ trùng (PART-07).
  if (activeLink && activeLink.matchStatus === 'CONFIRMED') {
    const meta = partnerStatusMeta(activeLink.partnerProfileStatus);
    const hasContact = (activeLink.partnerContactId ?? 0) > 0;
    return (
      <div className="flex flex-col items-start gap-1 max-w-[190px]">
        <span className="flex flex-wrap items-center gap-1">
          <span className={`${BADGE_BASE} bg-emerald-50 text-emerald-700 border-emerald-200`}>
            Đã liên kết
          </span>
          <span className={`${BADGE_BASE} ${meta.cls}`}>{meta.label}</span>
        </span>
        <div className={NAME_CLS} title={activeLink.partnerName}>
          {activeLink.partnerName}
        </div>
        {hasContact && activeLink.partnerContactName && (
          <div className="max-w-[180px] truncate text-[11px] text-slate-500" title={activeLink.partnerContactName}>
            Đầu mối: {activeLink.partnerContactName}
          </div>
        )}
        <span className="flex items-center gap-2">
          <button
            onClick={() => navigate(`/dashboard/partners/${activeLink.partnerId}`)}
            className="text-[11px] font-bold text-[#004c91] hover:underline cursor-pointer"
          >
            Xem hồ sơ
          </button>
          {canManage && (
            <button
              onClick={() => navigate(`/dashboard/partners/${activeLink.partnerId}#contacts`)}
              className="text-[11px] font-bold text-[#004c91] hover:underline cursor-pointer inline-flex items-center gap-0.5"
            >
              <UserPlus className="w-3 h-3" />
              {hasContact ? 'Cập nhật liên hệ' : 'Thêm liên hệ'}
            </button>
          )}
        </span>
      </div>
    );
  }

  // Gợi ý (SUGGESTED) — CHƯA phải quan hệ, chỉ là kết quả đối chiếu chờ người xác nhận.
  //
  // "Gợi ý" (quan hệ) và badge trạng thái hồ sơ đứng cạnh nhau chứ không gộp: một gợi ý trỏ tới hồ
  // sơ đã bị từ chối thì không được xác nhận, và người dùng phải thấy được VÌ SAO ngay tại dòng đó
  // thay vì bấm rồi nhận lỗi (PART-04/PART-05).
  if (activeLink && activeLink.matchStatus === 'SUGGESTED') {
    const meta = partnerStatusMeta(activeLink.partnerProfileStatus);
    const profileLinkable = activeLink.partnerProfileStatus === 'APPROVED'
      || activeLink.partnerProfileStatus === 'PENDING_APPROVAL';
    return (
      <div className="flex flex-col items-start gap-1 max-w-[190px]">
        <span className="flex flex-wrap items-center gap-1">
          <span className={`${BADGE_BASE} bg-sky-50 text-sky-700 border-sky-200`}>Gợi ý</span>
          <span className={`${BADGE_BASE} ${meta.cls}`}>{meta.label}</span>
        </span>
        <div className={NAME_CLS} title={activeLink.partnerName}>
          {activeLink.partnerName}
        </div>
        {canManage && (
          <span className="flex items-center gap-2">
            {profileLinkable ? (
              <button onClick={() => void confirmSuggestion()} disabled={busy}
                className="text-[11px] font-bold text-[#004c91] hover:underline disabled:opacity-40 cursor-pointer inline-flex items-center gap-0.5">
                <Link2 className="w-3 h-3" /> Xác nhận liên kết
              </button>
            ) : (
              <button onClick={() => navigate(`/dashboard/partners/${activeLink.partnerId}`)}
                className="text-[11px] font-bold text-amber-700 hover:underline cursor-pointer">
                Xem lý do
              </button>
            )}
            <button onClick={() => void dismissSuggestion()} disabled={busy}
              className="text-[11px] font-bold text-gray-400 hover:text-red-500 disabled:opacity-40 cursor-pointer inline-flex items-center gap-0.5">
              <X className="w-3 h-3" /> Không phải
            </button>
          </span>
        )}
      </div>
    );
  }

  // Chưa liên kết — badge nhẹ + Tạo / liên kết.
  return (
    <div className="flex flex-col items-start gap-1 max-w-[190px]">
      <span className={`${BADGE_BASE} bg-slate-100 text-slate-600 border-slate-200`}>
        Chưa liên kết
      </span>
      {canManage && hasTarget && (
        <span className="flex items-center gap-2">
          <button onClick={() => setCreateOpen(true)} disabled={busy}
            className="text-[11px] font-bold text-[#004c91] hover:underline disabled:opacity-40 cursor-pointer inline-flex items-center gap-0.5">
            <UserPlus className="w-3 h-3" /> Tạo / liên kết
          </button>
        </span>
      )}

      <CreatePartnerFromParticipantModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        visitInstanceId={visitInstanceId}
        guestMemberId={guestMemberId || null}
        minuteParticipantId={(minuteParticipantId ?? 0) > 0 ? minuteParticipantId : null}
        prefill={{
          organization: prefillOrganization,
          contactName: prefillContactName,
          contactEmail: prefillContactEmail,
          jobTitle: prefillJobTitle,
          nationality: prefillNationality,
          sourceLabel,
        }}
        onDone={() => { setCreateOpen(false); onChanged?.(); }}
      />
    </div>
  );
}
