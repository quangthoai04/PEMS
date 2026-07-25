/**
 * NewsContributionSection — nhóm "Tin tức" trong trang Đóng góp kết quả (spec §7).
 *
 * Dùng CHUNG danh sách + luồng tạo/sửa với News Management qua VisitNewsPostList
 * (không có form tạo tin thứ hai): nút Tạo/Sửa điều hướng sang /dashboard/news/create|edit
 * kèm ?visitInstanceId & returnTo. Backend enforce quyền: participant chỉ thấy bài mình,
 * Host thấy mọi bài của chuyến, Staff Leader đúng campus thấy mọi bài + duyệt/từ chối.
 */
import { NewsContributionStatus } from '../../../../features/delegations/types/delegations.types';
import { Newspaper } from 'lucide-react';
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

  return (
    <div className="py-5">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-9 h-9 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
          <Newspaper className="w-5 h-5" />
        </div>
        <div className="flex-1">
          <h2 className="text-base font-black text-[#004c91]">Tin tức</h2>
          <p className="text-xs font-semibold text-slate-500">
            Bài viết sau tiếp khách — mọi bài đều chờ Staff Leader duyệt trước khi đăng.
          </p>
        </div>
      </div>

      <div className="space-y-4">
        {data.newsNotRequired && (
          <p className="text-sm font-semibold text-slate-500 italic">
            Chuyến thăm này không yêu cầu bài tin tức (theo xác nhận của Host).
          </p>
        )}

        {!data.mediaConsentAllowed && (
          <p className="text-sm font-semibold text-rose-500 italic">
            Khách không đồng ý truyền thông, không thể tạo bài tin.
          </p>
        )}

        <VisitNewsPostList
          visitInstanceId={visitInstanceId}
          createBlocked={createBlocked}
          emptyText="Bạn chưa có bài tin tức nào cho chuyến thăm này."
        />
      </div>
    </div>
  );
}
