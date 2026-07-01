import React, { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  ChevronDown,
  ChevronUp,
  Clock,
  Lock,
  Loader2,
  AlertCircle,
  FileText,
  Users,
  Box,
  Image as ImageIcon,
  Newspaper,
  Calendar,
  Info,
  History,
  MessageSquare,
} from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type { ProcessSummaryPage, ContributionSectionStatus } from '../../../features/delegations/types/delegations.types';
import { RegistrantInfoReadOnly, DelegationInfoReadOnly } from '../../../features/delegations/components/RequestInfoReadOnly';

const getStatusColor = (status: string) => {
  switch (status) {
    case 'ASSIGNED': return 'bg-cyan-100 text-cyan-800 border-cyan-200';
    case 'BEFORE_VISIT': return 'bg-blue-100 text-blue-800 border-blue-200';
    case 'DURING_VISIT': return 'bg-[#f37021]/10 text-[#f37021] border-[#f37021]/20';
    case 'AFTER_VISIT': return 'bg-indigo-100 text-indigo-800 border-indigo-200';
    case 'CLOSED': return 'bg-[#00a651]/10 text-[#00a651] border-[#00a651]/20';
    case 'CANCELLED': return 'bg-rose-100 text-rose-800 border-rose-200';
    default: return 'bg-gray-100 text-gray-800 border-gray-200';
  }
};

const getStatusText = (status: string) => {
  switch (status) {
    case 'ASSIGNED': return 'Đã phân công Host';
    case 'BEFORE_VISIT': return 'Trước tiếp khách';
    case 'DURING_VISIT': return 'Đang tiếp khách';
    case 'AFTER_VISIT': return 'Sau tiếp khách';
    case 'CLOSED': return 'Đã đóng đoàn';
    case 'CANCELLED': return 'Đã hủy';
    default: return status;
  }
};

function SectionCard({ title, icon: Icon, children, isExpanded, onToggle, canView = true }: any) {
  if (!canView) {
    return (
      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm overflow-hidden mb-6 opacity-70">
        <div className="px-6 py-4 flex items-center justify-between bg-gray-50 border-b border-gray-100">
          <h2 className="text-lg font-bold text-gray-400 flex items-center gap-2">
            <Icon className="w-5 h-5 text-gray-400" />
            {title}
          </h2>
          <span className="text-xs font-bold text-gray-400 bg-gray-200 px-2 py-1 rounded-md flex items-center gap-1">
            <Lock className="w-3 h-3" /> KHÔNG CÓ QUYỀN XEM
          </span>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-2xl border border-[#004c91]/20 shadow-sm overflow-hidden mb-6 transition-all duration-300">
      <div 
        className="px-6 py-4 flex items-center justify-between cursor-pointer transition-colors bg-white hover:bg-slate-50 border-b border-gray-100"
        onClick={onToggle}
      >
        <h2 className="text-lg font-bold text-[#004c91] flex items-center gap-2">
          <Icon className="w-5 h-5 text-[#f37021]" />
          {title}
        </h2>
        <div className="w-8 h-8 rounded-full bg-slate-100 flex items-center justify-center text-slate-500">
          {isExpanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
        </div>
      </div>
      <AnimatePresence>
        {isExpanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden"
          >
            <div className="p-6 bg-white">
              {children}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

const EmptyState = ({ message = 'Chưa có dữ liệu cho phần này.' }) => (
  <div className="flex flex-col items-center justify-center py-8 text-center bg-slate-50 rounded-xl border border-dashed border-gray-200">
    <div className="w-12 h-12 bg-white rounded-full flex items-center justify-center shadow-sm mb-3">
      <Clock className="w-6 h-6 text-slate-300" />
    </div>
    <p className="text-sm font-medium text-slate-500">{message}</p>
  </div>
);

const WorkspaceStatus = ({ status }: { status: ContributionSectionStatus }) => {
  if (!status.canView) return <EmptyState message="Bạn không có quyền xem phần này." />;
  if (status.placeholder) return <EmptyState message="Chưa có dữ liệu được tạo." />;
  return <div className="text-sm text-gray-600 bg-slate-50 p-4 rounded-xl border border-slate-200">Dữ liệu đang được hiển thị... (Phase 2/3)</div>;
};

export function VisitProcessSummaryPage() {
  const navigate = useNavigate();
  const { visitInstanceId } = useParams();
  
  const [data, setData] = useState<ProcessSummaryPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({
    request: true,
    agenda: true,
    participants: false,
    logistics: false,
    minutes: false,
    media: false,
    news: false,
    feedback: false,
    timeline: false
  });

  const toggleSection = (key: string) => {
    setExpandedSections(prev => ({ ...prev, [key]: !prev[key] }));
  };

  useEffect(() => {
    const fetchData = async () => {
      if (!visitInstanceId) return;
      try {
        const result = await delegationsApi.getVisitInstanceSummary(visitInstanceId);
        setData(result);
        setError(false);
      } catch (err) {
        console.error(err);
        setError(true);
      } finally {
        setLoading(false);
      }
    };
    void fetchData();
  }, [visitInstanceId]);

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[400px]">
        <Loader2 className="w-8 h-8 animate-spin text-[#004c91] mb-4" />
        <p className="text-sm font-bold text-slate-500">Đang tải báo cáo tổng hợp...</p>
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto">
        <div className="bg-white rounded-[2rem] border border-gray-200 p-16 text-center shadow-sm flex flex-col items-center justify-center min-h-[350px]">
          <div className="w-20 h-20 bg-rose-50 rounded-full flex items-center justify-center mb-6">
            <Lock className="w-10 h-10 text-rose-400 stroke-[1.5]" />
          </div>
          <h2 className="text-xl font-bold text-slate-800 mb-2">Không có quyền truy cập</h2>
          <p className="text-gray-500 font-medium max-w-sm mx-auto leading-relaxed text-sm mb-6">
            Bạn không có quyền xem báo cáo tổng hợp của đoàn này, hoặc đoàn không tồn tại.
          </p>
          <button onClick={() => navigate('/dashboard/visit')} className="px-6 py-2.5 rounded-xl bg-[#004c91] text-white text-sm font-bold hover:bg-[#003b70] transition-colors outline-none">
            Về danh sách
          </button>
        </div>
      </div>
    );
  }

  const { permissions: perm } = data;

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-24">
      <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <span className="cursor-pointer hover:text-[#004c91] transition-colors" onClick={() => navigate('/dashboard/visit')}>Quản lý tiếp khách</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Báo cáo tổng hợp</span>
      </div>

      <div className="mb-6 flex flex-col sm:flex-row sm:items-end justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91] mb-2">{perm.delegationName}</h1>
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <span className={`px-2.5 py-1 rounded-lg border font-bold ${getStatusColor(perm.instanceStatus)}`}>
              {getStatusText(perm.instanceStatus)}
            </span>
            <span className="px-2.5 py-1 rounded-lg border border-slate-200 bg-slate-100 text-slate-700 font-bold">
              {perm.campusName || 'N/A'}
            </span>
            <span className="px-2.5 py-1 rounded-lg border border-purple-200 bg-purple-50 text-purple-700 font-bold flex items-center gap-1">
              Host: {perm.hostName || 'Chưa phân công'}
            </span>
            <span className="px-2.5 py-1 rounded-lg bg-emerald-50 text-emerald-700 font-bold border border-emerald-200 ml-auto flex items-center gap-1">
              <Lock className="w-3.5 h-3.5" /> READ-ONLY ({perm.relation})
            </span>
          </div>
        </div>
      </div>

      <div className="bg-blue-50 border-l-4 border-[#004c91] p-4 rounded-xl flex items-start gap-3 shadow-sm mb-8">
        <AlertCircle className="w-5 h-5 text-[#004c91] shrink-0 mt-0.5" />
        <p className="text-sm font-medium text-blue-900">
          Đây là màn hình <strong>Báo cáo tổng hợp (Chỉ đọc)</strong> dành cho {perm.relation === 'HO' ? 'Head Office' : 'Staff Leader'} giám sát quá trình tiếp khách. Mọi chức năng thao tác, cập nhật đều bị khóa.
        </p>
      </div>

      <div className="space-y-2">
        <SectionCard 
          title="Thông tin yêu cầu" 
          icon={Info} 
          isExpanded={expandedSections.request} 
          onToggle={() => toggleSection('request')}
          canView={perm.canViewRequestSummary}
        >
          {data.requestSummary ? (
            <div className="space-y-8">
              <RegistrantInfoReadOnly summary={data.requestSummary} />
              <DelegationInfoReadOnly summary={data.requestSummary} />
            </div>
          ) : <EmptyState />}
        </SectionCard>

        <SectionCard 
          title="Lịch trình làm việc (Agenda)" 
          icon={Calendar} 
          isExpanded={expandedSections.agenda} 
          onToggle={() => toggleSection('agenda')}
          canView={perm.canViewAgendaSummary}
        >
          {data.agendaSummary.length > 0 ? (
            <div className="space-y-4">
              {data.agendaSummary.map((item, idx) => (
                <div key={idx} className="p-4 rounded-xl border border-gray-200 bg-slate-50 flex flex-col gap-2">
                  <div className="flex justify-between items-start">
                    <h3 className="font-bold text-[#004c91]">{item.title}</h3>
                    <span className="text-xs font-bold text-slate-500 bg-white px-2 py-1 rounded-md border border-slate-200">
                      {new Date(item.startTime).toLocaleString('vi-VN')} - {item.endTime ? new Date(item.endTime).toLocaleString('vi-VN') : ''}
                    </span>
                  </div>
                  {item.description && <p className="text-sm text-gray-600">{item.description}</p>}
                  <div className="flex items-center gap-4 text-xs font-medium text-gray-500 mt-2">
                    <span className="flex items-center gap-1"><Info className="w-3 h-3" /> {item.location || 'Chưa cập nhật địa điểm'}</span>
                    <span className="flex items-center gap-1"><Users className="w-3 h-3" /> Phụ trách: {item.responsibleUserName || 'Chưa phân công'}</span>
                  </div>
                </div>
              ))}
            </div>
          ) : <EmptyState message="Không có mục lịch trình nào được lưu." />}
        </SectionCard>

        <SectionCard 
          title="Thành phần tham gia" 
          icon={Users} 
          isExpanded={expandedSections.participants} 
          onToggle={() => toggleSection('participants')}
          canView={perm.canViewParticipantSummary}
        >
          {data.participantSummary.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse min-w-[600px]">
                <thead>
                  <tr className="bg-slate-50">
                    <th className="p-3 text-xs font-bold text-slate-500 uppercase rounded-tl-xl">Họ tên</th>
                    <th className="p-3 text-xs font-bold text-slate-500 uppercase">Vai trò</th>
                    <th className="p-3 text-xs font-bold text-slate-500 uppercase rounded-tr-xl">Trạng thái</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {data.participantSummary.map((p, idx) => (
                    <tr key={idx} className="hover:bg-slate-50">
                      <td className="p-3 text-sm font-medium text-gray-900">{p.fullName}</td>
                      <td className="p-3 text-sm text-gray-600">{p.isHost ? 'Host' : p.participantRole}</td>
                      <td className="p-3 text-sm">
                        <span className={`px-2 py-0.5 rounded-md text-xs font-bold ${
                          p.status === 'ACCEPTED' ? 'bg-emerald-100 text-emerald-700' :
                          p.status === 'DECLINED' ? 'bg-rose-100 text-rose-700' :
                          p.status === 'INVITED' ? 'bg-amber-100 text-amber-700' : 'bg-gray-100 text-gray-700'
                        }`}>
                          {p.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : <EmptyState message="Chưa có người tham gia nào." />}
        </SectionCard>

        <SectionCard 
          title="Hậu cần & Chuẩn bị" 
          icon={Box} 
          isExpanded={expandedSections.logistics} 
          onToggle={() => toggleSection('logistics')}
          canView={perm.canViewLogisticsSummary}
        >
          {data.logisticsSummary.length > 0 ? (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {data.logisticsSummary.map((log, idx) => (
                <div key={idx} className="p-4 rounded-xl border border-gray-200 bg-white shadow-sm flex flex-col gap-2">
                  <div className="flex justify-between items-start">
                    <h3 className="font-bold text-gray-800 text-sm">{log.title}</h3>
                    <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">{log.itemType}</span>
                  </div>
                  <div className="flex items-center gap-2 text-xs">
                    <span className={`px-2 py-0.5 rounded border font-bold ${
                      log.status === 'DONE' ? 'bg-emerald-50 text-emerald-600 border-emerald-200' : 
                      'bg-amber-50 text-amber-600 border-amber-200'
                    }`}>
                      {log.status}
                    </span>
                    <span className="text-gray-500">
                      Đơn vị: {log.departmentName || 'Chưa rõ'}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          ) : <EmptyState message="Không có hạng mục hậu cần nào." />}
        </SectionCard>

        <SectionCard title="Biên bản làm việc" icon={FileText} isExpanded={expandedSections.minutes} onToggle={() => toggleSection('minutes')} canView={perm.canViewMinutesSummary}>
          <WorkspaceStatus status={data.minutesSummary} />
        </SectionCard>

        <SectionCard title="Hình ảnh / Media" icon={ImageIcon} isExpanded={expandedSections.media} onToggle={() => toggleSection('media')} canView={perm.canViewMediaSummary}>
          <WorkspaceStatus status={data.mediaSummary} />
        </SectionCard>

        <SectionCard title="Tin tức & Bài viết" icon={Newspaper} isExpanded={expandedSections.news} onToggle={() => toggleSection('news')} canView={perm.canViewNewsSummary}>
          <WorkspaceStatus status={data.newsSummary} />
        </SectionCard>
        
        <SectionCard title="Đánh giá chất lượng (Feedback)" icon={MessageSquare} isExpanded={expandedSections.feedback} onToggle={() => toggleSection('feedback')} canView={perm.canViewFeedbackSummary}>
          <EmptyState message="Feedback chỉ khả dụng sau khi chuyến thăm hoàn tất." />
        </SectionCard>

        <SectionCard title="Lịch sử cập nhật (Timeline)" icon={History} isExpanded={expandedSections.timeline} onToggle={() => toggleSection('timeline')} canView={perm.canViewTimeline}>
          <EmptyState message="Tính năng Timeline đang được phát triển." />
        </SectionCard>
      </div>
    </div>
  );
}
