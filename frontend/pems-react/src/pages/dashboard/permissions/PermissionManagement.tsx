/**
 * Trang PermissionManagement
 * Phân quyền module/tính năng cho các nhóm tài khoản thuộc hệ thống.
 */

import React, { useState } from 'react';
import { 
  Search, 
  Plus, 
  Edit3, 
  Trash2, 
  Shield, 
  Users, 
  ChevronDown, 
  ChevronRight, 
  X,
  AlertCircle
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';

import { useNavigate } from 'react-router-dom';

type Role = {
  id: string;
  name: string;
  code: string;
  description: string;
  userCount: number;
  status: 'active' | 'inactive';
};

const initialRoles: Role[] = [
  { id: '1', name: 'ADMIN', code: 'ADMIN', description: 'Quản trị viên toàn hệ thống', userCount: 5, status: 'active' },
  { id: '2', name: 'HO', code: 'HO', description: 'Cán bộ Head Office (Điều phối chung)', userCount: 15, status: 'active' },
  { id: '3', name: 'Staff Leader', code: 'STAFF_LEADER', description: 'Trưởng ban đối ngoại Campus', userCount: 8, status: 'active' },
  { id: '4', name: 'Campus Staff', code: 'STAFF', description: 'Cán bộ phòng IC/Đối ngoại cơ sở', userCount: 32, status: 'active' },
  { id: '5', name: 'Visitor', code: 'VISITOR', description: 'Khách tham quan', userCount: 120, status: 'active' },
];

const permissionGroups = [
  {
    id: 'guest',
    name: 'Nhóm Quản lý đoàn khách',
    permissions: [
      { id: 'guest_create', label: 'Đăng ký lịch tham quan' },
      { id: 'guest_approve', label: 'Phê duyệt yêu cầu liên cơ sở' },
      { id: 'guest_close', label: 'Đóng dữ liệu đoàn khách (Đã hoàn thành)' },
    ]
  },
  {
    id: 'doc',
    name: 'Nhóm Quản lý tài liệu',
    permissions: [
      { id: 'doc_view', label: 'Xem tài liệu' },
      { id: 'doc_upload', label: 'Tải lên tài liệu' },
      { id: 'doc_delete', label: 'Xóa tài liệu toàn hệ thống' },
    ]
  },
  {
    id: 'user',
    name: 'Nhóm Quản lý người dùng & Hệ thống',
    permissions: [
      { id: 'user_view', label: 'Xem danh sách người dùng' },
      { id: 'user_manage', label: 'Phân quyền & Chỉnh sửa tài khoản' },
    ]
  }
];

export function PermissionManagement() {
  const navigate = useNavigate();
  const [roles, setRoles] = useState<Role[]>(initialRoles);
  const [selectedRoleId, setSelectedRoleId] = useState<string>(roles[0].id);
  const [searchQuery, setSearchQuery] = useState('');
  
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [editingRole, setEditingRole] = useState<Role | null>(null);
  const [roleToDelete, setRoleToDelete] = useState<Role | null>(null);

  // States cho form thêm mới
  const [newRoleName, setNewRoleName] = useState('');
  const [newRoleCode, setNewRoleCode] = useState('');
  const [newRoleDesc, setNewRoleDesc] = useState('');

  // States cho nhóm quyền (expand/collapse)
  const [expandedGroups, setExpandedGroups] = useState<string[]>(permissionGroups.map(g => g.id));
  
  // State phân quyền (mock: lưu theo [roleId_permissionId]: boolean)
  const [rolePerms, setRolePerms] = useState<Record<string, boolean>>({
    '2_guest_create': true,
    '2_guest_approve': true,
    '2_doc_view': true,
    '2_doc_upload': true,
  });

  const selectedRole = roles.find(r => r.id === selectedRoleId);
  const filteredRoles = roles.filter(r => r.name.toLowerCase().includes(searchQuery.toLowerCase()) || r.code.toLowerCase().includes(searchQuery.toLowerCase()));

  const toggleGroup = (groupId: string) => {
    setExpandedGroups(prev => 
      prev.includes(groupId) ? prev.filter(id => id !== groupId) : [...prev, groupId]
    );
  };

  const handleTogglePermission = (permId: string) => {
    if (!selectedRoleId) return;
    const key = `${selectedRoleId}_${permId}`;
    setRolePerms(prev => ({
      ...prev,
      [key]: !prev[key]
    }));
  };

  const handleCreateRole = () => {
    if (!newRoleName.trim() || !newRoleCode.trim()) return;
    const newRole: Role = {
      id: Date.now().toString(),
      name: newRoleName,
      code: newRoleCode,
      description: newRoleDesc,
      userCount: 0,
      status: 'active'
    };
    setRoles([...roles, newRole]);
    setIsCreateModalOpen(false);
    setNewRoleName('');
    setNewRoleCode('');
    setNewRoleDesc('');
    setSelectedRoleId(newRole.id); // Tự động chọn role mới tạo
  };

  const handleDeleteRole = (e: React.MouseEvent, role: Role) => {
    e.stopPropagation();
    setRoleToDelete(role);
  };

  const openEditModal = (e: React.MouseEvent, role: Role) => {
    e.stopPropagation();
    setEditingRole(role);
    setIsEditModalOpen(true);
  };

  const handleUpdateRole = () => {
    if (!editingRole) return;
    setRoles(roles.map(r => r.id === editingRole.id ? editingRole : r));
    setIsEditModalOpen(false);
    setEditingRole(null);
  };

  return (
    <div className="space-y-8 animate-in fade-in duration-500 pb-24 pt-4 h-full flex flex-col">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-6">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý Vai trò & Phân quyền</span>
      </div>

      {/* Header */}
      <div className="flex flex-col gap-4">
        <div>
          <h2 className="text-3xl font-black text-[#004c91] tracking-tight">Quản lý Vai trò & Phân quyền</h2>
          <p className="text-base font-medium text-slate-500 mt-1">Cấu hình chi tiết chức năng truy cập cho từng nhóm người dùng</p>
        </div>
        <div className="flex justify-end">
          <button 
            onClick={() => setIsCreateModalOpen(true)}
            className="flex w-full sm:w-auto items-center justify-center gap-2 bg-[#f37021] hover:bg-[#d95d18] text-white px-6 py-3 rounded-xl font-bold transition-all shadow-[0_4px_12px_rgba(243,112,33,0.25)] hover:shadow-[0_6px_16px_rgba(243,112,33,0.35)] hover:-translate-y-0.5 active:translate-y-0"
          >
            <Plus className="w-5 h-5" />
            <span>Tạo Vai Trò Mới</span>
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 flex-1 h-[calc(100vh-240px)] min-h-[500px] pb-6 relative">
        {/* Cột trái: Danh sách Vai trò (1/3) */}
        <div className="col-span-1 flex flex-col min-h-0">
          <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm flex flex-col flex-1 min-h-0">
            <div className="mb-4 shrink-0">
              <div className="relative">
                <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" />
                <input 
                  type="text" 
                  placeholder="Tìm kiếm vai trò..." 
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="w-full bg-slate-50 border border-slate-200 rounded-xl pl-11 pr-4 py-2.5 text-sm font-medium text-slate-800 placeholder-slate-400 focus:outline-none focus:border-[#004c91] focus:bg-white transition-colors"
                />
              </div>
            </div>

            <div className="flex-1 overflow-y-auto space-y-3 pr-2 -mr-2 scrollbar-thin scrollbar-thumb-slate-200 hover:scrollbar-thumb-slate-300">
              {filteredRoles.map(role => (
                <div 
                  key={role.id}
                  onClick={() => setSelectedRoleId(role.id)}
                  className={`group relative p-4 rounded-xl border transition-all cursor-pointer overflow-hidden ${
                    selectedRoleId === role.id 
                    ? 'border-[#004c91] bg-blue-50/50 shadow-sm' 
                    : 'border-slate-200 bg-white hover:border-[#004c91]/50 hover:bg-slate-50'
                  } ${role.status === 'inactive' ? 'opacity-60 grayscale' : ''}`}
                >
                  <div className="flex justify-between items-start mb-2">
                    <h3 className={`font-bold ${selectedRoleId === role.id ? 'text-[#004c91]' : 'text-slate-800'}`}>
                      {role.name}
                    </h3>
                    <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                      <button 
                        onClick={(e) => openEditModal(e, role)}
                        className="p-1.5 text-slate-400 hover:text-blue-600 hover:bg-blue-100 rounded-md transition-colors"
                        title="Sửa"
                      >
                        <Edit3 className="w-4 h-4" />
                      </button>
                      <button 
                        onClick={(e) => handleDeleteRole(e, role)}
                        className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-100 rounded-md transition-colors"
                        title="Vô hiệu hóa"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                  <p className="text-xs text-slate-500 font-medium mb-3 line-clamp-2">
                    {role.description}
                  </p>
                  <div className="flex items-center gap-1.5 text-xs font-bold text-slate-600 bg-slate-100 w-fit px-2.5 py-1 rounded-md">
                    <Users className="w-3.5 h-3.5 text-slate-500" />
                    <span>{role.userCount} người dùng</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Cột phải: Ma trận phân quyền chi tiết (2/3) */}
        <div className="col-span-1 lg:col-span-2 flex flex-col min-h-0">
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm flex flex-col flex-1 overflow-hidden relative min-h-0">
            <div className="px-6 py-5 border-b border-slate-200 bg-slate-50/80 shrink-0">
              <h3 className="text-lg font-bold text-slate-800 flex items-center gap-2">
                Cấu hình quyền cho vai trò: 
                {selectedRole ? <span className="text-[#f37021]">{selectedRole.name}</span> : <span className="text-slate-400">Chưa chọn</span>}
              </h3>
              <p className="text-sm font-medium text-slate-500 mt-0.5">Tích chọn để cấp hoặc hủy quyền. Những thay đổi cần được lưu lại.</p>
            </div>

            <div className="flex-1 overflow-y-auto p-6 pb-20">
              {!selectedRole ? (
                <div className="h-full flex flex-col items-center justify-center text-slate-400">
                  <Shield className="w-16 h-16 mb-4 opacity-50" />
                  <p className="font-medium">Vui lòng chọn một vai trò ở danh sách bên trái</p>
                </div>
              ) : (
                <div className="space-y-4">
                  {permissionGroups.map(group => {
                    const isExpanded = expandedGroups.includes(group.id);
                    return (
                      <div key={group.id} className="border border-slate-200 rounded-xl overflow-hidden bg-white">
                        <button 
                          onClick={() => toggleGroup(group.id)}
                          className="w-full flex items-center justify-between px-5 py-4 bg-[#004c91] hover:bg-[#00386b] transition-colors text-left"
                        >
                          <span className="font-bold text-white">{group.name}</span>
                          {isExpanded ? <ChevronDown className="w-5 h-5 text-white/80" /> : <ChevronRight className="w-5 h-5 text-white/80" />}
                        </button>
                        
                        <AnimatePresence>
                          {isExpanded && (
                            <motion.div 
                              initial={{ height: 0 }}
                              animate={{ height: 'auto' }}
                              exit={{ height: 0 }}
                              className="overflow-hidden"
                            >
                              <div className="p-2 space-y-1 bg-white">
                                {group.permissions.map(perm => {
                                  const isChecked = !!rolePerms[`${selectedRoleId}_${perm.id}`];
                                  return (
                                    <div 
                                      key={perm.id} 
                                      onClick={() => handleTogglePermission(perm.id)}
                                      className="flex items-start gap-3 px-4 py-3 hover:bg-slate-50 rounded-lg cursor-pointer transition-colors"
                                    >
                                      <div className={`mt-0.5 w-5 h-5 rounded flex items-center justify-center shrink-0 transition-colors ${
                                        isChecked ? 'bg-[#004c91] border-[#004c91]' : 'border-2 border-slate-300'
                                      }`}>
                                        {isChecked && <svg className="w-3.5 h-3.5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}><path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" /></svg>}
                                      </div>
                                      <div>
                                        <p className="font-bold text-slate-800 text-sm">{perm.label}</p>
                                      </div>
                                    </div>
                                  );
                                })}
                              </div>
                            </motion.div>
                          )}
                        </AnimatePresence>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>

            {/* Sticky Bottom Bar */}
            {selectedRole && (
              <div className="absolute bottom-0 left-0 right-0 p-4 bg-white/95 backdrop-blur-md border-t border-slate-200 flex items-center justify-between shadow-[0_-4px_10px_rgba(0,0,0,0.02)]">
                <p className="text-sm font-medium text-slate-500">Đã tùy chỉnh cấu hình cho <strong className="text-slate-800">{selectedRole.name}</strong></p>
                <button className="flex items-center justify-center gap-2 bg-[#00a651] hover:bg-[#008f45] text-white px-8 py-2.5 rounded-xl font-bold transition-all shadow-md shadow-emerald-500/20 active:scale-95">
                  LƯU CẤU HÌNH
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Modal Cập nhật Vai trò */}
      <AnimatePresence>
        {isEditModalOpen && editingRole && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm px-4"
          >
            <motion.div 
              initial={{ scale: 0.95, opacity: 0, y: 20 }}
              animate={{ scale: 1, opacity: 1, y: 0 }}
              exit={{ scale: 0.95, opacity: 0, y: 20 }}
              className="bg-white rounded-2xl shadow-xl w-full max-w-lg overflow-hidden flex flex-col"
            >
              <div className="bg-[#004c91] px-6 py-4 flex items-center justify-between">
                 <h2 className="text-lg font-bold text-white tracking-tight">Cập nhật Vai trò</h2>
                 <button onClick={() => setIsEditModalOpen(false)} className="text-white/80 hover:text-white p-1 rounded-full transition-colors outline-none cursor-pointer">
                    <X className="w-5 h-5" />
                 </button>
              </div>
              <div className="p-6 space-y-5">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-2">Tên vai trò <span className="text-red-500">*</span></label>
                  <input 
                    type="text" 
                    value={editingRole.name}
                    onChange={(e) => setEditingRole({...editingRole, name: e.target.value})}
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:border-[#004c91] focus:bg-white font-medium text-slate-800 transition-colors"
                  />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-2">Mã hệ thống</label>
                  <input 
                    type="text" 
                    value={editingRole.code}
                    disabled
                    className="w-full px-4 py-3 bg-slate-100 border border-slate-200 rounded-xl focus:outline-none font-bold text-slate-500 cursor-not-allowed"
                  />
                  <p className="text-xs font-medium text-slate-400 mt-1">Mã hệ thống không thể thay đổi sau khi tạo</p>
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-2">Mô tả chức năng</label>
                  <textarea 
                    value={editingRole.description}
                    onChange={(e) => setEditingRole({...editingRole, description: e.target.value})}
                    rows={3}
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:border-[#004c91] focus:bg-white font-medium text-slate-800 transition-colors resize-none"
                  ></textarea>
                </div>
              </div>
              <div className="px-6 py-4 bg-slate-50 border-t border-slate-200 flex items-center gap-3 justify-end">
                <button 
                  onClick={() => setIsEditModalOpen(false)}
                  className="px-6 py-2.5 rounded-xl border border-slate-300 text-slate-700 font-bold hover:bg-slate-100 transition-colors"
                >
                  Hủy bỏ
                </button>
                <button 
                  onClick={handleUpdateRole}
                  className="px-6 py-2.5 rounded-xl bg-[#004c91] text-white font-bold hover:bg-[#00386b] transition-colors shadow-sm"
                >
                  Cập nhật
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Modal Tạo Vai trò mới */}
      <AnimatePresence>
        {isCreateModalOpen && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm px-4"
          >
            <motion.div 
              initial={{ scale: 0.95, opacity: 0, y: 20 }}
              animate={{ scale: 1, opacity: 1, y: 0 }}
              exit={{ scale: 0.95, opacity: 0, y: 20 }}
              className="bg-white rounded-2xl shadow-xl w-full max-w-lg overflow-hidden flex flex-col"
            >
              <div className="bg-[#f37021] px-6 py-4 flex items-center justify-between">
                 <h2 className="text-lg font-bold text-white tracking-tight">Tạo Vai trò hệ thống mới</h2>
                 <button onClick={() => setIsCreateModalOpen(false)} className="text-white/80 hover:text-white p-1 rounded-full transition-colors outline-none cursor-pointer">
                    <X className="w-5 h-5" />
                 </button>
              </div>
              <div className="p-6 space-y-5">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-2">Tên vai trò <span className="text-red-500">*</span></label>
                  <input 
                    type="text" 
                    value={newRoleName}
                    onChange={(e) => {
                      setNewRoleName(e.target.value);
                      // Tự động generate mã hệ thống
                      if (!newRoleCode) {
                        const code = e.target.value.toUpperCase().replace(/\s+/g, '_').normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[^A-Z0-9_]/g, '');
                        setNewRoleCode(code);
                      }
                    }}
                    placeholder="Ví dụ: Cán bộ Hậu cần"
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:border-[#f37021] focus:bg-white font-medium text-slate-800 transition-colors"
                  />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-2">Mã viết tắt hệ thống <span className="text-red-500">*</span></label>
                  <input 
                    type="text" 
                    value={newRoleCode}
                    onChange={(e) => setNewRoleCode(e.target.value.toUpperCase().replace(/\s+/g, '_'))}
                    placeholder="Ví dụ: CAN_BO_HAU_CAN"
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:border-[#f37021] focus:bg-white font-medium text-slate-800 transition-colors uppercase"
                  />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-2">Mô tả chức năng</label>
                  <textarea 
                    value={newRoleDesc}
                    onChange={(e) => setNewRoleDesc(e.target.value)}
                    placeholder="Ví dụ: Nhân sự phụ trách chuẩn bị phòng họp, xe điện và teabreak tại cơ sở."
                    rows={3}
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:border-[#f37021] focus:bg-white font-medium text-slate-800 transition-colors resize-none"
                  ></textarea>
                </div>
              </div>
              <div className="px-6 py-4 bg-slate-50 border-t border-slate-200 flex items-center gap-3 justify-end">
                <button 
                  onClick={() => setIsCreateModalOpen(false)}
                  className="px-6 py-2.5 rounded-xl border border-slate-300 text-slate-700 font-bold hover:bg-slate-100 transition-colors"
                >
                  Hủy bỏ
                </button>
                <button 
                  onClick={handleCreateRole}
                  disabled={!newRoleName.trim() || !newRoleCode.trim()}
                  className="px-6 py-2.5 rounded-xl bg-[#f37021] text-white font-bold hover:bg-[#d95d18] transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Xác nhận tạo
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Modal Xóa/Vô hiệu hóa Vai trò */}
      <AnimatePresence>
        {roleToDelete && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm px-4"
          >
            <motion.div 
              initial={{ scale: 0.95, opacity: 0, y: 20 }}
              animate={{ scale: 1, opacity: 1, y: 0 }}
              exit={{ scale: 0.95, opacity: 0, y: 20 }}
              className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden flex flex-col"
            >
              <div className="bg-red-600 px-6 py-4 flex items-center justify-between">
                 <h2 className="text-lg font-bold text-white tracking-tight">Xóa / Vô hiệu hóa Vai trò</h2>
                 <button onClick={() => setRoleToDelete(null)} className="text-white/80 hover:text-white p-1 rounded-full transition-colors outline-none cursor-pointer">
                    <X className="w-5 h-5" />
                 </button>
              </div>
              <div className="p-6">
                {roleToDelete.userCount > 0 ? (
                  <div className="flex flex-col items-center text-center space-y-4">
                    <div className="w-16 h-16 rounded-full bg-orange-100 flex items-center justify-center text-orange-500">
                      <AlertCircle className="w-8 h-8" />
                    </div>
                    <div>
                      <h3 className="text-lg font-bold text-slate-800">Không thể vô hiệu hóa!</h3>
                      <p className="text-sm font-medium text-slate-500 mt-2">
                        Vai trò <strong className="text-slate-800">{roleToDelete.name}</strong> hiện đang có <strong>{roleToDelete.userCount}</strong> người dùng. Vui lòng chuyển họ sang vai trò khác trước khi thực hiện thao tác này.
                      </p>
                    </div>
                  </div>
                ) : (
                  <div className="flex flex-col items-center text-center space-y-4">
                    <div className="w-16 h-16 rounded-full bg-red-100 flex items-center justify-center text-red-600">
                      <Trash2 className="w-8 h-8" />
                    </div>
                    <div>
                      <h3 className="text-lg font-bold text-slate-800">Xác nhận vô hiệu hóa</h3>
                      <p className="text-sm font-medium text-slate-500 mt-2">
                        Bạn có chắc chắn muốn chuyển vai trò <strong className="text-slate-800">{roleToDelete.name}</strong> sang trạng thái Ngừng hoạt động? Hành động này có thể khiến người dùng mất quyền truy cập nếu được gán lại.
                      </p>
                    </div>
                  </div>
                )}
              </div>
              <div className="px-6 py-4 bg-slate-50 border-t border-slate-200 flex items-center gap-3 justify-end">
                <button 
                  onClick={() => setRoleToDelete(null)}
                  className="px-5 py-2.5 rounded-xl border border-slate-300 text-slate-700 font-bold hover:bg-slate-100 transition-colors"
                >
                  {roleToDelete.userCount > 0 ? 'Đóng' : 'Hủy bỏ'}
                </button>
                {roleToDelete.userCount === 0 && (
                  <button 
                    onClick={() => {
                      setRoles(roles.map(r => r.id === roleToDelete.id ? { ...r, status: 'inactive' } : r));
                      setRoleToDelete(null);
                    }}
                    className="px-5 py-2.5 rounded-xl bg-red-600 text-white font-bold hover:bg-red-700 transition-colors shadow-sm"
                  >
                    Xác nhận vô hiệu hóa
                  </button>
                )}
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

    </div>
  );
}
