/**
 * NewsContributionSection — nhóm "Tin tức" trong trang Đóng góp kết quả (spec §7).
 *
 * Dùng CHUNG danh sách + luồng tạo/sửa với News Management qua VisitNewsPostList
 * (không có form tạo tin thứ hai): nút Tạo/Sửa điều hướng sang /dashboard/news/create|edit
 * kèm ?visitInstanceId & returnTo. Backend enforce quyền: participant chỉ thấy bài mình,
 * Host thấy mọi bài của chuyến, Staff Leader đúng campus thấy mọi bài + duyệt/từ chối.
 */
import { NewsContributionStatus } from '../../../../features/delegations/types/delegations.types';
import { Newspaper, AlertTriangle, Info } from 'lucide-react';
import { VisitNewsPostList } from '../../../../features/delegations/components/VisitNewsPostList';

interface Props {
  visitInstanceId: string;
  data: NewsContributionStatus;
  canView: boolean;
  instanceStatus: string;
  onChanged: () => void;
  isReadOnly?: boolean;
}

export function NewsContributionSection({ visitInstanceId, data, canView, isReadOnly = false }: Props) {
  if (!canView) return null;

  // Hai chặn nghiệp vụ hiển thị rõ cho người đóng góp (backend cũng enforce khi tạo/sửa).
  const createBlocked = data.newsNotRequired || !data.mediaConsentAllowed || isReadOnly;

  // Chỉ hiện 1 lý do ưu tiên nhất — tránh chồng nhiều message cùng ý (spec: consent > not-required).
  const reason = !data.mediaConsentAllowed
    ? { text: 'Khách không đồng ý truyền thông, không thể tạo bài tin.', tone: 'amber' as const }
    : data.newsNotRequired
      ? { text: 'Chuyến thăm này không yêu cầu bài tin tức (theo xác nhận của người phụ trách tiếp đón).', tone: 'slate' as const }
      : null;

  const statusLabel = reason ? 'Không khả dụng' : data.hasNews ? 'Đã có bài' : 'Chưa có bài';

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2.5 min-w-0">
          <span className="w-8 h-8 rounded-lg bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
            <Newspaper className="w-4 h-4" />
          </span>
          <div className="min-w-0">
            <h2 className="text-sm sm:text-base font-semibold text-[#004c91]">Tin tức</h2>
            <p className="text-xs font-normal text-slate-500">
              Mọi bài chờ Staff Leader duyệt trước khi đăng
            </p>
          </div>
        </div>
        <span className="text-xs font-bold text-slate-500 shrink-0 pt-1">{statusLabel}</span>
      </div>

      <div className="mt-3 space-y-3">
        {reason && (
          <div className={
            reason.tone === 'amber'
              ? 'flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm font-normal text-amber-700'
              : 'flex items-start gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-normal text-slate-600'
          }>
            {reason.tone === 'amber' ? <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" /> : <Info className="w-4 h-4 shrink-0 mt-0.5" />}
            <span>{reason.text}</span>
          </div>
        )}

        <VisitNewsPostList
          visitInstanceId={visitInstanceId}
          createBlocked={createBlocked}
          emptyText="Bạn chưa có bài tin tức nào cho chuyến thăm này."
          compact
          hideEmptyState={!!reason}
        />
      </div>
    </div>
  );
}
