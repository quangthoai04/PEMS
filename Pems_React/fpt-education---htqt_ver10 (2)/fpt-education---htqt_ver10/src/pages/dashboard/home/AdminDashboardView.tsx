/**
 * Component AdminDashboardView
 * Giao diện tổng quan chính dành riêng cho Admin.
 * Toàn quyền phân tích log cấp độ nguyên bản hệ thống.
 */

import React from 'react';
import { Users, Server, ShieldCheck, Activity, Database, Cpu, Clock } from 'lucide-react';
import { 
  LineChart, Line, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, AreaChart, Area
} from 'recharts';

const systemActivityData = [
  { time: '08:00', requests: 120, errors: 2 },
  { time: '10:00', requests: 450, errors: 5 },
  { time: '12:00', requests: 380, errors: 3 },
  { time: '14:00', requests: 520, errors: 8 },
  { time: '16:00', requests: 480, errors: 4 },
  { time: '18:00', requests: 240, errors: 1 },
  { time: '20:00', requests: 150, errors: 0 },
];

const activeUsersData = [
  { day: 'T2', users: 1200 },
  { day: 'T3', users: 1400 },
  { day: 'T4', users: 1350 },
  { day: 'T5', users: 1600 },
  { day: 'T6', users: 1550 },
  { day: 'T7', users: 800 },
  { day: 'CN', users: 650 },
];

export function AdminDashboardView() {
  return (
    <div className="space-y-6 animate-in fade-in duration-500 pt-4">
      {/* Metrics Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Total Users */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm flex items-center justify-between group hover:border-[#004c91]/50 transition-colors">
          <div>
            <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">Tổng người dùng</p>
            <h4 className="text-3xl font-black text-slate-800">4,821</h4>
            <p className="text-xs font-semibold text-emerald-500 mt-1 flex items-center gap-1">
              <Activity className="w-3 h-3" /> +12% so với tháng trước
            </p>
          </div>
          <div className="w-12 h-12 bg-blue-50 text-[#004c91] rounded-xl flex items-center justify-center shrink-0">
            <Users className="w-6 h-6" />
          </div>
        </div>

        {/* Server Uptime */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm flex items-center justify-between group hover:border-emerald-500/50 transition-colors">
          <div>
             <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">Uptime hệ thống</p>
             <h4 className="text-3xl font-black text-slate-800">99.98%</h4>
             <p className="text-xs font-semibold text-emerald-500 mt-1 flex items-center gap-1">
               <ShieldCheck className="w-3 h-3" /> Hoạt động ổn định
             </p>
          </div>
          <div className="w-12 h-12 bg-emerald-50 text-emerald-600 rounded-xl flex items-center justify-center shrink-0">
             <Server className="w-6 h-6" />
          </div>
        </div>

        {/* Database Load */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm flex items-center justify-between group hover:border-[#f37021]/50 transition-colors">
          <div>
             <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">Tải CSDL</p>
             <h4 className="text-3xl font-black text-slate-800">42%</h4>
             <p className="text-xs font-semibold text-slate-400 mt-1 flex items-center gap-1">
               <Database className="w-3 h-3" /> 2.4 TB Đang sử dụng
             </p>
          </div>
          <div className="w-12 h-12 bg-orange-50 text-[#f37021] rounded-xl flex items-center justify-center shrink-0">
             <Cpu className="w-6 h-6" />
          </div>
        </div>

        {/* Sync Status */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm flex items-center justify-between group hover:border-purple-500/50 transition-colors">
          <div>
            <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">Đồng bộ dữ liệu</p>
            <h4 className="text-3xl font-black text-slate-800">100%</h4>
            <p className="text-xs font-semibold text-slate-400 mt-1 flex items-center gap-1">
              <Clock className="w-3 h-3" /> Cập nhật 5 phút trước
            </p>
          </div>
          <div className="w-12 h-12 bg-purple-50 text-purple-600 rounded-xl flex items-center justify-center shrink-0">
             <Activity className="w-6 h-6" />
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* System Activity Chart */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col h-[350px]">
          <div className="mb-4">
            <h3 className="text-lg font-bold text-slate-800">Lưu lượng truy cập hệ thống (Hôm nay)</h3>
            <p className="text-xs font-semibold text-slate-500 mt-1">Số lượng requests và lỗi ghi nhận theo giờ</p>
          </div>
          <div className="flex-1 w-full min-h-0">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={systemActivityData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                <defs>
                  <linearGradient id="colorRequests" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#004c91" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#004c91" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E2E8F0" />
                <XAxis dataKey="time" axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: '#64748B', fontWeight: 600 }} dy={10} />
                <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: '#64748B', fontWeight: 600 }} />
                <Tooltip 
                  contentStyle={{ borderRadius: '12px', border: '1px solid #e2e8f0', boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)' }}
                />
                <Area type="monotone" dataKey="requests" stroke="#004c91" strokeWidth={3} fillOpacity={1} fill="url(#colorRequests)" name="Requests" />
                <Area type="monotone" dataKey="errors" stroke="#EF4444" strokeWidth={2} fillOpacity={0} name="Lỗi hệ thống" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Active Users Weekly */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col h-[350px]">
          <div className="mb-4">
            <h3 className="text-lg font-bold text-slate-800">Người dùng truy cập (Tuần này)</h3>
            <p className="text-xs font-semibold text-slate-500 mt-1">Biến động user active qua các ngày trong tuần</p>
          </div>
          <div className="flex-1 w-full min-h-0">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={activeUsersData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }} barSize={32}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E2E8F0" />
                <XAxis dataKey="day" axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: '#64748B', fontWeight: 600 }} dy={10} />
                <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: '#64748B', fontWeight: 600 }} />
                <Tooltip 
                  cursor={{ fill: '#F1F5F9' }}
                  contentStyle={{ borderRadius: '12px', border: 'none', boxShadow: '0 4px 10px rgb(0 0 0 / 0.1)' }}
                />
                <Bar dataKey="users" name="Active Users" fill="#f37021" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

      {/* System Logs / Audit Trail */}
      <div className="bg-white border border-slate-200 shadow-sm rounded-2xl overflow-hidden mt-6">
        <div className="px-6 py-5 border-b border-slate-100 flex items-center justify-between">
            <div>
              <h3 className="font-bold text-slate-800">Nhật ký hoạt động hệ thống</h3>
              <p className="text-xs text-slate-500 font-medium mt-1">Ghi nhận các thao tác quan trọng gần đây</p>
            </div>
            <button className="text-xs font-bold text-[#004c91] hover:underline px-3 py-1.5 bg-blue-50 rounded-lg">Xem tất cả</button>
        </div>
        <div className="divide-y divide-slate-100">
            <div className="p-4 px-6 flex items-start gap-4 hover:bg-slate-50 transition-colors">
                <div className="w-8 h-8 rounded-full bg-emerald-100 text-emerald-600 flex items-center justify-center shrink-0">
                    <ShieldCheck className="w-4 h-4" />
                </div>
                <div>
                    <p className="text-sm font-semibold text-slate-800"><span className="font-bold text-[#004c91]">System Admin</span> đã cập nhật cấu hình bảo mật SSO</p>
                    <p className="text-xs text-slate-400 mt-1">10 phút trước • IP: 192.168.1.100</p>
                </div>
            </div>
            <div className="p-4 px-6 flex items-start gap-4 hover:bg-slate-50 transition-colors">
                <div className="w-8 h-8 rounded-full bg-blue-100 text-blue-600 flex items-center justify-center shrink-0">
                    <Users className="w-4 h-4" />
                </div>
                <div>
                    <p className="text-sm font-semibold text-slate-800"><span className="font-bold text-[#f37021]">Auto Sync</span> đã đồng bộ 45 tài khoản sinh viên mới</p>
                    <p className="text-xs text-slate-400 mt-1">45 phút trước • Origin: FAP System</p>
                </div>
            </div>
            <div className="p-4 px-6 flex items-start gap-4 hover:bg-slate-50 transition-colors">
                <div className="w-8 h-8 rounded-full bg-red-100 text-red-600 flex items-center justify-center shrink-0">
                    <Activity className="w-4 h-4" />
                </div>
                <div>
                    <p className="text-sm font-semibold text-slate-800">Phát hiện cảnh báo tải trọng máy chủ vượt 80% (Spike traffic)</p>
                    <p className="text-xs text-slate-400 mt-1">2 giờ trước • Node: worker-02</p>
                </div>
            </div>
        </div>
      </div>
    </div>
  );
}
