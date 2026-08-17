import React from 'react';
import { Search } from 'lucide-react';

export type TimeRangeFilter = '' | '7d' | '30d' | 'custom';

interface Props {
  searchQuery: string;
  onSearchQueryChange: (value: string) => void;
  ratingLevel: string;
  onRatingLevelChange: (value: string) => void;
  timeRange: TimeRangeFilter;
  onTimeRangeChange: (value: TimeRangeFilter) => void;
  fromDate: string;
  toDate: string;
  onFromDateChange: (value: string) => void;
  onToDateChange: (value: string) => void;
  dateError: string;
  onReset: () => void;
  /** Chỉ HO truyền vào — lọc theo cơ sở, ẩn hoàn toàn với các role khác. */
  campusOptions?: { campusId: number; name: string }[];
  campusFilter?: string;
  onCampusFilterChange?: (value: string) => void;
}

export function FeedbackFilterBar({
  searchQuery,
  onSearchQueryChange,
  ratingLevel,
  onRatingLevelChange,
  timeRange,
  onTimeRangeChange,
  fromDate,
  toDate,
  onFromDateChange,
  onToDateChange,
  dateError,
  onReset,
  campusOptions,
  campusFilter,
  onCampusFilterChange,
}: Props) {
  return (
    <div className="bg-white rounded-2xl p-4 shadow-sm border border-slate-200 flex flex-col md:flex-row md:flex-wrap gap-3 md:items-center">
      <div className="relative w-full md:max-w-sm md:flex-1">
        <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
        <input
          type="text"
          placeholder="Tìm theo tên đoàn, người đánh giá, đối tượng được đánh giá..."
          value={searchQuery}
          onChange={(e) => onSearchQueryChange(e.target.value)}
          className="w-full pl-9 pr-3 py-2 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal"
        />
      </div>

      <select
        value={ratingLevel}
        onChange={(e) => onRatingLevelChange(e.target.value)}
        className="px-3 py-2 rounded-xl border border-slate-200 text-sm font-normal outline-none"
      >
        <option value="">Tất cả mức độ</option>
        <option value="LOW">Cảnh báo 1-2 sao</option>
        <option value="3">Trung bình 3 sao</option>
        <option value="GOOD">Tốt 4-5 sao</option>
      </select>

      <select
        value={timeRange}
        onChange={(e) => onTimeRangeChange(e.target.value as TimeRangeFilter)}
        className="px-3 py-2 rounded-xl border border-slate-200 text-sm font-normal outline-none"
      >
        <option value="">Tất cả thời gian</option>
        <option value="7d">7 ngày gần nhất</option>
        <option value="30d">30 ngày gần nhất</option>
        <option value="custom">Khoảng ngày tùy chọn</option>
      </select>

      {campusOptions && (
        <select
          value={campusFilter || ''}
          onChange={(e) => onCampusFilterChange?.(e.target.value)}
          className="px-3 py-2 rounded-xl border border-slate-200 text-sm font-normal outline-none"
        >
          <option value="">Tất cả cơ sở</option>
          {campusOptions.map((c) => (
            <option key={c.campusId} value={c.campusId}>{c.name}</option>
          ))}
        </select>
      )}

      {timeRange === 'custom' && (
        <div className="flex items-center gap-2 relative">
          <input
            type="date"
            value={fromDate}
            onChange={(e) => onFromDateChange(e.target.value)}
            className="px-3 py-2 rounded-xl border border-slate-200 text-sm font-normal outline-none"
            title="Từ ngày"
          />
          <span className="text-slate-400">-</span>
          <input
            type="date"
            value={toDate}
            onChange={(e) => onToDateChange(e.target.value)}
            className="px-3 py-2 rounded-xl border border-slate-200 text-sm font-normal outline-none"
            title="Đến ngày"
          />
          {dateError && <span className="text-red-500 text-xs absolute -bottom-5 left-0 whitespace-nowrap font-medium">{dateError}</span>}
        </div>
      )}

      <button
        onClick={onReset}
        className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-sm font-bold rounded-xl transition-colors whitespace-nowrap md:ml-auto"
      >
        Reset
      </button>
    </div>
  );
}
