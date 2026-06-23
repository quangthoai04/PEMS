/**
 * Trang DepartmentDetailDashboard
 * Xem chi tiết tác vụ biểu đồ, chỉ số hoạt động ở phạm trù phòng ban cục bộ.
 */

import React, { useState } from "react";
import { departmentManagementApi } from '../../../features/department-management/api/departmentManagementApi';
import toast, { Toaster } from 'react-hot-toast';
import { useNavigate, useParams } from "react-router-dom";
import {
  ChevronLeft,
  ChevronRight,
  Briefcase,
  Users,
  UserCheck,
  Mail,
  Phone,
  Key,
  Eye,
  Crown,
  RefreshCw,
  CheckCircle2,
  Clock,
  ListTodo,
  Search,
  Filter,
  Plus,
  X,
  Edit,
  Trash2,
  XCircle
} from "lucide-react";

export function DepartmentDetailDashboard() {
  const navigate = useNavigate();
  const { id } = useParams();

  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const isDeptLeader = user?.role?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'LEADER';
  const isStaffLeader = user?.role?.toUpperCase() === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER';
  const isStaffRole = user?.role?.toUpperCase() === 'STAFF';
  const isStaff = user?.role?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'STAFF';
  const isLeader = user?.role === 'ADMIN' || user?.role === 'Admin' || user?.role === 'HO' || isStaffLeader || isDeptLeader;
  const isHO = user?.role?.toUpperCase() === 'HO';
  const isSysAdmin = user?.role?.toUpperCase() === 'ADMIN' || user?.role === 'Admin';
  const canEditMember = isDeptLeader || isStaffLeader || isHO;
  const isStaffOrHO = user?.role?.toUpperCase() === 'STAFF' || isHO;

  // Mock data
  const departmentInfo = {
    name: "Phòng IT",
    totalStaff: 12,
    activeAccounts: 10,
    processingTasks: 3,
  };

  const mockMembers = Array.from({ length: 15 }, (_, i) => ({
    id: i === 0 ? "dept_leader" : i === 1 ? "dept_staff" : (i + 1).toString(),
    name: i === 0 ? "Nguyễn Văn Trưởng Phòng" : i === 1 ? "Nguyễn Văn Nhân Viên" : `Nhân viên ${i + 1}`,
    email: i === 0 ? "leader@fpt.edu.vn" : i === 1 ? "staff@fpt.edu.vn" : `nhanvien${i + 1}@fpt.edu.vn`,
    phone: `09000000${i.toString().padStart(2, "0")}`,
    status: i % 4 === 0 ? "Chưa cấp tài khoản" : "Đã cấp tài khoản",
    role: i === 0 ? "Trưởng phòng" : "Nhân viên",
    gender: i % 2 === 0 ? "Nam" : "Nữ",
    campus: "Hà Nội",
    systemRole: "Dept",
    avatarUrl: `https://ui-avatars.com/api/?name=${i === 0 ? "Nguyễn+Văn+Trưởng+Phòng" : i === 1 ? "Nguyễn+Văn+Nhân+Viên" : `Nhân+viên+${i + 1}`}&background=random`
  }));

  const leaders = mockMembers.filter(m => m.role === "Trưởng phòng");


const [tasks, setTasks] = useState([
    {
      id: 1,
      delegation: "Đoàn ĐH Deakin",
      task: "Chuẩn bị hệ thống mạng phòng họp VIP",
      status: "Chưa làm",
      assigneeId: "dept_staff",
      originalAssigneeId: "dept_staff",
      rejectReason: undefined
    },
    {
      id: 2,
      delegation: "Đoàn đối tác Nhật Bản",
      task: "Setup thiết bị trình chiếu",
      status: "Chưa làm",
      assigneeId: "dept_staff",
      originalAssigneeId: "dept_staff",
      rejectReason: undefined
    },
    {
      id: 3,
      delegation: "Đoàn học sinh THPT",
      task: "Hỗ trợ kỹ thuật hội trường",
      status: "Chưa làm",
      assigneeId: "dept_leader",
      originalAssigneeId: "dept_leader",
      rejectReason: undefined
    },
    {
      id: 4,
      delegation: "Đoàn khách quan trọng",
      task: "Khảo sát và chụp ảnh sự kiện",
      status: "Chưa làm",
      assigneeId: "dept_staff",
      originalAssigneeId: "dept_staff",
      rejectReason: undefined
    },
    {
      id: 5,
      delegation: "Đoàn khách quan chức năng",
      task: "Dự đón đoàn VIP",
      status: "Chưa làm",
      assigneeId: "dept_leader",
      originalAssigneeId: "dept_leader",
      rejectReason: ""
    }
  ]);


// Pagination for members
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(5);
  const [memberSearch, setMemberSearch] = useState("");
  const [memberStatus, setMemberStatus] = useState("");
  const [isAddMemberModalOpen, setIsAddMemberModalOpen] = useState(false);
  const [isViewMemberModalOpen, setIsViewMemberModalOpen] = useState(false);
  const [selectedMember, setSelectedMember] = useState<any>(null);
  const [isEditingMember, setIsEditingMember] = useState(false);
  const [editingMemberData, setEditingMemberData] = useState<any>(null);
  const [isDeleteMemberModalOpen, setIsDeleteMemberModalOpen] = useState(false);
  const [memberToDelete, setMemberToDelete] = useState<any>(null);

  const [taskPage, setTaskPage] = useState(1);
  const [tasksPerPage, setTasksPerPage] = useState(5);
  const [taskSearch, setTaskSearch] = useState("");
  const [taskStatus, setTaskStatus] = useState("");

  const [isChangeAssigneeModalOpen, setIsChangeAssigneeModalOpen] = useState(false);
  const [selectedTaskForAssigneeChange, setSelectedTaskForAssigneeChange] = useState<any>(null);
  const [newAssigneeId, setNewAssigneeId] = useState<string>("");

  const [isChangeLeaderModalOpen, setIsChangeLeaderModalOpen] = useState(false);
  const [newLeaderId, setNewLeaderId] = useState<string>("");

  const [isRejectTaskModalOpen, setIsRejectTaskModalOpen] = useState(false);
  const [taskToReject, setTaskToReject] = useState<number | null>(null);
  const [rejectTaskReason, setRejectTaskReason] = useState("");

  const [isViewRejectReasonModalOpen, setIsViewRejectReasonModalOpen] = useState(false);
  const [selectedRejectReason, setSelectedRejectReason] = useState("");

  const [members, setMembers] = useState<any[]>([]);
  const [totalMembers, setTotalMembers] = useState(0);
  const [isLoadingMembers, setIsLoadingMembers] = useState(false);
  const [isCreatingMember, setIsCreatingMember] = useState(false);
  const [newMemberData, setNewMemberData] = useState({ fullName: '', email: '', phone: '', gender: 'Nam', role: 'Nhân viên' });

  const targetDeptId = Number(id) || Number(user?.departmentId);

  const fetchMembers = async () => {
    if (!targetDeptId) return;
    setIsLoadingMembers(true);
    try {
      const res = await departmentManagementApi.searchPersonnel({
        departmentId: targetDeptId,
        keyword: memberSearch,
        status: memberStatus,
        pageNumber: currentPage,
        pageSize: itemsPerPage
      });
      setMembers(res.data.items || []);
      setTotalMembers(res.data.totalCount || 0);
    } catch (error) {
      console.error(error);
    } finally {
      setIsLoadingMembers(false);
    }
  };

  React.useEffect(() => {
    fetchMembers();
  }, [currentPage, itemsPerPage, memberSearch, memberStatus, targetDeptId]);

  const handleUpdateMember = async () => {
    try {
      await departmentManagementApi.updateDepartmentPersonnel({
        departmentId: targetDeptId,
        userId: editingMemberData.id,
        fullName: editingMemberData.name,
        phone: editingMemberData.phone,
        gender: editingMemberData.gender === "Nam" ? 0 : editingMemberData.gender === "Nữ" ? 1 : 2
      });
      toast.success('Đã cập nhật thông tin nhân sự thành công.');
      setIsEditingMember(false);
      fetchMembers();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi cập nhật');
    }
  };

  const handleDeleteMember = async () => {
    try {
      await departmentManagementApi.removePersonnel({
        departmentId: targetDeptId,
        userId: memberToDelete.id
      });
      toast.success('Đã gỡ nhân sự khỏi danh sách hoạt động.');
      setIsDeleteMemberModalOpen(false);
      fetchMembers();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi xóa nhân sự');
    }
  };

  const handleChangeLeader = async () => {
    try {
      await departmentManagementApi.reassignDepartmentLead({
        departmentId: targetDeptId,
        newLeaderUserId: newLeaderId
      });
      toast.success('Đã thay đổi trưởng phòng thành công.');
      setIsChangeLeaderModalOpen(false);
      fetchMembers();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi đổi trưởng phòng');
    }
  };

  const handleCreateMember = async () => {
    if (!newMemberData.fullName.trim() || !newMemberData.email.trim() || !newMemberData.phone.trim()) {
      toast.error('Vui lòng điền đầy đủ các trường bắt buộc');
      return;
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(newMemberData.email)) {
      toast.error('Định dạng email không hợp lệ');
      return;
    }

    const phoneRegex = /(0[3|5|7|8|9])+([0-9]{8})\b/;
    if (!phoneRegex.test(newMemberData.phone)) {
      toast.error('Số điện thoại không hợp lệ (10 số, bắt đầu bằng 03, 05, 07, 08, 09)');
      return;
    }

    setIsCreatingMember(true);
    try {
      const data = {
        departmentId: targetDeptId,
        fullName: newMemberData.fullName,
        email: newMemberData.email,
        phone: newMemberData.phone,
        gender: newMemberData.gender === "Nam" ? 0 : newMemberData.gender === "Nữ" ? 1 : 2,
        role: newMemberData.role
      };
      await departmentManagementApi.addDepartmentPersonnel(data);
      toast.success('Đã thêm nhân sự và gửi email thông báo thành công.');
      setIsAddMemberModalOpen(false);
      fetchMembers();
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi thêm nhân sự');
    } finally {
      setIsCreatingMember(false);
    }
  };

  // Filter tasks based on role
  const roleFilteredTasks = tasks.filter(t => {
    if (isLeader) return true;
    if (isStaff) return t.assigneeId === user?.account;
    return true; // For admin/ho
  });

  const filteredTasks = roleFilteredTasks.filter(t =>
    (taskSearch === "" || t.delegation.toLowerCase().includes(taskSearch.toLowerCase()) || t.task.toLowerCase().includes(taskSearch.toLowerCase())) &&
    (taskStatus === "" || t.status === taskStatus)
  );

  const totalTaskPages = Math.ceil(filteredTasks.length / tasksPerPage);
  const paginatedTasks = filteredTasks.slice(
    (taskPage - 1) * tasksPerPage,
    taskPage * tasksPerPage
  );

  const paginatedMembers = members;
  const totalPages = Math.ceil(totalMembers / itemsPerPage);

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12 relative">
      <Toaster position="top-right" />
      {/* Breadcrumb */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button
          onClick={() => navigate("/dashboard")}
          className="hover:text-[#004c91] transition-colors"
        >
          Dashboard
        </button>
        {(user?.role?.toUpperCase() !== 'DEPARTMENT' && user?.role?.toUpperCase() !== 'STAFF') && (
          <>
            <span className="mx-2">/</span>
            <button
              onClick={() => navigate("/dashboard/departments")}
              className="hover:text-[#004c91] transition-colors"
            >
              Quản lý phòng ban
            </button>
          </>
        )}
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Chi tiết phòng ban</span>
      </div>

      {/* 1. Header: Thông tin tổng quan */}
      <div className="mb-4 relative">
        <div className="flex items-center gap-6">
          <div>
            <div className="inline-flex items-center gap-2 px-3 py-1 bg-[#e6eff7] text-[#004c91] rounded-full text-xs font-bold uppercase tracking-wider mb-2">
              <Briefcase className="w-4 h-4" />
              Thông tin phòng ban
            </div>
            <h1 className="text-3xl font-black text-[#004c91] uppercase tracking-tight">
              {departmentInfo.name}
            </h1>
          </div>
        </div>
      </div>

      {!(isStaffRole || user?.role?.toUpperCase() === 'DEPARTMENT' || isHO) && (
        <>
          <hr className="border-gray-200 mb-6" />
          <div className="grid grid-cols-1 md:grid-cols-12 gap-5 mb-8">
            {/* Số tài khoản */}
            <div className="md:col-span-12 bg-gradient-to-br from-[#f37021] to-[#df6217] text-white rounded-xl p-5 shadow-sm flex flex-col justify-between relative overflow-hidden group max-h-[320px]">
              <div className="absolute top-3 right-3 opacity-10 group-hover:opacity-20 transition-opacity">
                <Key className="w-12 h-12" />
              </div>
              <div className="flex items-center justify-between z-10 w-full mb-3">
                <div className="flex items-center gap-2 font-bold uppercase tracking-wider text-sm text-orange-100">
                  <Key className="w-5 h-5" /> Tài khoản hoạt động
                </div>
              </div>
              <div className="z-10 mt-auto">
                <div className="flex items-baseline gap-1 mb-3">
                  <span className="text-6xl font-black">{departmentInfo.activeAccounts}</span>
                  <span className="text-2xl font-bold text-orange-200">/{departmentInfo.totalStaff} <span className="text-xl">tài khoản</span></span>
                </div>
                <p className="text-[11px] text-orange-100 border-t border-orange-800/20 pt-2 font-medium mt-2">
                  Số tài khoản đã cấp cho nhân sự
                </p>
              </div>
            </div>
          </div>
        </>
      )}

      <hr className="border-gray-200 mb-8" />

      {/* 3. Danh sách thành viên */}
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-bold text-[#004c91] flex items-center gap-2">
          <Users className="w-6 h-6" /> Danh sách nhân sự
        </h2>
        {isLeader && (
          <button
            onClick={() => setIsAddMemberModalOpen(true)}
            className="bg-[#f37021] hover:bg-[#d9621a] text-white px-4 py-2 rounded-xl text-sm font-bold transition-colors shadow-sm outline-none flex items-center gap-2"
          >
            <Plus className="w-4 h-4" /> Thêm nhân sự
          </button>
        )}
      </div>
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        {/* Lọc & Tìm kiếm cho nhân sự */}
        <div className="p-5 border-b border-[#004c91]/10 flex flex-col md:flex-row gap-4 justify-between bg-[#004c91]">
          <div className="relative max-w-sm w-full">
            <input
              type="text"
              placeholder="Tìm kiếm nhân sự..."
              className="w-full pl-10 pr-4 py-2 border-none rounded-xl focus:outline-none focus:ring-2 focus:ring-white bg-white/10 text-white placeholder-white/60 text-sm"
              value={memberSearch}
              onChange={(e) => {
                setMemberSearch(e.target.value);
                setCurrentPage(1);
              }}
            />
            <Search className="w-4 h-4 text-white/60 absolute left-3.5 top-1/2 -translate-y-1/2" />
          </div>
          {!(isStaffRole || user?.role?.toUpperCase() === 'DEPARTMENT' || isHO) && (
            <div className="flex items-center gap-3">
              <div className="flex items-center gap-2 text-sm font-medium text-white/80">
                <Filter className="w-4 h-4" />
                <span>Lọc trạng thái:</span>
              </div>
              <select
                className="border-none bg-white/10 text-white [&>option]:text-gray-800 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-white cursor-pointer"
                value={memberStatus}
                onChange={(e) => {
                  setMemberStatus(e.target.value);
                  setCurrentPage(1);
                }}
              >
                <option value="">Tất cả tài khoản</option>
                <option value="active">Đã cấp tài khoản</option>
                <option value="inactive">Chưa có tài khoản</option>
              </select>
            </div>
          )}
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-gray-600">
            <thead className="bg-[#004c91] text-white font-semibold text-[13px] uppercase tracking-wider border-b border-[#004c91]">
              <tr>
                <th className="p-4 pl-6 w-[5%] font-bold whitespace-nowrap">
                  STT
                </th>
                <th className="p-4 w-[20%] font-bold whitespace-nowrap">
                  Họ và tên
                </th>
                <th className="p-4 w-[20%] font-bold whitespace-nowrap">
                  Email
                </th>
                <th className="p-4 w-[15%] font-bold text-center whitespace-nowrap">
                  Số điện thoại
                </th>
                <th className="p-4 w-[15%] font-bold text-center whitespace-nowrap">
                  Chức vụ
                </th>
                {!(isStaffRole || user?.role?.toUpperCase() === 'DEPARTMENT' || isHO) && (
                  <th className="p-4 w-[15%] font-bold text-center whitespace-nowrap">
                    Tài khoản
                  </th>
                )}
                <th className="p-4 w-[15%] text-center font-bold whitespace-nowrap">
                  Hành động
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {paginatedMembers.length > 0 ? (
                paginatedMembers.map((member, index) => (
                  <tr
                    key={member.id}
                    className="hover:bg-gray-50/80 transition-colors group"
                  >
                    <td className="p-4 pl-6 font-medium text-gray-500">
                      {(currentPage - 1) * itemsPerPage + index + 1}
                    </td>
                    <td className="p-4">
                      <span className="font-bold text-gray-800 whitespace-nowrap">
                        {member.name}
                      </span>
                    </td>
                    <td className="p-4 font-medium text-gray-700 whitespace-nowrap">
                      {member.email}
                    </td>
                    <td className="p-4 font-medium text-center text-gray-700 whitespace-nowrap">
                      {member.phone}
                    </td>
                    <td className="p-4 text-center whitespace-nowrap">
                      <span className={`inline-flex items-center px-2 py-1 rounded-md text-[11px] font-bold ${member.role === "Trưởng phòng"
                        ? "bg-yellow-50 text-yellow-700 border border-yellow-200"
                        : "bg-gray-100 text-gray-600 border border-gray-200"
                        }`}>
                        {member.role === "Trưởng phòng" && <Crown className="w-3 h-3 mr-1" />}
                        {member.role}
                      </span>
                    </td>
                    {!(isStaffRole || user?.role?.toUpperCase() === 'DEPARTMENT' || isHO) && (
                      <td className="p-4 text-center whitespace-nowrap">
                        {member.status === "Đã cấp tài khoản" ? (
                          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-[#eaffe4] text-[#0aa14f] border border-[#ceefda]">
                            Đã cấp tài khoản
                          </span>
                        ) : (
                          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-red-50 text-red-600 border border-red-200">
                            Chưa cấp tài khoản
                          </span>
                        )}
                      </td>
                    )}
                    <td className="p-4 text-center">
                      <div className="flex items-center justify-center gap-2">
                        <button
                          className="p-2 text-gray-400 hover:text-[#004c91] hover:bg-[#e6eff7] rounded-lg transition-colors flex items-center justify-center outline-none"
                          title="Xem chi tiết"
                          onClick={() => {
                            setSelectedMember(member);
                            setEditingMemberData(member);
                            setIsEditingMember(false);
                            setIsViewMemberModalOpen(true);
                          }}
                        >
                          <Eye className="w-5 h-5" />
                        </button>
                        {canEditMember && (
                          member.role === "Trưởng phòng" ? (
                            <button
                              className="p-2 text-[#004c91] hover:bg-blue-50 rounded-lg transition-colors flex items-center justify-center outline-none"
                              title="Đổi trưởng phòng"
                              onClick={() => {
                                setIsChangeLeaderModalOpen(true);
                              }}
                            >
                              <Crown className="w-5 h-5" />
                            </button>
                          ) : (
                            <button
                              className="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors flex items-center justify-center outline-none"
                              title="Xóa nhân sự"
                              onClick={() => {
                                setMemberToDelete(member);
                                setIsDeleteMemberModalOpen(true);
                              }}
                            >
                              <Trash2 className="w-5 h-5" />
                            </button>
                          )
                        )}
                        {member.status === "Chưa cấp tài khoản" && user?.role?.toUpperCase() !== 'DEPARTMENT' && !(isStaffRole || isHO) && (
                          <button
                            className={`bg-[#0aa14f] hover:bg-[#088a42] text-white rounded-lg text-xs font-bold transition-colors shadow-sm outline-none flex items-center justify-center whitespace-nowrap ${isStaffOrHO ? 'p-2' : 'px-3 py-1.5 gap-1.5'}`}
                            title="Cấp tài khoản"
                          >
                            <Key className={isStaffOrHO ? "w-4 h-4" : "w-3.5 h-3.5"} />
                            {!isStaffOrHO && "Cấp tài khoản"}
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={(isStaffRole || user?.role?.toUpperCase() === 'DEPARTMENT' || isHO) ? 6 : 7}
                    className="py-12 text-center text-gray-500 bg-white font-medium"
                  >
                    Không tìm thấy nhân sự nào
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
              onChange={(e) => {
                setItemsPerPage(Number(e.target.value));
                setCurrentPage(1);
              }}
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
              {Array.from({ length: totalPages || 1 }, (_, i) => i + 1).map(
                (p) => (
                  <button
                    key={p}
                    className={`w-9 h-9 rounded-xl font-bold text-sm transition-colors ${currentPage === p
                      ? "bg-[#004c91] text-white shadow-sm border border-[#004c91]"
                      : "text-gray-600 hover:bg-gray-100 border border-transparent"
                      }`}
                    onClick={() => setCurrentPage(p)}
                  >
                    {p}
                  </button>
                )
              )}
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

      {/* Modal Thêm Nhân Sự */}
      {isAddMemberModalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm"
          onClick={() => setIsAddMemberModalOpen(false)}
        >
          <div
            className="bg-white rounded-2xl shadow-xl w-full max-w-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-200"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="p-6 border-b border-[#004c91] bg-[#004c91]">
              <h3 className="text-xl font-bold text-white">Thêm nhân sự</h3>
              <p className="text-xs text-blue-100 mt-1">Vui lòng điền đầy đủ thông tin bên dưới</p>
            </div>

            <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-bold text-[#004c91] mb-1.5">Họ và tên <span className="text-red-500">*</span></label>
                <input type="text" value={newMemberData.fullName} onChange={e => setNewMemberData({...newMemberData, fullName: e.target.value})} placeholder="Nhập họ và tên" className="w-full px-4 py-2 bg-gray-50 border border-gray-200 rounded-xl focus:bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all text-sm" />
              </div>

              <div>
                <label className="block text-sm font-bold text-[#004c91] mb-1.5">Email <span className="text-red-500">*</span></label>
                <input type="email" value={newMemberData.email} onChange={e => setNewMemberData({...newMemberData, email: e.target.value})} placeholder="Nhập địa chỉ email" className="w-full px-4 py-2 bg-gray-50 border border-gray-200 rounded-xl focus:bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all text-sm" />
              </div>

              <div>
                <label className="block text-sm font-bold text-[#004c91] mb-1.5">SĐT <span className="text-red-500">*</span></label>
                <input type="tel" value={newMemberData.phone} onChange={e => setNewMemberData({...newMemberData, phone: e.target.value})} placeholder="Nhập số điện thoại" className="w-full px-4 py-2 bg-gray-50 border border-gray-200 rounded-xl focus:bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all text-sm" />
              </div>

              <div>
                <label className="block text-sm font-bold text-[#004c91] mb-1.5">Giới tính <span className="text-red-500">*</span></label>
                <select value={newMemberData.gender} onChange={e => setNewMemberData({...newMemberData, gender: e.target.value})} className="w-full px-4 py-2 bg-gray-50 border border-gray-200 rounded-xl focus:bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all text-sm">
                  <option value="">Chọn giới tính</option>
                  <option value="Nam">Nam</option>
                  <option value="Nữ">Nữ</option>
                  <option value="Khác">Khác</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-bold text-[#004c91] mb-1.5">Phòng ban</label>
                <input type="text" disabled value="Phòng ban IT" className="w-full px-4 py-2 bg-gray-100 border border-gray-200 rounded-xl text-gray-500 text-sm font-medium cursor-not-allowed" />
              </div>

              <div>
                <label className="block text-sm font-bold text-[#004c91] mb-1.5">Chức vụ</label>
                <input type="text" disabled value="Nhân viên" className="w-full px-4 py-2 bg-gray-100 border border-gray-200 rounded-xl text-gray-500 text-sm font-medium cursor-not-allowed" />
              </div>
            </div>

            <div className="p-6 bg-gray-50 border-t border-gray-100 flex gap-3 justify-end">
              <button
                onClick={() => setIsAddMemberModalOpen(false)}
                className="px-5 py-2.5 rounded-xl border border-gray-300 text-gray-700 font-bold hover:bg-gray-100 transition-colors outline-none text-sm"
              >
                Hủy
              </button>
              <button
                onClick={handleCreateMember}
                disabled={isCreatingMember}
                className={`px-6 py-2.5 rounded-xl font-bold transition-colors outline-none text-sm shadow-sm ${isCreatingMember ? 'bg-[#f37021]/50 text-white/80 cursor-not-allowed' : 'bg-[#f37021] text-white hover:bg-[#d9621a]'}`}
              >
                {isCreatingMember ? 'Đang tạo...' : 'Tạo'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal Xem chi tiết nhân sự */}
      {isViewMemberModalOpen && selectedMember && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
          onClick={() => setIsViewMemberModalOpen(false)}
        >
          <div
            className="bg-[#f8fafc] rounded-3xl shadow-2xl w-full max-w-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-200 block"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Header / Cover */}
            <div className="h-32 bg-[#004c91] relative overflow-hidden">
              <div className="absolute inset-0 bg-gradient-to-t from-black/50 to-transparent"></div>

              {isEditingMember ? (
                <div className="absolute top-4 right-14 flex gap-2 z-10">
                  <button
                    onClick={handleUpdateMember}
                    className="p-2 bg-[#004c91]/80 hover:bg-[#004c91] text-white backdrop-blur-md rounded-xl text-sm font-bold shadow-sm transition-colors outline-none"
                  >
                    Lưu
                  </button>
                  <button
                    onClick={() => setIsEditingMember(false)}
                    className="p-2 bg-white/20 hover:bg-white/30 backdrop-blur-md text-white rounded-xl text-sm transition-colors outline-none cursor-pointer"
                  >
                    Hủy
                  </button>
                </div>
              ) : (
                canEditMember && (
                  <button
                    onClick={() => setIsEditingMember(true)}
                    className="absolute top-4 right-14 p-2 bg-white/20 hover:bg-white/30 backdrop-blur-md text-white rounded-full transition-colors outline-none z-10"
                    title="Chỉnh sửa thông tin"
                  >
                    <Edit className="w-5 h-5" />
                  </button>
                )
              )}
              <button
                onClick={() => setIsViewMemberModalOpen(false)}
                className="absolute top-4 right-4 p-2 bg-white/20 hover:bg-white/30 backdrop-blur-md text-white rounded-full transition-colors outline-none z-10"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="px-6 pb-6 pt-0 relative flex flex-col md:flex-row gap-6">
              {/* Left Column (Avatar & Basic Info) */}
              <div className="w-full md:w-1/3 pt-16 flex flex-col items-center text-center relative">
                {/* Avatar Float */}
                <div className="absolute -top-16 left-1/2 -translate-x-1/2">
                  <div className="relative">
                    <img
                      src={isEditingMember ? editingMemberData?.avatarUrl : selectedMember.avatarUrl}
                      alt={selectedMember.name}
                      className="w-28 h-28 rounded-2xl border-[6px] border-[#f8fafc] shadow-lg bg-white object-cover relative z-10"
                    />
                    <div className="absolute inset-0 rounded-2xl border-[6px] border-[#f8fafc] shadow-[0_0_20px_rgba(0,0,0,0.15)] z-0"></div>

                    {/* Badges next to Avatar */}
                    <div className="absolute top-4 left-[calc(100%+16px)] flex flex-col xl:flex-row gap-2.5 z-20 w-max">
                      <span className={`inline-flex items-center px-3.5 py-1.5 rounded-xl text-[13px] font-bold shadow-sm ${selectedMember.role === "Trưởng phòng"
                        ? "bg-gradient-to-r from-yellow-50 to-yellow-100 text-yellow-700 border border-yellow-200"
                        : "bg-white text-gray-700 border border-gray-200"
                        }`}>
                        {selectedMember.role === "Trưởng phòng" && <Crown className="w-4 h-4 mr-1.5" />}
                        {selectedMember.role}
                      </span>
                      <span className={`inline-flex items-center px-3.5 py-1.5 rounded-xl text-[13px] font-bold shadow-sm ${selectedMember.status === "Đã cấp tài khoản"
                        ? "bg-[#eaffe4] text-[#0aa14f] border border-[#ceefda]"
                        : "bg-red-50 text-red-600 border border-red-200"
                        }`}>
                        {selectedMember.status}
                      </span>
                    </div>
                  </div>
                </div>

                <div className="mt-4 w-full">
                  {isEditingMember ? (
                    <input
                      type="text"
                      value={editingMemberData.name}
                      onChange={(e) => setEditingMemberData({ ...editingMemberData, name: e.target.value })}
                      className="text-xl md:text-2xl font-black text-[#004c91] tracking-tight bg-white border border-gray-300 focus:border-[#004c91] rounded-xl px-3 py-1 w-full outline-none text-center"
                    />
                  ) : (
                    <h3 className="text-xl md:text-2xl font-black text-[#004c91] tracking-tight">{selectedMember.name}</h3>
                  )}
                  <div className="flex justify-center mt-3">
                    <div className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-100/50 text-[#004c91] rounded-xl text-sm font-bold border border-blue-200/50">
                      <Briefcase className="w-4 h-4" />
                      Vai trò: {selectedMember.systemRole}
                    </div>
                  </div>
                </div>
              </div>

              {/* Right Column (Details List) */}
              <div className="w-full md:w-2/3 md:pt-6 space-y-4">
                <div className="group flex items-center gap-4 bg-white p-4 rounded-2xl shadow-[0_2px_15px_-3px_rgba(0,0,0,0.05)] border border-gray-100 hover:border-[#004c91]/30 hover:shadow-[0_4px_20px_-3px_rgba(0,76,145,0.15)] transition-all">
                  <div className="w-12 h-12 rounded-xl bg-blue-50 group-hover:bg-[#004c91]/10 flex items-center justify-center text-[#004c91] shrink-0 transition-colors">
                    <Mail className="w-6 h-6" />
                  </div>
                  <div className="overflow-hidden w-full">
                    <p className="text-[11px] font-black text-gray-400 uppercase tracking-wider mb-0.5">Email liên hệ</p>
                    {isEditingMember ? (
                      <input
                        type="email"
                        value={editingMemberData.email}
                        onChange={(e) => setEditingMemberData({ ...editingMemberData, email: e.target.value })}
                        className="text-[15px] font-bold text-gray-800 bg-white border border-gray-300 focus:border-[#004c91] rounded-lg px-2 py-1 w-full outline-none"
                      />
                    ) : (
                      <p className="text-[15px] font-bold text-gray-800 truncate">{selectedMember.email}</p>
                    )}
                  </div>
                </div>

                <div className="group flex items-center gap-4 bg-white p-4 rounded-2xl shadow-[0_2px_15px_-3px_rgba(0,0,0,0.05)] border border-gray-100 hover:border-[#f37021]/30 hover:shadow-[0_4px_20px_-3px_rgba(243,112,33,0.15)] transition-all">
                  <div className="w-12 h-12 rounded-xl bg-orange-50 group-hover:bg-[#f37021]/10 flex items-center justify-center text-[#f37021] shrink-0 transition-colors">
                    <Phone className="w-6 h-6" />
                  </div>
                  <div className="w-full">
                    <p className="text-[11px] font-black text-gray-400 uppercase tracking-wider mb-0.5">Số điện thoại</p>
                    {isEditingMember ? (
                      <input
                        type="text"
                        value={editingMemberData.phone}
                        onChange={(e) => setEditingMemberData({ ...editingMemberData, phone: e.target.value })}
                        className="text-[15px] font-bold text-gray-800 bg-white border border-gray-300 focus:border-[#f37021] rounded-lg px-2 py-1 w-full outline-none"
                      />
                    ) : (
                      <p className="text-[15px] font-bold text-gray-800">{selectedMember.phone}</p>
                    )}
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div className="group flex flex-col gap-1.5 self-stretch bg-white p-4 rounded-2xl shadow-[0_2px_15px_-3px_rgba(0,0,0,0.05)] border border-gray-100 hover:border-gray-200 transition-all justify-center">
                    <p className="text-[11px] font-black text-gray-400 uppercase tracking-wider">Giới tính</p>
                    {isEditingMember ? (
                      <select
                        value={editingMemberData.gender}
                        onChange={(e) => setEditingMemberData({ ...editingMemberData, gender: e.target.value })}
                        className="text-[15px] font-bold text-gray-800 bg-white border border-gray-300 focus:border-gray-400 rounded-lg px-2 py-1 outline-none w-full"
                      >
                        <option value="Nam">Nam</option>
                        <option value="Nữ">Nữ</option>
                        <option value="Khác">Khác</option>
                      </select>
                    ) : (
                      <p className="text-[15px] font-bold text-gray-800">{selectedMember.gender}</p>
                    )}
                  </div>

                  <div className="group flex flex-col gap-1.5 self-stretch bg-white p-4 rounded-2xl shadow-[0_2px_15px_-3px_rgba(0,0,0,0.05)] border border-gray-100 hover:border-gray-200 transition-all justify-center">
                    <p className="text-[11px] font-black text-gray-400 uppercase tracking-wider">Cơ sở</p>
                    {isEditingMember ? (
                      <input
                        type="text"
                        disabled
                        value={`Campus ${selectedMember.campus}`}
                        className="text-[15px] font-bold text-gray-400 bg-gray-50 border border-gray-200 rounded-lg px-2 py-1 outline-none cursor-not-allowed w-full"
                      />
                    ) : (
                      <p className="text-[15px] font-bold text-gray-800">Campus {selectedMember.campus}</p>
                    )}
                  </div>
                </div>

                <div className="group flex flex-col gap-1.5 self-stretch bg-white p-4 rounded-2xl shadow-[0_2px_15px_-3px_rgba(0,0,0,0.05)] border border-gray-100 hover:border-gray-200 transition-all justify-center">
                  <p className="text-[11px] font-black text-gray-400 uppercase tracking-wider">Phòng ban</p>
                  {isEditingMember ? (
                    <input
                      type="text"
                      disabled
                      value={departmentInfo.name}
                      className="text-[15px] font-bold text-gray-400 bg-gray-50 border border-gray-200 rounded-lg px-2 py-1 outline-none cursor-not-allowed w-full"
                    />
                  ) : (
                    <p className="text-[15px] font-bold text-gray-800">{departmentInfo.name}</p>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modal Đổi người phụ trách */}
      {isChangeAssigneeModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
          <div className="bg-white rounded-3xl w-full max-w-md shadow-2xl overflow-hidden animate-in zoom-in-95 duration-300 flex flex-col max-h-[90vh]">
            <div className="p-6 border-b border-gray-100 bg-[#004c91] shrink-0">
              <h3 className="text-xl font-black text-white uppercase tracking-tight">Đổi người phụ trách</h3>
              <p className="text-sm font-medium text-blue-100 mt-1">Chọn người phụ trách mới cho nhiệm vụ</p>
            </div>
            <div className="p-6 bg-gray-50/30 overflow-y-auto flex-1">
              <div className="space-y-2">
                {members.map(member => (
                  <div
                    key={member.id}
                    onClick={() => setNewAssigneeId(member.id.toString())}
                    className={`p-3 rounded-xl border-2 cursor-pointer transition-all flex items-center gap-3 ${newAssigneeId === member.id.toString() ? 'border-[#004c91] bg-blue-50 shadow-sm' : 'border-transparent bg-white hover:border-gray-200 hover:bg-gray-50'}`}
                  >
                    <div className={`w-5 h-5 rounded-full border-2 flex items-center justify-center shrink-0 transition-colors ${newAssigneeId === member.id.toString() ? 'border-[#004c91] bg-white' : 'border-gray-300'}`}>
                      {newAssigneeId === member.id.toString() && <div className="w-2.5 h-2.5 rounded-full bg-[#004c91] animate-in zoom-in duration-200"></div>}
                    </div>
                    <div className="flex-1">
                      <p className={`text-[15px] font-bold ${newAssigneeId === member.id.toString() ? 'text-[#004c91]' : 'text-gray-800'}`}>{member.name}</p>
                      <p className="text-[12px] font-medium text-gray-500 truncate">{member.email}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
            <div className="p-6 bg-white border-t border-gray-100 flex justify-end gap-3 shrink-0">
              <button
                onClick={() => setIsChangeAssigneeModalOpen(false)}
                className="px-6 py-3 rounded-xl font-bold text-gray-600 hover:bg-gray-100 transition-colors outline-none"
              >
                Hủy bỏ
              </button>
              <button
                onClick={() => {
                  setTasks(tasks.map(t => t.id === selectedTaskForAssigneeChange?.id ? { ...t, assigneeId: newAssigneeId } : t));
                  setIsChangeAssigneeModalOpen(false);
                }}
                disabled={!newAssigneeId}
                className="px-6 py-3 rounded-xl font-black text-white bg-[#004c91] hover:bg-[#003b73] transition-colors shadow-lg shadow-[#004c91]/20 disabled:opacity-50 disabled:cursor-not-allowed outline-none uppercase tracking-wider"
              >
                Xác nhận
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal Thay đổi trưởng phòng */}
      {isChangeLeaderModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
          <div className="bg-white rounded-3xl w-full max-w-md shadow-2xl overflow-hidden animate-in zoom-in-95 duration-300 flex flex-col max-h-[90vh]">
            <div className="p-6 border-b border-gray-100 bg-[#004c91] shrink-0">
              <h3 className="text-xl font-black text-white uppercase tracking-tight">Thay đổi trưởng phòng</h3>
              <p className="text-sm font-medium text-blue-100 mt-1">Chọn trưởng phòng mới</p>
            </div>
            <div className="p-6 bg-gray-50/30 overflow-y-auto flex-1">
              <div className="space-y-2">
                {members.map(member => (
                  <div
                    key={member.id}
                    onClick={() => setNewLeaderId(member.id.toString())}
                    className={`p-3 rounded-xl border-2 cursor-pointer transition-all flex items-center gap-3 ${newLeaderId === member.id.toString() ? 'border-[#004c91] bg-blue-50 shadow-sm' : 'border-transparent bg-white hover:border-gray-200 hover:bg-gray-50'}`}
                  >
                    <div className={`w-5 h-5 rounded-full border-2 flex items-center justify-center shrink-0 transition-colors ${newLeaderId === member.id.toString() ? 'border-[#004c91] bg-white' : 'border-gray-300'}`}>
                      {newLeaderId === member.id.toString() && <div className="w-2.5 h-2.5 rounded-full bg-[#004c91] animate-in zoom-in duration-200"></div>}
                    </div>
                    <div className="flex-1">
                      <p className={`text-[15px] font-bold ${newLeaderId === member.id.toString() ? 'text-[#004c91]' : 'text-gray-800'}`}>{member.name}</p>
                      <p className="text-[12px] font-medium text-gray-500 truncate">{member.email}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
            <div className="p-6 bg-white border-t border-gray-100 flex justify-end gap-3 shrink-0">
              <button
                onClick={() => setIsChangeLeaderModalOpen(false)}
                className="px-6 py-3 rounded-xl font-bold text-gray-600 hover:bg-gray-100 transition-colors outline-none"
              >
                Hủy bỏ
              </button>
              <button
                onClick={handleChangeLeader}
                disabled={!newLeaderId}
                className="px-6 py-3 rounded-xl font-black text-white bg-[#004c91] hover:bg-[#003b73] transition-colors shadow-lg shadow-[#004c91]/20 disabled:opacity-50 disabled:cursor-not-allowed outline-none uppercase tracking-wider"
              >
                Xác nhận
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal Confirm Delete Member */}
      {isDeleteMemberModalOpen && memberToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-0">
          <div
            className="fixed inset-0 bg-black/40 backdrop-blur-sm transition-opacity"
            onClick={() => setIsDeleteMemberModalOpen(false)}
          ></div>

          <div className="bg-white rounded-[24px] shadow-2xl w-full max-w-md relative z-10 overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="p-6 text-center">
              <div className="w-16 h-16 rounded-full bg-red-100 text-red-600 flex items-center justify-center mx-auto mb-4">
                <Trash2 className="w-8 h-8" />
              </div>
              <h3 className="text-2xl font-black text-gray-900 tracking-tight mb-2">Xác nhận xóa tài khoản</h3>
              <p className="text-gray-600 mb-6 font-medium leading-relaxed">
                Bạn có chắc chắn muốn xóa nhân sự <span className="font-bold text-gray-900">{memberToDelete.name}</span> khỏi danh sách không? Hành động này không thể hoàn tác.
              </p>

              <div className="flex gap-3">
                <button
                  onClick={() => setIsDeleteMemberModalOpen(false)}
                  className="flex-1 px-4 py-3 rounded-xl font-bold text-gray-600 bg-gray-100 hover:bg-gray-200 transition-colors outline-none"
                >
                  Hủy bỏ
                </button>
                <button
                  onClick={handleDeleteMember}
                  className="flex-1 px-4 py-3 rounded-xl font-black text-white bg-red-600 hover:bg-red-700 transition-colors shadow-lg shadow-red-600/20 outline-none"
                >
                  Xác nhận xóa
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modal từ chối nhiệm vụ */}
      {isRejectTaskModalOpen && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-md overflow-hidden shadow-2xl animate-fade-in-quick">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between">
              <h3 className="font-bold text-gray-800 text-lg">Từ chối thư mời</h3>
              <button
                onClick={() => setIsRejectTaskModalOpen(false)}
                className="text-gray-400 hover:bg-gray-100 p-2 rounded-xl transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-5 space-y-4">
              <div className="space-y-1.5">
                <label className="text-sm font-semibold text-gray-700">Lý do từ chối</label>
                <textarea
                  className="w-full border border-gray-200 rounded-xl px-4 py-3 focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all text-sm min-h-[100px]"
                  placeholder="Nhập lý do..."
                  value={rejectTaskReason}
                  onChange={(e) => setRejectTaskReason(e.target.value)}
                />
              </div>
            </div>
            <div className="p-5 border-t border-gray-100 bg-gray-50 flex justify-end gap-3">
              <button
                onClick={() => setIsRejectTaskModalOpen(false)}
                className="px-5 py-2.5 text-sm font-semibold text-gray-600 hover:bg-white rounded-xl border border-transparent hover:border-gray-200 hover:shadow-sm transition-all"
              >
                Hủy
              </button>
              <button
                onClick={() => {
                  setTasks(tasks.map(t => t.id === taskToReject ? { ...t, status: "Từ chối", rejectReason: rejectTaskReason } : t));
                  setIsRejectTaskModalOpen(false);
                }}
                disabled={!rejectTaskReason.trim()}
                className="px-6 py-2.5 text-sm font-bold text-white bg-red-600 hover:bg-red-700 rounded-xl shadow-sm hover:shadow transition-all disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Xác nhận
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal xep lý do từ chối */}
      {isViewRejectReasonModalOpen && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-md overflow-hidden shadow-2xl animate-fade-in-quick">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between bg-red-50">
              <h3 className="font-bold text-red-700 text-lg flex items-center gap-2">
                <XCircle className="w-5 h-5" />
                Lý do từ chối
              </h3>
              <button
                onClick={() => setIsViewRejectReasonModalOpen(false)}
                className="text-red-700 hover:bg-red-100 p-2 rounded-xl transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6">
              <p className="text-gray-700 font-medium whitespace-pre-wrap">{selectedRejectReason}</p>
            </div>
            <div className="p-5 border-t border-gray-100 bg-gray-50 flex justify-end">
              <button
                onClick={() => setIsViewRejectReasonModalOpen(false)}
                className="px-6 py-2.5 text-sm font-bold text-white bg-[#004c91] hover:bg-[#003b73] rounded-xl shadow-sm hover:shadow transition-all"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}


