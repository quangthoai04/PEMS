/**
 * Trang HoVisitProcessDetail
 * Theo dõi quá trình vận hành tiếp đón và điểm danh của người quản trị HO.
 */

import React, { useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Home, ChevronRight, CheckCircle, XCircle, FileText, Activity, Calendar } from 'lucide-react';
import { motion } from 'motion/react';

export function HoVisitProcessDetail() {
  const navigate = useNavigate();
  const { id } = useParams();
  const location = useLocation();
  const guest = location.state?.guestData;

  const [activeTab, setActiveTab] = useState<'status' | 'minutes'>('status');

  const mockCampuses = [
    {
      name: 'Hà Nội',
      time: '09:00 15/06/2026 - 17:00 15/06/2026',
      status: 'Đã xác nhận',
      person: 'Cán bộ Nguyễn Văn A (Staff Leader)',
      rejectReason: null
    },
    {
      name: 'Đà Nẵng',
      time: '09:00 17/06/2026 - 17:00 17/06/2026',
      status: 'Từ chối',
      person: 'Cán bộ Trần Thị B (Staff Leader)',
      rejectReason: 'Trùng lịch tổ chức Capstone lầu 3, hết phòng họp VIP và không đủ xe điện điều phối.'
    }
  ];

  return (
    <div className="flex-1 w-full bg-[#f8fbff] min-h-screen">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center text-sm font-medium text-gray-500 px-4 md:px-8 mt-4">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate('/dashboard/visit')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Quản lý campus</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Chi tiết tiếp khách</span>
      </div>

      <div className="max-w-7xl mx-auto px-4 md:px-8 pb-12 w-full">
        {/* Header */}
        <div className="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <button 
              onClick={() => navigate('/dashboard/visit')}
              className="w-10 h-10 flex items-center justify-center rounded-xl bg-white border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all outline-none cursor-pointer shadow-sm"
            >
              <ArrowLeft className="w-5 h-5" />
            </button>
            <h1 className="text-3xl font-bold text-[#004c91]">Chi tiết tiếp khách</h1>
          </div>
          
          <div className="flex items-center gap-2 bg-white rounded-xl p-1.5 border border-gray-200 shadow-sm w-full md:w-auto">
            <button 
              onClick={() => setActiveTab('status')}
              className={`flex-1 md:flex-none px-6 py-2.5 rounded-lg text-sm font-bold flex items-center justify-center gap-2 transition-colors ${activeTab === 'status' ? 'bg-[#004c91] text-white shadow' : 'text-gray-500 hover:text-[#004c91] hover:bg-blue-50'}`}
            >
              <Activity className="w-4 h-4" />
              Tình trạng xử lý
            </button>
            <button 
              onClick={() => setActiveTab('minutes')}
              className={`flex-1 md:flex-none px-6 py-2.5 rounded-lg text-sm font-bold flex items-center justify-center gap-2 transition-colors ${activeTab === 'minutes' ? 'bg-[#004c91] text-white shadow' : 'text-gray-500 hover:text-[#004c91] hover:bg-blue-50'}`}
            >
              <FileText className="w-4 h-4" />
              Biên bản cuộc họp
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
          <div className="bg-[#004c91] px-6 py-4 md:px-8 border-b border-[#003366]">
            <h2 className="text-xl font-bold text-white mb-1">{guest?.name || 'Đoàn khách Đại học Monash'}</h2>
            <p className="text-white/80 text-sm font-medium">{guest?.org || 'Monash University'}</p>
          </div>
          
          <div className="p-6 md:p-8 space-y-8">
            {activeTab === 'status' ? (
              <div className="space-y-6">
                <h3 className="text-lg font-bold text-gray-800 mb-4 flex items-center gap-2">
                  <span className="w-2 h-6 bg-[#f37021] rounded-full inline-block"></span>
                  Tình trạng xử lý theo cơ sở
                </h3>
                
                <div className="grid gap-6 grid-cols-1">
                  {mockCampuses.map((campus, idx) => (
                    <motion.div 
                      key={idx}
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ delay: idx * 0.1 }}
                      className="bg-slate-50 border border-slate-200 rounded-xl p-5 shadow-sm relative overflow-hidden group hover:shadow-md transition-shadow"
                    >
                      <div className={`absolute top-0 left-0 w-1 h-full ${campus.status === 'Đã xác nhận' ? 'bg-green-500' : 'bg-red-500'}`}></div>
                      
                      <div className="ml-2 flex flex-col h-full">
                        <div className="flex flex-col sm:flex-row sm:items-center justify-between mb-4 gap-2">
                          <h4 className="text-base font-bold text-gray-900 border-b border-gray-200 pb-1 pr-6 inline-block">Cơ sở: {campus.name}</h4>
                          {campus.status === 'Đã xác nhận' ? (
                            <div className="bg-green-100 text-green-700 px-3 py-1.5 rounded-full text-xs font-bold flex items-center gap-1.5 shadow-sm w-fit">
                              <CheckCircle className="w-3.5 h-3.5" /> Đã Xác Nhận
                            </div>
                          ) : (
                            <div className="bg-red-100 text-red-700 px-3 py-1.5 rounded-full text-xs font-bold flex items-center gap-1.5 shadow-sm w-fit">
                              <XCircle className="w-3.5 h-3.5" /> Từ Chối
                            </div>
                          )}
                        </div>

                        <div className="space-y-3 text-sm text-gray-700 mt-2 flex-grow">
                          <div className="flex items-start gap-2">
                            <span className="font-bold min-w-[70px] text-gray-500 shrink-0">Thời gian:</span>
                            <span className="font-medium bg-white px-2 py-0.5 rounded border border-gray-100 break-words">{campus.time}</span>
                          </div>
                          
                          <div className="flex items-start gap-2">
                            <span className="font-bold min-w-[70px] text-gray-500 shrink-0">Người {campus.status === 'Đã xác nhận' ? 'duyệt' : 'từ chối'}:</span>
                            <span className="font-bold text-[#004c91]">{campus.person}</span>
                          </div>

                          {campus.rejectReason && (
                            <div className="flex items-start gap-2 pt-2 border-t border-gray-100 mt-3">
                              <span className="font-bold min-w-[70px] text-gray-500 shrink-0">Lý do từ chối:</span>
                              <span className="italic text-red-600 bg-red-50 px-3 py-2 rounded-lg border border-red-100 text-[13px] font-medium leading-relaxed">"{campus.rejectReason}"</span>
                            </div>
                          )}
                        </div>
                      </div>
                    </motion.div>
                  ))}
                </div>
              </div>
            ) : (
              <div className="space-y-8">
                <h3 className="text-lg font-bold text-gray-800 mb-4 flex items-center gap-2">
                  <span className="w-2 h-6 bg-[#004c91] rounded-full inline-block"></span>
                  Biên bản cuộc họp
                </h3>
                
                {mockCampuses.filter(c => c.status === 'Đã xác nhận').map((campus, idx) => (
                  <motion.div
                    key={idx}
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: idx * 0.1 }}
                    className="border border-gray-200 rounded-xl overflow-hidden shadow-sm"
                  >
                    <div className="bg-[#f8fbff] border-b border-gray-200 p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                      <div className="flex items-center flex-wrap gap-4">
                        <h4 className="font-bold text-[#004c91] text-lg flex items-center gap-2 border-r border-gray-300 pr-4">
                          <FileText className="w-5 h-5 text-[#f37021]" />
                          Cơ sở {campus.name}
                        </h4>
                        <span className="text-md font-bold text-gray-800">Biên bản họp trao đổi hợp tác</span>
                      </div>
                      <span className="text-sm font-bold text-gray-500 bg-white px-3 py-1.5 rounded-full border border-gray-200 shadow-sm shrink-0 flex items-center justify-center gap-1.5">
                        <Calendar className="w-4 h-4 text-gray-400" />
                        15/06/2026
                      </span>
                    </div>
                    
                    <div className="p-6 bg-white space-y-6">
                      <div>
                        <h5 className="text-sm font-bold text-gray-800 mb-3 border-l-4 border-[#004c91] pl-3 uppercase tracking-wider">Thành phần tham dự</h5>
                        <div className="overflow-x-auto">
                          <table className="w-full text-left border-collapse text-sm border border-gray-200 rounded-lg overflow-hidden">
                            <thead className="bg-gray-50">
                              <tr>
                                <th className="px-4 py-2 border-b border-gray-200 font-bold text-gray-600">STT</th>
                                <th className="px-4 py-2 border-b border-gray-200 font-bold text-gray-600">Họ và tên</th>
                                <th className="px-4 py-2 border-b border-gray-200 font-bold text-gray-600">Chức vụ</th>
                                <th className="px-4 py-2 border-b border-gray-200 font-bold text-gray-600">Đơn vị</th>
                              </tr>
                            </thead>
                            <tbody>
                              <tr className="border-b border-gray-100 hover:bg-gray-50/50">
                                <td className="px-4 py-2 text-center text-gray-500">1</td>
                                <td className="px-4 py-2 font-medium text-gray-900">Nguyễn Văn A</td>
                                <td className="px-4 py-2 text-gray-600">Staff Leader</td>
                                <td className="px-4 py-2 text-gray-600">Phòng TS & CTSV</td>
                              </tr>
                              <tr className="hover:bg-gray-50/50">
                                <td className="px-4 py-2 text-center text-gray-500">2</td>
                                <td className="px-4 py-2 font-medium text-gray-900">John Doe</td>
                                <td className="px-4 py-2 text-gray-600">Director</td>
                                <td className="px-4 py-2 text-gray-600">Đoàn đối tác</td>
                              </tr>
                            </tbody>
                          </table>
                        </div>
                      </div>

                      <div>
                        <h5 className="text-sm font-bold text-gray-800 mb-3 border-l-4 border-[#f37021] pl-3 uppercase tracking-wider">Nội dung trao đổi (Ghi chú)</h5>
                        <div className="bg-gray-50 p-4 rounded-xl text-sm text-gray-700 leading-relaxed border border-gray-100">
                          <p>- Bàn về định hướng hợp tác tuyển sinh và đào tạo năm 2026-2027.</p>
                          <p className="mt-2">- Thống nhất các chương trình campus tour sắp tới dành cho học sinh.</p>
                          <p className="mt-2">- Bàn giao tài liệu giới thiệu chương trình học bổng.</p>
                        </div>
                      </div>
                      
                      <div>
                        <h5 className="text-sm font-bold text-gray-800 mb-3 border-l-4 border-green-500 pl-3 uppercase tracking-wider">Đầu mục công việc (Next Steps)</h5>
                        <ul className="list-disc list-inside space-y-2 text-sm text-gray-700 bg-white border border-gray-100 p-4 rounded-xl shadow-sm">
                          <li><strong>Phòng Tuyển sinh:</strong> Gửi báo cáo chi tiết chương trình học bổng (Deadline: 20/06/2026).</li>
                          <li><strong>Đoàn đối tác:</strong> Phản hồi về lịch trình campus tour đợt 2 (Deadline: 25/06/2026).</li>
                        </ul>
                      </div>
                    </div>
                  </motion.div>
                ))}
                
                {mockCampuses.filter(c => c.status === 'Đã xác nhận').length === 0 && (
                  <div className="text-center p-8 bg-gray-50 rounded-xl border border-gray-200">
                    <FileText className="w-12 h-12 text-gray-300 mx-auto mb-3" />
                    <p className="text-gray-500 font-medium">Chưa có biên bản cuộc họp nào do chưa có cơ sở nào xác nhận đón tiếp.</p>
                  </div>
                )}
              </div>
            )}
            
          </div>
        </div>
      </div>
    </div>
  );
}
