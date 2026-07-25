import React, { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { SharedDashboardView } from '../departments/SharedDashboardView';
import { notificationsApi } from '../../../features/notifications/api/notificationsApi';

type DeptLeadTab = 'calendar' | 'assignments-progress';

const ASSIGNMENT_RELATED_TYPES = ['VISIT_REQUEST', 'VISIT_PARTICIPANT'];

export function DeptLeadVisitTasksPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [pendingAssignmentCount, setPendingAssignmentCount] = useState(0);

  useEffect(() => {
    let cancelled = false;
    notificationsApi.getNotifications({ isRead: false, pageSize: 50 })
      .then(data => {
        if (cancelled) return;
        const count = data.items.filter(n => n.relatedType && ASSIGNMENT_RELATED_TYPES.includes(n.relatedType)).length;
        setPendingAssignmentCount(count);
      })
      .catch(() => {});
    return () => { cancelled = true; };
  }, []);

  const rawTab = searchParams.get('tab');
  const activeTab: DeptLeadTab =
    rawTab === 'assignment' || rawTab === 'progress' || rawTab === 'assignments-progress'
      ? 'assignments-progress'
      : 'calendar';

  const visitInstanceIdParam = searchParams.get('visitInstanceId');
  const selectedVisitInstanceId = visitInstanceIdParam ? Number(visitInstanceIdParam) : null;

  const handleTabChange = (tab: DeptLeadTab) => {
    const next: Record<string, string> = { tab };
    if (selectedVisitInstanceId && tab === 'calendar') {
      next.visitInstanceId = String(selectedVisitInstanceId);
    }
    setSearchParams(next);
  };

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const isDeptLeader = true;

  return (
    <div className="w-full flex flex-col space-y-6 pb-12 overflow-x-hidden animate-in fade-in duration-300">
      <div className="border-b border-gray-100 pb-3">
        <h1 className="text-3xl font-bold text-[#004c91]">Nhiệm vụ tiếp khách</h1>
      </div>

      <div className="flex border-b border-gray-200 overflow-x-auto custom-scrollbar mb-6">
        <button
          type="button"
          onClick={() => handleTabChange('calendar')}
          className={`whitespace-nowrap px-6 py-3 font-bold text-sm border-b-2 transition-colors ${
            activeTab === 'calendar'
              ? 'border-[#004c91] text-[#004c91] bg-blue-50'
              : 'border-transparent text-gray-500 hover:text-[#004c91] hover:bg-blue-50/50'
          }`}
        >
          Bảng lịch
        </button>
        <button
          type="button"
          onClick={() => handleTabChange('assignments-progress')}
          className={`flex items-center gap-2 whitespace-nowrap px-6 py-3 font-bold text-sm border-b-2 transition-colors ${
            activeTab === 'assignments-progress'
              ? 'border-[#004c91] text-[#004c91] bg-blue-50'
              : 'border-transparent text-gray-500 hover:text-[#004c91] hover:bg-blue-50/50'
          }`}
        >
          Phân công và tiến độ
          {pendingAssignmentCount > 0 && (
            <span className="px-2 py-0.5 text-[10px] font-black bg-red-500 text-white rounded-full">
              {pendingAssignmentCount}
            </span>
          )}
        </button>
      </div>

      <div className={activeTab === 'calendar' ? 'bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden' : 'overflow-hidden'}>
        <SharedDashboardView
          user={user}
          isDeptLeader={isDeptLeader}
          initialVisitInstanceId={selectedVisitInstanceId}
          viewMode={activeTab === 'calendar' ? 'calendar' : 'assignments'}
        />
      </div>
    </div>
  );
}
