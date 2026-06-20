/**
 * Trang DepartmentManagement
 * Hoạt động điều phối, theo dõi cấu trúc định biên của các phòng ban hiện hữu.
 */

import React, { useState } from "react";
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
  Clock,
  MapPin,
  X,
} from "lucide-react";

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

  const stats = [
    { label: "Tổng số phòng", value: "06", icon: Briefcase, color: "text-[#004c91]", bg: "bg-[#e6eff7]" },
    { label: "Nhân sự", value: "54", icon: Users, color: "text-[#0aa14f]", bg: "bg-[#eaffe4]" },
  ];

  // Lọc dữ liệu
  const filteredData = departments.filter((item) => {
    const matchesSearch = item.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
                          item.head.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesStatus = statusFilter ? item.status === statusFilter : true;
    const matchesCampus = (!isHO || !campusFilter) ? true : item.campus === campusFilter;
    return matchesSearch && matchesStatus && matchesCampus;
  });

  // Phân trang
  const totalPages = Math.ceil(filteredData.length / itemsPerPage);
  const paginatedData = filteredData.slice(
    (currentPage - 1) * itemsPerPage,
    currentPage * itemsPerPage
  );

  const toggleStatus = (id: number) => {
    setDepartments((prev) =>
      prev.map((dept) =>
        dept.id === id
          ? {
              ...dept,
              status: dept.status === "Hoạt động" ? "Ngừng hoạt động" : "Hoạt động",
            }
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
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12">
      {/* Breadcrumb */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button
          onClick={() => navigate("/dashboard")}
          className="hover:text-[#004c91] transition-colors"
        >
          Dashboard
        </button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Quản lý phòng ban</span>
      </div>

      {/* Header */}
      <div className="border-b border-gray-100 pb-4 mb-6 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý phòng ban</h1>
      </div>

      {/* Stats Cards */}
      {!isStaffLeader && (
        isHO ? (
          <div className="grid grid-cols-1 md:grid-cols-5 gap-4 mb-8">
            {CAMPUSES.map((campus) => {
              const campusDepts = departments.filter((d) => d.campus === campus);
              const totalDepts = campusDepts.length;
              const totalStaff = campusDepts.reduce((sum, d) => sum + d.staffCount, 0);
              const totalWait = campusDepts.reduce((sum, d) => sum + d.waitingAccounts, 0);

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
        <div className="relative flex-1 min-w-[250px] max-w-md">
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
          onClick={() => setIsAddModalOpen(true)}
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
                  <tr
                    key={item.id}
                    className="hover:bg-gray-50/80 transition-colors group"
                  >
                    <td className="p-4 pl-6 font-medium text-gray-500">
                      {(currentPage - 1) * itemsPerPage + index + 1}
                    </td>
                    <td className="p-4">
                      <span className="font-bold text-gray-800 whitespace-nowrap">
                        {item.name}
                      </span>
                    </td>
                    {isHO && (
                      <td className="p-4 font-medium text-[#f37021] whitespace-nowrap">
                        <div className="flex items-center gap-1">
                          <MapPin className="w-3 h-3" />
                          {item.campus}
                        </div>
                      </td>
                    )}
                    {!isStaffLeader && (
                      <td className="p-4 text-center font-medium whitespace-nowrap">
                        <div className="flex items-center justify-center gap-1">
                          {item.staffCount}
                          <User className="w-3.5 h-3.5 text-gray-400" />
                        </div>
                      </td>
                    )}
                    <td className="p-4 font-medium text-gray-700 whitespace-nowrap">
                      {item.head}
                    </td>
                    <td className="p-4 text-center whitespace-nowrap">
                      <StatusBadge status={item.status} />
                    </td>
                    <td className="p-4 text-center">
                      <div className="flex items-center justify-center gap-3">
                        {!isStaffLeader && (
                          <button
                            onClick={() => navigate(`/dashboard/departments/${item.id}`)}
                            className="p-1.5 rounded-lg text-gray-400 hover:bg-[#e6eff7] hover:text-[#004c91] transition-colors outline-none cursor-pointer"
                            title="Xem chi tiết"
                          >
                            <Eye className="w-4 h-4" />
                          </button>
                        )}
                        <button
                          onClick={() => toggleStatus(item.id)}
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
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={isHO ? 7 : (isStaffLeader ? 5 : 6)}
                    className="py-12 text-center text-gray-500 bg-white font-medium"
                  >
                    Không tìm thấy dữ liệu nào
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
          <div 
            className="absolute inset-0 bg-black/40 backdrop-blur-sm"
            onClick={() => setIsAddModalOpen(false)}
          ></div>
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
                onChange={(e) => setNewDepartmentName(e.target.value)}
                placeholder="Nhập tên phòng ban..."
                className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-800 transition-shadow outline-none shadow-sm pb-4"
                autoFocus
              />

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
                  <select
                    className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-800 transition-shadow outline-none shadow-sm bg-white"
                  >
                    {CAMPUSES.map((cs) => (
                      <option key={cs} value={cs}>{cs}</option>
                    ))}
                  </select>
                </div>
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
                onClick={handleCreateDepartment}
                disabled={!newDepartmentName.trim()}
                className="px-5 py-2.5 rounded-xl font-bold text-white bg-[#f37021] hover:bg-[#d9621a] disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-sm outline-none"
              >
                Tạo mới
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

