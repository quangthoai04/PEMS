import React from 'react';
import {
  PERSONNEL_STATUS_LABELS,
  type PersonnelStatus,
} from '../types/departmentLeaderPersonnel.types';

/**
 * Status pill. INACTIVE and LOCKED get visually distinct treatments on purpose — merging them would
 * present a security lock as an ordinary deactivation, and the two have completely different
 * resolution paths.
 */
const STYLES: Record<PersonnelStatus, string> = {
  ACTIVE: 'bg-green-100 text-green-800',
  INACTIVE: 'bg-gray-200 text-gray-700',
  PENDING_EMAIL_CONFIRMATION: 'bg-amber-100 text-amber-800',
  LOCKED: 'bg-red-100 text-red-800',
};

export function PersonnelStatusBadge({ status }: { status: PersonnelStatus }) {
  return (
    <span
      className={`inline-flex whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-medium ${
        STYLES[status] ?? 'bg-gray-100 text-gray-700'
      }`}
    >
      {PERSONNEL_STATUS_LABELS[status] ?? status}
    </span>
  );
}
