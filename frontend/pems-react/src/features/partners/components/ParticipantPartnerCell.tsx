/**
 * ParticipantPartnerCell — cột "Đối tác" trong bảng người tham gia biên bản
 * (docs/PARTNER_canh/01 §10.3):
 *  - INTERNAL           → badge "Nội bộ", không có hành động.
 *  - link CONFIRMED     → badge theo trạng thái hồ sơ (Đối tác / Chờ duyệt) + Xem chi tiết.
 *  - link SUGGESTED     → badge "Gợi ý" + Liên kết / Bỏ qua.
 *  - chưa có            → badge "Chưa có" + Tạo đối tác / Quét danh thiếp.
 */
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Link2, ScanLine, UserPlus, X } from 'lucide-react';
import { partnersApi } from '../api/partnersApi';
import type { VisitGuestPartnerLink } from '../types/partners.types';
import { BusinessCardScanModal } from '../../business-card-ocr/components/BusinessCardScanModal';

interface Props {
  visitInstanceId: number;
  participantKind: string; // INTERNAL | GUEST | MANUAL
  minuteParticipantId?: number | null;
  guestMemberId?: number | null;
  /** Link đã tải sẵn từ cha (GET /visit-instances/{id}/partner-links). */
  link?: VisitGuestPartnerLink | null;
  /** Cho phép thao tác (Host/participant có quyền với visit). */
  canManage?: boolean;
  onChanged?: () => void;
}

export function ParticipantPartnerCell({
  visitInstanceId, participantKind, minuteParticipantId, guestMemberId,
  link, canManage = true, onChanged,
}: Props) {
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [scanOpen, setScanOpen] = useState(false);

  if (participantKind === 'INTERNAL') {
    return (
      <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border bg-blue-50 text-blue-700 border-blue-200">
        Nội bộ
      </span>
    );
  }

  const hasTarget = (minuteParticipantId ?? 0) > 0 || (guestMemberId ?? 0) > 0;
  const activeLink = link && link.matchStatus !== 'REJECTED' ? link : null;

  const confirmSuggestion = async () => {
    if (!activeLink) return;
    setBusy(true);
    try {
      await partnersApi.linkGuestToPartner(visitInstanceId, {
        guestMemberId: activeLink.guestMemberId,
        minuteParticipantId: activeLink.minuteParticipantId,
        partnerId: activeLink.partnerId,
        partnerContactId: activeLink.partnerContactId,
        matchSource: activeLink.matchSource,
        matchStatus: 'CONFIRMED',
      });
      onChanged?.();
    } finally { setBusy(false); }
  };

  const dismissSuggestion = async () => {
    if (!activeLink) return;
    setBusy(true);
    try {
      await partnersApi.rejectLinkSuggestion(visitInstanceId, activeLink.linkId);
      onChanged?.();
    } finally { setBusy(false); }
  };

  const createPartner = async () => {
    if (!hasTarget) return;
    setBusy(true);
    try {
      const result = await partnersApi.createPartnerFromGuest(visitInstanceId, {
        guestMemberId: guestMemberId || null,
        minuteParticipantId: (minuteParticipantId ?? 0) > 0 ? minuteParticipantId : null,
      });
      onChanged?.();
      navigate(`/dashboard/partners/${result.partnerId}`);
    } catch {
      onChanged?.();
    } finally { setBusy(false); }
  };

  if (activeLink && activeLink.matchStatus === 'CONFIRMED') {
    const approved = activeLink.partnerProfileStatus === 'APPROVED';
    return (
      <div className="flex flex-col items-start gap-1">
        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border ${
          approved
            ? 'bg-green-50 text-green-700 border-green-200'
            : 'bg-amber-50 text-amber-700 border-amber-200'
        }`}>
          {approved ? 'Đối tác' : 'Chờ duyệt'}: {activeLink.partnerName}
        </span>
        <button
          onClick={() => navigate(`/dashboard/partners/${activeLink.partnerId}`)}
          className="text-[11px] font-bold text-[#004c91] hover:underline cursor-pointer"
        >
          {approved ? 'Xem chi tiết' : 'Xem hồ sơ'}
        </button>
      </div>
    );
  }

  if (activeLink && activeLink.matchStatus === 'SUGGESTED') {
    return (
      <div className="flex flex-col items-start gap-1">
        <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border bg-sky-50 text-sky-700 border-sky-200">
          Gợi ý: {activeLink.partnerName}
        </span>
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

  return (
    <div className="flex flex-col items-start gap-1">
      <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border bg-gray-100 text-gray-500 border-gray-200">
        Chưa có
      </span>
      {canManage && hasTarget && (
        <span className="flex items-center gap-2">
          <button onClick={() => void createPartner()} disabled={busy}
            className="text-[11px] font-bold text-[#004c91] hover:underline disabled:opacity-40 cursor-pointer inline-flex items-center gap-0.5">
            <UserPlus className="w-3 h-3" /> Tạo đối tác
          </button>
          <button onClick={() => setScanOpen(true)} disabled={busy}
            className="text-[11px] font-bold text-[#f37021] hover:underline disabled:opacity-40 cursor-pointer inline-flex items-center gap-0.5">
            <ScanLine className="w-3 h-3" /> Quét danh thiếp
          </button>
        </span>
      )}

      <BusinessCardScanModal
        open={scanOpen}
        onClose={() => setScanOpen(false)}
        context={{
          visitInstanceId,
          guestMemberId: guestMemberId || null,
          minuteParticipantId: (minuteParticipantId ?? 0) > 0 ? minuteParticipantId : null,
        }}
        onConfirmed={() => { setScanOpen(false); onChanged?.(); }}
      />
    </div>
  );
}
