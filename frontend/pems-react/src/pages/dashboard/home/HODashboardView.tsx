import React, { useState, useEffect } from 'react';
import { 
  FileText, Calendar, Building2, Users, Newspaper, PieChart, 
  AlertTriangle, AlertCircle, MessageSquare, ChevronRight,
  Bell, FileCheck, Loader2
} from 'lucide-react';
import { Link } from 'react-router-dom';
import httpClient from '../../../shared/api/httpClient';

export function HODashboardView() {
  const [data, setData] = useState<any>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchOverview = async () => {
      try {
        const response = await httpClient.get('/dashboard/ho-overview');
        setData(response.data);
      } catch (error) {
        console.error('Failed to fetch HO dashboard overview:', error);
      } finally {
        setLoading(false);
      }
    };
    fetchOverview();
  }, []);

  if (loading || !data) {
    return (
      <div className="flex items-center justify-center h-64 text-[#004c91]">
        <Loader2 className="w-8 h-8 animate-spin" />
      </div>
    );
  }

  const {
    kpis,
    actionItems,
    pendingRequests,
    upcomingVisits,
    campusStatus,
    recentActivities
  } = data;

  return (
    <div className="space-y-6 animate-in fade-in duration-500 pb-12 pt-4">
      {/* 2. Quick action bar */}
      <div className="flex flex-wrap gap-3">
        <Link to="/dashboard/visit" className="flex items-center gap-2 bg-white px-4 py-2.5 rounded-lg border border-slate-200 shadow-sm hover:border-[#004c91] hover:text-[#004c91] transition-colors text-sm font-bold text-slate-700">
          <FileCheck className="w-4 h-4 text-[#004c91]" /> Duyệt đơn liên cơ sở
        </Link>
        <Link to="/dashboard/visit" className="flex items-center gap-2 bg-white px-4 py-2.5 rounded-lg border border-slate-200 shadow-sm hover:border-[#004c91] hover:text-[#004c91] transition-colors text-sm font-bold text-slate-700">
          <Calendar className="w-4 h-4 text-[#004c91]" /> Xem lịch tiếp khách
        </Link>
        <Link to="/dashboard/campus" className="flex items-center gap-2 bg-white px-4 py-2.5 rounded-lg border border-slate-200 shadow-sm hover:border-[#004c91] hover:text-[#004c91] transition-colors text-sm font-bold text-slate-700">
          <Building2 className="w-4 h-4 text-[#004c91]" /> Quản lý campus
        </Link>
        <Link to="/dashboard/accounts" className="flex items-center gap-2 bg-white px-4 py-2.5 rounded-lg border border-slate-200 shadow-sm hover:border-[#004c91] hover:text-[#004c91] transition-colors text-sm font-bold text-slate-700">
          <Users className="w-4 h-4 text-[#004c91]" /> Quản lý tài khoản
        </Link>
        <Link to="/dashboard/news" className="flex items-center gap-2 bg-white px-4 py-2.5 rounded-lg border border-slate-200 shadow-sm hover:border-[#004c91] hover:text-[#004c91] transition-colors text-sm font-bold text-slate-700">
          <Newspaper className="w-4 h-4 text-[#004c91]" /> Quản lý tin tức
        </Link>
        <Link to="/dashboard/reports" className="flex items-center gap-2 bg-white px-4 py-2.5 rounded-lg border border-slate-200 shadow-sm hover:border-[#004c91] hover:text-[#004c91] transition-colors text-sm font-bold text-slate-700">
          <PieChart className="w-4 h-4 text-[#004c91]" /> Xem báo cáo
        </Link>
      </div>

      {/* 3. KPI strip nhiệm vụ cần làm */}
      <div className="bg-white rounded-xl border border-slate-200 shadow-sm flex flex-col md:flex-row divide-y md:divide-y-0 md:divide-x divide-slate-100">
        <div className="flex-1 p-4 flex items-center justify-between">
          <div>
            <p className="text-xs font-bold text-slate-500 uppercase">Đơn liên cơ sở chờ duyệt</p>
            <p className="text-2xl font-black text-[#f37021] mt-1">{kpis?.pendingRequests || 0}</p>
          </div>
          <FileText className="w-8 h-8 text-[#f37021]/20" />
        </div>
        <div className="flex-1 p-4 flex items-center justify-between">
          <div>
            <p className="text-xs font-bold text-slate-500 uppercase">Đơn chờ quá 48h</p>
            <p className="text-2xl font-black text-red-600 mt-1">{kpis?.overdueRequests || 0}</p>
          </div>
          <AlertTriangle className="w-8 h-8 text-red-600/20" />
        </div>
        <div className="flex-1 p-4 flex items-center justify-between">
          <div>
            <p className="text-xs font-bold text-slate-500 uppercase">Sắp diễn ra (7 ngày)</p>
            <p className="text-2xl font-black text-[#004c91] mt-1">{kpis?.upcomingVisits || 0}</p>
          </div>
          <Calendar className="w-8 h-8 text-[#004c91]/20" />
        </div>
        <div className="flex-1 p-4 flex items-center justify-between">
          <div>
            <p className="text-xs font-bold text-slate-500 uppercase">Feedback thấp cần xem</p>
            <p className="text-2xl font-black text-amber-500 mt-1">{kpis?.lowFeedback || 0}</p>
          </div>
          <MessageSquare className="w-8 h-8 text-amber-500/20" />
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Cột trái rộng hơn (2/3) */}
        <div className="lg:col-span-2 space-y-6">

          {/* 4. Khối “Cần HO xử lý” */}
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm">
            <div className="px-5 py-4 border-b border-slate-100 flex items-center justify-between">
              <h3 className="font-bold text-slate-800 flex items-center gap-2">
                <AlertCircle className="w-5 h-5 text-[#f37021]" /> Cần HO xử lý
              </h3>
            </div>
            <div className="p-0">
              {actionItems?.length > 0 ? (
                <div className="divide-y divide-slate-100">
                  {actionItems.map((item: any, idx: number) => (
                    <div key={idx} className="p-4 flex items-center justify-between hover:bg-slate-50 transition-colors">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 rounded-full bg-orange-50 flex items-center justify-center shrink-0">
                          <AlertTriangle className="w-4 h-4 text-[#f37021]" />
                        </div>
                        <div>
                          <p className="text-sm font-bold text-slate-800">{item.title}</p>
                          <p className="text-xs text-slate-500 mt-0.5">{item.desc}</p>
                        </div>
                      </div>
                      <Link 
                        to={item.title.includes('Feedback') ? "/dashboard/feedback" : "/dashboard/visit"} 
                        className="text-sm font-bold text-[#004c91] hover:underline px-3 py-1.5 bg-blue-50 rounded-md"
                      >
                        Xem
                      </Link>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="h-24 flex items-center justify-center text-sm font-medium text-slate-500">
                  Không có công việc nào cần xử lý.
                </div>
              )}
            </div>
          </div>

          {/* 6. Đơn liên cơ sở chờ duyệt */}
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm">
            <div className="px-5 py-4 border-b border-slate-100 flex items-center justify-between">
              <h3 className="font-bold text-slate-800 flex items-center gap-2">
                <FileCheck className="w-5 h-5 text-[#004c91]" /> Đơn liên cơ sở chờ duyệt
              </h3>
            </div>
            <div>
              {pendingRequests?.length > 0 ? (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-sm">
                    <thead className="bg-slate-50 border-b border-slate-100 text-slate-500 uppercase text-xs">
                      <tr>
                        <th className="px-5 py-3 font-bold">Mã đơn</th>
                        <th className="px-5 py-3 font-bold">Tên đoàn</th>
                        <th className="px-5 py-3 font-bold">Campus ĐK</th>
                        <th className="px-5 py-3 font-bold text-right">Hành động</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {pendingRequests.map((req: any, idx: number) => (
                        <tr key={idx} className="hover:bg-slate-50">
                          <td className="px-5 py-3 font-semibold text-slate-800">{req.id}</td>
                          <td className="px-5 py-3 font-medium text-slate-600">{req.name}</td>
                          <td className="px-5 py-3 font-medium text-slate-600">{req.campus}</td>
                          <td className="px-5 py-3 text-right">
                            <Link to="/dashboard/visit" className="text-[#004c91] hover:text-[#003870] font-bold inline-flex items-center gap-1">
                              Chi tiết <ChevronRight className="w-4 h-4" />
                            </Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <div className="h-24 flex items-center justify-center text-sm font-medium text-slate-500">
                  Không có đơn liên cơ sở đang chờ duyệt.
                </div>
              )}
            </div>
          </div>

          {/* 7. Tình trạng campus compact */}
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm">
            <div className="px-5 py-4 border-b border-slate-100">
              <h3 className="font-bold text-slate-800 flex items-center gap-2">
                <Building2 className="w-5 h-5 text-emerald-600" /> Tình trạng vận hành campus
              </h3>
            </div>
            <div>
              {campusStatus?.length > 0 ? (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-sm">
                    <thead className="bg-slate-50 border-b border-slate-100 text-slate-500 uppercase text-xs">
                      <tr>
                        <th className="px-5 py-3 font-bold">Campus</th>
                        <th className="px-5 py-3 font-bold">Đang xử lý</th>
                        <th className="px-5 py-3 font-bold">Sắp diễn ra</th>
                        <th className="px-5 py-3 font-bold">Cảnh báo</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {campusStatus.map((c: any, idx: number) => (
                        <tr key={idx} className="hover:bg-slate-50">
                          <td className="px-5 py-3 font-bold text-slate-800">{c.name}</td>
                          <td className="px-5 py-3 font-medium text-slate-600">{c.processing}</td>
                          <td className="px-5 py-3 font-medium text-slate-600">{c.upcoming}</td>
                          <td className="px-5 py-3">
                            {c.alerts > 0 ? (
                              <span className="inline-flex items-center gap-1 text-xs font-bold text-red-600 bg-red-50 px-2 py-1 rounded-md">
                                <AlertTriangle className="w-3 h-3" /> {c.alerts}
                              </span>
                            ) : (
                              <span className="text-slate-400">-</span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <div className="h-24 flex items-center justify-center text-sm font-medium text-slate-500">
                  Chưa có dữ liệu vận hành campus.
                </div>
              )}
            </div>
          </div>

        </div>

        {/* Cột phải (1/3) */}
        <div className="space-y-6">

          {/* 5. Lịch / chuyến sắp tới */}
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm">
            <div className="px-5 py-4 border-b border-slate-100 flex items-center justify-between">
              <h3 className="font-bold text-slate-800 flex items-center gap-2">
                <Calendar className="w-5 h-5 text-[#004c91]" /> Lịch sắp tới
              </h3>
            </div>
            <div className="p-0">
              {upcomingVisits?.length > 0 ? (
                <div className="divide-y divide-slate-100">
                  {upcomingVisits.map((visit: any, idx: number) => (
                    <div key={idx} className="p-4 hover:bg-slate-50 transition-colors">
                      <p className="text-sm font-bold text-slate-800 truncate">{visit.name}</p>
                      <div className="flex items-center justify-between mt-2">
                        <span className="text-xs font-medium text-slate-500 bg-slate-100 px-2 py-1 rounded-md">{visit.campus}</span>
                        <span className="text-xs font-semibold text-[#004c91]">{visit.date}</span>
                      </div>
                    </div>
                  ))}
                  <Link to="/dashboard/visit" className="block w-full text-center py-3 text-sm font-bold text-[#004c91] hover:bg-slate-50">
                    Xem tất cả lịch
                  </Link>
                </div>
              ) : (
                <div className="h-24 flex items-center justify-center text-sm font-medium text-slate-500">
                  Không có chuyến sắp tới trong 7 ngày.
                </div>
              )}
            </div>
          </div>

          {/* 8. Hoạt động / thông báo gần đây */}
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm">
            <div className="px-5 py-4 border-b border-slate-100">
              <h3 className="font-bold text-slate-800 flex items-center gap-2">
                <Bell className="w-5 h-5 text-slate-600" /> Hoạt động gần đây
              </h3>
            </div>
            <div className="p-0">
              {recentActivities?.length > 0 ? (
                <div className="divide-y divide-slate-100">
                  {recentActivities.map((act: any, idx: number) => (
                    <div key={idx} className="p-4 flex gap-3 hover:bg-slate-50 transition-colors">
                      <div className="w-2 h-2 rounded-full bg-slate-300 mt-1.5 shrink-0" />
                      <div>
                        <p className="text-sm text-slate-700">{act.content}</p>
                        <p className="text-xs font-medium text-slate-400 mt-1">{act.time}</p>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="h-24 flex items-center justify-center text-sm font-medium text-slate-500">
                  Chưa có hoạt động mới.
                </div>
              )}
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}
