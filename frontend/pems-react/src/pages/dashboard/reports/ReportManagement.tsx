/**
 * Trang ReportManagement
 * Quản lý hệ thống chiến dịch, phân tích lượng đánh giá và lượt báo cáo.
 */

import React, { useState } from 'react';
import { Download, Calendar, Filter, Users, TrendingUp, Star, CalendarDays, Eye, FileText } from 'lucide-react';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend } from 'recharts';
import { REPORT_STATISTICS, VISITS_OVER_TIME, VISITOR_TYPES, TOP_RATED_VISITS } from './mockReportData';
import { DeptReportManagement } from './DeptReportManagement';

export function ReportManagement() {
  const [timeFilter, setTimeFilter] = useState('this_year');
  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const isDept = user?.role?.toUpperCase() === 'DEPARTMENT';
  
  if (isDept) {
    return <DeptReportManagement />;
  }

  const currentStats = REPORT_STATISTICS;
  const currentOverTime = VISITS_OVER_TIME;
  const currentVisitorTypes = VISITOR_TYPES;
  const currentTopRated = TOP_RATED_VISITS;

  return (
    <div className="space-y-6 animate-in fade-in duration-500 pb-12 font-sans">
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-2">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý báo cáo</span>
      </div>

      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <h2 className="text-3xl font-black text-[#004c91] tracking-tight">Thống kê & Báo cáo</h2>
          <p className="text-base font-medium text-slate-500 mt-1">Tổng quan dữ liệu các chuyến tham quan</p>
        </div>

        <div className="flex items-center gap-3 w-full md:w-auto">
          <div className="relative flex-1 md:w-48 bg-white border border-slate-200 rounded-xl flex items-center px-3 gap-2 shadow-sm">
            <Calendar className="w-4 h-4 text-slate-400" />
            <select 
              value={timeFilter}
              onChange={(e) => setTimeFilter(e.target.value)}
              className="flex-1 bg-transparent py-2.5 text-sm font-medium text-slate-700 outline-none appearance-none cursor-pointer"
            >
              <option value="this_week">Tuần này</option>
              <option value="this_month">Tháng này</option>
              <option value="last_month">Tháng trước</option>
              <option value="this_year">Năm nay</option>
              <option value="custom">Tùy chỉnh...</option>
            </select>
            <Filter className="w-4 h-4 text-slate-400 pointer-events-none" />
          </div>

          <button className="flex items-center justify-center gap-2 px-4 py-2.5 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#00386b] transition-colors shadow-sm cursor-pointer whitespace-nowrap">
            <Download className="w-4 h-4" />
            <span className="hidden sm:inline">Xuất báo cáo</span>
          </button>
        </div>
      </div>

      {/* Overview Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col justify-between group hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 rounded-xl bg-blue-50 flex items-center justify-center text-[#004c91] group-hover:scale-110 transition-transform">
              <TrendingUp className="w-6 h-6" />
            </div>
            <span className={`flex items-center gap-1 text-xs font-bold px-2 py-1 rounded-lg ${currentStats.visitsChange?.startsWith('-') ? 'text-red-600 bg-red-50' : 'text-green-600 bg-green-50'}`}>
              {currentStats.visitsChange}
            </span>
          </div>
          <div>
            <p className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-1">Tổng chuyến tham quan</p>
            <p className="text-3xl font-black text-slate-800">{currentStats.totalVisits}</p>
          </div>
        </div>

        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col justify-between group hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 rounded-xl bg-orange-50 flex items-center justify-center text-[#e85c0d] group-hover:scale-110 transition-transform">
              <Users className="w-6 h-6" />
            </div>
            <span className={`flex items-center gap-1 text-xs font-bold px-2 py-1 rounded-lg ${currentStats.visitorsChange?.startsWith('-') ? 'text-red-600 bg-red-50' : 'text-green-600 bg-green-50'}`}>
              {currentStats.visitorsChange}
            </span>
          </div>
          <div>
            <p className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-1">Tổng lượt khách</p>
            <p className="text-3xl font-black text-slate-800">{currentStats.totalVisitors?.toLocaleString()}</p>
          </div>
        </div>

        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col justify-between group hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 rounded-xl bg-purple-50 flex items-center justify-center text-purple-600 group-hover:scale-110 transition-transform">
              <CalendarDays className="w-6 h-6" />
            </div>
            <span className={`flex items-center gap-1 text-xs font-bold px-2 py-1 rounded-lg ${currentStats.upcomingChange?.startsWith('-') ? 'text-red-600 bg-red-50' : 'text-green-600 bg-green-50'}`}>
              {currentStats.upcomingChange}
            </span>
          </div>
          <div>
            <p className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-1">Chuyến sắp tới</p>
            <p className="text-3xl font-black text-slate-800">{currentStats.upcomingVisits}</p>
          </div>
        </div>

        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col justify-between group hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 rounded-xl flex items-center justify-center group-hover:scale-110 transition-transform bg-yellow-50 text-yellow-600">
              <Star className="w-6 h-6" />
            </div>
            <span className={`flex items-center gap-1 text-xs font-bold px-2 py-1 rounded-lg ${currentStats.ratingChange?.startsWith('-') ? 'text-red-600 bg-red-50' : 'text-green-600 bg-green-50'}`}>
              {currentStats.ratingChange}
            </span>
          </div>
          <div>
            <p className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-1">Điểm đánh giá TB</p>
            <div className="flex items-center gap-2">
              <p className="text-3xl font-black text-slate-800">{currentStats.averageRating}</p>
              <Star className="w-5 h-5 fill-yellow-400 text-yellow-400" />
            </div>
          </div>
        </div>
      </div>

      {/* Charts Section */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Main Chart */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm lg:col-span-2">
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center mb-6">
            <div>
              <h3 className="text-lg font-bold text-slate-800">Lượt khách tham quan</h3>
              <p className="text-sm text-slate-500 font-medium mt-1">Thống kê số lượng chuyến và lượt khách theo tháng</p>
            </div>
            <div className="flex items-center gap-4 mt-4 sm:mt-0">
               <div className="flex items-center gap-2">
                  <div className="w-3 h-3 rounded-full bg-[#004c91]"></div>
                  <span className="text-xs font-bold text-slate-600">Số lượt khách</span>
               </div>
               <div className="flex items-center gap-2">
                  <div className="w-3 h-3 rounded-full bg-[#f37021]"></div>
                  <span className="text-xs font-bold text-slate-600">Số chuyến</span>
               </div>
            </div>
          </div>
          <div className="h-[300px] w-full relative">
            <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
              <AreaChart data={currentOverTime} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                <defs>
                  <linearGradient id="colorVisitors" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#004c91" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#004c91" stopOpacity={0}/>
                  </linearGradient>
                  <linearGradient id="colorVisits" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#f37021" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#f37021" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                <XAxis dataKey="name" axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: '#64748b', fontWeight: 500 }} dy={10} />
                <YAxis yAxisId="left" axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: '#64748b', fontWeight: 600 }} />
                <YAxis yAxisId="right" orientation="right" axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: '#64748b' }} hide />
                <Tooltip 
                  contentStyle={{ borderRadius: '12px', border: 'none', boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1)' }}
                  labelStyle={{ fontWeight: 'bold', color: '#1e293b', marginBottom: '8px' }}
                />
                <Area yAxisId="left" type="monotone" dataKey="visitors" name="Lượt khách" stroke="#004c91" strokeWidth={3} fillOpacity={1} fill="url(#colorVisitors)" />
                <Area yAxisId="right" type="monotone" dataKey="visits" name="Số chuyến" stroke="#f37021" strokeWidth={3} fillOpacity={1} fill="url(#colorVisits)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Donut Chart */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col">
          <h3 className="text-lg font-bold text-slate-800">Phân bố loại đoàn khách</h3>
          <p className="text-sm text-slate-500 font-medium mt-1 mb-6">Tỷ lệ các nhóm đối tượng tham quan</p>
          
          <div className="flex-1 flex flex-col items-center justify-center min-h-[250px] w-full relative">
            <ResponsiveContainer width="100%" height={250} minWidth={1} minHeight={1}>
              <PieChart>
                <Pie
                  data={currentVisitorTypes}
                  cx="50%"
                  cy="50%"
                  innerRadius={60}
                  outerRadius={80}
                  paddingAngle={5}
                  dataKey="value"
                  stroke="none"
                >
                  {currentVisitorTypes.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip 
                  formatter={(value: number) => [`${value}%`, 'Tỷ lệ']}
                  contentStyle={{ borderRadius: '12px', border: 'none', boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)' }}
                />
              </PieChart>
            </ResponsiveContainer>

            <div className="grid grid-cols-2 gap-4 w-full mt-4">
               {currentVisitorTypes.map((item, index) => (
                  <div key={index} className="flex items-center gap-2">
                     <div className="w-3 h-3 rounded-full shrink-0" style={{ backgroundColor: item.color }}></div>
                     <span className="text-[11px] font-bold text-slate-600 uppercase tracking-widest leading-tight">{item.name}</span>
                  </div>
               ))}
            </div>
          </div>
        </div>
      </div>

      {/* Top Rated List */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
         <div className="p-6 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
            <div>
               <h3 className="text-lg font-bold text-slate-800">Các đoàn khách đánh giá cao nhất</h3>
               <p className="text-sm text-slate-500 font-medium mt-1">Danh sách đánh giá xuất sắc gần đây</p>
            </div>
            <button className="flex items-center gap-2 text-sm font-bold text-[#004c91] hover:text-[#00386b] transition-colors cursor-pointer bg-white px-4 py-2 border border-slate-200 rounded-xl shadow-sm hover:bg-slate-50">
               <FileText className="w-4 h-4" />
               Xem chi tiết
            </button>
         </div>
         <div className="max-w-full overflow-x-auto">
            <table className="w-full text-left border-collapse">
               <thead>
                  <tr className="bg-[#004c91] text-white">
                     <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center">Xếp hạng</th>
                     <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Tên đoàn khách</th>
                     <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Loại hình</th>
                     <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Số lượng</th>
                     <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Đánh giá</th>
                     <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Ngày thăm</th>
                  </tr>
               </thead>
               <tbody className="divide-y divide-slate-100">
                  {currentTopRated.map((visit, index) => (
                     <tr key={visit.id} className="hover:bg-blue-50/50 transition-colors group">
                        <td className="p-4 text-center">
                           <div className={`w-8 h-8 rounded-full flex items-center justify-center font-black mx-auto text-sm ${index === 0 ? 'bg-yellow-100 text-yellow-700' : index === 1 ? 'bg-slate-200 text-slate-600' : index === 2 ? 'bg-orange-100 text-orange-700' : 'bg-slate-50 text-slate-400'}`}>
                              {index + 1}
                           </div>
                        </td>
                        <td className="p-4">
                           <p className="text-sm font-bold text-slate-900 group-hover:text-[#004c91] transition-colors cursor-pointer">
                              {visit.name}
                           </p>
                        </td>
                        <td className="p-4 text-sm font-medium text-slate-600 whitespace-nowrap">
                           {visit.type}
                        </td>
                        <td className="p-4 text-sm font-medium text-slate-600 text-center whitespace-nowrap">
                           {visit.visitors}
                        </td>
                        <td className="p-4 text-center">
                           <div className="flex items-center justify-center gap-1 bg-yellow-50 px-2 py-1 rounded-lg w-fit mx-auto border border-yellow-100">
                              <span className="font-bold text-yellow-700">{visit.rating.toFixed(1)}</span>
                              <Star className="w-4 h-4 fill-yellow-400 text-yellow-400 -mt-0.5" />
                           </div>
                        </td>
                        <td className="p-4 text-sm font-medium text-slate-500 text-center whitespace-nowrap">
                           {visit.date}
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
