/**
 * VisitNewsSection — tin tức gắn với 1 campus instance (tab "Sau tiếp khách").
 *
 * Logic duyệt mới: MỌI bài (Host hay participant viết) đều PENDING_REVIEW chờ Staff Leader
 * duyệt — không còn "Host tạo thì tự public". Nút Tạo/Sửa điều hướng sang đúng form News
 * Management (kèm ?visitInstanceId & returnTo) — không có form tạo tin thứ hai.
 * Backend quyết định hiển thị: participant chỉ thấy bài mình; Host thấy mọi bài của chuyến;
 * Staff Leader đúng campus thấy mọi bài + nút duyệt/từ chối.
 */
import { useState } from 'react';
import { ChevronUp, ChevronDown, Newspaper } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { VisitNewsPostList } from '../../../features/delegations/components/VisitNewsPostList';

export function VisitNewsSection({ visitInstanceId, sectionNumber }: { visitInstanceId: number, sectionNumber?: string | number }) {
  const [expanded, setExpanded] = useState(true);

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden font-sans">
      <div 
        className="flex items-center justify-between bg-[#004c91] px-5 py-3.5 cursor-pointer select-none transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-3">
          {sectionNumber && (
            <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black text-white shrink-0">
              {sectionNumber}
            </span>
          )}
          {!sectionNumber && (
            <div className="p-1.5 bg-white/10 text-white rounded-lg border border-white/20 shrink-0">
              <Newspaper className="w-4 h-4" />
            </div>
          )}
          <div>
            <h2 className="text-sm font-black text-white tracking-tight uppercase">Tin tức đoàn khách</h2>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <div className="p-1 hover:bg-white/10 rounded-md transition-colors text-white">
            <svg className={`w-5 h-5 transform transition-transform ${!expanded ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </div>
        </div>
      </div>

      <AnimatePresence>
        {expanded && (
          <motion.div initial={{ height: 0 }} animate={{ height: 'auto' }} exit={{ height: 0 }} className="overflow-hidden">
            <div className="p-4 sm:p-6 md:p-8">
              <VisitNewsPostList visitInstanceId={visitInstanceId} />
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
