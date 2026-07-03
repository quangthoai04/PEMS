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
import { VISIT_SCOPE_LABELS } from '../types/delegations.types';

const formatDateTime = (value?: string | null) => {
  if (!value) return '-';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '-';
  return d.toLocaleString('vi-VN', {
    hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric',
  });
};

const workingLanguageLabel = (v?: string | null) =>
  v === 'VI' ? 'Tiếng Việt' : v === 'EN' ? 'Tiếng Anh' : (v || '-');

const mediaConsentLabel = (v?: string | null) =>
  v === 'AGREED' ? 'Đồng ý' : v === 'DECLINED' ? 'Không đồng ý' : (v || '-');

const transportationLabel = (v?: string | null) => {
  switch (v) {
    case 'SELF_ARRANGED': return 'Đoàn tự sắp xếp';
    case 'FPTU_SUPPORT': return 'FPTU hỗ trợ';
    case 'OTHER': return 'Khác';
    case 'UNKNOWN': return 'Chưa xác định';
    default: return v || '-';
  }
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

  return (
    <div className="space-y-5">
      {/* 1. Người đăng ký */}
      <section>
        <SectionTitle index={1}>Thông tin người đăng ký</SectionTitle>
        <div className="grid grid-cols-1 gap-x-10 md:grid-cols-2">
          <KV label="Họ và tên" value={data.registrant.fullName} />
          <KV label="Quốc tịch" value={data.registrant.nationality} />
          <KV label="Đơn vị công tác" value={data.registrant.organization} />
          <KV label="Chức danh" value={data.registrant.jobTitle} />
          <KV label="Số điện thoại" value={data.registrant.phone} />
          <KV label="Email" value={data.registrant.email} />
        </div>
      </section>

      {/* 2. Thông tin chuyến thăm */}
      <section>
        <SectionTitle index={2}>Thông tin chuyến thăm</SectionTitle>
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

      {/* 3. Cơ sở & thời gian dự kiến */}
      <section>
        <SectionTitle index={3}>Cơ sở &amp; thời gian dự kiến</SectionTitle>
        {data.campuses.length ? (
          <div className="overflow-x-auto rounded-md border border-slate-200">
            <table className="w-full min-w-[480px] border-collapse text-[13px]">
              <thead className="border-b border-slate-200 bg-slate-50">
                <tr className="text-left text-xs text-slate-500">
                  <th className="px-2.5 py-1.5 font-semibold">Cơ sở</th>
                  <th className="px-2.5 py-1.5 font-semibold">Bắt đầu</th>
                  <th className="px-2.5 py-1.5 font-semibold">Kết thúc</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.campuses.map((c) => (
                  <tr key={c.visitInstanceId}>
                    <td className="px-2.5 py-1.5 font-medium text-slate-800">
                      {c.campusName || '-'}{c.campusCode ? <span className="text-slate-400"> ({c.campusCode})</span> : null}
                    </td>
                    <td className="px-2.5 py-1.5 text-slate-600">{formatDateTime(c.plannedStartAt)}</td>
                    <td className="px-2.5 py-1.5 text-slate-600">{formatDateTime(c.plannedEndAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="py-1 text-[13px] italic text-slate-400">Chưa có dữ liệu cơ sở</p>
        )}
      </section>

      {/* 4. Danh sách khách */}
      <section>
        <SectionTitle index={4}>Danh sách khách</SectionTitle>
        <MemberTable members={data.guestMembers} emptyText="Chưa có dữ liệu khách" />
      </section>

      {/* 5. Team hỗ trợ khách */}
      <section>
        <SectionTitle index={5}>Team hỗ trợ khách</SectionTitle>
        <MemberTable members={data.externalSupportMembers} emptyText="Không có team hỗ trợ" />
      </section>

      {/* 6. Đầu mối liên hệ */}
      <section>
        <SectionTitle index={6}>Đầu mối liên hệ</SectionTitle>
        <div className="grid grid-cols-1 gap-x-10 md:grid-cols-2">
          <KV label="Họ và tên" value={data.contactPerson.fullName} />
          <KV label="Đơn vị công tác" value={data.contactPerson.organization} />
          <KV label="Số điện thoại" value={data.contactPerson.phone} />
          <KV label="Email" value={data.contactPerson.email} />
        </div>
      </section>

      {/* 7. Yêu cầu & Xác nhận bổ sung */}
      <section>
        <SectionTitle index={7}>Yêu cầu &amp; xác nhận bổ sung</SectionTitle>
        <div className="grid grid-cols-1 gap-x-10 md:grid-cols-2">
          <KV label="Ngôn ngữ làm việc" value={workingLanguageLabel(data.workingLanguage)} />
          <KV label="Dùng hình ảnh & thông tin" value={mediaConsentLabel(data.mediaConsentStatus)} />
          <KV label="Phương tiện di chuyển" value={transportationLabel(data.transportationType)} />
          <KV label="Chi tiết phương tiện" value={data.transportationDetail} />
        </div>
        <div className="mt-1.5 space-y-1.5">
          {data.mediaConsentNote ? <KVBlock label="Ghi chú về sử dụng hình ảnh" value={data.mediaConsentNote} /> : null}
          <KVBlock label="Ghi chú cho FPTU" value={data.noteToFptu} />
        </div>
      </section>
    </div>
  );
}
