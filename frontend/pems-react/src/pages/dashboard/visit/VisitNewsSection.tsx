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

export function VisitNewsSection({ visitInstanceId }: { visitInstanceId: number }) {
  const [expanded, setExpanded] = useState(true);

  return (
    <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden shadow-sm transition-all relative">
      <div className="bg-[#00a651] px-6 py-4 flex items-center justify-between cursor-pointer" onClick={() => setExpanded(!expanded)}>
        <h2 className="text-xl font-bold text-white flex items-center gap-2">
          <Newspaper className="w-5 h-5" /> Tin tức đoàn khách
        </h2>
        <button type="button" className="text-white hover:bg-white/20 p-1 rounded-full transition-colors">
          {expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
        </button>
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
