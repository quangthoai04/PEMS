import React from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  MapPin, User, Mail, Phone, Calendar, Clock, Star, AlertCircle, 
  Building, Globe, Briefcase, Car, Users, MessageSquare, ShieldCheck, CheckCircle2,
  CalendarCheck2, Info, ChevronRight, XCircle, Bell, Newspaper
} from 'lucide-react';
import { VisitorNotification, VisitorPublicNewsListItem } from '../../../features/delegations/types/delegations.types';
import { VisitProcessDetail, VisitProcessPermission } from '../../../features/delegations/types/delegations.types';
import { format } from 'date-fns';
import { vi } from 'date-fns/locale';

interface VisitorVisitDetailPageProps {
  perm: VisitProcessPermission | null;
  detail: VisitProcessDetail | null;
}

// ─────────────────────────────────────────────────────────────────────────────
// 1. COMPONENT CHÍNH
// ─────────────────────────────────────────────────────────────────────────────
export function VisitorVisitDetailPage({ perm, detail }: VisitorVisitDetailPageProps) {
  const navigate = useNavigate();

  if (!detail || !perm) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh]">
        <div className="w-12 h-12 border-4 border-[#004c91] border-t-transparent rounded-full animate-spin mb-4"></div>
        <p className="text-gray-500 font-medium">Đang tải thông tin chuyến thăm...</p>
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
        <h2 className="text-xl font-bold text-slate-800 mb-2">Chuyến thăm chưa được phân công người phụ trách</h2>
        <p className="text-slate-500 mb-6 max-w-md">Vui lòng quay lại trang Đơn tham quan của tôi để xem trạng thái đơn.</p>
        <button 
          onClick={() => navigate(-1)}
          className="px-6 py-2.5 bg-[#004c91] text-white font-bold rounded-xl shadow-sm hover:bg-[#003b70] transition-colors"
        >
          Quay lại
        </button>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <div className="max-w-6xl mx-auto px-6 lg:px-8 py-6 space-y-6 pb-24">
        
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

        {/* Thông tin người liên hệ (nếu có) */}
        {summary?.contactPersonFullName && (
          <VisitorContactPersonSection summary={summary} />
        )}

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
        <VisitorFeedbackCard status={detail.instanceStatus} isCancelled={isCancelled} />

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

function VisitorBreadcrumb() {
  const navigate = useNavigate();
  return (
    <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-2">
      <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none">Dashboard</button>
      <ChevronRight className="w-4 h-4" />
      <button onClick={() => navigate(-1)} className="hover:text-[#004c91] transition-colors outline-none">Đơn tham quan của tôi</button>
      <ChevronRight className="w-4 h-4" />
      <span className="text-[#004c91] font-bold">Chi tiết chuyến thăm</span>
    </div>
  );
}

function VisitorVisitHero({ detail }: { detail: VisitProcessDetail }) {
  const getStatusText = (status: string) => {
    switch (status) {
      case 'ASSIGNED':
      case 'BEFORE_VISIT': return 'Đã xác nhận lịch tham quan';
      case 'DURING_VISIT': return 'Chuyến thăm đang diễn ra';
      case 'AFTER_VISIT': return 'Chuyến thăm đã kết thúc';
      case 'CLOSED': return 'Chuyến thăm đã hoàn tất';
      case 'CANCELLED': return 'Đã hủy';
      default: return status;
    }
  };

  const statusText = getStatusText(detail.instanceStatus);

  return (
    <section className="rounded-3xl bg-gradient-to-r from-[#004c91] to-[#0066b3] p-6 lg:p-8 text-white shadow-lg relative overflow-hidden">
      <div className="absolute top-0 right-0 w-64 h-64 bg-white/5 rounded-full blur-3xl -translate-y-1/2 translate-x-1/3"></div>
      <div className="relative z-10">
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-white/20 border border-white/30 text-xs font-bold uppercase tracking-wider mb-4">
          <Info className="w-3.5 h-3.5" /> {statusText}
        </div>
        <h1 className="text-3xl lg:text-4xl font-black leading-tight mb-2 line-clamp-2">
          Đoàn {detail.delegationName}
        </h1>
        {detail.requestSummary?.visitType && (
          <p className="text-blue-100 font-medium">Phân loại: {detail.requestSummary.visitType}</p>
        )}
      </div>
    </section>
  );
}

function VisitorContactStrip({ detail }: { detail: VisitProcessDetail }) {
  const host = detail.host;
  
  if (!host) {
    return (
      <section className="rounded-3xl bg-white border border-slate-200 shadow-sm p-6 text-center">
        <p className="text-slate-500 font-medium italic">Nhà trường đang sắp xếp người phụ trách phù hợp. Thông tin sẽ hiển thị tại đây khi được phân công.</p>
      </section>
    );
  }

  const formatVisitTime = (start?: string | null, end?: string | null) => {
    if (!start) return 'Chưa xác định';
    const s = format(new Date(start), 'HH:mm dd/MM', { locale: vi });
    const e = end ? format(new Date(end), 'HH:mm dd/MM', { locale: vi }) : '...';
    return `${s} - ${e}`;
  };

  return (
    <section className="rounded-3xl bg-white border border-slate-200 shadow-sm overflow-hidden">
      <div className="px-6 py-5 border-l-4 border-[#f37021]">
        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
          
          <div className="flex items-start gap-4">
            <div className="w-14 h-14 rounded-2xl bg-blue-100 text-[#004c91] flex items-center justify-center font-black text-xl shrink-0">
              {host.fullName?.charAt(0) || 'H'}
            </div>
            <div>
              <p className="text-xs font-bold uppercase tracking-wide text-slate-500 mb-1">
                Người phụ trách chuyến thăm
              </p>
              <h2 className="text-lg font-bold text-slate-900 leading-tight">
                {host.fullName || 'Chưa cập nhật'}
              </h2>
              <p className="text-sm font-medium text-slate-500">
                {host.departmentName || 'FPT University'}
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 lg:min-w-[520px]">
            <ReadOnlyInfoField label="Email" value={host.email} />
            <ReadOnlyInfoField label="Số điện thoại" value={host.phone} />
            <ReadOnlyInfoField label="Cơ sở" value={detail.campusName} />
            <ReadOnlyInfoField label="Thời gian" value={formatVisitTime(detail.plannedStartAt, detail.plannedEndAt)} />
          </div>

        </div>
      </div>
    </section>
  );
}

function VisitorNextStepCard({ detail }: { detail: VisitProcessDetail }) {
  const status = detail.instanceStatus;
  if (status === 'CANCELLED') return null;

  let text = '';
  switch (status) {
    case 'ASSIGNED':
    case 'BEFORE_VISIT':
      text = 'Lịch tham quan của bạn đã được xác nhận. Vui lòng theo dõi lịch trình và liên hệ người phụ trách nếu có thay đổi.';
      break;
    case 'DURING_VISIT':
      text = 'Chuyến thăm đang diễn ra. Vui lòng liên hệ người phụ trách nếu cần hỗ trợ.';
      break;
    case 'AFTER_VISIT':
      text = 'Chuyến thăm đã kết thúc. Xin chân thành cảm ơn sự quan tâm của bạn dành cho FPT University.';
      break;
    case 'CLOSED':
      text = 'Chuyến thăm đã hoàn tất. Cảm ơn bạn đã ghé thăm FPT University.';
      break;
    default:
      text = 'Vui lòng theo dõi thông tin cập nhật mới nhất từ hệ thống.';
  }

  return (
    <div className="bg-blue-50 border border-blue-100 rounded-2xl p-5 flex items-start gap-4 shadow-sm">
      <div className="w-10 h-10 rounded-full bg-blue-100 flex items-center justify-center shrink-0">
        <Info className="w-5 h-5 text-[#004c91]" />
      </div>
      <div>
        <h3 className="text-sm font-bold text-[#004c91] mb-1">Hướng dẫn / Thông báo</h3>
        <p className="text-sm text-slate-700 font-medium leading-relaxed">{text}</p>
      </div>
    </div>
  );
}

function VisitorAgendaTimeline({ agenda }: { agenda: any[] }) {
  const formatTime = (iso?: string | null) => {
    if (!iso) return '';
    try { return format(new Date(iso), 'HH:mm'); } catch { return iso; }
  };

  return (
    <section>
      <h2 className="text-xl font-bold text-slate-800 mb-4 flex items-center gap-2">
        <Clock className="w-5 h-5 text-[#004c91]" /> Lịch trình chuyến thăm
      </h2>
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 lg:p-8">
        {(!agenda || agenda.length === 0) ? (
          <div className="text-center py-8">
            <CalendarCheck2 className="w-12 h-12 text-slate-300 mx-auto mb-3" />
            <p className="text-slate-500 font-medium">Lịch trình chi tiết đang được nhà trường cập nhật.</p>
          </div>
        ) : (
          <div className="relative space-y-6 before:absolute before:inset-0 before:ml-[17px] before:-translate-x-px before:h-full before:w-0.5 before:bg-slate-200">
            {agenda.map((it, idx) => (
              <div key={idx} className="relative flex items-start gap-6">
                <div className="flex items-center justify-center w-9 h-9 rounded-full border-4 border-white bg-[#004c91] text-white shrink-0 z-10 shadow-sm">
                  <span className="text-xs font-black">{idx + 1}</span>
                </div>
                <div className="flex-1 bg-slate-50 border border-slate-100 p-5 rounded-2xl hover:border-blue-200 transition-colors">
                  <div className="flex flex-wrap items-center justify-between mb-2 gap-2">
                    <span className="text-sm font-bold text-[#004c91] bg-blue-100/50 px-3 py-1 rounded-md">
                      {formatTime(it.startTime)} {it.endTime ? ` - ${formatTime(it.endTime)}` : ''}
                    </span>
                  </div>
                  <h3 className="text-base font-bold text-slate-800 mb-2">{it.title}</h3>
                  {(it.location || it.description) && (
                    <div className="space-y-2 mt-3 pt-3 border-t border-slate-200">
                      {it.location && (
                        <div className="flex items-start gap-2 text-sm">
                          <MapPin className="w-4 h-4 shrink-0 text-[#f37021] mt-0.5" />
                          <span className="font-bold text-slate-700">{it.location}</span>
                        </div>
                      )}
                      {it.description && (
                        <div className="flex items-start gap-2 text-sm text-slate-600">
                          <Info className="w-4 h-4 shrink-0 text-slate-400 mt-0.5" />
                          <span className="leading-relaxed">{it.description}</span>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

function VisitorRequestInfoSection({ summary }: { summary: any }) {
  return (
    <section>
      <h2 className="text-xl font-bold text-slate-800 mb-4 flex items-center gap-2">
        <ShieldCheck className="w-5 h-5 text-[#004c91]" /> Thông tin đăng ký đã gửi
      </h2>
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 lg:p-8 space-y-8">
        
        <div>
          <h3 className="text-lg font-bold text-slate-800 mb-4 flex items-center gap-2">
            <User className="w-5 h-5 text-[#f37021]" /> Người đại diện đăng ký
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <ReadOnlyInfoField label="Họ và tên" value={summary.registrantName} />
            <ReadOnlyInfoField label="Email" value={summary.registrantEmail} />
            <ReadOnlyInfoField label="Số điện thoại" value={summary.registrantPhone} />
            <ReadOnlyInfoField label="Quốc tịch" value={summary.registrantNationality} />
            <ReadOnlyInfoField label="Tổ chức / Đơn vị" value={summary.registrantOrganization} />
            <ReadOnlyInfoField label="Chức danh" value={summary.registrantJobTitle} />
          </div>
        </div>

        <div>
          <h3 className="text-lg font-bold text-slate-800 mb-4 flex items-center gap-2">
            <Users className="w-5 h-5 text-[#f37021]" /> Thông tin đoàn khách
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <ReadOnlyInfoField label="Tên đoàn" value={summary.delegationName} />
            <ReadOnlyInfoField label="Tổ chức / Đối tác" value={summary.registrantOrganization} />
            <ReadOnlyInfoField label="Số lượng khách" value={summary.guestMembers?.length ? `${summary.guestMembers.length} thành viên` : 'Chưa cập nhật'} />
            <ReadOnlyInfoField label="Mục đích chuyến thăm" value={summary.purpose} />
            <ReadOnlyInfoField label="Nội dung mong muốn trao đổi" value={summary.workingContent} />
            <ReadOnlyInfoField label="Ghi chú thêm" value={summary.noteToFptu} />
          </div>
        </div>

        <div>
          <h3 className="text-lg font-bold text-slate-800 mb-4 flex items-center gap-2">
            <Building className="w-5 h-5 text-[#f37021]" /> Thông tin chuyến thăm mong muốn
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <ReadOnlyInfoField label="Phân loại chuyến thăm" value={summary.visitType} />
            <ReadOnlyInfoField label="Ngôn ngữ sử dụng" value={summary.workingLanguage} />
            <ReadOnlyInfoField label="Phương tiện di chuyển" value={summary.transportationType ? `${summary.transportationType} ${summary.transportationDetail ? `(${summary.transportationDetail})` : ''}` : null} />
            <ReadOnlyInfoField label="Media Consent" value={summary.mediaConsentStatus === 'AGREE' ? 'Đồng ý ghi hình/chụp ảnh' : 'Từ chối ghi hình/chụp ảnh'} />
            {summary.mediaConsentNote && (
              <ReadOnlyInfoField label="Lưu ý về Media" value={summary.mediaConsentNote} />
            )}
          </div>
        </div>

      </div>
    </section>
  );
}

function VisitorGuestMembersSection({ members }: { members: any[] }) {
  return (
    <section>
      <h2 className="text-xl font-bold text-slate-800 mb-4 flex items-center gap-2">
        <Users className="w-5 h-5 text-[#004c91]" /> Danh sách thành viên đoàn
      </h2>
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-slate-600">
            <thead className="bg-slate-50 text-slate-500 uppercase font-bold text-xs">
              <tr>
                <th className="px-6 py-4">STT</th>
                <th className="px-6 py-4">Họ và tên</th>
                <th className="px-6 py-4">Chức danh</th>
                <th className="px-6 py-4">Tổ chức</th>
                <th className="px-6 py-4">Quốc tịch</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {members.map((m, idx) => (
                <tr key={idx} className="hover:bg-slate-50/50">
                  <td className="px-6 py-4 font-bold text-slate-400">{idx + 1}</td>
                  <td className="px-6 py-4 font-bold text-slate-800">{m.fullName || '—'}</td>
                  <td className="px-6 py-4">{m.jobTitle || '—'}</td>
                  <td className="px-6 py-4">{m.organization || '—'}</td>
                  <td className="px-6 py-4">{m.nationality || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}

function VisitorContactPersonSection({ summary }: { summary: any }) {
  return (
    <section>
      <h2 className="text-xl font-bold text-slate-800 mb-4 flex items-center gap-2">
        <User className="w-5 h-5 text-[#004c91]" /> Thông tin người liên hệ
      </h2>
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 lg:p-8">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          <ReadOnlyInfoField label="Họ và tên" value={summary.contactPersonFullName} />
          <ReadOnlyInfoField label="Email" value={summary.contactPersonEmail} />
          <ReadOnlyInfoField label="Số điện thoại" value={summary.contactPersonPhone} />
          <ReadOnlyInfoField label="Tổ chức" value={summary.contactPersonOrganization} />
        </div>
      </div>
    </section>
  );
}

function VisitorExternalSupportSection({ members }: { members: any[] }) {
  return (
    <section>
      <h2 className="text-xl font-bold text-slate-800 mb-4 flex items-center gap-2">
        <Users className="w-5 h-5 text-[#004c91]" /> Danh sách hỗ trợ đoàn
      </h2>
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-slate-600">
            <thead className="bg-slate-50 text-slate-500 uppercase font-bold text-xs">
              <tr>
                <th className="px-6 py-4">STT</th>
                <th className="px-6 py-4">Họ và tên</th>
                <th className="px-6 py-4">Chức danh</th>
                <th className="px-6 py-4">Tổ chức</th>
                <th className="px-6 py-4">Quốc tịch</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {members.map((m, idx) => (
                <tr key={idx} className="hover:bg-slate-50/50">
                  <td className="px-6 py-4 font-bold text-slate-400">{idx + 1}</td>
                  <td className="px-6 py-4 font-bold text-slate-800">{m.fullName || '—'}</td>
                  <td className="px-6 py-4">{m.jobTitle || '—'}</td>
                  <td className="px-6 py-4">{m.organization || '—'}</td>
                  <td className="px-6 py-4">{m.nationality || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}

function VisitorCampusInfoSection({ campusName }: { campusName?: string | null }) {
  if (!campusName) return null;
  
  return (
    <section>
      <h2 className="text-xl font-bold text-slate-800 mb-4 flex items-center gap-2">
        <Building className="w-5 h-5 text-[#004c91]" /> Thông tin cơ sở
      </h2>
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 lg:p-8">
        <div className="flex items-start gap-4">
          <div className="w-12 h-12 rounded-xl bg-orange-50 flex items-center justify-center shrink-0">
            <MapPin className="w-6 h-6 text-[#f37021]" />
          </div>
          <div>
            <h3 className="text-lg font-bold text-slate-900 mb-1">{campusName}</h3>
            <p className="text-sm text-slate-500 font-medium">FPT University</p>
          </div>
        </div>
      </div>
    </section>
  );
}

function VisitorFeedbackCard({ status, isCancelled }: { status: string, isCancelled: boolean }) {
  if (isCancelled || (status !== 'AFTER_VISIT' && status !== 'CLOSED')) {
    return null;
  }

  return (
    <section className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 lg:p-8 text-center mt-8">
      <div className="w-16 h-16 bg-orange-50 rounded-full flex items-center justify-center mx-auto mb-4">
        <Star className="w-8 h-8 text-[#f37021] fill-[#f37021]" />
      </div>
      <h3 className="text-lg font-bold text-slate-900 mb-2">Phản hồi chuyến thăm</h3>
      <p className="text-slate-600 font-medium max-w-lg mx-auto">
        Nhà trường rất mong nhận được phản hồi của bạn. Tính năng gửi phản hồi sẽ sớm được cập nhật.
      </p>
    </section>
  );
}

function VisitorCancelledBanner() {
  return (
    <section className="bg-rose-50 border border-rose-200 rounded-3xl p-6 lg:p-8 mt-8 text-center sm:text-left">
      <div className="flex flex-col sm:flex-row items-center sm:items-start gap-4">
        <div className="w-12 h-12 rounded-2xl bg-rose-100 flex items-center justify-center shrink-0">
          <XCircle className="w-6 h-6 text-rose-600" />
        </div>
        <div>
          <h3 className="text-lg font-bold text-rose-800 mb-1">Chuyến thăm đã bị hủy</h3>
          <p className="text-rose-700 text-sm font-medium">
            Chuyến tham quan này đã được hủy bỏ và sẽ không diễn ra như dự kiến. Nếu bạn cần hỗ trợ thêm, vui lòng liên hệ nhà trường.
          </p>
        </div>
      </div>
    </section>
  );
}

function ReadOnlyInfoField({ label, value }: { label: string, value?: string | null }) {
  return (
    <div className="rounded-2xl bg-slate-50 px-4 py-3 border border-slate-100">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</p>
      <p className="mt-1 text-sm font-semibold text-slate-900 break-words">{value || '—'}</p>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// THÔNG BÁO TỪ NHÀ TRƯỜNG
// ─────────────────────────────────────────────────────────────────────────────

function VisitorNotificationsSection({ notifications }: { notifications: VisitorNotification[] }) {
  const formatDateTime = (iso?: string | null) => {
    if (!iso) return '';
    try {
      return format(new Date(iso), 'HH:mm - dd/MM/yyyy', { locale: vi });
    } catch {
      return iso;
    }
  };

  return (
    <section className="mt-8">
      <h2 className="text-xl font-bold text-slate-800 mb-4 flex items-center gap-2">
        <Bell className="w-5 h-5 text-[#004c91]" /> Thông báo & Cập nhật
      </h2>
      <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6">
        <div className="space-y-4">
          {notifications.map((item) => (
            <div key={item.notificationId} className="rounded-2xl border border-slate-100 bg-slate-50 px-5 py-4">
              <p className="text-sm font-bold text-slate-900">{item.title}</p>
              {item.message && (
                <p className="mt-1.5 text-sm text-slate-600 leading-relaxed">{item.message}</p>
              )}
              <p className="mt-2 text-xs font-medium text-slate-400">
                {formatDateTime(item.createdAt)}
              </p>
            </div>
          ))}
        </div>
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
  if (!news || news.length === 0) return null;

  const formatDateTime = (iso?: string | null) => {
    if (!iso) return '';
    try {
      return format(new Date(iso), 'dd/MM/yyyy', { locale: vi });
    } catch {
      return iso;
    }
  };

  return (
    <section className="mt-8">
      <div className="flex items-center gap-2 mb-4">
        <Newspaper className="w-5 h-5 text-[#004c91]" />
        <h2 className="text-xl font-bold text-[#004c91]">
          Bản tin chuyến thăm
        </h2>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {news.map((item) => (
          <article
            key={item.newsId}
            className="rounded-2xl border border-slate-200 bg-white overflow-hidden hover:shadow-md transition-shadow flex flex-col"
          >
            {item.thumbnailUrl && (
              <img
                src={item.thumbnailUrl}
                alt={item.title}
                className="h-48 w-full object-cover"
              />
            )}

            <div className="p-5 flex flex-col flex-1">
              <h3 className="text-base font-bold text-slate-900 line-clamp-2">
                {item.title}
              </h3>

              {item.summary && (
                <p className="mt-2 text-sm text-slate-600 line-clamp-3">
                  {item.summary}
                </p>
              )}

              <div className="mt-4 text-xs font-medium text-slate-400 mt-auto pt-2">
                {item.publishedAt ? formatDateTime(item.publishedAt) : ''}
                {item.authorName ? ` · ${item.authorName}` : ''}
              </div>

              {onOpenDetail && (
                <div className="mt-4 pt-4 border-t border-slate-100">
                  <button 
                    type="button" 
                    onClick={() => onOpenDetail(item)}
                    className="text-sm font-bold text-[#004c91] hover:underline flex items-center gap-1 group"
                  >
                    Xem chi tiết
                    <ChevronRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
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
