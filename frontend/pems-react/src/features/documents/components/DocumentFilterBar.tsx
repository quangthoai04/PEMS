import React from 'react';
import { Search } from 'lucide-react';
import { DocumentOwnerType, DocumentStatus } from '../types/documents.types';
import { formatDocumentType, formatDocumentStatus } from '../../../shared/utils/domainLabels';

const OWNER_TYPE_OPTIONS: DocumentOwnerType[] = ['GENERAL', 'VISIT', 'PARTNER', 'MINUTES', 'NEWS', 'LOGISTICS', 'REPORT'];
const STATUS_OPTIONS: DocumentStatus[] = ['DRAFT', 'PUBLISHED', 'ARCHIVED'];

interface Props {
  searchQuery: string;
  onSearchQueryChange: (value: string) => void;
  ownerTypeFilter: DocumentOwnerType | 'All';
  onOwnerTypeFilterChange: (value: DocumentOwnerType | 'All') => void;
  statusFilter: DocumentStatus | 'All';
  onStatusFilterChange: (value: DocumentStatus | 'All') => void;
  dateFrom: string;
  onDateFromChange: (value: string) => void;
  dateTo: string;
  onDateToChange: (value: string) => void;
  onReset: () => void;
  /** Chỉ HO truyền vào — lọc theo cơ sở, ẩn hoàn toàn với các role khác. */
  campusOptions?: { campusId: number; name: string }[];
  campusFilter?: string;
  onCampusFilterChange?: (value: string) => void;
}

export function DocumentFilterBar({
  searchQuery,
  onSearchQueryChange,
  ownerTypeFilter,
  onOwnerTypeFilterChange,
  statusFilter,
  onStatusFilterChange,
  dateFrom,
  onDateFromChange,
  dateTo,
  onDateToChange,
  onReset,
  campusOptions,
  campusFilter,
  onCampusFilterChange,
}: Props) {
  return (
    <div className="bg-white rounded-xl p-3 border border-slate-200 shadow-sm flex flex-col md:flex-row md:flex-wrap gap-2.5 md:items-center">
      <div className="relative w-full md:max-w-xs md:flex-1">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 w-4 h-4 pointer-events-none" />
        <input
          type="text"
          placeholder="Tìm kiếm tài liệu, filename..."
          value={searchQuery}
          onChange={(e) => onSearchQueryChange(e.target.value)}
          className="w-full pl-9 pr-3 py-2 border border-slate-200 rounded-lg bg-white text-sm font-medium text-slate-800 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] transition-all placeholder:text-slate-400"
        />
      </div>

      <select
        value={ownerTypeFilter}
        onChange={(e) => onOwnerTypeFilterChange(e.target.value as DocumentOwnerType | 'All')}
        className="px-3 py-2 border border-slate-200 rounded-lg text-sm font-medium focus:outline-none focus:border-[#004c91] bg-white"
      >
        <option value="All">Tất cả loại</option>
        {OWNER_TYPE_OPTIONS.map((type) => (
          <option key={type} value={type}>{formatDocumentType(type)}</option>
        ))}
      </select>

      <select
        value={statusFilter}
        onChange={(e) => onStatusFilterChange(e.target.value as DocumentStatus | 'All')}
        className="px-3 py-2 border border-slate-200 rounded-lg text-sm font-medium focus:outline-none focus:border-[#004c91] bg-white"
      >
        <option value="All">Tất cả trạng thái</option>
        {STATUS_OPTIONS.map((status) => (
          <option key={status} value={status}>{formatDocumentStatus(status)}</option>
        ))}
      </select>

      {campusOptions && (
        <select
          value={campusFilter || ''}
          onChange={(e) => onCampusFilterChange?.(e.target.value)}
          className="px-3 py-2 border border-slate-200 rounded-lg text-sm font-medium focus:outline-none focus:border-[#004c91] bg-white"
        >
          <option value="">Tất cả cơ sở</option>
          {campusOptions.map((c) => (
            <option key={c.campusId} value={c.campusId}>{c.name}</option>
          ))}
        </select>
      )}

      <div className="flex items-center gap-1.5">
        <input
          type="date"
          value={dateFrom}
          onChange={(e) => onDateFromChange(e.target.value)}
          title="Ngày tải lên (từ)"
          className="px-2.5 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:border-[#004c91]"
        />
        <span className="text-slate-400">-</span>
        <input
          type="date"
          value={dateTo}
          onChange={(e) => onDateToChange(e.target.value)}
          title="Ngày tải lên (đến)"
          className="px-2.5 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:border-[#004c91]"
        />
      </div>

      <button
        onClick={onReset}
        className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-sm font-bold rounded-lg transition-colors whitespace-nowrap md:ml-auto"
      >
        Reset
      </button>
    </div>
  );
}
