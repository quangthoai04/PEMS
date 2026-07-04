/**
 * ApiManagement — màn ADMIN cấu hình API tích hợp (docs/PARTNER_canh/02 §10).
 * Trọng tâm: Google Document AI Business Card OCR — cấu hình, test kết nối,
 * bật/tắt, hạn mức tháng và nhật ký gọi API (đã sanitize, không chứa secret).
 */
import React, { useCallback, useEffect, useState } from 'react';
import {
  ScanLine, Loader2, RefreshCw, Power, PowerOff,
  Settings2, Activity, KeyRound, X,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { apiManagementApi } from '../../../features/api-management/api/apiManagementApi';
import type {
  ApiIntegration,
  ApiQuota,
  ApiRequestLogListResponse,
  UpsertGoogleDocumentAiOcrConfigRequest,
} from '../../../features/api-management/types/apiManagement.types';
import {
  getApiErrorMessage,
  showLoadingToast,
  showMessageErrorToast,
  updateToastSuccess,
  updateToastError,
  updateToastMessageError,
} from '../../../shared/utils/toast';

const inputCls =
  'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white';
const labelCls = 'block text-xs font-bold text-gray-500 uppercase mb-1';

/**
 * Diễn giải lỗi test kết nối Google Document AI thành thông báo rõ ràng cho người dùng.
 * Nguồn tín hiệu: HTTP status của request test + message backend (đã mask) — cả hai đều
 * có thể mang mã lỗi Google (403/404) tuỳ backend surface ở status hay ở body.
 */
function describeTestFailure(status: number | undefined, backendMessage?: string | null): string {
  const msg = (backendMessage ?? '').trim();
  const hay = `${status ?? ''} ${msg}`.toLowerCase();
  if (status === 403 || /\b403\b|permission|forbidden|không có quyền/.test(hay)) {
    return 'Không có quyền truy cập tài nguyên Google Cloud. Vui lòng kiểm tra Project ID và quyền service account.';
  }
  if (status === 404 || /\b404\b|not\s*found|processor|không tìm thấy/.test(hay)) {
    return 'Không tìm thấy processor hoặc project. Vui lòng kiểm tra Project ID, Location và Processor ID.';
  }
  if (/credential|service\s*account|secret\s*ref|chưa cấu hình/.test(hay)) {
    return 'Chưa cấu hình credential. Vui lòng nhập Service Account JSON hoặc Secret Ref.';
  }
  return msg || 'Test kết nối thất bại. Vui lòng thử lại.';
}

export function ApiManagement() {
  const navigate = useNavigate();

  const [integrations, setIntegrations] = useState<ApiIntegration[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [editTarget, setEditTarget] = useState<ApiIntegration | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [testBusy, setTestBusy] = useState<number | null>(null);
  const [statusBusy, setStatusBusy] = useState<number | null>(null);

  // Panel: quota + logs of the selected config
  const [panelTarget, setPanelTarget] = useState<ApiIntegration | null>(null);
  const [panelTab, setPanelTab] = useState<'QUOTA' | 'LOGS'>('QUOTA');
  const [quotas, setQuotas] = useState<ApiQuota[]>([]);
  const [quotaEdit, setQuotaEdit] = useState('');
  const [logs, setLogs] = useState<ApiRequestLogListResponse | null>(null);
  const [logsPage, setLogsPage] = useState(1);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setIntegrations(await apiManagementApi.getIntegrations());
    } catch (e: any) {
      setLoadError(e?.response?.data?.message || 'Không tải được danh sách cấu hình API.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  useEffect(() => {
    if (!panelTarget) return;
    if (panelTab === 'QUOTA') {
      apiManagementApi.getQuota(panelTarget.apiConfigId).then(setQuotas).catch(() => setQuotas([]));
    } else {
      apiManagementApi.getLogs(panelTarget.apiConfigId, logsPage, 20)
        .then(setLogs).catch(() => setLogs(null));
    }
  }, [panelTarget, panelTab, logsPage]);

  const test = async (config: ApiIntegration) => {
    setTestBusy(config.apiConfigId);
    const toastId = showLoadingToast('Đang kiểm tra kết nối...', `api-test-${config.apiConfigId}`);
    try {
      const result = await apiManagementApi.testConnection(config.apiConfigId);
      if (result.success) {
        updateToastSuccess(toastId, `Kết nối API thành công (${result.responseTimeMs} ms).`);
      } else {
        updateToastMessageError(toastId, describeTestFailure(undefined, result.message));
      }
      await load();
    } catch (e: any) {
      const message = describeTestFailure(
        e?.response?.status,
        getApiErrorMessage(e, 'Test kết nối thất bại. Vui lòng thử lại.'),
      );
      updateToastMessageError(toastId, message);
    } finally {
      setTestBusy(null);
    }
  };

  const toggleStatus = async (config: ApiIntegration) => {
    setStatusBusy(config.apiConfigId);
    const enabling = config.status !== 'ACTIVE';
    const toastId = showLoadingToast(
      enabling ? 'Đang bật cấu hình API...' : 'Đang tắt cấu hình API...',
      `api-toggle-${config.apiConfigId}`,
    );
    try {
      if (config.status === 'ACTIVE') await apiManagementApi.disable(config.apiConfigId);
      else await apiManagementApi.enable(config.apiConfigId);
      await load();
      updateToastSuccess(toastId, enabling ? 'Đã bật cấu hình API.' : 'Đã tắt cấu hình API.');
    } catch (e: any) {
      updateToastError(toastId, e, enabling ? 'Không thể bật cấu hình API.' : 'Không thể tắt cấu hình API.');
    } finally {
      setStatusBusy(null);
    }
  };

  const saveQuota = async () => {
    if (!panelTarget || !quotaEdit.trim()) return;
    const limit = Number(quotaEdit);
    if (!Number.isFinite(limit) || limit <= 0) {
      showMessageErrorToast('Hạn mức phải là số lớn hơn 0.', 'api-quota');
      return;
    }
    const toastId = showLoadingToast('Đang cập nhật hạn mức sử dụng...', 'api-quota');
    try {
      await apiManagementApi.updateQuota(panelTarget.apiConfigId, limit);
      setQuotaEdit('');
      setQuotas(await apiManagementApi.getQuota(panelTarget.apiConfigId));
      await load();
      updateToastSuccess(toastId, 'Đã cập nhật hạn mức sử dụng.');
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể cập nhật hạn mức sử dụng.');
    }
  };

  return (
    <div className="w-full pb-12">
      {/* Breadcrumb */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Cấu hình API</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6">
        <h1 className="text-3xl font-bold text-[#004c91]">Cấu hình API tích hợp</h1>
        <p className="text-sm text-gray-500 mt-1">
          Quản lý cấu hình cloud API (Google Document AI — quét danh thiếp), test kết nối,
          hạn mức và nhật ký. Credential được mã hoá và không bao giờ hiển thị lại.
        </p>
      </div>

      {loading ? (
        <div className="py-20 text-center text-gray-400">
          <Loader2 className="w-6 h-6 animate-spin inline-block mr-2" /> Đang tải cấu hình...
        </div>
      ) : loadError ? (
        <div className="py-20 text-center">
          <p className="text-red-500 text-sm font-medium">{loadError}</p>
          <button onClick={() => void load()} className="mt-3 text-sm font-bold text-[#004c91] hover:underline cursor-pointer">Thử lại</button>
        </div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
          {integrations.length === 0 && (
            <div className="col-span-full bg-white rounded-2xl border border-dashed border-gray-300 p-10 text-center">
              <ScanLine className="w-8 h-8 text-gray-300 mx-auto mb-3" />
              <p className="text-sm text-gray-500 mb-4">Chưa có cấu hình API nào.</p>
              <button
                onClick={() => { setEditTarget(null); setFormOpen(true); }}
                className="bg-[#f37021] hover:bg-[#d9621a] text-white px-4 py-2 rounded-lg text-sm font-bold cursor-pointer"
              >
                Cấu hình Google Document AI
              </button>
            </div>
          )}
          {integrations.map((config) => (
            <div key={config.apiConfigId} className="bg-white rounded-2xl shadow-sm border border-gray-200 p-5">
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-[#e6eff7] flex items-center justify-center">
                    <ScanLine className="w-5 h-5 text-[#004c91]" />
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-gray-800">{config.name}</h3>
                    <p className="text-xs text-gray-400 font-medium">
                      {config.providerName || '—'} · {config.purpose || '—'}
                    </p>
                  </div>
                </div>
                <span className={`px-2.5 py-1 rounded-md text-xs font-bold ${
                  config.status === 'ACTIVE' ? 'bg-green-50 text-green-600'
                    : config.status === 'INACTIVE' ? 'bg-gray-100 text-gray-500'
                      : 'bg-red-50 text-red-500'
                }`}>
                  {config.status}
                </span>
              </div>

              <dl className="mt-4 grid grid-cols-2 gap-x-6 gap-y-2 text-sm">
                <div><dt className="text-xs text-gray-400 font-bold uppercase">Base URL</dt>
                  <dd className="text-gray-600 truncate">{config.baseUrl}</dd></div>
                <div><dt className="text-xs text-gray-400 font-bold uppercase">Project ID</dt>
                  <dd className="text-gray-600 truncate">{config.projectId || '—'}</dd></div>
                <div><dt className="text-xs text-gray-400 font-bold uppercase">Location</dt>
                  <dd className="text-gray-600">{config.location || '—'}</dd></div>
                <div><dt className="text-xs text-gray-400 font-bold uppercase">Processor</dt>
                  <dd className="text-gray-600 truncate">
                    {config.processorId ? `${config.processorId.slice(0, 6)}••••` : '—'}
                  </dd></div>
                <div><dt className="text-xs text-gray-400 font-bold uppercase">Credential</dt>
                  <dd className={`font-bold ${config.hasCredential || config.secretRef ? 'text-green-600' : 'text-red-500'}`}>
                    <span className="inline-flex items-center gap-1">
                      <KeyRound className="w-3.5 h-3.5" />
                      {config.hasCredential ? 'Đã cấu hình (DB, mã hoá)' : config.secretRef ? `Secret ref: ${config.secretRef}` : 'Chưa có'}
                    </span>
                  </dd></div>
                <div><dt className="text-xs text-gray-400 font-bold uppercase">Giới hạn</dt>
                  <dd className="text-gray-600">{config.rateLimitPerMinute ?? '—'}/phút · {config.monthlyQuota ?? '—'}/tháng</dd></div>
                <div className="col-span-2"><dt className="text-xs text-gray-400 font-bold uppercase">Test gần nhất</dt>
                  <dd className={`text-sm ${config.lastTestStatus === 'SUCCESS' ? 'text-green-600' : config.lastTestStatus === 'FAILED' ? 'text-red-500' : 'text-gray-400'}`}>
                    {config.lastTestStatus
                      ? `${config.lastTestStatus} · ${config.lastTestedAt ? new Date(config.lastTestedAt).toLocaleString('vi-VN') : ''}`
                      : 'Chưa test'}
                    {config.lastTestMessage && <span className="block text-xs text-gray-400 truncate">{config.lastTestMessage}</span>}
                  </dd></div>
              </dl>

              <div className="mt-4 pt-4 border-t border-gray-100 flex flex-wrap items-center gap-2">
                <button
                  onClick={() => { setEditTarget(config); setFormOpen(true); }}
                  className="px-3 py-1.5 rounded-lg text-sm font-bold text-[#004c91] border border-[#004c91] hover:bg-[#e6eff7] transition-colors flex items-center gap-1.5 cursor-pointer"
                >
                  <Settings2 className="w-4 h-4" /> Chỉnh sửa
                </button>
                <button
                  onClick={() => void test(config)}
                  disabled={testBusy === config.apiConfigId}
                  className="px-3 py-1.5 rounded-lg text-sm font-bold text-gray-600 border border-gray-300 hover:bg-gray-50 transition-colors flex items-center gap-1.5 disabled:opacity-50 cursor-pointer"
                >
                  {testBusy === config.apiConfigId
                    ? <Loader2 className="w-4 h-4 animate-spin" />
                    : <RefreshCw className="w-4 h-4" />}
                  Test kết nối
                </button>
                <button
                  onClick={() => void toggleStatus(config)}
                  disabled={statusBusy === config.apiConfigId}
                  className={`px-3 py-1.5 rounded-lg text-sm font-bold border transition-colors flex items-center gap-1.5 disabled:opacity-50 cursor-pointer ${
                    config.status === 'ACTIVE'
                      ? 'text-red-500 border-red-200 hover:bg-red-50'
                      : 'text-green-600 border-green-200 hover:bg-green-50'
                  }`}
                  title={config.status !== 'ACTIVE' && config.lastTestStatus !== 'SUCCESS'
                    ? 'Cần test kết nối thành công trước khi kích hoạt' : undefined}
                >
                  {config.status === 'ACTIVE' ? <PowerOff className="w-4 h-4" /> : <Power className="w-4 h-4" />}
                  {config.status === 'ACTIVE' ? 'Tắt' : 'Kích hoạt'}
                </button>
                <button
                  onClick={() => { setPanelTarget(config); setPanelTab('QUOTA'); setLogsPage(1); }}
                  className="px-3 py-1.5 rounded-lg text-sm font-bold text-gray-600 border border-gray-300 hover:bg-gray-50 transition-colors flex items-center gap-1.5 cursor-pointer"
                >
                  <Activity className="w-4 h-4" /> Hạn mức & Nhật ký
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Config form modal */}
      {formOpen && (
        <GoogleDocumentAiConfigForm
          config={editTarget}
          onClose={() => setFormOpen(false)}
          onSaved={async () => { setFormOpen(false); await load(); }}
        />
      )}

      {/* Quota + logs panel */}
      {panelTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-3xl max-h-[85vh] overflow-y-auto p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-bold text-gray-800">{panelTarget.name}</h3>
              <button onClick={() => setPanelTarget(null)} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 cursor-pointer">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="flex items-center gap-1 border-b border-gray-200 mb-4">
              {(['QUOTA', 'LOGS'] as const).map((key) => (
                <button key={key} onClick={() => setPanelTab(key)}
                  className={`px-4 py-2 text-sm font-bold border-b-2 -mb-px cursor-pointer ${
                    panelTab === key ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-gray-400'
                  }`}>
                  {key === 'QUOTA' ? 'Hạn mức tháng' : 'Nhật ký gọi API'}
                </button>
              ))}
            </div>

            {panelTab === 'QUOTA' ? (
              <div>
                <div className="flex items-end gap-2 mb-4">
                  <div>
                    <label className={labelCls}>Hạn mức GLOBAL tháng hiện tại</label>
                    <input className={`${inputCls} w-44`} type="number" min={1}
                      placeholder="VD: 1000" value={quotaEdit} onChange={(e) => setQuotaEdit(e.target.value)} />
                  </div>
                  <button onClick={() => void saveQuota()} disabled={!quotaEdit.trim()}
                    className="bg-[#004c91] hover:bg-[#003a70] text-white px-4 py-2 rounded-lg text-sm font-bold disabled:opacity-50 cursor-pointer">
                    Cập nhật
                  </button>
                </div>
                <table className="w-full text-left">
                  <thead>
                    <tr className="bg-slate-50 text-[11px] uppercase text-gray-500">
                      <th className="px-4 py-2.5 font-bold">Kỳ</th>
                      <th className="px-4 py-2.5 font-bold">Phạm vi</th>
                      <th className="px-4 py-2.5 font-bold text-right">Đã dùng</th>
                      <th className="px-4 py-2.5 font-bold text-right">Hạn mức</th>
                      <th className="px-4 py-2.5 font-bold">Lần dùng cuối</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {quotas.length === 0 ? (
                      <tr><td colSpan={5} className="px-4 py-8 text-center text-gray-400 text-sm">Chưa có dữ liệu hạn mức</td></tr>
                    ) : quotas.map((q) => (
                      <tr key={q.apiUsageQuotaId}>
                        <td className="px-4 py-2.5 text-sm font-bold text-gray-700">{q.periodYyyymm}</td>
                        <td className="px-4 py-2.5 text-sm text-gray-600">{q.campusScopeKey}</td>
                        <td className="px-4 py-2.5 text-sm text-right font-bold text-[#004c91]">{q.usedCount}</td>
                        <td className="px-4 py-2.5 text-sm text-right text-gray-600">{q.monthlyLimit}</td>
                        <td className="px-4 py-2.5 text-xs text-gray-400">
                          {q.lastUsedAt ? new Date(q.lastUsedAt).toLocaleString('vi-VN') : '—'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div>
                <table className="w-full text-left">
                  <thead>
                    <tr className="bg-slate-50 text-[11px] uppercase text-gray-500">
                      <th className="px-4 py-2.5 font-bold">Thời gian</th>
                      <th className="px-4 py-2.5 font-bold">Endpoint</th>
                      <th className="px-4 py-2.5 font-bold text-center">Kết quả</th>
                      <th className="px-4 py-2.5 font-bold text-right">ms</th>
                      <th className="px-4 py-2.5 font-bold">Người gọi</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {!logs || logs.items.length === 0 ? (
                      <tr><td colSpan={5} className="px-4 py-8 text-center text-gray-400 text-sm">Chưa có nhật ký</td></tr>
                    ) : logs.items.map((log) => (
                      <tr key={log.apiRequestLogId}>
                        <td className="px-4 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                          {new Date(log.createdAt).toLocaleString('vi-VN')}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-gray-600 truncate max-w-[220px]" title={log.endpoint}>
                          {log.method} {log.endpoint}
                        </td>
                        <td className="px-4 py-2.5 text-center">
                          {log.success
                            ? <span className="text-green-600 text-xs font-bold">OK</span>
                            : <span className="text-red-500 text-xs font-bold" title={log.errorMessage || undefined}>
                                {log.errorCode || 'FAILED'}
                              </span>}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-right text-gray-500">{log.responseTimeMs ?? '—'}</td>
                        <td className="px-4 py-2.5 text-xs text-gray-500">{log.requestedByName || '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {logs && logs.totalCount > logs.pageSize && (
                  <div className="mt-3 flex justify-end gap-2 text-sm">
                    <button disabled={logsPage <= 1} onClick={() => setLogsPage((p) => p - 1)}
                      className="px-3 py-1 rounded-lg border border-gray-200 disabled:opacity-40 cursor-pointer">Trước</button>
                    <button
                      disabled={logsPage >= Math.ceil(logs.totalCount / logs.pageSize)}
                      onClick={() => setLogsPage((p) => p + 1)}
                      className="px-3 py-1 rounded-lg border border-gray-200 disabled:opacity-40 cursor-pointer">Sau</button>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

/** Form cấu hình Google Document AI — secret chỉ ghi, không bao giờ đọc lại. */
function GoogleDocumentAiConfigForm({
  config, onClose, onSaved,
}: {
  config: ApiIntegration | null;
  onClose: () => void;
  onSaved: () => Promise<void> | void;
}) {
  const [name, setName] = useState(config?.name ?? 'Google Document AI - Business Card OCR');
  const [projectId, setProjectId] = useState(config?.projectId ?? '');
  const [location, setLocation] = useState(config?.location ?? 'us');
  const [processorId, setProcessorId] = useState(config?.processorId ?? '');
  const [endpoint, setEndpoint] = useState(config?.endpoint ?? 'us-documentai.googleapis.com');
  const [serviceAccountJson, setServiceAccountJson] = useState('');
  const [replaceCredential, setReplaceCredential] = useState(!config?.hasCredential);
  const [secretRef, setSecretRef] = useState(config?.secretRef ?? '');
  const [rateLimit, setRateLimit] = useState(String(config?.rateLimitPerMinute ?? 20));
  const [monthlyQuota, setMonthlyQuota] = useState(String(config?.monthlyQuota ?? 1000));
  const [timeoutSeconds, setTimeoutSeconds] = useState(String(config?.timeoutSeconds ?? 60));
  const [retentionDays, setRetentionDays] = useState(String(config?.retentionDays ?? 30));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    const toastId = showLoadingToast('Đang lưu cấu hình API...', 'api-config-save');
    try {
      const payload: UpsertGoogleDocumentAiOcrConfigRequest = {
        name: name.trim(),
        projectId: projectId.trim(),
        location: location.trim(),
        processorId: processorId.trim(),
        endpoint: endpoint.trim(),
        serviceAccountJson: replaceCredential && serviceAccountJson.trim() ? serviceAccountJson : null,
        secretRef: secretRef.trim() || null,
        rateLimitPerMinute: Number(rateLimit) || 20,
        monthlyQuota: Number(monthlyQuota) || 1000,
        timeoutSeconds: Number(timeoutSeconds) || 60,
        retentionDays: Number(retentionDays) || 30,
      };
      if (config) await apiManagementApi.updateConfig(config.apiConfigId, payload);
      else await apiManagementApi.upsertGoogleDocumentAiConfig(payload);
      updateToastSuccess(toastId, 'Đã lưu cấu hình API thành công.');
      await onSaved();
    } catch (err: any) {
      // Backend trả lỗi thiếu credential → hiển thị thông báo chuẩn, rõ ràng.
      const backendMsg = getApiErrorMessage(err, 'Không thể lưu cấu hình API. Vui lòng kiểm tra lại thông tin.');
      const finalMsg = /credential|service\s*account|secret\s*ref|chưa cấu hình/i.test(backendMsg)
        ? 'Chưa cấu hình credential. Vui lòng nhập Service Account JSON hoặc Secret Ref.'
        : backendMsg;
      setError(finalMsg);
      updateToastMessageError(toastId, finalMsg);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <form onSubmit={submit} className="bg-white rounded-2xl shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto p-6">
        <div className="flex items-center justify-between mb-5">
          <h3 className="text-lg font-bold text-gray-800">
            {config ? 'Chỉnh sửa cấu hình Google Document AI' : 'Cấu hình Google Document AI'}
          </h3>
          <button type="button" onClick={onClose} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 cursor-pointer">
            <X className="w-5 h-5" />
          </button>
        </div>

        {error && (
          <div className="mb-4 bg-red-50 border border-red-100 text-red-600 text-sm rounded-lg px-3 py-2.5">{error}</div>
        )}

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="md:col-span-2">
            <label className={labelCls}>Tên cấu hình *</label>
            <input className={inputCls} value={name} onChange={(e) => setName(e.target.value)} required maxLength={150} />
          </div>
          <div>
            <label className={labelCls}>Project ID *</label>
            <input className={inputCls} value={projectId} onChange={(e) => setProjectId(e.target.value)} required />
          </div>
          <div>
            <label className={labelCls}>Location *</label>
            <select className={inputCls} value={location} onChange={(e) => setLocation(e.target.value)}>
              <option value="us">us</option>
              <option value="eu">eu</option>
            </select>
          </div>
          <div>
            <label className={labelCls}>Processor ID *</label>
            <input className={inputCls} value={processorId} onChange={(e) => setProcessorId(e.target.value)} required />
          </div>
          <div>
            <label className={labelCls}>Endpoint *</label>
            <input className={inputCls} value={endpoint} onChange={(e) => setEndpoint(e.target.value)} required
              placeholder="us-documentai.googleapis.com" />
          </div>

          <div className="md:col-span-2 border-t border-gray-100 pt-4">
            <label className={labelCls}>Service Account JSON</label>
            {config?.hasCredential && !replaceCredential ? (
              <div className="flex items-center justify-between bg-slate-50 border border-gray-200 rounded-lg px-3 py-2.5">
                <span className="text-sm font-mono text-gray-400">••••••••••••••••••••••••</span>
                <button type="button" onClick={() => setReplaceCredential(true)}
                  className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">
                  Thay credential
                </button>
              </div>
            ) : (
              <textarea
                className={`${inputCls} font-mono text-xs`}
                rows={5}
                value={serviceAccountJson}
                onChange={(e) => setServiceAccountJson(e.target.value)}
                placeholder='Dán nội dung file JSON service account ({"type":"service_account",...}) — sẽ được mã hoá, không hiển thị lại.'
              />
            )}
            <p className="mt-1 text-xs text-gray-400">
              Hoặc dùng Secret Ref (biến môi trường trên server) thay vì lưu JSON vào DB.
            </p>
          </div>
          <div className="md:col-span-2">
            <label className={labelCls}>Secret Ref (tuỳ chọn)</label>
            <input className={inputCls} value={secretRef} onChange={(e) => setSecretRef(e.target.value)}
              placeholder="GOOGLE_DOCUMENT_AI_SERVICE_ACCOUNT" />
          </div>

          <div>
            <label className={labelCls}>Rate limit / phút *</label>
            <input className={inputCls} type="number" min={1} value={rateLimit} onChange={(e) => setRateLimit(e.target.value)} required />
          </div>
          <div>
            <label className={labelCls}>Hạn mức tháng *</label>
            <input className={inputCls} type="number" min={1} value={monthlyQuota} onChange={(e) => setMonthlyQuota(e.target.value)} required />
          </div>
          <div>
            <label className={labelCls}>Timeout (giây, 5–120) *</label>
            <input className={inputCls} type="number" min={5} max={120} value={timeoutSeconds} onChange={(e) => setTimeoutSeconds(e.target.value)} required />
          </div>
          <div>
            <label className={labelCls}>Lưu OCR thô (ngày, 1–365) *</label>
            <input className={inputCls} type="number" min={1} max={365} value={retentionDays} onChange={(e) => setRetentionDays(e.target.value)} required />
          </div>
        </div>

        <div className="mt-6 flex justify-end gap-2">
          <button type="button" onClick={onClose} disabled={busy}
            className="px-4 py-2 rounded-lg text-sm font-bold text-gray-500 hover:bg-gray-100 transition-colors cursor-pointer">
            Huỷ
          </button>
          <button type="submit" disabled={busy}
            className="bg-[#004c91] hover:bg-[#003a70] text-white px-5 py-2 rounded-lg text-sm font-bold transition-colors disabled:opacity-50 cursor-pointer flex items-center gap-2">
            {busy && <Loader2 className="w-4 h-4 animate-spin" />}
            Lưu cấu hình
          </button>
        </div>
      </form>
    </div>
  );
}
