import apiClient from '../shared/api/httpClient';

export interface VisitExpenseItem {
  expenseItemId: number;
  itemOrigin: 'REQUEST_ITEM' | 'MANUAL' | 'ADDITIONAL' | 'DAMAGE_LOSS' | 'OTHER';
  itemName: string;
  description: string | null;
  quantity: number;
  unitName: string | null;
  unitPrice: number;
  totalAmount: number;
  itemNote: string | null;
  displayOrder: number;
  rowVersion: number;
}

export interface VisitExpenseReport {
  expenseReportId: number;
  visitInstanceId: number;
  reportScope: 'GENERAL' | 'LOGISTICS';
  logisticsItemId: number | null;
  departmentId: number | null;
  status: 'DRAFT' | 'SAVED' | 'FINALIZED' | 'CANCELLED';
  reportNote: string | null;
  noExpense: boolean;
  currencyCode: string;
  rowVersion: number;
  createdAt: string;
  totalAmount: number;
  // Chỉ có trong summary (host view)
  departmentName?: string | null;
  logisticsItemTitle?: string | null;
  items: VisitExpenseItem[];
}

export interface VisitInstanceExpenseSummary {
  visitInstanceId: number;
  totalAmount: number;
  generalReport: VisitExpenseReport | null;
  logisticsReports: VisitExpenseReport[];
}

export interface SaveExpenseItemDto {
  expenseItemId?: number;
  itemOrigin: 'REQUEST_ITEM' | 'MANUAL' | 'ADDITIONAL' | 'DAMAGE_LOSS' | 'OTHER';
  itemName: string;
  description?: string | null;
  quantity: number;
  unitName?: string | null;
  unitPrice: number;
  itemNote?: string | null;
  displayOrder: number;
}

export interface SaveExpenseReportCommand {
  reportNote?: string | null;
  rowVersion: number;
  /** true = xác nhận "Không có chi phí" cho báo cáo này */
  noExpense?: boolean;
  items: SaveExpenseItemDto[];
}

export interface RemindExpenseReportsResult {
  remindedCount: number;
  recipients: string[];
}

const visitExpenseService = {
  /**
   * Reads the general report. A READ — it never creates one, so a campus that has not filed costs
   * answers 204 and this resolves to null. Callers render an empty state; they must not treat the
   * absence of a report as a failure.
   */
  getGeneralExpenseReport: async (visitInstanceId: number): Promise<VisitExpenseReport | null> => {
    const res = await apiClient.get<VisitExpenseReport | ''>(`/VisitExpenses/general/${visitInstanceId}`);
    return res.status === 204 || !res.data ? null : (res.data as VisitExpenseReport);
  },

  /** Opens the general report. Host only, AFTER_VISIT only, idempotent on the server. */
  initializeGeneralExpenseReport: async (visitInstanceId: number): Promise<VisitExpenseReport> => {
    const res = await apiClient.post<VisitExpenseReport>(`/VisitExpenses/general/${visitInstanceId}/initialize`);
    return res.data;
  },

  getLogisticsExpenseReport: async (logisticsItemId: number): Promise<VisitExpenseReport> => {
    const res = await apiClient.get<VisitExpenseReport>(`/VisitExpenses/logistics/${logisticsItemId}`);
    return res.data;
  },

  saveExpenseReport: async (expenseReportId: number, data: SaveExpenseReportCommand): Promise<VisitExpenseReport> => {
    const res = await apiClient.put<VisitExpenseReport>(`/VisitExpenses/${expenseReportId}`, data);
    return res.data;
  },

  getExpenseSummary: async (visitInstanceId: number): Promise<VisitInstanceExpenseSummary> => {
    const res = await apiClient.get<VisitInstanceExpenseSummary>(`/VisitExpenses/summary/${visitInstanceId}`);
    return res.data;
  },

  remindExpenseReports: async (visitInstanceId: number): Promise<RemindExpenseReportsResult> => {
    const res = await apiClient.post<RemindExpenseReportsResult>(`/VisitExpenses/remind/${visitInstanceId}`);
    return res.data;
  }
};

export default visitExpenseService;
