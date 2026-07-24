/**
 * VisitRequestDetail — read-only detail of ONE assigned visit request, resolved for the exact target
 * visit instance from the route.
 *
 * Every field is real Pure V2 data for that instance (delegation, registrant, planned window, purpose,
 * working content, guests) fetched from the visit-process detail API — never hardcoded, never a sibling
 * campus, never a request-level fallback. The route `:id` is the visitInstanceId (same id the process
 * page resolves permissions with); `:type` is the task label the caller navigated with.
 */

import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { useNavigate, useParams, useLocation } from 'react-router-dom';
import { Info, Clock, User, Users, Calendar, FileText, Building2, Mail, Phone } from 'lucide-react';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type { VisitProcessDetail } from '../../../features/delegations/types/delegations.types';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { LoadingState, ErrorState } from '../../../shared/components/state';

const INSTANCE_STATUS_LABEL: Record<string, string> = {
  ASSIGNED: 'Đã phân công',
  BEFORE_VISIT: 'Trước tiếp khách',
  DURING_VISIT: 'Đang tiếp khách',
  AFTER_VISIT: 'Sau tiếp khách',
  CLOSED: 'Đã đóng',
  CANCELLED: 'Đã hủy',
};

export function VisitRequestDetail() {
  const navigate = useNavigate();
  const { id, type } = useParams();
  const location = useLocation();
  const returnUrl = (location.state as { returnTo?: string } | null)?.returnTo ?? '/dashboard/visit';

  const numericId = Number(id);
  const hasNumericId = Number.isFinite(numericId) && numericId > 0;

  const [detail, setDetail] = useState<VisitProcessDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

  const load = useCallback(async () => {
    if (!hasNumericId) { setError(new Error('invalid id')); setLoading(false); return; }
    setLoading(true);
    setError(null);
    try {
      // The route id is the visit instance; resolve its request id via the same permission call the
      // process page uses, then load the detail for exactly that (request, instance) pair.
      const perm = await delegationsApi.getVisitProcessPermissions(numericId);
      const d = await delegationsApi.getVisitProcessDetail(perm.visitRequestId, perm.visitInstanceId);
      setDetail(d);
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }, [numericId, hasNumericId]);

  useEffect(() => { void load(); }, [load]);

  const summary = detail?.requestSummary ?? null;
  const senderName = summary?.registrantName?.trim() || '—';
  const senderOrg = summary?.registrantOrganization?.trim() || null;
  const purpose = (summary?.workingContent ?? summary?.purpose ?? '')?.trim();
  const guests = summary?.guestMembers ?? [];
  const statusLabel = detail ? (INSTANCE_STATUS_LABEL[detail.instanceStatus] ?? detail.instanceStatus) : '';

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-24 relative animate-in fade-in duration-500">
      {/* Breadcrumb */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate(returnUrl)} className="hover:text-[#004c91] transition-colors outline-none">Quản lý tiếp khách</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate(`/dashboard/visit/process/${id}`)} className="hover:text-[#004c91] transition-colors outline-none">Quy trình tiếp khách</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Chi tiết yêu cầu</span>
      </div>

      <div className="mb-8 flex items-center gap-5 bg-white p-6 rounded-3xl shadow-[0_4px_20px_-4px_rgba(0,0,0,0.05)] border border-gray-100">
        <button onClick={() => navigate(-1)} className="flex items-center justify-center p-3 rounded-2xl border border-gray-200 bg-white shadow-sm hover:border-[#004c91] hover:text-[#004c91] hover:bg-blue-50 transition-all outline-none">
          <Info className="w-6 h-6" />
        </button>
        <div>
          <h1 className="text-2xl lg:text-3xl font-black text-[#004c91] tracking-tight uppercase">Chi tiết yêu cầu</h1>
          {type && <p className="text-sm font-medium text-gray-500 mt-1">Loại nhiệm vụ: {type}</p>}
        </div>
      </div>

      <div className="w-full max-w-4xl mx-auto">
        {loading ? (
          <LoadingState className="h-64" />
        ) : error ? (
          <div className="flex items-center justify-center h-64">
            <ErrorState error={error} onRetry={() => { void load(); }} />
          </div>
        ) : !detail ? (
          <LoadingState className="h-64" />
        ) : (
          <div className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-gray-100 overflow-hidden">
            <div className="flex items-center gap-4 bg-[#004c91] p-8 pb-6">
              <div className="p-3 bg-white/10 text-white rounded-2xl shadow-inner border border-white/20"><Info className="w-7 h-7" /></div>
              <div>
                <h2 className="text-2xl font-black text-white tracking-tight">{detail.delegationName}</h2>
                <p className="text-sm font-medium text-blue-100 mt-1">
                  {detail.campusName ? `${detail.campusName} · ` : ''}{statusLabel}
                </p>
              </div>
            </div>

            <div className="p-8 pt-6 space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Field icon={<User className="w-4 h-4" />} label="Người gửi">
                  <span className="text-sm font-black text-[#004c91]">{senderName}</span>
                  {senderOrg && <span className="block text-xs font-medium text-gray-500 mt-0.5">{senderOrg}</span>}
                </Field>
                <Field icon={<Users className="w-4 h-4" />} label="Đoàn khách">
                  <span className="text-sm font-black text-[#004c91]">{detail.delegationName}</span>
                </Field>
                <Field icon={<Building2 className="w-4 h-4" />} label="Cơ sở">
                  <span className="text-sm font-bold text-gray-800">{detail.campusName ?? '—'}</span>
                </Field>
                <Field icon={<Clock className="w-4 h-4" />} label="Trạng thái">
                  <span className="text-sm font-bold text-gray-800">{statusLabel}</span>
                </Field>
                <div className="md:col-span-2">
                  <Field icon={<Calendar className="w-4 h-4" />} label="Thời gian dự kiến">
                    <span className="text-sm font-bold text-gray-800">
                      {formatVietnamDateTime(detail.plannedStartAt)} — {formatVietnamDateTime(detail.plannedEndAt)}
                    </span>
                  </Field>
                </div>
              </div>

              {(summary?.registrantEmail || summary?.registrantPhone) && (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {summary?.registrantEmail && (
                    <Field icon={<Mail className="w-4 h-4" />} label="Email người gửi">
                      <span className="text-sm font-medium text-gray-800 break-all">{summary.registrantEmail}</span>
                    </Field>
                  )}
                  {summary?.registrantPhone && (
                    <Field icon={<Phone className="w-4 h-4" />} label="Điện thoại người gửi">
                      <span className="text-sm font-medium text-gray-800">{summary.registrantPhone}</span>
                    </Field>
                  )}
                </div>
              )}

              <div className="flex flex-col gap-3">
                <div className="flex items-center gap-2 text-gray-400">
                  <FileText className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung làm việc</span>
                </div>
                <div className="p-6 bg-gradient-to-br from-[#f8fafc] to-[#f1f5f9] rounded-2xl text-[15px] font-medium text-gray-700 leading-relaxed border border-gray-200 whitespace-pre-line">
                  {purpose ? purpose : <span className="italic text-gray-400">Chưa có nội dung làm việc.</span>}
                </div>
              </div>

              <div className="flex flex-col gap-3">
                <div className="flex items-center gap-2 text-gray-400">
                  <Users className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Thành viên đoàn ({guests.length})</span>
                </div>
                {guests.length === 0 ? (
                  <p className="text-sm italic text-gray-400">Chưa có thành viên đoàn.</p>
                ) : (
                  <ul className="divide-y divide-gray-100 rounded-2xl border border-gray-200 overflow-hidden">
                    {guests.map((g) => (
                      <li key={g.guestMemberId} className="flex items-center justify-between px-4 py-3 bg-white">
                        <div className="min-w-0">
                          <p className="text-sm font-bold text-gray-800 truncate">{g.fullName}</p>
                          <p className="text-xs text-gray-500 truncate">
                            {[g.jobTitle, g.organization, g.nationality].filter(Boolean).join(' · ') || '—'}
                          </p>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function Field({ icon, label, children }: { icon: ReactNode; label: string; children: ReactNode }) {
  return (
    <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100">
      <div className="flex items-center gap-2 text-gray-400 mb-2">
        {icon}
        <span className="text-[11px] font-bold uppercase tracking-wider">{label}</span>
      </div>
      {children}
    </div>
  );
}
