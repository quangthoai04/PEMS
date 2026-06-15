/**
 * Trang MinuteManagement
 * Quản lý và cung cấp biên bản tài liệu cho cuộc họp chuyến thăm.
 */

import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, Plus, Filter, FileText, Eye, MoreVertical, ArrowDown, ChevronLeft, ChevronRight, X, Calendar, Users, Square, CheckSquare, Building2, Check, AlertCircle } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';

interface Minute {
  id: string;
  name: string;
  uploadDate: string;
  guestName: string;
}

const mockMinutes: Minute[] = [
  {
    id: '1',
    name: 'Biên bản cuộc họp trao đổi hợp tác',
    uploadDate: '15/06/2026',
    guestName: 'Đoàn khách Đại học Quốc gia Hà Nội'
  },
  {
    id: '2',
    name: 'Biên bản thỏa thuận MOU',
    uploadDate: '10/06/2026',
    guestName: 'Tập đoàn Vingroup'
  },
  {
    id: '3',
    name: 'Biên bản tham quan phân luồng',
    uploadDate: '01/06/2026',
    guestName: 'Đại học Swinburne'
  },
  {
    id: '4',
    name: 'Biên bản ghi nhớ hợp tác công nghệ',
    uploadDate: '25/05/2026',
    guestName: 'Tập đoàn FPT'
  },
  {
    id: '5',
    name: 'Biên bản làm việc chi tiết',
    uploadDate: '10/05/2026',
    guestName: 'Khách sạn Mường Thanh'
  }
];

export function MinuteManagement() {
  const navigate = useNavigate();
  const [searchQuery, setSearchQuery] = useState('');
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>('desc');
  const [selectedMinute, setSelectedMinute] = useState<Minute | null>(null);

  const mockParticipants = [
    { id: 1, name: 'Nguyễn Văn A', role: 'Đại diện FPT HN', org: 'Trường Đại học FPT', isInternal: true, confirmed: true },
    { id: 2, name: 'Trần Thị B', role: 'Trưởng đoàn', org: 'Đại học Quốc gia', isInternal: false, isPartner: true, confirmed: true },
    { id: 3, name: 'Lê Văn C', role: 'Giảng viên', org: 'Đại học Quốc gia', isInternal: false, isPartner: false, confirmed: false },
  ];

  const filteredMinutes = [...mockMinutes]
    .filter(doc => doc.name.toLowerCase().includes(searchQuery.toLowerCase()) || doc.guestName.toLowerCase().includes(searchQuery.toLowerCase()))
    .sort((a, b) => {
      // Simplified sort for mock dates: DD/MM/YYYY
      const formatTime = (dateStr: string) => {
        const [day, month, year] = dateStr.split('/');
        return new Date(`${year}-${month}-${day}`).getTime();
      };
      
      const timeA = formatTime(a.uploadDate);
      const timeB = formatTime(b.uploadDate);
      
      if (sortOrder === 'asc') return timeA - timeB;
      return timeB - timeA;
    });

  const handleSortToggle = () => {
    setSortOrder(prev => prev === 'desc' ? 'asc' : 'desc');
  };

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto flex flex-col space-y-6 pb-12 animate-in fade-in duration-300">
      {/* 1. Header & Navigation Layer */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Quản lý biên bản</span>
      </div>
      
      <div className="border-b border-gray-100 pb-4 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91]">Quản lý biên bản</h1>
          <p className="text-gray-500 mt-2 font-medium">Lưu trữ biên bản họp của các đoàn khách</p>
        </div>
      </div>

      {/* 2. Action & Filter Controller */}
      <div className="w-full">
        <div className="flex flex-col lg:flex-row items-center gap-4">
          <div className="relative flex-1 w-full group">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 w-5 h-5 group-focus-within:text-[#004c91] transition-colors pointer-events-none" />
            <input 
              type="text" 
              placeholder="Tìm kiếm theo tên biên bản, tên đoàn khách..." 
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-11 pr-4 py-3 border border-slate-200 rounded-xl bg-white text-sm font-medium text-slate-800 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] transition-all placeholder:text-slate-400 shadow-sm"
            />
          </div>
        </div>
      </div>

      {/* 3. Data View */}
      <div className="bg-white rounded-3xl shadow-sm border border-slate-200 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[1000px]">
            <thead>
              <tr className="bg-[#004c91] border-b border-[#004c91]">
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider whitespace-nowrap">Tên biên bản</th>
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider whitespace-nowrap">ĐOÀN KHÁCH</th>
                <th 
                  className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider cursor-pointer group hover:bg-[#00386b] transition-colors whitespace-nowrap text-center"
                  onClick={handleSortToggle}
                >
                  <div className="flex items-center justify-center gap-1">
                    Thời gian
                    <ArrowDown className={`w-4 h-4 text-white/70 group-hover:text-white transition-transform duration-300 ${sortOrder === 'asc' ? 'rotate-180' : ''}`} />
                  </div>
                </th>
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider text-center whitespace-nowrap">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filteredMinutes.length > 0 ? filteredMinutes.map((doc, index) => (
                <motion.tr 
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: index * 0.05 }}
                  key={doc.id} 
                  className="transition-colors duration-200 hover:bg-slate-50/50 group"
                >
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-lg bg-[#004c91]/5 border border-[#004c91]/10 flex items-center justify-center shrink-0">
                        <FileText className="w-5 h-5 text-[#004c91]" />
                      </div>
                      <span className="text-[15px] font-medium text-slate-700 truncate max-w-[200px] md:max-w-[350px] group-hover:text-[#004c91] transition-colors cursor-pointer" title={doc.name}>{doc.name}</span>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    <span className="inline-flex px-3 py-1 text-xs font-medium rounded-md border bg-slate-50 text-slate-600 border-slate-200">
                      {doc.guestName}
                    </span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-center">
                    <span className="text-sm font-medium text-slate-600">
                      {doc.uploadDate}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex items-center justify-center">
                      <button 
                        onClick={() => setSelectedMinute(doc)}
                        className="p-2 text-gray-400 hover:text-[#004c91] hover:bg-blue-50 rounded-lg transition-colors outline-none cursor-pointer" 
                        title="Xem chi tiết"
                      >
                        <Eye className="w-5 h-5" />
                      </button>
                    </div>
                  </td>
                </motion.tr>
              )) : (
                <tr>
                  <td colSpan={4} className="px-6 py-12 text-center">
                     <div className="flex flex-col items-center justify-center">
                        <div className="w-16 h-16 bg-slate-50 rounded-full flex items-center justify-center mb-4">
                          <Search className="w-8 h-8 text-slate-300" />
                        </div>
                        <p className="text-slate-500 font-medium font-sans">Không tìm thấy biên bản phù hợp.</p>
                     </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        
        {/* Pagination */}
        {filteredMinutes.length > 0 && (
          <div className="px-6 py-4 border-t border-slate-200 bg-slate-50/50 flex flex-col sm:flex-row items-center justify-between gap-4">
            <div className="flex items-center gap-2 text-sm text-gray-600">
              <span>Hiển thị</span>
              <select className="border border-gray-200 rounded-lg px-2 py-1 bg-white focus:outline-none focus:ring-1 focus:ring-[#004c91] font-medium">
                <option value={5}>5</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
              </select>
              <span>bản ghi / trang</span>
            </div>
            <div className="flex items-center gap-2">
              <button disabled className="p-1.5 border border-slate-200 rounded-lg text-slate-400 bg-white cursor-not-allowed">
                <ChevronLeft className="w-5 h-5" />
              </button>
              <button className="w-8 h-8 flex items-center justify-center border border-[#004c91] rounded-lg text-sm font-bold text-white bg-[#2b5a8c] transition-colors outline-none cursor-pointer">
                1
              </button>
              <button disabled className="p-1.5 border border-slate-200 rounded-lg text-slate-400 bg-white cursor-not-allowed">
                <ChevronRight className="w-5 h-5" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Modal */}
      <AnimatePresence>
        {selectedMinute && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm px-4"
          >
            <motion.div 
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="bg-white rounded-2xl shadow-xl w-full max-w-4xl max-h-[90vh] overflow-hidden flex flex-col m-auto relative"
            >
              {/* Header */}
              <div className="bg-[#004c91] px-6 py-4 flex items-center justify-between border-b border-[#003366] shrink-0 sticky top-0 z-10">
                 <h2 className="text-xl font-bold text-white flex items-center gap-2">
                    <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm">
                      <FileText className="w-4 h-4 text-white" />
                    </span>
                    Chi tiết biên bản cuộc họp
                 </h2>
                 <button onClick={() => setSelectedMinute(null)} className="text-white hover:bg-white/20 p-1.5 rounded-full transition-colors cursor-pointer outline-none">
                    <X className="w-5 h-5" />
                 </button>
              </div>

              {/* Body */}
              <div className="p-6 md:p-8 overflow-y-auto w-full flex-1 min-h-0 bg-gray-50/30">
                 <fieldset disabled className="contents">
                    <div className="flex flex-col sm:flex-row sm:items-start gap-6 mb-8">
                        <div className="flex-1 w-full max-w-[450px]">
                           <label className="block text-sm font-bold text-gray-700 mb-2 ml-1">Tên biên bản</label>
                           <input 
                             type="text" 
                             value={selectedMinute.name}
                             className="bg-blue-50/50 text-blue-900 px-4 py-2.5 rounded-xl font-bold border border-blue-100 outline-none w-full opacity-90 cursor-not-allowed"
                             readOnly
                           />
                        </div>
                        <div>
                           <label className="block text-sm font-bold text-gray-700 mb-2 ml-1">Thời gian</label>
                           <div className="bg-blue-50/50 text-blue-900 px-4 py-2.5 rounded-xl font-bold flex items-center gap-2 border border-blue-100 opacity-90 cursor-not-allowed">
                              <Calendar className="w-5 h-5 text-blue-600 shrink-0" />
                              <input 
                                type="text" 
                                value={selectedMinute.uploadDate}
                                className="bg-transparent border-none outline-none font-bold text-blue-900 w-full cursor-not-allowed"
                                readOnly
                              />
                           </div>
                        </div>
                    </div>

                    <div className="mb-8 overflow-hidden rounded-xl border border-gray-200 bg-white">
                        <div className="bg-gray-50/70 px-5 py-3 border-b border-gray-200 flex items-center justify-between opacity-90">
                          <h3 className="text-sm font-bold text-gray-800 flex items-center gap-2 font-sans">
                            <Users className="w-4 h-4 text-[#004c91]" />
                            Bảng danh sách chi tiết người tham gia cuộc họp
                          </h3>
                          <span className="text-xs bg-[#004c91]/10 text-[#004c91] px-2.5 py-1 rounded-full font-bold">
                            {mockParticipants.length} thành viên
                          </span>
                        </div>
                        
                        <div className="overflow-x-auto max-h-[250px] overflow-y-auto">
                          <table className="w-full text-left border-collapse text-sm">
                            <thead className="sticky top-0 bg-white z-10 shadow-[0_1px_0_rgba(229,231,235,1)]">
                              <tr className="border-b border-gray-200 bg-gray-100/50 text-[11px] uppercase tracking-wider text-gray-500 font-extrabold font-sans">
                                <th className="px-4 py-3 text-center w-20">Có mặt</th>
                                <th className="px-5 py-3">Đại biểu tham gia</th>
                                <th className="px-5 py-3">Đơn vị của khách</th>
                              </tr>
                            </thead>
                            <tbody className="divide-y divide-gray-100">
                              {mockParticipants.map((p) => (
                                <tr key={p.id} className="hover:bg-gray-50/55 transition-colors">
                                  <td className="px-4 py-4 text-center">
                                    <div className="inline-flex items-center justify-center opacity-70">
                                      {p.confirmed ? (
                                        <CheckSquare className="w-5 h-5 text-green-600 fill-green-50" />
                                      ) : (
                                        <Square className="w-5 h-5 text-gray-300" />
                                      )}
                                    </div>
                                  </td>
                                  <td className="px-5 py-4 font-sans">
                                    <div className="font-semibold text-gray-900">{p.name}</div>
                                    <div className="text-xs text-gray-500 font-medium">{p.role}</div>
                                  </td>
                                  <td className="px-5 py-4 font-sans">
                                    <div className="flex items-center gap-1.5 text-gray-700 font-medium font-sans">
                                      <Building2 className="w-4 h-4 text-gray-400 shrink-0" />
                                      {p.org}
                                    </div>
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                    </div>

                    <div className="space-y-6">
                        <div>
                          <h3 className="text-base font-bold text-gray-800 mb-3 ml-2 relative before:content-[''] before:absolute before:left-[-12px] before:top-[6px] before:w-1.5 before:h-1.5 before:bg-[#f37021] before:rounded-full">Ghi chú</h3>
                          <textarea 
                            className="w-full bg-gray-50/50 border border-gray-200 rounded-xl p-4 text-sm font-medium text-gray-800 min-h-[120px] outline-none resize-none cursor-not-allowed opacity-90"
                            value="Đây là nội dung ghi chú mock cho biên bản cuộc họp."
                            readOnly
                          />
                        </div>

                        <div>
                          <h3 className="text-base font-bold text-gray-800 mb-3 ml-2 relative before:content-[''] before:absolute before:left-[-12px] before:top-[6px] before:w-1.5 before:h-1.5 before:bg-[#004c91] before:rounded-full">Đầu mục công việc</h3>
                          <div className="space-y-3 bg-gray-50/50 border border-gray-100 rounded-xl p-4 opacity-90 cursor-not-allowed">
                              <div className="flex flex-col sm:flex-row sm:items-center gap-3 bg-white/50 p-3 rounded-lg border border-gray-200 shadow-sm pointer-events-none">
                                <div className="flex items-center gap-3 flex-1">
                                  <div className="text-[#004c91]">
                                    <CheckSquare className="w-5 h-5 text-green-600" />
                                  </div>
                                  <span className="flex-1 text-sm font-medium line-through text-gray-400">Gửi mail cảm ơn đối tác</span>
                                </div>
                                <div className="flex items-center gap-2">
                                   <Calendar className="w-4 h-4 text-orange-500" />
                                   <span className="text-xs font-bold text-orange-700 bg-orange-50/50 px-2 py-1.5 rounded-md border border-orange-200">18/06/2026</span>
                                </div>
                              </div>
                          </div>
                        </div>
                    </div>

                 </fieldset>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
