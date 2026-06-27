import React from 'react';
import { useSearchParams } from 'react-router-dom';
import { SharedDashboardView } from '../departments/SharedDashboardView';

type DeptLeadTab = 'calendar' | 'assignments-progress';

export function DeptLeadVisitTasksPage() {
  const [searchParams, setSearchParams] = useSearchParams();

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
    <div className="w-full max-w-[1400px] mx-auto p-4 sm:p-6 lg:p-8 flex flex-col space-y-6 pb-12 overflow-x-hidden animate-in fade-in duration-300">
      <div className="border-b border-gray-100 pb-4">
        <h1 className="text-3xl font-bold text-[#004c91]">Nhiệm vụ tiếp khách</h1>
        <p className="text-slate-500 mt-2">Theo dõi, phân công và cập nhật các nhiệm vụ phòng ban được mời hỗ trợ.</p>
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
          className={`whitespace-nowrap px-6 py-3 font-bold text-sm border-b-2 transition-colors ${
            activeTab === 'assignments-progress'
              ? 'border-[#004c91] text-[#004c91] bg-blue-50'
              : 'border-transparent text-gray-500 hover:text-[#004c91] hover:bg-blue-50/50'
          }`}
        >
          Phân công và tiến độ
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
