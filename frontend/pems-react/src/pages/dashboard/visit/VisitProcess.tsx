/**
 * Trang VisitProcess
 * Chu kỳ giám sát chung toàn trình quá trình đón tiếp theo giai đoạn (Trước/Trong/Sau).
 */

import React, { useState, useEffect } from 'react';
import { useNavigate, useParams, useLocation } from 'react-router-dom';
import { 
  ChevronRight, 
  ChevronDown, 
  ChevronUp, 
  Clock, 
  Calendar, 
  Users, 
  CheckCircle2,
  Phone,
  Mail,
  MapPin,
  Building2,
  Car,
  Coffee,
  MoreHorizontal,
  MonitorPlay,
  UserCheck,
  UserX,
  CheckCircle,
  Bell,
  Plus,
  Search,
  Edit3,
  ArrowRight,
  X,
  Check,
  AlertCircle
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { VisitDuringTab } from './VisitDuringTab';
import { VisitAfterTab } from './VisitAfterTab';

export function VisitProcess() {
  const navigate = useNavigate();
  const location = useLocation();
  const { id } = useParams();

  const userStr = localStorage.getItem("currentUser");
  const currentUser = userStr ? JSON.parse(userStr) : null;
  const isHO = currentUser?.role?.toUpperCase() === 'HO';
  const isDept = currentUser?.role?.toUpperCase() === 'DEPT' || currentUser?.role?.toUpperCase() === 'STUDENT' || currentUser?.role?.toUpperCase() === 'VISITOR';
  const isStudent = currentUser?.role?.toUpperCase() === 'STUDENT';
  const isVisitor = currentUser?.role?.toUpperCase() === 'VISITOR';

  const [currentStatus, setCurrentStatus] = useState(() => {
    if (location.state?.status) return location.state.status;
    if (location.state?.isPrep) return 'Đang chuẩn bị';
    return (id === '1' ? 'Đang chuẩn bị' : 
            id === '4' ? 'Trong tiếp khách' : 
            (id === '2' || id === '5') ? 'Chờ đóng đoàn' : 'Trong tiếp khách');
  });

  const isReceptionDetail = window.location.pathname.includes('/reception-detail');
  const isReadOnlyRoute = location.state?.isReadOnly || isReceptionDetail || false;
  const isClosed = currentStatus === 'Đã đóng đoàn' || currentStatus === 'Đã kết thúc' || isReadOnlyRoute;

  const renderEmptyState = () => (
    <div className="bg-white rounded-[2rem] border border-gray-200 p-16 text-center shadow-sm flex flex-col items-center justify-center min-h-[350px] animate-in fade-in duration-300">
      <div className="w-20 h-20 bg-slate-100 rounded-full flex items-center justify-center mb-6">
        <Clock className="w-10 h-10 text-slate-400 stroke-[1.5]" />
      </div>
      <h2 className="text-xl font-bold text-slate-800 mb-2 font-sans tracking-tight">Chưa đến giai đoạn này</h2>
      <p className="text-gray-500 font-medium max-w-sm mx-auto leading-relaxed text-sm">
        Giai đoạn này sẽ được mở khóa sau khi hoàn tất các bước trước đó trong quy trình tiếp khách.
      </p>
    </div>
  );

  const [activeTab, setActiveTab] = useState(isReceptionDetail ? 'before' : (location.state?.defaultTab || 'before'));
  const isPrep = (currentStatus === 'Đang chuẩn bị' || currentStatus === 'Trước tiếp khách') && !isClosed;
  const [isInfoExpanded, setIsInfoExpanded] = useState(false);
  const [isSetupExpanded, setIsSetupExpanded] = useState(!isVisitor);
  const [isAlbumExpanded, setIsAlbumExpanded] = useState(false);
  const [isNewsExpanded, setIsNewsExpanded] = useState(false);
  
  const [isSection1Expanded, setIsSection1Expanded] = useState(true);
  const [isSection2Expanded, setIsSection2Expanded] = useState(true);
  const [isSection3Expanded, setIsSection3Expanded] = useState(true);
  const [isSection4Expanded, setIsSection4Expanded] = useState(true);

  const [isInfoSection1Expanded, setIsInfoSection1Expanded] = useState(false);
  const [isInfoSection2Expanded, setIsInfoSection2Expanded] = useState(false);
  const [isInfoSection3Expanded, setIsInfoSection3Expanded] = useState(false);
  
  const [isInfoEditableState, setIsInfoEditable] = useState(false);
  const [isSetupEditableState, setIsSetupEditable] = useState(true);
  const [isSetupConfirmed, setIsSetupConfirmed] = useState(false);
  const isInfoEditable = isInfoEditableState && !isClosed && !isDept;
  const isSetupEditable = isSetupEditableState && !isClosed && !isDept;

  // States for forms
  const [needLED, setNeedLED] = useState(true);
  const [sentImage, setSentImage] = useState(false);

  const [tourGuide, setTourGuide] = useState<'me' | 'other' | null>(null);
  
  const [selectedElectricCarDept, setSelectedElectricCarDept] = useState('');
  const [selectedDriverDept, setSelectedDriverDept] = useState('');
  const [selectedRoomDept, setSelectedRoomDept] = useState('');
  const [selectedTeabreakDept, setSelectedTeabreakDept] = useState('');
  const [selectedOtherDept, setSelectedOtherDept] = useState('');

  const [selectedTourGuide, setSelectedTourGuide] = useState('');
  const [addedTourGuides, setAddedTourGuides] = useState<string[]>([]);
  const [sentRequests, setSentRequests] = useState<Record<string, boolean>>({});

  const handleSendRequest = (type: string) => {
    setSentRequests(prev => ({ ...prev, [type]: true }));
  };

  const [participants, setParticipants] = useState({
    isMeHost: true,
    host: currentUser || null,
    supporters: [] as any[],
    otherDepts: [] as any[],
    students: [] as any[]
  });

  const [selectedHostOption, setSelectedHostOption] = useState('');
  const [addedHost, setAddedHost] = useState<string | null>('Trần Thị IC');
  const [showNoAccountError, setShowNoAccountError] = useState(false);

  const [selectedSupporterOption, setSelectedSupporterOption] = useState('');
  const [addedSupporters, setAddedSupporters] = useState<string[]>(['Thêm người A']);
  const [showSupporterNoAccountError, setShowSupporterNoAccountError] = useState(false);

  const [selectedOtherDeptOption, setSelectedOtherDeptOption] = useState('');
  const [participantOtherDept, setParticipantOtherDept] = useState('');
  const [addedOtherDepts, setAddedOtherDepts] = useState<string[]>(['Nguyễn Có TK']);
  const [showOtherDeptNoAccountError, setShowOtherDeptNoAccountError] = useState(false);

  const [studentSearchText, setStudentSearchText] = useState('');
  const [addedStudents, setAddedStudents] = useState<string[]>(['Sinh viên 123 - Trịnh Thăng Bình']);
  const [showStudentNoAccountError, setShowStudentNoAccountError] = useState(false);

  const [alerts, setAlerts] = useState({
    system: { days: 1, time: '09:00', period: 'AM' },
    email: { days: 2, time: '14:00', period: 'PM' }
  });

  const [setupDetails, setSetupDetails] = useState({
    electricCar: { borrowerName: '', quantity: '', date: '', startTime: '', endTime: '', note: '', confirmed: false, collapsed: false },
    driver: { borrowerName: '', quantity: '', date: '', startTime: '', endTime: '', note: '', confirmed: false, collapsed: false },
    room: { borrowerName: '', quantity: '', date: '', startTime: '', endTime: '', note: '', confirmed: false, collapsed: false },
    teabreak: { borrowerName: '', quantity: '', date: '', startTime: '', endTime: '', note: '', confirmed: false, collapsed: false }
  });

  const getLeaderName = (dept: string) => {
    if (!dept) return '';
    if (dept === "Tuyển sinh") {
      return "Nguyễn Văn Tuấn";
    } else if (dept === 'Hành chính') {
      return "Trần Thị Bích";
    } else if (dept === 'Các bộ môn liên quan') {
      return "Lê Văn Cường";
    }
    return "Leader " + dept;
  };

  const campusOptions = ['Hà Nội', 'Đà Nẵng', 'Cần Thơ', 'Hồ Chí Minh', 'Quy Nhơn'];
  const [visitMode, setVisitMode] = useState<'single' | 'multiple'>('single');
  const [visits, setVisits] = useState([
    { id: '1', campus: isHO ? 'Hà Nội' : 'Hà Nội', date: '2023-10-20', startTime: '08:00', endTime: '16:30' }
  ]);

  useEffect(() => {
    if (visitMode === 'single' && visits.length > 1) {
      setVisits([visits[0]]);
    }
  }, [visitMode]);

  const formatDetailSummary = (type: 'electricCar' | 'driver' | 'room' | 'teabreak', detail: any) => {
    const nameStr = detail.borrowerName ? `Người mượn: ${detail.borrowerName}` : 'Người mượn: Chưa nhập';
    let qtyStr = '';
    if (type === 'room') {
      qtyStr = detail.quantity ? `Phòng: ${detail.quantity}` : 'Phòng: Chưa nhập';
    } else if (type === 'teabreak') {
      qtyStr = detail.quantity ? `Số lượng suất: ${detail.quantity}` : 'Số lượng suất: 0';
    } else {
      qtyStr = detail.quantity ? `Số lượng: ${detail.quantity}` : 'Số lượng: 0';
    }

    let dateStr = 'Chưa chọn ngày';
    if (detail.date) {
      const parts = detail.date.split('-');
      if (parts.length === 3) {
        dateStr = `${parts[2]}/${parts[1]}/${parts[0]}`;
      } else {
        dateStr = detail.date;
      }
    }

    const start = detail.startTime || '--:--';
    const end = detail.endTime || '--:--';
    const timeStr = `${dateStr} từ ${start} - ${end}`;

    return { nameStr, qtyStr, timeStr };
  };

  const renderLeaderInfo = (dept: string, type: string) => {
    if (!dept) return null;
    let name = "Leader " + dept;
    if (dept === "Tuyển sinh") {
      name = "Nguyễn Văn Tuấn";
    } else if (dept === 'Hành chính') {
      name = "Trần Thị Bích";
    } else if (dept === 'Các bộ môn liên quan') {
      name = "Lê Văn Cường";
    }

    const isSent = sentRequests[type];

    return (
      <div className="mt-3 p-3 bg-yellow-50/80 border border-yellow-200 rounded-xl flex flex-col sm:flex-row sm:items-center gap-3 animate-in fade-in slide-in-from-top-2">
        <div className="w-10 h-10 rounded-full bg-yellow-500 text-white flex items-center justify-center font-bold text-lg">
          {name.charAt(0)}
        </div>
        <div className="flex-1">
          <div className="text-sm font-bold text-yellow-900 flex items-center gap-2 flex-wrap">
            {name} 
            <span className="text-[11px] font-bold uppercase tracking-wider text-yellow-700 bg-white px-2 py-0.5 rounded-md border border-yellow-200 shadow-sm">Trưởng phòng</span>
          </div>
          {isSent && (
            <div className="text-[13px] font-bold text-[#10b981] flex items-center gap-1 mt-1 animate-in fade-in slide-in-from-bottom-1"><CheckCircle2 className="w-3.5 h-3.5" /> Đã gửi yêu cầu tới "{name}"</div>
          )}
        </div>
        {!isSent ? (
          <button 
            type="button"
            onClick={() => handleSendRequest(type)}
            className="ml-auto px-4 py-2 bg-yellow-100 text-yellow-800 text-xs font-bold rounded-xl hover:bg-yellow-200 transition-all active:scale-[0.98] outline-none shadow-sm flex items-center gap-1"
          >
            Gửi yêu cầu
          </button>
        ) : (
          <button
            type="button"
            onClick={() => navigate(`/dashboard/visit/process/${id}/request/${type}`)}
            className="ml-auto px-4 py-2 bg-[#10b981]/10 text-[#10b981] text-xs font-bold rounded-xl hover:bg-[#10b981]/20 transition-all active:scale-[0.98] outline-none shadow-sm flex items-center gap-1 border border-[#10b981]/20"
          >
            Xem chi tiết <ArrowRight className="w-3.5 h-3.5" />
          </button>
        )}
      </div>
    );
  };

  const [rejectReasonModal, setRejectReasonModal] = useState<{ isOpen: boolean, targetId: string | null, targetName: string | null, reasonText: string }>({ isOpen: false, targetId: null, targetName: null, reasonText: '' });
  const [viewReasonModal, setViewReasonModal] = useState<{isOpen: boolean, targetName: string | null, reasonText: string}>({isOpen: false, targetName: null, reasonText: ''});

  const [confirmations, setConfirmations] = useState<Record<string, { time: string, name: string, status: 'accepted' | 'rejected', reason?: string }>>({});
  
  const setConfirmStatus = (id: string, name: string, status: 'accepted' | 'rejected') => {
    if (status === 'rejected') {
      if (confirmations[id]?.status === 'rejected') {
        setConfirmations(prev => {
          const next = { ...prev };
          delete next[id];
          return next;
        });
        return;
      }
      setRejectReasonModal({ isOpen: true, targetId: id, targetName: name, reasonText: '' });
      return;
    }
    setConfirmations(prev => {
      if (prev[id] && prev[id].status === status) {
        const next = { ...prev };
        delete next[id];
        return next;
      }
      return {
        ...prev,
        [id]: { time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }), name, status }
      };
    });
  };

  const handleConfirmReject = () => {
    if (rejectReasonModal.targetId && rejectReasonModal.targetName) {
      setConfirmations(prev => ({
        ...prev,
        [rejectReasonModal.targetId!]: { 
          time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }), 
          name: rejectReasonModal.targetName!, 
          status: 'rejected',
          reason: rejectReasonModal.reasonText
        }
      }));
    }
    setRejectReasonModal({ isOpen: false, targetId: null, targetName: null, reasonText: '' });
  };

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-24">
      <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <span className="cursor-pointer hover:text-[#004c91] transition-colors" onClick={() => navigate('/dashboard/visit')}>Quản lý tiếp khách</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">
          {isReceptionDetail ? 'Chi tiết đón tiếp' : 'Quy trình tiếp khách'}
        </span>
      </div>

      <div className="mb-8">
        <h1 className="text-3xl font-bold text-[#004c91]">
          {isReceptionDetail ? 'Chi tiết đón tiếp' : 'Quy trình tiếp khách'}
        </h1>
        <p className="text-gray-500 mt-1 font-medium">
          {isReceptionDetail 
            ? 'Thông tin chi tiết chuẩn bị đón tiếp đoàn khách (Trước tiếp khách)' 
            : 'Quản lý các bước chuẩn bị, đón tiếp và sau khi tiếp khách'
          }
        </p>
      </div>

      {isClosed && !isReceptionDetail && (
        <div className="mb-8 bg-slate-100 border-l-4 border-slate-500 p-5 rounded-2xl flex items-center gap-3 text-left shadow-sm">
          <AlertCircle className="w-5 h-5 text-slate-600 shrink-0" />
          <p className="text-sm font-bold text-slate-700">
            {(currentStatus === 'Đã đóng đoàn' || currentStatus === 'Đã kết thúc')
              ? 'Hồ sơ lưu trữ: Đoàn khách này đã hoàn thành quy trình tiếp đón và đóng hồ sơ lịch sử. Dữ liệu đang hiển thị ở chế độ xem (Chỉ đọc).'
              : 'Chỉ có HOST mới có thể chỉnh sửa thông tin'
            }
          </p>
        </div>
      )}

      {/* Tabs */}
      {!isReceptionDetail && (
        <div className="flex bg-white rounded-2xl p-1.5 shadow-sm border border-gray-200 mb-8 max-w-2xl">
          <button
            onClick={() => setActiveTab('before')}
            className={`flex-1 py-3 text-sm font-bold rounded-xl transition-all outline-none ${activeTab === 'before' ? 'bg-[#004c91] text-white shadow-md' : 'text-gray-500 hover:bg-gray-50 hover:text-gray-700'}`}
          >
            1. Trước tiếp khách
          </button>
          <button
            onClick={() => setActiveTab('during')}
            className={`flex-1 py-3 text-sm font-bold rounded-xl transition-all outline-none ${activeTab === 'during' ? 'bg-[#f37021] text-white shadow-md' : 'text-gray-500 hover:bg-gray-50 hover:text-gray-700'}`}
          >
            2. Đang tiếp khách
          </button>
          <button
            onClick={() => setActiveTab('after')}
            className={`flex-1 py-3 text-sm font-bold rounded-xl transition-all outline-none ${activeTab === 'after' ? 'bg-[#00a651] text-white shadow-md' : 'text-gray-500 hover:bg-gray-50 hover:text-gray-700'}`}
          >
            3. Sau tiếp khách
          </button>
        </div>
      )}

      {activeTab === 'before' && (
        <div className="space-y-6">
          {/* Phần 1: Thông tin chung */}
          <div className="bg-white rounded-[2rem] border border-gray-200 shadow-sm overflow-hidden transition-all duration-300">
            <div 
              className="px-8 py-6 flex items-center justify-between cursor-pointer transition-colors bg-[#f37021]"
              onClick={() => setIsInfoExpanded(!isInfoExpanded)}
            >
              <div>
                <h2 className="text-xl font-bold text-white border-l-4 border-white pl-3">1. Thông tin chung</h2>
                <p className="text-sm font-medium text-orange-100 mt-1 pl-4">Thông tin đoàn khách, thành phần tham dự và setup</p>
              </div>
              <div className="flex items-center gap-3">
                {!isInfoEditable && !isClosed && !isDept && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      setIsInfoEditable(true);
                      setIsInfoExpanded(true);
                    }}
                    className="w-10 h-10 rounded-full hover:bg-white/20 flex items-center justify-center text-white transition-colors"
                  >
                    <Edit3 className="w-5 h-5" />
                  </button>
                )}
                <div className="w-10 h-10 rounded-full bg-white/20 flex items-center justify-center text-white">
                  {isInfoExpanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                </div>
              </div>
            </div>

            <AnimatePresence>
              {isInfoExpanded && (
                <motion.div
                  initial={{ height: 0, opacity: 0 }}
                  animate={{ height: 'auto', opacity: 1 }}
                  exit={{ height: 0, opacity: 0 }}
                  className="border-t border-gray-100 overflow-hidden"
                >
                  <div className={`p-8 space-y-8 ${!isInfoEditable ? 'bg-slate-50/50 opacity-90' : 'bg-white'}`}>
                    {/* Section 1: Thông tin người tạo */}
                    <div className="bg-white rounded-2xl border border-[#004c91]/20 shadow-sm overflow-hidden">
                      <div 
                        className="bg-[#004c91] px-6 py-4 flex items-center justify-between cursor-pointer"
                        onClick={() => setIsInfoSection1Expanded(!isInfoSection1Expanded)}
                      >
                        <h2 className="text-lg font-bold text-white flex items-center gap-2">
                          <span className="flex items-center justify-center w-6 h-6 rounded-full bg-[#f37021] text-white font-black text-sm">1</span>
                          Thông tin người tạo
                        </h2>
                        <div className="w-8 h-8 rounded-full bg-white/10 flex items-center justify-center text-white">
                          {isInfoSection1Expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                        </div>
                      </div>
                      <AnimatePresence>
                        {isInfoSection1Expanded && (
                          <motion.div
                            initial={{ height: 0, opacity: 0 }}
                            animate={{ height: 'auto', opacity: 1 }}
                            exit={{ height: 0, opacity: 0 }}
                          >
                            <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-6 bg-white">
                              <div>
                                <label className="block text-sm font-bold text-gray-700 mb-2">Họ và tên</label>
                                <input type="text" readOnly={!isInfoEditable} className={`w-full px-4 py-2.5 rounded-xl border border-gray-200 text-gray-800 font-medium text-sm outline-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all'}`} defaultValue="Nguyễn Văn Tạo" />
                              </div>
                              <div>
                                <label className="block text-sm font-bold text-gray-700 mb-2">Đơn vị công tác</label>
                                <input type="text" readOnly={!isInfoEditable} className={`w-full px-4 py-2.5 rounded-xl border border-gray-200 text-gray-800 font-medium text-sm outline-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all'}`} defaultValue="Đại học FPT" />
                              </div>
                              <div>
                                <label className="block text-sm font-bold text-gray-700 mb-2">Chức danh, phòng ban</label>
                                <input type="text" readOnly={!isInfoEditable} className={`w-full px-4 py-2.5 rounded-xl border border-gray-200 text-gray-800 font-medium text-sm outline-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all'}`} defaultValue="Cán bộ phòng IC" />
                              </div>
                              <div>
                                <label className="block text-sm font-bold text-gray-700 mb-2">Số điện thoại</label>
                                <input type="text" readOnly={!isInfoEditable} className={`w-full px-4 py-2.5 rounded-xl border border-gray-200 text-gray-800 font-medium text-sm outline-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all'}`} defaultValue="0987654321" />
                              </div>
                              <div className="md:col-span-2">
                                <label className="block text-sm font-bold text-gray-700 mb-2">Email</label>
                                <input type="email" readOnly={!isInfoEditable} className={`w-full px-4 py-2.5 rounded-xl border border-gray-200 text-gray-800 font-medium text-sm outline-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all'}`} defaultValue="taonv@fe.edu.vn" />
                              </div>
                            </div>
                          </motion.div>
                        )}
                      </AnimatePresence>
                    </div>

                    {/* Section 2: Thông tin đoàn khách */}
                    <div className="bg-white rounded-2xl border border-[#004c91]/20 shadow-sm overflow-hidden">
                      <div 
                        className="bg-[#004c91] px-6 py-4 flex items-center justify-between cursor-pointer"
                        onClick={() => setIsInfoSection2Expanded(!isInfoSection2Expanded)}
                      >
                        <h2 className="text-lg font-bold text-white flex items-center gap-2">
                          <span className="flex items-center justify-center w-6 h-6 rounded-full bg-[#f37021] text-white font-black text-sm">2</span>
                          Thông tin đoàn khách
                        </h2>
                        <div className="w-8 h-8 rounded-full bg-white/10 flex items-center justify-center text-white">
                          {isInfoSection2Expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                        </div>
                      </div>
                      <AnimatePresence>
                        {isInfoSection2Expanded && (
                          <motion.div
                            initial={{ height: 0, opacity: 0 }}
                            animate={{ height: 'auto', opacity: 1 }}
                            exit={{ height: 0, opacity: 0 }}
                          >
                            <div className="p-6 space-y-6 bg-white border-t border-gray-100">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                          <div>
                            <label className="block text-sm font-bold text-gray-700 mb-2">Tên đoàn khách</label>
                            <input type="text" readOnly={!isInfoEditable} className={`w-full px-4 py-2.5 rounded-xl border border-gray-200 text-gray-800 font-medium text-sm outline-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all'}`} defaultValue="Đoàn đối tác từ Đại học Tokyo, Nhật Bản" />
                          </div>
                          <div className="relative">
                            <label className="block text-sm font-bold text-gray-700 mb-2">Cơ sở tới thăm</label>
                            <div className="relative">
                              <select
                                disabled={!isInfoEditable}
                                value={visitMode}
                                onChange={(e) => setVisitMode(e.target.value as 'single' | 'multiple')}
                                className={`w-full px-4 py-2.5 rounded-xl border border-gray-200 text-sm font-medium outline-none appearance-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90 text-gray-800' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all text-gray-900'}`}
                              >
                                <option value="single">Chỉ một cơ sở</option>
                                <option value="multiple">Liên cơ sở</option>
                              </select>
                              <ChevronDown className="w-4 h-4 text-gray-500 absolute right-4 top-1/2 -translate-y-1/2 pointer-events-none" />
                            </div>
                          </div>
                        </div>

                        <div className="bg-orange-50/50 p-5 rounded-xl border border-orange-100 relative">
                          <label className="block text-sm font-bold text-gray-800 mb-4">Thời gian dự kiến</label>
                          <div className="space-y-4 mt-2">
                            {visits.map((visit, index) => (
                              <div key={visit.id} className="flex flex-col xl:flex-row items-end gap-3 w-full animate-in fade-in slide-in-from-top-2 duration-300 pb-4 border-b border-gray-100 last:border-b-0 last:pb-0 relative">
                                {visitMode === 'multiple' && visits.length > 1 && isInfoEditable && (
                                  <button
                                    type="button"
                                    onClick={() => setVisits(visits.filter(v => v.id !== visit.id))}
                                    className="absolute -right-2 -top-2 w-6 h-6 bg-red-50 text-red-500 rounded-full flex items-center justify-center hover:bg-red-500 hover:text-white transition-colors"
                                  >
                                    <X className="w-3 h-3" />
                                  </button>
                                )}
                                {/* Chọn Cơ sở */}
                                <div className="flex-[1.2] w-full xl:w-auto relative">
                                  {index === 0 && <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wider">Cơ sở</label>}
                                  <div className="relative">
                                    <select
                                      disabled={!isInfoEditable}
                                      value={visit.campus}
                                      onChange={(e) => {
                                        const newVisits = [...visits];
                                        newVisits[index].campus = e.target.value;
                                        setVisits(newVisits);
                                      }}
                                      className={`w-full px-4 py-2.5 rounded-xl border border-gray-200 text-sm font-medium outline-none appearance-none pr-8 ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90 text-gray-800' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all text-gray-900'}`}
                                    >
                                      {campusOptions.map(c => <option key={c} value={c}>{c}</option>)}
                                    </select>
                                    <ChevronDown className="absolute right-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
                                  </div>
                                </div>
                                {/* Ngày bắt đầu */}
                                <div className="flex-[1.5] w-full xl:w-auto relative">
                                  {index === 0 && <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wider">Ngày bắt đầu</label>}
                                  <div className="relative">
                                    <input type="date" disabled={!isInfoEditable} value={visit.date} onChange={(e) => {
                                      const newVisits = [...visits];
                                      newVisits[index].date = e.target.value;
                                      setVisits(newVisits);
                                    }} className={`w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-200 text-sm font-medium outline-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90 text-gray-800' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all text-gray-900'}`} required />
                                    <Calendar className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91]" />
                                  </div>
                                </div>
                                {/* Thời gian bắt đầu */}
                                <div className="flex-1 w-full xl:w-auto relative">
                                  {index === 0 && <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wider">Thời gian bắt đầu</label>}
                                  <div className="relative">
                                    <input type="time" disabled={!isInfoEditable} value={visit.startTime} onChange={(e) => {
                                      const newV = [...visits]; newV[index].startTime = e.target.value; setVisits(newV);
                                    }} className={`w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-200 text-sm font-medium outline-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90 text-gray-800' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all text-gray-900'}`} required />
                                    <Clock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91]" />
                                  </div>
                                </div>
                                {/* Thời gian kết thúc */}
                                <div className="flex-1 w-full xl:w-auto relative">
                                  {index === 0 && <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wider">Thời gian kết thúc</label>}
                                  <div className="relative">
                                    <input type="time" disabled={!isInfoEditable} value={visit.endTime} onChange={(e) => {
                                      const newV = [...visits]; newV[index].endTime = e.target.value; setVisits(newV);
                                    }} className={`w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-200 text-sm font-medium outline-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90 text-gray-800' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all text-gray-900'}`} required />
                                    <Clock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91]" />
                                  </div>
                                </div>
                                {/* Giờ Việt Nam */}
                                <div className="flex-[0.8] w-full xl:w-auto flex items-center justify-center h-[44px] px-3 bg-white rounded-xl border border-gray-200 select-none cursor-default">
                                  <span className="text-[#004c91] text-sm font-bold whitespace-nowrap">VN (GMT+7)</span>
                                </div>
                              </div>
                            ))}
                          </div>

                          {visitMode === 'multiple' && isInfoEditable && (
                            <button
                              type="button"
                              onClick={() => setVisits([...visits, { id: Date.now().toString(), campus: 'Hà Nội', date: '', startTime: '', endTime: '' }])}
                              className="w-full mt-4 flex items-center justify-center gap-2 py-2.5 border-2 border-dashed border-[#f37021]/30 hover:border-[#f37021] text-[#f37021] rounded-xl text-sm font-bold transition-colors bg-white hover:bg-orange-50"
                            >
                              <Plus className="w-4 h-4" /> Thêm cơ sở
                            </button>
                          )}
                        </div>

                        <div className="space-y-6">
                          <div>
                            <label className="block text-sm font-bold text-gray-700 mb-2">Mục đích thăm</label>
                            <textarea readOnly={!isInfoEditable} className={`w-full px-4 py-3 rounded-xl border border-gray-200 text-gray-800 font-medium text-sm min-h-[80px] outline-none resize-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all'}`} defaultValue="Giao lưu và tham quan cơ sở vật chất, ký kết hợp tác đào tạo."></textarea>
                          </div>
                          <div>
                            <label className="block text-sm font-bold text-gray-700 mb-2">Nội dung làm việc</label>
                            <textarea readOnly={!isInfoEditable} className={`w-full px-4 py-3 rounded-xl border border-gray-200 text-gray-800 font-medium text-sm min-h-[80px] outline-none resize-none ${!isInfoEditable ? 'bg-gray-50/50 cursor-not-allowed opacity-90' : 'bg-white hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all'}`} defaultValue="Meeting trao đổi về chương trình hợp tác, Campus Tour, và dùng cơm trưa, teabreak."></textarea>
                          </div>
                        </div>
                            </div>
                          </motion.div>
                        )}
                      </AnimatePresence>
                    </div>

                    {/* Section 3: Setup */}
                    <div className="bg-white rounded-2xl border border-[#004c91]/20 shadow-sm overflow-hidden mb-8">
                      <div 
                        className="bg-[#004c91] px-6 py-4 flex items-center justify-between cursor-pointer"
                        onClick={() => setIsInfoSection3Expanded(!isInfoSection3Expanded)}
                      >
                        <h2 className="text-lg font-bold text-white flex items-center gap-2">
                          <span className="flex items-center justify-center w-6 h-6 rounded-full bg-[#f37021] text-white font-black text-sm">3</span>
                          Thiết lập & Điều phối sự kiện (Set up)
                        </h2>
                        <div className="w-8 h-8 rounded-full bg-white/10 flex items-center justify-center text-white">
                          {isInfoSection3Expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                        </div>
                      </div>
                      <AnimatePresence>
                        {isInfoSection3Expanded && (
                          <motion.div
                            initial={{ height: 0, opacity: 0 }}
                            animate={{ height: 'auto', opacity: 1 }}
                            exit={{ height: 0, opacity: 0 }}
                          >
                            <div className="p-0 bg-white border-t border-gray-100">
                        {/* 3.1 Loại hình tham quan */}
                        <div className="p-6 border-b border-gray-100">
                          <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-4">
                            <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                            1. Loại hình tham quan
                          </h3>
                          <div className="flex flex-wrap gap-4">
                            <label className="flex items-center gap-2">
                              <input type="checkbox" disabled={!isInfoEditable} defaultChecked className="w-5 h-5 rounded border-gray-300 text-[#004c91]" />
                              <span className="text-sm font-medium text-gray-700">Campus tour</span>
                            </label>
                            <label className="flex items-center gap-2">
                              <input type="checkbox" disabled={!isInfoEditable} defaultChecked className="w-5 h-5 rounded border-gray-300 text-[#004c91]" />
                              <span className="text-sm font-medium text-gray-700">Họp trao đổi</span>
                            </label>
                            <label className="flex items-center gap-2">
                              <input type="checkbox" disabled={!isInfoEditable} defaultChecked={false} className="w-5 h-5 rounded border-gray-300 text-[#004c91]" />
                              <span className="text-sm font-medium text-gray-700">Khác</span>
                            </label>
                          </div>
                        </div>

                        {/* 3.2 Agenda */}
                        <div className="p-6 border-b border-gray-100 bg-slate-50/50">
                          <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-4">
                            <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                            2. Agenda
                          </h3>
                          <div className="space-y-3">
                            <div className="flex flex-col md:flex-row items-end gap-3">
                              <div className="flex items-center gap-3 w-full md:w-auto shrink-0">
                                <div className="flex flex-col">
                                  <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Thời gian bắt đầu</label>
                                  <div className="border border-gray-200 rounded-xl bg-gray-50/50 p-1.5">
                                    <input type="time" readOnly={!isInfoEditable} defaultValue="08:00" className="w-[124px] px-2 py-1.5 outline-none text-sm bg-transparent font-medium" />
                                  </div>
                                </div>
                                <span className="text-gray-400 font-bold mt-5">-</span>
                                <div className="flex flex-col">
                                  <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Thời gian kết thúc</label>
                                  <div className="border border-gray-200 rounded-xl bg-gray-50/50 p-1.5">
                                    <input type="time" readOnly={!isInfoEditable} defaultValue="09:00" className="w-[124px] px-2 py-1.5 outline-none text-sm bg-transparent font-medium" />
                                  </div>
                                </div>
                              </div>
                              <div className="flex-1 w-full flex flex-col">
                                <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Nội dung</label>
                                <div className="flex items-center gap-3">
                                  <input type="text" readOnly={!isInfoEditable} defaultValue="Đón khách" className="flex-1 w-full px-4 py-3 rounded-xl border border-gray-200 bg-gray-50/50 text-gray-800 font-medium text-sm shadow-sm opacity-90" />
                                </div>
                              </div>
                            </div>
                            <div className="flex flex-col md:flex-row items-end gap-3">
                              <div className="flex items-center gap-3 w-full md:w-auto shrink-0">
                                <div className="flex flex-col">
                                  <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Thời gian bắt đầu</label>
                                  <div className="border border-gray-200 rounded-xl bg-gray-50/50 p-1.5">
                                    <input type="time" readOnly={!isInfoEditable} defaultValue="09:00" className="w-[124px] px-2 py-1.5 outline-none text-sm bg-transparent font-medium" />
                                  </div>
                                </div>
                                <span className="text-gray-400 font-bold mt-5">-</span>
                                <div className="flex flex-col">
                                  <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Thời gian kết thúc</label>
                                  <div className="border border-gray-200 rounded-xl bg-gray-50/50 p-1.5">
                                    <input type="time" readOnly={!isInfoEditable} defaultValue="11:00" className="w-[124px] px-2 py-1.5 outline-none text-sm bg-transparent font-medium" />
                                  </div>
                                </div>
                              </div>
                              <div className="flex-1 w-full flex flex-col">
                                <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Nội dung</label>
                                <div className="flex items-center gap-3">
                                  <input type="text" readOnly={!isInfoEditable} defaultValue="Meeting trao đổi hợp tác" className="flex-1 w-full px-4 py-3 rounded-xl border border-gray-200 bg-gray-50/50 text-gray-800 font-medium text-sm shadow-sm" />
                                </div>
                              </div>
                            </div>
                            <div className="flex flex-col md:flex-row items-end gap-3">
                              <div className="flex items-center gap-3 w-full md:w-auto shrink-0">
                                <div className="flex flex-col">
                                  <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Thời gian bắt đầu</label>
                                  <div className="border border-gray-200 rounded-xl bg-gray-50/50 p-1.5">
                                    <input type="time" readOnly={!isInfoEditable} defaultValue="11:00" className="w-[124px] px-2 py-1.5 outline-none text-sm bg-transparent font-medium" />
                                  </div>
                                </div>
                                <span className="text-gray-400 font-bold mt-5">-</span>
                                <div className="flex flex-col">
                                  <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Thời gian kết thúc</label>
                                  <div className="border border-gray-200 rounded-xl bg-gray-50/50 p-1.5">
                                    <input type="time" readOnly={!isInfoEditable} defaultValue="12:00" className="w-[124px] px-2 py-1.5 outline-none text-sm bg-transparent font-medium" />
                                  </div>
                                </div>
                              </div>
                              <div className="flex-1 w-full flex flex-col">
                                <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Nội dung</label>
                                <div className="flex items-center gap-3">
                                  <input type="text" readOnly={!isInfoEditable} defaultValue="Campus tour" className="flex-1 w-full px-4 py-3 rounded-xl border border-gray-200 bg-gray-50/50 text-gray-800 font-medium text-sm shadow-sm" />
                                </div>
                              </div>
                            </div>
                          </div>
                        </div>

                        {/* 3.3 Thành phần tham gia */}
            <div className="p-6 border-b border-gray-100">
              <h3 className={`text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-6`}>
                <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                3. Thành phần tham gia
              </h3>
              
              <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
                {/* 1. Host */}
                <div className={`bg-white border border-gray-200 border-l-[6px] border-l-[#004c91] rounded-xl p-5 shadow-sm bg-gradient-to-r from-[#004c91]/[0.03] to-transparent`}>
                  <div className="flex items-center justify-between mb-4">
                    <h4 className="font-bold text-[#004c91] text-base flex items-center gap-2">
                      <UserX className="w-5 h-5" /> 1. Host (Bắt buộc)
                    </h4>
                    <label className={`flex items-center gap-2 cursor-pointer bg-blue-50 px-3 py-1.5 rounded-lg border border-blue-100`}>
                      <input disabled={!isInfoEditable} type="checkbox" checked={participants.isMeHost} onChange={e => setParticipants({...participants, isMeHost: e.target.checked})} className="w-4 h-4 rounded border-gray-300 text-[#004c91]" />
                      <span className="text-xs font-bold text-[#004c91]">Là tôi</span>
                    </label>
                  </div>
                  {!participants.isMeHost && (
                    <div>
                      {addedHost && !showNoAccountError && (
                        <div className={`flex items-center p-3 bg-blue-50/80 rounded-xl border border-blue-200 mt-2 shadow-sm animate-in fade-in slide-in-from-top-2 ${isInfoEditable ? 'mb-4' : ''}`}>
                          <div className="flex items-center gap-3 flex-1">
                            <div className="w-8 h-8 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs ring-2 ring-blue-100">
                              {addedHost.charAt(0)}
                            </div>
                            <div>
                              <div className="text-sm font-bold text-[#004c91]">{addedHost}</div>
                              <div className="text-[11px] text-blue-600 font-bold flex items-center gap-1 mt-0.5 uppercase tracking-wide">
                                <CheckCircle className="w-3.5 h-3.5" /> Đã thêm
                              </div>
                            </div>
                          </div>
                          
                          <div className="ml-auto flex items-center gap-3">
                            <div className="flex flex-col items-end">
                              <div className="flex items-center gap-2">
                                {confirmations[`host-${addedHost}`] && (
                                   <span className={`text-xs font-bold ${confirmations[`host-${addedHost}`].status === 'accepted' ? 'text-emerald-600' : 'text-red-600'}`}>
                                     {confirmations[`host-${addedHost}`].name} {confirmations[`host-${addedHost}`].status === 'accepted' ? 'đồng ý' : 'từ chối'} lúc {confirmations[`host-${addedHost}`].time}
                                   </span>
                                )}
                                <div className="flex items-center gap-2">
                                  <button 
                                    onClick={() => setConfirmStatus(`host-${addedHost}`, addedHost, 'accepted')} 
                                    className={`p-1.5 rounded-lg border transition-all ${confirmations[`host-${addedHost}`]?.status === 'accepted' ? 'bg-emerald-600 text-white border-emerald-600 shadow-sm font-bold scale-102' : 'bg-emerald-50 hover:bg-emerald-100/80 text-emerald-700 border-emerald-200 hover:border-emerald-300'}`} 
                                    title="Đồng ý"
                                  >
                                    <Check className="w-4 h-4 text-inherit stroke-[3]" />
                                  </button>
                                  <button 
                                    onClick={() => setConfirmStatus(`host-${addedHost}`, addedHost, 'rejected')} 
                                    className={`p-1.5 rounded-lg border transition-all ${confirmations[`host-${addedHost}`]?.status === 'rejected' ? 'bg-red-600 text-white border-red-600 shadow-sm font-bold scale-102' : 'bg-red-50 hover:bg-red-100/80 text-red-700 border-red-200 hover:border-red-300'}`} 
                                    title="Từ chối"
                                  >
                                    <X className="w-4 h-4 text-inherit stroke-[3]" />
                                  </button>
                                </div>
                              </div>
                              {confirmations[`host-${addedHost}`]?.status === 'rejected' && confirmations[`host-${addedHost}`]?.reason && (
                                <button 
                                  onClick={() => setViewReasonModal({ isOpen: true, targetName: addedHost, reasonText: confirmations[`host-${addedHost}`].reason! })} 
                                  className="text-[11px] text-red-500 hover:text-red-700 underline mt-1 italic font-medium"
                                >
                                  Xem lý do
                                </button>
                              )}
                            </div>

                            {isInfoEditable && (
                              <button
                                onClick={() => setAddedHost(null)}
                                className="p-1.5 text-[#004c91] hover:text-red-500 rounded-lg hover:bg-red-50 shadow-sm border border-blue-100 bg-white transition-colors ml-2"
                              >
                                <X className="w-4 h-4" />
                              </button>
                            )}
                          </div>
                        </div>
                      )}

                      {isInfoEditable && (
                        <>
                          <label className="block text-xs font-medium text-gray-500 mb-1">Chọn người thuộc phòng IC thay thế</label>
                          <div className="flex gap-2">
                            <select disabled={!isInfoEditable} 
                              className="flex-1 px-4 py-2 rounded-lg border border-gray-300 text-sm outline-none focus:border-[#004c91] hover:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all bg-white"
                              value={selectedHostOption}
                              onChange={(e) => setSelectedHostOption(e.target.value)}
                            >
                              <option value="">Chọn tài khoản IC...</option>
                              <option value="Nguyễn Văn IC">Nguyễn Văn IC</option>
                              <option value="Trần Thị IC">Trần Thị IC</option>
                              <option value="Nguyễn Có TK">Nguyễn Có TK</option>
                            </select>
                            <button disabled={!isInfoEditable}
                              onClick={() => {
                                if (selectedHostOption) {
                                  setAddedHost(selectedHostOption);
                                  setShowNoAccountError(false);
                                }
                              }}
                              className="px-4 py-2 bg-blue-50 hover:bg-blue-100 text-[#004c91] rounded-lg text-sm font-bold transition-colors flex items-center gap-1 shrink-0"
                            >
                              <Plus className="w-4 h-4" /> Thêm
                            </button>
                          </div>
                        </>
                      )}
                      
                      {showNoAccountError && (
                        <p className="text-sm text-red-500 mt-2 font-medium">
                          Nguyễn Không TK chưa có tài khoản <ArrowRight className="w-4 h-4 inline mx-1" /> 
                          <span 
                            className="underline cursor-pointer hover:text-red-600 font-bold"
                            onClick={() => isInfoEditable && navigate('/dashboard/accounts?action=create')}
                          >
                            Tạo tài khoản
                          </span>
                        </p>
                      )}
                    </div>
                  )}
                  {participants.isMeHost && currentUser && (
                    <div className={`flex items-center gap-3 p-3 bg-gray-50 rounded-lg border border-gray-100 mt-2`}>
                      <div className="w-8 h-8 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs">
                        {currentUser.name?.charAt(0) || 'M'}
                      </div>
                      <div>
                        <div className="text-sm font-bold text-gray-900">{currentUser.name}</div>
                        <div className="text-xs text-gray-500">{currentUser.email}</div>
                      </div>
                    </div>
                  )}
                </div>

                {/* 2. Người hỗ trợ */}
                <div className={`bg-white border border-gray-300 border-l-[6px] border-l-[#004c91] rounded-xl p-5 shadow-sm hover:shadow-md transition-shadow bg-gradient-to-r from-[#004c91]/[0.03] to-transparent`}>
                  <h4 className="font-bold text-[#004c91] text-base flex items-center gap-2 mb-4">
                    <Users className="w-5 h-5 text-[#004c91]" /> 2. Staff hỗ trợ IC
                  </h4>
                  <div>
                    {addedSupporters.length > 0 && (
                      <div className={`space-y-2 ${isInfoEditable ? 'mb-4' : ''}`}>
                        {addedSupporters.map((supporter, idx) => (
                          <div key={idx} className="flex items-center p-3 bg-blue-50/80 rounded-xl border border-blue-200 shadow-sm animate-in fade-in slide-in-from-top-2">
                            <div className="flex items-center gap-3 flex-1">
                              <div className="w-8 h-8 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs ring-2 ring-blue-100">
                                {supporter.charAt(0)}
                              </div>
                              <div>
                                <div className="text-sm font-bold text-[#004c91]">{supporter}</div>
                                <div className="text-[11px] text-blue-600 font-bold flex items-center gap-1 mt-0.5 uppercase tracking-wide">
                                  <CheckCircle className="w-3.5 h-3.5" /> Đã thêm
                                </div>
                              </div>
                            </div>
                            
                            <div className="ml-auto flex items-center gap-3">
                              <div className="flex flex-col items-end">
                                <div className="flex items-center gap-2">
                                  {confirmations[`supporter-${supporter}`] && (
                                     <span className={`text-xs font-bold ${confirmations[`supporter-${supporter}`].status === 'accepted' ? 'text-emerald-600' : 'text-red-600'}`}>
                                       {confirmations[`supporter-${supporter}`].name} {confirmations[`supporter-${supporter}`].status === 'accepted' ? 'đồng ý' : 'từ chối'} lúc {confirmations[`supporter-${supporter}`].time}
                                     </span>
                                  )}
                                  <div className="flex items-center gap-2">
                                    <button 
                                      onClick={() => setConfirmStatus(`supporter-${supporter}`, supporter, 'accepted')} 
                                      className={`p-1.5 rounded-lg border transition-all ${confirmations[`supporter-${supporter}`]?.status === 'accepted' ? 'bg-emerald-600 text-white border-emerald-600 shadow-sm font-bold scale-102' : 'bg-emerald-50 hover:bg-emerald-100/80 text-emerald-700 border-emerald-200 hover:border-emerald-300'}`} 
                                      title="Đồng ý"
                                    >
                                      <Check className="w-4 h-4 text-inherit stroke-[3]" />
                                    </button>
                                    <button 
                                      onClick={() => setConfirmStatus(`supporter-${supporter}`, supporter, 'rejected')} 
                                      className={`p-1.5 rounded-lg border transition-all ${confirmations[`supporter-${supporter}`]?.status === 'rejected' ? 'bg-red-600 text-white border-red-600 shadow-sm font-bold scale-102' : 'bg-red-50 hover:bg-red-100/80 text-red-700 border-red-200 hover:border-red-300'}`} 
                                      title="Từ chối"
                                    >
                                      <X className="w-4 h-4 text-inherit stroke-[3]" />
                                    </button>
                                  </div>
                                </div>
                                {confirmations[`supporter-${supporter}`]?.status === 'rejected' && confirmations[`supporter-${supporter}`]?.reason && (
                                  <button 
                                    onClick={() => setViewReasonModal({ isOpen: true, targetName: supporter, reasonText: confirmations[`supporter-${supporter}`].reason! })} 
                                    className="text-[11px] text-red-500 hover:text-red-700 underline mt-1 italic font-medium"
                                  >
                                    Xem lý do
                                  </button>
                                )}
                              </div>

                              {isInfoEditable && (
                                <button
                                  onClick={() => setAddedSupporters(addedSupporters.filter(s => s !== supporter))}
                                  className="p-1.5 text-[#004c91] hover:text-red-500 rounded-lg hover:bg-red-50 shadow-sm border border-blue-100 bg-white transition-colors ml-2"
                                >
                                  <X className="w-4 h-4" />
                                </button>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}

                    {isInfoEditable && (
                      <div className="flex gap-2">
                        <select disabled={!isInfoEditable} 
                          className="flex-1 px-4 py-2 rounded-lg border border-gray-300 text-sm outline-none focus:border-[#004c91] hover:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all bg-white"
                          value={selectedSupporterOption}
                          onChange={(e) => setSelectedSupporterOption(e.target.value)}
                        >
                          <option value="">Chọn nhân sự phòng IC...</option>
                          <option value="Thêm người A">Thêm người A</option>
                          <option value="Nguyễn Có TK">Nguyễn Có TK</option>
                        </select>
                        <button disabled={!isInfoEditable}
                          onClick={() => {
                            if (selectedSupporterOption && !addedSupporters.includes(selectedSupporterOption)) {
                              setAddedSupporters([...addedSupporters, selectedSupporterOption]);
                              setShowSupporterNoAccountError(false);
                              setSelectedSupporterOption('');
                            }
                          }}
                          className="px-4 py-2 bg-blue-50 hover:bg-blue-100 text-[#004c91] rounded-lg text-sm font-bold transition-colors flex items-center gap-1 shrink-0"
                        >
                          <Plus className="w-4 h-4" /> Thêm
                        </button>
                      </div>
                    )}

                    {showSupporterNoAccountError && (
                      <p className="text-sm text-red-500 mt-2 font-medium">
                        Nguyễn Không TK chưa có tài khoản <ArrowRight className="w-4 h-4 inline mx-1" /> 
                        <span 
                          className="underline cursor-pointer hover:text-red-600 font-bold"
                          onClick={() => isInfoEditable && navigate('/dashboard/accounts?action=create')}
                        >
                          Tạo tài khoản
                        </span>
                      </p>
                    )}
                  </div>
                </div>

                {/* 3. Người tham gia phòng khác */}
                <div className={`bg-white border border-gray-300 border-l-[6px] border-l-[#004c91] rounded-xl p-5 shadow-sm hover:shadow-md transition-shadow bg-gradient-to-r from-[#004c91]/[0.03] to-transparent`}>
                  <h4 className="font-bold text-[#004c91] text-base flex items-center gap-2 mb-4">
                    <Users className="w-5 h-5 text-[#004c91]" /> 3. Phòng ban hỗ trợ
                  </h4>
                  <div>
                    {addedOtherDepts.length > 0 && (
                      <div className={`space-y-2 ${isInfoEditable ? 'mb-4' : ''}`}>
                        {addedOtherDepts.map((person, idx) => (
                          <div key={idx} className="flex items-center p-3 bg-blue-50/80 rounded-xl border border-blue-200 shadow-sm animate-in fade-in slide-in-from-top-2">
                            <div className="flex items-center gap-3 flex-1">
                              <div className="w-8 h-8 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs ring-2 ring-blue-100">
                                {person.charAt(0)}
                              </div>
                              <div>
                                <div className="text-sm font-bold text-[#004c91]">{person}</div>
                                <div className="text-[11px] text-blue-600 font-bold flex items-center gap-1 mt-0.5 uppercase tracking-wide">
                                  <CheckCircle className="w-3.5 h-3.5" /> Đã thêm
                                </div>
                              </div>
                            </div>

                            <div className="ml-auto flex items-center gap-3">
                              <div className="flex flex-col items-end">
                                <div className="flex items-center gap-2">
                                  {confirmations[`other-${person}`] && (
                                     <span className={`text-xs font-bold ${confirmations[`other-${person}`].status === 'accepted' ? 'text-emerald-600' : 'text-red-600'}`}>
                                       {confirmations[`other-${person}`].name} {confirmations[`other-${person}`].status === 'accepted' ? 'đồng ý' : 'từ chối'} lúc {confirmations[`other-${person}`].time}
                                     </span>
                                  )}
                                   <div className="flex items-center gap-2">
                                    <button 
                                      onClick={() => setConfirmStatus(`other-${person}`, person, 'accepted')} 
                                      className={`p-1.5 rounded-lg border transition-all ${confirmations[`other-${person}`]?.status === 'accepted' ? 'bg-emerald-600 text-white border-emerald-600 shadow-sm font-bold scale-102' : 'bg-emerald-50 hover:bg-emerald-100/80 text-emerald-700 border-emerald-200 hover:border-emerald-300'}`} 
                                      title="Đồng ý"
                                    >
                                      <Check className="w-4 h-4 text-inherit stroke-[3]" />
                                    </button>
                                    <button 
                                      onClick={() => setConfirmStatus(`other-${person}`, person, 'rejected')} 
                                      className={`p-1.5 rounded-lg border transition-all ${confirmations[`other-${person}`]?.status === 'rejected' ? 'bg-red-600 text-white border-red-600 shadow-sm font-bold scale-102' : 'bg-red-50 hover:bg-red-100/80 text-red-700 border-red-200 hover:border-red-300'}`} 
                                      title="Từ chối"
                                    >
                                      <X className="w-4 h-4 text-inherit stroke-[3]" />
                                    </button>
                                  </div>
                                </div>
                                {confirmations[`other-${person}`]?.status === 'rejected' && confirmations[`other-${person}`]?.reason && (
                                  <button 
                                    onClick={() => setViewReasonModal({ isOpen: true, targetName: person, reasonText: confirmations[`other-${person}`].reason! })} 
                                    className="text-[11px] text-red-500 hover:text-red-700 underline mt-1 italic font-medium"
                                  >
                                    Xem lý do
                                  </button>
                                )}
                              </div>

                              {isInfoEditable && (
                                <button
                                  onClick={() => setAddedOtherDepts(addedOtherDepts.filter(p => p !== person))}
                                  className="p-1.5 text-[#004c91] hover:text-red-500 rounded-lg hover:bg-red-50 shadow-sm border border-blue-100 bg-white transition-colors ml-2"
                                >
                                  <X className="w-4 h-4" />
                                </button>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}

                    {isInfoEditable && (
                      <div className="flex gap-2 items-center">
                        <select 
                          disabled={!isInfoEditable} 
                          className="w-[250px] px-4 py-2 rounded-lg border border-gray-300 text-sm outline-none focus:border-[#004c91] hover:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all bg-white"
                          value={participantOtherDept}
                          onChange={(e) => {
                            const dept = e.target.value;
                            setParticipantOtherDept(dept);
                            if (dept && dept !== "Chọn Phòng ban...") {
                              setSelectedOtherDeptOption(`Trưởng ${dept}`);
                            } else {
                              setSelectedOtherDeptOption('');
                            }
                          }}
                        >
                          <option value="">Chọn Phòng ban...</option>
                          <option value="Phòng Tuyển sinh">Phòng Tuyển sinh</option>
                          <option value="Phòng Đào tạo">Phòng Đào tạo</option>
                        </select>
                        
                        <div className="flex-1 px-4 py-2 text-sm text-gray-700 bg-gray-50 border border-gray-200 rounded-lg whitespace-nowrap overflow-hidden text-ellipsis">
                          {selectedOtherDeptOption || 'Chọn phòng ban để hiện trưởng phòng'}
                        </div>

                        <button 
                          disabled={!isInfoEditable}
                          onClick={() => {
                            if (selectedOtherDeptOption && !addedOtherDepts.includes(selectedOtherDeptOption)) {
                              setAddedOtherDepts([...addedOtherDepts, selectedOtherDeptOption]);
                              setShowOtherDeptNoAccountError(false);
                              setParticipantOtherDept('');
                              setSelectedOtherDeptOption('');
                            }
                          }}
                          className={`${!isInfoEditable ? 'opacity-50 cursor-not-allowed text-gray-400 bg-gray-100' : 'bg-blue-50 hover:bg-blue-100 text-[#004c91]'} px-4 py-2 rounded-lg text-sm font-bold transition-colors flex items-center gap-1 shrink-0 h-full`}
                        >
                          <Plus className="w-4 h-4" /> Thêm
                        </button>
                      </div>
                    )}

                    {showOtherDeptNoAccountError && (
                      <p className="text-sm text-red-500 mt-2 font-medium">
                        Nguyễn Không TK chưa có tài khoản <ArrowRight className="w-4 h-4 inline mx-1" /> 
                        <span 
                          className="underline cursor-pointer hover:text-red-600 font-bold"
                          onClick={() => isInfoEditable && navigate('/dashboard/accounts?action=create')}
                        >
                          Tạo tài khoản
                        </span>
                      </p>
                    )}
                  </div>
                </div>

                {/* 4. Sinh viên hỗ trợ */}
                <div className={`bg-white border border-gray-300 border-l-[6px] border-l-[#004c91] rounded-xl p-5 shadow-sm hover:shadow-md transition-shadow bg-gradient-to-r from-[#004c91]/[0.03] to-transparent`}>
                  <h4 className="font-bold text-[#004c91] text-base flex items-center gap-2 mb-4">
                    <Users className="w-5 h-5 text-[#004c91]" /> 4. Sinh viên hỗ trợ
                  </h4>
                  <div>
                    {addedStudents.length > 0 && (
                      <div className={`space-y-2 ${isInfoEditable ? 'mb-4' : ''}`}>
                        {addedStudents.map((student, idx) => (
                          <div key={idx} className="flex items-center p-3 bg-blue-50/80 rounded-xl border border-blue-200 shadow-sm animate-in fade-in slide-in-from-top-2">
                            <div className="flex items-center gap-3 flex-1">
                              <div className="w-8 h-8 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs ring-2 ring-blue-100">
                                S
                              </div>
                              <div>
                                <div className="text-sm font-bold text-[#004c91]">{student}</div>
                                <div className="text-[11px] text-blue-600 font-bold flex items-center gap-1 mt-0.5 uppercase tracking-wide">
                                  <CheckCircle className="w-3.5 h-3.5" /> Đã thêm
                                </div>
                              </div>
                            </div>

                            <div className="ml-auto flex items-center gap-3">
                              <div className="flex flex-col items-end">
                                <div className="flex items-center gap-2">
                                  {confirmations[`student-${student}`] && (
                                     <span className={`text-xs font-bold ${confirmations[`student-${student}`].status === 'accepted' ? 'text-emerald-600' : 'text-red-600'}`}>
                                       {confirmations[`student-${student}`].name} {confirmations[`student-${student}`].status === 'accepted' ? 'đồng ý' : 'từ chối'} lúc {confirmations[`student-${student}`].time}
                                     </span>
                                  )}
                                   <div className="flex items-center gap-2">
                                    <button 
                                      onClick={() => setConfirmStatus(`student-${student}`, student, 'accepted')} 
                                      className={`p-1.5 rounded-lg border transition-all ${confirmations[`student-${student}`]?.status === 'accepted' ? 'bg-emerald-600 text-white border-emerald-600 shadow-sm font-bold scale-102' : 'bg-emerald-50 hover:bg-emerald-100/80 text-emerald-700 border-emerald-200 hover:border-emerald-300'}`} 
                                      title="Đồng ý"
                                    >
                                      <Check className="w-4 h-4 text-inherit stroke-[3]" />
                                    </button>
                                    <button 
                                      onClick={() => setConfirmStatus(`student-${student}`, student, 'rejected')} 
                                      className={`p-1.5 rounded-lg border transition-all ${confirmations[`student-${student}`]?.status === 'rejected' ? 'bg-red-600 text-white border-red-600 shadow-sm font-bold scale-102' : 'bg-red-50 hover:bg-red-100/80 text-red-700 border-red-200 hover:border-red-300'}`} 
                                      title="Từ chối"
                                    >
                                      <X className="w-4 h-4 text-inherit stroke-[3]" />
                                    </button>
                                  </div>
                                </div>
                                {confirmations[`student-${student}`]?.status === 'rejected' && confirmations[`student-${student}`]?.reason && (
                                  <button 
                                    onClick={() => setViewReasonModal({ isOpen: true, targetName: student, reasonText: confirmations[`student-${student}`].reason! })} 
                                    className="text-[11px] text-red-500 hover:text-red-700 underline mt-1 italic font-medium"
                                  >
                                    Xem lý do
                                  </button>
                                )}
                              </div>

                              {isInfoEditable && (
                                <button
                                  onClick={() => setAddedStudents(addedStudents.filter(s => s !== student))}
                                  className="p-1.5 text-[#004c91] hover:text-red-500 rounded-lg hover:bg-red-50 shadow-sm border border-blue-100 bg-white transition-colors ml-2"
                                >
                                  <X className="w-4 h-4" />
                                </button>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}

                    {isInfoEditable && (
                      <div className="flex gap-2 mb-3">
                        <div className="flex-1 relative">
                          <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
                          <input disabled={!isInfoEditable} 
                            type="text" 
                            placeholder="Tìm kiếm theo Email hoặc MSSV học sinh..." 
                            className="w-full pl-9 pr-4 py-2 rounded-lg border border-gray-300 text-sm outline-none focus:border-[#004c91] hover:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all bg-white"
                            value={studentSearchText}
                            onChange={(e) => setStudentSearchText(e.target.value)}
                          />
                        </div>
                        <button disabled={!isInfoEditable}
                          onClick={() => {
                            if (studentSearchText === '123') {
                              if (!addedStudents.includes('Sinh viên 123 - Trịnh Thăng Bình')) {
                                setAddedStudents([...addedStudents, 'Sinh viên 123 - Trịnh Thăng Bình']);
                              }
                              setShowStudentNoAccountError(false);
                              setStudentSearchText('');
                            } else {
                              setShowStudentNoAccountError(true);
                            }
                          }}
                          className="px-4 py-2 bg-blue-50 hover:bg-blue-100 text-[#004c91] rounded-lg text-sm font-bold transition-colors flex items-center gap-1 shrink-0"
                        >
                          <Search className="w-4 h-4" /> Tìm & Thêm
                        </button>
                      </div>
                    )}

                    {showStudentNoAccountError && (
                      <p className="text-sm text-red-500 mt-2 font-medium">
                        Sinh viên chưa có tài khoản trên hệ thống <ArrowRight className="w-4 h-4 inline mx-1" /> 
                        <span className="font-bold">
                          Liên hệ Trưởng phòng IC để cấp tài khoản
                        </span>
                      </p>
                    )}
                  </div>
                </div>

              </div>
            </div>

            {/* 3.4 Cảnh báo */}
            <div className="p-6 border-b border-gray-100 bg-slate-50/50">
               <h3 className={`text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-6`}>
                <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                4. Cảnh báo & Thông báo
              </h3>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                {/* Thông báo hệ thống */}
                <div className="bg-white border border-blue-100 rounded-xl p-4 shadow-sm">
                  <h4 className="text-sm font-bold text-gray-700 flex items-center gap-2 mb-1">
                    <div className="w-8 h-8 rounded-full bg-blue-100 flex items-center justify-center text-[#004c91] shrink-0">
                      <Bell className="w-4 h-4" />
                    </div>
                    1. Thông báo tới các thành phần tham gia
                  </h4>
                  <p className="text-xs text-gray-500 mb-3 ml-10">Thông báo trên hệ thống</p>
                  <div className="flex flex-wrap items-center gap-2 ml-10">
                    <div className="flex items-center bg-gray-50 border border-gray-200 rounded-lg overflow-hidden">
                      <input disabled={!isInfoEditable} type="number" min="1" max="31" className="w-14 px-2 py-2 text-center text-sm font-bold outline-none bg-transparent" value={alerts.system.days} onChange={e => setAlerts({...alerts, system: {...alerts.system, days: parseInt(e.target.value)||1}})} />
                    </div>
                    <span className="text-xs text-gray-600 font-medium">ngày trước, vào lúc</span>
                    <input disabled={!isInfoEditable} type="time" className="px-2 py-2 border border-gray-200 rounded-lg text-sm outline-none bg-white" value={alerts.system.time} onChange={e => setAlerts({...alerts, system: {...alerts.system, time: e.target.value}})} />
                  </div>
                </div>

                {/* Thông báo email */}
                <div className="bg-white border border-orange-100 rounded-xl p-4 shadow-sm">
                  <h4 className="text-sm font-bold text-gray-700 flex items-center gap-2 mb-1">
                    <div className="w-8 h-8 rounded-full bg-orange-100 flex items-center justify-center text-[#f37021] shrink-0">
                      <Mail className="w-4 h-4" />
                    </div>
                    2. Thông báo tới HOST
                  </h4>
                  <p className="text-xs text-gray-500 mb-3 ml-10">Gửi email nhắc nhở host</p>
                  <div className="flex flex-wrap items-center gap-2 ml-10">
                    <div className="flex items-center bg-gray-50 border border-gray-200 rounded-lg overflow-hidden">
                      <input disabled={!isInfoEditable} type="number" min="1" max="31" className="w-14 px-2 py-2 text-center text-sm font-bold outline-none bg-transparent" value={alerts.email.days} onChange={e => setAlerts({...alerts, email: {...alerts.email, days: parseInt(e.target.value)||1}})} />
                    </div>
                    <span className="text-xs text-gray-600 font-medium">ngày trước, vào lúc</span>
                    <input disabled={!isInfoEditable} type="time" className="px-2 py-2 border border-gray-200 rounded-lg text-sm outline-none bg-white" value={alerts.email.time} onChange={e => setAlerts({...alerts, email: {...alerts.email, time: e.target.value}})} />
                  </div>
                </div>
              </div>
            </div>

            {/* 3.5 Ghi chú */}
                        <div className="p-6">
                          <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-3">
                            <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                            5. Ghi chú chung
                          </h3>
                          <textarea readOnly={!isInfoEditable} className="w-full px-4 py-3 rounded-xl border border-gray-200 bg-gray-50/50 text-gray-800 font-medium text-sm min-h-[100px] resize-none" defaultValue="Không có ghi chú thêm..."></textarea>
                        </div>

                            </div>
                          </motion.div>
                        )}
                      </AnimatePresence>
                    </div>

                    {isInfoEditable && (
                      <div className="flex justify-end gap-3 px-8 pb-8 pt-4 border-t border-gray-100">
                        <button 
                          onClick={() => setIsInfoEditable(false)}
                          className="px-8 py-3 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-colors shadow-sm outline-none"
                        >
                          Hủy
                        </button>
                        <button 
                          onClick={() => setIsInfoEditable(false)}
                          className="px-8 py-3 rounded-xl font-bold text-white bg-[#10b981] hover:bg-emerald-600 transition-all shadow-md hover:shadow-lg active:scale-[0.98] flex items-center gap-2 outline-none uppercase tracking-wider"
                        >
                          <CheckCircle2 className="w-5 h-5"/>
                          Hoàn thành
                        </button>
                      </div>
                    )}

                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>

          {/* Phần 2: Chuẩn bị chi tiết */}
          <div className="bg-white rounded-[2rem] border border-gray-200 shadow-sm overflow-hidden transition-all duration-300">
            <div 
              className="px-8 py-6 flex items-center justify-between cursor-pointer transition-colors bg-[#004c91]"
              onClick={() => setIsSetupExpanded(!isSetupExpanded)}
            >
              <div>
                <h2 className="text-xl font-bold text-white border-l-4 border-[#f37021] pl-3">2. Chuẩn bị chi tiết</h2>
                <p className="text-sm font-medium text-blue-100 mt-1 pl-4">Chuẩn bị cho từng loại hình tham quan</p>
              </div>
              <div className="flex items-center gap-3">
                {!isSetupEditable && !isClosed && !isDept && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      setIsSetupEditable(true);
                      setIsSetupExpanded(true);
                    }}
                    className="w-10 h-10 rounded-full hover:bg-white/20 flex items-center justify-center text-white transition-colors"
                  >
                    <Edit3 className="w-5 h-5" />
                  </button>
                )}
                <div className="w-10 h-10 rounded-full bg-white/10 flex items-center justify-center text-white">
                  {isSetupExpanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                </div>
              </div>
            </div>

            <AnimatePresence>
              {isSetupExpanded && (
                <motion.div
                  initial={{ height: 0, opacity: 0 }}
                  animate={{ height: 'auto', opacity: 1 }}
                  exit={{ height: 0, opacity: 0 }}
                  className="border-t border-gray-100 overflow-hidden"
                >
                  <div className={`p-8 space-y-12 ${!isSetupEditable ? 'bg-slate-50/50 opacity-90' : 'bg-white'}`}>
               {/* Mục 1: Welcome LED */}
               <div className="bg-white border border-gray-200 rounded-2xl shadow-sm overflow-hidden">
                 <div 
                   className="flex items-center justify-between px-6 py-4 cursor-pointer hover:bg-orange-50/50 transition-colors bg-white"
                   onClick={() => setIsSection1Expanded(!isSection1Expanded)}
                 >
                    <h3 className="text-xl font-bold text-orange-900 flex items-center gap-2">
                       <div className="p-1.5 bg-orange-100 rounded-lg"><MonitorPlay className="w-5 h-5 text-[#f37021]" /></div>
                       Mục 1: Welcome LED
                    </h3>
                    <div className="w-8 h-8 rounded-full bg-gray-50 flex items-center justify-center text-gray-500">
                      {isSection1Expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                    </div>
                 </div>

                 <AnimatePresence>
                   {isSection1Expanded && (
                     <motion.div
                       initial={{ height: 0, opacity: 0 }}
                       animate={{ height: 'auto', opacity: 1 }}
                       exit={{ height: 0, opacity: 0 }}
                     >
                       <div className="p-6 pt-2 border-t border-gray-100 bg-white">
                         <div className="space-y-4 mb-4">
                           <label className="flex items-center gap-3 cursor-pointer">
                             <input 
                               type="radio" 
                               name="needLED"
                               checked={needLED === false} 
                               disabled={!isSetupEditable} onChange={() => setNeedLED(false)} 
                               className="w-5 h-5 rounded-full border-gray-300 text-[#004c91] focus:ring-[#004c91]"
                             />
                             <span className="text-[15px] font-bold text-gray-700">Không cần màn LED</span>
                           </label>
                           <label className="flex items-center gap-3 cursor-pointer">
                             <input 
                               type="radio" 
                               name="needLED"
                               checked={needLED === true} 
                               disabled={!isSetupEditable} onChange={() => setNeedLED(true)} 
                               className="w-5 h-5 rounded-full border-gray-300 text-[#004c91] focus:ring-[#004c91]"
                             />
                             <span className="text-[15px] font-bold text-gray-700">Cần màn LED</span>
                           </label>
                         </div>

                         {needLED && (
                           <div className="pl-8 pt-4 border-t border-gray-100 animate-in fade-in slide-in-from-top-2">
                             <label className="flex items-center gap-3 cursor-pointer">
                               <input 
                                 type="checkbox" 
                                 checked={sentImage} 
                                 disabled={!isSetupEditable} onChange={(e) => setSentImage(e.target.checked)} 
                                 className="w-5 h-5 rounded border-gray-300 text-green-600 focus:ring-green-600"
                               />
                               <span className="text-[15px] font-bold text-gray-700">Xác nhận đã gửi ảnh</span>
                             </label>
                           </div>
                         )}
                       </div>
                     </motion.div>
                   )}
                 </AnimatePresence>
               </div>

               {/* Mục 2: Chuẩn bị cho Campus Tour */}
               <div className="bg-white border border-gray-200 rounded-2xl shadow-sm overflow-hidden">
                 <div 
                   className="flex items-center justify-between px-6 py-4 cursor-pointer hover:bg-orange-50/50 transition-colors bg-white"
                   onClick={() => setIsSection2Expanded(!isSection2Expanded)}
                 >
                    <h3 className="text-xl font-bold text-orange-900 flex items-center gap-2">
                       <div className="p-1.5 bg-orange-100 rounded-lg"><MapPin className="w-5 h-5 text-[#f37021]" /></div>
                       Mục 2: Chuẩn bị cho Campus Tour
                    </h3>
                    <div className="w-8 h-8 rounded-full bg-gray-50 flex items-center justify-center text-gray-500">
                      {isSection2Expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                    </div>
                 </div>

                 <AnimatePresence>
                   {isSection2Expanded && (
                     <motion.div
                       initial={{ height: 0, opacity: 0 }}
                       animate={{ height: 'auto', opacity: 1 }}
                       exit={{ height: 0, opacity: 0 }}
                     >
                       <div className="p-6 pt-2 border-t border-gray-100 bg-white">
                         <div className="space-y-8">
                    {/* Người dẫn */}
                    <div>
                      <h4 className="text-lg font-bold text-[#004c91] mb-3 flex items-center gap-2">
                        <UserCheck className="w-6 h-6 text-[#f37021]"/> Người dẫn <span className="text-red-500">*</span>
                      </h4>
                      <div className="flex flex-col gap-4 p-4 bg-blue-50/50 rounded-xl border border-blue-100">
                        <label className="flex items-center gap-2 cursor-pointer w-max">
                          <input 
                            type="radio" 
                            name="tourGuide"
                            checked={tourGuide === 'me'}
                            disabled={!isSetupEditable} onChange={() => setTourGuide('me')}
                            className="w-4 h-4 text-[#004c91] focus:ring-[#004c91]"
                          />
                          <span className="font-medium text-gray-700">Là tôi</span>
                        </label>
                        <div className="flex flex-col sm:flex-row gap-3 sm:items-start">
                          <label className="flex items-center gap-2 cursor-pointer shrink-0 sm:mt-2.5">
                            <input 
                              type="radio" 
                              name="tourGuide"
                              checked={tourGuide === 'other'}
                              disabled={!isSetupEditable} onChange={() => setTourGuide('other')}
                              className="w-4 h-4 text-[#004c91] focus:ring-[#004c91]"
                            />
                            <span className="font-medium text-gray-700">Người khác:</span>
                          </label>
                          <div className="flex-1 flex flex-col gap-2 w-full">
                            <div className="flex gap-2 items-center w-full">
                              <select 
                                value={selectedTourGuide}
                                onChange={(e) => setSelectedTourGuide(e.target.value)}
                                disabled={!isSetupEditable || tourGuide !== 'other'}
                                className="w-full max-w-sm px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors disabled:bg-gray-100 disabled:opacity-50 outline-none text-sm bg-white"
                              >
                                <option value="">Chọn từ danh sách tham dự...</option>
                                <option value="Trần Văn B (Phòng Hành chính)">Trần Văn B (Phòng Hành chính)</option>
                                <option value="Lê Thị C (Phòng Tuyển sinh)">Lê Thị C (Phòng Tuyển sinh)</option>
                              </select>
                              <button 
                                type="button"
                                onClick={() => {
                                  if (selectedTourGuide && !addedTourGuides.includes(selectedTourGuide)) {
                                    setAddedTourGuides([...addedTourGuides, selectedTourGuide]);
                                    setSelectedTourGuide('');
                                  }
                                }}
                                disabled={!isSetupEditable || tourGuide !== 'other' || !selectedTourGuide} 
                                className="flex items-center gap-1 px-3 py-2 bg-orange-50/80 text-orange-600 border border-orange-200 font-bold rounded-lg text-sm hover:bg-orange-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed outline-none cursor-pointer shrink-0"
                              >
                                <Plus className="w-4 h-4" /> Thêm
                              </button>
                            </div>
                            {addedTourGuides.length > 0 && tourGuide === 'other' && (
                              <div className="mt-2 space-y-2 max-w-[460px]">
                                {addedTourGuides.map((guide, idx) => (
                                  <div key={idx} className="flex items-center justify-between p-3 bg-emerald-50/80 rounded-lg border border-emerald-200 shadow-sm animate-in fade-in slide-in-from-top-2">
                                    <div className="flex items-center gap-3">
                                      <div className="w-8 h-8 rounded-full bg-emerald-500 text-white flex items-center justify-center font-bold text-xs shadow-sm border border-emerald-600">
                                        {guide.charAt(0)}
                                      </div>
                                      <div>
                                        <div className="text-sm font-bold text-emerald-900">{guide}</div>
                                        <div className="text-[11px] text-emerald-600 font-bold flex items-center gap-1 mt-0.5 uppercase tracking-wide">
                                           <CheckCircle2 className="w-3.5 h-3.5" /> Đã thêm
                                        </div>
                                      </div>
                                    </div>
                                    {isSetupEditable && (
                                      <button
                                        type="button" 
                                        onClick={() => setAddedTourGuides(addedTourGuides.filter((g) => g !== guide))}
                                        className="p-1.5 text-emerald-600 hover:text-red-500 rounded-md hover:bg-red-50 transition-colors bg-white shadow-sm border border-emerald-100"
                                      >
                                        <X className="w-4 h-4" />
                                      </button>
                                    )}
                                  </div>
                                ))}
                              </div>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>

                    <hr className="border-t-[2px] border-gray-200" />

                    {/* Xe điện */}
                    <div>
                      <h4 className="text-lg font-bold text-[#004c91] mb-3 flex items-center gap-2">
                        <Car className="w-6 h-6 text-[#f37021]"/> Xe điện
                        {sentRequests["electricCar"] && (
                          <span className="ml-2 text-xs font-bold text-amber-700 bg-amber-100 px-2 py-1 rounded-md flex items-center gap-1 border border-amber-300 shadow-sm animate-in fade-in">
                             <span className="w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse"></span> Chờ xác nhận
                          </span>
                        )}
                      </h4>
                      <div className="p-1">
                        {setupDetails.electricCar.collapsed ? (
                          <div 
                            className="flex items-center justify-between p-3 sm:p-3 bg-blue-50/40 hover:bg-blue-100/30 border border-blue-200 rounded-xl transition-all cursor-pointer shadow-sm"
                            onClick={() => setSetupDetails(p => ({...p, electricCar: {...p.electricCar, collapsed: false}}))}
                          >
                            <div className="flex items-center gap-3">
                              <CheckCircle className="w-5 h-5 text-[#004c91] shrink-0" />
                              <div className="flex flex-col md:flex-row md:items-center gap-2 md:gap-4 text-sm">
                                <span className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {getLeaderName(selectedElectricCarDept) || "Chưa chọn"}
                                </span>
                                <span className="font-bold text-[#004c91] bg-blue-50 px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  Số lượng: {setupDetails.electricCar.quantity || "0"}
                                </span>
                                <span className="text-gray-600 flex items-center gap-1.5 font-medium text-xs">
                                  <Calendar className="w-4 h-4 text-[#004c91]" />
                                  {formatDetailSummary('electricCar', setupDetails.electricCar).timeStr}
                                </span>
                              </div>
                            </div>
                            <div className="flex items-center gap-1 text-gray-500 font-bold hover:text-[#004c91] transition-colors text-xs">
                              <span className="hidden sm:inline">Chi tiết</span>
                              <ChevronDown className="w-5 h-5" />
                            </div>
                          </div>
                        ) : (
                          <div className="flex flex-col gap-4 p-5 bg-white border border-gray-200 rounded-xl shadow-sm">
                            <div className="flex items-center justify-between pb-3 border-b border-gray-100">
                              <span className="text-sm font-bold text-[#004c91] flex items-center gap-1.5">
                                <CheckCircle className="w-4 h-4 text-[#004c91]" />
                                {setupDetails.electricCar.confirmed ? "Đã xác nhận (Không thể chỉnh sửa)" : "Cấu hình chi tiết"}
                              </span>
                              <button 
                                type="button" 
                                onClick={() => setSetupDetails(p => ({...p, electricCar: {...p.electricCar, collapsed: true}}))}
                                className="p-1 px-2 rounded-lg hover:bg-gray-100 text-gray-500 hover:text-gray-800 transition-colors flex items-center gap-1 text-xs font-bold"
                              >
                                Thu gọn
                                <ChevronUp className="w-4 h-4" />
                              </button>
                            </div>

                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                              <div className="space-y-4">
                                <div>
                                  <label className="block text-xs font-bold text-gray-600 mb-1">Số lượng cần mượn</label>
                                  <input 
                                    type="number" 
                                    min="1" 
                                    className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-450 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" 
                                    placeholder="VD: 2" 
                                    disabled={!isSetupEditable || setupDetails.electricCar.confirmed} 
                                    value={setupDetails.electricCar.quantity} 
                                    onChange={e => setSetupDetails(p => ({...p, electricCar: {...p.electricCar, quantity: e.target.value}}))} 
                                  />
                                </div>
                                <div>
                                  <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian sử dụng</label>
                                  <div className="flex flex-col sm:flex-row gap-2 w-full">
                                    <input 
                                      type="date" 
                                      className="flex-1 px-3 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                      disabled={!isSetupEditable || setupDetails.electricCar.confirmed} 
                                      value={setupDetails.electricCar.date}
                                      onChange={e => setSetupDetails(p => ({...p, electricCar: {...p.electricCar, date: e.target.value}}))}
                                    />
                                    <div className="flex items-center gap-2 flex-1">
                                      <input 
                                        type="time" 
                                        className="flex-1 w-full min-w-[80px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                        disabled={!isSetupEditable || setupDetails.electricCar.confirmed} 
                                        value={setupDetails.electricCar.startTime}
                                        onChange={e => setSetupDetails(p => ({...p, electricCar: {...p.electricCar, startTime: e.target.value}}))}
                                      /> 
                                      <span className="text-gray-400 font-bold text-xs uppercase shrink-0">Đến</span>
                                      <input 
                                        type="time" 
                                        className="flex-1 w-full min-w-[80px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                        disabled={!isSetupEditable || setupDetails.electricCar.confirmed} 
                                        value={setupDetails.electricCar.endTime}
                                        onChange={e => setSetupDetails(p => ({...p, electricCar: {...p.electricCar, endTime: e.target.value}}))}
                                      />
                                    </div>
                                  </div>
                                </div>
                              </div>
                              <div className="space-y-4">
                                <div>
                                  <label className="block text-xs font-bold text-gray-600 mb-1">Ghi chú (Note)</label>
                                  <textarea 
                                    className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-450 transition-colors outline-none text-sm resize-none h-[100px] disabled:bg-gray-50 disabled:text-gray-400" 
                                    placeholder="Ghi chú thêm..." 
                                    disabled={!isSetupEditable || setupDetails.electricCar.confirmed} 
                                    value={setupDetails.electricCar.note}
                                    onChange={e => setSetupDetails(p => ({...p, electricCar: {...p.electricCar, note: e.target.value}}))}
                                  ></textarea>
                                </div>
                                <div>
                                  <label className="block text-xs font-bold text-gray-600 mb-1">Chọn phòng ban xử lý</label>
                                  <select 
                                    className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-[#004c91] transition-colors outline-none text-sm bg-white disabled:bg-gray-50 disabled:text-gray-400"
                                    value={selectedElectricCarDept}
                                    disabled={!isSetupEditable || setupDetails.electricCar.confirmed} 
                                    onChange={(e) => setSelectedElectricCarDept(e.target.value)}
                                  >
                                    <option value="">-- Chọn phòng ban --</option>
                                    <option value="Tuyển sinh">Phòng Tuyển sinh</option>
                                    <option value="Hành chính">Phòng Hành chính</option>
                                  </select>
                                  {renderLeaderInfo(selectedElectricCarDept, "electricCar")}
                                </div>
                              </div>
                            </div>

                          </div>
                        )}
                      </div>
                    </div>

                    <hr className="border-t-[2px] border-gray-200" />

                    {/* Người lái */}
                    <div>
                      <h4 className="text-lg font-bold text-[#004c91] mb-3 flex items-center gap-2">
                        <UserCheck className="w-6 h-6 text-[#f37021]"/> Người lái
                        {sentRequests["driver"] && (
                          <span className="ml-2 text-xs font-bold text-amber-700 bg-amber-100 px-2 py-1 rounded-md flex items-center gap-1 border border-amber-300 shadow-sm animate-in fade-in">
                             <span className="w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse"></span> Chờ xác nhận
                          </span>
                        )}
                      </h4>
                      <div className="p-1">
                        {setupDetails.driver.collapsed ? (
                          <div 
                            className="flex items-center justify-between p-3 sm:p-3 bg-blue-50/40 hover:bg-blue-100/30 border border-blue-200 rounded-xl transition-all cursor-pointer shadow-sm"
                            onClick={() => setSetupDetails(p => ({...p, driver: {...p.driver, collapsed: false}}))}
                          >
                            <div className="flex items-center gap-3">
                              <CheckCircle className="w-5 h-5 text-[#004c91] shrink-0" />
                              <div className="flex flex-col md:flex-row md:items-center gap-2 md:gap-4 text-sm">
                                <span className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {getLeaderName(selectedDriverDept) || "Chưa chọn"}
                                </span>
                                <span className="font-bold text-[#004c91] bg-blue-50 px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  Số lượng: {setupDetails.driver.quantity || "0"}
                                </span>
                                <span className="text-gray-600 flex items-center gap-1.5 font-medium text-xs">
                                  <Calendar className="w-4 h-4 text-[#004c91]" />
                                  {formatDetailSummary('driver', setupDetails.driver).timeStr}
                                </span>
                              </div>
                            </div>
                            <div className="flex items-center gap-1 text-gray-500 font-bold hover:text-[#004c91] transition-colors text-xs">
                              <span className="hidden sm:inline">Chi tiết</span>
                              <ChevronDown className="w-5 h-5" />
                            </div>
                          </div>
                        ) : (
                          <div className="flex flex-col gap-4 p-5 bg-white border border-gray-200 rounded-xl shadow-sm">
                            <div className="flex items-center justify-between pb-3 border-b border-gray-100">
                              <span className="text-sm font-bold text-[#004c91] flex items-center gap-1.5">
                                <CheckCircle className="w-4 h-4 text-[#004c91]" />
                                {setupDetails.driver.confirmed ? "Đã xác nhận (Không thể chỉnh sửa)" : "Cấu hình chi tiết"}
                              </span>
                              <button 
                                type="button" 
                                onClick={() => setSetupDetails(p => ({...p, driver: {...p.driver, collapsed: true}}))}
                                className="p-1 px-2 rounded-lg hover:bg-gray-100 text-gray-500 hover:text-gray-800 transition-colors flex items-center gap-1 text-xs font-bold"
                              >
                                Thu gọn
                                <ChevronUp className="w-4 h-4" />
                              </button>
                            </div>

                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                              <div className="md:col-span-2">
                                 <label className="block text-xs font-bold text-gray-600 mb-1">Số lượng</label>
                                 <input 
                                   type="number" 
                                   min="1" 
                                   className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-450 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" 
                                   placeholder="VD: 2" 
                                   disabled={!isSetupEditable || setupDetails.driver.confirmed} 
                                   value={setupDetails.driver.quantity} 
                                   onChange={e => setSetupDetails(p => ({...p, driver: {...p.driver, quantity: e.target.value}}))} 
                                 />
                              </div>
                              <div className="md:col-span-2">
                                 <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian hỗ trợ</label>
                                 <div className="flex flex-col sm:flex-row gap-2 w-full">
                                    <input 
                                      type="date" 
                                      className="flex-1 px-3 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400 focus:ring-1 focus:ring-[#004c91]" 
                                      disabled={!isSetupEditable || setupDetails.driver.confirmed} 
                                      value={setupDetails.driver.date}
                                      onChange={e => setSetupDetails(p => ({...p, driver: {...p.driver, date: e.target.value}}))}
                                    />
                                    <div className="flex items-center gap-2 flex-1">
                                      <input 
                                        type="time" 
                                        className="flex-1 w-full min-w-[80px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                        disabled={!isSetupEditable || setupDetails.driver.confirmed} 
                                        value={setupDetails.driver.startTime}
                                        onChange={e => setSetupDetails(p => ({...p, driver: {...p.driver, startTime: e.target.value}}))}
                                      /> 
                                      <span className="text-gray-400 font-bold text-xs uppercase shrink-0">Đến</span>
                                      <input 
                                        type="time" 
                                        className="flex-1 w-full min-w-[80px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                        disabled={!isSetupEditable || setupDetails.driver.confirmed} 
                                        value={setupDetails.driver.endTime}
                                        onChange={e => setSetupDetails(p => ({...p, driver: {...p.driver, endTime: e.target.value}}))}
                                      />
                                    </div>
                                 </div>
                              </div>
                              <div className="md:col-span-2 mt-2">
                                 <label className="block text-xs font-bold text-gray-600 mb-1">Chọn phòng ban</label>
                                 <select 
                                   className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-[#004c91] transition-colors outline-none text-sm bg-white disabled:bg-gray-50 disabled:text-gray-400"
                                   value={selectedDriverDept}
                                   disabled={!isSetupEditable || setupDetails.driver.confirmed} 
                                   onChange={(e) => setSelectedDriverDept(e.target.value)}
                                 >
                                  <option value="">-- Chọn --</option>
                                  <option value="Hành chính">Phòng Hành chính</option>
                                 </select>
                                 {renderLeaderInfo(selectedDriverDept, "driver")}
                              </div>
                            </div>

                          </div>
                        )}
                      </div>
                    </div>
                         </div>
                       </div>
                     </motion.div>
                   )}
                 </AnimatePresence>
               </div>

               {/* Mục 3: Chuẩn bị cho họp */}
               <div className="bg-white border border-gray-200 rounded-2xl shadow-sm overflow-hidden">
                 <div 
                   className="flex items-center justify-between px-6 py-4 cursor-pointer hover:bg-orange-50/50 transition-colors bg-white"
                   onClick={() => setIsSection3Expanded(!isSection3Expanded)}
                 >
                    <h3 className="text-xl font-bold text-orange-900 flex items-center gap-2">
                       <div className="p-1.5 bg-orange-100 rounded-lg"><Building2 className="w-5 h-5 text-[#f37021]" /></div>
                       Mục 3: Chuẩn bị cho họp
                    </h3>
                    <div className="w-8 h-8 rounded-full bg-gray-50 flex items-center justify-center text-gray-500">
                      {isSection3Expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                    </div>
                 </div>

                 <AnimatePresence>
                   {isSection3Expanded && (
                     <motion.div
                       initial={{ height: 0, opacity: 0 }}
                       animate={{ height: 'auto', opacity: 1 }}
                       exit={{ height: 0, opacity: 0 }}
                     >
                       <div className="p-6 pt-2 border-t border-gray-100 bg-white">
                         <div className="space-y-8">
                    {/* Phòng họp */}
                    <div>
                      <h4 className="text-lg font-bold text-[#004c91] mb-3 flex items-center gap-2">
                         Phòng họp <span className="text-red-500">*</span>
                         {sentRequests["room"] && (
                          <span className="ml-2 text-xs font-bold text-amber-700 bg-amber-100 px-2 py-1 rounded-md flex items-center gap-1 border border-amber-300 shadow-sm animate-in fade-in">
                             <span className="w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse"></span> Chờ xác nhận
                          </span>
                        )}
                      </h4>
                      <div className="p-1">
                        {setupDetails.room.collapsed ? (
                          <div 
                            className="flex items-center justify-between p-3 sm:p-3 bg-blue-50/40 hover:bg-blue-100/30 border border-blue-200 rounded-xl transition-all cursor-pointer shadow-sm"
                            onClick={() => setSetupDetails(p => ({...p, room: {...p.room, collapsed: false}}))}
                          >
                            <div className="flex items-center gap-3">
                              <CheckCircle className="w-5 h-5 text-[#004c91] shrink-0" />
                              <div className="flex flex-col md:flex-row md:items-center gap-2 md:gap-4 text-sm">
                                <span className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-200 text-xs shadow-sm">
                                  {getLeaderName(selectedRoomDept) || "Chưa chọn"}
                                </span>
                                <span className="font-bold text-[#004c91] bg-blue-50 px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  Phòng: {setupDetails.room.quantity || "Chưa chọn"}
                                </span>
                                <span className="text-gray-600 flex items-center gap-1.5 font-medium text-xs">
                                  <Calendar className="w-4 h-4 text-[#004c91]" />
                                  {formatDetailSummary('room', setupDetails.room).timeStr}
                                </span>
                              </div>
                            </div>
                            <div className="flex items-center gap-1 text-gray-500 font-bold hover:text-[#004c91] transition-colors text-xs">
                              <span className="hidden sm:inline">Chi tiết</span>
                              <ChevronDown className="w-5 h-5" />
                            </div>
                          </div>
                        ) : (
                          <div className="flex flex-col gap-4 p-5 bg-white border border-gray-200 rounded-xl shadow-sm">
                            <div className="flex items-center justify-between pb-3 border-b border-gray-100">
                              <span className="text-sm font-bold text-[#004c91] flex items-center gap-1.5">
                                <CheckCircle className="w-4 h-4 text-[#004c91]" />
                                {setupDetails.room.confirmed ? "Đã xác nhận (Không thể chỉnh sửa)" : "Cấu hình chi tiết"}
                              </span>
                              <button 
                                type="button" 
                                onClick={() => setSetupDetails(p => ({...p, room: {...p.room, collapsed: true}}))}
                                className="p-1 px-2 rounded-lg hover:bg-gray-100 text-gray-500 hover:text-gray-800 transition-colors flex items-center gap-1 text-xs font-bold"
                              >
                                Thu gọn
                                <ChevronUp className="w-4 h-4" />
                              </button>
                            </div>

                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                              <div className="md:col-span-2">
                                <label className="block text-xs font-bold text-gray-600 mb-1">Phòng</label>
                                <input 
                                  type="text" 
                                  className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-450 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" 
                                  placeholder="VD: Tòa Alpha, Phòng 101" 
                                  disabled={!isSetupEditable || setupDetails.room.confirmed} 
                                  value={setupDetails.room.quantity} 
                                  onChange={e => setSetupDetails(p => ({...p, room: {...p.room, quantity: e.target.value}}))} 
                                />
                              </div>
                              <div className="md:col-span-2">
                                <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian</label>
                                <div className="flex flex-col sm:flex-row gap-2 w-full">
                                  <input 
                                    type="date" 
                                    className="flex-1 px-3 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                    disabled={!isSetupEditable || setupDetails.room.confirmed} 
                                    value={setupDetails.room.date}
                                    onChange={e => setSetupDetails(p => ({...p, room: {...p.room, date: e.target.value}}))}
                                  />
                                  <div className="flex items-center gap-2 flex-1">
                                    <input 
                                      type="time" 
                                      className="flex-1 w-full min-w-[80px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                      disabled={!isSetupEditable || setupDetails.room.confirmed} 
                                      value={setupDetails.room.startTime}
                                      onChange={e => setSetupDetails(p => ({...p, room: {...p.room, startTime: e.target.value}}))}
                                    /> 
                                    <span className="text-gray-400 font-bold text-xs uppercase shrink-0">Đến</span>
                                    <input 
                                      type="time" 
                                      className="flex-1 w-full min-w-[80px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                      disabled={!isSetupEditable || setupDetails.room.confirmed} 
                                      value={setupDetails.room.endTime}
                                      onChange={e => setSetupDetails(p => ({...p, room: {...p.room, endTime: e.target.value}}))}
                                    />
                                  </div>
                                </div>
                              </div>
                              <div className="md:col-span-2 mt-2">
                                 <label className="block text-xs font-bold text-gray-600 mb-1">Phòng ban</label>
                                 <select 
                                   className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-[#004c91] transition-colors outline-none text-sm bg-white disabled:bg-gray-50 disabled:text-gray-400"
                                   value={selectedRoomDept}
                                   disabled={!isSetupEditable || setupDetails.room.confirmed} 
                                   onChange={(e) => setSelectedRoomDept(e.target.value)}
                                 >
                                  <option value="">-- Chọn --</option>
                                  <option value="Hành chính">Phòng Hành chính</option>
                                 </select>
                                 {renderLeaderInfo(selectedRoomDept, "room")}
                              </div>
                            </div>

                          </div>
                        )}
                      </div>
                    </div>

                    <hr className="border-t-[2px] border-gray-200" />

                    {/* Teabreak */}
                    <div>
                      <h4 className="text-lg font-bold text-[#004c91] mb-3 flex items-center gap-2">
                         <Coffee className="w-6 h-6 text-[#f37021]"/> Teabreak
                         {sentRequests["teabreak"] && (
                          <span className="ml-2 text-xs font-bold text-amber-700 bg-amber-100 px-2 py-1 rounded-md flex items-center gap-1 border border-amber-300 shadow-sm animate-in fade-in">
                             <span className="w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse"></span> Chờ xác nhận
                          </span>
                        )}
                      </h4>
                      <div className="p-1">
                        {setupDetails.teabreak.collapsed ? (
                          <div 
                            className="flex items-center justify-between p-3 sm:p-3 bg-blue-50/40 hover:bg-blue-100/30 border border-blue-200 rounded-xl transition-all cursor-pointer shadow-sm"
                            onClick={() => setSetupDetails(p => ({...p, teabreak: {...p.teabreak, collapsed: false}}))}
                          >
                            <div className="flex items-center gap-3">
                              <CheckCircle className="w-5 h-5 text-[#004c91] shrink-0" />
                              <div className="flex flex-col md:flex-row md:items-center gap-2 md:gap-4 text-sm">
                                <span className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {getLeaderName(selectedTeabreakDept) || "Chưa chọn"}
                                </span>
                                <span className="font-bold text-[#004c91] bg-blue-50 px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  Số lượng suất: {setupDetails.teabreak.quantity || "0"}
                                </span>
                                <span className="text-gray-600 flex items-center gap-1.5 font-medium text-xs">
                                  <Calendar className="w-4 h-4 text-[#004c91]" />
                                  {formatDetailSummary('teabreak', setupDetails.teabreak).timeStr}
                                </span>
                              </div>
                            </div>
                            <div className="flex items-center gap-1 text-gray-500 font-bold hover:text-[#004c91] transition-colors text-xs">
                              <span className="hidden sm:inline">Chi tiết</span>
                              <ChevronDown className="w-5 h-5" />
                            </div>
                          </div>
                        ) : (
                          <div className="flex flex-col gap-4 p-5 bg-white border border-gray-200 rounded-xl shadow-sm">
                            <div className="flex items-center justify-between pb-3 border-b border-gray-100">
                              <span className="text-sm font-bold text-[#004c91] flex items-center gap-1.5">
                                <CheckCircle className="w-4 h-4 text-[#004c91]" />
                                {setupDetails.teabreak.confirmed ? "Đã xác nhận (Không thể chỉnh sửa)" : "Cấu hình chi tiết"}
                              </span>
                              <button 
                                type="button" 
                                onClick={() => setSetupDetails(p => ({...p, teabreak: {...p.teabreak, collapsed: true}}))}
                                className="p-1 px-2 rounded-lg hover:bg-gray-100 text-gray-500 hover:text-gray-800 transition-colors flex items-center gap-1 text-xs font-bold"
                              >
                                Thu gọn
                                <ChevronUp className="w-4 h-4" />
                              </button>
                            </div>

                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                              <div className="md:col-span-2">
                                <label className="block text-xs font-bold text-gray-600 mb-1">Số lượng (suất)</label>
                                <input 
                                  type="number" 
                                  min="0" 
                                  className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-450 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" 
                                  placeholder="VD: 15" 
                                  disabled={!isSetupEditable || setupDetails.teabreak.confirmed} 
                                  value={setupDetails.teabreak.quantity} 
                                  onChange={e => setSetupDetails(p => ({...p, teabreak: {...p.teabreak, quantity: e.target.value}}))} 
                                />
                              </div>
                              <div>
                                <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian</label>
                                <div className="flex flex-col sm:flex-row gap-2 w-full">
                                  <input 
                                    type="date" 
                                    className="flex-1 px-3 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                    disabled={!isSetupEditable || setupDetails.teabreak.confirmed} 
                                    value={setupDetails.teabreak.date}
                                    onChange={e => setSetupDetails(p => ({...p, teabreak: {...p.teabreak, date: e.target.value}}))}
                                  />
                                  <div className="flex items-center gap-2 flex-1">
                                    <input 
                                      type="time" 
                                      className="flex-1 w-full min-w-[80px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                      disabled={!isSetupEditable || setupDetails.teabreak.confirmed} 
                                      value={setupDetails.teabreak.startTime}
                                      onChange={e => setSetupDetails(p => ({...p, teabreak: {...p.teabreak, startTime: e.target.value}}))}
                                    /> 
                                    <span className="text-gray-400 font-bold text-xs uppercase shrink-0">Đến</span>
                                    <input 
                                      type="time" 
                                      className="flex-1 w-full min-w-[80px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-450 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400" 
                                      disabled={!isSetupEditable || setupDetails.teabreak.confirmed} 
                                      value={setupDetails.teabreak.endTime}
                                      onChange={e => setSetupDetails(p => ({...p, teabreak: {...p.teabreak, endTime: e.target.value}}))}
                                    />
                                  </div>
                                </div>
                              </div>
                              <div>
                                 <label className="block text-xs font-bold text-gray-600 mb-1">Phòng ban</label>
                                 <select 
                                   className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-[#004c91] transition-colors outline-none text-sm bg-white disabled:bg-gray-50 disabled:text-gray-400"
                                   value={selectedTeabreakDept}
                                   disabled={!isSetupEditable || setupDetails.teabreak.confirmed} 
                                   onChange={(e) => setSelectedTeabreakDept(e.target.value)}
                                 >
                                  <option value="">-- Chọn --</option>
                                  <option value="Hành chính">Phòng Hành chính</option>
                                 </select>
                                 {renderLeaderInfo(selectedTeabreakDept, "teabreak")}
                              </div>
                              <div className="md:col-span-2">
                                <label className="block text-xs font-bold text-gray-600 mb-1">Ghi chú (Layout, khăn trải bàn, biển tên, kỹ thuật đặc biệt...)</label>
                                <textarea 
                                  className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-450 transition-colors outline-none text-sm resize-none h-[80px] disabled:bg-gray-50 disabled:text-gray-400" 
                                  placeholder="Yêu cầu chi tiết..." 
                                  disabled={!isSetupEditable || setupDetails.teabreak.confirmed}
                                  value={setupDetails.teabreak.note}
                                  onChange={e => setSetupDetails(p => ({...p, teabreak: {...p.teabreak, note: e.target.value}}))}
                                ></textarea>
                              </div>
                            </div>

                          </div>
                        )}
                      </div>
                    </div>
                         </div>
                       </div>
                     </motion.div>
                   )}
                 </AnimatePresence>
               </div>

               {/* Mục 4: Khác */}
               <div className="bg-white border border-gray-200 rounded-2xl shadow-sm overflow-hidden">
                 <div 
                   className="flex items-center justify-between px-6 py-4 cursor-pointer hover:bg-orange-50/50 transition-colors bg-white"
                   onClick={() => setIsSection4Expanded(!isSection4Expanded)}
                 >
                    <h3 className="text-xl font-bold text-orange-900 flex items-center gap-2">
                       <div className="p-1.5 bg-orange-100 rounded-lg"><MoreHorizontal className="w-5 h-5 text-[#f37021]" /></div>
                       Mục 4: Khác
                       {sentRequests["other"] && (
                         <span className="ml-2 text-xs font-bold text-amber-700 bg-amber-100 px-2 py-1 rounded-md flex items-center gap-1 border border-amber-300 shadow-sm animate-in fade-in">
                            <span className="w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse"></span> Chờ xác nhận
                         </span>
                       )}
                    </h3>
                    <div className="w-8 h-8 rounded-full bg-gray-50 flex items-center justify-center text-gray-500">
                      {isSection4Expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                    </div>
                 </div>

                 <AnimatePresence>
                   {isSection4Expanded && (
                     <motion.div
                       initial={{ height: 0, opacity: 0 }}
                       animate={{ height: 'auto', opacity: 1 }}
                       exit={{ height: 0, opacity: 0 }}
                     >
                       <div className="p-6 pt-2 border-t border-gray-100 bg-white">
                         <div>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6 p-4 bg-blue-50/50 rounded-xl border border-blue-100">
                      <div>
                        <label className="block text-xs font-bold text-gray-600 mb-1">Nội dung công việc</label>
                        <textarea className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm resize-none h-[100px]" placeholder="Chi tiết..." disabled={!isSetupEditable}></textarea>
                      </div>
                      <div className="space-y-4">
                        <div>
                          <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian dự kiến</label>
                          <div className="flex flex-col xl:flex-row gap-2 w-full">
                              <input type="date" className="w-full xl:w-auto px-3 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-400 focus:border-[#004c91] transition-colors outline-none bg-white" disabled={!isSetupEditable} />
                              <div className="flex items-center gap-2 w-full">
                                <input type="time" className="flex-1 w-full min-w-[90px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-400 focus:border-[#004c91] transition-colors outline-none bg-white" disabled={!isSetupEditable} /> 
                                <span className="text-gray-400 font-bold text-xs uppercase shrink-0">Đến</span>
                                <input type="time" className="flex-1 w-full min-w-[90px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-400 focus:border-[#004c91] transition-colors outline-none bg-white" disabled={!isSetupEditable} />
                              </div>
                          </div>
                        </div>
                        <div>
                           <label className="block text-xs font-bold text-gray-600 mb-1">Phòng ban liên quan</label>
                           <select 
                             className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm bg-white"
                             value={selectedOtherDept}
                             disabled={!isSetupEditable} onChange={(e) => setSelectedOtherDept(e.target.value)}
                           >
                            <option value="">-- Chọn --</option>
                            <option value="Hành chính">Phòng Hành chính</option>
                            <option value="Các bộ môn liên quan">Các bộ môn liên quan</option>
                           </select>
                           {renderLeaderInfo(selectedOtherDept, "other")}
                        </div>
                      </div>
                    </div>
                         </div>
                       </div>
                     </motion.div>
                   )}
                 </AnimatePresence>
               </div>

                 {isSetupEditable ? (
                   <div className="flex justify-end gap-3 pt-6 border-t border-gray-100 pb-4">
                     <button 
                       onClick={() => setIsSetupEditable(false)}
                       className="px-8 py-3 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-colors shadow-sm outline-none"
                     >
                       Hủy
                     </button>
                     <button 
                       onClick={() => { setIsSetupEditable(false); setIsSetupConfirmed(true); }} className="px-8 py-3 rounded-xl font-bold text-white bg-[#10b981] hover:bg-emerald-600 transition-all shadow-md hover:shadow-lg active:scale-[0.98] flex items-center gap-2 outline-none uppercase tracking-wider"
                     >
                       <CheckCircle2 className="w-5 h-5"/>
                       Hoàn thành
                     </button>
                   </div>
                 ) : (
                     !isClosed && isSetupConfirmed && (
                       <div className="flex justify-center pt-8 pb-4 border-t border-gray-100 animate-in fade-in slide-in-from-bottom-4 duration-500">
                        <button
                          onClick={() => {
                            setCurrentStatus('Trong tiếp khách');
                            setActiveTab('during');
                          }}
                          className="px-10 py-4 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#00386b] transition-all shadow-md hover:shadow-lg active:scale-[0.98] flex items-center gap-3 outline-none text-base uppercase tracking-wider cursor-pointer font-sans"
                        >
                          Chuyển sang bước tiếp theo
                          <ArrowRight className="w-5 h-5 text-white animate-pulse" />
                        </button>
                      </div>
                    )
                  )}

             </div>
           </motion.div>
         )}
       </AnimatePresence>
      </div>

      {/* Phần 3: Album ảnh (chỉ hiển thị cho VISITOR) */}
      {isVisitor && (
        <div className="bg-white rounded-[2rem] border border-gray-200 shadow-sm overflow-hidden transition-all duration-300 mt-6">
          <div 
            className="px-8 py-6 flex items-center justify-between cursor-pointer transition-colors bg-[#00a651]"
            onClick={() => setIsAlbumExpanded(!isAlbumExpanded)}
          >
            <div>
              <h2 className="text-xl font-bold text-white border-l-4 border-white pl-3">3. Album ảnh</h2>
              <p className="text-sm font-medium text-green-100 mt-1 pl-4">Thư viện hình ảnh của chuyến tham quan</p>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-white/20 flex items-center justify-center text-white">
                {isAlbumExpanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
              </div>
            </div>
          </div>

          <AnimatePresence>
            {isAlbumExpanded && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                className="border-t border-gray-100 overflow-hidden"
              >
                <div className="p-4 sm:p-6 md:p-8 bg-white">
                  <div className="p-6 border-2 border-dashed border-gray-200 rounded-2xl flex flex-col items-center justify-center min-h-[160px] max-w-xl mx-auto bg-gray-50/50">
                    <p className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">
                      Xem toàn bộ Album ảnh trên thư mục Drive
                    </p>
                  </div>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      )}

      {/* Phần 4: Bài tin tức (chỉ hiển thị cho VISITOR) */}
      {isVisitor && (
        <div className="bg-white rounded-[2rem] border border-gray-200 shadow-sm overflow-hidden transition-all duration-300 mt-6">
          <div 
            className="px-8 py-6 flex items-center justify-between cursor-pointer transition-colors bg-[#4F46E5]"
            onClick={() => setIsNewsExpanded(!isNewsExpanded)}
          >
            <div>
              <h2 className="text-xl font-bold text-white border-l-4 border-white pl-3">4. Bài tin tức</h2>
              <p className="text-sm font-medium text-indigo-100 mt-1 pl-4">Các bài đăng và tin tức sau chuyến tham quan</p>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-white/20 flex items-center justify-center text-white">
                {isNewsExpanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
              </div>
            </div>
          </div>

          <AnimatePresence>
            {isNewsExpanded && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                className="border-t border-gray-100 overflow-hidden"
              >
                <div className="p-4 sm:p-6 md:p-8 bg-white">
                  <div className="p-6 border-2 border-dashed border-gray-200 rounded-2xl flex flex-col items-center justify-center min-h-[160px] max-w-xl mx-auto bg-gray-50/50">
                    <p className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">
                      Trải nghiệm khó quên của học sinh tại FPTU
                    </p>
                  </div>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      )}

     </div>
      )}

      {activeTab === 'during' && (
        isPrep ? (
          renderEmptyState()
        ) : (
          <VisitDuringTab isReadOnly={isClosed} isDept={isDept} />
        )
      )}

      {activeTab === 'after' && (
        (isPrep || currentStatus === 'Trong tiếp khách') ? (
          renderEmptyState()
        ) : (
          <VisitAfterTab onTourCloseSuccess={() => navigate('/dashboard/visit')} isReadOnly={isClosed} isDept={isDept && !isStudent} />
        )
      )}

      {/* Rejection Reason Modal */}
      <AnimatePresence>
        {rejectReasonModal.isOpen && (
          <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setRejectReasonModal({ isOpen: false, targetId: null, targetName: null, reasonText: '' })} />
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="bg-white rounded-2xl p-6 w-full max-w-md relative z-10 shadow-2xl border border-gray-100"
            >
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-lg font-bold text-gray-900 flex items-center gap-2">
                  <X className="w-5 h-5 text-red-500" />
                  Lý do từ chối
                </h3>
                <button 
                  onClick={() => setRejectReasonModal({ isOpen: false, targetId: null, targetName: null, reasonText: '' })}
                  className="p-1.5 text-gray-400 hover:text-gray-600 rounded-lg hover:bg-gray-100 transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>
              <p className="text-sm text-gray-600 mb-4">
                Bạn đang từ chối sự tham gia của <span className="font-bold text-[#004c91]">{String(rejectReasonModal.targetName)}</span>. Vui lòng cung cấp lý do (bắt buộc):
              </p>
              <textarea
                value={rejectReasonModal.reasonText}
                onChange={(e) => setRejectReasonModal(prev => ({ ...prev, reasonText: e.target.value }))}
                className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-red-500 focus:ring-1 focus:ring-red-500 outline-none transition-colors mb-6 text-sm resize-none"
                rows={3}
                placeholder="Nhập lý do từ chối..."
              />
              <div className="flex justify-end gap-3">
                <button 
                  onClick={() => setRejectReasonModal({ isOpen: false, targetId: null, targetName: null, reasonText: '' })}
                  className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-colors"
                >
                  Huỷ
                </button>
                <button 
                  onClick={handleConfirmReject}
                  disabled={!rejectReasonModal.reasonText.trim()}
                  className="px-5 py-2.5 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                >
                  Xác nhận từ chối
                </button>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* View Reason Modal */}
      <AnimatePresence>
        {viewReasonModal.isOpen && (
          <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setViewReasonModal({ isOpen: false, targetName: null, reasonText: '' })} />
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="bg-white rounded-2xl p-6 w-full max-w-sm relative z-10 shadow-2xl border border-gray-100"
            >
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-lg font-bold text-gray-900 flex items-center gap-2">
                  <X className="w-5 h-5 text-red-500" />
                  Từ chối tham gia
                </h3>
                <button 
                  onClick={() => setViewReasonModal({ isOpen: false, targetName: null, reasonText: '' })}
                  className="p-1.5 text-gray-400 hover:text-gray-600 rounded-lg hover:bg-gray-100 transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>
              <p className="text-sm text-gray-600 mb-3">Lý do từ chối của <span className="font-bold text-[#004c91]">{viewReasonModal.targetName}</span>:</p>
              <div className="p-4 bg-red-50 text-red-800 rounded-xl border border-red-100 text-sm italic mb-6">
                "{viewReasonModal.reasonText}"
              </div>
              <div className="flex justify-end">
                <button 
                  onClick={() => setViewReasonModal({ isOpen: false, targetName: null, reasonText: '' })}
                  className="px-5 py-2 rounded-lg font-bold text-gray-600 bg-gray-100 hover:bg-gray-200 transition-colors"
                >
                  Đóng
                </button>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

    </div>
  );
}
