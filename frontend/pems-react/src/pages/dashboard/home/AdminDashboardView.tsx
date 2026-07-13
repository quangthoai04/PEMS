/**
 * Component AdminDashboardView
 * Tổng quan System Administration Console cho ADMIN — 100% dữ liệu thật từ
 * /api/admin/dashboard/* (không còn mock). Giờ hiển thị theo múi giờ Việt Nam.
 */

import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Users, KeyRound, ShieldAlert, Activity, Loader2, RefreshCw,
  ServerOff, Cpu, ScrollText, UserPlus, LogIn,
} from 'lucide-react';
import {
  AreaChart, Area, BarChart, Bar, LineChart, Line,
  XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer,
} from 'recharts';
import { adminApi } from '../../../features/admin/api/adminApi';
import type {
  AdminApiRequestActivityPoint,
  AdminDashboardSummary,
  AdminLoginActivityPoint,
  AdminRecentAuditItem,
  AdminSecurityOverview,
} from '../../../features/admin/types/admin.types';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';

// Cặp màu series đã validate CVD-safe trên nền trắng (dataviz checks pass):
// SUCCESS = xanh PEMS bậc sáng, FAILED = đỏ hệ thống. Màu chỉ nằm trên mark,
// text dùng token chữ thường (slate).
const SERIES_SUCCESS = '#0461b5';
const SERIES_FAILED = '#ef4444';
const SERIES_LATENCY = '#d9621a';

const tooltipStyle = {
  borderRadius: '12px',
  border: '1px solid #e2e8f0',
  boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)',
  fontSize: 12,
} as const;

/** Nhãn trục X dạng dd/MM từ chuỗi yyyy-MM-dd (ngày lịch VN do backend trả). */
const shortDate = (iso: string) => {
  const [, m, d] = iso.split('-');
  return `${d}/${m}`;
};

interface LoadState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
}

function useAdminData<T>(fetcher: () => Promise<T>): [LoadState<T>, () => void] {
  const [state, setState] = useState<LoadState<T>>({ data: null, loading: true, error: null });
  const load = useCallback(() => {
    setState((prev) => ({ ...prev, loading: true, error: null }));
    fetcher()
      .then((data) => setState({ data, loading: false, error: null }))
      .catch((e: any) => setState({
        data: null,
        loading: false,
        error: e?.response?.status === 403
          ? 'Bạn không có quyền xem dữ liệu quản trị.'
          : (e?.response?.data?.message || 'Không tải được dữ liệu.'),
      }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  useEffect(() => { load(); }, [load]);
  return [state, load];
}

function PanelState({ state, onRetry, children }: {
  state: LoadState<unknown>;
  onRetry: () => void;
  children: React.ReactNode;
}) {
  if (state.loading) {
    return (
      <div className="flex-1 flex items-center justify-center py-10 text-slate-400 text-sm">
        <Loader2 className="w-5 h-5 animate-spin mr-2" /> Đang tải dữ liệu...
      </div>
    );
  }
  if (state.error) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center py-10 text-center">
        <p className="text-sm font-semibold text-red-500 mb-2">{state.error}</p>
        <button
          onClick={onRetry}
          className="text-xs font-bold text-[#004c91] hover:underline flex items-center gap-1 cursor-pointer"
        >
          <RefreshCw className="w-3.5 h-3.5" /> Thử lại
        </button>
      </div>
    );
  }
  return <>{children}</>;
}

export function AdminDashboardView() {
  const [summary, reloadSummary] = useAdminData<AdminDashboardSummary>(() => adminApi.getDashboardSummary());
  const [loginActivity, reloadLogins] = useAdminData<AdminLoginActivityPoint[]>(() => adminApi.getLoginActivity(7));
  const [apiActivity, reloadApi] = useAdminData<AdminApiRequestActivityPoint[]>(() => adminApi.getIntegrationsActivity(7));
  const [security, reloadSecurity] = useAdminData<AdminSecurityOverview>(() => adminApi.getSecurityOverview());
  const [audits, reloadAudits] = useAdminData<AdminRecentAuditItem[]>(() => adminApi.getRecentAudits(8));

  const s = summary.data;
  const hasLoginData = (loginActivity.data ?? []).some((p) => p.success > 0 || p.failed > 0);
  const hasApiData = (apiActivity.data ?? []).some((p) => p.success > 0 || p.failed > 0);
  const latencyData = (apiActivity.data ?? []).filter((p) => p.avgResponseTimeMs != null);

  const statCards = [
    {
      label: 'Tài khoản',
      icon: Users,
      iconCls: 'bg-blue-50 text-[#004c91]',
      value: s ? s.accounts.total.toLocaleString('vi-VN') : '—',
      sub: s
        ? `${s.accounts.active} ACTIVE · ${s.accounts.inactive} INACTIVE · ${s.accounts.locked} LOCKED`
        : '',
      link: '/dashboard/accounts',
    },
    {
      label: 'Tài khoản mới (30 ngày)',
      icon: UserPlus,
      iconCls: 'bg-emerald-50 text-emerald-600',
      value: s ? s.accounts.newLast30Days.toLocaleString('vi-VN') : '—',
      sub: 'Tạo trong 30 ngày gần nhất',
      link: '/dashboard/accounts',
    },
    {
      label: 'Phiên đăng nhập',
      icon: KeyRound,
      iconCls: 'bg-orange-50 text-[#f37021]',
      value: s ? s.sessions.active.toLocaleString('vi-VN') : '—',
      sub: s ? `Đang hoạt động · ${s.sessions.expired} hết hạn · ${s.sessions.revoked} thu hồi` : '',
      link: '/dashboard/admin/sessions',
    },
    {
      label: 'Đăng nhập 24 giờ',
      icon: LogIn,
      iconCls: 'bg-blue-50 text-[#0461b5]',
      value: s ? `${s.logins24h.success.toLocaleString('vi-VN')}` : '—',
      sub: s ? `Thành công · ${s.logins24h.failed} thất bại` : '',
      link: '/dashboard/admin/security',
    },
    {
      label: 'Cảnh báo bảo mật (7 ngày)',
      icon: ShieldAlert,
      iconCls: 'bg-red-50 text-red-600',
      value: s ? (s.security.highLast7Days + s.security.criticalLast7Days).toLocaleString('vi-VN') : '—',
      sub: s ? `${s.security.highLast7Days} HIGH · ${s.security.criticalLast7Days} CRITICAL` : '',
      link: '/dashboard/admin/security',
    },
    {
      label: 'API tích hợp',
      icon: Cpu,
      iconCls: 'bg-purple-50 text-purple-600',
      value: s ? `${s.integrations.active}/${s.integrations.total}` : '—',
      sub: s
        ? `ACTIVE · ${s.integrations.testFailed} test FAILED · ${s.integrations.missingCredential} thiếu credential · ${s.integrations.quotaAbove80Percent} quota >80%`
        : '',
      link: '/dashboard/apis',
    },
  ];

  return (
    <div className="space-y-6 animate-in fade-in duration-500 pt-4">
      {/* Stat tiles — dữ liệu thật từ /admin/dashboard/summary */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
        <PanelState state={summary} onRetry={reloadSummary}>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {statCards.map((card) => {
              const Icon = card.icon;
              return (
                <Link
                  key={card.label}
                  to={card.link}
                  className="p-4 rounded-xl border border-slate-100 hover:border-[#004c91]/40 hover:shadow-sm transition-all flex items-start gap-3 group"
                >
                  <div className={`w-10 h-10 rounded-xl flex items-center justify-center shrink-0 ${card.iconCls}`}>
                    <Icon className="w-5 h-5" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-[11px] font-bold text-slate-500 uppercase tracking-wider">{card.label}</p>
                    <p className="text-2xl font-black text-slate-800 leading-tight">{card.value}</p>
                    {card.sub && <p className="text-xs text-slate-400 font-medium mt-0.5 truncate" title={card.sub}>{card.sub}</p>}
                  </div>
                </Link>
              );
            })}
          </div>
        </PanelState>
      </div>

      {/* Hạ tầng: backend chưa có telemetry → không hiển thị số liệu bịa */}
      <div className="bg-slate-50 border border-dashed border-slate-300 rounded-2xl p-4 flex items-center gap-3">
        <ServerOff className="w-5 h-5 text-slate-400 shrink-0" />
        <p className="text-sm text-slate-500 font-medium">
          Chưa cấu hình giám sát hạ tầng (uptime, CPU, RAM, dung lượng). Số liệu sẽ hiển thị khi backend có nguồn telemetry thật.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Login SUCCESS/FAILED 7 ngày */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col h-[340px]">
          <div className="mb-4">
            <h3 className="text-lg font-bold text-slate-800">Đăng nhập theo ngày (7 ngày)</h3>
            <p className="text-xs font-semibold text-slate-500 mt-1">Số lượt SUCCESS / FAILED từ login_logs</p>
          </div>
          <PanelState state={loginActivity} onRetry={reloadLogins}>
            {hasLoginData ? (
              <div className="flex-1 w-full min-h-0">
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={loginActivity.data ?? []} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                    <defs>
                      <linearGradient id="adminLoginSuccess" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor={SERIES_SUCCESS} stopOpacity={0.25} />
                        <stop offset="95%" stopColor={SERIES_SUCCESS} stopOpacity={0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E2E8F0" />
                    <XAxis dataKey="date" tickFormatter={shortDate} axisLine={false} tickLine={false}
                      tick={{ fontSize: 11, fill: '#64748B', fontWeight: 600 }} dy={8} />
                    <YAxis allowDecimals={false} axisLine={false} tickLine={false}
                      tick={{ fontSize: 11, fill: '#64748B', fontWeight: 600 }} />
                    <Tooltip contentStyle={tooltipStyle} labelFormatter={shortDate} />
                    <Legend wrapperStyle={{ fontSize: 12 }} />
                    <Area type="monotone" dataKey="success" name="Thành công" stroke={SERIES_SUCCESS}
                      strokeWidth={2} fill="url(#adminLoginSuccess)" />
                    <Area type="monotone" dataKey="failed" name="Thất bại" stroke={SERIES_FAILED}
                      strokeWidth={2} fillOpacity={0} />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            ) : (
              <div className="flex-1 flex items-center justify-center text-sm text-slate-400 font-medium">
                Chưa có lượt đăng nhập nào trong 7 ngày qua.
              </div>
            )}
          </PanelState>
        </div>

        {/* API request SUCCESS/FAILED 7 ngày */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col h-[340px]">
          <div className="mb-4">
            <h3 className="text-lg font-bold text-slate-800">API request theo ngày (7 ngày)</h3>
            <p className="text-xs font-semibold text-slate-500 mt-1">Số request SUCCESS / FAILED từ api_request_logs</p>
          </div>
          <PanelState state={apiActivity} onRetry={reloadApi}>
            {hasApiData ? (
              <div className="flex-1 w-full min-h-0">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={apiActivity.data ?? []} margin={{ top: 10, right: 10, left: -20, bottom: 0 }} barSize={16} barGap={2}>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E2E8F0" />
                    <XAxis dataKey="date" tickFormatter={shortDate} axisLine={false} tickLine={false}
                      tick={{ fontSize: 11, fill: '#64748B', fontWeight: 600 }} dy={8} />
                    <YAxis allowDecimals={false} axisLine={false} tickLine={false}
                      tick={{ fontSize: 11, fill: '#64748B', fontWeight: 600 }} />
                    <Tooltip cursor={{ fill: '#F1F5F9' }} contentStyle={tooltipStyle} labelFormatter={shortDate} />
                    <Legend wrapperStyle={{ fontSize: 12 }} />
                    <Bar dataKey="success" name="Thành công" fill={SERIES_SUCCESS} radius={[4, 4, 0, 0]} />
                    <Bar dataKey="failed" name="Thất bại" fill={SERIES_FAILED} radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            ) : (
              <div className="flex-1 flex items-center justify-center text-sm text-slate-400 font-medium">
                Chưa có request API nào trong 7 ngày qua.
              </div>
            )}
          </PanelState>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Response time trung bình (trục riêng, không dual-axis) */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col h-[300px]">
          <div className="mb-4">
            <h3 className="text-lg font-bold text-slate-800">Thời gian phản hồi API trung bình (ms)</h3>
            <p className="text-xs font-semibold text-slate-500 mt-1">Trung bình theo ngày, 7 ngày gần nhất</p>
          </div>
          <PanelState state={apiActivity} onRetry={reloadApi}>
            {latencyData.length > 0 ? (
              <div className="flex-1 w-full min-h-0">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={latencyData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E2E8F0" />
                    <XAxis dataKey="date" tickFormatter={shortDate} axisLine={false} tickLine={false}
                      tick={{ fontSize: 11, fill: '#64748B', fontWeight: 600 }} dy={8} />
                    <YAxis allowDecimals={false} axisLine={false} tickLine={false}
                      tick={{ fontSize: 11, fill: '#64748B', fontWeight: 600 }} />
                    <Tooltip contentStyle={tooltipStyle} labelFormatter={shortDate} />
                    <Line type="monotone" dataKey="avgResponseTimeMs" name="Trung bình (ms)"
                      stroke={SERIES_LATENCY} strokeWidth={2} dot={{ r: 3 }} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            ) : (
              <div className="flex-1 flex items-center justify-center text-sm text-slate-400 font-medium">
                Chưa có dữ liệu response time.
              </div>
            )}
          </PanelState>
        </div>

        {/* Security events HIGH/CRITICAL gần nhất */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col h-[300px]">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h3 className="text-lg font-bold text-slate-800">Security event mức cao</h3>
              <p className="text-xs font-semibold text-slate-500 mt-1">HIGH / CRITICAL gần nhất</p>
            </div>
            <Link to="/dashboard/admin/security" className="text-xs font-bold text-[#004c91] hover:underline px-3 py-1.5 bg-blue-50 rounded-lg">
              Xem tất cả
            </Link>
          </div>
          <PanelState state={security} onRetry={reloadSecurity}>
            {(security.data?.recentHighSeverity.length ?? 0) > 0 ? (
              <div className="flex-1 overflow-y-auto divide-y divide-slate-100 -mx-2">
                {security.data!.recentHighSeverity.map((ev) => (
                  <div key={ev.securityEventId} className="px-2 py-2.5 flex items-start gap-3">
                    <span className={`mt-0.5 shrink-0 inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[10px] font-black tracking-wider ${
                      ev.severity === 'CRITICAL' ? 'bg-red-100 text-red-700' : 'bg-orange-100 text-orange-700'
                    }`}>
                      <ShieldAlert className="w-3 h-3" /> {ev.severity}
                    </span>
                    <div className="min-w-0">
                      <p className="text-sm font-semibold text-slate-700 truncate">
                        {ev.eventType} · {ev.result}
                      </p>
                      <p className="text-xs text-slate-400 mt-0.5 truncate">
                        {ev.email || '—'} · IP {ev.ipAddress || '—'} · {formatVietnamDateTime(ev.createdAt)}
                      </p>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="flex-1 flex items-center justify-center text-sm text-slate-400 font-medium">
                Không có security event mức HIGH/CRITICAL.
              </div>
            )}
          </PanelState>
        </div>
      </div>

      {/* Audit log gần nhất */}
      <div className="bg-white border border-slate-200 shadow-sm rounded-2xl overflow-hidden">
        <div className="px-6 py-5 border-b border-slate-100 flex items-center justify-between">
          <div>
            <h3 className="font-bold text-slate-800 flex items-center gap-2">
              <ScrollText className="w-4 h-4 text-[#004c91]" /> Nhật ký kiểm toán gần nhất
            </h3>
            <p className="text-xs text-slate-500 font-medium mt-1">Các thao tác quản trị mới nhất từ audit_logs</p>
          </div>
          <Link to="/dashboard/admin/audit-logs" className="text-xs font-bold text-[#004c91] hover:underline px-3 py-1.5 bg-blue-50 rounded-lg">
            Xem tất cả
          </Link>
        </div>
        <PanelState state={audits} onRetry={reloadAudits}>
          {(audits.data?.length ?? 0) > 0 ? (
            <div className="divide-y divide-slate-100">
              {audits.data!.map((log) => (
                <div key={log.auditLogId} className="p-4 px-6 flex items-start gap-4 hover:bg-slate-50 transition-colors">
                  <div className="w-8 h-8 rounded-full bg-blue-100 text-[#004c91] flex items-center justify-center shrink-0">
                    <Activity className="w-4 h-4" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-slate-800 truncate">
                      <span className="font-bold text-[#004c91]">{log.actorName || log.actorEmail || 'Hệ thống'}</span>
                      {' '}· {log.action} · {log.entityType}{log.entityId ? ` #${log.entityId}` : ''}
                    </p>
                    <p className="text-xs text-slate-400 mt-1 truncate">
                      {formatVietnamDateTime(log.createdAt)}
                      {log.campusName ? ` · ${log.campusName}` : ''}
                      {log.ipAddress ? ` · IP: ${log.ipAddress}` : ''}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="py-10 text-center text-sm text-slate-400 font-medium">Chưa có bản ghi kiểm toán nào.</div>
          )}
        </PanelState>
      </div>
    </div>
  );
}
