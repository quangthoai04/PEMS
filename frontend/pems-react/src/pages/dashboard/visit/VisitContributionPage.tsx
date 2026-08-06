/**
 * VisitContributionPage — "Đóng góp kết quả chuyến thăm" (spec §5.3 / §7).
 *
 * Phase 1 skeleton: a read-only summary of the visit (request / agenda / participants / logistics)
 * plus workspace placeholders (minutes / media / news). Access is decided by the backend
 * (GET /delegations/visit-instances/{id}/contribution): Host, an ACCEPTED/ASSIGNED participant, or a
 * Department user with a real logistics relation. The page NEVER falls back to a permissive default —
 * a 403/404 renders Access Denied, and each section honors the booleans the backend returns.
 */
import React, { useEffect, useState } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import {
  Lock, ArrowLeft, FileText, Users, Clock, Building2, AlertCircle, User, ScrollText,
  Loader2, CalendarDays, MapPin, ClipboardList
} from 'lucide-react';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import { MinutesContributionSection } from './components/MinutesContributionSection';
import { MediaContributionSection } from './components/MediaContributionSection';
import { NewsContributionSection } from './components/NewsContributionSection';
import type {
  ContributionPage, ContributionLogisticsItem,
} from '../../../features/delegations/types/delegations.types';
import { toVietnamDateTimeLocalInput } from '../../../shared/utils/vietnamTime';

const INSTANCE_STATUS_LABELS: Record<string, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ xử lý tại cơ sở',
  ASSIGNED: 'Đã duyệt và phân công',
  BEFORE_VISIT: 'Trước tiếp khách',
  DURING_VISIT: 'Đang tiếp khách',
  AFTER_VISIT: 'Chờ đóng đoàn',
  CLOSED: 'Đã đóng đoàn',
  CANCELLED: 'Đã hủy',
  REJECTED: 'Từ chối',
};
const INSTANCE_STATUS_CLASS: Record<string, string> = {
  ASSIGNED: 'bg-cyan-50 text-cyan-700 border-cyan-200',
  BEFORE_VISIT: 'bg-blue-50 text-blue-700 border-blue-200',
  DURING_VISIT: 'bg-green-50 text-green-700 border-green-200',
  AFTER_VISIT: 'bg-orange-50 text-orange-700 border-orange-200',
  CLOSED: 'bg-slate-100 text-slate-700 border-slate-300',
  CANCELLED: 'bg-gray-100 text-gray-600 border-gray-200',
};
const RELATION_LABELS: Record<string, string> = {
  HOST: 'Người phụ trách tiếp đón',
  IC_SUPPORT: 'Hỗ trợ IC',
  DEPARTMENT_RELATED: 'Phòng ban hỗ trợ',
  STUDENT_RELATED: 'Sinh viên hỗ trợ',
};
const PARTICIPANT_ROLE_LABELS: Record<string, string> = {
  IC_HOST: 'Host', IC_SUPPORT: 'Hỗ trợ IC', DEPT_SUPPORT: 'Phòng ban', STUDENT: 'Sinh viên',
};
const PARTICIPANT_STATUS_LABELS: Record<string, string> = {
  INVITED: 'Chờ phản hồi', ACCEPTED: 'Đã nhận lời', ASSIGNED: 'Đã được giao',
  DECLINED: 'Đã từ chối', REMOVED: 'Đã gỡ',
};
const LOGISTICS_STATUS_LABELS: Record<string, string> = {
  REQUESTED: 'Đã yêu cầu', ASSIGNED: 'Đã phân công', ACCEPTED: 'Đã chấp nhận',
  IN_PROGRESS: 'Đang xử lý', DONE: 'Hoàn tất', REJECTED: 'Từ chối',
  CHANGE_PROPOSED: 'Đề xuất thay đổi', CANCELLED: 'Đã hủy', DECLINED: 'Từ chối phân công',
};

const pad = (n: number) => String(n).padStart(2, '0');
const fmtDateTime = (v?: string | null) => {
  if (!v) return '—';
  const local = toVietnamDateTimeLocalInput(v); // giờ Việt Nam cố định
  if (!local) return String(v);
  return `${local.slice(11, 16)} ${local.slice(8, 10)}/${local.slice(5, 7)}/${local.slice(0, 4)}`;
};

/** Section phẳng: header nhỏ + divider mỏng, không card/ô icon lớn */
function Section({ icon, title, badge, children }: {
  icon: React.ReactNode; title: string; badge?: React.ReactNode; children: React.ReactNode;
}) {
  return (
    <section className="px-4 sm:px-5 py-4">
      <div className="flex flex-wrap items-center gap-2 mb-2">
        <span className="text-[#004c91]">{icon}</span>
        <h2 className="text-sm font-black text-[#004c91] flex-1">{title}</h2>
        {badge}
      </div>
      {children}
    </section>
  );
}

function Field({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div>
      <p className="text-[10px] font-bold uppercase tracking-wide text-slate-400">{label}</p>
      <p className="text-sm font-semibold text-slate-800 break-words">{value || '—'}</p>
    </div>
  );
}



export function VisitContributionPage() {
  const { visitInstanceId } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const returnUrl = location.state?.returnTo ?? '/dashboard/visit';

  const [loading, setLoading] = useState(true);
  const [denied, setDenied] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [data, setData] = useState<ContributionPage | null>(null);

  const loadData = React.useCallback(() => {
    let active = true;
    if (!visitInstanceId || Number.isNaN(Number(visitInstanceId))) {
      setLoading(false);
      setDenied(true);
      return;
    }
    setLoading(true);
    setDenied(false);
    setError(null);
    delegationsApi.getVisitInstanceContribution(visitInstanceId)
      .then((res) => { if (active) setData(res); })
      .catch((e: any) => {
        if (!active) return;
        const status = e?.response?.status;
        // The backend is the single gate: 403 (no relation) / 404 (no such instance) → Access Denied.
        if (status === 403 || status === 404) setDenied(true);
        else setError('Không thể tải trang đóng góp kết quả. Vui lòng thử lại sau.');
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [visitInstanceId]);

  useEffect(() => {
    const cleanup = loadData();
    return cleanup;
  }, [loadData]);

  const Breadcrumb = (
    <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-6">
      <span>Dashboard</span>
      <span>/</span>
      <span className="cursor-pointer hover:text-[#004c91] transition-colors" onClick={() => navigate(returnUrl)}>
        Quản lý tiếp khách
      </span>
      <span>/</span>
      <span className="text-[#004c91] font-bold">Đóng góp kết quả</span>
    </div>
  );

  if (loading) {
    return (
      <div className="p-4 sm:p-6 md:p-8 w-full mx-auto">
        {Breadcrumb}
        <div className="flex flex-col items-center justify-center min-h-[320px] gap-3 text-slate-400">
          <Loader2 className="w-8 h-8 animate-spin text-[#004c91]" />
          <p className="text-sm font-semibold">Đang tải trang đóng góp kết quả...</p>
        </div>
      </div>
    );
  }

  if (denied) {
    return (
      <div className="p-4 sm:p-6 md:p-8 w-full mx-auto">
        {Breadcrumb}
        <div className="bg-white rounded-[2rem] border border-gray-200 p-16 text-center shadow-sm flex flex-col items-center justify-center min-h-[350px]">
          <div className="w-20 h-20 bg-rose-50 rounded-full flex items-center justify-center mb-6">
            <Lock className="w-10 h-10 text-rose-400 stroke-[1.5]" />
          </div>
          <h2 className="text-xl font-bold text-slate-800 mb-2">Không có quyền truy cập</h2>
          <p className="text-gray-500 font-medium max-w-sm mx-auto leading-relaxed text-sm mb-6">
            Bạn không có quyền đóng góp kết quả cho chuyến thăm này.
          </p>
          <button onClick={() => navigate(returnUrl)}
            className="px-6 py-2.5 rounded-xl bg-[#004c91] text-white text-sm font-bold hover:bg-[#003b70] transition-colors outline-none flex items-center gap-2">
            <ArrowLeft className="w-4 h-4" /> Về danh sách
          </button>
        </div>
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="p-4 sm:p-6 md:p-8 w-full mx-auto">
        {Breadcrumb}
        <div className="bg-white rounded-2xl border border-gray-200 p-12 text-center shadow-sm flex flex-col items-center justify-center min-h-[300px]">
          <AlertCircle className="w-12 h-12 text-orange-400 mb-4" />
          <p className="text-slate-600 font-semibold mb-6">{error || 'Không có dữ liệu.'}</p>
          <button onClick={() => navigate(0)}
            className="px-6 py-2.5 rounded-xl bg-[#004c91] text-white text-sm font-bold hover:bg-[#003b70] transition-colors">
            Thử lại
          </button>
        </div>
      </div>
    );
  }

  const { permissions: perm, summary, workspace } = data;
  const req = summary.request;
  const visitType = req?.visitType === 'OTHER' ? (req?.visitTypeOther || 'Khác') : req?.visitType;

  const scopedLogistics: ContributionLogisticsItem[] = summary.logistics || [];

  const userStr = typeof window !== 'undefined' ? localStorage.getItem('currentUser') : null;
  const currentUser = userStr ? JSON.parse(userStr) : null;
  const isDeptRole = currentUser?.roleCode === 'DEPARTMENT' || currentUser?.roleCode === 'DEPT_STAFF' || (perm.relation && (perm.relation.includes('DEPARTMENT') || perm.relation.includes('DEPT_STAFF')));
  const isReadOnlyContribution = perm.isReadOnly || isDeptRole;

  return (
    <div className="p-4 sm:p-6 md:p-8 w-full mx-auto pb-16 animate-in fade-in duration-300">
      {Breadcrumb}

      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm divide-y divide-slate-100">
        {/* Header compact */}
        <div className="px-4 sm:px-5 py-4">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div className="min-w-0">
              <h1 className="text-lg md:text-xl font-black text-[#004c91]">Đóng góp kết quả chuyến thăm</h1>
              <p className="text-sm font-bold text-slate-800 mt-0.5">{summary.delegationName}</p>
            </div>
            <div className="flex flex-wrap items-center gap-1.5">
              <span className={`inline-flex px-2.5 py-0.5 rounded-full border text-[11px] font-black ${INSTANCE_STATUS_CLASS[summary.instanceStatus] || 'bg-gray-100 text-gray-700 border-gray-200'
                }`}>
                {INSTANCE_STATUS_LABELS[summary.instanceStatus] || summary.instanceStatus}
              </span>
              <span className="inline-flex px-2.5 py-0.5 rounded-full border border-indigo-200 bg-indigo-50 text-indigo-700 text-[11px] font-black">
                Vai trò: {RELATION_LABELS[perm.relation] || perm.relation}
              </span>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-2 text-xs text-slate-600 font-semibold">
            <span className="inline-flex items-center gap-1.5">
              <Building2 className="w-3.5 h-3.5 text-[#f37021] shrink-0" /> {summary.campusName || '—'}
            </span>
            <span className="inline-flex items-center gap-1.5">
              <Clock className="w-3.5 h-3.5 text-[#f37021] shrink-0" /> {fmtDateTime(summary.plannedStartAt)}
            </span>
            <span className="inline-flex items-center gap-1.5">
              <User className="w-3.5 h-3.5 text-[#f37021] shrink-0" /> Người phụ trách tiếp đón: {summary.hostName || 'Chưa phân công'}
            </span>
            <span className="inline-flex items-center gap-1.5">
              <Users className="w-3.5 h-3.5 text-[#f37021] shrink-0" /> {summary.guestCount} khách
            </span>
          </div>

          {isReadOnlyContribution && (
            <p className="mt-2 inline-flex items-center gap-1.5 text-xs font-semibold text-slate-500">
              <Lock className="w-3.5 h-3.5 shrink-0" />
              {isDeptRole ? 'Phòng ban / Nhân sự phòng ban chỉ có quyền xem thông tin đóng góp.' : 'Chuyến thăm đã đóng/hủy — trang ở chế độ chỉ xem.'}
            </p>
          )}
        </div>

        {/* ── Nhóm A: Summary read-only ── */}
        {perm.canViewRequestSummary && req && (
          <Section icon={<FileText className="w-4 h-4" />} title="Thông tin yêu cầu">
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-2">
              <Field label="Tên đoàn" value={req.delegationName} />
              <Field label="Tổ chức/đơn vị" value={req.registrantOrganization} />
              <Field label="Loại chuyến thăm" value={visitType} />
              <Field label="Mục đích" value={req.purpose} />
              <Field label="Ngôn ngữ làm việc" value={req.workingLanguage} />
            </div>
            {req.workingContent && (
              <div className="mt-2 pt-2 border-t border-slate-100">
                <Field label="Nội dung làm việc" value={req.workingContent} />
              </div>
            )}
            {/* The OPERATIONAL contact of THIS campus, under its own heading and in full. It used to
                be one unlabelled "Người liên hệ" line carrying a name, sitting among the delegation's
                fields — which read as the registrant, and gave a contributor no way to reach anyone.
                Registrant and contact are different people and are now visibly different blocks. */}
            <div className="mt-2 pt-2 border-t border-slate-100">
              <p className="mb-1.5 text-[11px] font-bold uppercase tracking-wide text-[#004c91]">
                Đầu mối đoàn khách phối hợp tại cơ sở
              </p>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-6 gap-y-2">
                <Field label="Họ và tên" value={req.operationalContactFullName} />
                <Field label="Đơn vị công tác" value={req.operationalContactOrganization} />
                <Field label="Chức vụ" value={req.operationalContactJobTitle} />
                <Field label="Số điện thoại" value={req.operationalContactPhone} />
                <Field label="Email" value={req.operationalContactEmail} />
              </div>
            </div>
          </Section>
        )}

        {perm.canViewAgendaSummary && (
          <Section icon={<CalendarDays className="w-4 h-4" />} title="Lịch trình"
            badge={<span className="text-[11px] font-bold text-slate-400">{summary.agenda.length} mục</span>}>
            {summary.agenda.length === 0 ? (
              <p className="text-xs font-semibold text-slate-400">Chưa có dữ liệu cho phần này.</p>
            ) : (
              <ol className="space-y-2">
                {summary.agenda.map((a) => (
                  <li key={a.agendaId} className="flex gap-3">
                    <div className="text-xs font-bold text-[#004c91] whitespace-nowrap pt-0.5 w-[100px] shrink-0">
                      {fmtDateTime(a.startTime).slice(0, 5)}
                      {a.endTime ? ` - ${fmtDateTime(a.endTime).slice(0, 5)}` : ''}
                    </div>
                    <div className="min-w-0 border-l-2 border-slate-100 pl-3">
                      <p className="text-sm font-bold text-slate-800">{a.title}</p>
                      {(a.location || a.responsibleName || a.templateResponsibleRoleLabel) && (
                        <p className="text-xs text-slate-500 font-semibold mt-0.5">
                          {a.location && <span className="inline-flex items-center gap-1 mr-3"><MapPin className="w-3 h-3" /> {a.location}</span>}
                          {(a.responsibleName || a.templateResponsibleRoleLabel) && <>Phụ trách: {a.responsibleName || a.templateResponsibleRoleLabel}</>}
                        </p>
                      )}
                    </div>
                  </li>
                ))}
              </ol>
            )}
          </Section>
        )}

        {perm.canViewParticipantSummary && (
          <Section icon={<Users className="w-4 h-4" />} title="Thành phần tham gia"
            badge={<span className="text-[11px] font-bold text-slate-400">{summary.participants.length} người</span>}>
            {summary.participants.length === 0 ? (
              <p className="text-xs font-semibold text-slate-400">Chưa có dữ liệu cho phần này.</p>
            ) : (
              <div className="divide-y divide-slate-100">
                {summary.participants.map((p) => (
                  <div key={p.participantId} className="flex flex-wrap items-center justify-between gap-2 py-1.5">
                    <div className="min-w-0">
                      <p className="text-sm font-bold text-slate-800">
                        {p.fullName}{p.isHost && <span className="ml-2 text-[10px] font-black text-emerald-600">(Phụ trách)</span>}
                        <span className="ml-2 text-xs text-slate-500 font-semibold">
                          {PARTICIPANT_ROLE_LABELS[p.participantRole] || p.participantRole}
                          {p.departmentName ? ` · ${p.departmentName}` : ''}
                        </span>
                      </p>
                    </div>
                    <span className="text-[11px] font-bold text-slate-500">
                      {PARTICIPANT_STATUS_LABELS[p.status] || p.status}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </Section>
        )}

        {perm.canViewLogisticsSummary && (
          <Section icon={<ClipboardList className="w-4 h-4" />} title="Hậu cần liên quan"
            badge={perm.canViewRelatedLogisticsOnly
              ? <span className="text-[11px] font-bold text-slate-400">Chỉ hạng mục liên quan bạn</span>
              : <span className="text-[11px] font-bold text-slate-400">{scopedLogistics.length} mục</span>}>
            {scopedLogistics.length === 0 ? (
              <p className="text-xs font-semibold text-slate-400">Chưa có dữ liệu cho phần này.</p>
            ) : (
              <div className="divide-y divide-slate-100">
                {scopedLogistics.map((l) => (
                  <div key={l.logisticsItemId} className="flex flex-wrap items-center justify-between gap-2 py-1.5">
                    <div className="min-w-0">
                      <p className="text-sm font-bold text-slate-800">
                        {l.title}
                        <span className="ml-2 text-xs text-slate-500 font-semibold">
                          {l.departmentName || '—'}{l.assignedToName ? ` · ${l.assignedToName}` : ''}
                        </span>
                      </p>
                    </div>
                    <span className="text-[11px] font-bold text-slate-500">
                      {LOGISTICS_STATUS_LABELS[l.status] || l.status}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </Section>
        )}

        {/* ── Nhóm B: Workspace đóng góp ── */}
        <Section icon={<ScrollText className="w-4 h-4 text-[#f37021]" />} title="Đóng góp kết quả">
          <div className="divide-y divide-slate-100">
            {perm.canViewMinutes && workspace.minutes && (
              <MinutesContributionSection
                visitInstanceId={visitInstanceId}
                data={workspace.minutes}
                canView={perm.canViewMinutes}
                instanceStatus={summary.instanceStatus}
                onChanged={loadData}
                isReadOnly={isReadOnlyContribution}
              />
            )}
            {perm.canViewMedia && workspace.media && (
              <MediaContributionSection
                visitInstanceId={visitInstanceId}
                data={workspace.media}
                canView={perm.canViewMedia}
                instanceStatus={summary.instanceStatus}
                onChanged={loadData}
                relation={perm.relation}
                isReadOnly={isReadOnlyContribution}
              />
            )}
            {perm.canViewNews && workspace.news && (
              <NewsContributionSection
                visitInstanceId={visitInstanceId}
                data={workspace.news}
                canView={perm.canViewNews}
                instanceStatus={summary.instanceStatus}
                onChanged={loadData}
                isReadOnly={isReadOnlyContribution}
              />
            )}
          </div>
        </Section>
      </div>
    </div>
  );
}
