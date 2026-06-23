/**
 * Trang CreateVisitRequest
 * Đầu mối điền form xác lập yêu cầu chuyến tiếp đón.
 */

import React, { useState, useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ChevronRight, Home, ArrowLeft, Calendar, Clock, Plus, Trash2, Search, Bell, Mail, Info, UserX, CheckSquare, Users, Edit3, X, ArrowRight, CheckCircle, ChevronDown } from 'lucide-react';
import { motion } from 'motion/react';

export function CreateVisitRequest() {
  const navigate = useNavigate();
  const location = useLocation();
  const guestData = location.state?.guestData || null; // Y1 uses this, Y2 is null

  const userStr = localStorage.getItem("currentUser");
  const currentUser = userStr ? JSON.parse(userStr) : null;
  const isHO = currentUser?.role?.toUpperCase() === 'HO';

  // Form State
  const [hostInfo, setHostInfo] = useState({
    name: '',
    org: '',
    title: '',
    phone: '',
    email: ''
  });

  const [visitMode, setVisitMode] = useState<'single' | 'multiple'>('single');
  const [visits, setVisits] = useState([{ id: '1', campus: isHO ? 'Hà Nội' : 'Hà Nội', date: '', startTime: '', endTime: '' }]);
  const campusOptions = ['Hà Nội', 'Đà Nẵng', 'Cần Thơ', 'Hồ Chí Minh', 'Quy Nhơn'];

  useEffect(() => {
    if (visitMode === 'single' && visits.length > 1) {
      setVisits([visits[0]]);
    }
  }, [visitMode]);

  const [guestInfo, setGuestInfo] = useState<{
    name: string;
    purpose: string;
    workContent: string;
  }>({
    name: '',
    purpose: '',
    workContent: '',
  });

  const [visitType, setVisitType] = useState<string>('CAMPUS_TOUR');
  const [otherType, setOtherType] = useState('');
  
  // New fields from v8.4
  const [workingLanguage, setWorkingLanguage] = useState<string>('EN');
  const [transportationType, setTransportationType] = useState<string>('UNKNOWN');
  const [transportationDetail, setTransportationDetail] = useState<string>('');
  const [mediaConsentStatus, setMediaConsentStatus] = useState<string>('DECLINED');
  const [mediaConsentNote, setMediaConsentNote] = useState<string>('');
  
  const [agendaItems, setAgendaItems] = useState([{ startTime: '', endTime: '', content: '' }]);

  const [participants, setParticipants] = useState({
    isMeHost: true,
    host: currentUser || null,
    supporters: [] as any[],
    otherDepts: [] as any[],
    students: [] as any[]
  });

  const [selectedHostOption, setSelectedHostOption] = useState('');
  const [addedHost, setAddedHost] = useState<string | null>(null);
  const [showNoAccountError, setShowNoAccountError] = useState(false);

  const [selectedSupporterOption, setSelectedSupporterOption] = useState('');
  const [addedSupporters, setAddedSupporters] = useState<string[]>([]);
  const [showSupporterNoAccountError, setShowSupporterNoAccountError] = useState(false);

  const [selectedOtherDeptOption, setSelectedOtherDeptOption] = useState('');
  const [selectedOtherDept, setSelectedOtherDept] = useState('');
  const [addedOtherDepts, setAddedOtherDepts] = useState<string[]>([]);
  const [showOtherDeptNoAccountError, setShowOtherDeptNoAccountError] = useState(false);

  const [studentSearchText, setStudentSearchText] = useState('');
  const [addedStudents, setAddedStudents] = useState<string[]>([]);
  const [showStudentNoAccountError, setShowStudentNoAccountError] = useState(false);

  const [alerts, setAlerts] = useState({
    system: { days: 1, time: '09:00', period: 'AM' },
    email: { days: 2, time: '14:00', period: 'PM' }
  });

  const [notes, setNotes] = useState('');

  // Collapse state for 3 main sections
  const [collapseSection1, setCollapseSection1] = useState(false);
  const [collapseSection2, setCollapseSection2] = useState(false);
  const [collapseSection3, setCollapseSection3] = useState(false);

  // Section completion checkers (for green border)
  const isSection1Complete = !!(hostInfo.name && hostInfo.org && hostInfo.title && hostInfo.phone && hostInfo.email);
  const isSection2Complete = !!(guestInfo.name && visits.every(v => v.date && v.startTime && v.endTime) && guestInfo.purpose && guestInfo.workContent);
  const isSection3Complete = !!visitType;

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12 animate-in fade-in duration-300 relative">
      <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <span className="cursor-pointer hover:text-[#004c91] transition-colors" onClick={() => navigate('/dashboard/visit')}>Quản lý tiếp khách</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Tạo đoàn khách</span>
      </div>

      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91]">
            Tạo đoàn khách
          </h1>
          <p className="text-gray-500 mt-1 font-medium">
            {guestData ? "Phê duyệt và chuyển đổi yêu cầu thành đoàn khách chính thức" : "Tạo mới đoàn khách vào hệ thống"}
          </p>
        </div>
      </div>

      <div className="space-y-8 flex-1">
        
        {/* Section 1: Thông tin người tạo / đăng ký */}
        <div className={`bg-white rounded-2xl shadow-sm overflow-hidden border-2 transition-colors duration-300 ${isSection1Complete ? 'border-green-400' : 'border-[#004c91]/20'}`}>
          <div
            className="bg-[#004c91] px-6 py-4 border-b border-[#003366] flex items-center justify-between cursor-pointer select-none"
            onClick={() => setCollapseSection1(p => !p)}
          >
            <h2 className="text-lg font-bold text-white flex items-center gap-2">
              <span className="flex items-center justify-center w-6 h-6 rounded-full bg-[#f37021] text-white font-black text-sm">1</span>
              Thông tin người {guestData ? 'đăng ký' : 'tạo'}
              {isSection1Complete && <span className="ml-2 text-green-300 text-xs font-bold flex items-center gap-1">✓ Hoàn thành</span>}
            </h2>
            <ChevronDown className={`w-5 h-5 text-white/70 transition-transform duration-200 ${collapseSection1 ? '-rotate-90' : ''}`} />
          </div>
          {!collapseSection1 && (
          <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-2">Họ và tên</label>
              <input type="text" className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm bg-gray-50/50" value={hostInfo.name} onChange={e => setHostInfo({...hostInfo, name: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-2">Đơn vị công tác</label>
              <input type="text" className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm bg-gray-50/50" value={hostInfo.org} onChange={e => setHostInfo({...hostInfo, org: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-2">Chức danh, phòng ban</label>
              <input type="text" className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm bg-gray-50/50" value={hostInfo.title} onChange={e => setHostInfo({...hostInfo, title: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-2">Số điện thoại</label>
              <input type="text" className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm bg-gray-50/50" value={hostInfo.phone} onChange={e => setHostInfo({...hostInfo, phone: e.target.value})} />
            </div>
            <div className="md:col-span-2">
              <label className="block text-sm font-bold text-gray-700 mb-2">Email</label>
              <input type="email" className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm bg-gray-50/50" value={hostInfo.email} onChange={e => setHostInfo({...hostInfo, email: e.target.value})} />
            </div>
          </div>
          )}
        </div>

        {/* Section 2: Thông tin đoàn khách */}
        <div className={`bg-white rounded-2xl shadow-sm overflow-hidden border-2 transition-colors duration-300 ${isSection2Complete ? 'border-green-400' : 'border-[#004c91]/20'}`}>
          <div
            className="bg-[#004c91] px-6 py-4 border-b border-[#003366] flex items-center justify-between cursor-pointer select-none"
            onClick={() => setCollapseSection2(p => !p)}
          >
            <h2 className="text-lg font-bold text-white flex items-center gap-2">
              <span className="flex items-center justify-center w-6 h-6 rounded-full bg-[#f37021] text-white font-black text-sm">2</span>
              Thông tin đoàn khách
              {isSection2Complete && <span className="ml-2 text-green-300 text-xs font-bold flex items-center gap-1">✓ Hoàn thành</span>}
            </h2>
            <ChevronDown className={`w-5 h-5 text-white/70 transition-transform duration-200 ${collapseSection2 ? '-rotate-90' : ''}`} />
          </div>
          {!collapseSection2 && <div className="p-6 space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-bold text-gray-700 mb-2">Tên đoàn khách</label>
                <input type="text" className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm" value={guestInfo.name} onChange={e => setGuestInfo({...guestInfo, name: e.target.value})} placeholder="Nhập tên đoàn..." />
              </div>
              <div className="relative">
                <label className="block text-sm font-bold text-gray-700 mb-2">Cơ sở tới thăm</label>
                <div className="relative">
                  <select
                    value={visitMode}
                    onChange={(e) => setVisitMode(e.target.value as 'single' | 'multiple')}
                    className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm font-medium bg-white appearance-none"
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
                    {visitMode === 'multiple' && visits.length > 1 && (
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
                      {index === 0 && <label className="block text-xs font-bold text-gray-600 mb-1 uppercase tracking-wider">Cơ sở</label>}
                      <div className="relative">
                        <select
                          value={visit.campus}
                          onChange={(e) => {
                            const newVisits = [...visits];
                            newVisits[index].campus = e.target.value;
                            setVisits(newVisits);
                          }}
                          className="w-full px-4 py-2.5 rounded-xl border border-gray-200 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm font-medium bg-white shadow-sm appearance-none pr-8"
                        >
                          {campusOptions.map(c => <option key={c} value={c}>{c}</option>)}
                        </select>
                        <ChevronDown className="absolute right-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
                      </div>
                    </div>
                    {/* Ngày bắt đầu */}
                    <div className="flex-[1.5] w-full xl:w-auto relative">
                      {index === 0 && <label className="block text-xs font-bold text-gray-600 mb-1 uppercase tracking-wider">Ngày bắt đầu</label>}
                      <div className="relative">
                        <input type="date" value={visit.date} onChange={(e) => {
                          const newVisits = [...visits];
                          newVisits[index].date = e.target.value;
                          setVisits(newVisits);
                        }} className="w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-200 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm font-medium bg-white shadow-sm" required />
                        <Calendar className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91]" />
                      </div>
                    </div>
                    {/* Thời gian bắt đầu */}
                    <div className="flex-1 w-full xl:w-auto relative">
                      {index === 0 && <label className="block text-xs font-bold text-gray-600 mb-1 uppercase tracking-wider">TG Bắt đầu</label>}
                      <div className="relative">
                        <input type="time" value={visit.startTime} onChange={(e) => {
                          const newV = [...visits]; newV[index].startTime = e.target.value; setVisits(newV);
                        }} className="w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-200 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm font-medium bg-white shadow-sm" required />
                        <Clock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91]" />
                      </div>
                    </div>
                    {/* Thời gian kết thúc */}
                    <div className="flex-1 w-full xl:w-auto relative">
                      {index === 0 && <label className="block text-xs font-bold text-gray-600 mb-1 uppercase tracking-wider">TG Kết thúc</label>}
                      <div className="relative">
                        <input type="time" value={visit.endTime} onChange={(e) => {
                          const newV = [...visits]; newV[index].endTime = e.target.value; setVisits(newV);
                        }} className="w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-200 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm font-medium bg-white shadow-sm" required />
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

              {visitMode === 'multiple' && (
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
                <textarea className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm min-h-[80px] resize-none" value={guestInfo.purpose} onChange={e => setGuestInfo({...guestInfo, purpose: e.target.value})} placeholder="Nhập mục đích..."></textarea>
              </div>
              <div>
                <label className="block text-sm font-bold text-gray-700 mb-2">Nội dung làm việc</label>
                <textarea className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm min-h-[80px] resize-none" value={guestInfo.workContent} onChange={e => setGuestInfo({...guestInfo, workContent: e.target.value})} placeholder="Nhập nội dung dự kiến..."></textarea>
              </div>
            </div>
          </div>}
        </div>

        {/* Section 3: Setup */}
        <div className={`bg-white rounded-2xl shadow-sm overflow-hidden mb-8 border-2 transition-colors duration-300 ${isSection3Complete ? 'border-green-400' : 'border-[#004c91]/20'}`}>
          <div
            className="bg-[#004c91] px-6 py-4 border-b border-[#003366] flex items-center justify-between cursor-pointer select-none"
            onClick={() => setCollapseSection3(p => !p)}
          >
            <h2 className="text-lg font-bold text-white flex items-center gap-2">
              <span className="flex items-center justify-center w-6 h-6 rounded-full bg-[#f37021] text-white font-black text-sm">3</span>
              Thiết lập & Điều phối sự kiện (Set up)
              {isSection3Complete && <span className="ml-2 text-green-300 text-xs font-bold">✓ Hoàn thành</span>}
            </h2>
            <ChevronDown className={`w-5 h-5 text-white/70 transition-transform duration-200 ${collapseSection3 ? '-rotate-90' : ''}`} />
          </div>
          
          {!collapseSection3 && <div className="p-0">
            {/* 3.1 Loại hình tham quan */}
            <div className="p-6 border-b border-gray-100">
              <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-4">
                <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                1. Loại hình tham quan
              </h3>
              <div className="flex flex-wrap gap-4">
                {[
                  { value: 'CAMPUS_TOUR', label: 'Campus Tour' },
                  { value: 'MEETING', label: 'Họp trao đổi' },
                  { value: 'WORKSHOP', label: 'Workshop' },
                  { value: 'SIGNING_CEREMONY', label: 'Lễ ký kết' },
                  { value: 'EXCHANGE', label: 'Giao lưu' },
                  { value: 'OTHER', label: 'Khác' },
                ].map(type => (
                  <label key={type.value} className="flex items-center gap-2 cursor-pointer group">
                    <input type="radio" name="visitType" value={type.value} checked={visitType === type.value} onChange={() => setVisitType(type.value)} className="w-5 h-5 rounded-full border-gray-300 text-[#004c91] focus:ring-[#004c91]" />
                    <span className="text-sm font-medium text-gray-700 group-hover:text-gray-900">{type.label}</span>
                  </label>
                ))}
              </div>
              {visitType === 'OTHER' && (
                <div className="mt-4">
                  <input type="text" placeholder="Ghi chú loại hình khác..." className="w-full px-4 py-2 rounded-lg border border-gray-200 focus:border-[#004c91] outline-none text-sm" value={otherType} onChange={e => setOtherType(e.target.value)} />
                </div>
              )}
            </div>

            {/* 3.2 Agenda - chỉ hiện khi có loại hình tham quan */}
            {visitType && <div className="p-6 border-b border-gray-100 bg-slate-50/50">
              <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-2">
                <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                2. Agenda dự kiến
              </h3>
              <p className="text-xs text-gray-500 mb-4">Lên lịch trình theo từng khoảng thời gian. Bạn có thể thêm hoặc xóa các bước.</p>
              
              <div className="space-y-3">
                {agendaItems.map((item, index) => (
                  <div key={index} className="flex flex-col md:flex-row items-end gap-3">
                    <div className="flex items-center gap-3 w-full md:w-auto shrink-0">
                      <div className="flex flex-col">
                        <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Thời gian bắt đầu</label>
                        <div className="border border-gray-300 hover:border-[#004c91] focus-within:border-[#004c91] focus-within:ring-2 focus-within:ring-[#004c91]/20 rounded-xl bg-white p-1.5 transition-all">
                          <input 
                            type="time" 
                            value={item.startTime}
                            onChange={(e) => {
                              const newAgenda = [...agendaItems];
                              newAgenda[index].startTime = e.target.value;
                              setAgendaItems(newAgenda);
                            }}
                            className="w-[124px] px-2 py-1.5 focus:text-[#004c91] outline-none text-sm bg-transparent font-medium" 
                          />
                        </div>
                      </div>
                      <span className="text-gray-400 font-bold mt-5">-</span>
                      <div className="flex flex-col">
                        <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Thời gian kết thúc</label>
                        <div className="border border-gray-300 hover:border-[#004c91] focus-within:border-[#004c91] focus-within:ring-2 focus-within:ring-[#004c91]/20 rounded-xl bg-white p-1.5 transition-all">
                          <input 
                            type="time" 
                            value={item.endTime}
                            onChange={(e) => {
                              const newAgenda = [...agendaItems];
                              newAgenda[index].endTime = e.target.value;
                              setAgendaItems(newAgenda);
                            }}
                            className="w-[124px] px-2 py-1.5 focus:text-[#004c91] outline-none text-sm bg-transparent font-medium" 
                          />
                        </div>
                      </div>
                    </div>
                    <div className="flex-1 w-full flex flex-col">
                      <label className="text-[10px] uppercase font-bold text-gray-500 mb-1 ml-1">Nội dung</label>
                      <div className="flex items-center gap-3">
                        <input 
                          type="text" 
                          placeholder="Nội dung dự kiến..." 
                          value={item.content}
                          onChange={(e) => {
                            const newAgenda = [...agendaItems];
                            newAgenda[index].content = e.target.value;
                            setAgendaItems(newAgenda);
                          }}
                          className="flex-1 w-full px-4 py-3 rounded-xl border border-gray-300 hover:border-[#004c91] focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 outline-none text-sm bg-white transition-all shadow-sm" 
                        />
                        <button 
                          onClick={() => {
                            if (agendaItems.length > 1) {
                              setAgendaItems(agendaItems.filter((_, i) => i !== index));
                            }
                          }}
                          className={`p-3 rounded-xl transition-colors shrink-0 ${agendaItems.length > 1 ? 'text-red-500 hover:bg-red-50 hover:text-red-600' : 'text-gray-300 opacity-50 cursor-not-allowed'}`}
                        >
                          <X className="w-5 h-5" />
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
                
                <button 
                  onClick={() => setAgendaItems([...agendaItems, { startTime: '', endTime: '', content: '' }])}
                  className="flex items-center gap-2 text-sm font-bold text-[#004c91] hover:bg-[#004c91]/10 px-4 py-2 rounded-lg transition-colors mt-2"
                >
                  <Plus className="w-4 h-4" /> Thêm nội dung
                </button>
              </div>
            </div>}

            {/* 3.3 Thành phần tham gia */}
            <div className="p-6 border-b border-gray-100">
              <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-6">
                <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                3. Thành phần tham gia
              </h3>
              
              <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
                {/* 1. Host */}
                <div className="bg-white border border-gray-200 border-l-[6px] border-l-[#004c91] rounded-xl p-5 shadow-sm bg-gradient-to-r from-[#004c91]/[0.03] to-transparent">
                  <div className="flex items-center justify-between mb-4">
                    <h4 className="font-bold text-[#004c91] text-base flex items-center gap-2">
                      <UserX className="w-5 h-5" /> 1. Host (Bắt buộc)
                    </h4>
                    <label className="flex items-center gap-2 cursor-pointer bg-blue-50 px-3 py-1.5 rounded-lg border border-blue-100">
                      <input type="checkbox" checked={participants.isMeHost} onChange={e => setParticipants({...participants, isMeHost: e.target.checked})} className="w-4 h-4 rounded border-gray-300 text-[#004c91]" />
                      <span className="text-xs font-bold text-[#004c91]">Là tôi</span>
                    </label>
                  </div>
                  {!participants.isMeHost && (
                    <div>
                      <label className="block text-xs font-medium text-gray-500 mb-1">Chọn người thuộc phòng IC thay thế</label>
                      <div className="flex gap-2">
                        <select 
                          className="flex-1 px-4 py-2 rounded-lg border border-gray-300 text-sm outline-none focus:border-[#004c91] hover:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all"
                          value={selectedHostOption}
                          onChange={(e) => setSelectedHostOption(e.target.value)}
                        >
                          <option value="">Chọn tài khoản IC...</option>
                          <option value="Nguyễn Văn IC">Nguyễn Văn IC</option>
                          <option value="Trần Thị IC">Trần Thị IC</option>
                          <option value="Nguyễn Có TK">Nguyễn Có TK</option>
                        </select>
                        <button 
                          onClick={() => {
                            if (selectedHostOption === 'Nguyễn Không TK') {
                              setShowNoAccountError(true);
                              setAddedHost(null);
                            } else if (selectedHostOption) {
                              setAddedHost(selectedHostOption);
                              setShowNoAccountError(false);
                            }
                          }}
                          className="px-4 py-2 bg-blue-50 hover:bg-blue-100 text-[#004c91] rounded-lg text-sm font-bold transition-colors flex items-center gap-1 shrink-0"
                        >
                          <Plus className="w-4 h-4" /> Thêm
                        </button>
                      </div>
                      
                      {showNoAccountError && (
                        <p className="text-sm text-red-500 mt-2 font-medium">
                          Nguyễn Không TK chưa có tài khoản <ArrowRight className="w-4 h-4 inline mx-1" /> 
                          <span 
                            className="underline cursor-pointer hover:text-red-600 font-bold"
                            onClick={() => navigate('/dashboard/accounts?action=create')}
                          >
                            Tạo tài khoản
                          </span>
                        </p>
                      )}

                      {addedHost && !showNoAccountError && (
                        <div className="flex items-center gap-3 p-3 bg-gray-50 rounded-lg border border-gray-100 mt-2">
                          <div className="w-8 h-8 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs">
                            {addedHost.charAt(0)}
                          </div>
                          <div>
                            <div className="text-sm font-bold text-gray-900">{addedHost}</div>
                            <div className="text-xs text-green-600 font-medium flex items-center gap-1">
                              <CheckCircle className="w-3 h-3" /> Đã thêm
                            </div>
                          </div>
                          <button 
                            onClick={() => setAddedHost(null)}
                            className="ml-auto p-1.5 text-gray-400 hover:text-red-500 rounded-lg hover:bg-red-50 transition-colors"
                          >
                            <X className="w-4 h-4" />
                          </button>
                        </div>
                      )}
                    </div>
                  )}
                  {participants.isMeHost && currentUser && (
                    <div className="flex items-center gap-3 p-3 bg-gray-50 rounded-lg border border-gray-100 mt-2">
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
                <div className="bg-white border border-gray-200 border-l-[6px] border-l-[#004c91] rounded-xl p-5 shadow-sm bg-gradient-to-r from-[#004c91]/[0.03] to-transparent">
                  <h4 className="font-bold text-[#004c91] text-base flex items-center gap-2 mb-4">
                    <Users className="w-5 h-5" /> 2. Người hỗ trợ (Phòng IC)
                  </h4>
                  <div>
                    <div className="flex gap-2">
                      <select 
                        className="flex-1 px-4 py-2 rounded-lg border border-gray-300 text-sm outline-none focus:border-[#004c91] hover:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all"
                        value={selectedSupporterOption}
                        onChange={(e) => setSelectedSupporterOption(e.target.value)}
                      >
                        <option value="">Chọn nhân sự phòng IC...</option>
                        <option value="Thêm người A">Thêm người A</option>
                        <option value="Nguyễn Có TK">Nguyễn Có TK</option>
                      </select>
                      <button 
                        onClick={() => {
                          if (selectedSupporterOption) {
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

                    {showSupporterNoAccountError && (
                      <p className="text-sm text-red-500 mt-2 font-medium">
                        Nguyễn Không TK chưa có tài khoản <ArrowRight className="w-4 h-4 inline mx-1" /> 
                        <span 
                          className="underline cursor-pointer hover:text-red-600 font-bold"
                          onClick={() => navigate('/dashboard/accounts?action=create')}
                        >
                          Tạo tài khoản
                        </span>
                      </p>
                    )}

                    {addedSupporters.length > 0 && (
                      <div className="mt-3 space-y-2">
                        {addedSupporters.map((supporter, idx) => (
                          <div key={idx} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg border border-gray-100">
                            <div className="flex items-center gap-3">
                              <div className="w-8 h-8 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs">
                                {supporter.charAt(0)}
                              </div>
                              <div>
                                <div className="text-sm font-bold text-gray-900">{supporter}</div>
                                <div className="text-xs text-green-600 font-medium flex items-center gap-1">
                                  <CheckCircle className="w-3 h-3" /> Đã thêm
                                </div>
                              </div>
                            </div>
                            <button 
                              onClick={() => setAddedSupporters(addedSupporters.filter(s => s !== supporter))}
                              className="p-1.5 text-gray-400 hover:text-red-500 rounded-lg hover:bg-red-50 transition-colors"
                            >
                              <X className="w-4 h-4" />
                            </button>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

                {/* 3. Người tham gia phòng khác */}
                <div className="bg-white border border-gray-200 border-l-[6px] border-l-[#004c91] rounded-xl p-5 shadow-sm bg-gradient-to-r from-[#004c91]/[0.03] to-transparent">
                  <h4 className="font-bold text-[#004c91] text-base flex items-center gap-2 mb-4">
                    <Users className="w-5 h-5" /> 3. Người tham gia (Phòng ban khác)
                  </h4>
                  <div>
                    <div className="flex gap-2 items-center">
                      <select 
                        className="w-[250px] px-4 py-2 rounded-lg border border-gray-300 text-sm outline-none focus:border-[#004c91] hover:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all bg-white"
                        value={selectedOtherDept}
                        onChange={(e) => {
                          const dept = e.target.value;
                          setSelectedOtherDept(dept);
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
                        onClick={() => {
                          if (selectedOtherDeptOption) {
                            if (!addedOtherDepts.includes(selectedOtherDeptOption)) {
                              setAddedOtherDepts([...addedOtherDepts, selectedOtherDeptOption]);
                            }
                            setShowOtherDeptNoAccountError(false);
                            // Clear selections
                            setSelectedOtherDept('');
                            setSelectedOtherDeptOption('');
                          }
                        }}
                        className="px-4 py-2 bg-blue-50 hover:bg-blue-100 text-[#004c91] rounded-lg text-sm font-bold transition-colors flex items-center gap-1 shrink-0 h-full"
                      >
                        <Plus className="w-4 h-4" /> Thêm
                      </button>
                    </div>

                    {showOtherDeptNoAccountError && (
                      <p className="text-sm text-red-500 mt-2 font-medium">
                        Nguyễn Không TK chưa có tài khoản <ArrowRight className="w-4 h-4 inline mx-1" /> 
                        <span 
                          className="underline cursor-pointer hover:text-red-600 font-bold"
                          onClick={() => navigate('/dashboard/accounts?action=create')}
                        >
                          Tạo tài khoản
                        </span>
                      </p>
                    )}

                    {addedOtherDepts.length > 0 && (
                      <div className="mt-3 space-y-2">
                        {addedOtherDepts.map((person, idx) => (
                          <div key={idx} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg border border-gray-100">
                            <div className="flex items-center gap-3">
                              <div className="w-8 h-8 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs">
                                {person.charAt(0)}
                              </div>
                              <div>
                                <div className="text-sm font-bold text-gray-900">{person}</div>
                                <div className="text-xs text-green-600 font-medium flex items-center gap-1">
                                  <CheckCircle className="w-3 h-3" /> Đã thêm
                                </div>
                              </div>
                            </div>
                            <button 
                              onClick={() => setAddedOtherDepts(addedOtherDepts.filter(p => p !== person))}
                              className="p-1.5 text-gray-400 hover:text-red-500 rounded-lg hover:bg-red-50 transition-colors"
                            >
                              <X className="w-4 h-4" />
                            </button>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

                {/* 4. Sinh viên hỗ trợ */}
                <div className="bg-white border border-gray-200 border-l-[6px] border-l-[#004c91] rounded-xl p-5 shadow-sm bg-gradient-to-r from-[#004c91]/[0.03] to-transparent">
                  <h4 className="font-bold text-[#004c91] text-base flex items-center gap-2 mb-4">
                    <Users className="w-5 h-5" /> 4. Sinh viên hỗ trợ (Buddy, Media)
                  </h4>
                  <div>
                    <div className="flex gap-2 mb-3">
                      <div className="flex-1 relative">
                        <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
                        <input 
                          type="text" 
                          placeholder="Tìm kiếm theo Email hoặc MSSV học sinh..." 
                          className="w-full pl-9 pr-4 py-2 rounded-lg border border-gray-300 text-sm outline-none focus:border-[#004c91] hover:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all"
                          value={studentSearchText}
                          onChange={(e) => setStudentSearchText(e.target.value)}
                        />
                      </div>
                      <button 
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

                    {showStudentNoAccountError && (
                      <p className="text-sm text-red-500 mt-2 font-medium">
                        Sinh viên chưa có tài khoản trên hệ thống <ArrowRight className="w-4 h-4 inline mx-1" /> 
                        <span className="font-bold">
                          Liên hệ Trưởng phòng IC để cấp tài khoản
                        </span>
                      </p>
                    )}

                    {addedStudents.length > 0 && (
                      <div className="mt-3 space-y-2">
                        {addedStudents.map((student, idx) => (
                          <div key={idx} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg border border-gray-100">
                            <div className="flex items-center gap-3">
                              <div className="w-8 h-8 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs">
                                S
                              </div>
                              <div>
                                <div className="text-sm font-bold text-gray-900">{student}</div>
                                <div className="text-xs text-green-600 font-medium flex items-center gap-1">
                                  <CheckCircle className="w-3 h-3" /> Đã thêm
                                </div>
                              </div>
                            </div>
                            <button 
                              onClick={() => setAddedStudents(addedStudents.filter(s => s !== student))}
                              className="p-1.5 text-gray-400 hover:text-red-500 rounded-lg hover:bg-red-50 transition-colors"
                            >
                              <X className="w-4 h-4" />
                            </button>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

              </div>
            </div>  {/* end 3.3 */}

            {/* 3.4 Cảnh báo */}
            <div className="p-6 border-b border-gray-100 bg-slate-50/50">
               <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-6">
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
                      <input type="number" min="1" max="31" className="w-14 px-2 py-2 text-center text-sm font-bold outline-none bg-transparent" value={alerts.system.days} onChange={e => setAlerts({...alerts, system: {...alerts.system, days: parseInt(e.target.value)||1}})} />
                    </div>
                    <span className="text-xs text-gray-600 font-medium">ngày trước, vào lúc</span>
                    <input type="time" className="px-2 py-2 border border-gray-200 rounded-lg text-sm outline-none bg-white" value={alerts.system.time} onChange={e => setAlerts({...alerts, system: {...alerts.system, time: e.target.value}})} />
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
                      <input type="number" min="1" max="31" className="w-14 px-2 py-2 text-center text-sm font-bold outline-none bg-transparent" value={alerts.email.days} onChange={e => setAlerts({...alerts, email: {...alerts.email, days: parseInt(e.target.value)||1}})} />
                    </div>
                    <span className="text-xs text-gray-600 font-medium">ngày trước, vào lúc</span>
                    <input type="time" className="px-2 py-2 border border-gray-200 rounded-lg text-sm outline-none bg-white" value={alerts.email.time} onChange={e => setAlerts({...alerts, email: {...alerts.email, time: e.target.value}})} />
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
              <textarea 
                className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm min-h-[100px] bg-slate-50/50"
                value={notes}
                onChange={e => setNotes(e.target.value)}
                placeholder="Các ghi chú khác phục vụ công tác tổ chức đoàn..."
              ></textarea>
            </div>

          </div>}
        </div>
      </div>

      {/* Action Bar within page */}
      <div className="flex justify-end gap-3 mt-8">
        <button 
          onClick={() => navigate('/dashboard/visit')}
          className="px-6 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-colors shadow-sm"
        >
          Hủy
        </button>
        <button 
          onClick={() => {
            // handle submit
            navigate('/dashboard/visit');
          }}
          className="px-8 py-2.5 rounded-xl font-bold text-white bg-[#f37021] hover:bg-orange-600 transition-all hover:-translate-y-1 hover:shadow-lg active:translate-y-0 shadow-md flex items-center gap-2"
        >
          <Plus className="w-5 h-5" />
          Tạo đoàn khách
        </button>
      </div>

    </div>
  );
}
