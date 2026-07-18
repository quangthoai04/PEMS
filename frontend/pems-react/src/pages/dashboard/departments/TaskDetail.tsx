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
  FileText,
  Download,
  Loader2
} from 'lucide-react';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import toast from 'react-hot-toast';
import { toVietnamCalendarDate } from '../../../shared/utils/vietnamTime';
import { LogisticsExpensePanel } from './LogisticsExpensePanel';

function fmtDateTime(value?: string | null): string {
  if (!value) return '—';
  const [d, t] = value.replace(' ', 'T').split('T');
  if (!d) return value;
  const [y, m, day] = d.split('-');
  const hm = (t || '').slice(0, 5);
  if (!y || !m || !day) return value;
  return hm ? `${hm} ${day}/${m}/${y}` : `${day}/${m}/${y}`;
}

const inMemoryTaskStore: Record<string, any> = {};

export function TaskDetail() {
  const navigate = useNavigate();
  const { id, taskId } = useParams();

  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const isLeader = user?.role === 'ADMIN' || user?.role === 'HO' || user?.role?.toUpperCase() === 'STAFF' || (user?.role?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'LEADER');
  const isStaff = user?.role?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'STAFF';

  const location = useLocation();
  const taskStatusFromState = location.state?.taskStatus || "Đang làm";
  const assigneeIdFromState = location.state?.assigneeId;

  const isDeptLeader = user?.role?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'LEADER';

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

  const [detailData, setDetailData] = useState<any>(null);
  const [isLoading, setIsLoading] = useState(true);

  const fetchDetail = async () => {
    try {
      setIsLoading(true);
      const data = await departmentReceptionTasksApi.getRequestDetail(taskId!);
      setDetailData(data);
      if (data) {
        if (data.status === 'REQUESTED') setTaskActionStatus('pending');
        else if (data.status === 'ACCEPTED' || data.status === 'IN_PROGRESS' || data.status === 'DONE') setTaskActionStatus('confirmed');
        else if (data.status === 'REJECTED') {
          setTaskActionStatus('rejected');
          setRejectReason(data.rejectReason || '');
        }
        else if (data.status === 'CHANGE_PROPOSED') {
          setTaskActionStatus('waiting_for_approval');
        }
        if (data.actionTime) setActionTime(data.actionTime);
        if (data.proposedTime) setProposedTime(data.proposedTime);
        if (data.proposedContent) setProposedContent(data.proposedContent);
        if (data.proposedByRole) setProposedBy(data.proposedByRole);
      }
    } catch (e) {
      console.error(e);
      toast.error('Lỗi khi tải chi tiết nhiệm vụ');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (taskId) fetchDetail();
  }, [taskId]);

  const [taskActionStatus, setTaskActionStatus] = useState<'pending' | 'confirmed' | 'rejected' | 'waiting_for_approval'>('pending');
  const [rejectReason, setRejectReason] = useState("");
  const [tempRejectReason, setTempRejectReason] = useState("");
  const [actionTime, setActionTime] = useState<string | null>(null);
  const [isRejectModalOpen, setIsRejectModalOpen] = useState(false);

  const [isProposing, setIsProposing] = useState(false);
  const [proposedTime, setProposedTime] = useState(() => inMemoryTaskStore[`taskDetail_${taskId}_proposedTime`] || "");
  const [proposedContent, setProposedContent] = useState(() => inMemoryTaskStore[`taskDetail_${taskId}_proposedContent`] || "");
  const [proposedQuantity, setProposedQuantity] = useState(() => inMemoryTaskStore[`taskDetail_${taskId}_proposedQuantity`] || "");
  const [proposedBy, setProposedBy] = useState(() => inMemoryTaskStore[`taskDetail_${taskId}_proposedBy`] || "DEPARTMENT");
  const [proposalNote, setProposalNote] = useState('');
  const [isSubmitProposalModalOpen, setIsSubmitProposalModalOpen] = useState(false);
  const [busySign, setBusySign] = useState(false);
  const [borrowNote, setBorrowNote] = useState('');
  const [returnNote, setReturnNote] = useState('');

  const getCurrentTime = () => {
    // Giờ Việt Nam cố định cho mốc ký — không theo timezone browser.
    const now = toVietnamCalendarDate(new Date())!;
    return `${String(now.getUTCDate()).padStart(2, '0')}/${String(now.getUTCMonth() + 1).padStart(2, '0')}/${now.getUTCFullYear()}, ${String(now.getUTCHours()).padStart(2, '0')}:${String(now.getUTCMinutes()).padStart(2, '0')}`;
  };

  const isTaskComplete = detailData?.borrowProviderSignature?.signedAt && detailData?.borrowBorrowerSignature?.signedAt && detailData?.returnProviderSignature?.signedAt && detailData?.returnBorrowerSignature?.signedAt;

  const handleSign = async (type: 'BORROW' | 'RETURN') => {
    setBusySign(true);
    try {
      const note = type === 'BORROW' ? borrowNote : returnNote;
      await departmentReceptionTasksApi.signHandover(detailData.logisticsItemId, type, 'PROVIDER', note.trim() || undefined);
      toast.success(`Đã ký ${type === 'BORROW' ? 'bàn giao' : 'nhận lại'}`);
      fetchDetail();
    } catch (e: any) {
      toast.error(e.response?.data?.message || 'Không thể ký biên bản. Vui lòng thử lại.');
    } finally {
      setBusySign(false);
    }
  };

  const handleConfirm = async () => {
    try {
      await departmentReceptionTasksApi.confirmRequest(taskId!);
      toast.success('Xác nhận nhiệm vụ thành công');
      fetchDetail();
    } catch (e: any) {
      toast.error(e.response?.data?.message || 'Có lỗi xảy ra');
    }
  };

  const handleReject = async () => {
    if (!tempRejectReason.trim()) return;
    try {
      await departmentReceptionTasksApi.rejectRequest(taskId!, tempRejectReason);
      toast.success('Từ chối thành công');
      setIsRejectModalOpen(false);
      fetchDetail();
    } catch (e: any) {
      toast.error(e.response?.data?.message || 'Có lỗi xảy ra');
    }
  };

  const handleProposeChange = async () => {
    // proposal_note (lý do) is mandatory; proposed quantity/time/content are optional.
    const note = proposalNote.trim() || proposedContent.trim();
    if (!note) { toast.error('Vui lòng nhập lý do/ghi chú đề xuất.'); return; }
    const qty = proposedQuantity.trim() ? Number(proposedQuantity.trim()) : null;
    if (qty != null && (Number.isNaN(qty) || qty < 1)) { toast.error('Số lượng đề xuất phải là số nguyên ≥ 1.'); return; }
    try {
      await departmentReceptionTasksApi.proposeChange(taskId!, {
        proposedQuantity: qty,
        proposedUsageStartAt: proposedTime || null,
        proposedUsageEndAt: null,
        proposedDescription: proposedContent || null,
        proposalNote: note,
      });
      toast.success('Gửi đề xuất thay đổi thành công');
      setIsSubmitProposalModalOpen(false);
      setIsProposing(false);
      fetchDetail();
    } catch (e: any) {
      toast.error(e.response?.data?.message || 'Có lỗi xảy ra');
    }
  };

  if (isLoading) return <div className="p-8 text-center">Đang tải...</div>;
  if (!detailData) return <div className="p-8 text-center">Không tìm thấy chi tiết</div>;

  return (
    <div className="w-full pb-24 relative animate-in fade-in duration-500">
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
                taskActionStatus === 'pending' 
                  ? 'bg-gradient-to-r from-orange-50 to-orange-100 text-orange-600 border border-orange-200' 
                  : 'bg-gradient-to-r from-gray-50 to-gray-100 text-gray-600 border border-gray-200'
              }`}>
                <div className="w-2 h-2 rounded-full bg-orange-500 mr-2 animate-pulse"></div>
                {taskActionStatus === 'pending' ? 'Chưa tiếp nhận' : taskActionStatus === 'confirmed' ? 'Đang thực hiện' : taskActionStatus === 'rejected' ? 'Từ chối' : 'Chờ duyệt'}
              </span>
            </h1>
          </div>
        </div>
      </div>

      {/* Main Content */}
      <div className="flex flex-col w-full gap-8">
        
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

          <div className="flex-1 space-y-2 relative z-10 p-8 pt-6">
            
            {/* Flat Grid for Metadata (Clean layout without borders) */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-6 bg-slate-50/50 p-6 rounded-2xl text-xs">
              <div>
                <div className="flex items-center gap-2 text-slate-400 mb-1">
                  <User className="w-4 h-4" />
                  <span className="text-[10px] font-black uppercase tracking-wider">Người gửi</span>
                </div>
                <div className="text-[13px] font-black text-[#004c91]">{detailData.senderName}</div>
              </div>
              
              <div>
                <div className="flex items-center gap-2 text-slate-400 mb-1">
                  <Clock className="w-4 h-4" />
                  <span className="text-[10px] font-black uppercase tracking-wider">Thời gian gửi</span>
                </div>
                <div className="text-[13px] font-black text-[#004c91]">{detailData.requestedAt}</div>
              </div>

              <div className="md:col-span-2">
                <div className="flex items-center gap-2 text-slate-400 mb-1">
                  <Users className="w-4 h-4" />
                  <span className="text-[10px] font-black uppercase tracking-wider">Đoàn khách</span>
                </div>
                <div className="text-sm font-black text-[#004c91] border-l-[3px] border-[#f37021] pl-2 py-0.5 bg-transparent">
                  {detailData.delegationName}
                </div>
              </div>

              <div className="md:col-span-2">
                <div className="flex items-center gap-2 text-slate-400 mb-1">
                  <Calendar className="w-4 h-4" />
                  <span className="text-[10px] font-black uppercase tracking-wider">Thời gian sử dụng</span>
                </div>
                <div className="text-[14px] font-bold text-slate-800 flex items-center gap-2 mt-1">
                   <span className="px-2 py-0.5 bg-white rounded-lg border border-slate-200 shadow-sm text-[#004c91]">{detailData.startTime}</span>
                   <ChevronRight className="w-3 h-3 text-slate-400" />
                   <span className="px-2 py-0.5 bg-white rounded-lg border border-slate-200 shadow-sm text-[#004c91]">{detailData.endTime}</span>
                   <span className="text-[#004c91] font-black ml-1">{detailData.date}</span>
                </div>
              </div>

              {(isProposing || taskActionStatus === 'waiting_for_approval') && (
                <div className="md:col-span-4 bg-[#fff1e0] p-4 rounded-xl border border-[#ffd3a8] transition-all relative overflow-hidden animate-in fade-in zoom-in-95 duration-200">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <div className="flex items-center gap-2 text-[#e85c0d] mb-1.5">
                        <Calendar className="w-4 h-4" />
                        <span className="text-[10px] font-black uppercase tracking-wider">Thời gian sử dụng (Đề xuất)</span>
                      </div>
                      <input
                        type="text"
                        value={proposedTime}
                        onChange={(e) => setProposedTime(e.target.value)}
                        disabled={taskActionStatus === 'waiting_for_approval'}
                        placeholder="Nhập đề xuất thời gian..."
                        className="w-full px-3 py-2 rounded-lg border border-[#ffc288] bg-white text-xs font-medium focus:outline-none focus:border-[#e85c0d] focus:ring-1 focus:ring-[#e85c0d] text-gray-800 transition-shadow outline-none shadow-sm disabled:bg-orange-50/50 disabled:text-gray-500 disabled:cursor-not-allowed"
                      />
                    </div>
                    <div>
                      <div className="flex items-center gap-2 text-[#e85c0d] mb-1.5">
                        <FileText className="w-4 h-4" />
                        <span className="text-[10px] font-black uppercase tracking-wider">Số lượng (Đề xuất)</span>
                      </div>
                      <input
                        type="text"
                        inputMode="numeric"
                        value={proposedQuantity}
                        onChange={(e) => setProposedQuantity(e.target.value.replace(/\D/g, ''))}
                        disabled={taskActionStatus === 'waiting_for_approval'}
                        placeholder="Để trống nếu không đổi số lượng"
                        className="w-full px-3 py-2 rounded-lg border border-[#ffc288] bg-white text-xs font-medium focus:outline-none focus:border-[#e85c0d] focus:ring-1 focus:ring-[#e85c0d] text-gray-800 transition-shadow outline-none shadow-sm disabled:bg-orange-50/50 disabled:text-gray-500 disabled:cursor-not-allowed"
                      />
                    </div>
                  </div>
                </div>
              )}
            </div>

            <div className="pt-2 hover:opacity-100 transition-all cursor-default">
              <div className="flex items-center gap-2 text-slate-400 mb-2 pl-1">
                  <FileText className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết công việc</span>
              </div>
              <div className="px-5 py-4 bg-white rounded-xl text-sm font-medium text-slate-700 leading-relaxed border border-slate-200 hover:border-[#004c91]/30 transition-all shadow-sm">
                <div className="whitespace-pre-line">
                  {detailData.note || <span className="italic text-slate-400">Không có ghi chú.</span>}
                </div>
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
                <div className="flex items-center gap-2 text-[#e85c0d] mt-2">
                  <FileText className="w-4 h-4" />
                  <span className="text-[11px] font-bold uppercase tracking-wider">Lý do đề xuất <span className="text-red-500">*</span></span>
                </div>
                <textarea
                  value={proposalNote}
                  onChange={(e) => setProposalNote(e.target.value)}
                  disabled={taskActionStatus === 'waiting_for_approval'}
                  placeholder="Vì sao cần thay đổi (bắt buộc)..."
                  rows={2}
                  maxLength={1000}
                  className="p-4 bg-[#fff1e0] rounded-2xl text-sm font-medium text-gray-700 leading-relaxed border border-[#ffc288] focus:outline-none focus:border-[#e85c0d] focus:ring-1 focus:ring-[#e85c0d] transition-all shadow-inner w-full resize-none disabled:bg-orange-50/50 disabled:text-gray-500 disabled:cursor-not-allowed"
                />
              </div>
            )}

            {!isProposing && taskActionStatus === 'pending' && ['DEPARTMENT', 'STAFF'].includes(user?.role?.toUpperCase()) && (
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
                onClick={handleConfirm}
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

          {taskActionStatus === 'waiting_for_approval' && user?.role?.toUpperCase() !== proposedBy && (
            <div className="flex gap-4 p-8 pt-6 border-t border-gray-100 relative z-10 bg-gray-50/50">
              <button 
                onClick={() => setIsRejectModalOpen(true)}
                className="flex-1 py-4 rounded-2xl border-2 border-[#f37021] text-[#f37021] hover:bg-[#f37021] hover:text-white font-black uppercase tracking-wider transition-all duration-300 outline-none hover:shadow-lg hover:shadow-orange-500/20 active:scale-[0.98]">
                Từ chối
              </button>
              <button 
                onClick={handleConfirm}
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
                    Lúc: {detailData.actionTime}
                 </div>
               </button>
            </div>
          )}
        </div>

        {/* BOTTOM COMPONENT: INLINE HANDOVER DOCUMENT */}
        {taskActionStatus === 'confirmed' && (() => {
          const bg1 = detailData.borrowProviderSignature?.name ? `${detailData.borrowProviderSignature.name}` : null;
          const bg2 = detailData.borrowBorrowerSignature?.name ? `${detailData.borrowBorrowerSignature.name}` : null;
          const nt1 = detailData.returnBorrowerSignature?.name ? `${detailData.returnBorrowerSignature.name}` : null;
          const nt2 = detailData.returnProviderSignature?.name ? `${detailData.returnProviderSignature.name}` : null;

          const canSignBG1 = !bg1; // Department is PROVIDER
          const isBorrowDone = bg1 && bg2;
          const canSignNT2 = isBorrowDone && nt1 && !nt2; // Department is PROVIDER

          // parse date for top section
          let handoverTime = "....";
          let handoverDate = "..../..../20.....";
          if (detailData.borrowProviderSignature?.signedAt) {
            const [d, t] = detailData.borrowProviderSignature.signedAt.replace(' ', 'T').split('T');
            if (d && t) {
              const hm = t.slice(0, 5);
              const [y, m, day] = d.split('-');
              handoverTime = `${hm}`;
              handoverDate = `${day}/${m}/${y}`;
            }
          }

          const hostName = detailData.borrowBorrowerSignature?.name || detailData.returnBorrowerSignature?.name || detailData.senderName || 'Đại diện Host đón tiếp';
          const providerName = detailData.borrowProviderSignature?.name || detailData.returnProviderSignature?.name || detailData.assigneeName || 'Đại diện Phòng ban';

          return (
            <div className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-gray-100 flex flex-col hover:shadow-[0_12px_40px_-4px_rgba(243,112,33,0.08)] transition-shadow duration-500 overflow-hidden animate-in slide-in-from-right-8 fade-in relative duration-500 font-sans">
              
              {/* Header */}
              <div className="flex items-center gap-4 bg-[#f37021] px-8 py-5 relative z-10 flex-wrap justify-between print:hidden">
                <div className="flex items-center gap-4">
                  <div className="p-3 bg-white/10 text-white rounded-2xl shadow-inner border border-white/20 shrink-0">
                    <FileSignature className="w-6 h-6" />
                  </div>
                  <div>
                    <h2 className="text-xl font-black text-white tracking-tight uppercase">Biên bản điện tử</h2>
                    <p className="text-xs font-bold text-white/90 mt-0.5">Bàn giao & Nghiệm thu tài sản</p>
                  </div>
                </div>
                <button 
                  type="button" 
                  onClick={() => window.print()}
                  className="flex items-center gap-1.5 text-xs font-bold text-white bg-white/20 hover:bg-white/30 px-4 py-2 rounded-lg transition-colors outline-none border border-white/10 shrink-0 shadow-sm"
                >
                  <Download className="w-4 h-4" /> In / Tải PDF
                </button>
              </div>

              {/* Document Area */}
              <div className="p-6 md:p-10 bg-white">
                <style type="text/css" media="print">
                  {`
                    body * { visibility: hidden; }
                    .print-section, .print-section * { visibility: visible; }
                    .print-section { position: absolute; left: 0; top: 0; width: 100%; margin: 0; padding: 0; }
                    .print-hidden { display: none !important; }
                  `}
                </style>
                <div className="print-section max-w-4xl mx-auto">
                  <div className="text-center space-y-1 mb-8">
                    <h4 className="text-xl sm:text-2xl font-bold uppercase tracking-wide">
                      BIÊN BẢN BÀN GIAO VÀ NGHIỆM THU
                    </h4>
                    <p className="text-lg font-bold uppercase">
                      TÀI SẢN / TRANG THIẾT BỊ
                    </p>
                  </div>

                  <div className="space-y-3 text-[15px] leading-relaxed mb-6">
                    <p>
                      Hôm nay, lúc: <b>{handoverTime}</b> giờ, ngày <b>{handoverDate}</b>, tại: <b>Trường Đại học FPT Hòa Lạc</b>.
                    </p>
                    <p>Chúng tôi gồm:</p>
                    <div className="space-y-2 pl-4">
                      <div className="flex flex-wrap gap-x-8 gap-y-2">
                        <p className="flex-1 min-w-[250px]">Người bàn giao: <b>{providerName}</b></p>
                        <p className="flex-1 min-w-[200px]">Bộ phận: <b>Phòng ban hỗ trợ</b></p>
                      </div>
                      <div className="flex flex-wrap gap-x-8 gap-y-2">
                        <p className="flex-1 min-w-[250px]">Người nhận bàn giao: <b>{hostName}</b></p>
                        <p className="flex-1 min-w-[200px]">Bộ phận: <b>Host (Tiếp đón)</b></p>
                      </div>
                      <p>Lý do bàn giao: <b>Phục vụ công tác đón tiếp đoàn khách</b></p>
                      <p>Đoàn khách: <b>{detailData.delegationName}</b></p>
                      <p>Thời gian hẹn trả tài sản: <b>Sau khi kết thúc chuyến thăm</b></p>
                    </div>
                  </div>

                  <p className="font-bold text-[15px] mb-2">Cùng bàn giao tài sản với tình trạng sau:</p>
                  <div className="overflow-x-auto mb-6">
                    <table className="w-full border-collapse border border-slate-500 text-[14px]">
                      <thead>
                        <tr className="bg-slate-50">
                          <th className="border border-slate-500 p-2 text-center w-12">STT</th>
                          <th className="border border-slate-500 p-2 text-center">Nội dung</th>
                          <th className="border border-slate-500 p-2 text-center w-24">Số Lượng</th>
                          <th className="border border-slate-500 p-2 text-center">Tình Trạng bàn giao</th>
                          <th className="border border-slate-500 p-2 text-center">Tình Trạng nhận</th>
                          <th className="border border-slate-500 p-2 text-center">Ghi chú</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr>
                          <td className="border border-slate-500 p-2 text-center">1</td>
                          <td className="border border-slate-500 p-2 font-semibold">{detailData.title}</td>
                          <td className="border border-slate-500 p-2 text-center">{detailData.quantity || 1}</td>
                          <td className="border border-slate-500 p-2 text-center">
                            {detailData.borrowNote || (bg1 ? 'Tốt' : '')}
                          </td>
                          <td className="border border-slate-500 p-2 text-center">
                            {bg2 ? 'Đã xác nhận' : ''}
                          </td>
                          <td className="border border-slate-500 p-2"></td>
                        </tr>
                      </tbody>
                    </table>
                  </div>

                  <div className="space-y-1 text-[14px] mb-8">
                    <p className="font-bold">Quy định khi sử dụng tài sản:</p>
                    <ul className="list-disc pl-8 space-y-1">
                      <li>Người mượn tài sản phải tuân thủ đúng mục đích sử dụng, không tự ý chuyển giao cho người khác.</li>
                      <li>Khi có vấn đề xảy ra (bị hỏng hoặc không nguyên hiện trạng ban đầu), <b>người mượn tài sản</b> sẽ phải chịu hoàn toàn trách nhiệm chi trả chi phí sửa chữa/đền bù.</li>
                      <li>An toàn trong quá trình sử dụng tài sản sẽ do <b>người mượn tài sản</b> chịu hoàn toàn trách nhiệm.</li>
                      <li>Ghi chú khác: ....................................................................................................................</li>
                    </ul>
                    <p className="mt-4">
                      Tôi là <b>{hostName}</b>, đã đọc hiểu và cam kết thực hiện đúng quy định sử dụng.
                    </p>
                  </div>

                  {/* 4 NÚT KÝ */}
                  <div className="space-y-8">
                    {/* KHỐI BÀN GIAO */}
                    <div className="relative my-7">
                      <div className="absolute inset-0 flex items-center" aria-hidden="true"><div className="w-full border-t border-slate-300"></div></div>
                      <div className="relative flex justify-center"><span className="bg-white px-4 py-1.5 text-[10px] font-black text-slate-900 uppercase tracking-widest border border-slate-200 rounded-full shadow-sm">BÀN GIAO</span></div>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-slate-50/70 p-4 rounded-2xl border border-slate-200">
                      {/* Bên Giao (Department) */}
                      <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                        <div>
                          <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-2">Ghi chú Bên Giao</label>
                          {canSignBG1 ? (
                            <textarea rows={2} value={borrowNote} onChange={e => setBorrowNote(e.target.value)} disabled={busySign} className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#004c91] focus:ring-1 focus:ring-blue-200 outline-none resize-none bg-slate-50/30 print-hidden" placeholder="Ghi nhận tình trạng trước khi giao..." />
                          ) : (
                            <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                              {detailData.borrowNote || 'Tốt. Đã bàn giao.'}
                            </div>
                          )}
                        </div>
                        <div className={`border-2 rounded-xl p-3 relative ${bg1 ? 'border-emerald-500 bg-emerald-50/20' : canSignBG1 ? 'border-dashed border-[#004c91]/50 bg-blue-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                          {bg1 ? (
                            <div className="flex items-center gap-2.5">
                              <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                              <div>
                                <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ DUYỆT BÀN GIAO</span>
                                <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{bg1}</p>
                                <p className="text-[9px] text-slate-500">{fmtDateTime(detailData.borrowProviderSignature?.signedAt)}</p>
                              </div>
                            </div>
                          ) : (
                            <div className="flex flex-row items-center justify-between gap-3 w-full">
                              <div className="flex items-center gap-2">
                                <FileText className={`w-4 h-4 shrink-0 ${canSignBG1 ? 'text-[#004c91]' : 'text-slate-400'}`} />
                                <div className="text-left">
                                  <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao</span>
                                  <span className="text-[9px] text-slate-450">{canSignBG1 ? 'Nhấp để xác nhận' : 'Đã ký'}</span>
                                </div>
                              </div>
                              {canSignBG1 && (
                                <button type="button" onClick={() => handleSign('BORROW')} disabled={busySign} className="py-2 px-3 bg-blue-50 hover:bg-blue-100 text-[#004c91] font-extrabold text-[11px] rounded-xl border border-blue-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-sm print-hidden outline-none">
                                  {busySign ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileText className="w-3.5 h-3.5" />} Ký Giao
                                </button>
                              )}
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Bên Nhận (Host) */}
                      <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                        <div>
                          <label className="block text-[10px] font-black text-[#f37021] uppercase tracking-wider mb-2">Ghi chú Bên Nhận</label>
                          <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                            {bg2 ? 'Đã xác nhận nhận tài sản.' : 'Chưa ký nhận.'}
                          </div>
                        </div>
                        <div className={`border-2 rounded-xl p-3 relative ${bg2 ? 'border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                          {bg2 ? (
                            <div className="flex items-center gap-2.5">
                              <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                              <div>
                                <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ XÁC NHẬN</span>
                                <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{bg2}</p>
                                <p className="text-[9px] text-slate-500">{fmtDateTime(detailData.borrowBorrowerSignature?.signedAt)}</p>
                              </div>
                            </div>
                          ) : (
                            <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                              <div className="flex items-center gap-2">
                                <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                                <div className="text-left">
                                  <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                                  <span className="text-[9px] text-slate-450">Chờ Host ký</span>
                                </div>
                              </div>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>

                    {/* KHỐI NGHIỆM THU */}
                    <div className={`transition-opacity pt-4 ${!isBorrowDone ? 'opacity-40 pointer-events-none' : ''}`}>
                      <div className="relative my-7">
                        <div className="absolute inset-0 flex items-center" aria-hidden="true"><div className="w-full border-t border-slate-300"></div></div>
                        <div className="relative flex justify-center"><span className="bg-white px-4 py-1.5 text-[10px] font-black text-slate-900 uppercase tracking-widest border border-slate-200 rounded-full shadow-sm">NGHIỆM THU</span></div>
                      </div>

                      <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-[#f8fbfe] p-4 rounded-2xl border border-blue-200/50">
                        {/* Bên Giao (Host trả) */}
                        <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                          <div>
                            <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-2">Ghi chú Nghiệm thu (Bên Giao)</label>
                            <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                              {detailData.returnNote || (nt1 ? 'Đã bàn giao trả tài sản.' : 'Chưa bàn giao trả')}
                            </div>
                          </div>
                          <div className={`border-2 rounded-xl p-3 relative ${nt1 ? 'border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                            {nt1 ? (
                              <div className="flex items-center gap-2.5">
                                <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                                <div>
                                  <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ DUYỆT NGHIỆM THU</span>
                                  <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{nt1}</p>
                                  <p className="text-[9px] text-slate-500">{fmtDateTime(detailData.returnBorrowerSignature?.signedAt)}</p>
                                </div>
                              </div>
                            ) : (
                              <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                                <div className="flex items-center gap-2">
                                  <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                                  <div className="text-left">
                                    <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao (Trả)</span>
                                    <span className="text-[9px] text-slate-450">Chờ Host ký trả</span>
                                  </div>
                                </div>
                              </div>
                            )}
                          </div>
                        </div>

                        {/* Bên Nhận (Department) */}
                        <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                          <div>
                            <label className="block text-[10px] font-black text-[#f37021] uppercase tracking-wider mb-2">Ghi chú Nghiệm thu (Bên Nhận)</label>
                            {canSignNT2 ? (
                              <textarea rows={2} value={returnNote} onChange={e => setReturnNote(e.target.value)} disabled={busySign} className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#f37021] focus:ring-1 focus:ring-orange-200 outline-none resize-none bg-slate-50/30 print-hidden" placeholder="Ghi nhận hiện trạng lúc nhận lại..." />
                            ) : (
                              <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                                {nt2 ? 'Đã nghiệm thu nhận lại tài sản.' : 'Chờ Host ký trả trước.'}
                              </div>
                            )}
                          </div>
                          <div className={`border-2 rounded-xl p-3 relative ${nt2 ? 'border-emerald-500 bg-emerald-50/20' : canSignNT2 ? 'border-dashed border-[#f37021]/50 bg-orange-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                            {nt2 ? (
                              <div className="flex items-center gap-2.5">
                                <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                                <div>
                                  <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ NGHIỆM THU LẠI</span>
                                  <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{nt2}</p>
                                  <p className="text-[9px] text-slate-500">{fmtDateTime(detailData.returnProviderSignature?.signedAt)}</p>
                                </div>
                              </div>
                            ) : (
                              <div className="flex flex-row items-center justify-between gap-3 w-full">
                                <div className="flex items-center gap-2">
                                  <FileText className={`w-4 h-4 shrink-0 ${canSignNT2 ? 'text-[#f37021]' : 'text-slate-400'}`} />
                                  <div className="text-left">
                                    <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận (Lại)</span>
                                    <span className="text-[9px] text-slate-450">{canSignNT2 ? 'Nhấp để xác nhận' : 'Chờ Host trả'}</span>
                                  </div>
                                </div>
                                {canSignNT2 && (
                                  <button type="button" onClick={() => handleSign('RETURN')} disabled={busySign} className="py-2 px-3 bg-orange-50 hover:bg-orange-100 text-[#f37021] font-extrabold text-[11px] rounded-xl border border-orange-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-sm print-hidden outline-none">
                                    {busySign ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileText className="w-3.5 h-3.5" />} Ký Nhận
                                  </button>
                                )}
                              </div>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          );
        })()}

        {isTaskComplete && (
          <LogisticsExpensePanel logisticsItemId={detailData.logisticsItemId} />
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
                onClick={handleProposeChange}
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
