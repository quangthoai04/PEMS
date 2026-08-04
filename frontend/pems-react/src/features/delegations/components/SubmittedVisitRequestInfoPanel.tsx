/**
 * SubmittedVisitRequestInfoPanel — read-only render of exactly what the guest submitted in
 * the visit-request form. Shared by every screen that needs the "submitted form snapshot"
 * (pre-approval review, approved/waiting-host detail, rejected detail).
 *
 * Pure presentation: it contains NO approve/reject/assign-host logic and never mutates data.
 * It also never renders host-created data (agendas, participants, logistics, minutes).
 *
 * UI: compact key-value rows ("Họ và tên: Nguyễn Văn A") + section heading nhỏ với divider mỏng —
 * không dùng field-per-card để tiết kiệm diện tích (hồ sơ chi tiết kiểu enterprise).
 */

import React from 'react';
import type {
  SubmittedVisitRequestFormDetail,
  SubmittedGuestMember,
} from '../types/delegations.types';
import { VISIT_SCOPE_LABELS, INSTANCE_STATUS_LABELS } from '../types/delegations.types';

import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
const formatDateTime = (value?: string | null) => {
  return formatVietnamDateTime(value, { fallback: '-' });
};

const workingLanguageLabel = (v?: string | null) =>
  v === 'VI' ? 'Tiếng Việt' : v === 'EN' ? 'Tiếng Anh' : (v || '-');

const mediaConsentLabel = (v?: string | null) =>
  v === 'AGREED' ? 'Đồng ý' : v === 'DECLINED' ? 'Không đồng ý' : (v || '-');

/** Nhãn + màu badge trạng thái từng campus (campus-independent approval). */
const instanceStatusBadge = (status?: string | null): { label: string; cls: string } => {
  const label = status ? (INSTANCE_STATUS_LABELS[status as keyof typeof INSTANCE_STATUS_LABELS] ?? status) : '-';
  const cls =
    status === 'WAITING_REQUEST_APPROVAL' ? 'bg-amber-50 text-amber-700 border-amber-200'
    : status === 'REJECTED' ? 'bg-red-50 text-red-700 border-red-200'
    : status === 'CANCELLED' ? 'bg-slate-100 text-slate-500 border-slate-200'
    : status === 'CLOSED' ? 'bg-slate-100 text-slate-600 border-slate-200'
    : 'bg-emerald-50 text-emerald-700 border-emerald-200';
  return { label, cls };
};

/** Một dòng key-value compact: "Nhãn: Giá trị". */
const KV = ({ label, value }: { label: string; value?: string | null }) => (
  <div className="flex gap-2 py-0.5 text-[13px] leading-5">
    <span className="w-36 shrink-0 text-slate-500">{label}:</span>
    <span className="min-w-0 font-medium text-slate-800 break-words">{value?.trim() || '-'}</span>
  </div>
);

/** Field text dài: nhãn trên, nội dung ngay dưới, không đóng khung. */
const KVBlock = ({ label, value }: { label: string; value?: string | null }) => (
  <div className="py-0.5 text-[13px] leading-5">
    <span className="text-slate-500">{label}:</span>
    <p className="mt-0.5 font-medium text-slate-800 whitespace-pre-wrap break-words">{value?.trim() || '-'}</p>
  </div>
);

/** Section heading nhỏ + divider mỏng, không dùng box bo góc lớn. */
const SectionTitle = ({ index, children }: { index: number; children: React.ReactNode }) => (
  <h3 className="mb-2 flex items-center gap-2 border-b border-slate-200 pb-1.5 text-xs font-bold uppercase tracking-wide text-[#004c91]">
    <span className="text-slate-400">{index}.</span>
    {children}
  </h3>
);

const MemberTable = ({ members, emptyText }: { members: SubmittedGuestMember[]; emptyText: string }) => {
  if (!members.length) {
    return <p className="py-1 text-[13px] italic text-slate-400">{emptyText}</p>;
  }
  return (
    <div className="overflow-x-auto rounded-md border border-slate-200">
      <table className="w-full min-w-[560px] border-collapse text-[13px]">
        <thead className="border-b border-slate-200 bg-slate-50">
          <tr className="text-left text-xs text-slate-500">
            <th className="px-2.5 py-1.5 w-10 text-center font-semibold">STT</th>
            <th className="px-2.5 py-1.5 font-semibold">Họ và tên</th>
            <th className="px-2.5 py-1.5 font-semibold">Chức vụ</th>
            <th className="px-2.5 py-1.5 font-semibold">Đơn vị công tác</th>
            <th className="px-2.5 py-1.5 font-semibold">Quốc tịch</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {members.map((m, i) => (
            <tr key={m.guestMemberId}>
              <td className="px-2.5 py-1.5 text-center text-slate-400">{i + 1}</td>
              <td className="px-2.5 py-1.5 font-medium text-slate-800">{m.fullName || '-'}</td>
              <td className="px-2.5 py-1.5 text-slate-600">{m.jobTitle || '-'}</td>
              <td className="px-2.5 py-1.5 text-slate-600">{m.organization || '-'}</td>
              <td className="px-2.5 py-1.5 text-slate-600">{m.nationality || '-'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};

const VISIT_TYPE_LABELS: Record<string, string> = {
  WORKSHOP: 'Workshop / Hội thảo',
  SEMINAR: 'Chuyên đề (Seminar)',
  CAMPUS_TOUR: 'Tham quan Campus',
  ACADEMIC_EXCHANGE: 'Trao đổi học thuật',
  CULTURAL_EXCHANGE: 'Giao lưu văn hóa',
  MEETING: 'Họp / Làm việc',
};

const CREATED_SOURCE_LABELS: Record<string, string> = {
  VISITOR_SUBMITTED: 'Khách gửi đơn',
  INTERNAL_CREATED: 'Nội bộ tạo đơn',
};

export function SubmittedVisitRequestInfoPanel({ data }: { data: SubmittedVisitRequestFormDetail }) {
  const scopeLabel = VISIT_SCOPE_LABELS[data.visitScope] ?? data.visitScope;
  const visitTypeValue =
    data.visitType === 'OTHER' && data.visitTypeOther 
      ? data.visitTypeOther 
      : (VISIT_TYPE_LABELS[data.visitType] ?? data.visitType);
  const sourceLabel = CREATED_SOURCE_LABELS[data.createdSource] ?? data.createdSource;

  const canSeeRegistrantInfo = (() => {
    try {
      const userStr = typeof window !== 'undefined' ? localStorage.getItem('currentUser') : null;
      if (!userStr) return false;
      const user = JSON.parse(userStr);
      const roleCode = (user?.roleCode || user?.role || '').toUpperCase();
      return roleCode === 'STAFF' || roleCode === 'HO' || roleCode === 'ADMIN';
    } catch {
      return false;
    }
  })();

  let sectionCounter = 0;

  return (
    <div className="space-y-5">
      {/* 1. Người đăng ký (Chỉ IC role xem) */}
      {canSeeRegistrantInfo && (
        <section>
          <SectionTitle index={++sectionCounter}>Thông tin người đăng ký</SectionTitle>
          <div className="grid grid-cols-1 gap-x-10 md:grid-cols-2">
            <KV label="Họ và tên" value={data.registrant.fullName} />
            <KV label="Quốc tịch" value={data.registrant.nationality} />
            <KV label="Đơn vị công tác" value={data.registrant.organization} />
            <KV label="Chức danh" value={data.registrant.jobTitle} />
            <KV label="Số điện thoại" value={data.registrant.phone} />
            <KV label="Email" value={data.registrant.email} />
          </div>
        </section>
      )}

      {/* Thông tin chuyến thăm */}
      <section>
        <SectionTitle index={++sectionCounter}>Thông tin chuyến thăm</SectionTitle>
        <div className="grid grid-cols-1 gap-x-10 md:grid-cols-2">
          <KV label="Tên đoàn khách" value={data.delegationName} />
          <KV label="Phạm vi" value={scopeLabel} />
          <KV label="Loại hình" value={visitTypeValue} />
          <KV label="Nguồn tạo đơn" value={sourceLabel} />
        </div>
        <div className="mt-1.5 space-y-1.5">
          <KVBlock label="Mục đích thăm FPTU" value={data.purpose} />
          <KVBlock label="Nội dung làm việc" value={data.workingContent} />
        </div>
      </section>

      {/* Cơ sở & thời gian dự kiến */}
      <section>
        <SectionTitle index={++sectionCounter}>Cơ sở &amp; thời gian dự kiến</SectionTitle>
        {data.campuses.length ? (
          <div className="overflow-x-auto rounded-md border border-slate-200">
            <table className="w-full min-w-[640px] border-collapse text-[13px]">
              <thead className="border-b border-slate-200 bg-slate-50">
                <tr className="text-left text-xs text-slate-500">
                  <th className="px-2.5 py-1.5 font-semibold">Cơ sở</th>
                  <th className="px-2.5 py-1.5 font-semibold">Bắt đầu</th>
                  <th className="px-2.5 py-1.5 font-semibold">Kết thúc</th>
                  <th className="px-2.5 py-1.5 font-semibold">Trạng thái</th>
                  <th className="px-2.5 py-1.5 font-semibold">Host / Quyết định</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.campuses.map((c) => {
                  const badge = instanceStatusBadge(c.instanceStatus);
                  return (
                    <tr key={c.visitInstanceId} className={c.isOwnCampus ? 'bg-blue-50/40' : ''}>
                      <td className="px-2.5 py-1.5 font-medium text-slate-800">
                        {c.campusName || '-'}{c.campusCode ? <span className="text-slate-400"> ({c.campusCode})</span> : null}
                        {c.isOwnCampus && <span className="ml-1.5 rounded bg-[#004c91] px-1.5 py-0.5 text-[10px] font-bold text-white">Cơ sở của bạn</span>}
                      </td>
                      <td className="px-2.5 py-1.5 text-slate-600">{formatDateTime(c.plannedStartAt)}</td>
                      <td className="px-2.5 py-1.5 text-slate-600">{formatDateTime(c.plannedEndAt)}</td>
                      <td className="px-2.5 py-1.5">
                        <span className={`inline-block rounded border px-1.5 py-0.5 text-[11px] font-semibold ${badge.cls}`}>{badge.label}</span>
                      </td>
                      <td className="px-2.5 py-1.5 text-slate-600">
                        {c.instanceStatus === 'REJECTED'
                          ? <span className="text-red-700">Lý do: {c.decisionNote?.trim() || '-'}</span>
                          : (c.currentHostName?.trim() || (c.instanceStatus === 'WAITING_REQUEST_APPROVAL' ? 'Chờ Staff Leader xử lý' : '-'))}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="py-1 text-[13px] italic text-slate-400">Chưa có dữ liệu cơ sở</p>
        )}
      </section>

      {/* Danh sách khách (Chỉ IC role xem) */}
      {canSeeRegistrantInfo && (
        <section>
          <SectionTitle index={++sectionCounter}>Danh sách khách</SectionTitle>
          <MemberTable members={data.guestMembers} emptyText="Chưa có dữ liệu khách" />
        </section>
      )}

      {/* Team hỗ trợ khách (Chỉ IC role xem) */}
      {canSeeRegistrantInfo && (
        <section>
          <SectionTitle index={++sectionCounter}>Team hỗ trợ khách</SectionTitle>
          <MemberTable members={data.externalSupportMembers} emptyText="Không có team hỗ trợ" />
        </section>
      )}

      {/* Đầu mối liên hệ (Chỉ IC role xem) */}
      {canSeeRegistrantInfo && (
        <section>
          <SectionTitle index={++sectionCounter}>Đầu mối liên hệ</SectionTitle>
          <div className="grid grid-cols-1 gap-x-10 md:grid-cols-2">
            <KV label="Họ và tên" value={data.contactPerson.fullName} />
            <KV label="Đơn vị công tác" value={data.contactPerson.organization} />
            <KV label="Số điện thoại" value={data.contactPerson.phone} />
            <KV label="Email" value={data.contactPerson.email} />
          </div>
        </section>
      )}

      {/* Yêu cầu & nội dung biên bản công việc */}
      <section>
        <SectionTitle index={++sectionCounter}>Yêu cầu &amp; nội dung biên bản công việc</SectionTitle>
        <div className="grid grid-cols-1 gap-x-10 md:grid-cols-2">
          <KV label="Ngôn ngữ làm việc" value={workingLanguageLabel(data.workingLanguage)} />
          <KV label="Dùng hình ảnh & thông tin" value={mediaConsentLabel(data.mediaConsentStatus)} />
        </div>
        <div className="mt-1.5 space-y-1.5">
          <KVBlock label="Nhận diện phương tiện di chuyển tới FPTU" value={data.transportationNote} />
          {data.mediaConsentNote ? <KVBlock label="Ghi chú về sử dụng hình ảnh" value={data.mediaConsentNote} /> : null}
          <KVBlock label="Nội dung biên bản &amp; ghi chú cho FPTU" value={data.noteToFptu || data.workingContent} />
        </div>
      </section>
    </div>
  );
}
