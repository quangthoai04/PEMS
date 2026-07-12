/**
 * Trang DocumentManagement
 * Kho lưu trữ số các loại quy định mẫu, biên nhận biểu mẫu, tải về cho nhân sự.
 */

import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDocuments } from '../../../features/documents/hooks/useDocuments';
import { useAuth } from '../../../shared/hooks/useAuth';
import { useCampusFilterOptions } from '../../../features/campus-management/hooks/useCampusManagement';
import { DocumentOwnerType, DocumentStatus, StorageProvider, DocumentListItem } from '../../../features/documents/types/documents.types';
import { DocumentDetailModal } from './DocumentDetailModal';
import { DocumentSummaryCompact } from '../../../features/documents/components/DocumentSummaryCompact';
import { DocumentFilterBar } from '../../../features/documents/components/DocumentFilterBar';
import { DocumentTable } from '../../../features/documents/components/DocumentTable';

export function DocumentManagement() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const isHO = user?.roleCode === 'HO';
  const campusFilterOptions = useCampusFilterOptions();

  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  const [statusFilter, setStatusFilter] = useState<DocumentStatus | 'All'>('All');
  const [ownerTypeFilter, setOwnerTypeFilter] = useState<DocumentOwnerType | 'All'>('All');
  const [documentCategoryFilter] = useState('All');
  const [storageProviderFilter] = useState<StorageProvider | 'All'>('All');
  const [campusFilter, setCampusFilter] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [sortBy] = useState('uploaded_at');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');

  // Modal State
  const [selectedDoc, setSelectedDoc] = useState<DocumentListItem | null>(null);

  // React Query hook
  const { data: documentData, isLoading, isError } = useDocuments({
    q: debouncedSearch || undefined,
    status: statusFilter,
    ownerType: ownerTypeFilter,
    documentCategory: documentCategoryFilter !== 'All' ? documentCategoryFilter : undefined,
    storageProvider: storageProviderFilter,
    uploadedFrom: dateFrom || undefined,
    uploadedTo: dateTo || undefined,
    campusId: isHO && campusFilter ? Number(campusFilter) : undefined,
    page,
    pageSize,
    sortBy,
    sortDir
  });

  // Handle Search Debounce
  React.useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedSearch(searchQuery);
      setPage(1); // reset to page 1 on search
    }, 500);
    return () => clearTimeout(handler);
  }, [searchQuery]);

  const handleSortToggle = () => {
    setSortDir(prev => prev === 'desc' ? 'asc' : 'desc');
  };

  const handleResetFilters = () => {
    setSearchQuery('');
    setDebouncedSearch('');
    setStatusFilter('All');
    setOwnerTypeFilter('All');
    setCampusFilter('');
    setDateFrom('');
    setDateTo('');
    setPage(1);
  };

  const docs = documentData?.items || [];
  const totalCount = documentData?.totalCount || 0;
  const totalPages = documentData?.totalPages || 1;

  // Summaries calculation on current page for demo - if backend returns stats, use them instead.
  const summary = {
    total: totalCount,
    draft: docs.filter(d => d.status === 'DRAFT').length,
    published: docs.filter(d => d.status === 'PUBLISHED').length,
    archived: docs.filter(d => d.status === 'ARCHIVED').length,
  };

  return (
    <div className="w-full pb-12 flex flex-col space-y-4 animate-in fade-in duration-300">
      {/* 1. Header & Navigation Layer */}
      <div className="mb-2 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Quản lý tài liệu</span>
      </div>

      <div className="border-b border-gray-100 pb-4 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-bold text-[#004c91]">Quản lý tài liệu</h1>
            <span className="inline-flex px-2.5 py-1 text-xs font-semibold rounded-md bg-blue-100 text-blue-800 border border-blue-200 shadow-sm">
              Campus: {user?.campusName || 'Hệ thống'}
            </span>
          </div>
          <p className="text-gray-500 mt-2 font-medium">
            Tra cứu, xem và tải xuống tài liệu nghiệp vụ của campus {user?.campusName || 'Hệ thống'} được lưu trên Google Drive
          </p>
        </div>
      </div>

      {/* Summary compact */}
      <DocumentSummaryCompact
        total={summary.total}
        draft={summary.draft}
        published={summary.published}
        archived={summary.archived}
      />

      {/* 2. Filter Bar */}
      <DocumentFilterBar
        searchQuery={searchQuery}
        onSearchQueryChange={setSearchQuery}
        ownerTypeFilter={ownerTypeFilter}
        onOwnerTypeFilterChange={setOwnerTypeFilter}
        statusFilter={statusFilter}
        onStatusFilterChange={setStatusFilter}
        dateFrom={dateFrom}
        onDateFromChange={setDateFrom}
        dateTo={dateTo}
        onDateToChange={setDateTo}
        campusOptions={isHO ? campusFilterOptions?.campuses : undefined}
        campusFilter={campusFilter}
        onCampusFilterChange={setCampusFilter}
        onReset={handleResetFilters}
      />

      {/* 3. Data View */}
      <DocumentTable
        docs={docs}
        isLoading={isLoading}
        isError={isError}
        searchQuery={searchQuery}
        statusFilterActive={statusFilter !== 'All'}
        sortDir={sortDir}
        onSortToggle={handleSortToggle}
        onView={setSelectedDoc}
        page={page}
        pageSize={pageSize}
        totalPages={totalPages}
        onPageChange={setPage}
        onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
      />

      {/* Modal Xem chi tiết có Preview */}
      <DocumentDetailModal
        documentId={selectedDoc ? selectedDoc.documentId : null}
        onClose={() => setSelectedDoc(null)}
      />
    </div>
  );
}
