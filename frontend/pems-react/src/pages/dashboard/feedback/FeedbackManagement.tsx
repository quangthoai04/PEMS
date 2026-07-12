/**
 * Trang FeedbackManagement
 * Giao diện toàn trình quản lý các đánh giá lưu trữ công cộng hoặc qua mail cảm ơn.
 */

import React, { useState, useEffect, useMemo } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../shared/hooks/useAuth';
import { useCampusFilterOptions } from '../../../features/campus-management/hooks/useCampusManagement';
import { useFeedbacks } from '../../../features/feedbacks/hooks/useFeedbacks';
import { FeedbackFilterParams } from '../../../features/feedbacks/types/feedbacks.types';
import { FeedbackSummaryCompact } from '../../../features/feedbacks/components/FeedbackSummaryCompact';
import { FeedbackFilterBar, TimeRangeFilter } from '../../../features/feedbacks/components/FeedbackFilterBar';
import { FeedbackTable } from '../../../features/feedbacks/components/FeedbackTable';

const toIsoDate = (d: Date) => d.toISOString().slice(0, 10);

export function FeedbackManagement() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const isHO = user?.roleCode === 'HO';
  const campusFilterOptions = useCampusFilterOptions();

  const { summaries, loading, fetchSummaries } = useFeedbacks();

  const [searchQuery, setSearchQuery] = useState('');
  const [filterRating, setFilterRating] = useState('');
  const [timeRange, setTimeRange] = useState<TimeRangeFilter>('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [dateError, setDateError] = useState('');
  const [campusFilter, setCampusFilter] = useState('');

  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  // Debounce search
  const [debouncedSearch, setDebouncedSearch] = useState(searchQuery);
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchQuery);
    }, 500);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Validate custom date range
  useEffect(() => {
    if (timeRange === 'custom' && fromDate && toDate && fromDate > toDate) {
      setDateError('Từ ngày > Đến ngày');
    } else {
      setDateError('');
    }
  }, [timeRange, fromDate, toDate]);

  // Resolve effective from/to dates based on the selected time range
  const effectiveDates = useMemo(() => {
    if (timeRange === '7d') {
      const from = new Date();
      from.setDate(from.getDate() - 7);
      return { from: toIsoDate(from), to: undefined };
    }
    if (timeRange === '30d') {
      const from = new Date();
      from.setDate(from.getDate() - 30);
      return { from: toIsoDate(from), to: undefined };
    }
    if (timeRange === 'custom') {
      if (dateError) return null;
      return { from: fromDate || undefined, to: toDate || undefined };
    }
    return { from: undefined, to: undefined };
  }, [timeRange, fromDate, toDate, dateError]);

  // Fetch data
  useEffect(() => {
    if (!effectiveDates) return;
    const params: FeedbackFilterParams = {
      q: debouncedSearch || undefined,
      ratingLevel: filterRating || undefined,
      fromDate: effectiveDates.from,
      toDate: effectiveDates.to,
      campusId: isHO && campusFilter ? Number(campusFilter) : undefined,
      page: currentPage,
      pageSize,
    };

    fetchSummaries(params);
  }, [debouncedSearch, filterRating, effectiveDates, campusFilter, isHO, currentPage, pageSize, fetchSummaries]);

  const handleOpenViewSummary = (visitRequestId: number) => {
    navigate(`/dashboard/feedback/${visitRequestId}`);
  };

  const handleReset = () => {
    setSearchQuery('');
    setFilterRating('');
    setTimeRange('');
    setFromDate('');
    setToDate('');
    setDateError('');
    setCampusFilter('');
    setCurrentPage(1);
  };

  // Calculate summary stats from current summaries data (as approximation since no global stats API)
  const stats = useMemo(() => {
    if (!summaries?.items?.length) return { totalDelegations: 0, avgRating: 0, lowRating: 0, latest: null as string | null };

    let totalF = 0;
    let sumR = 0;
    let lowR = 0;
    let latest = '';

    summaries.items.forEach(item => {
      totalF += item.totalFeedbacks;
      sumR += (item.averageRating * item.totalFeedbacks);
      lowR += item.lowRatingCount;
      if (item.latestSubmittedAt && (!latest || new Date(item.latestSubmittedAt) > new Date(latest))) {
        latest = item.latestSubmittedAt;
      }
    });

    return {
      totalDelegations: summaries.totalItems,
      avgRating: totalF > 0 ? (sumR / totalF) : 0,
      lowRating: lowR,
      latest: latest || null
    };
  }, [summaries]);

  return (
    <div className="w-full pb-12 animate-in fade-in duration-500 font-sans">
      {/* Breadcrumbs */}
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý feedback</span>
      </div>

      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-bold text-[#004c91] tracking-tight">Quản lý feedback</h1>
            {user?.roleCode === 'STAFF' && user?.campusName && (
               <span className="px-2.5 py-1 bg-blue-100 text-blue-700 text-xs font-bold rounded-lg border border-blue-200">
                 Campus: {user.campusName}
               </span>
            )}
          </div>
          <p className="text-gray-500 mt-1 font-medium">Tổng hợp và tra cứu đánh giá của các đoàn khách đã hoàn tất</p>
        </div>
      </div>

      <FeedbackSummaryCompact
        totalDelegations={stats.totalDelegations}
        avgRating={stats.avgRating}
        lowRating={stats.lowRating}
        latest={stats.latest}
      />

      <div className="mb-4">
        <FeedbackFilterBar
          searchQuery={searchQuery}
          onSearchQueryChange={(v) => { setSearchQuery(v); setCurrentPage(1); }}
          ratingLevel={filterRating}
          onRatingLevelChange={(v) => { setFilterRating(v); setCurrentPage(1); }}
          timeRange={timeRange}
          onTimeRangeChange={(v) => { setTimeRange(v); setCurrentPage(1); }}
          fromDate={fromDate}
          toDate={toDate}
          onFromDateChange={(v) => { setFromDate(v); setCurrentPage(1); }}
          onToDateChange={(v) => { setToDate(v); setCurrentPage(1); }}
          dateError={dateError}
          campusOptions={isHO ? campusFilterOptions?.campuses : undefined}
          campusFilter={campusFilter}
          onCampusFilterChange={(v) => { setCampusFilter(v); setCurrentPage(1); }}
          onReset={handleReset}
        />
      </div>

      <FeedbackTable
        summaries={summaries}
        loading={loading}
        currentPage={currentPage}
        pageSize={pageSize}
        onView={handleOpenViewSummary}
      />

      {/* Pagination */}
      <div className="p-4 mt-4 border border-slate-200 rounded-2xl flex flex-col md:flex-row items-center justify-between gap-4 bg-slate-50">
        <div className="flex items-center gap-2 text-sm text-slate-500 font-medium">
          <span>Hiển thị</span>
          <select
            value={pageSize}
            onChange={(e) => {
              setPageSize(Number(e.target.value));
              setCurrentPage(1);
            }}
            className="px-2 py-1 bg-white border border-slate-200 rounded-lg outline-none cursor-pointer focus:border-[#004c91] transition-colors"
          >
            <option value={5}>5</option>
            <option value={10}>10</option>
            <option value={20}>20</option>
          </select>
          <span>bản ghi / trang</span>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
            disabled={currentPage === 1}
            className="cursor-pointer p-1 text-slate-500 hover:bg-slate-200 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            <ChevronLeft className="w-5 h-5" />
          </button>
          <div className="text-sm font-bold text-slate-700">
            Trang {currentPage} / {Math.max(1, summaries?.totalPages || 1)}
          </div>
          <button
            onClick={() => setCurrentPage(prev => prev + 1)}
            disabled={currentPage >= (summaries?.totalPages || 1)}
            className="cursor-pointer p-1 text-slate-500 hover:bg-slate-200 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            <ChevronRight className="w-5 h-5" />
          </button>
        </div>
      </div>
    </div>
  );
}
