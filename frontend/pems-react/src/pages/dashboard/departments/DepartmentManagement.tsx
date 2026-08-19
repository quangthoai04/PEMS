/**
 * Trang DepartmentManagement
 * Hoạt động điều phối, theo dõi cấu trúc định biên của các phòng ban hiện hữu.
 *
 * Staff Leader (STAFF/LEADER) dùng API thật (UC-101 add / UC-102 update / UC-103 search /
 * UC-104 list / UC-105 details). Các vai trò khác giữ nguyên giao diện mock hiện có.
 */

import React, { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Search,
  Plus,
  Eye,
  ChevronLeft,
  ChevronRight,
  Briefcase,
  Users,
  User,
  MapPin,
  X,
} from "lucide-react";
import { useDebounce } from "../../../shared/hooks/useDebounce";
import { useDepartmentList } from "../../../features/department-management/hooks/useDepartmentManagement";
import { departmentManagementApi } from "../../../features/department-management/api/departmentManagementApi";
import { getDepartmentErrorMessage } from "../../../features/department-management/api/departmentError";
import type {
  DepartmentStatusBlocker,
  DepartmentStatusImpact,
} from "../../../features/department-management/types/departmentManagement.types";

const CAMPUSES = ["Hà Nội", "Hồ Chí Minh", "Đà Nẵng", "Cần Thơ", "Quy Nhơn"];

export function DepartmentManagement() {
  const navigate = useNavigate();
  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase() || "";
  const isStaffLeader = userRole === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER';
  const isHO = user?.role === 'HO';

  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [campusFilter, setCampusFilter] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(5);

  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [newDepartmentName, setNewDepartmentName] = useState("");

  const [departments, setDepartments] = useState([
    { id: 1, name: "Phòng IT", staffCount: 8, waitingAccounts: 2, head: "Nguyễn Văn A", campus: "Hà Nội", status: "Hoạt động" },
    { id: 2, name: "Phòng Đào tạo", staffCount: 12, waitingAccounts: 5, head: "Trần Thị B", campus: "Hà Nội", status: "Hoạt động" },
    { id: 3, name: "Phòng Hợp tác quốc tế", staffCount: 5, waitingAccounts: 1, head: "Lê Văn C", campus: "Đà Nẵng", status: "Hoạt động" },
    { id: 4, name: "Phòng Marketing", staffCount: 10, waitingAccounts: 3, head: "Phạm Thị D", campus: "Hồ Chí Minh", status: "Hoạt động" },
    { id: 5, name: "Phòng Kế toán", staffCount: 4, waitingAccounts: 0, head: "Hoàng Văn E", campus: "Cần Thơ", status: "Ngừng hoạt động" },
    { id: 6, name: "Phòng CTSV", staffCount: 15, waitingAccounts: 4, head: "Vũ Văn F", campus: "Quy Nhơn", status: "Hoạt động" },
  ]);

  // ── Staff Leader real data (UC-101..UC-105). Other roles keep the mock view untouched. ──
  const [toast, setToast] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const showToast = (type: 'success' | 'error', text: string) => {
    setToast({ type, text });
    window.setTimeout(() => setToast(null), 3500);
  };

  const debouncedSearch = useDebounce(searchQuery, 400);
  const statusApi = statusFilter === 'Hoạt động' ? 'ACTIVE' : statusFilter === 'Ngừng hoạt động' ? 'INACTIVE' : '';

  useEffect(() => {
    if (isStaffLeader) setCurrentPage(1);
  }, [debouncedSearch, statusApi, itemsPerPage, isStaffLeader]);

  const slParams = useMemo(() => ({
    page: currentPage,
    pageSize: itemsPerPage,
    keyword: debouncedSearch.trim() || undefined,
    status: statusApi || undefined,
    sortBy: 'name',
    sortDirection: 'asc' as const,
  }), [currentPage, itemsPerPage, debouncedSearch, statusApi]);

  const { data: slData, departments: slDepartments, loading: slLoading, error: slError, refetch: slRefetch } =
    useDepartmentList(slParams, isStaffLeader);

  const slRows = slDepartments.map((d) => ({
    id: d.departmentId,
    name: d.name,
    head: d.headFullName || 'Chưa gán trưởng phòng',
    status: d.status === 'ACTIVE' ? 'Hoạt động' : 'Ngừng hoạt động',
    campus: d.campusName,
    staffCount: 0,
    waitingAccounts: 0,
    departmentType: d.departmentType,
    canToggleStatus: d.canToggleStatus,
  }));

  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  // UC-101 — create a GENERAL department in the Staff Leader's campus.
  const handleCreateDepartmentApi = async () => {
    const name = newDepartmentName.trim();
    if (!name) { setCreateError('Tên phòng ban là bắt buộc.'); return; }
    setCreating(true);
    setCreateError(null);
    try {
      await departmentManagementApi.createDepartment({ name });
      setNewDepartmentName('');
      setIsAddModalOpen(false);
      setCurrentPage(1);
      slRefetch();
      showToast('success', 'Đã thêm phòng ban mới.');
    } catch (err) {
      setCreateError(getDepartmentErrorMessage(err, 'Không thể tạo phòng ban. Vui lòng thử lại.'));
    } finally {
      setCreating(false);
    }
  };

  // ── UC-106 status confirmation modal (IC departments are not toggleable) ──
  // The toggle never flips optimistically: the row only changes after the API succeeds + refetch.
  const [statusConfirm, setStatusConfirm] = useState<{ id: number; name: string; target: 'ACTIVE' | 'INACTIVE' } | null>(null);
  const [statusImpact, setStatusImpact] = useState<DepartmentStatusImpact | null>(null);
  const [statusImpactLoading, setStatusImpactLoading] = useState(false);
  const [statusBlockers, setStatusBlockers] = useState<DepartmentStatusBlocker[] | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [statusSaving, setStatusSaving] = useState(false);

  const openStatusConfirm = async (row: { id: number; name: string; status: string; canToggleStatus?: boolean }) => {
    if (row.canToggleStatus === false) return;
    const target: 'ACTIVE' | 'INACTIVE' = row.status === 'Hoạt động' ? 'INACTIVE' : 'ACTIVE';
    setStatusConfirm({ id: row.id, name: row.name, target });
    setStatusImpact(null);
    setStatusBlockers(null);
    setStatusError(null);
    setStatusImpactLoading(true);
    try {
      const impact = await departmentManagementApi.getDepartmentStatusImpact(row.id, target);
      setStatusImpact(impact);
      if (impact.blockers?.length) setStatusBlockers(impact.blockers);
    } catch {
      // Preview is best-effort — the modal falls back to static text and the
      // backend re-validates blockers on submit regardless.
    } finally {
      setStatusImpactLoading(false);
    }
  };

  const closeStatusConfirm = () => {
    if (statusSaving) return;
    setStatusConfirm(null);
    setStatusImpact(null);
    setStatusBlockers(null);
    setStatusError(null);
  };

  const confirmStatusChange = async () => {
    if (!statusConfirm) return;
    setStatusSaving(true);
    setStatusError(null);
    try {
      const res = await departmentManagementApi.manageDepartmentStatus({
        departmentId: statusConfirm.id,
        status: statusConfirm.target,
      });
      setStatusConfirm(null);
      setStatusImpact(null);
      setStatusBlockers(null);
      slRefetch();
      showToast('success', res.message
        || (statusConfirm.target === 'ACTIVE' ? 'Đã kích hoạt lại phòng ban.' : 'Đã ngừng hoạt động phòng ban.'));
    } catch (err) {
      const body = (err as { response?: { data?: { errorCode?: string; data?: { blockers?: DepartmentStatusBlocker[] } } } })?.response?.data;
      if (body?.errorCode === 'DEPARTMENT_STATUS_BLOCKED_BY_DEPENDENCIES') {
        setStatusBlockers(body.data?.blockers ?? []);
      }
      setStatusError(getDepartmentErrorMessage(err, 'Không thể thay đổi trạng thái phòng ban.'));
    } finally {
      setStatusSaving(false);
    }
  };

  // ── UC-105 detail modal + UC-102 inline edit (Staff Leader) ──
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailData, setDetailData] = useState<any>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [isEditMode, setIsEditMode] = useState(false);
  const [editName, setEditName] = useState('');
  const [editError, setEditError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const openDetail = async (departmentId: number) => {
    setDetailOpen(true);
    setDetailData(null);
    setDetailError(null);
    setIsEditMode(false);
    setEditError(null);
    setDetailLoading(true);
    try {
      const data = await departmentManagementApi.getDepartmentDetails(departmentId);
      setDetailData(data);
    } catch (err) {
      setDetailError(getDepartmentErrorMessage(err, 'Không thể tải chi tiết phòng ban. Vui lòng thử lại.'));
    } finally {
      setDetailLoading(false);
    }
  };

  const closeDetail = () => {
    setDetailOpen(false);
    setDetailData(null);
    setDetailError(null);
    setIsEditMode(false);
    setEditError(null);
  };

  const startEdit = () => {
    if (!detailData) return;
    setEditName(detailData.name);
    setEditError(null);
    setIsEditMode(true);
  };

  const cancelEdit = () => {
    setEditName(detailData?.name ?? '');
    setEditError(null);
    setIsEditMode(false);
  };

  // UC-102 — save the new department name.
  const handleSaveName = async () => {
    if (!detailData) return;
    const name = editName.trim();
    if (!name) { setEditError('Tên phòng ban không được để trống.'); return; }
    if (name.length > 150) { setEditError('Tên phòng ban không được vượt quá 150 ký tự.'); return; }
    setSaving(true);
    setEditError(null);
    try {
      const res = await departmentManagementApi.updateDepartmentName({ departmentId: detailData.departmentId, name });
      setDetailData((prev: any) => prev ? { ...prev, name: res.name } : prev);
      setIsEditMode(false);
      slRefetch();
      showToast('success', res.changed ? 'Đã cập nhật tên phòng ban.' : 'Không có thay đổi nào để cập nhật.');
    } catch (err) {
      setEditError(getDepartmentErrorMessage(err, 'Không thể cập nhật tên phòng ban. Vui lòng thử lại.'));
    } finally {
      setSaving(false);
    }
  };

  const stats = [
    { label: "Tổng số phòng", value: "06", icon: Briefcase, color: "text-[#004c91]", bg: "bg-[#e6eff7]" },
    { label: "Nhân sự", value: "54", icon: Users, color: "text-[#0aa14f]", bg: "bg-[#eaffe4]" },
  ];

  // Lọc dữ liệu (mock, dùng cho vai trò không phải Staff Leader)
  const filteredData = departments.filter((item) => {
    const matchesSearch = item.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          item.head.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesStatus = statusFilter ? item.status === statusFilter : true;
    const matchesCampus = (!isHO || !campusFilter) ? true : item.campus === campusFilter;
    return matchesSearch && matchesStatus && matchesCampus;
  });

  // Phân trang — Staff Leader dùng dữ liệu/phân trang từ server; vai trò khác giữ mock client-side.
  const totalPages = isStaffLeader
    ? (slData?.totalPages ?? 0)
    : Math.ceil(filteredData.length / itemsPerPage);
  const paginatedData = isStaffLeader
    ? slRows
    : filteredData.slice((currentPage - 1) * itemsPerPage, currentPage * itemsPerPage);

  const toggleStatus = (id: number) => {
    setDepartments((prev) =>
      prev.map((dept) =>
        dept.id === id
          ? { ...dept, status: dept.status === "Hoạt động" ? "Ngừng hoạt động" : "Hoạt động" }
          : dept
      )
    );
  };

  const handleCreateDepartment = () => {
    if (newDepartmentName.trim()) {
      setDepartments([
        ...departments,
        {
          id: departments.length + 1,
          name: newDepartmentName,
          staffCount: 0,
          waitingAccounts: 0,
          head: "--",
          campus: isHO ? (campusFilter || "Hà Nội") : "Hà Nội",
          status: "Hoạt động",
        },
      ]);
      setNewDepartmentName("");
      setIsAddModalOpen(false);
    }
  };

  const StatusBadge = ({ status }: { status: string }) => {
    if (status === "Hoạt động") {
      return (
        <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-[#eaffe4] text-[#0aa14f] border border-[#ceefda]">
          Hoạt động
        </span>
      );
    }
    return (
      <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-gray-100 text-gray-600 border border-gray-200">
        Ngừng hoạt động
      </span>
    );
  };

  return (
    <div className="w-full pb-12">
      {/* Breadcrumb */}
      <div className="mb-2 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate("/dashboard")} className="hover:text-[#004c91] transition-colors">
          Dashboard
        </button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Quản lý phòng ban</span>
      </div>

      {/* Header */}
      <div className="border-b border-gray-100 pb-3 mb-4 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý phòng ban</h1>
      </div>

      {/* Stats Cards (ẩn với Staff Leader) */}
      {!isStaffLeader && (
        isHO ? (
          <div className="grid grid-cols-1 md:grid-cols-5 gap-4 mb-8">
            {CAMPUSES.map((campus) => {
              const campusDepts = departments.filter((d) => d.campus === campus);
              const totalDepts = campusDepts.length;
              const totalStaff = campusDepts.reduce((sum, d) => sum + d.staffCount, 0);
              return (
                <div
                  key={campus}
                  className={`bg-white rounded-2xl p-5 border cursor-pointer transition-all duration-300 ${campusFilter === campus ? 'border-[#004c91] bg-blue-50/40 ring-4 ring-[#004c91]/10 shadow-md scale-[1.02]' : 'border-gray-100 shadow-sm hover:border-[#004c91]/40 hover:shadow-md'}`}
                  onClick={() => setCampusFilter(campusFilter === campus ? "" : campus)}
                >
                  <div className="flex items-center gap-3 mb-4">
                    <div className="w-10 h-10 rounded-xl bg-orange-50 flex items-center justify-center shrink-0">
                      <MapPin className="w-5 h-5 text-[#f37021]" />
                    </div>
                    <h3 className="font-bold text-gray-900 text-sm leading-tight leading-4">{campus}</h3>
                  </div>
                  <div className="space-y-2">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-medium text-gray-500">Số phòng</span>
                      <span className="font-bold text-[#004c91]">{totalDepts}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-medium text-gray-500">Nhân sự</span>
                      <span className="font-bold text-[#0aa14f]">{totalStaff}</span>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
            {stats.map((stat, idx) => {
              const Icon = stat.icon;
              return (
                <div key={idx} className="bg-white rounded-2xl p-6 shadow-sm border border-gray-100 flex items-center gap-4">
                  <div className={`w-12 h-12 rounded-xl flex items-center justify-center ${stat.bg}`}>
                    <Icon className={`w-6 h-6 ${stat.color}`} />
                  </div>
                  <div>
                    <p className="text-sm font-bold text-gray-500 uppercase tracking-wider mb-1">{stat.label}</p>
                    <h3 className="text-2xl font-bold text-gray-900">{stat.value}</h3>
                  </div>
                </div>
              );
            })}
          </div>
        )
      )}

      {/* Filters & Actions */}
      <div className="flex flex-wrap items-center gap-3 mb-6">
        <div className="relative flex-1 min-w-0 sm:min-w-[250px] max-w-md">
          <Search className="w-4 h-4 text-gray-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            placeholder="Tìm kiếm theo tên phòng, trưởng phòng..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-10 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white shadow-sm"
          />
        </div>

        {isHO && (
          <select
            value={campusFilter}
            onChange={(e) => setCampusFilter(e.target.value)}
            className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] text-gray-700 bg-white shadow-sm cursor-pointer outline-none min-w-[150px]"
          >
            <option value="">Tất cả cơ sở</option>
            {CAMPUSES.map((cs) => (
              <option key={cs} value={cs}>{cs}</option>
            ))}
          </select>
        )}

        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] text-gray-700 bg-white shadow-sm cursor-pointer outline-none min-w-[180px]"
        >
          <option value="">Tất cả trạng thái</option>
          <option value="Hoạt động">Hoạt động</option>
          <option value="Ngừng hoạt động">Ngừng hoạt động</option>
        </select>

        <button
          onClick={() => { setCreateError(null); setNewDepartmentName(''); setIsAddModalOpen(true); }}
          className="ml-auto bg-[#f37021] hover:bg-[#d9621a] text-white px-4 py-2 rounded-lg text-sm font-bold flex items-center gap-1.5 transition-colors shadow-sm outline-none tracking-wide"
        >
          <Plus className="w-4 h-4" /> Thêm phòng ban mới
        </button>
      </div>

      {/* Data Table */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-gray-600">
            <thead className="bg-[#004c91] text-white font-semibold text-[13px] uppercase tracking-wider border-b border-[#004c91]">
              <tr>
                <th className="p-4 pl-6 w-[5%] font-bold whitespace-nowrap">STT</th>
                <th className="p-4 w-[20%] font-bold whitespace-nowrap">TÊN PHÒNG</th>
                {isHO && <th className="p-4 w-[15%] font-bold whitespace-nowrap">CƠ SỞ</th>}
                {!isStaffLeader && <th className="p-4 w-[10%] font-bold text-center whitespace-nowrap">NHÂN SỰ</th>}
                <th className="p-4 w-[20%] font-bold whitespace-nowrap">TRƯỞNG PHÒNG</th>
                <th className="p-4 w-[10%] text-center font-bold whitespace-nowrap">TRẠNG THÁI</th>
                <th className="p-4 w-[10%] text-center font-bold whitespace-nowrap">HÀNH ĐỘNG</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {paginatedData.length > 0 ? (
                paginatedData.map((item, index) => (
                  <tr key={item.id} className="hover:bg-gray-50/80 transition-colors group">
                    <td className="p-4 pl-6 font-medium text-gray-500">
                      {(currentPage - 1) * itemsPerPage + index + 1}
                    </td>
                    <td className="p-4">
                      <span className="font-bold text-gray-800 whitespace-nowrap">{item.name}</span>
                    </td>
                    {isHO && (
                      <td className="p-4 font-normal text-[#f37021] whitespace-nowrap">
                        <div className="flex items-center gap-1">
                          <MapPin className="w-3 h-3" />
                          {item.campus}
                        </div>
                      </td>
                    )}
                    {!isStaffLeader && (
                      <td className="p-4 text-center font-normal whitespace-nowrap">
                        <div className="flex items-center justify-center gap-1">
                          {item.staffCount}
                          <User className="w-3.5 h-3.5 text-gray-400" />
                        </div>
                      </td>
                    )}
                    <td className={`p-4 whitespace-nowrap ${item.head === 'Chưa gán trưởng phòng' ? 'font-normal text-gray-400 italic' : 'font-normal text-gray-700'}`}>
                      {item.head}
                    </td>
                    <td className="p-4 text-center whitespace-nowrap">
                      <StatusBadge status={item.status} />
                    </td>
                    <td className="p-4 text-center">
                      <div className="flex items-center justify-center gap-3">
                        <button
                          onClick={() => isStaffLeader ? openDetail(item.id) : navigate(`/dashboard/departments/${item.id}`)}
                          className="p-1.5 rounded-lg text-gray-400 hover:bg-[#e6eff7] hover:text-[#004c91] transition-colors outline-none cursor-pointer"
                          title="Xem chi tiết"
                        >
                          <Eye className="w-4 h-4" />
                        </button>
                        {isStaffLeader && (item as any).canToggleStatus === false ? (
                          <span className="text-xs font-normal text-gray-400 italic">Phòng mặc định</span>
                        ) : (
                          <button
                            onClick={() => isStaffLeader ? openStatusConfirm(item as any) : toggleStatus(item.id)}
                            className={`relative inline-flex h-[20px] w-[36px] items-center rounded-full transition-colors duration-300 focus:outline-none ${
                              item.status === 'Hoạt động' ? 'bg-[#004c91]' : 'bg-gray-300'
                            }`}
                            title={item.status === 'Hoạt động' ? 'Ngừng hoạt động' : 'Kích hoạt'}
                          >
                            <span
                              className={`inline-block h-[14px] w-[14px] transform rounded-full bg-white transition-transform duration-300 ${
                                item.status === 'Hoạt động' ? 'translate-x-[19px]' : 'translate-x-[3px]'
                              }`}
                            />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={isHO ? 7 : (isStaffLeader ? 5 : 6)} className="py-12 text-center text-gray-500 bg-white font-normal">
                    {isStaffLeader
                      ? (slLoading
                          ? 'Đang tải danh sách phòng ban...'
                          : slError
                            ? slError
                            : (debouncedSearch.trim() || statusApi)
                              ? 'Không tìm thấy phòng ban phù hợp với điều kiện lọc.'
                              : 'Chưa có phòng ban nào trong cơ sở này.')
                      : 'Không tìm thấy dữ liệu nào'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="flex items-center justify-between border-t border-gray-200 bg-white px-6 py-4">
          <div className="flex items-center gap-3 text-sm text-gray-600 font-medium">
            <span>Hiển thị</span>
            <select
              title="Items per page"
              className="border border-gray-300 bg-white rounded-lg px-2 py-1 outline-none focus:border-[#004c91] hover:border-gray-400 transition-colors cursor-pointer text-gray-700"
              value={itemsPerPage}
              onChange={(e) => { setItemsPerPage(Number(e.target.value)); setCurrentPage(1); }}
            >
              <option value={5}>5</option>
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
              <option value={100}>100</option>
            </select>
            <span>bản ghi / trang</span>
          </div>

          <div className="flex items-center gap-1.5">
            <button
              className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm"
              onClick={() => setCurrentPage(currentPage - 1)}
              disabled={currentPage === 1}
            >
              <ChevronLeft className="w-4 h-4" />
            </button>

            <div className="flex items-center gap-1">
              {Array.from({ length: totalPages }, (_, i) => i + 1).map(p => (
                <button
                  key={p}
                  className={`w-9 h-9 rounded-xl font-bold text-sm transition-colors ${currentPage === p ? 'bg-[#004c91] text-white shadow-sm border border-[#004c91]' : 'text-gray-600 hover:bg-gray-100 border border-transparent'}`}
                  onClick={() => setCurrentPage(p)}
                >
                  {p}
                </button>
              ))}
            </div>

            <button
              className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm"
              onClick={() => setCurrentPage(currentPage + 1)}
              disabled={currentPage === totalPages || totalPages === 0}
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>

      {/* Add Department Modal */}
      {isAddModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setIsAddModalOpen(false)}></div>
          <div className="relative bg-white rounded-3xl w-full max-w-md shadow-2xl overflow-hidden animate-in fade-in zoom-in duration-300">
            {/* Header */}
            <div className="flex items-center justify-between p-6 py-4 bg-[#004c91] text-white">
              <h3 className="text-xl font-bold">Thêm phòng ban</h3>
              <button
                onClick={() => setIsAddModalOpen(false)}
                className="text-white hover:text-gray-200 hover:bg-white/10 p-2 rounded-full transition-colors outline-none"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Body */}
            <div className="p-6">
              <label className="block text-sm font-bold text-gray-700 mb-2">
                Tên phòng ban <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={newDepartmentName}
                onChange={(e) => { setNewDepartmentName(e.target.value); if (createError) setCreateError(null); }}
                placeholder="Nhập tên phòng ban..."
                className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-800 transition-shadow outline-none shadow-sm pb-4"
                autoFocus
              />

              {isStaffLeader ? (
                <>
                  {createError && <p className="mt-2 text-sm font-normal text-red-600">{createError}</p>}
                  <div className="mt-4">
                    <label className="block text-sm font-bold text-gray-700 mb-2">Cơ sở</label>
                    <div className="w-full px-4 py-3 rounded-xl border border-gray-200 bg-gray-50 text-gray-600 font-normal">
                      {user?.campus || 'Cơ sở của bạn'}
                    </div>
                  </div>
                </>
              ) : (
                <>
                  <div className="mt-4">
                    <label className="block text-sm font-bold text-gray-700 mb-2">
                      Trưởng phòng <span className="text-red-500">*</span>
                    </label>
                    <select className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-800 transition-shadow outline-none shadow-sm bg-white">
                      <option value="">-- Chọn trưởng phòng --</option>
                      <option value="user1">Người dùng 1</option>
                      <option value="user2">Người dùng 2</option>
                    </select>
                  </div>

                  {isHO && (
                    <div className="mt-4">
                      <label className="block text-sm font-bold text-gray-700 mb-2">
                        Cơ sở <span className="text-red-500">*</span>
                      </label>
                      <select className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-800 transition-shadow outline-none shadow-sm bg-white">
                        {CAMPUSES.map((cs) => (
                          <option key={cs} value={cs}>{cs}</option>
                        ))}
                      </select>
                    </div>
                  )}
                </>
              )}
            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-3 p-6 pt-0">
              <button
                onClick={() => setIsAddModalOpen(false)}
                className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 hover:text-gray-800 transition-colors shadow-sm outline-none"
              >
                Hủy
              </button>
              <button
                onClick={isStaffLeader ? handleCreateDepartmentApi : handleCreateDepartment}
                disabled={!newDepartmentName.trim() || (isStaffLeader && creating)}
                className="px-5 py-2.5 rounded-xl font-bold text-white bg-[#f37021] hover:bg-[#d9621a] disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-sm outline-none"
              >
                {isStaffLeader && creating ? 'Đang lưu...' : 'Tạo mới'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* UC-106 — Status change confirmation modal (Staff Leader) */}
      {statusConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={closeStatusConfirm}></div>
          <div className="relative bg-white rounded-3xl w-full max-w-md shadow-2xl overflow-hidden animate-in fade-in zoom-in duration-300">
            {/* Header */}
            <div className={`flex items-center justify-between p-6 py-4 text-white ${statusConfirm.target === 'INACTIVE' ? 'bg-red-600' : 'bg-[#0aa14f]'}`}>
              <h3 className="text-xl font-bold">
                {statusConfirm.target === 'INACTIVE' ? 'Ngừng hoạt động phòng ban?' : 'Kích hoạt lại phòng ban?'}
              </h3>
              <button
                onClick={closeStatusConfirm}
                className="text-white hover:text-gray-200 hover:bg-white/10 p-2 rounded-full transition-colors outline-none"
                title="Đóng"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Body */}
            <div className="p-6 space-y-4">
              <p className="text-sm font-bold text-gray-800">{statusConfirm.name}</p>

              {statusConfirm.target === 'INACTIVE' ? (
                <div className="text-sm text-gray-600 space-y-1.5">
                  <p className="font-semibold text-gray-700">Sau khi ngừng hoạt động:</p>
                  <ul className="list-disc pl-5 space-y-1">
                    <li>Trưởng phòng và toàn bộ nhân sự thuộc phòng ban sẽ bị đăng xuất.</li>
                    <li>Các tài khoản này không thể đăng nhập cho đến khi phòng ban được kích hoạt lại.</li>
                    <li>Phòng ban sẽ không xuất hiện trong các lựa chọn phân công mới.</li>
                    <li>Dữ liệu và lịch sử nghiệp vụ vẫn được giữ nguyên.</li>
                  </ul>
                </div>
              ) : (
                <div className="text-sm text-gray-600 space-y-1.5">
                  <p className="font-semibold text-gray-700">Sau khi kích hoạt:</p>
                  <ul className="list-disc pl-5 space-y-1">
                    <li>Phòng ban có thể được chọn cho các phân công mới.</li>
                    <li>Các tài khoản đang ở trạng thái hoạt động có thể đăng nhập lại.</li>
                    <li>Các phiên đăng nhập cũ không được khôi phục.</li>
                    <li>Những tài khoản bị khóa riêng vẫn tiếp tục bị khóa.</li>
                  </ul>
                </div>
              )}

              {/* Impact summary (best-effort preview) */}
              {statusConfirm.target === 'INACTIVE' && (
                statusImpactLoading ? (
                  <p className="text-sm font-normal text-gray-400">Đang tải thông tin ảnh hưởng...</p>
                ) : statusImpact ? (
                  <div className="rounded-xl bg-gray-50 border border-gray-200 p-4 text-sm space-y-1">
                    <div className="flex items-center justify-between">
                      <span className="font-medium text-gray-600">Tài khoản bị ảnh hưởng</span>
                      <span className="font-normal text-gray-900">{statusImpact.affectedAccountCount}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="font-medium text-gray-600">Phiên đăng nhập đang hoạt động</span>
                      <span className="font-normal text-gray-900">{statusImpact.activeSessionCount}</span>
                    </div>
                  </div>
                ) : null
              )}

              {/* Blockers */}
              {statusBlockers && statusBlockers.length > 0 && (
                <div className="rounded-xl bg-red-50 border border-red-200 p-4">
                  <p className="text-sm font-bold text-red-700 mb-2">
                    Không thể ngừng hoạt động vì còn nghiệp vụ chưa hoàn tất:
                  </p>
                  <ul className="list-disc pl-5 space-y-1 text-sm text-red-700">
                    {statusBlockers.map((b) => (
                      <li key={b.type}>{b.message}</li>
                    ))}
                  </ul>
                </div>
              )}

              {statusError && <p className="text-sm font-normal text-red-600">{statusError}</p>}
            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-3 p-6 pt-0">
              <button
                onClick={closeStatusConfirm}
                disabled={statusSaving}
                className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 hover:text-gray-800 transition-colors shadow-sm outline-none disabled:opacity-50"
              >
                Hủy
              </button>
              <button
                onClick={confirmStatusChange}
                disabled={statusSaving || (!!statusBlockers && statusBlockers.length > 0)}
                className={`px-5 py-2.5 rounded-xl font-bold text-white transition-colors shadow-sm outline-none disabled:opacity-50 disabled:cursor-not-allowed ${
                  statusConfirm.target === 'INACTIVE' ? 'bg-red-600 hover:bg-red-700' : 'bg-[#0aa14f] hover:bg-[#088c44]'
                }`}
              >
                {statusSaving
                  ? 'Đang xử lý...'
                  : statusConfirm.target === 'INACTIVE' ? 'Ngừng hoạt động' : 'Kích hoạt lại'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Department Detail / Edit Modal (UC-105 / UC-102, Staff Leader) */}
      {detailOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={closeDetail}></div>
          <div className="relative bg-white rounded-3xl w-full max-w-md shadow-2xl overflow-hidden animate-in fade-in zoom-in duration-300">
            {/* Header */}
            <div className="flex items-center justify-between p-6 py-4 bg-[#004c91] text-white">
              <h3 className="text-xl font-bold flex items-center gap-2">
                <Briefcase className="w-5 h-5" />
                {isEditMode ? 'Chỉnh sửa phòng ban' : 'Chi tiết phòng ban'}
              </h3>
              <button
                onClick={closeDetail}
                className="text-white hover:text-gray-200 hover:bg-white/10 p-2 rounded-full transition-colors outline-none"
                title="Đóng"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Body */}
            <div className="p-6">
              {detailLoading && (
                <div className="py-10 text-center text-gray-400 font-normal">Đang tải chi tiết phòng ban...</div>
              )}

              {!detailLoading && detailError && (
                <div className="py-6 text-center">
                  <p className="text-sm font-normal text-red-600 mb-4">{detailError}</p>
                  <button
                    onClick={() => detailData?.departmentId && openDetail(detailData.departmentId)}
                    className="px-4 py-2 rounded-lg border border-gray-200 text-sm font-bold text-gray-600 hover:text-[#004c91] hover:border-[#004c91] transition-colors"
                  >
                    Thử lại
                  </button>
                </div>
              )}

              {!detailLoading && !detailError && detailData && (
                <div className="space-y-4">
                  <div>
                    <label className="block text-xs font-bold uppercase tracking-wider text-gray-500 mb-1">Tên phòng ban</label>
                    {isEditMode ? (
                      <>
                        <input
                          type="text"
                          value={editName}
                          onChange={(e) => { setEditName(e.target.value); if (editError) setEditError(null); }}
                          className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-800 shadow-sm"
                          autoFocus
                        />
                        {editError && <p className="mt-1.5 text-sm font-normal text-red-600">{editError}</p>}
                      </>
                    ) : (
                      <p className="text-base font-normal text-gray-800">{detailData.name}</p>
                    )}
                  </div>

                  <div>
                    <label className="block text-xs font-bold uppercase tracking-wider text-gray-500 mb-1">Cơ sở</label>
                    <p className="text-sm font-normal text-gray-700">{detailData.campusName}</p>
                  </div>

                  <div>
                    <label className="block text-xs font-bold uppercase tracking-wider text-gray-500 mb-1">Trưởng phòng</label>
                    {detailData.headFullName
                      ? <p className="text-sm font-normal text-gray-700">{detailData.headFullName}</p>
                      : <p className="text-sm font-normal text-gray-400 italic">Chưa gán trưởng phòng</p>}
                  </div>

                  <div>
                    <label className="block text-xs font-bold uppercase tracking-wider text-gray-500 mb-1">Trạng thái</label>
                    <StatusBadge status={detailData.status === 'ACTIVE' ? 'Hoạt động' : 'Ngừng hoạt động'} />
                  </div>
                </div>
              )}
            </div>

            {/* Footer */}
            {!detailLoading && !detailError && detailData && (
              <div className="flex items-center justify-end gap-3 p-6 pt-0">
                {isEditMode ? (
                  <>
                    <button
                      onClick={cancelEdit}
                      className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 hover:text-gray-800 transition-colors shadow-sm outline-none"
                    >
                      Hủy
                    </button>
                    <button
                      onClick={handleSaveName}
                      disabled={!editName.trim() || saving}
                      className="px-5 py-2.5 rounded-xl font-bold text-white bg-[#0aa14f] hover:bg-[#088c44] disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-sm outline-none"
                    >
                      {saving ? 'Đang lưu...' : 'Lưu'}
                    </button>
                  </>
                ) : (
                  <>
                    <button
                      onClick={closeDetail}
                      className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 hover:text-gray-800 transition-colors shadow-sm outline-none"
                    >
                      Đóng
                    </button>
                    {detailData.canEditName && (
                      <button
                        onClick={startEdit}
                        className="px-5 py-2.5 rounded-xl font-bold text-white bg-[#f37021] hover:bg-[#d9621a] transition-colors shadow-sm outline-none"
                      >
                        Chỉnh sửa
                      </button>
                    )}
                  </>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Toast (Staff Leader actions) — góc trên bên phải */}
      {toast && (
        <div className="fixed top-6 right-6 z-[60] animate-in fade-in slide-in-from-top-2 duration-300">
          <div className={`px-5 py-3 rounded-xl shadow-lg text-sm font-normal text-white ${toast.type === 'success' ? 'bg-[#0aa14f]' : 'bg-red-600'}`}>
            {toast.text}
          </div>
        </div>
      )}
    </div>
  );
}
