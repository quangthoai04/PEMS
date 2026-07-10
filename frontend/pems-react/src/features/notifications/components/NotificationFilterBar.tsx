import React from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../../shared/hooks/useAuth';
import { getVisibleNotificationFilters, type NotificationFilterKey } from '../constants/notificationFilters';

interface Props {
  value: NotificationFilterKey;
  onChange: (v: NotificationFilterKey) => void;
}

export function NotificationFilterBar({ value, onChange }: Props) {
  const { t } = useTranslation(['notifications']);
  const { user } = useAuth();
  const visibleFilters = getVisibleNotificationFilters(user?.roleCode);

  return (
    <div className="bg-white rounded-2xl p-2 shadow-sm border border-slate-200 flex gap-1.5 overflow-x-auto">
      {visibleFilters.map((k) => (
        <button
          key={k}
          onClick={() => onChange(k)}
          className={`px-3.5 py-2 rounded-xl text-sm font-bold whitespace-nowrap transition-colors ${
            value === k ? 'bg-[#004c91] text-white' : 'text-slate-600 hover:bg-slate-100'
          }`}
        >
          {t(`notifications:filters.${k}`)}
        </button>
      ))}
    </div>
  );
}
