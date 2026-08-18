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
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Crown,
  Eye,
  Loader2,
  Lock,
  MailCheck,
  MapPin,
  Plus,
  Search,
  UserCheck,
  UserMinus,
  Users,
} from 'lucide-react';

import { departmentLeaderPersonnelApi } from '../../../features/department-leader-personnel/api/departmentLeaderPersonnelApi';
import {
  getDepartmentLeaderErrorMessage,
  getDepartmentLeaderFieldErrors,
  isDepartmentLeaderScopeLost,
} from '../../../features/department-leader-personnel/api/departmentLeaderError';
import type { PersonnelFormErrors } from '../../../features/department-leader-personnel/validation/personnelValidation';
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
  // Field-level rejections from the API, handed to the modal so they render under the offending
  // input rather than only as a toast (a rule the server enforces but the client did not predict).
  const [formServerErrors, setFormServerErrors] = useState<PersonnelFormErrors>({});

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
    void logout().finally(() => navigate('/', { replace: true }));
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
    setFormServerErrors({});
    setFormOpen(true);
  };

  const openEdit = () => {
    if (!detail) return;
    setFormMode('edit');
    setDetailOpen(false);
    setFormServerErrors({});
    setFormOpen(true);
  };

  const handleFormSubmit = async (values: {
    fullName: string;
    email: string;
    phone: string;
    gender: PersonnelGender;
  }) => {
    setFormSubmitting(true);
    // A retry starts from a clean slate: last attempt's server errors must not outlive it.
    setFormServerErrors({});
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
      // The modal stays open so the operator can correct the field and retry — and the rejection is
      // attached to the field that caused it, not just announced in a toast that scrolls away.
      setFormServerErrors(getDepartmentLeaderFieldErrors(error));
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
      // A 200 means the request was accepted, not that the mail left the server — report which.
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
        navigate('/', { replace: true });
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
    <div className="space-y-6 p-4 pb-12 sm:p-6">
      {/* Breadcrumb */}
      <nav className="text-sm font-medium text-gray-500">
        <button type="button" onClick={() => navigate('/dashboard')} className="hover:text-[#004c91]">
          Trang chủ
        </button>
        <span className="mx-2 text-gray-300">/</span>
        <span className="font-semibold text-[#004c91]">Phòng ban của tôi</span>
      </nav>

      {/* Department header — navy hero. The statistics live inside it so the page opens on the brand
          colour instead of a wall of white cards. */}
      <section className="relative overflow-hidden rounded-[2rem] bg-gradient-to-br from-[#004c91] to-[#00386b] p-6 shadow-[0_10px_35px_-10px_rgba(0,76,145,0.55)] sm:p-7">
        {/* Decorative glow — purely cosmetic, kept behind the content. */}
        <div
          aria-hidden
          className="pointer-events-none absolute -right-20 -top-24 h-64 w-64 rounded-full bg-white/10 blur-2xl"
        />

        <div className="relative">
          {list.isLoadingDepartment ? (
            <div className="flex items-center gap-2 text-blue-100">
              <Loader2 className="h-5 w-5 animate-spin" />
              <span className="text-sm font-normal">Đang tải thông tin phòng ban...</span>
            </div>
          ) : list.departmentError ? (
            <p className="rounded-xl bg-white px-4 py-3 text-sm font-normal text-red-700">
              {list.departmentError}
            </p>
          ) : (
            department && (
              <>
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div className="min-w-0">
                    <h1 className="flex items-center gap-3 text-2xl font-black tracking-tight text-white sm:text-3xl">
                      <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/20">
                        <Building2 className="h-6 w-6 text-white" />
                      </span>
                      {department.departmentName}
                    </h1>
                    <div className="mt-3 flex flex-wrap gap-x-6 gap-y-1.5 text-sm font-normal text-blue-100">
                      <span className="flex items-center gap-1.5">
                        <MapPin className="h-4 w-4 text-blue-200" />
                        {department.campusName}
                      </span>
                      <span className="flex items-center gap-1.5">
                        <Crown className="h-4 w-4 text-amber-300" />
                        Trưởng phòng:{' '}
                        <strong className="font-bold text-white">
                          {department.currentLeaderName ?? '—'}
                        </strong>
                      </span>
                    </div>
                  </div>

                  <button
                    type="button"
                    onClick={() => void openTransfer(null)}
                    className="inline-flex items-center gap-2 rounded-xl border border-white/30 bg-white/10 px-4 py-2.5 text-sm font-bold text-white outline-none transition hover:bg-white/20 focus:ring-2 focus:ring-white/50"
                  >
                    <Crown className="h-4 w-4 text-amber-300" />
                    Đổi trưởng phòng
                  </button>
                </div>

                <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
                  <StatCard
                    label="Tổng nhân sự"
                    value={department.totalPersonnelCount}
                    icon={<Users className="h-4 w-4" />}
                  />
                  <StatCard
                    label="Hoạt động"
                    value={department.activePersonnelCount}
                    icon={<UserCheck className="h-4 w-4" />}
                    accent="text-emerald-300"
                  />
                  <StatCard
                    label="Vô hiệu hóa"
                    value={department.inactivePersonnelCount}
                    icon={<UserMinus className="h-4 w-4" />}
                    accent="text-slate-300"
                  />
                  <StatCard
                    label="Chờ xác nhận email"
                    value={department.pendingEmailConfirmationCount}
                    icon={<MailCheck className="h-4 w-4" />}
                    accent="text-amber-300"
                  />
                  <StatCard
                    label="Bị khóa"
                    value={department.lockedPersonnelCount}
                    icon={<Lock className="h-4 w-4" />}
                    accent="text-rose-300"
                  />
                </div>
              </>
            )
          )}
        </div>
      </section>

      {/* Toolbar + table */}
      <section className="overflow-hidden rounded-[2rem] border border-[#004c91] bg-white shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)]">
        <div className="flex flex-wrap items-center gap-3 border-b border-[#00386b] bg-[#004c91] p-5">
          <div className="relative min-w-[240px] flex-1">
            <Search className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-blue-200" />
            <input
              type="text"
              value={list.keyword}
              onChange={(e) => list.setKeyword(e.target.value)}
              placeholder="Tìm theo tên, email hoặc số điện thoại..."
              className="w-full rounded-2xl border-none bg-white/10 py-2.5 pl-11 pr-4 text-sm text-white shadow-inner outline-none transition placeholder:text-blue-200 focus:bg-white/20 focus:ring-2 focus:ring-white/50"
            />
          </div>

          <div className="relative">
            <select
              value={list.status}
              onChange={(e) => list.setStatus(e.target.value as PersonnelStatusFilter)}
              className="w-full appearance-none rounded-2xl border-none bg-white/10 py-2.5 pl-4 pr-10 text-sm font-normal text-white shadow-inner outline-none transition focus:bg-white/20 focus:ring-2 focus:ring-white/50"
            >
              {STATUS_FILTER_OPTIONS.map((option) => (
                // Native option lists render on the OS surface, so they need their own dark text.
                <option key={option.value} className="text-gray-900" value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
            <ChevronDown className="pointer-events-none absolute right-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-white opacity-70" />
          </div>

          <button
            type="button"
            onClick={openCreate}
            className="inline-flex items-center gap-2 rounded-2xl bg-[#f37021] px-4 py-2.5 text-sm font-bold text-white shadow-sm shadow-orange-500/20 outline-none transition hover:bg-[#e85c0d] hover:shadow-md hover:shadow-orange-500/40 focus:ring-2 focus:ring-orange-300"
          >
            <Plus className="h-4 w-4 stroke-[2.5]" />
            Thêm nhân sự
          </button>
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead className="border-b border-gray-200 bg-[#f8fafc] text-[11px] uppercase tracking-widest text-gray-500">
              <tr>
                <th className="px-4 py-4 pl-6 font-black">STT</th>
                <th className="px-4 py-4 font-black">Họ và tên</th>
                <th className="px-4 py-4 font-black">Email</th>
                <th className="px-4 py-4 font-black">Trạng thái</th>
                <th className="px-4 py-4 font-black">Chức vụ</th>
                <th className="px-4 py-4 pr-6 text-right font-black">Hành động</th>
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
                      className="rounded-xl border border-[#004c91] px-4 py-2 text-sm font-bold text-[#004c91] outline-none transition hover:bg-blue-50"
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
                  <tr key={row.userId} className="transition-colors hover:bg-blue-50/40">
                    <td className="px-4 py-3.5 pl-6 text-sm font-bold text-[#004c91]">
                      {startIndex + index + 1}
                    </td>
                    <td className="px-4 py-3.5">
                      <span className="flex items-center gap-2 font-bold text-[#004c91]">
                        {row.fullName}
                        {row.subRole === 'LEADER' && (
                          <Crown className="h-4 w-4 text-amber-500" aria-label="Trưởng phòng" />
                        )}
                      </span>
                    </td>
                    <td className="px-4 py-3.5 text-gray-600">{row.email}</td>
                    <td className="px-4 py-3.5">
                      <PersonnelStatusBadge status={row.status} />
                    </td>
                    <td className="px-4 py-3.5 font-normal text-gray-600">{row.position}</td>
                    <td className="px-4 py-3.5 pr-6">
                      <div className="flex items-center justify-end gap-2">
                        {row.canView && (
                          <button
                            type="button"
                            onClick={() => void openDetail(row.userId)}
                            title="Xem chi tiết"
                            className="flex h-9 w-9 items-center justify-center rounded-full border border-gray-200 bg-white text-gray-500 shadow-sm outline-none transition-all hover:border-[#004c91] hover:bg-blue-50 hover:text-[#004c91]"
                          >
                            <Eye className="h-4 w-4" />
                          </button>
                        )}
                        {/* Enable/disable is one switch, not two buttons: the two actions are mutually
                            exclusive per row, and `canDisable` is exactly "this account is on and you
                            may turn it off". The switch never writes — it opens the impact preview,
                            which is what actually confirms the change. */}
                        {row.canDisable || row.canEnable ? (
                          <label
                            className="relative ml-1 flex cursor-pointer items-center"
                            title={row.canDisable ? 'Vô hiệu hóa' : 'Kích hoạt'}
                          >
                            <input
                              type="checkbox"
                              className="peer sr-only"
                              checked={row.canDisable}
                              onChange={() =>
                                void openStatusImpact(row, row.canDisable ? 'INACTIVE' : 'ACTIVE')
                              }
                            />
                            <div className="relative h-5 w-10 rounded-full bg-gray-200 transition-colors peer-checked:bg-[#004c91] peer-focus-visible:ring-2 peer-focus-visible:ring-[#004c91]/40">
                              <div
                                className={`absolute left-0.5 top-0.5 h-4 w-4 rounded-full bg-white shadow-sm transition-transform ${
                                  row.canDisable ? 'translate-x-5' : 'translate-x-0'
                                }`}
                              />
                            </div>
                          </label>
                        ) : (
                          // Leader, locked and pending rows have no status action — say so instead of
                          // leaving a gap that reads as a missing control.
                          <span className="ml-1 text-sm text-gray-300">—</span>
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
          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-gray-100 bg-gray-50/60 px-6 py-4 text-sm">
            <div className="flex items-center gap-2 font-normal text-gray-500">
              <span>Hiển thị</span>
              <div className="relative">
                <select
                  value={list.pageSize}
                  onChange={(e) => list.setPageSize(Number(e.target.value))}
                  className="appearance-none rounded-lg border border-gray-200 bg-white px-3 py-1.5 pr-8 font-normal text-gray-700 outline-none focus:ring-2 focus:ring-[#004c91]/20"
                >
                  {PAGE_SIZE_OPTIONS.map((size) => (
                    <option key={size} value={size}>
                      {size}
                    </option>
                  ))}
                </select>
                <ChevronDown className="pointer-events-none absolute right-2 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-500" />
              </div>
              <span>
                / <strong className="font-normal text-[#004c91]">{page.totalItems}</strong> nhân sự — trang{' '}
                <strong className="font-normal text-[#004c91]">{page.page}</strong>/{page.totalPages}
              </span>
            </div>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => list.setCurrentPage(page.page - 1)}
                disabled={!page.hasPreviousPage || list.isLoadingList}
                className="inline-flex items-center gap-1 rounded-lg border border-gray-200 bg-white px-3 py-1.5 font-bold text-gray-600 outline-none transition-all hover:border-[#004c91] hover:bg-blue-50 hover:text-[#004c91] disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-gray-200 disabled:hover:bg-white disabled:hover:text-gray-600"
              >
                <ChevronLeft className="h-4 w-4" />
                Trước
              </button>
              <button
                type="button"
                onClick={() => list.setCurrentPage(page.page + 1)}
                disabled={!page.hasNextPage || list.isLoadingList}
                className="inline-flex items-center gap-1 rounded-lg border border-gray-200 bg-white px-3 py-1.5 font-bold text-gray-600 outline-none transition-all hover:border-[#004c91] hover:bg-blue-50 hover:text-[#004c91] disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-gray-200 disabled:hover:bg-white disabled:hover:text-gray-600"
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
        serverErrors={formServerErrors}
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
        onResendConfirmation={() => void handleResend()}
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

/**
 * Statistic tile rendered on the navy hero, so it is translucent white rather than a card of its
 * own. `accent` colours only the icon — the number stays white so the five tiles stay scannable as
 * one row instead of reading as five unrelated statuses.
 */
function StatCard({
  label,
  value,
  icon,
  accent = 'text-blue-200',
}: {
  label: string;
  value: number;
  icon?: React.ReactNode;
  accent?: string;
}) {
  return (
    <div className="rounded-2xl border border-white/15 bg-white/10 px-4 py-3 backdrop-blur-sm transition-colors hover:bg-white/15">
      <div className="flex items-center gap-2 text-[10px] font-bold uppercase tracking-wider text-blue-100">
        {icon && <span className={accent}>{icon}</span>}
        <span className="truncate">{label}</span>
      </div>
      <p className="mt-1.5 text-2xl font-black leading-none tracking-tight text-white">{value}</p>
    </div>
  );
}

export default MyDepartmentPage;
