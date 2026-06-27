import React, { useEffect, useMemo, useState } from 'react';
import {
  Briefcase,
  Calendar,
  CheckCircle2,
  Clock,
  Download,
  Filter,
  ReceiptText,
  AlertTriangle,
  UserCheck,
  Users,
} from 'lucide-react';
import {
  Area,
  AreaChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';

type ReportPeriod = 'this_month' | 'last_month' | 'this_quarter' | 'this_year';

type DepartmentReportItem = {
  itemType: 'INVITATION' | 'REQUEST';
  itemId: number;
  logisticsItemId?: number;
  delegationName: string;
  organizationName?: string;
  title: string;
  currentResponsibleName?: string;
  currentResponsibleRole?: string;
  uiStatus: string;
  statusLabel: string;
  startAt: string;
  endAt: string;
};

type StaffPerformance = {
  name: string;
  role: string;
  tasksAssigned: number;
  tasksCompleted: number;
  hoursSpent: number;
};

type SettlementSummary = {
  group: string;
  totalItems: number;
  totalQuantity: number;
  issueCount: number;
  latestNotes: string[];
};

const MONTH_LABELS = ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9', 'T10', 'T11', 'T12'];

const TYPE_COLORS: Record<string, string> = {
  INVITATION: '#004c91',
  REQUEST: '#f37021',
  DONE: '#10b981',
  IN_PROGRESS: '#8b5cf6',
};

const getPeriodRange = (period: ReportPeriod) => {
  const now = new Date();
  const startOfDay = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const endOfDay = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59, 999);

  if (period === 'this_month') {
    return {
      from: new Date(now.getFullYear(), now.getMonth(), 1),
      to: endOfDay(new Date(now.getFullYear(), now.getMonth() + 1, 0)),
      label: 'Tháng này',
    };
  }

  if (period === 'last_month') {
    return {
      from: new Date(now.getFullYear(), now.getMonth() - 1, 1),
      to: endOfDay(new Date(now.getFullYear(), now.getMonth(), 0)),
      label: 'Tháng trước',
    };
  }

  if (period === 'this_quarter') {
    const quarterStartMonth = Math.floor(now.getMonth() / 3) * 3;
    return {
      from: new Date(now.getFullYear(), quarterStartMonth, 1),
      to: endOfDay(new Date(now.getFullYear(), quarterStartMonth + 3, 0)),
      label: 'Quý này',
    };
  }

  return {
    from: startOfDay(new Date(now.getFullYear(), 0, 1)),
    to: endOfDay(new Date(now.getFullYear(), 11, 31)),
    label: 'Năm nay',
  };
};

const toDateInput = (date: Date) => date.toISOString().slice(0, 10);

const diffHours = (start?: string, end?: string) => {
  if (!start || !end) return 0;
  const startDate = new Date(start);
  const endDate = new Date(end);
  if (Number.isNaN(startDate.getTime()) || Number.isNaN(endDate.getTime())) return 0;
  return Math.max(0, (endDate.getTime() - startDate.getTime()) / 36e5);
};

const formatNumber = (value: number) => new Intl.NumberFormat('vi-VN').format(Math.round(value));

const inferSettlementGroup = (title = '', description = '') => {
  const text = `${title} ${description}`.toLowerCase();
  if (/car|xe|transport|đưa đón|di chuyển/.test(text)) return 'Phương tiện / xe';
  if (/meal|food|tea|trà|đồ ăn|ăn uống|nước/.test(text)) return 'Trà nước / đồ ăn';
  if (/equipment|projector|mic|loa|thiết bị|máy chiếu|điện tử/.test(text)) return 'Thiết bị điện tử';
  if (/room|phòng|hội trường|địa điểm/.test(text)) return 'Phòng / địa điểm';
  if (/banner|standee|poster|biển|băng rôn/.test(text)) return 'Ấn phẩm / bảng biểu';
  return 'Khác';
};

const normalizeNoteLines = (...notes: Array<string | undefined>) => notes
  .flatMap(note => (note || '').split('\n'))
  .map(line => line.trim())
  .filter(Boolean);

const hasIssueNote = (note: string) => /hỏng|thiếu|mất|bể|vỡ|bẩn|rách|đền|bù|khắc phục|damaged|missing|lost|broken/i.test(note);

export function DepartmentReportDashboard() {
  const [period, setPeriod] = useState<ReportPeriod>('this_year');
  const [items, setItems] = useState<DepartmentReportItem[]>([]);
  const [settlementRows, setSettlementRows] = useState<SettlementSummary[]>([]);
  const [settlementLoading, setSettlementLoading] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const range = useMemo(() => getPeriodRange(period), [period]);

  useEffect(() => {
    let mounted = true;
    const loadReport = async () => {
      setLoading(true);
      setError(null);
      try {
        const response = await departmentReceptionTasksApi.getAssignmentsProgress({
          ownerScope: 'DEPARTMENT',
          itemType: 'ALL',
          status: 'ALL',
          sortBy: 'date',
          sortDirection: 'ASC',
          fromDate: toDateInput(range.from),
          toDate: toDateInput(range.to),
          page: 1,
          pageSize: 1000,
        });
        if (!mounted) return;
        setItems(Array.isArray(response?.items) ? response.items : []);
      } catch (err: any) {
        if (!mounted) return;
        setError(err?.response?.data?.message || err?.message || 'Không thể tải dữ liệu báo cáo phòng ban.');
        setItems([]);
      } finally {
        if (mounted) setLoading(false);
      }
    };

    loadReport();
    return () => {
      mounted = false;
    };
  }, [range.from, range.to]);

  useEffect(() => {
    let mounted = true;
    const requestItems = items.filter(item => item.itemType === 'REQUEST');

    if (requestItems.length === 0) {
      setSettlementRows([]);
      return;
    }

    const loadSettlement = async () => {
      setSettlementLoading(true);
      try {
        const details = await Promise.all(
          requestItems.slice(0, 200).map(async item => {
            try {
              const detail = await departmentReceptionTasksApi.getRequestDetail(item.logisticsItemId || item.itemId);
              return { item, detail };
            } catch {
              return { item, detail: null };
            }
          }),
        );

        if (!mounted) return;

        const grouped = new Map<string, SettlementSummary>();
        details.forEach(({ item, detail }) => {
          const group = inferSettlementGroup(item.title, detail?.description || '');
          const current = grouped.get(group) || {
            group,
            totalItems: 0,
            totalQuantity: 0,
            issueCount: 0,
            latestNotes: [],
          };
          const notes = normalizeNoteLines(detail?.borrowNote, detail?.returnNote);
          const issueNotes = notes.filter(hasIssueNote);
          current.totalItems += 1;
          current.totalQuantity += Number(detail?.quantity || 1);
          current.issueCount += issueNotes.length;
          current.latestNotes.push(...(issueNotes.length ? issueNotes : notes).slice(0, 2));
          grouped.set(group, current);
        });

        setSettlementRows(
          Array.from(grouped.values())
            .map(row => ({ ...row, latestNotes: row.latestNotes.slice(0, 4) }))
            .sort((a, b) => b.totalQuantity - a.totalQuantity),
        );
      } finally {
        if (mounted) setSettlementLoading(false);
      }
    };

    loadSettlement();
    return () => {
      mounted = false;
    };
  }, [items]);

  const completedItems = useMemo(() => items.filter(item => item.uiStatus === 'DONE'), [items]);
  const assignedItems = useMemo(() => items.filter(item => item.uiStatus !== 'REQUESTED'), [items]);

  const totalHours = useMemo(
    () => items.reduce((sum, item) => sum + diffHours(item.startAt, item.endAt), 0),
    [items],
  );

  const uniquePartners = useMemo(() => {
    const keys = new Set<string>();
    items.forEach(item => {
      const key = (item.organizationName || item.delegationName || '').trim();
      if (key) keys.add(key);
    });
    return keys.size;
  }, [items]);

  const completionRate = items.length ? Math.round((completedItems.length / items.length) * 100) : 0;

  const chartData = useMemo(() => {
    const buckets = MONTH_LABELS.map(name => ({ name, assigned: 0, completed: 0 }));
    items.forEach(item => {
      const date = new Date(item.startAt);
      if (Number.isNaN(date.getTime())) return;
      const bucket = buckets[date.getMonth()];
      bucket.assigned += 1;
      if (item.uiStatus === 'DONE') bucket.completed += 1;
    });
    return period === 'this_year'
      ? buckets
      : buckets.filter((_, index) => {
          const date = new Date(range.from.getFullYear(), index, 1);
          return date >= new Date(range.from.getFullYear(), range.from.getMonth(), 1)
            && date <= new Date(range.to.getFullYear(), range.to.getMonth(), 1);
        });
  }, [items, period, range.from, range.to]);

  const distribution = useMemo(() => {
    const invitationCount = items.filter(item => item.itemType === 'INVITATION').length;
    const requestCount = items.filter(item => item.itemType === 'REQUEST').length;
    const inProgressCount = items.filter(item => item.uiStatus === 'IN_PROGRESS').length;
    const doneCount = completedItems.length;
    return [
      { name: 'Thư mời', value: invitationCount, color: TYPE_COLORS.INVITATION },
      { name: 'Đơn yêu cầu', value: requestCount, color: TYPE_COLORS.REQUEST },
      { name: 'Đang xử lý', value: inProgressCount, color: TYPE_COLORS.IN_PROGRESS },
      { name: 'Hoàn thành', value: doneCount, color: TYPE_COLORS.DONE },
    ].filter(item => item.value > 0);
  }, [items, completedItems.length]);

  const topStaff = useMemo<StaffPerformance[]>(() => {
    const map = new Map<string, StaffPerformance>();
    items.forEach(item => {
      const name = item.currentResponsibleName?.trim();
      if (!name) return;
      const current = map.get(name) || {
        name,
        role: item.currentResponsibleRole || 'Nhân sự phòng ban',
        tasksAssigned: 0,
        tasksCompleted: 0,
        hoursSpent: 0,
      };
      current.tasksAssigned += 1;
      if (item.uiStatus === 'DONE') current.tasksCompleted += 1;
      current.hoursSpent += diffHours(item.startAt, item.endAt);
      map.set(name, current);
    });
    return Array.from(map.values())
      .sort((a, b) => b.tasksCompleted - a.tasksCompleted || b.tasksAssigned - a.tasksAssigned)
      .slice(0, 6);
  }, [items]);

  const exportReport = () => {
    const rows = [
      ['Loại', 'Đoàn khách', 'Tổ chức', 'Nhiệm vụ', 'Người phụ trách', 'Vai trò', 'Trạng thái', 'Bắt đầu', 'Kết thúc'],
      ...items.map(item => [
        item.itemType === 'INVITATION' ? 'Thư mời' : 'Đơn yêu cầu',
        item.delegationName,
        item.organizationName || '',
        item.title,
        item.currentResponsibleName || '',
        item.currentResponsibleRole || '',
        item.statusLabel,
        item.startAt,
        item.endAt,
      ]),
    ];
    const csv = rows
      .map(row => row.map(cell => `"${String(cell).replace(/"/g, '""')}"`).join(','))
      .join('\n');
    const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `bao-cao-phong-ban-${toDateInput(range.from)}-${toDateInput(range.to)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const statCards = [
    {
      label: 'Tổng công việc',
      value: formatNumber(items.length),
      change: `${assignedItems.length} đã giao`,
      icon: Briefcase,
      color: 'text-[#004c91]',
      bg: 'bg-blue-50',
    },
    {
      label: 'Giờ tham gia',
      value: `${formatNumber(totalHours)}h`,
      change: `${formatNumber(totalHours / Math.max(1, items.length))}h TB`,
      icon: Clock,
      color: 'text-[#f37021]',
      bg: 'bg-orange-50',
    },
    {
      label: 'Đối tác đã kết nối',
      value: formatNumber(uniquePartners),
      change: `${items.filter(item => item.itemType === 'INVITATION').length} thư mời`,
      icon: UserCheck,
      color: 'text-purple-600',
      bg: 'bg-purple-50',
    },
    {
      label: 'Tỷ lệ hoàn thành công việc',
      value: `${completionRate}%`,
      change: `${completedItems.length}/${items.length || 0}`,
      icon: CheckCircle2,
      color: 'text-emerald-600',
      bg: 'bg-emerald-50',
    },
  ];

  return (
    <div className="space-y-6 animate-in fade-in duration-500 pb-12 font-sans">
      <div className="flex items-center gap-2 text-sm text-slate-500 font-medium">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Thống kê phòng ban</span>
      </div>

      <div className="flex flex-col xl:flex-row justify-between items-start xl:items-center gap-4">
        <div>
          <h2 className="text-3xl md:text-4xl font-black text-[#004c91] tracking-tight">
            Thống kê hiệu suất phòng ban
          </h2>
          <p className="text-base font-medium text-slate-500 mt-1">
            Tổng quan dữ liệu công việc và sự tham gia của nhân viên
          </p>
        </div>

        <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 w-full xl:w-auto">
          <div className="relative sm:w-56 bg-white border border-slate-200 rounded-xl flex items-center px-3 gap-2 shadow-sm">
            <Calendar className="w-4 h-4 text-slate-400" />
            <select
              value={period}
              onChange={event => setPeriod(event.target.value as ReportPeriod)}
              className="flex-1 bg-transparent py-3 text-sm font-bold text-slate-700 outline-none appearance-none cursor-pointer"
            >
              <option value="this_month">Tháng này</option>
              <option value="last_month">Tháng trước</option>
              <option value="this_quarter">Quý này</option>
              <option value="this_year">Năm nay</option>
            </select>
            <Filter className="w-4 h-4 text-slate-400 pointer-events-none" />
          </div>

          <button
            type="button"
            onClick={exportReport}
            disabled={loading || items.length === 0}
            className="flex items-center justify-center gap-2 px-5 py-3 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#00386b] disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-sm"
          >
            <Download className="w-4 h-4" />
            <span>Xuất báo cáo</span>
          </button>
        </div>
      </div>

      {error && (
        <div className="rounded-xl border border-red-100 bg-red-50 px-4 py-3 text-sm font-semibold text-red-700">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-5">
        {statCards.map(card => {
          const Icon = card.icon;
          return (
            <div key={card.label} className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm min-h-44 flex flex-col justify-between">
              <div className="flex items-center justify-between mb-6">
                <div className={`w-14 h-14 rounded-2xl ${card.bg} ${card.color} flex items-center justify-center`}>
                  <Icon className="w-7 h-7" />
                </div>
                <span className="text-xs font-black text-emerald-600 bg-emerald-50 px-3 py-1.5 rounded-lg">
                  {card.change}
                </span>
              </div>
              <div>
                <p className="text-sm font-black text-slate-400 uppercase tracking-wider mb-2">{card.label}</p>
                <p className="text-4xl font-black text-slate-900">{loading ? '...' : card.value}</p>
              </div>
            </div>
          );
        })}
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
        <section className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm xl:col-span-2">
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 mb-7">
            <div>
              <h3 className="text-xl font-black text-slate-900">Hiệu suất xử lý công việc</h3>
              <p className="text-sm text-slate-500 font-medium mt-1">Thống kê số lượng công việc được giao và đã hoàn thành</p>
            </div>
            <div className="flex items-center gap-4">
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-full bg-[#004c91]" />
                <span className="text-xs font-bold text-slate-600">Đã hoàn thành</span>
              </div>
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-full bg-[#f37021]" />
                <span className="text-xs font-bold text-slate-600">Được giao</span>
              </div>
            </div>
          </div>

          <div className="h-[330px] w-full">
            <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
              <AreaChart data={chartData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                <defs>
                  <linearGradient id="deptCompleted" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#004c91" stopOpacity={0.25} />
                    <stop offset="95%" stopColor="#004c91" stopOpacity={0} />
                  </linearGradient>
                  <linearGradient id="deptAssigned" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#f37021" stopOpacity={0.25} />
                    <stop offset="95%" stopColor="#f37021" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                <XAxis dataKey="name" axisLine={false} tickLine={false} tick={{ fill: '#64748b', fontSize: 12, fontWeight: 700 }} dy={10} />
                <YAxis axisLine={false} tickLine={false} tick={{ fill: '#64748b', fontSize: 12, fontWeight: 700 }} />
                <Tooltip contentStyle={{ borderRadius: 12, border: 'none', boxShadow: '0 10px 30px rgba(15, 23, 42, 0.14)' }} />
                <Area type="monotone" dataKey="completed" name="Đã hoàn thành" stroke="#004c91" strokeWidth={3} fill="url(#deptCompleted)" />
                <Area type="monotone" dataKey="assigned" name="Được giao" stroke="#f37021" strokeWidth={3} fill="url(#deptAssigned)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </section>

        <section className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm">
          <h3 className="text-xl font-black text-slate-900">Phân bổ mảng việc</h3>
          <p className="text-sm text-slate-500 font-medium mt-1 mb-6">Tỷ lệ các loại nhiệm vụ phòng ban đảm nhận</p>

          <div className="h-[280px]">
            <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
              <PieChart>
                <Pie
                  data={distribution}
                  cx="50%"
                  cy="50%"
                  innerRadius={66}
                  outerRadius={104}
                  paddingAngle={5}
                  dataKey="value"
                  stroke="none"
                >
                  {distribution.map((entry) => (
                    <Cell key={entry.name} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip contentStyle={{ borderRadius: 12, border: 'none', boxShadow: '0 10px 30px rgba(15, 23, 42, 0.14)' }} />
              </PieChart>
            </ResponsiveContainer>
          </div>

          <div className="grid grid-cols-2 gap-3 mt-3">
            {distribution.map(item => (
              <div key={item.name} className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-full shrink-0" style={{ backgroundColor: item.color }} />
                <span className="text-[11px] font-black text-slate-600 uppercase tracking-wider leading-tight">
                  {item.name}: {item.value}
                </span>
              </div>
            ))}
            {distribution.length === 0 && (
              <p className="col-span-2 text-sm font-semibold text-slate-400 text-center py-8">Chưa có dữ liệu</p>
            )}
          </div>
        </section>
      </div>

      <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="p-6 border-b border-slate-100 bg-slate-50/50 flex items-center justify-between">
          <div>
            <h3 className="text-xl font-black text-slate-900">Hiệu suất nhân sự phòng ban</h3>
            <p className="text-sm text-slate-500 font-medium mt-1">Tổng hợp theo người phụ trách trong khoảng thời gian đang chọn</p>
          </div>
          <Users className="w-5 h-5 text-[#004c91]" />
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-left">
            <thead>
              <tr className="bg-[#004c91] text-white">
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest text-center">#</th>
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest">Nhân sự</th>
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest">Vai trò</th>
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest text-right">Được giao</th>
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest text-right">Hoàn thành</th>
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest text-right">Giờ tham gia</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {topStaff.map((member, index) => (
                <tr key={member.name} className="hover:bg-blue-50/40 transition-colors">
                  <td className="px-5 py-4 text-center">
                    <div className="w-8 h-8 rounded-full bg-blue-50 text-[#004c91] flex items-center justify-center mx-auto font-black">
                      {index + 1}
                    </div>
                  </td>
                  <td className="px-5 py-4 font-black text-slate-900">{member.name}</td>
                  <td className="px-5 py-4 text-sm font-semibold text-slate-500">{member.role}</td>
                  <td className="px-5 py-4 text-right font-bold text-slate-700">{member.tasksAssigned}</td>
                  <td className="px-5 py-4 text-right font-bold text-emerald-600">{member.tasksCompleted}</td>
                  <td className="px-5 py-4 text-right font-bold text-[#004c91]">{formatNumber(member.hoursSpent)}h</td>
                </tr>
              ))}
              {topStaff.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-5 py-10 text-center text-sm font-semibold text-slate-400">
                    {loading ? 'Đang tải dữ liệu...' : 'Chưa có dữ liệu nhân sự trong kỳ này'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="p-6 border-b border-slate-100 bg-slate-50/50 flex flex-col md:flex-row md:items-center md:justify-between gap-3">
          <div>
            <h3 className="text-xl font-black text-slate-900">Thanh toán khắc phục</h3>
            <p className="text-sm text-slate-500 font-medium mt-1">
              Tổng hợp số lượng theo mảng việc và ghi chú bàn giao/nghiệm thu để phục vụ tính chi phí, hóa đơn.
            </p>
          </div>
          <div className="flex items-center gap-2 text-[#004c91] bg-blue-50 px-3 py-2 rounded-xl font-black text-sm">
            <ReceiptText className="w-4 h-4" />
            {settlementLoading ? 'Đang tổng hợp...' : `${settlementRows.length} mảng việc`}
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[900px] text-left">
            <thead>
              <tr className="bg-[#004c91] text-white">
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest">Mảng việc</th>
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest text-right">Số đơn</th>
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest text-right">Tổng số lượng</th>
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest text-right">Ghi chú hỏng/thiếu</th>
                <th className="px-5 py-4 text-[11px] font-black uppercase tracking-widest">Ghi chú cần xử lý</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {settlementRows.map(row => (
                <tr key={row.group} className="hover:bg-orange-50/40 transition-colors">
                  <td className="px-5 py-4">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center">
                        <ReceiptText className="w-5 h-5" />
                      </div>
                      <div>
                        <p className="font-black text-slate-900">{row.group}</p>
                        <p className="text-[11px] font-semibold text-slate-400">Theo đơn yêu cầu đã gửi phòng ban</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-5 py-4 text-right font-bold text-slate-700">{row.totalItems}</td>
                  <td className="px-5 py-4 text-right font-black text-[#004c91]">{formatNumber(row.totalQuantity)}</td>
                  <td className="px-5 py-4 text-right">
                    <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-black border ${
                      row.issueCount > 0
                        ? 'bg-red-50 text-red-700 border-red-100'
                        : 'bg-emerald-50 text-emerald-700 border-emerald-100'
                    }`}>
                      {row.issueCount > 0 && <AlertTriangle className="w-3.5 h-3.5" />}
                      {row.issueCount}
                    </span>
                  </td>
                  <td className="px-5 py-4">
                    {row.latestNotes.length > 0 ? (
                      <div className="space-y-1.5">
                        {row.latestNotes.map((note, index) => (
                          <p key={`${row.group}-${index}`} className="text-xs font-semibold text-slate-600 line-clamp-1" title={note}>
                            {note}
                          </p>
                        ))}
                      </div>
                    ) : (
                      <span className="text-xs font-semibold text-slate-400">Không có ghi chú phát sinh</span>
                    )}
                  </td>
                </tr>
              ))}

              {settlementRows.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-5 py-10 text-center text-sm font-semibold text-slate-400">
                    {settlementLoading ? 'Đang tổng hợp dữ liệu quyết toán...' : 'Chưa có đơn yêu cầu để tổng hợp thanh toán khắc phục'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
