/**
 * Component HODashboardView
 * Màn hình Dashboard vĩ mô của quản lý đa cơ sở Head Office đánh giá chuyến thăm.
 */

import React, { useState } from 'react';
import { 
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer,
  LineChart, Line, PieChart, Pie, Cell
} from 'recharts';
import { Download, Users, UsersRound, Contact, CheckCircle, Eye, X, ArrowRight, Check, XCircle } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';

const campusVisitsData = [
  { year: '2022', HN: 40, HCM: 30, DN: 20, CT: 10, QN: 5 },
  { year: '2023', HN: 55, HCM: 45, DN: 30, CT: 15, QN: 10 },
  { year: '2024', HN: 70, HCM: 60, DN: 40, CT: 20, QN: 15 },
  { year: '2025', HN: 90, HCM: 75, DN: 50, CT: 30, QN: 20 },
  { year: '2026', HN: 60, HCM: 50, DN: 35, CT: 20, QN: 15 },
];

const guestTypesData = [
  { name: 'Doanh nghiệp', value: 45, color: '#004c91' },
  { name: 'Đại học Quốc tế', value: 40, color: '#f37021' },
  { name: 'Chính phủ / Tổ chức', value: 15, color: '#00a651' },
];

const topNationsData = [
  { name: 'Nhật Bản', count: 42 },
  { name: 'Hàn Quốc', count: 35 },
  { name: 'Mỹ', count: 28 },
  { name: 'Úc', count: 22 },
  { name: 'Anh', count: 18 },
  { name: 'Đài Loan', count: 12 },
  { name: 'Singapore', count: 10 },
];

const feedbackData = [
  { month: 'T1', HN: 4.5, HCM: 4.2, DN: 4.8, CT: 4.0, QN: 4.1 },
  { month: 'T2', HN: 4.6, HCM: 4.1, DN: 4.7, CT: 4.2, QN: 4.0 },
  { month: 'T3', HN: 4.7, HCM: 4.4, DN: 4.9, CT: 4.1, QN: 4.2 },
  { month: 'T4', HN: 4.5, HCM: 4.5, DN: 4.8, CT: 4.0, QN: 4.4 },
  { month: 'T5', HN: 4.8, HCM: 4.6, DN: 4.9, CT: 4.3, QN: 4.5 },
  { month: 'T6', HN: 4.9, HCM: 4.7, DN: 4.8, CT: 4.5, QN: 4.6 },
];

const crossCampusVisits = [
  { id: 'VIS-2026-089', name: 'Đoàn ĐH Tokyo Metropolitan', date: '15/06/2026', route: 'Hà Nội ➔ Đà Nẵng', status: 'Cần xử lý' },
  { id: 'VIS-2026-085', name: 'Tập đoàn Samsung HQ', date: '20/06/2026', route: 'TP.HCM ➔ Cần Thơ', status: 'Đã xác nhận' },
  { id: 'VIS-2026-082', name: 'Đoàn khối Giáo dục Tây Úc', date: '25/06/2026', route: 'Quy Nhơn ➔ Đà Nẵng', status: 'Chờ duyệt' },
];

export function HODashboardView() {
  const [timeFilter, setTimeFilter] = useState('year');
  const [selectedVisit, setSelectedVisit] = useState<any>(null);

  // Custom tooltips
  const CustomBarTooltip = ({ active, payload, label }: any) => {
    if (active && payload && payload.length) {
      return (
        <div className="bg-white p-3 border border-slate-200 shadow-md rounded-xl">
          <p className="font-bold text-slate-800 mb-2">{label}</p>
          {payload.map((entry: any, index: number) => (
            <div key={index} className="flex items-center gap-2 text-sm font-medium mb-1">
              <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: entry.color }}></div>
              <span className="text-slate-600 w-16">{entry.name}:</span>
              <span className="text-slate-900 font-bold">{entry.value}</span>
            </div>
          ))}
          <div className="mt-2 pt-2 border-t border-slate-100 flex items-center justify-between font-bold">
            <span className="text-slate-600">Tổng cộng:</span>
            <span className="text-[#004c91]">{payload.reduce((sum: number, entry: any) => sum + entry.value, 0)}</span>
          </div>
        </div>
      );
    }
    return null;
  };

  const CustomLineTooltip = ({ active, payload, label }: any) => {
    if (active && payload && payload.length) {
      return (
        <div className="bg-white p-3 border border-slate-200 shadow-md rounded-xl">
          <p className="font-bold text-slate-800 mb-2">{label}</p>
          {payload.map((entry: any, index: number) => (
            <div key={index} className="flex items-center gap-2 text-sm font-medium mb-1">
              <div className="w-3 h-3 rounded-full" style={{ backgroundColor: entry.color }}></div>
              <span className="text-slate-600 w-12">{entry.name}:</span>
              <span className="text-slate-900 font-bold">{entry.value} ★</span>
            </div>
          ))}
        </div>
      );
    }
    return null;
  };

  return (
    <div className="space-y-8 animate-in fade-in duration-500 pb-12 pt-4">
      {/* Phần 1: Thanh điều khiển trên cùng */}
      <div className="flex flex-col md:flex-row items-center justify-between gap-4 bg-white p-6 rounded-2xl border border-slate-200 shadow-[0_2px_10px_rgba(0,0,0,0.04)]">
        <div>
          <h2 className="text-2xl font-black text-[#004c91] tracking-tight uppercase">BÁO CÁO THỐNG KÊ TOÀN QUỐC</h2>
          <p className="text-sm font-medium text-slate-500 mt-1">Tổng hợp dữ liệu đón tiếp khách tại tất cả cơ sở FPT University</p>
        </div>
        
        <div className="flex flex-wrap items-center gap-3 w-full md:w-auto">
          <select 
            value={timeFilter}
            onChange={(e) => setTimeFilter(e.target.value)}
            className="flex-1 md:w-56 bg-slate-50 border border-slate-200 text-slate-700 text-sm rounded-xl px-4 py-3 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 font-bold cursor-pointer transition-all"
          >
            <option value="year">Năm nay (2026)</option>
            <option value="q1">Quý 1</option>
            <option value="month">Tháng này</option>
            <option value="custom">Tùy chọn nâng cao...</option>
          </select>
          
          <button className="flex flex-1 md:flex-none items-center justify-center gap-2 bg-[#00a651] hover:bg-[#008f45] text-white px-6 py-3 rounded-xl font-bold transition-all shadow-[0_4px_12px_rgba(0,166,81,0.25)] hover:shadow-[0_6px_16px_rgba(0,166,81,0.35)] hover:-translate-y-0.5 active:translate-y-0 cursor-pointer">
            <Download className="w-5 h-5" />
            <span>Xuất Báo Cáo Excel</span>
          </button>
        </div>
      </div>

      {/* Phần 2: Hàng số liệu nhanh (4 Thẻ KPI) */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm relative overflow-hidden group hover:border-blue-300 transition-colors">
           <div className="flex justify-between items-start mb-4">
               <div>
                   <p className="text-sm font-bold text-slate-500 mb-1">Tổng đoàn khách</p>
                   <h4 className="text-4xl font-black text-slate-800">145</h4>
               </div>
               <div className="w-12 h-12 bg-blue-50 text-[#004c91] rounded-xl flex items-center justify-center">
                   <Users className="w-6 h-6" />
               </div>
           </div>
           <p className="text-xs font-semibold text-slate-400 mt-2">Đoàn khách toàn quốc năm nay</p>
        </div>

        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm relative overflow-hidden group hover:border-[#f37021]/50 transition-colors">
           <div className="flex justify-between items-start mb-4">
               <div>
                   <p className="text-sm font-bold text-slate-500 mb-1">Tổng lượt khách</p>
                   <h4 className="text-4xl font-black text-slate-800">1,240</h4>
               </div>
               <div className="w-12 h-12 bg-orange-50 text-[#f37021] rounded-xl flex items-center justify-center">
                    <UsersRound className="w-6 h-6" />
                </div>
            </div>
            <p className="text-xs font-semibold text-slate-400 mt-2">Lượt người thực tế đã đến trường</p>
         </div>

         <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm relative overflow-hidden group hover:border-purple-300 transition-colors">
           <div className="flex justify-between items-start mb-4">
               <div>
                   <p className="text-sm font-bold text-slate-500 mb-1">Hồ sơ Đối tác mới</p>
                   <h4 className="text-4xl font-black text-slate-800">38</h4>
               </div>
               <div className="w-12 h-12 bg-purple-50 text-purple-600 rounded-xl flex items-center justify-center">
                   <Contact className="w-6 h-6" />
               </div>
           </div>
           <p className="text-xs font-semibold text-slate-400 mt-2">Đối tác quét từ danh thiếp/sự kiện</p>
        </div>
        
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm relative overflow-hidden group hover:border-emerald-300 transition-colors">
           <div className="flex justify-between items-start mb-4">
               <div>
                   <p className="text-sm font-bold text-slate-500 mb-1">Tỷ lệ hoàn thành SLA</p>
                   <h4 className="text-4xl font-black text-slate-800">94.2<span className="text-2xl">%</span></h4>
               </div>
               <div className="w-12 h-12 bg-emerald-50 text-emerald-600 rounded-xl flex items-center justify-center">
                   <CheckCircle className="w-6 h-6" />
               </div>
           </div>
           <p className="text-xs font-semibold text-slate-400 mt-2">Đủ Biên bản, Ảnh, Tin tức đúng hạn</p>
        </div>
      </div>

      {/* Phần 3: Vùng biểu đồ phân tích (Lưới 2x2) */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        
        {/* Biểu đồ 1: Cột chồng - Số lượng đoàn theo cơ sở */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col h-full">
          <div className="mb-6">
            <h3 className="text-lg font-bold text-slate-800">Số lượng đoàn khách theo năm</h3>
            <p className="text-sm text-slate-500 font-medium">Theo dõi tăng trưởng giữa các cơ sở</p>
          </div>
          <div className="h-[300px] w-full mt-auto relative">
            <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
              <BarChart data={campusVisitsData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E2E8F0" />
                <XAxis dataKey="year" axisLine={false} tickLine={false} tick={{ fontSize: 13, fill: '#64748B', fontWeight: 600 }} dy={10} />
                <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 13, fill: '#64748B', fontWeight: 600 }} />
                <Tooltip content={<CustomBarTooltip />} cursor={{ fill: '#F1F5F9' }} />
                <Legend iconType="circle" wrapperStyle={{ paddingTop: '20px', fontSize: '13px', fontWeight: 600 }} />
                <Bar dataKey="HN" name="Hà Nội" stackId="a" fill="#004c91" radius={[0, 0, 4, 4]} maxBarSize={45} />
                <Bar dataKey="HCM" name="TP.HCM" stackId="a" fill="#f37021" maxBarSize={45} />
                <Bar dataKey="DN" name="Đà Nẵng" stackId="a" fill="#10B981" maxBarSize={45} />
                <Bar dataKey="CT" name="Cần Thơ" stackId="a" fill="#8B5CF6" maxBarSize={45} />
                <Bar dataKey="QN" name="Quy Nhơn" stackId="a" fill="#F59E0B" radius={[4, 4, 0, 0]} maxBarSize={45} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Biểu đồ 2: Donut - Cơ cấu loại hình khách */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col h-full">
          <div className="mb-6">
            <h3 className="text-lg font-bold text-slate-800">Cơ cấu loại hình đoàn khách</h3>
            <p className="text-sm text-slate-500 font-medium">Phân bổ tỷ trọng hợp tác hiện tại</p>
          </div>
          <div className="h-[320px] w-full mt-auto flex flex-col items-center">
            <div className="w-full h-[200px] relative">
              <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
                <PieChart>
                  <Pie
                    data={guestTypesData}
                    cx="50%"
                    cy="50%"
                    innerRadius={55}
                    outerRadius={85}
                    paddingAngle={5}
                    dataKey="value"
                    stroke="none"
                  >
                    {guestTypesData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip 
                    formatter={(value: number) => [`${value}%`, 'Tỷ trọng']}
                    contentStyle={{ borderRadius: '12px', border: '1px solid #e2e8f0', boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)', padding: '12px', fontWeight: 'bold' }}
                  />
                </PieChart>
              </ResponsiveContainer>
            </div>
            {/* Custom Legend */}
            <div className="w-full mt-2 grid grid-cols-2 gap-3 px-2">
                {guestTypesData.map((item, idx) => (
                    <div key={idx} className="flex items-start gap-2.5">
                        <div className="w-3.5 h-3.5 rounded-full shrink-0 mt-0.5" style={{ backgroundColor: item.color }}></div>
                        <div>
                            <p className="text-sm font-bold text-slate-800 leading-tight">{item.name}</p>
                            <p className="text-xs font-semibold text-slate-500 mt-0.5">{item.value}%</p>
                        </div>
                    </div>
                ))}
            </div>
          </div>
        </div>

        {/* Biểu đồ 3: Bar ngang - Top 10 Quốc gia */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col h-full">
          <div className="mb-6">
            <h3 className="text-lg font-bold text-slate-800">Top Quốc gia Đối tác</h3>
            <p className="text-sm text-slate-500 font-medium">Các luồng khách quốc tế nhiều nhất</p>
          </div>
          <div className="h-[300px] w-full mt-auto relative">
            <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
              <BarChart layout="vertical" data={topNationsData} margin={{ top: 10, right: 30, left: 10, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" horizontal={true} vertical={false} stroke="#E2E8F0" />
                <XAxis type="number" axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: '#64748B' }} />
                <YAxis dataKey="name" type="category" axisLine={false} tickLine={false} tick={{ fontSize: 13, fill: '#0F172A', fontWeight: 600 }} width={80} />
                <Tooltip 
                  cursor={{ fill: '#F1F5F9' }}
                  contentStyle={{ borderRadius: '12px', border: 'none', boxShadow: '0 4px 10px rgb(0 0 0 / 0.1)' }}
                  itemStyle={{ fontWeight: 'bold', color: '#004c91' }}
                  labelStyle={{ fontWeight: 'bold', color: '#64748b', display: 'none' }}
                />
                <Bar dataKey="count" fill="#4B8DDA" radius={[0, 4, 4, 0]} barSize={20} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
        
        {/* Biểu đồ 4: Đường xu hướng - Phối hợp giữa các Campus */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col h-full">
          <div className="mb-6">
            <h3 className="text-lg font-bold text-slate-800">Điểm đánh giá phối hợp chất lượng</h3>
            <p className="text-sm text-slate-500 font-medium">Chấm điểm xử lý vận hành (Thang 1-5 sao)</p>
          </div>
          <div className="h-[300px] w-full mt-auto relative">
            <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
              <LineChart data={feedbackData} margin={{ top: 10, right: 10, left: -25, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E2E8F0" />
                <XAxis dataKey="month" axisLine={false} tickLine={false} tick={{ fontSize: 13, fill: '#64748B', fontWeight: 600 }} dy={10} />
                <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 13, fill: '#64748B', fontWeight: 600 }} domain={[3.5, 5.0]} ticks={[3.5, 4.0, 4.5, 5.0]} />
                <Tooltip content={<CustomLineTooltip />} />
                <Legend iconType="circle" wrapperStyle={{ paddingTop: '20px', fontSize: '13px', fontWeight: 600 }} />
                <Line type="monotone" dataKey="HN" name="Hà Nội" stroke="#004c91" strokeWidth={3} dot={{ r: 4 }} activeDot={{ r: 6 }} />
                <Line type="monotone" dataKey="HCM" name="TP.HCM" stroke="#f37021" strokeWidth={3} dot={{ r: 4 }} activeDot={{ r: 6 }} />
                <Line type="monotone" dataKey="DN" name="Đà Nẵng" stroke="#10B981" strokeWidth={3} dot={{ r: 4 }} activeDot={{ r: 6 }} />
                <Line type="monotone" dataKey="CT" name="Cần Thơ" stroke="#8B5CF6" strokeWidth={3} dot={{ r: 4 }} activeDot={{ r: 6 }} />
                <Line type="monotone" dataKey="QN" name="Quy Nhơn" stroke="#F59E0B" strokeWidth={3} dot={{ r: 4 }} activeDot={{ r: 6 }} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

    </div>
  );
}
