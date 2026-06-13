/**
 * Trang DashboardHome
 * Điểm neo điều hướng gốc - Phân chia logic tuỳ biến theo Role.
 */

// Đây là trang phần tổng quan (Home/Overview) của khu vực quản trị (Dashboard)
import React, { useState, useEffect } from "react";
import { 
  Calendar, 
  Clock, 
  MapPin, 
  Sparkles, 
  ChevronLeft, 
  ChevronRight, 
  ChevronDown,
  Plus, 
  Trash2, 
  Tag, 
  Filter, 
  User, 
  LayoutDashboard, 
  Building, 
  CalendarDays, 
  FolderPlus, 
  X,
  FileText
} from "lucide-react";

import { HODashboardView } from './HODashboardView';
import { SharedDashboardView } from './SharedDashboardView';
import { AdminDashboardView } from './AdminDashboardView';

interface EventItem {
  id: string;
  time: string;
  title: string;
  category: "Inbound Guest" | "Meeting" | "Campus Visit" | "International Desk" | "Other";
  description: string;
}

const defaultEvents: Record<string, EventItem[]> = {
  "2026-05-11": [
    { id: '1', time: '08:30 - 10:00', title: 'Đón tiếp đại diện Đại học Swinburne (Úc)', category: 'Inbound Guest', description: 'Gặp mặt đối tác bàn về chương trình liên kết đào tạo 2+2.' },
    { id: '2', time: '13:30 - 15:30', title: 'Họp duy trì hồ sơ học sinh nhận học bổng ASEAN', category: 'Meeting', description: 'Xét duyệt hồ sơ từ phòng Hợp tác quốc tế gửi lên.' }
  ],
  "2026-05-12": [
    { id: '3', time: '09:00 - 11:30', title: 'Gặp gỡ doanh nghiệp hỗ trợ On-the-Job Training (OJT)', category: 'Meeting', description: 'Ký kết biên bản ghi nhớ hợp tác tuyển dụng sinh viên.' },
    { id: '4', time: '14:00 - 16:30', title: 'Campus Tour cho học sinh THPT Chuyên FPT', category: 'Campus Visit', description: 'Thuyết minh và dẫn đoàn tham quan các khu học xá hiện đại.' }
  ],
  "2026-05-18": [
    { id: '5', time: '10:00 - 12:00', title: 'Lễ ký kết biên bản ghi nhớ (MOU) với ĐH Quốc gia Singapore', category: 'Inbound Guest', description: 'Cột mốc quan trọng trong hợp tác trao đổi nghiên cứu khoa học.' },
    { id: '6', time: '15:00 - 17:00', title: 'Trao chứng chỉ hoàn thành khóa học ngắn hạn cho đoàn Inbound', category: 'International Desk', description: 'Cấp chứng nhận cho 15 sinh viên quốc tế tham gia trao đổi văn hóa.' }
  ],
  "2026-05-25": [
    { id: '7', time: '08:00 - 11:30', title: 'Ngày hội Việc làm & Giao lưu Quốc tế (Global Career Day)', category: 'Campus Visit', description: 'Sự kiện lớn thường niên với sự tham gia của 20 doanh nghiệp đa quốc gia.' },
    { id: '8', time: '14:00 - 15:30', title: 'Phê duyệt kế hoạch trao đổi sinh viên học kỳ Fall 2026', category: 'Meeting', description: 'Thông báo kết quả tuyển chọn sinh viên FPTU đi học tập nước ngoài.' }
  ]
};

export function DashboardHome() {
  // Get current user
  const userStr = localStorage.getItem("currentUser");
  const user = userStr
    ? JSON.parse(userStr)
    : {
        name: "Cán bộ Khách",
        campus: "Hà Nội",
        role: "GUEST",
      };

  const isStaffLeader = (user?.role?.toUpperCase() === 'STAFF' && user?.subRole === 'Leader') || user?.role?.toUpperCase() === 'STAFF_LEADER';
  const isDeptLeader = user?.role?.toUpperCase() === 'DEPT_LEADER' || user?.role?.toUpperCase() === 'DEPT LEADER' || (user?.role?.toUpperCase() === 'DEPT' && user?.subRole?.toUpperCase() !== 'STAFF');
  const isDeptStaff = user?.role?.toUpperCase() === 'DEPT_STAFF' || (user?.role?.toUpperCase() === 'DEPT' && user?.subRole?.toUpperCase() === 'STAFF');
  const isStudent = user?.role?.toUpperCase() === 'STUDENT';
  const isVisitor = user?.role?.toUpperCase() === 'VISITOR';
  const isAdmin = user?.role?.toUpperCase() === 'ADMIN';
  const isStaff = user?.role?.toUpperCase() === 'STAFF' || isStaffLeader || isDeptLeader || isDeptStaff || isStudent || isVisitor;

  // State to handle Events Map (synchronized with localStorage)
  const [events, setEvents] = useState<Record<string, EventItem[]>>(() => {
    const saved = localStorage.getItem("dashboardEvents");
    return saved ? JSON.parse(saved) : defaultEvents;
  });

  const [currentYear, setCurrentYear] = useState(2026);
  const [currentMonth, setCurrentMonth] = useState(4); // 0-indexed (4 is May)
  const [selectedDay, setSelectedDay] = useState(12);

  // States to filter events
  const [categoryFilter, setCategoryFilter] = useState<string>("All");

  // Add Event Form States
  const [isAddFormOpen, setIsAddFormOpen] = useState(false);
  const [formTitle, setFormTitle] = useState("");
  const [formTime, setFormTime] = useState("09:00 - 11:00");
  const [formCategory, setFormCategory] = useState<EventItem["category"]>("Meeting");
  const [formDescription, setFormDescription] = useState("");
  const [formError, setFormError] = useState("");

  // Save events to localStorage when they change
  useEffect(() => {
    localStorage.setItem("dashboardEvents", JSON.stringify(events));
  }, [events]);

  // Calendar Days generator helper
  const getCalendarDays = (year: number, month: number) => {
    const firstDay = new Date(year, month, 1).getDay();
    const totalDays = new Date(year, month + 1, 0).getDate();
    const prevMonthTotalDays = new Date(year, month, 0).getDate();
    const days = [];

    // Prior Month days padding
    for (let i = firstDay - 1; i >= 0; i--) {
      days.push({
        day: prevMonthTotalDays - i,
        month: month === 0 ? 11 : month - 1,
        year: month === 0 ? year - 1 : year,
        currentMonth: false
      });
    }

    // Current Month days
    const todayObj = new Date();
    for (let i = 1; i <= totalDays; i++) {
      const isToday = todayObj.getDate() === i && todayObj.getMonth() === month && todayObj.getFullYear() === year;
      days.push({
        day: i,
        month: month,
        year: year,
        currentMonth: true,
        isToday
      });
    }

    // Next Month days padding
    const remainingCells = 42 - days.length;
    for (let i = 1; i <= remainingCells; i++) {
      days.push({
        day: i,
        month: month === 11 ? 0 : month + 1,
        year: month === 11 ? year + 1 : year,
        currentMonth: false
      });
    }

    return days;
  };

  const daysGrid = getCalendarDays(currentYear, currentMonth);

  const prevMonth = () => {
    if (currentMonth === 0) {
      setCurrentMonth(11);
      setCurrentYear(prev => prev - 1);
    } else {
      setCurrentMonth(prev => prev - 1);
    }
    // Default select day 1 when switching month
    setSelectedDay(1);
  };

  const nextMonth = () => {
    if (currentMonth === 11) {
      setCurrentMonth(0);
      setCurrentYear(prev => prev + 1);
    } else {
      setCurrentMonth(prev => prev + 1);
    }
    setSelectedDay(1);
  };

  const formatDateKey = (year: number, month: number, day: number) => {
    const m = String(month + 1).padStart(2, '0');
    const d = String(day).padStart(2, '0');
    return `${year}-${m}-${d}`;
  };

  const selectedDateKey = formatDateKey(currentYear, currentMonth, selectedDay);
  const selectedDayEvents = events[selectedDateKey] || [];

  const handleAddEventSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!formTitle.trim()) {
      setFormError("Vui lòng nhập tiêu đề sự kiện");
      return;
    }

    const newEvent: EventItem = {
      id: Date.now().toString(),
      time: formTime,
      title: formTitle,
      category: formCategory,
      description: formDescription || "Không có mô tả bổ sung."
    };

    const currentDayEvents = events[selectedDateKey] || [];
    setEvents({
      ...events,
      [selectedDateKey]: [...currentDayEvents, newEvent]
    });

    // Reset Form
    setFormTitle("");
    setFormDescription("");
    setFormError("");
    setIsAddFormOpen(false);
  };

  const handleDeleteEvent = (eventId: string) => {
    const currentDayEvents = events[selectedDateKey] || [];
    const updatedEvents = currentDayEvents.filter(ev => ev.id !== eventId);
    
    const newEventsMap = { ...events };
    if (updatedEvents.length === 0) {
      delete newEventsMap[selectedDateKey];
    } else {
      newEventsMap[selectedDateKey] = updatedEvents;
    }
    setEvents(newEventsMap);
  };

  // Get total event count for current month
  const getMonthEventsCount = () => {
    let count = 0;
    const prefix = `${currentYear}-${String(currentMonth + 1).padStart(2, '0')}`;
    Object.keys(events).forEach(key => {
      if (key.startsWith(prefix)) {
        count += events[key].length;
      }
    });
    return count;
  };

  // Filter criteria for UI display
  const categoriesList = ["All", "Inbound Guest", "Meeting", "Campus Visit", "International Desk", "Other"];

  const filteredEvents = selectedDayEvents.filter(ev => {
    if (categoryFilter === "All") return true;
    return ev.category === categoryFilter;
  });

  const getCategoryTheme = (category: EventItem["category"]) => {
    switch (category) {
      case "Inbound Guest":
        return {
          bg: "bg-emerald-50 text-emerald-700 border-emerald-200",
          badge: "bg-emerald-100 text-emerald-800",
          bar: "bg-emerald-500",
          dot: "bg-emerald-500"
        };
      case "Meeting":
        return {
          bg: "bg-sky-50 text-sky-700 border-sky-200",
          badge: "bg-sky-100 text-sky-800",
          bar: "bg-sky-500",
          dot: "bg-sky-500"
        };
      case "Campus Visit":
        return {
          bg: "bg-amber-50 text-amber-700 border-amber-200",
          badge: "bg-amber-100 text-amber-800",
          bar: "bg-amber-500",
          dot: "bg-amber-500"
        };
      case "International Desk":
        return {
          bg: "bg-purple-50 text-purple-700 border-purple-200",
          badge: "bg-purple-100 text-purple-800",
          bar: "bg-purple-500",
          dot: "bg-purple-500"
        };
      default:
        return {
          bg: "bg-gray-50 text-gray-700 border-gray-200",
          badge: "bg-gray-100 text-gray-800",
          bar: "bg-gray-400",
          dot: "bg-gray-400"
        };
    }
  };

  const monthNames = [
    "Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6",
    "Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12"
  ];

  const weekdayNames = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"];

  return (
    <div className="p-6 md:p-8 space-y-8 max-w-7xl mx-auto animate-fade-in">
      
      {/* Jumbotron Welcome Banner */}
      <div className="bg-gradient-to-tr from-[#e3effd] via-[#fef0e6] to-[#e6f7eb] rounded-3xl p-6 md:p-8 text-slate-800 shadow-md relative overflow-hidden border border-slate-200/50">
        <div className="absolute inset-0 bg-[radial-gradient(#f37021_1px,transparent_1px)] opacity-5 pointer-events-none" style={{ backgroundSize: "24px 24px" }}></div>
        <div className="absolute top-0 right-0 w-[450px] h-[450px] bg-gradient-to-br from-[#f37021]/10 to-transparent rounded-full blur-[90px] translate-x-1/4 -translate-y-1/4 pointer-events-none"></div>
        <div className="absolute bottom-0 left-12 w-[250px] h-[250px] bg-[#004c91]/8 rounded-full blur-[60px] translate-y-1/2 pointer-events-none"></div>
        
        <div className="relative z-10 flex flex-col md:flex-row justify-between items-start md:items-center gap-6">
          <div className="space-y-3">
            <div className="flex items-center gap-2 mb-1 bg-white/60 text-slate-700 border border-slate-200/80 px-3.5 py-1.5 rounded-full text-xs font-bold uppercase tracking-wider w-fit shadow-xs">
              <Sparkles className="w-3.5 h-3.5 text-[#f37021] animate-pulse" />
              <span>{isAdmin ? "Khu vực Quản trị Hệ thống" : "Hệ thống quản trị hợp tác quốc tế"}</span>
            </div>
            <h1 className="text-2xl md:text-3xl lg:text-4xl font-extrabold mb-1 tracking-tight">
              <span className="text-transparent bg-clip-text bg-gradient-to-r from-[#004c91] to-[#f37021]">Xin chào, {user.name}</span>
            </h1>
            <p className="text-slate-600 text-xs md:text-sm font-medium max-w-2xl leading-relaxed">
              {isAdmin 
                ? "Chào mừng bạn trở lại! Dưới đây là tổng quan về trạng thái máy chủ, thống kê người dùng và các biểu đồ lưu lượng truy cập hệ thống theo thời gian thực."
                : "Chào mừng bạn trở lại góc quản lý! Mọi kế hoạch đón tiếp, chương trình giao lưu, Inbound, và lịch sự kiện quốc tế hôm nay đều nằm trong tầm tay của bạn."}
            </p>
          </div>
          
          <div className="bg-white/80 backdrop-blur-md border border-slate-200/60 p-5 rounded-2xl flex items-center gap-4 shadow-xs hover:bg-white transition-all shrink-0">
            <div className="w-11 h-11 bg-gradient-to-br from-[#004c91] to-[#0461b5] rounded-xl flex items-center justify-center text-white font-bold shadow-md shadow-blue-500/10">
              <Clock className="w-5.5 h-5.5 text-white" />
            </div>
            <div>
              <p className="text-[10px] uppercase font-bold text-slate-400 tracking-wider">Thời gian hệ thống</p>
              <p className="font-extrabold text-[#004c91] text-sm md:text-base tracking-wide mt-0.5">
                Tháng 8, 2026
              </p>
            </div>
          </div>
        </div>
      </div>

      {user.role?.toUpperCase() === 'HO' ? (
        <HODashboardView />
      ) : isAdmin ? (
        <AdminDashboardView />
      ) : isStaff ? (
        <SharedDashboardView user={user} isDeptLeader={isDeptLeader} isDeptStaff={isDeptStaff} isStudent={isStudent} isVisitor={isVisitor} />
      ) : (
      <>
      {/* Metrics Row */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        
        {/* Metric 1 */}
        <div className="bg-white border border-slate-100/80 rounded-2xl p-6 shadow-xs hover:shadow-md hover:-translate-y-1 transition-all duration-300 flex items-center gap-5 relative group overflow-hidden">
          <div className="absolute top-0 left-0 right-0 h-1 bg-gradient-to-r from-[#f37021] to-orange-400"></div>
          <div className="w-12 h-12 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center transition-all duration-300 group-hover:bg-[#f37021] group-hover:text-white shadow-sm shrink-0">
            <Building className="w-5.5 h-5.5" />
          </div>
          <div>
            <p className="text-[10px] font-extrabold text-slate-400 uppercase tracking-widest mb-1">Cơ sở hiện tại</p>
            <p className="text-lg font-extrabold text-slate-800">Campus {user.campus}</p>
          </div>
        </div>

        {/* Metric 2 */}
        <div className="bg-white border border-slate-100/80 rounded-2xl p-6 shadow-xs hover:shadow-md hover:-translate-y-1 transition-all duration-300 flex items-center gap-5 relative group overflow-hidden">
          <div className="absolute top-0 left-0 right-0 h-1 bg-gradient-to-r from-[#004c91] to-blue-400"></div>
          <div className="w-12 h-12 rounded-xl bg-blue-50 text-[#004c91] flex items-center justify-center transition-all duration-300 group-hover:bg-[#004c91] group-hover:text-white shadow-sm shrink-0">
            <CalendarDays className="w-5.5 h-5.5" />
          </div>
          <div>
            <p className="text-[10px] font-extrabold text-slate-400 uppercase tracking-widest mb-1">Kế hoạch trong tháng</p>
            <p className="text-lg font-extrabold text-slate-800">{getMonthEventsCount()} Sự kiện nổi bật</p>
          </div>
        </div>

        {/* Metric 3 */}
        <div className="bg-white border border-slate-100/80 rounded-2xl p-6 shadow-xs hover:shadow-md hover:-translate-y-1 transition-all duration-300 flex items-center gap-5 relative group overflow-hidden">
          <div className="absolute top-0 left-0 right-0 h-1 bg-gradient-to-r from-emerald-500 to-teal-400"></div>
          <div className="w-12 h-12 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center transition-all duration-300 group-hover:bg-emerald-500 group-hover:text-white shadow-sm shrink-0">
            <User className="w-5.5 h-5.5" />
          </div>
          <div>
            <p className="text-[10px] font-extrabold text-slate-400 uppercase tracking-widest mb-1">Quyền lực vai trò</p>
            <p className="text-lg font-extrabold text-[#0aa14f] uppercase tracking-wider">{user.role}</p>
          </div>
        </div>

      </div>

      {/* Main Integrated Calendar & Events Layout SECTION */}
      <div className="bg-white border border-slate-100 rounded-3xl shadow-lg hover:shadow-xl transition-shadow duration-300 overflow-hidden flex flex-col xl:flex-row">
        
        {/* Left Part: interactive calendar input panel */}
        <div className="w-full xl:w-1/2 bg-white border-b xl:border-b-0 xl:border-r border-slate-100 shrink-0 flex flex-col justify-between overflow-hidden">
          
          {/* Calendar Header with Blue Background */}
          <div className="bg-[#004c91] px-6 py-5 text-white flex items-center justify-between xl:h-24 shrink-0 shadow-xs">
            <h2 className="text-base font-extrabold text-white flex items-center gap-2">
              <Calendar className="w-5 h-5 text-[#f37021]" />
              Lịch Tháng
            </h2>
            <div className="flex items-center gap-1">
              <button 
                onClick={prevMonth}
                className="w-8 h-8 rounded-xl hover:bg-white/10 border border-transparent hover:border-white/20 flex items-center justify-center text-white/80 hover:text-white transition-all hover:shadow-xs cursor-pointer active:scale-95"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <span className="text-sm font-extrabold text-white min-w-[#110px] text-center uppercase tracking-wide">
                {monthNames[currentMonth]} {currentYear}
              </span>
              <button 
                onClick={nextMonth}
                className="w-8 h-8 rounded-xl hover:bg-white/10 border border-transparent hover:border-white/20 flex items-center justify-center text-white/80 hover:text-white transition-all hover:shadow-xs cursor-pointer active:scale-95"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>

          {/* Calendar Day Grid & Form Area with White Background */}
          <div className="p-6 md:p-8 flex-grow flex flex-col justify-between bg-white text-slate-800">
            <div className="w-full max-w-md mx-auto">
              {/* Calendar Grid */}
              <div className="grid grid-cols-7 gap-y-2 gap-x-1.5 mb-6 text-center text-xs">
                {weekdayNames.map(wd => (
                  <div key={wd} className="text-slate-400 font-extrabold py-2 uppercase tracking-widest text-[10px]">{wd}</div>
                ))}
                
                {daysGrid.map((cell, idx) => {
                  const dateStr = formatDateKey(cell.year, cell.month, cell.day);
                  const dayEvents = events[dateStr] || [];
                  const dayHasEvents = dayEvents.length > 0;
                  const isSelected = selectedDay === cell.day && cell.currentMonth;
                  const eventCategories = Array.from(new Set(dayEvents.map(e => e.category)));
                  
                  return (
                    <div key={idx} className="aspect-square flex justify-center items-center py-0.5">
                      <button
                        onClick={() => {
                          if (cell.currentMonth) {
                            setSelectedDay(cell.day);
                          } else {
                            setCurrentMonth(cell.month);
                            setCurrentYear(cell.year);
                            setSelectedDay(cell.day);
                          }
                        }}
                        className={`relative w-10 h-10 rounded-2xl flex flex-col items-center justify-center font-bold text-xs transition-all duration-200 cursor-pointer outline-none
                          ${isSelected 
                            ? "bg-gradient-to-br from-[#f37021] to-[#ff5c00] text-white shadow-md shadow-orange-500/30 scale-105" 
                            : ""
                          }
                          ${!isSelected && cell.isToday 
                            ? "bg-blue-50 border-2 border-dashed border-[#004c91] text-[#004c91] font-extrabold" 
                            : ""
                          }
                          ${!isSelected && cell.currentMonth && !cell.isToday 
                            ? "text-slate-700 hover:bg-[#004c91]/5 hover:text-[#004c91]" 
                            : ""
                          }
                          ${!cell.currentMonth 
                            ? "text-slate-300 font-normal hover:bg-slate-50 hover:text-slate-400" 
                            : ""
                          }
                        `}
                      >
                        <span className="relative z-10">{cell.day}</span>
                        
                        {/* Event Dot Indicators */}
                        {dayHasEvents && (
                          <div className="absolute bottom-1.5 flex justify-center gap-0.5">
                            {eventCategories.slice(0, 3).map((cat, dIdx) => {
                              return (
                                <span 
                                  key={dIdx} 
                                  className={`w-1 h-1 rounded-full ${isSelected ? "bg-white" : "bg-[#f37021]"}`} 
                                />
                              );
                            })}
                          </div>
                        )}
                      </button>
                    </div>
                  );
                })}
              </div>

              <div className="h-[1px] bg-slate-100 mb-6" />
            </div>

            {/* New Event Creation Form (integrated and collapsible) */}
            <div className="space-y-3 w-full max-w-md mx-auto">
              {!isAddFormOpen ? (
                <button
                  onClick={() => setIsAddFormOpen(true)}
                  className="w-full flex items-center justify-center gap-2 py-3.5 px-4 border-2 border-dashed border-[#f37021]/30 hover:border-[#f37021]/60 bg-[#f37021]/10 hover:bg-[#f37021]/15 text-xs font-extrabold text-[#f37021] rounded-2xl transition-all cursor-pointer active:scale-98 shadow-2xs"
                >
                  <Plus className="w-4 h-4 text-[#f37021]" />
                  Lên lịch đón tiếp mới
                </button>
              ) : (
                <div className="bg-white border border-slate-150 rounded-2xl p-4.5 space-y-4 relative shadow-sm animate-fade-in-quick">
                  <button 
                    type="button"
                    onClick={() => setIsAddFormOpen(false)}
                    className="absolute top-3 right-3 p-1.5 text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-full transition-colors cursor-pointer"
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                  
                  <h4 className="text-xs font-extrabold text-slate-800 uppercase tracking-wider flex items-center gap-1.5 border-b border-slate-50 pb-2">
                    <FolderPlus className="w-4 h-4 text-[#f37021]" />
                    Lịch trình {selectedDay}/{currentMonth + 1}
                  </h4>

                  {formError && (
                    <p className="text-[11px] text-red-600 bg-red-50 p-2 rounded-lg border border-red-100 font-bold leading-relaxed">
                      ⚠️ {formError}
                    </p>
                  )}

                  <div className="space-y-3">
                    <div>
                      <label className="block text-[10px] font-extrabold text-slate-400 uppercase tracking-wider mb-1">Tiêu đề sự kiện *</label>
                      <input
                        type="text"
                        required
                        placeholder="VD: Làm việc với trường Swinburne"
                        value={formTitle}
                        onChange={e => setFormTitle(e.target.value)}
                        className="w-full text-xs px-3.5 py-2.5 border border-slate-200 focus:border-[#f37021] focus:ring-2 focus:ring-orange-100 bg-slate-50/40 rounded-xl outline-none text-slate-800 placeholder-slate-400 font-medium transition-all"
                      />
                    </div>

                    <div className="grid grid-cols-2 gap-3">
                      <div>
                        <label className="block text-[10px] font-extrabold text-slate-400 uppercase tracking-wider mb-1">Thời gian</label>
                        <input
                          type="text"
                          placeholder="VD: 09:00 - 11:30"
                          value={formTime}
                          onChange={e => setFormTime(e.target.value)}
                          className="w-full text-xs px-3.5 py-2.5 border border-slate-200 focus:border-[#f37021] focus:ring-2 focus:ring-orange-100 bg-slate-50/40 rounded-xl outline-none text-slate-800 placeholder-slate-400 font-medium transition-all"
                        />
                      </div>
                      <div>
                        <label className="block text-[10px] font-extrabold text-slate-400 uppercase tracking-wider mb-1">Phân loại</label>
                        <select
                          value={formCategory}
                          onChange={e => setFormCategory(e.target.value as EventItem["category"])}
                          className="w-full text-xs px-2 py-2.5 border border-slate-200 focus:border-[#f37021] focus:ring-2 focus:ring-orange-100 bg-slate-50/50 rounded-xl outline-none text-slate-800 font-bold tracking-wide transition-all cursor-pointer"
                        >
                          <option value="Inbound Guest">Inbound Guest</option>
                          <option value="Meeting">Meeting (Họp)</option>
                          <option value="Campus Visit">Campus Visit</option>
                          <option value="International Desk">Intl Desk</option>
                          <option value="Other">Khác</option>
                        </select>
                      </div>
                    </div>

                    <div>
                      <label className="block text-[10px] font-extrabold text-slate-400 uppercase tracking-wider mb-1">Mô tả tóm tắt</label>
                      <textarea
                        rows={2}
                        placeholder="Nội dung tóm tắt buổi gặp mặt..."
                        value={formDescription}
                        onChange={e => setFormDescription(e.target.value)}
                        className="w-full text-xs px-3.5 py-2.5 border border-slate-200 focus:border-[#f37021] focus:ring-2 focus:ring-orange-100 bg-slate-50/40 rounded-xl outline-none text-slate-800 placeholder-slate-400 font-medium resize-none transition-all"
                      />
                    </div>
                  </div>

                  <div className="flex gap-2 pt-2 border-t border-slate-50">
                    <button
                      onClick={handleAddEventSubmit}
                      className="flex-1 text-xs py-2.5 px-3 bg-gradient-to-r from-[#f37021] to-[#ff5c00] text-white font-extrabold rounded-xl hover:opacity-95 shadow-md shadow-orange-500/10 cursor-pointer active:scale-98 transition-all"
                    >
                      Lưu lịch trình
                    </button>
                    <button
                      type="button"
                      onClick={() => setIsAddFormOpen(false)}
                      className="text-xs py-2.5 px-4 bg-white border border-slate-200 text-slate-650 font-bold rounded-xl hover:bg-slate-50 transition-colors cursor-pointer"
                    >
                      Hủy
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Right Part: detailed selected Day's schedules display */}
        <div className="flex-1 w-full xl:w-1/2 bg-white flex flex-col min-w-0">
          
          {/* Detailed Schedules Header with Orange Background */}
          <div className="bg-[#f37021] px-6 py-5 text-white flex flex-col md:flex-row md:items-center justify-between gap-4 shrink-0 shadow-sm xl:h-24">
            <div>
              <span className="text-[10px] font-extrabold text-orange-100/90 uppercase tracking-widest block mb-0.5">Lịch trình làm việc chi tiết</span>
              <h3 className="text-lg font-black text-white tracking-tight flex items-center gap-2">
                <span>Ngày {selectedDay} {monthNames[currentMonth]} {currentYear}</span>
              </h3>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-xs font-extrabold text-[#f37021] bg-white px-3.5 py-2 rounded-xl shrink-0 shadow-md">
                {selectedDayEvents.length} Lịch hoạt động
              </span>
            </div>
          </div>

          {/* Schedules Content with White background */}
          <div className="p-6 md:p-8 flex-grow flex flex-col bg-white">
            
            {/* Filtering Category Dropdown */}
            {selectedDayEvents.length > 0 && (
              <div className="flex items-center gap-2 mb-6 bg-white pl-3.5 pr-2 py-1.5 rounded-xl border border-slate-200/70 hover:border-slate-350 w-fit shadow-xs shrink-0 select-none transition-all">
                <span className="text-[10px] text-slate-400 font-extrabold flex items-center gap-1.5 shrink-0 uppercase tracking-widest">
                  <Filter className="w-3.5 h-3.5 text-[#004c91]" /> Bộ lọc chuyên mục:
                </span>
                <div className="relative flex items-center">
                  <select
                    id="category-filter-select"
                    value={categoryFilter}
                    onChange={e => setCategoryFilter(e.target.value)}
                    className="text-xs font-bold text-slate-700 bg-transparent pl-1.5 pr-7 py-1 border-none outline-none focus:ring-0 focus:outline-none cursor-pointer tracking-wide appearance-none relative z-10"
                  >
                    {categoriesList.map(cat => (
                      <option key={cat} value={cat} className="font-bold text-slate-700 bg-white">
                        {cat === "All" ? "Tất cả" : cat}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="w-3.5 h-3.5 text-slate-400 absolute right-1 pointer-events-none z-0" />
                </div>
              </div>
            )}

            {/* Events List display */}
            <div className="flex-grow space-y-4 text-slate-800">
              {filteredEvents.length > 0 ? (
                filteredEvents.map(ev => {
                  const theme = getCategoryTheme(ev.category);
                  return (
                    <div 
                      key={ev.id} 
                      className="bg-slate-50 hover:bg-slate-100/70 border border-slate-200/50 rounded-2xl p-6 hover:shadow-[0_12px_24px_rgba(0,0,0,0.04)] hover:-translate-y-0.5 transition-all duration-300 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-5 relative overflow-hidden group shadow-2xs"
                    >
                      {/* Left Accent Bar Indicator */}
                      <div className={`absolute top-0 bottom-0 left-0 w-1.5 ${theme.bar} transition-all duration-200 group-hover:w-2`} />
                      
                      <div className="flex-1 space-y-2.5 pl-3 min-w-0">
                        <div className="flex flex-wrap items-center gap-2.5">
                          <span className="text-xs font-bold text-slate-500 flex items-center gap-1 bg-slate-50 border border-slate-100 px-2.5 py-1 rounded-xl">
                            <Clock className="w-3.5 h-3.5 text-[#f37021]" />
                            {ev.time}
                          </span>
                          <span className={`text-[10px] font-extrabold px-2.5 py-1 rounded-xl border ${theme.badge} uppercase tracking-wider`}>
                            {ev.category}
                          </span>
                        </div>
                        
                        <h4 className="text-[15px] font-extrabold text-slate-800 group-hover:text-[#004c91] transition-colors leading-snug">
                          {ev.title}
                        </h4>
                        
                        <p className="text-xs text-slate-500 font-medium leading-relaxed max-w-2xl">
                          {ev.description}
                        </p>
                      </div>

                      <button
                        onClick={() => handleDeleteEvent(ev.id)}
                        className="p-2.5 text-slate-400 hover:text-red-650 hover:bg-red-50 hover:border-red-100 border border-transparent rounded-xl transition-all flex-shrink-0 self-end sm:self-center cursor-pointer active:scale-95 hover:shadow-3xs"
                        title="Xóa sự kiện"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  );
                })
              ) : (
                <div className="h-72 flex flex-col items-center justify-center border-2 border-dashed border-slate-200/85 rounded-3xl p-6 text-center select-none bg-slate-50/50 shadow-3xs">
                  <div className="w-14 h-14 rounded-2xl bg-white text-[#004c91] flex items-center justify-center mb-4 border border-slate-150 shadow-inner">
                    <Calendar className="w-6.5 h-6.5" />
                  </div>
                  <h4 className="text-sm font-extrabold text-slate-700 mb-1">
                    {categoryFilter !== "All" ? "Không có lịch lọc phù hợp" : "Khung thời gian trống"}
                  </h4>
                  <p className="text-xs text-slate-450 max-w-xs leading-relaxed font-semibold">
                    {categoryFilter !== "All" 
                      ? `Không tìm thấy chương trình nào phù hợp với bộ lọc "${categoryFilter}" cho ngày được chọn.`
                      : "Không có cuộc họp hay phái đoàn nào được ghi nhận vào ngày này. Hãy lên lịch trình mới ngay."
                    }
                  </p>
                  {categoryFilter !== "All" && (
                    <button
                      onClick={() => setCategoryFilter("All")}
                      className="mt-4 text-xs text-slate-600 font-extrabold hover:underline cursor-pointer flex items-center gap-1 bg-white px-3.5 py-2 border border-slate-200 hover:bg-slate-50 rounded-xl active:scale-95 transition-all shadow-3xs"
                    >
                      Xem toàn bộ kế hoạch ngày này
                    </button>
                  )}
                </div>
              )}
            </div>

          </div>

        </div>
      </div>
      </>
      )}

    </div>
  );
}
