/**
 * Trang VisitRequestManagement
 * Bảng kê tổng hợp và xem danh sách lịch sử yêu cầu chuyến đến đặc thù.
 */

import React, { useState } from 'react';
import { 
  Search, Plus, Clock, MapPin, CheckCircle, 
  AlertCircle, MinusCircle, Eye, Lock, Unlock, Users,
  ChevronLeft, ChevronRight, ChevronDown, Calendar,
  Check, X
} from 'lucide-react';
import { motion } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { VisitDetailsModal } from '../../../components/modals/VisitDetailsModal';

const MOCK_GUESTS_VISITOR = [
  {
    id: 101,
    name: 'Đoàn khách Tập đoàn VinFast',
    org: 'VinFast Corporation',
    time: '09:00 | 18/11/2026',
    campus: 'Hà Nội',
    host: 'Nguyễn Văn A', // This has HOST
    sender: 'Lý Quốc E',
    steps: { led: 'done', car: 'na', room: 'done', tea: 'pending' },
    status: 'Đã duyệt',
    types: ['Campus Tour', 'Họp trao đổi'],
    pax: 15,
  },
  {
    id: 102,
    name: 'Đoàn khách THPT Chuyên Hà Nội - Amsterdam',
    org: 'THPT Chuyên Hà Nội - Amsterdam',
    time: '14:30 | 19/11/2026',
    campus: 'Hà Nội',
    host: '', // This does NOT have HOST
    sender: 'Lê Thanh Bình',
    steps: { led: 'na', car: 'na', room: 'na', tea: 'na' },
    status: 'Đã duyệt',
    types: ['Campus Tour'],
    pax: 45,
  },
  {
    id: 103,
    name: 'Đoàn Thể Thao Học Đường Nhật Bản',
    org: 'Sport Association JP',
    time: '10:00 | 22/11/2026',
    campus: 'Hà Nội',
    host: '',
    sender: 'Tanaka Mitsui',
    steps: { led: 'pending', car: 'pending', room: 'pending', tea: 'pending' },
    status: 'Chờ duyệt',
    types: ['Campus Tour', 'Khác'],
    pax: 30,
  },
  {
    id: 104,
    name: 'Đoàn Nghiên Cứu Giáo Dục Na Uy',
    org: 'Education Board Norway',
    time: '08:30 | 05/10/2026',
    campus: 'Đà Nẵng',
    host: '',
    sender: 'Astrid Lindgren',
    steps: { led: 'na', car: 'na', room: 'na', tea: 'na' },
    status: 'Từ chối',
    rejectReason: 'Campus không đủ phòng hội nghị trống vào thời gian yêu cầu tiếp đón.',
    types: ['Họp trao đổi'],
    pax: 12,
  },
  {
    id: 105,
    name: 'Đoàn Đại biểu ĐH Quốc gia Singapore',
    org: 'NUS Singapore',
    time: '10:00 | 10/10/2026',
    campus: 'Hà Nội',
    host: 'Trần Thị B',
    sender: 'Wong Ka-wai',
    steps: { led: 'done', car: 'done', room: 'done', tea: 'done' },
    status: 'Đã đóng đoàn',
    types: ['Họp trao đổi', 'Campus Tour'],
    pax: 8,
  }
];

const MOCK_GUESTS_ALL = [
  {
    id: 1,
    name: 'Đoàn khách Đại học Monash',
    org: 'Monash University',
    time: '09:00 | 15/10/2026',
    campus: 'Hà Nội',
    host: 'Nguyễn Văn A',
    sender: 'Lý Quốc E',
    steps: { led: 'done', car: 'na', room: 'done', tea: 'pending' },
    status: 'Đang chuẩn bị',
    types: ['Campus Tour', 'Họp trao đổi'],
  },
  {
    id: 2,
    name: 'Đoàn Đối tác Samsung',
    org: 'Samsung R&D',
    time: '14:30 | 16/10/2026',
    campus: 'Đà Nẵng',
    host: 'Trần Thị B',
    sender: 'Hoàng Hải Y',
    steps: { led: 'done', car: 'done', room: 'done', tea: 'done' },
    status: 'Trong tiếp khách',
    types: ['Họp trao đổi'],
  },
  {
    id: 3,
    name: 'Tham quan Trường THPT Lê Lợi',
    org: 'THPT Lê Lợi',
    time: '08:00 | 18/10/2026',
    campus: 'Hà Nội',
    host: 'Lê Văn C',
    sender: 'Đinh Tuấn K',
    steps: { led: 'na', car: 'pending', room: 'na', tea: 'done' },
    status: 'Chờ đóng đoàn',
    types: ['Campus Tour', 'Khác'],
  },
  {
    id: 4,
    name: 'Đoàn khách ĐH Công Nghệ Nanyang',
    org: 'NTU Singapore',
    time: '10:00 | 20/10/2026',
    campus: 'Hồ Chí Minh',
    host: 'Phạm Văn D',
    sender: 'Mai Thị Mai',
    steps: { led: 'done', car: 'done', room: 'done', tea: 'done' },
    status: 'Đã đóng đoàn',
    types: ['Họp trao đổi'],
  },
  {
    id: 5,
    name: 'Tham quan tập đoàn Vingroup',
    org: 'Vingroup',
    time: '10:00 | 10/10/2026',
    campus: 'Hà Nội',
    host: 'Nguyễn Văn A',
    sender: 'Vũ Quốc L',
    steps: { led: 'done', car: 'done', room: 'done', tea: 'done' },
    status: 'Đã duyệt',
    types: ['Khác'],
  },
  {
    id: 6,
    name: 'Chuyến thăm Fsoft',
    org: 'FPT Software',
    time: '08:30 | 05/10/2026',
    campus: 'Đà Nẵng',
    host: 'Trần Thị B',
    sender: 'Phan Đăng Nhật',
    steps: { led: 'na', car: 'na', room: 'na', tea: 'na' },
    status: 'Từ chối',
    rejectReason: 'Trùng lịch làm việc với đoàn tiếp khách cấp cao khác tại Campus Đà Nẵng.',
    types: ['Họp trao đổi', 'Campus Tour'],
  },
  {
    id: 7,
    name: 'Đoàn khách Vinamilk',
    org: 'Vinamilk',
    time: '14:00 | 02/10/2026',
    campus: 'Hồ Chí Minh',
    host: 'Phạm Văn D',
    sender: 'Lê Cường',
    steps: { led: 'done', car: 'done', room: 'done', tea: 'done' },
    status: 'Chờ duyệt',
    types: ['Campus Tour'],
  },
  {
    id: 8,
    name: 'Đoàn khách Panasonic',
    org: 'Panasonic',
    time: '14:00 | 02/10/2026',
    campus: 'Hồ Chí Minh',
    host: 'Phạm Văn D',
    sender: 'Lê Cường',
    steps: { led: 'done', car: 'done', room: 'done', tea: 'done' },
    status: 'Đã kết thúc',
    types: ['Campus Tour'],
  }
];

export function VisitRequestManagement() {
  const navigate = useNavigate();
  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isHO = userRole === 'HO';
  const isStaff = userRole === 'STAFF';
  const isStaffLeader = isStaff && user?.subRole === 'Leader';
  const isAdmin = userRole === 'ADMIN' || isStaffLeader;
  const isStudent = userRole === 'STUDENT';
  const isDept = userRole === 'DEPT';
  const isVisitor = userRole === 'VISITOR';

  const [statusFilter, setStatusFilter] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [campusFilter, setCampusFilter] = useState('');
  const [typeFilters, setTypeFilters] = useState<string[]>([]);
  const [isTypeFilterOpen, setIsTypeFilterOpen] = useState(false);
  const [isStatusFilterOpen, setIsStatusFilterOpen] = useState(false);
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');

  // Main guests list state
  const [guestsList, setGuestsList] = useState<any[]>(() => {
    if (isVisitor) return MOCK_GUESTS_VISITOR;
    if (isDept || isStudent) return MOCK_GUESTS_ALL.slice(0, 4);
    return MOCK_GUESTS_ALL;
  });

  // Pagination State
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);

  // Reject Modal State
  const [isRejectModalOpen, setIsRejectModalOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [selectedGuestForReject, setSelectedGuestForReject] = useState<any>(null);

  // Rejection Reason Detail Modal State
  const [isReasonModalOpen, setIsReasonModalOpen] = useState(false);
  const [selectedGuestForReason, setSelectedGuestForReason] = useState<any>(null);

  // View Modal State
  const [isViewModalOpen, setIsViewModalOpen] = useState(false);
  const [selectedGuestForView, setSelectedGuestForView] = useState<any>(null);

  const handleGuestView = (guest: any) => {
    if (isVisitor) {
      if (guest.host && (guest.status === 'Đã duyệt' || guest.status === 'Đã đóng đoàn' || guest.status === 'Đang chuẩn bị' || guest.status === 'Trong tiếp khách' || guest.status === 'Chờ đóng đoàn' || guest.status === 'Đã kết thúc')) {
        navigate(`/dashboard/visit/reception-detail/${guest.id}`);
      } else {
        setSelectedGuestForView(guest);
        setIsViewModalOpen(true);
      }
      return;
    }

    if (guest.status === 'Chờ duyệt' || guest.status === 'Từ chối' || (isStaffLeader && guest.status === 'Đã duyệt')) {
      setSelectedGuestForView(guest);
      setIsViewModalOpen(true);
    } else if (isHO) {
      navigate(`/dashboard/visit/ho-detail/${guest.id}`, { state: { guestData: guest } });
    } else if (guest.status === 'Đang chuẩn bị') {
      navigate(`/dashboard/visit/process/${guest.id}`, { state: { isPrep: true, status: guest.status, isReadOnly: isStaffLeader } });
    } else if (guest.status === 'Trong tiếp khách') {
      navigate(`/dashboard/visit/process/${guest.id}`, { state: { defaultTab: 'during', status: guest.status, isReadOnly: isStaffLeader } });
    } else if (guest.status === 'Chờ đóng đoàn') {
      navigate(`/dashboard/visit/process/${guest.id}`, { state: { defaultTab: 'after', status: guest.status, isReadOnly: isStaffLeader } });
    } else if (guest.status === 'Đã đóng đoàn' || guest.status === 'Đã kết thúc') {
      navigate(`/dashboard/visit/process/${guest.id}`, { state: { defaultTab: 'before', status: guest.status, isReadOnly: isStaffLeader } });
    } else {
      setSelectedGuestForView(guest);
      setIsViewModalOpen(true);
    }
  };

  // Forward Modal State (HO role)
  const [isForwardModalOpen, setIsForwardModalOpen] = useState(false);
  const [selectedGuestForForward, setSelectedGuestForForward] = useState<any>(null);
  const [forwardCampuses, setForwardCampuses] = useState<string[]>(['Hà Nội']);

  // Count requests
  const pendingCount = guestsList.filter(g => g.status === 'Chờ duyệt').length;

  const getFilteredGuests = () => {
    let filtered = guestsList;
    
    if (statusFilter) {
      if (isHO) {
        if (statusFilter === 'Đã đóng đoàn' || statusFilter === 'Đã duyệt') {
          filtered = filtered.filter(g => g.status !== 'Chờ duyệt' && g.status !== 'Từ chối');
        } else {
          filtered = filtered.filter(g => g.status === statusFilter);
        }
      } else if (isStaffLeader) {
        filtered = filtered.filter(g => g.status === statusFilter);
      } else {
        filtered = filtered.filter(g => g.status === statusFilter);
      }
    }

    if (searchQuery) {
      filtered = filtered.filter(g => 
        g.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
        g.host.toLowerCase().includes(searchQuery.toLowerCase()) ||
        g.org.toLowerCase().includes(searchQuery.toLowerCase())
      );
    }

    if (campusFilter) {
      filtered = filtered.filter(g => g.campus === campusFilter);
    }

    if (typeFilters.length > 0) {
      filtered = filtered.filter(g => g.types.some(t => typeFilters.includes(t)));
    }

    return filtered;
  };

  const guests = getFilteredGuests();

  // Pagination calculation
  const totalPages = Math.max(1, Math.ceil(guests.length / pageSize));
  const paginatedGuests = guests.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  const getRowClassName = (campus: string) => {
    return 'border-l-2 border-transparent';
  };

  const getStatusBadge = (guest: any) => {
    const status = guest.status;
    
    if (isHO) {
      if (status === 'Chờ duyệt') {
        return <span className="bg-yellow-50 text-yellow-700 border border-yellow-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Chờ duyệt</span>;
      }
      if (status === 'Từ chối') {
        return <span className="bg-red-50 text-red-700 border border-red-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Từ chối</span>;
      }
      return <span className="bg-cyan-50 text-cyan-700 border border-cyan-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Đã duyệt</span>;
    }

    if (status === 'Đã duyệt') {
       return <span className="bg-cyan-50 text-cyan-700 border border-cyan-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Đã duyệt</span>;
    }

    if (status === 'Đang chuẩn bị') {
      return <span className="bg-blue-50 text-blue-700 border border-blue-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Trước tiếp khách</span>;
    }

    switch(status) {
      case 'Chờ duyệt':
        return <span className="bg-yellow-50 text-yellow-700 border border-yellow-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Chờ duyệt</span>;
      case 'Đang chuẩn bị':
        return <span className="bg-blue-50 text-blue-700 border border-blue-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Trước tiếp khách</span>;
      case 'Trong tiếp khách':
        return <span className="bg-green-50 text-green-700 border border-green-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Trong tiếp khách</span>;
      case 'Chờ đóng đoàn':
        return <span className="bg-orange-50 text-orange-700 border border-orange-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Chờ đóng đoàn</span>;
      case 'Đã đóng đoàn':
      case 'Đã kết thúc':
        return <span className="bg-slate-100 text-slate-700 border border-slate-300 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Đã đóng đoàn</span>;
      case 'Từ chối':
        return <span className="bg-red-50 text-red-700 border border-red-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">Từ chối</span>;
      default:
        return <span className="bg-gray-100 text-gray-700 border border-gray-200 px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap">{status}</span>;
    }
  };

  const renderStepIcon = (state: string) => {
    if (state === 'done') return <CheckCircle className="w-4 h-4 text-green-500 flex-shrink-0" />;
    if (state === 'pending') return <AlertCircle className="w-4 h-4 text-red-500 flex-shrink-0" />;
    return <MinusCircle className="w-4 h-4 text-gray-400 flex-shrink-0" />;
  };

  const getTypeColor = (type: string) => {
    switch (type) {
      case 'Campus Tour':
        return 'bg-orange-50 text-orange-600 border-orange-200';
      case 'Họp trao đổi':
        return 'bg-emerald-50 text-emerald-700 border-emerald-200';
      default:
        return 'bg-purple-50 text-purple-700 border-purple-200';
    }
  };

  const isCloseButtonActive = (guest: any) => {
    // A simplified logic indicating the active state of close button
    return guest.status === 'Chờ đóng đoàn' && 
           Object.values(guest.steps).every(s => s === 'done' || s === 'na');
  };

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto flex flex-col space-y-6 pb-12 animate-in fade-in duration-300">
      
      {/* 1. Header & Navigation Layer */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Quản lý tiếp khách</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý tiếp khách</h1>
        {!isHO && !isStaffLeader && !isDept && !isStudent && !isVisitor ? (
          <button 
            onClick={() => navigate('/dashboard/visit/create')}
            className="flex items-center justify-center gap-2 bg-[#F37021] hover:bg-orange-600 outline-none text-white px-4 py-2 rounded-lg font-bold shadow-sm transition-colors whitespace-nowrap w-full md:w-auto"
          >
            <Plus className="w-5 h-5" /> Tạo đoàn khách
          </button>
        ) : isHO ? (
          <button 
            onClick={() => navigate('/dashboard/visit/agenda-templates')}
            className="flex items-center justify-center gap-2 bg-[#F37021] hover:bg-orange-600 outline-none text-white px-4 py-2 rounded-lg font-bold shadow-sm transition-colors whitespace-nowrap w-full md:w-auto"
          >
            <Plus className="w-5 h-5" /> Quản lý mẫu Agenda
          </button>
        ) : null}
      </div>

      {/* 2. Action & Filter Controller */}
      <div className="w-full mb-6">
        <div className="flex flex-col md:flex-row gap-4 w-full items-center">
          <div className="relative w-full flex-1">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-5 h-5" />
            <input 
              type="text" 
              placeholder="Tìm kiếm tên đoàn, host, đối tác..." 
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-10 pr-4 py-2 bg-white border border-gray-300 rounded-lg text-sm font-medium focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] transition-colors"
            />
          </div>
          
          {/* Time Filter */}
          <div className="flex items-center justify-between gap-1 bg-white border border-gray-300 rounded-lg px-3 py-2 h-[40px] w-full md:w-auto">
            <Calendar className="w-4 h-4 text-gray-400" />
            <input 
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              className="text-sm font-medium text-gray-700 bg-transparent border-none focus:outline-none focus:ring-0 max-w-[110px]"
            />
            <span className="text-sm font-medium text-gray-500">-</span>
            <input 
              type="date"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
              className="text-sm font-medium text-gray-700 bg-transparent border-none focus:outline-none focus:ring-0 max-w-[110px]"
            />
          </div>
          
          {/* Removed Campus Filter for HO */}

          
          {/* Status Filter */}
          <div className="relative w-full md:max-w-[180px]">
            <button
              onClick={() => setIsStatusFilterOpen(!isStatusFilterOpen)}
              className="bg-white border border-gray-300 rounded-lg w-full h-[40px] px-3 py-2 text-sm font-medium text-gray-700 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] flex items-center justify-between outline-none transition-colors cursor-pointer"
            >
              <span className="truncate">
                {statusFilter === '' && 'Tất cả trạng thái'}
                {statusFilter === 'Chờ duyệt' && 'Chờ duyệt'}
                {statusFilter === 'Từ chối' && 'Từ chối'}
                {statusFilter === 'Đã duyệt' && 'Đã duyệt'}
                {(!isHO) && statusFilter === 'Đang chuẩn bị' && 'Trước tiếp khách'}
                {(!isHO) && statusFilter === 'Trong tiếp khách' && 'Trong tiếp khách'}
                {(!isHO) && statusFilter === 'Chờ đóng đoàn' && 'Chờ đóng đoàn'}
                {(!isHO) && statusFilter === 'Đã đóng đoàn' && 'Đã đóng đoàn'}
                {(!isHO) && statusFilter === 'Đã kết thúc' && 'Đã kết thúc'}
              </span>
              <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
            </button>

            {isStatusFilterOpen && (
              <>
                <div className="fixed inset-0 z-10" onClick={() => setIsStatusFilterOpen(false)} />
                <div className="absolute top-full right-0 md:left-0 mt-1 w-48 bg-white border border-gray-200 rounded-lg shadow-lg z-20 py-1 font-sans">
                  {(isHO ? [
                    { value: '', label: 'Tất cả trạng thái' },
                    { value: 'Chờ duyệt', label: 'Chờ duyệt' },
                    { value: 'Đã duyệt', label: 'Đã duyệt' },
                    { value: 'Từ chối', label: 'Từ chối' }
                  ] : isStaffLeader ? [
                    { value: '', label: 'Tất cả trạng thái' },
                    { value: 'Chờ duyệt', label: 'Chờ duyệt' },
                    { value: 'Đã duyệt', label: 'Đã duyệt' },
                    { value: 'Từ chối', label: 'Từ chối' },
                    { value: 'Đang chuẩn bị', label: 'Trước tiếp khách' },
                    { value: 'Trong tiếp khách', label: 'Trong tiếp khách' },
                    { value: 'Chờ đóng đoàn', label: 'Chờ đóng đoàn' },
                    { value: 'Đã đóng đoàn', label: 'Đã đóng đoàn' }
                  ] : isVisitor ? [
                    { value: '', label: 'Tất cả trạng thái' },
                    { value: 'Chờ duyệt', label: 'Chờ duyệt' },
                    { value: 'Đã duyệt', label: 'Đã duyệt' },
                    { value: 'Từ chối', label: 'Từ chối' },
                    { value: 'Đã đóng đoàn', label: 'Đã đóng đoàn' }
                  ] : (isDept || isStudent) ? [
                    { value: '', label: 'Tất cả trạng thái' },
                    { value: 'Đang chuẩn bị', label: 'Trước tiếp khách' },
                    { value: 'Trong tiếp khách', label: 'Trong tiếp khách' },
                    { value: 'Chờ đóng đoàn', label: 'Chờ đóng đoàn' },
                    { value: 'Đã đóng đoàn', label: 'Đã đóng đoàn' }
                  ] : [
                    { value: '', label: 'Tất cả trạng thái' },
                    { value: 'Chờ duyệt', label: 'Chờ duyệt' },
                    { value: 'Đã duyệt', label: 'Đã duyệt' },
                    { value: 'Từ chối', label: 'Từ chối' },
                    { value: 'Đang chuẩn bị', label: 'Trước tiếp khách' },
                    { value: 'Trong tiếp khách', label: 'Trong tiếp khách' },
                    { value: 'Chờ đóng đoàn', label: 'Chờ đóng đoàn' },
                    { value: 'Đã đóng đoàn', label: 'Đã đóng đoàn' }
                  ]).map(option => (
                    <div 
                      key={option.value}
                      className={`px-3 py-2 text-sm cursor-pointer hover:bg-slate-50 flex items-center ${statusFilter === option.value ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-gray-700 font-medium'}`}
                      onClick={() => {
                        setStatusFilter(option.value);
                        setIsStatusFilterOpen(false);
                      }}
                    >
                      {option.label}
                      {statusFilter === option.value && <Check className="w-4 h-4 ml-auto text-[#004c91]" />}
                    </div>
                  ))}
                </div>
              </>
            )}
          </div>
          
          <div className="relative w-full md:max-w-[180px]">
            <button
              onClick={() => setIsTypeFilterOpen(!isTypeFilterOpen)}
              className="bg-white border border-gray-300 rounded-lg w-full h-[40px] px-3 py-2 text-sm font-medium text-gray-700 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] flex items-center justify-between outline-none transition-colors cursor-pointer"
            >
              <span className="truncate">
                {typeFilters.length === 0 ? 'Tất cả loại hình' : `${typeFilters.length} loại hình đã chọn`}
              </span>
              <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
            </button>

            {isTypeFilterOpen && (
              <>
                <div className="fixed inset-0 z-10" onClick={() => setIsTypeFilterOpen(false)} />
                <div className="absolute top-full left-0 mt-1 w-48 bg-white border border-gray-200 rounded-lg shadow-lg z-20 py-1">
                  {['Campus Tour', 'Họp trao đổi', 'Khác'].map(type => (
                    <label key={type} className="flex items-center px-3 py-2 hover:bg-slate-50 cursor-pointer">
                      <input 
                        type="checkbox"
                        className="mr-2 rounded border-gray-300 text-[#004c91] focus:ring-[#004c91]"
                        checked={typeFilters.includes(type)}
                        onChange={(e) => {
                          if (e.target.checked) {
                            setTypeFilters([...typeFilters, type]);
                          } else {
                            setTypeFilters(typeFilters.filter(t => t !== type));
                          }
                        }}
                      />
                      <span className="text-sm font-medium text-gray-700">{type}</span>
                    </label>
                  ))}
                </div>
              </>
            )}
          </div>
        </div>
      </div>

      {/* 4. Main Data Table */}
      <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden flex flex-col">
        <div className="overflow-x-auto w-full">
          <table className="w-full text-left border-collapse min-w-[1050px]">
            <thead className="bg-[#004c91] text-white">
              <tr>
                <th className="p-2 sm:p-3 text-[12px] font-bold text-center uppercase tracking-wider whitespace-nowrap w-[60px]">STT</th>
                <th className="p-2 sm:p-3 text-[12px] font-bold text-left uppercase tracking-wider whitespace-nowrap">Tên đoàn khách</th>
                <th className="p-2 sm:p-3 text-[12px] font-bold text-left uppercase tracking-wider whitespace-nowrap">Thời gian</th>
                {!isHO && <th className="p-2 sm:p-3 text-[12px] font-bold text-center uppercase tracking-wider whitespace-nowrap">HOST</th>}
                <th className="p-2 sm:p-3 text-[12px] font-bold text-center uppercase tracking-wider whitespace-nowrap">Loại tham quan</th>
                <th className="p-2 sm:p-3 text-[12px] font-bold text-center uppercase tracking-wider whitespace-nowrap">Trạng thái</th>
                <th className="p-2 sm:p-3 text-[12px] font-bold text-center uppercase tracking-wider whitespace-nowrap w-[120px]">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {paginatedGuests.length > 0 ? paginatedGuests.map((guest, index) => {
                const isActive = isCloseButtonActive(guest);
                return (
                  <tr 
                    key={guest.id} 
                    className={`transition-colors duration-200 cursor-pointer ${getRowClassName(guest.campus)} hover:bg-blue-50 group`}
                    onClick={() => handleGuestView(guest)}
                  >
                    <td className="py-2.5 px-3 text-center font-bold text-[#004c91] text-sm">{(currentPage - 1) * pageSize + index + 1}</td>
                    <td className="py-2.5 px-3">
                      <p className="text-sm font-bold text-[#004c91] transition-colors">{guest.name}</p>
                    </td>
                    <td className="py-2.5 px-3">
                      <div className="flex items-center text-sm font-medium text-slate-700 whitespace-nowrap">
                        {guest.time}
                      </div>
                    </td>
                    {!isHO && (
                      <td className="py-2.5 px-3 text-center">
                        <div className="flex flex-col items-center justify-center">
                          {isVisitor ? (
                            guest.host ? (
                              <span className="font-bold text-[#004c91] text-sm whitespace-nowrap">{guest.host}</span>
                            ) : guest.status === 'Đã duyệt' ? (
                              <span className="text-xs text-[#004c91] font-medium whitespace-nowrap">Đang phân công<br/>người đón tiếp</span>
                            ) : (
                              <span className="font-bold text-gray-400 text-sm whitespace-nowrap">-</span>
                            )
                          ) : (guest.status !== 'Chờ duyệt' && guest.status !== 'Đã duyệt') ? (
                            <span className="font-bold text-[#004c91] text-sm whitespace-nowrap">{guest.host || guest.sender}</span>
                          ) : (
                            <span className="font-bold text-gray-400 text-sm whitespace-nowrap">-</span>
                          )}
                        </div>
                      </td>
                    )}
                    <td className="py-2.5 px-3 text-center">
                      <div className="flex flex-wrap items-center justify-center gap-1">
                        {guest.types.map((type, tIdx) => (
                          <span key={tIdx} className="text-xs font-medium text-slate-600 whitespace-nowrap">
                            {type}{tIdx < guest.types.length - 1 ? ', ' : ''}
                          </span>
                        ))}
                      </div>
                    </td>
                    <td className="py-2.5 px-3 text-center">
                      <div className="flex flex-col items-center justify-center gap-1">
                        {getStatusBadge(guest)}
                        {guest.status === 'Đã duyệt' && isStaff && !isStaffLeader && (
                          <div 
                            className="text-[11px] text-[#004c91] underline cursor-pointer hover:text-[#00386b] font-medium transition-colors"
                            onClick={(e) => {
                              e.stopPropagation();
                              navigate('/dashboard/visit/create', { state: { guestData: guest } });
                            }}
                          >
                            Nhận tiếp đón
                          </div>
                        )}
                      </div>
                    </td>
                    <td className="py-2.5 px-3">
                      <div className="flex items-center justify-center gap-2">
                        {guest.status === 'Chờ duyệt' && (
                          <>
                            <button 
                              className="p-2 text-slate-400 hover:text-[#004c91] hover:bg-blue-50 rounded-lg transition-colors outline-none cursor-pointer" 
                              title="Xem chi tiết"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleGuestView(guest);
                              }}
                            >
                              <Eye className="w-5 h-5" />
                            </button>
                            {(isHO || isStaffLeader || isAdmin) && (
                              <>
                                <button 
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    if (isHO) {
                                      setSelectedGuestForForward(guest);
                                      setIsForwardModalOpen(true);
                                    } else if (isStaffLeader) {
                                      setGuestsList(prev => prev.map(g => g.id === guest.id ? { ...g, status: 'Đã duyệt' } : g));
                                    }
                                  }}
                                  className="p-2 text-green-500 hover:text-green-600 hover:bg-green-50 rounded-lg transition-colors outline-none cursor-pointer" 
                                  title={isHO ? "Chuyển tiếp" : "Phê duyệt"}
                                >
                                  <Check className="w-5 h-5" />
                                </button>
                                <button 
                                  className="p-2 text-red-500 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors outline-none cursor-pointer" 
                                  title="Từ chối"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setSelectedGuestForReject(guest);
                                    setIsRejectModalOpen(true);
                                  }}
                                >
                                  <X className="w-5 h-5" />
                                </button>
                              </>
                            )}
                          </>
                        )}
                        
                        {(guest.status === 'Đã duyệt' || guest.status === 'Đang chuẩn bị' || guest.status === 'Trong tiếp khách' || guest.status === 'Chờ đóng đoàn' || guest.status === 'Đã đóng đoàn' || guest.status === 'Đã kết thúc') && (
                          <button 
                            className="p-2 text-slate-400 hover:text-[#004c91] hover:bg-[#ebf5ff] rounded-lg transition-colors outline-none cursor-pointer" 
                            title={isHO ? "Xem chi tiết tiếp khách" : isStaffLeader ? "Chi tiết quy trình" : (guest.status === 'Chờ đóng đoàn' ? "Xử lý kết thúc đoàn" : "Xem chi tiết / Xử lý quy trình")}
                            onClick={(e) => {
                              e.stopPropagation();
                              handleGuestView(guest);
                            }}
                          >
                            <Eye className="w-5 h-5" />
                          </button>
                        )}
                        
                        {guest.status === 'Từ chối' && (
                          <div className="flex items-center gap-2">
                            <button 
                              className="p-2 text-slate-400 hover:text-[#004c91] hover:bg-blue-50 rounded-lg transition-colors outline-none cursor-pointer" 
                              title="Xem chi tiết"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleGuestView(guest);
                              }}
                            >
                              <Eye className="w-5 h-5" />
                            </button>
                            <button
                              type="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                setSelectedGuestForReason(guest);
                                setIsReasonModalOpen(true);
                              }}
                              className="text-xs text-[#004c91] hover:text-[#00386b] underline cursor-pointer font-normal whitespace-nowrap px-1"
                            >
                              Xem lý do
                            </button>
                          </div>
                        )}
                        
                      </div>
                    </td>
                  </tr>
                );
              }) : (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-slate-500 font-medium">
                    <div className="flex flex-col items-center justify-center">
                      <Users className="w-12 h-12 text-slate-300 mb-3" />
                      <p>Không có đoàn khách nào.</p>
                    </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        
        {/* Pagination */}
        {guests.length > 0 && (
          <div className="p-6 border-t border-gray-100 flex flex-col sm:flex-row items-center justify-between gap-4 bg-gray-50/50">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-gray-500">Hiển thị</span>
              <div className="relative">
                <select
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value));
                    setCurrentPage(1);
                  }}
                  className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-bold text-gray-700 bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 appearance-none min-w-[70px] text-left"
                >
                  <option value={5}>5</option>
                  <option value={10}>10</option>
                  <option value={20}>20</option>
                  <option value={50}>50</option>
                </select>
                <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
              </div>
              <span className="text-sm font-medium text-gray-500">bản ghi / trang</span>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                disabled={currentPage === 1}
                className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <div className="flex items-center gap-1">
                {Array.from({ length: totalPages }, (_, i) => i + 1).map(page => (
                  <button
                    key={page}
                    onClick={() => setCurrentPage(page)}
                    className={`w-8 h-8 rounded-lg text-sm font-bold transition-all outline-none ${currentPage === page ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:bg-gray-200'}`}
                  >
                    {page}
                  </button>
                ))}
              </div>
              <button
                onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                disabled={currentPage === totalPages}
                className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Reject Modal */}
      {isRejectModalOpen && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <motion.div 
            initial={{ opacity: 0, scale: 0.95, y: 10 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden relative"
          >
            <div className="px-6 py-4 bg-red-600 flex items-center justify-between">
              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                <AlertCircle className="w-5 h-5 bg-white/20 rounded-full p-0.5" /> Từ chối đoàn khách
              </h3>
              <button 
                onClick={() => setIsRejectModalOpen(false)}
                className="text-white/80 hover:text-white transition-colors hover:bg-white/10 rounded-full p-1.5"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6">
              <p className="text-sm text-gray-700 mb-4">
                Vui lòng cung cấp lý do từ chối đoàn khách <span className="font-bold text-[#004c91]">{selectedGuestForReject?.name}</span>:
              </p>
              <textarea
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
                placeholder="Nhập lý do chi tiết để thông báo cho host..."
                className="w-full px-4 py-3 rounded-2xl border border-gray-200 focus:border-red-500 focus:ring-4 focus:ring-red-500/10 outline-none transition-all text-sm min-h-[140px] resize-none bg-gray-50/50 hover:bg-gray-50 focus:bg-white"
              />
            </div>
            
            <div className="px-6 py-5 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
              <button 
                onClick={() => setIsRejectModalOpen(false)}
                className="px-6 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:border-gray-300 hover:bg-gray-50 transition-all outline-none text-sm"
              >
                Hủy bỏ
              </button>
              <button 
                onClick={() => {
                  // Handle reject logic here
                  if (selectedGuestForReject) {
                    setGuestsList(p => p.map(g => 
                      g.id === selectedGuestForReject.id 
                        ? { ...g, status: 'Từ chối', rejectReason: rejectReason } 
                        : g
                    ));
                  }
                  setIsRejectModalOpen(false);
                  setRejectReason('');
                }}
                disabled={!rejectReason.trim()}
                className="px-6 py-2.5 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 shadow-sm transition-all outline-none text-sm disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
              >
                Từ chối
              </button>
            </div>
          </motion.div>
        </div>
      )}

      {/* View Modal */}
      <VisitDetailsModal 
        isOpen={isViewModalOpen} 
        onClose={() => setIsViewModalOpen(false)} 
        guest={selectedGuestForView} 
      />

      {/* Rejection Reason Modal */}
      {isReasonModalOpen && selectedGuestForReason && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <motion.div 
            initial={{ opacity: 0, scale: 0.95, y: 10 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden relative border border-gray-100"
          >
            <div className="px-6 py-4 bg-[#004c91] flex items-center justify-between">
              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                <AlertCircle className="w-5 h-5 bg-white/20 rounded-full p-0.5" /> Lý do từ chối yêu cầu
              </h3>
              <button 
                type="button"
                onClick={() => {
                  setIsReasonModalOpen(false);
                  setSelectedGuestForReason(null);
                }}
                className="text-white/85 hover:text-white transition-colors hover:bg-white/10 rounded-full p-1.5 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6 space-y-4 text-left">
              <div>
                <span className="text-[11px] uppercase tracking-wider font-bold text-gray-400">Đoàn khách</span>
                <p className="text-base font-bold text-slate-800 mt-0.5">{selectedGuestForReason.name}</p>
              </div>
              
              <div>
                <span className="text-[11px] uppercase tracking-wider font-bold text-gray-400">Đơn vị / Đối tác</span>
                <p className="text-sm font-semibold text-slate-600 mt-0.5">{selectedGuestForReason.org}</p>
              </div>

              <div>
                <span className="text-[11px] uppercase tracking-wider font-bold text-gray-400">Thời gian yêu cầu</span>
                <p className="text-sm font-semibold text-slate-600 mt-0.5">{selectedGuestForReason.time}</p>
              </div>

              <div className="bg-red-50 rounded-2xl p-4 border border-red-100 text-left">
                <span className="text-xs font-black text-red-600 uppercase tracking-wide block mb-1">Chi tiết lý do từ chối:</span>
                <p className="text-sm font-semibold text-red-950 leading-relaxed italic">
                  "{selectedGuestForReason.rejectReason || 'Không có lý do chi tiết được cung cấp.'}"
                </p>
              </div>
            </div>
            
            <div className="px-6 py-4 bg-gray-50 flex items-center justify-end border-t border-gray-100">
              <button 
                type="button"
                onClick={() => {
                  setIsReasonModalOpen(false);
                  setSelectedGuestForReason(null);
                }}
                className="px-6 py-2 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#00386b] shadow-sm transition-all outline-none text-sm cursor-pointer"
              >
                Đóng
              </button>
            </div>
          </motion.div>
        </div>
      )}
      
      {/* Forward Modal */}
      {isForwardModalOpen && selectedGuestForForward && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/40 backdrop-blur-sm">
          <motion.div 
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="bg-white rounded-3xl w-full max-w-md shadow-2xl overflow-hidden"
          >
            <div className="px-6 py-5 bg-[#004c91] text-white flex items-center justify-between">
              <h3 className="text-lg font-bold">Chuyển tiếp đơn tham quan</h3>
              <button 
                onClick={() => {
                  setIsForwardModalOpen(false);
                  setSelectedGuestForForward(null);
                }}
                className="text-white/70 hover:text-white transition-colors outline-none cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6 space-y-4">
              <div>
                <span className="text-[11px] uppercase tracking-wider font-bold text-gray-500">Tên đoàn khách</span>
                <p className="text-base font-bold text-slate-800 mt-0.5">{selectedGuestForForward.name}</p>
              </div>
              
              <div className="relative">
                <label className="block text-[11px] uppercase tracking-wider font-bold text-gray-500 mb-2">Cơ sở xử lý</label>
                <div className="flex flex-wrap gap-2">
                  {['Hà Nội', 'Đà Nẵng', 'Cần Thơ', 'Hồ Chí Minh', 'Quy Nhơn'].map(campus => {
                    const isSelected = forwardCampuses.includes(campus);
                    return (
                      <button
                        key={campus}
                        onClick={() => {
                          if (isSelected) {
                            setForwardCampuses(forwardCampuses.filter(c => c !== campus));
                          } else {
                            setForwardCampuses([...forwardCampuses, campus]);
                          }
                        }}
                        className={`px-3 py-1.5 rounded-lg border text-sm font-medium transition-colors ${isSelected ? 'border-[#f37021] bg-orange-50 text-[#f37021]' : 'border-gray-200 bg-white text-gray-700 hover:border-[#f37021]/50'}`}
                      >
                        {campus}
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>
            
            <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
              <button 
                type="button"
                onClick={() => {
                  setIsForwardModalOpen(false);
                  setSelectedGuestForForward(null);
                }}
                className="px-5 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer"
              >
                Hủy
              </button>
              <button 
                type="button"
                onClick={() => {
                  setIsForwardModalOpen(false);
                  setSelectedGuestForForward(null);
                }}
                className="px-6 py-2 rounded-xl font-bold text-white bg-[#f37021] hover:bg-[#d65e15] shadow-sm hover:shadow transition-all outline-none text-sm cursor-pointer"
              >
                Gửi đi
              </button>
            </div>
          </motion.div>
        </div>
      )}
      
    </div>
  );
}
