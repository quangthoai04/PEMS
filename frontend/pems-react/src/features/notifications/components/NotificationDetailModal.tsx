import React, { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { Bell, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { NotificationItem } from '../types/notification.types';
import { resolveNotificationPresentation } from '../utils/resolveNotificationPresentation';
import type { UiLanguage } from '../../../shared/utils/vietnamTime';

interface Props {
  item: NotificationItem | null;
  onClose: () => void;
}

export function NotificationDetailModal({ item, onClose }: Props) {
  if (!item) return null;
  return <NotificationDetailModalInner item={item} onClose={onClose} />;
}

function NotificationDetailModalInner({ item, onClose }: { item: NotificationItem; onClose: () => void }) {
  const { t, i18n } = useTranslation(['notifications']);
  const language = i18n.language as UiLanguage;
  const resolved = resolveNotificationPresentation(
    { metadataJson: item.metadataJson, createdAt: item.createdAt, legacyTitle: item.title, legacyMessage: item.message },
    language,
    t,
  );

  useEffect(() => {
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = prev; };
  }, []);

  return createPortal(
    <div className="fixed inset-0 z-[90] flex items-end sm:items-center justify-center p-0 sm:px-4">
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />
      <div className="relative flex w-full flex-col rounded-t-2xl bg-white shadow-2xl border border-slate-200 sm:w-[560px] sm:max-w-[96vw] sm:rounded-xl max-h-[92dvh] sm:max-h-[80dvh]">
        <div className="flex items-start justify-between gap-3 border-b border-slate-200 px-4 py-2.5">
          <h3 className="flex items-center gap-1.5 text-sm font-bold text-slate-900">
            <Bell className="h-4 w-4 text-[#004c91]" /> {t('notifications:detailModal.title')}
          </h3>
          <button
            type="button"
            onClick={onClose}
            className="shrink-0 rounded-full p-1.5 text-slate-500 hover:bg-slate-100 outline-none"
            aria-label={t('notifications:detailModal.close')}
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-4 py-4">
          <div className="flex items-start justify-between gap-2 mb-2">
            <h4 className="text-base font-bold text-slate-900">{resolved.title}</h4>
            {item.isActionRequired && (
              <span className="shrink-0 inline-block px-1.5 py-0.5 bg-orange-100 text-orange-700 text-[10px] font-bold rounded">
                {t('notifications:filters.actionRequired')}
              </span>
            )}
          </div>
          {resolved.message && (
            <p className="text-sm text-slate-600 whitespace-pre-line leading-relaxed">{resolved.message}</p>
          )}
          <p className="mt-4 text-xs text-slate-400">{resolved.relativeTime}</p>
        </div>
      </div>
    </div>,
    document.body
  );
}
