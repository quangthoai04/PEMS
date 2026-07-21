/**
 * ParticipantPartnerCell — cột "Đối tác" trong bảng người tham gia biên bản
 * (docs/PARTNER_canh/01 §10.3). UI tách rõ TRẠNG THÁI (badge nhỏ) và TÊN đối tác
 * (1 dòng, truncate + tooltip) để ô bảng gọn, không bị cao bất thường:
 *  - INTERNAL           → badge "Nội bộ".
 *  - link CONFIRMED     → badge trạng thái hồ sơ (Đã liên kết / Chờ duyệt / Từ chối) + tên + Xem hồ sơ.
 *  - link SUGGESTED     → badge "Gợi ý" + tên + Liên kết / Bỏ qua.
 *  - chưa link          → badge "Chưa liên kết" + Tạo / liên kết · Quét danh thiếp.
 */
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Link2, ScanLine, UserPlus, X } from 'lucide-react';
import { partnersApi } from '../api/partnersApi';
import type { VisitGuestPartnerLink } from '../types/partners.types';
import { BusinessCardScanModal } from '../../business-card-ocr/components/BusinessCardScanModal';
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
  /** Nhãn nguồn để ghi vào mô tả đối tác, vd "biên bản cuộc họp #12". */
  sourceLabel?: string | null;
  onChanged?: () => void;
}

/** Badge trạng thái theo profile_status của partner đã liên kết (chỉ chứa trạng thái, không kèm tên). */
function partnerStatusMeta(status?: string | null): { label: string; cls: string } {
  switch (status) {
    case 'APPROVED':
      return { label: 'Đã liên kết', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' };
    case 'PENDING_APPROVAL':
      return { label: 'Chờ duyệt', cls: 'bg-amber-50 text-amber-700 border-amber-200' };
    case 'REJECTED':
      return { label: 'Từ chối', cls: 'bg-red-50 text-red-700 border-red-200' };
    case 'DRAFT':
      return { label: 'Bản nháp', cls: 'bg-slate-100 text-slate-600 border-slate-200' };
    default:
      // Trạng thái lạ/null → không crash, coi như đã liên kết.
      return { label: 'Đã liên kết', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' };
  }
}

const BADGE_BASE = 'inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border whitespace-nowrap';
const NAME_CLS = 'max-w-[180px] truncate text-[13px] font-semibold text-slate-800';

export function ParticipantPartnerCell({
  visitInstanceId, participantKind, minuteParticipantId, guestMemberId,
  link, canManage = true,
  prefillOrganization, prefillContactName, prefillContactEmail, prefillJobTitle, sourceLabel,
  onChanged,
}: Props) {
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [scanOpen, setScanOpen] = useState(false);
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

  // Đã liên kết (CONFIRMED) — badge trạng thái + tên truncate + Xem hồ sơ.
  if (activeLink && activeLink.matchStatus === 'CONFIRMED') {
    const meta = partnerStatusMeta(activeLink.partnerProfileStatus);
    return (
      <div className="flex flex-col items-start gap-1 max-w-[190px]">
        <span className={`${BADGE_BASE} ${meta.cls}`}>{meta.label}</span>
        <div className={NAME_CLS} title={activeLink.partnerName}>
          {activeLink.partnerName}
        </div>
        <button
          onClick={() => navigate(`/dashboard/partners/${activeLink.partnerId}`)}
          className="text-[11px] font-bold text-[#004c91] hover:underline cursor-pointer"
        >
          Xem hồ sơ
        </button>
      </div>
    );
  }

  // Gợi ý (SUGGESTED) — badge "Gợi ý" + tên truncate + Liên kết / Bỏ qua.
  if (activeLink && activeLink.matchStatus === 'SUGGESTED') {
    return (
      <div className="flex flex-col items-start gap-1 max-w-[190px]">
        <span className={`${BADGE_BASE} bg-sky-50 text-sky-700 border-sky-200`}>Gợi ý</span>
        <div className={NAME_CLS} title={activeLink.partnerName}>
          {activeLink.partnerName}
        </div>
        {canManage && (
          <span className="flex items-center gap-2">
            <button onClick={() => void confirmSuggestion()} disabled={busy}
              className="text-[11px] font-bold text-[#004c91] hover:underline disabled:opacity-40 cursor-pointer inline-flex items-center gap-0.5">
              <Link2 className="w-3 h-3" /> Liên kết
            </button>
            <button onClick={() => void dismissSuggestion()} disabled={busy}
              className="text-[11px] font-bold text-gray-400 hover:text-red-500 disabled:opacity-40 cursor-pointer inline-flex items-center gap-0.5">
              <X className="w-3 h-3" /> Bỏ qua
            </button>
          </span>
        )}
      </div>
    );
  }

  // Chưa liên kết — badge nhẹ + Tạo / liên kết · Quét danh thiếp.
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
          <button onClick={() => setScanOpen(true)} disabled={busy}
            className="text-[11px] font-bold text-[#f37021] hover:underline disabled:opacity-40 cursor-pointer inline-flex items-center gap-0.5">
            <ScanLine className="w-3 h-3" /> Quét danh thiếp
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
          sourceLabel,
        }}
        onDone={() => { setCreateOpen(false); onChanged?.(); }}
      />

      <BusinessCardScanModal
        open={scanOpen}
        onClose={() => setScanOpen(false)}
        context={{
          visitInstanceId,
          guestMemberId: guestMemberId || null,
          minuteParticipantId: (minuteParticipantId ?? 0) > 0 ? minuteParticipantId : null,
          prefillOrganization,
        }}
        onConfirmed={() => { setScanOpen(false); onChanged?.(); }}
      />
    </div>
  );
}
