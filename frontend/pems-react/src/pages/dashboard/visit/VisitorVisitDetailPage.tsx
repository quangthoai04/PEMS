import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  MapPin, User, Mail, Phone, Calendar, Clock, Star, AlertCircle,
  Building, Globe, Briefcase, Car, Users, MessageSquare, ShieldCheck, CheckCircle2,
  CalendarCheck2, Info, ChevronRight, XCircle, Bell, Newspaper
} from 'lucide-react';
import { VisitorNotification, VisitorPublicNewsListItem } from '../../../features/delegations/types/delegations.types';
import { VisitProcessDetail, VisitProcessPermission } from '../../../features/delegations/types/delegations.types';
import { format } from 'date-fns';
import { vi, enUS } from 'date-fns/locale';
import { VisitFeedbackModal } from '../../../features/feedbacks/components/VisitFeedbackModal';

/** Visitor-only page (never rendered for Staff/HO/Department) — full bilingual, no role scoping needed. */
function useDateLocale() {
  const { i18n } = useTranslation();
  return i18n.language?.startsWith('en') ? enUS : vi;
}

interface VisitorVisitDetailPageProps {
  perm: VisitProcessPermission | null;
  detail: VisitProcessDetail | null;
}

// ─────────────────────────────────────────────────────────────────────────────
// 1. COMPONENT CHÍNH
// ─────────────────────────────────────────────────────────────────────────────
export function VisitorVisitDetailPage({ perm, detail }: VisitorVisitDetailPageProps) {
  const navigate = useNavigate();
  const { t } = useTranslation('visitorVisitDetail');

  if (!detail || !perm) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh]">
        <div className="w-12 h-12 border-4 border-[#004c91] border-t-transparent rounded-full animate-spin mb-4"></div>
        <p className="text-gray-500 font-medium">{t('loading')}</p>
      </div>
    );
  }

  const isCancelled = detail.instanceStatus === 'CANCELLED' || perm.requestStatus === 'CANCELLED';
  const summary = detail.requestSummary;

  // Block access if no host or not approved (unless cancelled)
  if (!detail.host && !isCancelled) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] text-center px-4">
        <div className="w-16 h-16 bg-slate-100 rounded-full flex items-center justify-center mb-4">
          <AlertCircle className="w-8 h-8 text-slate-400" />
        </div>
        <h2 className="text-xl font-bold text-slate-800 mb-2">{t('noHost.title')}</h2>
        <p className="text-slate-500 mb-6 max-w-md">{t('noHost.desc')}</p>
        <button
          onClick={() => navigate(-1)}
          className="px-6 py-2.5 bg-[#004c91] text-white font-bold rounded-xl shadow-sm hover:bg-[#003b70] transition-colors"
        >
          {t('noHost.back')}
        </button>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <div className="max-w-6xl mx-auto px-4 lg:px-6 py-6 pb-24">
        
        {/* 1. Breadcrumb */}
        <VisitorBreadcrumb />

        {/* 2. Hero Summary Card */}
        <VisitorVisitHero detail={detail} />

        {/* 3. Contact & Visit Info Strip */}
        <VisitorContactStrip detail={detail} />

        {/* 4. Status / Next Step Card */}
        <VisitorNextStepCard detail={detail} />

        {/* 5. Lịch trình chuyến thăm */}
        <VisitorAgendaTimeline agenda={detail.agenda} />

        {/* 6. Form đăng ký đã gửi */}
        {summary && <VisitorRequestInfoSection summary={summary} />}

        {/* Đầu mối đoàn khách phối hợp tại cơ sở. Shown whenever there is a form to show it against —
            not gated on the name, because a contact who has been invited but has not confirmed yet
            has an address and no name, and hiding the block is how the guest loses track of whom
            they nominated. */}
        {summary && <VisitorOperationalContactSection summary={summary} />}

        {/* 7. Danh sách thành viên đoàn */}
        {summary?.guestMembers && summary.guestMembers.length > 0 && (
          <VisitorGuestMembersSection members={summary.guestMembers} />
        )}

        {/* 8. Danh sách hỗ trợ đoàn (nếu có) */}
        {summary?.externalSupportMembers && summary.externalSupportMembers.length > 0 && (
          <VisitorExternalSupportSection members={summary.externalSupportMembers} />
        )}

        {/* 8. Thông tin cơ sở */}
        <VisitorCampusInfoSection campusName={detail.campusName} />

        {/* 9. Feedback sau chuyến thăm */}
        <VisitorFeedbackCard visitInstanceId={detail.visitInstanceId} status={detail.instanceStatus} isCancelled={isCancelled} />

        {/* 10. Thông báo / cập nhật từ nhà trường */}
        {detail.notifications && detail.notifications.length > 0 && (
          <VisitorNotificationsSection notifications={detail.notifications} />
        )}

        {/* 11. Bản tin chuyến thăm */}
        {detail.publicNews && detail.publicNews.length > 0 && (
          <VisitorPublicNewsSection 
            news={detail.publicNews} 
            onOpenDetail={(item) => navigate(`/news/${item.newsId}`, { state: { returnTo: window.location.pathname } })} 
          />
        )}

        {/* 12. Lý do hủy nếu CANCELLED */}
        {isCancelled && <VisitorCancelledBanner />}

      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// COMPONENT PHỤ TRỢ (Sub-components)
// ─────────────────────────────────────────────────────────────────────────────

const KV = ({ label, value }: { label: string; value?: React.ReactNode }) => (
  <div className="flex gap-2 py-0.5 text-[13px] leading-5">
    <span className="w-40 shrink-0 text-slate-500">{label}:</span>
    <span className="min-w-0 font-medium text-slate-800 break-words">{value || '-'}</span>
  </div>
);

const KVBlock = ({ label, value }: { label: string; value?: string | null }) => (
  <div className="py-0.5 text-[13px] leading-5">
    <span className="text-slate-500">{label}:</span>
    <p className="mt-0.5 font-medium text-slate-800 whitespace-pre-wrap break-words">{value?.trim() || '-'}</p>
  </div>
);

const SectionTitle = ({ index, children }: { index?: number; children: React.ReactNode }) => (
  <h3 className="mb-3 flex items-center gap-2 border-b border-slate-200 pb-1.5 text-sm font-bold uppercase tracking-wide text-[#004c91]">
    {index !== undefined && <span className="text-slate-400">{index}.</span>}
    {children}
  </h3>
);

function VisitorBreadcrumb() {
  const navigate = useNavigate();
  const { t } = useTranslation('visitorVisitDetail');
  return (
    <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-2">
      <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none">{t('breadcrumb.dashboard')}</button>
      <ChevronRight className="w-4 h-4" />
      <button onClick={() => navigate(-1)} className="hover:text-[#004c91] transition-colors outline-none">{t('breadcrumb.myRequests')}</button>
      <ChevronRight className="w-4 h-4" />
      <span className="text-[#004c91] font-bold">{t('breadcrumb.detail')}</span>
    </div>
  );
}

function VisitorVisitHero({ detail }: { detail: VisitProcessDetail }) {
  const { t } = useTranslation('visitorVisitDetail');
  const statusText = t(`status.${detail.instanceStatus}`, { defaultValue: detail.instanceStatus });

  return (
    <div className="mb-4 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
      <div className="flex items-center gap-2 mb-2">
        <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-blue-50 border border-blue-100 text-[11px] font-bold uppercase tracking-wider text-[#004c91]">
          <Info className="w-3.5 h-3.5" /> {statusText}
        </span>
        {detail.requestSummary?.visitType && (
          <span className="inline-flex items-center px-2 py-0.5 rounded-full bg-slate-50 border border-slate-200 text-[11px] font-bold uppercase tracking-wider text-slate-600">
            {detail.requestSummary.visitType}
          </span>
        )}
      </div>
      <h1 className="text-xl lg:text-2xl font-black leading-tight text-slate-900">
        {t('hero.delegationPrefix', { name: detail.delegationName })}
      </h1>
    </div>
  );
}

function VisitorContactStrip({ detail }: { detail: VisitProcessDetail }) {
  const host = detail.host;
  const { t } = useTranslation('visitorVisitDetail');
  const dateLocale = useDateLocale();

  if (!host) {
    return (
      <section className="rounded-3xl bg-white border border-slate-200 shadow-sm p-6 text-center">
        <p className="text-slate-500 font-medium italic">{t('hostSection.waitingHost')}</p>
      </section>
    );
  }

  const formatVisitTime = (start?: string | null, end?: string | null) => {
    if (!start) return t('hostSection.timeUnknown');
    const s = format(new Date(start), 'HH:mm dd/MM', { locale: dateLocale });
    const e = end ? format(new Date(end), 'HH:mm dd/MM', { locale: dateLocale }) : '...';
    return `${s} - ${e}`;
  };

  return (
    <section className="mb-6 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
      <SectionTitle>{t('hostSection.title')}</SectionTitle>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-x-10">
        <KV label={t('hostSection.fullName')} value={host.fullName} />
        <KV label={t('hostSection.department')} value={host.departmentName} />
        <KV label={t('hostSection.email')} value={host.email} />
        <KV label={t('hostSection.phone')} value={host.phone} />
        <KV label={t('hostSection.campus')} value={detail.campusName} />
        <KV label={t('hostSection.time')} value={formatVisitTime(detail.plannedStartAt, detail.plannedEndAt)} />
      </div>
    </section>
  );
}

function VisitorNextStepCard({ detail }: { detail: VisitProcessDetail }) {
  const { t } = useTranslation('visitorVisitDetail');
  const status = detail.instanceStatus;
  if (status === 'CANCELLED') return null;

  const text = ['ASSIGNED', 'BEFORE_VISIT', 'DURING_VISIT', 'AFTER_VISIT', 'CLOSED'].includes(status)
    ? t(`nextStep.${status}`)
    : t('nextStep.default');

  return (
    <div className="mb-6 bg-blue-50 border border-blue-100 rounded-xl p-4 flex items-start gap-4 shadow-sm">
      <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center shrink-0">
        <Info className="w-5 h-5 text-[#004c91]" />
      </div>
      <div>
        <h3 className="text-[14px] font-bold text-[#004c91] mb-1">{t('nextStep.title')}</h3>
        <p className="text-[13px] text-slate-700 font-medium leading-relaxed">{text}</p>
      </div>
    </div>
  );
}

function VisitorAgendaTimeline({ agenda }: { agenda: any[] }) {
  const { t } = useTranslation('visitorVisitDetail');
  const formatTime = (iso?: string | null) => {
    if (!iso) return '';
    try { return format(new Date(iso), 'HH:mm'); } catch { return iso; }
  };

  return (
    <section className="mb-6 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
      <SectionTitle>{t('agenda.title')}</SectionTitle>
      {(!agenda || agenda.length === 0) ? (
        <p className="text-[13px] italic text-slate-400">{t('agenda.empty')}</p>
      ) : (
        <div className="overflow-x-auto rounded-md border border-slate-200">
          <table className="w-full min-w-[480px] border-collapse text-[13px]">
            <thead className="border-b border-slate-200 bg-slate-50">
              <tr className="text-left text-xs text-slate-500">
                <th className="px-3 py-2 font-semibold w-[140px]">{t('agenda.time')}</th>
                <th className="px-3 py-2 font-semibold">{t('agenda.content')}</th>
                <th className="px-3 py-2 font-semibold">{t('agenda.location')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {agenda.map((it, idx) => (
                <tr key={idx}>
                  <td className="px-3 py-2 text-slate-600 whitespace-nowrap align-top">
                    <span className="font-medium text-[#004c91]">{formatTime(it.startTime)} {it.endTime ? `- ${formatTime(it.endTime)}` : ''}</span>
                  </td>
                  <td className="px-3 py-2 align-top">
                    <p className="font-medium text-slate-800">{it.title}</p>
                    {it.description && <p className="text-slate-500 mt-1 whitespace-pre-wrap">{it.description}</p>}
                  </td>
                  <td className="px-3 py-2 text-slate-600 align-top">{it.location || '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function VisitorRequestInfoSection({ summary }: { summary: any }) {
  const { t } = useTranslation('visitorVisitDetail');
  return (
    <section className="mb-6 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
      <SectionTitle>{t('requestInfo.title')}</SectionTitle>
      <div className="space-y-5">
        <div>
          <h4 className="text-[13px] font-bold text-[#f37021] mb-2 uppercase">{t('requestInfo.registrant')}</h4>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-x-10">
            <KV label={t('requestInfo.fullName')} value={summary.registrantName} />
            <KV label={t('requestInfo.email')} value={summary.registrantEmail} />
            <KV label={t('requestInfo.phone')} value={summary.registrantPhone} />
            <KV label={t('requestInfo.nationality')} value={summary.registrantNationality} />
            <KV label={t('requestInfo.organization')} value={summary.registrantOrganization} />
            <KV label={t('requestInfo.jobTitle')} value={summary.registrantJobTitle} />
          </div>
        </div>

        <div>
          <h4 className="text-[13px] font-bold text-[#f37021] mb-2 uppercase">{t('requestInfo.delegationInfo')}</h4>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-x-10">
            <KV label={t('requestInfo.delegationName')} value={summary.delegationName} />
            <KV label={t('requestInfo.partner')} value={summary.registrantOrganization} />
            <KV
              label={t('requestInfo.guestCount')}
              value={summary.guestMembers?.length ? t('requestInfo.guestCountUnit', { count: summary.guestMembers.length }) : t('requestInfo.guestCountUnknown')}
            />
          </div>
          <div className="mt-1.5 space-y-1.5">
            <KVBlock label={t('requestInfo.purpose')} value={summary.purpose} />
            <KVBlock label={t('requestInfo.workingContent')} value={summary.workingContent} />
          </div>
        </div>

        <div>
          <h4 className="text-[13px] font-bold text-[#f37021] mb-2 uppercase">{t('requestInfo.additionalInfo')}</h4>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-x-10">
            <KV label={t('requestInfo.visitType')} value={summary.visitType} />
            <KV label={t('requestInfo.workingLanguage')} value={summary.workingLanguage} />
            <KV label={t('requestInfo.transportation')} value={summary.transportationNote || null} />
            {/* The stored value is AGREED, not AGREE — the old comparison never matched, so a guest
                who had agreed was shown as having declined on their own submitted form. */}
            <KV label={t('requestInfo.mediaConsent')} value={summary.mediaConsentStatus === 'AGREED' ? t('requestInfo.mediaConsentAgreed') : t('requestInfo.mediaConsentDeclined')} />
          </div>
          {/* Always shown: this is the visitor's own submitted form, and a row that disappears when
              they left it blank makes them doubt the note was ever sent. KVBlock renders "-". */}
          <KVBlock label={t('requestInfo.notes')} value={summary.notes} />
        </div>
      </div>
    </section>
  );
}

function VisitorGuestMembersSection({ members }: { members: any[] }) {
  const { t } = useTranslation('visitorVisitDetail');
  return (
    <section className="mb-6 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
      <SectionTitle>{t('guestMembers.title')}</SectionTitle>
      <div className="overflow-x-auto rounded-md border border-slate-200">
        <table className="w-full min-w-[560px] border-collapse text-[13px]">
          <thead className="border-b border-slate-200 bg-slate-50">
            <tr className="text-left text-xs text-slate-500">
              <th className="px-2.5 py-1.5 w-10 text-center font-semibold">{t('guestMembers.no')}</th>
              <th className="px-2.5 py-1.5 font-semibold">{t('guestMembers.fullName')}</th>
              <th className="px-2.5 py-1.5 font-semibold">{t('guestMembers.jobTitle')}</th>
              <th className="px-2.5 py-1.5 font-semibold">{t('guestMembers.organization')}</th>
              <th className="px-2.5 py-1.5 font-semibold">{t('guestMembers.nationality')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {members.map((m, idx) => (
              <tr key={idx}>
                <td className="px-2.5 py-1.5 text-center text-slate-400">{idx + 1}</td>
                <td className="px-2.5 py-1.5 font-medium text-slate-800">{m.fullName || '-'}</td>
                <td className="px-2.5 py-1.5 text-slate-600">{m.jobTitle || '-'}</td>
                <td className="px-2.5 py-1.5 text-slate-600">{m.organization || '-'}</td>
                <td className="px-2.5 py-1.5 text-slate-600">{m.nationality || '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function VisitorOperationalContactSection({ summary }: { summary: any }) {
  const { t } = useTranslation('visitorVisitDetail');
  return (
    <section className="mb-6 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
      <SectionTitle>{t('operationalContact.title')}</SectionTitle>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-x-10">
        <KV label={t('operationalContact.fullName')} value={summary.operationalContactFullName} />
        <KV label={t('operationalContact.organization')} value={summary.operationalContactOrganization} />
        <KV label={t('operationalContact.jobTitle')} value={summary.operationalContactJobTitle} />
        <KV label={t('operationalContact.phone')} value={summary.operationalContactPhone} />
        <KV label={t('operationalContact.email')} value={summary.operationalContactEmail} />
      </div>
    </section>
  );
}

function VisitorExternalSupportSection({ members }: { members: any[] }) {
  const { t } = useTranslation('visitorVisitDetail');
  return (
    <section className="mb-6 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
      <SectionTitle>{t('externalSupport.title')}</SectionTitle>
      <div className="overflow-x-auto rounded-md border border-slate-200">
        <table className="w-full min-w-[560px] border-collapse text-[13px]">
          <thead className="border-b border-slate-200 bg-slate-50">
            <tr className="text-left text-xs text-slate-500">
              <th className="px-2.5 py-1.5 w-10 text-center font-semibold">{t('guestMembers.no')}</th>
              <th className="px-2.5 py-1.5 font-semibold">{t('guestMembers.fullName')}</th>
              <th className="px-2.5 py-1.5 font-semibold">{t('guestMembers.jobTitle')}</th>
              <th className="px-2.5 py-1.5 font-semibold">{t('guestMembers.organization')}</th>
              <th className="px-2.5 py-1.5 font-semibold">{t('guestMembers.nationality')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {members.map((m, idx) => (
              <tr key={idx}>
                <td className="px-2.5 py-1.5 text-center text-slate-400">{idx + 1}</td>
                <td className="px-2.5 py-1.5 font-medium text-slate-800">{m.fullName || '-'}</td>
                <td className="px-2.5 py-1.5 text-slate-600">{m.jobTitle || '-'}</td>
                <td className="px-2.5 py-1.5 text-slate-600">{m.organization || '-'}</td>
                <td className="px-2.5 py-1.5 text-slate-600">{m.nationality || '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function VisitorCampusInfoSection({ campusName }: { campusName?: string | null }) {
  const { t } = useTranslation('visitorVisitDetail');
  if (!campusName) return null;

  return (
    <section className="mb-6 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
      <SectionTitle>{t('campusInfo.title')}</SectionTitle>
      <div className="flex items-start gap-3">
        <div className="w-10 h-10 rounded-lg bg-orange-50 flex items-center justify-center shrink-0">
          <MapPin className="w-5 h-5 text-[#f37021]" />
        </div>
        <div>
          <h3 className="text-[13px] font-bold text-slate-900">{campusName}</h3>
          <p className="text-[12px] text-slate-500 font-medium">FPT University</p>
        </div>
      </div>
    </section>
  );
}

function VisitorFeedbackCard({ visitInstanceId, status, isCancelled }: { visitInstanceId: number, status: string, isCancelled: boolean }) {
  const { t } = useTranslation('visitorVisitDetail');
  const [modalOpen, setModalOpen] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  if (isCancelled || (status !== 'AFTER_VISIT' && status !== 'CLOSED')) {
    return null;
  }

  return (
    <>
      <section className="mb-6 bg-white p-6 rounded-xl border border-slate-200 shadow-sm text-center">
        <div className="w-12 h-12 bg-orange-50 rounded-full flex items-center justify-center mx-auto mb-3">
          {submitted
            ? <CheckCircle2 className="w-6 h-6 text-emerald-500" />
            : <Star className="w-6 h-6 text-[#f37021] fill-[#f37021]" />}
        </div>
        <h3 className="text-[15px] font-bold text-slate-900 mb-1">{t('feedbackCard.title')}</h3>
        {submitted ? (
          <p className="text-[13px] text-emerald-600 font-bold">{t('feedbackCard.thanks')}</p>
        ) : (
          <>
            <p className="text-[13px] text-slate-600 font-medium max-w-lg mx-auto mb-4">
              {t('feedbackCard.prompt')}
            </p>
            <button
              type="button"
              onClick={() => setModalOpen(true)}
              className="inline-flex items-center gap-2 px-5 py-2.5 bg-[#f37021] hover:bg-orange-600 text-white text-[13px] font-bold rounded-xl shadow-sm transition-colors"
            >
              <Star className="w-4 h-4" /> {t('feedbackCard.cta')}
            </button>
          </>
        )}
      </section>

      <VisitFeedbackModal
        open={modalOpen}
        visitInstanceId={visitInstanceId}
        onClose={() => setModalOpen(false)}
        onSubmitted={() => setSubmitted(true)}
      />
    </>
  );
}

function VisitorCancelledBanner() {
  const { t } = useTranslation('visitorVisitDetail');
  return (
    <section className="mb-6 bg-rose-50 border border-rose-200 rounded-xl p-4 sm:p-5 text-center sm:text-left">
      <div className="flex flex-col sm:flex-row items-center sm:items-start gap-4">
        <div className="w-10 h-10 rounded-lg bg-rose-100 flex items-center justify-center shrink-0">
          <XCircle className="w-5 h-5 text-rose-600" />
        </div>
        <div>
          <h3 className="text-[14px] font-bold text-rose-800 mb-1">{t('cancelledBanner.title')}</h3>
          <p className="text-rose-700 text-[13px] font-medium">
            {t('cancelledBanner.desc')}
          </p>
        </div>
      </div>
    </section>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// THÔNG BÁO TỪ NHÀ TRƯỜNG
// ─────────────────────────────────────────────────────────────────────────────

function VisitorNotificationsSection({ notifications }: { notifications: VisitorNotification[] }) {
  const { t } = useTranslation('visitorVisitDetail');
  const dateLocale = useDateLocale();
  const formatDateTime = (iso?: string | null) => {
    if (!iso) return '';
    try {
      return format(new Date(iso), 'HH:mm - dd/MM/yyyy', { locale: dateLocale });
    } catch {
      return iso;
    }
  };

  return (
    <section className="mb-6 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
      <SectionTitle>{t('notifications.title')}</SectionTitle>
      <div className="space-y-3">
        {notifications.map((item) => (
          <div key={item.notificationId} className="rounded-lg border border-slate-100 bg-slate-50 px-4 py-3">
            <p className="text-[13px] font-bold text-slate-900">{item.title}</p>
            {item.message && (
              <p className="mt-1 text-[13px] text-slate-600 leading-relaxed">{item.message}</p>
            )}
            <p className="mt-1.5 text-[11px] font-medium text-slate-400">
              {formatDateTime(item.createdAt)}
            </p>
          </div>
        ))}
      </div>
    </section>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// BẢN TIN CHUYẾN THĂM
// ─────────────────────────────────────────────────────────────────────────────

function VisitorPublicNewsSection({ 
  news, 
  onOpenDetail 
}: { 
  news: VisitorPublicNewsListItem[],
  onOpenDetail?: (item: VisitorPublicNewsListItem) => void
}) {
  const { t } = useTranslation('visitorVisitDetail');
  const dateLocale = useDateLocale();
  if (!news || news.length === 0) return null;

  const formatDateTime = (iso?: string | null) => {
    if (!iso) return '';
    try {
      return format(new Date(iso), 'dd/MM/yyyy', { locale: dateLocale });
    } catch {
      return iso;
    }
  };

  return (
    <section className="mb-6 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
      <SectionTitle>{t('publicNews.title')}</SectionTitle>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {news.map((item) => (
          <article
            key={item.newsId}
            className="rounded-lg border border-slate-200 bg-white overflow-hidden hover:shadow-sm transition-shadow flex flex-col"
          >
            {item.thumbnailUrl && (
              <img
                src={item.thumbnailUrl}
                alt={item.title}
                className="h-32 w-full object-cover"
              />
            )}
            <div className="p-3 flex flex-col flex-1">
              <h3 className="text-[13px] font-bold text-slate-900 line-clamp-2">
                {item.title}
              </h3>
              {item.summary && (
                <p className="mt-1 text-[12px] text-slate-600 line-clamp-2">
                  {item.summary}
                </p>
              )}
              <div className="mt-2 text-[11px] font-medium text-slate-400 mt-auto pt-2">
                {item.publishedAt ? formatDateTime(item.publishedAt) : ''}
                {item.authorName ? ` · ${item.authorName}` : ''}
              </div>
              {onOpenDetail && (
                <div className="mt-3 pt-3 border-t border-slate-100">
                  <button 
                    type="button" 
                    onClick={() => onOpenDetail(item)}
                    className="text-[12px] font-bold text-[#004c91] hover:underline flex items-center gap-1 group"
                  >
                    {t('publicNews.viewDetail')}
                    <ChevronRight className="w-3.5 h-3.5 group-hover:translate-x-1 transition-transform" />
                  </button>
                </div>
              )}
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
