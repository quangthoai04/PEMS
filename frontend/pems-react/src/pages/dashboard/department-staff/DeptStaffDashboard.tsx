/**
 * DeptStaffDashboard – Dashboard cho DEPARTMENT + subRole STAFF
 * Chỉ có 2 tab: Bảng lịch | Nhiệm vụ được giao
 * Không có banner riêng (DashboardHome đã render banner bên trên)
 */
import React, { useState, useEffect, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Calendar, ClipboardList } from 'lucide-react';
import { Toaster } from 'react-hot-toast';
import { StaffCalendarTab } from './StaffCalendarTab';
import { StaffTasksTab } from './StaffTasksTab';
import { useDeptStaffData, DEFAULT_FILTER } from './useDeptStaffData';
import type { AssignmentsFilter } from './useDeptStaffData';
import type { TaskStatusFilter } from './useDeptStaffData';
import { StaffLeaderTaskModal, type StaffLeaderTaskModalItem } from './StaffLeaderTaskModal';
import { toVietnamCalendarDate } from '../../../shared/utils/vietnamTime';

const TABS = [
  { id: 'calendar', label: 'Bảng lịch', icon: Calendar },
  { id: 'tasks',    label: 'Nhiệm vụ được giao', icon: ClipboardList },
] as const;
type TabId = typeof TABS[number]['id'];

export function DeptStaffDashboard() {
  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;

  const [activeTab, setActiveTab] = useState<TabId>('calendar');
  // Năm hiện tại theo giờ Việt Nam (đêm giao thừa VN, browser Mỹ vẫn ra năm VN).
  const [currentYear, setCurrentYear] = useState((toVietnamCalendarDate(new Date()) ?? new Date()).getUTCFullYear());
  const [filter, setFilter] = useState<AssignmentsFilter>(DEFAULT_FILTER);
  
  const [searchParams, setSearchParams] = useSearchParams();
  const [urlTaskItem, setUrlTaskItem] = useState<StaffLeaderTaskModalItem | null>(null);

  useEffect(() => {
    const taskId = searchParams.get('taskId');
    const itemType = searchParams.get('itemType');
    if (taskId && (itemType === 'REQUEST' || itemType === 'INVITATION')) {
      setUrlTaskItem({
        rawId: parseInt(taskId, 10),
        itemType: itemType as 'REQUEST' | 'INVITATION',
      });
      setActiveTab('tasks');
    }
  }, [searchParams]);

  const {
    calendarItems, calendarLoading, fetchCalendar,
    tasks, totalTasks, tasksLoading, attentionItems,
    fetchTasks,
  } = useDeptStaffData(currentYear);

  useEffect(() => { fetchCalendar(); }, [fetchCalendar]);

  useEffect(() => {
    fetchTasks(filter);
  }, [filter, fetchTasks]);

  const handleFilterChange = useCallback((patch: Partial<AssignmentsFilter>) => {
    setFilter(prev => ({ ...prev, ...patch }));
  }, []);

  const handleRefresh = useCallback(() => {
    fetchCalendar();
    fetchTasks(filter);
  }, [fetchCalendar, fetchTasks, filter]);

  return (
    <div className="space-y-5">
      <Toaster position="top-right" containerStyle={{ zIndex: 9999 }} />

      {/* Tab switcher + content */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        {/* Tab bar */}
        <div className="flex border-b border-slate-200">
          {TABS.map(tab => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-2.5 px-6 py-4 text-sm font-black transition-all border-b-2 ${
                  isActive
                    ? 'border-[#004c91] text-[#004c91] bg-blue-50/50'
                    : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-50'
                }`}>
                <Icon className="w-4 h-4" />
                {tab.label}
                {tab.id === 'tasks' && attentionItems.length > 0 && (
                  <span className="ml-1 px-2 py-0.5 text-[10px] font-black bg-orange-500 text-white rounded-full">
                    {attentionItems.length}
                  </span>
                )}
              </button>
            );
          })}
        </div>

        {/* Tab content */}
        <div className="p-5 md:p-6">
          {activeTab === 'calendar' && (
            <StaffCalendarTab
              year={currentYear}
              onYearChange={setCurrentYear}
              calendarItems={calendarItems}
              calendarLoading={calendarLoading}
              onRefresh={handleRefresh}
            />
          )}
          {activeTab === 'tasks' && (
            <StaffTasksTab
              user={user}
              tasks={tasks}
              totalTasks={totalTasks}
              tasksLoading={tasksLoading}
              attentionItems={attentionItems}
              filter={filter}
              onFilterChange={handleFilterChange}
              onRefresh={handleRefresh}
            />
          )}
        </div>
      </div>

      {urlTaskItem && (
        <StaffLeaderTaskModal
          item={urlTaskItem}
          onClose={() => {
            setUrlTaskItem(null);
            const newParams = new URLSearchParams(searchParams);
            newParams.delete('taskId');
            newParams.delete('itemType');
            setSearchParams(newParams, { replace: true });
          }}
          onRefresh={handleRefresh}
        />
      )}
    </div>
  );
}
