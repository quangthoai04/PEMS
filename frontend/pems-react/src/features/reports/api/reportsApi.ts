import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type { HoReportFilters, HoReportExportRequest, HoReportOverview } from '../types/reports.types';
import type { StaffLeaderReportFilters, StaffLeaderReportExportRequest, StaffLeaderReportOverview } from '../types/staffLeaderReports.types';
import type {
  DeptLeaderReportFilters,
  DeptLeaderReportExportRequest,
  DeptLeaderReportOverview,
  DeptLeaderInvoiceVisit,
  DeptLeaderInvoiceItem,
} from '../types/deptLeaderReports.types';

/** Chuyển filters UI → query params; bỏ giá trị "ALL"/rỗng để backend hiểu là không lọc. */
function toQueryParams(filters: HoReportFilters): Record<string, string | number> {
  const params: Record<string, string | number> = { preset: filters.preset };
  if (filters.preset === 'CUSTOM') {
    if (filters.fromDate) params.fromDate = filters.fromDate;
    if (filters.toDate) params.toDate = filters.toDate;
  }
  if (filters.campusId) params.campusId = filters.campusId;
  if (filters.visitScope !== 'ALL') params.visitScope = filters.visitScope;
  if (filters.requestStatus !== 'ALL') params.requestStatus = filters.requestStatus;
  if (filters.campusInstanceStatus !== 'ALL') params.campusInstanceStatus = filters.campusInstanceStatus;
  if (filters.visitType !== 'ALL') params.visitType = filters.visitType;
  return params;
}

export interface HoReportExportFile {
  blob: Blob;
  fileName: string;
}

export const reportsApi = {
  getHoReportOverview: async (filters: HoReportFilters): Promise<HoReportOverview> => {
    const { data } = await httpClient.get<HoReportOverview>(API_ENDPOINTS.reports.hoOverview, {
      params: toQueryParams(filters),
    });
    return data;
  },

  exportHoReport: async (request: HoReportExportRequest): Promise<HoReportExportFile> => {
    const response = await httpClient.post(API_ENDPOINTS.reports.hoExport, {
      preset: request.preset,
      fromDate: request.preset === 'CUSTOM' ? request.fromDate : undefined,
      toDate: request.preset === 'CUSTOM' ? request.toDate : undefined,
      campusId: request.campusId,
      visitScope: request.visitScope,
      requestStatus: request.requestStatus,
      campusInstanceStatus: request.campusInstanceStatus,
      visitType: request.visitType,
      exportFormat: request.exportFormat,
      reportSections: request.reportSections,
    }, { responseType: 'blob' });

    // Lấy tên file backend đặt trong Content-Disposition; fallback theo format.
    const disposition: string = response.headers?.['content-disposition'] ?? '';
    const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
    const plainMatch = /filename="?([^";]+)"?/i.exec(disposition);
    const ext = request.exportFormat === 'PDF' ? 'pdf' : request.exportFormat === 'CSV' ? 'csv' : 'xlsx';
    const fileName = utf8Match
      ? decodeURIComponent(utf8Match[1])
      : plainMatch
        ? plainMatch[1]
        : `PEMS_HO_Report.${ext}`;

    return { blob: response.data as Blob, fileName };
  },

  getStaffLeaderReportOverview: async (filters: StaffLeaderReportFilters): Promise<StaffLeaderReportOverview> => {
    const params: Record<string, string | number> = { preset: filters.preset };
    if (filters.preset === 'CUSTOM') {
      if (filters.fromDate) params.fromDate = filters.fromDate;
      if (filters.toDate) params.toDate = filters.toDate;
    }
    if (filters.visitStatus !== 'ALL') params.visitStatus = filters.visitStatus;
    if (filters.requestStatus !== 'ALL') params.requestStatus = filters.requestStatus;
    if (filters.hostUserId !== 'ALL') params.hostUserId = filters.hostUserId;
    if (filters.departmentId !== 'ALL') params.departmentId = filters.departmentId;
    if (filters.logisticsStatus !== 'ALL') params.logisticsStatus = filters.logisticsStatus;
    if (filters.feedbackRating !== 'ALL') params.feedbackRating = filters.feedbackRating;

    const { data } = await httpClient.get<StaffLeaderReportOverview>('/reports/staff-leader-overview', { params });
    return data;
  },

  exportStaffLeaderReport: async (request: StaffLeaderReportExportRequest): Promise<HoReportExportFile> => {
    const response = await httpClient.post('/reports/staff-leader-overview/export', {
      preset: request.preset,
      fromDate: request.preset === 'CUSTOM' ? request.fromDate : undefined,
      toDate: request.preset === 'CUSTOM' ? request.toDate : undefined,
      visitStatus: request.visitStatus,
      requestStatus: request.requestStatus,
      hostUserId: request.hostUserId,
      departmentId: request.departmentId,
      logisticsStatus: request.logisticsStatus,
      feedbackRating: request.feedbackRating,
      exportFormat: request.exportFormat,
      reportSections: request.reportSections,
    }, { responseType: 'blob' });

    const disposition: string = response.headers?.['content-disposition'] ?? '';
    const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
    const plainMatch = /filename="?([^";]+)"?/i.exec(disposition);
    const ext = request.exportFormat === 'PDF' ? 'pdf' : request.exportFormat === 'CSV' ? 'csv' : 'xlsx';
    const fileName = utf8Match
      ? decodeURIComponent(utf8Match[1])
      : plainMatch
        ? plainMatch[1]
        : `PEMS_StaffLeader_Report.${ext}`;

    return { blob: response.data as Blob, fileName };
  },

  // ────────────────────── Department Leader ──────────────────────

  getDeptLeaderReportOverview: async (filters: DeptLeaderReportFilters): Promise<DeptLeaderReportOverview> => {
    const params: Record<string, string | number> = { preset: filters.preset };
    if (filters.preset === 'CUSTOM') {
      if (filters.fromDate) params.fromDate = filters.fromDate;
      if (filters.toDate) params.toDate = filters.toDate;
    }
    if (filters.logisticsStatus !== 'ALL') params.logisticsStatus = filters.logisticsStatus;
    if (filters.itemType !== 'ALL') params.itemType = filters.itemType;
    if (filters.priority !== 'ALL') params.priority = filters.priority;
    if (filters.assignedUserId !== 'ALL') params.assignedUserId = filters.assignedUserId;
    if (filters.dueStatus !== 'ALL') params.dueStatus = filters.dueStatus;
    if (filters.handoverStatus !== 'ALL') params.handoverStatus = filters.handoverStatus;
    if (filters.feedbackRating !== 'ALL') params.feedbackRating = filters.feedbackRating;

    const { data } = await httpClient.get<DeptLeaderReportOverview>('/reports/department-leader-overview', { params });
    return data;
  },

  exportDeptLeaderReport: async (request: DeptLeaderReportExportRequest): Promise<HoReportExportFile> => {
    const response = await httpClient.post('/reports/department-leader-overview/export', {
      preset: request.preset,
      fromDate: request.preset === 'CUSTOM' ? request.fromDate : undefined,
      toDate: request.preset === 'CUSTOM' ? request.toDate : undefined,
      logisticsStatus: request.logisticsStatus,
      itemType: request.itemType,
      priority: request.priority,
      assignedUserId: request.assignedUserId,
      dueStatus: request.dueStatus,
      handoverStatus: request.handoverStatus,
      feedbackRating: request.feedbackRating,
      exportFormat: request.exportFormat,
      reportSections: request.reportSections,
    }, { responseType: 'blob' });

    const disposition: string = response.headers?.['content-disposition'] ?? '';
    const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
    const plainMatch = /filename="?([^";]+)"?/i.exec(disposition);
    const ext = request.exportFormat === 'PDF' ? 'pdf' : request.exportFormat === 'CSV' ? 'csv' : 'xlsx';
    const fileName = utf8Match
      ? decodeURIComponent(utf8Match[1])
      : plainMatch
        ? plainMatch[1]
        : `PEMS_DeptLeader_Report.${ext}`;

    return { blob: response.data as Blob, fileName };
  },

  /** Lấy danh sách visit trong scope department để chọn cho tab Hóa đơn. */
  getDeptLeaderInvoiceVisits: async (filters: Pick<DeptLeaderReportFilters, 'preset' | 'fromDate' | 'toDate'>): Promise<DeptLeaderInvoiceVisit[]> => {
    const params: Record<string, string> = { preset: filters.preset };
    if (filters.preset === 'CUSTOM') {
      if (filters.fromDate) params.fromDate = filters.fromDate;
      if (filters.toDate) params.toDate = filters.toDate;
    }
    const { data } = await httpClient.get<DeptLeaderInvoiceVisit[]>('/reports/department-leader-invoice/visits', { params });
    return data;
  },

  /** Lấy danh sách logistics items host yêu cầu phòng ban chuẩn bị cho một visit. */
  getDeptLeaderInvoiceItems: async (visitInstanceId: number): Promise<DeptLeaderInvoiceItem[]> => {
    const { data } = await httpClient.get<DeptLeaderInvoiceItem[]>(`/reports/department-leader-invoice/visits/${visitInstanceId}/items`);
    return data;
  },

  /** Gọi backend generate + download PDF hóa đơn. */
  exportDeptLeaderInvoicePdf: async (payload: {
    visitInstanceId: number;
    invoiceTitle: string;
    invoiceNote: string;
    items: { logisticsItemId: number; itemName: string; itemType: string; quantity: number; unit: string | null; unitPrice: number; note: string }[];
  }): Promise<HoReportExportFile> => {
    const response = await httpClient.post('/reports/department-leader-invoice/export-pdf', payload, { responseType: 'blob' });
    const disposition: string = response.headers?.['content-disposition'] ?? '';
    const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
    const plainMatch = /filename="?([^";]+)"?/i.exec(disposition);
    const fileName = utf8Match
      ? decodeURIComponent(utf8Match[1])
      : plainMatch
        ? plainMatch[1]
        : `PEMS_Department_Invoice.pdf`;
    return { blob: response.data as Blob, fileName };
  },
};
