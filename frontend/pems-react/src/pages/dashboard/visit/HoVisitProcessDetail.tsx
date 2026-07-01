/**
 * Trang HoVisitProcessDetail
 * Theo dõi quá trình vận hành tiếp đón và điểm danh của người quản trị HO.
 */

import React, { useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Home, ChevronRight, CheckCircle, XCircle, FileText, Activity, Calendar } from 'lucide-react';
import { motion } from 'motion/react';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';

export function HoVisitProcessDetail() {
  const navigate = useNavigate();
  const { id } = useParams();
  const location = useLocation();
  const guest = location.state?.guestData;
  const returnUrl = location.state?.returnTo ?? '/dashboard/visit';

  const [activeTab, setActiveTab] = useState<'status' | 'minutes'>('status');
  const [isFormOpen, setIsFormOpen] = useState(false);

  const campuses = guest?.campusProgressItems?.map((item: any) => ({
    name: item.campusName || '-',
    time: item.plannedStartAt && item.plannedEndAt
      ? `${new Date(item.plannedStartAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })} ${new Date(item.plannedStartAt).toLocaleDateString('vi-VN')} - ${new Date(item.plannedEndAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })} ${new Date(item.plannedEndAt).toLocaleDateString('vi-VN')}`
      : 'Chưa có thời gian',
    status: item.instanceStatus,
    person: item.hostName || 'Chưa phân công',
    rejectReason: item.cancellationReason || null,
    visitInstanceId: item.visitInstanceId
  })) || [];

  return (
    <div className="flex-1 w-full bg-[#f8fbff] min-h-screen">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center text-sm font-medium text-gray-500 px-4 md:px-8 mt-4">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate(returnUrl)} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Quản lý campus</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Chi tiết tiếp khách</span>
      </div>

      <div className="max-w-7xl mx-auto px-4 md:px-8 pb-12 w-full">
        {/* Header */}
        <div className="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <button 
              onClick={() => navigate(returnUrl)}
              className="w-10 h-10 flex items-center justify-center rounded-xl bg-white border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all outline-none cursor-pointer shadow-sm"
            >
              <ArrowLeft className="w-5 h-5" />
            </button>
            <h1 className="text-3xl font-bold text-[#004c91]">Chi tiết tiếp khách</h1>
          </div>
          
          <div className="flex items-center gap-2 bg-white rounded-xl p-1.5 border border-gray-200 shadow-sm w-full md:w-auto">
            <button 
              onClick={() => setIsFormOpen(true)}
              className="flex-1 md:flex-none px-6 py-2.5 rounded-lg text-sm font-bold flex items-center justify-center gap-2 transition-colors bg-white text-gray-600 hover:text-[#004c91] hover:bg-blue-50 outline-none"
            >
              <FileText className="w-4 h-4" />
              Xem đơn yêu cầu
            </button>
            <button 
              className={`flex-1 md:flex-none px-6 py-2.5 rounded-lg text-sm font-bold flex items-center justify-center gap-2 transition-colors bg-[#004c91] text-white shadow cursor-default`}
            >
              <Activity className="w-4 h-4" />
              Tình trạng xử lý
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
            <div className="space-y-6">
                <h3 className="text-lg font-bold text-gray-800 mb-4 flex items-center gap-2">
                  <span className="w-2 h-6 bg-[#f37021] rounded-full inline-block"></span>
                  Tình trạng xử lý theo cơ sở
                </h3>
                
                <div className="grid gap-6 grid-cols-1">
                  {campuses.map((campus: any, idx: number) => (
                    <motion.div 
                      key={idx}
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ delay: idx * 0.1 }}
                      className="bg-slate-50 border border-slate-200 rounded-xl p-5 shadow-sm relative overflow-hidden group hover:shadow-md transition-shadow"
                    >
                      <div className={`absolute top-0 left-0 w-1 h-full ${campus.status === 'CANCELLED' || campus.status === 'REJECTED' ? 'bg-red-500' : campus.status === 'WAITING_HOST_ASSIGNMENT' || campus.status === 'WAITING_REQUEST_APPROVAL' ? 'bg-yellow-500' : 'bg-green-500'}`}></div>
                      
                      <div className="ml-2 flex flex-col h-full">
                        <div className="flex flex-col sm:flex-row sm:items-center justify-between mb-4 gap-2">
                          <h4 className="text-base font-bold text-gray-900 border-b border-gray-200 pb-1 pr-6 inline-block">Cơ sở: {campus.name}</h4>
                          {campus.status === 'CANCELLED' || campus.status === 'REJECTED' ? (
                            <div className="bg-red-100 text-red-700 px-3 py-1.5 rounded-full text-xs font-bold flex items-center gap-1.5 shadow-sm w-fit">
                              <XCircle className="w-3.5 h-3.5" /> {campus.status === 'CANCELLED' ? 'Đã Hủy' : 'Từ Chối'}
                            </div>
                          ) : campus.status === 'WAITING_HOST_ASSIGNMENT' || campus.status === 'WAITING_REQUEST_APPROVAL' ? (
                            <div className="bg-yellow-100 text-yellow-700 px-3 py-1.5 rounded-full text-xs font-bold flex items-center gap-1.5 shadow-sm w-fit">
                              <Activity className="w-3.5 h-3.5" /> Chờ Xử Lý
                            </div>
                          ) : (
                            <div className="bg-green-100 text-green-700 px-3 py-1.5 rounded-full text-xs font-bold flex items-center gap-1.5 shadow-sm w-fit">
                              <CheckCircle className="w-3.5 h-3.5" /> Đã Xác Nhận
                            </div>
                          )}
                        </div>

                        <div className="space-y-3 text-sm text-gray-700 mt-2 flex-grow">
                          <div className="flex items-start gap-2">
                            <span className="font-bold min-w-[70px] text-gray-500 shrink-0">Thời gian:</span>
                            <span className="font-medium bg-white px-2 py-0.5 rounded border border-gray-100 break-words">{campus.time}</span>
                          </div>
                          
                          <div className="flex items-start gap-2">
                            <span className="font-bold min-w-[70px] text-gray-500 shrink-0">Người phụ trách:</span>
                            <span className="font-bold text-[#004c91]">{campus.person}</span>
                          </div>

                          {campus.rejectReason && (
                            <div className="flex items-start gap-2 pt-2 border-t border-gray-100 mt-3">
                              <span className="font-bold min-w-[70px] text-gray-500 shrink-0">Lý do hủy/từ chối:</span>
                              <span className="italic text-red-600 bg-red-50 px-3 py-2 rounded-lg border border-red-100 text-[13px] font-medium leading-relaxed">"{campus.rejectReason}"</span>
                            </div>
                          )}
                          {campus.visitInstanceId && campus.status !== 'WAITING_REQUEST_APPROVAL' && campus.status !== 'WAITING_HOST_ASSIGNMENT' && (
                            <div className="pt-3 border-t border-gray-100 mt-3">
                              <button
                                onClick={() => navigate(`/dashboard/visit/process-summary/${campus.visitInstanceId}`)}
                                className="px-4 py-2 bg-white border border-[#004c91] text-[#004c91] rounded-lg text-sm font-bold flex items-center gap-2 hover:bg-blue-50 transition-colors shadow-sm outline-none"
                              >
                                <FileText className="w-4 h-4" />
                                Xem báo cáo tổng hợp & biên bản
                              </button>
                            </div>
                          )}
                        </div>
                      </div>
                    </motion.div>
                  ))}
                </div>
              </div>
            
          </div>
        </div>
      </div>

      <SubmittedVisitRequestDetailModal
        isOpen={isFormOpen}
        visitRequestId={guest?.visitRequestId ?? null}
        onClose={() => setIsFormOpen(false)}
      />
    </div>
  );
}
