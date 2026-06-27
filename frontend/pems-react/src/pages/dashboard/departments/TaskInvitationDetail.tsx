/**
 * Trang TaskInvitationDetail
 * Thao tác phân luồng tham gia tương tác liên bộ về một chuyên đề được ủy quyền tiếp đón.
 */

import React, { useState, useEffect } from 'react';
import { useNavigate, useParams, useLocation } from 'react-router-dom';
import {
  ChevronLeft,
  ChevronRight,
  Info,
  FileSignature,
  Clock,
  User,
  CheckCircle2,
  AlertCircle,
  Users,
  Calendar,
  ShieldCheck,
  PenLine,
  FileText
} from 'lucide-react';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import toast from 'react-hot-toast';

const inMemoryTaskStore: Record<string, any> = {};

export function TaskInvitationDetail() {
  const navigate = useNavigate();
  const { id, taskId } = useParams();

  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const isLeader = user?.role === 'ADMIN' || user?.role === 'HO' || user?.role?.toUpperCase() === 'STAFF' || (user?.role?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'LEADER');
  const isStaff = user?.role?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'STAFF';

  const location = useLocation();
  const taskStatusFromState = location.state?.taskStatus || "Đang làm";
  const assigneeNameFromState = location.state?.assigneeName || "Chưa giao";
  const assigneeIdFromState = location.state?.assigneeId;
  const originalAssigneeIdFromState = location.state?.originalAssigneeId;

  const [detailData, setDetailData] = useState<any>(null);
  const [isLoading, setIsLoading] = useState(true);

  const fetchDetail = async () => {
    try {
      setIsLoading(true);
      const data = await departmentReceptionTasksApi.getInvitationDetail(taskId!);
      setDetailData(data);
      if (data) {
        if (data.status === 'INVITED') setTaskActionStatus('pending');
        else if (data.status === 'ACCEPTED') setTaskActionStatus('confirmed');
        else if (data.status === 'DECLINED') {
          setTaskActionStatus('rejected');
          setRejectReason(data.rejectReason || '');
        }
        if (data.actionTime) setActionTime(data.actionTime);
      }
    } catch (e) {
      console.error(e);
      toast.error('Lỗi khi tải chi tiết thư mời');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (taskId) fetchDetail();
  }, [taskId]);

  const [taskActionStatus, setTaskActionStatus] = useState<'pending' | 'confirmed' | 'rejected'>('pending');

  const [rejectReason, setRejectReason] = useState("");
  const [tempRejectReason, setTempRejectReason] = useState("");
  const [actionTime, setActionTime] = useState<string | null>(null);
  const [isRejectModalOpen, setIsRejectModalOpen] = useState(false);



  const [requesterSigned, setRequesterSigned] = useState(false);
  const [supporterSigned, setSupporterSigned] = useState(false);

  const getCurrentTime = () => {
    const now = new Date();
    return `${now.getHours().toString().padStart(2, '0')}:${now.getMinutes().toString().padStart(2, '0')}:${now.getSeconds().toString().padStart(2, '0')} ${now.getDate().toString().padStart(2, '0')}-${(now.getMonth() + 1).toString().padStart(2, '0')}-${now.getFullYear()}`;
  };

  const [requesterSignTime, setRequesterSignTime] = useState<string | null>(null);
  const [supporterSignTime, setSupporterSignTime] = useState<string | null>(null);

  const handleRequesterSign = () => {
    if (!requesterSigned) {
      setRequesterSigned(true);
      setRequesterSignTime(getCurrentTime());
    }
  };

  const handleSupporterSign = () => {
    if (!supporterSigned) {
      setSupporterSigned(true);
      setSupporterSignTime(getCurrentTime());
    }
  };

  const isTaskComplete = requesterSigned && supporterSigned;

  const handleAccept = async () => {
    try {
      await departmentReceptionTasksApi.acceptInvitation(taskId!);
      toast.success('Xác nhận tham gia thành công');
      fetchDetail();
    } catch (e: any) {
      toast.error(e.response?.data?.message || 'Có lỗi xảy ra');
    }
  };

  const handleReject = async () => {
    if (!tempRejectReason.trim()) return;
    try {
      await departmentReceptionTasksApi.declineInvitation(taskId!, tempRejectReason);
      toast.success('Từ chối thành công');
      setIsRejectModalOpen(false);
      fetchDetail();
    } catch (e: any) {
      toast.error(e.response?.data?.message || 'Có lỗi xảy ra');
    }
  };

  if (isLoading) return <div className="p-8 text-center">Đang tải...</div>;
  if (!detailData) return <div className="p-8 text-center">Không tìm thấy thư mời</div>;

  const isButtonsDisabled = taskActionStatus !== 'pending';

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-24 relative animate-in fade-in duration-500">
      {/* Breadcrumb */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button
          onClick={() => navigate('/dashboard')}
          className="hover:text-[#004c91] transition-colors outline-none"
        >
          Dashboard
        </button>
        {(user?.role?.toUpperCase() !== 'DEPARTMENT' && user?.role?.toUpperCase() !== 'STAFF') && (
          <>
            <span className="mx-2">/</span>
            <button
              onClick={() => navigate('/dashboard/departments')}
              className="hover:text-[#004c91] transition-colors outline-none"
            >
              Quản lý phòng ban
            </button>
          </>
        )}
        <span className="mx-2">/</span>
        <button
          onClick={() => navigate(`/dashboard/departments/${id}`)}
          className="hover:text-[#004c91] transition-colors outline-none"
        >
          {user?.role?.toUpperCase() === 'DEPARTMENT' || user?.role?.toUpperCase() === 'STAFF' ? 'Nhiệm vụ của tôi' : 'Chi tiết phòng ban'}
        </button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Thư mời tham gia</span>
      </div>

      <div className="mb-8 flex items-center justify-between bg-white p-6 rounded-3xl shadow-[0_4px_20px_-4px_rgba(0,0,0,0.05)] border border-gray-100 relative overflow-hidden">
        {/* Subtle background decoration */}
        <div className="absolute top-0 right-0 w-64 h-64 bg-gradient-to-br from-blue-50 to-transparent rounded-full -translate-y-1/2 translate-x-1/3 opacity-70 blur-3xl"></div>
        <div className="absolute bottom-0 left-0 w-48 h-48 bg-gradient-to-tr from-orange-50 to-transparent rounded-full translate-y-1/3 -translate-x-1/4 opacity-70 blur-2xl"></div>

        <div className="flex items-center gap-5 relative z-10">
          <button
            onClick={() => navigate(-1)}
            className="flex items-center justify-center p-3 rounded-2xl border border-gray-200 bg-white shadow-sm hover:border-[#004c91] hover:text-[#004c91] hover:bg-blue-50 transition-all outline-none group"
          >
            <ChevronLeft className="w-6 h-6 group-hover:-translate-x-0.5 transition-transform" />
          </button>
          <div>
            <h1 className="text-3xl lg:text-4xl font-black text-[#004c91] tracking-tight uppercase flex items-center gap-4">
              THƯ MỜI THAM GIA ĐOÀN KHÁCH
              <span className={`inline-flex items-center px-4 py-1.5 rounded-full text-sm font-bold shadow-sm ${taskActionStatus === 'pending'
                  ? 'bg-gradient-to-r from-orange-50 to-orange-100 text-orange-600 border border-orange-200'
                  : 'bg-gradient-to-r from-gray-50 to-gray-100 text-gray-600 border border-gray-200'
                }`}>
                <div className="w-2 h-2 rounded-full bg-orange-500 mr-2 animate-pulse"></div>
                {taskActionStatus === 'pending' ? 'Chưa xác nhận' : taskActionStatus === 'confirmed' ? 'Đã xác nhận' : 'Đã từ chối'}
              </span>
            </h1>
          </div>
        </div>
      </div>

      {/* Main Content */}
      <div className="grid grid-cols-1 lg:max-w-3xl mx-auto gap-8">

        {/* LEFT COLUMN: TASK INFORMATION */}
        <div className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-gray-100 p-0 flex flex-col relative overflow-hidden group/card hover:shadow-[0_12px_40px_-4px_rgba(0,76,145,0.08)] transition-shadow duration-500">

          <div className="flex items-center gap-4 bg-[#004c91] p-8 pb-6 relative z-10">
            <div className="p-3 bg-white/10 text-white rounded-2xl shadow-inner border border-white/20">
              <Info className="w-7 h-7" />
            </div>
            <div>
              <h2 className="text-2xl font-black text-white tracking-tight">Chi tiết thư mời</h2>
              <p className="text-sm font-medium text-blue-100 mt-1">Thông tin sự kiện</p>
            </div>
          </div>

          <div className="flex-1 space-y-6 relative z-10 p-8 pt-6">
            {/* Bento Grid for Metadata */}
            <div className="grid grid-cols-2 gap-4">
              <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 hover:border-[#004c91]/30 hover:shadow-md hover:bg-white transition-all cursor-default">
                <div className="flex items-center gap-2 text-gray-400 mb-2">
                  <User className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Người gửi</span>
                </div>
                <div className="text-sm font-black text-[#004c91]">{detailData.senderName}</div>
              </div>

              <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 hover:border-[#004c91]/30 hover:shadow-md hover:bg-white transition-all cursor-default">
                <div className="flex items-center gap-2 text-gray-400 mb-2">
                  <Clock className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian gửi</span>
                </div>
                <div className="text-sm font-black text-[#004c91]">{detailData.invitedAt}</div>
              </div>

              <div className="col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 hover:border-[#f37021]/30 hover:shadow-md hover:bg-white transition-all cursor-default">
                <div className="flex items-center gap-2 text-gray-400 mb-2">
                  <Users className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Đoàn khách</span>
                </div>
                <div className="text-base font-black text-[#004c91] border-l-4 border-[#f37021] pl-3 py-1 bg-transparent">
                  {detailData.delegationName}
                </div>
              </div>

              <div className="col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 hover:border-[#004c91]/30 hover:shadow-md hover:bg-white transition-all cursor-default relative overflow-hidden">
                <div className="absolute right-0 top-0 w-32 h-32 bg-blue-50 rounded-full translate-x-1/2 -translate-y-1/2 blur-2xl"></div>
                <div className="flex items-center gap-2 text-gray-400 mb-2 relative z-10">
                  <Calendar className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian diễn ra</span>
                </div>
                <div className="text-[15px] font-bold text-gray-800 relative z-10 flex items-center gap-3">
                  <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{detailData.startTime}</span>
                  <ChevronRight className="w-4 h-4 text-gray-400" />
                  <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{detailData.endTime}</span>
                  <span className="text-[#004c91] font-bold ml-1">{detailData.date}</span>
                </div>
              </div>
            </div>

            <div className="flex flex-col gap-3 pt-4 hover:opacity-100 transition-all cursor-default">
              <div className="flex items-center gap-2 text-gray-400">
                <FileText className="w-4 h-4" />
                <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung</span>
              </div>
              <div className="p-6 bg-gradient-to-br from-[#f8fafc] to-[#f1f5f9] rounded-2xl text-[15px] font-medium text-gray-700 leading-relaxed border border-gray-200 hover:border-[#004c91]/30 hover:shadow-md hover:bg-white transition-all shadow-inner relative overflow-hidden">
                <div className="absolute left-0 top-0 bottom-0 w-1 bg-[#004c91]"></div>
                <div className="whitespace-pre-line">
                  {detailData.note}
                </div>
              </div>
            </div>
          </div>

          {taskActionStatus === 'pending' && (
            <div className="flex gap-4 p-8 pt-6 border-t border-gray-100 relative z-10 bg-gray-50/50">
              <button
                onClick={() => setIsRejectModalOpen(true)}
                disabled={isButtonsDisabled}
                className={`flex-1 py-4 rounded-2xl border-2 font-black uppercase tracking-wider transition-all duration-300 outline-none ${isButtonsDisabled
                    ? 'border-gray-200 text-gray-400 cursor-not-allowed opacity-50 bg-gray-50'
                    : 'border-[#f37021] text-[#f37021] hover:bg-[#f37021] hover:text-white hover:shadow-lg hover:shadow-orange-500/20 active:scale-[0.98]'
                  }`}>
                Từ chối
              </button>
              <button
                onClick={handleAccept}
                disabled={isButtonsDisabled}
                className={`flex-1 py-4 rounded-2xl font-black uppercase tracking-wider transition-all duration-300 outline-none border ${isButtonsDisabled
                    ? 'bg-gray-100 text-gray-400 border-gray-200 cursor-not-allowed opacity-50'
                    : 'bg-[#004c91] text-white hover:bg-[#003b73] shadow-lg shadow-[#004c91]/20 hover:shadow-[0_8px_25px_rgba(0,76,145,0.3)] active:scale-[0.98] border-blue-600'
                  }`}>
                Xác nhận tham gia
              </button>
            </div>
          )}

          {taskActionStatus === 'confirmed' && (
            <div className="flex gap-4 p-8 pt-6 border-t border-gray-100 relative z-10 bg-green-50/50">
              <button disabled className="w-full py-4 rounded-2xl bg-[#16a34a] text-white font-black uppercase tracking-wider shadow-lg shadow-green-500/20 opacity-80 cursor-not-allowed flex flex-col items-center justify-center gap-1">
                <div className="flex items-center gap-2">
                  <CheckCircle2 className="w-5 h-5" />
                  <span>Đã xác nhận tham gia</span>
                </div>
                <div className="text-xs font-medium text-green-100 lowercase tracking-normal bg-green-800/20 px-3 py-1 rounded-full mt-1">
                  Lúc: {detailData.actionTime}
                </div>
              </button>
            </div>
          )}

          {taskActionStatus === 'rejected' && (
            <div className="flex gap-4 p-8 pt-6 border-t border-gray-100 relative z-10 bg-red-50/50">
              <div className="w-1/3 flex flex-col justify-center items-center py-4 rounded-2xl bg-red-100 text-red-600 font-black uppercase tracking-wider border border-red-200">
                <div className="flex items-center gap-2">
                  <AlertCircle className="w-5 h-5" />
                  <span>Đã từ chối</span>
                </div>
                <div className="text-[10px] font-bold text-red-500 mt-0.5 tracking-normal">
                  {detailData.actionTime}
                </div>
              </div>
              <div className="w-2/3 bg-white border border-red-100 rounded-2xl p-4 shadow-sm flex flex-col justify-center">
                <span className="text-xs font-bold text-gray-400 uppercase mb-1">Lý do từ chối:</span>
                <span className="text-sm font-medium text-gray-700 italic">"{detailData.rejectReason}"</span>
              </div>
            </div>
          )}

        </div>



      </div>

      {/* Modal Từ chối */}
      {isRejectModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
          <div className="bg-white rounded-3xl w-full max-w-lg shadow-2xl overflow-hidden animate-in zoom-in-95 duration-300">
            <div className="p-6 border-b border-gray-100">
              <h3 className="text-xl font-black text-gray-900 uppercase tracking-tight">Xác nhận từ chối</h3>
            </div>
            <div className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-bold text-gray-700 mb-2 uppercase tracking-wider">Lý do từ chối <span className="text-[#f37021]">*</span></label>
                <textarea
                  value={tempRejectReason}
                  onChange={(e) => setTempRejectReason(e.target.value)}
                  className="w-full min-h-[140px] p-4 rounded-xl border-2 border-gray-200 outline-none focus:border-[#f37021] focus:ring-4 focus:ring-orange-500/10 transition-all font-medium text-[15px] text-gray-800 placeholder:text-gray-400 resize-none bg-gray-50/50"
                  placeholder="Vui lòng nhập lý do từ chối tham gia để hệ thống ghi nhận..."
                />
              </div>
            </div>
            <div className="p-6 bg-gray-50 border-t border-gray-100 flex justify-end gap-3">
              <button
                onClick={() => { setIsRejectModalOpen(false); setTempRejectReason(""); }}
                className="px-6 py-3 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none"
              >
                Hủy bỏ
              </button>
              <button
                onClick={handleReject}
                disabled={!tempRejectReason.trim()}
                className="px-6 py-3 rounded-xl font-black text-white bg-[#f37021] hover:bg-orange-600 transition-colors shadow-lg shadow-orange-500/20 disabled:opacity-50 disabled:cursor-not-allowed outline-none uppercase tracking-wider"
              >
                Xác nhận từ chối
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}
