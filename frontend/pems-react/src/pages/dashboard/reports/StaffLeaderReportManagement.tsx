import React, { useState, useEffect } from 'react';
import { Download, Calendar, Filter, FileText, CheckCircle, Clock, AlertTriangle, PlayCircle } from 'lucide-react';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell } from 'recharts';
import { reportsApi } from '../../../features/reports/api/reportsApi';
import type { StaffLeaderReportFilters, StaffLeaderReportOverview } from '../../../features/reports/types/staffLeaderReports.types';
import toast from 'react-hot-toast';

export function StaffLeaderReportManagement() {
  const [filters, setFilters] = useState<StaffLeaderReportFilters>({
    preset: 'THIS_YEAR',
    visitStatus: 'ALL',
    requestStatus: 'ALL',
    hostUserId: 'ALL',
    departmentId: 'ALL',
    logisticsStatus: 'ALL',
    feedbackRating: 'ALL'
  });
  
  const [data, setData] = useState<StaffLeaderReportOverview | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchData();
  }, [filters.preset]);

  const fetchData = async () => {
    try {
      setLoading(true);
      const res = await reportsApi.getStaffLeaderReportOverview(filters);
      setData(res);
    } catch (err) {
      toast.error("Không thể tải báo cáo. Vui lòng thử lại.");
    } finally {
      setLoading(false);
    }
  };

  const handleExport = async () => {
    try {
      const toastId = toast.loading('Đang xuất báo cáo...');
      const { blob, fileName } = await reportsApi.exportStaffLeaderReport({
        ...filters,
        exportFormat: 'CSV',
        reportSections: []
      });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', fileName);
      document.body.appendChild(link);
      link.click();
      link.remove();
      toast.success('Xuất báo cáo thành công', { id: toastId });
    } catch (err) {
      toast.error('Lỗi khi xuất báo cáo');
    }
  };

  if (loading || !data) {
    return <div className="p-8 text-center text-slate-500">Đang tải dữ liệu...</div>;
  }

  const kpis = data.kpis;
  const pipeline = data.campusLifecyclePipeline || [];
  
  return (
    <div className="space-y-6 animate-in fade-in duration-500 pb-12 font-sans bg-slate-50 min-h-screen">
      <div className="bg-white border-b border-slate-200 px-8 py-6">
        <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-2">
          <span>Dashboard</span>
          <span>/</span>
          <span className="text-[#004c91] font-bold">Báo cáo campus</span>
        </div>
        
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
          <div>
            <div className="flex items-center gap-3">
              <h2 className="text-2xl font-black text-[#004c91] tracking-tight">Báo cáo vận hành campus</h2>
              <span className="px-2.5 py-1 rounded-full bg-blue-50 text-[#004c91] text-xs font-bold border border-blue-100">
                Staff Leader
              </span>
            </div>
            <p className="text-sm font-medium text-slate-500 mt-1">Tổng quan xử lý yêu cầu, phân công host, logistics và chất lượng tiếp đón</p>
          </div>

          <div className="flex items-center gap-3">
            <div className="relative flex items-center bg-white border border-slate-200 rounded-lg px-3 py-2 shadow-sm">
              <Calendar className="w-4 h-4 text-slate-400 mr-2" />
              <select 
                value={filters.preset}
                onChange={(e) => setFilters(f => ({ ...f, preset: e.target.value }))}
                className="bg-transparent text-sm font-medium text-slate-700 outline-none cursor-pointer appearance-none pr-4"
              >
                <option value="THIS_MONTH">Tháng này</option>
                <option value="THIS_QUARTER">Quý này</option>
                <option value="THIS_YEAR">Năm nay</option>
              </select>
            </div>
            <button onClick={handleExport} className="flex items-center gap-2 px-4 py-2 bg-[#004c91] text-white text-sm font-bold rounded-lg hover:bg-[#00386b] transition-colors shadow-sm cursor-pointer">
              <Download className="w-4 h-4" />
              Xuất báo cáo
            </button>
          </div>
        </div>
      </div>

      <div className="px-8 space-y-6">
        {/* KPI Strip */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-sm flex flex-wrap divide-x divide-slate-100">
          <div className="flex-1 min-w-[120px] p-4">
            <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">Chờ duyệt</p>
            <p className="text-2xl font-black text-slate-800">{kpis.pendingSingleCampusApproval}</p>
          </div>
          <div className="flex-1 min-w-[120px] p-4">
            <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">Chờ gán host</p>
            <p className="text-2xl font-black text-[#f37021]">{kpis.waitingHostAssignment}</p>
          </div>
          <div className="flex-1 min-w-[120px] p-4">
            <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">Đang chuẩn bị</p>
            <p className="text-2xl font-black text-[#004c91]">{kpis.beforeVisit}</p>
          </div>
          <div className="flex-1 min-w-[120px] p-4">
            <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">Đang diễn ra</p>
            <p className="text-2xl font-black text-emerald-600">{kpis.duringVisit}</p>
          </div>
          <div className="flex-1 min-w-[120px] p-4">
            <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">Sau tiếp khách</p>
            <p className="text-2xl font-black text-purple-600">{kpis.afterVisit}</p>
          </div>
          <div className="flex-1 min-w-[120px] p-4">
            <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">Chưa đóng/quá hạn</p>
            <p className="text-2xl font-black text-red-600">{kpis.overdueOrNotClosed}</p>
          </div>
          <div className="flex-1 min-w-[120px] p-4 bg-slate-50/50">
            <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">Feedback TB</p>
            <p className="text-2xl font-black text-slate-800">{kpis.averageFeedbackRating?.toFixed(1) || '-'}</p>
          </div>
          <div className="flex-1 min-w-[120px] p-4 bg-slate-50/50">
            <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">Tổng khách</p>
            <p className="text-2xl font-black text-slate-800">{kpis.totalGuests}</p>
          </div>
        </div>

        {/* Attention Items */}
        {data.attentionItems.length > 0 && (
          <div className="bg-orange-50 border border-orange-100 rounded-xl p-4 flex items-center gap-4">
             <AlertTriangle className="w-5 h-5 text-[#f37021] shrink-0" />
             <div className="flex gap-4 flex-wrap text-sm text-orange-900 font-medium">
               {data.attentionItems.map((item, idx) => (
                 <span key={idx} className="flex items-center gap-1.5">
                   <span className="font-bold">{item.count}</span> {item.label}
                   {idx < data.attentionItems.length - 1 && <span className="text-orange-300 ml-2">|</span>}
                 </span>
               ))}
             </div>
          </div>
        )}

        {/* Lifecycle Pipeline */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-6">
           <h3 className="text-sm font-bold text-slate-800 mb-4">Tiến độ chuyến thăm tại campus</h3>
           <div className="flex items-center w-full relative h-12">
              <div className="absolute top-1/2 left-0 right-0 h-1 bg-slate-100 -translate-y-1/2 z-0" />
              {pipeline.map((item, idx) => (
                <div key={item.status} className="flex-1 flex flex-col items-center relative z-10 group cursor-pointer">
                  <div className={`w-8 h-8 rounded-full flex items-center justify-center font-bold text-xs shadow-sm transition-transform group-hover:scale-110 ${item.count > 0 ? 'bg-[#004c91] text-white border-2 border-white' : 'bg-slate-200 text-slate-400 border-2 border-white'}`}>
                    {item.count}
                  </div>
                  <span className={`text-[10px] font-bold mt-2 uppercase tracking-wide text-center ${item.count > 0 ? 'text-slate-700' : 'text-slate-400'}`}>
                    {item.labelVi}
                  </span>
                </div>
              ))}
           </div>
        </div>

        {/* Charts & Workload */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
           <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-6">
              <h3 className="text-sm font-bold text-slate-800 mb-4">Khối lượng công việc Host</h3>
              {data.hostWorkload.length > 0 ? (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-sm">
                     <thead>
                        <tr className="border-b border-slate-100 text-slate-400">
                           <th className="pb-2 font-bold">Host</th>
                           <th className="pb-2 font-bold text-center">Đang phụ trách</th>
                           <th className="pb-2 font-bold text-center">Sắp tới 7 ngày</th>
                        </tr>
                     </thead>
                     <tbody className="divide-y divide-slate-50">
                        {data.hostWorkload.map((host) => (
                          <tr key={host.hostUserId}>
                             <td className="py-3 font-semibold text-slate-700">{host.hostName}</td>
                             <td className="py-3 text-center font-bold text-[#004c91]">{host.assignedCount}</td>
                             <td className="py-3 text-center font-bold text-[#f37021]">{host.upcoming7Days}</td>
                          </tr>
                        ))}
                     </tbody>
                  </table>
                </div>
              ) : (
                <div className="text-center text-slate-400 py-8 text-sm">Không có dữ liệu host.</div>
              )}
           </div>

           <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-6">
              <h3 className="text-sm font-bold text-slate-800 mb-4">Tiến độ logistics / hỗ trợ phòng ban</h3>
              {data.logisticsByDepartment.length > 0 ? (
                <div className="space-y-4">
                  {data.logisticsByDepartment.map((dept) => (
                    <div key={dept.departmentId}>
                       <div className="flex justify-between text-xs font-bold text-slate-600 mb-1">
                          <span>{dept.departmentName}</span>
                          <span>{dept.done}/{dept.totalItems} Hoàn thành</span>
                       </div>
                       <div className="w-full h-2 bg-slate-100 rounded-full overflow-hidden flex">
                          <div style={{ width: `${(dept.done/dept.totalItems)*100}%` }} className="bg-emerald-500 h-full" />
                          <div style={{ width: `${(dept.inProgress/dept.totalItems)*100}%` }} className="bg-[#004c91] h-full" />
                          <div style={{ width: `${(dept.rejected/dept.totalItems)*100}%` }} className="bg-red-500 h-full" />
                       </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-center text-slate-400 py-8 text-sm">Không có dữ liệu logistics.</div>
              )}
           </div>
        </div>
      </div>
    </div>
  );
}
