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
/** Canonical visitType labels for VisitProcessRequestSummary — reused by VisitContributionPage
 * (and any other screen rendering this same summary type) so the mapping has one home. */
export const VISIT_TYPE_LABELS: Record<string, string> = {
  CAMPUS_TOUR: 'Tham quan cơ sở (Campus tour)', MEETING: 'Họp trao đổi', WORKSHOP: 'Hội thảo',
  SIGNING_CEREMONY: 'Lễ ký kết', EXCHANGE: 'Giao lưu', OTHER: 'Khác',
};
const MEDIA_CONSENT_LABELS: Record<string, string> = { AGREED: 'Đồng ý', DECLINED: 'Không đồng ý' };
/** Canonical workingLanguage labels for VisitProcessRequestSummary — same reuse rationale. */
export const WORKING_LANG_LABELS: Record<string, string> = { VI: 'Tiếng Việt', EN: 'Tiếng Anh' };

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

/** Dòng key-value compact ("Nhãn: Giá trị") — thay cho field-per-card cũ để tiết kiệm diện tích. */
function Field({ label, value, className = '', multiline = false }: {
  label: string; value?: string | null; className?: string; multiline?: boolean;
}) {
  const has = value != null && String(value).trim() !== '';
  if (multiline) {
    return (
      <div className={`py-0.5 text-[13px] leading-5 ${className}`}>
        <span className="text-gray-500">{label}:</span>
        <p className="mt-0.5 whitespace-pre-wrap break-words font-normal text-gray-800">
          {has ? value : <span className="font-normal italic text-gray-400">{EMPTY}</span>}
        </p>
      </div>
    );
  }
  return (
    <div className={`flex gap-2 py-0.5 text-[13px] leading-5 ${className}`}>
      <span className="w-40 shrink-0 text-gray-500">{label}:</span>
      <span className="min-w-0 break-words font-normal text-gray-800">
        {has ? value : <span className="font-normal italic text-gray-400">{EMPTY}</span>}
      </span>
    </div>
  );
}

export function RegistrantInfoReadOnly({ summary }: { summary?: VisitProcessRequestSummary | null }) {
  return (
    <div className="grid grid-cols-1 gap-x-10 bg-white px-4 py-3 md:grid-cols-2">
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
    return <p className="text-[13px] italic text-gray-400">{emptyText}</p>;
  }
  return (
    <div className="overflow-x-auto rounded-md border border-gray-200">
      <table className="min-w-full text-[13px]">
        <thead className="border-b border-gray-200 bg-gray-50 text-left text-xs text-gray-500">
          <tr>
            <th className="px-2.5 py-1.5 font-semibold">STT</th>
            <th className="px-2.5 py-1.5 font-semibold">Họ và tên</th>
            <th className="px-2.5 py-1.5 font-semibold">Chức vụ</th>
            <th className="px-2.5 py-1.5 font-semibold">Đơn vị</th>
            <th className="px-2.5 py-1.5 font-semibold">Quốc tịch</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {members.map((m, i) => (
            <tr key={m.guestMemberId || i}>
              <td className="px-2.5 py-1.5 text-gray-500">{i + 1}</td>
              <td className="px-2.5 py-1.5 font-medium text-gray-800">{m.fullName}</td>
              <td className="px-2.5 py-1.5 text-gray-600">{m.jobTitle || '—'}</td>
              <td className="px-2.5 py-1.5 text-gray-600">{m.organization || '—'}</td>
              <td className="px-2.5 py-1.5 text-gray-600">{m.nationality || '—'}</td>
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
    <div className="space-y-3 border-t border-gray-100 bg-white px-4 py-3">
      <div className="grid grid-cols-1 gap-x-10 md:grid-cols-2">
        <Field label="Tên đoàn khách" value={summary?.delegationName} />
        <Field label="Phạm vi" value={scopeLabel} />
        <Field label="Loại hình tham quan" value={visitTypeLabel} />
        <Field label="Ngôn ngữ làm việc" value={summary?.workingLanguage ? (WORKING_LANG_LABELS[summary.workingLanguage] || summary.workingLanguage) : null} />
        <Field label="Đồng ý truyền thông" value={summary?.mediaConsentStatus ? (MEDIA_CONSENT_LABELS[summary.mediaConsentStatus] || summary.mediaConsentStatus) : null} />
      </div>

      <div>
        <p className="mb-1 border-b border-gray-200 pb-1 text-xs font-bold uppercase tracking-wide text-[#004c91]">Cơ sở &amp; thời gian dự kiến</p>
        <div className="overflow-x-auto rounded-md border border-gray-200">
          <table className="min-w-full text-[13px]">
            <thead className="border-b border-gray-200 bg-gray-50 text-left text-xs text-gray-500">
              <tr>
                <th className="px-2.5 py-1.5 font-semibold">Cơ sở</th>
                <th className="px-2.5 py-1.5 font-semibold">Bắt đầu</th>
                <th className="px-2.5 py-1.5 font-semibold">Kết thúc</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {(summary?.campuses ?? []).length === 0 ? (
                <tr><td className="px-2.5 py-1.5 italic text-gray-400" colSpan={3}>{EMPTY}</td></tr>
              ) : (
                summary!.campuses.map((c) => (
                  <tr key={c.visitInstanceId} className={c.isCurrent ? 'bg-blue-50/40' : ''}>
                    <td className="px-2.5 py-1.5 text-gray-800">
                      {c.campusName}
                      {c.isCurrent && <span className="ml-2 rounded bg-[#004c91] px-1.5 py-0.5 text-[10px] font-bold text-white">Đang xử lý</span>}
                    </td>
                    <td className="px-2.5 py-1.5 text-gray-600">{fmtDateTime(c.plannedStartAt)}</td>
                    <td className="px-2.5 py-1.5 text-gray-600">{fmtDateTime(c.plannedEndAt)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Đầu mối đoàn khách phối hợp tại cơ sở — THIS campus's own guest-side coordinator.
          Rendered unconditionally: the person running the visit needs to know whether there is a
          coordinator and what is known about them, and a block that disappears when a name happens
          to be blank answers neither question. Each field falls back to "Chưa có thông tin" on its
          own, so a missing name never hides a phone number that is present. */}
      <div>
        <p className="mb-1 border-b border-gray-200 pb-1 text-xs font-bold uppercase tracking-wide text-[#004c91]">
          Đầu mối đoàn khách phối hợp tại cơ sở
        </p>
        <div className="grid grid-cols-1 gap-x-10 md:grid-cols-2">
          <Field label="Họ và tên" value={summary?.operationalContactFullName} />
          <Field label="Đơn vị công tác" value={summary?.operationalContactOrganization} />
          <Field label="Chức vụ" value={summary?.operationalContactJobTitle} />
          <Field label="Số điện thoại" value={summary?.operationalContactPhone} />
          <Field label="Email" value={summary?.operationalContactEmail} />
        </div>
      </div>

      <div className="space-y-1">
        <Field label="Mục đích thăm" value={summary?.purpose} multiline />
        <Field label="Nội dung làm việc" value={summary?.workingContent} multiline />
        {/* Rendered unconditionally, like every other field above. A row that vanishes when the guest
            left it blank reads as "this form never asked", and the host planning the visit cannot tell
            that from "asked and nothing to report" — which is exactly what they need to know before
            they stop looking for dietary or accessibility requirements. */}
        <Field label="Nhận diện phương tiện di chuyển" value={summary?.transportationNote} multiline />
        <Field label="Ghi chú gửi FPTU" value={summary?.notes} multiline />
      </div>

      <div>
        <p className="mb-1 border-b border-gray-200 pb-1 text-xs font-bold uppercase tracking-wide text-[#004c91]">Danh sách khách mời</p>
        <MembersTable members={summary?.guestMembers ?? []} emptyText="Chưa có danh sách khách mời." />
      </div>
      {(summary?.externalSupportMembers?.length ?? 0) > 0 && (
        <div>
          <p className="mb-1 border-b border-gray-200 pb-1 text-xs font-bold uppercase tracking-wide text-[#004c91]">Đội ngũ hỗ trợ bên ngoài</p>
          <MembersTable members={summary!.externalSupportMembers} emptyText="" />
        </div>
      )}
    </div>
  );
}
