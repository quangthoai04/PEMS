import React, { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Search, Filter, Eye, ChevronLeft, ChevronRight, ListTodo, X, CheckCircle2 } from "lucide-react";

export function DeptLeadAssignmentTab() {
  const navigate = useNavigate();
  const { id } = useParams();

  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const isDeptLeader = user?.role?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'LEADER';
  const isStaffLeader = user?.role?.toUpperCase() === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER';
  const isLeader = user?.role === 'ADMIN' || user?.role === 'Admin' || user?.role === 'HO' || isStaffLeader || isDeptLeader;

  const mockMembers = Array.from({ length: 15 }, (_, i) => ({
    id: i === 0 ? "dept_leader" : i === 1 ? "dept_staff" : (i + 1).toString(),
    name: i === 0 ? "Nguyễn Văn Trưởng Phòng" : i === 1 ? "Nguyễn Văn Nhân Viên" : `Nhân viên ${i + 1}`,
    role: i === 0 ? "Trưởng phòng" : "Nhân viên",
  }));

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
      delegation: "Đoàn ĐH Greenwich",
      task: "Chuẩn bị tài liệu hướng dẫn",
      status: "Từ chối",
      assigneeId: "dept_staff",
      originalAssigneeId: "dept_staff",
      rejectReason: "Quá tải công việc, xin điều phối người khác"
    },
    {
      id: 5,
      delegation: "Đoàn khách quan chức năng",
      task: "Kiểm tra an ninh mạng",
      status: "Hoàn thành",
      assigneeId: "dept_staff",
      originalAssigneeId: "dept_staff",
      rejectReason: undefined
    },
  ]);

  const [taskPage, setTaskPage] = useState(1);
  const [tasksPerPage, setTasksPerPage] = useState(5);
  const [taskSearch, setTaskSearch] = useState("");
  const [taskStatus, setTaskStatus] = useState("");

  const filteredTasks = tasks.filter((t) => {
    const matchesSearch = t.delegation.toLowerCase().includes(taskSearch.toLowerCase()) || 
                          t.task.toLowerCase().includes(taskSearch.toLowerCase());
    const matchesStatus = taskStatus ? t.status === taskStatus : true;
    return matchesSearch && matchesStatus;
  });

  const totalTaskPages = Math.ceil(filteredTasks.length / tasksPerPage);
  const paginatedTasks = filteredTasks.slice(
    (taskPage - 1) * tasksPerPage,
    taskPage * tasksPerPage
  );

  const [isChangeAssigneeModalOpen, setIsChangeAssigneeModalOpen] = useState(false);
  const [selectedTaskForAssigneeChange, setSelectedTaskForAssigneeChange] = useState<any>(null);
  const [newAssigneeId, setNewAssigneeId] = useState("");
  const [isAssigning, setIsAssigning] = useState(false);
  const [showSuccessModal, setShowSuccessModal] = useState(false);
  const [successMessage, setSuccessMessage] = useState("");

  const handleChangeAssignee = () => {
    if (!newAssigneeId) return;
    setIsAssigning(true);
    setTimeout(() => {
      setTasks(prev => prev.map(t => {
        if (t.id === selectedTaskForAssigneeChange.id) {
          return { ...t, assigneeId: newAssigneeId, status: t.status === "Từ chối" ? "Chưa làm" : t.status };
        }
        return t;
      }));
      setIsAssigning(false);
      setIsChangeAssigneeModalOpen(false);
      setSuccessMessage("Đã cập nhật người phụ trách thành công");
      setShowSuccessModal(true);
    }, 600);
  };

  return (
    <div>
      {/* 4. Khối hiệu suất / Nhiệm vụ */}
      <div className="mb-4">
        <div className="flex items-center gap-3 px-2">
          <div className="p-2 bg-[#e6eff7] rounded-xl text-[#004c91]">
            <ListTodo className="w-6 h-6 " />
          </div>
          <h2 className="text-xl font-extrabold text-[#004c91] uppercase tracking-wide m-0">
            NHIỆM VỤ ĐIỀU PHỐI & THƯ MỜI THAM GIA
          </h2>
        </div>
      </div>
      
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden mb-8">
        {/* Lọc & Tìm kiếm */}
        <div className="p-5 border-b border-[#004c91]/10 flex flex-col md:flex-row gap-4 justify-between bg-[#004c91]">
          <div className="relative max-w-sm w-full">
            <input 
              type="text"
              placeholder="Tìm kiếm nhiệm vụ..."
              className="w-full pl-10 pr-4 py-2 border-none rounded-xl focus:outline-none focus:ring-2 focus:ring-white bg-white/10 text-white placeholder-white/60 text-sm"
              value={taskSearch}
              onChange={(e) => {
                setTaskSearch(e.target.value);
                setTaskPage(1);
              }}
            />
            <Search className="w-4 h-4 text-white/60 absolute left-3.5 top-1/2 -translate-y-1/2" />
          </div>
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2 text-sm font-medium text-white/80">
              <Filter className="w-4 h-4" />
              <span>Lọc trạng thái:</span>
            </div>
            <select 
              className="border-none bg-white/10 text-white [&>option]:text-gray-800 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-white cursor-pointer"
              value={taskStatus}
              onChange={(e) => {
                setTaskStatus(e.target.value);
                setTaskPage(1);
              }}
            >
              <option value="">Tất cả trạng thái</option>
              <option value="Chưa làm">Chưa làm</option>
              <option value="Đang làm">Đang làm</option>
              <option value="Hoàn thành">Hoàn thành</option>
              <option value="Từ chối">Từ chối</option>
            </select>
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-gray-600">
            <thead className="bg-[#004c91] text-white font-semibold text-[13px] uppercase tracking-wider border-b border-[#004c91]">
              <tr>
                <th className="p-4 pl-6 font-bold whitespace-nowrap w-[25%]">Đoàn khách</th>
                <th className="p-4 font-bold whitespace-nowrap w-[25%]">Nhiệm vụ được giao</th>
                <th className="p-4 font-bold whitespace-nowrap w-[20%] text-center">Người phụ trách</th>
                <th className="p-4 font-bold text-center whitespace-nowrap w-[15%]">Trạng thái</th>
                <th className="p-4 font-bold text-center whitespace-nowrap w-[15%]">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {paginatedTasks.map((task) => (
                <tr
                  key={task.id}
                  className="hover:bg-gray-50/80 transition-colors group"
                >
                  <td className="p-4 pl-6 font-bold text-gray-800">
                    {task.delegation}
                  </td>
                  <td className="p-4 font-medium text-gray-600">
                    {task.task}
                  </td>
                  <td className="p-4 text-center">
                    <div className="inline-flex flex-col items-center">
                      <span className="font-bold text-[#004c91]">{mockMembers.find(m => m.id.toString() === task.assigneeId)?.name || "Chưa giao"}</span>
                      {isLeader && (
                        <button 
                          onClick={() => {
                            setSelectedTaskForAssigneeChange(task);
                            setNewAssigneeId("");
                            setIsChangeAssigneeModalOpen(true);
                          }}
                          className="text-[11px] text-blue-500 hover:text-blue-700 underline mt-1 font-medium transition-colors outline-none cursor-pointer"
                        >
                          Đổi người phụ trách
                        </button>
                      )}
                    </div>
                  </td>
                  <td className="p-4 text-center">
                    <span
                      className={`inline-flex items-center px-3 py-1.5 rounded-full text-xs font-bold ${
                        task.status === "Hoàn thành"
                          ? "bg-[#eaffe4] text-[#0aa14f] border border-[#ceefda]"
                          : task.status === "Từ chối"
                          ? "bg-red-50 text-red-600 border border-red-100"
                          : task.status === "Đang làm"
                          ? "bg-orange-100 text-orange-600 border border-orange-200"
                          : "bg-gray-100 text-gray-600 border border-gray-200"
                      }`}
                    >
                      {task.status}
                    </span>
                  </td>
                  <td className="p-4 text-center">
                    <div className="flex items-center justify-center gap-2">
                      <button 
                        onClick={() => {
                          const assigneeName = mockMembers.find(m => m.id.toString() === task.assigneeId)?.name || "Chưa giao";
                          if (task.delegation === "Đoàn đối tác Nhật Bản" || task.delegation === "Đoàn khách quan chức năng") {
                            navigate(`/dashboard/departments/${id}/invitations/${task.id}`, { state: { taskStatus: task.status, assigneeId: task.assigneeId, originalAssigneeId: task.originalAssigneeId, assigneeName } });
                          } else {
                            navigate(`/dashboard/departments/${id}/tasks/${task.id}`, { state: { taskStatus: task.status, assigneeId: task.assigneeId, originalAssigneeId: task.originalAssigneeId, assigneeName } });
                          }
                        }}
                        className="p-2 text-gray-400 hover:text-[#004c91] hover:bg-[#e6eff7] rounded-lg transition-colors flex items-center justify-center outline-none cursor-pointer" 
                        title="Xem chi tiết"
                      >
                        <Eye className="w-5 h-5" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {filteredTasks.length === 0 && (
                <tr>
                  <td
                    colSpan={5}
                    className="py-12 text-center text-gray-500 bg-white font-medium"
                  >
                    Không tìm thấy nhiệm vụ phù hợp
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        
        {/* Nhiệm vụ Pagination */}
        {filteredTasks.length > 0 && (
          <div className="flex items-center justify-between border-t border-gray-200 bg-white px-6 py-4">
            <div className="flex items-center gap-3 text-sm text-gray-600 font-medium">
              <span>Hiển thị</span>
              <select
                className="border border-gray-300 bg-white rounded-lg px-2 py-1 outline-none focus:border-[#004c91] hover:border-gray-400 transition-colors cursor-pointer text-gray-700"
                value={tasksPerPage}
                onChange={(e) => {
                  setTasksPerPage(Number(e.target.value));
                  setTaskPage(1);
                }}
              >
                <option value={5}>5</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
              </select>
              <span>kết quả</span>
            </div>
            <div className="flex items-center gap-1.5">
              <button
                className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm"
                onClick={() => setTaskPage(taskPage - 1)}
                disabled={taskPage === 1}
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <div className="flex items-center gap-1">
                {Array.from({ length: totalTaskPages || 1 }, (_, i) => i + 1).map((p) => (
                    <button
                      key={p}
                      className={`w-9 h-9 rounded-xl font-bold text-sm transition-colors cursor-pointer ${
                        taskPage === p
                          ? "bg-[#004c91] text-white shadow-sm border border-[#004c91]"
                          : "text-gray-600 hover:bg-gray-100 border border-transparent"
                      }`}
                      onClick={() => setTaskPage(p)}
                    >
                      {p}
                    </button>
                  )
                )}
              </div>
              <button
                className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm cursor-pointer"
                onClick={() => setTaskPage(taskPage + 1)}
                disabled={taskPage === totalTaskPages || totalTaskPages === 0}
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Modal Phân công */}
      {isChangeAssigneeModalOpen && selectedTaskForAssigneeChange && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden animate-in zoom-in-95 duration-200">
            <div className="bg-[#004c91] px-6 py-4 flex items-center justify-between">
              <h3 className="text-lg font-bold text-white">Đổi người phụ trách</h3>
              <button 
                onClick={() => setIsChangeAssigneeModalOpen(false)}
                className="text-white/70 hover:text-white transition-colors cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6 space-y-5">
              {/* Info Box */}
              <div className="bg-blue-50/50 rounded-xl p-4 border border-blue-100/50 space-y-3">
                <div>
                  <span className="text-xs font-bold text-[#004c91] uppercase tracking-wider block mb-1">Nhiệm vụ</span>
                  <p className="text-sm font-medium text-gray-800">{selectedTaskForAssigneeChange.task}</p>
                </div>
                <div className="pt-3 border-t border-blue-100/50">
                  <span className="text-xs font-bold text-[#004c91] uppercase tracking-wider block mb-1">Đoàn khách</span>
                  <p className="text-sm font-medium text-gray-800">{selectedTaskForAssigneeChange.delegation}</p>
                </div>
              </div>

              {selectedTaskForAssigneeChange.status === "Từ chối" && selectedTaskForAssigneeChange.rejectReason && (
                <div className="bg-red-50 rounded-xl p-4 border border-red-100">
                  <span className="text-xs font-bold text-red-700 uppercase tracking-wider block mb-1">Lý do từ chối</span>
                  <p className="text-sm text-red-600 font-medium">{selectedTaskForAssigneeChange.rejectReason}</p>
                </div>
              )}

              <div>
                <label className="block text-sm font-bold text-gray-700 mb-2">
                  Chọn người phụ trách mới
                </label>
                <select 
                  className="w-full h-11 border border-gray-300 rounded-xl px-4 text-sm focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none"
                  value={newAssigneeId}
                  onChange={(e) => setNewAssigneeId(e.target.value)}
                >
                  <option value="">-- Chọn nhân sự --</option>
                  {mockMembers.map(m => (
                    <option key={m.id} value={m.id} disabled={m.id === selectedTaskForAssigneeChange.assigneeId}>
                      {m.name} - {m.role} {m.id === selectedTaskForAssigneeChange.assigneeId ? "(Đang phụ trách)" : ""}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="border-t border-gray-100 px-6 py-4 bg-gray-50 flex justify-end gap-3">
              <button 
                onClick={() => setIsChangeAssigneeModalOpen(false)}
                className="px-5 py-2.5 text-sm font-bold text-gray-600 hover:bg-gray-200 rounded-xl transition-colors cursor-pointer"
              >
                Hủy bỏ
              </button>
              <button 
                onClick={handleChangeAssignee}
                disabled={!newAssigneeId || isAssigning}
                className="px-5 py-2.5 text-sm font-bold text-white bg-[#f37021] hover:bg-[#d9621a] rounded-xl transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 cursor-pointer"
              >
                {isAssigning ? (
                  <>
                    <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
                    Đang xử lý...
                  </>
                ) : "Xác nhận đổi"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Success Modal */}
      {showSuccessModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm overflow-hidden animate-in zoom-in-95 duration-200 p-6 text-center">
            <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
              <CheckCircle2 className="w-8 h-8 text-green-500" />
            </div>
            <h3 className="text-xl font-bold text-gray-900 mb-2">Thành công!</h3>
            <p className="text-gray-600 mb-6">{successMessage}</p>
            <button
              onClick={() => setShowSuccessModal(false)}
              className="w-full py-2.5 bg-[#004c91] text-white rounded-xl font-bold hover:bg-[#003b70] transition-colors cursor-pointer"
            >
              Đóng
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

// Thêm CheckCircle2 vào import ở đầu file.
// Oops, cần import thêm CheckCircle2 từ lucide-react. Tôi sẽ sửa lại sau.
