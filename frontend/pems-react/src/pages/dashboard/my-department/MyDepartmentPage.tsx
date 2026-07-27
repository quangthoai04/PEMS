/**
 * Trang "Phòng ban của tôi" — quản lý nhân sự dành cho Trưởng phòng ban (DEPARTMENT + LEADER).
 *
 * Route: /dashboard/my-department. Deliberately NOT /dashboard/departments/:id — the department is
 * resolved from the signed-in Leader server-side, so there is no id in the URL for anyone to swap.
 *
 * Nothing on this page is hard-coded: department name, campus, current leader and every statistic
 * come from the API, and every action's availability comes from backend-issued flags rather than a
 * role string read out of local storage.
 */

import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import {
  Building2,
  ChevronLeft,
  ChevronRight,
  Crown,
  Eye,
  Loader2,
  MapPin,
  Plus,
  Search,
  UserCheck,
  Users,
} from 'lucide-react';

import { departmentLeaderPersonnelApi } from '../../../features/department-leader-personnel/api/departmentLeaderPersonnelApi';
import {
  getDepartmentLeaderErrorMessage,
  isDepartmentLeaderScopeLost,
} from '../../../features/department-leader-personnel/api/departmentLeaderError';
import { useMyDepartmentPersonnel } from '../../../features/department-leader-personnel/hooks/useMyDepartmentPersonnel';
import {
  STATUS_FILTER_OPTIONS,
  type LeaderCandidates,
  type PersonnelDetail,
  type PersonnelGender,
  type PersonnelListItem,
  type PersonnelStatusFilter,
  type PersonnelStatusImpact,
} from '../../../features/department-leader-personnel/types/departmentLeaderPersonnel.types';
import { PersonnelDetailModal } from '../../../features/department-leader-personnel/components/PersonnelDetailModal';
import { PersonnelFormModal } from '../../../features/department-leader-personnel/components/PersonnelFormModal';
import { PersonnelStatusBadge } from '../../../features/department-leader-personnel/components/PersonnelStatusBadge';
import { StatusImpactModal } from '../../../features/department-leader-personnel/components/StatusImpactModal';
import { TransferLeadershipModal } from '../../../features/department-leader-personnel/components/TransferLeadershipModal';
import { useAuthContext } from '../../../shared/auth/AuthContext';

const PAGE_SIZE_OPTIONS = [5, 10, 20, 50];

export function MyDepartmentPage() {
  const navigate = useNavigate();
  const { logout } = useAuthContext();

  const list = useMyDepartmentPersonnel();

  // ── Modal state ──
  const [formOpen, setFormOpen] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [formSubmitting, setFormSubmitting] = useState(false);

  const [detailOpen, setDetailOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detail, setDetail] = useState<PersonnelDetail | null>(null);
  const [resending, setResending] = useState(false);

  const [impactOpen, setImpactOpen] = useState(false);
  const [impactLoading, setImpactLoading] = useState(false);
  const [impactError, setImpactError] = useState<string | null>(null);
  const [impact, setImpact] = useState<PersonnelStatusImpact | null>(null);
  const [impactTarget, setImpactTarget] = useState<PersonnelListItem | PersonnelDetail | null>(null);
  const [statusSubmitting, setStatusSubmitting] = useState(false);

  const [transferOpen, setTransferOpen] = useState(false);
  const [transferLoading, setTransferLoading] = useState(false);
  const [transferError, setTransferError] = useState<string | null>(null);
  const [candidates, setCandidates] = useState<LeaderCandidates | null>(null);
  const [transferPreselect, setTransferPreselect] = useState<number | null>(null);
  const [transferSubmitting, setTransferSubmitting] = useState(false);

  // A Leader who lost their role mid-session cannot recover this page — send them to sign in again
  // rather than looping on 403s.
  useEffect(() => {
    if (!list.scopeLost) return;
    toast.error('Bạn không còn quyền quản lý phòng ban. Vui lòng đăng nhập lại.');
    void logout().finally(() => navigate('/login', { replace: true }));
  }, [list.scopeLost, logout, navigate]);

  // ── Detail ──────────────────────────────────────────────────────────────
  const openDetail = useCallback(async (userId: number) => {
    setDetailOpen(true);
    setDetailLoading(true);
    setDetailError(null);
    setDetail(null);
    try {
      // Always the real API — a list row is a summary and is not a sufficient source for the modal.
      setDetail(await departmentLeaderPersonnelApi.getPersonnelDetail(userId));
    } catch (error) {
      setDetailError(getDepartmentLeaderErrorMessage(error, 'Không tải được thông tin nhân sự.'));
    } finally {
      setDetailLoading(false);
    }
  }, []);

  // ── Create / edit ───────────────────────────────────────────────────────
  const openCreate = () => {
    setFormMode('create');
    setDetail(null);
    setFormOpen(true);
  };

  const openEdit = () => {
    if (!detail) return;
    setFormMode('edit');
    setDetailOpen(false);
    setFormOpen(true);
  };

  const handleFormSubmit = async (values: {
    fullName: string;
    email: string;
    phone: string;
    gender: PersonnelGender;
  }) => {
    setFormSubmitting(true);
    try {
      if (formMode === 'create') {
        const result = await departmentLeaderPersonnelApi.createPersonnel(values);

        // Truthful reporting: the account exists, but the confirmation email may not have gone out.
        if (result.emailNotificationStatus === 'SENT') {
          toast.success(result.message);
        } else {
          toast(result.message, { icon: '⚠️', duration: 6000 });
        }
      } else {
        if (!detail) return;
        const result = await departmentLeaderPersonnelApi.updatePersonnel(detail.userId, values);

        // A 200 does not mean something changed, and it does not mean the emails were delivered.
        if (!result.changed) {
          toast(result.message, { icon: 'ℹ️' });
        } else if (
          result.emailChanged &&
          result.emailNotificationStatus !== 'SENT' &&
          result.emailNotificationStatus !== 'NOT_REQUIRED'
        ) {
          toast(result.message, { icon: '⚠️', duration: 7000 });
        } else {
          toast.success(result.message);
        }
      }

      setFormOpen(false);
      await list.refreshAll();
    } catch (error) {
      if (isDepartmentLeaderScopeLost(error)) {
        toast.error(getDepartmentLeaderErrorMessage(error));
        return;
      }
      // The modal stays open so the operator can correct the field and retry.
      toast.error(getDepartmentLeaderErrorMessage(error));
    } finally {
      setFormSubmitting(false);
    }
  };

  // ── Resend confirmation ─────────────────────────────────────────────────
  const handleResend = async () => {
    if (!detail) return;
    setResending(true);
    try {
      const result = await departmentLeaderPersonnelApi.resendEmailConfirmation(detail.userId);
      if (result.emailNotificationStatus === 'SENT') toast.success(result.message);
      else toast(result.message, { icon: '⚠️', duration: 6000 });
    } catch (error) {
      toast.error(getDepartmentLeaderErrorMessage(error));
    } finally {
      setResending(false);
    }
  };

  // ── Status change (preview → confirm) ───────────────────────────────────
  const openStatusImpact = useCallback(
    async (target: PersonnelListItem | PersonnelDetail, targetStatus: 'ACTIVE' | 'INACTIVE') => {
      setImpactTarget(target);
      setImpactOpen(true);
      setDetailOpen(false);
      setImpactLoading(true);
      setImpactError(null);
      setImpact(null);
      try {
        setImpact(await departmentLeaderPersonnelApi.getStatusImpact(target.userId, targetStatus));
      } catch (error) {
        setImpactError(getDepartmentLeaderErrorMessage(error, 'Không kiểm tra được ảnh hưởng.'));
      } finally {
        setImpactLoading(false);
      }
    },
    [],
  );

  const handleStatusConfirm = async (reason: string) => {
    if (!impact || !impactTarget) return;
    setStatusSubmitting(true);
    try {
      const result = await departmentLeaderPersonnelApi.changePersonnelStatus(impactTarget.userId, {
        targetStatus: impact.targetStatus as 'ACTIVE' | 'INACTIVE',
        reason: reason || undefined,
      });
      toast.success(result.message);
      setImpactOpen(false);
      await list.refreshAll();
    } catch (error) {
      toast.error(getDepartmentLeaderErrorMessage(error));
    } finally {
      setStatusSubmitting(false);
    }
  };

  // ── Leadership transfer ─────────────────────────────────────────────────
  const openTransfer = useCallback(async (preselect?: number | null) => {
    setTransferPreselect(preselect ?? null);
    setTransferOpen(true);
    setDetailOpen(false);
    setTransferLoading(true);
    setTransferError(null);
    setCandidates(null);
    try {
      // Its own endpoint — never the current page of the table.
      setCandidates(await departmentLeaderPersonnelApi.getLeaderCandidates());
    } catch (error) {
      setTransferError(getDepartmentLeaderErrorMessage(error, 'Không tải được danh sách ứng viên.'));
    } finally {
      setTransferLoading(false);
    }
  }, []);

  const handleTransferConfirm = async (newLeaderUserId: number) => {
    setTransferSubmitting(true);
    try {
      const result = await departmentLeaderPersonnelApi.transferLeadership(newLeaderUserId);
      toast.success(result.message, { duration: 6000 });
      setTransferOpen(false);

      // The caller is no longer a Leader and their token is void — sign out rather than leave them
      // on a screen every request will now reject.
      if (result.actorMustSignInAgain) {
        await logout();
        navigate('/login', { replace: true });
      } else {
        await list.refreshAll();
      }
    } catch (error) {
      toast.error(getDepartmentLeaderErrorMessage(error));
    } finally {
      setTransferSubmitting(false);
    }
  };

  const department = list.department;
  const page = list.page;
  const rows = page?.items ?? [];
  const startIndex = page ? (page.page - 1) * page.pageSize : 0;

  return (
    <div className="space-y-6 p-4 sm:p-6">
      {/* Breadcrumb */}
      <nav className="text-sm text-gray-500">
        <button type="button" onClick={() => navigate('/dashboard')} className="hover:text-gray-700">
          Trang chủ
        </button>
        <span className="mx-2">/</span>
        <span className="font-medium text-gray-900">Phòng ban của tôi</span>
      </nav>

      {/* Department header */}
      <section className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
        {list.isLoadingDepartment ? (
          <div className="flex items-center gap-2 text-gray-500">
            <Loader2 className="h-5 w-5 animate-spin" />
            <span className="text-sm">Đang tải thông tin phòng ban...</span>
          </div>
        ) : list.departmentError ? (
          <p className="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{list.departmentError}</p>
        ) : (
          department && (
            <>
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
                    <Building2 className="h-6 w-6 text-blue-600" />
                    {department.departmentName}
                  </h1>
                  <div className="mt-2 flex flex-wrap gap-x-6 gap-y-1 text-sm text-gray-600">
                    <span className="flex items-center gap-1.5">
                      <MapPin className="h-4 w-4 text-gray-400" />
                      {department.campusName}
                    </span>
                    <span className="flex items-center gap-1.5">
                      <Crown className="h-4 w-4 text-amber-500" />
                      Trưởng phòng: {department.currentLeaderName ?? '—'}
                    </span>
                  </div>
                </div>

                <button
                  type="button"
                  onClick={() => void openTransfer(null)}
                  className="inline-flex items-center gap-2 rounded-md border border-amber-300 px-4 py-2 text-sm font-medium text-amber-700 hover:bg-amber-50"
                >
                  <Crown className="h-4 w-4" />
                  Đổi trưởng phòng
                </button>
              </div>

              <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
                <StatCard
                  label="Tổng nhân sự"
                  value={department.totalPersonnelCount}
                  icon={<Users className="h-5 w-5 text-blue-600" />}
                />
                <StatCard
                  label="Hoạt động"
                  value={department.activePersonnelCount}
                  icon={<UserCheck className="h-5 w-5 text-green-600" />}
                />
                <StatCard label="Vô hiệu hóa" value={department.inactivePersonnelCount} />
                <StatCard label="Chờ xác nhận email" value={department.pendingEmailConfirmationCount} />
                <StatCard label="Bị khóa" value={department.lockedPersonnelCount} />
              </div>
            </>
          )
        )}
      </section>

      {/* Toolbar */}
      <section className="rounded-lg border border-gray-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center gap-3 border-b p-4">
          <div className="relative min-w-[220px] flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              value={list.keyword}
              onChange={(e) => list.setKeyword(e.target.value)}
              placeholder="Tìm theo tên, email hoặc số điện thoại..."
              className="w-full rounded-md border border-gray-300 py-2 pl-9 pr-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/30"
            />
          </div>

          <select
            value={list.status}
            onChange={(e) => list.setStatus(e.target.value as PersonnelStatusFilter)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/30"
          >
            {STATUS_FILTER_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>

          <button
            type="button"
            onClick={openCreate}
            className="inline-flex items-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            <Plus className="h-4 w-4" />
            Thêm nhân sự
          </button>
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
              <tr>
                <th className="px-4 py-3 font-medium">STT</th>
                <th className="px-4 py-3 font-medium">Họ và tên</th>
                <th className="px-4 py-3 font-medium">Email</th>
                <th className="px-4 py-3 font-medium">Trạng thái</th>
                <th className="px-4 py-3 font-medium">Chức vụ</th>
                <th className="px-4 py-3 text-right font-medium">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {list.isLoadingList && (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center text-gray-500">
                    <Loader2 className="mx-auto mb-2 h-6 w-6 animate-spin" />
                    Đang tải danh sách nhân sự...
                  </td>
                </tr>
              )}

              {!list.isLoadingList && list.listError && (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center">
                    <p className="mb-3 text-sm text-red-600">{list.listError}</p>
                    <button
                      type="button"
                      onClick={() => void list.refreshList()}
                      className="rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
                    >
                      Thử lại
                    </button>
                  </td>
                </tr>
              )}

              {/* "No result for this filter" and "department has nobody yet" are different situations. */}
              {list.isNoResult && (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center text-sm text-gray-500">
                    Không tìm thấy nhân sự phù hợp với điều kiện tìm kiếm.
                  </td>
                </tr>
              )}

              {list.isEmpty && (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center text-sm text-gray-500">
                    Phòng ban chưa có nhân sự nào. Bấm <strong>Thêm nhân sự</strong> để bắt đầu.
                  </td>
                </tr>
              )}

              {!list.isLoadingList &&
                !list.listError &&
                rows.map((row, index) => (
                  <tr key={row.userId} className="hover:bg-gray-50">
                    <td className="px-4 py-3 text-gray-500">{startIndex + index + 1}</td>
                    <td className="px-4 py-3">
                      <span className="flex items-center gap-2 font-medium text-gray-900">
                        {row.fullName}
                        {row.subRole === 'LEADER' && (
                          <Crown className="h-4 w-4 text-amber-500" aria-label="Trưởng phòng" />
                        )}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-600">{row.email}</td>
                    <td className="px-4 py-3">
                      <PersonnelStatusBadge status={row.status} />
                    </td>
                    <td className="px-4 py-3 text-gray-600">{row.position}</td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end gap-2">
                        {row.canView && (
                          <button
                            type="button"
                            onClick={() => void openDetail(row.userId)}
                            title="Xem chi tiết"
                            className="rounded p-1.5 text-blue-600 hover:bg-blue-50"
                          >
                            <Eye className="h-4 w-4" />
                          </button>
                        )}
                        {row.canDisable && (
                          <button
                            type="button"
                            onClick={() => void openStatusImpact(row, 'INACTIVE')}
                            className="rounded px-2 py-1 text-xs font-medium text-red-600 hover:bg-red-50"
                          >
                            Vô hiệu hóa
                          </button>
                        )}
                        {row.canEnable && (
                          <button
                            type="button"
                            onClick={() => void openStatusImpact(row, 'ACTIVE')}
                            className="rounded px-2 py-1 text-xs font-medium text-green-600 hover:bg-green-50"
                          >
                            Kích hoạt
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {page && page.totalItems > 0 && (
          <div className="flex flex-wrap items-center justify-between gap-3 border-t px-4 py-3 text-sm">
            <div className="flex items-center gap-2 text-gray-600">
              <span>Hiển thị</span>
              <select
                value={list.pageSize}
                onChange={(e) => list.setPageSize(Number(e.target.value))}
                className="rounded-md border border-gray-300 px-2 py-1 outline-none focus:border-blue-500"
              >
                {PAGE_SIZE_OPTIONS.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
              <span>
                / {page.totalItems} nhân sự — trang {page.page}/{page.totalPages}
              </span>
            </div>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => list.setCurrentPage(page.page - 1)}
                disabled={!page.hasPreviousPage || list.isLoadingList}
                className="inline-flex items-center gap-1 rounded-md border border-gray-300 px-3 py-1.5 font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-40"
              >
                <ChevronLeft className="h-4 w-4" />
                Trước
              </button>
              <button
                type="button"
                onClick={() => list.setCurrentPage(page.page + 1)}
                disabled={!page.hasNextPage || list.isLoadingList}
                className="inline-flex items-center gap-1 rounded-md border border-gray-300 px-3 py-1.5 font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-40"
              >
                Sau
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>
          </div>
        )}
      </section>

      {/* Modals */}
      <PersonnelFormModal
        open={formOpen}
        mode={formMode}
        personnel={formMode === 'edit' ? detail : null}
        submitting={formSubmitting}
        onClose={() => setFormOpen(false)}
        onSubmit={handleFormSubmit}
      />

      <PersonnelDetailModal
        open={detailOpen}
        loading={detailLoading}
        error={detailError}
        personnel={detail}
        resending={resending}
        onClose={() => setDetailOpen(false)}
        onEdit={openEdit}
        onChangeStatus={(target) => detail && void openStatusImpact(detail, target)}
        onResendConfirmation={() => void handleResend()}
        onTransferLeadership={() => void openTransfer(detail?.userId ?? null)}
      />

      <StatusImpactModal
        open={impactOpen}
        loading={impactLoading}
        error={impactError}
        impact={impact}
        personnelName={impactTarget?.fullName ?? ''}
        submitting={statusSubmitting}
        onClose={() => setImpactOpen(false)}
        onConfirm={(reason) => void handleStatusConfirm(reason)}
      />

      <TransferLeadershipModal
        open={transferOpen}
        loading={transferLoading}
        error={transferError}
        candidates={candidates}
        preselectedUserId={transferPreselect}
        submitting={transferSubmitting}
        departmentName={department?.departmentName ?? ''}
        onClose={() => setTransferOpen(false)}
        onConfirm={(userId) => void handleTransferConfirm(userId)}
      />
    </div>
  );
}

function StatCard({
  label,
  value,
  icon,
}: {
  label: string;
  value: number;
  icon?: React.ReactNode;
}) {
  return (
    <div className="rounded-md border border-gray-200 bg-gray-50 px-4 py-3">
      <div className="flex items-center gap-2 text-xs uppercase tracking-wide text-gray-500">
        {icon}
        <span>{label}</span>
      </div>
      <p className="mt-1 text-2xl font-semibold text-gray-900">{value}</p>
    </div>
  );
}

export default MyDepartmentPage;
