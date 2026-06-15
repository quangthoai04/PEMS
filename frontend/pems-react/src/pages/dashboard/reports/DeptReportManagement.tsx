/**
 * Trang DeptReportManagement
 * Trung tâm bảng thống kê mức quy mô theo phòng ban hiệu suất phục vụ.
 */

import React, { useState } from 'react';
import { Download, Calendar, Filter, Briefcase, Clock, UserCheck, CheckCircle2, TrendingUp, Users, Target, Activity } from 'lucide-react';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend } from 'recharts';
import { DEPT_LEADER_STATISTICS, DEPT_TASKS_OVER_TIME, DEPT_TASKS_BY_TYPE, TOP_PERFORMING_MEMBERS } from './mockReportData';

export function DeptReportManagement() {
  const [timeFilter, setTimeFilter] = useState('this_year');

  return (
    <div className="space-y-6 animate-in fade-in duration-500 pb-12 font-sans">
      <div className="flex items-center gap-2 text-sm text-slate-500 font-medium mb-4">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Thống kê phòng ban</span>
      </div>

      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <h2 className="text-3xl font-black text-[#004c91] tracking-tight">Thống kê hiệu suất phòng ban</h2>
          <p className="text-base font-medium text-slate-500 mt-1">Tổng quan dữ liệu công việc và sự tham gia của nhân viên</p>
        </div>

        <div className="flex items-center gap-3 w-full md:w-auto">
          <div className="relative flex-1 md:w-48">
            <select
              value={timeFilter}
              onChange={(e) => setTimeFilter(e.target.value)}
              className="w-full appearance-none bg-white border border-slate-200 text-slate-700 py-2.5 pl-10 pr-8 rounded-xl focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] font-semibold text-sm shadow-sm"
            >
              <option value="this_month">Tháng này</option>
              <option value="last_month">Tháng trước</option>
              <option value="this_quarter">Quý này</option>
              <option value="this_year">Năm nay</option>
            </select>
            <Calendar className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
            <Filter className="w-4 h-4 text-slate-400 absolute right-3.5 top-1/2 -translate-y-1/2" />
          </div>
          <button className="flex items-center justify-center gap-2 bg-[#004c91] hover:bg-[#00386b] text-white px-5 py-2.5 rounded-xl font-bold text-sm transition-colors shadow-md hover:shadow-lg active:scale-95 whitespace-nowrap">
            <Download className="w-4 h-4" />
            <span className="hidden sm:inline">Xuất báo cáo</span>
          </button>
        </div>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col justify-between group hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 rounded-xl bg-blue-50 flex items-center justify-center text-[#004c91] group-hover:scale-110 transition-transform">
              <Briefcase className="w-6 h-6" />
            </div>
            <span className="flex items-center gap-1 text-xs font-bold text-green-600 bg-green-50 px-2 py-1 rounded-lg">
               {DEPT_LEADER_STATISTICS.tasksChange}
            </span>
          </div>
          <div>
            <p className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-1">Tổng công việc</p>
            <p className="text-3xl font-black text-slate-800">{DEPT_LEADER_STATISTICS.totalTasks}</p>
          </div>
        </div>

        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col justify-between group hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 rounded-xl bg-orange-50 flex items-center justify-center text-[#e85c0d] group-hover:scale-110 transition-transform">
              <Clock className="w-6 h-6" />
            </div>
            <span className="flex items-center gap-1 text-xs font-bold text-green-600 bg-green-50 px-2 py-1 rounded-lg">
               {DEPT_LEADER_STATISTICS.hoursChange}
            </span>
          </div>
          <div>
            <p className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-1">Giờ tham gia (TB)</p>
            <p className="text-3xl font-black text-slate-800">{DEPT_LEADER_STATISTICS.totalHours}h</p>
          </div>
        </div>

        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col justify-between group hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 rounded-xl bg-purple-50 flex items-center justify-center text-purple-600 group-hover:scale-110 transition-transform">
              <UserCheck className="w-6 h-6" />
            </div>
            <span className="flex items-center gap-1 text-xs font-bold text-green-600 bg-green-50 px-2 py-1 rounded-lg">
               {DEPT_LEADER_STATISTICS.partnersChange}
            </span>
          </div>
          <div>
            <p className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-1">Đối tác đã kết nối</p>
            <p className="text-3xl font-black text-slate-800">{DEPT_LEADER_STATISTICS.totalPartners}</p>
          </div>
        </div>

        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col justify-between group hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 rounded-xl bg-emerald-50 flex items-center justify-center text-emerald-600 group-hover:scale-110 transition-transform">
              <CheckCircle2 className="w-6 h-6" />
            </div>
            <span className="flex items-center gap-1 text-xs font-bold text-green-600 bg-green-50 px-2 py-1 rounded-lg">
               {DEPT_LEADER_STATISTICS.completionChange}
            </span>
          </div>
          <div>
            <p className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-1">Tỷ lệ hoàn thành công việc</p>
            <div className="flex items-center gap-2">
              <p className="text-3xl font-black text-slate-800">{DEPT_LEADER_STATISTICS.completionRate}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Charts section */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Area Chart */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm lg:col-span-2">
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center mb-6">
            <div>
              <h3 className="text-lg font-bold text-slate-800">Hiệu suất xử lý công việc</h3>
              <p className="text-sm text-slate-500 font-medium mt-1">Thống kê số lượng công việc được giao và đã hoàn thành</p>
            </div>
            <div className="flex items-center gap-4 mt-4 sm:mt-0">
               <div className="flex items-center gap-2">
                  <div className="w-3 h-3 rounded-full bg-[#004c91]"></div>
                  <span className="text-xs font-bold text-slate-600">Đã hoàn thành</span>
               </div>
               <div className="flex items-center gap-2">
                  <div className="w-3 h-3 rounded-full bg-[#f37021]"></div>
                  <span className="text-xs font-bold text-slate-600">Được giao</span>
               </div>
            </div>
          </div>
          <div className="h-[300px] w-full relative">
            <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
              <AreaChart data={DEPT_TASKS_OVER_TIME} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                <defs>
                  <linearGradient id="colorCompleted" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#004c91" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#004c91" stopOpacity={0}/>
                  </linearGradient>
                  <linearGradient id="colorAssigned" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#f37021" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#f37021" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                <XAxis 
                  dataKey="name" 
                  axisLine={false} 
                  tickLine={false} 
                  tick={{ fill: '#64748b', fontSize: 12, fontWeight: 600 }}
                  dy={10}
                />
                <YAxis 
                  axisLine={false} 
                  tickLine={false} 
                  tick={{ fill: '#64748b', fontSize: 12, fontWeight: 600 }}
                  dx={-10}
                />
                <Tooltip 
                  contentStyle={{ borderRadius: '12px', border: 'none', boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1)' }}
                  labelStyle={{ fontWeight: 'bold', color: '#1e293b', marginBottom: '8px' }}
                />
                <Area yAxisId="left" type="monotone" dataKey="completed" name="Hoàn thành" stroke="#004c91" strokeWidth={3} fillOpacity={1} fill="url(#colorCompleted)" />
                <Area yAxisId="right" type="monotone" dataKey="assigned" name="Được giao" stroke="#f37021" strokeWidth={3} fillOpacity={1} fill="url(#colorAssigned)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Donut Chart */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col">
          <h3 className="text-lg font-bold text-slate-800">Phân bố mảng việc</h3>
          <p className="text-sm text-slate-500 font-medium mt-1 mb-6">Tỷ lệ các loại nhiệm vụ phòng ban đảm nhận</p>
          
          <div className="flex-1 flex flex-col items-center justify-center min-h-[250px] w-full relative">
            <ResponsiveContainer width="100%" height={250} minWidth={1} minHeight={1}>
              <PieChart>
                <Pie
                  data={DEPT_TASKS_BY_TYPE}
                  cx="50%"
                  cy="50%"
                  innerRadius={60}
                  outerRadius={100}
                  paddingAngle={5}
                  dataKey="value"
                  stroke="none"
                >
                  {DEPT_TASKS_BY_TYPE.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip 
                  contentStyle={{ borderRadius: '12px', border: 'none', boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)' }}
                  itemStyle={{ fontWeight: 'bold' }}
                />
              </PieChart>
            </ResponsiveContainer>

            <div className="grid grid-cols-2 gap-4 w-full mt-4">
               {DEPT_TASKS_BY_TYPE.map((item, index) => (
                  <div key={index} className="flex items-center gap-2">
                     <div className="w-3 h-3 rounded-full shrink-0" style={{ backgroundColor: item.color }}></div>
                     <span className="text-[11px] font-bold text-slate-600 uppercase tracking-widest leading-tight">{item.name}</span>
                  </div>
               ))}
            </div>
          </div>
        </div>
      </div>

      {/* Top tables */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
         <div className="p-6 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
            <div>
               <h3 className="text-lg font-bold text-slate-800">Cán bộ xuất sắc</h3>
               <p className="text-sm text-slate-500 font-medium mt-1">Danh sách cán bộ có hiệu suất công việc cao nhất</p>
            </div>
            <button className="flex items-center gap-2 text-sm font-bold text-[#004c91] hover:text-[#00386b] transition-colors cursor-pointer bg-white px-4 py-2 border border-slate-200 rounded-xl shadow-sm hover:bg-slate-50">
               <Users className="w-4 h-4" />
               Xem tất cả
            </button>
         </div>
         <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[700px]">
               <thead>
                  <tr className="bg-white border-b border-slate-100">
                     <th className="p-4 text-xs font-bold text-slate-400 uppercase tracking-wider text-center w-16">Xếp hạng</th>
                     <th className="p-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Cán bộ</th>
                     <th className="p-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Vai trò</th>
                     <th className="p-4 text-xs font-bold text-slate-400 uppercase tracking-wider text-right">Số việc hoàn thành</th>
                     <th className="p-4 text-xs font-bold text-slate-400 uppercase tracking-wider text-right">Số giờ tham gia</th>
                  </tr>
               </thead>
               <tbody className="divide-y divide-slate-100">
                  {TOP_PERFORMING_MEMBERS.map((member, index) => (
                     <tr key={member.id} className="hover:bg-blue-50/50 transition-colors group">
                        <td className="p-4 text-center">
                           <div className={`w-8 h-8 rounded-full flex items-center justify-center font-black mx-auto text-sm ${index === 0 ? 'bg-yellow-100 text-yellow-700' : index === 1 ? 'bg-slate-200 text-slate-600' : index === 2 ? 'bg-orange-100 text-orange-700' : 'bg-slate-50 text-slate-400'}`}>
                              #{index + 1}
                           </div>
                        </td>
                        <td className="p-4">
                           <div className="flex items-center gap-3">
                              <img src={member.avatar} alt={member.name} className="w-10 h-10 rounded-full border border-slate-200 shadow-sm" />
                              <span className="font-bold text-slate-800">{member.name}</span>
                           </div>
                        </td>
                        <td className="p-4 text-slate-600 font-medium">
                           {member.role}
                        </td>
                        <td className="p-4 text-right">
                           <span className="font-bold text-slate-800">{member.tasksCompleted}</span>
                        </td>
                        <td className="p-4 text-right">
                           <div className="flex justify-end items-center gap-1.5">
                              <Clock className="w-4 h-4 text-slate-400" />
                              <span className="font-bold text-[#004c91]">{member.hoursSpent}h</span>
                           </div>
                        </td>
                     </tr>
                  ))}
               </tbody>
            </table>
         </div>
      </div>
    </div>
  );
}
