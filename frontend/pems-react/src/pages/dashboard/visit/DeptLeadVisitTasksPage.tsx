import React, { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { VisitRequestManagement } from './VisitRequestManagement';
import { SharedDashboardView } from '../home/SharedDashboardView';
import { DepartmentDetailDashboard } from '../departments/DepartmentDetailDashboard';
import { DeptLeadAssignmentTab } from './DeptLeadAssignmentTab';

export function DeptLeadVisitTasksPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  
  // Read tab from query params, default to 'calendar'
  const tabParam = searchParams.get('tab');
  const activeTab = tabParam || 'calendar';

  const handleTabChange = (tab: string) => {
    setSearchParams({ tab });
  };

  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const isDeptLeader = true; // Since this page is only accessible by DeptLeader

  return (
    <div className="w-full max-w-[1400px] mx-auto p-4 sm:p-6 lg:p-8 flex flex-col space-y-6 pb-12 overflow-x-hidden animate-in fade-in duration-300">
      <div className="border-b border-gray-100 pb-4">
        <h1 className="text-3xl font-bold text-[#004c91]">Nhiệm vụ tiếp khách</h1>
        <p className="text-slate-500 mt-2">Theo dõi, phân công và cập nhật các nhiệm vụ phòng ban được mời hỗ trợ.</p>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-gray-200 overflow-x-auto custom-scrollbar mb-6">
        <button
          onClick={() => handleTabChange('calendar')}
          className={`whitespace-nowrap px-6 py-3 font-bold text-sm border-b-2 transition-colors ${activeTab === 'calendar' ? 'border-[#004c91] text-[#004c91] bg-blue-50' : 'border-transparent text-gray-500 hover:text-[#004c91] hover:bg-blue-50/50'}`}
        >
          Bảng lịch
        </button>
        <button
          onClick={() => handleTabChange('assignment')}
          className={`whitespace-nowrap px-6 py-3 font-bold text-sm border-b-2 transition-colors ${activeTab === 'assignment' ? 'border-[#004c91] text-[#004c91] bg-blue-50' : 'border-transparent text-gray-500 hover:text-[#004c91] hover:bg-blue-50/50'}`}
        >
          Phân công
        </button>
        <button
          onClick={() => handleTabChange('progress')}
          className={`whitespace-nowrap px-6 py-3 font-bold text-sm border-b-2 transition-colors ${activeTab === 'progress' ? 'border-[#004c91] text-[#004c91] bg-blue-50' : 'border-transparent text-gray-500 hover:text-[#004c91] hover:bg-blue-50/50'}`}
        >
          Theo dõi tiến độ đoàn khách
        </button>
      </div>

      {/* Tab Contents */}
      {activeTab === 'calendar' && (
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
          {/* Reuse SharedDashboardView but hide its KPI cards if possible, or just render it. 
              Since SharedDashboardView is complex, let's just render it and we can hide metrics via CSS. */}
          <div className="dept-lead-calendar-wrapper">
             <style>{`
               .dept-lead-calendar-wrapper .grid.grid-cols-1.md\\:grid-cols-3.gap-6 { display: none !important; }
             `}</style>
             <SharedDashboardView user={user} isDeptLeader={isDeptLeader} />
          </div>
        </div>
      )}

      {activeTab === 'assignment' && (
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 overflow-hidden">
          <DeptLeadAssignmentTab />
        </div>
      )}

      {activeTab === 'progress' && (
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden dept-lead-progress-wrapper">
          <style>{`
            .dept-lead-progress-wrapper > div { padding: 0 !important; max-width: 100% !important; }
            .dept-lead-progress-wrapper h1 { display: none !important; }
            .dept-lead-progress-wrapper .text-slate-500.mt-2 { display: none !important; }
          `}</style>
          <VisitRequestManagement isEmbedded={true} />
        </div>
      )}
    </div>
  );
}
