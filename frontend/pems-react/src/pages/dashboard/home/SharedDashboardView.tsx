/**
 * Component SharedDashboardView
 * Màn hình chung quy định bảng nhiệm vụ và tiến độ tiếp đón khách dành cho Roles.
 */

import React, { useState, useMemo } from 'react';
import { 
  Calendar as CalendarIcon, 
  Calendar,
  ChevronLeft, 
  ChevronRight, 
  ChevronUp,
  Trash2, 
  MoreVertical, 
  X, 
  MapPin, 
  Clock, 
  Users, 
  User,
  Bookmark, 
  CheckSquare, 
  Plus, 
  Eye, 
  AlertCircle,
  TrendingUp,
  FileText,
  Bell,
  Sparkles,
  Info,
  ChevronDown,
  Edit2
} from 'lucide-react';

interface Event {
  id: string;
  title: string;
  date: string; // YYYY-MM-DD
  time: string;
  category: 'Lời mời tham gia' | 'Đơn yêu cầu mượn đồ';
  color: string; // css color classes
  hoverColor: string;
  location: string;
  host: string;
  guests: string;
  checklist: string[];
  purpose?: string;       // Mục đích đón tiếp
  vipLevel?: string;      // Phân cấp VIP
  contactPerson?: string; // Điều phối viên phụ trách
  hotelInfo?: string;     // Khách sạn lưu trú
  bannerText?: string;    // Băng rôn LED
  carBooking?: string;    // Xe đưa đón
}

const INITIAL_EVENTS: Event[] = [
  {
    id: 'e-invitation-8',
    title: 'Thư mời tham gia sự kiện',
    date: '2026-08-08',
    time: '14:00 - 16:30',
    category: 'Lời mời tham gia',
    color: 'bg-emerald-50 text-emerald-700 border-emerald-300 hover:bg-emerald-100',
    hoverColor: 'border-emerald-500',
    location: 'Hội trường sảnh tòa nhà Alpha',
    host: 'Nguyễn Văn A',
    guests: 'Đoàn đối tác Nhật Bản',
    checklist: [],
    purpose: 'Trân trọng kính mời anh/chị tham gia tiếp đón và giao lưu cùng đoàn đối tác từ Nhật Bản.\n\nVui lòng chuẩn bị tài liệu liên quan để trao đổi hợp tác.',
    vipLevel: 'Standard',
    contactPerson: 'Nguyễn Văn A'
  },
  {
    id: 'safuri-car-event',
    title: 'Yêu cầu mượn xe điện cho đoàn khách Safuri',
    date: '2026-08-08',
    time: '08:00 - 17:30',
    category: 'Đơn yêu cầu mượn đồ',
    color: 'bg-orange-50 text-orange-700 border-orange-300 hover:bg-orange-100',
    hoverColor: 'border-orange-500',
    location: 'Campus Hòa Lạc, Đại học FPT',
    host: 'Phòng Hậu cần & Đội xe điện',
    guests: 'Đoàn khách Safuri',
    checklist: ['Kiểm tra xe bảo dưỡng', 'Cử tài xế túc trực', 'Hoàn tất biên bản bàn giao'],
    purpose: 'Mượn xe điện phục vụ di chuyển đoàn khách Safuri tham quan doanh nghiệp và campus.',
    vipLevel: 'VIP',
    contactPerson: 'Trần Văn Tuyến (Điều hành xe - 0914.555.666)'
  }];

export function SharedDashboardView({ user, isDeptLeader, isDeptStaff, isStudent, isVisitor }: { user?: any, isDeptLeader?: boolean, isDeptStaff?: boolean, isStudent?: boolean, isVisitor?: boolean }) {
  const [events, setEvents] = useState<Event[]>(() => {
    let baseEvents = INITIAL_EVENTS;
    if (isDeptLeader || isDeptStaff || isStudent || isVisitor) {
      baseEvents = [
        ...INITIAL_EVENTS,
        {
          id: 'dl-invitation',
          title: 'Thư mời: Họp giao ban Quản lý Khối',
          date: '2026-08-25',
          time: '14:00 - 15:30',
          category: 'Lời mời tham gia',
          color: 'bg-emerald-50 text-emerald-700 border-emerald-300 hover:bg-emerald-100',
          hoverColor: 'border-emerald-500',
          location: 'Phòng họp Beta, Tòa nhà Delta',
          host: 'Ban Giám đốc',
          guests: 'Các Trưởng phòng/ban',
          checklist: ['Chuẩn bị báo cáo tiến độ'],
          purpose: 'Kính mời Trưởng phòng tham gia buổi họp giao ban Quản lý Khối hàng tháng.\n\nVui lòng chuẩn bị báo cáo tiến độ các dự án đang triển khai.',
          vipLevel: 'Standard',
          contactPerson: 'Phòng Hành chính'
        },
        {
          id: 'dl-request',
          title: 'Yêu cầu: Phê duyệt mua sắm thiết bị phòng họp',
          date: '2026-08-26',
          time: '10:00 - 11:00',
          category: 'Đơn yêu cầu mượn đồ',
          color: 'bg-orange-50 text-orange-700 border-orange-300 hover:bg-orange-100',
          hoverColor: 'border-orange-500',
          location: 'Hệ thống eProcurement',
          host: 'Phòng Hành chính Quản trị',
          guests: '',
          checklist: ['Đọc báo giá', 'Xác nhận budget'],
          purpose: 'Đề xuất phê duyệt thay mới 2 màn hình tương tác tại phòng Beta.',
          vipLevel: 'Standard',
          contactPerson: 'Trưởng nhóm IT'
        }
      ];
    }
    if (isStudent) {
      return baseEvents.filter(e => e.category !== 'Đơn yêu cầu mượn đồ');
    }
    if (isVisitor) {
      return baseEvents.filter(e => e.category !== 'Đơn yêu cầu mượn đồ' && e.category !== 'Lời mời tham gia');
    }
    return baseEvents;
  });
  const [activePopoverEvent, setActivePopoverEvent] = useState<Event | null>(null);
  const [selectedCategoryFilter, setSelectedCategoryFilter] = useState<string>('All');

  // Thư mời interaction states
  const [invitationStatus, setInvitationStatus] = useState<'pending' | 'rejecting' | 'rejected' | 'accepted'>('pending');
  const [rejectReason, setRejectReason] = useState('');
  const [acceptSignature, setAcceptSignature] = useState<{name: string, time: string} | null>(null);
  const [showAssignDropdown, setShowAssignDropdown] = useState(false);
  const [assignedPerson, setAssignedPerson] = useState<string | null>(null);

  // Đơn yêu cầu interaction states
  const [requestStatus, setRequestStatus] = useState<'pending' | 'rejecting' | 'rejected' | 'accepted'>('pending');
  const [requestAcceptSignature, setRequestAcceptSignature] = useState<{name: string, time: string} | null>(null);
  const [requestRejectReason, setRequestRejectReason] = useState('');
  const [isProposing, setIsProposing] = useState(false);
  const [proposalNote, setProposalNote] = useState('');
  const [proposalSubmitted, setProposalSubmitted] = useState(false);

  // Dept preliminary states
  const [deptPreliminaryStatus, setDeptPreliminaryStatus] = useState<'pending' | 'rejecting' | 'rejected' | 'accepted'>('pending');
  const [deptRejectReason, setDeptRejectReason] = useState('');

  React.useEffect(() => {
    setInvitationStatus('pending');
    setRejectReason('');
    setAcceptSignature(null);
    setShowAssignDropdown(false);
    setAssignedPerson(null);
    setRequestStatus('pending');
    setRequestAcceptSignature(null);
    setRequestRejectReason('');
    setIsProposing(false);
    setProposalNote('');
    setProposalSubmitted(false);
    setDeptPreliminaryStatus('pending');
    setDeptRejectReason('');
  }, [activePopoverEvent?.id]);

  const MOCK_STAFF_LIST = [
    { id: '1', name: 'Nguyễn Văn Hùng', email: 'hungnv45@fpt.edu.vn' },
    { id: '2', name: 'Trần Thị Mai', email: 'maitt12@fpt.edu.vn' },
    { id: '3', name: 'Lê Hoàng Phong', email: 'phonglh8@fpt.edu.vn' }
  ];

  // States for interactive handover & acceptance of Safuri event
  const [safuriBG1Signed, setSafuriBG1Signed] = useState<string | null>(null);
  const [safuriBG2Signed, setSafuriBG2Signed] = useState<string | null>(null);
  const [safuriNT1Signed, setSafuriNT1Signed] = useState<string | null>(null);
  const [safuriNT2Signed, setSafuriNT2Signed] = useState<string | null>(null);

  const [safuriBG1Note, setSafuriBG1Note] = useState('Xe sạc đầy pin 100%, có trang bị 10 ô mang thương hiệu FPT.');
  const [safuriBG2Note, setSafuriBG2Note] = useState('Đã kiểm tra xe vận hành êm ái, đầy đủ ô dù.');
  const [safuriNT1Note, setSafuriNT1Note] = useState('Đã nhận lại chìa khóa, xe sạch sẽ.');
  const [safuriNT2Note, setSafuriNT2Note] = useState('Xe trả nguyên trạng, hoàn tất phiên bàn giao.');

  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({
    creator: false,
    guests: false,
    setup: false,
    details: false
  });

  const toggleSection = (section: string) => {
    setExpandedSections(prev => ({
      ...prev,
      [section]: !prev[section]
    }));
  };

  // Collapse all accordion sections whenever a new event is opened
  React.useEffect(() => {
    setExpandedSections({
      creator: false,
      guests: false,
      setup: false,
      details: false
    });
  }, [activePopoverEvent?.id]);

  const [currentYear, setCurrentYear] = useState(2026);
  const [currentMonth, setCurrentMonth] = useState(7); // August (7 since 0-indexed)

  const [showAddFormModal, setShowAddFormModal] = useState(false);
  const [selectedCellDate, setSelectedCellDate] = useState<string | null>('2026-08-26');

  // States for Vietnamese Miniature Date Picker & Views
  const [showMiniCalendar, setShowMiniCalendar] = useState(false);
  const [miniMonth, setMiniMonth] = useState(7);
  const [miniYear, setMiniYear] = useState(2026);
  const [showDisplayDropdown, setShowDisplayDropdown] = useState(false);
  const [displayMode, setDisplayMode] = useState<'Ngày' | 'Tuần' | 'Tháng' | 'Năm'>('Tháng');

  // New states for Calendar Type ("Trong văn phòng", "Lịch của tôi")
  const [calendarType, setCalendarType] = useState<'Trong văn phòng' | 'Lịch của tôi'>((isDeptLeader || isStudent || isVisitor) ? 'Lịch của tôi' : 'Trong văn phòng');
  const [showTypeDropdown, setShowTypeDropdown] = useState(false);

  // Filter events based on type
  const filteredEvents = useMemo(() => {
    if (calendarType === 'Lịch của tôi') {
      if (isStudent || isVisitor) return events;
      return events.filter(
        e => e.host.toLowerCase().includes('staff') || 
             e.host.toLowerCase().includes('leader') || 
             e.id === 'viet-new-year-eve' ||
             e.id.startsWith('dl-')
      );
    }
    return events; // "Trong văn phòng" shows all
  }, [events, calendarType, isStudent, isVisitor]);

  // All events within the selected month and year
  const eventsInCurrentMonthAndYear = useMemo(() => {
    return filteredEvents.filter(e => {
      const parts = e.date.split('-');
      if (parts.length < 3) return false;
      const year = parseInt(parts[0], 10);
      const month = parseInt(parts[1], 10);
      return month === (currentMonth + 1) && year === currentYear;
    });
  }, [filteredEvents, currentMonth, currentYear]);
  // New Event Form State
  const [newTitle, setNewTitle] = useState('');
  const [newTime, setNewTime] = useState('09:00 - 11:00');
  const [newCategory, setNewCategory] = useState<Event['category']>('Lời mời tham gia');
  const [newLocation, setNewLocation] = useState('Phòng họp Alpha, Hòa Lạc');
  const [newHost, setNewHost] = useState('Office of International Affairs');
  const [newGuests, setNewGuests] = useState('International Delegates');
  const [newChecklistStr, setNewChecklistStr] = useState('Chuẩn bị quà tặng\nĐặt bàn trà bánh');
  const [showDetailSection, setShowDetailSection] = useState(false);
  const monthNames = [
    'Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
    'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'
  ];

  const handlePrev = () => {
    setActivePopoverEvent(null);
    if (displayMode === 'Ngày' || displayMode === 'Tuần') {
      if (selectedCellDate) {
        const d = new Date(selectedCellDate);
        if (displayMode === 'Ngày') {
          d.setDate(d.getDate() - 1);
        } else {
          d.setDate(d.getDate() - 7);
        }
        const y = d.getFullYear();
        const m = d.getMonth();
        const dateStr = `${y}-${String(m + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
        setSelectedCellDate(dateStr);
        setCurrentYear(y);
        setCurrentMonth(m);
      }
    } else if (displayMode === 'Năm') {
      setCurrentYear(y => y - 1);
    } else {
      if (currentMonth === 0) {
        setCurrentMonth(11);
        setCurrentYear(y => y - 1);
      } else {
        setCurrentMonth(m => m - 1);
      }
    }
  };

  const handleNext = () => {
    setActivePopoverEvent(null);
    if (displayMode === 'Ngày' || displayMode === 'Tuần') {
      if (selectedCellDate) {
        const d = new Date(selectedCellDate);
        if (displayMode === 'Ngày') {
          d.setDate(d.getDate() + 1);
        } else {
          d.setDate(d.getDate() + 7);
        }
        const y = d.getFullYear();
        const m = d.getMonth();
        const dateStr = `${y}-${String(m + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
        setSelectedCellDate(dateStr);
        setCurrentYear(y);
        setCurrentMonth(m);
      }
    } else if (displayMode === 'Năm') {
      setCurrentYear(y => y + 1);
    } else {
      if (currentMonth === 11) {
        setCurrentMonth(0);
        setCurrentYear(y => y + 1);
      } else {
        setCurrentMonth(m => m + 1);
      }
    }
  };

  const handleResetToAugust2026 = () => {
    setCurrentMonth(7);
    setCurrentYear(2026);
    setActivePopoverEvent(null);
  };

  const daysGrid = useMemo(() => {
    // Return grid of days, aligned Monday to Sunday.
    // Monday is index 1, Sunday is 0 from getDay(), let's map Monday to 0, Sunday to 6
    const firstDayIndexRaw = new Date(currentYear, currentMonth, 1).getDay();
    // Raw index: 0=Sun, 1=Mon, 2=Tue, 3=Wed, 4=Thu, 5=Fri, 6=Sat
    // Mapped to 0=Mon, 1=Tue, 2=Wed, 3=Thu, 4=Fri, 5=Sat, 6=Sun
    const firstDayIndex = firstDayIndexRaw === 0 ? 6 : firstDayIndexRaw - 1;

    const totalDays = new Date(currentYear, currentMonth + 1, 0).getDate();
    const prevMonthTotalDays = new Date(currentYear, currentMonth, 0).getDate();

    const days = [];

    // July / Prior Month padding
    for (let i = firstDayIndex - 1; i >= 0; i--) {
      const d = prevMonthTotalDays - i;
      const m = currentMonth === 0 ? 11 : currentMonth - 1;
      const y = currentMonth === 0 ? currentYear - 1 : currentYear;
      const mStr = String(m + 1).padStart(2, '0');
      const dStr = String(d).padStart(2, '0');
      days.push({
        day: d,
        dateString: `${y}-${mStr}-${dStr}`,
        isCurrent: false
      });
    }

    // Current Month
    for (let i = 1; i <= totalDays; i++) {
      const mStr = String(currentMonth + 1).padStart(2, '0');
      const dStr = String(i).padStart(2, '0');
      days.push({
        day: i,
        dateString: `${currentYear}-${mStr}-${dStr}`,
        isCurrent: true
      });
    }

    // Remaining Cells (up to 42 / 6 rows)
    const remaining = 42 - days.length;
    for (let i = 1; i <= remaining; i++) {
      const m = currentMonth === 11 ? 0 : currentMonth + 1;
      const y = currentMonth === 11 ? currentYear + 1 : currentYear;
      const mStr = String(m + 1).padStart(2, '0');
      const dStr = String(i).padStart(2, '0');
      days.push({
        day: i,
        dateString: `${y}-${mStr}-${dStr}`,
        isCurrent: false
      });
    }

    return days;
  }, [currentYear, currentMonth]);

  // Partitions daysGrid into weeks lists (chunks of 7)
  const weeks = useMemo(() => {
    const list = [];
    for (let i = 0; i < daysGrid.length; i += 7) {
      list.push(daysGrid.slice(i, i + 7));
    }
    return list;
  }, [daysGrid]);

  // Find the sub-array of 7 days containing selectedCellDate
  const currentWeekDays = useMemo(() => {
    const found = weeks.find(w => w.some(d => d.dateString === selectedCellDate));
    return found || weeks[0] || [];
  }, [weeks, selectedCellDate]);

  // Year view: helper to generate days for any specific month index of the current year (Monday-first)
  const getDaysForMonth = (year: number, monthIndex: number) => {
    // 0=Sun, 1=Mon, 2=Tue, 3=Wed, 4=Thu, 5=Fri, 6=Sat
    const firstDayRaw = new Date(year, monthIndex, 1).getDay();
    const firstDayIndex = firstDayRaw === 0 ? 6 : firstDayRaw - 1; // Mon-first representation
    const totalDays = new Date(year, monthIndex + 1, 0).getDate();
    const prevMonthTotal = new Date(year, monthIndex, 0).getDate();
    const cells = [];

    // Prior Month padding
    for (let i = firstDayIndex - 1; i >= 0; i--) {
      const d = prevMonthTotal - i;
      const m = monthIndex === 0 ? 11 : monthIndex - 1;
      cells.push({ day: d, isCurrent: false, month: m });
    }
    // Current month days
    for (let i = 1; i <= totalDays; i++) {
      cells.push({ day: i, isCurrent: true, month: monthIndex });
    }
    // Next month padding alignment
    const remaining = 42 - cells.length;
    for (let i = 1; i <= remaining; i++) {
      const m = monthIndex === 11 ? 0 : monthIndex + 1;
      cells.push({ day: i, isCurrent: false, month: m });
    }
    return cells;
  };

  const miniDaysGrid = useMemo(() => {
    // Sunday-indexed first day of the month (0 = Sunday, 1 = Monday ...)
    const firstDayIndex = new Date(miniYear, miniMonth, 1).getDay();
    const totalDays = new Date(miniYear, miniMonth + 1, 0).getDate();
    const prevMonthTotalDays = new Date(miniYear, miniMonth, 0).getDate();

    const days = [];

    // Prior Month padding (Sunday-indexed)
    for (let i = firstDayIndex - 1; i >= 0; i--) {
      const d = prevMonthTotalDays - i;
      const m = miniMonth === 0 ? 11 : miniMonth - 1;
      const y = miniMonth === 0 ? miniYear - 1 : miniYear;
      days.push({
        day: d,
        month: m,
        year: y,
        isCurrentMonth: false
      });
    }

    // Current Month
    for (let i = 1; i <= totalDays; i++) {
      days.push({
        day: i,
        month: miniMonth,
        year: miniYear,
        isCurrentMonth: true
      });
    }

    // Next Month padding
    const remaining = 42 - days.length;
    for (let i = 1; i <= remaining; i++) {
      const m = miniMonth === 11 ? 0 : miniMonth + 1;
      const y = miniMonth === 11 ? miniYear + 1 : miniYear;
      days.push({
        day: i,
        month: m,
        year: y,
        isCurrentMonth: false
      });
    }

    return days;
  }, [miniYear, miniMonth]);

  const handleOpenAddModal = (dateStr: string) => {
    setSelectedCellDate(dateStr);
    setNewTitle('');
    setShowAddFormModal(true);
  };

  const handleAddEventSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTitle.trim() || !selectedCellDate) return;

    let colorClasses = 'bg-orange-50 text-orange-700 border-orange-300 hover:bg-orange-100';
    let hoverColor = 'border-orange-500';
    if (newCategory === 'Lời mời tham gia') {
       colorClasses = 'bg-blue-50 text-blue-700 border-blue-300 hover:bg-blue-100';
       hoverColor = 'border-blue-500';
    }
    if (newCategory === 'Lời mời tham gia') {
       colorClasses = 'bg-emerald-50 text-emerald-700 border-emerald-300 hover:bg-emerald-100';
       hoverColor = 'border-emerald-500';
    }

    const checklist = newChecklistStr.split('\n').map(l => l.trim()).filter(l => l.length > 0);

    const newEv: Event = {
      id: 'e_' + Date.now(),
      title: newTitle,
      date: selectedCellDate,
      time: newTime,
      category: newCategory,
      color: colorClasses,
      hoverColor: hoverColor,
      location: newLocation,
      host: newHost,
      guests: newGuests,
      checklist
    };

    setEvents(p => [...p, newEv]);
    setShowAddFormModal(false);
    setActivePopoverEvent(newEv);
  };

  const handleDeleteEvent = (id: string) => {
    setEvents(p => p.filter(e => e.id !== id));
    if (activePopoverEvent?.id === id) {
      setActivePopoverEvent(null);
    }
  };

  return (
    <div className="space-y-6">


    <div className="bg-white rounded-3xl border border-slate-200/85 shadow-md p-4 sm:p-6 md:p-8 font-sans">
      
      {/* Shared Header Bar */}
      <header className="pb-5 border-b border-slate-100 flex flex-wrap items-center justify-between gap-4 mb-6">
        <div>
          <span className="text-[10px] font-bold text-[#f37021] uppercase tracking-widest block mb-0.5">FPT University • PEMS v3.0</span>
          <h1 className="text-xl md:text-2xl font-black text-[#004c91] tracking-tight">
            Lịch chung & theo dõi sự kiện
          </h1>
        </div>

        {/* Google-Calendar-style toolbar button group */}
        <div className="flex items-center gap-4 flex-wrap">
          <div className="bg-slate-100 p-0.5 rounded-xl border border-slate-200 flex items-center gap-1">
            <button
              onClick={handleResetToAugust2026}
              className="px-4 py-2 text-xs font-bold text-slate-700 bg-white shadow-xs hover:bg-slate-50 border border-slate-250/60 rounded-lg transition-all"
            >
              Hôm nay
            </button>
            <div className="h-4 w-px bg-slate-200 mx-1"></div>
            <button
              onClick={handlePrev}
              className="p-2 text-slate-600 hover:bg-white rounded-lg hover:text-slate-800 hover:shadow-3xs transition-all active:scale-95"
              title="Trước"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <button
              onClick={handleNext}
              className="p-2 text-slate-600 hover:bg-white rounded-lg hover:text-slate-800 hover:shadow-3xs transition-all active:scale-95"
              title="Sau"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>

          {/* Month & Year Dropdown Trigger with Mini Calendar Popover */}
          <div className="relative">
            <button
              onClick={() => {
                setMiniMonth(currentMonth);
                setMiniYear(currentYear);
                setShowMiniCalendar(!showMiniCalendar);
                setShowDisplayDropdown(false);
              }}
              className="flex items-center justify-between w-[155px] px-4 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-700 hover:bg-slate-50 transition-colors shadow-3xs"
            >
              <span className="text-slate-800 select-none">
                {displayMode === 'Ngày' && selectedCellDate ? `Ngày ${selectedCellDate.split('-').reverse().join('/')}` :
                 displayMode === 'Tuần' && selectedCellDate ? `Tuần ${(() => {
                   const d = new Date(selectedCellDate);
                   const startYear = new Date(d.getFullYear(), 0, 1);
                   const days = Math.floor((d.getTime() - startYear.getTime()) / (24 * 60 * 60 * 1000));
                   return Math.ceil((d.getDay() + 1 + days) / 7);
                 })()}` :
                 displayMode === 'Năm' ? `Năm ${currentYear}` :
                 `Tháng ${currentMonth + 1}, ${currentYear}`}
              </span>
              <ChevronDown className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
            </button>

            {showMiniCalendar && (
              <>
                {/* Overlay backdrop to close clicking outside */}
                <div 
                  className="fixed inset-0 z-25" 
                  onClick={() => setShowMiniCalendar(false)} 
                />
                
                {/* Popover Card */}
                <div className="absolute right-0 top-full mt-2 w-[280px] bg-white border border-slate-200 rounded-2xl shadow-xl z-30 p-4 animate-fade-in-quick text-slate-800">
                  
                  {/* Miniature header */}
                  <div className="flex items-center justify-between mb-3.5">
                    <span className="text-xs font-extrabold text-slate-700">
                      Tháng {miniMonth + 1} năm {miniYear}
                    </span>
                    <div className="flex items-center gap-1">
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          if (miniMonth === 0) {
                            setMiniMonth(11);
                            setMiniYear(y => y - 1);
                          } else {
                            setMiniMonth(m => m - 1);
                          }
                        }}
                        className="p-1 text-slate-550 hover:bg-slate-50 border border-transparent hover:border-slate-200 rounded-lg hover:shadow-3xs transition-all active:scale-95"
                      >
                        <ChevronLeft className="w-3.5 h-3.5" />
                      </button>
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          if (miniMonth === 11) {
                            setMiniMonth(0);
                            setMiniYear(y => y + 1);
                          } else {
                            setMiniMonth(m => m + 1);
                          }
                        }}
                        className="p-1 text-slate-550 hover:bg-slate-50 border border-transparent hover:border-slate-200 rounded-lg hover:shadow-3xs transition-all active:scale-95"
                      >
                        <ChevronRight className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </div>

                  {/* Week days labels */}
                  <div className="grid grid-cols-7 text-center text-[10px] font-black text-slate-400 mb-2">
                    <div>CN</div>
                    <div>T2</div>
                    <div>T3</div>
                    <div>T4</div>
                    <div>T5</div>
                    <div>T6</div>
                    <div>T7</div>
                  </div>

                  {/* Days grid */}
                  <div className="grid grid-cols-7 text-center gap-y-1 text-xs">
                    {miniDaysGrid.map((cell, idx) => {
                      const mStr = String(cell.month + 1).padStart(2, '0');
                      const dStr = String(cell.day).padStart(2, '0');
                      const cellDateStr = `${cell.year}-${mStr}-${dStr}`;
                      const isSelected = selectedCellDate === cellDateStr;
                      return (
                        <button
                          key={idx}
                          type="button"
                          onClick={() => {
                            setCurrentMonth(cell.month);
                            setCurrentYear(cell.year);
                            setSelectedCellDate(cellDateStr);
                            
                            // Find matching event or clear
                            const ev = events.find(e => e.date === cellDateStr);
                            if (ev) {
                              setActivePopoverEvent(ev);
                            } else {
                              setActivePopoverEvent(null);
                            }
                            setShowMiniCalendar(false);
                          }}
                          className={`w-7 h-7 rounded-full flex items-center justify-center font-bold transition-all mx-auto select-none ${
                            isSelected
                              ? 'bg-[#f37021] text-white shadow-sm font-extrabold scale-105'
                              : cell.isCurrentMonth
                                ? 'text-slate-800 hover:bg-slate-100'
                                : 'text-slate-300 hover:bg-slate-50'
                          }`}
                        >
                          {cell.day}
                        </button>
                      );
                    })}
                  </div>
                </div>
              </>
            )}
          </div>

          {/* Display Mode Dropdown "Hiển thị: " -> Ngày, Tuần, Tháng, Năm */}
          <div className="relative">
            <button
              onClick={() => {
                setShowDisplayDropdown(!showDisplayDropdown);
                setShowMiniCalendar(false);
                setShowTypeDropdown(false);
              }}
              className="flex items-center justify-between w-[150px] px-4 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-700 hover:bg-slate-50 transition-colors shadow-3xs"
            >
              <span className="text-slate-800 font-extrabold select-none">Hiển thị: {displayMode}</span>
              <ChevronDown className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
            </button>

            {showDisplayDropdown && (
              <>
                <div 
                  className="fixed inset-0 z-25" 
                  onClick={() => setShowDisplayDropdown(false)} 
                />
                <div className="absolute right-0 top-full mt-2 w-[150px] bg-white border border-slate-200 rounded-2xl shadow-xl z-30 py-2 animate-fade-in-quick text-slate-800">
                  {(['Ngày', 'Tuần', 'Tháng', 'Năm'] as const).map((mode) => (
                    <button
                      key={mode}
                      type="button"
                      onClick={() => {
                        setDisplayMode(mode);
                        setShowDisplayDropdown(false);
                      }}
                      className={`w-full text-left px-5 py-2.5 text-xs font-bold transition-colors block ${
                        displayMode === mode 
                          ? 'bg-slate-50 text-[#004c91]' 
                          : 'text-slate-700 hover:bg-slate-50'
                      }`}
                    >
                      {mode}
                    </button>
                  ))}
                </div>
              </>
            )}
          </div>

          {/* New Calendar Type Dropdown "Loại lịch: " -> Trong văn phòng, Lịch của tôi */}
          <div className="relative">
            <button
              onClick={() => {
                setShowTypeDropdown(!showTypeDropdown);
                setShowDisplayDropdown(false);
                setShowMiniCalendar(false);
              }}
              className="flex items-center justify-between w-[240px] px-4 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-[#004c91] hover:bg-slate-50 transition-colors shadow-3xs"
            >
              <span className="select-none text-left truncate">Loại lịch: {calendarType}</span>
              <ChevronDown className="w-3.5 h-3.5 text-[#004c91]/75 flex-shrink-0 ml-1" />
            </button>

            {showTypeDropdown && (
              <>
                <div 
                  className="fixed inset-0 z-25" 
                  onClick={() => setShowTypeDropdown(false)} 
                />
                <div className="absolute right-0 top-full mt-2 w-[240px] bg-white border border-slate-200 rounded-2xl shadow-xl z-30 py-2 animate-fade-in-quick text-slate-800">
                  {((isDeptLeader || isStudent || isVisitor) ? ['Lịch của tôi'] : ['Trong văn phòng', 'Lịch của tôi']).map((type) => (
                    <button
                      key={type}
                      type="button"
                      onClick={() => {
                        setCalendarType(type as 'Trong văn phòng' | 'Lịch của tôi');
                        setShowTypeDropdown(false);
                      }}
                      className={`w-full text-left px-5 py-2.5 text-xs font-bold transition-colors block ${
                        calendarType === type 
                          ? 'bg-slate-50 text-[#f37021]' 
                          : 'text-slate-700 hover:bg-slate-50'
                      }`}
                    >
                      {type}
                    </button>
                  ))}
                </div>
              </>
            )}
          </div>
        </div>
      </header>

      {/* Grid of Calendar (Full Width) */}
      <div className="relative">
        <div className="w-full">
          
           {/* Calendar Container */}
          <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm overflow-hidden flex flex-col">
            
            {/* 1. MONTH VIEW */}
            {displayMode === 'Tháng' && (
              <>
                {/* Days of the week header */}
                <div className="grid grid-cols-7 bg-[#004c91] border-b border-[#002f63] text-center text-xs font-extrabold text-white uppercase tracking-wider py-4">
                  <div>Thứ Hai</div>
                  <div>Thứ Ba</div>
                  <div>Thứ Tư</div>
                  <div>Thứ Năm</div>
                  <div>Thứ Sáu</div>
                  <div>Thứ Bảy</div>
                  <div>Chủ Nhật</div>
                </div>

                {/* Grid of Days */}
                <div className="grid grid-cols-7 grid-rows-6 flex-grow h-[690px] divide-x divide-y divide-slate-100 bg-slate-50/20">
                  {daysGrid.map((cell, idx) => {
                    const dayEvents = filteredEvents.filter(e => e.date === cell.dateString);
                    const isSelected = selectedCellDate === cell.dateString;
                    return (
                      <div
                        key={idx}
                        onClick={() => {
                          setSelectedCellDate(cell.dateString);
                          const dayEvs = filteredEvents.filter(e => e.date === cell.dateString);
                          if (dayEvs.length > 0) {
                            setDisplayMode('Ngày');
                          }
                        }}
                        className={`h-[115px] max-h-[115px] overflow-hidden p-2 flex flex-col justify-between transition-colors group relative cursor-pointer ${
                          isSelected
                            ? 'bg-orange-50 ring-2 ring-inset ring-[#f37021] z-10 shadow-sm'
                            : cell.isCurrent
                              ? 'bg-white hover:bg-orange-50/80 text-slate-800'
                              : 'bg-slate-50/30 hover:bg-orange-50/30 text-slate-350'
                        }`}
                      >
                        {/* Header of Date cell */}
                        <div className="flex justify-between items-center mb-1">
                          <span className={`text-xs font-extrabold px-1.5 py-0.5 rounded-md ${
                            cell.dateString === '2026-08-26' && cell.isCurrent
                              ? 'bg-red-500 text-white shadow-xs'
                              : isSelected
                                ? 'bg-[#f37021] text-white'
                                : cell.isCurrent ? 'text-slate-700' : 'text-slate-400'
                          }`}>
                            {cell.day}
                          </span>
                          
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              handleOpenAddModal(cell.dateString);
                            }}
                            className="opacity-0 group-hover:opacity-100 text-[#f37021] hover:text-[#004c91] transition-opacity p-0.5 hover:bg-orange-100 rounded-md cursor-pointer"
                            title="Add Logistics Event"
                          >
                            <Plus className="w-3.5 h-3.5" />
                          </button>
                        </div>

                        {/* Event cards space */}
                        <div className="flex-grow space-y-1 overflow-y-auto no-scrollbar pt-1">
                          {dayEvents.map(ev => {
                            const isHighlighted = activePopoverEvent?.id === ev.id;
                            return (
                              <div
                                key={ev.id}
                                id={`event-card-${ev.id}`}
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setSelectedCellDate(cell.dateString);
                                  setActivePopoverEvent(ev);
                                }}
                                className={`px-2 py-1.5 rounded-lg border text-[10px] font-bold leading-tight cursor-pointer transition-all truncate selection:bg-transparent ${ev.color} ${ev.hoverColor} ${
                                  isHighlighted ? 'ring-2 ring-orange-500/10 border-orange-400 shadow-sm' : ''
                                }`}
                              >
                                <span className="inline-block w-1.5 h-1.5 rounded-full mr-1.5 bg-current" />
                                {ev.title}
                              </div>
                            );
                          })}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </>
            )}

            {/* 2. WEEK VIEW */}
            {displayMode === 'Tuần' && (
              <>
                <div className="grid grid-cols-7 bg-[#004c91] border-b border-[#002f63] text-center text-xs font-extrabold text-white uppercase tracking-wider py-4">
                  <div>Thứ Hai</div>
                  <div>Thứ Ba</div>
                  <div>Thứ Tư</div>
                  <div>Thứ Năm</div>
                  <div>Thứ Sáu</div>
                  <div>Thứ Bảy</div>
                  <div>Chủ Nhật</div>
                </div>

                <div className="grid grid-cols-7 flex-grow min-h-[160px] pb-2 divide-x divide-slate-100 bg-slate-50/20">
                  {currentWeekDays.map((cell, idx) => {
                    const dayEvents = filteredEvents.filter(e => e.date === cell.dateString);
                    const isSelected = selectedCellDate === cell.dateString;
                    return (
                      <div
                        key={idx}
                        onClick={() => {
                          setSelectedCellDate(cell.dateString);
                          const dayEvs = filteredEvents.filter(e => e.date === cell.dateString);
                          if (dayEvs.length > 0) {
                            setDisplayMode('Ngày');
                          }
                        }}
                        className={`p-3.5 flex flex-col justify-between transition-colors group relative cursor-pointer ${
                          isSelected
                            ? 'bg-orange-50 ring-2 ring-inset ring-[#f37021] z-10 shadow-sm'
                            : cell.isCurrent
                              ? 'bg-white hover:bg-orange-50 text-slate-800'
                              : 'bg-slate-50/30 hover:bg-orange-50/60 text-slate-350'
                        }`}
                      >
                        <div className="flex justify-between items-center mb-2">
                          <span className={`text-xs font-extrabold px-2 py-1 rounded-md ${
                            cell.dateString === '2026-08-26' && cell.isCurrent
                              ? 'bg-red-500 text-white'
                              : isSelected 
                                ? 'bg-[#f37021] text-white shadow-xs' 
                                : 'text-slate-700 bg-slate-100'
                          }`}>
                            {cell.day}
                          </span>
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              handleOpenAddModal(cell.dateString);
                            }}
                            className="opacity-0 group-hover:opacity-100 text-[#f37021] hover:text-[#004c91] transition-opacity p-0.5 hover:bg-orange-100 rounded-md"
                            title="Thêm công việc"
                          >
                            <Plus className="w-3.5 h-3.5" />
                          </button>
                        </div>

                        <div className="flex-grow space-y-1.5 overflow-y-auto no-scrollbar pt-1">
                          {dayEvents.map(ev => {
                            const isHighlighted = activePopoverEvent?.id === ev.id;
                            return (
                              <div
                                key={ev.id}
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setSelectedCellDate(cell.dateString);
                                  setActivePopoverEvent(ev);
                                }}
                                className={`px-2 py-2 rounded-lg border text-[10px] font-bold leading-tight cursor-pointer transition-all ${ev.color} ${ev.hoverColor} ${
                                  isHighlighted ? 'ring-2 ring-[#f37021]/30 border-[#f37021] shadow-sm font-extrabold scale-[1.01]' : ''
                                }`}
                              >
                                <span className="inline-block w-1.5 h-1.5 rounded-full mr-1.5 bg-current" />
                                {ev.title}
                              </div>
                            );
                          })}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </>
            )}

            {/* 3. DAY VIEW */}
            {displayMode === 'Ngày' && (
              <div className="p-6 flex flex-col flex-grow min-h-[640px]">
                {/* Quay lại button */}
                <div className="mb-4">
                  <button
                    type="button"
                    onClick={() => setDisplayMode('Tháng')}
                    className="flex items-center gap-2 px-3.5 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-[11px] font-black rounded-xl transition-all shadow-3xs border border-slate-200/60"
                  >
                    <ChevronLeft className="w-3.5 h-3.5 text-slate-600" />
                    <span>Quay lại lịch tháng</span>
                  </button>
                </div>

                <div className="flex justify-between items-center pb-4 border-b border-slate-100 mb-5">
                  <div>
                    <h3 className="text-sm font-extrabold text-[#004c91] uppercase tracking-wider">
                      Lịch trình chi tiết ngày {(() => {
                        if (!selectedCellDate) return 'chưa chọn';
                        const parts = selectedCellDate.split('-');
                        return `${parts[2]}/${parts[1]}/${parts[0]}`;
                      })()}
                    </h3>
                    <p className="text-xs text-slate-500 mt-1 font-medium">Báo cáo hậu cần nội bộ trực tiếp cho điều phối viên</p>
                  </div>
                  
                  <button
                    onClick={() => selectedCellDate && handleOpenAddModal(selectedCellDate)}
                    className="flex items-center gap-1.5 px-3.5 py-2 bg-[#f37021] text-white text-xs font-black rounded-lg hover:opacity-90 active:scale-95 transition-all shadow-sm"
                  >
                    <Plus className="w-3.5 h-3.5" />
                    <span>Thêm sự kiện</span>
                  </button>
                </div>

                {(() => {
                  const dayEvents = filteredEvents.filter(e => e.date === selectedCellDate);
                  if (dayEvents.length === 0) {
                    return (
                      <div className="flex flex-col items-center justify-center py-24 text-center flex-grow">
                        <div className="w-16 h-16 bg-slate-50 border border-slate-150 rounded-full flex items-center justify-center text-slate-350 mb-4 shadow-3xs">
                          <CalendarIcon className="w-7 h-7" />
                        </div>
                        <h4 className="text-xs font-black text-slate-700">Không có sự kiện hậu cần nào</h4>
                        <p className="text-[11px] text-slate-400 mt-1 max-w-xs font-medium">
                          Ngày này hiện chưa có chương trình đón tiếp hay cuộc họp quốc tế nào được thiết lập.
                        </p>
                      </div>
                    );
                  }

                  return (
                    <div className="space-y-4 flex-grow overflow-y-auto no-scrollbar max-h-[500px] pr-1">
                      {dayEvents.map((ev) => {
                        const isHighlighted = activePopoverEvent?.id === ev.id;
                        return (
                          <div
                            key={ev.id}
                            onClick={() => setActivePopoverEvent(ev)}
                            className={`p-4 rounded-xl border transition-all cursor-pointer relative ${ev.color} ${ev.hoverColor} ${
                              isHighlighted ? 'ring-2 ring-[#f37021] border-[#f37021] scale-[1.002]' : 'border-slate-100'
                            }`}
                          >
                            <div className="flex items-start justify-between gap-3">
                              <div className="flex items-center gap-2">
                                <span className="w-2.5 h-2.5 rounded-full bg-current" />
                                <span className="text-[10px] font-black uppercase tracking-wider opacity-90">{ev.category}</span>
                              </div>
                              <span className="text-[11px] font-bold opacity-80">{ev.time}</span>
                            </div>
                            
                            <h4 className="text-sm font-black mt-2 leading-snug">{ev.title}</h4>
                            
                            <div className="mt-3.5 grid grid-cols-1 md:grid-cols-2 gap-2 text-[11px] font-medium opacity-90 border-t border-current/10 pt-2.5">
                              <div className="flex items-center gap-1.5">
                                <MapPin className="w-3.5 h-3.5 shrink-0 text-[#f37021]" />
                                <span className="truncate">{ev.location}</span>
                              </div>
                              <div className="flex items-center gap-1.5">
                                <Users className="w-3.5 h-3.5 shrink-0 text-[#004c91]" />
                                <span className="truncate">{ev.guests}</span>
                              </div>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  );
                })()}
              </div>
            )}

            {/* 4. YEAR VIEW */}
            {displayMode === 'Năm' && (
              <div className="p-6 overflow-y-auto no-scrollbar max-h-[720px]">
                <h3 className="text-sm font-extrabold text-[#004c91] uppercase tracking-wider mb-6 text-center">
                  Tổng quan danh mục sự kiện năm {currentYear}
                </h3>
                
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                  {monthNames.map((mName, mIdx) => {
                    const mDays = getDaysForMonth(currentYear, mIdx);
                    return (
                      <div key={mIdx} className="bg-slate-50/50 p-3 rounded-2xl border border-slate-150/80 hover:border-orange-200 transition-colors">
                        <h4 className="text-xs font-black text-[#004c91] text-center mb-2.5">{mName}</h4>
                        
                        {/* Mon-Sun labels representation */}
                        <div className="grid grid-cols-7 text-center text-[9px] font-black text-slate-400 mb-1">
                          <div>T2</div><div>T3</div><div>T4</div><div>T5</div><div>T6</div><div>T7</div><div>CN</div>
                        </div>
                        
                        <div className="grid grid-cols-7 text-center gap-0.5 text-[10px]">
                          {mDays.map((cell, cIdx) => {
                            const mStr = String(cell.month + 1).padStart(2, '0');
                            const dStr = String(cell.day).padStart(2, '0');
                            const cellDateStr = `${currentYear}-${mStr}-${dStr}`;
                            const isSelected = selectedCellDate === cellDateStr;
                            const hasEvents = filteredEvents.some(e => e.date === cellDateStr);

                            return (
                              <button
                                key={cIdx}
                                type="button"
                                onClick={() => {
                                  setCurrentMonth(mIdx);
                                  setSelectedCellDate(cellDateStr);
                                  setDisplayMode('Tháng');
                                }}
                                className={`w-5 h-5 rounded-full flex items-center justify-center font-bold transition-all mx-auto ${
                                  isSelected
                                    ? 'bg-[#f37021] text-white font-black'
                                    : hasEvents
                                      ? 'bg-orange-100 text-orange-700 hover:bg-orange-200'
                                      : cell.isCurrent
                                        ? 'text-slate-700 hover:bg-slate-200'
                                        : 'text-slate-350 hover:bg-slate-100/50'
                                }`}
                              >
                                {cell.day}
                              </button>
                            );
                          })}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

          </div>

          {/* Left / Right Panel Side Card statistics & active popover tooltip */}
          <div className="hidden">
            
            {/* Popover Display panel (Active Popover Tooltip showcase) */}
            {activePopoverEvent ? (
              <div className="bg-white rounded-2xl border-2 border-slate-200 shadow-lg overflow-hidden relative transition-all duration-300 animate-fade-in-quick">
                
                {/* Decorative Festive top banner card header for Festive category */}
                {activePopoverEvent.category === 'Lời mời tham gia' ? (
                  <div className="bg-gradient-to-r from-blue-700 to-[#004c91] p-4 text-white relative">
                    <div className="absolute top-0 right-0 p-8 bg-[radial-gradient(#ffffff_1px,transparent_1px)] opacity-10 pointer-events-none" style={{ backgroundSize: '12px 12px' }}></div>
                    <div className="flex justify-between items-start">
                      <div className="flex items-center gap-2">
                        <span className="bg-blue-800 text-blue-200 border border-blue-300/30 text-[9px] font-black uppercase px-2 py-0.5 rounded-full tracking-widest shadow-inner">
                          ★ VIP Đón tiếp ★
                        </span>
                        <Sparkles className="w-4 h-4 text-blue-250 animate-pulse" />
                      </div>

                      {/* Clean action icons at the top-right corner of the tooltip */}
                      <div className="flex items-center gap-1.5 relative z-10 bg-black/10 p-1 rounded-lg border border-white/10">
                        <button
                          onClick={() => handleDeleteEvent(activePopoverEvent.id)}
                          className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors"
                          title="Trash bin"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                        <button className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors" title="More options">
                          <MoreVertical className="w-3.5 h-3.5" />
                        </button>
                        <button
                          onClick={() => setActivePopoverEvent(null)}
                          className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors"
                          title="Close"
                        >
                          <X className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>

                    <div className="mt-4">
                      <span className="text-[10px] uppercase font-bold tracking-widest text-amber-200">Tiêu điểm Phái đoàn Khách mời</span>
                      <h4 className="text-[17px] font-black text-white mt-1 leading-snug drop-shadow-xs">
                        {activePopoverEvent.title}
                      </h4>
                    </div>
                  </div>
                ) : (
                  <div className="bg-gradient-to-r from-[#004c91] to-blue-700 p-4 text-white relative">
                    <div className="flex justify-between items-start">
                      <div className="flex items-center gap-2">
                        <span className="bg-blue-900 border border-blue-500/30 text-[9px] font-bold uppercase px-2.5 py-0.5 rounded-full tracking-widest">
                          Mục {activePopoverEvent.category}
                        </span>
                      </div>

                      {/* Action icons */}
                      <div className="flex items-center gap-1.5 relative z-10 bg-black/10 p-1 rounded-lg border border-white/10">
                        <button
                          onClick={() => handleDeleteEvent(activePopoverEvent.id)}
                          className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors"
                          title="Xóa sự kiện"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                        <button className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors" title="Tùy chọn khác">
                          <MoreVertical className="w-3.5 h-3.5" />
                        </button>
                        <button
                          onClick={() => setActivePopoverEvent(null)}
                          className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors"
                          title="Đóng"
                        >
                          <X className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>

                    <div className="mt-4">
                      <span className="text-[10px] uppercase font-bold tracking-widest text-blue-200">Sự kiện Hậu cần PEMS</span>
                      <h4 className="text-[17px] font-black text-white mt-1 leading-snug">
                        {activePopoverEvent.title}
                      </h4>
                    </div>
                  </div>
                )}

                {/* Event Details Content Area */}
                <div className="p-5 space-y-3.5 text-slate-800 text-xs max-h-[580px] overflow-y-auto no-scrollbar">
                  
                  {/* VIP Level Badge Row (if any) */}
                  {activePopoverEvent.vipLevel && (
                    <div className="flex gap-3 pb-3 border-b border-slate-100">
                      <div className="w-8 h-8 rounded-lg bg-red-50 text-red-600 flex items-center justify-center shrink-0">
                        <Sparkles className="w-4 h-4" />
                      </div>
                      <div>
                        <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Cấp độ tiếp đón ngoại giao</span>
                        <span className={`inline-block text-[9px] font-black uppercase px-2 py-0.5 mt-1 rounded ${
                          activePopoverEvent.vipLevel === 'VVIP' 
                            ? 'bg-red-600 text-white animate-pulse'
                            : activePopoverEvent.vipLevel === 'VIP'
                              ? 'bg-amber-550 bg-[#f37021] text-white'
                              : 'bg-slate-100 text-slate-650'
                        }`}>
                          {activePopoverEvent.vipLevel} CLASS / GUEST
                        </span>
                      </div>
                    </div>
                  )}

                  {/* Purpose / Work info */}
                  {activePopoverEvent.purpose && (
                    <div className="flex gap-3 pb-3 border-b border-slate-100">
                      <div className="w-8 h-8 rounded-lg bg-amber-50 text-amber-700 flex items-center justify-center shrink-0">
                        <FileText className="w-4 h-4" />
                      </div>
                      <div>
                        <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Mục đích đón tiếp & Nội dung làm việc</span>
                        <p className="font-bold text-slate-700 mt-0.5 leading-relaxed">{activePopoverEvent.purpose}</p>
                      </div>
                    </div>
                  )}

                  {/* Time field */}
                  <div className="flex gap-3 pb-3 border-b border-slate-100">
                    <div className="w-8 h-8 rounded-lg bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
                      <Clock className="w-4 h-4" />
                    </div>
                    <div>
                      <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Ngày & Giờ diễn ra</span>
                      <p className="font-bold text-slate-700 mt-0.5 leading-relaxed">
                        {(() => {
                          const d = new Date(activePopoverEvent.date);
                          const weekdays = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
                          return `${weekdays[d.getDay() || 0]}, ngày ${d.getDate()} tháng ${d.getMonth() + 1} năm ${d.getFullYear()}`;
                        })()}
                      </p>
                      <p className="font-bold text-[#f37021] mt-0.5">{activePopoverEvent.time}</p>
                    </div>
                  </div>

                  {/* Location field */}
                  <div className="flex gap-3 pb-3 border-b border-slate-100">
                    <div className="w-8 h-8 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
                      <MapPin className="w-4 h-4" />
                    </div>
                    <div>
                      <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Địa điểm & Vị trí tổ chức</span>
                      <p className="font-bold text-slate-700 mt-0.5 leading-normal">{activePopoverEvent.location}</p>
                    </div>
                  </div>

                  {/* Vehicle scheduling details */}
                  {activePopoverEvent.carBooking && (
                    <div className="flex gap-3 pb-3 border-b border-slate-100">
                      <div className="w-8 h-8 rounded-lg bg-cyan-50 text-cyan-700 flex items-center justify-center shrink-0">
                        <TrendingUp className="w-4 h-4" />
                      </div>
                      <div>
                        <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Xe công vụ & Đưa đón</span>
                        <p className="font-semibold text-slate-650 mt-1 leading-relaxed">{activePopoverEvent.carBooking}</p>
                      </div>
                    </div>
                  )}

                  {/* Banner / Welcoming Text */}
                  {activePopoverEvent.bannerText && (
                    <div className="flex gap-3 pb-3 border-b border-slate-100">
                      <div className="w-8 h-8 rounded-lg bg-red-550/10 bg-red-50 text-red-650 flex items-center justify-center shrink-0">
                        <Bell className="w-4 h-4" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Khẩu hiệu chào mừng trên màn hình LED</span>
                        <div className="bg-slate-900 text-yellow-300 font-mono text-[9px] p-2 mt-1 rounded border border-slate-950 leading-snug break-words">
                          {activePopoverEvent.bannerText}
                        </div>
                      </div>
                    </div>
                  )}

                  {/* Host field */}
                  <div className="flex gap-3 pb-3 border-b border-slate-100">
                    <div className="w-8 h-8 rounded-lg bg-purple-50 text-purple-600 flex items-center justify-center shrink-0">
                      <Bookmark className="w-4 h-4" />
                    </div>
                    <div>
                      <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Bộ phận FPTU chủ trì / Host</span>
                      <p className="font-bold text-slate-700 mt-0.5 leading-normal">{activePopoverEvent.host}</p>
                    </div>
                  </div>

                  {/* Contact Person Details */}
                  {activePopoverEvent.contactPerson && (
                    <div className="flex gap-3 pb-3 border-b border-slate-100">
                      <div className="w-8 h-8 rounded-lg bg-emerald-50 text-emerald-600 flex items-center justify-center shrink-0">
                        <Users className="w-4 h-4" />
                      </div>
                      <div>
                        <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Cán bộ điều phối liên hệ</span>
                        <p className="font-bold text-emerald-700 mt-0.5">{activePopoverEvent.contactPerson}</p>
                      </div>
                    </div>
                  )}

                  {/* Hotel list information */}
                  {activePopoverEvent.hotelInfo && (
                    <div className="flex gap-3 pb-3 border-b border-slate-100">
                      <div className="w-8 h-8 rounded-lg bg-pink-50 text-pink-600 flex items-center justify-center shrink-0">
                        <MapPin className="w-4 h-4" />
                      </div>
                      <div>
                        <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Khách sạn lưu trú đoàn khách</span>
                        <p className="font-semibold text-slate-650 mt-1 leading-relaxed">{activePopoverEvent.hotelInfo}</p>
                      </div>
                    </div>
                  )}

                  {/* Guests field */}
                  <div className="flex gap-3 pb-3 border-b border-slate-100">
                    <div className="w-8 h-8 rounded-lg bg-sky-50 text-sky-600 flex items-center justify-center shrink-0">
                      <Users className="w-4 h-4" />
                    </div>
                    <div>
                      <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Chi tiết đoàn khách đối tác</span>
                      <p className="font-bold text-slate-700 mt-0.5 leading-relaxed">{activePopoverEvent.guests}</p>
                    </div>
                  </div>

                  {/* Logistics Checklist */}
                  <div className="space-y-2.5 pt-1">
                    <span className="text-[10px] font-extrabold text-[#f37021] uppercase tracking-widest block">
                      ✔ Checklist nhiệm vụ hậu cần:
                    </span>
                    <ul className="space-y-1.5 bg-slate-50 p-3 rounded-xl border border-slate-200/60 font-medium text-slate-650">
                      {activePopoverEvent.checklist.map((item, idx) => (
                        <li key={idx} className="flex items-start gap-2">
                          <CheckSquare className="w-3.5 h-3.5 text-emerald-500 shrink-0 mt-0.5" />
                          <span>{item}</span>
                        </li>
                      ))}
                    </ul>
                  </div>

                </div>
              </div>
            ) : (
              <div className="bg-slate-50 rounded-2xl border-2 border-dashed border-slate-200 p-6 text-center text-slate-400">
                <Info className="w-8 h-8 mx-auto text-slate-350 mb-2" />
                <p className="text-xs font-bold">Vui lòng chọn bất kỳ sự kiện nào trên lịch để rà soát chi tiết hậu cần & thao tác.</p>
              </div>
            )}

            {/* Dynamic Month/Year Events list list sidebar (Lịch chi tiết toàn bộ sự kiện của tháng) */}
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5 space-y-4">
              <div>
                <h3 className="text-sm font-extrabold text-[#004c91] uppercase tracking-wider">
                  Các mốc sự kiện trong {monthNames[currentMonth]} năm {currentYear}
                </h3>
                <p className="text-[10px] text-slate-400 mt-0.5 font-semibold">Tự động tập hợp dựa trên lịch học & phái đoàn đã khởi tạo</p>
              </div>

              <div className="space-y-2.5 max-h-[360px] overflow-y-auto no-scrollbar pr-0.5">
                {eventsInCurrentMonthAndYear.length === 0 ? (
                  <div className="text-center py-6 text-slate-400 text-xs italic">
                    Không có chương trình hay đoàn khách tiếp đón nào được đăng ký trong tháng này.
                  </div>
                ) : (
                  eventsInCurrentMonthAndYear.map((ev) => {
                    const isSelected = selectedCellDate === ev.date && activePopoverEvent?.id === ev.id;
                    const isTodayHighlight = ev.date === '2026-08-26';
                    const parts = ev.date.split('-');
                    const displayDayNum = parts[2];
                    
                    return (
                      <div
                        key={ev.id}
                        onClick={() => {
                          setSelectedCellDate(ev.date);
                          setActivePopoverEvent(ev);
                        }}
                        className={`p-3 rounded-xl border text-xs cursor-pointer transition-all ${
                          isSelected
                            ? 'bg-orange-50/90 border-[#f37021] ring-1 ring-[#f37021] text-slate-800'
                            : 'bg-slate-50 hover:bg-orange-50/40 hover:border-orange-200 text-slate-700 border-slate-100'
                        }`}
                      >
                        <div className="flex justify-between items-center gap-1.5 mb-2 leading-none">
                          <span className={`text-[9px] font-black uppercase tracking-wider px-2 py-0.5 rounded ${
                            ev.category === 'Lời mời tham gia' 
                              ? 'bg-blue-100 text-blue-800' 
                              : ev.category === 'Lời mời tham gia'
                                ? 'bg-emerald-100 text-emerald-800'
                                : 'bg-orange-100 text-orange-800'
                          }`}>
                            {ev.category}
                          </span>
                          <span className="text-[10px] font-bold text-[#f37021]">
                            {ev.time}
                          </span>
                        </div>
                        
                        <h4 className="font-extrabold text-[#004c91] text-[11px] mb-1.5 leading-snug line-clamp-2">
                          {ev.title}
                        </h4>

                        <div className="flex items-center justify-between text-[10px] font-extrabold text-slate-450 border-t border-slate-100 pt-1.5 mt-1.5">
                          <span className="text-slate-500 font-extrabold">Ngày {displayDayNum} {monthNames[currentMonth]}</span>
                          <span className="text-slate-400 font-medium truncate max-w-[110px]">{ev.location}</span>
                        </div>
                      </div>
                    );
                  })
                )}
              </div>

              <div className="p-3 bg-[#004c91]/5 rounded-xl flex items-center justify-between gap-3 text-[11px] leading-relaxed font-bold text-slate-650 border border-[#004c91]/10">
                <span>Tổng số sự kiện cần rà soát:</span>
                <span className="font-black text-[#004c91] bg-white border border-[#004c91]/20 px-2 py-0.5 rounded-md shadow-3xs">
                  {eventsInCurrentMonthAndYear.length} sự kiện
                </span>
              </div>
            </div>

          </div>
        </div>
      </div>

      {/* Add event modal */}
      {showAddFormModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl max-w-3xl w-full border border-slate-200 shadow-2xl overflow-hidden animate-fade-in-quick">
            
            <div className="bg-[#004c91] px-5 py-4 text-white flex justify-between items-center">
              <h3 className="font-black text-sm flex items-center gap-2">
                <CalendarIcon className="w-4 h-4 text-[#f37021]" />
                Lên Lịch Công Tác ({selectedCellDate})
              </h3>
              <button 
                onClick={() => setShowAddFormModal(false)}
                className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded-full transition-colors"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleAddEventSubmit} className="p-6 space-y-4 text-xs text-slate-800">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                {/* Left Column */}
                <div className="space-y-4">
                  <div>
                    <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                      Tiêu đề sự kiện *
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="VD: Tiếp đón hiệu trưởng đại học đối tác"
                      value={newTitle}
                      onChange={e => setNewTitle(e.target.value)}
                      className="w-full text-xs px-3.5 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                    />
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                        Khung giờ
                      </label>
                      <input
                        type="text"
                        required
                        value={newTime}
                        onChange={e => setNewTime(e.target.value)}
                        className="w-full text-xs px-3.5 py-2.5 border border-[#f37021] md:border-slate-200 rounded-xl focus:border-[#f37021] outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                        Phân loại
                      </label>
                      <select
                        value={newCategory}
                        onChange={e => setNewCategory(e.target.value as Event['category'])}
                        className="w-full text-xs px-3 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none cursor-pointer"
                      >
                        {!isVisitor && <option value="Lời mời tham gia">Thư mời</option>}
                        {!(isStudent || isVisitor) && <option value="Đơn yêu cầu mượn đồ">Đơn yêu cầu</option>}
                      </select>
                    </div>
                  </div>

                  <div>
                    <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                      Địa điểm tổ chức
                    </label>
                    <input
                      type="text"
                      value={newLocation}
                      onChange={e => setNewLocation(e.target.value)}
                      className="w-full text-xs px-3.5 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                    />
                  </div>
                </div>

                {/* Right Column */}
                <div className="space-y-4">
                  <div>
                    <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                      Đơn vị FPTU Chủ trì / Host
                    </label>
                    <input
                      type="text"
                      value={newHost}
                      onChange={e => setNewHost(e.target.value)}
                      className="w-full text-xs px-3.5 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                      Chi tiết phái đoàn đối tác khách
                    </label>
                    <input
                      type="text"
                      value={newGuests}
                      onChange={e => setNewGuests(e.target.value)}
                      className="w-full text-xs px-3.5 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                      Logistics Checklist (Mỗi dòng một nhiệm vụ)
                    </label>
                    <textarea
                      rows={3}
                      value={newChecklistStr}
                      onChange={e => setNewChecklistStr(e.target.value)}
                      className="w-full text-xs px-3.5 py-2 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none resize-none font-sans bg-slate-50/20"
                    />
                  </div>
                </div>
              </div>

              <div className="flex justify-end gap-2.5 pt-4 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setShowAddFormModal(false)}
                  className="py-2.5 px-4 bg-slate-150 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors cursor-pointer"
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  className="py-2.5 px-7 bg-[#f37021] text-white font-black rounded-xl hover:opacity-90 active:scale-98 transition-all cursor-pointer shadow-3xs"
                >
                  Xác nhận lưu
                </button>
              </div>

            </form>
          </div>
        </div>
      )}

      {/* Wide Horizontal Table Modal representing Giai đoạn 1: Trước tiếp khách */}
      {activePopoverEvent && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4 overflow-y-auto">
          <div className="bg-white rounded-2xl max-w-5xl w-full border border-slate-200 shadow-2xl overflow-hidden animate-fade-in-quick flex flex-col my-8">
            
            {/* Modal Title Banner */}
            <div className={`${activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'accepted' ? 'bg-[#f37021]' : 'bg-[#004c91]'} px-6 py-5 text-white flex justify-between items-center relative shadow-sm border-b border-white/10`}>
              <div className="flex items-center gap-3">
                <div className="p-2.5 rounded-xl bg-white/10 border border-white/20 flex items-center justify-center">
                  {activePopoverEvent.category === 'Lời mời tham gia' || (activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'pending') ? <Info className="w-5 h-5 text-white" /> : <FileText className="w-5 h-5 text-white" />}
                </div>
                <div>
                  <h3 className={`font-extrabold tracking-tight text-white leading-tight font-sans ${activePopoverEvent.category === 'Lời mời tham gia' || (activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'pending') ? 'text-xl md:text-3xl' : 'text-base md:text-lg'}`}>
                    {activePopoverEvent.category === 'Lời mời tham gia' ? 'Chi tiết thư mời' : activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'pending' ? 'Thông tin chi tiết' : activePopoverEvent.title}
                  </h3>
                  <p className={`text-white/80 mt-1 ${activePopoverEvent.category === 'Lời mời tham gia' ? 'text-sm font-medium' : 'text-[11px] mt-0.5'}`}>
                    {activePopoverEvent.category === 'Lời mời tham gia'
                      ? 'Thông tin sự kiện'
                      : activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'pending'
                        ? 'Nhiệm vụ được giao'
                        : activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'accepted'
                        ? 'Hồ sơ pháp lý: Biên bản bàn giao kỹ thuật & Nghiệm thu xe điện đối ngoại' 
                        : 'Bảng chi tiết thông tin và phương án cơ sở vật chất đón tiếp phái đoàn'}
                  </p>
                </div>
              </div>

              <button 
                onClick={() => setActivePopoverEvent(null)}
                className="text-white/85 hover:text-white p-2 hover:bg-white/10 rounded-full transition-all flex items-center justify-center shadow-3xs"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Modal Contents in a clean wide Horizontal Table layout */}
            <div className="p-6 md:p-8 space-y-4 overflow-y-auto max-h-[70vh] no-scrollbar bg-slate-50/50">
                {/* Xem chi tiết đoàn đón khách */}
                <div className="w-full">
                  <button
                    onClick={() => setShowDetailSection(!showDetailSection)}
                    className="w-full flex items-center justify-between px-5 py-3.5 bg-orange-50 hover:bg-orange-100 text-[#f37021] font-black rounded-xl transition-colors border border-orange-200"
                  >
                    <span className="flex items-center gap-2 text-sm uppercase tracking-wider">
                      <Users className="w-5 h-5" /> Xem chi tiết đoàn đón khách
                    </span>
                    {showDetailSection ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                  </button>
                  
                  {showDetailSection && (
                    <div className="mt-4 bg-white border border-orange-100 rounded-2xl shadow-sm overflow-hidden animate-fade-in-quick text-sm">
                      
                      {/* 1. Thông tin người tạo */}
                      <div className="p-5 border-b border-orange-100">
                        <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                          <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">1</span>
                          Thông tin người tạo
                        </h4>
                        <p className="text-xs text-slate-500 mb-4">Chi tiết về người liên hệ, đơn vị phụ trách đăng ký lịch</p>
                        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 bg-slate-50 p-4 rounded-xl text-xs">
                          <div>
                            <p className="text-slate-500 mb-1">Họ và tên</p>
                            <p className="font-bold text-slate-800">Nguyễn Hữu Trí</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Email</p>
                            <p className="font-bold text-slate-800">tringuyenh@fpt.edu.vn</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Đơn vị công tác</p>
                            <p className="font-bold text-slate-800">Office of International Affairs (OIA) & Staff Leaders</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Chức danh</p>
                            <p className="font-bold text-slate-800">Director of OIA</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Số điện thoại (SĐT)</p>
                            <p className="font-bold text-slate-800">0905.111.222</p>
                          </div>
                        </div>
                      </div>

                      {/* 2. Thông tin đoàn khách */}
                      <div className="p-5 border-b border-orange-100">
                        <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                          <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">2</span>
                          Thông tin đoàn khách
                        </h4>
                        <p className="text-xs text-slate-500 mb-4">Tên cơ quan, thời gian, cơ sở hoạt động và mục đích đối ngoại</p>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 bg-slate-50 p-4 rounded-xl text-xs mb-4">
                          <div>
                            <p className="text-slate-500 mb-1">Tên đoàn</p>
                            <p className="font-bold text-slate-800">15+ International Professors & 40 inbound exchange students</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Cơ sở đón tiếp</p>
                            <p className="font-bold text-slate-800">Grand Hall, Alpha Campus, FPT University</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Ngày bắt đầu</p>
                            <p className="font-bold text-slate-800">Thứ Tư, 26/8/2026</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Thời gian</p>
                            <p className="font-bold text-slate-800">18:00 - 22:00</p>
                          </div>
                        </div>
                        <div className="space-y-3 bg-slate-50 p-4 rounded-xl text-xs">
                          <div>
                            <p className="text-slate-500 mb-1 font-bold">Mục đích thăm</p>
                            <p className="text-slate-800">Giao lưu Tất niên đậm bản sắc văn hóa Việt dành cho đội ngũ giáo sư & sinh viên nước ngoài.</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1 font-bold">Nội dung làm việc</p>
                            <p className="text-slate-800">Phiên làm việc phối hợp chặt chẽ giữa đơn vị chủ trì Đại học FPT cùng đoàn đối tác nhằm cụ thể hoá các hoạt động trao đổi văn hoá học thuật, rà soát chi tiết cơ sở vật chất hạ tầng của cơ sở Hòa Lạc phục vụ học viên quốc tế học tập, nghiên cứu và sinh hoạt đối ngoại.</p>
                          </div>
                        </div>
                      </div>

                      {/* 3. Setup */}
                      <div className="p-5 border-b border-orange-100">
                        <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                          <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">3</span>
                          Setup
                        </h4>
                        <p className="text-xs text-slate-500 mb-4">Tiêu chí bố trí tham quan, chương trình chi tiết & thành phần tham gia</p>
                        <div className="bg-slate-50 p-4 rounded-xl text-xs space-y-4">
                          <div>
                            <p className="text-slate-500 mb-1">Loại hình tham quan</p>
                            <p className="font-bold text-slate-800">Đón tiếp đoàn khách quốc tế và sự kiện</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-2 font-bold">Agenda chi tiết</p>
                            <table className="w-full text-left bg-white border border-slate-200 rounded-lg overflow-hidden">
                              <thead className="bg-[#004c91] text-white">
                                <tr>
                                  <th className="p-2 w-1/4">Khung Giờ</th>
                                  <th className="p-2">Khung nội dung chi tiết đón tiếp & tham quan dự kiến</th>
                                </tr>
                              </thead>
                              <tbody>
                                <tr className="border-b"><td className="p-2 font-bold">18:00 - 18:15</td><td className="p-2">Tập trung phái đoàn, đón tiếp xã giao sảnh Alpha, chụp hình lưu niệm check-in.</td></tr>
                                <tr className="border-b"><td className="p-2 font-bold">18:15 - 19:30</td><td className="p-2">Làm việc trao đổi học thuật, thảo luận chi tiết hợp tác hành chính tại phòng họp VIP sảnh Alpha.</td></tr>
                                <tr><td className="p-2 font-bold">19:30 - 22:00</td><td className="p-2">Campus Tour: Di chuyển bằng xe điện tham quan khu phát triển công nghệ cao, Thư viện số và chào tạm biệt đoàn.</td></tr>
                              </tbody>
                            </table>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-2 font-bold">Thành phần tham gia</p>
                            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                              <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Host</p><p className="font-bold">Nguyễn Văn A</p></div>
                              <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Người hỗ trợ bên IC</p><p className="font-bold">Nguyễn Văn B</p></div>
                              <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Người thuộc phòng ban khác</p><p className="font-bold">Nguyễn Văn C</p></div>
                              <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Sinh viên hỗ trợ</p><p className="font-bold">Nguyễn Văn D</p></div>
                            </div>
                          </div>
                        </div>
                      </div>

                      {/* 4. Detail setup */}
                      <div className="p-5 bg-orange-50/50">
                        <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                          <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">4</span>
                          Detail setup
                        </h4>
                        <p className="text-xs text-slate-500 mb-4">Yêu cầu kỹ thuật về khẩu hiệu trình chiếu LED và công tác chuẩn bị đón tiếp Campus Tour</p>
                        <div className="space-y-4">
                          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm text-xs">
                            <h5 className="font-bold text-slate-800 border-b pb-2 mb-2 text-[13px]">Mục 1: Trình chiếu khẩu hiệu LED</h5>
                            <p className="mb-1"><span className="font-bold text-[#0aa14f]">Có sử dụng</span> <span className="text-slate-500">(Hiển thị chạy tự động dọc theo màn hình LED lớn sảnh chính đón khách)</span></p>
                            <p className="bg-slate-100 p-2 rounded text-[#f37021] font-bold text-center border border-slate-200 mt-2">"FPT UNIVERSITY LUNAR NEW YEAR EVE CELEBRATION FOR INTERNATIONALS"</p>
                          </div>
                          
                          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm text-xs">
                            <h5 className="font-bold text-slate-800 border-b pb-2 mb-3 text-[13px]">Mục 2: Chuẩn bị cho Campus Tour</h5>
                            <div className="space-y-3">
                              <div>
                                <p className="font-bold text-[#004c91] mb-1">Phần 1: Người dẫn</p>
                                <p className="text-slate-700 bg-slate-50 p-2 rounded border border-slate-100">Bố trí 02 Đại sứ sinh viên xuất sắc hướng dẫn dẫn đoàn và thuyết minh lưu loát bằng tiếng Anh/Việt.</p>
                              </div>
                              <div>
                                <p className="font-bold text-[#004c91] mb-1">Phần 2: Xe điện</p>
                                <p className="text-slate-700 bg-slate-50 p-2 rounded border border-slate-100">Chuẩn bị sẵn 01 xe điện sạc đầy pin 100%, bảo dưỡng lốp, lau dọn khu vực ghế tươm tất.</p>
                              </div>
                              <div>
                                <p className="font-bold text-[#004c91] mb-1">Phần 3: Người lái</p>
                                <p className="text-slate-700 bg-slate-50 p-2 rounded border border-slate-100">Cử cán bộ lái xe điện chuyên trách túc trực, trang phục lịch thiệp, an toàn.</p>
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>

                    </div>
                  )}
                </div>
    

              {activePopoverEvent.category === 'Lời mời tham gia' && (
                <div className="bg-white rounded-2xl border border-slate-200/85 p-6 md:p-8 font-sans w-full max-w-4xl mx-auto space-y-6 relative overflow-visible">
                  
                  {/* BENTO GRID (Người gửi, Thời gian gửi, Đoàn khách, Thời gian diễn ra) */}
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    
                    <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                      <div className="flex items-center gap-2 text-gray-400 mb-2">
                        <User className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Người gửi</span>
                      </div>
                      <div className="text-sm font-black text-[#004c91]">{activePopoverEvent.host}</div>
                    </div>
                    
                    <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                      <div className="flex items-center gap-2 text-gray-400 mb-2">
                        <Clock className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian gửi</span>
                      </div>
                      <div className="text-sm font-black text-[#004c91]">08:30 15-10-2023</div>
                    </div>

                    <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default flex flex-col justify-center">
                      <div className="flex items-center gap-2 text-gray-400 mb-2">
                        <Users className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Đoàn khách</span>
                      </div>
                      <div className="text-base font-black text-[#004c91] border-l-4 border-[#f37021] pl-3 py-1 bg-transparent leading-none flex items-center">
                        {activePopoverEvent.guests}
                      </div>
                    </div>

                    <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default relative overflow-hidden flex flex-col justify-center">
                      <div className="flex items-center gap-2 text-gray-400 mb-2 relative z-10">
                        <Calendar className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian diễn ra</span>
                      </div>
                      <div className="text-[15px] font-bold text-gray-800 relative z-10 flex items-center flex-wrap gap-2 sm:gap-3">
                         <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activePopoverEvent.time?.split('-')[0]?.trim()}</span>
                         <ChevronRight className="w-4 h-4 text-gray-400" />
                         <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activePopoverEvent.time?.split('-')[1]?.trim()}</span>
                         <span className="text-[#004c91] font-bold ml-1">{activePopoverEvent.date?.split('-').reverse().join('-')}</span>
                      </div>
                    </div>

                  </div>

                  <div className="flex flex-col gap-3 pt-4 transition-all cursor-default relative z-10">
                    <div className="flex items-center gap-2 text-gray-400">
                        <FileText className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung</span>
                    </div>
                    <div className="text-[15px] font-medium text-gray-700 leading-relaxed transition-all relative">
                      {typeof activePopoverEvent.purpose === 'string' && activePopoverEvent.purpose.split('\n').map((line, idx) => (
                        <p key={idx} className={idx > 0 && line.startsWith('Vui lòng') ? 'mt-4 font-bold text-gray-900 border-l-2 border-[#004c91] pl-3 py-1 bg-blue-50/50' : 'mb-2'}>
                          {line}
                        </p>
                      ))}
                    </div>
                  </div>

                  {invitationStatus === 'rejecting' && (
                    <div className="animate-fade-in-quick pt-4">
                      <label className="block text-[11px] font-bold text-gray-700 uppercase tracking-wider mb-2">Lý do từ chối</label>
                      <textarea
                        rows={3}
                        className="w-full text-sm p-4 border border-red-200 rounded-xl focus:border-red-500 focus:ring-1 focus:ring-red-200 outline-none resize-none"
                        placeholder="Nhập lý do không thể tham gia..."
                        value={rejectReason}
                        onChange={(e) => setRejectReason(e.target.value)}
                        autoFocus
                      />
                      <div className="flex justify-end gap-3 mt-3">
                        <button
                          onClick={() => {
                            setInvitationStatus('pending');
                            setRejectReason('');
                          }}
                          className="px-5 py-2 rounded-xl text-gray-500 hover:bg-gray-100 font-bold text-xs"
                        >
                          Hủy
                        </button>
                        <button
                          onClick={() => setInvitationStatus('rejected')}
                          disabled={!rejectReason.trim()}
                          className="px-5 py-2 rounded-xl bg-red-600 text-white hover:bg-red-700 font-bold text-xs disabled:opacity-50"
                        >
                          Gửi phản hồi
                        </button>
                      </div>
                    </div>
                  )}

                  {invitationStatus === 'rejected' && (
                    <div className="animate-fade-in-quick pt-4">
                       <div className="p-4 rounded-2xl border border-red-200 bg-red-50 flex items-start gap-3">
                          <Info className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />
                          <div>
                            <span className="text-red-800 font-bold text-sm block mb-1">Đã từ chối tham gia</span>
                            <span className="text-red-600/80 text-xs italic">"{rejectReason}"</span>
                          </div>
                       </div>
                    </div>
                  )}

                  {invitationStatus === 'accepted' && acceptSignature && (
                    <div className="animate-fade-in-quick pt-4">
                       <div className="p-5 rounded-2xl border border-[#004c91] bg-blue-50/50 flex flex-col gap-3">
                          <div className="flex items-center gap-2">
                             <div className="w-6 h-6 rounded-full bg-[#004c91] flex items-center justify-center">
                               <CheckSquare className="w-3.5 h-3.5 text-white" />
                             </div>
                             <span className="text-[#004c91] font-black text-sm">Đã xác nhận tham gia</span>
                          </div>
                          <div className="border-t border-blue-100 pt-3">
                             <div className="flex flex-col">
                                <span className="text-xs text-slate-500 font-medium">Xác nhận bởi: <span className="font-extrabold text-[#004c91]">{acceptSignature.name}</span></span>
                                <span className="text-[10px] text-slate-400 font-mono mt-0.5">{acceptSignature.time}</span>
                             </div>
                          </div>
                       </div>
                    </div>
                  )}

                  {invitationStatus === 'pending' && (
                  <div className="flex gap-4 pt-6 mt-6 border-t border-gray-100 flex-col relative z-10 w-full animate-fade-in-quick">
                    
                    <div className="flex flex-col sm:flex-row gap-4 w-full">
                      <button 
                        onClick={() => setInvitationStatus('rejecting')}
                        disabled={!!assignedPerson}
                        className={`flex-1 py-4 px-6 rounded-2xl border-2 font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${
                          (!!assignedPerson)
                            ? 'border-gray-200 text-gray-400 bg-gray-50 cursor-not-allowed' 
                            : 'border-[#f37021] text-[#f37021] hover:bg-[#f37021] hover:text-white hover:shadow-lg active:scale-[0.98] cursor-pointer'
                        }`}>
                        Từ chối
                      </button>
                      <button 
                        onClick={() => {
                          const now = new Date();
                          const timeStr = `${String(now.getDate()).padStart(2, '0')}/${String(now.getMonth() + 1).padStart(2, '0')}/${now.getFullYear()}, ${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
                          setAcceptSignature({ name: user?.name || 'Khách', time: timeStr });
                          setInvitationStatus('accepted');
                        }}
                        disabled={!!assignedPerson}
                        className={`flex-1 py-4 px-6 rounded-2xl font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${
                          (!!assignedPerson)
                            ? 'bg-gray-100 text-gray-400 border border-gray-200 cursor-not-allowed' 
                            : 'bg-[#004c91] text-white hover:bg-[#003b73] shadow-lg shadow-[#004c91]/20 active:scale-[0.98] border border-blue-600 cursor-pointer'
                        }`}>
                        Xác nhận tham gia
                      </button>
                    </div>

                    {isDeptLeader && (
                       <div className="w-full relative mt-2">
                         <button 
                           onClick={() => setShowAssignDropdown(!showAssignDropdown)}
                           disabled={isDeptStaff && deptPreliminaryStatus !== 'accepted'}
                           className={`w-full py-3.5 px-6 rounded-2xl bg-slate-100 text-slate-700 font-black uppercase tracking-wider transition-all duration-300 outline-none text-xs text-center flex items-center justify-center gap-2 ${
                             isDeptStaff && deptPreliminaryStatus !== 'accepted' ? 'opacity-50 cursor-not-allowed border-dashed' : 'hover:bg-slate-200 border border-slate-200'
                           }`}>
                           <User className="w-4 h-4" />
                           {assignedPerson ? `Đã giao: ${assignedPerson}` : 'Ủy quyền / Đổi người phụ trách'}
                         </button>
                         {showAssignDropdown && (
                           <div className="absolute top-full left-0 right-0 mt-2 bg-white border border-slate-200 rounded-xl shadow-[0_8px_30px_-4px_rgba(0,0,0,0.1)] z-50 overflow-hidden">
                             <div className="py-2">
                               {MOCK_STAFF_LIST.map((staff) => (
                                 <button
                                   key={staff.id}
                                   className="w-full px-4 py-3 text-left hover:bg-slate-50 border-b border-slate-50 last:border-0 transition-colors group flex items-start justify-between"
                                   onClick={() => {
                                     setAssignedPerson(staff.name);
                                     setShowAssignDropdown(false);
                                   }}
                                 >
                                    <div>
                                      <span className="block text-sm font-bold text-slate-800 group-hover:text-[#004c91]">{staff.name}</span>
                                      <span className="block text-xs font-medium text-slate-500 mt-0.5">{staff.email}</span>
                                    </div>
                                    {assignedPerson === staff.name && (
                                      <CheckSquare className="w-4 h-4 text-[#004c91]" />
                                    )}
                                 </button>
                               ))}
                             </div>
                           </div>
                         )}
                       </div>
                    )}

                  </div>
                  )}

                </div>
              )}

              {activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && (
                <div className="bg-white rounded-2xl border border-slate-200/85 p-6 md:p-8 font-sans w-full max-w-4xl mx-auto space-y-6 relative overflow-visible">
                  
                  {/* BENTO GRID (Người gửi, Thời gian gửi, Đoàn khách, Thời gian sử dụng) */}
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    
                    <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                      <div className="flex items-center gap-2 text-gray-400 mb-2">
                        <User className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Người gửi</span>
                      </div>
                      <div className="text-sm font-black text-[#004c91]">{activePopoverEvent.host}</div>
                    </div>
                    
                    <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                      <div className="flex items-center gap-2 text-gray-400 mb-2">
                        <Clock className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian gửi</span>
                      </div>
                      <div className="text-sm font-black text-[#004c91]">08:30 15-10-2023</div>
                    </div>

                    {activePopoverEvent.guests && (
                      <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default flex flex-col justify-center">
                        <div className="flex items-center gap-2 text-gray-400 mb-2">
                          <Users className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Đoàn khách</span>
                        </div>
                        <div className="text-base font-black text-[#004c91] border-l-4 border-[#f37021] pl-3 py-1 bg-transparent leading-none flex items-center uppercase">
                          {activePopoverEvent.guests}
                        </div>
                      </div>
                    )}

                    <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default relative overflow-hidden flex flex-col justify-center">
                      <div className="flex items-center gap-2 text-gray-400 mb-2 relative z-10">
                        <Calendar className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian sử dụng</span>
                      </div>
                      <div className="text-[15px] font-bold text-gray-800 relative z-10 flex items-center flex-wrap gap-2 sm:gap-3">
                         <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activePopoverEvent.time?.split('-')[0]?.trim()}</span>
                         <ChevronRight className="w-4 h-4 text-gray-400" />
                         <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activePopoverEvent.time?.split('-')[1]?.trim()}</span>
                         <span className="text-sm text-[#004c91] ml-2 font-black">{activePopoverEvent.date.split('-').reverse().join('-')}</span>
                      </div>
                      <div className="absolute right-0 top-1/2 -translate-y-1/2 opacity-[0.02] pointer-events-none scale-150 mr-4">
                        <Calendar className="w-24 h-24 text-gray-900" />
                      </div>
                    </div>

                    {isProposing && !proposalSubmitted && (
                      <div className="col-span-1 sm:col-span-2 p-4 bg-orange-50/50 rounded-2xl border border-orange-200 cursor-default relative overflow-hidden flex flex-col justify-center animate-fade-in-quick mt-[-4px]">
                        <div className="flex items-center gap-2 text-[#de703b] mb-2 relative z-10">
                          <Calendar className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian sử dụng (Đề xuất)</span>
                        </div>
                        <input
                          type="text"
                          className="w-full text-sm p-3.5 border border-orange-200 rounded-xl focus:border-orange-500 focus:ring-1 focus:ring-orange-200 outline-none bg-white font-medium text-slate-800 placeholder:font-normal placeholder:text-gray-400"
                          placeholder="Nhập đề xuất thời gian..."
                        />
                      </div>
                    )}

                  </div>

                  <div className="flex flex-col gap-3 pt-4 transition-all cursor-default relative z-10">
                    <div className="flex items-center gap-2 text-gray-400">
                        <FileText className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết công việc</span>
                    </div>
                    <div className="p-6 bg-[#f8fafc] rounded-2xl text-[15px] font-medium text-gray-700 leading-relaxed border border-gray-200 transition-all relative">
                      {typeof activePopoverEvent.purpose === 'string' && activePopoverEvent.purpose.split('\n').map((line, idx) => (
                        <p key={idx} className={idx > 0 && line.startsWith('*') ? 'mt-4 font-bold text-gray-900 border-l-2 border-[#004c91] pl-3 py-1 bg-blue-50/50' : 'mb-2'}>
                          {line}
                        </p>
                      ))}
                    </div>
                  </div>

                  {requestStatus === 'pending' && !isProposing && !proposalSubmitted && (
                    <div className="flex justify-end pt-2">
                      <button 
                        onClick={() => setIsProposing(true)}
                        disabled={!!assignedPerson}
                        className={`px-5 py-2.5 rounded-xl border border-orange-200 text-[#f37021] bg-orange-50 font-bold text-xs flex items-center gap-2 transition-colors ${(!!assignedPerson) ? 'opacity-50 cursor-not-allowed' : 'hover:bg-orange-100'}`}>
                        <Edit2 className="w-4 h-4" />
                        Đề xuất thay đổi
                      </button>
                    </div>
                  )}

                  {isProposing && (
                    <div className="flex flex-col gap-3 transition-all cursor-default relative z-10 animate-fade-in-quick mt-2">
                       <div className="flex items-center gap-2 text-[#de703b] mt-2">
                           <FileText className="w-4 h-4" />
                           <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết công việc (Đề xuất)</span>
                       </div>
                       <textarea
                         rows={4}
                         className="w-full text-sm p-5 border border-orange-200 rounded-2xl focus:border-orange-500 focus:ring-1 focus:ring-orange-200 outline-none resize-none bg-orange-50/50 font-medium text-slate-800 placeholder:font-normal placeholder:text-gray-400"
                         placeholder="Nhập đề xuất nội dung..."
                         value={proposalNote}
                         onChange={(e) => setProposalNote(e.target.value)}
                         autoFocus
                       />
                       <div className="flex justify-end gap-3 mt-1">
                         <button
                           onClick={() => {
                             setIsProposing(false);
                             setProposalNote('');
                           }}
                           className="px-5 py-2.5 rounded-xl text-gray-500 hover:bg-gray-100 font-bold text-xs"
                         >
                           Hủy
                         </button>
                         <button
                           onClick={() => {
                             setIsProposing(false);
                             setProposalSubmitted(true);
                           }}
                           disabled={!proposalNote.trim()}
                           className="px-5 py-2.5 rounded-xl bg-[#de703b] text-white hover:bg-[#c9602c] font-bold text-xs disabled:opacity-50"
                         >
                           Gửi đề xuất
                         </button>
                       </div>
                    </div>
                  )}

                  {proposalSubmitted && (
                     <div className="mt-4 animate-fade-in-quick">
                        <div className="bg-[#de703b] text-white rounded-2xl p-5 flex flex-col items-center justify-center text-center shadow-md border border-[#c9602c]">
                          <div className="flex items-center gap-2.5 mb-2.5">
                            <Clock className="w-5 h-5" />
                            <span className="font-extrabold text-sm uppercase tracking-wider">Chờ xác nhận (Đề xuất thay đổi)</span>
                          </div>
                          <div className="bg-black/15 px-4 py-1.5 rounded-full inline-block">
                             <span className="text-white/95 text-xs font-medium">
                               bởi: {user?.name || 'trần b hỗ trợ'} - {new Date().toLocaleString('vi-VN')}
                             </span>
                          </div>
                        </div>
                     </div>
                  )}

                  {requestStatus === 'rejecting' && (
                    <div className="mt-4 pt-4 border-t border-red-100 animate-fade-in-quick">
                      <div className="flex items-center gap-2 text-red-600 mb-2">
                        <Info className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Lý do từ chối</span>
                      </div>
                      <textarea
                        rows={3}
                        className="w-full text-sm p-4 border border-red-200 rounded-xl focus:border-red-500 focus:ring-1 focus:ring-red-200 outline-none resize-none"
                        placeholder="Nhập lý do không thể tiếp nhận..."
                        value={requestRejectReason}
                        onChange={(e) => setRequestRejectReason(e.target.value)}
                        autoFocus
                      />
                      <div className="flex justify-end gap-3 mt-3">
                        <button
                          onClick={() => {
                            setRequestStatus('pending');
                            setRequestRejectReason('');
                          }}
                          className="px-5 py-2 rounded-xl text-gray-500 hover:bg-gray-100 font-bold text-xs"
                        >
                          Hủy
                        </button>
                        <button
                          onClick={() => setRequestStatus('rejected')}
                          disabled={!requestRejectReason.trim()}
                          className="px-5 py-2 rounded-xl bg-red-600 text-white hover:bg-red-700 font-bold text-xs disabled:opacity-50"
                        >
                          Gửi phản hồi
                        </button>
                      </div>
                    </div>
                  )}

                  {requestStatus === 'rejected' && (
                    <div className="animate-fade-in-quick pt-4">
                       <div className="p-4 rounded-2xl border border-red-200 bg-red-50 flex items-start gap-3">
                          <Info className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />
                          <div>
                            <span className="text-red-800 font-bold text-sm block mb-1">Đã từ chối nhiệm vụ</span>
                            <span className="text-red-600/80 text-xs italic">"{requestRejectReason}"</span>
                          </div>
                       </div>
                    </div>
                  )}

                  {(requestStatus === 'pending' || requestStatus === 'accepted') && !isProposing && !proposalSubmitted && (
                    <div className="flex gap-4 pt-6 mt-6 border-t border-gray-100 flex-col relative z-10 w-full animate-fade-in-quick">
                      {requestStatus === 'pending' && (
                        <div className="flex flex-col sm:flex-row gap-4 w-full">
                          <button 
                            onClick={() => setRequestStatus('rejecting')}
                            disabled={!!assignedPerson}
                            className={`flex-1 py-4 px-6 rounded-2xl border-2 font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${
                              (!!assignedPerson)
                                ? 'border-gray-200 text-gray-400 bg-gray-50 cursor-not-allowed' 
                                : 'border-[#f37021] text-[#f37021] hover:bg-[#f37021] hover:text-white hover:shadow-lg active:scale-[0.98] cursor-pointer'
                            }`}>
                            Từ chối
                          </button>
                          <button 
                            onClick={() => {
                              const now = new Date();
                              const timeStr = `${String(now.getDate()).padStart(2, '0')}/${String(now.getMonth() + 1).padStart(2, '0')}/${now.getFullYear()}, ${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
                              setRequestAcceptSignature({ name: user?.name || 'Khách', time: timeStr });
                              setRequestStatus('accepted');
                            }}
                            disabled={!!assignedPerson}
                            className={`flex-1 py-4 px-6 rounded-2xl font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${
                              (!!assignedPerson)
                                ? 'bg-gray-100 text-gray-400 border border-gray-200 cursor-not-allowed' 
                                : 'bg-[#004c91] text-white hover:bg-[#003b73] shadow-lg shadow-[#004c91]/20 active:scale-[0.98] border border-blue-600 cursor-pointer'
                            }`}>
                            Xác nhận nhiệm vụ
                          </button>
                        </div>
                      )}
                      
                      {requestStatus === 'accepted' && (
                        <div className="p-4 rounded-2xl border border-green-200 bg-green-50 flex items-start gap-3 relative">
                          <CheckSquare className="w-5 h-5 text-green-600 shrink-0 mt-0.5" />
                          <div className="flex-1">
                            <span className="text-green-800 font-bold text-sm block mb-2">Đã xác nhận nhiệm vụ</span>
                            {requestAcceptSignature && (
                                <div className="bg-green-100/50 px-3 py-1.5 rounded-lg inline-block w-full sm:w-auto mb-2">
                                  <span className="text-green-800 text-[11px] font-medium flex flex-col sm:flex-row sm:items-center sm:gap-1">
                                    <span>bởi: <span className="font-bold">{requestAcceptSignature.name}</span></span>
                                    <span className="hidden sm:inline">-</span>
                                    <span>{requestAcceptSignature.time}</span>
                                  </span>
                                </div>
                            )}
                            <span className="text-green-600/80 text-xs font-medium block">Bên dưới là biên bản bàn giao & nghiệm thu.</span>
                          </div>
                       </div>
                      )}

                      {isDeptLeader && requestStatus === 'pending' && (
                         <div className="w-full relative mt-2">
                           <button 
                             onClick={() => setShowAssignDropdown(!showAssignDropdown)}
                             disabled={isDeptStaff && deptPreliminaryStatus !== 'accepted'}
                             className={`w-full py-3.5 px-6 rounded-2xl bg-slate-100 text-slate-700 font-black uppercase tracking-wider transition-all duration-300 outline-none text-xs text-center flex items-center justify-center gap-2 ${
                               isDeptStaff && deptPreliminaryStatus !== 'accepted' ? 'opacity-50 cursor-not-allowed border-dashed' : 'hover:bg-slate-200 border border-slate-200'
                             }`}>
                             <User className="w-4 h-4" />
                             {assignedPerson ? `Đã giao: ${assignedPerson}` : 'Ủy quyền / Đổi người phụ trách'}
                           </button>
                           {showAssignDropdown && (
                             <div className="absolute bottom-full mb-2 left-0 right-0 bg-white border border-slate-200 rounded-xl shadow-[0_-8px_30px_-4px_rgba(0,0,0,0.1)] z-50 overflow-hidden">
                               <div className="py-2">
                                 {MOCK_STAFF_LIST.map((staff) => (
                                   <button
                                     key={staff.id}
                                     className="w-full px-4 py-3 text-left hover:bg-slate-50 border-b border-slate-50 last:border-0 transition-colors group flex items-start justify-between"
                                     onClick={() => {
                                       setAssignedPerson(staff.name);
                                       setShowAssignDropdown(false);
                                     }}
                                   >
                                      <div>
                                        <span className="block text-sm font-bold text-slate-800 group-hover:text-[#004c91]">{staff.name}</span>
                                        <span className="block text-xs font-medium text-slate-500 mt-0.5">{staff.email}</span>
                                      </div>
                                      {assignedPerson === staff.name && (
                                        <CheckSquare className="w-4 h-4 text-[#004c91]" />
                                      )}
                                   </button>
                                 ))}
                               </div>
                             </div>
                           )}
                         </div>
                      )}
                    </div>
                  )}

                </div>
              )}

              {activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'accepted' && (
                /* Safuri Event Layout */
                <div className="bg-white rounded-2xl border border-slate-200/85 shadow-md p-6 md:p-10 font-sans max-w-4xl mx-auto space-y-6 relative overflow-hidden">
                  {/* Draft decorative watermark stamp */}
                  <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 text-slate-100/15 text-5xl sm:text-7xl font-sans font-black tracking-widest uppercase pointer-events-none select-none -rotate-12">
                    FPT UNIVERSITY
                  </div>

                  {/* National Emblem Text & FPTU Header */}
                  <div className="flex flex-col sm:flex-row justify-between border-b border-slate-150 pb-5 text-xs gap-4 text-slate-550 relative z-10">
                    <div className="text-left space-y-1">
                      <p className="font-extrabold text-slate-900 text-xs sm:text-sm uppercase tracking-wide">TRƯỜNG ĐẠI HỌC FPT HÒA LẠC</p>
                      <p className="font-bold text-[11px] text-slate-550">Tổ Quản Lý Thiết Bị & Xe Điện Nội Khu</p>
                      <p className="text-[10px] text-slate-450 font-mono">Số văn bản: FPTU/BGNT-XD/2026-088</p>
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
                      BIÊN BẢN BÀN GIAO VÀ NGHIỆM THU PHƯƠNG TIỆN
                    </h4>
                    <p className="text-[11px] font-semibold text-slate-505 italic">
                      (V/v: Thiết lập thủ tục bàn giao và bảo đảm vận hành xe điện nội khu dịch vụ đối ngoại)
                    </p>
                  </div>

                  {/* Core Minutes Information */}
                  <div className="space-y-4 text-xs text-slate-750 font-normal leading-relaxed relative z-10 font-sans">
                    <p className="text-justify">
                      Căn cứ kế hoạch tiếp đón phái đoàn cao cấp đối tác thương mại <strong className="text-[#f37021] font-black">Safuri</strong> ghé thăm và làm việc tại campus Đại học FPT Hòa Lạc ngày 08 tháng 08 năm 2026. Hôm nay, đúng lúc 08h00, các bên tham gia trực tiếp đã có mặt đầy đủ để ký bàn giao, ghi nhận hiện trạng kỹ thuật bàn giao của phương tiện xe điện phục vụ di chuyển an toàn trong nội khu:
                    </p>

                    {/* Side by side parties info */}
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 bg-slate-50/80 p-5 rounded-2xl border border-slate-200/50">
                      <div className="space-y-1.5 border-r border-slate-200/45 pr-2">
                        <p className="font-black text-[#004c91] text-xs uppercase tracking-wide">BÊN GIAO (BỘ PHẬN PHƯƠNG TIỆN)</p>
                                   <div className="flex items-center gap-1.5">• <strong>Phương tiện bàn giao</strong>: <span className="text-slate-850 font-bold">Xe điện du lịch chuyên dụng</span></div>
                        <div className="flex items-center gap-1.5">• <strong>Mã danh định xe</strong>: <span className="text-slate-850 font-bold">FPTU-EV-09 / Loại 8 ghế</span></div>
                        <div className="flex items-center gap-1.5">• <strong>Tình trạng pin lái</strong>: <span className="text-emerald-700 font-extrabold">Đã sạc đầy 100% cực bền</span></div>
                        <div className="flex items-center gap-1.5">• <strong>Tính năng kỹ thuật</strong>: <span className="text-slate-850 font-bold font-sans">Phanh nhạy, Đèn còi đạt chuẩn</span></div>
                        <div className="flex items-center gap-1.5">• <strong>Phụ kiện bàn giao</strong>: <span className="text-slate-850 font-bold">01 Chìa khóa thông minh, 01 Sổ vận trình</span></div>
                        <div className="flex items-center gap-1.5">• <strong>Vật phẩm đi kèm</strong>: <span className="text-[#f37021] font-extrabold">10 Ô FPT dù cam xếp ở khay sau</span></div>
                      </div>
                      <div className="space-y-1.5 pl-2">
                        <p className="font-black text-[#004c91] text-xs uppercase tracking-wide">BÊN NHẬN (CÁN BỘ ĐÓN TIẾP)</p>
                        <div className="flex items-center gap-1.5">• <strong>Đơn vị tiếp nhận</strong>: <span className="text-slate-850 font-bold">Ban Đào tạo & CTSV</span></div>
                        <div className="flex items-center gap-1.5">• <strong>Mục đích sử dụng</strong>: <span className="text-slate-850 font-bold">Đón tiếp đoàn Safuri</span></div>
                        <div className="flex items-center gap-1.5">• <strong>Thời gian dự kiến</strong>: <span className="text-slate-850 font-bold">08:00 - 16:30, 08/08/2026</span></div>
                        <div className="flex items-center gap-1.5">• <strong>Trách nhiệm</strong>: <span className="text-slate-850 font-bold">Bảo quản tài sản, sử dụng đúng mục đích</span></div>
                        <div className="flex items-center gap-1.5">• <strong>Cam kết đền bù</strong>: <span className="text-red-600 font-bold">Chịu trách nhiệm bảo quản</span></div>
                      </div>
                    </div>
                  </div>

                  {/* Gray horizontal divider with Handover text */}
                  <div className="relative my-7">
                    <div className="absolute inset-0 flex items-center" aria-hidden="true">
                      <div className="w-full border-t border-slate-350"></div>
                    </div>
                    <div className="relative flex justify-center text-xs uppercase font-extrabold tracking-widest">
                      <span className="bg-white text-slate-900 font-black px-4 py-1.5 rounded-full border border-slate-200 shadow-3xs uppercase text-[11px] tracking-widest">BÀN GIAO</span>
                    </div>
                  </div>

                  {/* Handover Signatures with Notes on the SAME row */}
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-slate-50/70 p-4.5 rounded-2xl border border-slate-200">
                    
                    {/* Block Bên Giao */}
                    <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-3xs flex flex-col justify-between gap-4">
                      <div>
                        <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-1.5">
                          Ghi chú Bên Giao
                        </label>
                        <textarea
                          rows={2}
                          value={safuriBG1Note}
                          onChange={e => setSafuriBG1Note(e.target.value)}
                          className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#f37021] outline-none resize-none font-sans bg-slate-50/30 focus:ring-1 focus:ring-orange-200"
                          disabled={!!safuriBG1Signed}
                          placeholder="Nhập ý kiến Bên Giao đầu giờ..."
                        />
                      </div>

                      {/* Horizontal Signature Box */}
                      <div className={`border-2 rounded-xl p-3 relative group shadow-3xs transition-colors ${safuriBG1Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-white hover:border-[#004c91]/40'}`}>
                        {safuriBG1Signed ? (
                          <div className="flex flex-row items-center justify-between gap-4 animate-fade-in-quick w-full">
                            <div className="flex items-center gap-2.5">
                              <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs border border-emerald-200 shadow-3xs shrink-0">✓</div>
                              <div className="text-left">
                                <span className="text-[9px] font-black uppercase text-emerald-800 tracking-wider font-sans block leading-none mb-0.5">ĐÃ KÝ DUYỆT BÀN GIAO</span>
                                <p className="text-[11px] font-extrabold text-slate-805 leading-snug truncate max-w-[170px]">{safuriBG1Signed.split(' - ')[0]}</p>
                                <p className="text-[9px] text-slate-500 font-mono mt-0.5 leading-none">{safuriBG1Signed.split(' - ')[1]}</p>
                              </div>
                            </div>
                          </div>
                        ) : (
                          <div className="flex flex-row items-center justify-between gap-3 w-full">
                            <div className="flex items-center gap-2">
                              <FileText className="w-4 h-4 text-[#f37021]/80 shrink-0" />
                              <div className="text-left">
                                <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao</span>
                                <span className="text-[9px] text-slate-450">Nhấp để hoàn tất BG1</span>
                              </div>
                            </div>
                            <button
                              type="button"
                              onClick={() => {
                                const now = new Date();
                                const timeStr = `${String(now.getDate()).padStart(2, '0')}/${String(now.getMonth() + 1).padStart(2, '0')}/2026, ${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
                                setSafuriBG1Signed(`Nguyễn Văn Lái (Tổ Xe Điện) - ${timeStr}`);
                              }}
                              className="py-2 px-3 bg-orange-50 hover:bg-orange-100 hover:text-[#f37021] text-slate-705 font-extrabold text-[11px] rounded-xl border border-slate-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-3xs shrink-0"
                            >
                              <FileText className="w-3.5 h-3.5" />
                              <span>Ký xác nhận (BG1)</span>
                            </button>
                          </div>
                        )}
                      </div>
                    </div>

                    {/* Block Bên Nhận */}
                    <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-3xs flex flex-col justify-between gap-4">
                      <div>
                        <label className="block text-[10px] font-black text-[#f37021] uppercase tracking-wider mb-1.5">
                          Ghi chú Bên Nhận
                        </label>
                        <textarea
                          rows={2}
                          value={safuriBG2Note}
                          onChange={e => setSafuriBG2Note(e.target.value)}
                          className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#f37021] outline-none resize-none font-sans bg-slate-50/30 focus:ring-1 focus:ring-orange-200"
                          disabled={!!safuriBG2Signed}
                          placeholder="Nhập ý kiến Bên Nhận đầu giờ..."
                        />
                      </div>

                      {/* Horizontal Signature Box */}
                      <div className={`border-2 rounded-xl p-3 relative group shadow-3xs transition-colors ${safuriBG2Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-white hover:border-[#f37021]/40'}`}>
                        {safuriBG2Signed ? (
                          <div className="flex flex-row items-center justify-between gap-4 animate-fade-in-quick w-full">
                            <div className="flex items-center gap-2.5">
                              <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs border border-emerald-200 shadow-3xs shrink-0">✓</div>
                              <div className="text-left">
                                <span className="text-[9px] font-black uppercase text-emerald-800 tracking-wider font-sans block leading-none mb-0.5">ĐÃ KÝ DUYỆT BÀN GIAO</span>
                                <p className="text-[11px] font-extrabold text-slate-850 leading-snug truncate max-w-[170px]">{safuriBG2Signed.split(' - ')[0]}</p>
                                <p className="text-[9px] text-slate-500 font-mono mt-0.5 leading-none">{safuriBG2Signed.split(' - ')[1]}</p>
                              </div>
                            </div>
                          </div>
                        ) : (
                          <div className="flex flex-row items-center justify-between gap-3 w-full">
                            <div className="flex items-center gap-2">
                              <FileText className="w-4 h-4 text-[#f37021]/80 shrink-0" />
                              <div className="text-left">
                                <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                                <span className="text-[9px] text-slate-450">Nhấp để hoàn tất BG2</span>
                              </div>
                            </div>
                            <button
                              type="button"
                              onClick={() => {
                                const now = new Date();
                                const timeStr = `${String(now.getDate()).padStart(2, '0')}/${String(now.getMonth() + 1).padStart(2, '0')}/2026, ${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
                                setSafuriBG2Signed(`Nguyễn Văn Trưởng Phòng CTSV - ${timeStr}`);
                              }}
                              className="py-2 px-3 bg-orange-50 hover:bg-orange-100 hover:text-[#f37021] text-slate-705 font-extrabold text-[11px] rounded-xl border border-slate-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-3xs shrink-0"
                            >
                              <FileText className="w-3.5 h-3.5" />
                              <span>Ký xác nhận (BG2)</span>
                            </button>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>

                  {/* Toggle Acceptance Row when both are signed */}
                  {safuriBG1Signed && safuriBG2Signed ? (
                    <div className="animate-fade-in-quick space-y-6 pt-2 font-sans">
                      {/* Gray horizontal divider with Nghiệm thu text */}
                      <div className="relative my-7">
                        <div className="absolute inset-0 flex items-center" aria-hidden="true">
                          <div className="w-full border-t border-slate-350"></div>
                        </div>
                        <div className="relative flex justify-center text-xs uppercase font-extrabold tracking-widest">
                          <span className="bg-white text-slate-900 font-black px-4 py-1.5 rounded-full border border-slate-200 shadow-3xs uppercase text-[11px] tracking-widest">NGHIỆM THU</span>
                        </div>
                      </div>

                      {/* Acceptance signatures with Notes on the SAME row */}
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-[#f8fbfe] p-4.5 rounded-2xl border border-blue-200/50">
                        
                        {/* Block Bên Giao Nghiệm Thu */}
                        <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-3xs flex flex-col justify-between gap-4">
                          <div>
                            <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-1.5">
                              Ghi chú Nghiệm thu (Bên Giao)
                            </label>
                            <textarea
                              rows={2}
                              value={safuriNT1Note}
                              onChange={e => setSafuriNT1Note(e.target.value)}
                              className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#004c91] outline-none resize-none font-sans bg-slate-50/30 focus:ring-1 focus:ring-blue-200"
                              disabled={!!safuriNT1Signed}
                              placeholder="Ghi nhận hiện trạng lúc trả..."
                            />
                          </div>

                          {/* Horizontal Signature Box */}
                          <div className={`border-2 rounded-xl p-3 relative group shadow-3xs transition-colors ${safuriNT1Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-white hover:border-[#004c91]/40'}`}>
                            {safuriNT1Signed ? (
                              <div className="flex flex-row items-center justify-between gap-4 animate-fade-in-quick w-full">
                                <div className="flex items-center gap-2.5">
                                  <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs border border-emerald-200 shadow-3xs shrink-0">✓</div>
                                  <div className="text-left font-sans">
                                    <span className="text-[9px] font-black uppercase text-emerald-800 tracking-wider font-sans block leading-none mb-0.5">ĐÃ KÝ DUYỆT NGHIỆM THU</span>
                                    <p className="text-[11px] font-extrabold text-slate-805 leading-snug truncate max-w-[170px]">{safuriNT1Signed.split(' - ')[0]}</p>
                                    <p className="text-[9px] text-slate-500 font-mono mt-0.5 leading-none">{safuriNT1Signed.split(' - ')[1]}</p>
                                  </div>
                                </div>
                              </div>
                            ) : (
                              <div className="flex flex-row items-center justify-between gap-3 w-full">
                                <div className="flex items-center gap-2">
                                  <FileText className="w-4 h-4 text-[#004c91]/80 shrink-0" />
                                  <div className="text-left font-sans">
                                    <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao</span>
                                    <span className="text-[9px] text-slate-450">Nhấp để hoàn tất NT1</span>
                                  </div>
                                </div>
                                <button
                                  type="button"
                                  onClick={() => {
                                    const now = new Date();
                                    const timeStr = `${String(now.getDate()).padStart(2, '0')}/${String(now.getMonth() + 1).padStart(2, '0')}/2026, ${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
                                    setSafuriNT1Signed(`Nguyễn Văn Lái (Tổ Xe Điện) - ${timeStr}`);
                                  }}
                                  className="py-2 px-3 bg-blue-50 hover:bg-blue-100 hover:text-[#004c91] text-slate-705 font-extrabold text-[11px] rounded-xl border border-slate-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-3xs shrink-0"
                                >
                                  <FileText className="w-3.5 h-3.5" />
                                  <span>Ký Nghiệm thu (NT1)</span>
                                </button>
                              </div>
                            )}
                          </div>
                        </div>

                        {/* Block Bên Nhận Nghiệm Thu */}
                        <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-3xs flex flex-col justify-between gap-4">
                          <div>
                            <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-1.5">
                              Ghi chú Nghiệm thu (Bên Nhận)
                            </label>
                            <textarea
                              rows={2}
                              value={safuriNT2Note}
                              onChange={e => setSafuriNT2Note(e.target.value)}
                              className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#004c91] outline-none resize-none font-sans bg-slate-50/30 focus:ring-1 focus:ring-blue-200"
                              disabled={!!safuriNT2Signed}
                              placeholder="Nhận xét tình trạng bàn giao trả..."
                            />
                          </div>

                          {/* Horizontal Signature Box */}
                          <div className={`border-2 rounded-xl p-3 relative group shadow-3xs transition-colors ${safuriNT2Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-white hover:border-[#004c91]/40'}`}>
                            {safuriNT2Signed ? (
                              <div className="flex flex-row items-center justify-between gap-4 animate-fade-in-quick w-full">
                                <div className="flex items-center gap-2.5">
                                  <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs border border-emerald-200 shadow-3xs shrink-0">✓</div>
                                  <div className="text-left font-sans">
                                    <span className="text-[9px] font-black uppercase text-emerald-800 tracking-wider font-sans block leading-none mb-0.5">ĐÃ KÝ DUYỆT NGHIỆM THU</span>
                                    <p className="text-[11px] font-extrabold text-slate-850 leading-snug truncate max-w-[170px]">{safuriNT2Signed.split(' - ')[0]}</p>
                                    <p className="text-[9px] text-slate-500 font-mono mt-0.5 leading-none">{safuriNT2Signed.split(' - ')[1]}</p>
                                  </div>
                                </div>
                              </div>
                            ) : (
                              <div className="flex flex-row items-center justify-between gap-3 w-full">
                                <div className="flex items-center gap-2">
                                  <FileText className="w-4 h-4 text-[#004c91]/80 shrink-0" />
                                  <div className="text-left font-sans">
                                    <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                                    <span className="text-[9px] text-slate-450">Nhấp để hoàn tất NT2</span>
                                  </div>
                                </div>
                                <button
                                  type="button"
                                  onClick={() => {
                                    const now = new Date();
                                    const timeStr = `${String(now.getDate()).padStart(2, '0')}/${String(now.getMonth() + 1).padStart(2, '0')}/2026, ${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
                                    setSafuriNT2Signed(`Nguyễn Văn Trưởng Phòng CTSV - ${timeStr}`);
                                  }}
                                  className="py-2 px-3 bg-blue-50 hover:bg-blue-100 hover:text-[#004c91] text-slate-705 font-extrabold text-[11px] rounded-xl border border-slate-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-3xs shrink-0"
                                >
                                  <FileText className="w-3.5 h-3.5" />
                                  <span>Ký Nghiệm thu (NT2)</span>
                                </button>
                              </div>
                            )}
                          </div>
                        </div>

                      </div>
                    </div>
                  ) : (
                    <div className="bg-amber-50/85 rounded-2xl p-4.5 text-center text-xs text-amber-900 border border-amber-200 animate-pulse relative z-10 flex items-center justify-center gap-2 font-sans">
                      <span className="w-2 h-2 rounded-full bg-amber-500" />
                      <span className="font-semibold text-amber-950">Tiến trình an toàn: Vui lòng ký đầy đủ 2 ô "Bàn giao" đợt 1 bên trên để tự động mở khóa hồ sơ "Nghiệm thu bồi hoàn" đợt 2 sau khi hoàn tất hành trình di chuyển đoàn Safuri.</span>
                    </div>
                  )}

                </div>
              )}

              

              </div>

            {/* Footer controls inside modal */}
            <div className="bg-slate-50 px-6 py-4 flex justify-end items-center border-t border-slate-200 rounded-b-2xl">
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setActivePopoverEvent(null)}
                  className="px-5 py-2.5 bg-[#004c91] text-white hover:opacity-90 text-[11px] font-bold rounded-xl transition-colors shadow-3xs"
                >
                  {activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'accepted' ? 'Đóng biên bản bàn giao & nghiệm thu' : 'Đóng bảng chi tiết'}
                </button>
              </div>
            </div>

          </div>
        </div>
      )}

    </div>
    </div>
  );
}
