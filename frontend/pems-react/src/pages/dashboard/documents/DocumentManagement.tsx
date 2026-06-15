/**
 * Trang DocumentManagement
 * Kho lưu trữ số các loại quy định mẫu, biên nhận biểu mẫu, tải về cho nhân sự.
 */

import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, Plus, Filter, FileText, Download, Eye, Trash2, FileIcon, FileType2, FileSpreadsheet, FileArchive, MoreVertical, FileDown, ArrowDown, ChevronLeft, ChevronRight } from 'lucide-react';
import { motion } from 'motion/react';

interface Document {
  id: string;
  name: string;
  type: string;
  size: string;
  uploadDate: string;
  partner: string;
}

const mockDocuments: Document[] = [
  {
    id: '1',
    name: 'hướng dẫn quy trình đón tiếp khách đoàn.pdf',
    type: 'pdf',
    size: '2.4 MB',
    uploadDate: '15/06/2026',
    partner: 'Đại học Quốc gia Hà Nội'
  },
  {
    id: '2',
    name: 'template_ke_hoach_tiep_khach_tieu_chuan.docx',
    type: 'doc',
    size: '1.2 MB',
    uploadDate: '10/06/2026',
    partner: 'Tập đoàn Vingroup'
  },
  {
    id: '3',
    name: 'profile_fpt_university_2026.pdf',
    type: 'pdf',
    size: '15.6 MB',
    uploadDate: '01/06/2026',
    partner: 'Đại học Swinburne'
  },
  {
    id: '4',
    name: 'mau_bien_ban_ghi_nho_mou.docx',
    type: 'doc',
    size: '800 KB',
    uploadDate: '25/05/2026',
    partner: 'Tập đoàn FPT'
  },
  {
    id: '5',
    name: 'danh_sach_khach_san_lien_ket.xlsx',
    type: 'xls',
    size: '300 KB',
    uploadDate: '10/05/2026',
    partner: 'Khách sạn Mường Thanh'
  }
];

export function DocumentManagement() {
  const navigate = useNavigate();
  const [searchQuery, setSearchQuery] = useState('');
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>('desc');

  const filteredDocs = [...mockDocuments]
    .filter(doc => doc.name.toLowerCase().includes(searchQuery.toLowerCase()) || doc.partner.toLowerCase().includes(searchQuery.toLowerCase()))
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
        <span className="text-[#004c91] font-bold">Quản lý tài liệu</span>
      </div>
      
      <div className="border-b border-gray-100 pb-4 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91]">Quản lý tài liệu</h1>
          <p className="text-gray-500 mt-2 font-medium">Lưu trữ tài liệu của đối tác</p>
        </div>
      </div>

      {/* 2. Action & Filter Controller */}
      <div className="w-full">
        <div className="flex flex-col lg:flex-row items-center gap-4">
          <div className="relative flex-1 w-full group">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 w-5 h-5 group-focus-within:text-[#004c91] transition-colors pointer-events-none" />
            <input 
              type="text" 
              placeholder="Tìm kiếm theo tên tài liệu, tên đối tác..." 
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
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider whitespace-nowrap">Tên Tài Liệu</th>
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider whitespace-nowrap">ĐỐI TÁC</th>
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider whitespace-nowrap text-center">Kích Thước</th>
                <th 
                  className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider cursor-pointer group hover:bg-[#00386b] transition-colors whitespace-nowrap text-center"
                  onClick={handleSortToggle}
                >
                  <div className="flex items-center justify-center gap-1">
                    Ngày Tải Lên
                    <ArrowDown className={`w-4 h-4 text-white/70 group-hover:text-white transition-transform duration-300 ${sortOrder === 'asc' ? 'rotate-180' : ''}`} />
                  </div>
                </th>
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider text-center whitespace-nowrap">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filteredDocs.length > 0 ? filteredDocs.map((doc, index) => (
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
                      {doc.partner}
                    </span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-center">
                    <span className="text-sm font-medium text-slate-600">
                      {doc.size}
                    </span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-center">
                    <span className="text-sm font-medium text-slate-600">
                      {doc.uploadDate}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex items-center justify-center gap-2">
                      <button className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-[#004c91] bg-blue-50 border border-blue-100 hover:bg-blue-100 hover:border-blue-200 rounded-lg transition-colors outline-none cursor-pointer whitespace-nowrap" title="Tải xuống">
                        <Download className="w-4 h-4" /> Tải xuống
                      </button>
                    </div>
                  </td>
                </motion.tr>
              )) : (
                <tr>
                  <td colSpan={5} className="px-6 py-12 text-center">
                     <div className="flex flex-col items-center justify-center">
                        <div className="w-16 h-16 bg-slate-50 rounded-full flex items-center justify-center mb-4">
                          <Search className="w-8 h-8 text-slate-300" />
                        </div>
                        <p className="text-slate-500 font-medium font-sans">Không tìm thấy tài liệu phù hợp.</p>
                     </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        
        {/* Pagination */}
        {filteredDocs.length > 0 && (
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
    </div>
  );
}
