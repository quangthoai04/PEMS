import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Users, Calendar, Bell, ClipboardList, Clock } from 'lucide-react';
import { SharedDashboardView } from './SharedDashboardView';

export function DeptLeadDashboardView({ user }: { user: any }) {
  const navigate = useNavigate();

  return (
    <div className="w-full max-w-[1400px] mx-auto p-4 sm:p-6 lg:p-8 flex flex-col space-y-6 pb-12 animate-in fade-in duration-300">
      {/* Header */}
      <div className="flex flex-col space-y-1">
        <h1 className="text-2xl sm:text-3xl font-black text-gray-900 tracking-tight flex items-center gap-3">
          <span className="bg-gradient-to-r from-[#004c91] to-blue-600 bg-clip-text text-transparent">
            Tổng quan (Action Center)
          </span>
        </h1>
        <p className="text-sm font-medium text-slate-500">
          Trung tâm xử lý tác vụ và thông báo dành cho Trưởng phòng
        </p>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-6">
        {/* Card: Chờ phân công */}
        <div 
          onClick={() => navigate('/dashboard/visit?tab=assignment')}
          className="bg-white border border-orange-100 rounded-2xl p-5 lg:p-6 shadow-sm hover:shadow-md hover:-translate-y-1 transition-all duration-300 flex items-center gap-4 cursor-pointer group"
        >
          <div className="w-12 h-12 lg:w-14 lg:h-14 rounded-2xl bg-orange-50 text-orange-500 flex items-center justify-center group-hover:bg-orange-500 group-hover:text-white transition-colors shrink-0">
            <ClipboardList className="w-6 h-6 lg:w-7 lg:h-7" />
          </div>
          <div>
            <p className="text-[10px] lg:text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">Chờ phân công</p>
            <p className="text-2xl lg:text-3xl font-black text-slate-800">3</p>
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
            <p className="text-2xl lg:text-3xl font-black text-slate-800">8</p>
          </div>
        </div>

        {/* Card: Đang xử lý */}
        <div 
          onClick={() => navigate('/dashboard/visit?tab=progress')}
          className="bg-white border border-emerald-100 rounded-2xl p-5 lg:p-6 shadow-sm hover:shadow-md hover:-translate-y-1 transition-all duration-300 flex items-center gap-4 cursor-pointer group"
        >
          <div className="w-12 h-12 lg:w-14 lg:h-14 rounded-2xl bg-emerald-50 text-emerald-500 flex items-center justify-center group-hover:bg-emerald-500 group-hover:text-white transition-colors shrink-0">
            <Clock className="w-6 h-6 lg:w-7 lg:h-7" />
          </div>
          <div>
            <p className="text-[10px] lg:text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">Đang xử lý</p>
            <p className="text-2xl lg:text-3xl font-black text-slate-800">1</p>
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
            <p className="text-2xl lg:text-3xl font-black text-slate-800">12</p>
          </div>
        </div>
      </div>

      {/* Quick Action Lists */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mt-6">
        {/* List: Tác vụ cần xử lý ngay */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
          <div className="p-5 border-b border-slate-100 bg-slate-50/50 flex items-center gap-3">
            <div className="p-2 bg-orange-100 text-orange-600 rounded-xl">
              <Clock className="w-5 h-5" />
            </div>
            <h2 className="text-lg font-bold text-slate-800">Tác vụ cần xử lý nhanh</h2>
          </div>
          <div className="p-0 flex-1 divide-y divide-slate-100">
            <div 
              className="p-5 hover:bg-slate-50 transition-colors cursor-pointer flex justify-between items-center"
              onClick={() => navigate('/dashboard/visit?tab=assignment')}
            >
              <div>
                <p className="font-bold text-slate-800">Đoàn ĐH Deakin - Chuẩn bị phòng họp</p>
                <p className="text-sm text-slate-500 mt-1">Hạn chót: Hôm nay</p>
              </div>
              <span className="px-3 py-1 bg-red-50 text-red-600 rounded-full text-xs font-bold whitespace-nowrap">Chưa phân công</span>
            </div>
            <div 
              className="p-5 hover:bg-slate-50 transition-colors cursor-pointer flex justify-between items-center"
              onClick={() => navigate('/dashboard/visit?tab=assignment')}
            >
              <div>
                <p className="font-bold text-slate-800">Đoàn đối tác Nhật Bản - Setup máy chiếu</p>
                <p className="text-sm text-slate-500 mt-1">Hạn chót: Ngày mai</p>
              </div>
              <span className="px-3 py-1 bg-red-50 text-red-600 rounded-full text-xs font-bold whitespace-nowrap">Chưa phân công</span>
            </div>
          </div>
          <div 
            className="p-4 border-t border-slate-100 bg-slate-50 text-center cursor-pointer hover:bg-slate-100 transition-colors text-[#004c91] font-bold text-sm"
            onClick={() => navigate('/dashboard/visit?tab=assignment')}
          >
            Xem tất cả (2)
          </div>
        </div>

        {/* List: Lịch tiếp đón sắp tới (Mini Calendar or List) */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
          <div className="p-5 border-b border-slate-100 bg-slate-50/50 flex items-center gap-3">
            <div className="p-2 bg-blue-100 text-[#004c91] rounded-xl">
              <Calendar className="w-5 h-5" />
            </div>
            <h2 className="text-lg font-bold text-slate-800">Lịch tiếp đón sắp tới</h2>
          </div>
          <div className="p-0 flex-1 divide-y divide-slate-100">
             {/* Just a mini-list of upcoming events */}
             <div className="p-5 flex gap-4 items-start">
               <div className="flex flex-col items-center justify-center w-12 h-12 bg-blue-50 rounded-xl text-[#004c91] border border-blue-100 shrink-0">
                 <span className="text-[10px] font-bold uppercase">Thg 8</span>
                 <span className="text-lg font-black leading-none">26</span>
               </div>
               <div>
                 <p className="font-bold text-slate-800">Đoàn ĐH Deakin tham quan</p>
                 <p className="text-sm text-slate-500 mt-1">09:00 - 11:30 • Campus Hola</p>
               </div>
             </div>
             <div className="p-5 flex gap-4 items-start">
               <div className="flex flex-col items-center justify-center w-12 h-12 bg-blue-50 rounded-xl text-[#004c91] border border-blue-100 shrink-0">
                 <span className="text-[10px] font-bold uppercase">Thg 8</span>
                 <span className="text-lg font-black leading-none">28</span>
               </div>
               <div>
                 <p className="font-bold text-slate-800">Đoàn đối tác Nhật Bản</p>
                 <p className="text-sm text-slate-500 mt-1">14:00 - 16:00 • Campus Hola</p>
               </div>
             </div>
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
