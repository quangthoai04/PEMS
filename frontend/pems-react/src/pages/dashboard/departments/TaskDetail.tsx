/**
 * Trang TaskDetail
 * Cung cấp thông tin tiến độ trạng thái phản hồi và theo dõi thời gian thực của nhiệm vụ.
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

const inMemoryTaskStore: Record<string, any> = {};

export function TaskDetail() {
  const navigate = useNavigate();
  const { id, taskId } = useParams();

  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const isLeader = user?.role === 'ADMIN' || user?.role === 'HO' || user?.role === 'Staff' || (user?.role === 'Dept' && user?.subRole === 'Leader');
  const isStaff = user?.role === 'Dept' && user?.subRole === 'Staff';

  const location = useLocation();
  const taskStatusFromState = location.state?.taskStatus || "Đang làm";
  const assigneeIdFromState = location.state?.assigneeId;

  const isDeptLeader = user?.role === 'Dept' && user?.subRole === 'Leader';

  const shouldDisableButtons = () => {
    if (isDeptLeader) {
      if (assigneeIdFromState && assigneeIdFromState !== "dept_leader") {
        return true;
      }
      return false;
    }
    return false;
  };
  const isButtonsDisabled = shouldDisableButtons();

  const [taskActionStatus, setTaskActionStatus] = useState<'pending' | 'confirmed' | 'rejected' | 'waiting_for_approval'>(() => {
    if (taskStatusFromState === "Chưa làm") return "pending";
    if (taskStatusFromState === "Từ chối") return "rejected";
    return inMemoryTaskStore[`taskDetail_${taskId}_actionStatus`] || 'pending';
  });
  const [rejectReason, setRejectReason] = useState("");
  const [tempRejectReason, setTempRejectReason] = useState("");
  const [actionTime, setActionTime] = useState<string | null>(() => inMemoryTaskStore[`taskDetail_${taskId}_actionTime`] || null);
  const [isRejectModalOpen, setIsRejectModalOpen] = useState(false);

  const [isProposing, setIsProposing] = useState(false);
  const [proposedTime, setProposedTime] = useState(() => inMemoryTaskStore[`taskDetail_${taskId}_proposedTime`] || "");
  const [proposedContent, setProposedContent] = useState(() => inMemoryTaskStore[`taskDetail_${taskId}_proposedContent`] || "");
  const [proposedBy, setProposedBy] = useState(() => inMemoryTaskStore[`taskDetail_${taskId}_proposedBy`] || "DEPT");
  const [isSubmitProposalModalOpen, setIsSubmitProposalModalOpen] = useState(false);

  useEffect(() => {
    inMemoryTaskStore[`taskDetail_${taskId}_actionStatus`] = taskActionStatus;
  }, [taskActionStatus, taskId]);

  useEffect(() => {
    inMemoryTaskStore[`taskDetail_${taskId}_proposedBy`] = proposedBy;
  }, [proposedBy, taskId]);

  useEffect(() => {
    inMemoryTaskStore[`taskDetail_${taskId}_proposedTime`] = proposedTime;
  }, [proposedTime, taskId]);

  useEffect(() => {
    inMemoryTaskStore[`taskDetail_${taskId}_proposedContent`] = proposedContent;
  }, [proposedContent, taskId]);

  useEffect(() => {
    if (actionTime) {
      inMemoryTaskStore[`taskDetail_${taskId}_actionTime`] = actionTime;
    }
  }, [actionTime, taskId]);

  const [bg1Signed, setBg1Signed] = useState<string|null>(null);
  const [bg2Signed, setBg2Signed] = useState<string|null>(null);
  const [bgNote, setBgNote] = useState('');

  const [nt1Signed, setNt1Signed] = useState<string|null>(null);
  const [nt2Signed, setNt2Signed] = useState<string|null>(null);
  const [ntNote, setNtNote] = useState('');

  const getCurrentTime = () => {
    const now = new Date();
    return `${String(now.getDate()).padStart(2, '0')}/${String(now.getMonth() + 1).padStart(2, '0')}/${now.getFullYear()}, ${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
  };

  const isTaskComplete = bg1Signed && bg2Signed && nt1Signed && nt2Signed;

  // Mock data
  const taskStatus = taskStatusFromState;
  const coordinatorName = "Nguyễn Văn Trưởng Phòng";
  const supporterName = isStaff ? user?.name : "Trần B Hỗ Trợ";

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
        {(user?.role?.toUpperCase() !== 'DEPT' && user?.role?.toUpperCase() !== 'STAFF') && (
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
          Chi tiết phòng ban
        </button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Chi tiết nhiệm vụ</span>
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
              CHI TIẾT NHIỆM VỤ ĐIỀU PHỐI
              <span className={`inline-flex items-center px-4 py-1.5 rounded-full text-sm font-bold shadow-sm ${
                taskStatus === 'Đang làm' 
                  ? 'bg-gradient-to-r from-orange-50 to-orange-100 text-orange-600 border border-orange-200' 
                  : 'bg-gradient-to-r from-gray-50 to-gray-100 text-gray-600 border border-gray-200'
              }`}>
                <div className="w-2 h-2 rounded-full bg-orange-500 mr-2 animate-pulse"></div>
                {taskStatus}
              </span>
            </h1>
          </div>
        </div>
      </div>

      {/* Main Content */}
      <div className="flex flex-col w-full max-w-4xl mx-auto gap-8">
        
        {/* TOP COMPONENT: TASK INFORMATION */}
        <div className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-gray-100 p-0 flex flex-col relative overflow-hidden group/card hover:shadow-[0_12px_40px_-4px_rgba(0,76,145,0.08)] transition-shadow duration-500">
          
          {taskActionStatus === 'waiting_for_approval' && user?.role?.toUpperCase() !== proposedBy && (
            <div className="bg-[#fff1e0] px-8 py-4 shrink-0 flex items-center justify-center gap-2 text-[#e85c0d] font-black uppercase tracking-widest text-sm relative z-10 border-b-2 border-[#ffd3a8]">
                <AlertCircle className="w-5 h-5" /> CÓ ĐỀ XUẤT THAY ĐỔI
            </div>
          )}

          <div className="flex items-center gap-4 bg-[#004c91] p-8 pb-6 relative z-10">
            <div className="p-3 bg-white/10 text-white rounded-2xl shadow-inner border border-white/20">
              <Info className="w-7 h-7" />
            </div>
            <div>
              <h2 className="text-2xl font-black text-white tracking-tight">Thông tin chi tiết</h2>
              <p className="text-sm font-medium text-blue-100 mt-1">Nhiệm vụ được giao</p>
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
                <div className="text-sm font-black text-[#004c91]">Nguyễn Văn A</div>
              </div>
              
              <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 hover:border-[#004c91]/30 hover:shadow-md hover:bg-white transition-all cursor-default">
                <div className="flex items-center gap-2 text-gray-400 mb-2">
                  <Clock className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian gửi</span>
                </div>
                <div className="text-sm font-black text-[#004c91]">08:30 15-10-2023</div>
              </div>

              <div className="col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 hover:border-[#f37021]/30 hover:shadow-md hover:bg-white transition-all cursor-default">
                <div className="flex items-center gap-2 text-gray-400 mb-2">
                  <Users className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Đoàn khách</span>
                </div>
                <div className="text-base font-black text-[#004c91] border-l-4 border-[#f37021] pl-3 py-1 bg-transparent">
                  ĐOÀN TRƯỜNG ĐẠI HỌC CÔNG NGHỆ SYDNEY (UTS)
                </div>
              </div>

              <div className="col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 hover:border-[#004c91]/30 hover:shadow-md hover:bg-white transition-all cursor-default relative overflow-hidden">
                <div className="absolute right-0 top-0 w-32 h-32 bg-blue-50 rounded-full translate-x-1/2 -translate-y-1/2 blur-2xl"></div>
                <div className="flex items-center gap-2 text-gray-400 mb-2 relative z-10">
                  <Calendar className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian sử dụng</span>
                </div>
                <div className="text-[15px] font-bold text-gray-800 relative z-10 flex items-center gap-3">
                   <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">14:00</span>
                   <ChevronRight className="w-4 h-4 text-gray-400" />
                   <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">16:30</span>
                   <span className="text-[#004c91] font-bold ml-1">16-10-2023</span>
                </div>
              </div>

              {(isProposing || taskActionStatus === 'waiting_for_approval') && (
                <div className="col-span-2 p-4 bg-[#fff1e0] rounded-2xl border border-[#ffd3a8] transition-all relative overflow-hidden animate-in fade-in zoom-in-95 duration-200">
                  <div className="flex items-center gap-2 text-[#e85c0d] mb-2 relative z-10">
                    <Calendar className="w-4 h-4" />
                    <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian sử dụng (Đề xuất)</span>
                  </div>
                  <input
                    type="text"
                    value={proposedTime}
                    onChange={(e) => setProposedTime(e.target.value)}
                    disabled={taskActionStatus === 'waiting_for_approval'}
                    placeholder="Nhập đề xuất thời gian..."
                    className="w-full px-4 py-2.5 rounded-xl border border-[#ffc288] bg-white text-sm focus:outline-none focus:border-[#e85c0d] focus:ring-1 focus:ring-[#e85c0d] text-gray-800 transition-shadow outline-none shadow-sm disabled:bg-orange-50/50 disabled:text-gray-500 disabled:cursor-not-allowed"
                  />
                </div>
              )}
            </div>

            <div className="flex flex-col gap-3 pt-4 hover:opacity-100 transition-all cursor-default">
              <div className="flex items-center gap-2 text-gray-400">
                  <FileText className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết công việc</span>
              </div>
              <div className="p-6 bg-gradient-to-br from-[#f8fafc] to-[#f1f5f9] rounded-2xl text-[15px] font-medium text-gray-700 leading-relaxed border border-gray-200 hover:border-[#004c91]/30 hover:shadow-md hover:bg-white transition-all shadow-inner relative overflow-hidden">
                <div className="absolute left-0 top-0 bottom-0 w-1 bg-[#004c91]"></div>
                Yêu cầu mượn 2 xe điện loại 8 chỗ phục vụ đoàn di chuyển quanh khuôn viên trường đại học FPT. 
                Đón khách tại sảnh tòa nhà Alpha, sau đó di chuyển qua Beta và Gamma. 
                <br/><br/>
                <span className="font-bold text-gray-900">* Yêu cầu tài xế có mặt trước 15 phút tại điểm xuất phát, trang phục lịch sự theo chuẩn FPT.</span>
              </div>
            </div>

            {(isProposing || taskActionStatus === 'waiting_for_approval') && (
              <div className="flex flex-col gap-3 pt-4 hover:opacity-100 transition-all cursor-default animate-in fade-in zoom-in-95 duration-200">
                <div className="flex items-center gap-2 text-[#e85c0d]">
                    <FileText className="w-4 h-4" />
                    <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết công việc (Đề xuất)</span>
                </div>
                <textarea
                  value={proposedContent}
                  onChange={(e) => setProposedContent(e.target.value)}
                  disabled={taskActionStatus === 'waiting_for_approval'}
                  placeholder="Nhập đề xuất nội dung..."
                  rows={4}
                  className="p-6 bg-[#fff1e0] rounded-2xl text-[15px] font-medium text-gray-700 leading-relaxed border border-[#ffc288] focus:outline-none focus:border-[#e85c0d] focus:ring-1 focus:ring-[#e85c0d] transition-all shadow-inner relative overflow-hidden w-full resize-none disabled:bg-orange-50/50 disabled:text-gray-500 disabled:cursor-not-allowed"
                />
              </div>
            )}

            {!isProposing && taskActionStatus === 'pending' && ['DEPT', 'STAFF'].includes(user?.role?.toUpperCase()) && (
              <div className="flex justify-end pt-2">
                <button 
                  onClick={() => setIsProposing(true)}
                  disabled={isButtonsDisabled}
                  className={`px-6 py-2.5 rounded-xl border bg-orange-50 font-bold text-xs flex items-center gap-2 transition-colors ${isButtonsDisabled ? 'opacity-50 cursor-not-allowed text-gray-500 border-gray-200' : 'text-[#f37021] hover:bg-orange-100 border-orange-200'}`}
                >
                  <PenLine className="w-4 h-4" /> Đề xuất thay đổi
                </button>
              </div>
            )}
          </div>

          {taskActionStatus === 'pending' && !isProposing && (
            <div className="flex gap-4 p-8 pt-6 border-t border-gray-100 relative z-10 bg-gray-50/50">
              <button 
                onClick={() => setIsRejectModalOpen(true)}
                disabled={isButtonsDisabled}
                className={`flex-1 py-4 rounded-2xl border-2 font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${isButtonsDisabled ? 'border-gray-200 text-gray-400 bg-gray-50 cursor-not-allowed' : 'border-[#f37021] text-[#f37021] hover:bg-[#f37021] hover:text-white hover:shadow-lg active:scale-[0.98] cursor-pointer'}`}>
                Từ chối
              </button>
              <button 
                onClick={() => { setTaskActionStatus('confirmed'); setActionTime(getCurrentTime()); }}
                disabled={isButtonsDisabled}
                className={`flex-1 py-4 rounded-2xl font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${isButtonsDisabled ? 'bg-gray-100 text-gray-400 border border-gray-200 cursor-not-allowed' : 'bg-[#004c91] text-white hover:bg-[#003b73] shadow-lg shadow-[#004c91]/20 active:scale-[0.98] border border-blue-600 cursor-pointer'}`}>
                Xác nhận nhiệm vụ
              </button>
            </div>
          )}

          {taskActionStatus === 'pending' && isProposing && (
            <div className="flex gap-4 p-8 pt-6 border-t border-gray-100 relative z-10 bg-gray-50/50">
              <button 
                onClick={() => setIsProposing(false)}
                className="flex-1 py-4 rounded-2xl border-2 border-gray-300 text-gray-500 hover:bg-gray-100 hover:text-gray-700 font-black uppercase tracking-wider transition-all duration-300 outline-none active:scale-[0.98]">
                Hủy
              </button>
              <button 
                onClick={() => setIsSubmitProposalModalOpen(true)}
                className="flex-1 py-4 rounded-2xl bg-[#16a34a] text-white font-black uppercase tracking-wider hover:bg-[#15803d] transition-all duration-300 shadow-lg shadow-green-500/20 outline-none hover:shadow-[0_8px_25px_rgba(22,163,74,0.3)] active:scale-[0.98] border border-green-600">
                Gửi đề xuất
              </button>
            </div>
          )}

          {taskActionStatus === 'confirmed' && (
            <div className="flex gap-4 p-8 pt-6 border-t border-gray-100 relative z-10 bg-green-50/50">
               <button disabled className="w-full py-4 rounded-2xl bg-[#16a34a] text-white font-black uppercase tracking-wider shadow-lg shadow-green-500/20 opacity-80 cursor-not-allowed flex flex-col items-center justify-center gap-1">
                 <div className="flex items-center gap-2">
                   <CheckCircle2 className="w-5 h-5" /> 
                   <span>Đã xác nhận nhiệm vụ</span>
                 </div>
                 <div className="text-xs font-medium text-green-100 lowercase tracking-normal bg-green-800/20 px-3 py-1 rounded-full mt-1">
                    Bởi: {supporterName} - {actionTime}
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
                 <div className="text-xs font-bold text-red-700 mt-1 capitalize tracking-normal">
                    {supporterName}
                 </div>
                 <div className="text-[10px] font-bold text-red-500 mt-0.5 tracking-normal">
                    {actionTime}
                 </div>
               </div>
               <div className="w-2/3 bg-white border border-red-100 rounded-2xl p-4 shadow-sm flex flex-col justify-center">
                 <span className="text-xs font-bold text-gray-400 uppercase mb-1">Lý do từ chối:</span>
                 <span className="text-sm font-medium text-gray-700 italic">"{rejectReason}"</span>
               </div>
            </div>
          )}

          {taskActionStatus === 'waiting_for_approval' && user?.role?.toUpperCase() !== proposedBy && (
            <div className="flex gap-4 p-8 pt-6 border-t border-gray-100 relative z-10 bg-gray-50/50">
              <button 
                onClick={() => setIsRejectModalOpen(true)}
                className="flex-1 py-4 rounded-2xl border-2 border-[#f37021] text-[#f37021] hover:bg-[#f37021] hover:text-white font-black uppercase tracking-wider transition-all duration-300 outline-none hover:shadow-lg hover:shadow-orange-500/20 active:scale-[0.98]">
                Từ chối
              </button>
              <button 
                onClick={() => {
                   setTaskActionStatus('confirmed');
                   setActionTime(getCurrentTime());
                }}
                className="flex-1 py-4 rounded-2xl bg-[#004c91] text-white font-black uppercase tracking-wider hover:bg-[#00386b] transition-all duration-300 shadow-lg shadow-blue-500/20 outline-none hover:shadow-[0_8px_25px_rgba(0,76,145,0.3)] active:scale-[0.98]">
                Xác nhận
              </button>
            </div>
          )}

          {taskActionStatus === 'waiting_for_approval' && user?.role?.toUpperCase() === proposedBy && (
            <div className="flex gap-4 p-8 pt-6 border-t border-gray-100 relative z-10 bg-orange-50/50">
               <button disabled className="w-full py-4 rounded-2xl bg-[#e85c0d] text-white font-black uppercase tracking-wider shadow-lg shadow-orange-500/20 opacity-90 cursor-not-allowed flex flex-col items-center justify-center gap-1">
                 <div className="flex items-center gap-2">
                   <Clock className="w-5 h-5" /> 
                   <span>Chờ xác nhận (Đề xuất thay đổi)</span>
                 </div>
                 <div className="text-xs font-medium text-orange-100 lowercase tracking-normal bg-orange-800/20 px-3 py-1 rounded-full mt-1">
                    Bởi: {supporterName} - {actionTime}
                 </div>
               </button>
            </div>
          )}
        </div>

        {/* BOTTOM COMPONENT: DIGITAL AGREEMENT SIGNING */}
        {taskActionStatus === 'confirmed' && (
        <div className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-gray-100 p-0 flex flex-col hover:shadow-[0_12px_40px_-4px_rgba(243,112,33,0.08)] transition-shadow duration-500 overflow-hidden animate-in slide-in-from-right-8 fade-in relative duration-500">
          
          <div className="flex items-center gap-4 bg-[#f37021] p-8 pb-6 relative z-10">
            <div className="p-3 bg-white/10 text-white rounded-2xl shadow-inner border border-white/20">
              <FileSignature className="w-7 h-7" />
            </div>
            <div>
              <h2 className="text-2xl font-black text-white tracking-tight">Đơn thỏa thuận</h2>
              <p className="text-sm font-medium text-white/90 mt-1">Ký kết điện tử giữa các bên</p>
            </div>
          </div>

          <div className="flex-1 flex flex-col text-sm font-medium text-gray-700 p-8 pt-6">
            {/* Agreement Document Form */}
            <div className="bg-white rounded-2xl border border-slate-200/85 shadow-md p-6 md:p-8 font-sans w-full max-w-4xl mx-auto space-y-6 relative overflow-hidden mb-8">
              {/* Draft decorative watermark stamp */}
              <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 text-slate-100/30 text-4xl sm:text-6xl font-sans font-black tracking-widest uppercase pointer-events-none select-none -rotate-12">
                FPT UNIVERSITY
              </div>

              {/* National Emblem Text & FPTU Header */}
              <div className="flex flex-col sm:flex-row justify-between border-b border-slate-150 pb-5 text-xs gap-4 text-slate-550 relative z-10">
                <div className="text-left space-y-1">
                  <p className="font-extrabold text-slate-900 text-xs sm:text-sm uppercase tracking-wide">TRƯỜNG ĐẠI HỌC FPT HÒA LẠC</p>
                  <p className="font-bold text-[11px] text-slate-550">Phòng Dịch vụ sinh viên & Lễ tân</p>
                  <p className="text-[10px] text-slate-450 font-mono">Số văn bản: FPTU/TT-DVSV/2026-088</p>
                </div>
                <div className="text-left sm:text-right space-y-1">
                  <p className="font-extrabold text-slate-900 uppercase text-[11px] tracking-wider">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</p>
                  <p className="font-black text-[11px] text-[#f37021]">Độc lập - Tự do - Hạnh phúc</p>
                  <div className="w-24 sm:w-32 h-[1px] bg-slate-250 sm:ml-auto mt-1" />
                </div>
              </div>

              {/* Official Document Title */}
              <div className="text-center space-y-1.5 relative z-10 pt-2">
                <h4 className="text-base sm:text-xl font-black text-[#004c91] uppercase tracking-wide">
                  ĐƠN THỎA THUẬN VÀ GIAO VIỆC
                </h4>
                <p className="text-[11px] font-semibold text-slate-505 italic">
                  (V/v: Thiết lập trách nhiệm và phân công công việc)
                </p>
              </div>

              {/* Core Minutes Information */}
              <div className="space-y-4 text-xs text-slate-750 font-normal leading-relaxed relative z-10 font-sans">
                <p className="text-justify px-2">
                  Căn cứ quyết định phân công nhiệm vụ ngày hôm nay. Các bên liên quan đã được thông báo và đồng ý thực hiện đúng trách nhiệm công việc được giao tại campus Đại học FPT Hòa Lạc. Các bên tham gia trực tiếp xác nhận rõ ràng bằng chữ ký số:
                </p>

                {/* Side by side parties info */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 bg-slate-50/80 p-5 rounded-2xl border border-slate-200/50">
                  <div className="space-y-2 border-slate-200/45 md:border-r md:pr-2 pb-4 md:pb-0 border-b md:border-b-0">
                    <p className="font-black text-[#004c91] text-[11px] uppercase tracking-wide">BÊN GIAO VIỆC (Người phân công)</p>
                    <div className="flex items-center gap-1.5 pt-1">• <strong>Người phân công</strong>: <span className="text-[#004c91] font-bold">{coordinatorName}</span></div>
                    <div className="flex items-center gap-1.5">• <strong>Chức danh</strong>: <span className="text-slate-850 font-bold">Lãnh đạo đơn vị / Host</span></div>
                    <div className="flex items-center gap-1.5">• <strong>Nhiệm vụ</strong>: <span className="text-slate-850 font-bold">Giám sát và kiểm tra tiến độ</span></div>
                  </div>
                  <div className="space-y-2 md:pl-2">
                    <p className="font-black text-[#f37021] text-[11px] uppercase tracking-wide">BÊN NHẬN VIỆC (Người Hỗ trợ)</p>
                    <div className="flex items-center gap-1.5 pt-1">• <strong>Người nhận nhiệm vụ</strong>: <span className="text-[#f37021] font-bold">{supporterName}</span></div>
                    <div className="flex items-center gap-1.5">• <strong>Chức danh</strong>: <span className="text-slate-850 font-bold">Cán bộ / Tình nguyện viên</span></div>
                    <div className="flex items-center gap-1.5">• <strong>Nhiệm vụ</strong>: <span className="text-slate-850 font-bold">Thực hiện công tác được giao</span></div>
                  </div>
                </div>
              </div>

            </div>

            <div className="space-y-6 mt-auto">
              <div className="relative my-6">
                <div className="absolute inset-0 flex items-center" aria-hidden="true">
                  <div className="w-full border-t border-slate-200"></div>
                </div>
                <div className="relative flex justify-center text-xs uppercase font-extrabold tracking-widest">
                  <span className="bg-white text-slate-900 font-black px-4 py-1.5 rounded-full border border-slate-200 shadow-sm text-[11px] tracking-widest uppercase">BÀN GIAO</span>
                </div>
              </div>

              {/* Bàn giao Row */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="bg-slate-50/50 rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col gap-4">
                  <div>
                    <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-1.5">Ghi chú Bên Giao</label>
                    <textarea 
                      rows={2} 
                      value={bgNote} 
                      onChange={e => setBgNote(e.target.value)} 
                      disabled={!!bg1Signed}
                      className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#004c91] outline-none resize-none font-sans bg-white focus:ring-1 focus:ring-blue-100" 
                      placeholder="Không có ghi chú..."
                    />
                  </div>
                  <div 
                    className={`group relative p-3 rounded-xl border-2 transition-all shadow-sm cursor-pointer mt-auto ${
                      bg1Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-300 bg-white hover:border-[#004c91]/40'
                    }`}
                    onClick={() => {
                      if (!bg1Signed) setBg1Signed(`${coordinatorName} - ${getCurrentTime()}`);
                      else setBg1Signed(null);
                    }}
                  >
                    <div className="flex items-center gap-3">
                      <div className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 border shadow-sm ${bg1Signed ? 'bg-emerald-50 text-emerald-700 border-emerald-200 font-bold' : 'bg-slate-50 text-slate-400 border-slate-200'}`}>
                        {bg1Signed ? '✓' : <FileSignature className="w-4 h-4" />}
                      </div>
                      <div className="text-left flex-1 min-w-0">
                        {bg1Signed ? (
                          <>
                            <span className="text-[9px] font-black uppercase text-emerald-800 block mb-0.5">Xác nhận Giao</span>
                            <p className="text-[11px] font-extrabold text-slate-800 truncate">{bg1Signed.split(' - ')[0]}</p>
                            <p className="text-[9px] text-slate-500 font-mono mt-0.5">{bg1Signed.split(' - ')[1]}</p>
                          </>
                        ) : (
                          <>
                            <span className="text-[9px] font-black text-slate-500 uppercase block mb-1">Chữ ký Bên Giao</span>
                            <p className="text-[10px] font-bold text-slate-400">Chạm để đóng dấu điện tử <PenLine className="w-3 h-3 inline ml-1 opacity-60" /></p>
                          </>
                        )}
                      </div>
                    </div>
                  </div>
                </div>

                <div className="bg-slate-50/50 rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col gap-4">
                  <div className="mb-auto">
                    <label className="block text-[10px] font-black text-[#f37021] uppercase tracking-wider mb-1.5">Ghi chú Bên Nhận</label>
                    <p className="text-[11px] text-slate-500 italic p-2.5 bg-slate-100 rounded-xl border border-slate-200">Không có ghi chú tại thời điểm bàn giao.</p>
                  </div>
                  <div 
                    className={`group relative p-3 rounded-xl border-2 transition-all shadow-sm cursor-pointer mt-auto ${
                      bg2Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-300 bg-white hover:border-[#f37021]/40'
                    }`}
                    onClick={() => {
                      if (!bg2Signed) setBg2Signed(`${supporterName} - ${getCurrentTime()}`);
                      else setBg2Signed(null);
                    }}
                  >
                    <div className="flex items-center gap-3">
                      <div className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 border shadow-sm ${bg2Signed ? 'bg-emerald-50 text-emerald-700 border-emerald-200 font-bold' : 'bg-slate-50 text-slate-400 border-slate-200'}`}>
                        {bg2Signed ? '✓' : <FileSignature className="w-4 h-4" />}
                      </div>
                      <div className="text-left flex-1 min-w-0">
                        {bg2Signed ? (
                          <>
                            <span className="text-[9px] font-black uppercase text-emerald-800 block mb-0.5">Xác nhận Nhận</span>
                            <p className="text-[11px] font-extrabold text-slate-800 truncate">{bg2Signed.split(' - ')[0]}</p>
                            <p className="text-[9px] text-slate-500 font-mono mt-0.5">{bg2Signed.split(' - ')[1]}</p>
                          </>
                        ) : (
                          <>
                            <span className="text-[9px] font-black text-slate-500 uppercase block mb-1">Chữ ký Bên Nhận</span>
                            <p className="text-[10px] font-bold text-slate-400">Chạm để đóng dấu điện tử <PenLine className="w-3 h-3 inline ml-1 opacity-60" /></p>
                          </>
                        )}
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {bg1Signed && bg2Signed && (
                <div className="animate-fade-in-quick space-y-6">
                  <div className="relative my-8">
                    <div className="absolute inset-0 flex items-center" aria-hidden="true">
                      <div className="w-full border-t border-slate-200"></div>
                    </div>
                    <div className="relative flex justify-center text-xs uppercase font-extrabold tracking-widest">
                      <span className="bg-white text-slate-900 font-black px-4 py-1.5 rounded-full border border-slate-200 shadow-sm text-[11px] tracking-widest uppercase">NGHIỆM THU</span>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="bg-slate-50/50 rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col gap-4">
                      <div>
                         <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-1.5">Ghi chú Nghiệm thu (Bên Giao)</label>
                         <textarea 
                          rows={2} 
                          value={ntNote} 
                          onChange={e => setNtNote(e.target.value)} 
                          disabled={!!nt1Signed}
                          className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#004c91] outline-none resize-none font-sans bg-white focus:ring-1 focus:ring-blue-100" 
                          placeholder="Nhập đánh giá hoàn thành..."
                        />
                      </div>
                      <div 
                        className={`group relative p-3 rounded-xl border-2 transition-all shadow-sm cursor-pointer mt-auto ${
                          nt1Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-300 bg-white hover:border-[#004c91]/40'
                        }`}
                        onClick={() => {
                          if (!nt1Signed) setNt1Signed(`${coordinatorName} - ${getCurrentTime()}`);
                          else setNt1Signed(null);
                        }}
                      >
                        <div className="flex items-center gap-3">
                          <div className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 border shadow-sm ${nt1Signed ? 'bg-emerald-50 text-emerald-700 border-emerald-200 font-bold' : 'bg-slate-50 text-slate-400 border-slate-200'}`}>
                            {nt1Signed ? '✓' : <FileSignature className="w-4 h-4" />}
                          </div>
                          <div className="text-left flex-1 min-w-0">
                            {nt1Signed ? (
                              <>
                                <span className="text-[9px] font-black uppercase text-emerald-800 block mb-0.5">Nghiệm thu (Giao)</span>
                                <p className="text-[11px] font-extrabold text-slate-800 truncate">{nt1Signed.split(' - ')[0]}</p>
                                <p className="text-[9px] text-slate-500 font-mono mt-0.5">{nt1Signed.split(' - ')[1]}</p>
                              </>
                            ) : (
                              <>
                                <span className="text-[9px] font-black text-slate-500 uppercase block mb-1">Chữ ký Bên Giao</span>
                                <p className="text-[10px] font-bold text-slate-400">Chạm để đóng dấu điện tử <PenLine className="w-3 h-3 inline ml-1 opacity-60" /></p>
                              </>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>

                    <div className="bg-slate-50/50 rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col gap-4">
                      <div className="mb-auto">
                        <label className="block text-[10px] font-black text-[#f37021] uppercase tracking-wider mb-1.5">Ghi chú Nghiệm thu (Bên Nhận)</label>
                        <p className="text-[11px] text-slate-500 italic p-2.5 bg-slate-100 rounded-xl border border-slate-200">Đã hoàn thành.</p>
                      </div>
                      <div 
                        className={`group relative p-3 rounded-xl border-2 transition-all shadow-sm cursor-pointer mt-auto ${
                          nt2Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-300 bg-white hover:border-[#f37021]/40'
                        }`}
                        onClick={() => {
                          if (!nt2Signed) setNt2Signed(`${supporterName} - ${getCurrentTime()}`);
                          else setNt2Signed(null);
                        }}
                      >
                        <div className="flex items-center gap-3">
                          <div className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 border shadow-sm ${nt2Signed ? 'bg-emerald-50 text-emerald-700 border-emerald-200 font-bold' : 'bg-slate-50 text-slate-400 border-slate-200'}`}>
                            {nt2Signed ? '✓' : <FileSignature className="w-4 h-4" />}
                          </div>
                          <div className="text-left flex-1 min-w-0">
                            {nt2Signed ? (
                              <>
                                <span className="text-[9px] font-black uppercase text-emerald-800 block mb-0.5">Trình nghiệm thu (Nhận)</span>
                                <p className="text-[11px] font-extrabold text-slate-800 truncate">{nt2Signed.split(' - ')[0]}</p>
                                <p className="text-[9px] text-slate-500 font-mono mt-0.5">{nt2Signed.split(' - ')[1]}</p>
                              </>
                            ) : (
                              <>
                                <span className="text-[9px] font-black text-slate-500 uppercase block mb-1">Chữ ký Bên Nhận</span>
                                <p className="text-[10px] font-bold text-slate-400">Chạm để đóng dấu điện tử <PenLine className="w-3 h-3 inline ml-1 opacity-60" /></p>
                              </>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
        )}

      </div>

      {/* FOOTER ACTION */}
      <div className="mt-8 flex justify-center lg:justify-end border-t border-gray-200 pt-6 relative">
         {isTaskComplete && (
           <div className="absolute right-12 top-6 w-64 h-64 bg-green-400 rounded-full blur-[100px] opacity-20 pointer-events-none animate-pulse"></div>
         )}
        <button 
          disabled={!isTaskComplete}
          className={`flex items-center justify-center gap-2 w-full lg:w-auto px-6 py-2.5 rounded-lg text-sm font-bold uppercase tracking-wider transition-all duration-500 outline-none
            ${isTaskComplete 
              ? 'bg-gradient-to-r from-[#22c55e] to-[#16a34a] text-white shadow-[0_8px_30px_rgba(34,197,94,0.35)] hover:shadow-[0_12px_40px_rgba(34,197,94,0.5)] hover:-translate-y-1 scale-100' 
              : 'bg-gray-100 text-gray-400 border-2 border-gray-200 cursor-not-allowed scale-95'
            }
          `}
        >
          {isTaskComplete && <CheckCircle2 className="w-5 h-5" />}
          {!isTaskComplete && <AlertCircle className="w-5 h-5 opacity-50" />}
          {isTaskComplete ? 'Hoàn thành nhiệm vụ' : 'Chưa đủ điều kiện'}
        </button>
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
                  placeholder="Vui lòng nhập lý do từ chối nhiệm vụ này để hệ thống ghi nhận..."
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
                onClick={() => {
                  if (tempRejectReason.trim()) {
                    setRejectReason(tempRejectReason);
                    setTaskActionStatus('rejected');
                    setActionTime(getCurrentTime());
                    setIsRejectModalOpen(false);
                  }
                }}
                disabled={!tempRejectReason.trim()}
                className="px-6 py-3 rounded-xl font-black text-white bg-[#f37021] hover:bg-orange-600 transition-colors shadow-lg shadow-orange-500/20 disabled:opacity-50 disabled:cursor-not-allowed outline-none uppercase tracking-wider"
              >
                Xác nhận từ chối
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal Submit Proposal */}
      {isSubmitProposalModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
          <div className="bg-white rounded-3xl w-full max-w-md shadow-2xl overflow-hidden animate-in zoom-in-95 duration-300">
            <div className="p-6 border-b border-gray-100 bg-orange-50/50">
              <h3 className="text-xl font-black text-[#e85c0d] uppercase tracking-tight">Xác nhận gửi đề xuất</h3>
            </div>
            <div className="p-6">
              <p className="text-gray-700 text-[15px] font-medium leading-relaxed">
                Bạn có chắc chắn muốn gửi đề xuất thay đổi thời gian và nội dung công việc này không? 
                <br/><br/>
                Yêu cầu đề xuất sẽ được gửi lên hệ thống và chuyển trạng thái công việc thành <strong>Chờ xác nhận</strong>.
              </p>
            </div>
            <div className="p-6 bg-gray-50 border-t border-gray-100 flex justify-end gap-3">
              <button 
                onClick={() => setIsSubmitProposalModalOpen(false)}
                className="px-6 py-3 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none"
              >
                Hủy bỏ
              </button>
              <button 
                onClick={() => {
                   setTaskActionStatus('waiting_for_approval');
                   setProposedBy(user?.role?.toUpperCase() || 'DEPT');
                   setIsProposing(false);
                   setActionTime(getCurrentTime());
                   setIsSubmitProposalModalOpen(false);
                }}
                className="px-6 py-3 rounded-xl font-black text-white bg-[#16a34a] hover:bg-green-700 transition-colors shadow-lg shadow-green-500/20 outline-none uppercase tracking-wider"
              >
                Gửi đề xuất
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}
