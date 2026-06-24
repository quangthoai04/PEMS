/**
 * SubmittedVisitRequestInfoPanel — read-only render of exactly what the guest submitted in
 * the visit-request form. Shared by every screen that needs the "submitted form snapshot"
 * (pre-approval review, approved/waiting-host detail, rejected detail).
 *
 * Pure presentation: it contains NO approve/reject/assign-host logic and never mutates data.
 * It also never renders host-created data (agendas, participants, logistics, minutes).
 */

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

const ReadOnlyField = ({ label, value }: { label: string; value?: string | null }) => (
  <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
    <p className="text-xs font-bold text-slate-500">{label}</p>
    <p className="mt-1 text-sm font-semibold text-slate-900 break-words">{value || '-'}</p>
  </div>
);

const SectionTitle = ({ index, children }: { index: number; children: React.ReactNode }) => (
  <h3 className="text-base sm:text-lg font-black text-[#004c91] border-b-2 border-[#f37021]/30 pb-2 mb-5 flex items-center gap-2 w-max pr-6">
    <span className="flex items-center justify-center w-6 h-6 rounded-full bg-[#f37021] text-white text-sm">{index}</span>
    {children}
  </h3>
);

const MemberTable = ({ members, emptyText }: { members: SubmittedGuestMember[]; emptyText: string }) => {
  if (!members.length) {
    return (
      <div className="bg-white border border-slate-200 rounded-xl p-4 text-center text-sm font-medium text-slate-500">
        {emptyText}
      </div>
    );
  }
  return (
    <div className="bg-white border border-slate-200 rounded-xl overflow-x-auto shadow-sm">
      <table className="w-full min-w-[640px] border-collapse text-sm">
        <thead className="bg-slate-50 border-b border-slate-200">
          <tr>
            <th className="p-3 text-center font-bold text-slate-700 w-14">STT</th>
            <th className="p-3 text-left font-bold text-slate-700 border-l border-slate-200">Họ và tên</th>
            <th className="p-3 text-left font-bold text-slate-700 border-l border-slate-200">Chức vụ</th>
            <th className="p-3 text-left font-bold text-slate-700 border-l border-slate-200">Đơn vị công tác</th>
            <th className="p-3 text-left font-bold text-slate-700 border-l border-slate-200">Quốc tịch</th>
          </tr>
        </thead>
        <tbody>
          {members.map((m, i) => (
            <tr key={m.guestMemberId} className="border-b border-slate-100 last:border-b-0">
              <td className="p-3 text-center font-bold text-slate-400">{i + 1}</td>
              <td className="p-3 border-l border-slate-100 font-medium text-slate-800">{m.fullName || '-'}</td>
              <td className="p-3 border-l border-slate-100 font-medium text-slate-700">{m.jobTitle || '-'}</td>
              <td className="p-3 border-l border-slate-100 font-medium text-slate-700">{m.organization || '-'}</td>
              <td className="p-3 border-l border-slate-100 font-medium text-slate-700">{m.nationality || '-'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};

export function SubmittedVisitRequestInfoPanel({ data }: { data: SubmittedVisitRequestFormDetail }) {
  const scopeLabel = VISIT_SCOPE_LABELS[data.visitScope] ?? data.visitScope;
  const visitTypeValue =
    data.visitType === 'OTHER' && data.visitTypeOther ? data.visitTypeOther : data.visitType;

  return (
    <div className="space-y-10">
      {/* 1. Người đăng ký */}
      <section>
        <SectionTitle index={1}>THÔNG TIN NGƯỜI ĐĂNG KÝ</SectionTitle>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-4">
          <ReadOnlyField label="Họ và tên" value={data.registrant.fullName} />
          <ReadOnlyField label="Quốc tịch" value={data.registrant.nationality} />
          <ReadOnlyField label="Đơn vị công tác" value={data.registrant.organization} />
          <ReadOnlyField label="Chức danh" value={data.registrant.jobTitle} />
          <ReadOnlyField label="Số điện thoại" value={data.registrant.phone} />
          <ReadOnlyField label="Email" value={data.registrant.email} />
        </div>
      </section>

      {/* 2. Thông tin chuyến thăm */}
      <section>
        <SectionTitle index={2}>THÔNG TIN CHUYẾN THĂM</SectionTitle>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-4">
          <ReadOnlyField label="Tên đoàn khách" value={data.delegationName} />
          <ReadOnlyField label="Phạm vi" value={scopeLabel} />
          <ReadOnlyField label="Loại hình" value={visitTypeValue} />
          <ReadOnlyField label="Nguồn tạo đơn" value={data.createdSource} />
        </div>
        <div className="mt-4 space-y-4">
          <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
            <p className="text-xs font-bold text-slate-500">Mục đích thăm FPTU</p>
            <p className="mt-1 text-sm font-medium text-slate-900 whitespace-pre-wrap">{data.purpose || '-'}</p>
          </div>
          <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
            <p className="text-xs font-bold text-slate-500">Nội dung làm việc</p>
            <p className="mt-1 text-sm font-medium text-slate-900 whitespace-pre-wrap">{data.workingContent || '-'}</p>
          </div>
        </div>
      </section>

      {/* 3. Cơ sở & thời gian dự kiến */}
      <section>
        <SectionTitle index={3}>CƠ SỞ &amp; THỜI GIAN DỰ KIẾN</SectionTitle>
        {data.campuses.length ? (
          <div className="bg-white border border-slate-200 rounded-xl overflow-x-auto shadow-sm">
            <table className="w-full min-w-[640px] border-collapse text-sm">
              <thead className="bg-slate-50 border-b border-slate-200">
                <tr>
                  <th className="p-3 text-left font-bold text-slate-700">Cơ sở</th>
                  <th className="p-3 text-left font-bold text-slate-700 border-l border-slate-200">Bắt đầu</th>
                  <th className="p-3 text-left font-bold text-slate-700 border-l border-slate-200">Kết thúc</th>
                </tr>
              </thead>
              <tbody>
                {data.campuses.map((c) => (
                  <tr key={c.visitInstanceId} className="border-b border-slate-100 last:border-b-0">
                    <td className="p-3 font-semibold text-slate-800">
                      {c.campusName || '-'}{c.campusCode ? <span className="text-slate-400 font-medium"> ({c.campusCode})</span> : null}
                    </td>
                    <td className="p-3 border-l border-slate-100 font-medium text-slate-700">{formatDateTime(c.plannedStartAt)}</td>
                    <td className="p-3 border-l border-slate-100 font-medium text-slate-700">{formatDateTime(c.plannedEndAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="bg-white border border-slate-200 rounded-xl p-4 text-center text-sm font-medium text-slate-500">
            Chưa có dữ liệu cơ sở
          </div>
        )}
      </section>

      {/* 4. Danh sách khách */}
      <section>
        <SectionTitle index={4}>DANH SÁCH KHÁCH</SectionTitle>
        <MemberTable members={data.guestMembers} emptyText="Chưa có dữ liệu khách" />
      </section>

      {/* 5. Team hỗ trợ khách */}
      <section>
        <SectionTitle index={5}>TEAM HỖ TRỢ KHÁCH</SectionTitle>
        <MemberTable members={data.externalSupportMembers} emptyText="Không có team hỗ trợ" />
      </section>

      {/* 6. Đầu mối liên hệ */}
      <section>
        <SectionTitle index={6}>ĐẦU MỐI LIÊN HỆ</SectionTitle>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-4">
          <ReadOnlyField label="Họ và tên" value={data.contactPerson.fullName} />
          <ReadOnlyField label="Đơn vị công tác" value={data.contactPerson.organization} />
          <ReadOnlyField label="Số điện thoại" value={data.contactPerson.phone} />
          <ReadOnlyField label="Email" value={data.contactPerson.email} />
        </div>
      </section>

      {/* 7. Yêu cầu & Xác nhận bổ sung */}
      <section>
        <SectionTitle index={7}>YÊU CẦU &amp; XÁC NHẬN BỔ SUNG</SectionTitle>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-4">
          <ReadOnlyField label="Ngôn ngữ làm việc" value={workingLanguageLabel(data.workingLanguage)} />
          <ReadOnlyField label="Đồng ý sử dụng hình ảnh & thông tin" value={mediaConsentLabel(data.mediaConsentStatus)} />
          <ReadOnlyField label="Phương tiện di chuyển" value={transportationLabel(data.transportationType)} />
          <ReadOnlyField label="Chi tiết phương tiện" value={data.transportationDetail} />
        </div>
        {data.mediaConsentNote ? (
          <div className="mt-4 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
            <p className="text-xs font-bold text-slate-500">Ghi chú về sử dụng hình ảnh</p>
            <p className="mt-1 text-sm font-medium text-slate-900 whitespace-pre-wrap">{data.mediaConsentNote}</p>
          </div>
        ) : null}
        <div className="mt-4 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
          <p className="text-xs font-bold text-slate-500">Ghi chú cho FPTU</p>
          <p className="mt-1 text-sm font-medium text-slate-900 whitespace-pre-wrap">{data.noteToFptu || '-'}</p>
        </div>
      </section>
    </div>
  );
}
