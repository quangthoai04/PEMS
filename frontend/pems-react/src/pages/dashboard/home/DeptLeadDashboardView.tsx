import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Users, Calendar, ClipboardList, Clock, AlertCircle } from 'lucide-react';
import { useDepartmentLeaderDashboard } from '../../../features/dashboard/hooks/useDepartmentLeaderDashboard';
import { format, parseISO } from 'date-fns';
import { vi } from 'date-fns/locale';

export function DeptLeadDashboardView({ user }: { user: any }) {
  const navigate = useNavigate();
  const { data, loading, error } = useDepartmentLeaderDashboard();
  
  const [currentTime, setCurrentTime] = useState<Date | null>(null);

  useEffect(() => {
    if (data?.serverNow) {
      setCurrentTime(parseISO(data.serverNow));
      
      const interval = setInterval(() => {
        setCurrentTime(prev => prev ? new Date(prev.getTime() + 60000) : null);
      }, 60000);
      
      return () => clearInterval(interval);
    }
  }, [data?.serverNow]);

  if (loading) {
    return <div className="p-8 text-center text-slate-500">Đang tải dữ liệu tổng quan...</div>;
  }

  if (error) {
    return (
      <div className="p-8 text-center text-red-500 flex flex-col items-center gap-2">
        <AlertCircle className="w-8 h-8" />
        <p>Lỗi: {error}</p>
      </div>
    );
  }

  const formattedTime = currentTime 
    ? format(currentTime, "EEEE, dd/MM/yyyy - HH:mm", { locale: vi })
    : '';

  const openCalendarItem = (visitInstanceId: number) => {
    navigate(`/dashboard/visit?tab=calendar&visitInstanceId=${visitInstanceId}`);
  };

  const cleanDelegationName = (name: string) =>
    name
      .replace(/\b(WAITING_REQUEST_APPROVAL|PENDING_APPROVAL|APPROVED|ASSIGNED|BEFORE_VISIT|DURING_VISIT|AFTER_VISIT|CLOSED|CANCELLED|REJECTED)\b/g, '')
      .replace(/\s{2,}/g, ' ')
      .replace(/\s+campus/g, ' campus')
      .trim();

  return (
    <div className="w-full max-w-[1400px] mx-auto p-4 sm:p-6 lg:p-8 flex flex-col space-y-6 pb-12 animate-in fade-in duration-300">
      {/* Header */}
      <div className="flex flex-col space-y-1">
        <div className="flex justify-between items-center">
          <h1 className="text-2xl sm:text-3xl font-black text-gray-900 tracking-tight flex items-center gap-3">
            <span className="bg-gradient-to-r from-[#004c91] to-blue-600 bg-clip-text text-transparent">
              Tổng quan (Action Center)
            </span>
          </h1>
          {formattedTime && (
            <div className="text-sm font-semibold bg-slate-100 text-slate-600 px-3 py-1.5 rounded-lg border border-slate-200">
              {formattedTime}
            </div>
          )}
        </div>
        <p className="text-sm font-medium text-slate-500">
          Trung tâm xử lý tác vụ và thông báo dành cho Trưởng phòng
        </p>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-6">
        {/* Card: Chờ phân công */}
        <div 
          onClick={() => navigate('/dashboard/visit?tab=assignment&status=REQUESTED')}
          className="bg-white border border-orange-100 rounded-2xl p-5 lg:p-6 shadow-sm hover:shadow-md hover:-translate-y-1 transition-all duration-300 flex items-center gap-4 cursor-pointer group"
        >
          <div className="w-12 h-12 lg:w-14 lg:h-14 rounded-2xl bg-orange-50 text-orange-500 flex items-center justify-center group-hover:bg-orange-500 group-hover:text-white transition-colors shrink-0">
            <ClipboardList className="w-6 h-6 lg:w-7 lg:h-7" />
          </div>
          <div>
            <p className="text-[10px] lg:text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">Chờ phân công</p>
            <p className="text-2xl lg:text-3xl font-black text-slate-800">{data?.pendingAssignmentCount || 0}</p>
          </div>
        </div>

        {/* Card: Đoàn sắp tới */}
        <div 
          onClick={() => navigate('/dashboard/visit?tab=calendar')}
          className="bg-white border border-blue-100 rounded-2xl p-5 lg:p-6 shadow-sm hover:shadow-md hover:-translate-y-1 transition-all duration-300 flex items-center gap-4 cursor-pointer group"
        >
          <div className="w-12 h-12 lg:w-14 lg:h-14 rounded-2xl bg-blue-50 text-[#004c91] flex items-center justify-center group-hover:bg-[#004c91] group-hover:text-white transition-colors shrink-0">
            <Calendar className="w-6 h-6 lg:w-7 lg:h-7" />
          </div>
          <div>
            <p className="text-[10px] lg:text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">Đoàn sắp tới</p>
            <p className="text-2xl lg:text-3xl font-black text-slate-800">{data?.upcomingDelegationCount || 0}</p>
          </div>
        </div>

        {/* Card: Đang xử lý */}
        <div 
          onClick={() => navigate('/dashboard/visit?tab=assignment&status=IN_PROGRESS')}
          className="bg-white border border-emerald-100 rounded-2xl p-5 lg:p-6 shadow-sm hover:shadow-md hover:-translate-y-1 transition-all duration-300 flex items-center gap-4 cursor-pointer group"
        >
          <div className="w-12 h-12 lg:w-14 lg:h-14 rounded-2xl bg-emerald-50 text-emerald-500 flex items-center justify-center group-hover:bg-emerald-500 group-hover:text-white transition-colors shrink-0">
            <Clock className="w-6 h-6 lg:w-7 lg:h-7" />
          </div>
          <div>
            <p className="text-[10px] lg:text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">Đang xử lý</p>
            <p className="text-2xl lg:text-3xl font-black text-slate-800">{data?.processingDelegationCount || 0}</p>
          </div>
        </div>

        {/* Card: Nhân sự */}
        <div 
          onClick={() => navigate(`/dashboard/departments/${user?.departmentId || '1'}`)}
          className="bg-white border border-purple-100 rounded-2xl p-5 lg:p-6 shadow-sm hover:shadow-md hover:-translate-y-1 transition-all duration-300 flex items-center gap-4 cursor-pointer group"
        >
          <div className="w-12 h-12 lg:w-14 lg:h-14 rounded-2xl bg-purple-50 text-purple-500 flex items-center justify-center group-hover:bg-purple-500 group-hover:text-white transition-colors shrink-0">
            <Users className="w-6 h-6 lg:w-7 lg:h-7" />
          </div>
          <div>
            <p className="text-[10px] lg:text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">Nhân sự</p>
            <p className="text-2xl lg:text-3xl font-black text-slate-800">{data?.activePersonnelCount || 0}</p>
          </div>
        </div>
      </div>

      {/* Quick Action Lists */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mt-6">
        {/* List: Tác vụ cần xử lý ngay */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
          <div className="p-5 border-b border-slate-100 bg-slate-50/50 flex items-center justify-between gap-3">
            <div className="p-2 bg-orange-100 text-orange-600 rounded-xl">
              <Clock className="w-5 h-5" />
            </div>
            <h2 className="text-lg font-bold text-slate-800">Tác vụ cần xử lý nhanh</h2>
          </div>
          <div className="p-0 flex-1 divide-y divide-slate-100 max-h-[540px] overflow-y-auto">
            {data?.quickTasks && data.quickTasks.length > 0 ? (
              data.quickTasks.map((task) => (
                <div 
                  key={`${task.itemType || 'REQUEST'}-${task.participantId ?? task.logisticsItemId}`}
                  className="p-5 hover:bg-slate-50 transition-colors cursor-pointer flex justify-between items-center gap-4"
                  onClick={() => navigate('/dashboard/visit?tab=assignment&status=REQUESTED')}
                >
                  <div>
                    <p className="font-bold text-slate-800">{cleanDelegationName(task.delegationName)} - {task.taskTitle}</p>
                    <p className="text-sm text-slate-500 mt-1">
                      {task.dueAt ? `Hạn chót: ${format(parseISO(task.dueAt), 'dd/MM/yyyy')}` : 'Không có hạn chót'}
                    </p>
                  </div>
                  {true ? (
                    <span className="px-3 py-1 bg-red-50 text-red-600 rounded-full text-xs font-bold whitespace-nowrap">Chưa phân công</span>
                  ) : (
                    <span className="px-3 py-1 bg-blue-50 text-blue-600 rounded-full text-xs font-bold whitespace-nowrap">Đã phân công: {task.assignedToName}</span>
                  )}
                </div>
              ))
            ) : (
              <div className="p-8 text-center text-slate-500">
                Không có tác vụ nào cần xử lý nhanh.
              </div>
            )}
          </div>
          <div 
            className="p-4 border-t border-slate-100 bg-slate-50 text-center cursor-pointer hover:bg-slate-100 transition-colors text-[#004c91] font-bold text-sm"
            onClick={() => navigate('/dashboard/visit?tab=assignment&status=REQUESTED')}
          >
            Xem tất cả
          </div>
        </div>

        {/* List: Lịch tiếp đón sắp tới (Mini Calendar or List) */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
          <div className="p-5 border-b border-slate-100 bg-slate-50/50 flex items-center justify-between gap-3">
            <div className="p-2 bg-blue-100 text-[#004c91] rounded-xl">
              <Calendar className="w-5 h-5" />
            </div>
            <h2 className="text-lg font-bold text-slate-800">Lịch tiếp đón sắp tới</h2>
          </div>
          <div className="p-0 flex-1 divide-y divide-slate-100 max-h-[540px] overflow-y-auto">
             {data?.upcomingSchedules && data.upcomingSchedules.length > 0 ? (
               data.upcomingSchedules.map((schedule) => {
                 const startDate = parseISO(schedule.plannedStartAt);
                 const endDate = parseISO(schedule.plannedEndAt);
                 return (
                   <div
                     key={`${schedule.itemType || 'REQUEST'}-${schedule.participantId ?? schedule.logisticsItemId ?? schedule.visitInstanceId}`}
                     className="p-5 flex gap-4 items-start cursor-pointer hover:bg-slate-50 transition-colors"
                     onClick={() => openCalendarItem(schedule.visitInstanceId)}
                   >
                     <div className="flex flex-col items-center justify-center w-12 h-12 bg-blue-50 rounded-xl text-[#004c91] border border-blue-100 shrink-0">
                       <span className="text-[10px] font-bold uppercase">Thg {format(startDate, 'M')}</span>
                       <span className="text-lg font-black leading-none">{format(startDate, 'd')}</span>
                     </div>
                     <div>
                       <p className="font-bold text-slate-800">{cleanDelegationName(schedule.delegationName)}</p>
                       <p className="text-sm text-slate-500 mt-1">
                         {format(startDate, 'HH:mm')} - {format(endDate, 'HH:mm')} • {schedule.campusName}
                       </p>
                     </div>
                   </div>
                 );
               })
             ) : (
               <div className="p-8 text-center text-slate-500">
                 Không có lịch tiếp đón sắp tới.
               </div>
             )}
          </div>
          <div 
            className="p-4 border-t border-slate-100 bg-slate-50 text-center cursor-pointer hover:bg-slate-100 transition-colors text-[#004c91] font-bold text-sm"
            onClick={() => navigate('/dashboard/visit?tab=calendar')}
          >
            Mở bảng lịch chi tiết
          </div>
        </div>
      </div>
    </div>
  );
}

