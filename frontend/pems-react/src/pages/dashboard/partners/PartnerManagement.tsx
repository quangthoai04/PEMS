/**
 * PartnerManagement — danh sách đối tác chạy DB thật (docs/PARTNER_canh/01 §10.1).
 * Backend quyết định scope + hành động qua allowedActions/canCreate; UI chỉ render.
 */
import React, { useCallback, useEffect, useState } from 'react';
import {
  Search, Plus, Eye, Check, X, ChevronLeft, ChevronRight, Globe2, Loader2,
} from 'lucide-react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import { partnersApi } from '../../../features/partners/api/partnersApi';
import type {
  PartnerListItem,
  PartnerListResponse,
} from '../../../features/partners/types/partners.types';
import {
  PROFILE_STATUS_LABELS,
  VISIBILITY_LABELS,
} from '../../../features/partners/types/partners.types';
import { useDebounce } from '../../../shared/hooks/useDebounce';
import {
  showLoadingToast,
  updateToastSuccess,
  updateToastError,
} from '../../../shared/utils/toast';

interface ActiveCampus {
  campusId: number;
  campusCode: string;
  campusName: string;
}

function StatusBadge({ status }: { status: PartnerListItem['profileStatus'] }) {
  const label = PROFILE_STATUS_LABELS[status] ?? status;
  const cls =
    status === 'APPROVED' ? 'text-[#22c55e] bg-green-50'
      : status === 'PENDING_APPROVAL' ? 'text-[#eab308] bg-yellow-50'
        : status === 'REJECTED' ? 'text-[#ef4444] bg-red-50'
          : 'text-gray-500 bg-gray-100';
  return (
    <span className={`${cls} px-3 py-1 rounded-md text-[13px] font-bold tracking-wide whitespace-nowrap`}>
      {label}
    </span>
  );
}

export function PartnerManagement() {
  const navigate = useNavigate();

  // Đến từ 1 thông báo cụ thể (?partnerId=...): chỉ hiển thị đúng hồ sơ đó thay vì cả danh
  // sách. Nút "Xem tất cả" (hoặc đổi filter/tìm kiếm) xoá param này để xem lại toàn bộ.
  const [searchParams, setSearchParams] = useSearchParams();
  const notificationPartnerId = searchParams.get('partnerId') || '';

  const [data, setData] = useState<PartnerListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const debouncedSearch = useDebounce(searchQuery, 400);
  const [countryFilter, setCountryFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [campusFilter, setCampusFilter] = useState('');
  const [campuses, setCampuses] = useState<ActiveCampus[]>([]);
  const [countries, setCountries] = useState<string[]>([]);

  // Reject modal
  const [rejectTarget, setRejectTarget] = useState<PartnerListItem | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [actionBusy, setActionBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const res = await partnersApi.getPartners({
        search: debouncedSearch || undefined,
        country: countryFilter || undefined,
        campusId: campusFilter ? Number(campusFilter) : undefined,
        profileStatus: statusFilter || undefined,
        partnerId: notificationPartnerId ? Number(notificationPartnerId) : undefined,
        page,
        pageSize,
      });
      setData(res);
      // Country filter options are collected from data already loaded (DB-driven).
      setCountries((prev) => {
        const merged = new Set(prev);
        res.items.forEach((i) => { if (i.country) merged.add(i.country); });
        return Array.from(merged).sort();
      });
    } catch (e: any) {
      setLoadError(e?.response?.data?.message || 'Không tải được danh sách đối tác.');
    } finally {
      setLoading(false);
    }
  }, [debouncedSearch, countryFilter, campusFilter, statusFilter, notificationPartnerId, page, pageSize]);

  useEffect(() => { void load(); }, [load]);

  const clearNotificationFilter = () => {
    const params = new URLSearchParams(searchParams);
    params.delete('partnerId');
    setSearchParams(params, { replace: true });
    setPage(1);
  };

  useEffect(() => {
    httpClient
      .get<ActiveCampus[]>(API_ENDPOINTS.campuses.active)
      .then((res) => setCampuses(res.data))
      .catch(() => setCampuses([]));
  }, []);

  useEffect(() => { setPage(1); }, [debouncedSearch, countryFilter, campusFilter, statusFilter, notificationPartnerId, pageSize]);

  const approve = async (item: PartnerListItem) => {
    setActionBusy(true);
    const toastId = showLoadingToast('Đang duyệt hồ sơ đối tác...', `partner-approve-${item.partnerId}`);
    try {
      await partnersApi.approvePartner(item.partnerId);
      await load();
      updateToastSuccess(toastId, 'Đã duyệt hồ sơ đối tác.');
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể duyệt hồ sơ đối tác.');
    } finally {
      setActionBusy(false);
    }
  };

  const submitReject = async () => {
    if (!rejectTarget || !rejectReason.trim()) return;
    setActionBusy(true);
    const toastId = showLoadingToast('Đang từ chối hồ sơ đối tác...', `partner-reject-${rejectTarget.partnerId}`);
    try {
      await partnersApi.rejectPartner(rejectTarget.partnerId, rejectReason.trim());
      setRejectTarget(null);
      setRejectReason('');
      await load();
      updateToastSuccess(toastId, 'Đã từ chối hồ sơ đối tác.');
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể từ chối hồ sơ đối tác.');
    } finally {
      setActionBusy(false);
    }
  };

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / pageSize)) : 1;

  return (
    <div className="w-full pb-12">
      {/* Breadcrumb */}
      <div className="mb-2 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors cursor-pointer">
          Dashboard
        </button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Quản lý đối tác</span>
      </div>

      <div className="border-b border-gray-100 pb-3 mb-4 flex justify-between items-center">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý đối tác</h1>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3 mb-4">
        <div className="relative flex-1 min-w-[250px] max-w-md">
          <Search className="w-4 h-4 text-gray-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            placeholder="Tìm kiếm theo mã hoặc tên đối tác..."
            value={searchQuery}
            onChange={(e) => { setSearchQuery(e.target.value); if (notificationPartnerId) clearNotificationFilter(); }}
            className="w-full pl-10 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white shadow-sm"
          />
        </div>

        <select
          value={countryFilter}
          onChange={(e) => { setCountryFilter(e.target.value); if (notificationPartnerId) clearNotificationFilter(); }}
          className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] text-gray-600 bg-white font-normal shadow-sm cursor-pointer min-w-[150px]"
        >
          <option value="">Tất cả quốc gia</option>
          {countries.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>

        <select
          value={campusFilter}
          onChange={(e) => { setCampusFilter(e.target.value); if (notificationPartnerId) clearNotificationFilter(); }}
          className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] text-gray-600 bg-white font-normal shadow-sm cursor-pointer min-w-[150px]"
        >
          <option value="">Tất cả cơ sở</option>
          {campuses.map((c) => (
            <option key={c.campusId} value={c.campusId}>{c.campusName}</option>
          ))}
        </select>

        <select
          value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value); if (notificationPartnerId) clearNotificationFilter(); }}
          className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] text-gray-600 bg-white font-normal shadow-sm cursor-pointer min-w-[150px]"
        >
          <option value="">Tất cả trạng thái</option>
          <option value="PENDING_APPROVAL">Chờ duyệt</option>
          <option value="APPROVED">Đã duyệt</option>
          <option value="REJECTED">Từ chối</option>
          <option value="DRAFT">Bản nháp</option>
        </select>

        {data?.canCreate && (
          <button
            onClick={() => navigate('/dashboard/partners/create')}
            className="ml-auto bg-[#f37021] hover:bg-[#d9621a] text-white px-4 py-2 rounded-md text-sm font-bold flex items-center gap-1.5 transition-colors shadow-sm cursor-pointer tracking-wide"
          >
            <Plus className="w-4 h-4 flex-shrink-0" /> Thêm mới đối tác
          </button>
        )}
      </div>

      {notificationPartnerId && (
        <div className="flex items-center justify-between gap-3 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm mb-4">
          <span className="font-normal text-blue-800">
            Đang hiển thị đúng đối tác từ thông báo bạn vừa bấm.
          </span>
          <button
            onClick={clearNotificationFilter}
            className="shrink-0 rounded-lg border border-blue-300 bg-white px-3 py-1.5 text-xs font-bold text-blue-700 hover:bg-blue-100 transition-colors"
          >
            Xem tất cả
          </button>
        </div>
      )}

      {/* Table */}
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden mb-2">
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-left">
            <thead>
              <tr className="bg-[#004c91] text-white text-[12px] tracking-wide uppercase whitespace-nowrap text-center">
                <th className="p-3 font-bold w-[50px]">STT</th>
                <th className="p-3 font-bold text-left pl-6">Mã đối tác</th>
                <th className="p-3 font-bold text-left pl-6">Tên đối tác</th>
                <th className="p-3 font-bold text-left pl-6">Quốc gia</th>
                <th className="p-3 font-bold">Campus sở hữu</th>
                <th className="p-3 font-bold">Người tạo</th>
                <th className="p-3 font-bold w-[120px]">Trạng thái</th>
                <th className="p-3 font-bold w-[110px]">Hiển thị</th>
                <th className="p-3 font-bold w-[130px]">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {loading ? (
                <tr>
                  <td colSpan={9} className="py-14 text-center text-gray-400">
                    <Loader2 className="w-6 h-6 animate-spin inline-block mr-2" />
                    Đang tải danh sách đối tác...
                  </td>
                </tr>
              ) : loadError ? (
                <tr>
                  <td colSpan={9} className="py-14 text-center">
                    <p className="text-red-500 text-sm font-normal">{loadError}</p>
                    <button onClick={() => void load()} className="mt-3 text-sm font-bold text-[#004c91] hover:underline cursor-pointer">
                      Thử lại
                    </button>
                  </td>
                </tr>
              ) : data && data.items.length > 0 ? (
                data.items.map((item, index) => (
                  <tr key={item.partnerId} className="hover:bg-gray-50/80 transition-colors text-center">
                    <td className="p-3 align-middle text-sm text-gray-600 font-medium">
                      {(data.page - 1) * data.pageSize + index + 1}
                    </td>
                    <td className="p-3 align-middle pl-6 text-sm font-bold text-gray-800 whitespace-nowrap text-left">
                      {item.partnerCode || '—'}
                    </td>
                    <td className="p-3 align-middle pl-6 text-sm text-gray-700 text-left">
                      <div className="flex items-center gap-2">
                        <Globe2 className="w-4 h-4 text-gray-300 flex-shrink-0" />
                        <span className="font-medium">{item.name}</span>
                      </div>
                    </td>


                    <td className="p-3 align-middle pl-6 text-sm text-gray-600 whitespace-nowrap text-left">
                      {item.country || '—'}
                    </td>
                    <td className="p-3 align-middle text-sm text-gray-600 whitespace-nowrap">
                      {item.ownerCampusName}
                    </td>
                    <td className="p-3 align-middle text-sm font-normal text-gray-700 whitespace-nowrap">
                      {item.creatorName || '—'}
                    </td>
                    <td className="p-3 align-middle"><StatusBadge status={item.profileStatus} /></td>
                    <td className="p-3 align-middle text-xs font-normal text-gray-500">
                      {VISIBILITY_LABELS[item.visibility] ?? item.visibility}
                    </td>
                    <td className="p-3 align-middle">
                      <div className="flex items-center justify-center gap-1">
                        <button
                          onClick={() => navigate(`/dashboard/partners/${item.partnerId}`)}
                          className="p-1.5 rounded-lg text-gray-400 hover:bg-[#e6eff7] hover:text-[#004c91] transition-colors cursor-pointer"
                          title="Xem chi tiết"
                        >
                          <Eye className="w-[16px] h-[16px]" />
                        </button>
                        {item.allowedActions.includes('APPROVE') && (
                          <button
                            onClick={() => void approve(item)}
                            disabled={actionBusy}
                            className="p-1.5 rounded-lg text-gray-400 hover:bg-[#eaffe4] hover:text-[#0aa14f] transition-colors disabled:opacity-40 cursor-pointer"
                            title="Duyệt"
                          >
                            <Check className="w-[18px] h-[18px] stroke-[2.5]" />
                          </button>
                        )}
                        {item.allowedActions.includes('REJECT') && (
                          <button
                            onClick={() => { setRejectTarget(item); setRejectReason(''); }}
                            disabled={actionBusy}
                            className="p-1.5 rounded-lg text-gray-400 hover:bg-red-50 hover:text-red-600 transition-colors disabled:opacity-40 cursor-pointer"
                            title="Từ chối"
                          >
                            <X className="w-[18px] h-[18px] stroke-[2.5]" />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={9} className="py-12 text-center text-gray-500 bg-white">
                    Không tìm thấy đối tác nào
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Pagination */}
      <div className="flex items-center justify-between mt-6">
        <div className="flex items-center gap-3 text-sm text-gray-600 font-normal">
          <span>Hiển thị</span>
          <select
            value={pageSize}
            onChange={(e) => setPageSize(Number(e.target.value))}
            className="border border-gray-300 bg-white rounded-lg px-2 py-1 outline-none focus:border-[#004c91] cursor-pointer text-gray-700"
          >
            {[5, 10, 20, 50].map((n) => <option key={n} value={n}>{n}</option>)}
          </select>
          <span>bản ghi / trang · Tổng {data?.totalCount ?? 0}</span>
        </div>

        <div className="flex items-center gap-1.5">
          <button
            className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 bg-white shadow-sm cursor-pointer"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page <= 1}
          >
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="px-3 text-sm font-normal text-gray-600">{page} / {totalPages}</span>
          <button
            className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 bg-white shadow-sm cursor-pointer"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={page >= totalPages}
          >
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Reject modal — reason mandatory */}
      {rejectTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
            <h3 className="text-lg font-bold text-gray-800 mb-1">Từ chối đối tác</h3>
            <p className="text-sm text-gray-500 mb-4">
              Từ chối hồ sơ <span className="font-bold text-gray-700">{rejectTarget.name}</span>.
              Lý do là bắt buộc.
            </p>
            <textarea
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              rows={4}
              placeholder="Nhập lý do từ chối..."
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700"
            />
            <div className="mt-4 flex justify-end gap-2">
              <button
                onClick={() => setRejectTarget(null)}
                disabled={actionBusy}
                className="px-4 py-2 rounded-lg text-sm font-bold text-gray-500 hover:bg-gray-100 transition-colors cursor-pointer"
              >
                Huỷ
              </button>
              <button
                onClick={() => void submitReject()}
                disabled={!rejectReason.trim() || actionBusy}
                className="px-4 py-2 rounded-lg text-sm font-bold text-white bg-red-500 hover:bg-red-600 transition-colors disabled:opacity-50 cursor-pointer"
              >
                {actionBusy ? 'Đang xử lý...' : 'Từ chối'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
