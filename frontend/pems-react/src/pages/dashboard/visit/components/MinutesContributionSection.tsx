/**
 * MinutesContributionSection — phần "Biên bản" trong trang Đóng góp kết quả.
 *
 * Nút "Tạo biên bản"/"Sửa biên bản" mở modal NGAY TRÊN TRANG, bên trong render lại đúng
 * MinutesCard của luồng Quản lý tiếp khách (tên/trạng thái biên bản, điểm danh người tham gia,
 * nội dung, đầu mục công việc, cơ chế lock) — cùng component/API nên dữ liệu hai nơi luôn đồng bộ,
 * không có form biên bản thứ hai. Quyền do backend quyết (canCreate/canEdit + participant scope);
 * frontend chỉ ẩn nút, không thay thế authorization backend.
 */
import { useState } from 'react';
import { MinutesContributionStatus } from '../../../../features/delegations/types/delegations.types';
import { FileText, Lock, Edit3, X } from 'lucide-react';
import { sanitizeHtml } from '../../../../shared/security/sanitizeHtml';
import { MinutesCard } from '../MinutesCard';

interface Props {
  visitInstanceId: string;
  data: MinutesContributionStatus;
  canView: boolean;
  instanceStatus: string;
  onChanged: () => void;
  isReadOnly?: boolean;
}

export function MinutesContributionSection({ visitInstanceId, data, canView, onChanged, isReadOnly = false }: Props) {
  const [modalOpen, setModalOpen] = useState(false);

  if (!canView) return null;

  const closeModal = () => {
    setModalOpen(false);
    // Đồng bộ lại trạng thái tóm tắt của trang Đóng góp sau khi tạo/sửa trong modal.
    onChanged();
  };

  return (
    <div className="py-5">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-9 h-9 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
          <FileText className="w-5 h-5" />
        </div>
        <div className="flex-1">
          <h2 className="text-base font-black text-[#004c91]">Biên bản</h2>
          <p className="text-xs font-semibold text-slate-500">
            {data.hasMinutes ? 'Đã khởi tạo' : 'Chưa có biên bản'}
          </p>
        </div>
        {data.status === 'COMPLETED' && (
          <span className="inline-flex px-2.5 py-1 rounded-full border border-emerald-200 bg-emerald-50 text-emerald-700 text-[11px] font-bold">
            Hoàn tất
          </span>
        )}
      </div>

      <div className="space-y-4">
        {data.content ? (
          // ql-editor (không phải "prose" — chưa cài @tailwindcss/typography) để bullet/số thứ
          // tự/indent hiện đúng như lúc gõ trong RichTextEditor, dùng chung CSS Quill.
          <div
            className="ql-editor !h-auto !min-h-0 w-full rounded-xl border border-slate-200 bg-slate-50/60 !px-4 !py-3 text-sm text-slate-700"
            dangerouslySetInnerHTML={{ __html: sanitizeHtml(data.content) }}
          />
        ) : (
          <p className="text-sm font-semibold text-slate-400">Nội dung biên bản trống.</p>
        )}

        {data.lockedByName && (
          <div className="flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-2.5 text-sm font-semibold text-amber-700">
            <Lock className="w-4 h-4 shrink-0" />
            Đang được chỉnh sửa bởi {data.lockedByName}
          </div>
        )}

        {data.canCurrentUserEdit && !isReadOnly && (
          <div className="pt-2 flex flex-wrap gap-3">
            <button
              type="button"
              onClick={() => setModalOpen(true)}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 text-[#004c91] hover:bg-blue-100 rounded-lg text-sm font-bold transition-colors"
            >
              <Edit3 className="w-4 h-4" />
              {data.hasMinutes ? 'Sửa biên bản' : 'Tạo biên bản'}
            </button>
          </div>
        )}
      </div>

      {/* Modal biên bản — render lại MinutesCard đầy đủ chức năng ngay trên trang */}
      {modalOpen && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-[110] px-2 sm:px-6 py-6">
          <div className="w-full max-w-5xl max-h-full flex flex-col">
            <div className="flex justify-end mb-2">
              <button
                type="button"
                onClick={closeModal}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white/95 text-slate-600 hover:text-slate-900 text-sm font-bold shadow-sm transition-colors"
                aria-label="Đóng biên bản"
              >
                <X className="w-4 h-4" /> Đóng
              </button>
            </div>
            <div className="overflow-y-auto rounded-2xl">
              <MinutesCard
                visitInstanceId={Number(visitInstanceId)}
                isReadOnly={!data.canCurrentUserEdit}
              />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
