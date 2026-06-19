/**
 * Trang VisitRequestManagement
 * Bảng kê tổng hợp và xem danh sách lịch sử yêu cầu chuyến đến đặc thù.
 */

import React, { useState } from 'react';
import { 
  Search, Plus, Clock, MapPin, CheckCircle, 
  AlertCircle, MinusCircle, Eye, Lock, Unlock, Users,
  ChevronLeft, ChevronRight, ChevronDown, Calendar,
  Check, X, XCircle
} from 'lucide-react';
import { motion } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { VisitDetailsModal } from '../../../components/modals/VisitDetailsModal';
import { canCancelInstance, VisitRequestStatus, VisitInstanceStatus } from '../../../features/delegations/types/delegations.types';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';

type ActionIconButtonProps = {
  title: string;
  icon: React.ReactNode;
  tone?: 'blue' | 'green' | 'red' | 'gray' | 'orange';
  disabled?: boolean;
  onClick: (event: React.MouseEvent<HTMLButtonElement>) => void;
};

const ActionIconButton = ({
  title,
  icon,
  tone = 'blue',
  disabled = false,
  onClick,
}: ActionIconButtonProps) => {
  const toneClassMap: Record<string, string> = {
    blue: 'text-slate-500 hover:text-[#004c91] hover:bg-blue-50',
    green: 'text-green-500 hover:text-green-600 hover:bg-green-50',
    red: 'text-red-500 hover:text-red-600 hover:bg-red-50',
    gray: 'text-slate-400 hover:text-slate-600 hover:bg-slate-100',
    orange: 'text-orange-500 hover:text-orange-600 hover:bg-orange-50',
  };

  return (
    <button
      type="button"
      title={title}
      aria-label={title}
      disabled={disabled}
      onClick={onClick}
      className={`inline-flex h-9 w-9 items-center justify-center rounded-lg transition-colors outline-none cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed ${toneClassMap[tone]}`}
    >
      {icon}
    </button>
  );
};

const ActionSlot = ({ children }: { children?: React.ReactNode }) => {
  return (
    <div className="flex h-9 w-9 items-center justify-center">
      {children ?? <span className="invisible h-9 w-9" />}
    </div>
  );
};



export function VisitRequestManagement() {
  const navigate = useNavigate();
  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const currentUserId = String(user?.userId ?? user?.id ?? user?.user_id ?? '');
  const userRole = user?.role?.toUpperCase();
  const isSystemAdmin = userRole === 'ADMIN';
  const isHO = userRole === 'HO';
  const isStaff = userRole === 'STAFF';
  const isStaffLeader = isStaff && user?.subRole === 'Leader';
  const isRegularStaff = isStaff && user?.subRole !== 'Leader';
  const isVisitor = userRole === 'VISITOR';
  const isStudent = userRole === 'STUDENT';
  const isDept = userRole === 'DEPT';

  const emptyFilters = {
    keyword: '',
    status: '',
    visitScopes: [] as string[],
    fromDate: '',
    toDate: '',
  };
  const [draftFilters, setDraftFilters] = useState(emptyFilters);
  const [appliedFilters, setAppliedFilters] = useState(emptyFilters);
  const [filterError, setFilterError] = useState<string | null>(null);
  const [listError, setListError] = useState<string | null>(null);
  
  const [isTypeFilterOpen, setIsTypeFilterOpen] = useState(false);
  const [isStatusFilterOpen, setIsStatusFilterOpen] = useState(false);
  const [isDateFilterOpen, setIsDateFilterOpen] = useState(false);

  const formatDateOnly = (dateStr: string) => {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    return `${day}/${month}/${year}`;
  };

  const handleApplyFilters = () => {
    if (draftFilters.fromDate && draftFilters.toDate && draftFilters.fromDate > draftFilters.toDate) {
      setFilterError('Từ ngày không được lớn hơn Đến ngày.');
      return;
    }
    setFilterError(null);
    setAppliedFilters(draftFilters);
    setCurrentPage(1);
  };

  const handleResetFilters = () => {
    setDraftFilters(emptyFilters);
    setAppliedFilters(emptyFilters);
    setFilterError(null);
    setCurrentPage(1);
  };

  // Main guests list state
  const [guestsList, setGuestsList] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [totalGuests, setTotalGuests] = useState(0);

  // Pagination State
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10); // Default to 10 for backend

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

  // Cancel Modal State
  const [isCancelModalOpen, setIsCancelModalOpen] = useState(false);
  const [cancellationReason, setCancellationReason] = useState('');
  const [selectedGuestForCancel, setSelectedGuestForCancel] = useState<any>(null);
  const [isCancelling, setIsCancelling] = useState(false);
  const [cancelError, setCancelError] = useState<string | null>(null);

  const formatDateTime = (value?: string | null) => {
    if (!value) return '-';
    return new Date(value).toLocaleString('vi-VN', {
      hour: '2-digit',
      minute: '2-digit',
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  };

  const formatDateTimeShort = (value?: string | null) => {
    if (!value) return '-';
    return new Date(value).toLocaleString('vi-VN', {
      hour: '2-digit',
      minute: '2-digit',
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  };

  const getVietnameseStatus = (reqStatus: string, campStatus?: string) => {
    if (campStatus === 'CANCELLED') return 'Đã hủy';
    if (reqStatus === 'CANCELLED') return 'Đã hủy';
    if (reqStatus === 'PENDING_APPROVAL') return 'Chờ duyệt';
    if (reqStatus === 'REJECTED') return 'Từ chối';
    if (reqStatus === 'APPROVED') {
      if (!campStatus || campStatus === 'WAITING_REQUEST_APPROVAL') return 'Chờ phân công';
      if (campStatus === 'ASSIGNED') return 'Đã duyệt';
      if (campStatus === 'BEFORE_VISIT') return 'Trước tiếp khách';
      if (campStatus === 'DURING_VISIT') return 'Trong tiếp khách';
      if (campStatus === 'AFTER_VISIT') return 'Chờ đóng đoàn';
      if (campStatus === 'CLOSED') return 'Đã đóng đoàn';
    }
    return reqStatus;
  };

  const STATUS_FILTER_OPTIONS = [
    { value: '', label: 'Tất cả trạng thái' },
    { value: 'PENDING_APPROVAL', label: 'Chờ duyệt', requestStatus: 'PENDING_APPROVAL' },
    { value: 'REJECTED', label: 'Từ chối', requestStatus: 'REJECTED' },
    { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
    { value: 'WAITING_REQUEST_APPROVAL', label: 'Chờ phân công', requestStatus: 'APPROVED', campusStatus: 'WAITING_REQUEST_APPROVAL' },
    { value: 'ASSIGNED', label: 'Đã duyệt', requestStatus: 'APPROVED', campusStatus: 'ASSIGNED' },
    { value: 'BEFORE_VISIT', label: 'Trước tiếp khách', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
    { value: 'DURING_VISIT', label: 'Trong tiếp khách', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
    { value: 'AFTER_VISIT', label: 'Chờ đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'AFTER_VISIT' },
    { value: 'CLOSED', label: 'Đã đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'CLOSED' }
  ];

  const VISIT_SCOPE_OPTIONS = [
    { value: 'SINGLE_CAMPUS', label: 'Single Campus' },
    { value: 'MULTI_CAMPUS', label: 'Multi-Campus' }
  ];

  const fetchGuests = async () => {
    try {
      setIsLoading(true);
      setListError(null);
      const params: any = {
        page: currentPage,
        pageSize: pageSize
      };
      
      const keyword = appliedFilters.keyword.trim();
      if (keyword) {
        params.keyword = keyword;
      }

      if (appliedFilters.status) {
        const option = STATUS_FILTER_OPTIONS.find(o => o.value === appliedFilters.status);
        if (option) {
          if (option.cancelledOnly) params.cancelledOnly = true;
          if (option.requestStatus) params.requestStatus = option.requestStatus;
          if (option.campusStatus) params.campusStatus = option.campusStatus;
        }
      }
      
      if (appliedFilters.visitScopes.length > 0) {
        params.visitScopes = appliedFilters.visitScopes.join(',');
      }
      
      if (appliedFilters.fromDate) params.fromDate = appliedFilters.fromDate;
      if (appliedFilters.toDate) params.toDate = appliedFilters.toDate;

      const response = await delegationsApi.getVisitRequestManagementList(params);
      
      const mappedGuests = (response.items || []).map((item: any) => ({
        ...item,
        id: item.visitInstanceId || item.visitRequestId,
        name: item.delegationName || 'Không có tên',
        org: item.partnerName || '-',
        time: item.plannedStartAt ? new Date(item.plannedStartAt).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) : '-',
        plannedStartAt: item.plannedStartAt,
        plannedEndAt: item.plannedEndAt,
        campus: item.campusName || '-',
        host: item.hostName || '',
        sender: item.visitorName || '',
        status: getVietnameseStatus(item.requestStatus, item.campusStatus),
        requestStatus: item.requestStatus,
        campusStatus: item.campusStatus,
        visitorUserId: item.visitorUserId,
        currentHostUserId: item.currentHostUserId,
        types: [item.visitScope === 'MULTI_CAMPUS' ? 'Multi-Campus' : 'Single Campus'],
      }));
      setGuestsList(mappedGuests);
      setTotalGuests(response.totalItems || 0);
    } catch (error) {
      console.error("Failed to fetch visit requests", error);
      setListError('Không thể tải danh sách tiếp khách. Vui lòng thử lại.');
    } finally {
      setIsLoading(false);
    }
  };

  React.useEffect(() => {
    fetchGuests();
  }, [
    currentPage, 
    pageSize, 
    appliedFilters.keyword, 
    appliedFilters.status, 
    appliedFilters.visitScopes.join(','), 
    appliedFilters.fromDate, 
    appliedFilters.toDate
  ]);

  const handleGuestView = (guest: any) => {
    if (guest.requestStatus === 'CANCELLED' || guest.campusStatus === 'CANCELLED') {
      setSelectedGuestForView(guest);
      setIsViewModalOpen(true);
      return;
    }

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

  // Count requests (using filtered data)
  const pendingCount = guestsList.filter(g => g.status === 'Chờ duyệt').length;

  const paginatedGuests = guestsList;
  const totalPages = Math.max(1, Math.ceil(totalGuests / pageSize));

  const getRowClassName = (campus: string) => {
    return 'border-l-2 border-transparent';
  };

  const getStatusBadgeConfig = (status: string) => {
    const baseClass = "inline-flex min-w-[96px] max-w-[130px] justify-center whitespace-nowrap rounded-full border px-2.5 py-1 text-xs font-semibold";
    switch(status) {
      case 'Chờ duyệt':
      case 'Chờ phân công':
        return <span className={`${baseClass} bg-yellow-50 text-yellow-700 border-yellow-200`}>{status}</span>;
      case 'Đã duyệt':
        return <span className={`${baseClass} bg-cyan-50 text-cyan-700 border-cyan-200`}>Đã duyệt</span>;
      case 'Trước tiếp khách':
        return <span className={`${baseClass} bg-blue-50 text-blue-700 border-blue-200`}>Trước tiếp khách</span>;
      case 'Trong tiếp khách':
        return <span className={`${baseClass} bg-green-50 text-green-700 border-green-200`}>Trong tiếp khách</span>;
      case 'Chờ đóng đoàn':
        return <span className={`${baseClass} bg-orange-50 text-orange-700 border-orange-200`}>Chờ đóng đoàn</span>;
      case 'Đã đóng đoàn':
      case 'Đã kết thúc':
        return <span className={`${baseClass} bg-slate-100 text-slate-700 border-slate-300`}>Đã đóng đoàn</span>;
      case 'Từ chối':
        return <span className={`${baseClass} bg-red-50 text-red-700 border-red-200`}>Từ chối</span>;
      case 'Đã hủy':
        return <span className={`${baseClass} bg-gray-100 text-gray-600 border-gray-200`}>Đã hủy</span>;
      default:
        return <span className={`${baseClass} bg-gray-100 text-gray-700 border-gray-200`}>{status}</span>;
    }
  };

  const getStatusBadge = (guest: any) => {
    return getStatusBadgeConfig(guest.status);
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

  const renderRowActions = (guest: any) => {
    const isVisitorOwner = isVisitor && String(guest.visitorUserId ?? '') === currentUserId;
    const isAssignedHost = String(guest.currentHostUserId ?? '') === currentUserId;
    const canCancelByActor = isVisitorOwner || (isAssignedHost && isStaff);
    const canShowCancel = canCancelByActor && canCancelInstance(guest.requestStatus, guest.campusStatus);
    const canShowView = Boolean(guest.visitRequestId || guest.id);
    const canShowRejectReason = guest.status === 'Từ chối';
    const isPending = guest.status === 'Chờ duyệt';

    const isMultiCampus = guest.visitScope === 'MULTI_CAMPUS' || guest.types?.includes('Multi-Campus');
    const isSingleCampus = guest.visitScope === 'SINGLE_CAMPUS' || guest.types?.includes('Single Campus');
    
    const canHOApproveReject = isHO && isPending && isMultiCampus;
    const canStaffLeaderApproveReject = isStaffLeader && isPending && isSingleCampus && String(guest.campusId ?? '') === String(user?.primaryCampusId ?? user?.campusId ?? '');
    const canShowApproveOrReject = canHOApproveReject || canStaffLeaderApproveReject;

    return (
      <div className="flex items-center justify-end gap-1">
        <ActionSlot>
          {canShowView && (
            <ActionIconButton
              title={isHO ? "Xem chi tiết tiếp khách" : isStaffLeader ? "Chi tiết quy trình" : (guest.status === 'Chờ đóng đoàn' ? "Xử lý kết thúc đoàn" : "Xem chi tiết")}
              tone="blue"
              icon={<Eye className="h-5 w-5" />}
              onClick={(e) => {
                e.stopPropagation();
                handleGuestView(guest);
              }}
            />
          )}
        </ActionSlot>

        <ActionSlot>
          {isPending ? (
            canShowApproveOrReject && (
              <ActionIconButton
                title={isHO ? 'Phê duyệt tổng' : 'Phê duyệt'}
                tone="green"
                icon={<Check className="h-5 w-5" />}
                onClick={(e) => {
                  e.stopPropagation();
                  if (isHO) {
                    setSelectedGuestForForward(guest);
                    setIsForwardModalOpen(true);
                  } else if (isStaffLeader) {
                    setGuestsList(prev => prev.map(g => g.id === guest.id ? { ...g, status: 'Đã duyệt' } : g));
                  }
                }}
              />
            )
          ) : (
            canShowCancel && (
              <ActionIconButton
                title="Hủy lịch thăm"
                tone="red"
                icon={<XCircle className="h-5 w-5" />}
                onClick={(e) => {
                  e.stopPropagation();
                  setSelectedGuestForCancel(guest);
                  setIsCancelModalOpen(true);
                  setCancelError(null);
                }}
              />
            )
          )}
        </ActionSlot>

        <ActionSlot>
          {isPending ? (
            canShowApproveOrReject && (
              <ActionIconButton
                title="Từ chối"
                tone="red"
                icon={<X className="h-5 w-5" />}
                onClick={(e) => {
                  e.stopPropagation();
                  setSelectedGuestForReject(guest);
                  setIsRejectModalOpen(true);
                }}
              />
            )
          ) : (
            canShowRejectReason && (
              <ActionIconButton
                title="Xem lý do từ chối"
                tone="orange"
                icon={<AlertCircle className="h-5 w-5" />}
                onClick={(e) => {
                  e.stopPropagation();
                  setSelectedGuestForReason(guest);
                  setIsReasonModalOpen(true);
                }}
              />
            )
          )}
        </ActionSlot>
      </div>
    );
  };

  if (isSystemAdmin) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh] animate-in fade-in duration-300">
        <AlertCircle className="w-16 h-16 text-slate-300 mb-4" />
        <h2 className="text-2xl font-bold text-slate-700 mb-2">Không có quyền truy cập</h2>
        <p className="text-slate-500 text-center max-w-md">
          Tài khoản Admin hiện tại không tham gia vào luồng quản lý tiếp khách. Vui lòng đăng nhập với tài khoản có thẩm quyền (Staff, HO, v.v.).
        </p>
      </div>
    );
  }

  return (
    <div className="w-full max-w-[1320px] mx-auto p-4 sm:p-6 lg:p-8 flex flex-col space-y-6 pb-12 animate-in fade-in duration-300">
      
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
      <div className="w-full mb-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm overflow-visible">
        <div className="flex flex-col gap-3">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-[minmax(300px,1fr)_180px_170px_210px_112px_44px] xl:items-end w-full">
            {/* Keyword Filter */}
            <div className="min-w-0 w-full">
              <label className="block text-xs font-bold text-slate-500 mb-1">Tìm kiếm</label>
              <div className="relative w-full">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-5 h-5" />
                <input 
                  type="text" 
                  placeholder="Tìm tên đoàn, host, đối tác..." 
                  value={draftFilters.keyword}
                  onChange={(e) => setDraftFilters({...draftFilters, keyword: e.target.value})}
                  className="w-full pl-10 pr-4 h-11 bg-white border border-slate-300 rounded-xl text-sm font-semibold text-slate-700 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10 transition-colors min-w-0"
                />
              </div>
            </div>

            {/* Status Filter */}
            <div className="relative min-w-0 w-full">
              <label className="block text-xs font-bold text-slate-500 mb-1">Trạng thái</label>
              <button
                onClick={() => setIsStatusFilterOpen(!isStatusFilterOpen)}
                className="flex h-11 w-full min-w-0 items-center justify-between rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10"
              >
                <span className="min-w-0 truncate">
                  {STATUS_FILTER_OPTIONS.find(o => o.value === draftFilters.status)?.label ?? 'Tất cả trạng thái'}
                </span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
              </button>

              {isStatusFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsStatusFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 w-full min-w-[210px] rounded-xl border border-slate-200 bg-white py-1 shadow-lg font-sans">
                    {STATUS_FILTER_OPTIONS.map(option => (
                      <div 
                        key={option.value}
                        className={`px-3 py-2 text-sm cursor-pointer hover:bg-slate-50 flex items-center ${draftFilters.status === option.value ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-gray-700 font-medium'}`}
                        onClick={() => {
                          setDraftFilters({...draftFilters, status: option.value});
                          setIsStatusFilterOpen(false);
                        }}
                      >
                        {option.label}
                        {draftFilters.status === option.value && <Check className="w-4 h-4 ml-auto text-[#004c91]" />}
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>

            {/* Visit Scope Filter */}
            <div className="relative min-w-0 w-full">
              <label className="block text-xs font-bold text-slate-500 mb-1">Phạm vi</label>
              <button
                onClick={() => setIsTypeFilterOpen(!isTypeFilterOpen)}
                className="flex h-11 w-full min-w-0 items-center justify-between rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10"
              >
                <span className="min-w-0 truncate">
                  {draftFilters.visitScopes.length === 0 ? 'Tất cả phạm vi' : draftFilters.visitScopes.length === 1 ? (VISIT_SCOPE_OPTIONS.find(x => x.value === draftFilters.visitScopes[0])?.label ?? '1 phạm vi') : `${draftFilters.visitScopes.length} phạm vi`}
                </span>
                <ChevronDown className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
              </button>

              {isTypeFilterOpen && (
                <>
                  <div className="fixed inset-0 z-10" onClick={() => setIsTypeFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-1 w-full min-w-[210px] rounded-xl border border-slate-200 bg-white py-1 shadow-lg">
                    {VISIT_SCOPE_OPTIONS.map(scope => (
                      <label key={scope.value} className="flex items-center px-3 py-2 hover:bg-slate-50 cursor-pointer">
                        <input 
                          type="checkbox"
                          className="mr-2 rounded border-gray-300 text-[#004c91] focus:ring-[#004c91]"
                          checked={draftFilters.visitScopes.includes(scope.value)}
                          onChange={(e) => {
                            if (e.target.checked) {
                              setDraftFilters({...draftFilters, visitScopes: [...draftFilters.visitScopes, scope.value]});
                            } else {
                              setDraftFilters({...draftFilters, visitScopes: draftFilters.visitScopes.filter(t => t !== scope.value)});
                            }
                          }}
                        />
                        <span className="text-sm font-medium text-gray-700">{scope.label}</span>
                      </label>
                    ))}
                  </div>
                </>
              )}
            </div>
            
            {/* Date Range Control */}
            <div className="relative min-w-0 w-full">
              <label className="block text-xs font-bold text-slate-500 mb-1">Khoảng ngày</label>
              <button
                onClick={() => setIsDateFilterOpen(!isDateFilterOpen)}
                className="flex h-11 w-full min-w-0 items-center justify-between rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none transition-colors focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10"
              >
                <span className="min-w-0 truncate">
                  {!draftFilters.fromDate && !draftFilters.toDate
                    ? 'Chọn khoảng ngày'
                    : draftFilters.fromDate && !draftFilters.toDate
                    ? `Từ ${formatDateOnly(draftFilters.fromDate)}`
                    : !draftFilters.fromDate && draftFilters.toDate
                    ? `Đến ${formatDateOnly(draftFilters.toDate)}`
                    : `${formatDateOnly(draftFilters.fromDate)} - ${formatDateOnly(draftFilters.toDate)}`}
                </span>
                <Calendar className="w-4 h-4 text-gray-500 flex-shrink-0 ml-2 pointer-events-none" />
              </button>

              {isDateFilterOpen && (
                <>
                  <div className="fixed inset-0 z-20" onClick={() => setIsDateFilterOpen(false)} />
                  <div className="absolute left-0 top-full z-30 mt-2 w-[280px] bg-white border border-slate-200 rounded-2xl shadow-lg p-4">
                    <div className="flex flex-col gap-3">
                      <div className="w-full space-y-1">
                        <label className="block text-xs font-bold text-slate-500">Từ ngày</label>
                        <input 
                          type="date"
                          value={draftFilters.fromDate}
                          onChange={(e) => setDraftFilters({...draftFilters, fromDate: e.target.value})}
                          className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10"
                        />
                      </div>
                      <div className="w-full space-y-1">
                        <label className="block text-xs font-bold text-slate-500">Đến ngày</label>
                        <input 
                          type="date"
                          value={draftFilters.toDate}
                          onChange={(e) => setDraftFilters({...draftFilters, toDate: e.target.value})}
                          className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10"
                        />
                      </div>
                      <button
                        onClick={() => setIsDateFilterOpen(false)}
                        className="mt-2 h-9 w-full rounded-lg bg-slate-100 text-sm font-semibold text-slate-700 hover:bg-slate-200 transition-colors"
                      >
                        Đóng
                      </button>
                    </div>
                  </div>
                </>
              )}
            </div>
            
            <button
              onClick={handleApplyFilters}
              className="inline-flex h-11 w-full items-center justify-center rounded-xl bg-[#004c91] px-4 text-sm font-bold text-white transition-colors hover:bg-[#003b70] whitespace-nowrap"
            >
              Áp dụng
            </button>

            <button
              onClick={handleResetFilters}
              title="Xóa bộ lọc"
              aria-label="Xóa bộ lọc"
              className="inline-flex h-11 w-11 items-center justify-center rounded-xl border border-slate-300 bg-white text-slate-500 transition-colors hover:bg-slate-50 hover:text-red-500 flex-shrink-0"
            >
              <X className="h-5 w-5" />
            </button>
          </div>
          {filterError && (
            <div className="text-red-500 text-sm font-medium mt-1">
              <AlertCircle className="w-4 h-4 inline-block mr-1" />
              {filterError}
            </div>
          )}
        </div>
      </div>

      {/* 4. Main Data List (Redesigned) */}
      <div className="w-full overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm flex flex-col">
        {/* Desktop View */}
        <div className="hidden lg:block w-full">
          <div className="grid grid-cols-[52px_minmax(0,1fr)_210px_135px_120px] bg-[#004c91] text-white">
            <div className="p-3 text-[12px] font-bold text-center uppercase tracking-wider whitespace-nowrap">STT</div>
            <div className="p-3 text-[12px] font-bold text-left uppercase tracking-wider whitespace-nowrap">Thông tin đoàn</div>
            <div className="p-3 text-[12px] font-bold text-left uppercase tracking-wider whitespace-nowrap">Lịch tiếp</div>
            <div className="p-3 text-[12px] font-bold text-center uppercase tracking-wider whitespace-nowrap">Trạng thái</div>
            <div className="p-3 text-[12px] font-bold text-center uppercase tracking-wider whitespace-nowrap">Hành động</div>
          </div>
          
          <div className="flex flex-col">
            {isLoading ? (
              <div className="py-12 text-center text-slate-500 font-medium">
                <p>Đang tải danh sách...</p>
              </div>
            ) : listError ? (
              <div className="py-12 text-center text-red-500 font-medium">
                <AlertCircle className="w-8 h-8 mx-auto mb-2 text-red-400" />
                <p>{listError}</p>
              </div>
            ) : paginatedGuests.length > 0 ? paginatedGuests.map((guest, index) => (
              <div 
                key={guest.id} 
                className={`grid grid-cols-[52px_minmax(0,1fr)_210px_135px_120px] items-center min-h-[78px] border-b border-slate-200/70 transition-colors duration-150 cursor-pointer ${index % 2 === 0 ? 'bg-white' : 'bg-slate-50'} hover:bg-blue-50 group`}
                onClick={() => handleGuestView(guest)}
              >
                <div className="py-3 px-3 text-center font-bold text-[#004c91] text-sm">{(currentPage - 1) * pageSize + index + 1}</div>
                <div className="py-3 px-3 min-w-0 flex flex-col justify-center pr-4">
                  <p className="text-sm font-bold text-[#004c91] line-clamp-2 break-words mb-1" title={guest.name}>{guest.name}</p>
                  <p className="text-xs font-medium text-slate-500 truncate" title={`${guest.org} · ${guest.types.join(', ')}`}>
                    {guest.org} · {guest.types.join(', ')}
                  </p>
                  {!isHO && (
                    <p className="text-xs font-medium text-slate-600 mt-1 truncate">
                      <span className="text-slate-400">Host:</span> {guest.host || (guest.status === 'Đã duyệt' && isVisitor ? 'Đang phân công' : '-')}
                      <span className="mx-1 text-slate-300">|</span>
                      <span className="text-slate-400">Campus:</span> {guest.campus || '-'}
                    </p>
                  )}
                </div>
                <div className="py-3 px-3 text-sm leading-6 text-slate-700">
                  <div className="flex items-center gap-2 whitespace-nowrap">
                    <span className="w-9 text-slate-400 font-medium">Từ:</span>
                    <span className="font-semibold text-slate-800">
                      {formatDateTimeShort(guest.plannedStartAt)}
                    </span>
                  </div>

                  <div className="flex items-center gap-2 whitespace-nowrap">
                    <span className="w-9 text-slate-400 font-medium">Đến:</span>
                    <span className="font-semibold text-slate-800">
                      {formatDateTimeShort(guest.plannedEndAt)}
                    </span>
                  </div>
                </div>
                <div className="py-3 px-3 flex flex-col items-center justify-center gap-1">
                  {getStatusBadge(guest)}
                </div>
                <div className="py-3 px-2 flex items-center justify-center">
                  {renderRowActions(guest)}
                </div>
              </div>
            )) : (
              <div className="py-12 text-center text-slate-500 font-medium flex flex-col items-center justify-center">
                <Users className="w-12 h-12 text-slate-300 mb-3" />
                <p>{(appliedFilters.keyword || appliedFilters.status || appliedFilters.visitScopes.length > 0 || appliedFilters.fromDate || appliedFilters.toDate) ? 'Không tìm thấy đoàn khách phù hợp với bộ lọc.' : 'Không có đoàn khách nào.'}</p>
              </div>
            )}
          </div>
        </div>

        {/* Mobile / Tablet View */}
        <div className="lg:hidden w-full p-4 space-y-4 bg-slate-50/50">
          {paginatedGuests.length > 0 ? paginatedGuests.map((guest, index) => (
            <div 
              key={guest.id} 
              className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm hover:border-[#004c91]/30 transition-colors cursor-pointer"
              onClick={() => handleGuestView(guest)}
            >
              <div className="flex items-start justify-between gap-3 mb-3">
                <div className="min-w-0 flex-1">
                  <p className="font-bold text-[#004c91] text-sm line-clamp-2 mb-1 leading-snug">{guest.name}</p>
                  <p className="text-xs text-slate-500 truncate">{guest.org} · {guest.types.join(', ')}</p>
                </div>
                <div className="flex-shrink-0">
                  {getStatusBadge(guest)}
                </div>
              </div>

              <div className="grid grid-cols-1 gap-1.5 text-xs text-slate-600 bg-slate-50 p-3 rounded-xl border border-slate-100">
                <div className="flex items-center gap-2">
                  <Calendar className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
                  <span className="truncate">
                    {formatDateTimeShort(guest.plannedStartAt)} <span className="text-slate-400 mx-1">→</span> {formatDateTimeShort(guest.plannedEndAt)}
                  </span>
                </div>
                {!isHO && (
                  <>
                    <div className="flex items-center gap-2 mt-0.5">
                      <Users className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
                      <span className="truncate"><span className="text-slate-400">Host:</span> {guest.host || (guest.status === 'Đã duyệt' && isVisitor ? 'Đang phân công' : '-')}</span>
                    </div>
                    <div className="flex items-center gap-2 mt-0.5">
                      <MapPin className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
                      <span className="truncate"><span className="text-slate-400">Campus:</span> {guest.campus || '-'}</span>
                    </div>
                  </>
                )}
              </div>
              
              <div className="mt-3 flex items-center justify-end border-t border-slate-100 pt-3">
                {renderRowActions(guest)}
              </div>
            </div>
          )) : (
            <div className="py-10 text-center text-slate-500 font-medium flex flex-col items-center justify-center">
              <Users className="w-12 h-12 text-slate-300 mb-3" />
              <p>{(appliedFilters.keyword || appliedFilters.status || appliedFilters.visitScopes.length > 0 || appliedFilters.fromDate || appliedFilters.toDate) ? 'Không tìm thấy đoàn khách phù hợp với bộ lọc.' : 'Không có đoàn khách nào.'}</p>
            </div>
          )}
        </div>
        
        {/* Pagination */}
        {totalGuests > 0 && (
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

      {/* Cancel Modal */}
      {isCancelModalOpen && selectedGuestForCancel && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <motion.div 
            initial={{ opacity: 0, scale: 0.95, y: 10 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden relative border border-gray-100"
          >
            <div className="px-6 py-4 bg-red-600 flex items-center justify-between">
              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                <AlertCircle className="w-5 h-5 bg-white/20 rounded-full p-0.5" /> Xác nhận hủy lịch thăm
              </h3>
              <button 
                type="button"
                onClick={() => {
                  setIsCancelModalOpen(false);
                  setCancellationReason('');
                  setSelectedGuestForCancel(null);
                }}
                className="text-white/85 hover:text-white transition-colors hover:bg-white/10 rounded-full p-1.5 cursor-pointer"
                disabled={isCancelling}
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6 space-y-4">
              <p className="text-sm text-gray-700">
                Bạn đang thực hiện hủy lịch thăm của đoàn <span className="font-bold text-[#004c91]">{selectedGuestForCancel?.name}</span>. Hành động này không thể hoàn tác.
              </p>
              
              <div>
                <label className="block text-sm font-bold text-gray-700 mb-2">Lý do hủy <span className="text-red-500">*</span></label>
                <textarea
                  value={cancellationReason}
                  onChange={(e) => setCancellationReason(e.target.value)}
                  placeholder="Nhập lý do hủy hoặc thông tin khách đã xác nhận hủy..."
                  className="w-full px-4 py-3 rounded-2xl border border-gray-200 focus:border-red-500 focus:ring-4 focus:ring-red-500/10 outline-none transition-all text-sm min-h-[100px] resize-none bg-gray-50/50 hover:bg-gray-50 focus:bg-white"
                  disabled={isCancelling}
                />
                {cancelError && (
                  <p className="text-red-500 text-sm mt-2">{cancelError}</p>
                )}
              </div>
            </div>
            
            <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
              <button 
                type="button"
                onClick={() => {
                  setIsCancelModalOpen(false);
                  setCancellationReason('');
                  setSelectedGuestForCancel(null);
                  setCancelError(null);
                }}
                className="px-5 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer"
                disabled={isCancelling}
              >
                Đóng
              </button>
              <button 
                type="button"
                onClick={async () => {
                  const reason = cancellationReason.trim();
                  if (!reason) {
                    setCancelError('Vui lòng nhập lý do hủy.');
                    return;
                  }

                  if (!selectedGuestForCancel?.visitRequestId) {
                    setCancelError('Thiếu mã lịch thăm/campus instance để hủy.');
                    return;
                  }

                  if (selectedGuestForCancel.types && selectedGuestForCancel.types.includes('Multi-Campus') && !selectedGuestForCancel.visitInstanceId) {
                    setCancelError('Thiếu mã lịch thăm/campus instance để hủy.');
                    return;
                  }

                  setIsCancelling(true);
                  setCancelError(null);
                  
                  try {
                    const payload = { cancellationReason: reason };
                    if (selectedGuestForCancel.visitInstanceId) {
                      await delegationsApi.cancelVisitRequestCampus(selectedGuestForCancel.visitRequestId, selectedGuestForCancel.visitInstanceId, payload);
                    } else {
                      await delegationsApi.cancelVisitRequest(selectedGuestForCancel.visitRequestId, payload);
                    }
                    
                    setIsCancelModalOpen(false);
                    setCancellationReason('');
                    setSelectedGuestForCancel(null);

                    try {
                      await fetchGuests();
                      console.log('[Cancel success] refetch called');
                    } catch (fetchErr) {
                      console.error("Refetch error after cancel:", fetchErr);
                      alert("Đã hủy thành công nhưng tải lại danh sách thất bại");
                    }
                  } catch(e: any) {
                    const errorMsg = e?.response?.data?.message || e?.response?.data?.title || e?.message || 'Lỗi không xác định';
                    setCancelError(`Không thể hủy lịch thăm. Lỗi: ${errorMsg}`);
                    console.error('Lỗi khi gọi API hủy:', {
                        endpoint: selectedGuestForCancel.visitInstanceId ? `/api/Delegations/${selectedGuestForCancel.visitRequestId}/campuses/${selectedGuestForCancel.visitInstanceId}/cancel` : `/api/Delegations/${selectedGuestForCancel.visitRequestId}/cancel`,
                        payload: { cancellationReason: reason },
                        statusCode: e?.response?.status,
                        response: e?.response?.data,
                        error: e
                    });
                  } finally {
                    setIsCancelling(false);
                  }
                }}
                disabled={!cancellationReason.trim() || isCancelling}
                className="px-6 py-2 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
              >
                {isCancelling ? 'Đang xử lý...' : 'Xác nhận hủy'}
              </button>
            </div>
          </motion.div>
        </div>
      )}
      
    </div>
  );
}
