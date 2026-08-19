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
import { useTranslation } from 'react-i18next';
import {
  Info, Clock, User, Users, Calendar, FileText, Building2, Mail, Phone,
  Languages, Camera, Car, StickyNote,
} from 'lucide-react';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type { VisitProcessDetail } from '../../../features/delegations/types/delegations.types';
import { formatLocalizedDateTime, type UiLanguage } from '../../../shared/utils/vietnamTime';
import { LoadingState, ErrorState } from '../../../shared/components/state';
import { useAuth } from '../../../shared/hooks/useAuth';

export function VisitRequestDetail() {
  const navigate = useNavigate();
  const { id, type } = useParams();
  const location = useLocation();
  const returnUrl = (location.state as { returnTo?: string } | null)?.returnTo ?? '/dashboard/visit';

  const { effectiveRole } = useAuth();
  const { t, i18n } = useTranslation('visitTasks');
  // Shared by every business role — same VISITOR-only bilingual boundary as Sidebar/Profile.
  const isVisitor = effectiveRole === 'VISITOR';
  const language = (isVisitor ? i18n.language : 'vi') as UiLanguage;
  const tt = (key: string, options?: Record<string, unknown>) => t(key, { ...(options || {}), lng: language });

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
  // Purpose and working content are two different answers on the form — why the delegation is
  // coming, and what will actually be done. Coalescing them showed only one, and which one you got
  // depended on whether working content happened to be filled in.
  const purpose = summary?.purpose?.trim() || '';
  const workingContent = summary?.workingContent?.trim() || '';
  const guests = summary?.guestMembers ?? [];
  const supportMembers = summary?.externalSupportMembers ?? [];
  const statusLabel = detail ? tt(`requestDetail.status.${detail.instanceStatus}`, { defaultValue: detail.instanceStatus }) : '';
  const visitTypeLabel = summary?.visitType
    ? (summary.visitType === 'OTHER'
        ? (summary.visitTypeOther?.trim() || tt('requestDetail.visitType.OTHER'))
        : tt(`requestDetail.visitType.${summary.visitType}`, { defaultValue: summary.visitType }))
    : null;
  const workingLanguageLabel = summary?.workingLanguage
    ? tt(`requestDetail.workingLanguage.${summary.workingLanguage}`, { defaultValue: summary.workingLanguage })
    : null;
  const mediaConsentLabel = summary?.mediaConsentStatus
    ? tt(`requestDetail.mediaConsent.${summary.mediaConsentStatus}`, { defaultValue: summary.mediaConsentStatus })
    : null;
  const opContact = summary
    ? [summary.operationalContactFullName, summary.operationalContactJobTitle, summary.operationalContactOrganization]
        .map(v => v?.trim()).filter(Boolean).join(' — ')
    : '';
  const opContactReach = summary
    ? [summary.operationalContactPhone, summary.operationalContactEmail]
        .map(v => v?.trim()).filter(Boolean).join(' · ')
    : '';

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-24 relative animate-in fade-in duration-500">
      {/* Breadcrumb */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none">{tt('requestDetail.breadcrumb.dashboard')}</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate(returnUrl)} className="hover:text-[#004c91] transition-colors outline-none">{tt('requestDetail.breadcrumb.management')}</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate(`/dashboard/visit/process/${id}`)} className="hover:text-[#004c91] transition-colors outline-none">{tt('requestDetail.breadcrumb.process')}</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">{tt('requestDetail.breadcrumb.detail')}</span>
      </div>

      <div className="mb-8 flex items-center gap-5 bg-white p-6 rounded-3xl shadow-[0_4px_20px_-4px_rgba(0,0,0,0.05)] border border-gray-100">
        <button onClick={() => navigate(-1)} className="flex items-center justify-center p-3 rounded-2xl border border-gray-200 bg-white shadow-sm hover:border-[#004c91] hover:text-[#004c91] hover:bg-blue-50 transition-all outline-none">
          <Info className="w-6 h-6" />
        </button>
        <div>
          <h1 className="text-2xl lg:text-3xl font-black text-[#004c91] tracking-tight uppercase">{tt('requestDetail.title')}</h1>
          {type && <p className="text-sm font-normal text-gray-500 mt-1">{tt('requestDetail.taskType')}: {type}</p>}
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
                <p className="text-sm font-normal text-blue-100 mt-1">
                  {detail.campusName ? `${detail.campusName} · ` : ''}{statusLabel}
                </p>
              </div>
            </div>

            <div className="p-8 pt-6 space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Field icon={<User className="w-4 h-4" />} label={tt('requestDetail.fields.sender')}>
                  <span className="text-sm font-black text-[#004c91]">{senderName}</span>
                  {senderOrg && <span className="block text-xs font-normal text-gray-500 mt-0.5">{senderOrg}</span>}
                </Field>
                <Field icon={<Users className="w-4 h-4" />} label={tt('requestDetail.fields.delegation')}>
                  <span className="text-sm font-black text-[#004c91]">{detail.delegationName}</span>
                </Field>
                <Field icon={<Building2 className="w-4 h-4" />} label={tt('requestDetail.fields.campus')}>
                  <span className="text-sm font-normal text-gray-800">{detail.campusName ?? '—'}</span>
                </Field>
                <Field icon={<Clock className="w-4 h-4" />} label={tt('requestDetail.fields.status')}>
                  <span className="text-sm font-normal text-gray-800">{statusLabel}</span>
                </Field>
                <div className="md:col-span-2">
                  <Field icon={<Calendar className="w-4 h-4" />} label={tt('requestDetail.fields.plannedTime')}>
                    <span className="text-sm font-normal text-gray-800">
                      {formatLocalizedDateTime(detail.plannedStartAt, language)} — {formatLocalizedDateTime(detail.plannedEndAt, language)}
                    </span>
                  </Field>
                </div>
              </div>

              {(summary?.registrantEmail || summary?.registrantPhone
                || summary?.registrantJobTitle || summary?.registrantNationality) && (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {summary?.registrantJobTitle && (
                    <Field icon={<User className="w-4 h-4" />} label={tt('requestDetail.fields.senderJobTitle')}>
                      <span className="text-sm font-normal text-gray-800">{summary.registrantJobTitle}</span>
                    </Field>
                  )}
                  {summary?.registrantNationality && (
                    <Field icon={<User className="w-4 h-4" />} label={tt('requestDetail.fields.senderNationality')}>
                      <span className="text-sm font-normal text-gray-800">{summary.registrantNationality}</span>
                    </Field>
                  )}
                  {summary?.registrantEmail && (
                    <Field icon={<Mail className="w-4 h-4" />} label={tt('requestDetail.fields.senderEmail')}>
                      <span className="text-sm font-normal text-gray-800 break-all">{summary.registrantEmail}</span>
                    </Field>
                  )}
                  {summary?.registrantPhone && (
                    <Field icon={<Phone className="w-4 h-4" />} label={tt('requestDetail.fields.senderPhone')}>
                      <span className="text-sm font-normal text-gray-800">{summary.registrantPhone}</span>
                    </Field>
                  )}
                </div>
              )}

              {/* Yêu cầu bổ sung — the same block the guest filled in on the form. */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {visitTypeLabel && (
                  <Field icon={<Info className="w-4 h-4" />} label={tt('requestDetail.fields.visitType')}>
                    <span className="text-sm font-normal text-gray-800">{visitTypeLabel}</span>
                  </Field>
                )}
                {workingLanguageLabel && (
                  <Field icon={<Languages className="w-4 h-4" />} label={tt('requestDetail.fields.workingLanguage')}>
                    <span className="text-sm font-normal text-gray-800">{workingLanguageLabel}</span>
                  </Field>
                )}
                {mediaConsentLabel && (
                  <Field icon={<Camera className="w-4 h-4" />} label={tt('requestDetail.fields.mediaConsent')}>
                    <span className="text-sm font-normal text-gray-800">{mediaConsentLabel}</span>
                  </Field>
                )}
                <Field icon={<Car className="w-4 h-4" />} label={tt('requestDetail.fields.transportation')}>
                  {summary?.transportationNote?.trim()
                    ? <span className="text-sm font-normal text-gray-800 whitespace-pre-line">{summary.transportationNote}</span>
                    : <span className="text-sm italic text-gray-400">{tt('requestDetail.fields.transportationEmpty')}</span>}
                </Field>
                {opContact && (
                  <div className="md:col-span-2">
                    <Field icon={<User className="w-4 h-4" />} label={tt('requestDetail.fields.operationalContact')}>
                      <span className="text-sm font-normal text-gray-800">{opContact}</span>
                      {opContactReach && (
                        <span className="block text-xs font-normal text-gray-500 mt-0.5 break-all">{opContactReach}</span>
                      )}
                    </Field>
                  </div>
                )}
              </div>

              {/* Purpose and working content are asked separately and answered separately. */}
              <div className="flex flex-col gap-3">
                <div className="flex items-center gap-2 text-gray-400">
                  <FileText className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">{tt('requestDetail.fields.purpose')}</span>
                </div>
                <div className="p-6 bg-gradient-to-br from-[#f8fafc] to-[#f1f5f9] rounded-2xl text-[15px] font-normal text-gray-700 leading-relaxed border border-gray-200 whitespace-pre-line">
                  {purpose ? purpose : <span className="italic text-gray-400">{tt('requestDetail.fields.purposeEmpty')}</span>}
                </div>
              </div>

              <div className="flex flex-col gap-3">
                <div className="flex items-center gap-2 text-gray-400">
                  <FileText className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">{tt('requestDetail.fields.workingContent')}</span>
                </div>
                <div className="p-6 bg-gradient-to-br from-[#f8fafc] to-[#f1f5f9] rounded-2xl text-[15px] font-normal text-gray-700 leading-relaxed border border-gray-200 whitespace-pre-line">
                  {workingContent ? workingContent : <span className="italic text-gray-400">{tt('requestDetail.fields.workingContentEmpty')}</span>}
                </div>
              </div>

              {/* Shown whether or not the guest wrote anything — same treatment as "Mục đích" and
                  "Nội dung làm việc" above. Silence and an unasked question look identical once the
                  block is gone, and only one of them means there is nothing to prepare for. */}
              <div className="flex flex-col gap-3">
                <div className="flex items-center gap-2 text-gray-400">
                  <StickyNote className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">{tt('requestDetail.fields.notes')}</span>
                </div>
                <div className="p-6 bg-gradient-to-br from-[#f8fafc] to-[#f1f5f9] rounded-2xl text-[15px] font-normal text-gray-700 leading-relaxed border border-gray-200 whitespace-pre-line">
                  {summary?.notes?.trim() ? summary.notes : <span className="italic text-gray-400">{tt('requestDetail.fields.notesEmpty')}</span>}
                </div>
              </div>

              <div className="flex flex-col gap-3">
                <div className="flex items-center gap-2 text-gray-400">
                  <Users className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">{tt('requestDetail.fields.guestMembersCount', { count: guests.length })}</span>
                </div>
                {guests.length === 0 ? (
                  <p className="text-sm italic text-gray-400">{tt('requestDetail.fields.guestMembersEmpty')}</p>
                ) : (
                  <ul className="divide-y divide-gray-100 rounded-2xl border border-gray-200 overflow-hidden">
                    {guests.map((g) => (
                      <li key={g.guestMemberId} className="flex items-center justify-between px-4 py-3 bg-white">
                        <div className="min-w-0">
                          <p className="text-sm font-bold text-gray-800 truncate" title={g.fullName}>{g.fullName}</p>
                          <p className="text-xs text-gray-500 truncate">
                            {[g.jobTitle, g.organization, g.nationality].filter(Boolean).join(' · ') || '—'}
                          </p>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              {/* Support members are a SEPARATE group with their own list — they arrive under
                  different rules from the delegation, and one running count would imply otherwise.
                  The backend has always sent them; this screen simply never showed them. */}
              {supportMembers.length > 0 && (
                <div className="flex flex-col gap-3">
                  <div className="flex items-center gap-2 text-gray-400">
                    <Users className="w-4 h-4" />
                    <span className="text-[11px] font-bold uppercase tracking-wider">{tt('requestDetail.fields.supportMembersCount', { count: supportMembers.length })}</span>
                  </div>
                  <ul className="divide-y divide-gray-100 rounded-2xl border border-gray-200 overflow-hidden">
                    {supportMembers.map((s) => (
                      <li key={s.guestMemberId} className="flex items-center justify-between px-4 py-3 bg-white">
                        <div className="min-w-0">
                          <p className="text-sm font-bold text-gray-800 truncate" title={s.fullName}>{s.fullName}</p>
                          <p className="text-xs text-gray-500 truncate">
                            {[s.jobTitle, s.organization, s.nationality].filter(Boolean).join(' · ') || '—'}
                          </p>
                        </div>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
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
