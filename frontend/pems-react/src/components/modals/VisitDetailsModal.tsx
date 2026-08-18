/**
 * Component VisitDetailsModal
 * Modal hiển thị thông tin chi tiết về một chuyến thăm cụ thể.
 * Bao gồm các tab thông tin chung, danh sách đoàn khách, lịch trình và các tài liệu liên quan.
 */

import React, { useEffect } from 'react';
import { X, Calendar, Clock, Download, ChevronDown } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { CancellationReasonPanel } from '../../features/delegations/components/CancellationReasonPanel';

interface VisitDetailsModalProps {
  isOpen: boolean;
  onClose: () => void;
  guest: any;
}

const ReadOnlyField = ({ label, value }: { label: string, value: string }) => (
  <div>
    <label className="block text-sm font-bold text-gray-500 mb-1">
      {label}
    </label>
    <div className="w-full px-4 py-2.5 rounded-xl border border-gray-100 bg-gray-50 text-sm font-normal text-gray-900 shadow-sm">
      {value || '-'}
    </div>
  </div>
);

export function VisitDetailsModal({ isOpen, onClose, guest }: VisitDetailsModalProps) {
  const userStr = localStorage.getItem("currentUser");
  const currentUser = userStr ? JSON.parse(userStr) : null;
  const isHO = currentUser?.role?.toUpperCase() === 'HO';

  const displayCampus = (campus: string) => {
    if (!campus) return '-';
    if (isHO && !campus.includes(',')) {
      return campus === 'Hà Nội' ? 'Hà Nội, Hồ Chí Minh' : `${campus}, Hà Nội`;
    }
    return campus;
  };

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

  if (!guest) return null;

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
            <div className="flex-none px-6 py-5 sm:px-10 flex flex-col sm:flex-row items-start sm:items-center justify-between text-white relative z-10 overflow-hidden bg-[#004c91]">
              <div className="relative z-10 pr-8">
                <h2 className="text-xl sm:text-2xl font-black tracking-tight mb-1 text-white">ĐƠN YÊU CẦU THAM QUAN</h2>
                <p className="text-white/80 font-normal text-xs sm:text-sm max-w-2xl">
                  Thông tin chi tiết về đoàn khách
                </p>
              </div>
              <button 
                onClick={onClose}
                className="absolute top-4 right-4 sm:top-5 sm:right-6 p-2 text-white/70 hover:text-white hover:bg-white/20 rounded-full transition-all flex-shrink-0 z-20"
              >
                <X className="w-5 h-5 sm:w-6 sm:h-6" />
              </button>
            </div>

            {/* Body */}
            <div className="flex-1 overflow-y-auto px-4 sm:px-10 py-8 bg-white custom-scrollbar">
              <div className="space-y-12">

                {/* UC-136: cancellation reason shown at the TOP for cancelled rows */}
                {(guest.isCancelled || guest.requestStatus === 'CANCELLED' || guest.campusStatus === 'CANCELLED') && (
                  <CancellationReasonPanel
                    cancellationLevel={guest.cancellationLevel}
                    cancelledByName={guest.cancelledByName}
                    cancelledByUserId={guest.cancelledBy}
                    cancelledAt={guest.cancelledAt}
                    cancellationActorType={guest.cancellationActorType}
                    cancellationSource={guest.cancellationSource}
                    cancellationReason={guest.cancellationReason}
                    contextLabel={guest.cancellationLevel === 'CAMPUS_INSTANCE' ? (guest.campus || null) : null}
                  />
                )}

                {/* 1. Thông tin người đăng ký */}
                <section>
                  <h3 className="text-lg sm:text-xl font-black text-[#004c91] border-b-2 border-[#f37021]/30 pb-2 mb-6 flex items-center gap-2 w-max pr-6">
                    <span className="flex items-center justify-center w-6 h-6 sm:w-7 sm:h-7 rounded-full bg-[#f37021] text-white text-sm">1</span>
                    THÔNG TIN NGƯỜI ĐĂNG KÝ
                  </h3>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-6">
                    <ReadOnlyField label="Họ và tên" value={guest.host || guest.sender} />
                    <ReadOnlyField label="Quốc Tịch" value="Việt Nam" />
                    <ReadOnlyField label="Đơn vị công tác" value={guest.org} />
                    <ReadOnlyField label="Chức danh, phòng ban" value="Giám đốc" />
                    <ReadOnlyField label="SĐT" value="0987654321" />
                    <ReadOnlyField label="Email" value="contact@example.com" />
                  </div>
                </section>

                {/* 2. Thông tin đoàn khách */}
                <section>
                  <h3 className="text-lg sm:text-xl font-black text-[#004c91] border-b-2 border-[#f37021]/30 pb-2 mb-6 flex items-center gap-2 w-max pr-6">
                    <span className="flex items-center justify-center w-6 h-6 sm:w-7 sm:h-7 rounded-full bg-[#f37021] text-white text-sm">2</span>
                    THÔNG TIN ĐOÀN KHÁCH
                  </h3>
                  
                  <div className="space-y-8">
                    {/* Khối 1 */}
                    <div className="bg-slate-50/50 rounded-2xl border-l-4 border-l-[#f37021] border border-gray-100 p-5 sm:p-7 shadow-sm">
                      <h4 className="text-gray-800 font-bold text-base mb-5 border-b border-gray-200 pb-2 uppercase tracking-wide">I. Thông tin chuyến thăm</h4>
                      <div className="space-y-6">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                          <ReadOnlyField label="Tên đoàn khách" value={guest.name} />
                          <div className="relative">
                            <label className="block text-sm font-bold text-gray-500 mb-2">Cơ sở tới thăm</label>
                            <div className="relative">
                              <select
                                disabled
                                value={displayCampus(guest.campus).includes(',') ? 'multiple' : 'single'}
                                className="w-full px-4 py-2.5 rounded-xl border border-gray-200 text-sm font-normal text-gray-900 bg-gray-50 shadow-sm appearance-none outline-none cursor-not-allowed"
                              >
                                <option value="single">Chỉ một cơ sở</option>
                                <option value="multiple">Liên cơ sở</option>
                              </select>
                              <ChevronDown className="absolute right-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500 pointer-events-none" />
                            </div>
                          </div>
                        </div>

                        <div className="bg-orange-50/50 p-5 rounded-xl border border-orange-100 relative">
                          <label className="block text-sm font-bold text-gray-800 mb-4">Thời gian dự kiến</label>
                          <div className="space-y-4 mt-2">
                            {displayCampus(guest.campus).split(',').map((campusItem, index) => (
                              <div key={index} className="flex flex-col xl:flex-row items-end gap-3 w-full animate-in fade-in slide-in-from-top-2 duration-300 pb-4 border-b border-gray-100 last:border-b-0 last:pb-0 relative">
                                <div className="flex-[1.2] w-full xl:w-auto relative">
                                  {index === 0 && <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wider">Cơ sở</label>}
                                  <div className="relative">
                                    <select
                                      disabled
                                      value={campusItem.trim()}
                                      className="w-full px-4 py-2.5 rounded-xl border border-gray-200 text-sm font-normal text-gray-900 bg-gray-50 shadow-sm appearance-none pr-8 cursor-not-allowed"
                                    >
                                      <option value={campusItem.trim()}>{campusItem.trim()}</option>
                                    </select>
                                    <ChevronDown className="absolute right-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
                                  </div>
                                </div>
                                <div className="flex-[1.5] w-full xl:w-auto relative">
                                  {index === 0 && <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wider">Ngày bắt đầu</label>}
                                  <div className="relative">
                                    <input type="text" disabled value={guest.date || "2023-10-20"} className="w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-200 text-sm font-normal text-gray-900 bg-gray-50 shadow-sm cursor-not-allowed" />
                                    <Calendar className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91]" />
                                  </div>
                                </div>
                                <div className="flex-1 w-full xl:w-auto relative">
                                  {index === 0 && <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wider">TG Bắt đầu</label>}
                                  <div className="relative">
                                    <input type="time" disabled value={guest.time ? guest.time.split(' - ')[0] : "08:00"} className="w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-200 text-sm font-normal text-gray-900 bg-gray-50 shadow-sm cursor-not-allowed" />
                                    <Clock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91]" />
                                  </div>
                                </div>
                                <div className="flex-1 w-full xl:w-auto relative">
                                  {index === 0 && <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wider">TG Kết thúc</label>}
                                  <div className="relative">
                                    <input type="time" disabled value={guest.time ? guest.time.split(' - ')[1] : "16:30"} className="w-full px-4 py-2.5 pl-10 rounded-xl border border-gray-200 text-sm font-normal text-gray-900 bg-gray-50 shadow-sm cursor-not-allowed" />
                                    <Clock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91]" />
                                  </div>
                                </div>
                                <div className="flex-[0.8] w-full xl:w-auto flex items-center justify-center h-[44px] px-3 bg-white rounded-xl border border-gray-200 select-none cursor-default">
                                  <span className="text-[#004c91] text-sm font-normal whitespace-nowrap">VN (GMT+7)</span>
                                </div>
                              </div>
                            ))}
                          </div>
                        </div>

                        <div className="space-y-6">
                          <div>
                            <label className="block text-sm font-bold text-gray-500 mb-2">Mục đích thăm FPTU</label>
                            <div className="w-full px-4 py-3 rounded-xl border border-gray-100 bg-gray-50 text-sm shadow-sm font-normal text-gray-900 min-h-[80px]">
                              Tham quan và tìm hiểu môi trường học tập
                            </div>
                          </div>
                          <div>
                            <label className="block text-sm font-bold text-gray-500 mb-2">Nội dung làm việc tại FPTU</label>
                            <div className="w-full px-4 py-3 rounded-xl border border-gray-100 bg-gray-50 text-sm shadow-sm font-normal text-gray-900 min-h-[80px]">
                              Tham quan campus, trao đổi với bộ phận tuyển sinh
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>

                    {/* Khối 2 */}
                    <div className="bg-blue-50/20 rounded-2xl border-l-4 border-l-[#004c91] border border-gray-100 p-5 sm:p-7 shadow-sm mt-8">
                      <h4 className="text-[#004c91] font-bold text-base mb-5 border-b border-blue-100 pb-2 uppercase tracking-wide">II. Thành phần tham dự & Liên hệ</h4>
                      <div className="space-y-8">
                        <div>
                          <div className="flex items-center justify-between mb-2">
                            <label className="block text-sm font-bold text-gray-500">Danh sách khách ({guest.pax} người)</label>
                            <button type="button" className="inline-flex items-center gap-2 px-3 py-1.5 bg-white text-slate-700 text-xs font-bold rounded-lg hover:bg-slate-50 transition-colors border border-slate-200 shadow-sm">
                              <Download className="w-3.5 h-3.5" /> Tải file đính kèm
                            </button>
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
                                </tr>
                              </thead>
                              <tbody>
                                <tr className="border-b border-gray-100 hover:bg-orange-50/50 transition-colors">
                                  <td className="p-3 text-center font-bold text-slate-400">1</td>
                                  <td className="p-3 border-l border-gray-100 font-medium">Nguyễn Văn A</td>
                                  <td className="p-3 border-l border-gray-100 font-normal">-</td>
                                  <td className="p-3 border-l border-gray-100 font-normal">{guest.org}</td>
                                  <td className="p-3 border-l border-gray-100 font-normal">Việt Nam</td>
                                </tr>
                                <tr className="border-b border-gray-100 hover:bg-orange-50/50 transition-colors">
                                  <td className="p-3 text-center font-bold text-slate-400">2</td>
                                  <td className="p-3 border-l border-gray-100 font-medium">Trần Thị B</td>
                                  <td className="p-3 border-l border-gray-100 font-normal">-</td>
                                  <td className="p-3 border-l border-gray-100 font-normal">{guest.org}</td>
                                  <td className="p-3 border-l border-gray-100 font-normal">Việt Nam</td>
                                </tr>
                              </tbody>
                            </table>
                          </div>
                        </div>

                        <div>
                          <label className="block text-sm font-bold text-gray-500 mb-2">Danh sách team hỗ trợ khách</label>
                          <div className="bg-white border border-gray-200 rounded-xl overflow-x-auto shadow-sm p-4 text-center font-normal text-gray-500">
                            Không có
                          </div>
                        </div>

                        <div>
                          <label className="block text-sm font-bold text-gray-500 mb-2">Thông tin đầu mối liên hệ</label>
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
                                <tr className="hover:bg-orange-50/50 transition-colors">
                                  <td className="p-3 font-medium">{guest.host}</td>
                                  <td className="p-3 border-l border-gray-100 font-normal">{guest.org}</td>
                                  <td className="p-3 border-l border-gray-100 font-normal">0987654321</td>
                                  <td className="p-3 border-l border-gray-100 font-normal">contact@example.com</td>
                                </tr>
                              </tbody>
                            </table>
                          </div>
                        </div>
                      </div>
                    </div>

                    {/* Khối 3 */}
                    <div className="bg-slate-50/50 rounded-2xl border-l-4 border-l-[#f37021] border border-gray-100 p-5 sm:p-7 shadow-sm mt-8">
                      <h4 className="text-gray-800 font-bold text-base mb-5 border-b border-gray-200 pb-2 uppercase tracking-wide">III. Yêu cầu & Xác nhận bổ sung</h4>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                        <div>
                          <label className="block text-sm font-bold text-gray-500 mb-2">Ngôn ngữ sử dụng</label>
                          <div className="w-max px-4 py-2 bg-blue-50 text-[#004c91] font-bold rounded-lg border border-blue-100">
                            Tiếng Việt
                          </div>
                        </div>
                        <div>
                          <label className="block text-sm font-bold text-gray-500 mb-2">Xác nhận sử dụng hình ảnh & Thông tin</label>
                          <div className="w-max px-4 py-2 bg-orange-50 text-[#f37021] font-bold rounded-lg border border-orange-100">
                            Đồng ý
                          </div>
                        </div>
                      </div>
                      
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-8 pt-6 mt-6 border-t border-gray-200">
                        <div>
                          <label className="block text-sm font-bold text-gray-500 mb-2">Nhận diện phương tiện di chuyển tới FPTU</label>
                          <div className="w-full px-4 py-3 rounded-xl border border-gray-100 bg-gray-50 text-sm shadow-sm font-normal text-gray-900 min-h-[80px]">
                            Xe khách 45 chỗ, biển số 29A-12345
                          </div>
                        </div>
                        <div>
                          <label className="block text-sm font-bold text-gray-500 mb-2">Ghi chú cho FPTU</label>
                          <div className="w-full px-4 py-3 rounded-xl border border-gray-100 bg-gray-50 text-sm shadow-sm font-normal text-gray-900 min-h-[80px]">
                            Không có ghi chú gì thêm
                          </div>
                        </div>
                      </div>
                    </div>

                  </div>
                </section>
              </div>
            </div>
            
            <div className="px-6 py-4 bg-gray-50 border-t border-gray-100 flex justify-end">
              <button 
                onClick={onClose}
                className="px-6 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors shadow-sm"
              >
                Đóng
              </button>
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
