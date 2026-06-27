/**
 * Read-only renderers for the guest's original registration, shown on the VisitProcess "Trước tiếp
 * khách" screen. Data comes from VisitProcessDetail.requestSummary (real submitted form) — NEVER
 * hard-coded. Times are formatted from the local wall-clock string WITHOUT new Date() so no timezone
 * shift is introduced (PEMS stores DATETIME as local wall-clock).
 */
import React from 'react';
import type { VisitProcessRequestSummary, VisitProcessGuestMember } from '../types/delegations.types';

const EMPTY = 'Chưa có thông tin';

const VISIT_SCOPE_LABELS: Record<string, string> = { SINGLE_CAMPUS: 'Một cơ sở', MULTI_CAMPUS: 'Liên cơ sở' };
const VISIT_TYPE_LABELS: Record<string, string> = {
  CAMPUS_TOUR: 'Tham quan cơ sở (Campus tour)', MEETING: 'Họp trao đổi', WORKSHOP: 'Hội thảo',
  SIGNING_CEREMONY: 'Lễ ký kết', EXCHANGE: 'Giao lưu', OTHER: 'Khác',
};
const MEDIA_CONSENT_LABELS: Record<string, string> = { AGREED: 'Đồng ý', DECLINED: 'Không đồng ý' };
const TRANSPORT_LABELS: Record<string, string> = {
  SELF_ARRANGED: 'Tự sắp xếp', FPTU_SUPPORT: 'FPTU hỗ trợ', UNKNOWN: 'Chưa xác định', OTHER: 'Khác',
};
const WORKING_LANG_LABELS: Record<string, string> = { VI: 'Tiếng Việt', EN: 'Tiếng Anh' };

/** "YYYY-MM-DD[ T]HH:mm[:ss]" → "DD/MM/YYYY HH:mm" via pure string slicing (no Date / no TZ shift). */
function fmtDateTime(value?: string | null): string {
  if (!value) return EMPTY;
  const v = value.replace(' ', 'T');
  const [datePart, timePart] = v.split('T');
  if (!datePart) return value;
  const [y, m, d] = datePart.split('-');
  if (!y || !m || !d) return value;
  const hm = (timePart || '').slice(0, 5);
  return hm ? `${d}/${m}/${y} ${hm}` : `${d}/${m}/${y}`;
}

function Field({ label, value, className = '', multiline = false }: {
  label: string; value?: string | null; className?: string; multiline?: boolean;
}) {
  const has = value != null && String(value).trim() !== '';
  return (
    <div className={className}>
      <label className="mb-2 block text-sm font-bold text-gray-700">{label}</label>
      <div className={`w-full rounded-xl border border-gray-200 bg-gray-50/50 px-4 py-2.5 text-sm font-medium text-gray-800 ${multiline ? 'min-h-[80px] whitespace-pre-wrap' : ''}`}>
        {has ? value : <span className="font-normal italic text-gray-400">{EMPTY}</span>}
      </div>
    </div>
  );
}

export function RegistrantInfoReadOnly({ summary }: { summary?: VisitProcessRequestSummary | null }) {
  return (
    <div className="grid grid-cols-1 gap-6 bg-white p-6 md:grid-cols-2">
      <Field label="Họ và tên người đăng ký" value={summary?.registrantName} />
      <Field label="Đơn vị / tổ chức" value={summary?.registrantOrganization} />
      <Field label="Chức danh / phòng ban" value={summary?.registrantJobTitle} />
      <Field label="Số điện thoại" value={summary?.registrantPhone} />
      <Field label="Quốc tịch" value={summary?.registrantNationality} />
      <Field label="Email" value={summary?.registrantEmail} />
    </div>
  );
}

function MembersTable({ members, emptyText }: { members: VisitProcessGuestMember[]; emptyText: string }) {
  if (!members || members.length === 0) {
    return <p className="text-sm italic text-gray-400">{emptyText}</p>;
  }
  return (
    <div className="overflow-x-auto rounded-xl border border-gray-200">
      <table className="min-w-full text-sm">
        <thead className="bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
          <tr>
            <th className="px-3 py-2 font-bold">STT</th>
            <th className="px-3 py-2 font-bold">Họ và tên</th>
            <th className="px-3 py-2 font-bold">Chức vụ</th>
            <th className="px-3 py-2 font-bold">Đơn vị</th>
            <th className="px-3 py-2 font-bold">Quốc tịch</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {members.map((m, i) => (
            <tr key={m.guestMemberId || i}>
              <td className="px-3 py-2 text-gray-500">{i + 1}</td>
              <td className="px-3 py-2 font-medium text-gray-800">{m.fullName}</td>
              <td className="px-3 py-2 text-gray-600">{m.jobTitle || '—'}</td>
              <td className="px-3 py-2 text-gray-600">{m.organization || '—'}</td>
              <td className="px-3 py-2 text-gray-600">{m.nationality || '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function DelegationInfoReadOnly({ summary }: { summary?: VisitProcessRequestSummary | null }) {
  const visitTypeLabel = summary?.visitType
    ? (summary.visitType === 'OTHER'
        ? (summary.visitTypeOther?.trim() || VISIT_TYPE_LABELS.OTHER)
        : (VISIT_TYPE_LABELS[summary.visitType] || summary.visitType))
    : null;
  const scopeLabel = summary?.visitScope ? (VISIT_SCOPE_LABELS[summary.visitScope] || summary.visitScope) : null;

  return (
    <div className="space-y-6 border-t border-gray-100 bg-white p-6">
      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
        <Field label="Tên đoàn khách" value={summary?.delegationName} />
        <Field label="Phạm vi" value={scopeLabel} />
        <Field label="Loại hình tham quan" value={visitTypeLabel} />
        <Field label="Ngôn ngữ làm việc" value={summary?.workingLanguage ? (WORKING_LANG_LABELS[summary.workingLanguage] || summary.workingLanguage) : null} />
        <Field label="Đồng ý sử dụng hình ảnh" value={summary?.mediaConsentStatus ? (MEDIA_CONSENT_LABELS[summary.mediaConsentStatus] || summary.mediaConsentStatus) : null} />
        <Field label="Phương tiện di chuyển" value={summary?.transportationType ? (TRANSPORT_LABELS[summary.transportationType] || summary.transportationType) : null} />
      </div>

      <div>
        <label className="mb-2 block text-sm font-bold text-gray-700">Cơ sở &amp; thời gian dự kiến</label>
        <div className="overflow-x-auto rounded-xl border border-orange-100 bg-orange-50/40">
          <table className="min-w-full text-sm">
            <thead className="text-left text-xs uppercase tracking-wide text-gray-500">
              <tr>
                <th className="px-3 py-2 font-bold">Cơ sở</th>
                <th className="px-3 py-2 font-bold">Bắt đầu</th>
                <th className="px-3 py-2 font-bold">Kết thúc</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-orange-100">
              {(summary?.campuses ?? []).length === 0 ? (
                <tr><td className="px-3 py-2 italic text-gray-400" colSpan={3}>{EMPTY}</td></tr>
              ) : (
                summary!.campuses.map((c) => (
                  <tr key={c.visitInstanceId} className={c.isCurrent ? 'bg-white font-semibold' : ''}>
                    <td className="px-3 py-2 text-gray-800">
                      {c.campusName}
                      {c.isCurrent && <span className="ml-2 rounded-md bg-[#004c91] px-1.5 py-0.5 text-[10px] font-bold text-white">Đang xử lý</span>}
                    </td>
                    <td className="px-3 py-2 text-gray-600">{fmtDateTime(c.plannedStartAt)}</td>
                    <td className="px-3 py-2 text-gray-600">{fmtDateTime(c.plannedEndAt)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      <Field label="Mục đích thăm" value={summary?.purpose} multiline />
      <Field label="Nội dung làm việc" value={summary?.workingContent} multiline />
      {summary?.transportationDetail?.trim() && <Field label="Chi tiết phương tiện" value={summary.transportationDetail} />}
      {summary?.mediaConsentNote?.trim() && <Field label="Ghi chú hình ảnh" value={summary.mediaConsentNote} multiline />}
      {summary?.noteToFptu?.trim() && <Field label="Ghi chú của khách" value={summary.noteToFptu} multiline />}

      <div>
        <label className="mb-2 block text-sm font-bold text-gray-700">Danh sách khách mời</label>
        <MembersTable members={summary?.guestMembers ?? []} emptyText="Chưa có danh sách khách mời." />
      </div>
      {(summary?.externalSupportMembers?.length ?? 0) > 0 && (
        <div>
          <label className="mb-2 block text-sm font-bold text-gray-700">Đội ngũ hỗ trợ bên ngoài</label>
          <MembersTable members={summary!.externalSupportMembers} emptyText="" />
        </div>
      )}
    </div>
  );
}
