/**
 * Component VisitingFormPopup
 * Modal popup cho phép người dùng đăng ký hoặc chỉnh sửa thông tin biểu mẫu chuyến thăm.
 * Thu thập chi tiết số lượng khách, mục đích và ngày dự kiến.
 */

import React, { useState, useEffect, useRef } from 'react';
import { X, Plus, Trash2, Calendar, Clock, Upload, Download, Check, ChevronDown } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';

interface VisitingFormPopupProps {
  isOpen: boolean;
  onClose: () => void;
}

const InputField = ({ label, type = "text", required = false, value, onChange, placeholder }: any) => {
  const isLabelAsterisk = label.includes('*');
  const cleanLabel = label.replace('*', '');
  
  return (
    <div>
      <label className={`block text-base font-bold mb-2 ${isLabelAsterisk ? 'text-gray-900' : 'text-gray-700'}`}>
        {cleanLabel} {(isLabelAsterisk || required) && <span className="text-red-500">*</span>}
      </label>
      <input
        type={type}
        required={isLabelAsterisk || required}
        value={value}
        onChange={onChange}
        placeholder={placeholder}
        className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none transition-shadow bg-white text-sm font-medium text-gray-900 shadow-sm"
      />
    </div>
  );
};

const FormLabel = ({ children, required, subtitle }: {children: React.ReactNode, required?: boolean, subtitle?: string}) => (
  <div className="mb-2">
    <label className="block text-base font-bold text-gray-900">
      {children} {required && <span className="text-red-500">*</span>}
    </label>
    {subtitle && <p className="text-xs text-gray-500 mt-0.5">{subtitle}</p>}
  </div>
);

export function VisitingFormPopup({ isOpen, onClose }: VisitingFormPopupProps) {
  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = 'unset';
    }
    return () => {
      document.body.style.overflow = 'unset';
    };
  }, [isOpen]);

  // State
  const [registerInfo, setRegisterInfo] = useState({
    fullName: '',
    organization: '',
    jobTitle: '',
    phone: '',
    email: '',
    nationality: ''
  });

  const [dateError, setDateError] = useState('');

  const [visitors, setVisitors] = useState([{ id: '1', fullName: '', jobTitle: '', organization: '', nationality: '' }]);
  
  const [supportTeam, setSupportTeam] = useState([{ id: '1', fullName: '', jobTitle: '', organization: '', nationality: '' }]);
  const [isSupportTeamSameAsRegister, setIsSupportTeamSameAsRegister] = useState(false);

  const [contactPoint, setContactPoint] = useState({ fullName: '', organization: '', phone: '', email: '' });
  const [isContactPointSameAsRegister, setIsContactPointSameAsRegister] = useState(false);

  const [visitMode, setVisitMode] = useState<'single' | 'multiple'>('single');
  const [visits, setVisits] = useState([{ id: '1', campus: 'Hà Nội', startDatetime: '', endDatetime: '' }]);
  const campusOptions = ['Hà Nội', 'Đà Nẵng', 'Cần Thơ', 'Hồ Chí Minh', 'Quy Nhơn'];

  const handleStartDatetimeChange = (val: string, index: number) => {
    const newVisits = [...visits];
    newVisits[index].startDatetime = val;
    setVisits(newVisits);

    if (val) {
      const selectedDate = new Date(val);
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      const diffMs = selectedDate.getTime() - today.getTime();
      const diffDays = diffMs / (1000 * 60 * 60 * 24);
      if (diffDays <= 3 && diffDays >= 0) {
        setDateError('FPTU mong muốn được đón tiếp bạn chu đáo nên hãy thông báo tới chúng tôi sớm nhất khi bạn có kế hoạch.');
      } else {
        setDateError('');
      }
    } else {
      setDateError('');
    }
  };

  useEffect(() => {
    if (visitMode === 'single' && visits.length > 1) {
      setVisits([{ ...visits[0] }]);
    }
  }, [visitMode]);

  const handleSupportCheckbox = (checked: boolean) => {
    setIsSupportTeamSameAsRegister(checked);
    if (checked) {
       setSupportTeam(prev => {
          const newTeam = [...prev];
          if (newTeam.length === 0) {
             newTeam.push({ id: Date.now().toString(), fullName: registerInfo.fullName, jobTitle: registerInfo.jobTitle, organization: registerInfo.organization, nationality: registerInfo.nationality });
          } else {
             newTeam[0] = { ...newTeam[0], fullName: registerInfo.fullName, jobTitle: registerInfo.jobTitle, organization: registerInfo.organization, nationality: registerInfo.nationality };
          }
          return newTeam;
       });
    }
  };

  const handleContactCheckbox = (checked: boolean) => {
    setIsContactPointSameAsRegister(checked);
    if (checked) {
       setContactPoint({
          fullName: registerInfo.fullName,
          organization: registerInfo.organization,
          phone: registerInfo.phone,
          email: registerInfo.email
       });
    } else {
       setContactPoint({ fullName: '', organization: '', phone: '', email: '' });
    }
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="fixed inset-0 z-[100] bg-black/60 backdrop-blur-sm flex items-center justify-center p-3 sm:p-6 pb-safe"
          onClick={onClose}
        >
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ duration: 0.3, ease: 'easeOut' }}
            onClick={(e) => e.stopPropagation()}
            className="bg-white w-full max-w-5xl max-h-[92vh] rounded-2xl shadow-2xl flex flex-col overflow-hidden relative border border-gray-100"
          >
            {/* Header */}
            <div className="flex-none px-6 py-5 sm:px-10 flex flex-col sm:flex-row items-start sm:items-center justify-between text-white relative z-10 overflow-hidden bg-gradient-to-br from-[#004c91] to-[#013565]">
              {/* Decorative elements */}
              <div className="absolute top-0 right-0 w-64 h-64 bg-white/5 rounded-full -translate-y-1/2 translate-x-1/3 blur-2xl"></div>
              <div className="absolute bottom-0 left-0 w-40 h-40 bg-[#f37021]/20 rounded-full translate-y-1/2 -translate-x-1/4 blur-xl"></div>
              
              <div className="relative z-10 pr-8">
                <div className="inline-flex items-center gap-2 px-2.5 py-1 bg-white/10 text-orange-200 rounded-full text-[10px] font-bold uppercase tracking-wider mb-2">
                  <span className="w-1.5 h-1.5 bg-[#f37021] rounded-full animate-pulse"></span>
                  Campus Visit
                </div>
                <h2 className="text-xl sm:text-2xl font-black tracking-tight mb-1">ĐĂNG KÝ THAM QUAN TRƯỜNG</h2>
                <p className="text-blue-100/90 font-medium text-xs sm:text-sm max-w-2xl">
                  Vui lòng điền đầy đủ thông tin dưới đây để đăng ký lịch trình tham quan.
                </p>
              </div>
              <button 
                onClick={onClose}
                className="absolute top-4 right-4 sm:top-5 sm:right-6 p-2 text-white/70 hover:text-white hover:bg-white/20 rounded-full transition-all flex-shrink-0 z-20"
              >
                <X className="w-5 h-5 sm:w-6 sm:h-6" />
              </button>
            </div>

            {/* Form Body - Scrollable */}
            <div className="flex-1 overflow-y-auto px-4 sm:px-10 py-8 bg-white custom-scrollbar">
              <form className="space-y-12" onSubmit={(e) => e.preventDefault()}>
                
                {/* 1. Thông tin người đăng ký */}
                <section>
                  <h3 className="text-lg sm:text-xl font-black text-[#004c91] border-b-2 border-[#f37021]/30 pb-2 mb-6 flex items-center gap-2 w-max pr-6">
                    <span className="flex items-center justify-center w-6 h-6 sm:w-7 sm:h-7 rounded-full bg-[#f37021] text-white text-sm">1</span>
                    THÔNG TIN NGƯỜI ĐĂNG KÝ
                  </h3>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-6">
                    <InputField label="Họ và tên*" value={registerInfo.fullName} onChange={(e: any) => setRegisterInfo({...registerInfo, fullName: e.target.value})} required />
                    <div>
                      <label className="block text-base font-bold text-gray-900 mb-2">
                        Quốc Tịch <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="text"
                        required
                        value={registerInfo.nationality}
                        onChange={(e: any) => setRegisterInfo({...registerInfo, nationality: e.target.value})}
                        className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none transition-shadow bg-white text-sm font-medium text-gray-900 shadow-sm"
                      />
                    </div>
                    <InputField label="Đơn vị công tác*" value={registerInfo.organization} onChange={(e: any) => setRegisterInfo({...registerInfo, organization: e.target.value})} required />
                    <InputField label="Chức danh, phòng ban*" value={registerInfo.jobTitle} onChange={(e: any) => setRegisterInfo({...registerInfo, jobTitle: e.target.value})} required />
                    <InputField label="SĐT*" type="tel" value={registerInfo.phone} onChange={(e: any) => setRegisterInfo({...registerInfo, phone: e.target.value})} required />
                    <InputField label="Email*" type="email" value={registerInfo.email} onChange={(e: any) => setRegisterInfo({...registerInfo, email: e.target.value})} required />
                  </div>
                </section>

                {/* 2. Thông tin đoàn khách */}
                <section>
                  <h3 className="text-lg sm:text-xl font-black text-[#004c91] border-b-2 border-[#f37021]/30 pb-2 mb-6 flex items-center gap-2 w-max pr-6">
                    <span className="flex items-center justify-center w-6 h-6 sm:w-7 sm:h-7 rounded-full bg-[#f37021] text-white text-sm">2</span>
                    THÔNG TIN ĐOÀN KHÁCH
                  </h3>
                  
                  <div className="space-y-8">
                    {/* Khối 1: Thông tin chuyến thăm */}
                    <div className="bg-slate-50/50 rounded-2xl border-l-4 border-l-[#f37021] border border-gray-100 p-5 sm:p-7 shadow-sm">
                      <h4 className="text-gray-800 font-bold text-base mb-5 border-b border-gray-200 pb-2 uppercase tracking-wide">I. Thông tin chuyến thăm</h4>
                      <div className="space-y-6">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                          {/* Tên đoàn */}
                          <div>
                            <InputField label="Tên đoàn khách*" required />
                          </div>
                          {/* Cơ sở */}
                          <div className="relative">
                            <FormLabel required>Cơ sở muốn tới thăm</FormLabel>
                            <div className="relative">
                              <select
                                value={visitMode}
                                onChange={(e) => setVisitMode(e.target.value as 'single' | 'multiple')}
                                className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none bg-white text-sm font-medium text-gray-900 shadow-sm appearance-none"
                              >
                                <option value="single">Chỉ một cơ sở</option>
                                <option value="multiple">Liên cơ sở</option>
                              </select>
                              <ChevronDown className="w-4 h-4 text-gray-500 absolute right-4 top-1/2 -translate-y-1/2 pointer-events-none" />
                            </div>
                          </div>
                        </div>

                        {/* Thời gian */}
                        <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm relative">
                          <FormLabel required>Thời gian dự kiến thăm FPTU</FormLabel>
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
                                      className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm font-medium bg-white shadow-sm appearance-none pr-8"
                                    >
                                      {campusOptions.map(c => <option key={c} value={c}>{c}</option>)}
                                    </select>
                                    <ChevronDown className="absolute right-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
                                  </div>
                                </div>
                                {/* Thời Gian Bắt đầu */}
                                <div className="flex-[1.5] w-full xl:w-auto relative">
                                  {index === 0 && <label className="block text-xs font-bold text-gray-600 mb-1 uppercase tracking-wider">Thời Gian Bắt đầu</label>}
                                  <div className="relative">
                                    <input
                                      type="datetime-local"
                                      value={visit.startDatetime}
                                      onChange={(e) => handleStartDatetimeChange(e.target.value, index)}
                                      className="w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm font-medium bg-white shadow-sm"
                                      required
                                    />
                                    <Calendar className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91]" />
                                  </div>
                                </div>
                                {/* Thời Gian Kết thúc */}
                                <div className="flex-[1.5] w-full xl:w-auto relative">
                                  {index === 0 && <label className="block text-xs font-bold text-gray-600 mb-1 uppercase tracking-wider">Thời Gian Kết thúc</label>}
                                  <div className="relative">
                                    <input
                                      type="datetime-local"
                                      value={visit.endDatetime}
                                      onChange={(e) => {
                                        const newV = [...visits]; newV[index].endDatetime = e.target.value; setVisits(newV);
                                      }}
                                      className="w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none text-sm font-medium bg-white shadow-sm"
                                      required
                                    />
                                    <Clock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91]" />
                                  </div>
                                </div>
                                {/* Giờ Việt Nam */}
                                <div className="flex-[0.8] w-full xl:w-auto flex items-center justify-center h-[44px] px-3 bg-gray-50 rounded-xl border border-gray-200 select-none cursor-default">
                                  <span className="text-[#004c91] text-sm font-bold whitespace-nowrap">VN (GMT+7)</span>
                                </div>
                              </div>
                            ))}
                          </div>

                          {visitMode === 'multiple' && (
                            <button
                              type="button"
                              onClick={() => setVisits([...visits, { id: Date.now().toString(), campus: 'Hà Nội', startDatetime: '', endDatetime: '' }])}
                              className="w-full mt-4 flex items-center justify-center gap-2 py-2.5 border-2 border-dashed border-[#f37021]/30 hover:border-[#f37021] text-[#f37021] rounded-xl text-sm font-bold transition-colors bg-orange-50/50 hover:bg-orange-50"
                            >
                              <Plus className="w-4 h-4" /> Thêm cơ sở
                            </button>
                          )}

                          {dateError && (
                            <motion.p 
                              initial={{ opacity: 0, height: 0 }}
                              animate={{ opacity: 1, height: 'auto' }}
                              className="text-red-600 text-sm mt-3 font-semibold bg-red-50 p-2.5 rounded-lg border border-red-100 flex items-start gap-2"
                            >
                              <span className="text-lg leading-none">⚠️</span>
                              {dateError}
                            </motion.p>
                          )}
                        </div>

                        <div className="space-y-6">
                          <div>
                            <FormLabel required>Mục đích thăm FPTU</FormLabel>
                            <textarea 
                              rows={3} 
                              placeholder="Nhập mục đích chuyến thăm..."
                              className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none transition-shadow bg-white text-sm shadow-sm resize-none font-medium text-gray-900"
                              required
                            ></textarea>
                          </div>
                          <div>
                            <FormLabel required>Nội dung làm việc tại FPTU</FormLabel>
                            <textarea 
                              rows={3} 
                              placeholder="Nhập nội dung làm việc cụ thể..."
                              className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none transition-shadow bg-white text-sm shadow-sm resize-none font-medium text-gray-900"
                              required
                            ></textarea>
                          </div>
                        </div>
                      </div>
                    </div>

                    {/* Khối 2: Thành phần tham dự & Liên hệ */}
                    <div className="bg-blue-50/20 rounded-2xl border-l-4 border-l-[#004c91] border border-gray-100 p-5 sm:p-7 shadow-sm mt-8">
                      <h4 className="text-[#004c91] font-bold text-base mb-5 border-b border-blue-100 pb-2 uppercase tracking-wide">II. Thành phần tham dự & Liên hệ</h4>
                      <div className="space-y-8">
                        {/* Danh sách khách */}
                        <div>
                          <FormLabel required>Danh sách khách</FormLabel>
                      <div className="bg-white border border-gray-200 rounded-xl overflow-x-auto shadow-sm">
                        <table className="w-full min-w-[750px] border-collapse text-sm">
                          <thead className="bg-slate-50 border-b border-gray-200">
                            <tr>
                              <th className="p-3 text-center font-bold text-slate-700 w-14">STT</th>
                              <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Họ và tên</th>
                              <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Chức vụ</th>
                              <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Đơn vị công tác</th>
                              <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Quốc tịch</th>
                              <th className="p-3 text-center w-14 border-l border-gray-200"></th>
                            </tr>
                          </thead>
                          <tbody>
                            <AnimatePresence>
                              {visitors.map((v, i) => (
                                  <motion.tr 
                                    initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, height: 0 }}
                                    key={v.id} 
                                    className="border-b border-gray-100 last:border-b-0 hover:bg-orange-50/50 focus-within:bg-orange-50 transition-colors"
                                  >
                                    <td className="p-3 text-center font-bold text-slate-400">{i + 1}</td>
                                    <td className="p-0 border-l border-gray-100"><input required placeholder="Nhập tên..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={v.fullName} onChange={e => {
                                      const nv = [...visitors]; nv[i].fullName = e.target.value; setVisitors(nv);
                                    }}/></td>
                                    <td className="p-0 border-l border-gray-100"><input required placeholder="Nhập chức vụ..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={v.jobTitle} onChange={e => {
                                      const nv = [...visitors]; nv[i].jobTitle = e.target.value; setVisitors(nv);
                                    }}/></td>
                                    <td className="p-0 border-l border-gray-100"><input required placeholder="Nhập đơn vị..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={v.organization} onChange={e => {
                                      const nv = [...visitors]; nv[i].organization = e.target.value; setVisitors(nv);
                                    }}/></td>
                                    <td className="p-0 border-l border-gray-100"><input required placeholder="Nhập quốc tịch..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={v.nationality} onChange={e => {
                                      const nv = [...visitors]; nv[i].nationality = e.target.value; setVisitors(nv);
                                    }}/></td>
                                    <td className="p-2 border-l border-gray-100 text-center">
                                      <button disabled={visitors.length === 1} onClick={() => setVisitors(visitors.filter(vi => vi.id !== v.id))} className="p-2 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors disabled:opacity-30 disabled:hover:bg-transparent" type="button">
                                        <Trash2 className="w-4 h-4"/>
                                      </button>
                                    </td>
                                  </motion.tr>
                              ))}
                            </AnimatePresence>
                          </tbody>
                        </table>
                      </div>
                      <div className="flex flex-wrap items-center justify-between gap-3 mt-4">
                        <button type="button" onClick={() => setVisitors([...visitors, {id: Date.now().toString(), fullName: '', jobTitle: '', organization: '', nationality: ''}])} className="inline-flex items-center gap-2 px-4 py-2 bg-[#f37021]/10 text-[#f37021] text-sm font-bold rounded-xl hover:bg-[#f37021]/20 transition-colors">
                          <Plus className="w-4 h-4" /> Thêm khách
                        </button>
                        <div className="flex flex-wrap gap-2 sm:gap-3">
                            <button type="button" className="inline-flex items-center gap-2 px-4 py-2 bg-white text-slate-700 text-sm font-bold rounded-xl hover:bg-slate-50 transition-colors border border-slate-200 shadow-sm">
                              <Download className="w-4 h-4" /> Tải mẫu
                            </button>
                            <button type="button" className="inline-flex items-center gap-2 px-4 py-2 bg-white text-[#004c91] text-sm font-bold rounded-xl hover:bg-blue-50 transition-colors border border-slate-200 shadow-sm">
                              <Upload className="w-4 h-4" /> Up danh sách
                            </button>
                        </div>
                      </div>
                    </div>

                        {/* Danh sách team hỗ trợ khách */}
                        <div>
                          <FormLabel required>Danh sách team hỗ trợ khách</FormLabel>
                      <div className="mb-4 flex items-center">
                        <label className="flex items-center gap-2.5 cursor-pointer text-sm text-[#004c91] font-bold select-none bg-blue-50/50 px-3 py-1.5 rounded-lg border border-blue-100 hover:bg-blue-50 transition-colors">
                          <input type="checkbox" checked={isSupportTeamSameAsRegister} onChange={e => handleSupportCheckbox(e.target.checked)} className="w-4 h-4 rounded text-[#004c91] focus:ring-[#004c91] border-blue-300 cursor-pointer" />
                          Tôi là người hỗ trợ khách
                        </label>
                      </div>
                      <div className="bg-white border border-gray-200 rounded-xl overflow-x-auto shadow-sm">
                        <table className="w-full min-w-[750px] border-collapse text-sm">
                          <thead className="bg-slate-50 border-b border-gray-200">
                            <tr>
                              <th className="p-3 text-center font-bold text-slate-700 w-14">STT</th>
                              <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Họ và tên</th>
                              <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Chức vụ</th>
                              <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Đơn vị công tác</th>
                              <th className="p-3 text-left font-bold text-slate-700 border-l border-gray-200">Quốc tịch</th>
                              <th className="p-3 text-center w-14 border-l border-gray-200"></th>
                            </tr>
                          </thead>
                          <tbody>
                            <AnimatePresence>
                              {supportTeam.map((v, i) => (
                                  <motion.tr 
                                    initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, height: 0 }}
                                    key={v.id} 
                                    className="border-b border-gray-100 last:border-b-0 hover:bg-orange-50/50 focus-within:bg-orange-50 transition-colors"
                                  >
                                    <td className="p-3 text-center font-bold text-slate-400">{i + 1}</td>
                                    <td className="p-0 border-l border-gray-100"><input required placeholder="Nhập tên..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={v.fullName} onChange={e => {
                                      const nv = [...supportTeam]; nv[i].fullName = e.target.value; setSupportTeam(nv);
                                    }}/></td>
                                    <td className="p-0 border-l border-gray-100"><input required placeholder="Nhập chức vụ..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={v.jobTitle} onChange={e => {
                                      const nv = [...supportTeam]; nv[i].jobTitle = e.target.value; setSupportTeam(nv);
                                    }}/></td>
                                    <td className="p-0 border-l border-gray-100"><input required placeholder="Nhập đơn vị..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={v.organization} onChange={e => {
                                      const nv = [...supportTeam]; nv[i].organization = e.target.value; setSupportTeam(nv);
                                    }}/></td>
                                    <td className="p-0 border-l border-gray-100"><input required placeholder="Nhập quốc tịch..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={v.nationality} onChange={e => {
                                      const nv = [...supportTeam]; nv[i].nationality = e.target.value; setSupportTeam(nv);
                                    }}/></td>
                                    <td className="p-2 border-l border-gray-100 text-center">
                                      <button disabled={supportTeam.length === 1} onClick={() => setSupportTeam(supportTeam.filter(vi => vi.id !== v.id))} className="p-2 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors disabled:opacity-30 disabled:hover:bg-transparent" type="button">
                                        <Trash2 className="w-4 h-4"/>
                                      </button>
                                    </td>
                                  </motion.tr>
                              ))}
                            </AnimatePresence>
                          </tbody>
                        </table>
                      </div>
                      <div className="flex flex-wrap items-center justify-between gap-3 mt-4">
                        <button type="button" onClick={() => setSupportTeam([...supportTeam, {id: Date.now().toString(), fullName: '', jobTitle: '', organization: '', nationality: ''}])} className="inline-flex items-center gap-2 px-4 py-2 bg-[#f37021]/10 text-[#f37021] text-sm font-bold rounded-xl hover:bg-[#f37021]/20 transition-colors">
                          <Plus className="w-4 h-4" /> Thêm nhân sự
                        </button>
                        <div className="flex flex-wrap gap-2 sm:gap-3">
                            <button type="button" className="inline-flex items-center gap-2 px-4 py-2 bg-white text-slate-700 text-sm font-bold rounded-xl hover:bg-slate-50 transition-colors border border-slate-200 shadow-sm">
                              <Download className="w-4 h-4" /> Tải mẫu
                            </button>
                            <button type="button" className="inline-flex items-center gap-2 px-4 py-2 bg-white text-[#004c91] text-sm font-bold rounded-xl hover:bg-blue-50 transition-colors border border-slate-200 shadow-sm">
                              <Upload className="w-4 h-4" /> Up danh sách
                            </button>
                        </div>
                      </div>
                    </div>

                        {/* Thông tin đầu mối liên hệ */}
                        <div>
                          <FormLabel required>Thông tin đầu mối liên hệ</FormLabel>
                      <div className="mb-4 flex items-center">
                        <label className="flex items-center gap-2.5 cursor-pointer text-sm text-[#004c91] font-bold select-none bg-blue-50/80 px-3 py-1.5 rounded-lg border border-blue-200 hover:bg-blue-100 transition-colors">
                          <input type="checkbox" checked={isContactPointSameAsRegister} onChange={e => handleContactCheckbox(e.target.checked)} className="w-4 h-4 rounded text-[#004c91] focus:ring-[#004c91] border-blue-300 cursor-pointer" />
                          Tôi là đầu mối liên hệ
                        </label>
                      </div>
                      <div className="bg-white border border-gray-200 rounded-xl overflow-x-auto shadow-sm">
                        <table className="w-full min-w-[700px] border-collapse text-sm">
                          <thead className="bg-[#004c91]/5 border-b border-gray-200">
                            <tr>
                              <th className="p-3 text-left font-bold text-[#004c91]">Họ và tên</th>
                              <th className="p-3 text-left font-bold text-[#004c91] border-l border-gray-200">Đơn vị công tác</th>
                              <th className="p-3 text-left font-bold text-[#004c91] border-l border-gray-200">Số điện thoại</th>
                              <th className="p-3 text-left font-bold text-[#004c91] border-l border-gray-200">Email</th>
                            </tr>
                          </thead>
                          <tbody>
                              <tr className="hover:bg-orange-50/50 focus-within:bg-orange-50 transition-colors">
                                <td className="p-0"><input required placeholder="Nhập tên..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={contactPoint.fullName} onChange={e => setContactPoint({...contactPoint, fullName: e.target.value})}/></td>
                                <td className="p-0 border-l border-gray-100"><input required placeholder="Nhập đơn vị..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={contactPoint.organization} onChange={e => setContactPoint({...contactPoint, organization: e.target.value})}/></td>
                                <td className="p-0 border-l border-gray-100"><input required type="tel" placeholder="Nhập SĐT..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={contactPoint.phone} onChange={e => setContactPoint({...contactPoint, phone: e.target.value})}/></td>
                                <td className="p-0 border-l border-gray-100"><input required type="email" placeholder="Nhập Email..." className="w-full p-3 bg-transparent outline-none font-medium placeholder:text-gray-300" value={contactPoint.email} onChange={e => setContactPoint({...contactPoint, email: e.target.value})}/></td>
                              </tr>
                          </tbody>
                        </table>
                      </div>
                        </div>
                      </div>
                    </div>

                    {/* Khối 3: Yêu cầu bổ sung */}
                    <div className="bg-slate-50/50 rounded-2xl border-l-4 border-l-[#f37021] border border-gray-100 p-5 sm:p-7 shadow-sm mt-8">
                      <h4 className="text-gray-800 font-bold text-base mb-5 border-b border-gray-200 pb-2 uppercase tracking-wide">III. Yêu cầu bổ sung</h4>

                      {/* Hàng 1: Ngôn ngữ sử dụng & Nhận diện phương tiện */}
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                        {/* Ngôn ngữ sử dụng */}
                        <div>
                          <FormLabel required>Ngôn ngữ sử dụng</FormLabel>
                          <div className="flex items-center gap-8 mt-2 mb-3">
                            <label className="flex items-center gap-2.5 cursor-pointer group">
                              <input type="radio" name="language" value="tienganh" required className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer" />
                              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">Tiếng Anh</span>
                            </label>
                            <label className="flex items-center gap-2.5 cursor-pointer group">
                              <input type="radio" name="language" value="tiengviet" required className="w-5 h-5 text-[#f37021] border-gray-300 focus:ring-[#f37021] cursor-pointer" />
                              <span className="text-gray-800 font-bold text-sm group-hover:text-[#004c91] transition-colors">Tiếng Việt</span>
                            </label>
                          </div>
                          <div className="bg-slate-50 border border-slate-100 p-3 rounded-xl">
                            <p className="text-xs text-slate-500 italic leading-relaxed">
                              <span className="font-bold text-slate-600 not-italic mr-1">Note:</span>
                              Hiện tại FPTU chỉ có thể hỗ trợ bằng 2 ngôn ngữ tiếng Anh và tiếng Việt. Với lựa chọn khác ngoài tiếng Anh hoặc tiếng Việt, đầu mối gửi visit request sẽ cần chủ động bố trí phiên dịch viên (nếu cần).
                            </p>
                          </div>
                        </div>

                        {/* Nhận diện phương tiện di chuyển tới FPTU */}
                        <div>
                          <InputField label="Nhận diện phương tiện di chuyển tới FPTU" placeholder="Ví dụ: Xe khách 45 chỗ, biển số 29A-XXXXX..." />
                          <div className="text-xs text-slate-500 bg-slate-50 p-3 rounded-xl border border-slate-100 mt-2">
                            <ul className="list-none space-y-2 italic">
                              <li className="flex gap-2 items-start">
                                <span className="text-[#004c91] font-bold not-italic">∗</span>
                                Các phương tiện cá nhân không được di chuyển trong khuôn viên trường nếu chưa được cho phép.
                              </li>
                              <li className="flex gap-2 items-start">
                                <span className="text-[#004c91] font-bold not-italic">∗</span>
                                Với các đoàn khách di chuyển từ FSO và có số lượng khách đông (6 người trở lên), đầu mối phụ trách chủ động yêu cầu xe điện từ FSO qua FPTU và cả trong quá trình campus tour.
                              </li>
                            </ul>
                          </div>
                        </div>
                      </div>

                      {/* Hàng 2: Ghi chú cho FPTU - full width */}
                      <div className="mt-8">
                        <label className="block text-base font-bold text-gray-900 mb-2">Ghi chú cho FPTU</label>
                        <textarea
                          rows={5}
                          placeholder="Nhập bất kỳ ghi chú thiết yếu nào..."
                          className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021] outline-none transition-shadow bg-white text-sm shadow-sm resize-none font-medium text-gray-900"
                        ></textarea>
                      </div>

                    </div>

                  </div>
                </section>
              </form>
            </div>

            {/* Footer */}
            <div className="flex-none py-3 px-5 sm:py-4 sm:px-6 bg-white border-t border-gray-100 flex justify-between items-center sm:justify-end gap-3 rounded-b-2xl shadow-[0_-4px_20px_rgba(0,0,0,0.02)] z-20">
              <button
                type="button"
                onClick={onClose}
                className="px-6 py-3 rounded-xl font-bold text-gray-600 bg-white border-2 border-gray-200 hover:bg-gray-50 hover:text-gray-900 transition-colors w-full sm:w-auto text-center"
              >
                Hủy
              </button>
              <button
                type="submit"
                onClick={onClose}
                className="px-8 py-3 rounded-xl font-black tracking-wide text-white bg-gradient-to-r from-[#f37021] to-[#e06111] hover:from-[#e06111] hover:to-[#c4530c] shadow-lg shadow-orange-500/30 transition-all transform hover:-translate-y-0.5 w-full sm:w-auto text-center"
              >
                Gửi đơn
              </button>
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
